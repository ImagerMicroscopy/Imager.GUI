using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;


namespace ImagerAvalonia.ViewModels;



public partial class ExperimentalPanelViewModel :  ViewModelBase
{
    public List<int[]> AcqDetDetectionsList = new();
    public List<AcqDetPair> AcqDetPairs = new();

    public ObservableCollection<NodeBase> Items { get; set; }
    public SystemDefinedSettingsViewModel AcquisitionSettings;
    [ObservableProperty] public string? _ExperimentName;
    [ObservableProperty] public ViewModelBase _ContentPane;
    [ObservableProperty] public NodeBase _SelectedTreeItem;
    [ObservableProperty] public RootNode _Root;
    [ObservableProperty] public bool _AreSourcesAvailable = true;
    [ObservableProperty] public bool _AreStagesAvailable = true;



   
    public List<EnabledAcquisition> EnabledAcquisitions = new();
    private readonly INodeFactory _nodeFactory = new NodeFactory();
    public readonly EnabledAcquisitionTracker AcquisitionTracker = new();

    public ExperimentalPanelViewModel(SystemDefinedSettingsViewModel user_acq,  IStageControl stageControl)
    {

        Root = new RootNode();

        if (user_acq.Acquisitions.ToList().First().Sources.Count==0)
        {
            AreSourcesAvailable = false;
        }

        if (stageControl.StageName==null)
        {
            AreStagesAvailable = false;
        }

        AcquisitionSettings = user_acq;
        Root.UserAcquisitionSettings = AcquisitionSettings;
        ObservableCollection<NodeBase> init_nodes = new ObservableCollection<NodeBase>() { Root };
        Items = init_nodes;
        SelectedTreeItem =  Root;
        ContentPane = Root.NodeViewModel;


    }


    public void SetExpNodes(ObservableCollection<NodeBase> nodes)
    { 
        Items = nodes;
        if (Items.Count > 0 && Items[0] is RootNode root)
        {
            Root = root; //Assumes first node is always root
        }
    }

    public void SetAcquisitions(ObservableCollection<AcquisitionSettingsViewModel> acquisitions)
    {
        foreach(var acq in acquisitions)
        {
            EnabledAcquisitions.Add( new EnabledAcquisition(true, acq) );
        }
        AcquisitionTracker.EnabledAcquisitions.AddRange(EnabledAcquisitions);
    }


    public string GetStoragePath()
    {
        if (this.Items[0].NodeViewModel is RootPanelViewModel root_vm)
        {
            RootPanelViewModel main_storage_directory =root_vm;
            return System.IO.Path.Combine(main_storage_directory.GetOutputFolder(), $"{main_storage_directory.GetUniqueFileName()}.tif");

        }
        else
        {
            throw new Exception("Could not get storage path");
        }
    }

    public void DeleteNode()
    {
        if (SelectedTreeItem != null)
        {
            SelectedTreeItem.DeleteExperiment(null, true);
        }
    }

    public void AddNode(string? elementName)
    {

        NodeBase node = _nodeFactory.CreateChildNodeOfType(elementName==null ? string.Empty : elementName , Root.UserAcquisitionSettings, Root);
        if(node.NodeViewModel is AcquisitionPanelViewModel acquisition_vm)
        {
            acquisition_vm.SetAcquisitionTracker(AcquisitionTracker);
        }
        Root.AppendNextItemToNode(node);

    }



    public override void Dispose()
    {
        Root.Dispose();
    }
}


