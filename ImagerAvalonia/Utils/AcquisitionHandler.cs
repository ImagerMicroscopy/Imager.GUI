// Required namespaces for measurement control, JSON handling, threading, and collections

using ImagerAvalonia.Services.MeasurementControl;
using MessagePack;
using Newtonsoft.Json.Linq;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;


namespace ImagerAvalonia.Utils
{

    public interface IAcquisitionHandler
    {
        public void ProcessIncomingData(byte[] receivedData);
    }

    // Stores X, Y, Z positional data
    public class PositionData
    {
        public List<double> XPositions { get; } = new();
        public List<double> YPositions { get; } = new();
        public List<double> ZPositions { get; } = new();
    }

    // Stores image byte data, size info, and metadata for each image plane
    public class ImageData
    {
        public List<byte[]> Images { get; set; } = new();
        public List<List<uint>> Sizes = new List<List<uint>>();
        public List<TiffPlaneMetadata> Metadata = new List<TiffPlaneMetadata>();
        public List<string> Decisions = new List<string>();
        public List<XYStagePosition> TraversedPositions = new();
        public string? Payload { get; set; }
        public List<string> ImageResponseType = new();
        public string? Comment { get; internal set; }

        public ImageData this[int index]
        {
            get
            {
                return new ImageData
                {
                    Images = new List<byte[]> { Images[index] },
                    Sizes = new List<List<uint>> { Sizes[index] },
                    Metadata = new List<TiffPlaneMetadata> { Metadata[index] },
                };
            }
        }
    }

    // Stores acquisition metadata including detector and acquisition names and timestamps
    public class DetectionData
    {
        public List<string> DetectorNames { get; set; } = new();
        public List<string> AcquisitionNames { get; set; } = new();
        public List<double> Timestamps { get; set; } = new();
    }

    // Represents the state of acquisition
    public enum AcquisitionState
    {
        Running,
        Completed,
        Canceled
    }

    public class ImagerData
    {
        public ImageData AcquiredImages { get; set; } = new();
        public PositionData AcquiredPositionData { get; set; } = new();
        public DetectionData AcquiredDetectionData { get; set; } = new();
    }



    public class MessagePackAcquisitionHandler
    {

        private readonly IStorageProvider _storageProvider;
        private readonly ComUtils _comUtils;

        public List<TiffPlaneMetadata> receivedMetaData = new();
        private int numReceivedData = -1;
        public AcquisitionState state = AcquisitionState.Running;

        public bool IsNewDataAvailable = false;


        public MessagePackAcquisitionHandler(IStorageProvider storageProvider, ComUtils comUtils)
        {
            _storageProvider = storageProvider;
            _comUtils = comUtils;

        }

        public PositionData positions = new();
        public ImageData images = new();
        public DetectionData acquisitions = new();
        public int numDatasets = 0;

        public void StartAcquisition()
        {
            JObject program = _storageProvider._measurementProgram;
            var parsed_program = program["program"];
            if (parsed_program != null)
            {
                _comUtils.SendDataRequest(parsed_program.ToString(Newtonsoft.Json.Formatting.None), ComUtils.isokresponse, _ => { }, _ => { });
            }
            _storageProvider.OpenWriteStream();
        }

        public ImageData FetchData(CancellationTokenSource src)
        {
            string response = "";
            var image_data = new ImageData();

            Console.WriteLine($"[{DateTime.Now}] Requesting data image");

            // Send fetch request and process response
            _comUtils.SendDataRequest(ComUtils.fetchmessage, "", response_message => { response = response_message; }, receivedData => {
                image_data = ProcessIncomingData(receivedData);
                Console.WriteLine($"[{DateTime.Now}] Received data: number of images = {image_data.Images.Count}, Number of decisions: {image_data.Decisions.Count}, Received data: {numReceivedData}");

                if (image_data.Images.Count > 0)
                {
                    _storageProvider.SavePlanes(image_data.Images, image_data.Metadata);
                }
                if (image_data.Decisions.Count > 0)
                {
                    _storageProvider.SaveDecisions(image_data.Decisions);
                }
                AcknowledgeData(numReceivedData);
                IsNewDataAvailable = true;

            });

            // Handle special cases in response string
            if (!String.IsNullOrEmpty(response))
            {
                if (response.Contains(ComUtils.no_more_spectra))
                {
                    state = AcquisitionState.Completed;
                    IsNewDataAvailable = true;
                    return new ImageData();
                }
                if (response.Contains("error"))
                {
                    state = AcquisitionState.Completed;
                    throw new AcquisitionException($"User error {response}");
                }
            }

            // If cancellation is requested, send cancel message
            if (src.IsCancellationRequested)
            {
             
                state = AcquisitionState.Canceled;
                _comUtils.SendDataRequest(ComUtils.cancelacquisition, "", _ => { }, _ => { });
            }
            return image_data;
        }

        public ImageData ProcessIncomingData(byte[] receivedData)
        {

            var images = new ImageData();
        

            var reader = new MessagePackReader(new ReadOnlySequence<byte>(receivedData));

            while (reader.Consumed <= receivedData.Length - 5)
            {

                MessagePackData message_data = MessagePackSerializer.Deserialize<MessagePackData>(ref reader);
                images.ImageResponseType.Add(message_data.message.type);
                numReceivedData = message_data.index;

                if (message_data.message.type == "smartprogramdecisionmessage")
                {
                    if (message_data.message.decision is null)
                    {
                        images.Decisions.Add(string.Empty);
                    }
                    else
                    { images.Decisions.Add(message_data.message.decision); }
                }

                var data = message_data.message;


                if (data.metadata is not null)
                {

                    var tiffMeta = new TiffPlaneMetadata
                    {

                        PositionX = data.metadata.stageposition.x,
                        PositionY = data.metadata.stageposition.y,
                        PositionZ = data.metadata.stageposition.z,
                        AcquisitionName = data.metadata.acquisitiontype,
                        DetectorName = data.data.detectorname,
                        Width = (uint)data.data.ncols,
                        Height = (uint)data.data.nrows,
                        TimePoint = data.data.timestamp,
                        DetectionIndex = data.metadata.detectionindex,
                        PositionName = data.metadata.stagepositionname,
                        ElementID = data.metadata.detectionelementid,
                        CurrentStagePosition = new XYStagePosition(data.metadata.stageposition.x,
                        data.metadata.stageposition.y, data.metadata.stageposition.z, data.metadata.stageposition.usinghardwareautofocus,
                        data.metadata.stageposition.hardwareautofocusoffset, data.metadata.stagepositionname ?? string.Empty)

                    };


                    images.ImageResponseType.Add(message_data.message.type);
                    images.Metadata.Add(tiffMeta);
                    images.Images.Add(data.data.imagedata);
                    images.Sizes.Add(new List<uint>() { (uint)data.data.ncols, (uint)data.data.nrows });
                }
            }

            return images;
        }

        // Sends an acknowledgment for received data
        public void AcknowledgeData(int total_data_length)
        {
             _comUtils.SendDataRequest(ComUtils.acknowledgemessage(total_data_length), "", _ => { }, _ => { });
        }
    }








    // Main handler for managing acquisition process
    public class AcquisitionHandler 
    {
        private readonly IStorageProvider _storageProvider;
        private readonly ComUtils _comUtils;

        public List<TiffPlaneMetadata> receivedMetaData = new();
        private int numReceivedData = -1;
        public AcquisitionState state = AcquisitionState.Running;

        public bool IsNewDataAvailable = false;

        // Constants for parsing incoming byte data
        const int MAGIC_NUMBER_SIZE = 2;
        const int MESSAGE_SIZE_BYTES = 4;
        const int NUM_TYPE_BYTES = 1;
        const int NUM_DATASETS_BYTES = 4;


        public AcquisitionHandler(IStorageProvider storageProvider, ComUtils comUtils)
        {
            _storageProvider = storageProvider;
            _comUtils = comUtils;
        }

        // Data containers for current acquisition
        public PositionData positions = new();
        public ImageData images = new();
        public DetectionData acquisitions = new();
        public int numDatasets = 0;

        // Starts the acquisition by sending a program request and opening the storage stream
        public void StartAcquisition()
        {
            JObject program = _storageProvider._measurementProgram;
            var parsed_program = program["program"];
            if (parsed_program != null)
            {
                _comUtils.SendDataRequest(parsed_program.ToString(Newtonsoft.Json.Formatting.None), ComUtils.isokresponse, _ => { }, _ => { });
            }
            _storageProvider.OpenWriteStream();
        }

        // Fetches data, handles incoming response and state transitions, and saves the data
        public ImageData FetchData(CancellationTokenSource src)
        {
            string response = "";
            var s = new System.Diagnostics.Stopwatch();
            var image_data = new ImageData();

            // Send fetch request and process response
            _comUtils.SendDataRequest(ComUtils.fetchmessage, "", response_message => { response = response_message; }, receivedData => {
                image_data = ProcessIncomingData(receivedData);
                AcknowledgeData(numReceivedData);
                IsNewDataAvailable = true;

                // Save image data and update stored frame counts
                _storageProvider.SavePlanes(images.Images, images.Metadata);
            });

            // Handle special cases in response string
            if (!String.IsNullOrEmpty(response))
            {
                if (response.Contains(ComUtils.no_more_spectra))
                {
                    state = AcquisitionState.Completed;
                    IsNewDataAvailable = false;
                    return image_data;
                }
                if (response.Contains("error"))
                {
                    state = AcquisitionState.Completed;
                    throw new AcquisitionException($"User error {response}");
                }
            }

            // If cancellation is requested, send cancel message
            if (src.IsCancellationRequested)
            {
                state = AcquisitionState.Canceled;
                _comUtils.SendDataRequest(ComUtils.cancelacquisition, "", _ => { }, _ => { });
            }
            return image_data;
        }

        // Parses incoming byte data into structured acquisition info
        public ImageData ProcessIncomingData(byte[] receivedData)
        {
            // Reset data containers
            positions = new();
            images = new();
            acquisitions = new();

            // Local helper to copy bytes
            void CopyBytes(byte[] source, int startIndex, byte[] destination)
            {
                Array.Copy(source, startIndex, destination, 0, destination.Length);
            }

            // Buffers for parsing header values
            byte[] magicNumber = new byte[MAGIC_NUMBER_SIZE];
            byte[] totalMessageSize = new byte[MESSAGE_SIZE_BYTES];
            byte[] numType = new byte[NUM_TYPE_BYTES];
            byte[] numDatasetsArray = new byte[NUM_DATASETS_BYTES];

            // Copy header data
            CopyBytes(receivedData, 0, magicNumber);
            CopyBytes(receivedData, MAGIC_NUMBER_SIZE, totalMessageSize);
            CopyBytes(receivedData, MAGIC_NUMBER_SIZE + MESSAGE_SIZE_BYTES, numType);
            CopyBytes(receivedData, MAGIC_NUMBER_SIZE + MESSAGE_SIZE_BYTES + NUM_TYPE_BYTES, numDatasetsArray);

            int startIndex = MAGIC_NUMBER_SIZE + MESSAGE_SIZE_BYTES + NUM_TYPE_BYTES + NUM_DATASETS_BYTES;

            // Read number of datasets and parse the rest of the data
            numDatasets = BitConverter.ToInt32(numDatasetsArray);
            List<ulong> dataset_index = ImagerCommunication.FetchUInt64ParameterFromBytesAtOffset(numDatasets, receivedData, ref startIndex);
            receivedMetaData = new List<TiffPlaneMetadata>();

            // Parse and store positional metadata for each dataset
            for (int i = 0; i < numDatasets; i++)
            {
                TiffPlaneMetadata tiffPlaneMetadata = new TiffPlaneMetadata
                {
                    PositionX = BitConverter.ToDouble(receivedData, startIndex),
                    PositionY = BitConverter.ToDouble(receivedData, startIndex + 8),
                    PositionZ = BitConverter.ToDouble(receivedData, startIndex + 16)
                };

                images.Metadata.Add(tiffPlaneMetadata);
                startIndex += 24;
            }

            // Extract remaining metadata and image data
            acquisitions.AcquisitionNames = ImagerCommunication.FetchAcquisitionOrDetectorName(numDatasets, receivedData, ref startIndex);
            acquisitions.DetectorNames = ImagerCommunication.FetchAcquisitionOrDetectorName(numDatasets, receivedData, ref startIndex);
            images.Sizes = ImagerCommunication.FetchUInt32ParameterFromBytesAtOffset(numDatasets, receivedData, ref startIndex);
            acquisitions.Timestamps = ImagerCommunication.FetchFloat64ParameterFromBytesAtOffset(numDatasets, receivedData, ref startIndex);
            images.Images = ImagerCommunication.FetchImageData(numDatasets, receivedData, ref startIndex, images.Sizes, new byte[1]);

            // Populate full metadata structure
            for (int i = 0; i < numDatasets; i++)
            {
                images.Metadata[i].AcquisitionName = acquisitions.AcquisitionNames[i];
                images.Metadata[i].DetectorName = acquisitions.DetectorNames[i];
                images.Metadata[i].Width = images.Sizes[i][0];
                images.Metadata[i].Height = images.Sizes[i][1];
                images.Metadata[i].TimePoint = acquisitions.Timestamps[i];
                //Console.WriteLine(acquisitions.Timestamps[i]);


            }

            // Track how many datasets have been received
            numReceivedData += numDatasets;
            return images;
        }

        // Sends an acknowledgment for received data
        public void AcknowledgeData(int total_data_length)
        {
            _comUtils.SendDataRequest(ComUtils.acknowledgemessage(total_data_length), "", _ => { }, _ => { });
        }
    }

    // Custom exception for acquisition errors
    public class AcquisitionException : Exception
    {
        private string _message;

        // Custom message formatting
        public override string Message => $"Exception occured during acquisition run: {_message}";

        public AcquisitionException(string message) : base(message)
        {
            _message = message;
        }
    }
}
