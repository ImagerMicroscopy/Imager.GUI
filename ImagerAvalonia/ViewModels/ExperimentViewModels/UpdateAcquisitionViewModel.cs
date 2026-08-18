using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.MeasurementControl;
using System;
using System.Collections.ObjectModel;
using System.Linq;


namespace ImagerAvalonia.ViewModels
{
    public partial class UpdateAcquisitionViewModel : MeasurementElementViewModel
    {
        [ObservableProperty]
        private ObservableCollection<ToUpdateAcquisition> toUpdateAcquisitions = new();

        public UpdateAcquisitionViewModel(GlobalDefinedSettingsViewModel acquisitions)
        {
            ToUpdateAcquisitions = new ObservableCollection<ToUpdateAcquisition>(
                acquisitions.Acquisitions.Select(x => {
                    var acq = new ToUpdateAcquisition(x.Name, false);
                    return acq;
                })
            );
            ToUpdateAcquisitions[0].Enabledupdate = true;
            acquisitions.Acquisitions.CollectionChanged += Acquisitions_CollectionChanged;
            Header = "Update Acquisition";
        }

        private void Acquisitions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (sender is ObservableCollection<AcquisitionSettingsViewModel> acquisitions)
            {
                var to_update_acq_names = ToUpdateAcquisitions.Select(x => x.Name);
                var updated_acqs = acquisitions.Select(x => x.Name).ToList();

                foreach (var acquisition in acquisitions)
                {
                    if (!to_update_acq_names.Contains(acquisition.Name))
                    {
                        var newAcq = new ToUpdateAcquisition(acquisition.Name, false);
                        ToUpdateAcquisitions.Add(newAcq);
                    }
                }

                var to_remove_acqs = ToUpdateAcquisitions.Where(x => !updated_acqs.Contains(x.Name)).ToList();
                ToUpdateAcquisitions = new ObservableCollection<ToUpdateAcquisition>(
                    ToUpdateAcquisitions.Except(to_remove_acqs)
                );
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }


        public override MeasurementElementBase ToModel()
        {
            return new UpdateAcquisition
            {
                AcquisitionTypeName = ToUpdateAcquisitions.FirstOrDefault(a => a.Enabledupdate)?.Name ?? "",
                ElementId = Elementid.ToString(),
                DetectionName = ToUpdateAcquisitions.FirstOrDefault(a => a.Enabledupdate)?.Name ?? ""  ,
                SmartProgramID = SmartProgramBindings[0].SmartProgramID.ToString()
            };
        }
    }

    public partial class ToUpdateAcquisition : ViewModelBase
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private bool enabledupdate = false;

        public ToUpdateAcquisition(string name, bool enabledupdate)
        {
            Name = name;
            Enabledupdate = enabledupdate;
        }
    }
}