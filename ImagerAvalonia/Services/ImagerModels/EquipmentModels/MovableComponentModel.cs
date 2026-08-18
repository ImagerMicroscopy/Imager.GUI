using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImagerAvalonia.Services.ImagerModels.EquipmentModels
{
    public abstract class MovableComponentPartProperties
    {
        [JsonConverter(typeof(SelectedMovableComponentConverter))]
        public abstract MovableComponentType Type { get; }

        public string ComponentName { get; set; } = string.Empty;
    }

    public class ContinuousMovableComponentPartProperties : MovableComponentPartProperties
    {
        [JsonIgnore] public double MinValue { get; set; }
        [JsonIgnore] public double MaxValue { get; set; }

        public double? desiredsetting;
        public double increment;

        [JsonConverter(typeof(SelectedMovableComponentConverter))]
        public override MovableComponentType Type
            => MovableComponentType.continuousmovablecomponent;

        public ContinuousMovableComponentPartProperties(
            string name,
            double minvalue,
            double maxvalue,
            double incrementval,
            double? desiredsetting)
        {
            ComponentName = name;
            MinValue = minvalue;
            MaxValue = maxvalue;

            this.desiredsetting = desiredsetting ?? minvalue;
            increment = incrementval;
        }
    }

    public class DiscreteMovableComponentPartProperties : MovableComponentPartProperties
    {
        [JsonIgnore]
        public List<string> PossibleSettings { get; set; } = new();

        public string desiredsetting { get; set; }

        [JsonConverter(typeof(SelectedMovableComponentConverter))]
        public override MovableComponentType Type
            => MovableComponentType.discretemovablecomponent;

        public DiscreteMovableComponentPartProperties(
            string name,
            List<string> settings,
            string? desiredsetting)
        {
            ComponentName = name;
            PossibleSettings = settings ?? new List<string>();

            if (string.IsNullOrEmpty(desiredsetting))
            {
                if (PossibleSettings.Count == 0)
                    throw new Exception($"Settings for {name} contain no elements.");

                this.desiredsetting = PossibleSettings[0];
            }
            else
            {
                this.desiredsetting = desiredsetting;
            }
        }
    }

    public class MovableComponentModel
    {
        [JsonIgnore]
        public List<MovableComponentPart> movablecomponents { get; set; } = new();

        public List<MovableComponentPartProperties?> movablecomponentsettings { get; set; } = new();

        public string equipmentname = string.Empty;

        public MovableComponentModel(List<MovableComponentPart> parts, string name)
        {
            movablecomponents = parts;
            equipmentname = name;
            movablecomponentsettings = parts.Select(x => x.movablecomponent).ToList();
        }

        public MovableComponentModel() { }

        public MovableComponentModel(MovableComponentModel other)
        {
            movablecomponents = other.movablecomponents
                .Select(x => new MovableComponentPart(x))
                .ToList();

            movablecomponentsettings = other.movablecomponentsettings
                   .Select(CloneComponentProperties)
                   .ToList();

            equipmentname = other.equipmentname;    
        }

        private static MovableComponentPartProperties? CloneComponentProperties(MovableComponentPartProperties? src)
        {
            return src switch
            {
                null => null,

                DiscreteMovableComponentPartProperties d =>
                    new DiscreteMovableComponentPartProperties(
                        d.ComponentName,
                        new List<string>(d.PossibleSettings),   // deep-copy the list
                        d.desiredsetting),

                ContinuousMovableComponentPartProperties c =>
                    new ContinuousMovableComponentPartProperties(
                        c.ComponentName,
                        c.MinValue,
                        c.MaxValue,
                        c.increment,
                        c.desiredsetting),

                _ => throw new NotSupportedException(
                    $"Unknown component properties type: {src.GetType().Name}")
            };
        }

        public void SetValueByName(string componentname, string? value)
        {
            foreach (var component in movablecomponents)
            {
                if (component.Name != componentname)
                    continue;

                if (value == null)
                    continue;

                switch (component.movablecomponent)
                {
                    case ContinuousMovableComponentPartProperties continuous:
                        continuous.desiredsetting = Convert.ToDouble(value);
                        break;

                    case DiscreteMovableComponentPartProperties discrete:
                        discrete.desiredsetting = value;
                        break;
                }
            }
        }
    }

    public partial class MovableSettingPart 
    {
        public MovableComponentPartProperties? movablecomponent { get; set; }
        public string Name = string.Empty;

        public string EquipmentName { get; set; } = string.Empty;

        [JsonConstructor]
        public MovableSettingPart(
            string componentname,
            string type,
            string increment,
            string desiredsetting)
        {
            Name = componentname;

            if (!Enum.TryParse(type, true, out MovableSettingType setting_type))
                throw new Exception("Unknown Movable Setting Type");

            switch (setting_type)
            {
                case MovableSettingType.discretemovablesetting:
                    movablecomponent =
                        new DiscreteMovableComponentPartProperties(
                            componentname,
                            new List<string>(),
                            desiredsetting);
                    break;

                case MovableSettingType.continuousmovablesetting:
                    movablecomponent =
                        new ContinuousMovableComponentPartProperties(
                            componentname,
                            0,
                            0,
                            Convert.ToDouble(increment),
                            string.IsNullOrEmpty(desiredsetting)
                                ? null
                                : double.Parse(desiredsetting));
                    break;
            }
        }
    }

    public partial class MovableComponentPart 
    {
        public MovableComponentPartProperties? movablecomponent { get; set; }
        public string Name = string.Empty;

        public string EquipmentName { get; set; } = string.Empty;
        public List<string> FilterNames { get; set; } = new();

        public MovableComponentPart() { }


        [JsonConstructor]
        public MovableComponentPart(
            string componentname,
            List<string> possiblesettings,
            string type,
            string maxvalue,
            string minvalue,
            string increment,
            string desiredsetting)
        {
            Name = componentname;

            if (!Enum.TryParse(type, true, out MovableComponentType component_type))
                throw new Exception($"Invalid component type: {componentname}");

            switch (component_type)
            {
                case MovableComponentType.discretemovablecomponent:
                    movablecomponent =
                        new DiscreteMovableComponentPartProperties(
                            componentname,
                            possiblesettings,
                            desiredsetting);
                    break;

                case MovableComponentType.continuousmovablecomponent:
                    movablecomponent =
                        new ContinuousMovableComponentPartProperties(
                            componentname,
                            Convert.ToDouble(minvalue),
                            Convert.ToDouble(maxvalue),
                            Convert.ToDouble(increment),
                            string.IsNullOrEmpty(desiredsetting)
                                ? null
                                : double.Parse(desiredsetting));
                    break;
            }
        }

        public MovableComponentPart(MovableComponentPart src)
        {
            FilterNames = new List<string>(src.FilterNames);
            EquipmentName = src.EquipmentName;
            Name = src.Name;

            movablecomponent = src.movablecomponent switch
            {
                DiscreteMovableComponentPartProperties d =>
                    new DiscreteMovableComponentPartProperties(
                        d.ComponentName,
                        new List<string>(d.PossibleSettings),
                        d.desiredsetting),

                ContinuousMovableComponentPartProperties c =>
                    new ContinuousMovableComponentPartProperties(
                        c.ComponentName,
                        c.MinValue,
                        c.MaxValue,
                        c.increment,
                        c.desiredsetting),

                _ => throw new NotSupportedException("Unknown component type.")
            };
        }
    }

    public enum MovableComponentType
    {
        discretemovablecomponent,
        continuousmovablecomponent
    }

    public enum MovableSettingType
    {
        discretemovablesetting,
        continuousmovablesetting,
    }
}