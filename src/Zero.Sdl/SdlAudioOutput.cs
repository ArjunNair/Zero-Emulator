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
            return SDL_GetAudioStreamQueued(_stream) <= TargetQueuedFrames * AudioFormat.FrameBytes;
        }

        public byte[] LockBuffer() => _buffer;

        public void UnlockBuffer(byte[] buffer)
        {
            if (_stream == null || buffer == null) return;
            fixed (byte* p = buffer)
                SDL_PutAudioStreamData(_stream, (IntPtr)p, buffer.Length);
        }
    }
}
