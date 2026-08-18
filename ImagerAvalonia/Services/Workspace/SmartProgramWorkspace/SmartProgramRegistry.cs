using ImagerAvalonia.Services.ImagerModels.SmartProgramModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImagerAvalonia.Services.Workspace.SmartProgramWorkspace
{
    public class SmartProgramRegistry
    {
        public List<SmartProgramModel> DefinedPrograms = new();

        internal JObject SerializeAllDags()
        {
            var serializedPrograms = JArray.FromObject(DefinedPrograms);

            var serializedDags = new JObject
            {
                ["code"] = serializedPrograms,
                ["type"] = "dagorchestratorcode"
            };
            return serializedDags;
        }
    }
}
