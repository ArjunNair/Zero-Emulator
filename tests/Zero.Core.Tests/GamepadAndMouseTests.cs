using System.Collections.Generic;
using System.Threading.Tasks;
using Speccy;
using SpeccyCommon;
using Xunit;
using Zero.Emulation;
using Zero.Emulation.Host;
using Zero.Emulation.Input;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.Core.Tests
{
    public class GamepadMappingTests
    {
        [Fact]
        public void Default_mapping_routes_buttons_and_dpad()
        {
            var m = GamepadMapping.Default();
            var raw = new GamepadInput { Connected = true, Buttons = (1u << GamepadButtons.South) | (1u << GamepadButtons.DpadLeft) | (1u << GamepadButtons.Start) };
            var keys = new List<keyCode>();
            GamepadState s = m.Resolve(raw, keys);
            Assert.True(s.Fire1); Assert.True(s.Left); Assert.False(s.Right); Assert.False(s.Fire2);
            Assert.Equal(keyCode.ENTER, Assert.Single(keys));
        }

        [Fact]
        public void Stick_steers_and_custom_key_binding_presses_spectrum_key()
        {
            var m = new GamepadMapping();
            m.Set(GamepadButtons.West, GamepadActions.KeyPrefix + keyCode.SPACE);
            m.Set(GamepadButtons.East, GamepadActions.Fire2);
            m.Set(GamepadButtons.East, GamepadActions.None); // unbinding removes the entry
            var raw = new GamepadInput { Connected = true, LeftX = 20000, LeftY = -20000, Buttons = (1u << GamepadButtons.West) | (1u << GamepadButtons.East) };
            var keys = new List<keyCode>();
            GamepadState s = m.Resolve(raw, keys);
            Assert.True(s.Right); Assert.True(s.Up); Assert.False(s.Fire2);
            Assert.Equal(keyCode.SPACE, Assert.Single(keys));
            Assert.Equal(GamepadActions.None, m.ActionFor(GamepadButtons.East));
        }

        [Fact]
        public void Mapping_round_trips_through_settings_json()
        {
            var s = new EmulatorSettings();
            s.Input.Gamepad2Buttons.Set(GamepadButtons.Guide, GamepadActions.KeyPrefix + keyCode.Q);
            string file = System.IO.Path.GetTempFileName();
            try
            {
                s.Save(file);
                EmulatorSettings back = EmulatorSettings.Load(file);
                Assert.Equal("Key:Q", back.Input.Gamepad2Buttons.ActionFor(GamepadButtons.Guide));
                Assert.Equal(GamepadActions.Fire1, back.Input.Gamepad1Buttons.ActionFor(GamepadButtons.South));
            }
            finally { System.IO.File.Delete(file); }
        }
    }

    internal sealed class FakeGamepads : IGamepadSource
    {
        public GamepadInput State = new GamepadInput { Connected = true };
        public int Count => 1;
        public void Update() { }
        public string GetName(int index) => "Fake pad";
        public GamepadInput Poll(int index) => index == 0 ? State : default;
        public void Dispose() { }
    }

    [Collection("PZXFile static state")]
    public class SessionInputTests
    {
        private static async Task RunFrames(EmulatorSession s, int n)
        {
            long target = s.FrameCount + n;
            while (s.FrameCount < target) await Task.Delay(5);
        }

        [Fact]
        public async Task Gamepad_reaches_kempston_port_and_bound_key()
        {
            var pads = new FakeGamepads();
            var settings = new EmulatorSettings();
            settings.Input.Gamepad1Emulates = (int)zx_spectrum.JoysticksEmulated.KEMPSTON;
            using (var session = new EmulatorSession(settings) { RomDirectory = TestPaths.RomDir, AudioFactory = () => new NullAudioOutput(), Gamepads = pads })
            {
                session.Start();
                await RunFrames(session, 5);
                pads.State = new GamepadInput { Connected = true, LeftX = 30000, Buttons = (1u << GamepadButtons.South) | (1u << GamepadButtons.Start) };
                await RunFrames(session, 3);
                (int joy, bool enter) = await session.InvokeAsync(() =>
                    (session.Machine.joystickState[(int)zx_spectrum.JoysticksEmulated.KEMPSTON], session.Machine.keyBuffer[(int)keyCode.ENTER]));
                Assert.Equal(SpeccyGlobals.JOYSTICK_MOVE_RIGHT | SpeccyGlobals.JOYSTICK_BUTTON_1, joy);
                Assert.True(enter);
                Assert.Equal(pads.State.Buttons, session.LastGamepadInput(0).Buttons);
            }
        }

        [Fact]
        public async Task Mouse_deltas_and_buttons_reach_the_kempston_mouse()
        {
            var settings = new EmulatorSettings();
            settings.Input.EnableKempstonMouse = true;
            using (var session = new EmulatorSession(settings) { RomDirectory = TestPaths.RomDir, AudioFactory = () => new NullAudioOutput() })
            {
                session.Start();
                await RunFrames(session, 5);
                Assert.NotNull(session.KempstonMouseDevice);
                byte x0 = session.KempstonMouseDevice.MouseX, y0 = session.KempstonMouseDevice.MouseY;
                session.Mouse.Move(10, 4);
                session.Mouse.SetButton(MouseState.LeftButton, true);
                await RunFrames(session, 3);
                KempstonMouse m = session.KempstonMouseDevice;
                Assert.Equal((byte)(x0 + 10), m.MouseX);
                Assert.Equal((byte)(y0 - 4), m.MouseY); // Y counts upwards
                Assert.Equal(0xFF & ~0x2, m.MouseButton);
                Assert.Equal(m.MouseButton, await session.InvokeAsync(() => session.Machine.In(64223)));

                settings.Input.EnableKempstonMouse = false;
                session.ApplySettings();
                await RunFrames(session, 2);
                Assert.Null(session.KempstonMouseDevice);
            }
        }
    }
}
