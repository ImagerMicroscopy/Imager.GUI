
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services;
using ImagerAvalonia.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;


namespace ImagerAvalonia.ViewModels;

[JsonConverter(typeof(DagNodeInputViewModelConverter))]
public partial class DagNodeInputViewModel : ViewModelBase
{
    public readonly Guid _parent_node;
    public string input_type;
    public string image_dir;
    public string element_type;

    [ObservableProperty] string _displayedType = string.Empty;

    public DagNodeOutputViewModel? InputTarget { get; private set; }


    public DagNodeInputViewModel(Guid id, NodeInput input)
    {
        _parent_node = id;
        input_type = input.Datatype;
        image_dir = input.ImageDir;
        element_type = input.ElementType;

        if (element_type != string.Empty)
        {
            DisplayedType = element_type;
        }
        else
        {
            DisplayedType = input_type;
        }

    }



    public bool SetInputTarget(DagNodeOutputViewModel? inputTarget)
    {
        if (InputTarget==null)
        {
            InputTarget = inputTarget;
            return true;
        }

        if(inputTarget is null)
        {
            InputTarget = null;
        }

        return false;

    }

    public class DagNodeInputViewModelConverter : JsonConverter<DagNodeInputViewModel>
    {
        public override void WriteJson(JsonWriter writer, DagNodeInputViewModel? dagnode, JsonSerializer serializer)
        {
            var obj = new JObject();
            if (dagnode != null)
            {
                obj["input_type"] = dagnode.input_type;
                var inputParams = new JObject();

                var inputJsonParams = new JObject
                {
                    //["image_shape"] = new JArray(256, 256),
                    //["image_type"] = "float32",
                    ["image_dir"] = ResolveInputPath(dagnode)
                };

                inputParams["input_json_params"] = inputJsonParams;
                obj["isinputnode"] = true;
                obj["input_params"] = inputParams;
            }
            obj.WriteTo(writer);

        }

        private string ResolveInputPath(DagNodeInputViewModel vm)
        {
            if(String.IsNullOrEmpty(vm.image_dir))
            {
                return vm.InputTarget.parent_node.ToString();
            }
            else
            {
                return vm.image_dir;
            }
        }

        public override DagNodeInputViewModel ReadJson(JsonReader reader, Type objectType, DagNodeInputViewModel? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }


}

