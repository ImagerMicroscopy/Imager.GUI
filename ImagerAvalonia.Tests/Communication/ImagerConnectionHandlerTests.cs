using System.Net;
using System.Net.Sockets;
using System.Text;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Utils;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ImagerAvalonia.Tests.Communication;

/// <summary>
/// Runs the real ImagerConnectionHandler over TCP against a fake Imager.Core server.
/// </summary>
public class ImagerConnectionHandlerTests
{
    private static async Task<ImagerResponse> SendAsync(
        FakeImagerCoreServer server,
        ImagerRequest request,
        CancellationToken cancellationToken = default)
    {
        using var handler = new ImagerConnectionHandler("127.0.0.1", server.Port);
        return await handler.SendRequestAsync(request, cancellationToken);
    }

    [Fact]
    public async Task Sends_request_json_to_the_server()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.StatusOk);

        await SendAsync(server, new GetDetectorPropertiesRequest("DummyCam1"));

        Assert.True(server.ReceivedRequests.TryDequeue(out var request));
        Assert.Equal("getdetectorproperties", request["action"]?.ToString());
        Assert.Equal("DummyCam1", request["detectorname"]?.ToString());
    }

    [Fact]
    public async Task Status_ok_is_parsed()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.StatusOk);

        var response = await SendAsync(server, new PingRequest());

        Assert.IsType<StatusOkResponse>(response);
    }

    [Fact]
    public async Task Status_error_carries_the_error_text()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.StatusError("camera not ready"));

        var response = await SendAsync(server, new PingRequest());

        var error = Assert.IsType<StatusErrorResponse>(response);
        Assert.Equal("camera not ready", error.Error);
    }

    [Fact]
    public async Task Status_error_without_error_field_gets_a_default_message()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson("""{"responsetype":"status","status":"error"}""");

        var response = await SendAsync(server, new PingRequest());

        var error = Assert.IsType<StatusErrorResponse>(response);
        Assert.Equal("Unknown Error", error.Error);
    }

    [Fact]
    public async Task Pong_is_parsed()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.Pong);

        Assert.IsType<PongResponse>(await SendAsync(server, new PingRequest()));
    }

    [Fact]
    public async Task Available_detectors_are_parsed()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.AvailableDetectors);

        var response = await SendAsync(server, new ListAvailableDetectorsRequest());

        var detectors = Assert.IsType<AvailableDetectorsResponse>(response);
        Assert.Equal(new[] { "DummyCam1", "DummyCam2" }, detectors.DetectorNames);
    }

    [Fact]
    public async Task Detector_properties_are_parsed_with_framerate()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.DummyCam1Properties);

        var response = await SendAsync(server, new GetDetectorPropertiesRequest("DummyCam1"));

        var props = Assert.IsType<DetectorPropertiesResponse>(response);
        Assert.Equal(20.0, props.FrameRate);

        var parsed = props.DetectorProperties.ToObject<List<DetectorEquipmentProperties>>()!;
        Assert.Equal(3, parsed.Count);

        var binning = Assert.IsType<CategoricDetectorProperty>(parsed[2]);
        Assert.Equal("1", binning.current);
    }

    [Fact]
    public async Task Detector_properties_without_framerate_default_to_zero()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(
            """{"responsetype":"detectorproperties","detectorproperties":[]}""");

        var response = await SendAsync(server, new GetDetectorPropertiesRequest("x"));

        Assert.Equal(0.0, Assert.IsType<DetectorPropertiesResponse>(response).FrameRate);
    }

    [Fact]
    public async Task Motorized_stage_position_is_parsed()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.MotorizedStagePosition);

        var response = await SendAsync(server, new GetMotorizedStagePositionRequest("dStage"));

        var pos = Assert.IsType<MotorizedStagePositionResponse>(response).Position.Coordinates;
        Assert.Equal(1.5, pos.x);
        Assert.Equal(-2.25, pos.y);
        Assert.Equal(10.0, pos.z);
        Assert.True(pos.usinghardwareautofocus);
        Assert.Equal(0.75, pos.hardwareautofocusoffset);
    }

    [Fact]
    public async Task Available_equipment_is_returned_as_array()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.AvailableEquipment);

        var response = await SendAsync(server, new ListAvailableEquipmentRequest());

        var equipment = Assert.IsType<AvailableEquipmentResponse>(response);
        Assert.Equal(3, Assert.IsType<JArray>(equipment.Equipment).Count);
    }

    [Fact]
    public async Task Shared_memory_name_is_parsed()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.SharedMemoryName);

        var response = await SendAsync(server, new UseSharedMemoryForTransferRequest(true));

        Assert.Equal("imager_shm", Assert.IsType<SharedMemoryNameResponse>(response).Name);
    }

    [Fact]
    public async Task Async_status_messages_are_parsed()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.AsyncStatusMessages);

        var response = await SendAsync(server, new FetchAsyncStatusMessagesRequest());

        Assert.Equal(new[] { "starting", "position 1 of 3" },
            Assert.IsType<AsyncStatusMessagesResponse>(response).Messages);
    }

    [Fact]
    public async Task Async_acquisition_running_is_parsed()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.AsyncAcquisitionRunning);

        var response = await SendAsync(server, new IsAsyncAcquisitionRunningRequest());

        Assert.True(Assert.IsType<AsyncAcquisitionIsRunningResponse>(response).Running);
    }

    [Fact]
    public async Task No_new_async_data_is_distinguished_from_no_more_data_coming()
    {
        using (var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.NoNewAsyncData))
            Assert.IsType<StatusNoNewAsyncDataResponse>(await SendAsync(server, new FetchAsyncDataRequest()));

        using (var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.NoNewAsyncDataComing))
            Assert.IsType<StatusNoNewAsyncDataComingResponse>(await SendAsync(server, new FetchAsyncDataRequest()));
    }

    [Fact]
    public async Task Invalid_query_from_backend_is_returned_as_unknown_json_with_the_reason()
    {
        // This is the response Imager.Core sends when it cannot parse a request, e.g. a
        // setdetectorproperty with "current": null. It has no "status" field, so it does
        // not map to StatusErrorResponse; the reason is only available via the raw JSON.
        const string reason = "parsing Text failed, expected String, but encountered Null";
        using var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.InvalidQuery(reason));

        var response = await SendAsync(server, new PingRequest());

        var unknown = Assert.IsType<UnknownJsonResponse>(response);
        Assert.Contains(reason, unknown.JsonString);
    }

    [Fact]
    public async Task Server_level_error_object_is_returned_as_unknown_json()
    {
        // SimpleJSONServer answers a handler timeout with {"error":"handlertimeout"}.
        using var server = FakeImagerCoreServer.RespondingWithJson("""{"error":"handlertimeout"}""");

        var response = await SendAsync(server, new PingRequest());

        Assert.Contains("handlertimeout", Assert.IsType<UnknownJsonResponse>(response).JsonString);
    }

    [Fact]
    public async Task Unrecognised_response_type_is_returned_as_unknown_json()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson("""{"responsetype":"somethingnew","x":1}""");

        var response = await SendAsync(server, new PingRequest());

        Assert.Contains("somethingnew", Assert.IsType<UnknownJsonResponse>(response).JsonString);
    }

    [Fact]
    public async Task Malformed_json_is_returned_as_unknown_json()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson("{\"responsetype\": \"status\", ");

        var response = await SendAsync(server, new PingRequest());

        Assert.IsType<UnknownJsonResponse>(response);
    }

    [Fact]
    public async Task Trailing_nulls_and_whitespace_are_ignored()
    {
        using var server = new FakeImagerCoreServer(_ =>
            Encoding.UTF8.GetBytes(ImagerCoreWire.StatusOk + "\r\n\t  \0\0\0"));

        Assert.IsType<StatusOkResponse>(await SendAsync(server, new PingRequest()));
    }

    [Fact]
    public async Task Leading_whitespace_is_still_treated_as_json()
    {
        using var server = new FakeImagerCoreServer(_ => Encoding.UTF8.GetBytes("  \n" + ImagerCoreWire.Pong));

        Assert.IsType<PongResponse>(await SendAsync(server, new PingRequest()));
    }

    [Fact]
    public async Task Empty_payload_is_returned_as_empty_unknown_json()
    {
        using var server = new FakeImagerCoreServer(_ => Array.Empty<byte>());

        var response = await SendAsync(server, new PingRequest());

        Assert.Equal("", Assert.IsType<UnknownJsonResponse>(response).JsonString);
    }

    [Fact]
    public async Task Response_split_over_many_tcp_writes_is_reassembled()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.DummyCam1Properties);
        server.ChunkSize = 7;

        var response = await SendAsync(server, new GetDetectorPropertiesRequest("DummyCam1"));

        Assert.IsType<DetectorPropertiesResponse>(response);
    }

    [Fact]
    public async Task Large_response_is_read_completely()
    {
        var names = Enumerable.Range(0, 20_000).Select(i => $"detector-{i}").ToArray();
        var json = new JObject
        {
            ["responsetype"] = "availabledetectors",
            ["detectornames"] = new JArray(names)
        }.ToString();

        using var server = FakeImagerCoreServer.RespondingWithJson(json);

        var response = await SendAsync(server, new ListAvailableDetectorsRequest());

        Assert.Equal(names, Assert.IsType<AvailableDetectorsResponse>(response).DetectorNames);
    }

    [Fact]
    public async Task Binary_messagepack_image_messages_are_decoded()
    {
        byte[] image = { 10, 20, 30, 40, 50, 60 };
        var payload = ImagerCoreWire.Concat(
            ImagerCoreWire.AcquiredDataChannelMessage(index: 7, imageData: image, detectorName: "DummyCam2"),
            ImagerCoreWire.AcquiredDataChannelMessage(index: 8));

        using var server = new FakeImagerCoreServer(_ => payload);

        var response = await SendAsync(server, new FetchAsyncDataRequest());

        var images = Assert.IsType<AsyncAcquiredImagesResponse>(response);
        Assert.Equal(2, images.Messages.Length);

        var first = images.Messages[0];
        Assert.Equal(7UL, first.Index);
        Assert.Equal("acquireddatamessage", first.Message.Type);
        Assert.Equal("DummyCam2", first.Message.Data.DetectorName);
        Assert.Equal(image, first.Message.Data.ImageData);
        Assert.Equal(2, first.Message.Data.NRows);
        Assert.Equal(3, first.Message.Data.NCols);
        Assert.Equal(2, first.Message.Data.PixelFormat);
        Assert.Equal(1.25f, first.Message.Data.TimeStamp);
        Assert.Equal("Default", first.Message.MetaData.AcquisitionType);
        Assert.Equal("det-1", first.Message.MetaData.DetectionElementId);
        Assert.Equal("pos1", first.Message.MetaData.StagePositionName);
        Assert.Equal(3.0, first.Message.MetaData.StagePosition.Z);

        Assert.Equal(8UL, images.Messages[1].Index);
    }

    [Fact]
    public async Task Binary_smart_program_decision_message_without_data_is_decoded()
    {
        const string decision = """{"decision":"stop","programid":"p1","timestamp":3.5}""";
        var payload = ImagerCoreWire.Concat(
            ImagerCoreWire.AcquiredDataChannelMessage(index: 1),
            ImagerCoreWire.SmartProgramDecisionChannelMessage(index: 2, decision));

        using var server = new FakeImagerCoreServer(_ => payload);

        var response = await SendAsync(server, new FetchAsyncDataRequest());

        var images = Assert.IsType<AsyncAcquiredImagesResponse>(response);
        Assert.Equal(2, images.Messages.Length);

        var msg = images.Messages[1].Message;
        Assert.Equal("smartprogramdecisionmessage", msg.Type);
        Assert.Equal(decision, msg.Decision);
        Assert.Null(msg.Data);
        Assert.Null(msg.MetaData);
    }

    [Fact]
    public async Task Corrupt_binary_payload_becomes_status_error()
    {
        // 0xC1 is the one byte MessagePack never uses.
        using var server = new FakeImagerCoreServer(_ => new byte[] { 0xC1, 0xC1, 0xC1 });

        var response = await SendAsync(server, new FetchAsyncDataRequest());

        var error = Assert.IsType<StatusErrorResponse>(response);
        Assert.StartsWith("Failed to decode binary MessagePack data", error.Error);
    }

    [Fact]
    public async Task Each_request_uses_its_own_connection()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.Pong);
        using var handler = new ImagerConnectionHandler("127.0.0.1", server.Port);

        for (int i = 0; i < 5; i++)
            Assert.IsType<PongResponse>(await handler.SendRequestAsync(new PingRequest()));

        Assert.Equal(5, server.ReceivedRequests.Count);
    }

    [Fact]
    public async Task Concurrent_requests_each_get_their_own_response()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(req =>
            new JObject
            {
                ["responsetype"] = "sharedmemoryname",
                ["name"] = req["detectorname"]
            }.ToString());
        using var handler = new ImagerConnectionHandler("127.0.0.1", server.Port);

        var tasks = Enumerable.Range(0, 10)
            .Select(i => handler.SendRequestAsync(new GetDetectorPropertiesRequest($"cam{i}")))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        for (int i = 0; i < 10; i++)
            Assert.Equal($"cam{i}", Assert.IsType<SharedMemoryNameResponse>(responses[i]).Name);
    }

    [Fact]
    public async Task Cancellation_aborts_a_request_waiting_for_the_server()
    {
        using var server = FakeImagerCoreServer.RespondingWithJson(ImagerCoreWire.Pong);
        server.ResponseDelay = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SendAsync(server, new PingRequest(), cts.Token));
    }

    [Fact]
    public async Task Connection_refused_throws()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        using var handler = new ImagerConnectionHandler("127.0.0.1", port);

        await Assert.ThrowsAnyAsync<SocketException>(() => handler.SendRequestAsync(new PingRequest()));
    }
}
