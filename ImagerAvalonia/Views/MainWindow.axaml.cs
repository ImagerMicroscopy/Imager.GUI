using Avalonia;
using Avalonia.Controls;
using ImagerAvalonia.ViewModels;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static SkiaSharp.HarfBuzz.SKShaper;

namespace ImagerAvalonia.Views;


public static class ScreenHelper
{
    public static PixelRect ScreenSize;
    public static double Scaling;
}

public partial class MainWindow : Window
{
    public MainView MainUserAndEquipmentControl;
    public bool ForceClosing = true;
    public MainWindow()
    {
        Closing += MainWindow_Closing;
        var topLevel = TopLevel.GetTopLevel(this);
        var screens = topLevel.Screens;
        var primaryScreen = screens.Primary;

        ScreenHelper.ScreenSize = primaryScreen.WorkingArea;
        ScreenHelper.Scaling = primaryScreen.Scaling;
        InitializeComponent();

        this.WindowState = WindowState.Maximized;

        MainUserAndEquipmentControl = this.FindControl<UserControl>("MainUserAndExperimentControl") as MainView;



    }

    public void ApplyEquipment(EquipmentInitResult result)
    {
        if (MainUserAndEquipmentControl.DataContext is MainViewModel mainVM)
            mainVM.ApplyEquipment(result);
    }

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {

        ForceClosing = !ForceClosing;
    }

    public void InitializeImageControlPanel()
    {
        MainUserAndEquipmentControl.InitializeImageControlPanel();

    }

}