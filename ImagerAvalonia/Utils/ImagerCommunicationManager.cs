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
    private static ImagerCommunicationManager? _instance;
    public static ImagerCommunicationManager Instance => _instance ??= new ImagerCommunicationManager();

    private IImagerConnectionHandler? _connectionHandler;

    private IImagerConnectionHandler ConnectionHandler
    {
        get
        {
            if (_connectionHandler == null && App.Container != null)
            {
                _connectionHandler = Autofac.ResolutionExtensions.Resolve<IImagerConnectionHandler>(App.Container);
            }
            return _connectionHandler ?? throw new InvalidOperationException("Connection handler not initialized yet.");
        }
    }

    private ILogger<ImagerCommunicationManager>? _logger;

    private ILogger<ImagerCommunicationManager>? Logger
    {
        get
        {
            if (_logger == null && App.Container != null)
            {
                _logger = Autofac.ResolutionExtensions.Resolve<ILogger<ImagerCommunicationManager>>(App.Container);
            }
            return _logger;
        }
    }

    private ImagerCommunicationManager() {
        
    }

    /// <summary>
    /// Helper to send a request, validate the expected response type, and return either the expected response or an error response.
    /// </summary>
    private async Task<(TResponse? Result, StatusErrorResponse? Error)> SendAndValidateAsync<TResponse>(ImagerRequest request, CancellationToken cancellationToken) 
        where TResponse : ImagerResponse {
        try {
            var response = await ConnectionHandler.SendRequestAsync(request, cancellationToken);
            
            if (response is TResponse expected) {
                return (expected, null);
            }
            
            if (response is StatusErrorResponse errorRes) {
                return (null, errorRes);
            }
            
            Logger?.LogError("Imager request '{Action}' returned unexpected response type: {ResponseType}", request.Action, response.GetType().Name);
            return (null, new StatusErrorResponse($"Unexpected response type: {response.GetType().Name}"));
        }
        catch (Exception ex) {
            Logger?.LogError(ex, "Imager request '{Action}' encountered an exception.", request.Action);
            return (null, new StatusErrorResponse($"Exception: {ex.Message}"));
        }
    }

    public async Task PingAsync(CancellationToken cancellationToken = default) {
        var (result, error) = await SendAndValidateAsync<PongResponse>(new PingRequest(), cancellationToken);
        if (error != null) throw new InvalidOperationException($"Ping failed: {error.Error}");
    }

    public async Task<JsonElement> ListWavelengthsAsync(CancellationToken cancellationToken = default) {
        var (result, error) = await SendAndValidateAsync<WavelengthsResponse>(new ListWavelengthsRequest(), cancellationToken);
        if (error != null) throw new InvalidOperationException($"ListWavelengths failed: {error.Error}");
        return result!.Wavelengths;
    }

    public async Task<List<Equipment>> ListAvailableEquipmentAsync(CancellationToken cancellationToken = default) {
        var (result, error) = await SendAndValidateAsync<AvailableEquipmentResponse>(new ListAvailableEquipmentRequest(), cancellationToken);
        if (error != null) throw new InvalidOperationException($"ListAvailableEquipment failed: {error.Error}");
        var equipmentElement = result!.Equipment;
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
        var (names, error) = await SendAndValidateAsync<AvailableDetectorsResponse>(new ListAvailableDetectorsRequest(), cancellationToken);
        if (error != null) throw new InvalidOperationException($"ListAvailableDetectors failed: {error.Error}");
        
        List<DetectorEquipment> detectors = [];
        foreach (var detName in names!.DetectorNames) {
            var (propsResult, propsError) = await SendAndValidateAsync<DetectorPropertiesResponse>(new GetDetectorPropertiesRequest(detName), cancellationToken);
            if (propsError != null) throw new InvalidOperationException($"GetDetectorProperties for {detName} failed: {propsError.Error}");
            var detector = new DetectorEquipment(detName, propsResult!.DetectorProperties);
            detector.Framerate = propsResult.FrameRate;
            detectors.Add(detector);
        }

        return detectors;
    }

    public async Task<StagePosition> GetMotorizedStagePositionAsync(string stageName, CancellationToken cancellationToken = default) {
        var (result, error) = await SendAndValidateAsync<MotorizedStagePositionResponse>(new GetMotorizedStagePositionRequest(stageName), cancellationToken);
        if (error != null) throw new InvalidOperationException($"GetMotorizedStagePosition failed: {error.Error}");
        return result!.Position ?? throw new InvalidOperationException("Motorized stage position was null.");
    }

    public async Task SetMotorizedStagePositionAsync(string stageName, StagePosition position, CancellationToken cancellationToken = default) {
        var (result, error) = await SendAndValidateAsync<StatusOkResponse>(new SetMotorizedStagePositionRequest(stageName, position), cancellationToken);
        if (error != null) throw new InvalidOperationException($"SetMotorizedStagePosition failed: {error.Error}");
    }

    public async Task SetDetectorPropertyAsync(string detectorName, object propertyValue, CancellationToken cancellationToken = default) {
        var (result, error) = await SendAndValidateAsync<StatusOkResponse>(new SetDetectorPropertyRequest(detectorName, propertyValue), cancellationToken);
        if (error != null) throw new InvalidOperationException($"SetDetectorProperty failed: {error.Error}");
    }

    public void ExecuteMeasurementProgram(JsonElement program, JsonElement? definedDetections, JsonElement? smartProgramCode, System.Threading.Channels.ChannelWriter<MeasurementEvent> channelWriter, CancellationToken cancellationToken = default)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // 1. Send Initialization Request
                var request = new ExecuteMeasurementProgramRequest(program, definedDetections, smartProgramCode);
                var startResponse = await ConnectionHandler.SendRequestAsync(request, cancellationToken);

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
                    var dataResponse = await ConnectionHandler.SendRequestAsync(new FetchAsyncDataRequest(), cancellationToken);
                    
                    if (dataResponse is AsyncAcquiredImagesResponse imgs)
                    {
                        if (imgs.Messages.Length > 0)
                        {
                            await channelWriter.WriteAsync(new MeasurementDataEvent(imgs.Messages), cancellationToken);

                            ulong lastIndex = imgs.Messages[^1].Index;
                            _ = await SendAndValidateAsync<StatusOkResponse>(
                                new AcknowledgeDataReceiptRequest(lastIndex), 
                                cancellationToken);
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
                        var msgResponse = await ConnectionHandler.SendRequestAsync(new FetchAsyncStatusMessagesRequest(), cancellationToken);
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
        var (result, error) = await SendAndValidateAsync<StatusOkResponse>(new CancelAsyncAcquisitionRequest(), cancellationToken);
        if (error != null) {
            Logger?.LogInformation("Cancel measurement returned error (expected if not running): {Error}", error.Error);
        }
    }
}
