
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImagerAvalonia.Utils
{



    public class ComUtils

    {
        public string awaiting_spectra = "\"status\":\"nonewspectra\"";
        public static string no_more_spectra = "\"status\":\"nonewspectracoming\"";
        public static string isokresponse = "\"status\",\"status\":\"ok\"";
        public string detectorproperties = "\"responsetype\":\"detectorproperties\"";
        public static string cancelacquisition = "{\"action\":\"cancelasyncacquisition\"}";
        public static string fetchasyncstatus = "{\"action\":\"fetchasyncstatusmessages\"}";

        public string detectors = "\"responsetype\":\"availabledetectors\"";
        public string availableequipment = "\"responsetype\":\"availableequipment\"";

        public string listdetectors = "{\"action\":\"listavailabledetectors\"}";
        public string listavailableequipment = "{\"action\":\"listavailableequipment\"}";
        public static string fetchmessage = "{\"action\":\"fetchasyncspectra\"}";

        public static string set_detectorproperties(string detector_name, string property_val) => $"{{ \"action\":\"setdetectorproperty\", \"detectorname\": \"{detector_name}\" , \"property\": property_val }}";
        public static string get_detectorproperties(string detector_name) => $"{{ \"action\":\"getdetectorproperties\", \"detectorname\": \"{detector_name}\" }}";
        public static string get_stageposition(string stage_name) => $"{{\"action\":\"getmotorizedstageposition\",\"name\": \"{stage_name}\"}}";
        public static string set_stageposition(string stage_name,
                                               string af_offset, string af, string x, string y, string z
                                               ) => $"{{\"action\":\"setmotorizedstageposition\",\"name\":\"{stage_name}\",\"position\":{{\"hardwareautofocusoffset\":{af_offset},\"usinghardwareautofocus\":{af.ToLower()},\"x\":{x},\"y\":{y},\"z\":{z}}}}}";
        public static string acknowledgemessage(int n) => $"{{ \"action\":\"acknowledgedatareceipt\",\"uptoandincluding\":{n.ToString("F1", CultureInfo.InvariantCulture)}}}";


        public virtual void SendDataRequest(string message, string validation_message, Action<string>? perform_data_manipulation, Action<byte[]>? perform_numeric_data_manipulation)
        {
            using (ImagerCommunication ImagerCommunicator = new ImagerCommunication())
            {

                ImagerCommunicator.EstablishConnectionStream();
                ImagerCommunicator.SendMessage(message);

                byte[] received_data = ImagerCommunicator.ReadData();

                if (received_data.Length > 0)
                {
                    if (ImagerCommunicator.CheckIfMessageIsJson(received_data))
                    {
                        string message_response = Encoding.ASCII.GetString(received_data, 0, received_data.Length);
                        if (ImagerCommunicator.IsOKResponse(message_response, validation_message))
                        {
                            if (!(perform_data_manipulation == null))
                            {
                                perform_data_manipulation.Invoke(message_response);
                            }
                        }
                        else
                        {
                            throw new UnexpectedAnswerException(message_response, validation_message);
                        }
                    }
                    else
                    {
                        perform_numeric_data_manipulation?.Invoke(received_data);
                    }
                }
            }
        }

        public static void SendSingleMessage(string message)
        {
            using (ImagerCommunication ImagerCommunicator = new ImagerCommunication())
            {

                ImagerCommunicator.EstablishConnectionStream();
                ImagerCommunicator.SendMessage(message);

            }
        }

    }

    public class ImagerCommunication : IDisposable
    {
        private int _port = 3200;
        private TcpClient? _fetchclient;
        private NetworkStream? _stream_fetcher;


        public ImagerCommunication()
        {


        }
        public void Dispose()
        {

            _stream_fetcher?.Close();
            _fetchclient?.Close();

        }


        public bool CheckIfMessageIsJson(byte[] message)
        {

            string first_token = Encoding.ASCII.GetString(message, 0, 1);
            string last_token = Encoding.ASCII.GetString(message, message.Length - 5, 1);

            if (first_token == "{" && last_token == "}")
            {
                return true;

            }
            return false;


        }

        public static List<string> FetchAcquisitionOrDetectorName(int n_dsets, byte[] received_data, ref int startind)
        {




            List<string> names = new List<string>();

            for (int i = 1; i <= n_dsets; i++)
            {
                Byte[] utf8name_length = new Byte[1];

                Byte[] utf8_name = new Byte[256];

                Array.Copy(received_data, startind, utf8name_length, 0, utf8name_length.Length);
                startind += 1;
                Array.Copy(received_data, startind, utf8_name, 0, utf8name_length[0]);
                startind += utf8name_length[0];
                // remove trailing zeros

                utf8_name = Utils.TrimEnd(utf8_name);

                names.Add(Encoding.UTF8.GetString(utf8_name, 0, utf8_name.Length));

            }
            return names;

        }

        public static List<ulong> FetchUInt64ParameterFromBytesAtOffset(int n_dsets, byte[] received_data, ref int startind)
        {

            Byte[] offset_index = new Byte[8];


            List<ulong> result = new List<ulong>();
            for (int i = 1; i <= n_dsets; i++)
            {
                Array.Copy(received_data, startind, offset_index, 0, offset_index.Length);


                result.Add(BitConverter.ToUInt64(offset_index));
                startind += 8;

            }
            return result;
        }
        public static List<List<uint>> FetchUInt32ParameterFromBytesAtOffset(int n_dsets, byte[] received_data, ref int startind)
        {
            Byte[] offset_index = new Byte[4];

            List<List<uint>> all_sizes = new List<List<uint>>() { };
            for (int i = 1; i <= n_dsets; i++)
            {
                List<uint> result = new List<uint>();
                Array.Copy(received_data, startind, offset_index, 0, offset_index.Length);


                result.Add(BitConverter.ToUInt32(offset_index));
                startind += 4;

                Array.Copy(received_data, startind, offset_index, 0, offset_index.Length);
                result.Add(BitConverter.ToUInt32(offset_index));
                startind += 4;
                all_sizes.Add(result);

            }
            return all_sizes;
        }

        public static List<double> FetchFloat64ParameterFromBytesAtOffset(int n_dsets, byte[] received_data, ref int startind)
        {
            Byte[] offset_index = new Byte[8];

            List<double> result = new List<double>();
            for (int i = 1; i <= n_dsets; i++)
            {
                Array.Copy(received_data, startind, offset_index, 0, offset_index.Length);


                result.Add(BitConverter.ToDouble(offset_index));
                startind += 8;

            }
            return result;
        }

        public static List<Byte[]> FetchImageData(int n_dsets, byte[] received_data, ref int startind, List<List<uint>> image_sizes, Byte[] num_type)
        {
            List<Byte[]> result = new List<Byte[]>();


            for (int i = 1; i <= n_dsets; i++)
            {
                if (num_type[0] == 0)
                {
                    Byte[] im_data = new byte[2 * image_sizes[i - 1][0] * image_sizes[i - 1][1]];
                    Array.Copy(received_data, startind, im_data, 0, im_data.Length);
                    result.Add(im_data);
                    startind += im_data.Length;
                }


            }
            return result;

        }
        public byte[] ReadData()
        {
            Task<byte[]> check_if_data_availble = Task.Run(() =>

            {
                Byte[] response_message = Array.Empty<Byte>();

                while (response_message.Length == 0)
                {
                    response_message = CheckIfDataAvailable();
                }
                return response_message;

            }
            );


            if (check_if_data_availble.Wait(20000))
            {
                return check_if_data_availble.Result; // Completed within timeout
            }
            else
            {
                throw new TimeOutResponseFromImager();
            }


        }

        public byte[] CheckIfDataAvailable()
        {


            if (_stream_fetcher != null)
            {
                if (_stream_fetcher.DataAvailable)
                {

                    Int32 num_bytes_read = 0;


                    byte[] receive_data_size_buffer = new byte[4];
                    Int32 bytes = _stream_fetcher.Read(receive_data_size_buffer, 0, receive_data_size_buffer.Length);
                    byte[] message = new byte[BitConverter.ToInt32(receive_data_size_buffer, 0)];


                    while (num_bytes_read < BitConverter.ToInt32(receive_data_size_buffer, 0) - 4)
                    {
                        num_bytes_read += _stream_fetcher.Read(message, num_bytes_read, message.Length - 4 - num_bytes_read);
                    }
                    return message;

                }
                else
                {
                    return Array.Empty<Byte>();
                }

            }
            else
            {
                throw new ArgumentNullException("Network stream not initialized");
            }


        }



        public void EstablishConnectionStream()
        {
            _fetchclient = new TcpClient();

            if (_fetchclient.ConnectAsync("localhost", _port).Wait(TimeSpan.FromSeconds(3)))
            {
                _fetchclient.LingerState = new LingerOption(true, 0);
                _stream_fetcher = _fetchclient.GetStream();
                Thread.Sleep(1);
            }
            else
            {
                throw new SocketException(1, "Could not connect to Imager backend upon 20 second timeout. Check if the backend is running.");

            }
        }

        public void SendMessage(string message)
        {
            if (_stream_fetcher != null)
            {
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(message);
                _stream_fetcher.Write(data, 0, data.Length);
            }
            else
            {
                throw new ArgumentNullException("Network stream not initialized");
            }

        }


        // Checks if incoming message contains the target. 
        public bool IsOKResponse(string message, string target_message)
        {
            if (message.Contains(target_message) || target_message == "")
            {
                return true;
            }
            return false;

        }

    }

    public class UnknownDataException : ApplicationException
    {
        public UnknownDataException(string message)
        {
            System.Diagnostics.Debug.WriteLine($"Uknown data response from the server: {message}");
        }

    }
    public class TimeOutResponseFromImager : TimeoutException
    {
        public TimeOutResponseFromImager()
        {
            System.Diagnostics.Debug.WriteLine("Did not receive answer from server in 2 seconds.");
        }

    }

    public class UnexpectedAnswerException : ApplicationException
    {
        private string _message;
        private string _target_message;

        public override string Message => $"UnexpectedAnswerException: Got {_message}. Expected: {_target_message}";

        public UnexpectedAnswerException(string message, string target_message)
        {
            _message = message;
            _target_message = target_message;
        }

    }
    public static class Utils
    {
        public static byte[] TrimEnd(byte[] array)
        {
            int lastIndex = Array.FindLastIndex(array, b => b != 0);

            Array.Resize(ref array, lastIndex + 1);

            return array;
        }

    }
}
