using System;
using System.Collections.Generic;

namespace ImagerAvalonia.Views.MultiChannelView
{
    /// <summary>
    /// Synchronizes the frames of the different channels of one multi-channel
    /// composite: it keeps the latest frame per channel and hands all of them out
    /// together once every enabled channel has one. The composite is therefore
    /// rendered from the newest frame of each channel, never mixing an old frame
    /// of one channel with a newer one of another more than the arrival order
    /// forces.
    ///
    /// Instance state by design: each multi-channel view owns one buffer, so its
    /// composite is built only from its own frames. (The buffer this replaces was
    /// static, which blended frames across simultaneously active views and raced
    /// between their display loops.)
    /// </summary>
    internal sealed class ChannelFrameBuffer
    {
        public const int ChannelCount = 5;

        /// <summary>The pending frame per channel slot. Internal for tests.</summary>
        internal readonly byte[]?[] Frames = new byte[ChannelCount][];

        /// <summary>Stores the frame for its channel; a newer frame replaces a
        /// pending older one.</summary>
        public void Add(int channelIndex, byte[] frame)
        {
            if (channelIndex >= 0 && channelIndex < ChannelCount)
            {
                Frames[channelIndex] = frame;
            }
        }

        /// <summary>Clears all pending frames.</summary>
        public void Clear() => Array.Clear(Frames);

        /// <summary>
        /// Takes the pending frames for compositing, but only once every enabled
        /// channel has one: until then the buffer keeps waiting. On success the
        /// buffer is cleared for the next composite; channels without a frame are
        /// null in the returned array.
        /// </summary>
        public bool TryTakeCompleted(IReadOnlyCollection<int> enabledChannels, out byte[]?[] frames)
        {
            foreach (int channel in enabledChannels)
            {
                if (Frames[channel] is null)
                {
                    frames = Array.Empty<byte[]?>();
                    return false;
                }
            }

            frames = (byte[]?[])Frames.Clone();
            Clear();
            return true;
        }
    }
}
