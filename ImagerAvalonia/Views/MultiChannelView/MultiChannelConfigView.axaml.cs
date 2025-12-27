using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.Services;
using ImagerAvalonia.ViewModels;
using ImagerAvalonia.Views.ViewUtils;

namespace ImagerAvalonia.Views;

public partial class MultiChannelConfigView : UserControl
{
    public MultiChannelConfigView(ILifetimeScope scope)
    {
        InitializeComponent();
        DataContext = new MultiChannelViewModel();
    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }


}


