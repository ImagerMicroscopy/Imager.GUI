using ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels;
using ImagerAvalonia.Services.MeasurementControl;


namespace ImagerAvalonia.Services.Workspace.ExperimentWorkspace
{
    public class ExperimentLoaderService
    {
        public ExperimentLoaderService() { }

        public MeasurementProgram GetProgramTree(string programJson)
        {
            var programTree = MeasurementSerializer.Deserialize<MeasurementProgram>(programJson);
            return programTree;
        }
    }
}
