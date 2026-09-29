
using ImagerAvalonia.Services.MeasurementControl;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;

namespace ImagerAvalonia.Services.Storage
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

        /// <summary>Smart program decisions stored in the open file, as JSON strings.</summary>
        List<string> GetSmartProgramDecisions();

        byte[] ReadPlane(string acq_name, string det_name, int time_position);

        TiffPlaneMetadata GetPlaneMetadata(string acqName, string detName, int imageidx);

        string GetImagerProgram();

        int GetMaxNumberOfFrames();

        int GetNumberOfImages(string acqname, string detname);

        void SetMaxFrameNumber(int max_frames);
        
        void OpenWriteStream();

        void CloseReadWriteStream();

        void OpenReadStream();

        void SetStoragePath(string path);

        void SetMeasurementProgram(string measurementProgramJson);

        void SetAcqDetPairs(List<Tuple<string,string>> acqDetPair);

        List<Tuple<string, string>> GetStorageSchema();

        List<uint> GetPlaneSize();
        int GetImageIndex(string acqName, string detName, int requestedTime);
        int LoadMaxFrameNumber();
        bool SetEnabledStorage(bool isExperimentStorageEnabled);
        int GetOpenID();
        void SetOpenID(int id);

        string _storagePath { get;  }

        string _measurementProgram { get; }
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
            double* timePoint);

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


        public List<Tuple<string, string>> AcqDetPairs { get; set; }
        private Dictionary<Tuple<string, string>, int[]>? _storageSchema = new();

        private int _width;
        private int _height;


        public string _storagePath { get; private set; }
        public string _measurementProgram { get; set; }
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

        public int GetOpenID()
        {
            return _storageId; 
        }

        public void SetOpenID(int id)
        {
            _storageId = id;
        }

        public bool SetEnabledStorage(bool isstorageenabled)
        {
            _isStorageEnabled = isstorageenabled;
            return isstorageenabled;
        }

        public int GetImageIndex(string acqName, string detName, int requestedTime)
        {
            IntPtr acqTypeName = Marshal.StringToHGlobalAnsi(acqName);
            IntPtr detectorName = Marshal.StringToHGlobalAnsi(detName);
            try
            {
                int imageIdx = -1;
                unsafe
                {
                    // An unknown channel is an error in the library; treat it as "no image yet".
                    if (MISGetImageIndex(_storageId, acqTypeName, detectorName, requestedTime, (IntPtr)(&imageIdx)) != 0)
                        return -1;
                }
                return imageIdx;
            }
            finally
            {
                Marshal.FreeHGlobal(detectorName);
                Marshal.FreeHGlobal(acqTypeName);
            }
        }

        public TiffPlaneMetadata GetPlaneMetadata(string acqName, string detName, int imageidx)
        {
            if (!AcqDetPairs.Contains(new Tuple<string,string>(acqName, detName)))
                throw new Exception("Acquisition/Detection pair not present in the dataset");

            TiffPlaneMetadata metadata = new TiffPlaneMetadata();
            IntPtr acqTypeName = Marshal.StringToHGlobalAnsi(acqName);
            IntPtr detectorName = Marshal.StringToHGlobalAnsi(detName);
            try
            {
                unsafe
                {
                    double posx = 0, posy = 0, posz = 0, timePoint = 0;
                    long detectionIndex = 0;
                    char* position_name_ptr = null;

                    MISGetStagePosition(_storageId, acqTypeName, detectorName, imageidx, &posx, &posy, &posz);
                    MISGetTimePoint(_storageId, acqTypeName, detectorName, imageidx, &timePoint);
                    MISGetDetectionIndex(_storageId, acqTypeName, detectorName, imageidx, (IntPtr)(&detectionIndex));

                    string? positionName = null;
                    if (MISGetStagePositionName(_storageId, acqTypeName, detectorName, imageidx, &position_name_ptr) == 0
                        && position_name_ptr != null)
                    {
                        positionName = Marshal.PtrToStringAnsi((IntPtr)position_name_ptr);
                        MISFreeStagePositionName((IntPtr)position_name_ptr);
                    }

                    metadata.PositionX = posx;
                    metadata.PositionY = posy;
                    metadata.PositionZ = posz;
                    metadata.AcquisitionName = acqName;
                    metadata.DetectorName = detName;
                    metadata.PositionName = positionName;
                    metadata.Width = (uint)_width;
                    metadata.Height = (uint)_height;
                    metadata.TimePoint = timePoint;
                    metadata.DetectionIndex = (int)detectionIndex;
                    metadata.CurrentStagePosition = new XYStagePosition(0, posx, posy, posz, false, positionName ?? string.Empty);
                }
                return metadata;
            }
            finally
            {
                Marshal.FreeHGlobal(acqTypeName);
                Marshal.FreeHGlobal(detectorName);
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
                try
                {
                    if (MISOpenFile(input_path_ptr, out int storageId) != 0)
                        throw new IOException($"Could not open measurement storage '{_storagePath}'.");

                    _storageId = storageId;
                    OpenStorageIDS.OpenStorageIDSList.Add(_storageId);
                }
                finally
                {
                    Marshal.FreeHGlobal(input_path_ptr);
                }
            }
        }

        public string? GetImagerProgram()
        {
            unsafe
            {
                char* imager_program_ptr = null;

                if (MISGetImagerProgram(_storageId, &imager_program_ptr) != 0 || imager_program_ptr == null)
                    return null;

                string? imager_program = Marshal.PtrToStringAnsi((IntPtr)imager_program_ptr);
                MISFreeProgramDescription(imager_program_ptr);
                return imager_program;
            }
        }

        public void OpenWriteStream()
        {
            if (_storagePath != null && _isStorageEnabled)
            {
                // If the library cannot create the file, it terminates the whole process
                // (its writer thread is left un-joined), so make sure it can before calling it.
                EnsureStorageFileIsWritable(_storagePath);

                IntPtr measurement_descriptor_ptr = Marshal.StringToHGlobalAnsi(_measurementProgram);
                IntPtr storage_path_ptr = Marshal.StringToHGlobalAnsi(_storagePath);
                try
                {
                    if (MISNewStorage(storage_path_ptr, measurement_descriptor_ptr, out int storerId) != 0)
                        throw new IOException($"Could not create measurement storage '{_storagePath}'.");

                    _storageId = storerId;
                    OpenStorageIDS.OpenStorageIDSList.Add(_storageId);
                }
                finally
                {
                    Marshal.FreeHGlobal(measurement_descriptor_ptr);
                    Marshal.FreeHGlobal(storage_path_ptr);
                }
            }
        }

        private static void EnsureStorageFileIsWritable(string path)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                throw new DirectoryNotFoundException($"Storage folder '{directory}' does not exist.");

            try
            {
                // The library creates/truncates the file anyway.
                using (new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new IOException($"Cannot write measurement storage '{path}'.", ex);
            }
        }

     

        public byte[] ReadPlane(string acq_name, string det_name, int time_position)
        {
            if (!_isStorageEnabled || time_position == -1)
                return Array.Empty<byte>();

            IntPtr acqTypeName = Marshal.StringToHGlobalAnsi(acq_name);
            IntPtr detectorName = Marshal.StringToHGlobalAnsi(det_name);
            try
            {
                unsafe
                {
                    ushort* data_buffer = null;
                    int nRows = 0, nCols = 0;

                    // The library reports rows first, then columns. On failure (no such image or
                    // channel, or a pixel format the library cannot read back) return "no image",
                    // which is what callers already handle, instead of reading a garbage pointer.
                    if (MISGetImage(_storageId, acqTypeName, detectorName, time_position, &data_buffer, ref nRows, ref nCols) != 0
                        || data_buffer == null)
                    {
                        return Array.Empty<byte>();
                    }

                    try
                    {
                        _height = nRows;
                        _width = nCols;

                        // Mono16: two bytes per pixel, already in the byte order the GUI uses.
                        byte[] byteArray = new byte[nRows * nCols * 2];
                        fixed (byte* dest = byteArray)
                        {
                            Buffer.MemoryCopy(data_buffer, dest, byteArray.Length, byteArray.Length);
                        }
                        return byteArray;
                    }
                    finally
                    {
                        MISReleaseImageData(data_buffer);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(acqTypeName);
                Marshal.FreeHGlobal(detectorName);
            }
        }

        public void SavePlanes(List<byte[]> data_buffer, List<TiffPlaneMetadata> metadata)
        {
            if (_storagePath == null || !_isStorageEnabled)
                return;

            for (int buf_ind = 0; buf_ind < data_buffer.Count; buf_ind++)
            {
                byte[] frame_data = data_buffer[buf_ind];
                var meta = metadata[buf_ind];

                IntPtr acqTypeName = Marshal.StringToHGlobalAnsi(meta.AcquisitionName);
                IntPtr detectorName = Marshal.StringToHGlobalAnsi(meta.DetectorName);
                IntPtr posName = Marshal.StringToHGlobalAnsi(meta.PositionName ?? string.Empty);
                try
                {
                    unsafe
                    {
                        fixed (byte* data_buf_ptr = frame_data)
                        {
                            // The library takes rows (height) before columns (width); passing
                            // width first stored every non-square image transposed in the file.
                            MISAddNewImage(_storageId, acqTypeName, detectorName, meta.TimePoint,
                                           meta.PositionX, meta.PositionY, meta.PositionZ,
                                           meta.DetectionIndex, posName, meta.Type,
                                           (int)meta.Height, (int)meta.Width, data_buf_ptr);
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(acqTypeName);
                    Marshal.FreeHGlobal(detectorName);
                    Marshal.FreeHGlobal(posName);
                }
            }
        }

        public void SaveDecisions(List<string> decisions)
        {
            // Same guard as SavePlanes: there is no open storage in live mode or when
            // storage is disabled.
            if (_storagePath == null || !_isStorageEnabled)
                return;

            foreach(var decision in decisions)
            {
                // The library re-parses every decision as JSON when it writes the file; one
                // that is not valid JSON would make that throw inside the native writer.
                if (!IsJson(decision))
                    continue;

                // nlohmann::json requires UTF-8, which StringToHGlobalAnsi is not on Windows.
                IntPtr decisionPtr = Marshal.StringToCoTaskMemUTF8(decision);
                try
                {
                    MISAddSmartProgramDecision(_storageId, decisionPtr);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(decisionPtr);
                }
            }
        }

        private static bool IsJson(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            try
            {
                Newtonsoft.Json.Linq.JToken.Parse(text);
                return true;
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return false;
            }
        }

        public List<string> GetSmartProgramDecisions()
        {
            var decisions = new List<string>();
            if (_storagePath == null || !_isStorageEnabled)
                return decisions;

            unsafe
            {
                IntPtr array = IntPtr.Zero;
                int count = 0;
                MISGetSmartProgramDecisions(_storageId, (IntPtr)(&array), (IntPtr)(&count));

                try
                {
                    for (int i = 0; i < count && array != IntPtr.Zero; i++)
                    {
                        var decision = Marshal.PtrToStringUTF8(Marshal.ReadIntPtr(array, i * IntPtr.Size));
                        if (decision != null)
                            decisions.Add(decision);
                    }
                }
                finally
                {
                    if (array != IntPtr.Zero)
                        MISFreeStringArray(array);
                }
            }

            return decisions;
        }

        public int GetNumberOfImages(string acquisition, string detector)
        {
            IntPtr acqName = Marshal.StringToHGlobalAnsi(acquisition);
            IntPtr detectorName = Marshal.StringToHGlobalAnsi(detector);
            try
            {
                int nimages = 0;
                unsafe
                {
                    if (MISGetNumberOfImages(_storageId, acqName, detectorName, (IntPtr)(&nimages)) != 0)
                        return 0;
                }
                return nimages;
            }
            finally
            {
                Marshal.FreeHGlobal(acqName);
                Marshal.FreeHGlobal(detectorName);
            }
        }

        public int LoadMaxFrameNumber()
        {
            // The library writes an int64 here; a 4-byte buffer was overrun.
            long numDetections = 0;
            unsafe
            {
                if (MISGetNumberOfDetections(_storageId, (IntPtr)(&numDetections)) != 0)
                    return 0;
            }
            return (int)numDetections;
        }

        public void SetStoragePath(string path)
        {
            _storagePath = path;
        }

        public void SetMeasurementProgram(string measurementProgramJson)
        {
            _measurementProgram = measurementProgramJson;
        }

        public int GetMaxNumberOfFrames()
        {
            return MaxFrames; 
        }

        public void SetMaxFrameNumber(int max_frames)
        {
            MaxFrames = max_frames;
        }

        public void SetAcqDetPairs( List<Tuple<string,string>> acqDetPairs)
        {
            AcqDetPairs = acqDetPairs;
        }

        public List<Tuple<string, string>> GetStorageSchema()
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

  
        public XYStagePosition CurrentStagePosition = IStageControl.DefaultStagePosition;

    }


    
}
