using System;

namespace Zero.Emulation.Host
{
    /// <summary>Digital joystick state after mapping: what the emulated interface sees.</summary>
    public struct GamepadState
    {
        public bool Connected;
        public bool Up, Down, Left, Right;
        public bool Fire1, Fire2, Fire3;
    }

    /// <summary>
    /// Raw state of one gamepad in SDL's standard layout: left stick axes and a bitmask of the
    /// SDL_GamepadButton indices (bit n = button n, see <see cref="Zero.Emulation.Input.GamepadButtons"/>).
    /// </summary>
    public struct GamepadInput
    {
        public bool Connected;
        public short LeftX, LeftY;
        public uint Buttons;

        public bool IsDown(int button) => button >= 0 && button < 32 && (Buttons & (1u << button)) != 0;
    }

    /// <summary>Host-provided gamepad access (SDL3 in the desktop app). Polled once per frame on the emulation thread.</summary>
    public interface IGamepadSource : IDisposable
    {
        /// <summary>Pump the underlying API; call once per frame before <see cref="Poll"/>.</summary>
        void Update();

        int Count { get; }

        string GetName(int index);

        GamepadInput Poll(int index);
    }
}
