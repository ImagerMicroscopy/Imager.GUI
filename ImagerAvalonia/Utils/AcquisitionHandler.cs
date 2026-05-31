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
    public class MessagePackAcquisitionHandler {
        private readonly IStorageProvider _storageProvider;
        private readonly IImagerConnectionHandler _connectionHandler;

        public List<TiffPlaneMetadata> receivedMetaData = new();
        private ulong numReceivedData = 0;
        public AcquisitionState state = AcquisitionState.Running;

        public bool IsNewDataAvailable = false;

        public MessagePackAcquisitionHandler(IStorageProvider storageProvider, IImagerConnectionHandler connectionHandler) {
            _storageProvider = storageProvider;
            _connectionHandler = connectionHandler;
        }

        public PositionData positions = new();
        public ImageData images = new();
        public DetectionData acquisitions = new();
        public int numDatasets = 0;

        public ImageData ProcessMessages(ChannelMessage[] messages) {
            var images_data = new ImageData();

            foreach (var message_data in messages) {
                images_data.ImageResponseType.Add(message_data.Message.Type);
                numReceivedData = message_data.Index;

                if (message_data.Message.Type == "smartprogramdecisionmessage") {
                    if (message_data.Message.Decision is null){
                        images_data.Decisions.Add(string.Empty);
                    }
                    else { 
                        images_data.Decisions.Add(message_data.Message.Decision); 
                    }
                }

                var msg = message_data.Message;

                if (msg.MetaData is not null && msg.Data is not null) {
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
