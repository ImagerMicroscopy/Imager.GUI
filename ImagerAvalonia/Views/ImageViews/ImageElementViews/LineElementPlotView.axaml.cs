using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;

using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
using System.Collections.Generic;
using System.Linq;

namespace ImagerAvalonia.Views.LineElementPlotviews;

public partial class LineElementPlotView : UserControl
{
    private LineElementViewModel _lineElementViewModel;


    private NumericUpDown XMin;
    private NumericUpDown XMax;
    private CheckBox XAuto;
    private ColumnDefinition ControlColumn;
    private NumericUpDown YMin;
    private NumericUpDown YMax;
    private CheckBox YAuto;
    private ContentControl _contentControl;
    private bool _isPanelVisible = false;
    private AvaPlot plt;

    private Dictionary<IImageElement, Signal> AllLineIntensities = new();
    private AvaPlot LineProfilePlot = new AvaPlot();

    private object _lockObj = new();

    public LineElementPlotView()
    {
        InitializeComponent();
        DataContextChanged += CircleElementPlotView_DataContextChanged;

        XMin = this.FindControl<NumericUpDown>("XMinInput")!;
        XMax = this.FindControl<NumericUpDown>("XMaxInput")!;
        XAuto = this.FindControl<CheckBox>("XAutoCheck")!;

        // Properties for Y controls
        YMin = this.FindControl<NumericUpDown>("YMinInput")!;
        YMax = this.FindControl<NumericUpDown>("YMaxInput")!;
        YAuto = this.FindControl<CheckBox>("YAutoCheck")!;

        // Default values
        XMin.Value = 0;
        XMax.Value = 10;
        YMin.Value = 0;
        YMax.Value = 10;

        // Set Auto mode by default
        XAuto.IsChecked = true;
        YAuto.IsChecked = true;

        // Event handlers for LostFocus
        XMin.ValueChanged += (s, e) => UpdateManualAxis("X");
        XMax.ValueChanged += (s, e) => UpdateManualAxis("X");
        YMin.ValueChanged += (s, e) => UpdateManualAxis("Y");
        YMax.ValueChanged += (s, e) => UpdateManualAxis("Y");

        // Event handlers for Auto checkboxes
        XAuto.IsCheckedChanged += (s, e) => UpdateAutoAxis("X");
        YAuto.IsCheckedChanged += (s, e) => UpdateAutoAxis("Y");
    }


    

    private void CircleElementPlotView_DataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext != null )
        {
            _lineElementViewModel = DataContext as LineElementViewModel;
            _lineElementViewModel.OnPlotsUpdated += _circleElementViewModel_OnPlotsUpdated;

            LineProfilePlot.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#454545");
            LineProfilePlot.Plot.DataBackground.Color = ScottPlot.Colors.Gray;
            LineProfilePlot.Plot.Axes.Color(ScottPlot.Colors.White);
            LineProfilePlot.Plot.XLabel("Distance [px]");
            LineProfilePlot.Plot.YLabel("Intensity");


            _contentControl = this.FindControl<ContentControl>("PlotContainer")!;
            _contentControl.Content = LineProfilePlot;
            plt = LineProfilePlot;

            if (_lineElementViewModel != null)
            {
                foreach (var lineregion in _lineElementViewModel.LineRegions)
                {

                    var plots = lineregion.RetrievePlotControls();
                    LineProfilePlot.Plot.Add.Plottable(plots[0]);
                    AllLineIntensities.TryAdd(lineregion, (Signal)plots[0]);


                }
            }

            ControlColumn = this.FindControl<Grid>("MainGrid")!.ColumnDefinitions[2];
           

            UpdateAutoAxis("X");
            UpdateAutoAxis("Y");
        }
    }




    private void _circleElementViewModel_OnPlotsUpdated(List<double> newIntensity,IImageElement element, XYStagePosition pos)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (AllLineIntensities.TryGetValue(element, out var signalplot))
            {
          

                lock (_lockObj)
                {

                    if (_lineElementViewModel.PinnedPosition != IStageControl.DefaultXYStagePosition)
                    {
                        if (_lineElementViewModel.PinnedPosition.IsEqual( pos ))
                        {
                            signalplot.Data = new SignalSourceDouble(newIntensity, 1);
                        }
                    }
                    else
                    {
                        signalplot.Data = new SignalSourceDouble(newIntensity, 1);

                    }
                }
            }
            if (XAuto.IsChecked == true)
            {
                LineProfilePlot.Plot.Axes.AutoScaleExpandX();
            }

            if (YAuto.IsChecked == true)
            {
                LineProfilePlot.Plot.Axes.AutoScaleExpandY();
            }
            LineProfilePlot.Refresh();

        });
    }



    private async void ToggleControls_Click(object? sender, RoutedEventArgs e)
    {

        var grid = this.FindControl<Grid>("MainGrid")!;

        if (grid.ColumnDefinitions.Count > 1)
        {
            var currentWidth = grid.ColumnDefinitions[2].Width;

            grid.ColumnDefinitions[2].Width =
                currentWidth.IsAuto ? new GridLength(0) : GridLength.Auto;
        }


        var controlPanel = this.FindControl<Border>("ControlPanel")!;
        double from = controlPanel.Width;
        double to = _isPanelVisible ? 0 : 300; // Adjust panel width as needed
        _isPanelVisible = !_isPanelVisible;

        var animation = new Animation
        {
            
            
            Duration = System.TimeSpan.FromMilliseconds(250),
            Easing = new SineEaseOut(),
            FillMode = FillMode.Forward,
            Children =
        {
            new KeyFrame
            {
                Cue = new Cue(0d),
                Setters = { new Setter(Border.WidthProperty, from) }
            },
            new KeyFrame
            {
                Cue = new Cue(1d),
                Setters = { new Setter(Border.WidthProperty, to) }
            }
        }
        };

        await animation.RunAsync(controlPanel);
    }

    private void UpdateManualAxis(string axis)
    {
        if (axis == "X" && XAuto.IsChecked != true)
        {
            if (XMin.Value != null && XMax.Value != null)
            {
                plt.Plot.Axes.SetLimitsX((double)XMin.Value, (double)XMax.Value);
                plt.Refresh();
            }
        }
        else if (axis == "Y" && YAuto.IsChecked != true)
        {
            if (YMin.Value != null && YMax.Value != null)
            {
                plt.Plot.Axes.SetLimitsY((double)YMin.Value, (double)YMax.Value);
                plt.Refresh();
            }
        }
    }

    private void UpdateAutoAxis(string axis)
    {
        if (axis == "X")
        {
            bool auto = XAuto.IsChecked == true;

            XMin.IsEnabled = !auto;
            XMax.IsEnabled = !auto;

            if (auto)
            {
                plt.Plot.Axes.AutoScaleX();
                plt.Refresh();
            }
        }
        else if (axis == "Y")
        {
            bool auto = YAuto.IsChecked == true;

            YMin.IsEnabled = !auto;
            YMax.IsEnabled = !auto;

            if (auto)
            {
                plt.Plot.Axes.AutoScaleY();
                plt.Refresh();
            }
        }
    }





    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }
}





