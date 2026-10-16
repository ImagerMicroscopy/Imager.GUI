using Avalonia.Headless.XUnit;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using Xunit;

namespace ImagerAvalonia.Tests.Imaging;

public class ContrastAdjViewModelTests
{
    [AvaloniaFact]
    public void Mono8_format_reduces_the_control_bounds_and_clamps_stored_limits()
    {
        var vm = new ContrastAdjViewModel();
        vm.Min_disp_val = 1000;
        vm.Max_disp_val = 50000;

        vm.SetPixelFormat(ImagePixelFormat.Mono8);

        Assert.Equal(255, vm.SliderMaximum);
        Assert.Equal(255, vm.Min_disp_val);
        Assert.Equal(255, vm.Max_disp_val);
    }

    [AvaloniaTheory]
    [InlineData(ImagePixelFormat.Mono16)]
    [InlineData(ImagePixelFormat.Float64)]
    public void Non_mono8_formats_use_the_full_16_bit_display_scale(ImagePixelFormat format)
    {
        var vm = new ContrastAdjViewModel();
        vm.SetPixelFormat(ImagePixelFormat.Mono8);

        vm.SetPixelFormat(format);

        Assert.Equal(65535, vm.SliderMaximum);
    }

    [AvaloniaFact]
    public void Min_above_max_is_allowed_and_notifies_consumers()
    {
        var vm = new ContrastAdjViewModel();
        var notifications = 0;
        vm.OnContrastValsChanged += (_, _) => notifications++;

        vm.Min_disp_val = 60000;
        vm.Max_disp_val = 100;

        Assert.True(vm.Min_disp_val > vm.Max_disp_val);
        // The first change fires immediately; changes inside the throttle
        // window are coalesced into one trailing notification.
        Assert.True(notifications >= 1);
    }
}
