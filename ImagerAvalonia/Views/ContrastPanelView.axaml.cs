using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using System;
using System.Collections.Generic;
using ScottPlot.Avalonia;
using System.Linq;

using ImagerAvalonia.ViewModels;


namespace ImagerAvalonia.Views;

public partial class ContrastPanelView : UserControl
{


   
    private AvaPlot histogram_plot;
    private DispatcherTimer _refreshTimer;
    private ContrastAdjViewModel _viewModel;
    private ContrastAdjViewModel? _selectedContrastCell;

    private readonly object _lockHistogram = new();




    public event EventHandler? UpdateContrastOnCurrentImage;

    /// <summary>
    /// Contrast settings per grid cell, addressed by row/column. Used by the field
    /// viewer, which addresses its cells by index (see GridContrastEventArgs).
    /// </summary>
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
        var cell = contrastSettings[row_col_ind.row][row_col_ind.col];

        // Re-selecting a cell must not stack duplicate handlers: swap the
        // subscription to the newly selected cell.
        if (_selectedContrastCell != null)
        {
            _selectedContrastCell.OnContrastValsChanged -= SelectedCell_ContrastValsChanged;
        }

        _selectedContrastCell = cell;
        DataContext = cell;
        cell.OnContrastValsChanged += SelectedCell_ContrastValsChanged;
    }

    private void SelectedCell_ContrastValsChanged(object? sender, EventArgs e)
    {
        UpdateContrastOnCurrentImage?.Invoke(this, e);
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



    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }


}






/// <summary>
/// Addresses a contrast-settings grid cell by row and column. Used by the field
/// viewer, whose cells are index-based rather than acquisition/detector-keyed.
/// </summary>
public class GridContrastEventArgs : EventArgs
{
    public int row { get; }
    public int col { get; }
    public string? acquistion { get; }
    public string? detector { get; }

    public GridContrastEventArgs(int value1, int value2, string? acq, string? det)
    {
        row = value1;
        col = value2;

        acquistion = acq;
        detector = det;
    }
}
