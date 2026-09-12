using Speccy;

namespace Zero.Core.Tests
{
    /// <summary>Audio sink that never blocks: lets the core run headless at full speed.</summary>
    internal sealed class NullAudioOutput : IAudioOutput
    {
        private readonly byte[] _buffer = new byte[882 * 2 * 2];

        public int FramesSubmitted { get; private set; }

        public void Play() { }
        public void Stop() { }
        public void Shutdown() { }
        public void SetVolume(float volume) { }
        public bool FinishedPlaying() => true;
        public byte[] LockBuffer() => _buffer;
        public void UnlockBuffer(byte[] buffer) { FramesSubmitted++; }
    }
}
