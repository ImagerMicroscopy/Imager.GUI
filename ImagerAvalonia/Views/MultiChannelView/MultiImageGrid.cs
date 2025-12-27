using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.Views.MultiChannelView
{
    internal class MultiImageGrid : ImageGrid, IImageGridInitializable
    {
        private MultiChannelViewModel _viewModel;
        private MultiChannelContrastViewModel _contrastViewModel;
        private const int NUM_CHANNELS = 5;
        private MultiImageCanvas _multiImageCanvas;
        private static readonly Dictionary<string, int> _channelMaps = new Dictionary<string, int>()
        {
            {"Red", 0 },
            {"Green",1 },
            {"Blue",2 },
            {"Yellow",3 },
            {"Transmission",4 }
        };

        private static readonly List<byte[][]> _imageChannels = new();


        public MultiImageGrid(MultiChannelViewModel multiChannelConfigViewModel, MultiChannelContrastViewModel contrastVM)
        {
            DataContext = multiChannelConfigViewModel;
            _viewModel = multiChannelConfigViewModel;
            _contrastViewModel = contrastVM;
            _imageChannels.Add(new byte[5][] );
            SetupRenderTransform();

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


        public new void Initialize(List<string> acquisitions, List<string> detectors)
        {
            RowDefinitions.Clear();
            ColumnDefinitions.Clear();
            Children.Clear();



            _multiImageCanvas = new MultiImageCanvas();
            _imageChannels.Clear();
            _imageChannels.Add(new byte[5][]);

            Grid.SetColumn(_multiImageCanvas, 0);
            Grid.SetRow(_multiImageCanvas, 0);
            Children.Add(_multiImageCanvas);

        }


        public async Task ReceiveChannelImage(string acq, string det, string elementid, int imageSizeX, int imageSizeY, byte[] image)
        {
            var channels = _viewModel.GetChannel(det, acq, elementid);
            foreach(var channel in channels)
            {
                if(_channelMaps.TryGetValue(channel, out int channel_ind))
                {
                    foreach(var img_queue in _imageChannels)
                    {
                        if (img_queue[channel_ind] is null)
                        {
                            img_queue[channel_ind] = image;
                            break;
                        }
                    }
                }
            }
            var userdefinedchannels = _viewModel.GetUserChannels();
            var current_occupied_channels = _imageChannels[0];
            bool are_all_channels_occupied = true;

            List<int> bitmap_lengths = new();
            foreach(var ch_ind in userdefinedchannels)
            {
                if (current_occupied_channels[ch_ind] is null)
                {
                    are_all_channels_occupied = false;
                    break;
                }
                else 
                {
                   bitmap_lengths.Add( current_occupied_channels[ch_ind].Length );
                } 
                    
            }
            if(are_all_channels_occupied) 
            {
                //&& bitmap_lengths.Select( x => x!=null).Any(o => o != bitmap_lengths[0]))
                await Dispatcher.UIThread.InvokeAsync(() =>
                {

                    _multiImageCanvas.UpdateMultiBitmap(
                      current_occupied_channels[0],
                      current_occupied_channels[1],
                      current_occupied_channels[2],
                      current_occupied_channels[3],
                      current_occupied_channels[4],
                      imageSizeX,
                      imageSizeY,
                      _contrastViewModel
                      );
                    _multiImageCanvas.SetBitmap();
                    _multiImageCanvas.InvalidateBitmap();


                });
                _imageChannels.RemoveAt(0);
                if(_imageChannels.Count==0)
                {
                    _imageChannels.Add(new byte[5][]);
                }
            }
        }
    }
}
