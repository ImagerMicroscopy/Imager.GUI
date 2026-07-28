using ImagerAvalonia.Services;
using ImagerAvalonia.Services.ImagerModels.MeasurementElementsModels;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Services.Workspace.SmartProgramWorkspace;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ImagerAvalonia.Utils
{
    public class ImageHandler
    {


        private readonly IStorageProvider   _storageProvider;
       
        private readonly ILogger? _logger; 

        private readonly MessagePackAcquisitionHandler? _acquisitionHandler;

        private readonly IImagerConnectionHandler _connectionHandler;

        private Channel<ImageData> _imageReader = Channel.CreateUnbounded<ImageData>();

        public bool ShowLiveView = true;
        private System.Timers.Timer _throttleTimer = new System.Timers.Timer(10) { AutoReset = false };
        private bool _canFire = true;


        public ImageHandler(IStorageProvider storageProvider)
        {
            _storageProvider = storageProvider; 
        }

        public ImageHandler(IStorageProvider storageProvider, ILogger logger, IImagerConnectionHandler connectionHandler )
        {
            _storageProvider = storageProvider;
            _logger = logger;
            _acquisitionHandler = new MessagePackAcquisitionHandler(storageProvider, connectionHandler);
            _connectionHandler = connectionHandler;
            _throttleTimer.Elapsed += (_, _) => _canFire = true;
        }

        public event EventHandler<ImageData>? UpdateImageDisplay;
        public event EventHandler<ImageData>? UpdateFieldViewDisplay;
        public event EventHandler<ImageData>? UpdateAsyncProgress;
        public event EventHandler<ImageData>? UpdateCurrentPositions;
        public event EventHandler<ImageData>? UpdateImageElements;



        public void NewLoadedImageDataAvailable(ImageData imageData)
        {
            UpdateImageDisplay?.Invoke(this, imageData);
            UpdateCurrentPositions?.Invoke(this, imageData);

        }



        public void NewImageDataAvailable(ImageData imageData, bool ShowLive)
        {
            if (imageData.Images.Count == 0) return;
            UpdateCurrentPositions?.Invoke(this, imageData);
            UpdateAsyncProgress?.Invoke(this, imageData);
            UpdateImageElements?.Invoke(this, imageData);
            UpdateFieldViewDisplay?.Invoke(this, imageData);


            if (ShowLive && _canFire)
            {
                _canFire = false;
                _throttleTimer.Start();
                UpdateImageDisplay?.Invoke(this, imageData);
            }
        }


        public void EnableDisableLiveView(object? sender, EventArgs e)
        {
            ShowLiveView = !ShowLiveView;
        }


        public ImageData RequestImageMetadata(List<Tuple<string, string>> AcqDetPairs, int RequestedTime)
        {
            ImageData imageData = new ImageData();
            imageData.Metadata = new List<TiffPlaneMetadata>() { };
            foreach (var acq_det_pair in AcqDetPairs)
            {
                //WIP
            }
            return imageData;
        }


        public ImageData RequestImage(List<Tuple<string, string>> AcqDetPairs, int RequestedTime)
        {
            ImageData imageData = new ImageData();

            imageData.Images = new List<byte[]> { };
            imageData.Metadata = new List<TiffPlaneMetadata>() { };
            imageData.Sizes = new List<List<uint>>() { };
            int num_datasets = 0;
            foreach (var acq_det_pair in AcqDetPairs)
            {
                int image_idx = _storageProvider.GetImageIndex(acq_det_pair.Item1, acq_det_pair.Item2, RequestedTime);
                byte[] image_data = _storageProvider.ReadPlane(acq_det_pair.Item1, acq_det_pair.Item2, image_idx);
                if (image_data.Length > 0)
                {
                    num_datasets += 1;
                    imageData.Images.Add(image_data);
                    var plane_metadata = _storageProvider.GetPlaneMetadata(acq_det_pair.Item1, acq_det_pair.Item2, image_idx);
                    imageData.Metadata.Add(plane_metadata);
                    imageData.Sizes.Add(_storageProvider.GetPlaneSize());
                    imageData.TraversedPositions.Add(plane_metadata.CurrentStagePosition);
                }
            }
            return imageData;

        }


        public void LoadImage(object? sender,OnDetectionRequestedEventArgs e )
        {
                ImageData imageData = RequestImage(e.AcqDetPairs, e.RequestedTime);
            
                NewLoadedImageDataAvailable(imageData);
        }

        public async Task ImageReader(CancellationToken token)
        {
            while (await _imageReader.Reader.WaitToReadAsync(token))
            {
                if(token.IsCancellationRequested)
                {
                    return ;
                }
                while (_imageReader.Reader.TryRead(out ImageData? acquiredData))
                {                  
                    NewImageDataAvailable(acquiredData, ShowLiveView);
                }         
            }
        }


        public async Task<bool> ParseProgramAndShowData( CancellationTokenSource src, 
            MeasurementProgram program,
            SmartProgramRegistry smartPrograms){
            try {
                

                await Task.Run(async () => {

                    var request = new ExecuteMeasurementProgramRequest(
                        JObject.FromObject(program.Program, Newtonsoft.Json.JsonSerializer.Create(MeasurementSerializer.Settings)),
                        JObject.FromObject(program.Detections , Newtonsoft.Json.JsonSerializer.Create(DetectionEquipmentSerializer.Settings)),
                        smartPrograms.SerializeAllDags()
                    );

                    if (request != null) {
                      

                        var channel = Channel.CreateUnbounded<MeasurementEvent>();
                        ImagerCommunicationManager.Instance.ExecuteMeasurementProgram(request, channel.Writer, src.Token);
  

                        _storageProvider.OpenWriteStream();

                        await foreach (var measurementEvent in channel.Reader.ReadAllAsync(src.Token)) {
                            switch (measurementEvent) {
                                case MeasurementDataEvent dataEvent:
                                    var images = _acquisitionHandler.ProcessMessages(dataEvent.Messages);
                                    if (images.Images.Count > 0) {
                                        _storageProvider.SavePlanes(images.Images, images.Metadata);
                                        var _ = Task.Run(() => NewImageDataAvailable(images, ShowLiveView));
                                    }
                                    break;

                                case MeasurementStatusTextEvent statusEvent:
                                    foreach (var msg in statusEvent.Messages) {
                                        _logger.LogInformation(msg);
                                    }
                                    break;

                                case MeasurementErrorEvent errorEvent:
                                    _logger.LogError($"Measurement Error: {errorEvent.Error}");
                                    break;

                                case MeasurementCompletedEvent _:
                                    return; // End of stream
                            }
                        }
                    }
                }, src.Token);

                return true;
            } catch (OperationCanceledException) {
                // Task was cancelled, this is expected when stopping live acquisition
                _logger.LogInformation("Live acquisition was cancelled by user");
                return false;
            }
        }       

        
    }
    public class OnDetectionRequestedEventArgs : EventArgs
    {
        public List<Tuple<string, string>> AcqDetPairs  { get; private set; }
        //public Dictionary<AcqDetPair, StagePosition> DetectionPositions { get; }

        public int RequestedTime { get; }


        public OnDetectionRequestedEventArgs(List<Tuple<string, string>> acqDetPairs , int requestedTime)
        {
            AcqDetPairs = acqDetPairs;
            //DetectionPositions = detectionPositions ?? throw new ArgumentNullException(nameof(detectionPositions));
            RequestedTime = requestedTime;
        }
    }
}


