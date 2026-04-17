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

    public AcquisitionPanelView(SystemDefinedSettingsViewModel availableAcquisitions)
    {
        InitializeComponent();
        DataContext = new AcquisitionPanelViewModel(availableAcquisitions); 
    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }
}