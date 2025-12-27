using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using Autofac;
using ImagerAvalonia.ViewModels;


namespace ImagerAvalonia.Views;

public partial class StatusWindowView : UserControl
{
    public StatusWindowView()
    {
        InitializeComponent();
        DataContext = App.Container.Resolve<StatusWindowViewModel>();

    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }
}