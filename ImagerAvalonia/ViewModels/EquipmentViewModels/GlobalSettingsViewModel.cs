using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Workspace;
using ImagerAvalonia.ViewModels.MeasurementViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace ImagerAvalonia.ViewModels
{
    public partial class GlobalDefinedSettingsViewModel : ViewModelBase
    {
        [ObservableProperty] ObservableCollection<AcquisitionSettingsViewModel> _Acquisitions = new();
        private ImagerWorkspace? _imagerWorkspace;
        private ExperimentManager? _experimentManager;

        public List<RobotModel> Robots { get; set; } = new();


        public GlobalDefinedSettingsViewModel( )
        {

        }

        public IReadOnlyDictionary<string, AcquisitionSettingsViewModel> ReconcileAcquisitions(
            IEnumerable<(string Name, DetectionParams Params)> incoming,
            EquipmentWorkspace equipment)
        {
            var nameMap = new Dictionary<string, AcquisitionSettingsViewModel>();

            foreach (var (name, parameters) in incoming)
                nameMap[name] = CreateAcquisitionWithUniqueName(name, parameters, equipment);

            return nameMap;
        }

        private AcquisitionSettingsViewModel CreateAcquisitionWithUniqueName(
            string desiredName, DetectionParams incomingParams, EquipmentWorkspace equipment)
        {
            var uniqueName = GenerateUniqueName(desiredName, Acquisitions.Select(a => a.Name));

            var reconciled = EquipmentReconciler.Reconcile(incomingParams, equipment);

            var newAcq = new AcquisitionSettingsViewModel(
                uniqueName,
                reconciled.Irradiation,
                reconciled.MovableComponents,
                reconciled.Detectors,
                _imagerWorkspace,
                _experimentManager);

            Acquisitions.Add(newAcq);
            return newAcq;
        }

        private static string GenerateUniqueName(string baseName, IEnumerable<string> existingNames)
        {
            var existing = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
            if (!existing.Contains(baseName))
                return baseName;

            int suffix = 2;
            string candidate;
            do
            {
                candidate = $"{baseName} ({suffix})";
                suffix++;
            } while (existing.Contains(candidate));

            return candidate;
        }

        internal void SetImagerWorkSpace(ImagerWorkspace imagerWorkspace)
        {
            _imagerWorkspace = imagerWorkspace;
        }

        internal void SetExperimentManager(ExperimentManager experimentManager)
        {
            _experimentManager = experimentManager;
        }
    }

}
