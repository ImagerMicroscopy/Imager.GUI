using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.ViewModels;



namespace ImagerAvalonia.Views;

public partial class StageLoopTemplateView: Window
{
    public StageLoopTemplateView(StageLoopViewModel vm)
    {
        DataContext = new StageLoopTemplateViewModel(vm);
        this.Closing += StageLoopTemplateView_Closing;
        InitializeComponent();
    }

    private void StageLoopTemplateView_Closing(object? sender, WindowClosingEventArgs e)
    {
        e.Cancel = true;
        this.Hide();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }

}
