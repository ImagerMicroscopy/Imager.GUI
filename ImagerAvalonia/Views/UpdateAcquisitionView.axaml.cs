using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;

namespace ImagerAvalonia.Views;

public partial class UpdateAcquisitionView : UserControl
{

    public UpdateAcquisitionView()
    {
        InitializeComponent();

    }
    public UpdateAcquisitionView(UserDefinedAcquisitions availableAcquisitions)
    {
        InitializeComponent();
        DataContext = new UpdateAcquisitionViewModel(availableAcquisitions);

    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}