using ImagerAvalonia.Services.ImagerModels.SmartProgramModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImagerAvalonia.Services.Workspace.SmartProgramWorkspace
{
    public class SmartProgramRegistry
    {
        public List<SmartProgramModel> DefinedPrograms = new();

        // Haskell (the measurement backend on the other end of the TCP
        // connection) only needs the DAG wiring - it must never receive a
        // SmartProgram's bundled .py source. FileBundle is stripped here,
        // not via [JsonIgnore] on the model, because the same model IS
        // serialized with FileBundle included elsewhere (project save/load -
        // see FullEquipmentStateSerializer / SmartPrograms on FullEquipmentState).
        internal JObject SerializeAllDags()
        {
            var serializedPrograms = new JArray(
                DefinedPrograms.Select(program =>
                {
                    var jo = JObject.FromObject(program);
                    jo.Remove(nameof(SmartProgramModel.FileBundle));
                    return jo;
                }));

            var serializedDags = new JObject
            {
                ["code"] = serializedPrograms,
                ["type"] = "dagorchestratorcode"
            };
            return serializedDags;
        }
    }
}
