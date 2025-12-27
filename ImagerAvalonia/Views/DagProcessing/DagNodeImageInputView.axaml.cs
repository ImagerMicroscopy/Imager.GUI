using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.VisualTree;
using ImagerAvalonia.ViewModels;

namespace ImagerAvalonia.Views;

public partial class DagNodeImageInputView : UserControl
{
    public Ellipse ConnectorEndPoint;


    public DagNodeImageInputView()
    {
        InitializeComponent();
        ConnectorEndPoint = this.FindControl<Ellipse>("ConnectorPoint")!;



    }
    public void RemoveOutput(DagNodeImageOutputView output_view)
    {
        var output_vm = output_view.DataContext as DagNodeOutputViewModel;
        if (output_vm != null)
        {
            output_vm.RemoveOutputTarget(this.DataContext as DagNodeInputViewModel); 
        }
    }

    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        var parentDagNode = this.FindAncestorOfType<DagNodeBase>();

        parentDagNode.ConnectorAttached(this, e);
    }


}