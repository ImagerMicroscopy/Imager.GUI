using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using System;

namespace ImagerAvalonia.ViewModels
{
    public partial class TimeLapseViewModel : MeasurementElementViewModel
    {
        [ObservableProperty]
        private decimal? timeDelta = 0.001m;

        [ObservableProperty]
        private double? nTimes = 1;

        public int num_frames => (int)(NTimes ?? 1);

        public TimeLapseViewModel()
        {
            DisplayedInfo = $"({NTimes} times Δt = {TimeDelta}s)";
            Header = "Time Lapse";
            PropertyChanged += TimeLapseViewModel_PropertyChanged;
        }

        private void TimeLapseViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            DisplayedInfo = $"({NTimes} times Δt = {TimeDelta}s)";
        }

        public override void Dispose()
        {
            PropertyChanged -= TimeLapseViewModel_PropertyChanged;
            base.Dispose();
        }

        public override MeasurementElementBase ToModel()
        {
            return new TimeLapseElement
            {
                NTotal = NTimes ?? 1,
                TimeDelta = (double)(TimeDelta ?? 0),
                ElementId = Elementid.ToString(),
                SmartProgramId = SelectedProgramId?.SmartProgramID.ToString() ?? null
            };
        }

        public override void LoadFromModel(MeasurementElementBase measurementElement, LoadContext context)
        {
            var model = (TimeLapseElement)measurementElement;

            if (Guid.TryParse(model.ElementId, out var parsedId))
            {
                Elementid = parsedId;
            }

            NTimes = model.NTotal;
            TimeDelta = (decimal)model.TimeDelta;

            LoadSmartProgramBinding(model.SmartProgramId);
        }
    }
}
