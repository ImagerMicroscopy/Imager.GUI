using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.Services;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views.ViewUtils;
using System;

namespace ImagerAvalonia.Views;

public partial class SmartUpdateAcquisitionView : UserControl
{
    public SmartUpdateAcquisitionView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.Contains("ImagerAvalonia.MeasurementElementViewModel")
            && e.Data.Get("ImagerAvalonia.MeasurementElementViewModel") is MeasurementElementViewModel)
        {
            e.DragEffects = DragDropEffects.Move;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains("ImagerAvalonia.MeasurementElementViewModel"))
        {
            return;
        }

        if (e.Data.Get("ImagerAvalonia.MeasurementElementViewModel") is not MeasurementElementViewModel draggedNode)
        {
            return;
        }

        if (DataContext is not SmartUpdateAcquisitionFunctionViewModel vm)
        {
            return;
        }

        vm.SelectedNode = draggedNode;
        draggedNode.OnNodeDeleted += vm.DraggedNode_OnNodeDeleted; ;

        e.DragEffects = DragDropEffects.Move;
    }



    private void OnClearSelectionClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SmartUpdateAcquisitionFunctionViewModel vm)
        {
            vm.RemoveExperimentBindings();
        }
    }
}