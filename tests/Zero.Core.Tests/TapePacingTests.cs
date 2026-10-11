using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Zero.Emulation;
using Zero.Emulation.Host;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.Core.Tests
{
    /// <summary>
    /// A playing tape runs in real time at 1x. The core leaves out its audio wait while the tape
    /// plays, so until the session paced it a tape always loaded flat out, Fast Loading or not.
    /// </summary>
    [Collection("PZXFile static state")]
    public class TapePacingTests
    {
        [Fact]
        public async Task A_playing_tape_runs_in_real_time_at_1x()
        {
            var settings = new EmulatorSettings();
            settings.Tape.FastLoad = false;
            settings.Tape.EdgeLoad = false;
            using var session = new EmulatorSession(settings)
            {
                RomDirectory = TestPaths.RomDir,
                AudioFactory = () => new TimerPacedAudioOutput() // paced on the clock, like a sound card
            };
            session.Start();
            var boot = Stopwatch.StartNew();
            while (session.EmulatedFrameCount < 50 && boot.ElapsedMilliseconds < 10000) await Task.Delay(10);

            string tap = Path.Combine(TestPaths.ProgramsDir, "Demos", "Overscan.tap");
            Assert.True(await session.InvokeAsync(() => session.Tape.Insert(tap)));
            await session.InvokeAsync(session.Tape.Play);

            long start = session.EmulatedFrameCount;
            var clock = Stopwatch.StartNew();
            await Task.Delay(1500);
            double fps = (session.EmulatedFrameCount - start) / clock.Elapsed.TotalSeconds;

            Assert.True(await session.InvokeAsync(() => session.Tape.IsPlaying));
            Assert.InRange(fps, 35, 65);
            // The counter follows the same clock: a second and a half of tape is about one count.
            Assert.InRange(await session.InvokeAsync(() => session.Tape.Counter), 0, 1);
        }
    }
}
