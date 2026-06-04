using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Exceptions;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels
{
    internal partial class DataSplitterViewModel : ViewModelBase
    {
        [ObservableProperty] string? _outputFolder;

        [ObservableProperty] bool _splitDataByAcquisition;
        [ObservableProperty] bool _splitDataByCamera;
        [ObservableProperty] bool _splitDataByPosition;

        private Utils.IStorageProvider _storageProvider;

        public DataSplitterViewModel(Utils.IStorageProvider storageProvider)
        {
            this._storageProvider = storageProvider;
        }

        public async Task GetMetaDataAsync()
        {
            await Task.Run(() =>
            {
                var imagerprogram = _storageProvider.GetImagerProgram();
                JObject program = JObject.Parse(imagerprogram);
                List<AcqDetPair> acqDetPairs = new();

                if (program.TryGetValue("program", out JToken? imager_program) && imager_program is not null)
                {
                    var acquistions = EquipmentState.GetAcquisitionsFromImagerProgram(imager_program);


                    foreach(var acquisition in acquistions)
                    {
                        foreach(var detector in acquisition.Detector)
                        {
                            var acqDetPair = new AcqDetPair(acquisition, detector.Detectorname);
                            if(!acqDetPairs.Contains(acqDetPair))
                            {
                                acqDetPairs.Add(acqDetPair);
                            }
}
                    }
                }
                _storageProvider.SetAcqDetPairs(acqDetPairs);
                
                int num_detections = _storageProvider.LoadMaxFrameNumber();
                List<TiffPlaneMetadata> tiffPlanes = new();
                for (int det = 0; det < num_detections; det++)
                {
                    foreach(var acqDetPair in acqDetPairs)
                    {
                        if(det==0)
                        {
                            _storageProvider.ReadPlane(acqDetPair.acqName, acqDetPair.detName, 0); // sets the _width, _height, by reading the first plane.
                        }
                        int imindex = _storageProvider.GetImageIndex(acqDetPair.acqName, acqDetPair.detName, det);
                        tiffPlanes.Add(_storageProvider.GetPlaneMetadata(acqDetPair.acqName, acqDetPair.detName, imindex));
                    }
                }
                tiffPlanes = tiffPlanes
                .OrderBy(x => x.TimePoint)
                .ToList();
            });
        }

        public async Task SplitData()
        {

        }
    }
}
