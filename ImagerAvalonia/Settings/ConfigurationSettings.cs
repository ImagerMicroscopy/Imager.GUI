using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.Settings
{
    public class ConfigurationSettings
    {
        public string? Pythonpath { get; set; }
        public bool IsLogBookEnabled { get; set; } = false;
        public string? ImagerPath { get; set; } =  Environment.CurrentDirectory;

        public ConfigurationSettings(string path) 
        {
            string contents = File.ReadAllText(path);

            JObject settings = JObject.Parse(contents);

            if (settings.TryGetValue("pythonpath", out var interpreterpath))
            {
                Pythonpath = interpreterpath.ToString();
            }
            if (settings.TryGetValue("islogbookenabled", out var isenabled))
            {
                IsLogBookEnabled = isenabled.Value<bool?>() ?? false;
            }
            if (settings.TryGetValue("imagerpath", out var imagerpath))
            {
                if(string.IsNullOrEmpty(imagerpath.ToString()))
                {
                    ImagerPath = Environment.CurrentDirectory;
                }
                else
                {
                    ImagerPath = imagerpath.ToString();
                }
            }
        }
    }

    public class LogBookConfigurationSettings
    {
        public JObject LogSettings { get; set; }

        public LogBookConfigurationSettings(string logbookconfigpath)
        {
            try
            {
                string contents = File.ReadAllText(logbookconfigpath);
                LogSettings = JObject.Parse(contents);

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }
    }
}
