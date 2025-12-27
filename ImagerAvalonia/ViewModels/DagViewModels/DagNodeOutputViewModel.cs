
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace ImagerAvalonia.ViewModels;

public partial class DagNodeOutputViewModel : ViewModelBase
{
    public readonly Guid parent_node;

    [ObservableProperty] string _displayedOutputType = string.Empty;
    public List<DagNodeInputViewModel> OutputTarget { get; private set; } = new();


    public DagNodeOutputViewModel(Guid id, NodeOutput nodeOutput)
    {
        parent_node = id;

        DisplayedOutputType = nodeOutput.Datatype;
        
    }

    public bool SetOutputTarget(DagNodeInputViewModel outputTarget)
    {
        if (!OutputTarget.Contains(outputTarget))
        {
            OutputTarget.Add(outputTarget);
            //this.OutputTarget = outputTarget;
            return true;
        }
        
        return false;
        
    }

    public void RemoveOutputTargets()
    {

        foreach (var target in OutputTarget)
        {
            target.SetInputTarget(null);

        }
        OutputTarget.Clear();
    }

    public void RemoveOutputTarget(DagNodeInputViewModel outputTarget)
    {
        OutputTarget.Remove(outputTarget);
    }


}

