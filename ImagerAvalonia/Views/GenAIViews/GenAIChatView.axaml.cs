using Autofac;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ImagerAvalonia.ViewModels.GenAIViewModels;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;

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
        vm.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _scrollViewer?.ScrollToEnd();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Keep the spinner bubble in view when it appears.
        if (e.PropertyName == nameof(GenAIChatViewModel.IsBusy))
            Dispatcher.UIThread.Post(() => _scrollViewer?.ScrollToEnd(), DispatcherPriority.Background);
    }

    private void InputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is GenAIChatViewModel vm && vm.SendCommand.CanExecute(null))
        {
            vm.SendCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async void CopyMessage_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not ChatMessageViewModel message)
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        try
        {
            await clipboard.SetTextAsync(message.Text);
        }
        catch
        {
            return;
        }

        if (sender is not Button button || button.Content is not "Copy")
            return;

        button.Content = "Copied";
        await Task.Delay(TimeSpan.FromSeconds(1.2));
        button.Content = "Copy";
    }
}
