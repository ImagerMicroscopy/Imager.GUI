using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels
{
    public partial class UpdateAcquisitionViewModel : MeasurementViewModel
    {
        [ObservableProperty] ObservableCollection<ToUpdateAcquisition> _toUpdateAcquisitions = new();

        public UpdateAcquisitionViewModel(UserDefinedAcquisitions acquisitions)
        {
            ToUpdateAcquisitions = new ObservableCollection<ToUpdateAcquisition>
                ( acquisitions.Acquisitions.Select(x => new ToUpdateAcquisition(x.Name, false)));
            acquisitions.Acquisitions.CollectionChanged += Acquisitions_CollectionChanged;
        }

        private void Acquisitions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (sender is ObservableCollection<AcquisitionSettingsViewModel> acquisitions)
            {
                var to_update_acq_names = ToUpdateAcquisitions.Select(x => x.Name);
                var updated_acqs = acquisitions.Select(x => x.Name).ToList();

                foreach (var acquisition in acquisitions)
                {
                    if(!to_update_acq_names.Contains(acquisition.Name))
                    {
                        ToUpdateAcquisitions.Add(new ToUpdateAcquisition(acquisition.Name, false));
                    }
                }
                var to_remove_acqs = ToUpdateAcquisitions.Where(x => !updated_acqs.Contains(x.Name)).ToList();
                ToUpdateAcquisitions = new ObservableCollection<ToUpdateAcquisition>
                    ( ToUpdateAcquisitions.Except(to_remove_acqs));
            }
        }
    }

    public partial class ToUpdateAcquisition : ViewModelBase
    {
        [ObservableProperty] string _name;
        [ObservableProperty] bool _enabledupdate;

        public ToUpdateAcquisition(string name, bool enabledupdate)
        {
            Name = name;
            Enabledupdate = enabledupdate;
        }
    }
}
