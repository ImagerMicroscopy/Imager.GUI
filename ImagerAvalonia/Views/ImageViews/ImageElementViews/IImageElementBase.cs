using Avalonia.Controls;
using Avalonia.Input;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;
using ScottPlot;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ImagerAvalonia.Views
{
    public abstract partial class ImageElementControlBase : Control
    {
        public abstract Control AddedVisualType { get; protected set; }
        public abstract List<Control> AddedRegions { get; protected set; }
        public abstract string RegionName { get; protected set; }

        public abstract void OnDotPointerMoved(object? sender, PointerEventArgs e);
        public abstract void ElementKeyDown(object? sender, KeyEventArgs e);
        public abstract void OnPointerPressed(object? sender, PointerPressedEventArgs e);

        public abstract List<IImageElement> RetrieveRegionParameters();
        internal abstract void SetRegionName(string name);
    }

    public interface IImageElement
    {
        //double X { get; }
        //double Y { get; }
        //double CanvasX { get; }
        //double CanvasY { get; }
        Avalonia.Media.Color Color { get; }
        string RegionParameterName { get; }

        List<IPlottable> RetrievePlotControls();
        Control GenerateRegionControl();
        ElementPlotViewModel GenerateRegionPlotControl(List<IImageElement> image_elements, string acq, string det, string reg, ObservableCollection<XYStagePosition> stagePositions);
    }
}
