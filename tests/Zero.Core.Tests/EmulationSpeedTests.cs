using System.Threading.Tasks;
using Xunit;
using Zero.Emulation;
using Zero.TestSupport;

namespace Zero.Core.Tests
{
    public class EmulationSpeedTests
    {
        private static async Task<EmulatorSession> Boot(int speed)
        {
            var settings = new Zero.Emulation.Settings.EmulatorSettings();
            settings.Emulation.EmulationSpeed = speed;
            var s = new EmulatorSession(settings) { RomDirectory = TestPaths.RomDir, AudioFactory = () => new NullAudioOutput() };
            s.Start();
            while (s.FrameCount < 5) await Task.Delay(5);
            return s;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(10)]
        public async Task The_machine_runs_one_frame_per_turn_at_1x_and_that_many_above_it(int speed)
        {
            // Above 1x the machine runs several frames for every one it hands to the display, painting
            // only the last of them. A count of what was handed over therefore reads a fraction of
            // what the Spectrum actually did, which is the number anyone means by frames per second.
            using (EmulatorSession s = await Boot(speed))
            {
                long handedOver = s.FrameCount, ran = s.EmulatedFrameCount;
                while (s.FrameCount < handedOver + 40) await Task.Delay(5);
                long turns = s.FrameCount - handedOver;
                long frames = s.EmulatedFrameCount - ran;

                double perTurn = (double)frames / turns;
                Assert.True(perTurn > speed - 0.5 && perTurn < speed + 0.5,
                    $"at {speed}x the machine ran {perTurn:F2} frames per turn of the loop, not {speed} "
                    + $"(the machine says its speed is {s.Machine?.emulationSpeed}, settings say {s.Settings.Emulation.EmulationSpeed})");
            }
        }
    }
}
