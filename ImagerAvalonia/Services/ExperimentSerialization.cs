using Autofac;
using Avalonia.Threading;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;



namespace ImagerAvalonia.Services;

public interface IExperimentSerialization
{
    public JObject  SerializeExperiment(ExperimentalPanelViewModel experiment, SmartProcessingRegisterViewModel processes);
    public void TryAddStagePosition(XYStagePosition xyz);
    public List<XYStagePosition> ExperimentPositions { get; set; }
    public Dictionary<string, string> AcquisitionMaps { get; set; }
    public NodeBase GetDeserializedExperiment(JToken serialized_progran, UserDefinedAcquisitions acquisitions);
    int GetMaxNumberOfDetectionsInTree(ExperimentalPanelViewModel experiment);


    public List<AcqDetPair> GetAcqDetPairs();

    public TraversalState TraverseExperiment(NodeBase node);
    void SetExperiment(ExperimentalPanelViewModel exp);
}



public class ExperimentSerialization : IExperimentSerialization
{

    public List<XYStagePosition> ExperimentPositions { get; set; } = new List<XYStagePosition>() {IStageControl.DefaultXYStagePosition };
    public List<AcqDetPair> AcqDetPairs = new();
    public Dictionary<string, string> AcquisitionMaps { get; set; } = new(); // required for when an acquisition is renamed upon loading
    private ExperimentalPanelViewModel Experiment;
    private readonly IMeasurementTypeRegistry registry = App.Container.Resolve<IMeasurementTypeRegistry>();



    public List<AcqDetPair> GetAcqDetPairs()
    {
        var acq_det_pairs = Experiment.AcquisitionTracker.EnabledAcquisitions
            .Where(x => x.IsEnabled)
            .Select(x => x.acquisition)
            .Distinct()
            .Select(x => x.AcqDetPairs).SelectMany(x => x);

        AcqDetPairs = acq_det_pairs.ToList();

        return AcqDetPairs;
    }


    public void TryAddStagePosition(XYStagePosition xyStagePosition)
    {
        var found_position = ExperimentPositions.Select(x => x.IsEqual(xyStagePosition)).Where(x => x==true);
        if(found_position.Count()==0)
        {
            ExperimentPositions.Add(xyStagePosition);
        }
    }

    public void SetExperiment(ExperimentalPanelViewModel experiment)
    {
        Experiment = experiment;    
    }

    public int GetMaxNumberOfDetectionsInTree(ExperimentalPanelViewModel experiment)
    {
        NodeBase node = experiment.Root;
        TraversalState state = TraverseExperiment(node);
        
        return state.TraversalProgress;
    }


    public JObject SerializeExperiment(ExperimentalPanelViewModel experiment, SmartProcessingRegisterViewModel processViewModel)
    {

        RootNode root_node = experiment.Root;
        var acq_names = experiment.AcquisitionTracker.EnabledAcquisitions
            .Where(x => x.IsEnabled)
            .Select(x => x.acquisition)
            .Distinct();




        if (acq_names.Any(x => string.IsNullOrEmpty(x.Name)))
        {
            throw new Exception("One or multiple acquisition names are empty. \n Experiment can't contain acquisitions with empty names");
        }
        if (acq_names.Select(x => x.Name).Count() != acq_names.Select(x => x.Name).Distinct().Count())
        {
            throw new Exception("One or multiple acquisition names are the same. \n Experiment can't contain multiple acquisitions with the same name");
        }


        JObject measurement_program = new JObject();
        measurement_program["program"] = root_node.Traverse(this);




        //List<string> defined_acquisitions = GetAllUsedAcquisitions(Items);
        measurement_program["defineddetections"] = new JObject();
        foreach (var detection in acq_names)
        {
            measurement_program["defineddetections"][detection.Name] = detection.SerializeAcquisition();
        }
        measurement_program["action"] = "executemeasurementprogram";
        measurement_program["smartprogramcode"] = new JObject();

        measurement_program["smartprogramcode"]["code"] = processViewModel.SerializeAllDags();
        measurement_program["smartprogramcode"]["type"] = "dagorchestratorcode";


        return measurement_program;


    }



    public TraversalState TraverseExperiment(NodeBase node)
    {
        var newTraversal = new TraversalState();

        node.TraverseMeasurement(newTraversal);
        return newTraversal;
    }

    public NodeBase GetDeserializedExperiment(JToken serialized_program, UserDefinedAcquisitions acquisitions)
    {
        NodeBase root = new RootNode();
        root.UserAcquisitionSettings = acquisitions;
        NodeBase exp_nodes = DeserializeExperiment(serialized_program["program"]["elements"], root, acquisitions);
        return exp_nodes;
    }

    public NodeBase DeserializeExperiment(JToken serialized_program, NodeBase node, UserDefinedAcquisitions acquisitions)
    {


        foreach (JToken elements in serialized_program ?? Enumerable.Empty<JToken>())
        {
            if (elements is JObject NodeValue)
            {
                string element = NodeValue?["elementtype"]?.Value<string>() ?? string.Empty;

                var registry = App.Container.Resolve<IMeasurementTypeRegistry>();
                Type elementType = registry.GetTypeByDescription(element);


                if (elementType == null)
                    continue;

                object[] args = { acquisitions };

                if (Activator.CreateInstance(elementType, args) is IMeasurementTypes measurementType)
                {
                    measurementType.Deserialize(NodeValue, this);

                    switch (measurementType.NodeType)
                    {
                        case TreeMeasurementType.ExperimentType:
                            {
                                var exp_node = new ExperimentNode(node, measurementType);
                                node.Children.Add(exp_node);

                                if (NodeValue?["elements"] is JToken elementsToken)
                                {
                                    DeserializeExperiment(elementsToken, exp_node, acquisitions);
                                }

                                break;
                            }
                        case TreeMeasurementType.ActionType:
                            {
                                var act_node = new ActionNode(node, measurementType);
                                if (act_node.MeasurementType is Detection acq && acq.MeasurementView.DataContext is AcquisitionPanelViewModel vm)
                                {
                                    vm.SetAcquisitionTracker(Experiment.AcquisitionTracker);
                                }
                                node.Children.Add(act_node);
                                break;
                            }
                    }
                }
                else
                {
                    throw new NotImplementedException(
                        "Measurement type not supported by IMeasurementTypes interface"
                    );
                }
            }
        }
        return node;
    }
}




public class TraversalState
{
   
    public int TraversalProgress = 0;
  

    public TraversalState()
    {

    }

    public void UpdateLast()
    {
        TraversalProgress++;
    }
}
















