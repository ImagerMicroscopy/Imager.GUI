using Autofac;
using CommunityToolkit.Mvvm.ComponentModel;
using ImagerAvalonia.Services.Workspace;

namespace ImagerAvalonia.ViewModels;

/// <summary>
/// A tab in the center panel. The three fixed tabs (Live view, Field view, Smart
/// Program) wrap their content view model directly; measurement and opened-file tabs
/// additionally own the acquisition session that feeds them. Disposing a measurement
/// tab detaches the view model and disposes the session.
/// </summary>
public partial class ImageTabViewModel : ViewModelBase
{
    private readonly ILifetimeScope? _viewModelScope;

    public ImageTabViewModel(string header, object content, bool isFixed = false, ILifetimeScope? viewModelScope = null)
    {
        Header = header;
        Content = content;
        IsFixed = isFixed;
        _viewModelScope = viewModelScope;
    }

    public string Header { get; }

    /// <summary>The content view model shown inside the tab (image display, field
    /// viewer or smart program register).</summary>
    public object Content { get; }

    /// <summary>The image display view model when this tab shows images; null for the
    /// Field view and Smart Program tabs.</summary>
    public ImageDisplayViewModel? ViewModel => Content as ImageDisplayViewModel;

    /// <summary>Fixed tabs (Live view, Field view, Smart Program) are part of the
    /// panel itself and cannot be closed or popped out.</summary>
    public bool IsFixed { get; }

    public AcquisitionSession? Session { get; private set; }

    [ObservableProperty] private bool _isRunning;

    /// <summary>Fixed tabs and running measurements cannot be closed.</summary>
    public bool IsClosable => !IsFixed && !IsRunning;

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(IsClosable));

    internal void AttachSession(AcquisitionSession session)
    {
        Session = session;
        IsRunning = session.Kind == AcquisitionSessionKind.Experiment;
        ViewModel?.AttachHandler(session.Handler, enableFrameRequests: session.Kind != AcquisitionSessionKind.Live);
    }

    /// <summary>
    /// An experiment run ended (completed or stopped). The tab can now be closed, and the
    /// slider browses the saved frames instead of following the acquisition.
    /// </summary>
    internal void SessionFinished()
    {
        IsRunning = false;
        if (ViewModel is not { } viewModel) return;
        viewModel.IsExperimentRunning = false;
        viewModel.IsDataStreaming = false;
        if (Session != null)
        {
            viewModel.SetOpenID(Session.StorageProvider.GetOpenID());
            if (Session.Kind == AcquisitionSessionKind.Experiment)
            {
                viewModel.ShowLiveView = false;
            }
        }
    }

    internal void Dispose()
    {
        ViewModel?.DetachHandler();
        Session?.Dispose();
        _viewModelScope?.Dispose();
    }
}
