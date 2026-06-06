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

        public UpdateAcquisitionViewModel(SystemDefinedSettingsViewModel acquisitions) {
            ToUpdateAcquisitions = new ObservableCollection<ToUpdateAcquisition>
                (acquisitions.Acquisitions.Select(x => {
                    var acq = new ToUpdateAcquisition(x.Name, false);
                    acq.PropertyChanged += ToUpdateAcquisition_PropertyChanged;
                    return acq;
                }));
            acquisitions.Acquisitions.CollectionChanged += Acquisitions_CollectionChanged;
            this.PropertyChanged += UpdateAcquisitionViewModel_PropertyChanged;
        }

        private void UpdateAcquisitionViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(SelectedProgramId) && ExperimentBuilder != null) {
                var detectionName = ToUpdateAcquisitions.FirstOrDefault(a => a.Enabledupdate)?.Name;
                ExperimentBuilder.UpdateUpdateAcquisition(
                    Elementid,
                    detectionName,
                    SelectedProgramId?.SmartProgramID.ToString());
            }
        }

        private void ToUpdateAcquisition_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(ToUpdateAcquisition.Enabledupdate) && ExperimentBuilder != null) {
                var acq = sender as ToUpdateAcquisition;
                if (acq != null) {
                    string? detectionName = acq.Enabledupdate ? acq.Name : null;
                    ExperimentBuilder.UpdateUpdateAcquisition(
                        Elementid,
                        detectionName,
                        SelectedProgramId?.SmartProgramID.ToString());
                }
            }
        }

        private void Acquisitions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            if (sender is ObservableCollection<AcquisitionSettingsViewModel> acquisitions) {
                var to_update_acq_names = ToUpdateAcquisitions.Select(x => x.Name);
                var updated_acqs = acquisitions.Select(x => x.Name).ToList();

                foreach (var acquisition in acquisitions) {
                    if(!to_update_acq_names.Contains(acquisition.Name)) {
                        var newAcq = new ToUpdateAcquisition(acquisition.Name, false);
                        newAcq.PropertyChanged += ToUpdateAcquisition_PropertyChanged;
                        ToUpdateAcquisitions.Add(newAcq);
                    }
                }
                var to_remove_acqs = ToUpdateAcquisitions.Where(x => !updated_acqs.Contains(x.Name)).ToList();
                ToUpdateAcquisitions = new ObservableCollection<ToUpdateAcquisition>
                    (ToUpdateAcquisitions.Except(to_remove_acqs));
            }
        }

        public override void Dispose() {
            this.PropertyChanged -= UpdateAcquisitionViewModel_PropertyChanged;
            foreach (var acq in ToUpdateAcquisitions) {
                acq.PropertyChanged -= ToUpdateAcquisition_PropertyChanged;
            }
        }
    }

    public partial class ToUpdateAcquisition : ViewModelBase
    {
        [ObservableProperty] string _name;
        [ObservableProperty] bool _enabledupdate;

        public ToUpdateAcquisition(string name, bool enabledupdate) {
            Name = name;
            Enabledupdate = enabledupdate;
        }
    }
}
