
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using ImagerAvalonia.Services.MeasurementControl;


namespace ImagerAvalonia.ViewModels;

public partial class SourcesViewModel : ViewModelBase
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _equipmentName;
    [ObservableProperty] private string _displayedName;
    [ObservableProperty] private ObservableCollection<Channel> _channels;

    private readonly Source _lightSource;
    public Source LightSource => _lightSource;





    public SourcesViewModel(Source src)
    {
        Name = src.LightSourceName;

        _lightSource = src;
        EquipmentName = src.EquipmentName;
        DisplayedName = $"{EquipmentName}/{Name}";

        Channels = new ObservableCollection<Channel>();


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

    private void Channel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
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

    }
    public override void Dispose()
    {

    }





    public JObject Serialize() => LightSource.Serialize();



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

