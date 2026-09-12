using System;
using System.Threading;
using Speccy;
using SpeccyCommon;

namespace Zero.Emulation.Input
{
    /// <summary>
    /// Positional key state written by the UI thread and copied into the machine once per frame on
    /// the emulation thread. Keys are Zero's own <see cref="keyCode"/> values, so each shell only
    /// has to translate its toolkit's key enum, not Spectrum matrix rows.
    /// </summary>
    public sealed class KeyboardState
    {
        private readonly bool[] _pressed = new bool[(int)keyCode.LAST];
        private int _version;

        public void SetKey(keyCode key, bool down)
        {
            if (key >= keyCode.LAST) return;
            Volatile.Write(ref _pressed[(int)key], down);
            Interlocked.Increment(ref _version);
        }

        public bool IsDown(keyCode key) => Volatile.Read(ref _pressed[(int)key]);

        public void ReleaseAll()
        {
            Array.Clear(_pressed, 0, _pressed.Length);
            Interlocked.Increment(ref _version);
        }

        /// <summary>Emulation thread: copy into the machine's key buffer.</summary>
        internal void ApplyTo(zx_spectrum zx, bool suppressCursorKeys)
        {
            bool[] target = zx.keyBuffer;
            if (target == null) return;
            int n = Math.Min(target.Length, _pressed.Length);
            for (int i = 0; i < n; i++)
                target[i] = Volatile.Read(ref _pressed[i]);

            if (suppressCursorKeys)
            {
                target[(int)keyCode.LEFT] = false;
                target[(int)keyCode.RIGHT] = false;
                target[(int)keyCode.UP] = false;
                target[(int)keyCode.DOWN] = false;
            }
        }
    }
}
