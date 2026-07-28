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

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}