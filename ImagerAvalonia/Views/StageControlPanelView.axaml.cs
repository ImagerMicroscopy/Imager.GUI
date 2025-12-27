using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ImagerAvalonia.ViewModels;



namespace ImagerAvalonia.Views;

public partial class StageControlPanelView : UserControl
{




    public StageControlPanelView()
    {


        InitializeComponent();
        DataContext = App.Container.Resolve<StageControlPanelViewModel>(); 


    }




    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }



}


    

