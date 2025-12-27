using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Xaml.Interactions.Custom;
using ImagerAvalonia.ViewModels;

namespace ImagerAvalonia.Views;

public partial class RootPanelView : UserControl
{

    public RootPanelView()
    {
        InitializeComponent();

    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }
    
}