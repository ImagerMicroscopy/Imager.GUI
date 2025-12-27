using Autofac;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views;
using ImagerAvalonia.Views.ImageViews;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels
{
    public partial class LineElementViewModel : ElementPlotViewModel
    {
        public ObservableCollection<string> Plots { get; } =
            new ObservableCollection<string>(Enum.GetNames(typeof(PlotTypes)));

        private readonly IStageControl _stageControl;

        public event Action<List<double> , IImageElement, XYStagePosition>? OnPlotsUpdated;
        public event Action<XYStagePosition>? OnPositionChanged;

        public override string AcquisitionName { get ; set ; }
        public override string DetectorName { get; set; }
        public override string CollectionName { get; set; }




        public Color PlotColor;
        public List<IImageElement> LineRegions;

        [ObservableProperty] string _selectedPlotType = "Mean";
        [ObservableProperty] string _detName;
        [ObservableProperty] string _acqName;
        [ObservableProperty] string _acqDetDisplay;
        [ObservableProperty] private XYStagePosition _PinnedPosition;
        public ObservableCollection<XYStagePosition> AvailableStagePositions { get; }

        public LineElementViewModel(List<IImageElement> element, string det, string acq, string reg, ObservableCollection<XYStagePosition> xYStagePositions) 
        { 
            PlotColor = element.First().Color;
            LineRegions = element;
            AcquisitionName = acq;
            DetectorName = det;
            CollectionName = reg;

            AcqName = AcquisitionName;
            DetName = DetectorName;
            AcqDetDisplay = $"{AcqName}/{DetName}";

            AvailableStagePositions = xYStagePositions;
            PinnedPosition = AvailableStagePositions[0];
        }



        public override async Task UpdateImageElements(byte[] imagedata, int imwidth, int imheight, XYStagePosition xy, double time)
        {
            foreach(LineImageElement lineRegion in LineRegions)
            {
                List<double> lineIntensity = await lineRegion.ComputeValue(imagedata, (uint)imwidth, (uint)imheight);
                OnPlotsUpdated?.Invoke(lineIntensity,  lineRegion, xy);
            }
        }



        partial void OnPinnedPositionChanged(XYStagePosition value)
        {
            OnPositionChanged?.Invoke(value);
        }
    }
}
