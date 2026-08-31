using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.Services;
using ImagerAvalonia.ViewModels;

namespace ImagerAvalonia.Views;

public partial class MultiChannelConfigChannelView : UserControl
{
    public MultiChannelConfigChannelView()
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

        if (sender is not Border border || border.DataContext is not MultiChannelConfigChannelViewModel channel)
        {
            return;
        }

        if (draggedNode is DetectionElementViewModel detectionNode)
        {
            channel.SelectedDetection = detectionNode;
            channel.SelectedNode = detectionNode;
            detectionNode.OnNodeDeleted += channel.NodeDeleted;
        }
        e.DragEffects = DragDropEffects.Move;
    }

    private void OnClearDropClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not MultiChannelConfigChannelViewModel channel)
        {
            return;
        }

        if (channel.SelectedNode is MeasurementElementViewModel node)
        {
            node.OnNodeDeleted -= channel.NodeDeleted;
        }

        channel.SelectedDetection = null;
        channel.SelectedNode = null;
    }
}
