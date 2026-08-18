using System;
using System.Collections.Generic;


namespace ImagerAvalonia.Services.ImagerModels.SmartProgramModels
{

    public class InputFunctionModel
    {
        public string methodname { get; set; } = string.Empty;

        public List<InputParameterModel> inputparams { get; set; } = new();

    }


    public class InputParameterModel
    {
        public string? acquisition { get; set; }

        public string? detection { get; set; }

        public Guid? elementid { get; set; }

        public void Clear()
        {
            acquisition = null;
            detection = null;
            elementid = null;
        }
    }
}