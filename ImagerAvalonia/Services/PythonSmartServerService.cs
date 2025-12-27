using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ImagerAvalonia.Exceptions;
using ScottPlot.Colormaps;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImagerAvalonia.Services
{
    public class PythonSmartServerService
    {

        private List<FileSystemWatcher> Watchers = new List<FileSystemWatcher>();
        public Process PythonProcess = new Process();
        private string? PythonPath;
        private const int DebounceDelayMs = 500;
        private string[] _watchDirectories;

        public PythonSmartServerService() {


            PythonPath = App.Configuration.Pythonpath;
            

            PythonProcess.StartInfo.FileName = PythonPath;
            PythonProcess.StartInfo.Arguments = "-m uvicorn main:app --host 127.0.0.1 --port 5100 --timeout-keep-alive 600 --log-level warning"; 
            PythonProcess.StartInfo.WorkingDirectory = "SmartProgramPython";
            PythonProcess.StartInfo.RedirectStandardOutput = true;
            PythonProcess.StartInfo.RedirectStandardError = true;
            PythonProcess.StartInfo.UseShellExecute = false;
            PythonProcess.StartInfo.CreateNoWindow = true;


        }



        public async void StartSmartProgram()
        {
            bool pythonexists = File.Exists(PythonPath);
            if (PythonPath is null || !pythonexists) 
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    await ExceptionWindowHandler.ShowDialogAsync(
                        "Error", "Python path does not exist. Smart server not initiated", "You can set the python path in the Config.json.", desktop.MainWindow);
                    return;
                }
            }


            PythonProcess.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
            PythonProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };

            PythonProcess.Exited += (s, e) =>

            {
                Console.WriteLine("Python process exited. Restarting...");
            };
            PythonProcess.Start();
            PythonProcess.BeginOutputReadLine();
            PythonProcess.BeginErrorReadLine();
        }

        internal void KillSmartProgram()
        {
            PythonProcess.Kill();
        }
    }
}
