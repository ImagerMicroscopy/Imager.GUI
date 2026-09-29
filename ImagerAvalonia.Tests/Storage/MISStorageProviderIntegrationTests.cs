using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Services.MeasurementControl;
using Xunit;

namespace ImagerAvalonia.Tests.Storage;

/// <summary>
/// MISStorageProvider against the real native library (Imager.TiffLib). Each test writes a
/// storage file into its own temp folder, closes it, reopens it and reads it back exactly
/// the way ImagerWorkspace / ImageHandler do.
/// </summary>
[Collection(NativeStorageCollection.Name)]
public sealed class MISStorageProviderIntegrationTests : IDisposable
{
    private const int Mono16 = 0;

    private readonly string _dir = Directory.CreateTempSubdirectory("mis-tests-").FullName;
    private readonly List<MISStorageProvider> _open = new();

    public void Dispose()
    {
        foreach (var provider in _open)
        {
            try { provider.CloseReadWriteStream(); } catch { }
        }
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string PathFor(string name) => Path.Combine(_dir, name);

    private MISStorageProvider NewProvider(string file, string program = """{"apiversion":"2.0"}""",
        params (string Acq, string Det)[] pairs)
    {
        var provider = new MISStorageProvider();
        _open.Add(provider);
        provider.SetEnabledStorage(true);
        provider.SetStoragePath(file);
        provider.SetMeasurementProgram(program);
        provider.SetAcqDetPairs((pairs.Length == 0 ? new (string Acq, string Det)[] { ("Acq1", "DummyCam1") } : pairs)
            .Select(p => Tuple.Create(p.Acq, p.Det)).ToList());
        return provider;
    }

    /// <summary>A Mono16 image whose pixel at (row, col) is base + row * 100 + col.</summary>
    private static byte[] Image16(int rows, int cols, ushort @base = 0)
    {
        var pixels = new ushort[rows * cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                pixels[r * cols + c] = (ushort)(@base + r * 100 + c);
        var bytes = new byte[pixels.Length * 2];
        Buffer.BlockCopy(pixels, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static TiffPlaneMetadata Meta(int rows, int cols, int detectionIndex, double time = 0,
        string acq = "Acq1", string det = "DummyCam1", string position = "pos1",
        double x = 1, double y = 2, double z = 3) => new()
    {
        AcquisitionName = acq,
        DetectorName = det,
        Width = (uint)cols,
        Height = (uint)rows,
        Type = Mono16,
        DetectionIndex = detectionIndex,
        TimePoint = time,
        PositionName = position,
        PositionX = x, PositionY = y, PositionZ = z,
    };

    /// <summary>Writes the given planes, closes, and reopens the same provider for reading.</summary>
    private MISStorageProvider WriteAndReopen(string file, IEnumerable<(byte[] Data, TiffPlaneMetadata Meta)> planes,
        IEnumerable<string>? decisions = null, string program = """{"apiversion":"2.0"}""",
        params (string Acq, string Det)[] pairs)
    {
        var provider = NewProvider(file, program, pairs);
        provider.OpenWriteStream();

        var list = planes.ToList();
        provider.SavePlanes(list.Select(p => p.Data).ToList(), list.Select(p => p.Meta).ToList());
        if (decisions != null)
            provider.SaveDecisions(decisions.ToList());

        provider.CloseReadWriteStream();
        provider.OpenReadStream();
        return provider;
    }

    // ---------- Basic round trip ----------

    [NativeStorageFact]
    public void Written_file_exists_after_close()
    {
        var file = PathFor("exists.tif");

        WriteAndReopen(file, new[] { (Image16(2, 3), Meta(2, 3, 0)) });

        Assert.True(File.Exists(file));
        Assert.True(new FileInfo(file).Length > 0);
    }

    [NativeStorageFact]
    public void Image_pixels_round_trip()
    {
        var image = Image16(4, 4, 1000);
        var provider = WriteAndReopen(PathFor("pixels.tif"), new[] { (image, Meta(4, 4, 0)) });

        Assert.Equal(1, provider.GetNumberOfImages("Acq1", "DummyCam1"));
        Assert.Equal(image, provider.ReadPlane("Acq1", "DummyCam1", 0));
    }

    [NativeStorageFact]
    public void Non_square_image_keeps_its_width_and_height()
    {
        // 2 rows x 5 columns.
        var image = Image16(2, 5);
        var provider = WriteAndReopen(PathFor("nonsquare.tif"), new[] { (image, Meta(2, 5, 0)) });

        var read = provider.ReadPlane("Acq1", "DummyCam1", 0);

        Assert.Equal(image, read);
        Assert.Equal(new List<uint> { 5, 2 }, provider.GetPlaneSize()); // [width, height]
        var meta = provider.GetPlaneMetadata("Acq1", "DummyCam1", 0);
        Assert.Equal(5u, meta.Width);
        Assert.Equal(2u, meta.Height);
    }

    [NativeStorageFact]
    public void Non_square_image_is_written_with_the_right_dimensions_in_the_file()
    {
        // Checked with an independent TIFF reader: what Fiji / Igor will see.
        var file = PathFor("nonsquare-header.tif");
        WriteAndReopen(file, new[] { (Image16(2, 5), Meta(2, 5, 0)) });

        var size = Assert.Single(TiffHeader.ReadImageSizes(file));
        Assert.Equal(5ul, size.Width);
        Assert.Equal(2ul, size.Length);
    }

    [NativeStorageFact]
    public void Plane_metadata_round_trips()
    {
        var provider = WriteAndReopen(PathFor("meta.tif"), new[]
        {
            (Image16(2, 2), Meta(2, 2, 0, time: 0.5, position: "well A1", x: 10.5, y: -3.25, z: 7)),
            (Image16(2, 2), Meta(2, 2, 1, time: 1.5, position: "well B2", x: 11, y: -4, z: 8)),
        });

        var second = provider.GetPlaneMetadata("Acq1", "DummyCam1", 1);

        Assert.Equal("Acq1", second.AcquisitionName);
        Assert.Equal("DummyCam1", second.DetectorName);
        Assert.Equal("well B2", second.PositionName);
        Assert.Equal((11.0, -4.0, 8.0), (second.PositionX, second.PositionY, second.PositionZ));
        Assert.Equal(1.5, second.TimePoint);
        Assert.Equal(1, second.DetectionIndex);
        Assert.Equal("well B2", second.CurrentStagePosition.Name);
    }

    [NativeStorageFact]
    public void Several_channels_are_stored_separately()
    {
        var provider = WriteAndReopen(PathFor("channels.tif"), new[]
            {
                (Image16(2, 2, 100), Meta(2, 2, 0, acq: "Acq1", det: "DummyCam1")),
                (Image16(2, 2, 200), Meta(2, 2, 0, acq: "Acq1", det: "DummyCam2")),
                (Image16(2, 2, 300), Meta(2, 2, 1, acq: "Acq2", det: "DummyCam1")),
                (Image16(2, 2, 400), Meta(2, 2, 2, acq: "Acq1", det: "DummyCam1")),
            },
            pairs: new[] { ("Acq1", "DummyCam1"), ("Acq1", "DummyCam2"), ("Acq2", "DummyCam1") });

        Assert.Equal(2, provider.GetNumberOfImages("Acq1", "DummyCam1"));
        Assert.Equal(1, provider.GetNumberOfImages("Acq1", "DummyCam2"));
        Assert.Equal(1, provider.GetNumberOfImages("Acq2", "DummyCam1"));

        Assert.Equal(Image16(2, 2, 200), provider.ReadPlane("Acq1", "DummyCam2", 0));
        Assert.Equal(Image16(2, 2, 400), provider.ReadPlane("Acq1", "DummyCam1", 1));
    }

    [NativeStorageFact]
    public void Image_index_is_the_latest_image_at_or_before_a_detection_index()
    {
        var provider = WriteAndReopen(PathFor("index.tif"), new[]
            {
                (Image16(2, 2), Meta(2, 2, 0, acq: "Acq1")),
                (Image16(2, 2), Meta(2, 2, 1, acq: "Acq2")),
                (Image16(2, 2), Meta(2, 2, 2, acq: "Acq1")),
            },
            pairs: new[] { ("Acq1", "DummyCam1"), ("Acq2", "DummyCam1") });

        Assert.Equal(0, provider.GetImageIndex("Acq1", "DummyCam1", 0));
        Assert.Equal(0, provider.GetImageIndex("Acq1", "DummyCam1", 1));
        Assert.Equal(1, provider.GetImageIndex("Acq1", "DummyCam1", 2));
        Assert.Equal(-1, provider.GetImageIndex("Acq2", "DummyCam1", 0));
    }

    [NativeStorageFact]
    public void Number_of_detections_is_read_back()
    {
        var provider = WriteAndReopen(PathFor("detections.tif"),
            Enumerable.Range(0, 5).Select(i => (Image16(2, 2), Meta(2, 2, i))));

        Assert.Equal(5, provider.LoadMaxFrameNumber());
    }

    [NativeStorageFact]
    public void Imager_program_is_stored_and_read_back()
    {
        var program = FullProgramJson();

        var provider = WriteAndReopen(PathFor("program.tif"), new[] { (Image16(2, 2), Meta(2, 2, 0)) }, program: program);
        var stored = provider.GetImagerProgram();

        // The program is kept inside the file's OME-XML, and XML parsing normalises line
        // endings, so on Windows the indented JSON (\r\n) comes back with \n. Compare the JSON.
        Assert.NotNull(stored);
        Assert.True(Newtonsoft.Json.Linq.JToken.DeepEquals(
                Newtonsoft.Json.Linq.JToken.Parse(program), Newtonsoft.Json.Linq.JToken.Parse(stored!)),
            "Stored imager program differs from what was written.");
        Assert.Equal(program.Replace("\r\n", "\n"), stored!.Replace("\r\n", "\n"));
    }

    [NativeStorageFact]
    public void Stored_program_can_be_loaded_as_a_project()
    {
        // What a saved measurement file is later opened with: the stored program must still
        // be a loadable .imag project.
        var provider = WriteAndReopen(PathFor("program-load.tif"), new[] { (Image16(2, 2), Meta(2, 2, 0)) },
            program: FullProgramJson());

        var state = ImagerAvalonia.Services.Workspace.FullEquipmentStateSerializer.Deserialize(provider.GetImagerProgram()!);

        Assert.Equal(70, state.CurrentProgram.Program.CountTotalDetections());
        Assert.Equal(new[] { "Acq1" }, state.CurrentProgram.Detections.Keys);
    }

    // ---------- Smart program decisions ----------

    [NativeStorageFact]
    public void Smart_program_decisions_are_stored_and_read_back()
    {
        var decisions = new[]
        {
            """{"decision":"stop","programid":"p1","timestamp":3.5}""",
            """{"decision":"continue","programid":"p1","timestamp":4.0}""",
        };

        var provider = WriteAndReopen(PathFor("decisions.tif"), new[] { (Image16(2, 2), Meta(2, 2, 0)) }, decisions);

        var read = provider.GetSmartProgramDecisions();
        Assert.Equal(2, read.Count);
        Assert.Equal("stop", Newtonsoft.Json.Linq.JObject.Parse(read[0])["decision"]!.ToString());
        Assert.Equal(4.0, (double)Newtonsoft.Json.Linq.JObject.Parse(read[1])["timestamp"]!);
    }

    [NativeStorageFact]
    public void File_without_decisions_reads_back_an_empty_list()
    {
        var provider = WriteAndReopen(PathFor("nodecisions.tif"), new[] { (Image16(2, 2), Meta(2, 2, 0)) });

        Assert.Empty(provider.GetSmartProgramDecisions());
    }

    [NativeStorageFact]
    public void Decision_that_is_not_json_is_skipped_instead_of_breaking_the_file()
    {
        var provider = WriteAndReopen(PathFor("baddecision.tif"), new[] { (Image16(2, 2), Meta(2, 2, 0)) },
            new[] { "not json at all", """{"decision":"stop"}""" });

        Assert.Equal(1, provider.GetNumberOfImages("Acq1", "DummyCam1"));
        Assert.Equal("stop", Newtonsoft.Json.Linq.JObject.Parse(Assert.Single(provider.GetSmartProgramDecisions()))["decision"]!.ToString());
    }

    // ---------- Disabled storage / errors ----------

    [NativeStorageFact]
    public void Disabled_storage_writes_nothing()
    {
        var file = PathFor("disabled.tif");
        var provider = NewProvider(file);
        provider.SetEnabledStorage(false);

        provider.OpenWriteStream();
        provider.SavePlanes(new List<byte[]> { Image16(2, 2) }, new List<TiffPlaneMetadata> { Meta(2, 2, 0) });
        provider.SaveDecisions(new List<string> { """{"decision":"stop"}""" });
        provider.CloseReadWriteStream();

        Assert.False(File.Exists(file));
    }

    [NativeStorageFact]
    public void Creating_storage_in_a_missing_folder_fails_loudly()
    {
        var provider = NewProvider(Path.Combine(_dir, "no", "such", "folder", "x.tif"));

        Assert.ThrowsAny<IOException>(() => provider.OpenWriteStream());
    }

    [NativeStorageFact]
    public void Opening_a_missing_file_fails_loudly()
    {
        var provider = NewProvider(PathFor("missing.tif"));

        Assert.ThrowsAny<IOException>(() => provider.OpenReadStream());
    }

    [NativeStorageFact]
    public void Reading_an_image_that_does_not_exist_returns_no_image()
    {
        var provider = WriteAndReopen(PathFor("oob.tif"), new[] { (Image16(2, 2), Meta(2, 2, 0)) });

        // Used to carry on with an uninitialised pointer when the library reported an error.
        Assert.Empty(provider.ReadPlane("Acq1", "DummyCam1", 5));
        Assert.Empty(provider.ReadPlane("Nope", "DummyCam1", 0));
        Assert.Equal(0, provider.GetNumberOfImages("Nope", "DummyCam1"));
        Assert.Equal(-1, provider.GetImageIndex("Nope", "DummyCam1", 0));

        // The provider is still usable afterwards.
        Assert.Equal(Image16(2, 2), provider.ReadPlane("Acq1", "DummyCam1", 0));
    }

    [NativeStorageFact]
    public void Reading_with_time_position_minus_one_returns_no_image()
    {
        var provider = WriteAndReopen(PathFor("minus1.tif"), new[] { (Image16(2, 2), Meta(2, 2, 0)) });

        Assert.Empty(provider.ReadPlane("Acq1", "DummyCam1", -1));
    }

    [NativeStorageFact]
    public void Many_reads_do_not_leak_images_in_flight()
    {
        var image = Image16(8, 8, 7);
        var provider = WriteAndReopen(PathFor("many.tif"), new[] { (image, Meta(8, 8, 0)) });

        for (int i = 0; i < 500; i++)
            Assert.Equal(image, provider.ReadPlane("Acq1", "DummyCam1", 0));
    }

    private static string FullProgramJson()
    {
        var state = new ImagerAvalonia.Services.Workspace.FullEquipmentState
        {
            CurrentEquipment = TestData.EquipmentFixtures.Workspace(),
            CurrentProgram = new ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels.MeasurementProgram(
                Serialization.ImagProjectSerializationTests.SampleProgram(),
                new Dictionary<string, ImagerAvalonia.Services.MeasurementControl.DetectionParams>
                {
                    ["Acq1"] = TestData.EquipmentFixtures.Detection(),
                }),
        };
        return ImagerAvalonia.Services.Workspace.FullEquipmentStateSerializer.Serialize(state);
    }
}

/// <summary>The native library keeps global state (storer ids, images in flight); run these one at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NativeStorageCollection
{
    public const string Name = "Native MIS storage";
}
