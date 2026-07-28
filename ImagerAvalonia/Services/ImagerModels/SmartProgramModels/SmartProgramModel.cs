using ImagerAvalonia.Services.ImagerModels.SmartProgramModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImagerAvalonia.Services.ImagerModels.SmartProgramModels
{
    public class SmartProgramModel
    {
        public Guid SmartProgramID { get; } = Guid.NewGuid();
        public SmartProgramDefinition SmartProgramDefinition = new();

        [JsonIgnore]
        public string LoadedFolder { get; set; } = string.Empty;
        [JsonIgnore]
        public Dictionary<string, string> ProgramFolders { get; } = new();


        public void RegisterProgram(string name, string path)
        {
            ProgramFolders[name] = path;
        }


        public void Clear()
        {
            SmartProgramDefinition.methods.Clear();
            SmartProgramDefinition.parameters.Clear();
            SmartProgramDefinition.acquisitionupdates.Clear();
        }


        public string? GetProgramPath()
        {
            return ProgramFolders.TryGetValue(
                SmartProgramDefinition.programname,
                out var path)
                ? path
                : null;
        }
    }


}

public class SmartProgramDefinition
{

    public string programname { get; set; } = string.Empty;

    public List<InputFunctionModel> methods { get; set; } = new();

    public List<InputParameterBase> parameters { get; set; } = new();

    public List<SmartAcquisitionUpdateModel> acquisitionupdates { get; } = new();

}