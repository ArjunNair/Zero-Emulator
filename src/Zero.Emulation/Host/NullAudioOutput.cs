using Speccy;

namespace Zero.Emulation.Host
{
    /// <summary>Discards audio and never blocks: the emulator runs as fast as the CPU allows.</summary>
    public sealed class NullAudioOutput : IAudioOutput
    {
        private readonly byte[] _buffer = new byte[AudioFormat.FrameBytes];

        public long FramesSubmitted { get; private set; }

        public void Play() { }
        public void Stop() { }
        public void Shutdown() { }
        public void SetVolume(float volume) { }
        public bool FinishedPlaying() => true;
        public byte[] LockBuffer() => _buffer;
        public void UnlockBuffer(byte[] buffer) { FramesSubmitted++; }
    }

    /// <summary>The fixed PCM format the core produces.</summary>
    public static class AudioFormat
    {
        public const int SampleRate = 44100;
        public const int Channels = 2;
        public const int BytesPerSample = 2;
        /// <summary>Stereo sample frames per emulated video frame (44100 / 50).</summary>
        public const int SamplesPerFrame = 882;
        public const int FrameBytes = SamplesPerFrame * Channels * BytesPerSample;
        public const double FrameSeconds = SamplesPerFrame / (double)SampleRate;
    }
}
