using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ImagerAvalonia.Views;

public partial class AcquisitionsPanelView : UserControl
{
    public AcquisitionsPanelView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
