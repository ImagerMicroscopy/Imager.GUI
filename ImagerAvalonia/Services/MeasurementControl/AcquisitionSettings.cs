using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace ImagerAvalonia.Services.MeasurementControl
{
    public static class AcquisitionSettingsFactory
    {
        public static AcquisitionSettings CopyFromDeserializedModel(AcquisitionSettings defaultSettings, AcquisitionSettingsDeserializationModel modelSettings)
        {
            // Set the detector properties from deserialized model into the model with current available hardware parameters
            defaultSettings.Detector.ForEach(d => d.IsEnabled = false);
            foreach (var detectormodel in modelSettings.Detectors)
            {
                var found_detector = defaultSettings.Detector.Find(x => x.Detectorname == detectormodel.Detectorname);
                if (found_detector != null)
                {
                    found_detector.IsEnabled = true;

                    for (int id = 0; id < found_detector.DetectorProperties.Count(); id++)
                    {
                        var detectorproperty = found_detector.DetectorProperties[id];
                        var property = modelSettings.GetDetectorEquipmentPropertyByName(detectorproperty.descriptor, detectormodel.Detectorname);
                        switch (detectorproperty.kind)
                        {
                            case PropertyKind.numeric:
                                if (detectorproperty is NumericDetectorProperty num_prop && property is NumericDetectorProperty numeric_value)
                                {
                                    found_detector.DetectorProperties[id] = new NumericDetectorProperty(num_prop.descriptor, num_prop.propertycode, numeric_value.value);
                                }
                                break;
                            case PropertyKind.discrete:
                                if (detectorproperty is CategoricDetectorProperty dis_prop && property is CategoricDetectorProperty cat_value)
                                {
                                    found_detector.DetectorProperties[id] = new CategoricDetectorProperty(dis_prop.descriptor, dis_prop.propertycode, cat_value.current, dis_prop.availableoptions);
                                }
                                break;
                        }
                    }
                }
            }
            // Set the movable component  properties from deserialized model into the model with current available hardware parameters

            foreach (var movablecomponentmodel in modelSettings.MovableComponents)
            {
                var found_movablecomponent = defaultSettings.FilterWheels.Find(x => x.equipmentname == movablecomponentmodel.equipmentname);
                if (found_movablecomponent != null)
                {
                    foreach (var movablecomponentpart in movablecomponentmodel.movablecomponentsettings)
                    {
                        if (movablecomponentpart.movablecomponent!= null)
                        {
                            switch (movablecomponentpart.movablecomponent.Type)
                            {
                                case MovableComponentType.discretemovablecomponent:
                                    if (movablecomponentpart.movablecomponent is DiscreteMovableComponentPartProperties discrete_property)
                                    {
                                        found_movablecomponent.SetValueByName(movablecomponentpart.Name, discrete_property.desiredsetting);
                                    }
                                    break;
                                case MovableComponentType.continuousmovablecomponent:
                                    if (movablecomponentpart.movablecomponent is ContinuousMovableComponentPartProperties continuous_property)
                                    {
                                        found_movablecomponent.SetValueByName(movablecomponentpart.Name, continuous_property.desiredsetting.ToString());
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
            foreach (var irradiationmodel in modelSettings.Irradiation)
            {
                var found_irradiation = defaultSettings.Sources.Find(x => x.EquipmentName == irradiationmodel.equipmentname);
                if (found_irradiation != null)
                {
                    for (int id = 0; id < irradiationmodel.lightsourcechannel.Count(); id++)
                    {
                        found_irradiation.LightsourceChannel.Add(irradiationmodel.lightsourcechannel[id]);
                        found_irradiation.LightsourcePower.Add((int)irradiationmodel.lightsourcepower[id]);
                    }
                }
            }





            return defaultSettings;
        }

        public static AcquisitionSettings FromDetectorNames(string name, List<string> detectorNames)
        {
            var settings = new AcquisitionSettings(name);
            settings.Detector = detectorNames.Select(n => new DetectorEquipment(n) { IsEnabled = true }).ToList();
            return settings;
        }

        public static AcquisitionSettings FromComponents(string name,
            List<Source> sources, List<MovableComponent> filterWheels, List<DetectorEquipment> detectors)
        {
            var settings = new AcquisitionSettings(name);
            settings.Sources = sources.Select(x => new Source(x)).ToList();
            settings.FilterWheels = filterWheels.Select(x => new MovableComponent(x)).ToList();
            settings.Detector = detectors.Select(x => new DetectorEquipment(x)).ToList();

            foreach (var detector in settings.Detector)
            {
                if (detector.IsEnabled)
                {
                    settings.acqDetPairs.Add(new AcqDetPair(settings, detector.Detectorname));
                }
            }

            return settings;
        }

        public static AcquisitionSettings CloneWithName(string name, AcquisitionSettings original)
        {
            var settings = new AcquisitionSettings(name);
            settings.Sources = original.Sources.Select(x => new Source(x)).ToList();
            settings.FilterWheels = original.FilterWheels.Select(x => new MovableComponent(x)).ToList();
            settings.Detector = original.Detector.Select(x => new DetectorEquipment(x)).ToList();
            return settings;
        }

        public static AcquisitionSettings FromName(string name)
        {
            return new AcquisitionSettings(name);
        }
    }



    public partial class AcquisitionSettings : ObservableObject
    {

        [ObservableProperty] private string _name;

        public int AcquisitionSettingsID;
        public List<Source> Sources;
        public List<MovableComponent> FilterWheels;
        public List<DetectorEquipment> Detector;
        public List<AcqDetPair> acqDetPairs = new();
        public List<Stage> Stages = new();

        public AcquisitionSettings(string name)
        {
            Name = name;
            Sources = new();
            FilterWheels = new();
            Detector = new();
        }


        public JObject SerializeAcquisition()
        {

            JObject serialized_acquisition_settings = new JObject();
            List<DetectorEquipment> enabled_detectors = new List<DetectorEquipment> { };
            foreach (DetectorEquipment d in Detector)
            {
                if (d.IsEnabled)
                {
                    enabled_detectors.Add(d);
                }
            }

            List<Source> enabled_sources = new List<Source> { };
            foreach (Source s in Sources)
            {
                if (s.LightsourceChannel.Count > 0)
                {
                    enabled_sources.Add(s);
                }

            }



            serialized_acquisition_settings["detectors"] = JArray.FromObject(enabled_detectors.Select(x => x.Serialize()).ToList());
            serialized_acquisition_settings["movablecomponents"] = JArray.FromObject(FilterWheels.Select(x => x.Serialize()).ToList());
            serialized_acquisition_settings["irradiation"] = JArray.FromObject(enabled_sources.Select(x => x.Serialize()).ToList());

            return serialized_acquisition_settings;
        }
    }

    public class AcqDetPair
    {
        private AcquisitionSettings acqNameSource;

        public string acqName
        {
            get => acqNameSource.Name;
            set => acqNameSource.Name = value;
        }

        public string detName;

        public AcqDetPair(AcquisitionSettings acqName, string detName)
        {
            this.acqNameSource = acqName;
            this.detName = detName;
        }

        public AcqDetPair(string acqName, string detName)
        {
            acqNameSource = new AcquisitionSettings(acqName);
            this.detName = detName;
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is AcqDetPair other)
            {
                return (other.acqName == this.acqName && other.detName == this.detName);
            }
            return base.Equals(obj);
        }
    }

}
