using Autofac;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.ViewModels;
using System.Linq;
using System.Collections.Generic;
using Avalonia.Interactivity;
using ImagerAvalonia.Services.MeasurementControl;
using System.Collections.ObjectModel;
using System;

namespace ImagerAvalonia.Views;

public partial class StageLoopView : UserControl
{
    private StageLoopTemplateView? _templateView ;    

    public StageLoopView()
    {
        InitializeComponent();
    }
    public StageLoopView(SystemDefinedSettingsViewModel availableAcquisitions)
    {
        InitializeComponent();
        DataContext = App.Container.Resolve<StageLoopViewModel>();

    }
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is Border s)
        {
            OnRowPasteClipboardContent(s.Child, e);
        }
    }



    private void OnRowPasteClipboardContent(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            var values = clipboard?.GetTextAsync().Result;
            if (DataContext is StageLoopViewModel stage_vm)
            {

                string[] positions = string.IsNullOrWhiteSpace(values)
                    ? Array.Empty<string>()
                    : values.Split("\r\n"); foreach (string position in positions)
                {
                    if (!string.IsNullOrEmpty(position))
                    {
                        List<string> xy_vals = new List<string>(position.Split('\t'));
                        xy_vals = xy_vals.Select(x => x.Replace("\"", "")).ToList();

                        XYStagePosition pasted_position = new XYStagePosition(float.Parse(xy_vals[0]), float.Parse(xy_vals[1]), float.Parse(xy_vals[2]), bool.Parse(xy_vals[3]), float.Parse(xy_vals[4]), xy_vals[5]);

                        stage_vm.AppendStagePosition(pasted_position);
                        
                    }
                }
            }
        }
    }

    private void OpenStageTemplatePanel(object? sender, RoutedEventArgs e)
    {
        if(_templateView == null)
        {
            if (DataContext is StageLoopViewModel vm)
            {
                _templateView = new StageLoopTemplateView(vm);
                _templateView.Closed += (_, _) => _templateView = null;

            }
        }

        _templateView?.Show();
        
    }
}