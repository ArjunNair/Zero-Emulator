using Zero.TestSupport;
using System;
using System.Linq;
using Speccy;
using Xunit;
using Xunit.Abstractions;

namespace Zero.Core.Tests
{
    /// <summary>
    /// Boots every supported machine headless from its ROM and checks the expected firmware
    /// screen appears. This is the acceptance test for the managed FDC stubs: +3 and Pentagon
    /// must still reach their menus with no disk controller behind the ports.
    /// </summary>
    public class MachineBootTests
    {
        private readonly ITestOutputHelper _output;

        public MachineBootTests(ITestOutputHelper output) { _output = output; }

        public static zx_spectrum Create(string model, IAudioOutput audio)
        {
            switch (model)
            {
                case "48k": return new zx_48k(audio, false);
                case "128k": return new zx_128k(audio, false);
                case "128ke": return new zx_128ke(audio, false);
                case "plus3": return new zx_plus3(audio, false);
                case "pentagon": return new Pentagon_128k(audio, false);
                default: throw new ArgumentException(model);
            }
        }

        public static zx_spectrum BootFor(string model, int frames, IAudioOutput audio = null)
        {
            zx_spectrum zx = Create(model, audio ?? new NullAudioOutput());
            Assert.True(zx.LoadROM(TestPaths.RomDir, model + ".rom"), "ROM load failed for " + model);
            zx.emulationSpeed = 1;
            zx.Reset(true);
            zx.Start();
            for (int i = 0; i < frames; i++) {
                zx.Run();
                zx.needsPaint = false; // the host clears this after presenting a frame
            }
            return zx;
        }

        [Theory]
        [InlineData("48k", "© 1982 Sinclair Research Ltd")]
        [InlineData("128k", "128 BASIC")]
        [InlineData("128ke", "128 BASIC")]
        [InlineData("plus3", "+3 BASIC")]
        [InlineData("pentagon", "128 BASIC")]
        public void Boots_to_firmware_screen(string model, string expected)
        {
            zx_spectrum zx = BootFor(model, 300);
            string screen = ScreenText.Dump(zx);
            _output.WriteLine(screen);
            Assert.Contains(expected, screen);
            zx.Shutdown();
        }

        [Fact]
        public void Emulation_pushes_audio_frames()
        {
            var audio = new NullAudioOutput();
            zx_spectrum zx = BootFor("48k", 100, audio);
            // 44100 Hz stereo at 50 fps = 882 stereo samples per frame = one buffer per frame.
            Assert.InRange(audio.FramesSubmitted, 90, 110);
            zx.Shutdown();
        }
    }
}
