using Speccy;
using SpeccyCommon;
using Zero.Emulation.Host;

namespace Zero.Emulation.Input
{
    /// <summary>
    /// Feeds digital joystick state into the machine as either Kempston port bits or the key
    /// presses a Sinclair / Cursor interface would generate. Mirrors the WinForms InputSystem.
    /// </summary>
    internal static class JoystickRouter
    {
        public static byte ToKempstonBits(in GamepadState s)
        {
            byte bits = 0;
            if (s.Right) bits |= SpeccyGlobals.JOYSTICK_MOVE_RIGHT;
            else if (s.Left) bits |= SpeccyGlobals.JOYSTICK_MOVE_LEFT;
            if (s.Down) bits |= SpeccyGlobals.JOYSTICK_MOVE_DOWN;
            else if (s.Up) bits |= SpeccyGlobals.JOYSTICK_MOVE_UP;
            if (s.Fire1) bits |= SpeccyGlobals.JOYSTICK_BUTTON_1;
            if (s.Fire2) bits |= SpeccyGlobals.JOYSTICK_BUTTON_2;
            if (s.Fire3) bits |= SpeccyGlobals.JOYSTICK_BUTTON_3;
            return bits;
        }

        /// <summary>Apply one joystick's state for the given emulated interface type.</summary>
        public static void Apply(zx_spectrum zx, KempstonJoystick kempston, int type, in GamepadState s)
        {
            switch ((zx_spectrum.JoysticksEmulated)type)
            {
                case zx_spectrum.JoysticksEmulated.KEMPSTON:
                    byte bits = ToKempstonBits(s);
                    zx.joystickState[type] = bits;
                    kempston?.SetState(bits);
                    break;

                case zx_spectrum.JoysticksEmulated.SINCLAIR2: // keys 1-5
                    Press(zx, keyCode._1, s.Left);
                    Press(zx, keyCode._2, s.Right);
                    Press(zx, keyCode._3, s.Down);
                    Press(zx, keyCode._4, s.Up);
                    Press(zx, keyCode._5, s.Fire1);
                    break;

                case zx_spectrum.JoysticksEmulated.SINCLAIR1: // keys 6-0
                    Press(zx, keyCode._6, s.Left);
                    Press(zx, keyCode._7, s.Right);
                    Press(zx, keyCode._8, s.Down);
                    Press(zx, keyCode._9, s.Up);
                    Press(zx, keyCode._0, s.Fire1);
                    break;

                case zx_spectrum.JoysticksEmulated.CURSOR: // 5 6 7 8 + 0
                    Press(zx, keyCode._5, s.Left);
                    Press(zx, keyCode._8, s.Right);
                    Press(zx, keyCode._6, s.Down);
                    Press(zx, keyCode._7, s.Up);
                    Press(zx, keyCode._0, s.Fire1);
                    break;
            }
        }

        private static void Press(zx_spectrum zx, keyCode key, bool down)
        {
            if (down) zx.keyBuffer[(int)key] = true; // never release a key the keyboard itself holds
        }
    }
}
