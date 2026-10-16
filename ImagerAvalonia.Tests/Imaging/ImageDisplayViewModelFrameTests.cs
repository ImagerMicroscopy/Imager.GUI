using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using Moq;
using Xunit;

namespace ImagerAvalonia.Tests.Imaging;

/// <summary>
/// The frame-selection logic of the image display. Frame indices are zero-based
/// throughout (SelectedFrame, storage requests); MaxFrameCount and AcquiredFrameCount
/// are counts; one-based numbers exist only in the user-facing AcquisitionProgress.
/// Frames beyond AcquiredFrameCount are pending rather than silently clamped to the
/// newest image.
/// </summary>
public sealed class ImageDisplayViewModelFrameTests
{
    private static ImageDisplayViewModel Vm(int maxFrameCount = 10)
    {
        var vm = new ImageDisplayViewModel { MaxFrameCount = maxFrameCount };
        vm.SetGridData(new List<Tuple<string, string>> { new("Acq1", "DummyCam1") });
        return vm;
    }

    private static List<int> Requests(ImageDisplayViewModel vm)
    {
        var requested = new List<int>();
        vm.OnDetectionRequested += (_, e) => requested.Add(e.RequestedTime);
        return requested;
    }

    [Fact]
    public void Live_view_tracks_the_newest_frame_and_leaves_the_slider_at_max()
    {
        var vm = Vm(maxFrameCount: 10);
        var requested = Requests(vm);

        vm.UpdateDisplayedImageNumber("Acq1", "DummyCam1", 0);
        vm.UpdateDisplayedImageNumber("Acq1", "DummyCam1", 9);

        Assert.Equal(10, vm.AcquiredFrameCount);
        Assert.Equal(9, vm.SelectedFrame); // zero-based: the last frame is the slider maximum
        Assert.Equal(9, vm.LastFrameIndex);
        Assert.Equal("10/10", vm.AcquisitionProgress); // user-facing text is one-based
        Assert.Empty(requested); // no storage request is needed while following
    }

    [Fact]
    public void Slider_requests_pass_the_zero_based_index_unchanged()
    {
        var vm = Vm();
        var requested = Requests(vm);

        vm.ShowLiveView = false;
        vm.UpdateDisplayedImageNumber("Acq1", "DummyCam1", 0);
        vm.UpdateDisplayedImageNumber("Acq1", "DummyCam1", 1);
        vm.UpdateDisplayedImageNumber("Acq1", "DummyCam1", 2);

        vm.SelectedFrame = 2;

        Assert.Equal(new[] { 2 }, requested);
    }

    [Fact]
    public void Browsing_past_the_newest_frame_marks_it_pending_without_requesting()
    {
        var vm = Vm();
        var requested = Requests(vm);

        vm.ShowLiveView = false;
        vm.UpdateDisplayedImageNumber("Acq1", "DummyCam1", 0);
        vm.UpdateDisplayedImageNumber("Acq1", "DummyCam1", 1);

        vm.SelectedFrame = 5;

        Assert.True(vm.IsFramePending);
        Assert.Empty(requested); // requesting would silently clamp to the newest frame

        vm.SelectedFrame = 1;

        Assert.False(vm.IsFramePending);
        Assert.Equal(new[] { 1 }, requested);
    }

    [Fact]
    public void A_pending_frame_is_loaded_once_it_has_been_acquired()
    {
        var vm = Vm();
        var requested = Requests(vm);

        vm.ShowLiveView = false;
        vm.UpdateDisplayedImageNumber("Acq1", "DummyCam1", 0);
        vm.SelectedFrame = 3;
        Assert.True(vm.IsFramePending);

        // The acquisition catches up with the frame the user browsed ahead to.
        vm.UpdateDisplayedImageNumber("Acq1", "DummyCam1", 3);

        Assert.Equal(4, vm.AcquiredFrameCount);
        Assert.False(vm.IsFramePending);
        Assert.Equal(new[] { 3 }, requested);
    }

    [Fact]
    public void Re_enabling_show_last_snaps_to_and_loads_the_newest_frame()
    {
        var vm = Vm();
        var requested = Requests(vm);

        vm.ShowLiveView = false;
        vm.UpdateDisplayedImageNumber("Acq1", "DummyCam1", 0);
        vm.UpdateDisplayedImageNumber("Acq1", "DummyCam1", 1);
        vm.UpdateDisplayedImageNumber("Acq1", "DummyCam1", 2);
        vm.SelectedFrame = 0;

        vm.ShowLiveView = true;

        Assert.Equal(2, vm.SelectedFrame); // zero-based index of the newest frame
        Assert.Equal(new[] { 2 }, requested);
    }

    [Fact]
    public void The_progress_text_follows_the_selected_frame_in_every_code_path()
    {
        var vm = Vm();

        vm.ShowLiveView = false;
        vm.UpdateDisplayedImageNumber("Acq1", "DummyCam1", 0);
        vm.UpdateDisplayedImageNumber("Acq1", "DummyCam1", 1);
        vm.UpdateDisplayedImageNumber("Acq1", "DummyCam1", 2);

        vm.SelectedFrame = 1;
        Assert.Equal("2/10", vm.AcquisitionProgress);

        // Re-enabling "show last" snaps the slider: the derived text must follow.
        vm.ShowLiveView = true;
        Assert.Equal("3/10", vm.AcquisitionProgress);
    }

    [Fact]
    public void Frame_requests_are_not_made_while_show_last_is_on()
    {
        var vm = Vm();
        var requested = Requests(vm);

        vm.SelectedFrame = 5; // ShowLiveView is on by default

        Assert.Empty(requested);
    }

    [Fact]
    public void The_handlers_live_view_flag_mirrors_the_checkbox()
    {
        var handler = new ImageHandler(Mock.Of<IStorageProvider>());
        var vm = new ImageDisplayViewModel();
        vm.AttachHandler(handler, enableFrameRequests: false);

        vm.ShowLiveView = false;
        Assert.False(handler.ShowLiveView);

        vm.ShowLiveView = true;
        Assert.True(handler.ShowLiveView);
    }

    [Fact]
    public void Attaching_syncs_the_handlers_live_view_flag_to_the_current_state()
    {
        var handler = new ImageHandler(Mock.Of<IStorageProvider>());
        var vm = new ImageDisplayViewModel { ShowLiveView = false };

        vm.AttachHandler(handler, enableFrameRequests: false);

        Assert.False(handler.ShowLiveView);
    }

    [Fact]
    public void The_live_view_locks_show_last_on()
    {
        var vm = new ImageDisplayViewModel { ShowLiveView = false };

        vm.IsLiveView = true;

        Assert.True(vm.ShowLiveView);
    }

    [Fact]
    public void Show_last_can_never_be_turned_off_on_the_live_view()
    {
        var vm = new ImageDisplayViewModel();
        var handler = new ImageHandler(Mock.Of<IStorageProvider>());
        vm.AttachHandler(handler, enableFrameRequests: false);
        vm.IsLiveView = true;

        vm.ShowLiveView = false;

        Assert.True(vm.ShowLiveView);
        Assert.True(handler.ShowLiveView); // the handler never sees a transient "off"
    }

    [Fact]
    public void Show_last_stays_freely_toggleable_on_non_live_views()
    {
        var vm = new ImageDisplayViewModel();

        vm.ShowLiveView = false;

        Assert.False(vm.ShowLiveView);
    }
}
