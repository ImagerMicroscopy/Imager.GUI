using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.ViewModels;

namespace ImagerAvalonia.Views;

public partial class MultiChannelContrastView : UserControl
{
    public MultiChannelContrastView()
    {
        InitializeComponent();
        DataContext = new MultiChannelContrastViewModel();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }

}