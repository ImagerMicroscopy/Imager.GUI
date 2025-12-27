using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views.ViewUtils;
using System;

namespace ImagerAvalonia.Views;

public partial class SmartUpdateAcquisitionView : ExperimentSelector
{
    public SmartUpdateAcquisitionView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void ExpPanel_DetectionDoubleTapped(object? sender, Services.NodeBase e)
    {
        if (DataContextSender is SmartUpdateAcquisitionFunctionViewModel updateAcquisitionVM)
        {
            expPanel.DetectionDoubleTapped -= ExpPanel_DetectionDoubleTapped;
            window.Close();

            if ( e is ActionNode actionNode && e.MeasurementType is UpdateAcquisition)
            {
                updateAcquisitionVM.SelectedNode = actionNode;
                e.OnNodeDeleted += updateAcquisitionVM.NodeDeleted;
            }
        }
    }
}