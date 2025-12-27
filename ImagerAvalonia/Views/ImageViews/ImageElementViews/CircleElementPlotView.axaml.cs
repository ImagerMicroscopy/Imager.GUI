using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;

using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using System.Collections.Generic;
using System.Linq;

namespace ImagerAvalonia.Views.CircleElementPlotviews;

public partial class CircleElementPlotView : UserControl
{
    private CircleElementViewModel _circleElementViewModel;


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

    private Dictionary<IImageElement, DataLogger> MinPlotDataLoggers = new();
    private Dictionary<IImageElement, DataLogger> MaxPlotDataLoggers = new();
    private Dictionary<IImageElement, DataLogger> MeanPlotDataLoggers = new();
    private Dictionary<IImageElement, List<uint>> min_vals = new();
    private Dictionary<IImageElement, List<uint>> max_vals = new();
    private Dictionary<IImageElement, List<double>> mean_vals = new();
    private Dictionary<IImageElement, List<XYStagePosition>> stage_positions = new();
    private Dictionary<IImageElement, List<double>> time_vals = new();


    private AvaPlot MaxPlot = new AvaPlot();
    private AvaPlot MeanPlot = new AvaPlot();
    private AvaPlot MinPlot = new AvaPlot();
    private object _lockObj = new();

    public CircleElementPlotView()
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
            _circleElementViewModel = DataContext as CircleElementViewModel;
            _circleElementViewModel.OnPlotsUpdated += _circleElementViewModel_OnPlotsUpdated;
            _circleElementViewModel.OnPlotTypeChanged += _circleElementViewModel_OnSelectedPlotTypeChanged;
            _circleElementViewModel.OnPositionChanged += _circleElementViewModel_OnPositionChanged;
            //plt = MeanPlot;
            MinPlot.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#454545");
            MinPlot.Plot.DataBackground.Color = ScottPlot.Colors.Gray;
            MinPlot.Plot.Axes.Color(ScottPlot.Colors.White);
            MinPlot.Plot.XLabel("Time [s]");
            MinPlot.Plot.YLabel("Intensity");

            MeanPlot.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#454545");
            MeanPlot.Plot.DataBackground.Color = ScottPlot.Colors.Gray;
            MeanPlot.Plot.Axes.Color(ScottPlot.Colors.White);
            MeanPlot.Plot.XLabel("Time [s]");
            MeanPlot.Plot.YLabel("Intensity");

            MaxPlot.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#454545");
            MaxPlot.Plot.DataBackground.Color = ScottPlot.Colors.Gray;
            MaxPlot.Plot.Axes.Color(ScottPlot.Colors.White);
            MaxPlot.Plot.XLabel("Time [s]");
            MaxPlot.Plot.YLabel("Intensity");

            _contentControl = this.FindControl<ContentControl>("PlotContainer")!;
            _contentControl.Content = MeanPlot;
           
            plt = MeanPlot;

            if (_circleElementViewModel != null)
            {
                foreach (var circleregion in _circleElementViewModel.CircleRegions)
                {

                    var plots = circleregion.RetrievePlotControls();
                    if (plots.All(x => x.GetType() == typeof(DataLogger)))
                    {
                        var logger_plots = plots.Select(x => (DataLogger)x).ToList();

                        min_vals.Add(circleregion, new List<uint>());
                        max_vals.Add(circleregion, new List<uint>());
                        mean_vals.Add(circleregion, new List<double>());
                        stage_positions.Add(circleregion, new List<XYStagePosition>());
                        time_vals.Add(circleregion, new List<double>());


                        MinPlotDataLoggers.Add(circleregion, logger_plots[0]);
                        MaxPlotDataLoggers.Add(circleregion, logger_plots[1]);
                        MeanPlotDataLoggers.Add(circleregion, logger_plots[2]);



                        MinPlot.Plot.Add.Plottable(logger_plots[0]);
                        MaxPlot.Plot.Add.Plottable(logger_plots[1]);
                        MeanPlot.Plot.Add.Plottable(logger_plots[2]);
                    }

                }
            }

            ControlColumn = this.FindControl<Grid>("MainGrid")!.ColumnDefinitions[2];
           

            // Apply initial Auto mode
            UpdateAutoAxis("X");
            UpdateAutoAxis("Y");
        }
    }

    private void _circleElementViewModel_OnPositionChanged(XYStagePosition pinned_position)
    {
        lock (_lockObj)
        {
            foreach (var element in min_vals.Keys)
            {
                if (MinPlotDataLoggers.TryGetValue(element, out var minlog) &&
                MaxPlotDataLoggers.TryGetValue(element, out var maxlog) &&
                MeanPlotDataLoggers.TryGetValue(element, out var meanlog))
                {

                    minlog.Data.Clear();
                    maxlog.Data.Clear();
                    meanlog.Data.Clear();


                    for (int i = 0; i < stage_positions[element].Count; i++)
                    {
                        var position = stage_positions[element][i];
                        if (pinned_position != IStageControl.DefaultXYStagePosition)
                        {
                            if (position.IsEqual(pinned_position))
                            {
                                minlog.Add(time_vals[element][i], min_vals[element][i]);
                                maxlog.Add(time_vals[element][i], max_vals[element][i]);
                                meanlog.Add(time_vals[element][i], mean_vals[element][i]);
                            }
                        }
                        else
                        {
                            minlog.Add(time_vals[element][i], min_vals[element][i]);
                            maxlog.Add(time_vals[element][i], max_vals[element][i]);
                            meanlog.Add(time_vals[element][i], mean_vals[element][i]);
                        }
                    }  
                }
            }
        }
    }

    private void _circleElementViewModel_OnSelectedPlotTypeChanged(string plottype)
    {
        switch (plottype)
        {
            case "Min":
                plt = MinPlot;
                _contentControl.Content = MinPlot;
                break;

            case "Max":
                plt = MaxPlot;
                _contentControl.Content = MaxPlot;

                break;

            case "Mean":
                plt = MeanPlot;
                _contentControl.Content = MeanPlot;

                break;
        }
        Dispatcher.UIThread.Invoke(() => { plt.Refresh(); _contentControl.InvalidateVisual(); });


    }



    private void _circleElementViewModel_OnPlotsUpdated(uint min, uint max, double mean,double time,IImageElement element, XYStagePosition pos)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (MinPlotDataLoggers.TryGetValue(element, out var minlog) && MaxPlotDataLoggers.TryGetValue(element, out var maxlog) && MeanPlotDataLoggers.TryGetValue(element, out var meanlog))
            {
          

                lock (_lockObj)
                {
                    var lastval = minlog.Data.Coordinates.LastOrDefault();
                    if (time < lastval.X)
                    {
                        minlog.Data.Clear();
                        maxlog.Data.Clear();
                        meanlog.Data.Clear();

                        min_vals[element].Clear();
                        max_vals[element].Clear();
                        mean_vals[element].Clear();
                        time_vals[element].Clear();
                        stage_positions[element].Clear();

                    }
                    if (_circleElementViewModel.PinnedPosition != IStageControl.DefaultXYStagePosition)
                    {
                        if (_circleElementViewModel.PinnedPosition.IsEqual(pos))
                        {
                            minlog.Add(time, min);
                            maxlog.Add(time, max);
                            meanlog.Add(time, mean);
                        }
                    }
                    else
                    {
                        minlog.Add(time, min);
                        maxlog.Add(time, max);
                        meanlog.Add(time, mean);
                    }
                }

                min_vals[element].Add(min); 
                max_vals[element].Add(max);
                mean_vals[element].Add(mean);
                time_vals[element].Add(time);
                stage_positions[element].Add(pos);
            }
           
     
            MinPlot.Refresh();
            MaxPlot.Refresh();
            MeanPlot.Refresh();
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





