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
        try
        {
            await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy | DragDropEffects.Move);
        }
        catch (Exception)
        {
            // A failed drag must never take the application down.
        }
        finally
        {
            border.Cursor = Cursor.Default;
        }

        _draggedNode = null;
    }

    // =========================
    // Drag and drop
    //
    // Every node card is a single drop target with three zones: dragging over
    // the top quarter of the card inserts the dragged node before it, the
    // bottom quarter inserts it after it, and the middle nests it inside (for
    // containers). Leaves cannot contain anything, so their whole lower part
    // means "insert after". The root only accepts nesting.
    // =========================

    private enum DropZone
    {
        InsertBefore,
        InsertAfter,
        Nest
    }

    public void OnNodeDragOver(object sender, DragEventArgs e)
    {
        if (!TryGetDrop(sender, e, out var card, out var draggedNode, out var destNode, out var zone)
            || !IsValidDrop(draggedNode, destNode, zone))
        {
            e.DragEffects = DragDropEffects.None;
            ShowDropFeedback(null, DropZone.Nest);
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        ShowDropFeedback(card, zone);
    }

    public void OnNodeDragLeave(object sender, RoutedEventArgs e)
    {
        ShowDropFeedback(null, DropZone.Nest);
    }

    public void OnNodeDrop(object sender, DragEventArgs e)
    {
        ShowDropFeedback(null, DropZone.Nest);

        if (!TryGetDrop(sender, e, out var card, out var draggedNode, out var destNode, out var zone)
            || !IsValidDrop(draggedNode, destNode, zone))
        {
            return;
        }

        switch (zone)
        {
            case DropZone.InsertBefore:
                InsertBefore(draggedNode, destNode);
                break;
            case DropZone.InsertAfter:
                InsertAfter(draggedNode, destNode);
                break;
            default:
                NestInto(draggedNode, destNode);
                break;
        }
    }

    private bool TryGetDrop(object? sender, DragEventArgs e,
        out Border card, out MeasurementElementViewModel draggedNode,
        out MeasurementElementViewModel destNode, out DropZone zone)
    {
        card = null!;
        draggedNode = null!;
        destNode = null!;
        zone = DropZone.Nest;

        if (!e.Data.Contains(DragFormat) || e.Data.Get(DragFormat) is not MeasurementElementViewModel dragged)
        {
            return false;
        }

        if (sender is not Border border || border.DataContext is not MeasurementElementViewModel dest)
        {
            return false;
        }

        card = border;
        draggedNode = dragged;
        destNode = dest;
        zone = GetDropZone(border, e, dest);
        return true;
    }

    private static DropZone GetDropZone(Border card, DragEventArgs e, MeasurementElementViewModel destNode)
    {
        if (destNode is RootNode)
        {
            return DropZone.Nest;
        }

        double relativeY = e.GetPosition(card).Y / card.Bounds.Height;

        if (!destNode.CanHaveChildren)
        {
            // Leaves cannot nest anything, so only the top quarter means "before".
            return relativeY < 0.25 ? DropZone.InsertBefore : DropZone.InsertAfter;
        }

        if (relativeY < 0.25)
        {
            return DropZone.InsertBefore;
        }

        if (relativeY > 0.75)
        {
            return DropZone.InsertAfter;
        }

        return DropZone.Nest;
    }

    private static bool IsValidDrop(MeasurementElementViewModel draggedNode, MeasurementElementViewModel destNode, DropZone zone)
    {
        if (draggedNode == destNode)
        {
            return false;
        }

        // Never allow dropping a node into its own subtree, in any zone.
        if (CheckIfNodeIsParentOfChild(draggedNode, destNode))
        {
            return false;
        }

        if (zone == DropZone.Nest)
        {
            return destNode.CanHaveChildren;
        }

        // Inserting before or after requires the target to sit in a parent list.
        return destNode.Parent != null;
    }

    private Border? _highlightedCard;
    private Border? _highlightedBeforeLine;
    private Border? _highlightedAfterLine;

    // Draws the drop feedback: an accent border for nesting, or a purple
    // insertion line at the top or bottom edge of the hovered card for sibling
    // positions. A null card clears any previous feedback.
    private void ShowDropFeedback(Border? card, DropZone zone)
    {
        if (_highlightedCard != null)
        {
            // IMPORTANT: clear the local value instead of setting it back to a
            // hardcoded color. Setting it (even to the "default" color) leaves a
            // local value on this Border forever, which will permanently outrank
            // any style setter (including the selected-card style) for this element.
            _highlightedCard.ClearValue(Border.BorderBrushProperty);
            _highlightedCard = null;
        }

        if (_highlightedBeforeLine != null)
        {
            _highlightedBeforeLine.Opacity = 0;
            _highlightedBeforeLine = null;
        }

        if (_highlightedAfterLine != null)
        {
            _highlightedAfterLine.Opacity = 0;
            _highlightedAfterLine = null;
        }

        if (card == null)
        {
            return;
        }

        switch (zone)
        {
            case DropZone.Nest:
                card.BorderBrush = new SolidColorBrush(Color.Parse("#ad76ea"));
                _highlightedCard = card;
                break;

            case DropZone.InsertBefore:
                _highlightedBeforeLine = FindInsertLine(card, "InsertBeforeLine");
                if (_highlightedBeforeLine != null)
                {
                    _highlightedBeforeLine.Opacity = 1;
                }
                break;

            case DropZone.InsertAfter:
                _highlightedAfterLine = FindInsertLine(card, "InsertAfterLine");
                if (_highlightedAfterLine != null)
                {
                    _highlightedAfterLine.Opacity = 1;
                }
                break;
        }
    }

    private static Border? FindInsertLine(Border card, string name)
    {
        if (card.Parent is not Grid grid)
        {
            return null;
        }

        foreach (var child in grid.Children)
        {
            if (child is Border b && b.Name == name)
            {
                return b;
            }
        }

        return null;
    }

    // =========================
    // Move operations (all through ExperimentBuilder.MoveNode, which validates
    // ancestors, container capability and index bounds)
    // =========================

    private void NestInto(MeasurementElementViewModel draggedNode, MeasurementElementViewModel destNode)
    {
        if (draggedNode.FindRoot() is not RootNode root || root.Builder is not { } builder)
        {
            return;
        }

        // Nest at the end. The builder counts the index against the list after
        // the dragged node is removed, so a node that already lives in this
        // container appends at Count - 1 instead of Count.
        int appendIndex = destNode.Children.Count - (draggedNode.Parent == destNode ? 1 : 0);
        builder.MoveNode(draggedNode, destNode, appendIndex);
    }

    private void InsertBefore(MeasurementElementViewModel draggedNode, MeasurementElementViewModel destNode)
    {
        if (draggedNode.FindRoot() is not RootNode root || root.Builder is not { } builder)
        {
            return;
        }

        var newParent = destNode.Parent!;
        int insertIndex = newParent.Children.IndexOf(destNode);

        // The builder validates the index against the list as it will look after
        // the dragged node is removed, so a node taken from before the target
        // shifts the index down by one.
        if (draggedNode.Parent == newParent && newParent.Children.IndexOf(draggedNode) < insertIndex)
        {
            insertIndex--;
        }

        builder.MoveNode(draggedNode, newParent, insertIndex);
    }

    private void InsertAfter(MeasurementElementViewModel draggedNode, MeasurementElementViewModel destNode)
    {
        if (draggedNode.FindRoot() is not RootNode root || root.Builder is not { } builder)
        {
            return;
        }

        var newParent = destNode.Parent!;
        int insertIndex = newParent.Children.IndexOf(destNode) + 1;

        // Same post-removal adjustment as InsertBefore.
        if (draggedNode.Parent == newParent && newParent.Children.IndexOf(draggedNode) < newParent.Children.IndexOf(destNode))
        {
            insertIndex--;
        }

        builder.MoveNode(draggedNode, newParent, insertIndex);
    }

    private static bool CheckIfNodeIsParentOfChild(MeasurementElementViewModel node, MeasurementElementViewModel child)
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
