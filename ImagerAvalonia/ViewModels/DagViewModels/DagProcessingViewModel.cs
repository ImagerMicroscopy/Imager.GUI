

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImagerAvalonia.Services;
using ImagerAvalonia.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels;

public partial class DagProcessingViewModel : ViewModelBase
{
    private readonly IPythonCom _nodeComService;
    [ObservableProperty] Guid _dagId = Guid.NewGuid();
    private Dictionary<string, NodeInfo> _apiParameters = new();
    public List<DagNodeViewModel> AddedNodes = new();
    public event EventHandler<NodeInfo>? NodeAdded;

    [ObservableProperty] ObservableCollection<string> _apiNodes = new();
    


    public DagProcessingViewModel(IPythonCom nodeCom)
    {
        _nodeComService = nodeCom;
        try
        {
            Task.Run(() => this.SetUpNodes());
        }
        catch 
        { 

        }

    }

    private async Task SetUpNodes()
    {
        var nodes = await _nodeComService.SetUpAvailableNodes();
        if(nodes!=null)
        {
            var api_nodes = JsonConvert.DeserializeObject<ObservableCollection<string>>(nodes);
            if (api_nodes != null)
            {
                ApiNodes = api_nodes;
                foreach (var node in ApiNodes)
                {
                    var node_view_model = await _nodeComService.GetNodeInfo(node);
                    if (node_view_model != null)
                    {
                        NodeInfo? node_info = JsonConvert.DeserializeObject<NodeInfo>(node_view_model);
                        if (node_info != null)
                        {
                            node_info.ApiPath = node;

                            _apiParameters.Add(node, node_info);
                        }
                    }

                }
            }
        }
    }

    [RelayCommand]
    public void AddNode(string api_route)
    {
        NodeAdded?.Invoke(this, _apiParameters[api_route]);
    }

    [RelayCommand]
    public void AddImageInputNode()
    {
        NodeInfo input_info = new NodeInfo();
        NodeParams input_params = new NodeParams();
        NodeOutput output_info = new NodeOutput();
        input_info.ApiPath = "Image Input";
        input_info.IsNodeInput = true;
        output_info.Datatype = "Image2D";
        input_params.Datatype = "ImageInput";


        input_info.Parameters.Add(input_params);
        input_info.Output.Add(output_info);


        NodeAdded?.Invoke(this, input_info);
    }

    public JArray SerializeNodes()
    {
        var topologically_sorted_nodes = TopologicallySort(AddedNodes);

        var array = new JArray();
        foreach (var dagnode in topologically_sorted_nodes)
        {
            array.Add(JObject.FromObject(dagnode));
        }

        return array;
    }

    [RelayCommand]
    public async Task SubmitDag()
    {
        JArray dag_nodes = SerializeNodes();

        var response = await _nodeComService.SubmitNodes(dag_nodes);

        var exception = await response.Content.ReadAsStringAsync();

    }

    public List<DagNodeViewModel> TopologicallySort(List<DagNodeViewModel> dagNodes)
    {
        var permMarks = new HashSet<Guid>();
        var tempMarks = new HashSet<Guid>();
        var sorted = new List<DagNodeViewModel>();

        void Visit(DagNodeViewModel node)
        {
            if (permMarks.Contains(node.Guid)) return;
            if (tempMarks.Contains(node.Guid)) throw new Exception("Graph contains a loop");

            tempMarks.Add(node.Guid);

            // Visit prerequisites first
            foreach (var prereq in node.InboundNodes ?? Enumerable.Empty<DagNodeViewModel>())
            {
                Visit(prereq);
            }

            tempMarks.Remove(node.Guid);
            permMarks.Add(node.Guid);
            sorted.Add(node);
        }

        foreach (var node in dagNodes)
        {
            Visit(node);
        }

        return sorted; // no reverse needed in this approach
    }


}
public class NodeInfo
{
    public List<NodeInput> Input { get; set; } = new();
    public List<NodeOutput> Output { get; set; } = new();
    public List<NodeParams> Parameters { get; set; } = new();
    public string ApiPath { get; set; } = string.Empty;
    public bool IsNodeInput { get; set; } = false;
    public bool IsNodeOutput { get; set; } = false;
    public bool IsLazyNode { get; set; } = false;

}

public class NodeInput
{
    public string Datatype { get; set; } = string.Empty;
    public string ImageDir { get; set; } = string.Empty;
    public string ElementType { get; set; } = string.Empty;

}

public class NodeOutput
{
    public string Datatype { get; set; } = string.Empty;
    public string ImageDir { get; set; } = string.Empty;
    public string ElementType { get; set; } = string.Empty;

}

public class NodeParams
{
    public string Datatype { get; set; } = string.Empty;
    public float Value { get; set; } = 0f;
    public string Name { get; set; } = string.Empty;
    public List<string> Options { get; set; } = new();
    public string SelectedValue { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
}

