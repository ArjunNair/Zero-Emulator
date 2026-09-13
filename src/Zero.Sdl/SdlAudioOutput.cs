using System;
using System.Diagnostics;
using SDL;
using Speccy;
using Zero.Emulation.Host;
using static SDL.SDL3;

namespace Zero.Sdl
{
    /// <summary>
    /// Pushes the core's 16-bit stereo 44.1 kHz frames into an SDL3 audio stream.
    ///
    /// Pacing: the device drains the stream at its own rate and <see cref="FinishedPlaying"/> reports
    /// "room for more" once fewer than <see cref="TargetQueuedFrames"/> frames are waiting, so the audio
    /// clock drives the emulation thread (~60 ms latency).
    ///
    /// The core does not wait for audio while a tape is playing or at speeds above 1x. In those
    /// periods <see cref="LockBuffer"/> returns null once <see cref="MaxQueuedFrames"/> are queued and
    /// the extra frames are dropped, exactly like the original DirectSound ring buffer, so latency
    /// stays bounded and the queue never runs away.
    ///
    /// Stall protection: if the queue stops draining for <see cref="StallSeconds"/> while we are
    /// actively waiting on it (device disappeared, no audio session, sandbox) we pace from the wall
    /// clock instead of freezing emulation, and recover as soon as the device consumes data again.
    /// </summary>
    public sealed unsafe class SdlAudioOutput : IAudioOutput
    {
        public const int TargetQueuedFrames = 3;
        public const int MaxQueuedFrames = 6;
        private const double StallSeconds = 0.5;

        private SDL_AudioStream* _stream;
        private readonly byte[] _buffer = new byte[AudioFormat.FrameBytes];
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private bool _disposed;
        private int _lastQueued = -1;
        private double _lastProgressSeconds; // last time we saw the queue drain, or weren't waiting on it
        private double _nextDeadline;

        /// <summary>True while the device is not draining and pacing comes from the wall clock.</summary>
        public bool Stalled { get; private set; }

        /// <summary>Frames discarded because the queue was full (unpaced periods). Diagnostic.</summary>
        public long DroppedFrames { get; private set; }

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
            _lastProgressSeconds = _clock.Elapsed.TotalSeconds;
        }

        public void Play()
        {
            if (_stream != null) SDL_ResumeAudioStreamDevice(_stream);
            _lastProgressSeconds = _clock.Elapsed.TotalSeconds;
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

            if (queued < _lastQueued)
            {
                _lastProgressSeconds = now; // the device consumed something
                if (Stalled) Stalled = false;
            }
            _lastQueued = queued;

            if (queued <= TargetQueuedFrames * AudioFormat.FrameBytes)
            {
                _lastProgressSeconds = now; // not waiting on the device, so it cannot be "stalled"
                return true;
            }

            if (!Stalled && now - _lastProgressSeconds > StallSeconds)
            {
                Stalled = true;
                _nextDeadline = now;
            }

            if (Stalled)
            {
                if (now < _nextDeadline) return false;
                _nextDeadline += AudioFormat.FrameSeconds;
                if (_nextDeadline < now - 0.25) _nextDeadline = now;
                return true;
            }
            return false;
        }

        public byte[] LockBuffer()
        {
            if (_stream == null) return _buffer;
            if (SDL_GetAudioStreamQueued(_stream) > MaxQueuedFrames * AudioFormat.FrameBytes)
            {
                DroppedFrames++;
                return null; // queue full: the core skips this frame's audio
            }
            return _buffer;
        }

        public void UnlockBuffer(byte[] buffer)
        {
            if (_stream == null || buffer == null) return;
            fixed (byte* p = buffer)
                SDL_PutAudioStreamData(_stream, (IntPtr)p, buffer.Length);
        }
    }
}
