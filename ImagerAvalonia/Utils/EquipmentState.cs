using System.Collections.Generic;
using System.Collections.ObjectModel;

using ImagerAvalonia.Services.MeasurementControl;
using Newtonsoft.Json.Linq;

namespace ImagerAvalonia.Utils
{

    public enum EquipmentPropertyType
    {
        LightSourceProperty,
        MovableComponentProperty,
        DetectorProperty
    }

    public class EquipmentState
    {
        //public List<List<string>> EquipmentPaths = new();
        public List<EquipmentProperty> EquipmentProperties = new();

        public EquipmentState() 
        { 
        
        }


        public static ObservableCollection<AcquisitionSettings> GetAcquisitionsFromImagerProgram(JToken serialized_program)
        {
            ObservableCollection<AcquisitionSettings> acq_settings = new();
            JToken detections = serialized_program["defineddetections"];
            foreach (JProperty det_key in detections.Children())
            {

                string acq_name = det_key.Name;
                JToken detectors = det_key.Value["detectors"];
                JToken movablecomponents = det_key.Value["movablecomponentsettings"];
                JToken irradiation = det_key.Value["irradiation"];

                List<string> detector_names = new();
                foreach (JToken det in detectors)
                {
                    detector_names.Add(det["detectorname"].ToObject<string>());
                }

                AcquisitionSettings acq_setting =  AcquisitionSettingsFactory.FromDetectorNames(acq_name, detector_names);
                foreach (DetectorEquipment detector in acq_setting.Detector)
                {
                    acq_setting.acqDetPairs.Add(new AcqDetPair(acq_setting, detector.Detectorname));
                }

                acq_settings.Add(acq_setting);

            }

            return acq_settings;
        }


        public List<MovableComponent> ParseAvailableFilterWheels(List<Equipment> eq)
        {
            var availableFilterWheels = new List<MovableComponent>();
            for (int fw = 0; fw < eq.Count; fw++)
            {
                if (eq[fw].availablemovablecomponents.Count != 0)
                {
                    availableFilterWheels.Add(new MovableComponent(eq[fw].availablemovablecomponents, eq[fw].name));
                    foreach(var component in eq[fw].availablemovablecomponents)
                    {
                        EquipmentProperties.Add(new EquipmentProperty()
                        {
                            EquipmentPath = new List<string>() { eq[fw].name, component.Name },
                            EquipmentType = EquipmentPropertyType.MovableComponentProperty
                        });
                    }

                }
            }
            return availableFilterWheels;
        }

        public List<Robots> ParseAvailableRobots(List<Equipment> eq)
        {
            var robots = new List<Robots> ();
            for (int rb = 0; rb < eq.Count; rb++)
            {
                if(eq[rb].availablerobots.Count !=0)
                {
                    foreach(var robot in eq[rb].availablerobots)
                    {
                        robot.EquipmentName = eq[rb].name;
                    }    

                    robots.AddRange(eq[rb].availablerobots);
                }
            }
            return robots;
        }


        public List<Source> ParseAvailableLightSources(List<Equipment> eq)
        {
            var availableSources = new List<Source>();

            for (int sc = 0; sc < eq.Count; sc++)
            {
                if (eq[sc].availablelightsources.Count != 0)
                {
                    foreach (var lightsource in eq[sc].availablelightsources)
                    {
                        lightsource.EquipmentName = eq[sc].name;
                        availableSources.Add(lightsource);
                        foreach (string channelname in lightsource.AvailableChannels)
                        {
                            EquipmentProperties.Add(new EquipmentProperty()
                            {
                                EquipmentPath = new List<string>() { lightsource.EquipmentName, lightsource.LightSourceName, channelname },
                                EquipmentType = EquipmentPropertyType.LightSourceProperty
                            });

                        }
                    }
                }
            }
            return availableSources;
        }

    }

    public class EquipmentProperty
    {
        public List<string> EquipmentPath;
        public EquipmentPropertyType EquipmentType;
    }
}
