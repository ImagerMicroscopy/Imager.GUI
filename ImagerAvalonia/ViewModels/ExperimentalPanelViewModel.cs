using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ImagerAvalonia.ViewModels;

public partial class ExperimentalPanelViewModel : ViewModelBase
{
    private readonly ExperimentBuilder _experimentBuilder;
    public LoadContext? ReferenceContext { get; set; }
    public ObservableCollection<MeasurementElementViewModel> Items { get; set; }
    public GlobalDefinedSettingsViewModel AcquisitionSettings;

    [ObservableProperty] public string? _ExperimentName;
    [ObservableProperty] public MeasurementElementViewModel _ContentPane;
    [ObservableProperty] public MeasurementElementViewModel _SelectedTreeItem;
    [ObservableProperty] public RootNode _Root;
    [ObservableProperty] public bool _AreSourcesAvailable = true;
    [ObservableProperty] public bool _AreStagesAvailable = true;

    public AcquisitionDetectorTracker AcqDetTracker { get; } = new();
    public ObservableCollection<EnabledAcquisition> EnabledAcquisitions { get; } = new();
    //public readonly EnabledAcquisitionTracker AcquisitionTracker = new();

    public ExperimentalPanelViewModel(
        GlobalDefinedSettingsViewModel user_acq,
        IStageControl stageControl,
        ExperimentBuilder experimentBuilder)
    {
        _experimentBuilder = experimentBuilder;
        Root = new RootNode
        {
            StorageService = experimentBuilder.StorageService
        };

        if (user_acq.Acquisitions.Any() && user_acq.Acquisitions.First().Sources.Count == 0)
        {
            AreSourcesAvailable = false;
        }

        if (stageControl.StageName == null)
        {
            AreStagesAvailable = false;
        }

        AcquisitionSettings = user_acq;
        Root.UserAcquisitionSettings = AcquisitionSettings;

        Items = new ObservableCollection<MeasurementElementViewModel> { Root };
        SelectedTreeItem = Root;
        ContentPane = Root;
    }


    public string GetStoragePath() => _experimentBuilder.StorageService.GetStoragePath();

    [RelayCommand]
    public void DeleteNode()
    {
        if (SelectedTreeItem?.Parent != null)
        {
            _experimentBuilder.RemoveNode(SelectedTreeItem);
        }
    }

    [RelayCommand]
    public void AddElementCommand(ExperimentElementType type)
    {
        _experimentBuilder.AddNode(type, Root);
    }

    /// <summary>Builds the MeasurementElement tree from this panel's Root and returns it.</summary>
    public MeasurementElementBase GetMeasurementElement()
    {
        return _experimentBuilder.BuildMeasurementProgram(Root);
    }

    public override void Dispose()
    {
        _experimentBuilder.Dispose();
        Root.Dispose();
    }
}


public class AcquisitionDetectorTracker
{
    public List<int[]> Detections { get; } = new();
    public List<Tuple<string, string>> Pairs { get; } = new();
}