using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels
{
    public partial class MultiChannelViewModel: ViewModelBase
    {
        [ObservableProperty] ObservableCollection<ChannelViewModel> _availableChannels = new();

        public MultiChannelViewModel() 
        { 
            AvailableChannels.Add(new ChannelViewModel("Red"));
            AvailableChannels.Add(new ChannelViewModel("Green"));
            AvailableChannels.Add(new ChannelViewModel("Blue"));
            AvailableChannels.Add(new ChannelViewModel("Yellow"));
            AvailableChannels.Add(new ChannelViewModel("Transmission"));

        }

        public List<string> GetChannel(string det, string acq, string elementid)
        {
            return AvailableChannels
                .Where(x => x.IsEnabled && (x.GetChannelOccupation(det, acq, elementid) ?? false))
                .Select( x=> x.ChannelName)
                .ToList(); 
        }

        public IEnumerable<int> GetUserChannels()
        {
            return AvailableChannels.Where(x => x.IsEnabled).ToList().Select(x => AvailableChannels.IndexOf(x));
        }

        public partial class ChannelViewModel :ViewModelBase
        {
            [ObservableProperty] bool _isEnabled = false;
            [ObservableProperty] MultiChannelConfigChannelViewModel _configChannelViewModel = new();
            [ObservableProperty] string _channelName = string.Empty;

            public ChannelViewModel(string channelName)
            {
                ChannelName = channelName;
            }

            public void OnChannelAdded()
            {
                IsEnabled = true;
            }
            public void OnChannelRemoved()
            {
                IsEnabled = false;
            }

            public bool? GetChannelOccupation(string acq, string det, string elementid)
            {
                if(ConfigChannelViewModel.AcquisitionInput == null || ConfigChannelViewModel.DetectorInput==null
                    || ConfigChannelViewModel.SelectedDetection==null)
                {
                    return null;
                }

                return ConfigChannelViewModel.SelectedDetection.Elementid.ToString() == elementid &&
                   ConfigChannelViewModel.AcquisitionInput.Name == acq &&
                   ConfigChannelViewModel.DetectorInput.Name == det;
            }
        }
    }

}

