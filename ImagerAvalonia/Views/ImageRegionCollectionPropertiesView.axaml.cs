using Autofac;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ImagerAvalonia.Views;




public partial class ImageRegionCollectionPropertiesView : UserControl
{
    private ImageElementCollectionsView _imageElementCollectionsView;
    private List<ElementPlotViewModel> elementPlotViewModels = new();
    private Channel<ImageProcessingJob>? _channel;
    private CancellationTokenSource _cts = new();
    private ImageRegionDisplayViewModel _viewModel; 

    public ImageRegionCollectionPropertiesView(ILifetimeScope scope)
    {

        InitializeComponent();
        _imageElementCollectionsView = new ImageElementCollectionsView();
        elementPlotViewModels = _imageElementCollectionsView.GetElementPlots().ToList();
        _viewModel = scope.Resolve<ImageRegionDisplayViewModel>();
       
        _viewModel.SetStagePositions(new List<XYStagePosition>() {IStageControl.DefaultStagePosition });
        _viewModel.ClearPlots += _viewModel_ClearPlots;
        _viewModel.InitiatePlot += OnPlotInitiationRequested;
        _viewModel.UpdateNumberOfRowsInGrid += OnUpdateNumberOfRowsInGrid;
        _viewModel.RemovePlot += _viewModel_RemovePlot;
        DataContext = _viewModel;
        InitializeChannelProcessor();

        //DataContextChanged += OnDataContextChanged;
    }

    private void _viewModel_ClearPlots()
    {
        _imageElementCollectionsView.ClearPlots();
    }

    private void OnUpdateNumberOfRowsInGrid(int num_rows)
    {
        _imageElementCollectionsView.SetGridRows(num_rows);
    }

    private void _viewModel_RemovePlot(string removedplot)
    {
        _imageElementCollectionsView.RemoveElementPlot(removedplot);
        elementPlotViewModels = elementPlotViewModels.Where(x => x.CollectionName != removedplot).ToList();
    }

    private void OnPlotInitiationRequested(string arg1, string arg2, ElementPlotViewModel arg3)
    {
        _imageElementCollectionsView.AddElementPlot(arg1, arg2, arg3);
        elementPlotViewModels.Add(arg3);

    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }

    internal void UpdateRegions(byte[] image_data, string acq, string det, int imwidth, int imheight,XYStagePosition xy, double timepoint)
    {
        foreach (var elementViewModel in elementPlotViewModels)
        {
            if(elementViewModel.AcquisitionName == acq && elementViewModel.DetectorName == det)
            {
                var job = new ImageProcessingJob
                {
                    ImageBytes = image_data,
                    Width = imwidth,
                    Height = imheight,
                    ElementVM = elementViewModel,
                    XY = xy,
                    Time = timepoint
                };

                _channel.Writer.TryWrite(job);
            }
        }
    }
    private void InitializeChannelProcessor()
    {
        var options = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _channel = Channel.CreateBounded<ImageProcessingJob> (options);
        
        Task.Run(() => ProcessChannelAsync(_cts.Token));
    }
    private async Task ProcessChannelAsync(CancellationToken token)
    {
        var reader = _channel.Reader;

        while (await reader.WaitToReadAsync(token))
        {
            while (reader.TryRead(out var job))
            {
                try
                {
                    await job.ElementVM.UpdateImageElements(job.ImageBytes, job.Width, job.Height, job.XY, job.Time);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to process image: exception caused by {ex.Message}");
                }
            }
        }
    }

    internal void SetXYitions(ObservableCollection<XYStagePosition> e)
    {
        _viewModel.SetStagePositions(e.ToList());
    }

    public class ImageProcessingJob
    {
        public byte[] ImageBytes { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public ElementPlotViewModel ElementVM {get; init;}
        public XYStagePosition XY { get; init;}
        public double Time { get; init; }
    }

    

}
