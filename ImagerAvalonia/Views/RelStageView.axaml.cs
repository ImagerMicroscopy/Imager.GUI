using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.ViewModels;
using Autofac;

namespace ImagerAvalonia.Views;

public partial class RelStageView : UserControl
{


    public RelStageView()
    {
        InitializeComponent();

    }

    public RelStageView(UserDefinedAcquisitions availableAcquisitions)
    {
        InitializeComponent();

        DataContext = App.Container.Resolve<RelStageViewModel>(new TypedParameter(typeof(UserDefinedAcquisitions), availableAcquisitions));
    }
    public void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void TextBlock_ActualThemeVariantChanged(object? sender, System.EventArgs e)
    {
    }
}