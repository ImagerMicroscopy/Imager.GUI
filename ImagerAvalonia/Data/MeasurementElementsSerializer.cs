using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ImagerAvalonia.Data.Measurements;

/// <summary>
/// Configures JSON serialization for MeasurementElements without polluting 
/// the domain classes with attributes. Matches the Haskell Aeson encoding.
/// </summary>
public static class MeasurementElementsSerializer
{
    public static JsonSerializerOptions Options { get; }

    static MeasurementElementsSerializer()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        
        // 1. Add mappings for Polymorphism
        resolver.Modifiers.Add(ConfigurePolymorphism);
        
        // 2. Add mappings to handle property renaming (lowercasing, specific mappings)
        resolver.Modifiers.Add(ConfigurePropertyNames);

        Options = new JsonSerializerOptions
        {
            TypeInfoResolver = resolver,
            WriteIndented = true,
            IncludeFields = true, // Required for legacy classes that use fields
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        // 3. Haskell defines planes as !(Int, Int) array structures. We map the C# ValueTuple.
        Options.Converters.Add(new IntTupleArrayConverter());
    }

    private static void ConfigurePolymorphism(JsonTypeInfo jsonTypeInfo)
    {
        // Setup polymorphic discriminator for the main MeasurementElement ADT
        if (jsonTypeInfo.Type == typeof(MeasurementElement))
        {
            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "elementtype",
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                DerivedTypes =
                {
                    new JsonDerivedType(typeof(MEDetection), "detection"),
                    new JsonDerivedType(typeof(MEIrradiation), "irradiation"),
                    new JsonDerivedType(typeof(MEWait), "wait"),
                    new JsonDerivedType(typeof(MEUpdateAcquisition), "updateacquisition"),
                    new JsonDerivedType(typeof(MEExecuteRobotProgram), "executerobotprogram"),
                    new JsonDerivedType(typeof(MEDoTimes), "dotimes"),
                    new JsonDerivedType(typeof(METimeLapse), "timelapse"),
                    new JsonDerivedType(typeof(MEStageLoop), "stageloop"),
                    new JsonDerivedType(typeof(MERelativeStageLoop), "relativestageloop")
                }
            };
        }
        // Setup polymorphic discriminator for RobotProgramArgument ADT
        else if (jsonTypeInfo.Type == typeof(RobotProgramArgument))
        {
            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "robotprogramargumenttype",
                DerivedTypes =
                {
                    new JsonDerivedType(typeof(DiscreteRobotProgramArgument), "discrete"),
                    new JsonDerivedType(typeof(ContinuousRobotProgramArgument), "continuous")
                }
            };
        }
        // Setup polymorphic discriminator for Legacy DetectorEquipmentProperties
        else if (jsonTypeInfo.Type == typeof(ImagerAvalonia.Services.MeasurementControl.DetectorEquipmentProperties))
        {
            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "kind",
                DerivedTypes =
                {
                    new JsonDerivedType(typeof(ImagerAvalonia.Services.MeasurementControl.NumericDetectorProperty), "numeric"),
                    new JsonDerivedType(typeof(ImagerAvalonia.Services.MeasurementControl.CategoricDetectorProperty), "discrete")
                }
            };
        }
    }

    private static void ConfigurePropertyNames(JsonTypeInfo jsonTypeInfo)
    {
        // For System.Text.Json polymorphism to work cleanly with the legacy DetectorEquipmentProperties,
        // we completely hide the C# 'kind' property from STJ since the polymorphic discriminator natively injects the "kind" JSON.
        if (typeof(ImagerAvalonia.Services.MeasurementControl.DetectorEquipmentProperties).IsAssignableFrom(jsonTypeInfo.Type))
        {
            var kindProp = System.Linq.Enumerable.FirstOrDefault(jsonTypeInfo.Properties, p => p.Name.Equals("kind", StringComparison.OrdinalIgnoreCase));
            if (kindProp != null)
            {
                kindProp.ShouldSerialize = (obj, _) => false;
            }
        }

        foreach (var property in jsonTypeInfo.Properties)
        {
            // Haskell essentially expects lowercase names by default
            string defaultLowercase = property.Name.ToLowerInvariant();
            
            // Map C# explicit readability names to Haskell's shorthand keys
            property.Name = property.Name switch
            {
                "DurationInSeconds" => "duration",
                "WaitDurationInSeconds" => "timedelta",
                "ArgumentValue" => "argument",
                "ComponentSettings" => "movablecomponentsettings",
                "LightSourceChannels" => "lightsourcechannel",
                "Powers" => "lightsourcepower",
                _ => defaultLowercase
            };
        }
    }
}

/// <summary>
/// Converts C# ValueTuple (int, int) to JSON Array [int, int] to match Haskell !(Int, Int)
/// </summary>
public class IntTupleArrayConverter : JsonConverter<(int, int)>
{
    public override (int, int) Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected array for tuple.");
        
        reader.Read();
        int item1 = reader.GetInt32();
        
        reader.Read();
        int item2 = reader.GetInt32();
        
        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray) throw new JsonException("Expected end of array for tuple.");
        
        return (item1, item2);
    }

    public override void Write(Utf8JsonWriter writer, (int, int) value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Item1);
        writer.WriteNumberValue(value.Item2);
        writer.WriteEndArray();
    }
}
