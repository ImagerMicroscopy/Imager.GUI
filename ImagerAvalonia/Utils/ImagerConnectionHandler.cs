using ImagerAvalonia.Services.MeasurementControl;
using MessagePack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImagerAvalonia.Utils;

#region Requests

public abstract record ImagerRequest
{
    [JsonProperty("action")]
    public string Action { get; protected set; } = string.Empty;

    public string ToJson()
    {
        return JsonConvert.SerializeObject(this, GetType(),
            new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
    }
}

public record AcquireDataRequest : ImagerRequest
{
    [JsonProperty("params")]
    public object Params { get; init; }

    public AcquireDataRequest(object parameters)
    {
        Params = parameters;
        Action = "acquiredata";
    }
}

public record ListWavelengthsRequest : ImagerRequest
{
    public ListWavelengthsRequest() => Action = "listwavelengths";
}

public record ListAvailableEquipmentRequest : ImagerRequest
{
    public ListAvailableEquipmentRequest() => Action = "listavailableequipment";
}

public record GetMotorizedStagePositionRequest : ImagerRequest
{
    [JsonProperty("name")]
    public string StageName { get; init; }

    public GetMotorizedStagePositionRequest(string stageName)
    {
        StageName = stageName;
        Action = "getmotorizedstageposition";
    }
}

public record SetMotorizedStagePositionRequest : ImagerRequest
{
    [JsonProperty("name")]
    public string StageName { get; init; }

    [JsonProperty("position")]
    public StageCoordinates Position { get; init; }

    public SetMotorizedStagePositionRequest(string stageName, StageCoordinates position)
    {
        StageName = stageName;
        Position = position;
        Action = "setmotorizedstageposition";
    }
}

public record ListAvailableDetectorsRequest : ImagerRequest
{
    public ListAvailableDetectorsRequest() => Action = "listavailabledetectors";
}

public record GetDetectorPropertiesRequest : ImagerRequest
{
    [JsonProperty("detectorname")]
    public string DetectorName { get; init; }

    public GetDetectorPropertiesRequest(string detectorName)
    {
        DetectorName = detectorName;
        Action = "getdetectorproperties";
    }
}

public record SetDetectorPropertyRequest : ImagerRequest
{
    [JsonProperty("detectorname")]
    public string DetectorName { get; init; }

    [JsonProperty("property")]
    public object PropertyValue { get; init; }

    public SetDetectorPropertyRequest(string detectorName, object propertyValue)
    {
        DetectorName = detectorName;
        PropertyValue = propertyValue;
        Action = "setdetectorproperty";
    }
}

public record PingRequest : ImagerRequest
{
    public PingRequest() => Action = "ping";
}

public record ExecuteMeasurementProgramRequest : ImagerRequest
{
    [JsonProperty("program")]
    public JObject Program { get; init; }

    [JsonProperty("defineddetections")]
    public JObject DefinedDetections { get; init; }

    [JsonProperty("smartprogramcode")]
    public JObject SmartProgramCode { get; init; }

    public ExecuteMeasurementProgramRequest(
        JObject program,
        JObject definedDetections,
        JObject smartProgramCode)
    {
        Program = program;
        DefinedDetections = definedDetections;
        SmartProgramCode = smartProgramCode;
        Action = "executemeasurementprogram";
    }
}

public record FetchAsyncDataRequest : ImagerRequest
{
    public FetchAsyncDataRequest() => Action = "fetchasyncspectra";
}

public record UseSharedMemoryForTransferRequest : ImagerRequest
{
    [JsonProperty("usesharedmemory")]
    public bool UseSharedMemory { get; init; }

    public UseSharedMemoryForTransferRequest(bool useSharedMemory)
    {
        UseSharedMemory = useSharedMemory;
        Action = "usesharedmemoryfortransfer";
    }
}

public record AcknowledgeDataReceiptRequest : ImagerRequest
{
    [JsonProperty("uptoandincluding")]
    public ulong UpToAndIncluding { get; init; }

    public AcknowledgeDataReceiptRequest(ulong upToAndIncluding)
    {
        UpToAndIncluding = upToAndIncluding;
        Action = "acknowledgedatareceipt";
    }
}

public record FetchAsyncStatusMessagesRequest : ImagerRequest
{
    public FetchAsyncStatusMessagesRequest() => Action = "fetchasyncstatusmessages";
}

public record CancelAsyncAcquisitionRequest : ImagerRequest
{
    public CancelAsyncAcquisitionRequest() => Action = "cancelasyncacquisition";
}

public record IsAsyncAcquisitionRunningRequest : ImagerRequest
{
    public IsAsyncAcquisitionRunningRequest() => Action = "isasyncacquisitionrunning";
}

[MessagePackObject]
public record StagePosition(
    [property: Key("hardwareautofocusoffset")] double HardwareAutofocusOffset,
    [property: Key("usinghardwareautofocus")] bool UsingHardwareAutofocus,
    [property: Key("x")] double X,
    [property: Key("y")] double Y,
    [property: Key("z")] double Z
);

#endregion

#region Binary Data Models

[MessagePackObject]
public record ChannelMessage(
    [property: Key("index")] ulong Index,
    [property: Key("message")] AsyncMeasurementMessage Message
);

[MessagePackObject]
public record AsyncMeasurementMessage(
    [property: Key("type")] string Type,
    [property: Key("data")] AcquiredData Data,
    [property: Key("metadata")] AcquisitionMetaData MetaData,
    [property: Key("payload")] string Decision
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
    [property: Key("stagepositionname")] string StagePositionName
);

#endregion

#region Responses

public abstract record ImagerResponse;

public record StatusOkResponse() : ImagerResponse;
public record StatusErrorResponse(string Error) : ImagerResponse;
public record StatusNoNewAsyncDataResponse() : ImagerResponse;
public record StatusNoNewAsyncDataComingResponse() : ImagerResponse;
public record StatusAcquiredDataCopiedToSharedMemoryResponse(string SharedMemoryName) : ImagerResponse;

public record AcquiredDataResponse(JToken Data) : ImagerResponse;
public record WavelengthsResponse(JToken Wavelengths) : ImagerResponse;
public record AvailableEquipmentResponse(JToken Equipment) : ImagerResponse;

public record MotorizedStagePositionResponse(XYStagePosition Position) : ImagerResponse;
public record AvailableDetectorsResponse(string[] DetectorNames) : ImagerResponse;

public record DetectorPropertiesResponse(JToken DetectorProperties, double FrameRate) : ImagerResponse;

public record PongResponse() : ImagerResponse;
public record AsyncAcquiredDataResponse(JToken Data) : ImagerResponse;

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

public class ImagerConnectionHandler : IImagerConnectionHandler, IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private SharedMemoryReader? _sharedMemoryReader;

    public ImagerConnectionHandler(string host = "localhost", int port = 3200)
    {
        _host = host;
        _port = port;
    }

    public void Dispose() {
        _sharedMemoryReader?.Dispose();
    }

    public async Task<ImagerResponse> SendRequestAsync(ImagerRequest request, CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        client.LingerState = new LingerOption(true, 0);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(20));

        await client.ConnectAsync(_host, _port, linkedCts.Token);
        await using var stream = client.GetStream();

        string jsonPayload = request.ToJson();

        if (request is ExecuteMeasurementProgramRequest)
        {
            Console.WriteLine($"[ImagerConnectionHandler] Sending:\n{jsonPayload}");
        }

        byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);
        await stream.WriteAsync(payloadBytes, linkedCts.Token);

        byte[] sizeBuffer = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(sizeBuffer, 0, sizeof(int), linkedCts.Token);

        int totalBytes = BitConverter.ToInt32(sizeBuffer, 0);
        int payloadSize = totalBytes - sizeof(int);

        if (payloadSize <= 0)
            return new UnknownJsonResponse("");

        byte[] responseBuffer = new byte[payloadSize];
        await stream.ReadExactlyAsync(responseBuffer, 0, payloadSize, linkedCts.Token);

        return ParseResponse(responseBuffer);
    }

    private ImagerResponse ParseResponse(byte[] data) {

        // check if binary data containing images or other messages
        if (!IsJsonPayload(data)) {
            try {
                var messages = new List<ChannelMessage>();
                var reader = new MessagePack.MessagePackReader(data);

                while (!reader.End) {
                    var msg = MessagePack.MessagePackSerializer.Deserialize<ChannelMessage>(ref reader);
                    messages.Add(msg);
                }

                return new AsyncAcquiredImagesResponse(messages.ToArray());
            }
            catch (Exception ex) {
                return new StatusErrorResponse(
                    $"Failed to decode binary MessagePack data: {ex.Message}");
            }
        }

        string jsonString = Encoding.UTF8.GetString(data)
            .TrimEnd('\0', ' ', '\r', '\n', '\t');

        try {
            var root = JObject.Parse(jsonString);

            string respType = root["responsetype"]?.ToString() ?? string.Empty;

            if (respType == "acquireddatacopiedtosharedmemory") {
                string shmName = root["sharedmemoryname"]?.ToString();
                
                if (string.IsNullOrEmpty(shmName)) {
                    throw new InvalidOperationException("Shared memory name cannot be null or empty.");
                }

                if (_sharedMemoryReader == null) {
                    _sharedMemoryReader = new SharedMemoryReader();
                }

                if (_sharedMemoryReader.GetMapName() != shmName) {
                    _sharedMemoryReader.Connect(shmName);
                }

                // The first 8 bytes contain the data length
                byte[] lengthPrefix = _sharedMemoryReader.ReadData(0, 8);
                long dataLength = BitConverter.ToInt64(lengthPrefix, 0);

                // Read the payload (excluding the 8-byte prefix)
                byte[] payload = _sharedMemoryReader.ReadData(8, (int)(dataLength - 8));
                
                // Route this binary data back into the existing ParseResponse flow since this is now binary data
                return ParseResponse(payload);
            }

            return respType switch {
                "status" =>
                    root["status"]?.ToString() == "ok"
                        ? new StatusOkResponse()
                        : new StatusErrorResponse(root["error"]?.ToString() ?? "Unknown Error"),

                "asyncacquisitionspectrastatus" =>
                    root["status"]?.ToString() == "nonewspectra"
                        ? new StatusNoNewAsyncDataResponse()
                        : new StatusNoNewAsyncDataComingResponse(),

                "acquireddata" =>
                    new AcquiredDataResponse(root["data"]),

                "wavelengths" =>
                    new WavelengthsResponse(root["wavelengths"]),

                "availableequipment" =>
                    new AvailableEquipmentResponse(root["equipment"]),

                "motorizedstageposition" =>
                    new MotorizedStagePositionResponse(
                        new XYStagePosition(
                            JsonConvert.DeserializeObject<StageCoordinates>(
                                root["position"].ToString())!,
                            "")),

                "availabledetectors" =>
                    new AvailableDetectorsResponse(
                        JsonConvert.DeserializeObject<string[]>(
                            root["detectornames"]?.ToString() ?? "")
                        ?? Array.Empty<string>()),

                "detectorproperties" =>
                    new DetectorPropertiesResponse(
                        root["detectorproperties"],
                        root["framerate"]?.Value<double>() ?? 0.0),

                "pong" => new PongResponse(),

                "asyncdata" =>
                    new AsyncAcquiredDataResponse(root["data"]),

                "sharedmemoryname" =>
                    new SharedMemoryNameResponse(
                        root["name"]?.ToString() ?? ""),

                "asyncstatusmessages" =>
                    new AsyncStatusMessagesResponse(
                        JsonConvert.DeserializeObject<string[]>(
                            root["messages"]?.ToString() ?? "")
                        ?? Array.Empty<string>()),

                "asyncacquisitionstatus" =>
                    new AsyncAcquisitionIsRunningResponse(
                        root["running"]?.Value<bool>() ?? false),

                _ => new UnknownJsonResponse(jsonString)
            };
        }
        catch (JsonException)
        {
            return new UnknownJsonResponse(jsonString);
        }
    }

    private bool IsJsonPayload(byte[] data)
    {
        if (data.Length < 2) return false;

        int i = 0;
        while (i < data.Length && (data[i] == 0 || char.IsWhiteSpace((char)data[i])))
            i++;

        return i < data.Length && data[i] == '{';
    }
}