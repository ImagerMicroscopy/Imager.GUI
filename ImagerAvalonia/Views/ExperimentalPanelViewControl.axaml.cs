using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ImagerAvalonia.Services;
using ImagerAvalonia.ViewModels;
using System;

using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace ImagerAvalonia.Views;

public partial class ExperimentalPanelViewControl : UserControl
{
    public string ExperimetPanelName { get; set; }
    private bool _isDragging = false;
    private Control? _selected_visual;
    private Control? _selected_item;
    private NodeBase? _draggedNode;
    private Control? _draggedNodeVisual;

    private Cursor _openHand;
    private Cursor _closedHand;
    public event EventHandler<NodeBase>? DetectionDoubleTapped;

    public ExperimentalPanelViewControl(ExperimentalPanelViewModel expViewModel)
    {
        DataContext = expViewModel;
        
        ExperimentalPanelViewModel dt = (ExperimentalPanelViewModel)DataContext;
        dt.PropertyChanged += OnSelectionChanged;

        InitializeComponent();
        (_openHand, _closedHand) = CreateCursors();
    }



    public ExperimentalPanelViewControl()
    {
        InitializeComponent();
        DataContextChanged += ExperimentalPanelViewControl_DataContextChanged;

        (_openHand, _closedHand) = CreateCursors();
    }

    private static (Cursor open, Cursor closed) CreateCursors()
    {
        var bitmap_open = new Bitmap(AssetLoader.Open(new Uri("avares://ImagerAvalonia/Assets/cursors/cursor_hand_open_line_icon.png")));
        var bitmap_closed = new Bitmap(AssetLoader.Open(new Uri("avares://ImagerAvalonia/Assets/cursors/cursor_hand_grab_line_icon.png")));
        return (new Cursor(bitmap_open, new PixelPoint(10, 0)), new Cursor(bitmap_closed, new PixelPoint(10, 0)));
    }

    private void ExperimentalPanelViewControl_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ExperimentalPanelViewModel dt)
        {
            dt.PropertyChanged += OnSelectionChanged;
        }
    }


    public void PanelPointerPressed(object sender, PointerPressedEventArgs e)
    {
        this.Cursor = new Cursor(StandardCursorType.Arrow);
    }


    public void PanelPointerReleased(object sender, RoutedEventArgs e)
    {
        this.Cursor = new Cursor(StandardCursorType.Arrow);
        if(_selected_visual!=null)
        {
            _selected_visual.Cursor = _openHand;
        }    
        _isDragging = false;
        _draggedNodeVisual = null;
        
    }


    public void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
        {

            if (_selected_item is Border brdr)
            {
                brdr.BorderThickness = new Thickness(1);
                brdr.BorderBrush = new SolidColorBrush(Color.Parse("#2e2e2e"));

            }

            _isDragging = true;
            if (sender is Border txt)
            {
                e.Pointer.Capture(null);

                _selected_item = txt;

                if (_selected_item is Border newbrdr)
                {


                    newbrdr.BorderBrush = new SolidColorBrush(Color.Parse("#ad76ea"));
                    newbrdr.BorderThickness = new Thickness(1);

                }

                _draggedNodeVisual = txt;
                if (txt.Child !=null && txt.Child.DataContext is NodeBase node)
                {
                    _draggedNode = node;
                }
  
                _draggedNodeVisual.Cursor = _closedHand;
                this.Cursor = _closedHand;
            }

        }



    }

    public void OnPointerExitedActionNode(object sender, RoutedEventArgs e)
    {
        if (_selected_visual != null)
        {
            _selected_visual.Effect = null;
            _selected_visual.Cursor = new Cursor(StandardCursorType.Arrow);
        }
    }

    public void OnPointerExitedExperimentNode(object sender, PointerEventArgs e)
    {

        if (_selected_visual != null)
        {

            _selected_visual.Effect = null;

            if (_isDragging )
            {
      
                if (_selected_visual.Parent is StackPanel stck)
                {
                    AnimateWidth(_selected_visual, stck, _selected_visual.Height, 15, TimeSpan.FromMilliseconds(100));
                }
            }
            else
            {
                _selected_visual.Cursor = new Cursor(StandardCursorType.Arrow);
            }
        }
    }

    public void OnPointerEnteredActionNode(object sender, PointerEventArgs e)
    {
       
        if (_selected_visual != null)
        {
            _selected_visual.Effect = null;

        }
        _selected_visual = (Border)sender;



        if (_isDragging)
        {
            _selected_visual.Cursor = _closedHand;
            return;
        }
        _selected_visual.Cursor = _openHand;



    }

    public void OnPointerEnteredExperimentNode(object sender, PointerEventArgs e)
    {
        //TreeView sourceTree = this.FindControl<Avalonia.Controls.TreeView>("ExperimentTreeView");
        //var point = e.GetPosition(sourceTree);
        if (_draggedNodeVisual==sender)
        {
            return;
        }
        if (_selected_visual != null)
        {
            _selected_visual.Effect = null;
        }
        _selected_visual = (Border)sender;


        if (_isDragging )
        {
            if(_selected_visual.Parent is StackPanel stck)
            {
                stck.Height = 20;
            }
            _selected_visual.Height = 20;
            _selected_visual.Margin = new Thickness(0, -5, 0, 0);
            _selected_visual.Cursor = _closedHand;
            return;
            //AnimateWidth(_selected_visual, _selected_visual.Height, 35, TimeSpan.FromMilliseconds(50));
        }
        _selected_visual.Cursor = _openHand;

    }


    public void TreeViewPointerReleased(object sender, RoutedEventArgs e)
    {
        _isDragging = false;
    }

    public void AddAbovePointerEntered(object sender, PointerEventArgs e)
    {
        if(sender is StackPanel rect)
        {
            if (_isDragging)
            {
        
                
            }
        }
    }

    public void OnElementDoubleTapped(object sender, TappedEventArgs e)
    {
        TreeView sourceTree = this.FindControl<Avalonia.Controls.TreeView>("ExperimentTreeView")!;
        var point = e.GetPosition(sourceTree);

        var visual = sourceTree?.GetVisualAt(point);

        NodeBase dest_node = (NodeBase)visual.DataContext;

        DetectionDoubleTapped?.Invoke(this, dest_node);   
    }

    private async Task AnimateWidth(Control control, Control parentcontrol, double from, double to, TimeSpan duration)
    {
        var widthAnimation = new Animation
        {
            Duration = duration,
            FillMode = FillMode.Forward,
            Easing = new CubicEaseInOut(),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(Layoutable.HeightProperty, from) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(Layoutable.HeightProperty, to) }
                }
            }
        };
        await widthAnimation.RunAsync(control);
        parentcontrol.Height = 20;
        control.Margin = new Thickness(0, 0, 0, 0);
    }



    public void AddPointerExited(object sender, PointerEventArgs e)
    {
        if (sender is StackPanel rect && _isDragging)
        {
            rect.Opacity = 0;
        }
    }
    public void AddPointerMoved(object sender, PointerEventArgs e)
    {
        if (sender is StackPanel rect && _isDragging)
        {
            rect.Opacity = 1;
        }
    }

    public void OnRootPointerReleased(object sender, PointerReleasedEventArgs e)
    {

        _isDragging = false;

        this.Cursor = new Cursor(StandardCursorType.Arrow);
        _selected_visual.Cursor = _openHand;
        

    }

    public void OnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;

            if (sender is StackPanel rect )
            {
                rect.Opacity = 0;
            }

            TreeView sourceTree = this.FindControl<Avalonia.Controls.TreeView>("ExperimentTreeView")!;
            var point = e.GetPosition(sourceTree);

            var visual = sourceTree?.GetVisualAt(point);

            NodeBase dest_node = (NodeBase)visual.DataContext;
            if (_draggedNode != dest_node) 
            {
                if (!CheckIfNodeIsParentOfChild(_draggedNode, dest_node))
                {
                    int node_ind = dest_node.Parent.Children.IndexOf(dest_node);
                    _draggedNode.Parent.Children.Remove(_draggedNode);
                    dest_node.Parent.Children.Insert(node_ind, _draggedNode);
                    _draggedNode.Parent = dest_node.Parent;
                }
            }
            this.Cursor = new Cursor(StandardCursorType.Arrow);
            _selected_visual.Cursor = _openHand;
        }

    }

    private bool CheckIfNodeIsParentOfChild(NodeBase node, NodeBase child)
    {
        bool isfound = false;
        foreach(NodeBase child_node in node.Children)
        {
            if (!isfound)
            {
                if (child_node == child)
                {
                    isfound =  true;
                }
                else
                {
                    isfound = CheckIfNodeIsParentOfChild(child_node, child);
                }
            }
        }
        return isfound;
    }

    public void OnPointerReleasedAddWithin(object sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;


            TreeView sourceTree = this.FindControl<Avalonia.Controls.TreeView>("ExperimentTreeView")!;
            var point = e.GetPosition(sourceTree);

            var visual = sourceTree?.GetVisualAt(point);
            if (visual != null)
            {

                if (visual.DataContext is NodeBase dest_node)
                {
                    if (_draggedNode != dest_node)
                    {
                        if (sender is Border node_border)
                        {
                            AnimateWidth(node_border, node_border.Parent as Control, _selected_visual.Height, 15, TimeSpan.FromMilliseconds(100));

                        }


                        if (!CheckIfNodeIsParentOfChild(_draggedNode, dest_node))
                        {

                            _draggedNode.Parent.Children.Remove(_draggedNode);
                            dest_node.Children.Add(_draggedNode);
                            _draggedNode.Parent = dest_node;
                        }

                    }
                }
                this.Cursor = new Cursor(StandardCursorType.Arrow);
                _selected_visual.Cursor = _openHand;
            }
        }

    }

    private void OnAddElementClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            string? elementType = menuItem.Header as string;
            var context = DataContext as ExperimentalPanelViewModel;
            if (context != null)
            {
                context.AddNode(elementType);
            }
        }
    }



    public void OnSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {

        ExperimentalPanelViewModel dt = sender as ExperimentalPanelViewModel;
        TreeView s = this.FindControl<Avalonia.Controls.TreeView>("ExperimentTreeView")!;
        NodeBase selected_node = (NodeBase)s.SelectedItem;

        if (e.PropertyName == "SelectedTreeItem" )
        {
            //TreeView s = (TreeView)sender;
            if (s.SelectedItem != null)
            {

                    //dt.SelectedTreeItem = (INode)s.SelectedItem;
                dt.ContentPane = selected_node.NodeViewModel;
                

            }
            else
            {
                if (s.Items.Count > 0)
                {

                    NodeBase root_vm = (NodeBase)s.Items[0];
                    //dt.SelectedTreeItem = (INode)s.Items[0];
                    dt.ContentPane = root_vm.NodeViewModel;
                }

            }
        }
        
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



    // ...


    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }


}
