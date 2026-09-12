using System;
using SDL;
using Speccy;
using Zero.Emulation.Host;
using static SDL.SDL3;

namespace Zero.Sdl
{
    /// <summary>
    /// Pushes the core's 16-bit stereo 44.1 kHz frames into an SDL3 audio stream. The device
    /// drains the stream at its own rate; <see cref="FinishedPlaying"/> reports "room for more"
    /// once fewer than <see cref="TargetQueuedFrames"/> frames are waiting, which is what paces
    /// the emulation thread to the audio clock (~60 ms of latency).
    /// </summary>
    public sealed unsafe class SdlAudioOutput : IAudioOutput
    {
        public const int TargetQueuedFrames = 3;

        private SDL_AudioStream* _stream;
        private readonly byte[] _buffer = new byte[AudioFormat.FrameBytes];
        private bool _disposed;

        // Stall protection: a device that is present but not consuming (no session audio, sandbox,
        // unplugged output) would otherwise freeze emulation. After StallSeconds without the queue
        // draining we pace from the wall clock like TimerPacedAudioOutput.
        private const double StallSeconds = 0.5;
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        private double _lastDrainSeconds;
        private int _lastQueued = -1;
        private double _nextDeadline;

        /// <summary>True once the device stopped draining and pacing switched to the wall clock.</summary>
        public bool Stalled { get; private set; }

        public SdlAudioOutput()
        {
            SdlRuntime.EnsureInit(SDL_InitFlags.SDL_INIT_AUDIO);
            var spec = new SDL_AudioSpec
            {
                format = SDL_AudioFormat.SDL_AUDIO_S16LE,
                channels = AudioFormat.Channels,
                freq = AudioFormat.SampleRate
            };
            _stream = SDL_OpenAudioDeviceStream(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, &spec, null, IntPtr.Zero);
            if (_stream == null)
                throw new InvalidOperationException("SDL_OpenAudioDeviceStream failed: " + SDL_GetError());
        }

        public void Play()
        {
            if (_stream != null) SDL_ResumeAudioStreamDevice(_stream);
        }

        public void Stop()
        {
            if (_stream != null) SDL_PauseAudioStreamDevice(_stream);
        }

        public void Shutdown()
        {
            if (_disposed) return;
            _disposed = true;
            if (_stream != null)
            {
                SDL_DestroyAudioStream(_stream);
                _stream = null;
            }
        }

        public void SetVolume(float volume)
        {
            if (_stream != null) SDL_SetAudioStreamGain(_stream, Math.Clamp(volume, 0f, 1f));
        }

        public bool FinishedPlaying()
        {
            if (_stream == null) return true;

            int queued = SDL_GetAudioStreamQueued(_stream);
            double now = _clock.Elapsed.TotalSeconds;

            if (queued < _lastQueued || _lastQueued < 0)
            {
                _lastDrainSeconds = now; // the device consumed something
                if (Stalled) { Stalled = false; _nextDeadline = now; }
            }
            _lastQueued = queued;

            if (queued <= TargetQueuedFrames * AudioFormat.FrameBytes)
                return true;

            if (now - _lastDrainSeconds > StallSeconds)
            {
                if (!Stalled) { Stalled = true; _nextDeadline = now; SDL_ClearAudioStream(_stream); _lastQueued = 0; }
                return now >= _nextDeadline;
            }
            return false;
        }

        public byte[] LockBuffer() => _buffer;

        public void UnlockBuffer(byte[] buffer)
        {
            if (_stream == null || buffer == null) return;
            if (Stalled)
            {
                _nextDeadline += AudioFormat.FrameSeconds;
                double now = _clock.Elapsed.TotalSeconds;
                if (_nextDeadline < now - 0.25) _nextDeadline = now;
                return; // don't pile more data onto a device that isn't draining
            }
            fixed (byte* p = buffer)
                SDL_PutAudioStreamData(_stream, (IntPtr)p, buffer.Length);
        }
    }
}
