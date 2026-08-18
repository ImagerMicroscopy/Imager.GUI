using Autofac;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.ViewModels;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Runtime.Intrinsics.X86;
using System.Text;




namespace ImagerAvalonia.Views;

public partial class SmartProgramView: UserControl
{

    private SmartProcessingRegisterViewModel _viewModel;

    public SmartProgramView()
    {
        InitializeComponent();
        _viewModel = App.Container.Resolve<SmartProcessingRegisterViewModel>();

        DataContext = _viewModel;
        SmartProgramTab = this.FindControl<TabControl>("SmartProgramTab");


    }

    private void TabProcessing_PointerPressed(object? sender, RoutedEventArgs e)
    {

        AddProcessingTab();

        SmartProgramTab.SelectedIndex = SmartProgramTab.Items.Count - 2; // second-last tab

    }



    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }


    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {

            var tabItem = button.TemplatedParent as TabItem;
            if (tabItem != null && tabItem.Content is SmartProgramEditorView smartprogramview)
            {
                smartprogramview.CloseAll();
                if(smartprogramview.DataContext is SmartProgramViewModel vm)
                {
                    _viewModel.RemoveSmartProgram(vm, vm.Model);

                }
            }
            SmartProgramTab.Items.Remove(tabItem);
        }
    }

    public void AddProcessingTab()
    {
        var dagView = new SmartProgramEditorView();
        string smartProgramTextID = string.Empty;
        if (dagView.DataContext is SmartProgramViewModel vm)
        {
            smartProgramTextID = vm.SmartProgramID.ToString().Substring(0,10);
        }

        var tabItem = new TabItem
        {
            Header = smartProgramTextID,
            Content = dagView
        };

        

        int plusIndex = SmartProgramTab.Items.Count - 1;
        SmartProgramTab.Items.Insert(plusIndex, tabItem);
        SmartProgramTab.SelectedItem = tabItem;
        //}
    }


}


