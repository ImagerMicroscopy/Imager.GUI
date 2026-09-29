using System.Collections.Concurrent;
using System.Threading.Channels;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ImagerAvalonia.Tests.Communication;

/// <summary>
/// Records every request and answers from a per-request-type script.
/// </summary>
internal sealed class ScriptedConnectionHandler : IImagerConnectionHandler
{
    private readonly Func<ImagerRequest, ImagerResponse> _respond;

    public ConcurrentQueue<ImagerRequest> Requests { get; } = new();

    public ScriptedConnectionHandler(Func<ImagerRequest, ImagerResponse> respond) => _respond = respond;

    public Task<ImagerResponse> SendRequestAsync(ImagerRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Enqueue(request);
        return Task.FromResult(_respond(request));
    }

    public IEnumerable<T> RequestsOf<T>() where T : ImagerRequest => Requests.OfType<T>();
}

public class ImagerCommunicationManagerTests
{
    private static ImagerCommunicationManager CreateManager(Func<ImagerRequest, ImagerResponse> respond, out ScriptedConnectionHandler handler)
    {
        handler = new ScriptedConnectionHandler(respond);
        return new ImagerCommunicationManager(handler);
    }

    private static JToken PropertiesOf(string responseJson) => JObject.Parse(responseJson)["detectorproperties"]!;

    // ---------- Ping ----------

    [Fact]
    public async Task Ping_succeeds_on_pong()
    {
        var manager = CreateManager(_ => new PongResponse(), out var handler);

        await manager.PingAsync();

        Assert.IsType<PingRequest>(Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task Ping_throws_with_backend_error()
    {
        var manager = CreateManager(_ => new StatusErrorResponse("down"), out _);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.PingAsync());

        Assert.Contains("down", ex.Message);
    }

    [Fact]
    public async Task Ping_throws_on_unexpected_response_type()
    {
        var manager = CreateManager(_ => new StatusOkResponse(), out _);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.PingAsync());

        Assert.Contains(nameof(StatusOkResponse), ex.Message);
    }

    [Fact]
    public async Task Transport_exceptions_are_wrapped()
    {
        var manager = CreateManager(_ => throw new System.Net.Sockets.SocketException(), out _);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.PingAsync());

        Assert.StartsWith("Ping failed: Exception:", ex.Message);
    }

    [Fact]
    public async Task Invalid_query_from_backend_surfaces_as_unexpected_response()
    {
        // Imager.Core's "invalidquery: ..." reply has no "status" field, so the manager
        // reports it as an unexpected UnknownJsonResponse rather than a backend error.
        var manager = CreateManager(
            _ => new UnknownJsonResponse(ImagerCoreWire.InvalidQuery("expected String, but encountered Null")),
            out _);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.SetDetectorPropertyAsync("cam", new NumericDetectorProperty("x", 0, 1)));

        Assert.Contains(nameof(UnknownJsonResponse), ex.Message);
    }

    // ---------- Detectors ----------

    [Fact]
    public async Task List_available_detectors_fetches_properties_for_each_detector()
    {
        var manager = CreateManager(req => req switch
        {
            ListAvailableDetectorsRequest => new AvailableDetectorsResponse(new[] { "DummyCam1", "DummyCam2" }),
            GetDetectorPropertiesRequest { DetectorName: "DummyCam1" } =>
                new DetectorPropertiesResponse(PropertiesOf(ImagerCoreWire.DummyCam1Properties), 20.0),
            GetDetectorPropertiesRequest { DetectorName: "DummyCam2" } =>
                new DetectorPropertiesResponse(PropertiesOf(ImagerCoreWire.DummyCam2Properties), 200.0),
            _ => throw new InvalidOperationException($"Unexpected {req.Action}")
        }, out var handler);

        var detectors = await manager.ListAvailableDetectorsAsync();

        Assert.Equal(new[] { "DummyCam1", "DummyCam2" }, detectors.Select(d => d.Detectorname));
        Assert.Equal(new[] { 20.0, 200.0 }, detectors.Select(d => d.Framerate));
        Assert.Equal(new[] { "DummyCam1", "DummyCam2" },
            handler.RequestsOf<GetDetectorPropertiesRequest>().Select(r => r.DetectorName));

        var cam1 = detectors[0].DetectorProperties;
        Assert.Equal(3, cam1.Count);

        var exposure = Assert.IsType<NumericDetectorProperty>(cam1[0]);
        Assert.Equal("Exposure time", exposure.descriptor);
        Assert.Equal(0.1, exposure.value);

        var cropping = Assert.IsType<CategoricDetectorProperty>(cam1[1]);
        Assert.Equal(1, cropping.propertycode);
        Assert.Equal("64x64", cropping.current);
        Assert.Equal(new[] { "16x16", "32x32", "64x64", "128x128" }, cropping.availableoptions);

        var cam2Binning = Assert.IsType<CategoricDetectorProperty>(detectors[1].DetectorProperties[1]);
        Assert.Equal("2", cam2Binning.current);
    }

    [Fact]
    public async Task Every_categoric_property_from_backend_has_a_non_null_current()
    {
        var manager = CreateManager(req => req switch
        {
            ListAvailableDetectorsRequest => new AvailableDetectorsResponse(new[] { "DummyCam1" }),
            _ => new DetectorPropertiesResponse(PropertiesOf(ImagerCoreWire.DummyCam1Properties), 20.0),
        }, out _);

        var detectors = await manager.ListAvailableDetectorsAsync();

        Assert.All(detectors.SelectMany(d => d.DetectorProperties).OfType<CategoricDetectorProperty>(),
            p => Assert.False(string.IsNullOrEmpty(p.current)));
    }

    [Fact]
    public async Task List_available_detectors_with_no_detectors_returns_empty()
    {
        var manager = CreateManager(_ => new AvailableDetectorsResponse(Array.Empty<string>()), out var handler);

        Assert.Empty(await manager.ListAvailableDetectorsAsync());
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task List_available_detectors_throws_when_properties_request_fails()
    {
        var manager = CreateManager(req => req switch
        {
            ListAvailableDetectorsRequest => new AvailableDetectorsResponse(new[] { "DummyCam1" }),
            _ => new StatusErrorResponse("no such detector"),
        }, out _);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ListAvailableDetectorsAsync());

        Assert.Equal("no such detector", ex.Message);
    }

    [Fact]
    public async Task Set_detector_property_sends_the_property_object()
    {
        var manager = CreateManager(_ => new StatusOkResponse(), out var handler);
        var property = new CategoricDetectorProperty("Binning", 2, "4", new List<string> { "1", "2", "4" });

        await manager.SetDetectorPropertyAsync("DummyCam1", property);

        var request = Assert.IsType<SetDetectorPropertyRequest>(Assert.Single(handler.Requests));
        Assert.Equal("DummyCam1", request.DetectorName);
        Assert.Same(property, request.PropertyValue);
    }

    [Fact]
    public async Task Set_detector_property_throws_on_backend_error()
    {
        var manager = CreateManager(_ => new StatusErrorResponse("out of range"), out _);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.SetDetectorPropertyAsync("cam", new NumericDetectorProperty("Exposure", 0, -1)));

        Assert.Equal("out of range", ex.Message);
    }

    // ---------- Stage ----------

    [Fact]
    public async Task Get_stage_position_returns_coordinates()
    {
        var coords = new StageCoordinates(0, false, 1, 2, 3);
        var manager = CreateManager(_ => new MotorizedStagePositionResponse(new XYStagePosition(coords, "")), out var handler);

        var position = await manager.GetMotorizedStagePositionAsync("dStage");

        Assert.Same(coords, position.Coordinates);
        Assert.Equal("dStage", Assert.IsType<GetMotorizedStagePositionRequest>(Assert.Single(handler.Requests)).StageName);
    }

    [Fact]
    public async Task Set_stage_position_sends_request_and_throws_on_error()
    {
        var coords = new StageCoordinates(0, false, 1, 2, 3);

        var ok = CreateManager(_ => new StatusOkResponse(), out var handler);
        await ok.SetMotorizedStagePositionAsync("dStage", coords);
        Assert.Same(coords, Assert.IsType<SetMotorizedStagePositionRequest>(Assert.Single(handler.Requests)).Position);

        var failing = CreateManager(_ => new StatusErrorResponse("limit switch"), out _);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            failing.SetMotorizedStagePositionAsync("dStage", coords));
        Assert.Equal("limit switch", ex.Message);
    }

    // ---------- Equipment ----------

    [Fact]
    public async Task List_available_equipment_parses_backend_equipment()
    {
        var equipment = JObject.Parse(ImagerCoreWire.AvailableEquipment)["equipment"]!;
        var manager = CreateManager(_ => new AvailableEquipmentResponse(equipment), out _);

        var result = await manager.ListAvailableEquipmentAsync();

        Assert.Equal(new[] { "fw1", "hello", "Dummy stage" }, result.Select(e => e.name));
        Assert.Equal(2, result[0].availablemovablecomponents.Count);
        Assert.Single(result[1].availablelightsources);
        Assert.True(result[2].hasmotorizedstage);
        Assert.Equal("dStage", result[2].motorizedstageName);
    }

    [Fact]
    public async Task List_available_equipment_throws_when_equipment_is_not_an_array()
    {
        var manager = CreateManager(_ => new AvailableEquipmentResponse(new JObject()), out _);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ListAvailableEquipmentAsync());
    }

    // ---------- Measurement program ----------

    private static ExecuteMeasurementProgramRequest EmptyProgram() =>
        new(new JObject(), new JObject(), new JObject());

    private static ChannelMessage Message(ulong index) =>
        new(index, new AsyncMeasurementMessage("acquireddatamessage", null!, null!, null!));

    private static async Task<List<MeasurementEvent>> RunProgramAsync(
        ImagerCommunicationManager manager,
        CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<MeasurementEvent>();
        manager.ExecuteMeasurementProgram(EmptyProgram(), channel.Writer, cancellationToken);

        var events = new List<MeasurementEvent>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var e in channel.Reader.ReadAllAsync(timeout.Token))
            events.Add(e);
        return events;
    }

    [Fact]
    public async Task Measurement_program_streams_data_acknowledges_and_completes()
    {
        var dataResponses = new Queue<ImagerResponse>(new ImagerResponse[]
        {
            new StatusNoNewAsyncDataResponse(),
            new AsyncAcquiredImagesResponse(new[] { Message(0), Message(1) }),
            new AsyncAcquiredImagesResponse(new[] { Message(2) }),
            new StatusNoNewAsyncDataComingResponse(),
        });
        var statusResponses = new Queue<ImagerResponse>(new ImagerResponse[]
        {
            new AsyncStatusMessagesResponse(new[] { "started" }),
            new AsyncStatusMessagesResponse(Array.Empty<string>()),
            new AsyncStatusMessagesResponse(new[] { "halfway" }),
        });

        var manager = CreateManager(req => req switch
        {
            UseSharedMemoryForTransferRequest => new SharedMemoryNameResponse("shm"),
            ExecuteMeasurementProgramRequest => new StatusOkResponse(),
            FetchAsyncDataRequest => dataResponses.Dequeue(),
            FetchAsyncStatusMessagesRequest => statusResponses.Dequeue(),
            AcknowledgeDataReceiptRequest => new StatusOkResponse(),
            _ => throw new InvalidOperationException($"Unexpected {req.Action}")
        }, out var handler);

        var events = await RunProgramAsync(manager);

        Assert.IsType<MeasurementStartedEvent>(events.First());
        Assert.IsType<MeasurementCompletedEvent>(events.Last());

        var data = events.OfType<MeasurementDataEvent>().ToList();
        Assert.Equal(2, data.Count);
        Assert.Equal(new ulong[] { 0, 1 }, data[0].Messages.Select(m => m.Index));
        Assert.Equal(new ulong[] { 2 }, data[1].Messages.Select(m => m.Index));

        var status = events.OfType<MeasurementStatusTextEvent>().SelectMany(e => e.Messages);
        Assert.Equal(new[] { "started", "halfway" }, status);

        Assert.Equal(new ulong[] { 1, 2 },
            handler.RequestsOf<AcknowledgeDataReceiptRequest>().Select(r => r.UpToAndIncluding));

        var shm = Assert.Single(handler.RequestsOf<UseSharedMemoryForTransferRequest>());
        Assert.True(shm.UseSharedMemory);
        Assert.IsType<UseSharedMemoryForTransferRequest>(handler.Requests.First());
        Assert.IsType<ExecuteMeasurementProgramRequest>(handler.Requests.ElementAt(1));

        Assert.DoesNotContain(events, e => e is MeasurementErrorEvent);
    }

    [Fact]
    public async Task Measurement_program_reports_start_error_and_does_not_poll()
    {
        var manager = CreateManager(req => req switch
        {
            UseSharedMemoryForTransferRequest => new SharedMemoryNameResponse("shm"),
            ExecuteMeasurementProgramRequest => new StatusErrorResponse("no detector enabled"),
            _ => throw new InvalidOperationException($"Unexpected {req.Action}")
        }, out var handler);

        var events = await RunProgramAsync(manager);

        var error = Assert.IsType<MeasurementErrorEvent>(Assert.Single(events));
        Assert.Equal("Failed to start: no detector enabled", error.Error);
        Assert.Empty(handler.RequestsOf<FetchAsyncDataRequest>());
    }

    [Fact]
    public async Task Measurement_program_reports_unexpected_start_response()
    {
        var manager = CreateManager(req => req switch
        {
            ExecuteMeasurementProgramRequest => new UnknownJsonResponse(ImagerCoreWire.InvalidQuery("bad program")),
            _ => new SharedMemoryNameResponse("shm"),
        }, out _);

        var events = await RunProgramAsync(manager);

        var error = Assert.IsType<MeasurementErrorEvent>(Assert.Single(events));
        Assert.Contains(nameof(UnknownJsonResponse), error.Error);
    }

    [Fact]
    public async Task Measurement_program_stops_on_polling_error()
    {
        var manager = CreateManager(req => req switch
        {
            UseSharedMemoryForTransferRequest => new SharedMemoryNameResponse("shm"),
            ExecuteMeasurementProgramRequest => new StatusOkResponse(),
            FetchAsyncDataRequest => new StatusErrorResponse("camera disconnected"),
            _ => throw new InvalidOperationException($"Unexpected {req.Action}")
        }, out var handler);

        var events = await RunProgramAsync(manager);

        Assert.IsType<MeasurementStartedEvent>(events[0]);
        Assert.Equal("Polling error: camera disconnected", Assert.IsType<MeasurementErrorEvent>(events[1]).Error);
        Assert.IsType<MeasurementCompletedEvent>(events[2]);
        Assert.Empty(handler.RequestsOf<FetchAsyncStatusMessagesRequest>());
    }

    [Fact]
    public async Task Measurement_program_reports_critical_failure_when_transport_throws()
    {
        var manager = CreateManager(req => req switch
        {
            UseSharedMemoryForTransferRequest => new SharedMemoryNameResponse("shm"),
            ExecuteMeasurementProgramRequest => new StatusOkResponse(),
            _ => throw new IOException("connection reset")
        }, out _);

        var events = await RunProgramAsync(manager);

        var error = events.OfType<MeasurementErrorEvent>().Single();
        Assert.Equal("Critical failure: connection reset", error.Error);
    }

    [Fact]
    public async Task Measurement_program_cancellation_completes_the_channel_without_error()
    {
        using var cts = new CancellationTokenSource();
        var manager = CreateManager(req =>
        {
            if (req is FetchAsyncDataRequest)
                cts.Cancel();

            return req switch
            {
                UseSharedMemoryForTransferRequest => new SharedMemoryNameResponse("shm"),
                ExecuteMeasurementProgramRequest => new StatusOkResponse(),
                FetchAsyncDataRequest => new StatusNoNewAsyncDataResponse(),
                _ => new AsyncStatusMessagesResponse(Array.Empty<string>()),
            };
        }, out _);

        var events = await RunProgramAsync(manager, cts.Token);

        Assert.IsType<MeasurementStartedEvent>(events[0]);
        Assert.DoesNotContain(events, e => e is MeasurementErrorEvent);
    }

    [Fact]
    public async Task Cancel_measurement_program_sends_cancel_and_swallows_errors()
    {
        var manager = CreateManager(_ => new StatusErrorResponse("nothing running"), out var handler);

        await manager.CancelMeasurementProgramAsync();

        Assert.IsType<CancelAsyncAcquisitionRequest>(Assert.Single(handler.Requests));
    }
}
