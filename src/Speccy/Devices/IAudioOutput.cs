namespace Speccy
{
    /// <summary>
    /// Host-provided audio sink. The emulation core produces 16-bit stereo PCM at 44.1 kHz in
    /// fixed-size frames and hands them over through the lock/unlock pair; the host owns the
    /// actual device (DirectSound today, SDL3 later) and paces playback.
    /// </summary>
    public interface IAudioOutput
    {
        /// <summary>Start (or resume) playback.</summary>
        void Play();

        /// <summary>Pause playback without releasing the device.</summary>
        void Stop();

        /// <summary>Release the device. The instance must not be used afterwards.</summary>
        void Shutdown();

        /// <summary>Linear volume, 0.0 (silent) .. 1.0 (full). Values above 1.0 may be clamped.</summary>
        void SetVolume(float volume);

        /// <summary>
        /// True when the sink has room for another frame. The core busy-waits on this at 1x speed,
        /// which is what ties emulation pacing to the audio clock.
        /// </summary>
        bool FinishedPlaying();

        /// <summary>Borrow an empty buffer to fill, or null if none is free right now.</summary>
        byte[] LockBuffer();

        /// <summary>Return a filled buffer (obtained from <see cref="LockBuffer"/>) for playback.</summary>
        void UnlockBuffer(byte[] buffer);
    }
}
