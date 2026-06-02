using System;
using System.Collections.Generic;
using ImagerAvalonia.Utils;

namespace ImagerAvalonia.Services.Workspace;

/// <summary>
/// Holds the data (images, analysis results) for the current session.
/// The Avalonia UI binds specifically to this to render grids, canvases, and graphs.
/// </summary>
public class DataWorkspace
{
    // The collection of all images in memory for the current run/loaded file
    private readonly List<ChannelMessage> _allImages = new();

    public IReadOnlyList<ChannelMessage> AllImages => _allImages.AsReadOnly();
    
    // The very last image received, useful for "Live View" Canvases
    public ChannelMessage? LatestImage { get; private set; }

    public event EventHandler? DataCleared;
    public event EventHandler<ChannelMessage>? NewDataAdded;

    public void AddImage(ChannelMessage imageMsg)
    {
        _allImages.Add(imageMsg);
        LatestImage = imageMsg;
        NewDataAdded?.Invoke(this, imageMsg);
    }

    public void ClearWorkspace()
    {
        _allImages.Clear();
        LatestImage = null;
        DataCleared?.Invoke(this, EventArgs.Empty);
    }
}
