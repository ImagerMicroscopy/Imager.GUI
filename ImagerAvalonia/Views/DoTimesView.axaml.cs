using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.ObjectModel;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;


namespace ImagerAvalonia.Views;

public partial class DoTimesView : UserControl
{
    public DoTimesView()
    {
        InitializeComponent();
    }
    //public DoTimesView(SystemDefinedSettingsViewModel availableAcquisitions)
    //{
    //    InitializeComponent();
    //    DataContext = new ViewModels.DoTimesViewModel();
    //}
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }
}