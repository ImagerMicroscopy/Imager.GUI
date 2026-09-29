using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;


namespace ImagerAvalonia.ViewModels
{
    public partial class UpdateAcquisitionViewModel : MeasurementElementViewModel
    {
        [ObservableProperty]
        private ObservableCollection<ToUpdateAcquisition> toUpdateAcquisitions = new();

        private readonly GlobalDefinedSettingsViewModel _acquisitions;

        public UpdateAcquisitionViewModel(GlobalDefinedSettingsViewModel acquisitions)
        {
            _acquisitions = acquisitions;
            ToUpdateAcquisitions = new ObservableCollection<ToUpdateAcquisition>(
                acquisitions.Acquisitions.Select(x => {
                    var acq = new ToUpdateAcquisition(x.Name, false);
                    return acq;
                })
            );
            if (ToUpdateAcquisitions.Count > 0)
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
            _acquisitions.Acquisitions.CollectionChanged -= Acquisitions_CollectionChanged;
            base.Dispose();
        }


        public override MeasurementElementBase ToModel()
        {
            return new UpdateAcquisition
            {
                AcquisitionTypeName = ToUpdateAcquisitions.FirstOrDefault(a => a.Enabledupdate)?.Name ?? "",
                ElementId = Elementid.ToString(),
                DetectionName = ToUpdateAcquisitions.FirstOrDefault(a => a.Enabledupdate)?.Name ?? ""  ,
                SmartProgramID = SmartProgramBindings.FirstOrDefault(b => b != null)?.SmartProgramID.ToString()
            };
        }

        public override void LoadFromModel(MeasurementElementBase model, LoadContext context)
        {
            if (model is not UpdateAcquisition update)
                throw new ArgumentException($"Expected {nameof(UpdateAcquisition)}", nameof(model));

            base.LoadFromModel(model, context);

            // Saved names may have been made unique when the project was loaded.
            var savedName = update.AcquisitionTypeName;
            if (context.AcquisitionNameMap.TryGetValue(savedName, out var renamed))
                savedName = renamed.Name;

            var match = ToUpdateAcquisitions.FirstOrDefault(a => a.Name == savedName);
            if (match is null)
                return;

            foreach (var acquisition in ToUpdateAcquisitions)
                acquisition.Enabledupdate = acquisition == match;
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