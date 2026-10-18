using System.IO;
using System.Threading.Tasks;
using Xunit;
using Zero.Emulation;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.Core.Tests
{
    /// <summary>
    /// Auto-load hard-resets the machine and types LOAD "" once the machine says its reset is over,
    /// which it decides by counting interrupts after the ROM's RAM check. A tape opened while the
    /// machine was still counting used to carry that count into the new boot, so the reset was
    /// declared over at the first interrupt, LOAD "" went to a ROM still clearing the screen and
    /// the tape never started.
    /// </summary>
    [Collection("PZXFile static state")]
    public class AutoLoadTests
    {
        /// <summary>Under the 40 interrupts the machine waits at least, so no boot here may finish.</summary>
        private const int FramesAfterEi = 30;
        private const int Reloads = 4;

        private enum Phase { WaitingForReset, Booting, Counting, Watching }

        [Fact]
        public async Task Reopening_a_tape_while_the_machine_boots_still_autoloads_it()
        {
            var settings = new EmulatorSettings();
            settings.Tape.AutoLoad = true;
            using var session = new EmulatorSession(settings)
            {
                RomDirectory = TestPaths.RomDir,
                AudioFactory = () => new NullAudioOutput()
            };
            string tap = Path.Combine(TestPaths.ProgramsDir, "Demos", "Overscan.tap");

            // Everything below runs on the emulation thread, one step per frame, so each load lands
            // on an exact frame of the boot whatever the host's speed.
            var outcome = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            Phase phase = Phase.Booting;
            int loads = 0, frames = 0;
            session.FrameReady += () =>
            {
                var zx = session.Machine;
                frames++;
                switch (phase)
                {
                    case Phase.WaitingForReset: // the load's hard reset puts the ROM back in its RAM check
                        if (!zx.cpu.iff_1) phase = Phase.Booting;
                        break;
                    case Phase.Booting: // interrupts come on once the RAM check is done
                        if (zx.cpu.iff_1) { phase = Phase.Counting; frames = 0; }
                        break;
                    case Phase.Counting:
                        if (zx.isResetOver)
                            outcome.TrySetResult($"boot {loads + 1} was declared over {frames} frames after the RAM check");
                        else if (frames == FramesAfterEi)
                        {
                            session.LoadFile(tap);
                            phase = ++loads > Reloads ? Phase.Watching : Phase.WaitingForReset;
                            frames = 0;
                        }
                        break;
                    case Phase.Watching:
                        if (session.Tape.IsPlaying) outcome.TrySetResult(null);
                        else if (frames > 1000) outcome.TrySetResult("LOAD \"\" was never typed: the tape did not start");
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
