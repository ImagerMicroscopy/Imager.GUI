using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views;
using ImagerAvalonia.Views.ViewUtils;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;


namespace ImagerAvalonia.Tests.Views;

/// <summary>
/// SPEC - the field viewer is a live, coarse map of the sample built from acquired
/// camera frames, one overlay per acquisition/detector pair:
///
/// 1. Tile placement: a frame is drawn at
///    screen position = mosaic origin + (stage position - anchor) / (µm per screen unit),
///    where µm per screen unit = pixelSize_nm / 100. The anchor is the stage position
///    read when the viewer is engaged; if the stage is unreadable, the first received
///    frame becomes the anchor. Tiles are 1/10th of the frame size in screen units.
/// 2. Tile rendering: every 8th pixel is sampled and contrast-stretched against the
///    current per-(acquisition, detector) limits. Changing the limits re-renders the
///    tiles already on screen.
/// 3. Duplicate suppression: a frame within 10% of the field of view of the last
///    placed tile is skipped. The first frame after engaging (or after clearing)
///    always places, whatever the stage read returned.
/// 4. Overlap: a later tile covering an earlier one occludes it (the map shows the
///    most recent state of the sample).
/// 5. Engage: engaging anchors the mosaic at the current stage position and starts
///    placing tiles; disengaging freezes the map. Engagement mirrors the viewer model
///    state exactly - a failed stage read must not silently disable tile placement.
/// 6. Selection: selecting an acquisition/detector shows that pair's mosaic and its
///    contrast settings, exclusively.
/// 7. Lifecycle: rebuilding the schema replaces the canvases (no orphaned canvases
///    accumulate); detaching the view stops image processing.
/// 8. Stage moves: right-clicking the map offers "Move stage here" when the map's
///    anchor is a trusted stage read and no measurement is running. The target
///    accounts for pan and zoom. A move farther than 0.5 mm from the current
///    position requires confirmation. Z and autofocus state are preserved;
///    blocking hardware calls stay off the UI thread.
/// </summary>
public sealed class FieldViewerSpecTests
{
    private const int ScreenWidth = 1920;
    private const int ScreenHeight = 1080;
    // Mosaic origin for the screen size above: (width/2 - 260, height/2).
    private const double OriginX = ScreenWidth / 2 - 260;
    private const double OriginY = ScreenHeight / 2;

    private static Mock<IStageControl> StageAt(double x, double y)
    {
        var stage = new Mock<IStageControl>();
        stage.Setup(s => s.ReadStagePosition()).Returns(new XYStagePosition(0, x, y, 0, false, "Stage"));
        return stage;
    }

    private static Mock<IStageControl> UnreadableStage()
    {
        var stage = new Mock<IStageControl>();
        stage.Setup(s => s.ReadStagePosition()).Returns((XYStagePosition?)null);
        return stage;
    }

    private static FieldViewerView CreateView(FieldViewerViewModel vm)
    {
        ScreenHelper.ScreenSize = new PixelRect(0, 0, ScreenWidth, ScreenHeight);
        ScreenHelper.Scaling = 1.0;
        return new FieldViewerView { DataContext = vm };
    }

    private static List<Tuple<string, string>> Pairs(params (string acq, string det)[] pairs) =>
        pairs.Select(p => Tuple.Create(p.acq, p.det)).ToList();

    /// <summary>
    /// One plane of a uniform mid-gray image; every pixel holds `value` on the
    /// 16-bit display scale, acquired at the given stage position in microns.
    /// </summary>
    private static ImageData Frame(
        double positionX,
        double positionY,
        uint width = 512,
        uint height = 512,
        ushort value = 0x8000,
        string acquisition = "Acq1",
        string detector = "Det1")
    {
        var bytes = new byte[width * height * 2];
        for (int i = 0; i < bytes.Length; i += 2)
        {
            bytes[i] = (byte)(value & 0xFF);
            bytes[i + 1] = (byte)(value >> 8);
        }

        var metadata = new TiffPlaneMetadata
        {
            Width = width,
            Height = height,
            AcquisitionName = acquisition,
            DetectorName = detector,
            Type = ImagePixelFormat.Mono16,
            PositionX = positionX,
            PositionY = positionY,
        };

        return new ImageData { Images = { bytes }, Metadata = { metadata } };
    }

    /// <summary>The per-pair canvases (everything except the background canvas).</summary>
    private static List<Canvas> PairCanvases(FieldViewerView view)
    {
        var grid = view.FindControl<Grid>("ParentCanvasGrid")!;
        return grid.Children.OfType<Canvas>().Where(c => c.Name != "CNVS").ToList();
    }

    private static List<Image> Tiles(Canvas canvas) => canvas.Children.OfType<Image>().ToList();

    private static async Task<bool> EventuallyAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
                return true;
            await Task.Delay(25);
        }
        return condition();
    }

    // --- Spec 1: placement -------------------------------------------------

    [AvaloniaFact]
    public async Task Tiles_are_placed_proportionally_to_stage_displacement()
    {
        var vm = new FieldViewerViewModel(StageAt(0, 0).Object);
        var view = CreateView(vm);
        vm.SetGridData(Pairs(("Acq1", "Det1")));
        vm.EngageFieldViewer();

        vm.ProcessImages(null, Frame(30, 0));
        vm.ProcessImages(null, Frame(60, 0));

        var canvas = Assert.Single(PairCanvases(view));
        Assert.True(await EventuallyAsync(() => Tiles(canvas).Count == 2), "expected two tiles");

        // 100 nm pixels: one screen unit per micrometer of stage travel.
        var tiles = Tiles(canvas);
        Assert.Equal(30, Canvas.GetLeft(tiles[1]) - Canvas.GetLeft(tiles[0]), 1);
        Assert.Equal(Canvas.GetTop(tiles[0]), Canvas.GetTop(tiles[1]), 1);
    }

    // --- Spec 3: duplicate suppression --------------------------------------

    [AvaloniaFact]
    public async Task A_frame_substantially_re_imaging_the_last_tile_is_skipped()
    {
        var vm = new FieldViewerViewModel(StageAt(0, 0).Object);
        var view = CreateView(vm);
        vm.SetGridData(Pairs(("Acq1", "Det1")));
        vm.EngageFieldViewer();

        vm.ProcessImages(null, Frame(30, 0));
        var canvas = Assert.Single(PairCanvases(view));
        Assert.True(await EventuallyAsync(() => Tiles(canvas).Count == 1), "expected the first tile");

        // 2 µm is far below the duplicate threshold (10% of a 51.2 µm field of view).
        vm.ProcessImages(null, Frame(32, 0));
        await Task.Delay(300);
        Assert.Single(Tiles(canvas));
    }

    [AvaloniaFact]
    public async Task The_duplicate_threshold_scales_with_the_field_of_view()
    {
        var vm = new FieldViewerViewModel(StageAt(0, 0).Object);
        var view = CreateView(vm);
        vm.SetGridData(Pairs(("Acq1", "Det1")));
        vm.EngageFieldViewer();

        // 64-pixel frames at 100 nm span 6.4 µm; a 2 µm move is a third of the
        // field of view and must produce a tile.
        vm.ProcessImages(null, Frame(2, 0, width: 64, height: 64));

        var canvas = Assert.Single(PairCanvases(view));
        Assert.True(await EventuallyAsync(() => Tiles(canvas).Count == 1),
            "a third-of-FOV move must place a tile regardless of the field size");
    }

    // --- Spec 5: engage anchors the map -------------------------------------

    [AvaloniaFact]
    public async Task Engaging_places_the_first_frame_at_the_mosaic_origin()
    {
        var vm = new FieldViewerViewModel(StageAt(10, 0).Object);
        var view = CreateView(vm);
        vm.SetGridData(Pairs(("Acq1", "Det1")));
        vm.EngageFieldViewer();

        // The frame sits exactly at the engage position: it anchors the map and
        // must be visible at the origin, not skipped for "not moving".
        vm.ProcessImages(null, Frame(10, 0));

        var canvas = Assert.Single(PairCanvases(view));
        Assert.True(await EventuallyAsync(() => Tiles(canvas).Count == 1),
            "the frame acquired at the anchor position must place a tile");
        var tile = Assert.Single(Tiles(canvas));
        Assert.Equal(OriginX, Canvas.GetLeft(tile), 1);
        Assert.Equal(OriginY, Canvas.GetTop(tile), 1);
    }

    [AvaloniaFact]
    public async Task An_engaged_viewer_places_tiles_even_when_the_stage_is_unreadable()
    {
        var vm = new FieldViewerViewModel(UnreadableStage().Object);
        var view = CreateView(vm);
        vm.SetGridData(Pairs(("Acq1", "Det1")));
        vm.EngageFieldViewer();

        vm.ProcessImages(null, Frame(30, 0));

        var canvas = Assert.Single(PairCanvases(view));
        Assert.True(await EventuallyAsync(() => Tiles(canvas).Count == 1),
            "engagement must mirror the viewer model even if the stage read failed");
    }

    // --- Spec 4: overlap -----------------------------------------------------

    [AvaloniaFact]
    public async Task A_later_tile_covering_the_same_region_occludes_the_earlier_one()
    {
        var vm = new FieldViewerViewModel(StageAt(0, 0).Object);
        var view = CreateView(vm);
        vm.SetGridData(Pairs(("Acq1", "Det1")));
        vm.EngageFieldViewer();

        vm.ProcessImages(null, Frame(30, 0));
        vm.ProcessImages(null, Frame(60, 0));

        var canvas = Assert.Single(PairCanvases(view));
        Assert.True(await EventuallyAsync(() => Tiles(canvas).Count == 2), "expected two overlapping tiles");

        // 51.2 µm tiles placed 30 µm apart overlap; the later tile must come after
        // the earlier one in the canvas (children paint in order, so it occludes).
        var tiles = Tiles(canvas);
        Assert.Equal(Canvas.GetTop(tiles[0]), Canvas.GetTop(tiles[1]), 1);
        Assert.True(Canvas.GetLeft(tiles[1]) > Canvas.GetLeft(tiles[0]));
    }

    // --- Spec 2: contrast -----------------------------------------------------

    [AvaloniaFact]
    public async Task Changing_contrast_re_renders_the_tiles_on_screen()
    {
        var vm = new FieldViewerViewModel(StageAt(0, 0).Object);
        var view = CreateView(vm);
        vm.SetGridData(Pairs(("Acq1", "Det1")));
        vm.EngageFieldViewer();

        vm.ProcessImages(null, Frame(30, 0));
        var canvas = Assert.Single(PairCanvases(view));
        Assert.True(await EventuallyAsync(() => Tiles(canvas).Count == 1), "expected one tile");

        var tile = Assert.Single(Tiles(canvas));
        var initialBitmap = tile.Source;
        Assert.IsType<WriteableBitmap>(initialBitmap);

        var panel = view.FindControl<ContrastPanelView>("ContrastPanelFieldView")!;
        panel.SetCurrentContrastVals(this, new GridContrastEventArgs(0, 0, null, null));
        panel.contrastSettings[0][0].Min_disp_val = 32768;

        // The tile is re-rendered: it gets a fresh bitmap rendered from its
        // cached sample against the new limits. (The headless platform's
        // framebuffer bytes are not reliably readable, so the tonal result is
        // covered by the stretch primitive test below.)
        Assert.True(await EventuallyAsync(() => !ReferenceEquals(initialBitmap, tile.Source)),
            "raising the contrast floor must re-render the existing tile");
    }

    [Fact]
    public void The_contrast_stretch_maps_the_tile_tonalities()
    {
        // 0x8000 pixels against limits [0, 65535] stretch to themselves...
        Assert.Equal(0x8000, TiffHandler.StretchedValue(0x8000, 0, 65535, 1.0));

        // ... and a contrast floor raised to the pixel value renders black.
        Assert.Equal((ushort)0, TiffHandler.StretchedValue(0x8000, 32768, 65535, 2.0));
    }

    // --- Spec 7: lifecycle -----------------------------------------------------

    [AvaloniaFact]
    public void Rebuilding_the_schema_leaves_no_orphaned_canvases()
    {
        var vm = new FieldViewerViewModel(StageAt(0, 0).Object);
        var view = CreateView(vm);

        vm.SetGridData(Pairs(("Acq1", "Det1")));
        vm.SetGridData(Pairs(("Acq2", "Det2")));

        var canvas = Assert.Single(PairCanvases(view));
        Assert.NotNull(canvas);
    }

    [AvaloniaFact]
    public async Task Detaching_the_view_stops_processing()
    {
        var vm = new FieldViewerViewModel(StageAt(0, 0).Object);
        var view = CreateView(vm);
        vm.SetGridData(Pairs(("Acq1", "Det1")));
        vm.EngageFieldViewer();

        var window = new Window { Content = view };
        window.Show();

        vm.ProcessImages(null, Frame(30, 0));
        var canvas = Assert.Single(PairCanvases(view));
        Assert.True(await EventuallyAsync(() => Tiles(canvas).Count == 1), "expected one tile while attached");

        window.Content = null;
        vm.ProcessImages(null, Frame(60, 0));
        await Task.Delay(300);

        Assert.Single(Tiles(canvas));
    }

    [AvaloniaFact]
    public async Task Reattaching_the_view_resumes_processing()
    {
        var vm = new FieldViewerViewModel(StageAt(0, 0).Object);
        var view = CreateView(vm);
        vm.SetGridData(Pairs(("Acq1", "Det1")));
        vm.EngageFieldViewer();

        var window = new Window { Content = view };
        window.Show();

        vm.ProcessImages(null, Frame(30, 0));
        var canvas = Assert.Single(PairCanvases(view));
        Assert.True(await EventuallyAsync(() => Tiles(canvas).Count == 1), "expected one tile while attached");

        // Toggling to another tab and back suspends and resumes the same
        // (cached) view; it must keep placing tiles afterwards.
        window.Content = null;
        window.Content = view;
        vm.ProcessImages(null, Frame(60, 0));

        Assert.True(await EventuallyAsync(() => Tiles(canvas).Count == 2),
            "a reattached view must resume placing tiles");
    }

    // --- Clear view ------------------------------------------------------------

    [AvaloniaFact]
    public async Task Clearing_then_re_imaging_the_same_region_redraws_it()
    {
        var vm = new FieldViewerViewModel(StageAt(0, 0).Object);
        var view = CreateView(vm);
        vm.SetGridData(Pairs(("Acq1", "Det1")));
        vm.EngageFieldViewer();

        vm.ProcessImages(null, Frame(30, 0));
        var canvas = Assert.Single(PairCanvases(view));
        Assert.True(await EventuallyAsync(() => Tiles(canvas).Count == 1), "expected one tile");

        var clearButton = view.GetLogicalDescendants().OfType<Button>()
            .Single(b => (b.Content as string)?.Contains("Clear view") == true);
        clearButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Empty(Tiles(canvas));

        // The same region re-images: the cleared map must draw it again.
        vm.ProcessImages(null, Frame(30, 0));
        Assert.True(await EventuallyAsync(() => Tiles(canvas).Count == 1),
            "re-imaging the same region after a clear must redraw it");
    }

    // --- Spec 6: selection --------------------------------------------------------

    [AvaloniaFact]
    public async Task Selecting_a_pair_shows_its_mosaic_exclusively()
    {
        var vm = new FieldViewerViewModel(StageAt(0, 0).Object);
        var view = CreateView(vm);
        vm.SetGridData(Pairs(("Acq1", "Det1"), ("Acq2", "Det2")));
        vm.EngageFieldViewer();

        vm.ProcessImages(null, Frame(30, 0, acquisition: "Acq1", detector: "Det1"));
        vm.ProcessImages(null, Frame(30, 0, acquisition: "Acq2", detector: "Det2"));

        var canvases = PairCanvases(view);
        // The schema is the cross product of acquisitions and detectors.
        Assert.Equal(4, canvases.Count);
        Assert.True(await EventuallyAsync(() => Tiles(canvases[0]).Count == 1 && Tiles(canvases[3]).Count == 1),
            "expected one tile per pair");

        vm.SelectedAcquisition = "Acq2";
        vm.SelectedDetector = "Det2";

        Assert.True(await EventuallyAsync(() => canvases[3].IsVisible && !canvases[0].IsVisible),
            "the selected pair's canvas must be the only visible one");
    }

    // --- Schema ------------------------------------------------------------

    [AvaloniaFact]
    public void Schema_pairs_populate_acquisitions_and_detectors()
    {
        var vm = new FieldViewerViewModel();

        vm.SetGridData(Pairs(("A", "D1"), ("B", "D2")));

        Assert.Equal(new[] { "A", "B" }, vm.Acquisitions);
        Assert.Equal(new[] { "D1", "D2" }, vm.Detectors);
    }

    [AvaloniaFact]
    public void A_schema_reload_keeps_a_selection_that_still_exists()
    {
        var vm = new FieldViewerViewModel();
        vm.SetGridData(Pairs(("Acq1", "Det1"), ("Acq2", "Det2")));
        vm.SelectedAcquisition = "Acq2";
        vm.SelectedDetector = "Det2";

        // A live-session restart reloads the same schema; the combo boxes
        // must not forget the selection.
        vm.SetGridData(Pairs(("Acq1", "Det1"), ("Acq2", "Det2")));

        Assert.Equal("Acq2", vm.SelectedAcquisition);
        Assert.Equal("Det2", vm.SelectedDetector);
    }

    // --- Spec 8: stage moves from the map -------------------------------------

    [Fact]
    public void The_map_reports_stage_positions_for_canvas_points()
    {
        var mosaic = new FieldViewerMosaic { OriginX = 700, OriginY = 540, PixelSizeNm = 100 };
        mosaic.SetAnchor(1000, 2000);

        // Canvas point 30 units right of and 20 units above the origin.
        Assert.True(mosaic.TryGetStagePositionAt(730, 520, out var stageX, out var stageY));
        Assert.Equal(1030, stageX, 3);
        Assert.Equal(2020, stageY, 3);
    }

    [Fact]
    public void The_map_refuses_stage_positions_without_a_stage_anchor()
    {
        var mosaic = new FieldViewerMosaic { OriginX = 700, OriginY = 540 };

        // No stage read at engage: the anchor may be in frame-metadata units,
        // so no stage target may be derived from the map.
        Assert.False(mosaic.TryGetStagePositionAt(730, 520, out _, out _));
    }

    private static Mock<IStageControl> StageWithPositions()
    {
        var stage = new Mock<IStageControl>();
        stage.Setup(s => s.ReadStagePosition())
            .Returns(new XYStagePosition(0, 1000, 2000, 55, true, "Stage"));
        return stage;
    }

    [Fact]
    public async Task Moving_the_stage_targets_the_requested_position_preserving_z_and_autofocus()
    {
        var stage = StageWithPositions();
        var captured = new List<XYStagePosition>();
        stage.Setup(s => s.SetStagePosition(Capture.In(captured)));
        var vm = new FieldViewerViewModel(stage.Object);

        // A small move: no confirmation needed.
        await vm.MoveStageTo(1030, 1985);

        var sent = Assert.Single(captured);
        Assert.Equal(1030, sent.Coordinates.x, 3);
        Assert.Equal(1985, sent.Coordinates.y, 3);
        Assert.Equal(55, sent.Coordinates.z, 3);
        Assert.True(sent.Coordinates.usinghardwareautofocus);
        Assert.False(vm.IsStageMoveInFlight);
    }

    [Fact]
    public async Task A_far_move_requires_confirmation()
    {
        var stage = StageWithPositions();
        var captured = new List<XYStagePosition>();
        stage.Setup(s => s.SetStagePosition(Capture.In(captured)));
        var vm = new FieldViewerViewModel(stage.Object);

        double? askedDistance = null;
        vm.MoveConfirmation = distance =>
        {
            askedDistance = distance;
            return Task.FromResult(false);
        };

        // 2 mm away: past the 0.5 mm threshold.
        await vm.MoveStageTo(3000, 2000);

        Assert.Equal(2000, askedDistance.GetValueOrDefault(), 1);
        Assert.Empty(captured);

        vm.MoveConfirmation = _ => Task.FromResult(true);
        await vm.MoveStageTo(3000, 2000);

        Assert.Equal(3000, Assert.Single(captured).Coordinates.x, 3);
    }

    [Fact]
    public async Task A_short_move_does_not_ask_for_confirmation()
    {
        var stage = StageWithPositions();
        var captured = new List<XYStagePosition>();
        stage.Setup(s => s.SetStagePosition(Capture.In(captured)));
        var vm = new FieldViewerViewModel(stage.Object) { MoveConfirmation = _ => Task.FromCanceled<bool>(new CancellationToken(true)) };

        // 30 µm: well below the threshold; a cancelled confirmation would
        // never complete the move.
        await vm.MoveStageTo(1030, 2000);

        Assert.Single(captured);
    }

    [Fact]
    public async Task Stage_moves_are_blocked_while_a_measurement_runs()
    {
        var stage = StageWithPositions();
        var vm = new FieldViewerViewModel(stage.Object) { IsMeasurementRunning = true };

        await vm.MoveStageTo(1030, 2000);

        stage.Verify(s => s.SetStagePosition(It.IsAny<XYStagePosition>()), Times.Never);
    }

    [Fact]
    public async Task A_stage_that_cannot_be_read_cannot_be_moved()
    {
        var stage = UnreadableStage();
        var vm = new FieldViewerViewModel(stage.Object);

        await vm.MoveStageTo(1030, 2000);

        stage.Verify(s => s.SetStagePosition(It.IsAny<XYStagePosition>()), Times.Never);
    }

    [AvaloniaFact]
    public void A_right_click_target_accounts_for_pan_and_zoom()
    {
        var vm = new FieldViewerViewModel(StageAt(1000, 2000).Object);
        var view = CreateView(vm);
        vm.SetGridData(Pairs(("Acq1", "Det1")));
        vm.EngageFieldViewer();

        // Pan and zoom the map; the click position arrives in screen (grid)
        // coordinates and must be un-transformed before resolving the target.
        view.SetMapTransformForTests(2, 100, 50);
        var canvas = Assert.Single(PairCanvases(view));

        var canvasPoint = new Point(730, 520); // 30 units right of, 20 above the origin
        var gridPoint = canvasPoint.Transform(canvas.RenderTransform!.Value);

        Assert.True(view.TryGetMoveTarget(gridPoint, out var stageX, out var stageY));
        Assert.Equal(1030, stageX, 3);
        Assert.Equal(2020, stageY, 3);
    }

    [AvaloniaFact]
    public void A_right_click_target_is_gated_by_the_session_and_anchor()
    {
        var vm = new FieldViewerViewModel(StageAt(1000, 2000).Object);
        var view = CreateView(vm);
        vm.SetGridData(Pairs(("Acq1", "Det1")));
        vm.EngageFieldViewer();

        Assert.True(view.TryGetMoveTarget(new Point(700, 540), out _, out _));

        vm.IsMeasurementRunning = true;
        Assert.False(view.TryGetMoveTarget(new Point(700, 540), out _, out _));
        vm.IsMeasurementRunning = false;
        Assert.True(view.TryGetMoveTarget(new Point(700, 540), out _, out _));

        // An unreadable stage at engage leaves the map without a trusted
        // anchor: no stage move may be offered.
        var untrustedVm = new FieldViewerViewModel(UnreadableStage().Object);
        var untrustedView = CreateView(untrustedVm);
        untrustedVm.SetGridData(Pairs(("Acq1", "Det1")));
        untrustedVm.EngageFieldViewer();

        Assert.False(untrustedView.TryGetMoveTarget(new Point(700, 540), out _, out _));
    }
}
