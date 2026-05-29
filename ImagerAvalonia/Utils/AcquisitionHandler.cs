using System;
using System.Collections.Generic;
using System.Threading;
using System.Buffers;
using MessagePack;
using Newtonsoft.Json.Linq;
using ImagerAvalonia.Services.MeasurementControl;
using System.Threading.Tasks;
using System.Text.Json;

namespace ImagerAvalonia.Utils
{

    public interface IAcquisitionHandler
    {
        public void ProcessIncomingData(byte[] receivedData);
    }

    // Stores X, Y, Z positional data
    public class PositionData
    {
        public List<XYStagePosition> Positions = new();
    }

    // Stores multi-channel image data and metadata
    public class ImageData
    {
        public List<byte[]> Images = new();
        public List<TiffPlaneMetadata> Metadata = new();
        public List<string> ImageResponseType = new();
        public List<List<uint>> Sizes = new();
        public List<XYStagePosition> TraversedPositions = new();
        public List<string> Decisions = new();
        public TimeSpan? AcquistionTime = null;
    }

    // Represents experimental variables/conditions
    public class DetectionData
    {
        public Dictionary<string, string> ExperimentalVariables = new();
    }

    // Main data structure combining positions, images, and detections
    public class ImagerData
    {
        public PositionData positions = new();
        public ImageData images = new();
        public DetectionData acquisitions = new();
    }

    // Handles acquisition using MessagePack formatting
    public class MessagePackAcquisitionHandler
    {
        private readonly IStorageProvider _storageProvider;
        private readonly IImagerConnectionHandler _connectionHandler;

        public List<TiffPlaneMetadata> receivedMetaData = new();
        private ulong numReceivedData = 0;
        public AcquisitionState state = AcquisitionState.Running;

        public bool IsNewDataAvailable = false;

        public MessagePackAcquisitionHandler(IStorageProvider storageProvider, IImagerConnectionHandler connectionHandler)
        {
            _storageProvider = storageProvider;
            _connectionHandler = connectionHandler;
        }

        public PositionData positions = new();
        public ImageData images = new();
        public DetectionData acquisitions = new();
        public int numDatasets = 0;

        public async Task StartAcquisitionAsync(CancellationToken cancellationToken)
        {
            JObject program = _storageProvider._measurementProgram;
            var parsed_program = program?["program"]; // This is the 'measurement_program' containing action, program, defineddetections, etc.
            if (parsed_program != null)
            {
                var innerProgram = parsed_program["program"];
                var definedDetections = parsed_program["defineddetections"];
                var smartProgramCode = parsed_program["smartprogramcode"];
                
                using var docProgram = JsonDocument.Parse(innerProgram.ToString(Newtonsoft.Json.Formatting.None));
                JsonElement? docDefinedDetections = definedDetections != null ? JsonDocument.Parse(definedDetections.ToString(Newtonsoft.Json.Formatting.None)).RootElement : null;
                JsonElement? docSmartProgramCode = smartProgramCode != null ? JsonDocument.Parse(smartProgramCode.ToString(Newtonsoft.Json.Formatting.None)).RootElement : null;

                var request = new ExecuteMeasurementProgramRequest(docProgram.RootElement.Clone(), docDefinedDetections?.Clone(), docSmartProgramCode?.Clone());
                await _connectionHandler.SendRequestAsync(request, cancellationToken);
            }
            _storageProvider.OpenWriteStream();
        }

        public async Task<ImageData> FetchDataAsync(CancellationTokenSource src)
        {
            var image_data = new ImageData();

            Console.WriteLine($"[{DateTime.Now}] Requesting async data");

            ImagerResponse response = null;
            try
            {
                // Send fetch request and process response
                response = await _connectionHandler.SendRequestAsync(new FetchAsyncDataRequest(), src.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("FetchDataAsync operation was cancelled.");
            }

            if (response != null)
            {
                if (response is AsyncAcquiredImagesResponse imagesResponse)
                {
                    image_data = ProcessMessages(imagesResponse.Messages);
                    Console.WriteLine($"[{DateTime.Now}] Received data: number of images = {image_data.Images.Count}");

                    if (image_data.Images.Count > 0)
                    {
                        _storageProvider.SavePlanes(image_data.Images, image_data.Metadata);
                    }
                    if (image_data.Decisions.Count > 0)
                    {
                        _storageProvider.SaveDecisions(image_data.Decisions);
                    }
                    try {
                        await AcknowledgeDataAsync(numReceivedData, src.Token);
                    } catch (OperationCanceledException) {}
                    IsNewDataAvailable = true;
                }
                else if (response is StatusNoNewAsyncDataResponse)
                {
                    // no more spectra
                    state = AcquisitionState.Completed;
                    IsNewDataAvailable = true;
                    return new ImageData();
                }
                else if (response is StatusNoNewAsyncDataComingResponse)
                {
                    // just no new data right now, but might come later
                }
                else if (response is StatusErrorResponse err)
                {
                    if (err.Error.Contains("AsyncCancelled"))
                    {
                        Console.WriteLine("Server reported AsyncCancelled. Setting state to Canceled.");
                        state = AcquisitionState.Canceled;
                        return image_data;
                    }
                    state = AcquisitionState.Completed;
                    Console.WriteLine($"SERVER RETURNED ERROR: {err.Error}"); 
                    throw new AcquisitionException($"User error {err.Error}");
                }
            }

            // If cancellation is requested, send cancel message
            if (src.IsCancellationRequested)
            {
                state = AcquisitionState.Canceled;
                await _connectionHandler.SendRequestAsync(new CancelAsyncAcquisitionRequest());
            }
            return image_data;
        }

        public ImageData ProcessMessages(ChannelMessage[] messages)
        {
            var images_data = new ImageData();

            foreach (var message_data in messages)
            {
                images_data.ImageResponseType.Add(message_data.Message.Type);
                numReceivedData = message_data.Index;

                if (message_data.Message.Type == "smartprogramdecisionmessage")
                {
                    if (message_data.Message.Decision is null)
                    {
                        images_data.Decisions.Add(string.Empty);
                    }
                    else
                    { 
                        images_data.Decisions.Add(message_data.Message.Decision); 
                    }
                }

                var msg = message_data.Message;

                if (msg.MetaData is not null && msg.Data is not null)
                {
                    var sp = msg.MetaData.StagePosition;
                    var tiffMeta = new TiffPlaneMetadata
                    {
                        PositionX = sp.X,
                        PositionY = sp.Y,
                        PositionZ = sp.Z,
                        AcquisitionName = msg.MetaData.AcquisitionType,
                        DetectorName = msg.Data.DetectorName,
                        Width = (uint)msg.Data.NCols,
                        Height = (uint)msg.Data.NRows,
                        TimePoint = msg.Data.TimeStamp,
                        Type = msg.Data.PixelFormat,
                        DetectionIndex = msg.MetaData.DetectionIndex,
                        PositionName = msg.MetaData.StagePositionName ?? string.Empty,
                        ElementID = msg.MetaData.DetectionElementId,
                        CurrentStagePosition = new XYStagePosition(
                            (float)sp.X, (float)sp.Y, (float)sp.Z, sp.UsingHardwareAutofocus,
                            (float)sp.HardwareAutofocusOffset, msg.MetaData.StagePositionName ?? string.Empty)
                    };

                    images_data.Metadata.Add(tiffMeta);
                    images_data.Images.Add(msg.Data.ImageData);
                    images_data.Sizes.Add(new List<uint>() { (uint)msg.Data.NCols, (uint)msg.Data.NRows });
                }
            }

            return images_data;
        }

        // Sends an acknowledgment for received data
        public async Task AcknowledgeDataAsync(ulong lastIndex, CancellationToken cancellationToken)
        {
            await _connectionHandler.SendRequestAsync(new AcknowledgeDataReceiptRequest(lastIndex), cancellationToken);
        }
    }

    public enum AcquisitionState
    {
        Running,
        Completed,
        Failed,
        Canceled
    }

    public class AcquisitionException : Exception
    {
        public AcquisitionException(string message) : base(message)
        {
        }
    }
}
