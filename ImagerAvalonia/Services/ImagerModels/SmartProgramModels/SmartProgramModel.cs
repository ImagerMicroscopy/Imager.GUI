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
        // Needs a (private) setter so Newtonsoft can restore the original ID
        // on deserialize - without it, every reload silently generates a new
        // random GUID here, which breaks DetectionElement.SmartProgramIds
        // matching (those are saved as strings against this ID) and any
        // other cross-reference to a specific SmartProgram made before save.
        public Guid SmartProgramID { get; set; } = Guid.NewGuid();
        public SmartProgramDefinition SmartProgramDefinition = new();

        /// <summary>
        /// The program's main .py file plus all locally-connected .py files,
        /// fetched from the Python API and kept around purely for storage
        /// (project save/load, TIFF metadata) so a SmartProgram's source
        /// survives across sessions/machines. Null until an export has been
        /// requested (or a bundle has been loaded) for this program.
        ///
        /// IMPORTANT: this must never reach the measurement backend
        /// (Haskell) - see SmartProgramRegistry.SerializeAllDags, which
        /// explicitly strips this field from the TCP payload it builds.
        /// </summary>
        public SmartProgramBundle? FileBundle { get; set; }

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