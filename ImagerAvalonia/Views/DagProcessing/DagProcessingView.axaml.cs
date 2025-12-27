using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

using ImagerAvalonia.ViewModels;
using System;





namespace ImagerAvalonia.Views;

public partial class DagProcessingView : UserControl
{
    Canvas DagCanvas;

    public bool newNodeConnectorInitiated = false;
    public NodeConnector? SelectedConnector;
    public NodeConnector? SelectedConnectorVisual;

    public Visual? SelectedNode;
    public DagNodeOutputViewModel? SelectedNodeOutput;
    public DagNodeInputViewModel? SelectedNodeInput;
    private DagProcessingViewModel _viewModel;

    public DagProcessingView()
    {
        InitializeComponent();
        _viewModel = App.Container.Resolve<DagProcessingViewModel>();
        DataContext = _viewModel;
        DagCanvas = this.FindControl<Canvas>("ProcessingCanvas")!;

        this.Initialized += DagProcessingView_Initialized;
        this.KeyDown += DagProcessingView_KeyDown;
        this.KeyDown += DagProcessingView_KeyDownDeleteConnector;
        //this.PointerPressed += DagProcessingView_PointerPressed;

    }

    private void DagProcessingView_KeyDownDeleteConnector(object? sender, KeyEventArgs e)
    {
        if (SelectedNode != null && e.Key==Key.Delete)
        {
            switch (SelectedNode)
            {
                case NodeConnector node_connector:
                    DagCanvas.PointerMoved -= node_connector.UpdateConnectorOnMove;
                    DagCanvas.Children.Remove(node_connector);


                    node_connector.Dispose();
                    //node_connector = null;
                    break;

                case DagNodeBase dag_node:
                    if (dag_node.DataContext is DagNodeViewModel dag_node_vm)
                    {
                        foreach (var dagnode in _viewModel.AddedNodes)
                        {
                            foreach (var dagnodeoutput in dagnode.DagNodeOutputs)
                            {
                                foreach (var dagnodeinput in dag_node_vm.DagNodeInputs)
                                {
                                    dagnodeoutput.RemoveOutputTarget(dagnodeinput);
                                }
                            }
                            foreach (var dagnodeinput in dagnode.DagNodeInputs)
                            {
                                foreach (var dagnodeoutput in dag_node_vm.DagNodeOutputs)
                                {
                                    if(dagnodeinput.InputTarget==dagnodeoutput)
                                    {
                                        dagnodeinput.SetInputTarget(null);
                                    }
                                }
                            }
                        }
                        _viewModel.AddedNodes.Remove(dag_node_vm);
                    }
                    
                    dag_node.OnNodeDeleted(sender, e);

                    break;
            }
            
        }
    }
    public void DagProcessingView_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        
        if (SelectedNode != null)
        {
            if (this.SelectedNode.Effect != null)
            {
                this.SelectedNode.Effect = null;
            }
        }
        this.Focus();
        if(sender is Visual selected_visual)
        {
            SelectedNode = selected_visual;
            SelectedNode.Effect = new DropShadowEffect() { BlurRadius = 10, Color = Colors.Red };
        }


        //foreach (Visual child in DagCanvas.Children)
        //{
        //    //if(child is DagNodeBase dagnode)
        //    //{ 
        //    Point localPoint = e.GetPosition(child);
        //    var childRect = new Rect(child.Bounds.Size);

        //    if (childRect.Contains(localPoint))
        //    {
        //        selected_visual = child;

        //        SelectedNode = selected_visual;
        //        SelectedNode.Effect = new DropShadowEffect() { BlurRadius = 10, Color = Colors.Red };

        //        break;
        //    }
        //}



    }

    private void DagProcessingView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (SelectedConnector != null && e.Key==Key.Escape && newNodeConnectorInitiated)
        {
            DagCanvas.Children.Remove(SelectedConnector);
            DagCanvas.PointerMoved -= SelectedConnector.UpdateConnectorOnMove;
            SelectedConnector = null;
            newNodeConnectorInitiated = false;
        }
    }

    


    private void DagProcessingView_Initialized(object? sender, EventArgs e)
    {
       
        //DagCanvas = this.FindControl<Canvas>("ProcessingCanvas")!;
  

        if(DataContext is DagProcessingViewModel dagview)
        {
            dagview.NodeAdded += Dagview_NodeAdded;
        }

    }

    private void Dagview_NodeAdded(object? sender, NodeInfo e)
    {
        var dagNode = new DagNodeBase(DagCanvas, this, e);
        if(dagNode.DataContext is DagNodeViewModel dagNodevm)
        {
            _viewModel.AddedNodes.Add(dagNodevm);

        }

        dagNode.PointerPressed += DagProcessingView_PointerPressed;
        DagCanvas.Children.Add(dagNode);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }


}


