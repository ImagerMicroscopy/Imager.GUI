using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.Data;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;


namespace ImagerAvalonia.Views;

public partial class LogBookView : Window
{
    public LogBookView(JObject logbooksettings, JObject logbooksettingsend, bool isEnd)
    {
        InitializeComponent();
        var vm = new LogBookViewModel(logbooksettings, logbooksettingsend, isEnd);
        vm.OnDataSubmitted += Vm_OnDataSubmitted;
        DataContext = vm;

        this.Closing += OnClosing;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        e.Cancel = true;
    }

    public void SetLogOutMode(bool isEnd)
    {
        if(DataContext is LogBookViewModel vm)
        {
            vm.IsEnd = isEnd;
        }
    }

    private void Vm_OnDataSubmitted(object? sender, System.EventArgs e)
    {
        Hide();
    }

    public LogBookView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }

    internal void SetDBContext(LogBookContext db)
    {
        if(DataContext is LogBookViewModel vm)
        {
            vm.SetVMDBContext(db) ;
        }
    }

    internal void SetEndDate()
    {
        if (DataContext is LogBookViewModel vm)
        {
            vm.EndDate = DateTime.Now;
        }
    }
}