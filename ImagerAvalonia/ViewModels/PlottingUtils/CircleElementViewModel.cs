using Autofac;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Views;
using ImagerAvalonia.Views.ImageViews;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels
{

    public abstract partial class ElementPlotViewModel : ViewModelBase
    {
        public abstract string AcquisitionName { get; set; }
        public abstract string DetectorName { get; set; }
        public abstract string CollectionName { get; set; }
        public abstract Task UpdateImageElements(byte[] imagedata, int imwidth, int imheight, XYStagePosition xy, double time);
    }


    public enum PlotTypes
    {
        Min,
        Max,
        Mean,
    }




    public partial class CircleElementViewModel : ElementPlotViewModel
    {
        public ObservableCollection<string> Plots { get; } =
            new ObservableCollection<string>(Enum.GetNames(typeof(PlotTypes)));

        public event Action<uint,uint,double,double, IImageElement, XYStagePosition>? OnPlotsUpdated;
        public event Action<string>? OnPlotTypeChanged;
        public event Action<XYStagePosition>? OnPositionChanged;

        public override string AcquisitionName { get ; set ; }
        public override string DetectorName { get; set; }
        public override string CollectionName { get; set; }




        public Color PlotColor;
        public List<IImageElement> CircleRegions;

        [ObservableProperty] string _selectedPlotType = "Mean";
        [ObservableProperty] string _detName;
        [ObservableProperty] string _acqName;
        [ObservableProperty] string _acqDetDisplay;
        [ObservableProperty] private XYStagePosition _PinnedPosition;
        public ObservableCollection<XYStagePosition> AvailableStagePositions { get; }

        public CircleElementViewModel(List<IImageElement> element, string det, string acq, string reg, ObservableCollection<XYStagePosition> xYStagePositions) 
        { 
            PlotColor = element.First().Color;
            CircleRegions = element;
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
            foreach(CircleImageElement circleRegion in CircleRegions)
            {
                (var minval, var maxval, var meanval) = await circleRegion.ComputeValue(imagedata, (uint)imwidth, (uint)imheight);
                OnPlotsUpdated?.Invoke(minval, maxval, meanval,time, circleRegion, xy);
            }
        }

        partial void OnSelectedPlotTypeChanged(string value)
        {
            OnPlotTypeChanged?.Invoke(value);
        }

        partial void OnPinnedPositionChanged(XYStagePosition value)
        {
            OnPositionChanged?.Invoke(value);
        }
    }
}
