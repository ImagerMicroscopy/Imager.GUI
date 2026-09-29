using System.Buffers;
using MessagePack;

namespace ImagerAvalonia.Tests.Communication;

/// <summary>
/// Canned Imager.Core responses, written to match the Haskell encoders in
/// Imager.Core/src/CuvettorTypes.hs, Camera/SCCameraTypes.hs and
/// Measurements/MeasurementProgramEncoding.hs.
/// </summary>
internal static class ImagerCoreWire
{
    public const string StatusOk = """{"responsetype":"status","status":"ok"}""";

    public static string StatusError(string error) =>
        $$"""{"responsetype":"status","status":"error","error":"{{error}}"}""";

    public const string Pong = """{"responsetype":"pong"}""";

    public const string NoNewAsyncData =
        """{"responsetype":"asyncacquisitionspectrastatus","status":"nonewspectra"}""";

    public const string NoNewAsyncDataComing =
        """{"responsetype":"asyncacquisitionspectrastatus","status":"nonewspectracoming"}""";

    public const string SharedMemoryName = """{"responsetype":"sharedmemoryname","name":"imager_shm"}""";

    // Main.hs: a request that fails to parse is answered with this shape (no "status" field).
    public static string InvalidQuery(string reason) =>
        $$"""{"responsetype":"invalidquery: {{reason}}"}""";

    public const string AvailableDetectors =
        """{"responsetype":"availabledetectors","detectornames":["DummyCam1","DummyCam2"]}""";

    public const string DummyCam1Properties = """
        {
          "responsetype": "detectorproperties",
          "detectorproperties": [
            { "propertycode": 0, "descriptor": "Exposure time", "kind": "numeric", "value": 0.1 },
            { "propertycode": 1, "descriptor": "Sensor cropping", "kind": "discrete", "current": "64x64",
              "availableoptions": ["16x16", "32x32", "64x64", "128x128"] },
            { "propertycode": 2, "descriptor": "Binning", "kind": "discrete", "current": "1",
              "availableoptions": ["1", "2", "4"] }
          ],
          "framerate": 20.0
        }
        """;

    public const string DummyCam2Properties = """
        {
          "responsetype": "detectorproperties",
          "detectorproperties": [
            { "propertycode": 0, "descriptor": "Exposure time", "kind": "numeric", "value": 0.05 },
            { "propertycode": 1, "descriptor": "Binning", "kind": "discrete", "current": "2",
              "availableoptions": ["1", "2", "4"] }
          ],
          "framerate": 200.0
        }
        """;

    public const string MotorizedStagePosition = """
        {"responsetype":"motorizedstageposition",
         "position":{"x":1.5,"y":-2.25,"z":10.0,"usinghardwareautofocus":true,"hardwareautofocusoffset":0.75}}
        """;

    public const string AsyncStatusMessages =
        """{"responsetype":"asyncstatusmessages","messages":["starting","position 1 of 3"]}""";

    public const string AsyncAcquisitionRunning = """{"responsetype":"asyncacquisitionstatus","running":true}""";

    public const string AvailableEquipment = """
        {
          "responsetype": "availableequipment",
          "equipment": [
            {
              "name": "fw1",
              "availablelightsources": [],
              "availablemovablecomponents": [
                { "componentname": "fw", "type": "discretemovablecomponent",
                  "possiblesettings": ["DAPI", "GFP", "YFP", "RFP", "640"] },
                { "componentname": "sl", "type": "continuousmovablecomponent",
                  "minvalue": 0, "maxvalue": 100, "increment": 1 }
              ],
              "availablerobots": [],
              "hasmotorizedstage": false,
              "motorizedstageName": ""
            },
            {
              "name": "hello",
              "availablelightsources": [
                { "name": "ls", "channels": ["ch1", "ch2"], "allowmultiplechannels": true, "cancontrolpower": true }
              ],
              "availablemovablecomponents": [],
              "availablerobots": [],
              "hasmotorizedstage": false,
              "motorizedstageName": ""
            },
            {
              "name": "Dummy stage",
              "availablelightsources": [],
              "availablemovablecomponents": [],
              "availablerobots": [],
              "hasmotorizedstage": true,
              "motorizedstageName": "dStage"
            }
          ]
        }
        """;

    /// <summary>
    /// Encodes an "acquireddatamessage" ChannelMessage the way Imager.Core does:
    /// string-keyed maps, Double as float64, and no "payload" key.
    /// </summary>
    public static byte[] AcquiredDataChannelMessage(
        ulong index,
        string detectorName = "DummyCam1",
        int nRows = 2,
        int nCols = 3,
        byte[]? imageData = null,
        int pixelFormat = 2,
        double timestamp = 1.25,
        string acquisitionType = "Default",
        int detectionIndex = 0,
        string detectionElementId = "det-1",
        int nImagesWithDetectionIndex = 1,
        string stagePositionName = "pos1")
    {
        imageData ??= Enumerable.Range(0, nRows * nCols).Select(i => (byte)i).ToArray();

        var buffer = new ArrayBufferWriter<byte>();
        var w = new MessagePackWriter(buffer);

        w.WriteMapHeader(2);
        w.Write("index"); w.Write(index);
        w.Write("message");

        w.WriteMapHeader(3);
        w.Write("metadata");
        w.WriteMapHeader(6);
        w.Write("stageposition");
        w.WriteMapHeader(5);
        w.Write("x"); w.Write(1.0);
        w.Write("y"); w.Write(2.0);
        w.Write("z"); w.Write(3.0);
        w.Write("usinghardwareautofocus"); w.Write(false);
        w.Write("hardwareautofocusoffset"); w.Write(0.0);
        w.Write("stagepositionname"); w.Write(stagePositionName);
        w.Write("acquisitiontype"); w.Write(acquisitionType);
        w.Write("detectionindex"); w.Write(detectionIndex);
        w.Write("nimageswithdetectionindex"); w.Write(nImagesWithDetectionIndex);
        w.Write("detectionelementid"); w.Write(detectionElementId);

        w.Write("data");
        w.WriteMapHeader(6);
        w.Write("nrows"); w.Write(nRows);
        w.Write("ncols"); w.Write(nCols);
        w.Write("timestamp"); w.Write(timestamp);
        w.Write("detectorname"); w.Write(detectorName);
        w.Write("imagedata"); w.Write(imageData);
        w.Write("numtype"); w.Write(pixelFormat);

        w.Write("type"); w.Write("acquireddatamessage");

        w.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Encodes a "smartprogramdecisionmessage" ChannelMessage: only "payload" and "type",
    /// no "data" or "metadata".
    /// </summary>
    public static byte[] SmartProgramDecisionChannelMessage(ulong index, string payloadJson)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var w = new MessagePackWriter(buffer);

        w.WriteMapHeader(2);
        w.Write("index"); w.Write(index);
        w.Write("message");
        w.WriteMapHeader(2);
        w.Write("payload"); w.Write(payloadJson);
        w.Write("type"); w.Write("smartprogramdecisionmessage");

        w.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    public static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
}
