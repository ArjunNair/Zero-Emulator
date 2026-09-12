using System;

namespace Zero.Emulation.Host
{
    /// <summary>One rendered Spectrum frame, border included, as 0x00RRGGBB pixels.</summary>
    public sealed class VideoFrame
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int[] Pixels { get; private set; } = Array.Empty<int>();
        public long FrameNumber { get; internal set; }

        internal void EnsureSize(int width, int height)
        {
            if (width != Width || height != Height || Pixels.Length != width * height)
            {
                Width = width;
                Height = height;
                Pixels = new int[width * height];
            }
        }
    }

    /// <summary>
    /// Triple-buffered frame hand-off between the emulation thread (producer) and the UI thread
    /// (consumer). The producer always has a free buffer to write into, the consumer always gets
    /// the most recent complete frame, and neither ever blocks on the other. Frames the UI never
    /// asked for are simply dropped.
    /// </summary>
    public sealed class FrameQueue
    {
        private readonly VideoFrame[] _buffers = { new VideoFrame(), new VideoFrame(), new VideoFrame() };
        private readonly object _sync = new object();
        private int _back = 0;    // being written by the producer
        private int _ready = 1;   // latest complete frame
        private int _front = 2;   // held by the consumer
        private bool _hasNew;
        private long _frameCounter;

        /// <summary>Producer: buffer to fill for the next frame.</summary>
        public VideoFrame BeginWrite(int width, int height)
        {
            VideoFrame f = _buffers[_back];
            f.EnsureSize(width, height);
            return f;
        }

        /// <summary>Producer: publish the buffer returned by <see cref="BeginWrite"/>.</summary>
        public void EndWrite()
        {
            lock (_sync)
            {
                _buffers[_back].FrameNumber = ++_frameCounter;
                int t = _ready; _ready = _back; _back = t;
                _hasNew = true;
            }
        }

        /// <summary>Consumer: the newest frame if one arrived since the last call, else null.</summary>
        public VideoFrame TryAcquireLatest()
        {
            lock (_sync)
            {
                if (!_hasNew) return null;
                int t = _front; _front = _ready; _ready = t;
                _hasNew = false;
                return _buffers[_front];
            }
        }

        public long FramesProduced { get { lock (_sync) return _frameCounter; } }
    }
}
