using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.ReactiveUI;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace ImagerAvalonia.ViewModels
{
    public partial class SmartProgramOutputViewModel : ViewModelBase
    {
        [ObservableProperty] string _smartProgramOutput = "Smart program running!";
        [ObservableProperty] ThrottledObservableCollection<TextRun> _runList = new ThrottledObservableCollection<TextRun>(TimeSpan.FromMilliseconds(200));

        private Queue<string> OutputQueue = new();
        private readonly PythonSmartServerService _pythonSmartServerService;

        public SmartProgramOutputViewModel(PythonSmartServerService pythonSmartServerService)
        {
            _pythonSmartServerService = pythonSmartServerService;
            _pythonSmartServerService.PythonProcess.OutputDataReceived += PythonProcess_OutputDataReceived;
            _pythonSmartServerService.PythonProcess.ErrorDataReceived += PythonProcess_ErrorDataReceived;
        }

        private void PythonProcess_ErrorDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (RunList.Count > 200)
                {
                    RunList.RemoveAt(RunList.Count - 1);
                }
                RunList.Add(new TextRun(e.Data, "Red"));


            }, DispatcherPriority.Background);
        }

        private async void PythonProcess_OutputDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (RunList.Count > 200)
                {
                    RunList.RemoveAt(RunList.Count - 1);
                }
                RunList.Add(new TextRun(e.Data, "White"));

            }, DispatcherPriority.Background);
        }
    }
    public class TextRun
    {
        public string Text { get; set; }
        public string Color { get; set; }

        public TextRun(string title, string color)
        {
            Text = title;
            Color = color;
        }
    }

    public class ThrottledObservableCollection<T> : ObservableCollection<T>, IDisposable
    {
        private readonly Subject<Unit> _changeSignal = new();
        private readonly IDisposable _subscription;

        public ThrottledObservableCollection(TimeSpan throttleTime)
        {
            IScheduler scheduler = AvaloniaScheduler.Instance;
            _subscription = _changeSignal
                .Sample(throttleTime)
                .ObserveOn(scheduler)
                .Subscribe(_ =>
                {
                    base.OnCollectionChanged(
                        new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                });
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            _changeSignal.OnNext(Unit.Default);
        }

        public void Dispose()
        {
            _subscription.Dispose();
            _changeSignal.Dispose();
        }
    }
}
