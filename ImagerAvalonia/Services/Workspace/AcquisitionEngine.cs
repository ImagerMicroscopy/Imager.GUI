using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ImagerAvalonia.Data;
using ImagerAvalonia.Data.Measurements;
using ImagerAvalonia.Utils;

namespace ImagerAvalonia.Services.Workspace;

/// <summary>
/// A strict hardware orchestration class. It knows absolutely nothing about the UI.
/// It only knows how to talk to IImagerCommunicationManager and route data out via events.
/// </summary>
public class AcquisitionEngine
{
    private readonly IImagerCommunicationManager _comManager;
    private CancellationTokenSource? _activeCts;

    public event EventHandler? MeasurementStarted;
    public event EventHandler? MeasurementCompleted;
    public event EventHandler<string>? ErrorOccurred;
    
    // Instead of directly updating UI properties, it fires an event when an image arrives
    public event EventHandler<ChannelMessage>? ImageReceived;
    public event EventHandler<string>? StatusMessageReceived;

    public AcquisitionEngine(IImagerCommunicationManager comManager)
    {
        _comManager = comManager;
    }

    public async Task RunMeasurementAsync(
        MeasurementElement program, 
        List<DefinedDetection> detections, 
        List<object> smartCode,
        CancellationToken externalToken = default)
    {
        _activeCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        var channel = Channel.CreateUnbounded<MeasurementEvent>();

        // Start reading the channel immediately in the background
        var consumerTask = ConsumeEventsAsync(channel.Reader, _activeCts.Token);

        // Kick off the measurement using the new overload
        _comManager.ExecuteMeasurementProgram(program, detections, smartCode, channel.Writer, _activeCts.Token);

        // Wait for the pipeline to drain completely
        await consumerTask;
    }

    public void Cancel()
    {
        _activeCts?.Cancel();
        _comManager.CancelMeasurementProgramAsync().Wait(); // Fire and forget or handle properly
    }

    private async Task ConsumeEventsAsync(ChannelReader<MeasurementEvent> reader, CancellationToken token)
    {
        try
        {
            await foreach (var evt in reader.ReadAllAsync(token))
            {
                switch (evt)
                {
                    case MeasurementStartedEvent:
                        MeasurementStarted?.Invoke(this, EventArgs.Empty);
                        break;
                    case MeasurementDataEvent dataEvt:
                        foreach (var msg in dataEvt.Messages)
                        {
                            ImageReceived?.Invoke(this, msg);
                        }
                        break;
                    case MeasurementStatusTextEvent txtEvt:
                        foreach (var txt in txtEvt.Messages)
                        {
                            StatusMessageReceived?.Invoke(this, txt);
                        }
                        break;
                    case MeasurementCompletedEvent:
                        MeasurementCompleted?.Invoke(this, EventArgs.Empty);
                        break;
                    case MeasurementErrorEvent errEvt:
                        ErrorOccurred?.Invoke(this, errEvt.Error);
                        MeasurementCompleted?.Invoke(this, EventArgs.Empty); // Force complete on error
                        break;
                }
            }
        }
        catch (OperationCanceledException) { /* normal cancellation */ }
    }
}
