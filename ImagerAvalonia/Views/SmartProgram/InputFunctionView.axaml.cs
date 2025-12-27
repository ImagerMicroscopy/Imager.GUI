
using Avalonia.Markup.Xaml;
using ImagerAvalonia.Services;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views.ViewUtils;


namespace ImagerAvalonia.Views;







public partial class InputFunctionView : ExperimentSelector
{

    private InputParameterViewModel _selectedParameter;


    public InputFunctionView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }


    protected override void ExpPanel_DetectionDoubleTapped(object? sender, Services.NodeBase e)
    {
        if (DataContextSender is InputParameterViewModel selectedParameter)
        {
            _selectedParameter = selectedParameter;
            expPanel.DetectionDoubleTapped -= ExpPanel_DetectionDoubleTapped;
            window.Close();
            if (DataContext is InputFunctionViewModel vm && e is ActionNode actionNode)
            {
                _selectedParameter.SelectedDetection = actionNode.NodeViewModel;
                _selectedParameter.SelectedNode = actionNode;
                e.OnNodeDeleted += _selectedParameter.NodeDeleted;
            }
        }
    }
}