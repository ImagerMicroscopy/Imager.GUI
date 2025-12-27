using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.Tests.ViewModels
{
    using System.Collections.Generic;

    internal class SerializationResponses
    {
        public static readonly EquipmentConfiguration ExpectedConfiguration = new EquipmentConfiguration
        {
            Detectors = new List<string>(),

            MovableComponents = new List<MovableComponent>
        {
            new MovableComponent
            {
                EquipmentName = "fw1",
                MovableComponentSettings = new List<MovableComponentSetting>
                {
                    new DiscreteMovableSetting
                    {
                        ComponentName = "fw",
                        DesiredSetting = "DAPI",
                        Type = "discretemovablesetting"
                    },
                    new ContinuousMovableSetting
                    {
                        ComponentName = "sl",
                        DesiredSetting = 0.0,
                        Increment = 1.0,
                        Type = "continuousmovablesetting"
                    }
                }
            },
            new MovableComponent
            {
                EquipmentName = "fw2",
                MovableComponentSettings = new List<MovableComponentSetting>
                {
                    new DiscreteMovableSetting
                    {
                        ComponentName = "fw",
                        DesiredSetting = "DAPI/GFP/RFP/640",
                        Type = "discretemovablesetting"
                    },
                    new ContinuousMovableSetting
                    {
                        ComponentName = "sl",
                        DesiredSetting = 0.0,
                        Increment = 1.0,
                        Type = "continuousmovablesetting"
                    }
                }
            }
        },

            Irradiation = new List<IrradiationSetting>
        {
            new IrradiationSetting
            {
                EquipmentName = "hello",
                LightSourceName = "ls",
                LightSourceChannel = new List<string> { "ch1" },
                LightSourcePower = new List<int> { 40 }
            }
        }
        };
    }

    // Supporting classes
    public class EquipmentConfiguration
    {
        public List<string> Detectors { get; set; } =  new List<string>();
        public List<MovableComponent> MovableComponents { get; set; } = new();  
        public List<IrradiationSetting> Irradiation { get; set; } = new();   
    }

    public class MovableComponent
    {
        public string? EquipmentName { get; set; }
        public List<MovableComponentSetting> MovableComponentSettings { get; set; } = new();
    }

    public abstract class MovableComponentSetting
    {
        public string? ComponentName { get; set; }
        public string? Type { get; set; }
    }

    public class DiscreteMovableSetting : MovableComponentSetting
    {
        public string? DesiredSetting { get; set; }
    }

    public class ContinuousMovableSetting : MovableComponentSetting
    {
        public double DesiredSetting { get; set; }
        public double Increment { get; set; }
    }

    public class IrradiationSetting
    {
        public string? EquipmentName { get; set; }
        public string? LightSourceName { get; set; }
        public List<string> LightSourceChannel { get; set; } = new();
        public List<int> LightSourcePower { get; set; } = new();
    }

}
