using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ImagerAvalonia.Views;

public partial class ImageElementCollectionsView : Window
{
    private ImageElementCollectionsViewModel _viewModel;
    private List<string> CollectionNames = new();
    private List<string> AcquisitionNames = new();

    public ImageElementCollectionsView()
    {
        InitializeComponent();

        _viewModel = new ImageElementCollectionsViewModel();
        DataContext = _viewModel;
        Closing += ImageElementCollectionsView_Closing;

    }

    private void ImageElementCollectionsView_Closing(object? sender, WindowClosingEventArgs e)
    {
        e.Cancel = true;
        this.Hide();

    }



    internal void RemoveElementPlot(string collectionname)
    {
        _viewModel.PlotElements = new ObservableCollection<ElementPlotViewModel>(
            _viewModel.PlotElements.Where(x => x.CollectionName != collectionname).ToList()) ;
    }

    internal void AddElementPlot(string detname, string acqname,ElementPlotViewModel elementPanelViewModel)
    {
        if (AcquisitionNames.Contains(acqname))
        {
            int col_index = AcquisitionNames.IndexOf(acqname);
        }


        if (AcquisitionNames.Contains(acqname))
        {
            int col_index = AcquisitionNames.IndexOf(acqname);  
        }



        if (_viewModel.PlotElements.Any(x => (x.CollectionName == elementPanelViewModel.CollectionName &&
            x.DetectorName == elementPanelViewModel.DetectorName && x.AcquisitionName == elementPanelViewModel.AcquisitionName)))
        {
            Show();
            return;
        }

        _viewModel.PlotElements.Add(elementPanelViewModel);
        Show();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }


    private void IncreaseValue(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.TemplatedParent is NumericUpDown numericUpDown)
        {
            if (numericUpDown.Value == null)
            {
                numericUpDown.Value = numericUpDown.Minimum;
            }
            else
            {
                numericUpDown.Value = Math.Min((decimal)numericUpDown.Value + (decimal)numericUpDown.Increment, (decimal)numericUpDown.Maximum);
            }
        }
    }


    private void DecreaseValue(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.TemplatedParent is NumericUpDown numericUpDown)
        {
            if (numericUpDown.Value == null)
            {
                numericUpDown.Value = numericUpDown.Minimum;
            }
            else
            {
                numericUpDown.Value = Math.Max((decimal)numericUpDown.Value - (decimal)numericUpDown.Increment, (decimal)numericUpDown.Minimum);
            }
        }
    }

    internal ObservableCollection<ElementPlotViewModel> GetElementPlots()
    {
        return _viewModel.PlotElements;
    }

    internal void SetGridRows(int num_rows)
    {
        _viewModel.NumberOfCollections = num_rows;  
    }

    internal void ClearPlots()
    {
        _viewModel.PlotElements.Clear();
    }
}

