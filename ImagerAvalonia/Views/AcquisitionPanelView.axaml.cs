using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.ViewModels;


namespace ImagerAvalonia.Views;

public partial class AcquisitionPanelView : UserControl
{

    public AcquisitionPanelView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }
}