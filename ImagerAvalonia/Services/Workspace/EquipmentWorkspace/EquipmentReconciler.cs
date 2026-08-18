using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.MeasurementControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImagerAvalonia.Services.Workspace
{
    public static class EquipmentReconciler
    {
        public static DetectionParams Reconcile(DetectionParams incoming, EquipmentWorkspace equipment)
        {
            return new DetectionParams
            {
                Irradiation = equipment.AvailableSources
                    .Select(master => ReconcileSource(master, incoming.Irradiation))
                    .ToList(),

                MovableComponents = incoming.MovableComponents
                    .Select(m => ReconcileMovableComponent(m, equipment.AvailableFilterWheels))
                    .ToList(),

                // Detectors don't have the selected/available split problem — clone as-is
                Detectors = incoming.Detectors
                    .Select(d => new DetectorEquipmentModel(d))
                    .ToList()
            };
        }

        private static Source ReconcileSource(Source master, List<Source> incomingIrradiation)
        {
            var selected = incomingIrradiation.FirstOrDefault(s => s.EquipmentName == master.EquipmentName);

            var result = new Source(master); // pulls current AvailableChannels, name, capability flags

            if (selected != null)
            {
                result.LightsourceChannel = new List<string>(selected.LightsourceChannel);
                result.LightsourcePower = new List<int>(selected.LightsourcePower);
                result.IsEnabled = true;
            }
            else
            {
                // not selected at save time — present, but disabled, empty selection
                result.LightsourceChannel = new List<string>();
                result.LightsourcePower = new List<int>();
                result.IsEnabled = false;
            }

            return result;
        }
        private static MovableComponentModel ReconcileMovableComponent(MovableComponentModel incoming, IReadOnlyList<MovableComponentModel> available)
        {
            var master = available.First(m => m.equipmentname == incoming.equipmentname);

        
            var parts = master.movablecomponents
                .Select(p => new MovableComponentPart(p))
                .ToList();

            // Saved selections, keyed by component name.
            var incomingByName = incoming.movablecomponentsettings
                .Where(s => s != null)
                .ToDictionary(s => s!.ComponentName, s => s!);

            foreach (var part in parts)
            {
                if (!incomingByName.TryGetValue(part.Name, out var selected))
                    continue; // no saved selection for this component — keep master default

                switch (part.movablecomponent, selected)
                {
                    case (DiscreteMovableComponentPartProperties target, DiscreteMovableComponentPartProperties source):
                        target.desiredsetting = source.desiredsetting;
                        break;

                    case (ContinuousMovableComponentPartProperties target, ContinuousMovableComponentPartProperties source):
                        target.desiredsetting = source.desiredsetting;
                        break;
                }
            }
            return new MovableComponentModel(parts, master.equipmentname);
        }
    }
}
