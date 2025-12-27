using Autofac;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ImagerAvalonia.Exceptions;
using ImagerAvalonia.ViewModels;
using ScottPlot;
using System;





namespace ImagerAvalonia.Views;

public partial class SmartProgramEditorView : UserControl
{
    private EditorView? _editorView;
    private readonly SmartProcessingRegisterViewModel _registerViewModel;   
    public SmartProgramEditorView()
    {
        InitializeComponent();
        var vm = App.Container.Resolve<SmartProgramViewModel>();
        _registerViewModel = App.Container.Resolve<SmartProcessingRegisterViewModel>();
        vm.OnOpenFolderRequested += VM_OnOpenFolderRequested;
        vm.OnSelectedProgramChangedEvent += VM_OnSelectedProgramChangedEvent;
        DataContext = vm;
        _editorView = this.FindControl<EditorView>("EditorView");
        _editorView.OnReloadRequested += _editorView_OnReloadRequested;
        
    }

    private async void _editorView_OnReloadRequested(object? sender, string e)
    {
        SmartProgramViewModel vm = DataContext as SmartProgramViewModel;
        await vm.SubmitFolderToSmartProgramServer(e, false);
        vm.OnFileReloadRequested(sender, e);
    }

    public async void saveFile(object? sender, RoutedEventArgs e)
    {
        _editorView.SaveDocument(sender, e);
    }

    public void CloseAll()
    {
        SmartProgramViewModel vm = DataContext as SmartProgramViewModel;
        _registerViewModel.SmartProgramViewModels.Remove(vm);
        vm.ClearMethods();
    }

    private void VM_OnSelectedProgramChangedEvent(object? sender, string e)
    {
        _editorView.SetDocument(e);
    }

    private async void VM_OnOpenFolderRequested(object? sender, EventArgs e)
    {
        if(sender is SmartProgramViewModel vm)
        {
            var topLevel = new Window();

            try
            {
                var files = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Folder",
                    AllowMultiple = false

                });
                if (files.Count != 0)
                {
                    var toloadpythonFolder = files[0].Path.ToString();
                    toloadpythonFolder = toloadpythonFolder.Replace("file:///", "");
                    vm.SubmitFolderToSmartProgramServer(toloadpythonFolder, true);
                }
            }
            catch (Exception ex)
            {
                await ExceptionWindowHandler.ShowExceptionAsync(ex);
            }
        }
    }



    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }


}


