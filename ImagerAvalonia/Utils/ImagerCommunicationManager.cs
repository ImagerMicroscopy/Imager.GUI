using ImagerAvalonia.Services;
using ImagerAvalonia.Services.ImagerModels.EquipmentModels;
using ImagerAvalonia.Services.MeasurementControl;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImagerAvalonia.Utils;

public interface IImagerCommunicationManager
{
    Task PingAsync(CancellationToken cancellationToken = default);
    Task<JToken> ListWavelengthsAsync(CancellationToken cancellationToken = default);
    Task<List<EquipmentContainer>> ListAvailableEquipmentAsync(CancellationToken cancellationToken = default);
    Task<List<DetectorEquipmentModel>> ListAvailableDetectorsAsync(CancellationToken cancellationToken = default);

    Task<XYStagePosition> GetMotorizedStagePositionAsync(string stageName, CancellationToken cancellationToken = default);
    Task SetMotorizedStagePositionAsync(string stageName, StageCoordinates position, CancellationToken cancellationToken = default);

    Task SetDetectorPropertyAsync(string detectorName, object propertyValue, CancellationToken cancellationToken = default);

    void ExecuteMeasurementProgram(
        ExecuteMeasurementProgramRequest request,
        System.Threading.Channels.ChannelWriter<MeasurementEvent> channelWriter,
        CancellationToken cancellationToken = default);



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
                _connectionHandler =
                    Autofac.ResolutionExtensions.Resolve<IImagerConnectionHandler>(App.Container);
            }

            return _connectionHandler
                ?? throw new InvalidOperationException("Connection handler not initialized yet.");
        }
    }

    private ILogger<ImagerCommunicationManager>? _logger;

    private ILogger<ImagerCommunicationManager>? Logger
    {
        get
        {
            if (_logger == null && App.Container != null)
            {
                _logger =
                    Autofac.ResolutionExtensions.Resolve<ILogger<ImagerCommunicationManager>>(App.Container);
            }

            return _logger;
        }
    }

    public bool IsInteractionEnabled { get; set; } = true;

    private ImagerCommunicationManager() { }

    private async Task<(TResponse? Result, StatusErrorResponse? Error)>
        SendAndValidateAsync<TResponse>(ImagerRequest request, CancellationToken cancellationToken)
        where TResponse : ImagerResponse
    {
        try
        {
            var response = await ConnectionHandler.SendRequestAsync(request, cancellationToken);

            if (response is TResponse ok)
                return (ok, null);

            if (response is StatusErrorResponse err)
                return (null, err);

            Logger?.LogError(
                "Imager request '{Action}' returned unexpected response type: {Type}",
                request.Action,
                response.GetType().Name);

            return (null, new StatusErrorResponse($"Unexpected response type: {response.GetType().Name}"));
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Imager request '{Action}' failed.", request.Action);
            return (null, new StatusErrorResponse($"Exception: {ex.Message}"));
        }
    }

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        var (_, error) = await SendAndValidateAsync<PongResponse>(new PingRequest(), cancellationToken);
        if (error != null)
            throw new InvalidOperationException($"Ping failed: {error.Error}");
    }

    public async Task<JToken> ListWavelengthsAsync(CancellationToken cancellationToken = default)
    {
        var (result, error) =
            await SendAndValidateAsync<WavelengthsResponse>(new ListWavelengthsRequest(), cancellationToken);

        if (error != null)
            throw new InvalidOperationException(error.Error);

        return result!.Wavelengths;
    }

    public async Task<List<EquipmentContainer>> ListAvailableEquipmentAsync(CancellationToken cancellationToken = default)
    {
        var (result, error) =
            await SendAndValidateAsync<AvailableEquipmentResponse>(
                new ListAvailableEquipmentRequest(),
                cancellationToken);

        if (error != null)
            throw new InvalidOperationException(error.Error);

        var equipmentArray = result!.Equipment as JArray
            ?? throw new InvalidOperationException("Expected equipment array.");

        return equipmentArray
            .Select(eq => eq.ToObject<EquipmentContainer>(
                JsonSerializer.Create(DetectionEquipmentSerializer.Settings)))
            .OfType<EquipmentContainer>()
            .ToList();
    }

    public async Task<List<DetectorEquipmentModel>> ListAvailableDetectorsAsync(CancellationToken cancellationToken = default)
    {
        var (names, error) =
            await SendAndValidateAsync<AvailableDetectorsResponse>(
                new ListAvailableDetectorsRequest(),
                cancellationToken);

        if (error != null)
            throw new InvalidOperationException(error.Error);

        var detectors = new List<DetectorEquipmentModel>();

        foreach (var detName in names!.DetectorNames)
        {
            var (propsResult, propsError) =
                await SendAndValidateAsync<DetectorPropertiesResponse>(
                    new GetDetectorPropertiesRequest(detName),
                    cancellationToken);

            if (propsError != null)
                throw new InvalidOperationException(propsError.Error);

            var props = propsResult!.DetectorProperties.ToObject<List<DetectorEquipmentProperties>>()
                         ?? new List<DetectorEquipmentProperties>();

            var detector = new DetectorEquipmentModel(detName, props);
            detector.Framerate = propsResult.FrameRate;

            detectors.Add(detector);
        }

        return detectors;
    }

    public async Task<XYStagePosition> GetMotorizedStagePositionAsync(string stageName, CancellationToken cancellationToken = default)
    {
        var (result, error) =
            await SendAndValidateAsync<MotorizedStagePositionResponse>(
                new GetMotorizedStagePositionRequest(stageName),
                cancellationToken);

        if (error != null)
            throw new InvalidOperationException(error.Error);

        return result!.Position
            ?? throw new InvalidOperationException("Motorized stage position was null.");
    }

    public async Task SetMotorizedStagePositionAsync(string stageName, StageCoordinates position, CancellationToken cancellationToken = default)
    {
        var (_, error) =
            await SendAndValidateAsync<StatusOkResponse>(
                new SetMotorizedStagePositionRequest(stageName, position),
                cancellationToken);

        if (error != null)
            throw new InvalidOperationException(error.Error);
    }

    public async Task SetDetectorPropertyAsync(string detectorName, object propertyValue, CancellationToken cancellationToken = default)
    {
        var (_, error) =
            await SendAndValidateAsync<StatusOkResponse>(
                new SetDetectorPropertyRequest(detectorName, propertyValue),
                cancellationToken);

        if (error != null)
            throw new InvalidOperationException(error.Error);
    }

    public void ExecuteMeasurementProgram(ExecuteMeasurementProgramRequest request,
        System.Threading.Channels.ChannelWriter<MeasurementEvent> channelWriter,
        CancellationToken cancellationToken = default)
    {
        _ = Task.Run(async () => {
            try {
                // Request the server to use shared memory for data transfer
                await SendAndValidateAsync<SharedMemoryNameResponse>(
                    new UseSharedMemoryForTransferRequest(true),
                    cancellationToken);

                var startResponse =
                    await ConnectionHandler.SendRequestAsync(request, cancellationToken);

                if (startResponse is StatusErrorResponse err) {
                    await channelWriter.WriteAsync(
                        new MeasurementErrorEvent($"Failed to start: {err.Error}"),
                        cancellationToken);

                    return;
                }

                if (startResponse is not StatusOkResponse) {
                    await channelWriter.WriteAsync(
                        new MeasurementErrorEvent($"Unexpected start response: {startResponse.GetType().Name}"),
                        cancellationToken);

                    return;
                }

                await channelWriter.WriteAsync(new MeasurementStartedEvent(), cancellationToken);

                bool keepPolling = true;

                while (keepPolling && !cancellationToken.IsCancellationRequested) {
                    var dataResponse =
                        await ConnectionHandler.SendRequestAsync(new FetchAsyncDataRequest(), cancellationToken);

                    if (dataResponse is AsyncAcquiredImagesResponse imgs) {
                        if (imgs.Messages.Length > 0) {
                            await channelWriter.WriteAsync(
                                new MeasurementDataEvent(imgs.Messages),
                                cancellationToken);

                            ulong lastIndex = imgs.Messages[^1].Index;

                            _ = await SendAndValidateAsync<StatusOkResponse>(
                                new AcknowledgeDataReceiptRequest(lastIndex),
                                cancellationToken);
                        }
                    } else if (dataResponse is StatusNoNewAsyncDataComingResponse) {
                        keepPolling = false;
                    } else if (dataResponse is StatusErrorResponse dataErr) {
                        await channelWriter.WriteAsync(
                            new MeasurementErrorEvent($"Polling error: {dataErr.Error}"),
                            cancellationToken);

                        keepPolling = false;
                    }

                    if (keepPolling) {
                        var msgResponse =
                            await ConnectionHandler.SendRequestAsync(
                                new FetchAsyncStatusMessagesRequest(),
                                cancellationToken);

                        if (msgResponse is AsyncStatusMessagesResponse msgs && msgs.Messages.Length > 0) {
                            await channelWriter.WriteAsync(
                                new MeasurementStatusTextEvent(msgs.Messages),
                                cancellationToken);
                        }
                    }

                    await Task.Delay(50, cancellationToken);
                }

                await channelWriter.WriteAsync(new MeasurementCompletedEvent(), cancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) {
                await channelWriter.WriteAsync(
                    new MeasurementErrorEvent($"Critical failure: {ex.Message}"),
                    CancellationToken.None);
            }
            finally {
                channelWriter.TryComplete();
            }
        }, cancellationToken);
    }

    public async Task CancelMeasurementProgramAsync(CancellationToken cancellationToken = default)
    {
        var (_, error) =
            await SendAndValidateAsync<StatusOkResponse>(
                new CancelAsyncAcquisitionRequest(),
                cancellationToken);

        if (error != null)
            Logger?.LogInformation("Cancel returned error: {Error}", error.Error);
    }
}