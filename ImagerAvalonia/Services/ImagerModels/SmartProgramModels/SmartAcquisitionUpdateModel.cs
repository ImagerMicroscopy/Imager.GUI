using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImagerAvalonia.Services.ImagerModels.SmartProgramModels
{
    public class SmartAcquisitionUpdateModel
    {
        public Guid elementid { get; set; } = Guid.Empty;

        public string acquisitionupdatefunction { get; set; } = string.Empty;

        public List<UpdateAcquisitionParameterModel> acquisitionupdateparameters { get; set; } = new();

        [JsonIgnore]
        public Guid SmartProgramId { get; set; }
    }

    // Mirrors the shape actually produced/consumed on the wire for each entry in
    // "acquisitionupdateparameters" - see UpdateAcquisitionFunctionParameterViewModel.
    public class UpdateAcquisitionParameterModel
    {
        public List<List<string>> SelectedEquipmentPaths { get; set; } = new();

        [JsonConverter(typeof(StringEnumConverter))]
        public EquipmentPropertyType PropertyType { get; set; }

        public string ParameterName { get; set; } = string.Empty;

        public List<string> EquipmentPaths { get; set; } = new();

        public string SelectedPath { get; set; } = string.Empty;

        public int SelectedPathIndex { get; set; }
    }
}
