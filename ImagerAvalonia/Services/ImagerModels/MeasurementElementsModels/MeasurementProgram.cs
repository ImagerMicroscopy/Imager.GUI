using ImagerAvalonia.Services.MeasurementControl;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels
{
    public class MeasurementProgram
    {
        public MeasurementElementBase Program { get; set; }
        public Dictionary<string, DetectionParams> Detections { get; set; }
        public string ApiVersion { get; set; } = "2.0";
         

        [JsonConstructor]
        public MeasurementProgram(MeasurementElementBase program,
            Dictionary<string, DetectionParams> detections)
        {
            Program = program;
            Detections = detections;
        }
    }
}
