using DynamicData;
using ImagerAvalonia.Services.MeasurementControl;
using Microsoft.Extensions.Logging;
using ScottPlot;
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

        private readonly ComUtils? _comUtils;

        private Channel<ImageData> _imageReader = Channel.CreateUnbounded<ImageData>();

        public bool ShowLiveView = true;
        private System.Timers.Timer _throttleTimer = new System.Timers.Timer(10) { AutoReset = false };
        private bool _canFire = true;


        public ImageHandler(IStorageProvider storageProvider)
        {
            _storageProvider = storageProvider; 
        }

        public ImageHandler(IStorageProvider storageProvider, ILogger logger, ComUtils comUtils )
        {
            _storageProvider = storageProvider;
            _logger = logger;
            _acquisitionHandler = new MessagePackAcquisitionHandler(storageProvider, comUtils);
            _comUtils = comUtils;
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


        public ImageData RequestImageMetadata(List<AcqDetPair> AcqDetPairs, int RequestedTime)
        {
            ImageData imageData = new ImageData();
            imageData.Metadata = new List<TiffPlaneMetadata>() { };
            foreach (AcqDetPair acq_det_pair in AcqDetPairs)
            {
                //WIP
            }
            return imageData;
        }


        public ImageData RequestImage(List<AcqDetPair> AcqDetPairs, int RequestedTime)
        {
            ImageData imageData = new ImageData();

            imageData.Images = new List<byte[]> { };
            imageData.Metadata = new List<TiffPlaneMetadata>() { };
            imageData.Sizes = new List<List<uint>>() { };
            int num_datasets = 0;
            foreach (AcqDetPair acq_det_pair in AcqDetPairs)
            {
                int image_idx = _storageProvider.GetImageIndex(acq_det_pair.acqName, acq_det_pair.detName, RequestedTime);
                byte[] image_data = _storageProvider.ReadPlane(acq_det_pair.acqName, acq_det_pair.detName, image_idx);
                if (image_data.Length > 0)
                {
                    num_datasets += 1;
                    imageData.Images.Add(image_data);
                    var plane_metadata = _storageProvider.GetPlaneMetadata(acq_det_pair.acqName, acq_det_pair.detName, image_idx);
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


        public async Task<bool> ParseProgramAndShowData( CancellationTokenSource src)
        {


            CancellationTokenSource source = src;


            Task _Enable_live_view = new Task(() =>
            {
                if (_acquisitionHandler is not null)
                {
                    _acquisitionHandler.StartAcquisition();


                    while (_acquisitionHandler.state == AcquisitionState.Running)
                    {

                        var images = _acquisitionHandler.FetchData(src);

                        if (_acquisitionHandler.IsNewDataAvailable)
                        {

                            Task.Run(() => NewImageDataAvailable(images, ShowLiveView));

                            if (_comUtils is not null)
                            {
                                _comUtils.SendDataRequest(ComUtils.fetchasyncstatus, "", response_message => { _logger?.LogInformation(response_message); }, response_data => { });
                            }

                            _acquisitionHandler.IsNewDataAvailable = false;
                        }
                    }
                }
            }
            , source.Token);


            _Enable_live_view.Start();
            await _Enable_live_view;

            

            return _Enable_live_view.IsCompleted;
            
        }       

        
    }
    public class OnDetectionRequestedEventArgs : EventArgs
    {
        public List<AcqDetPair> AcqDetPairs  { get; private set; }
        //public Dictionary<AcqDetPair, XYStagePosition> DetectionPositions { get; }

        public int RequestedTime { get; }


        public OnDetectionRequestedEventArgs(List<AcqDetPair> acqDetPairs , int requestedTime)
        {
            AcqDetPairs = acqDetPairs;
            //DetectionPositions = detectionPositions ?? throw new ArgumentNullException(nameof(detectionPositions));
            RequestedTime = requestedTime;
        }
    }
}
