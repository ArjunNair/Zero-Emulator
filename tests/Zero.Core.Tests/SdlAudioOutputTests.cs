using System;
using System.Diagnostics;
using System.Threading;
using Xunit;
using Zero.Emulation.Host;
using Zero.Sdl;

namespace Zero.Core.Tests
{
    /// <summary>Needs a real audio device; the test passes trivially where none exists (CI).</summary>
    public class SdlAudioOutputTests
    {
        private static SdlAudioOutput TryOpen()
        {
            try { return new SdlAudioOutput(); }
            catch (Exception) { return null; }
        }

        [Fact]
        public void Paces_at_frame_rate_and_never_stalls_on_a_live_device()
        {
            SdlAudioOutput audio = TryOpen();
            if (audio == null) return;
            try
            {
                audio.SetVolume(0f);
                audio.Play();
                var sw = Stopwatch.StartNew();
                int pushed = 0;
                while (sw.ElapsedMilliseconds < 1000)
                {
                    if (audio.FinishedPlaying()) { byte[] b = audio.LockBuffer(); if (b != null) { audio.UnlockBuffer(b); pushed++; } }
                    else Thread.Sleep(1);
                }
                Assert.InRange(pushed, 40, 65); // ~50 frames per second
                Assert.False(audio.Stalled);
            }
            finally { audio.Shutdown(); }
        }

        [Fact]
        public void Unpaced_bursts_are_capped_and_sound_resumes_afterwards()
        {
            SdlAudioOutput audio = TryOpen();
            if (audio == null) return;
            try
            {
                audio.SetVolume(0f);
                audio.Play();
                // Tape loading / turbo: the core submits frames without ever asking FinishedPlaying.
                int accepted = 0;
                for (int i = 0; i < 200; i++) { byte[] b = audio.LockBuffer(); if (b != null) { audio.UnlockBuffer(b); accepted++; } }
                Assert.InRange(accepted, 1, SdlAudioOutput.MaxQueuedFrames + 2);
                Assert.True(audio.DroppedFrames > 100);

                // Back at 1x: within a few frames the queue drains to the target and pacing resumes, no stall.
                var sw = Stopwatch.StartNew();
                while (!audio.FinishedPlaying() && sw.ElapsedMilliseconds < 500) Thread.Sleep(1);
                Assert.True(audio.FinishedPlaying());
                Assert.False(audio.Stalled);
            }
            finally { audio.Shutdown(); }
        }
    }
}
