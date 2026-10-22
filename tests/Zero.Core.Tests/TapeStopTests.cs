using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Zero.Emulation;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.Core.Tests
{
    /// <summary>
    /// Stopping the tape from the deck stops it until Play. With Auto Play on, the deck used to
    /// start it again as soon as the loader went back to polling, skipping on to the next block,
    /// and flash loading must leave a stopped tape alone too.
    /// </summary>
    [Collection("PZXFile static state")]
    public class TapeStopTests
    {
        private enum Phase { Loading, Playing, Stopped, Replayed }

        [Fact]
        public async Task A_stopped_tape_stays_stopped_until_play()
        {
            var settings = new EmulatorSettings();
            settings.Tape.AutoLoad = true;
            settings.Tape.AutoPlay = true;
            settings.Tape.EdgeLoad = true;
            // Flash loading starts off, so the tape plays long enough to be stopped; it goes on once
            // the tape is stopped, to show its handlers leave a stopped tape alone too.
            settings.Tape.FlashLoad = false;
            settings.Tape.RomTraps = true;
            using var session = new EmulatorSession(settings)
            {
                RomDirectory = TestPaths.RomDir,
                AudioFactory = () => new NullAudioOutput()
            };
            string tap = Path.Combine(TestPaths.ProgramsDir, "Demos", "Overscan.tap");

            // On the emulation thread, a step per frame: stop 30 frames into playback, watch the
            // deck for 300 frames, then press Play.
            var outcome = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            Phase phase = Phase.Loading;
            int frames = 0, stoppedAt = 0;
            session.FrameReady += () =>
            {
                var zx = session.Machine;
                frames++;
                switch (phase)
                {
                    case Phase.Loading:
                        if (frames == 5) session.LoadFile(tap);
                        else if (zx.tapeIsPlaying) { phase = Phase.Playing; frames = 0; }
                        else if (frames > 3000) outcome.TrySetResult("the tape never started");
                        break;
                    case Phase.Playing:
                        if (frames == 30)
                        {
                            session.Tape.Stop();
                            session.Tape.FlashLoad = true;
                            stoppedAt = zx.blockCounter;
                            phase = Phase.Stopped;
                            frames = 0;
                        }
                        break;
                    case Phase.Stopped:
                        if (zx.tapeIsPlaying)
                            outcome.TrySetResult($"the tape started again {frames} frames after Stop");
                        else if (zx.blockCounter != stoppedAt)
                            outcome.TrySetResult($"the stopped tape moved from block {stoppedAt} to {zx.blockCounter}");
                        else if (frames == 300)
                        {
                            session.Tape.Play();
                            phase = Phase.Replayed;
                        }
                        break;
                    case Phase.Replayed:
                        outcome.TrySetResult(zx.tapeIsPlaying ? null : "Play did not start the tape");
                        break;
                }
            };
            session.Start();

            Task done = await Task.WhenAny(outcome.Task, Task.Delay(30000));
            Assert.True(done == outcome.Task, "timed out");
            Assert.Null(await outcome.Task);
        }
    }
}
