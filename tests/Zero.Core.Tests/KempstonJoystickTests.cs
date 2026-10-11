using System;
using System.Linq;
using System.Threading.Tasks;
using Speccy;
using SpeccyCommon;
using Xunit;
using Zero.Emulation;
using Zero.Emulation.Host;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.Core.Tests
{
    /// <summary>
    /// Reads the Kempston joystick through each machine's real port-read path. A fully decoded
    /// interface answers $1F and not $DF; the one-chip interface decodes A5 alone and answers both.
    /// The Kempston mouse sits on the bus alongside it and must not drown it out.
    /// </summary>
    public class KempstonJoystickTests
    {
        // Fire + down + right. Distinctive enough that nothing else on the bus returns it by accident.
        private const byte Held = 0x15;

        public static TheoryData<MachineModel> Machines => new TheoryData<MachineModel>
        {
            MachineModel._48k, MachineModel._128k, MachineModel._128ke, MachineModel._plus3, MachineModel._pentagon,
        };

        private static async Task<(byte Port1F, byte PortDF)> Read(MachineModel model, bool fullDecode, bool mouse)
        {
            var s = new EmulatorSettings();
            s.Emulation.Model = model;
            s.Input.Gamepad1Emulates = (int)zx_spectrum.JoysticksEmulated.KEMPSTON;
            s.Input.KempstonUsesPort1F = fullDecode;
            s.Input.EnableKempstonMouse = mouse;

            using var session = new EmulatorSession(s) { RomDirectory = TestPaths.RomDir, AudioFactory = () => new NullAudioOutput() };
            session.Start();
            long target = session.FrameCount + 30;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (session.FrameCount < target)
            {
                if (sw.ElapsedMilliseconds > 30000) throw new TimeoutException("frames stopped coming");
                await Task.Delay(4);
            }

            return await session.InvokeAsync(() =>
            {
                Assert.Equal(model, session.Model);
                session.Machine.io_devices.OfType<KempstonJoystick>().Single().SetState(Held);
                return (session.Machine.In(0x001F), session.Machine.In(0x00DF));
            });
        }

        [Theory]
        [MemberData(nameof(Machines))]
        public async Task A_fully_decoded_interface_answers_1F_and_not_DF(MachineModel model)
        {
            var (p1f, pdf) = await Read(model, fullDecode: true, mouse: false);
            Assert.Equal(Held, p1f);
            Assert.NotEqual(Held, pdf);
        }

        [Theory]
        [MemberData(nameof(Machines))]
        public async Task The_one_chip_interface_answers_both_1F_and_DF(MachineModel model)
        {
            var (p1f, pdf) = await Read(model, fullDecode: false, mouse: false);
            Assert.Equal(Held, p1f);
            Assert.Equal(Held, pdf);
        }

        [Theory]
        [MemberData(nameof(Machines))]
        public async Task The_mouse_does_not_overwrite_the_joystick(MachineModel model)
        {
            // The mouse is registered after the joystick and returns 0xFF for ports that aren't its
            // own. Read in full that would have reported every direction and fire held.
            var (p1f, _) = await Read(model, fullDecode: true, mouse: true);
            Assert.Equal(Held, p1f);
        }
    }
}
