using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;
using System;
using System.ComponentModel;

namespace ImagerAvalonia.Views;

public partial class ExperimentalPanelViewControl : UserControl
{
    private const string DragFormat = "ImagerAvalonia.MeasurementElementViewModel";

    public string ExperimetPanelName { get; set; }

    private Point _pointerPressedPosition;
    private Border? _pressedBorder;
    private MeasurementElementViewModel? _draggedNode;

    // Tracks which node's Border currently has the "selected" class applied.
    private Border? _selectedNodeBorder;

    public event EventHandler<MeasurementElementViewModel>? DetectionDoubleTapped;

    public ExperimentalPanelViewControl(ExperimentalPanelViewModel expViewModel)
    {
        DataContext = expViewModel;

        ExperimentalPanelViewModel dt = (ExperimentalPanelViewModel)DataContext;
        dt.PropertyChanged += OnSelectionChanged;

        InitializeComponent();
    }

    public ExperimentalPanelViewControl()
    {
        InitializeComponent();
        DataContextChanged += ExperimentalPanelViewControl_DataContextChanged;
    }

    private void ExperimentalPanelViewControl_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ExperimentalPanelViewModel dt)
        {
            dt.PropertyChanged += OnSelectionChanged;
        }
    }

    // Begins a drag gesture when the user presses and moves over a tree node's Border.
    public void OnNodePointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (sender is not Border border || border.DataContext is not MeasurementElementViewModel node)
        {
            return;
        }

        // --- Selection highlight ---
        _selectedNodeBorder?.Classes.Remove("selected");
        border.Classes.Add("selected");
        _selectedNodeBorder = border;
        // ---------------------------

        _pressedBorder = border;
        _pointerPressedPosition = e.GetPosition(border);
    }

    public async void OnNodePointerMoved(object sender, PointerEventArgs e)
    {
        if (_pressedBorder is null || _pressedBorder != sender)
        {
            return;
        }

        if (!e.GetCurrentPoint(_pressedBorder).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var position = e.GetPosition(_pressedBorder);
        if (Point.Distance(position, _pointerPressedPosition) < 4)
        {
            return;
        }

        var border = _pressedBorder;
        _pressedBorder = null;

        if (border.DataContext is not MeasurementElementViewModel node)
        {
            return;
        }

        _draggedNode = node;

        var dragData = new DataObject();
        dragData.Set(DragFormat, node);

        border.Cursor = new Cursor(StandardCursorType.Hand);
        await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy | DragDropEffects.Move);
        border.Cursor = Cursor.Default;

        _draggedNode = null;
    }



    public void OnNodeDragOver(object sender, DragEventArgs e)
    {
        if (!TryGetDropTarget(sender, e, out var destNode))
        {
            e.DragEffects = DragDropEffects.None;
            HighlightDropTarget(null);
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        HighlightDropTarget(sender as Border);
    }

    public void OnNodeDragLeave(object sender, RoutedEventArgs e)
    {
        HighlightDropTarget(null);
    }

    public void OnNodeDrop(object sender, DragEventArgs e)
    {
        HighlightDropTarget(null);

        if (!TryGetDropTarget(sender, e, out var destNode))
        {
            return;
        }

        MoveNode(_draggedNode!, destNode);
    }

    // Drop zone rendered as a thin strip above each node; dropping here inserts the
    // dragged node as a sibling immediately before the node instead of nesting into it.
    public void OnInsertBeforeDragOver(object sender, DragEventArgs e)
    {
        if (!TryGetInsertBeforeTarget(sender, e, out _))
        {
            e.DragEffects = DragDropEffects.None;
            HighlightInsertStrip(null);
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        HighlightInsertStrip(sender as Control);
    }

    public void OnInsertBeforeDragLeave(object sender, RoutedEventArgs e)
    {
        HighlightInsertStrip(null);
    }

    public void OnInsertBeforeDrop(object sender, DragEventArgs e)
    {
        HighlightInsertStrip(null);

        if (!TryGetInsertBeforeTarget(sender, e, out var destNode))
        {
            return;
        }

        InsertBefore(_draggedNode!, destNode);
    }

    private bool TryGetInsertBeforeTarget(object sender, DragEventArgs e, out MeasurementElementViewModel destNode)
    {
        destNode = null!;

        if (!e.Data.Contains(DragFormat) || e.Data.Get(DragFormat) is not MeasurementElementViewModel draggedNode)
        {
            return false;
        }

        if (sender is not Control strip || strip.DataContext is not MeasurementElementViewModel candidate)
        {
            return false;
        }

        if (candidate is RootNode || candidate == draggedNode || candidate.Parent == draggedNode)
        {
            return false;
        }

        if (CheckIfNodeIsParentOfChild(draggedNode, candidate))
        {
            return false;
        }

        destNode = candidate;
        return true;
    }

    private Control? _highlightedStrip;

    private void HighlightInsertStrip(Control? strip)
    {
        if (_highlightedStrip == strip)
        {
            return;
        }

        if (_highlightedStrip != null)
        {
            _highlightedStrip.Opacity = 0;
        }

        if (strip != null)
        {
            strip.Opacity = 1;
        }

        _highlightedStrip = strip;
    }

    private void InsertBefore(MeasurementElementViewModel draggedNode, MeasurementElementViewModel destNode)
    {
        var oldParent = draggedNode.Parent;
        var newParent = destNode.Parent;

        oldParent.Children.Remove(draggedNode);
        int insertIndex = newParent.Children.IndexOf(destNode);

        newParent.Children.Insert(insertIndex, draggedNode);
        draggedNode.Parent = newParent;
    }

    private bool TryGetDropTarget(object sender, DragEventArgs e, out MeasurementElementViewModel destNode)
    {
        destNode = null!;

        if (!e.Data.Contains(DragFormat) || e.Data.Get(DragFormat) is not MeasurementElementViewModel draggedNode)
        {
            return false;
        }

        if (sender is not Border border || border.DataContext is not MeasurementElementViewModel candidate)
        {
            return false;
        }

        if (candidate == draggedNode)
        {
            return false;
        }

        if (CheckIfNodeIsParentOfChild(draggedNode, candidate))
        {
            return false;
        }

        destNode = candidate;
        return true;
    }

    private Border? _highlightedBorder;

    private void HighlightDropTarget(Border? border)
    {
        if (_highlightedBorder == border)
        {
            return;
        }

        if (_highlightedBorder != null)
        {
            // IMPORTANT: clear the local value instead of setting it back to a
            // hardcoded color. Setting it (even to the "default" color) leaves a
            // local value on this Border forever, which will permanently outrank
            // any style setter (including the .selected class) for this element.
            _highlightedBorder.ClearValue(Border.BorderBrushProperty);
        }

        if (border != null)
        {
            border.BorderBrush = new SolidColorBrush(Color.Parse("#ad76ea"));
        }

        _highlightedBorder = border;
    }

    private void SwapNodes(MeasurementElementViewModel draggedNode, MeasurementElementViewModel destNode)
    {
        var draggedParent = draggedNode.Parent;
        var destParent = destNode.Parent;

        if (draggedParent == destParent)
        {
            var siblings = draggedParent.Children;
            int draggedIndex = siblings.IndexOf(draggedNode);
            int destIndex = siblings.IndexOf(destNode);
            (siblings[draggedIndex], siblings[destIndex]) = (siblings[destIndex], siblings[draggedIndex]);
            return;
        }

        int draggedInd = draggedParent.Children.IndexOf(draggedNode);
        int destInd = destParent.Children.IndexOf(destNode);

        draggedParent.Children[draggedInd] = destNode;
        destParent.Children[destInd] = draggedNode;

        destNode.Parent = draggedParent;
        draggedNode.Parent = destParent;
    }

    private void MoveNode(MeasurementElementViewModel draggedNode, MeasurementElementViewModel destNode)
    {
        if (destNode is RootNode)
        {
            if (!draggedNode.MeasurementElement.CanHaveChildren() && destNode.MeasurementElement.CanHaveChildren())
            {
                draggedNode.Parent.Children.Remove(draggedNode);
                destNode.Children.Add(draggedNode);
                draggedNode.Parent = destNode;
            }
            return;
        }

        bool destCanHaveChildren = destNode.MeasurementElement.CanHaveChildren();
        bool draggedCanHaveChildren = draggedNode.MeasurementElement.CanHaveChildren();

        if ((destCanHaveChildren && !draggedCanHaveChildren) || (destCanHaveChildren && draggedCanHaveChildren))
        {
            draggedNode.Parent.Children.Remove(draggedNode);
            destNode.Children.Add(draggedNode);
            draggedNode.Parent = destNode;
        }
        else if (!destCanHaveChildren && !draggedCanHaveChildren)
        {
            SwapNodes(draggedNode, destNode);
        }
    }

    private bool CheckIfNodeIsParentOfChild(MeasurementElementViewModel node, MeasurementElementViewModel child)
    {
        foreach (MeasurementElementViewModel child_node in node.Children)
        {
            if (child_node == child || CheckIfNodeIsParentOfChild(child_node, child))
            {
                return true;
            }
        }
        return false;
    }

    public void OnElementDoubleTapped(object sender, TappedEventArgs e)
    {
        if (sender is Border border && border.DataContext is MeasurementElementViewModel dest_node)
        {
            DetectionDoubleTapped?.Invoke(this, dest_node);
        }
    }

    public void OnSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
    }

    private void IncreaseValue(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.TemplatedParent is NumericUpDown numericUpDown)
        {
            if (numericUpDown.Value == null)
            {
                numericUpDown.Value = numericUpDown.Minimum;
            }
            else
            {
                numericUpDown.Value = Math.Min((decimal)numericUpDown.Value + (decimal)numericUpDown.Increment, (decimal)numericUpDown.Maximum);
            }
        }
    }

    private void DecreaseValue(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.TemplatedParent is NumericUpDown numericUpDown)
        {
            if (numericUpDown.Value == null)
            {
                numericUpDown.Value = numericUpDown.Minimum;
            }
            else
            {
                numericUpDown.Value = Math.Max((decimal)numericUpDown.Value - (decimal)numericUpDown.Increment, (decimal)numericUpDown.Minimum);
            }
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}