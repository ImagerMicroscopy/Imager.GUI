using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using ImagerAvalonia.Exceptions;
using ImagerAvalonia.ViewModels;
using System;

namespace ImagerAvalonia.Views;

public partial class DataSplitterWindow : Window
{
    public DataSplitterWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void SelectOutputFolder(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is DataSplitterViewModel vm)
        {
            var topLevel = TopLevel.GetTopLevel(this);

            if (topLevel is null)
                return;

            try
            {
                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions
                    {
                        Title = "Select Folder",
                        AllowMultiple = false
                    });

                if (folders.Count > 0)
                {
                    vm.OutputFolder = folders[0].Path.LocalPath;
                }
            }
            catch (Exception ex)
            {
                await ExceptionWindowHandler.ShowExceptionAsync(ex);
                vm.OutputFolder = string.Empty;
            }
        }
    }

}