using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using ImagerAvalonia.ViewModels;
using System;
using System.ComponentModel;
using System.IO;




namespace ImagerAvalonia.Views;

public partial class ImageControlPanelView : UserControl
{


    private TabControl _imageTabs;

    private Button _expEnabled;
    private Button _liveEnabled;
    private ImageDisplayView _imagePanel;

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
        _imageTabs = this.FindControl<TabControl>("TabLiveFocus")!;
        _imagePanel = (this.FindControl<UserControl>("ImagePanel")! as ImageDisplayView)!;

        _playButtonIcon = (StreamGeometry)this.FindResource("play_circle_regular");
        _pauseButtonIcon = (StreamGeometry)this.FindResource("pause_regular");
        _recButtonIcon = (StreamGeometry)this.FindResource("record_regular");
        _stopButtonIcon = (StreamGeometry)this.FindResource("stop_regular");
        // This is a hack. Avalonia does not load the DataContext for inactive tabs. 
        // This makes it so that the DataContextChanged event is not fired, even though
        // the data context for FieldView has been set. 

    }


    private void ImageControlPanelView_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ImageControlPanelViewModel viewModel)
        {
            viewModel.PropertyChanged += OnExperimentEnabled;
            viewModel.PropertyChanged += OnLiveEnabled;
            var fieldView = this.FindControl<UserControl>("FieldViewer")!;
            fieldView.DataContext = viewModel.FieldView;

        }
    }


    private void OnLiveEnabled(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IsLiveEnabled")
        {
            _imagePanel.SetImProgress(false);
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
        StyledElement parentcontrol = ((Control)sender).Parent;
        TabItem tb = null;

        while (parentcontrol != null && tb == null)
        {
            if (parentcontrol is TabItem item)
            {
                tb = item;
                break;
            }

            parentcontrol = parentcontrol.Parent;
        }

        if (tb != null)
        {
            _imageTabs.Items.Remove(tb);
        }

    }

    private void OnExperimentEnabled(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IsExperimentEnabled")
        {
            _imagePanel.SetImProgress(true);
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
                    TabItem tab = new TabItem();
                    ImageDisplayView imView = new ImageDisplayView();
                    Uri uri = new Uri(path.Path.ToString());
                    string localPath = uri.LocalPath;
                    string fileName = Path.GetFileName(localPath);


                    tab.Header = fileName;
                    tab.Content = imView;
                    tab.DoubleTapped += OnTabPointerDoubleTapped;
                    tab.Classes.Add("LocalTabItem");

                    viewModel.TifDataPath = path.Path.ToString();
                    localPath = localPath.Replace("\\", "/");
                    var imViewVm = new ImageDisplayViewModel();
                    imView.DataContext = imViewVm;
                    viewModel.LoadTifData(imViewVm, localPath);


                    var contentControl = tab.Content as ContentControl;

                    if (contentControl != null)
                    {
                        contentControl.Margin = new Avalonia.Thickness(0, -2, 0, 0);
                    }
                    _imageTabs.Items.Add(tab);


                    //viewModel.TifDataPath = path.Path.ToString();
                    //localPath = localPath.Replace("\\", "/");
                    //viewModel.LoadTifData((ImageDisplayViewModel)imView.DataContext, localPath);

                }
            }
        }
    }

    private void OnTabPointerDoubleTapped(object? sender, TappedEventArgs e)
    {
        var tabItem = sender as TabItem;

        if (tabItem != null)
        {
            var userControl = tabItem.Content as UserControl;

            if (userControl != null)
            {
                tabItem.Content = null;

                var newWindow = new Window
                {
                    Title = tabItem.Header.ToString(),
                    Content = userControl
                };
                _imageTabs.Items.Remove(tabItem);
                newWindow.Show();
            }
        }
    }


    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is TabControl tabView)
        {
            if (DataContext is ImageControlPanelViewModel vm)
            {
                if(tabView.SelectedContent is ImageDisplayView imageDisplay &&
                    imageDisplay.DataContext is ImageDisplayViewModel imVM)
                {
                    vm.SelectedView = imVM;
                }
            }
        }
    }
}




