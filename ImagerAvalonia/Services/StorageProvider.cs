
using ImagerAvalonia.Services.MeasurementControl;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;

namespace ImagerAvalonia.Utils
{




    public static class OpenStorageIDS
    {
        public static List<int> OpenStorageIDSList = new();
        public static void CloseAllImageIDS()
        {
            foreach (int streamId in OpenStorageIDSList) {
                MISStorageProvider.CloseStream(streamId);
            }            
        }

    }

    public interface IStorageProvider
    {

        void SavePlanes(List<byte[]> data_buffer, List<TiffPlaneMetadata> metadata  );

        void SaveDecisions(List<string> decisions);

        byte[] ReadPlane(string acq_name, string det_name, int time_position);

        TiffPlaneMetadata GetPlaneMetadata(string acqName, string detName, int imageidx);

        string GetImagerProgram();

        int GetMaxNumberOfFrames();

        void SetMaxFrameNumber(int max_frames);
        
        void OpenWriteStream();

        void CloseReadWriteStream();

        void OpenReadStream();

        void SetStoragePath(string path);

        void SetMeasurementProgram(JObject measurementProgram);

        void SetAcqDetPairs(List<AcqDetPair> acqDetPair);

        List<AcqDetPair> GetStorageSchema();

        List<uint> GetPlaneSize();
        int GetImageIndex(string acqName, string detName, int requestedTime);
        int LoadMaxFrameNumber();
        bool SetEnabledStorage(bool isExperimentStorageEnabled);

        string _storagePath { get;  }

        JObject _measurementProgram { get; } 
    }





    public class MISStorageProvider : IStorageProvider
    {

        private const string DllName = "MeasurementImageStorageDLL";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISOpenFile(IntPtr outputFilePath, out int storerId);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISNewStorage(IntPtr outputFilePath, IntPtr measurementDescriptor, out int storerId);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISClose(long storerID);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISAddNewImage(
            long storerID,
            IntPtr acqTypeName,
            IntPtr detectorName,
            double timePoint,
            double stageX,
            double stageY,
            double stageZ,
            long detectionIndex,
            IntPtr stagePositionName,
            int imageType,
            int nRows,
            int nCols,
            byte* data);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISAddSmartProgramDecision(
            long storerID,
            IntPtr encodedSmartProgramDecision);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISGetNumberOfDetections(long storerID, IntPtr numDetections);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISGetAcquisitionNames(long storerID, IntPtr acqTypeNamesPtr, IntPtr nAcqTypes);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISGetDetectorNames(long storerID, IntPtr detectorNamesPtr, IntPtr nDetectors);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISGetNumberOfImages(long storerID, IntPtr acqTypeName, IntPtr detectorName, IntPtr nImages);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISGetImageIndex(
            long storerID,
            IntPtr acqTypeName,
            IntPtr detectorName,
            long detectionIndex,
            IntPtr imageIdxPtr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISGetImage(
            long storerID,
            IntPtr acqTypeName,
            IntPtr detectorName,
            int imageIdx,
            ushort** dataLocationPtr,
            ref int nRows,
            ref int nCols);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISReleaseImageData(ushort* dataPtr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISGetTimePoint(
            long storerID,
            IntPtr acqTypeName,
            IntPtr detectorName,
            int imageIdx,
            IntPtr timePoint);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISGetStagePosition(
            long storerID,
            IntPtr acqTypeName,
            IntPtr detectorName,
            int imageIdx,
            double* stageX,
            double* stageY,
            double* stageZ);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISGetDetectionIndex(
            long storerID,
            IntPtr acqTypeName,
            IntPtr detectorName,
            int imageIdx,
            IntPtr detectionIndex);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISGetStagePositionName(
            long storerID,
            IntPtr acqTypeName,
            IntPtr detectorName,
            int imageIdx,
            char** namePtr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISFreeStagePositionName(IntPtr name);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISGetImagerProgram(long storerID, char** programDescriptionPtr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISFreeProgramDescription(char* programDescriptionPtr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe int MISGetSmartProgramDecisions(
            long storerID,
            IntPtr encodedSmartProgramDecisionsPtr,
            IntPtr numberOfDecisionsPtr);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern unsafe void MISFreeStringArray(IntPtr array);


        public MISStorageProvider()
        {
     
        }


        static MISStorageProvider()
        {
            NativeLibrary.SetDllImportResolver(
                typeof(MISStorageProvider).Assembly,
                ResolveNativeLibrary);
        }

        private static IntPtr ResolveNativeLibrary(
            string libraryName,
            Assembly assembly,
            DllImportSearchPath? searchPath)
        {
            if (libraryName != DllName)
                return IntPtr.Zero;

            string baseDir = AppContext.BaseDirectory;
            string fullPath;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                fullPath = Path.Combine(baseDir, "MeasurementImageStorageDLL.dll");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                fullPath = Path.Combine(baseDir, "libMeasurementImageStorageDLL.so");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                fullPath = Path.Combine(baseDir, "libMeasurementImageStorageDLL.dylib");
            else
                return IntPtr.Zero;

            return NativeLibrary.Load(fullPath);
        }

        private bool _isStorageEnabled  = true;
        private int _readPlane;
        private int _storageId;


        public List<AcqDetPair> AcqDetPairs { get; set; }
        private Dictionary<Tuple<string, string>, int[]>? _storageSchema = new();

        private int _width;
        private int _height;


        public string _storagePath { get; private set; }
        public JObject _measurementProgram { get; set; }
        public List<TiffPlaneMetadata> metadata { get; set; }
        public int MaxFrames = 0;

        internal static void CloseStream(int  streamId)
        {
            MISClose(streamId);
        }

        public void CloseReadWriteStream()
        {
            if (_storagePath != null && _isStorageEnabled )
            {
                MISClose(_storageId);
            }
        }

        public bool SetEnabledStorage(bool isstorageenabled)
        {
            _isStorageEnabled = isstorageenabled;
            return isstorageenabled;
        }

        public int GetImageIndex(string acqName, string detName, int requestedTime)
        {
            unsafe
            {
                IntPtr acqTypeName = Marshal.StringToHGlobalAnsi(acqName);
                IntPtr detectorName = Marshal.StringToHGlobalAnsi(detName);
                IntPtr image_idx_pointer = Marshal.AllocHGlobal(sizeof(int));
                MISGetImageIndex(_storageId, acqTypeName, detectorName, requestedTime, image_idx_pointer);
                int imageIdx = Marshal.ReadInt32(image_idx_pointer);

                Marshal.FreeHGlobal(detectorName);
                Marshal.FreeHGlobal(acqTypeName);
                Marshal.FreeHGlobal(image_idx_pointer);

                return imageIdx;
            }
        }

        public TiffPlaneMetadata GetPlaneMetadata(string acqName, string detName, int imageidx)
        {
            TiffPlaneMetadata metadata = new TiffPlaneMetadata();

            if (AcqDetPairs.Contains(new AcqDetPair(acqName, detName)))
            {
                IntPtr acqTypeName = Marshal.StringToHGlobalAnsi(acqName);
                IntPtr detectorName = Marshal.StringToHGlobalAnsi(detName);
                unsafe
                {
                    double[] posx = new double[1];
                    double[] posy = new double[1];
                    double[] posz = new double[1];
                    char* position_name_ptr;

                    fixed (double* posx_ptr = posx, posy_ptr = posy, posz_ptr = posz)
                    {
                        MISGetStagePosition(_storageId, acqTypeName, detectorName, imageidx, posx_ptr, posy_ptr, posz_ptr);
                        MISGetStagePositionName(_storageId, acqTypeName, detectorName, imageidx, &position_name_ptr);
                        metadata.PositionX = posx[0];
                        metadata.PositionY = posy[0];
                        metadata.PositionZ = posz[0];
                        metadata.AcquisitionName = acqName;
                        metadata.DetectorName = detName;
                        metadata.PositionName = Marshal.PtrToStringAnsi((IntPtr)position_name_ptr);
                        metadata.Width = (uint)_width;
                        metadata.Height = (uint)_height;
                        metadata.CurrentStagePosition = new XYStagePosition((float)metadata.PositionX, (float)metadata.PositionY, (float)metadata.PositionZ,
                            false, 0, metadata.PositionName);

                        MISFreeStagePositionName((IntPtr)position_name_ptr);

                    }
                }
                Marshal.FreeHGlobal(acqTypeName);
                Marshal.FreeHGlobal(detectorName);
                return metadata;

            }
            else
            {
                throw new Exception("Acquisition/Detection pair not present in the dataset");
            }
        }

        public List<uint> GetPlaneSize()
        {
          
            return new List<uint>() { (uint)_width, (uint)_height };
        }

        public void OpenReadStream()
        {
            if (_storagePath != null && _isStorageEnabled)
            {

                IntPtr input_path_ptr = Marshal.StringToHGlobalAnsi(_storagePath);
                int storageIdPtr;

                MISOpenFile(input_path_ptr, out storageIdPtr);
                _storageId = storageIdPtr;
                OpenStorageIDS.OpenStorageIDSList.Add(_storageId);
            }
        }

        public string? GetImagerProgram()
        {
            unsafe
            {
                char* imager_program_ptr;

                MISGetImagerProgram(_storageId, &imager_program_ptr);

                string imager_program = Marshal.PtrToStringAnsi((IntPtr)imager_program_ptr);

                MISFreeProgramDescription(imager_program_ptr);

                return imager_program;

            }

        }

        public void OpenWriteStream()
        {
            if (_storagePath != null && _isStorageEnabled)
            {
                string measurement_program = _measurementProgram.ToString(Newtonsoft.Json.Formatting.None);
                IntPtr measurement_descriptor_ptr = Marshal.StringToHGlobalAnsi(measurement_program);
                IntPtr storage_path_ptr = Marshal.StringToHGlobalAnsi(_storagePath);
                int storerId;

                MISNewStorage(storage_path_ptr, measurement_descriptor_ptr, out storerId);
                _storageId = (int)storerId;
                OpenStorageIDS.OpenStorageIDSList.Add(_storageId);

                Marshal.FreeHGlobal(measurement_descriptor_ptr);
                Marshal.FreeHGlobal(storage_path_ptr);
            }
        }

     

        public byte[] ReadPlane(string acq_name, string det_name, int time_position)
        {
            unsafe
            {
                if (_isStorageEnabled)
                {
                    IntPtr acqTypeName = Marshal.StringToHGlobalAnsi(acq_name);
                    IntPtr detectorName = Marshal.StringToHGlobalAnsi(det_name);

                    if (time_position != -1)
                    {

                        ushort* data_buffer;


                        MISGetImage(_storageId, acqTypeName, detectorName, time_position, &data_buffer, ref _width, ref _height);

                        byte[] byteArray = new byte[_width * _height * 2];
                        fixed (byte* dest = byteArray)
                        {
                            byte* d = dest;
                            if (data_buffer != null)
                            {
                                for (int buf_ind = 0; buf_ind < _width * _height; buf_ind++)
                                {
                                    *(d++) = (byte)(*data_buffer);
                                    *(d++) = (byte)(*data_buffer >> 8);
                                    data_buffer++;

                                }
                            }
                            MISReleaseImageData(data_buffer - _width * _height);
                            return byteArray;
                        }
                    }
                }

                return Array.Empty<byte>();

            }

        }

        public void SavePlanes(List<byte[]> data_buffer, List<TiffPlaneMetadata> metadata)
        {

            for (int buf_ind = 0; buf_ind < data_buffer.Count; buf_ind++)
            {
                if (_storagePath != null   && _isStorageEnabled)
                {
                    byte[] frame_data = data_buffer[buf_ind];
                    //ushort[] frame_data = new ushort[data_buffer[buf_ind].Length / 2];
                    //Buffer.BlockCopy(data_buffer[buf_ind], 0, frame_data, 0, data_buffer[buf_ind].Length);


                    IntPtr acqTypeName = Marshal.StringToHGlobalAnsi(metadata[buf_ind].AcquisitionName);
                    IntPtr detectorName = Marshal.StringToHGlobalAnsi(metadata[buf_ind].DetectorName);
                    IntPtr posName = Marshal.StringToHGlobalAnsi(metadata[buf_ind].PositionName);

                    long detectionIdx = metadata[buf_ind].DetectionIndex;
                    unsafe
                    {
                        fixed (byte* data_buf_ptr = frame_data)
                        {
                            System.Diagnostics.Debug.WriteLine(detectionIdx);
                            MISAddNewImage(_storageId, acqTypeName, detectorName, metadata[buf_ind].TimePoint, metadata[buf_ind].PositionX, metadata[buf_ind].PositionY, metadata[buf_ind].PositionZ,
                                           detectionIdx, posName,metadata[buf_ind].Type, (int)metadata[buf_ind].Width, (int)metadata[buf_ind].Height, data_buf_ptr);
                        }
                    }
                }
            }
        }

        public void SaveDecisions(List<string> decisions)
        {
            foreach(var decision in decisions)
            {
                IntPtr decisionPtr = Marshal.StringToHGlobalAnsi(decision);
                MISAddSmartProgramDecision(_storageId, decisionPtr);
            }
        }

        public int LoadMaxFrameNumber()
        {
            IntPtr num_detections = Marshal.AllocHGlobal(sizeof(int));
            MISGetNumberOfDetections(_storageId, num_detections);
            return Marshal.ReadInt32(num_detections);
        }

        public void SetStoragePath(string path)
        {
            _storagePath = path;
        }

        public void SetMeasurementProgram(JObject measurementProgram)
        { 
            _measurementProgram = measurementProgram; 
        }

        public int GetMaxNumberOfFrames()
        {
            return MaxFrames; 
        }

        public void SetMaxFrameNumber(int max_frames)
        {
            MaxFrames = max_frames;
        }

        public void SetAcqDetPairs( List<AcqDetPair> acqDetPairs)
        {
            AcqDetPairs = acqDetPairs;
        }

        public List<AcqDetPair> GetStorageSchema()
        {
            return AcqDetPairs;
        }
    }









    public class TiffPlaneMetadata
    {
        public uint Width;
        public uint Height;
        public int DetectionIndex;

        public string AcquisitionName = string.Empty;
        public string DetectorName = string.Empty;
        public double TimePoint;


        public int Type; 
        public double PositionX;
        public double PositionY;
        public double PositionZ;
        public string? PositionName;
        public string ElementID = string.Empty;

  
        public XYStagePosition CurrentStagePosition = IStageControl.DefaultXYStagePosition;

    }


    
}
