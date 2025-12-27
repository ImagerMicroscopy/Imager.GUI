using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

using Avalonia.VisualTree;
using ImagerAvalonia.ViewModels;
using System.Collections.Generic;





namespace ImagerAvalonia.Views;

public partial class DagNodeBase : UserControl
{
    private bool _isDragging = false;
    //private bool _showLiveView = true;
    private Avalonia.Point _lastMousePosition;
    private double _translateTransformOnCanvasX = 0;
    private double _translateTransformOnCanvasY = 0;
    private DagProcessingView _parentView;
    private List<NodeConnector> _outboundConnectors = new();
    private List<NodeConnector> _inboundConnectors = new();

    private List<UserControl> _childrenViews = new();
    private List<UserControl> _parentViews = new();



    private Canvas _parentCanvas;


    public DagNodeBase(Canvas canvas, DagProcessingView parentView, NodeInfo node_info)
    {
        InitializeComponent();
        DataContext = new DagNodeViewModel(node_info);
        _parentView = parentView;
        _parentCanvas = canvas;


        ZIndex = 3;
        this.PointerPressed += OnPointerPressed;
        this.PointerMoved += OnPointerMoved;
        this.PointerReleased += OnPointerReleased;
        //this.KeyDown += OnNodeDeleted;
        this.Focusable = true;
    }


    private NodeConnector AddConnector(Point startpoint, Visual startpoint_visual, Control output_view )
    {
        var NodeConnectorBase = new NodeConnector(startpoint,startpoint_visual, output_view );
        NodeConnectorBase.PointerPressed += _parentView.DagProcessingView_PointerPressed;
        _outboundConnectors.Add(NodeConnectorBase);
        _parentCanvas.Children.Add(NodeConnectorBase);

        return NodeConnectorBase;
    }


    public static readonly StyledProperty<string> NodeTitleProperty =
        AvaloniaProperty.Register<DagNodeBase, string>(
            nameof(NodeTitle), "Default Title");

    public string NodeTitle
    {
        get => GetValue(NodeTitleProperty);
        set => SetValue(NodeTitleProperty, value);
    }




    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }


    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {


        if (!_parentView.newNodeConnectorInitiated)
        {
            _isDragging = !_isDragging;
            _lastMousePosition = e.GetPosition(this.GetVisualParent());
        }

           
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {

            if (_isDragging)
            {
                var currentPosition = e.GetPosition(this.GetVisualParent());
                var delta = currentPosition - _lastMousePosition;

                _translateTransformOnCanvasX += delta.X;
                _translateTransformOnCanvasY += delta.Y;
                Canvas.SetTop(this,_translateTransformOnCanvasY);
                Canvas.SetLeft(this,_translateTransformOnCanvasX);

                //foreach (var outboundpoint in _outboundEndPoints)
                //{

                foreach (var outboundConnector in _outboundConnectors)
                {
                    Point relativePosition = this.TranslatePoint(new Point(0, 0), outboundConnector.startVisual).Value;

                    outboundConnector.UpdateStart(new Point(_translateTransformOnCanvasX - relativePosition.X, _translateTransformOnCanvasY - relativePosition.Y));

                }
                foreach (var inboundConnector in _inboundConnectors)
                {

                    Point relativePosition = this.TranslatePoint(new Point(0, 0), inboundConnector.endVisual).Value;

                    inboundConnector.UpdateEnd(new Point(_translateTransformOnCanvasX - relativePosition.X , _translateTransformOnCanvasY - relativePosition.Y));

                }




            _lastMousePosition = currentPosition;
            }
        
    }

    public void ConnectorInitiated(object? sender, PointerPressedEventArgs e)
    {
        var output_view = sender as DagNodeImageOutputView;
        if (!_parentView.newNodeConnectorInitiated)
        {
            Point startpoint = e.GetPosition(this.GetVisualParent());
            _parentView.SelectedConnector = AddConnector(startpoint, output_view.ConnectorEndPoint, output_view);
            _parentView.SelectedConnector.PointerPressed += _parentView.DagProcessingView_PointerPressed;
            //_parentView.SelectedNode = this;
            if (output_view.DataContext is DagNodeOutputViewModel vm)
            {
                _parentView.SelectedNodeOutput = vm;
                _parentView.newNodeConnectorInitiated = true;
                _parentCanvas.PointerMoved += _parentView.SelectedConnector.UpdateConnectorOnMove;
            }
        }
    }

    public void OnNodeDeleted(object? sender, KeyEventArgs e)
    {
        if(e.Key==Key.Delete || e.Key==Key.Back)
        {
            var vm_current = DataContext as DagNodeViewModel;

            foreach(DagNodeInputViewModel input_vm in vm_current.DagNodeInputs)
            {
                input_vm.SetInputTarget(null);

            }

            foreach (DagNodeOutputViewModel output_vm in vm_current.DagNodeOutputs)
            {
                output_vm.RemoveOutputTargets();
            }
            foreach (var connector in _inboundConnectors)
            {
                _parentCanvas.Children.Remove(connector);
            }

            foreach (var connector in _outboundConnectors)
            {
                _parentCanvas.Children.Remove(connector);
            }

            _parentCanvas.Children.Remove(this);
        }
    }


    public void ConnectorAttached(object sender, PointerPressedEventArgs e)
    {
        if(sender is DagNodeImageInputView input_view && _parentView.newNodeConnectorInitiated)
        {
      
            var vm_current = DataContext as DagNodeViewModel;
            if(_parentView.SelectedNode!=null && _parentView.SelectedNode.DataContext is DagNodeViewModel vm_parent
                && _parentView.SelectedConnector!=null)
            {
                _parentView.SelectedConnector.SetEndPoint(input_view.ConnectorEndPoint);
                _parentView.SelectedConnector.SetEndControl(input_view);



                _parentView.newNodeConnectorInitiated = false;
                _parentCanvas.PointerMoved -= _parentView.SelectedConnector.UpdateConnectorOnMove;

                if (_parentView.SelectedNodeOutput.SetOutputTarget((DagNodeInputViewModel)input_view.DataContext) &&
                   ((DagNodeInputViewModel)input_view.DataContext).SetInputTarget(_parentView.SelectedNodeOutput))
                {

                    _inboundConnectors.Add(_parentView.SelectedConnector);
                    //this.ChildrenNodes.Add(input_view);
                    //input_view._parentViews.Add(this);
                }
                else
                {
                    _parentCanvas.Children.Remove(_parentView.SelectedConnector);
                    _parentView.SelectedConnector = null;
                }
            }

        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;

    }

    // This is to prevent that dragging is enabled when interacting with combobox. 
    // Otherwise, you can do bound checking, but this is easier. 
    private void OnComboBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDragging = true;
    }
}


