using System.Diagnostics;
using System.Threading;
using Speccy;

namespace Zero.Emulation.Host
{
    /// <summary>
    /// Silent sink that still paces emulation to real time. Used when there is no audio device or
    /// the user muted sound: the core keeps calling <see cref="FinishedPlaying"/> until one frame's
    /// worth of wall-clock time (20 ms) has elapsed since the previous buffer was accepted.
    /// </summary>
    public sealed class TimerPacedAudioOutput : IAudioOutput
    {
        private readonly byte[] _buffer = new byte[AudioFormat.FrameBytes];
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private double _nextDeadline;
        private bool _playing;

        public void Play() { _playing = true; _nextDeadline = _clock.Elapsed.TotalSeconds; }
        public void Stop() { _playing = false; }
        public void Shutdown() { _playing = false; }
        public void SetVolume(float volume) { }

        public bool FinishedPlaying()
        {
            if (!_playing) return true;
            double now = _clock.Elapsed.TotalSeconds;
            if (now >= _nextDeadline) return true;
            // Don't burn a whole core spinning; the caller loops on us with Sleep(1) anyway.
            double remaining = _nextDeadline - now;
            if (remaining > 0.002) Thread.Sleep(1);
            return false;
        }

        public byte[] LockBuffer() => _buffer;

        public void UnlockBuffer(byte[] buffer)
        {
            double now = _clock.Elapsed.TotalSeconds;
            _nextDeadline += AudioFormat.FrameSeconds;
            // If we fell far behind (debugger, laptop lid), resync instead of racing to catch up.
            if (_nextDeadline < now - 0.25) _nextDeadline = now;
        }
    }
}
