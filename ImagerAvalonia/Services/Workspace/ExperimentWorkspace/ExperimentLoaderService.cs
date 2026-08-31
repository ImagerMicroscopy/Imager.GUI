using ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels;
using ImagerAvalonia.Services.MeasurementControl;


namespace ImagerAvalonia.Services.Workspace.ExperimentWorkspace
{
    public class ExperimentLoaderService
    {
        public ExperimentLoaderService() { }

        /// <summary>
        /// Extracts just the program tree + detections from a TIFF's embedded program
        /// descriptor, which now stores the full FullEquipmentState .imag document.
        /// Deliberately does not touch CurrentEquipment or SmartPrograms - reopening a
        /// TIFF only needs the program tree/detections it already ran with, not a
        /// SmartProgram reload or a hardware/acquisition reconciliation pass (those are
        /// specific to .imag project load, see ExperimentManager.ParseLoadedExperiment).
        /// </summary>
        public MeasurementProgram GetProgramTree(string programJson)
        {
            var fullEquipmentState = FullEquipmentStateSerializer.Deserialize(programJson);
            return fullEquipmentState.CurrentProgram;
        }
    }
}
