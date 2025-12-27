
using Autofac;
using CommunityToolkit.Mvvm.ComponentModel;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.Linq;


namespace ImagerAvalonia.ViewModels;

public abstract partial class DagNodeParametersViewModel : ViewModelBase
{
    public readonly Guid _parent_node;
    [ObservableProperty] string _parameterName;
    public string datatype;

    public static DagNodeParametersViewModel GetDagNodeParameterVMFactory(NodeParams node_info, Guid id)
    {
        switch (node_info.Datatype)
        {
            case ("Scalar"):
                return new NumericDagNodeParameters(id, node_info);
            case ("Categoric"):
                return new CategoricDagNodeParameters(id, node_info);
            case ("ImageInput"):
                return new ImageInputDagNodeParameters(id, node_info);
            case ("Image2DPath"):
                return new ImageInputDagNodeParameters(id, node_info);
            case ("AcquisitionName"):
                return new AcquisitionNameParameter(id, node_info);
            case ("DetectorName"):
                return new DetectorNameParameter(id, node_info);
        }
        throw new ArgumentException($"Unsupported external API datatype: {node_info.Datatype}");
    }

    public DagNodeParametersViewModel(Guid id, string name, string datatype)
    {
        _parent_node = id;
        _parameterName = name;
        this.datatype = datatype;

    }

}

[JsonConverter(typeof(NumericDagNodeParametersConverter))]
public partial class NumericDagNodeParameters : DagNodeParametersViewModel
{
    [ObservableProperty] float _value;

    public NumericDagNodeParameters(Guid id, NodeParams parameters) : base(id, parameters.Name, parameters.Datatype) 
    {
        _value =  parameters.Value;

    }
}

[JsonConverter(typeof(ImageInputDagNodeParametersConverter))]
public partial class ImageInputDagNodeParameters : DagNodeParametersViewModel
{
    [ObservableProperty] string _imageInput;

    public ImageInputDagNodeParameters(Guid id, NodeParams parameters) : base(id, parameters.Name, parameters.Datatype)
    {
        _imageInput = parameters.ImagePath;
    }
}
[JsonConverter(typeof(AcquisitionNameParameterConverter))]
public partial class AcquisitionNameParameter : DagNodeParametersViewModel
{
    [ObservableProperty] AcquisitionSettingsViewModel _AcquisitionInput;
    [ObservableProperty] ObservableCollection<AcquisitionSettingsViewModel> _DefinedAcquisitions = new();


    UserDefinedAcquisitions UserDefinedAcquisitions { get; set; }   

    public AcquisitionNameParameter(Guid id, NodeParams parameters) : base(id, parameters.Name, parameters.Datatype)
    {
        UserDefinedAcquisitions = App.Container.Resolve<UserDefinedAcquisitions>();
        DefinedAcquisitions = UserDefinedAcquisitions.Acquisitions;

    }
}


[JsonConverter(typeof(DetectorNameParameterConverter))]
public partial class DetectorNameParameter : DagNodeParametersViewModel
{
    [ObservableProperty] DetectorEquipmentViewModel _detectorInput;
    [ObservableProperty] ObservableCollection<DetectorEquipmentViewModel> _DefinedDetectors = new();
    UserDefinedAcquisitions UserDefinedAcquisitions { get; set; }


    public DetectorNameParameter(Guid id, NodeParams parameters) : base(id, parameters.Name, parameters.Datatype)
    {

        UserDefinedAcquisitions = App.Container.Resolve<UserDefinedAcquisitions>();
        DefinedDetectors = UserDefinedAcquisitions.Acquisitions.First().Detector;
    }
}



[JsonConverter(typeof(CategoricDagNodeParametersConverter))]
public partial class CategoricDagNodeParameters : DagNodeParametersViewModel
{
    [ObservableProperty] ObservableCollection<string> _categories;
    [ObservableProperty] string _selectedCategory;

    public CategoricDagNodeParameters(Guid id, NodeParams parameters) : base(id, parameters.Name, parameters.Datatype)
    {
        _categories = new ObservableCollection<string>(parameters.Options);
        _selectedCategory = parameters.SelectedValue;
    }
}


public class ImageInputDagNodeParametersConverter : JsonConverter<ImageInputDagNodeParameters>
{
    public override void WriteJson(JsonWriter writer, ImageInputDagNodeParameters dagnode, JsonSerializer serializer)
    {
        var obj = new JObject();
        obj["input_type"] = dagnode.datatype;
        var inputParams = new JObject();

        var inputJsonParams = new JObject
        {
            ["image_dir"] = dagnode.ImageInput
        };

        inputParams["input_json_params"] = inputJsonParams;
        obj["input_params"] = inputParams;
        obj["isinputnode"] = true;
        obj.WriteTo(writer);

    }

    public override ImageInputDagNodeParameters ReadJson(JsonReader reader, Type objectType, ImageInputDagNodeParameters existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}

public class CategoricDagNodeParametersConverter : JsonConverter<CategoricDagNodeParameters>
{
    public override void WriteJson(JsonWriter writer, CategoricDagNodeParameters dagnode, JsonSerializer serializer)
    {
        var obj = new JObject();
        obj["input_type"] = dagnode.datatype;
        var inputParams = new JObject();

        var inputJsonParams = new JObject
        {
            ["options"] = JArray.FromObject(dagnode.Categories.ToList()),
            ["name"] = dagnode.ParameterName,
            ["selectedvalue"] = dagnode.SelectedCategory
        };

        inputParams["input_json_params"] = inputJsonParams;
        obj["input_params"] = inputParams;
        obj.WriteTo(writer);

    }

    public override CategoricDagNodeParameters ReadJson(JsonReader reader, Type objectType, CategoricDagNodeParameters existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}



public class NumericDagNodeParametersConverter : JsonConverter<NumericDagNodeParameters>
{
    public override void WriteJson(JsonWriter writer, NumericDagNodeParameters? dagnode, JsonSerializer serializer)
    {
        var obj = new JObject();
        if (dagnode != null)
        {
            obj["input_type"] = dagnode.datatype;
            var inputParams = new JObject();

            var inputJsonParams = new JObject
            {
                ["value"] = dagnode.Value,
                ["name"] = dagnode.ParameterName,
            };

            inputParams["input_json_params"] = inputJsonParams;
            obj["input_params"] = inputParams;
            obj.WriteTo(writer);
        }

    }

    public override NumericDagNodeParameters ReadJson(JsonReader reader, Type objectType, NumericDagNodeParameters? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}





public class DetectorNameParameterConverter : JsonConverter<DetectorNameParameter>
{
    public override void WriteJson(JsonWriter writer, DetectorNameParameter? dagnode, JsonSerializer serializer)
    {
        var obj = new JObject();
        if (dagnode != null)
        {
            obj["input_type"] = dagnode.datatype;
            var inputParams = new JObject();

            var inputJsonParams = new JObject
            {
                ["value"] = dagnode.DetectorInput.Name,
                ["name"] = dagnode.ParameterName,
            };

            inputParams["input_json_params"] = inputJsonParams;
            obj["input_params"] = inputParams;
            obj.WriteTo(writer);
        }

    }

    public override DetectorNameParameter ReadJson(JsonReader reader, Type objectType, DetectorNameParameter? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}


public class AcquisitionNameParameterConverter : JsonConverter<AcquisitionNameParameter>
{
    public override void WriteJson(JsonWriter writer, AcquisitionNameParameter? dagnode, JsonSerializer serializer)
    {
        var obj = new JObject();
        if (dagnode != null)
        {
            obj["input_type"] = dagnode.datatype;
            var inputParams = new JObject();

            var inputJsonParams = new JObject
            {
                ["value"] = dagnode.AcquisitionInput.Name,
                ["name"] = dagnode.ParameterName,
            };

            inputParams["input_json_params"] = inputJsonParams;
            obj["input_params"] = inputParams;
            obj.WriteTo(writer);
        }

    }

    public override AcquisitionNameParameter ReadJson(JsonReader reader, Type objectType, AcquisitionNameParameter? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}



