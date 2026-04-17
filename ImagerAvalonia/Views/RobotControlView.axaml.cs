using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.ViewModels;
using System.Collections.ObjectModel;
using ImagerAvalonia.Services.MeasurementControl;


namespace ImagerAvalonia.Views;

public partial class RobotControlView : UserControl
{
    public RobotControlView()
    {
        InitializeComponent();
    }
    public RobotControlView(SystemDefinedSettingsViewModel availableAcquisitions)
    {
        InitializeComponent();
        DataContext = new RobotControlViewModel(availableAcquisitions.Robots);
    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}