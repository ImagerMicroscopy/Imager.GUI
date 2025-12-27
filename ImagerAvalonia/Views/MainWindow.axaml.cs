using Avalonia;
using Avalonia.Controls;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

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

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {

        ForceClosing = !ForceClosing;
    }

    public void InitializeUserEquipment()
    {
        MainUserAndEquipmentControl.InitializeDataContextEquipment();
    }

    public void InitializeImageControlPanel()
    {
        MainUserAndEquipmentControl.InitializeImageControlPanel();

    }



}