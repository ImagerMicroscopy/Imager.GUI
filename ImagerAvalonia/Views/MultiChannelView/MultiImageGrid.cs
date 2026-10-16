using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ImagerAvalonia.Views.MultiChannelView
{
    /// <summary>
    /// The multi-channel composite view: one canvas blending the configured
    /// channels. Unlike the tile grid, this is a single image on a free pan/zoom
    /// surface (wheel zoom anchored at the cursor, middle-drag pan), because there
    /// are no tiles to fit. Channel frames are synchronized per instance (see
    /// ChannelFrameBuffer), so two simultaneously active views each build their
    /// composite from their own frames only.
    /// </summary>
    internal class MultiImageGrid : ImageGrid
    {
        private MultiChannelViewModel _viewModel;
        private MultiChannelContrastViewModel _contrastViewModel;
        private MultiImageCanvas _multiImageCanvas;

        /// <summary>This view's channel frames; per instance, see the class comment.</summary>
        private readonly ChannelFrameBuffer _frameBuffer = new();

        private readonly ScaleTransform _scaleTransform = new(1, 1);
        private readonly TranslateTransform _translateTransform = new(0, 0);
        private bool _isPanning;
        private Point _lastPanPosition;

        private static readonly Dictionary<string, int> _channelMaps = new Dictionary<string, int>()
        {
            {"Red", 0 },
            {"Green",1 },
            {"Blue",2 },
            {"Yellow",3 },
            {"Transmission",4 }
        };


        public MultiImageGrid(MultiChannelViewModel multiChannelConfigViewModel, MultiChannelContrastViewModel contrastVM)
        {
            DataContext = multiChannelConfigViewModel;
            _viewModel = multiChannelConfigViewModel;
            _contrastViewModel = contrastVM;

            RenderTransform = new TransformGroup
            {
                Children = new Transforms
                {
                    _scaleTransform,
                    _translateTransform,
                }
            };
            // Avalonia's default render transform origin is (0.5, 0.5), which would
            // pivot the pan/zoom around the control's center and break the
            // cursor-anchored zoom math; pin it to the top-left corner.
            RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);

            PointerWheelChanged += OnPointerWheelChanged;
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
        }


        public void Initialize(List<string> acquisitions, List<string> detectors)
        {
            RowDefinitions.Clear();
            ColumnDefinitions.Clear();
            Children.Clear();


            _multiImageCanvas = new MultiImageCanvas();
            _frameBuffer.Clear();

            Grid.SetColumn(_multiImageCanvas, 0);
            Grid.SetRow(_multiImageCanvas, 0);
            Children.Add(_multiImageCanvas);

            // Reset the pan/zoom surface for the new measurement.
            _scaleTransform.ScaleX = 1;
            _scaleTransform.ScaleY = 1;
            _translateTransform.X = 0;
            _translateTransform.Y = 0;
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            var position = e.GetPosition(this);
            double oldScale = _scaleTransform.ScaleX;
            double factor = e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1;
            double newScale = Math.Clamp(oldScale * factor, 0.1, 32);
            if (newScale == oldScale) return;

            // Anchor the zoom at the cursor.
            _scaleTransform.ScaleX = newScale;
            _scaleTransform.ScaleY = newScale;
            _translateTransform.X = position.X - (newScale / oldScale) * (position.X - _translateTransform.X);
            _translateTransform.Y = position.Y - (newScale / oldScale) * (position.Y - _translateTransform.Y);
            e.Handled = true;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
            {
                _isPanning = true;
                _lastPanPosition = e.GetPosition(this);
                e.Handled = true;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPanning) return;

            var position = e.GetPosition(this);
            _translateTransform.X += position.X - _lastPanPosition.X;
            _translateTransform.Y += position.Y - _lastPanPosition.Y;
            _lastPanPosition = position;
            e.Handled = true;
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                e.Handled = true;
            }
        }

        public async Task ReceiveChannelImage(string acq, string det, string elementid, int imageSizeX, int imageSizeY, byte[] image, ImagePixelFormat format)
        {
            var channels = _viewModel.GetChannel(det, acq, elementid);
            foreach (var channel in channels)
            {
                if (_channelMaps.TryGetValue(channel, out int channelIndex))
                {
                    _frameBuffer.Add(channelIndex, image);
                }
            }

            // The composite is rendered only once every enabled channel has a
            // frame (see ChannelFrameBuffer); until then, this image stays
            // pending.
            if (!_frameBuffer.TryTakeCompleted(_viewModel.GetUserChannels().ToList(), out var frames))
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _multiImageCanvas.UpdateMultiBitmap(
                    frames[0],
                    frames[1],
                    frames[2],
                    frames[3],
                    frames[4],
                    imageSizeX,
                    imageSizeY,
                    _contrastViewModel,
                    format
                    );
                _multiImageCanvas.SetBitmap();
                _multiImageCanvas.InvalidateBitmap();
            });
        }
    }
}
