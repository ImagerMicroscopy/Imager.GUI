using ImagerAvalonia.ViewModels;
using Xunit;

namespace ImagerAvalonia.Tests.Imaging;

/// <summary>
/// The slot model of the image grid: SetGridData builds one slot view model per
/// acquisition/detector combination (the cross product the grid lays out), and
/// SelectedSlot is the single source of truth for selection — the IsSelected
/// flag of exactly one slot is set, in every code path.
/// </summary>
public sealed class ImageDisplayViewModelSlotTests
{
    private static List<Tuple<string, string>> Pairs(params (string acq, string det)[] pairs) =>
        pairs.Select(p => new Tuple<string, string>(p.acq, p.det)).ToList();

    [Fact]
    public void SetGridData_builds_one_slot_per_acquisition_detector_combination()
    {
        var vm = new ImageDisplayViewModel();

        vm.SetGridData(Pairs(("Acq1", "Cam1"), ("Acq1", "Cam2"), ("Acq2", "Cam1")));

        Assert.Equal(4, vm.Slots.Count); // cross product: 2 acquisitions x 2 detectors
        Assert.All(vm.Slots, slot => Assert.Equal($"{slot.AcqName}/{slot.DetName}", slot.HeaderText));
        Assert.Contains(vm.Slots, s => s.AcqName == "Acq1" && s.DetName == "Cam1");
        Assert.Contains(vm.Slots, s => s.AcqName == "Acq1" && s.DetName == "Cam2");
        Assert.Contains(vm.Slots, s => s.AcqName == "Acq2" && s.DetName == "Cam1");
        Assert.Contains(vm.Slots, s => s.AcqName == "Acq2" && s.DetName == "Cam2");
    }

    [Fact]
    public void The_first_slot_is_selected_by_default()
    {
        var vm = new ImageDisplayViewModel();

        vm.SetGridData(Pairs(("Acq1", "Cam1"), ("Acq2", "Cam1")));

        Assert.Same(vm.Slots[0], vm.SelectedSlot);
        Assert.True(vm.Slots[0].IsSelected);
        Assert.Single(vm.Slots, s => s.IsSelected);
    }

    [Fact]
    public void Changing_the_selection_moves_the_IsSelected_flag()
    {
        var vm = new ImageDisplayViewModel();
        vm.SetGridData(Pairs(("Acq1", "Cam1"), ("Acq2", "Cam1")));

        vm.SelectedSlot = vm.Slots[1];

        Assert.Same(vm.Slots[1], vm.SelectedSlot);
        Assert.False(vm.Slots[0].IsSelected);
        Assert.True(vm.Slots[1].IsSelected);
        Assert.Single(vm.Slots, s => s.IsSelected);
    }

    [Fact]
    public void Rebuilding_the_grid_deselects_the_old_slots_and_selects_the_new_first()
    {
        var vm = new ImageDisplayViewModel();
        vm.SetGridData(Pairs(("Acq1", "Cam1"), ("Acq2", "Cam1")));
        var oldSelected = vm.SelectedSlot!;
        var oldSlots = vm.Slots.ToList();

        vm.SetGridData(Pairs(("Acq3", "Cam1")));

        Assert.All(oldSlots, slot => Assert.False(slot.IsSelected));
        Assert.NotSame(oldSelected, vm.SelectedSlot);
        Assert.Same(vm.Slots[0], vm.SelectedSlot);
        Assert.Single(vm.Slots, s => s.IsSelected);
    }

    [Fact]
    public void Clearing_the_grid_data_drops_the_selection()
    {
        var vm = new ImageDisplayViewModel();
        vm.SetGridData(Pairs(("Acq1", "Cam1")));

        vm.SetGridData(null);

        Assert.Empty(vm.Slots);
        Assert.Null(vm.SelectedSlot);
    }
}
