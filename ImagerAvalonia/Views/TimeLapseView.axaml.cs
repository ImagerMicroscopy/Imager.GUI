using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.ViewModels;
using System.Collections.ObjectModel;
using ImagerAvalonia.Services.MeasurementControl;


namespace ImagerAvalonia.Views;

public partial class TimeLapseView : UserControl
{
    public TimeLapseView()
    {
        InitializeComponent();
    }
    public TimeLapseView(SystemDefinedSettingsViewModel availableAcquisitions)
    {
        InitializeComponent();
        DataContext = new ViewModels.TimeLapseViewModel();
    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }
}