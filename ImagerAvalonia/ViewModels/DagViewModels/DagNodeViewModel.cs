
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.Linq;


namespace ImagerAvalonia.ViewModels;


[JsonConverter(typeof(DagNodeViewModelConverter))]
public partial class DagNodeViewModel : ViewModelBase
{
    public readonly Guid Guid = Guid.NewGuid();
    public bool IsInputNode = false;
    public bool IsOutputNode = false;
    public bool IsLazyNode = false;

    public ObservableCollection<DagNodeViewModel> OutboundNodes { get; set; } = new();
    public ObservableCollection<DagNodeViewModel> InboundNodes { get; set; } = new();

    [ObservableProperty] ObservableCollection<DagNodeInputViewModel> _dagNodeInputs = new();
    [ObservableProperty] ObservableCollection<DagNodeOutputViewModel> _dagNodeOutputs = new();
    [ObservableProperty] ObservableCollection<DagNodeParametersViewModel> _dagNodeParameters = new();

    [ObservableProperty] string _apiPath = "";


    private NodeInfo _nodeInfo { get; set; }

    public DagNodeViewModel(NodeInfo nodeinfo)
    {
        _nodeInfo = nodeinfo;
        ApiPath = nodeinfo.ApiPath;
        IsInputNode = nodeinfo.IsNodeInput;
        IsOutputNode = nodeinfo.IsNodeOutput;
        IsLazyNode = nodeinfo.IsLazyNode;   
        


        foreach (var input_item in nodeinfo.Input)
        {
            DagNodeInputs.Add(new DagNodeInputViewModel(Guid, input_item));
        }

        foreach (var output_item in nodeinfo.Output)
        {
            DagNodeOutputs.Add(new DagNodeOutputViewModel(Guid, output_item));
        }

        foreach(var param_items in nodeinfo.Parameters)
        {
            DagNodeParameters.Add(DagNodeParametersViewModel.GetDagNodeParameterVMFactory( param_items, Guid));   
        }
    }


    public class DagNodeViewModelConverter : JsonConverter<DagNodeViewModel>
    {
        public override void WriteJson(JsonWriter writer, DagNodeViewModel? dagnode, JsonSerializer serializer)
        {
            var obj = new JObject();


            obj["node_id"] = dagnode.Guid.ToString();
            var input_nodes = new JArray();
            var output_nodes = new JArray();

            //obj["input_nodes"] = new JArray();
            var input_parameters = new JObject();
            input_parameters["node_id"] = dagnode.Guid.ToString();


            var inputArray = new JArray();
            var inputParameterArray = new JArray();

            foreach (var dag_input in dagnode.DagNodeInputs)
            {
                input_nodes.Add(dag_input.InputTarget.parent_node.ToString());
                inputParameterArray.Add(JToken.FromObject(dag_input, serializer));
            }


            foreach (var dag_params in dagnode.DagNodeParameters)
            {
                inputParameterArray.Add(JToken.FromObject(dag_params, serializer));
            }


            foreach (var dag_input in dagnode.DagNodeOutputs)
            {
                output_nodes.Add(new JArray(dag_input.OutputTarget.Select(x => x._parent_node.ToString())));
            }

            input_parameters["input"] = inputParameterArray;
            obj["input_nodes"] = input_nodes;
            obj["input_parameters"] = input_parameters;
            obj["output_nodes"] = output_nodes;
            obj["isoutputnode"] = dagnode.IsOutputNode;
            obj["isinputnode"] = dagnode.IsInputNode;
            obj["islazynode"] = dagnode.IsLazyNode;

            obj["api_path"] = dagnode.ApiPath;
            obj.WriteTo(writer);
        }
        public override DagNodeViewModel ReadJson(JsonReader reader, Type objectType, DagNodeViewModel? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }


}

