using System;

namespace Zero.Emulation.Host
{
    /// <summary>Digital snapshot of one gamepad, already thresholded from analogue sticks.</summary>
    public struct GamepadState
    {
        public bool Connected;
        public bool Up, Down, Left, Right;
        public bool Fire1, Fire2, Fire3;
    }

    /// <summary>Host-provided gamepad access (SDL3 in the desktop app). Polled once per frame.</summary>
    public interface IGamepadSource : IDisposable
    {
        /// <summary>Pump the underlying API; call once per frame before <see cref="Poll"/>.</summary>
        void Update();

        int Count { get; }

        string GetName(int index);

        GamepadState Poll(int index);
    }
}
