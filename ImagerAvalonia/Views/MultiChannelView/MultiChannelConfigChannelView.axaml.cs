using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.Services;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views.ViewUtils;
using System;

namespace ImagerAvalonia.Views;

public partial class MultiChannelConfigChannelView : ExperimentSelector
{
    public MultiChannelConfigChannelView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void ExpPanel_DetectionDoubleTapped(object? sender, Services.NodeBase e)
    {
        expPanel.DetectionDoubleTapped -= ExpPanel_DetectionDoubleTapped;
        if (DataContext is MultiChannelConfigChannelViewModel vm && e is ActionNode actionNode)
        {
            vm.SelectedDetection = actionNode.NodeViewModel;
            vm.SelectedNode = actionNode;
            e.OnNodeDeleted += vm.NodeDeleted;
        }
        window.Close();

    }

}