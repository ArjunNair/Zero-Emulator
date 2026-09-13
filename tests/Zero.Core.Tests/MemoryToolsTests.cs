using System.Linq;
using System.Threading.Tasks;
using SpeccyCommon;
using Xunit;
using Zero.Emulation;
using Zero.Emulation.Host;
using Zero.TestSupport;

namespace Zero.Core.Tests
{
    [Collection("PZXFile static state")]
    public class MemoryToolsTests
    {
        private static async Task<EmulatorSession> Boot(MachineModel model)
        {
            var settings = new Zero.Emulation.Settings.EmulatorSettings();
            settings.Emulation.Model = model;
            var s = new EmulatorSession(settings) { RomDirectory = TestPaths.RomDir, AudioFactory = () => new NullAudioOutput() };
            s.Start();
            while (s.FrameCount < 5) await Task.Delay(5);
            return s;
        }

        [Fact]
        public async Task Load_and_save_binary_by_address_round_trip()
        {
            using (EmulatorSession s = await Boot(MachineModel._48k))
            {
                byte[] data = Enumerable.Range(0, 300).Select(i => (byte)(i * 7)).ToArray();
                Assert.Equal(300, await s.LoadBinaryAsync(data, 0x8000));
                Assert.Equal(data, await s.SaveBinaryAsync(0x8000, 300));
                // truncated at the top of memory
                Assert.Equal(10, await s.LoadBinaryAsync(data, 65526));
                Assert.False(s.HasRamBanks);
            }
        }

        [Fact]
        public async Task Load_and_save_by_bank_on_128k()
        {
            using (EmulatorSession s = await Boot(MachineModel._128k))
            {
                Assert.True(s.HasRamBanks);
                byte[] data = Enumerable.Range(0, 10000).Select(i => (byte)(i ^ (i >> 8))).ToArray();
                Assert.Equal(10000, await s.LoadBinaryToBankAsync(data, 3));
                Assert.Equal(data, await s.SaveBankAsync(3, 10000));
            }
        }

        [Fact]
        public async Task Typing_a_keyword_token_reaches_the_basic_editor()
        {
            using (EmulatorSession s = await Boot(MachineModel._48k))
            {
                while (s.FrameCount < 250) await Task.Delay(5);
                s.TypeToken(EmulatorSession.KeywordToken("LOAD"));
                long t = s.FrameCount + 10;
                while (s.FrameCount < t) await Task.Delay(5);
                string screen = await s.InvokeAsync(() => ScreenText.Dump(s.Machine));
                Assert.Contains("LOAD", screen);
            }
        }
    }
}
