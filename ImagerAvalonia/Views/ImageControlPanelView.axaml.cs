using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ImagerAvalonia.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;




namespace ImagerAvalonia.Views;

public partial class ImageControlPanelView : UserControl
{


    private Button _expEnabled;
    private Button _liveEnabled;

    private StreamGeometry _playButtonIcon;
    private StreamGeometry _pauseButtonIcon;
    private StreamGeometry? _recButtonIcon;
    private StreamGeometry? _stopButtonIcon;

    public ImageControlPanelView()
    {


        InitializeComponent();

        this.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        this.AddHandler(DragDrop.DropEvent, OnDrop);
        DataContextChanged += ImageControlPanelView_DataContextChanged;




        _liveEnabled = this.FindControl<Button>("EnableLive")!;
        _expEnabled = this.FindControl<Button>("StartExperiment")!;

        _playButtonIcon = (StreamGeometry)this.FindResource("play_circle_regular");
        _pauseButtonIcon = (StreamGeometry)this.FindResource("pause_regular");
        _recButtonIcon = (StreamGeometry)this.FindResource("record_regular");
        _stopButtonIcon = (StreamGeometry)this.FindResource("stop_regular");

    }


    private void ImageControlPanelView_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ImageControlPanelViewModel viewModel)
        {
            viewModel.PropertyChanged += OnExperimentEnabled;
            viewModel.PropertyChanged += OnLiveEnabled;

            // Build the fixed tabs' views up front so they are subscribed to their
            // view models from the start: the Field view accumulates images, and the
            // Smart Program view picks up programs restored when a project is loaded.
            // (When the tabs were declared in XAML, their views were built at startup.)
            if (this.FindResource("ImageTabContentTemplate") is ImageTabContentTemplate template)
            {
                foreach (var tab in viewModel.Tabs.Where(t => t.IsFixed))
                    template.EnsureBuilt(tab);
            }
        }
    }


    private void OnLiveEnabled(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IsLiveEnabled")
        {
            _expEnabled.IsEnabled = !_expEnabled.IsEnabled;
            var livebutton_text = this.FindControl<TextBlock>("LiveButtonText");
            var livebutton_icon = this.Find<PathIcon>("PlayButtonDisplayIcon");


            if (livebutton_text.Text == "Live")
            {
                livebutton_text.Text = "Stop";
                livebutton_icon.Data = _pauseButtonIcon;
            }
            else
            {
                livebutton_text.Text = "Live";
                livebutton_icon.Data = _playButtonIcon;

            }

        }
    }


    private void OnTabCloseButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ImageTabViewModel tab } &&
            DataContext is ImageControlPanelViewModel viewModel)
        {
            // Removes the tab unless it is fixed or its measurement is running.
            viewModel.CloseImageTab(tab);
        }
    }

    private void OnExperimentEnabled(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IsExperimentEnabled")
        {
            var expbutton_text = this.FindControl<TextBlock>("ExperimentButtonText");
            var recbutton_icon = this.Find<PathIcon>("RecButtonDisplayIcon");

            if (expbutton_text.Text == "Start")
            {
                expbutton_text.Text = "Stop";
                recbutton_icon.Data = _stopButtonIcon;
                _liveEnabled.IsEnabled = false;

            }
            else
            {
                recbutton_icon.Data = _recButtonIcon;
                expbutton_text.Text = "Start";
                _liveEnabled.IsEnabled = true;

            }
        }
    }


    private void OnDragOver(object? sender, DragEventArgs e)
    {

        e.DragEffects = DragDropEffects.Copy;

    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is ImageControlPanelViewModel viewModel)
        {
            var filePaths = e.Data.GetFiles();
            if (filePaths != null)
            {
                foreach (var path in filePaths)
                {
                    viewModel.OpenFileInNewTab(path.Path.ToString());
                }
            }
        }
    }

    private void OnTabPointerDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not ImageTabViewModel tab) return;
        if (DataContext is not ImageControlPanelViewModel viewModel) return;
        // Fixed tabs and running measurements cannot be popped out; a tab that was
        // just closed (double-tap on its close button) is no longer in the collection.
        if (!tab.IsClosable || !viewModel.Tabs.Contains(tab)) return;

        viewModel.Tabs.Remove(tab);

        // The popped-out window shows the tab's cached view; closing the window closes
        // the tab. The same template instance is shared, so the window gets the view the
        // tab strip was showing instead of building a new one.
        var newWindow = new Window
        {
            Title = tab.Header,
            Content = new ContentPresenter
            {
                Content = tab,
                ContentTemplate = this.FindResource("ImageTabContentTemplate") as IDataTemplate
            }
        };
        newWindow.Closed += (_, _) => viewModel.CloseImageTab(tab);
        newWindow.Show();
    }


    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }
}



