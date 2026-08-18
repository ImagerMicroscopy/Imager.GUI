using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using System;


namespace ImagerAvalonia.ViewModels;

public partial class WaitViewModel : MeasurementElementViewModel
{



    [ObservableProperty]
    public double _WaitPeriod;

    public WaitViewModel()
    {
        DisplayedInfo = $"({WaitPeriod} seconds)";
        Header = "Wait";
    }

    public override MeasurementElementBase ToModel()
    {
        return new WaitElement
        {
            Duration = WaitPeriod,
            ElementId = Elementid.ToString()
        };
    }
    public override void LoadFromModel(MeasurementElementBase measurementElement, LoadContext context)
    {
        var model = (WaitElement)measurementElement;

        WaitPeriod = model.Duration;

        if (Guid.TryParse(model.ElementId, out var parsedId))
        {
            Elementid = parsedId;
        }

        DisplayedInfo = $"({WaitPeriod} seconds)";
    }

    partial void OnWaitPeriodChanged(double value)
    {
        DisplayedInfo = $"({WaitPeriod} seconds)";
    }
}

