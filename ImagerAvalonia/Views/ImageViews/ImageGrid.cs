using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using ImagerAvalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;


namespace ImagerAvalonia.Views
{

    public interface IImageGridInitializable
    {  
        void Initialize(List<string> acquisitions, List<string> detectors);
    }

    internal class ImageGrid : Grid, IImageGridInitializable
    {

        private Dictionary<(string, string), ImageCanvas> _imageData = new Dictionary<(string, string), ImageCanvas>();
        private Point _lastMousePosition;
        private ImageGridViewModel _viewModel;
        

        public ImageCanvasViewModel SelectedCanvasViewModel => _viewModel.SelectedImage;
        public ImageCanvas? SelectedImage { get; private set; }

        public bool isDragging = false;
        public event EventHandler? OnSelectedItemChanged;

        public ImageRegionDisplayViewModel? SharedRegionViewModel;
        protected readonly TranslateTransform _translateTransform = new TranslateTransform { X = 0, Y = 0 };
        protected readonly ScaleTransform _scaleTransform = new ScaleTransform { ScaleX = 0.5, ScaleY = 0.5 };


        public ImageGrid()
        {
            SetupRenderTransform(); // optional default transform

            PointerWheelChanged += OnPointerWheelChanged;
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;

            _viewModel = new ImageGridViewModel();
            DataContext = _viewModel;
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        }




        private void SetupRenderTransform()
        {
            

            RenderTransform = new TransformGroup
            {
                Children = new Transforms
                {
                    _scaleTransform,                   
                    _translateTransform,

                }
            };
        }



        public void Initialize(List<string> acquisitions, List<string> detectors)
        {
            RowDefinitions.Clear();
            ColumnDefinitions.Clear();
            Children.Clear();
            _imageData.Clear(); 

            detectors.ForEach(_ => RowDefinitions.Add(new RowDefinition { Height = new GridLength(820) }));
            acquisitions.ForEach(_ => ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(820) }));


            if (SharedRegionViewModel == null)
                return;
            SharedRegionViewModel.Acquisitions = new ObservableCollection<string>(acquisitions);
            SharedRegionViewModel.Detectors = new ObservableCollection<string>(detectors);
            for (int col = 0; col < acquisitions.Count; col++)
            {
                for (int row = 0; row < detectors.Count; row++)
                {
                    string acquisition = acquisitions[col];
                    string detector = detectors[row];

                    var imagePanel = new ImageCanvas(acquisition, detector, SharedRegionViewModel);
                    imagePanel.DoubleTapped += OnImageSelected;

                    Grid.SetColumn(imagePanel, col);
                    Grid.SetRow(imagePanel, row);
                    Children.Add(imagePanel);

                    _imageData[(acquisition, detector)] = imagePanel;
                }
            }

            SelectedImage = null;
            if (_imageData.TryGetValue((acquisitions[0], detectors[0]), out var imCanvas))
            {
                if(imCanvas.DataContext is ImageCanvasViewModel imcanvasvm)
                {
                    _viewModel.SelectedImage = imcanvasvm;
                }
            }
        }





        private void OnImageSelected(object? sender, TappedEventArgs e)
        {
            if (sender is ImageCanvas imcanvas) 
            {
                SelectedImage = imcanvas;
                foreach (var item in Children)
                {
                    item.Effect = null;

                }
                imcanvas.Effect = imcanvas.SelectedEffect();
                if(imcanvas.DataContext is ImageCanvasViewModel imvm)
                    _viewModel.SelectedImage = imvm;
                    

                OnSelectedItemChanged?.Invoke(this, new EventArgs());
            }
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            double zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
            _scaleTransform.ScaleX *= zoomFactor;
            _scaleTransform.ScaleY *= zoomFactor;


        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {

            isDragging = true;
            var visual = this.FindAncestorOfType<Canvas>();
            if (visual == null)
                return;
            _lastMousePosition = e.GetPosition(visual);
            
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {

            if (isDragging)
            {
                var visual = this.FindAncestorOfType<Canvas>();
                if (visual == null)
                    return;

                var currentPosition = e.GetPosition(visual);
                var delta = currentPosition - _lastMousePosition;

                _translateTransform.X += delta.X;
                _translateTransform.Y += delta.Y;

                _lastMousePosition = currentPosition;
            }
            
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            isDragging = false;
        }



        public ImageCanvas this[string key1, string key2]
        {
            get
            {
                if (_imageData.TryGetValue((key1, key2), out var value))
                    return value;
                else
                    throw new KeyNotFoundException($"Key ({key1}, {key2}) not found.");
            }
            set
            {
                _imageData[(key1, key2)] = value;
            }
        }
    }
}

