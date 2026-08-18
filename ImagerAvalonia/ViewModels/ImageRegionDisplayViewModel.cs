
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using ImagerAvalonia.Views;
using Autofac;
using ImagerAvalonia.Services.MeasurementControl;


namespace ImagerAvalonia.ViewModels;





public partial class ImageRegionDisplayViewModel : ViewModelBase
{

    [ObservableProperty] private string? _selectedAcq;
    [ObservableProperty] private string? _selectedDet;
    [ObservableProperty] private string? _selectedRegion;

    public event Action? ClearPlots;
    public event Action<string, string, ElementPlotViewModel>? InitiatePlot;
    public event Action<int>? UpdateNumberOfRowsInGrid;

    public event Action<string>? RemovePlot;
    public event EventHandler<List<IImageElement>>? RegionsChanged;
    public event EventHandler? HideAllCollectionsRequested;


    public List<IImageElement> RegionParameters = new();

    [ObservableProperty] private ObservableCollection<string> _collectionNames = new();
    [ObservableProperty] private ObservableCollection<string> _detectors = new();
    [ObservableProperty] private ObservableCollection<string> _acquisitions = new();
    private ObservableCollection<XYStagePosition>? _stagePositions;


    public ImageRegionDisplayViewModel()
    {
        CollectionNames.CollectionChanged += CollectionNames_CollectionChanged;
    }

    private void CollectionNames_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateNumberOfRowsInGrid?.Invoke(CollectionNames.Count);
    }


    public void DeleteCollection()
    {
        if (string.IsNullOrWhiteSpace(SelectedRegion)) return;

        if (CollectionNames.Contains(SelectedRegion))
        {
            RegionParameters.RemoveAll(x => x.RegionParameterName == SelectedRegion);
            RegionsChanged?.Invoke(this, RegionParameters);
            RemovePlot?.Invoke(SelectedRegion);
            CollectionNames.Remove(SelectedRegion);
        }
    }

    public void HideAllCollections()
    {
        HideAllCollectionsRequested?.Invoke(this, new EventArgs());
    }


    public void SetPlotParams()
    {

        ClearPlots?.Invoke();
        foreach (var collectionname in CollectionNames)
        {
            foreach (var acquisition in Acquisitions)
            {
                foreach (var detector in Detectors)
                {
                    var selected_points = RegionParameters.Where(x => x.RegionParameterName == collectionname).ToList();
                    if (selected_points.Count > 0)
                    {
                        ElementPlotViewModel collection_vm = selected_points.First().GenerateRegionPlotControl(selected_points, detector, acquisition, collectionname, _stagePositions);
                        InitiatePlot?.Invoke(acquisition, detector, collection_vm);
                    }
                }
            }
        }
    }


    internal void AddRegions(List<IImageElement> regionParameters)
    {
        if(regionParameters.Count>0)
        {
            var regionName = regionParameters[0].RegionParameterName;
            if (!CollectionNames.Contains(regionName))
                CollectionNames.Add(regionName);
            RegionParameters.AddRange(regionParameters);
            RegionsChanged?.Invoke(this, RegionParameters);


        }
    }

    internal void SetStagePositions(List<XYStagePosition> experimentPositions)
    {
        _stagePositions = new ObservableCollection<XYStagePosition>(experimentPositions);
    }
}

