using ImagerAvalonia.Services.Storage;
using ImagerAvalonia.Utils;
using System;
using System.Collections.Generic;

namespace ImagerAvalonia.ViewModels;

/// <summary>
/// One tile placed on the field-viewer map: the contrast-free pixel sample it
/// was built from (so it can be re-rendered when the contrast limits change)
/// and where/with which limits it is currently drawn.
/// </summary>
public sealed class FieldViewerTile
{
    public ushort[] Sample = Array.Empty<ushort>();
    public int Width;
    public int Height;
    public double ScreenX;
    public double ScreenY;
    public int ContrastMin;
    public int ContrastMax;
    public ImagePixelFormat Format;
}

/// <summary>
/// State of one acquisition x detector overlay of the field viewer.
/// </summary>
public sealed class FieldViewerChannel
{
    public bool HasAnchor;
    public double AnchorX;
    public double AnchorY;

    public bool HasTiles;
    public double LastStageX;
    public double LastStageY;

    public readonly List<FieldViewerTile> Tiles = new();
}

/// <summary>
/// Tile placement logic of the field viewer, free of UI concerns. The spec it
/// implements lives in FieldViewerSpecTests:
/// - a frame is placed at mosaic origin + (stage position - anchor) /
///   (micrometers per screen unit), where the anchor is the stage position
///   read on engage, or the first received frame if the stage is unreadable;
/// - a frame within DuplicateThresholdFraction of the field of view of the
///   last placed tile is a re-image and is skipped; the first frame of a
///   channel is always placed;
/// - one screen unit spans 10 image pixels, so a field of W pixels is drawn
///   W/10 units wide and stage travel is mapped with the same scale.
/// All members must be called from a single thread (the field viewer calls
/// them on the UI thread).
/// </summary>
public sealed class FieldViewerMosaic
{
    public const double DuplicateThresholdFraction = 0.1;

    private readonly Dictionary<(string Acquisition, string Detector), FieldViewerChannel> _channels = new();

    public double PixelSizeNm { get; set; } = 100;

    /// <summary>Screen position the anchor maps to (the mosaic origin).</summary>
    public double OriginX { get; set; }
    public double OriginY { get; set; }

    /// <summary>Micrometers of stage travel per screen unit.</summary>
    private double MicronsPerScreenUnit => PixelSizeNm / 100.0;

    public void Rebuild(IEnumerable<(string Acquisition, string Detector)> keys)
    {
        _channels.Clear();
        foreach (var key in keys)
            _channels[key] = new FieldViewerChannel();
    }

    public bool TryGetChannel((string Acquisition, string Detector) key, out FieldViewerChannel channel) =>
        _channels.TryGetValue(key, out channel!);

    /// <summary>
    /// Sets the engage anchor on channels that have not received frames yet.
    /// Channels that already anchored (from their first frame) keep theirs.
    /// </summary>
    public void SetAnchor(double stageX, double stageY)
    {
        foreach (var channel in _channels.Values)
        {
            if (channel.HasAnchor)
                continue;
            channel.HasAnchor = true;
            channel.AnchorX = stageX;
            channel.AnchorY = stageY;
        }
    }

    /// <summary>
    /// Decides whether the frame should place a tile and where. The first
    /// frame of a channel always places; afterwards a frame within the
    /// duplicate threshold of the last placed tile is skipped.
    /// </summary>
    public bool ShouldPlace(TiffPlaneMetadata metadata, FieldViewerChannel channel, out double screenX, out double screenY)
    {
        if (!channel.HasAnchor)
        {
            channel.HasAnchor = true;
            channel.AnchorX = metadata.PositionX;
            channel.AnchorY = metadata.PositionY;
        }

        screenX = OriginX + (metadata.PositionX - channel.AnchorX) / MicronsPerScreenUnit;
        // Stage +Y is up, canvas +Y is down.
        screenY = OriginY - (metadata.PositionY - channel.AnchorY) / MicronsPerScreenUnit;

        if (channel.HasTiles)
        {
            double fieldOfViewUm = Math.Min(metadata.Width, metadata.Height) * PixelSizeNm / 1000.0;
            double threshold = DuplicateThresholdFraction * fieldOfViewUm;
            if (Math.Abs(metadata.PositionX - channel.LastStageX) <= threshold &&
                Math.Abs(metadata.PositionY - channel.LastStageY) <= threshold)
                return false;
        }

        return true;
    }

    public FieldViewerTile Place(
        TiffPlaneMetadata metadata,
        FieldViewerChannel channel,
        ushort[] sample,
        int sampleWidth,
        int sampleHeight,
        double screenX,
        double screenY,
        int contrastMin,
        int contrastMax)
    {
        var tile = new FieldViewerTile
        {
            Sample = sample,
            Width = sampleWidth,
            Height = sampleHeight,
            ScreenX = screenX,
            ScreenY = screenY,
            ContrastMin = contrastMin,
            ContrastMax = contrastMax,
            Format = metadata.Type,
        };
        channel.Tiles.Add(tile);
        channel.HasTiles = true;
        channel.LastStageX = metadata.PositionX;
        channel.LastStageY = metadata.PositionY;
        return tile;
    }

    /// <summary>
    /// Clears every tile but keeps the anchors, so a re-image of the same
    /// region redraws at the same position.
    /// </summary>
    public void ClearTiles()
    {
        foreach (var channel in _channels.Values)
        {
            channel.Tiles.Clear();
            channel.HasTiles = false;
        }
    }
}
