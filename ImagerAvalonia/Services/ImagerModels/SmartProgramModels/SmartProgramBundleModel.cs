using System.Collections.Generic;

namespace ImagerAvalonia.Services.ImagerModels.SmartProgramModels
{
    /// <summary>
    /// A single .py file's content, keyed by its path relative to the
    /// program's root folder (forward-slash separated). Field names match
    /// the Python API's SmartProgramFile wire shape exactly.
    /// </summary>
    public class SmartProgramFile
    {
        public string relative_path { get; set; } = string.Empty;
        public string content { get; set; } = string.Empty;
    }

    /// <summary>
    /// A smart program's main .py file plus every locally-connected .py file
    /// it (transitively) imports, as returned by GET /submission/export_bundle
    /// and accepted by POST /submission/import_bundle. Field names match the
    /// Python API's SmartProgramBundle wire shape exactly.
    ///
    /// This is storage-only: it is persisted alongside SmartProgramDefinition
    /// (see SmartProgramModel.FileBundle) so a SmartProgram's source survives
    /// project save/load, but it must never be sent to the measurement
    /// backend (Haskell) - see SmartProgramRegistry.SerializeAllDags, which
    /// strips this field before building the TCP payload.
    /// </summary>
    public class SmartProgramBundle
    {
        public string programname { get; set; } = string.Empty;
        public SmartProgramFile main_file { get; set; } = new();
        public List<SmartProgramFile> dependencies { get; set; } = new();
        public List<string> requirements { get; set; } = new();
    }
}
