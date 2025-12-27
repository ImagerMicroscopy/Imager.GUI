using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.VisualTree;
using ImagerAvalonia.ViewModels;

namespace ImagerAvalonia.Views;

public partial class DagNodeImageOutputView : UserControl
{

    public Ellipse ConnectorEndPoint;

    public DagNodeImageOutputView()
    {
        InitializeComponent();
        ConnectorEndPoint = this.FindControl<Ellipse>("ConnectorPoint");
     

    }

    public void RemoveInput(DagNodeImageInputView input_view)
    {
        var input_vm = input_view.DataContext as DagNodeInputViewModel;
        if (input_vm != null) 
        {
            input_vm.SetInputTarget(null);
        }
    }


    private void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        var parentDagNode = this.FindAncestorOfType<DagNodeBase>();

        parentDagNode.ConnectorInitiated(this, e);
    }


}