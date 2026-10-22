using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Zero.Emulation;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.Core.Tests
{
    // A real TZX loaded through its signal, not the ROM trap: the ROM reads the header and the
    // program from the pulses the player makes, so their timing and the pauses must be right.
    [Collection("PZXFile static state")]
    public class TzxLoadingTests
    {
        // Quick: with no audio to pace it, the machine runs flat out while the tape plays.
        [Fact]
        public async Task Edge_loading_reads_a_tzx_through_its_signal()
        {
            var settings = new EmulatorSettings();
            settings.Tape.AutoLoad = true;
            settings.Tape.AutoPlay = true;
            settings.Tape.EdgeLoad = true;
            settings.Tape.FlashLoad = false;
            using var session = new EmulatorSession(settings)
            {
                RomDirectory = TestPaths.RomDir,
                AudioFactory = () => new NullAudioOutput()
            };

            // Block 0 is text, 1 a program's header, 2 the program. Once block 3 starts, both loaded.
            string path = Path.Combine(TestPaths.ProgramsDir, "Horizons - Side A.tzx");
            var outcome = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            long frames = 0;
            bool sawHeader = false;
            session.FrameReady += () =>
            {
                var zx = session.Machine;
                if (++frames == 5) session.LoadFile(path);
                if (frames % 25 != 0) return;
                string screen = ScreenText.Dump(zx);
                sawHeader |= screen.Contains("Program:");
                if (screen.Contains("loading error")) outcome.TrySetResult("tape loading error at block " + zx.blockCounter);
                else if (zx.blockCounter >= 3) outcome.TrySetResult(sawHeader ? "" : "the header's name was never shown");
                else if (frames > 50 * 90) outcome.TrySetResult("stuck at block " + zx.blockCounter);
            };
            session.Start();

            Task done = await Task.WhenAny(outcome.Task, Task.Delay(TimeSpan.FromSeconds(120)));
            Assert.True(done == outcome.Task, "timed out");
            Assert.Equal("", await outcome.Task);
        }
    }
}
