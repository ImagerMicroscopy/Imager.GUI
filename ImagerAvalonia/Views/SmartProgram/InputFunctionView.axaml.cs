
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.Services;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views.ViewUtils;
using System;


namespace ImagerAvalonia.Views;







public partial class InputFunctionView : UserControl
{

    //private InputParameterViewModel _selectedParameter;


    public InputFunctionView()
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
            e.DragEffects = DragDropEffects.Move; // must match source's offered effect
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

        if (sender is not Border border || border.DataContext is not InputParameterViewModel parameter)
        {
            return;
        }

        parameter.SelectedDetection = draggedNode;
        parameter.SelectedNode = draggedNode;
        draggedNode.OnNodeDeleted += parameter.NodeDeleted;

        e.DragEffects = DragDropEffects.Move;
    }

    private void OnClearDropClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not InputParameterViewModel parameter)
        {
            return;
        }

        if (parameter.SelectedNode is MeasurementElementViewModel node)
        {
            node.OnNodeDeleted -= parameter.NodeDeleted;
        }

        parameter.SelectedDetection = null;
        parameter.SelectedNode = null;
    }
}