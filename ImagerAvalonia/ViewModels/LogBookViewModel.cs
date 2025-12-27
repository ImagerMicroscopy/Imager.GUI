using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImagerAvalonia.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

namespace ImagerAvalonia.ViewModels
{
    public partial class LogBookViewModel : ObservableValidator
    {
        [ObservableProperty] ObservableCollection<LogBookSettingViewModel> _settingsStart = new();
        [ObservableProperty] ObservableCollection<LogBookSettingViewModel> _settingsEnd = new();

        [ObservableProperty]
        [Required(ErrorMessage = "User name is required.")]
        string _userName;
        [ObservableProperty] DateTime _startDate = DateTime.Now;
        [ObservableProperty] DateTime _endDate;
        [ObservableProperty] bool _isEnd;

        private Guid SessionID = Guid.NewGuid();
        private DbContext _dbContext;

        public event EventHandler OnDataSubmitted;

        public LogBookViewModel(JObject logbooksettings, JObject logbooksettingsend, bool isEnd)
        {
            IsEnd = isEnd;
            foreach (var logbooksetting in logbooksettings)
            {
                string logbooksettingkey = logbooksetting.Key;
                JToken logbooksettingvalue = logbooksetting.Value;

                SettingsStart.Add(new LogBookSettingViewModel(logbooksettingkey,
                 JsonConvert.DeserializeObject<List<string>>(logbooksettingvalue.ToString())));

            }

            foreach (var logbooksetting in logbooksettingsend)
            {
                string logbooksettingkey = logbooksetting.Key;
                JToken logbooksettingvalue = logbooksetting.Value;

                SettingsEnd.Add(new LogBookSettingViewModel(logbooksettingkey,
                 JsonConvert.DeserializeObject<List<string>>(logbooksettingvalue.ToString())));

            }
        }

        public async void SubmitEntry()
        {
            if (_dbContext is LogBookContext logBookContext)
            {
                if (!IsEnd)
                {
                    ValidateAllProperties();

                    if (HasErrors)
                        return;
                    await logBookContext.SubmitLoginEntry(SessionID, UserName, StartDate);
                }
                else
                {
                    var obj = new LogBookSerialized
                    {
                        SettingsStart = SettingsStart.ToList(),
                        SettingsEnd = SettingsEnd.ToList()
                    };

                    string json = JsonConvert.SerializeObject(obj, Formatting.Indented);
                    await logBookContext.SubmitLogoutEntry(SessionID, json);
                }
                OnDataSubmitted?.Invoke(this, new EventArgs());

            }
        }

        internal void SetVMDBContext(LogBookContext db)
        {
            _dbContext = db;
        }
    }

    public partial class LogBookSettingViewModel : ViewModelBase
    {
        [ObservableProperty] ObservableCollection<LogBookSettingIsEnabled> _logbooksettingvalues;
        [ObservableProperty] string _logbooksettingkey;

        public LogBookSettingViewModel(string logbooksettingkey, List<string> logbooksettingvalue)
        {
            Logbooksettingkey = logbooksettingkey;
            Logbooksettingvalues = new ObservableCollection<LogBookSettingIsEnabled>(logbooksettingvalue.Select(x => new LogBookSettingIsEnabled(x)));


        }
    }

    public partial class LogBookSettingIsEnabled : ViewModelBase
    {
        [ObservableProperty] string _logbookvalue;
        [ObservableProperty] bool _isvalueenabled;

        public LogBookSettingIsEnabled(string logbookvalue)
        {
            Logbookvalue = logbookvalue;
            Isvalueenabled = false;
        }
    }






    public class LogBookSerialized
    {
        [JsonConverter(typeof(LogBookSettingCollectionConverter))]
        public List<LogBookSettingViewModel> SettingsStart { get; set; }

        [JsonConverter(typeof(LogBookSettingCollectionConverter))]
        public List<LogBookSettingViewModel> SettingsEnd { get; set; }
    }



    public class LogBookSettingCollectionConverter : JsonConverter<List<LogBookSettingViewModel>>
    {
        public override void WriteJson(JsonWriter writer, List<LogBookSettingViewModel> value, JsonSerializer serializer)
        {
            // 1. Start the main object container
            writer.WriteStartObject();

            foreach (var setting in value)
            {

                writer.WritePropertyName(setting.Logbooksettingkey);

                writer.WriteStartObject();

                foreach (var item in setting.Logbooksettingvalues)
                {

                    writer.WritePropertyName(item.Logbookvalue);
                    writer.WriteValue(item.Isvalueenabled);
                }

                writer.WriteEndObject();
            }

            // End main object
            writer.WriteEndObject();
        }

        public override List<LogBookSettingViewModel> ReadJson(
            JsonReader reader,
            Type objectType,
            List<LogBookSettingViewModel> existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            throw new NotImplementedException("Deserialization not supported.");
        }
    }


    public class LogBookSettingConverter : JsonConverter<LogBookSettingViewModel>
    {
        public override void WriteJson(JsonWriter writer, LogBookSettingViewModel value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(value.Logbooksettingkey);

            foreach (var item in value.Logbooksettingvalues)
            {
                writer.WritePropertyName(item.Logbookvalue);
                writer.WriteValue(item.Isvalueenabled);
            }

            writer.WriteEndObject();
        }

        public override LogBookSettingViewModel ReadJson(
            JsonReader reader,
            Type objectType,
            LogBookSettingViewModel existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            throw new NotImplementedException("Deserialization not supported.");
        }
    }
}
