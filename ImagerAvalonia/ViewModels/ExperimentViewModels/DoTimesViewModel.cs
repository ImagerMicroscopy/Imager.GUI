
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using System;

namespace ImagerAvalonia.ViewModels;

public partial class DoTimesViewModel : MeasurementElementViewModel
{
    [ObservableProperty] int _NumRepeats = 0;
    public int num_frames { get { return NumRepeats; } set { NumRepeats = value; } }

    public DoTimesViewModel(GlobalDefinedSettingsViewModel settings)
    {
        DisplayedInfo = "(0 times)";
        Header = "Do Times";
    }


    partial void OnNumRepeatsChanged(int value)
    {
        DisplayedInfo = $"({NumRepeats} times)";
    }

    public override MeasurementElementBase ToModel()
    {
        return new DoTimesElement
        {
            NTotal = NumRepeats,
            SmartProgramId = SelectedProgramId?.ToString() ?? null,
            ElementId = Elementid.ToString()
        };
    }

    public override void LoadFromModel(MeasurementElementBase model, LoadContext context)
    {
        if (model is not DoTimesElement doTimes)
            throw new ArgumentException($"Expected {nameof(DoTimesElement)}", nameof(model));

        base.LoadFromModel(model, context);
        NumRepeats = doTimes.NTotal; // triggers OnNumRepeatsChanged -> updates DisplayedInfo
    }
}

