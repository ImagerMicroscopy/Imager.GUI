
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;


namespace ImagerAvalonia.ViewModels;

public partial class SourcesEquipmentViewModel : ViewModelBase
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _equipmentName;
    [ObservableProperty] private string _displayedName;
    [ObservableProperty] private ObservableCollection<Channel> _channels;

    public Source LightSource;

    private readonly ImagerWorkspace? _imagerWorkspace;
    private readonly ExperimentManager? _experimentManager;
    private int _channelPropertyChangedDepth;




    public SourcesEquipmentViewModel(Source src, ImagerWorkspace? imagerWorkspace = null,
        ExperimentManager? experimentManager = null)
    {
        _imagerWorkspace = imagerWorkspace;
        _experimentManager = experimentManager;

        Name = src.LightSourceName;

        LightSource = src;
        EquipmentName = src.EquipmentName;
        DisplayedName = $"{EquipmentName}/{Name}";

        Channels = new ObservableCollection<Channel>();

        if (!src.allowmultiplechannels && src.LightsourceChannel.Count > 1)
        {
            // Sources without multiple simultaneous channels may have at most
            // one active channel — keep the first, drop the rest.
            src.LightsourceChannel.RemoveRange(1, src.LightsourceChannel.Count - 1);
            src.LightsourcePower.RemoveRange(1, src.LightsourcePower.Count - 1);
        }


        foreach (var available_channel in src.AvailableChannels)
        {
            if(src.LightsourceChannel.Contains(available_channel))
            {
                var activated_channel = new Channel(available_channel, src.LightsourcePower[src.LightsourceChannel.IndexOf(available_channel)]);
                activated_channel.IsEnabled = true;
                Channels.Add(activated_channel);
            }
            else
            {
                Channels.Add(new Channel(available_channel, 100));
            }
        }
      

        foreach (var channel in this.Channels)
        {
            channel.PropertyChanged += Channel_PropertyChanged;
        }

    }

    private async void Channel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // The single-channel exclusivity logic below changes another channel's
        // IsEnabled, which raises this handler again while it is still running.
        // Only the outermost change may restart the live view, otherwise one
        // user action would stop and start the live program twice.
        _channelPropertyChangedDepth++;
        try
        {



        if (sender is Channel changedChannel)
        {
            if (e.PropertyName == nameof(Channel.PowerLevel))
            {
                int channel_index = LightSource.LightsourceChannel.IndexOf(changedChannel.Name);
                if (channel_index >= 0)
                {
                    LightSource.LightsourcePower[channel_index] = changedChannel.PowerLevel;
                }
            }
            if (e.PropertyName == nameof(Channel.IsEnabled))
            {
                if (changedChannel.IsEnabled)
                {
                    if (!LightSource.allowmultiplechannels)
                    {
                        // At most one channel may be active: activating one
                        // deactivates the currently active channel.
                        foreach (var other in Channels)
                        {
                            if (other != changedChannel && other.IsEnabled)
                            {
                                other.IsEnabled = false;
                            }
                        }
                    }
                    if (!LightSource.LightsourceChannel.Contains(changedChannel.Name))
                    {
                        LightSource.LightsourceChannel.Add(changedChannel.Name);
                        LightSource.LightsourcePower.Add(changedChannel.PowerLevel);
                    }
                }
                else
                {
                    int index = LightSource.LightsourceChannel.IndexOf(changedChannel.Name);
                    if (index >= 0)
                    {
                        LightSource.LightsourceChannel.RemoveAt(index);
                        LightSource.LightsourcePower.RemoveAt(index);
                    }
                }
            }
        }
        if(LightSource.LightsourceChannel.Count==0)
        {
            LightSource.IsEnabled = false;
        }
        else
        {
            LightSource.IsEnabled = true;
        }

        }
        finally
        {
            _channelPropertyChangedDepth--;
        }

        if (_channelPropertyChangedDepth > 0)
            return;

        await RestartLiveIfAcquiringAsync();
    }

    // Light source settings are part of the detection configuration that was
    // serialized when the live program started, so a change during a live view
    // can only take effect by stopping and restarting the live program. A
    // change while a restart is still stopping the previous program is picked
    // up by that restart: StopLiveAsync has already left the acquiring state,
    // so no second restart is started.
    private async Task RestartLiveIfAcquiringAsync()
    {
        if (_imagerWorkspace == null || _experimentManager == null)
            return;
        if (_imagerWorkspace.CurrentState != WorkspaceState.Acquiring)
            return;

        await _imagerWorkspace.StopLiveAsync();
        await _imagerWorkspace.StartLiveAsync(_experimentManager.SelectedDetection);
    }
    public override void Dispose()
    {

    }



    public partial class  Channel : ViewModelBase
    {
        [ObservableProperty]
        private int _powerLevel = 100;
        [ObservableProperty]
        private string _name;
        [ObservableProperty]
        private bool _isEnabled;


        public Channel(string name, int powerlevel)
        {
            this.Name = name;
            this.PowerLevel = powerlevel;
            IsEnabled = false;

            

        }
        public override void Dispose()
        {

        }
    }

}

