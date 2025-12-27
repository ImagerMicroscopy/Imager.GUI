using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using System;
using System.ComponentModel;
using System.Collections.Generic;
using ScottPlot.Avalonia;
using System.Linq;
using System.Timers;

using ImagerAvalonia.ViewModels;


namespace ImagerAvalonia.Views;

public partial class ContrastPanelView : UserControl
{


   
    private AvaPlot histogram_plot;
    private int selected_row = 0;
    private int selected_col = 0;
    private DispatcherTimer _refreshTimer;
    private ContrastAdjViewModel _viewModel;

    private Timer _throttleTimer;
    private bool _canFire = true;
    private readonly object _lockHistogram = new();




    public Dictionary<int, WriteableBitmap> GridImages;
    public Dictionary<ValueTuple<int, int>, int> FlattenedGrid;



    public event EventHandler? UpdateContrastOnCurrentImage;

    public List<List<ContrastAdjViewModel>> contrastSettings = new();



    public ContrastPanelView()
    {
        //SliderMaxPanel = this.Find<StackPanel>("SliderMaxPanel");
        //SliderMinPanel = this.Find<StackPanel>("SliderMinPanel");

        _viewModel = new ContrastAdjViewModel();
        _viewModel.OnHistogramUpdateRequested += _viewModel_OnHistogramUpdateRequested;
        DataContext = _viewModel;

        InitializeComponent();
        histogram_plot = this.Find<AvaPlot>("HistogramPlot")!;
        if (histogram_plot != null)
        {
            histogram_plot.Plot.FigureBackground.Color = ScottPlot.Color.Gray(180);
            histogram_plot.Plot.DataBackground.Color = ScottPlot.Color.Gray(180);
            histogram_plot.Plot.XLabel("Intensity",10);
            histogram_plot.Plot.YLabel("Counts", 10);
            histogram_plot.Plot.Axes.Bottom.TickLabelStyle = new ScottPlot.LabelStyle() { FontSize = 8 };
            histogram_plot.Plot.Axes.Left.TickLabelStyle = new ScottPlot.LabelStyle() { FontSize = 8 };

        }


        _throttleTimer = new System.Timers.Timer(20) { AutoReset = false };
        _throttleTimer.Elapsed += (_, _) => { System.Diagnostics.Debug.WriteLine("Elapsed timer"); _canFire = true; };

        UpdateContrastOnCurrentImage += (s, e) =>
        {
            if (_canFire)
            {
                _canFire = false;
                _throttleTimer.Start();
            }
        };
        DataContextChanged += ContrastPanelView_DataContextChanged;
    }

    public void ContrastPanelView_DataContextChanged(object? sender, EventArgs e)
    {
        if(DataContext is ContrastAdjViewModel vm)
        {
            vm.OnHistogramUpdateRequested += _viewModel_OnHistogramUpdateRequested;
        }
    }



    public void _viewModel_OnHistogramUpdateRequested(double[] histogram_vals_x, double[] histogram_vals_y_low, double[] histogram_vals_y)
    {
        UpdateHistogram(histogram_vals_y, histogram_vals_x, histogram_vals_y_low);
    }

    public void PopulateContrastSettings(object? sender, GridContrastEventArgs row_col_ind)
    {
        while (contrastSettings.Count <= row_col_ind.row)
        {
            contrastSettings.Add(new List<ContrastAdjViewModel>());
        }

        while (contrastSettings[row_col_ind.row].Count <= row_col_ind.col)
        {
            contrastSettings[row_col_ind.row].Add(null); 
        }

        contrastSettings[row_col_ind.row][row_col_ind.col] = new ContrastAdjViewModel();
    }

    public void SetSliderVisibility(bool visibility)
    {
        SliderMaxPanel = this.Find<Border>("SliderMaxPanel")!;
        SliderMinPanel = this.Find<Border>("SliderMinPanel")!;
        SliderMaxPanel.IsVisible = visibility;
        SliderMinPanel.IsVisible = visibility;
    }

    public void SetToggleAutoContrastVisibility(bool visibility)
    {
        var AutoContrastToggle = this.Find<Border>("AutoContrastPanel")!;
        AutoContrastToggle.IsVisible = visibility;

    }

    public void SetHistogramVisibility(bool visibility)
    {
       histogram_plot.IsVisible = visibility;
    }

    public void SetCurrentContrastVals(object sender, GridContrastEventArgs row_col_ind)
    {
        DataContext = contrastSettings[row_col_ind.row][row_col_ind.col];

        contrastSettings[row_col_ind.row][row_col_ind.col].PropertyChanged += OnContrastValuesChanged;

        selected_row = row_col_ind.row;
        selected_col = row_col_ind.col;
    }

    public void OnContrastValuesChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_canFire)
        {
            return; // Discard this event
        }
        UpdateContrastOnCurrentImage?.Invoke(this, new EventArgs());
    }




    public void UpdateHistogram(double[] histogram_vals_y, double[] histogram_vals_x, double[] histogram_vals_y_low)
    {
        lock (_lockHistogram)
        {
            
            histogram_plot.Plot.Clear();
            var fill = histogram_plot.Plot.Add.FillY(histogram_vals_x, histogram_vals_y, histogram_vals_y_low);
            fill.FillColor = ScottPlot.Colors.Blue.WithAlpha(100);
            fill.LineColor = ScottPlot.Colors.Blue;
            fill.MarkerColor = ScottPlot.Colors.Blue;
            fill.LineWidth = 2;
            histogram_plot.Plot.Axes.SetLimits(histogram_vals_x.Min(), histogram_vals_x.Max(), 0, histogram_vals_y.Max());

            if (_refreshTimer == null)
            {
                _refreshTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(20),
                };
                _refreshTimer.Tick += (s, e) =>
                {
                    _refreshTimer.Stop();
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        histogram_plot.Refresh();
                    }, DispatcherPriority.Background);
                };
            }

            _refreshTimer.Stop();
            _refreshTimer.Start();
        }
        
    }




    //public byte[] UpdateContrastAndHistogramValues(byte[] image_array, ref double[] histogram_vals_y, ref double[] histogram_vals_y_low, ref double[] histogram_vals_x, int row, int col)
    //{



    //    ushort min_val = (ushort)_contrastSettings[row][col].Min_disp_val;
    //    ushort max_val = (ushort)_contrastSettings[row][col].Max_disp_val;

    //    ushort[] converted_array = new ushort[image_array.Length / 2];

    //    //double[] histogram_vals_y = new double[257];
    //    for (int hist_val = 0; hist_val < histogram_vals_y.Length; hist_val++)
    //    {
    //        histogram_vals_y[hist_val] = 0;
    //    }

    //    //double[] histogram_vals_y_low = new double[257];
    //    for (int hist_val = 0; hist_val < histogram_vals_y.Length; hist_val++)
    //    {
    //        histogram_vals_y[hist_val] = 0;
    //    }



    //    Buffer.BlockCopy(image_array, 0, converted_array,0, image_array.Length);


    //    double min_image_val = converted_array.Min();
    //    double max_image_val = converted_array.Max();
    //    if (_contrastSettings[row][col].IsAutoContrastEnabled)
    //    {
    //        min_val = Convert.ToUInt16(min_image_val);
    //        max_val = Convert.ToUInt16(max_image_val);
    //    }

    //    double bin_size = (max_image_val - min_image_val) / 256;


    //    //double[] histogram_vals_x = new double[257];
    //    for (int hist_val = 0; hist_val < histogram_vals_x.Length; hist_val++)
    //    {
    //        histogram_vals_x[hist_val] = min_image_val + hist_val * bin_size;
    //    }

    //    //var timer = new System.Diagnostics.Stopwatch();
    //    //timer.Start();


    //    unsafe
    //    {
    //        fixed (ushort* ptr = converted_array)
    //        {
    //            for (int i = 0; i < converted_array.Length; i++)
    //            {
    //                double bin_value = (double)ptr[i] - min_image_val;
    //                int bin_position = 0;

    //                if (bin_value != 0)
    //                {
    //                    bin_position = (int)(bin_value / bin_size);
    //                }
 
    //                histogram_vals_y[bin_position] += 1;

    //                if (ptr[i] < max_val && ptr[i] > min_val)
    //                {
    //                    ptr[i] = (ushort)((float)((ptr[i] - min_val))/(max_val-min_val)*ushort.MaxValue);
    //                }
    //                else if (ptr[i] < min_val)
    //                {
    //                    ptr[i] = 0;
    //                }
    //                else if (ptr[i] > max_val)
    //                {
    //                    ptr[i] = ushort.MaxValue;
    //                }
    //            }
    //        }
    //    }





    //    Buffer.BlockCopy(converted_array, 0, image_array, 0, converted_array.Length * 2);


    //    return image_array;
    //}







    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }


}





