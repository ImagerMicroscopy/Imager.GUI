using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ImagerAvalonia.ViewModels;
using System;
using System.Collections.Generic;

namespace ImagerAvalonia.Views
{
    public partial class ImageRegionDisplayView : UserControl
    {
        private ImageElementControlBase? _selectedRegion;
        private readonly ImageRegionDisplayViewModel _viewModel;
        private Canvas _elementCanvas = null!;

        public ImageRegionDisplayView(ImageRegionDisplayViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();

            _elementCanvas = this.FindControl<Canvas>("RegionsControl")!;
            DataContext = _viewModel;

            _viewModel.RegionsChanged += OnRegionsChanged;
            _viewModel.HideAllCollectionsRequested += OnHideAllCollectionsRequested; ;


            foreach (var regionParam in _viewModel.RegionParameters)
            {
                //var regtype = new RegionType()
                _elementCanvas.Children.Add(regionParam.GenerateRegionControl());
            }
        }


        private void OnHideAllCollectionsRequested(object? sender, EventArgs e)
        {
            foreach(var control in _elementCanvas.Children)
            {
                if(control.Name!="SelectionVisual")
                {
                    control.IsVisible = !control.IsVisible;
                }
            }            
        }

        private void OnRegionsChanged(object? sender, List<IImageElement> regionParameters)
        {
            _elementCanvas.Children.Clear();
            foreach (var regionParam in regionParameters)
            {
                _elementCanvas.Children.Add(regionParam.GenerateRegionControl());
            }
        }


        internal void SubmitSelection()
        {
            if (_selectedRegion is null)
                return;

            _elementCanvas.PointerMoved -= _selectedRegion.OnDotPointerMoved;
            _elementCanvas.PointerPressed -= _selectedRegion.OnPointerPressed;
            _elementCanvas.KeyDown -= _selectedRegion.ElementKeyDown;

            // Remove only elements related to selected region
            _elementCanvas.Children.Remove(_selectedRegion.AddedVisualType);
            foreach (var element in _selectedRegion.AddedRegions)
            {
                _elementCanvas.Children.Remove(element);
            }

            var regionName = $"Collection ID: {Guid.NewGuid()}";
            _selectedRegion.SetRegionName(regionName);

            _viewModel.AddRegions(_selectedRegion.RetrieveRegionParameters());
            _selectedRegion = null;
        }

        internal void EnableSelection(ImageElementControlBase region)
        {
            _selectedRegion = region;
            _elementCanvas.Children.Add(region.AddedVisualType);

            _elementCanvas.PointerMoved += region.OnDotPointerMoved;
            _elementCanvas.PointerPressed += region.OnPointerPressed;
            _elementCanvas.KeyDown += region.ElementKeyDown;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _viewModel.RegionsChanged -= OnRegionsChanged;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
