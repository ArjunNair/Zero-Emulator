using System.Threading;

namespace Zero.Emulation.Input
{
    /// <summary>
    /// Relative mouse movement accumulated by the UI thread (in Spectrum pixels) and consumed once per
    /// frame by the emulation thread, which feeds it to the Kempston mouse interface.
    /// </summary>
    public sealed class MouseState
    {
        private int _dx, _dy, _buttons;

        public const int LeftButton = 1, RightButton = 2;

        public void Move(int dx, int dy)
        {
            Interlocked.Add(ref _dx, dx);
            Interlocked.Add(ref _dy, dy);
        }

        public void SetButton(int button, bool down)
        {
            int old, updated;
            do
            {
                old = _buttons;
                updated = down ? old | button : old & ~button;
            } while (Interlocked.CompareExchange(ref _buttons, updated, old) != old);
        }

        public int Buttons => Volatile.Read(ref _buttons);

        /// <summary>Emulation thread: take the movement accumulated since the last call.</summary>
        internal void Consume(out int dx, out int dy)
        {
            dx = Interlocked.Exchange(ref _dx, 0);
            dy = Interlocked.Exchange(ref _dy, 0);
        }
    }
}
