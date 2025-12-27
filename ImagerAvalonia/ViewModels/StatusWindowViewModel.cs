
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services;
using System.Collections.ObjectModel;


namespace ImagerAvalonia.ViewModels;

public partial class StatusWindowViewModel : ViewModelBase
{

    [ObservableProperty] string _displayStatusMessage = System.String.Empty;
    [ObservableProperty] ObservableCollection<string> _statusMessages = new();


    public StatusWindowViewModel() { }

    public StatusWindowViewModel(ObservableLoggerProvider loggerProvider)
    {
        StatusMessages = loggerProvider.Logs;
        StatusMessages.CollectionChanged += StatusMessages_CollectionChanged;
    }

    private void StatusMessages_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        DisplayStatusMessage = StatusMessages[StatusMessages.Count - 1].Replace(',','\n').Replace('[','\n').Replace(']','\n').Replace("\"","");
    }
    public override void Dispose()
    {

    }


}

