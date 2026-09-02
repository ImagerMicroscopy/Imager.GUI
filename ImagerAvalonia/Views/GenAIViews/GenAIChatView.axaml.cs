using Autofac;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.ViewModels.GenAIViewModels;
using System.Collections.Specialized;

namespace ImagerAvalonia.Views.GenAIViews;

public partial class GenAIChatView : UserControl
{
    private readonly ScrollViewer? _scrollViewer;

    public GenAIChatView()
    {
        InitializeComponent();

        var vm = App.Container.Resolve<GenAIChatViewModel>();
        DataContext = vm;

        _scrollViewer = this.FindControl<ScrollViewer>("MessagesScrollViewer");
        vm.Messages.CollectionChanged += Messages_CollectionChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _scrollViewer?.ScrollToEnd();
    }

    private void InputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is GenAIChatViewModel vm && vm.SendCommand.CanExecute(null))
        {
            vm.SendCommand.Execute(null);
            e.Handled = true;
        }
    }
}
