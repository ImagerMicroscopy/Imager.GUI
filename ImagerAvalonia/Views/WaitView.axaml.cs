using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.ViewModels;
using System.Collections.ObjectModel;
using ImagerAvalonia.Services.MeasurementControl;


namespace ImagerAvalonia.Views;

public partial class WaitView : UserControl
{
    public WaitView()
    {
        InitializeComponent();
    }
    public WaitView(SystemDefinedSettingsViewModel availableAcquisitions)
    {
        InitializeComponent();
        DataContext = new ViewModels.WaitViewModel();
    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }
}