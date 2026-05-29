using System;
using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;



namespace ImagerAvalonia.Services
{
    internal class LoggerService : ILogger
    {
        private readonly string _categoryName;
        private readonly ObservableLoggerProvider _provider;

        public LoggerService(string categoryName, ObservableLoggerProvider provider)
        {
            _categoryName = categoryName;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string response = formatter(state, exception);
            if(_categoryName=="Imager")
            {
                response = response.TrimEnd('\0');
            }

            string logentry = $"{DateTime.Now:HH:mm:ss}: [{_categoryName}] {response}";

            Dispatcher.UIThread.Invoke(() =>
            {

                _provider.Logs.Add(logentry);
                
            });
        }
    }

    public class ObservableLoggerProvider : ILoggerProvider
    {
        public ObservableCollection<string> Logs  = new ObservableCollection<string>() {"Welcome to Imager!"};

        public ILogger CreateLogger(string categoryName)
        {
            return new LoggerService(categoryName, this); 
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
