using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ImagerAvalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ImagerAvalonia.Views
{
    /// <summary>
    /// The image display surface: one tile per slot view model (acquisition x
    /// detector), laid out in equal, star-sized grid cells that share the available
    /// space. Structure and selection state live in the view models
    /// (ImageDisplayViewModel.Slots / SelectedSlot); this control only lays out the
    /// tiles, routes selection clicks, and manages the maximized-tile mode.
    /// Zooming and panning are per tile (see ImageCanvas); this grid has no
    /// transform of its own.
    /// </summary>
    internal class ImageGrid : Grid
    {
        private readonly Dictionary<(string, string), ImageCanvas> _imageData = new();

        /// <summary>The tile currently filling the whole grid area, if any.
        /// Double-click a tile to maximize, double-click again (or Esc, routed by
        /// the hosting view) to restore.</summary>
        private ImageCanvas? _maximizedTile;

        public ImageCanvas? SelectedImage { get; private set; }

        /// <summary>Raised when a tile is selected by a click; carries the slot's
        /// view model, which owns the IsSelected flag the tiles highlight with.</summary>
        public event EventHandler<ImageCanvasViewModel>? OnSelectedItemChanged;

        public ImageRegionDisplayViewModel? SharedRegionViewModel;

        public ImageGrid()
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        }

        /// <summary>
        /// Builds the tiles from the slot view models. The grid is laid out with a
        /// horizontal priority to match typical wide screens: for N slots it uses
        /// floor(sqrt(N)) rows and ceil(N/rows) columns, filling row-major in slot
        /// order — two slots end up side by side, not stacked. Every cell is
        /// star-sized, so the available space is shared equally, whatever the
        /// window size.
        /// </summary>
        public void Initialize(IReadOnlyList<ImageCanvasViewModel> slots, ImageRegionDisplayViewModel? sharedRegionViewModel)
        {
            RowDefinitions.Clear();
            ColumnDefinitions.Clear();
            Children.Clear();
            _imageData.Clear();
            _maximizedTile = null;
            SelectedImage = null;

            SharedRegionViewModel = sharedRegionViewModel;
            if (SharedRegionViewModel == null || slots.Count == 0)
                return;

            var detectors = new List<string>();
            var acquisitions = new List<string>();
            foreach (var slot in slots)
            {
                if (!acquisitions.Contains(slot.AcqName)) acquisitions.Add(slot.AcqName);
                if (!detectors.Contains(slot.DetName)) detectors.Add(slot.DetName);
            }

            int rowCount = (int)Math.Floor(Math.Sqrt(slots.Count));
            if (rowCount < 1) rowCount = 1;
            int columnCount = (int)Math.Ceiling((double)slots.Count / rowCount);

            for (int r = 0; r < rowCount; r++)
            {
                RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }
            for (int c = 0; c < columnCount; c++)
            {
                ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            SharedRegionViewModel.Acquisitions = new ObservableCollection<string>(acquisitions);
            SharedRegionViewModel.Detectors = new ObservableCollection<string>(detectors);

            int index = 0;
            foreach (var slot in slots)
            {
                var tile = new ImageCanvas(slot, SharedRegionViewModel)
                {
                    DataContext = slot
                };
                tile.PointerPressed += Tile_OnPointerPressed;
                tile.DoubleTapped += Tile_OnDoubleTapped;
                Grid.SetColumn(tile, index % columnCount);
                Grid.SetRow(tile, index / columnCount);
                Children.Add(tile);
                _imageData[(slot.AcqName, slot.DetName)] = tile;
                index++;
            }

            if (Children.Count > 0 && Children[0] is ImageCanvas firstTile)
            {
                SelectedImage = firstTile;
            }
        }

        private void Tile_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is ImageCanvas tile && tile.DataContext is ImageCanvasViewModel slot &&
                e.GetCurrentPoint(tile).Properties.IsLeftButtonPressed)
            {
                SelectedImage = tile;
                OnSelectedItemChanged?.Invoke(this, slot);
            }
        }

        private void Tile_OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (sender is ImageCanvas tile)
            {
                if (_maximizedTile == tile)
                {
                    RestoreMaximizedTile();
                }
                else
                {
                    if (_maximizedTile != null)
                    {
                        RestoreMaximizedTile();
                    }
                    foreach (var child in Children)
                    {
                        child.IsVisible = ReferenceEquals(child, tile);
                    }
                    Grid.SetRowSpan(tile, Math.Max(1, RowDefinitions.Count));
                    Grid.SetColumnSpan(tile, Math.Max(1, ColumnDefinitions.Count));
                    _maximizedTile = tile;
                    tile.FitWorld();
                }
            }
        }

        /// <summary>Restores the maximized tile, if any; a no-op otherwise.</summary>
        public void RestoreMaximizedTile()
        {
            if (_maximizedTile == null) return;
            Grid.SetRowSpan(_maximizedTile, 1);
            Grid.SetColumnSpan(_maximizedTile, 1);
            foreach (var child in Children)
            {
                child.IsVisible = true;
            }
            _maximizedTile.FitWorld();
            _maximizedTile = null;
        }

        /// <summary>Restores every tile to its fitted (unzoomed) state.</summary>
        public void ResetView()
        {
            foreach (var tile in _imageData.Values)
            {
                tile?.FitWorld();
            }
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
