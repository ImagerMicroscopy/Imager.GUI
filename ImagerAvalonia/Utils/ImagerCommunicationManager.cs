using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ImagerAvalonia.Services.MeasurementControl;
using Newtonsoft.Json.Linq;

namespace ImagerAvalonia.Utils;

public interface IImagerCommunicationManager
{
    Task PingAsync(CancellationToken cancellationToken = default);
    Task<JsonElement> ListWavelengthsAsync(CancellationToken cancellationToken = default);
    Task<List<Equipment>> ListAvailableEquipmentAsync(CancellationToken cancellationToken = default);
    Task<List<DetectorEquipment>> ListAvailableDetectorsAsync(CancellationToken cancellationToken = default);
    
    Task<StagePosition> GetMotorizedStagePositionAsync(string stageName, CancellationToken cancellationToken = default);
    Task SetMotorizedStagePositionAsync(string stageName, StagePosition position, CancellationToken cancellationToken = default);
    
    Task SetDetectorPropertyAsync(string detectorName, object propertyValue, CancellationToken cancellationToken = default);
    
    void ExecuteMeasurementProgram(JsonElement program, JsonElement? definedDetections, JsonElement? smartProgramCode, System.Threading.Channels.ChannelWriter<MeasurementEvent> channelWriter, CancellationToken cancellationToken = default);
    Task CancelMeasurementProgramAsync(CancellationToken cancellationToken = default);
}

public abstract record MeasurementEvent;
public record MeasurementStartedEvent : MeasurementEvent;
public record MeasurementDataEvent(ChannelMessage[] Messages) : MeasurementEvent;
public record MeasurementStatusTextEvent(string[] Messages) : MeasurementEvent;
public record MeasurementErrorEvent(string Error) : MeasurementEvent;
public record MeasurementCompletedEvent : MeasurementEvent;

public class ImagerCommunicationManager : IImagerCommunicationManager
{
    private readonly IImagerConnectionHandler _connectionHandler;
    private readonly ILogger<ImagerCommunicationManager> _logger;

    public ImagerCommunicationManager(ILogger<ImagerCommunicationManager> logger) {
        _connectionHandler = new ImagerConnectionHandler();
        _logger = logger;
    }

    /// <summary>
    /// Helper to send a request, validate the expected response type, and handle errors.
    /// </summary>
    private async Task<TResponse> SendAndValidateAsync<TResponse>(ImagerRequest request, CancellationToken cancellationToken) 
        where TResponse : ImagerResponse {
        try {
            var response = await _connectionHandler.SendRequestAsync(request, cancellationToken);
            

            if (response is TResponse expected) {
                return expected;
            }
            

            if (response is StatusErrorResponse errorRes) {
                _logger.LogError("Imager request '{Action}' failed with error: {Error}", request.Action, errorRes.Error);
            }
            else {
                _logger.LogError("Imager request '{Action}' returned unexpected response type: {ResponseType}", request.Action, response.GetType().Name);
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Imager request '{Action}' encountered an exception.", request.Action);
        }

        throw new InvalidOperationException("Failed to send and validate request.");
    }

    public async Task PingAsync(CancellationToken cancellationToken = default) {
        await SendAndValidateAsync<PongResponse>(new PingRequest(), cancellationToken);
    }

    public async Task<JsonElement> ListWavelengthsAsync(CancellationToken cancellationToken = default) {
        var result = await SendAndValidateAsync<WavelengthsResponse>(new ListWavelengthsRequest(), cancellationToken);
        return result.Wavelengths;
    }

    public async Task<List<Equipment>> ListAvailableEquipmentAsync(CancellationToken cancellationToken = default) {
        var result = await SendAndValidateAsync<AvailableEquipmentResponse>(new ListAvailableEquipmentRequest(), cancellationToken);
        var equipmentElement = result.Equipment;
        if (equipmentElement.ValueKind != JsonValueKind.Array) {
            throw new InvalidOperationException("Expected 'equipment' to be a JSON array.");
        }
        var equipmentList = new List<Equipment>();
        foreach(var eqElement in equipmentElement.EnumerateArray()) {
            equipmentList.Add(new Equipment(eqElement));
        }
        return equipmentList;
    }

    public async Task<List<DetectorEquipment>> ListAvailableDetectorsAsync(CancellationToken cancellationToken = default) {
        var names = await SendAndValidateAsync<AvailableDetectorsResponse>(new ListAvailableDetectorsRequest(), cancellationToken);
        
        List<DetectorEquipment> detectors = [];
        foreach (var detName in names.DetectorNames) {
            var (Properties, FrameRate) = await GetDetectorPropertiesAsync(detName, cancellationToken);
            var detector = new DetectorEquipment(detName, Properties);
            detector.Framerate = FrameRate;
            detectors.Add(detector);
        }

        return detectors;
    }

    public async Task<StagePosition> GetMotorizedStagePositionAsync(string stageName, CancellationToken cancellationToken = default) {
        var result = await SendAndValidateAsync<MotorizedStagePositionResponse>(new GetMotorizedStagePositionRequest(stageName), cancellationToken);
        return result.Position ?? throw new InvalidOperationException("Motorized stage position was null.");
    }

    public async Task SetMotorizedStagePositionAsync(string stageName, StagePosition position, CancellationToken cancellationToken = default) {
        await SendAndValidateAsync<StatusOkResponse>(new SetMotorizedStagePositionRequest(stageName, position), cancellationToken);
    }

    public async Task<(JsonElement Properties, double FrameRate)> GetDetectorPropertiesAsync(string detectorName, CancellationToken cancellationToken = default) {
        var result = await SendAndValidateAsync<DetectorPropertiesResponse>(new GetDetectorPropertiesRequest(detectorName), cancellationToken);
        return (result.DetectorProperties, result.FrameRate);
    }

    public async Task SetDetectorPropertyAsync(string detectorName, object propertyValue, CancellationToken cancellationToken = default) {
        await SendAndValidateAsync<StatusOkResponse>(new SetDetectorPropertyRequest(detectorName, propertyValue), cancellationToken);
    }

    public void ExecuteMeasurementProgram(JsonElement program, JsonElement? definedDetections, JsonElement? smartProgramCode, System.Threading.Channels.ChannelWriter<MeasurementEvent> channelWriter, CancellationToken cancellationToken = default)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // 1. Send Initialization Request
                var request = new ExecuteMeasurementProgramRequest(program, definedDetections, smartProgramCode);
                var startResponse = await _connectionHandler.SendRequestAsync(request, cancellationToken);

                if (startResponse is StatusErrorResponse err)
                {
                    await channelWriter.WriteAsync(new MeasurementErrorEvent($"Failed to start measurement: {err.Error}"), cancellationToken);
                    return;
                }
                else if (startResponse is not StatusOkResponse)
                {
                    await channelWriter.WriteAsync(new MeasurementErrorEvent($"Unexpected response when starting measurement: {startResponse.GetType().Name}"), cancellationToken);
                    return;
                }

                // Initialized successfully
                await channelWriter.WriteAsync(new MeasurementStartedEvent(), cancellationToken);

                // 2. Poll Loop
                bool keepPolling = true;
                while (keepPolling && !cancellationToken.IsCancellationRequested)
                {
                    // A. Fetch Data Streams (Images / Binary Decision Payloads)
                    var dataResponse = await _connectionHandler.SendRequestAsync(new FetchAsyncDataRequest(), cancellationToken);
                    
                    if (dataResponse is AsyncAcquiredImagesResponse imgs)
                    {
                        if (imgs.Messages.Length > 0)
                        {
                            await channelWriter.WriteAsync(new MeasurementDataEvent(imgs.Messages), cancellationToken);
                        }
                    }
                    else if (dataResponse is StatusNoNewAsyncDataComingResponse)
                    {
                        // Signal from hardware that the entire queue is completely exhausted.
                        keepPolling = false;
                    }
                    else if (dataResponse is StatusErrorResponse dataErr)
                    {
                        await channelWriter.WriteAsync(new MeasurementErrorEvent($"Data polling error: {dataErr.Error}"), cancellationToken);
                        keepPolling = false;
                    }

                    // B. Fetch Strings / Status Messages
                    if (keepPolling)
                    {
                        var msgResponse = await _connectionHandler.SendRequestAsync(new FetchAsyncStatusMessagesRequest(), cancellationToken);
                        if (msgResponse is AsyncStatusMessagesResponse textMsgs && textMsgs.Messages.Length > 0)
                        {
                            await channelWriter.WriteAsync(new MeasurementStatusTextEvent(textMsgs.Messages), cancellationToken);
                        }
                    }

                    // Throttle the loop briefly to avoid a 100% spinlock on SendRequestAsync
                    await Task.Delay(50, cancellationToken);
                }

                await channelWriter.WriteAsync(new MeasurementCompletedEvent(), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Task deliberately cancelled by user/token.
            }
            catch (Exception ex)
            {
                await channelWriter.WriteAsync(new MeasurementErrorEvent($"Critical loop failure: {ex.Message}"), default);
            }
            finally
            {
                channelWriter.TryComplete();
            }
        }, cancellationToken);
    }

    public async Task CancelMeasurementProgramAsync(CancellationToken cancellationToken = default)
    {
        await SendAndValidateAsync<StatusOkResponse>(new CancelAsyncAcquisitionRequest(), cancellationToken);
    }
}
