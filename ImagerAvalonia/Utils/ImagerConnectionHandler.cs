using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

namespace ImagerAvalonia.Utils;

#region Requests

public abstract record ImagerRequest
{
    [JsonPropertyName("action")]
    public string Action { get; protected set; } = string.Empty;

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, GetType(), new JsonSerializerOptions 
        { 
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull 
        });
    }
}

public record AcquireDataRequest : ImagerRequest
{
    [JsonPropertyName("params")] public object Params { get; init; }
    public AcquireDataRequest(object parameters) { Params = parameters; Action = "acquiredata"; }
}

public record ListWavelengthsRequest : ImagerRequest { public ListWavelengthsRequest() { Action = "listwavelengths"; } }
public record ListAvailableEquipmentRequest : ImagerRequest { public ListAvailableEquipmentRequest() { Action = "listavailableequipment"; } }

public record GetMotorizedStagePositionRequest : ImagerRequest
{
    [JsonPropertyName("name")] public string StageName { get; init; }
    public GetMotorizedStagePositionRequest(string stageName) { StageName = stageName; Action = "getmotorizedstageposition"; }
}

public record SetMotorizedStagePositionRequest : ImagerRequest
{
    [JsonPropertyName("name")] public string StageName { get; init; }
    [JsonPropertyName("position")] public StagePosition Position { get; init; }
    public SetMotorizedStagePositionRequest(string stageName, StagePosition position) { StageName = stageName; Position = position; Action = "setmotorizedstageposition"; }
}

public record ListAvailableDetectorsRequest : ImagerRequest { public ListAvailableDetectorsRequest() { Action = "listavailabledetectors"; } }

public record GetDetectorPropertiesRequest : ImagerRequest
{
    [JsonPropertyName("detectorname")] public string DetectorName { get; init; }
    public GetDetectorPropertiesRequest(string detectorName) { DetectorName = detectorName; Action = "getdetectorproperties"; }
}

public record SetDetectorPropertyRequest : ImagerRequest
{
    [JsonPropertyName("detectorname")] public string DetectorName { get; init; }
    [JsonPropertyName("property")] public object PropertyValue { get; init; }
    public SetDetectorPropertyRequest(string detectorName, object propertyValue) { DetectorName = detectorName; PropertyValue = propertyValue; Action = "setdetectorproperty"; }
}

public record PingRequest : ImagerRequest { public PingRequest() { Action = "ping"; } }

public record ExecuteMeasurementProgramRequest : ImagerRequest
{
    [JsonPropertyName("program")] public object Program { get; init; }
    [JsonPropertyName("defineddetections")] public object DefinedDetections { get; init; }
    [JsonPropertyName("smartprogramcode")] public object SmartProgramCode { get; init; }
    public ExecuteMeasurementProgramRequest(object program, object definedDetections, object smartProgramCode) 
    { 
        Program = program; DefinedDetections = definedDetections; SmartProgramCode = smartProgramCode; Action = "executemeasurementprogram"; 
    }
}

public record FetchAsyncDataRequest : ImagerRequest { public FetchAsyncDataRequest() { Action = "fetchasyncspectra"; } }

public record UseSharedMemoryForTransferRequest : ImagerRequest
{
    [JsonPropertyName("usesharedmemory")] public bool UseSharedMemory { get; init; }
    public UseSharedMemoryForTransferRequest(bool useSharedMemory) { UseSharedMemory = useSharedMemory; Action = "usesharedmemoryfortransfer"; }
}

public record AcknowledgeDataReceiptRequest : ImagerRequest
{
    [JsonPropertyName("uptoandincluding")] public ulong UpToAndIncluding { get; init; }
    public AcknowledgeDataReceiptRequest(ulong upToAndIncluding) { UpToAndIncluding = upToAndIncluding; Action = "acknowledgedatareceipt"; }
}


public record FetchAsyncStatusMessagesRequest : ImagerRequest { public FetchAsyncStatusMessagesRequest() { Action = "fetchasyncstatusmessages"; } }
public record CancelAsyncAcquisitionRequest : ImagerRequest { public CancelAsyncAcquisitionRequest() { Action = "cancelasyncacquisition"; } }
public record IsAsyncAcquisitionRunningRequest : ImagerRequest { public IsAsyncAcquisitionRunningRequest() { Action = "isasyncacquisitionrunning"; } }

[MessagePackObject]
public record StagePosition(
    [property: Key("hardwareautofocusoffset"), JsonPropertyName("hardwareautofocusoffset")] double HardwareAutofocusOffset,
    [property: Key("usinghardwareautofocus"), JsonPropertyName("usinghardwareautofocus")] bool UsingHardwareAutofocus,
    [property: Key("x"), JsonPropertyName("x")] double X,
    [property: Key("y"), JsonPropertyName("y")] double Y,
    [property: Key("z"), JsonPropertyName("z")] double Z
);

#endregion

#region Binary Data Models (MessagePack)

[MessagePackObject]
public record ChannelMessage(
    [property: Key("index")] ulong Index,
    [property: Key("message")] AsyncMeasurementMessage Message
);

[MessagePackObject]
public record AsyncMeasurementMessage(
    [property: Key("type")] string Type,
    [property: Key("data")] AcquiredData? Data,
    [property: Key("metadata")] AcquisitionMetaData? MetaData,
    [property: Key("payload")] string? Decision
);

[MessagePackObject]
public record AcquiredData(
    [property: Key("detectorname")] string DetectorName,
    [property: Key("imagedata")] byte[] ImageData,
    [property: Key("ncols")] int NCols,
    [property: Key("nrows")] int NRows,
    [property: Key("numtype")] int PixelFormat,
    [property: Key("timestamp")] float TimeStamp
);

[MessagePackObject]
public record AcquisitionMetaData(
    [property: Key("acquisitiontype")] string AcquisitionType,
    [property: Key("detectionindex")] int DetectionIndex,
    [property: Key("detectionelementid")] string DetectionElementId,
    [property: Key("nimageswithdetectionindex")] int NImagesWithDetectionIndex,
    [property: Key("stageposition")] StagePosition StagePosition,
    [property: Key("stagepositionname")] string? StagePositionName
);

#endregion

#region Responses

public abstract record ImagerResponse;

public record StatusOkResponse() : ImagerResponse;
public record StatusErrorResponse(string Error) : ImagerResponse;
public record StatusNoNewAsyncDataResponse() : ImagerResponse;
public record StatusNoNewAsyncDataComingResponse() : ImagerResponse;
public record StatusAcquiredDataCopiedToSharedMemoryResponse(string SharedMemoryName) : ImagerResponse;
public record AcquiredDataResponse(JsonElement Data) : ImagerResponse;
public record WavelengthsResponse(JsonElement Wavelengths) : ImagerResponse;
public record AvailableEquipmentResponse(JsonElement Equipment) : ImagerResponse;
public record MotorizedStagePositionResponse(StagePosition Position) : ImagerResponse;
public record AvailableDetectorsResponse(string[] DetectorNames) : ImagerResponse;
public record DetectorPropertiesResponse(JsonElement DetectorProperties, double FrameRate) : ImagerResponse;
public record PongResponse() : ImagerResponse;
public record AsyncAcquiredDataResponse(JsonElement Data) : ImagerResponse;
public record SharedMemoryNameResponse(string Name) : ImagerResponse;
public record AsyncStatusMessagesResponse(string[] Messages) : ImagerResponse;
public record AsyncAcquisitionIsRunningResponse(bool Running) : ImagerResponse;

public record AsyncAcquiredImagesResponse(ChannelMessage[] Messages) : ImagerResponse;
public record UnknownJsonResponse(string JsonString) : ImagerResponse;

#endregion

public interface IImagerConnectionHandler
{
    Task<ImagerResponse> SendRequestAsync(ImagerRequest request, CancellationToken cancellationToken = default);
}

public class ImagerConnectionHandler : IImagerConnectionHandler
{
    private readonly string _host;
    private readonly int _port;

    public ImagerConnectionHandler(string host = "localhost", int port = 3200)
    {
        _host = host;
        _port = port;
    }

    public async Task<ImagerResponse> SendRequestAsync(ImagerRequest request, CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        client.LingerState = new LingerOption(true, 0);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(20));

        await client.ConnectAsync(_host, _port, linkedCts.Token);
        await using var stream = client.GetStream();

        byte[] payloadBytes = Encoding.UTF8.GetBytes(request.ToJson());
        await stream.WriteAsync(payloadBytes, linkedCts.Token);

        byte[] sizeBuffer = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(sizeBuffer, 0, sizeof(int), linkedCts.Token);
        int totalBytes = BitConverter.ToInt32(sizeBuffer, 0);
        int payloadSize = totalBytes - sizeof(int);

        if (payloadSize <= 0) return new UnknownJsonResponse("");

        byte[] responseBuffer = new byte[payloadSize];
        await stream.ReadExactlyAsync(responseBuffer, 0, payloadSize, linkedCts.Token);

        return ParseResponse(responseBuffer);
    }

    private ImagerResponse ParseResponse(byte[] data)
    {
        if (!IsJsonPayload(data))
        {
            try
            {
                var messages = new List<ChannelMessage>();
                var reader = new MessagePackReader(data);
                
                while (!reader.End)
                {
                    var msg = MessagePackSerializer.Deserialize<ChannelMessage>(ref reader);
                    messages.Add(msg);
                }

                return new AsyncAcquiredImagesResponse(messages.ToArray());
            }
            catch (Exception ex)
            {
                return new StatusErrorResponse($"Failed to decode binary message pack data: {ex.Message}");
            }
        }

        string jsonString = Encoding.UTF8.GetString(data).TrimEnd('\0', ' ', '\r', '\n', '\t');

        try
        {
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("responsetype", out var responseTypeProp))
            {
                string respType = responseTypeProp.GetString() ?? string.Empty;

                return respType switch
                {
                    "status" => root.TryGetProperty("status", out var status) && status.GetString() == "ok" 
                        ? new StatusOkResponse() 
                        : new StatusErrorResponse(root.TryGetProperty("error", out var err) ? err.GetString() ?? "" : "Unknown Error"),
                    
                    "asyncacquisitionspectrastatus" => root.TryGetProperty("status", out var asyncStatus) && asyncStatus.GetString() == "nonewspectra"
                        ? new StatusNoNewAsyncDataResponse()
                        : new StatusNoNewAsyncDataComingResponse(),

                    "acquireddatacopiedtosharedmemory" => new StatusAcquiredDataCopiedToSharedMemoryResponse(
                        root.TryGetProperty("sharedmemoryname", out var smName) ? smName.GetString() ?? "" : ""),
                    
                    "acquireddata" => new AcquiredDataResponse(root.GetProperty("data")),
                    "wavelengths" => new WavelengthsResponse(root.GetProperty("wavelengths")),
                    "availableequipment" => new AvailableEquipmentResponse(root.GetProperty("equipment")),
                    
                    "motorizedstageposition" => new MotorizedStagePositionResponse(
                        JsonSerializer.Deserialize<StagePosition>(root.GetProperty("position").GetRawText())!),
                    
                    "availabledetectors" => new AvailableDetectorsResponse(
                        JsonSerializer.Deserialize<string[]>(root.GetProperty("detectornames").GetRawText()) ?? Array.Empty<string>()),
                    
                    "detectorproperties" => new DetectorPropertiesResponse(
                        root.GetProperty("detectorproperties"), root.TryGetProperty("framerate", out var fr) ? fr.GetDouble() : 0.0),
                    
                    "pong" => new PongResponse(),
                    "asyncdata" => new AsyncAcquiredDataResponse(root.GetProperty("data")),
                    
                    "sharedmemoryname" => new SharedMemoryNameResponse(
                        root.TryGetProperty("name", out var name) ? name.GetString() ?? "" : ""),
                    
                    "asyncstatusmessages" => new AsyncStatusMessagesResponse(
                        JsonSerializer.Deserialize<string[]>(root.GetProperty("messages").GetRawText()) ?? Array.Empty<string>()),
                    
                    "asyncacquisitionstatus" => new AsyncAcquisitionIsRunningResponse(
                        root.TryGetProperty("running", out var run) && run.GetBoolean()),
                    
                    _ => new UnknownJsonResponse(jsonString)
                };
            }

            return new UnknownJsonResponse(jsonString);
        }
        catch (JsonException)
        {
            return new UnknownJsonResponse(jsonString);
        }
    }

    private bool IsJsonPayload(byte[] data)
    {
        if (data.Length < 2) return false;

        int startObj = 0;
        while (startObj < data.Length && (data[startObj] == '\0' || char.IsWhiteSpace((char)data[startObj])))
        {
            startObj++;
        }

        return startObj < data.Length && data[startObj] == '{';
    }
}
