using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ImagerAvalonia.ViewModels;

namespace ImagerAvalonia.Views;

public partial class SmartServerOutputView : UserControl
{
    private ScrollViewer? _scrollViewer;

    public SmartServerOutputView()
    {
        
        InitializeComponent();

        _scrollViewer = this.FindControl<ScrollViewer>("ScrollViewer");
    }

    private void RunList_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _scrollViewer.ScrollToEnd();
        }, DispatcherPriority.Background);
    }


    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}