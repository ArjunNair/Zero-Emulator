using System.Collections.Generic;
using Avalonia.Input;
using SpeccyCommon;

namespace Zero.App.Input
{
    /// <summary>
    /// Translates Avalonia key events into Zero's positional <see cref="keyCode"/>s.
    ///
    /// Letters and digits use <see cref="PhysicalKey"/> (keyboard position, layout independent).
    /// Shift is Caps Shift, Ctrl is Symbol Shift, exactly like the original Windows build. PC
    /// punctuation keys are translated into the Symbol Shift + letter combination the Spectrum
    /// uses for that symbol, so typing ",", "-" or '"' works as a PC user expects.
    /// </summary>
    public static class KeyMap
    {
        private static readonly Dictionary<PhysicalKey, keyCode> Direct = new Dictionary<PhysicalKey, keyCode>
        {
            [PhysicalKey.A] = keyCode.A, [PhysicalKey.B] = keyCode.B, [PhysicalKey.C] = keyCode.C, [PhysicalKey.D] = keyCode.D,
            [PhysicalKey.E] = keyCode.E, [PhysicalKey.F] = keyCode.F, [PhysicalKey.G] = keyCode.G, [PhysicalKey.H] = keyCode.H,
            [PhysicalKey.I] = keyCode.I, [PhysicalKey.J] = keyCode.J, [PhysicalKey.K] = keyCode.K, [PhysicalKey.L] = keyCode.L,
            [PhysicalKey.M] = keyCode.M, [PhysicalKey.N] = keyCode.N, [PhysicalKey.O] = keyCode.O, [PhysicalKey.P] = keyCode.P,
            [PhysicalKey.Q] = keyCode.Q, [PhysicalKey.R] = keyCode.R, [PhysicalKey.S] = keyCode.S, [PhysicalKey.T] = keyCode.T,
            [PhysicalKey.U] = keyCode.U, [PhysicalKey.V] = keyCode.V, [PhysicalKey.W] = keyCode.W, [PhysicalKey.X] = keyCode.X,
            [PhysicalKey.Y] = keyCode.Y, [PhysicalKey.Z] = keyCode.Z,
            [PhysicalKey.Digit0] = keyCode._0, [PhysicalKey.Digit1] = keyCode._1, [PhysicalKey.Digit2] = keyCode._2,
            [PhysicalKey.Digit3] = keyCode._3, [PhysicalKey.Digit4] = keyCode._4, [PhysicalKey.Digit5] = keyCode._5,
            [PhysicalKey.Digit6] = keyCode._6, [PhysicalKey.Digit7] = keyCode._7, [PhysicalKey.Digit8] = keyCode._8,
            [PhysicalKey.Digit9] = keyCode._9,
            [PhysicalKey.NumPad0] = keyCode._0, [PhysicalKey.NumPad1] = keyCode._1, [PhysicalKey.NumPad2] = keyCode._2,
            [PhysicalKey.NumPad3] = keyCode._3, [PhysicalKey.NumPad4] = keyCode._4, [PhysicalKey.NumPad5] = keyCode._5,
            [PhysicalKey.NumPad6] = keyCode._6, [PhysicalKey.NumPad7] = keyCode._7, [PhysicalKey.NumPad8] = keyCode._8,
            [PhysicalKey.NumPad9] = keyCode._9,
            [PhysicalKey.Space] = keyCode.SPACE,
            [PhysicalKey.Enter] = keyCode.ENTER, [PhysicalKey.NumPadEnter] = keyCode.ENTER,
            [PhysicalKey.ShiftLeft] = keyCode.SHIFT, [PhysicalKey.ShiftRight] = keyCode.SHIFT,
            [PhysicalKey.ControlLeft] = keyCode.CTRL, [PhysicalKey.ControlRight] = keyCode.CTRL,
            [PhysicalKey.AltLeft] = keyCode.ALT, [PhysicalKey.AltRight] = keyCode.ALT,
            [PhysicalKey.Backspace] = keyCode.BACK, [PhysicalKey.Tab] = keyCode.TAB, [PhysicalKey.CapsLock] = keyCode.CAPS,
            [PhysicalKey.Escape] = keyCode.ESC, [PhysicalKey.Delete] = keyCode.DEL, [PhysicalKey.Insert] = keyCode.INS,
            [PhysicalKey.Home] = keyCode.HOME, [PhysicalKey.End] = keyCode.END,
            [PhysicalKey.PageUp] = keyCode.PGUP, [PhysicalKey.PageDown] = keyCode.PGDOWN,
            [PhysicalKey.ArrowLeft] = keyCode.LEFT, [PhysicalKey.ArrowRight] = keyCode.RIGHT,
            [PhysicalKey.ArrowUp] = keyCode.UP, [PhysicalKey.ArrowDown] = keyCode.DOWN,
            [PhysicalKey.F1] = keyCode.F1, [PhysicalKey.F2] = keyCode.F2, [PhysicalKey.F3] = keyCode.F3, [PhysicalKey.F4] = keyCode.F4,
            [PhysicalKey.F5] = keyCode.F5, [PhysicalKey.F6] = keyCode.F6, [PhysicalKey.F7] = keyCode.F7, [PhysicalKey.F8] = keyCode.F8,
            [PhysicalKey.F9] = keyCode.F9, [PhysicalKey.F10] = keyCode.F10, [PhysicalKey.F11] = keyCode.F11, [PhysicalKey.F12] = keyCode.F12,
        };

        /// <summary>Unshifted punctuation: PC key → Symbol Shift + Spectrum key.</summary>
        private static readonly Dictionary<PhysicalKey, keyCode> SymbolUnshifted = new Dictionary<PhysicalKey, keyCode>
        {
            [PhysicalKey.Comma] = keyCode.N,        // ,
            [PhysicalKey.Period] = keyCode.M,       // .
            [PhysicalKey.Slash] = keyCode.V,        // /
            [PhysicalKey.Semicolon] = keyCode.O,    // ;
            [PhysicalKey.Quote] = keyCode._7,       // '
            [PhysicalKey.Minus] = keyCode.J,        // -
            [PhysicalKey.Equal] = keyCode.L,        // =
            [PhysicalKey.Backquote] = keyCode.X,    // £ (nearest thing to `)
            [PhysicalKey.NumPadMultiply] = keyCode.B,
            [PhysicalKey.NumPadAdd] = keyCode.K,
            [PhysicalKey.NumPadSubtract] = keyCode.J,
            [PhysicalKey.NumPadDivide] = keyCode.V,
            [PhysicalKey.NumPadDecimal] = keyCode.M,
        };

        /// <summary>Shifted punctuation and digits: PC key → Symbol Shift + Spectrum key.</summary>
        private static readonly Dictionary<PhysicalKey, keyCode> SymbolShifted = new Dictionary<PhysicalKey, keyCode>
        {
            [PhysicalKey.Digit1] = keyCode._1,      // !
            [PhysicalKey.Digit2] = keyCode._2,      // @
            [PhysicalKey.Digit3] = keyCode._3,      // #
            [PhysicalKey.Digit4] = keyCode._4,      // $
            [PhysicalKey.Digit5] = keyCode._5,      // %
            [PhysicalKey.Digit6] = keyCode.H,       // ^
            [PhysicalKey.Digit7] = keyCode._6,      // &
            [PhysicalKey.Digit8] = keyCode.B,       // *
            [PhysicalKey.Digit9] = keyCode._8,      // (
            [PhysicalKey.Digit0] = keyCode._9,      // )
            [PhysicalKey.Minus] = keyCode._0,       // _
            [PhysicalKey.Equal] = keyCode.K,        // +
            [PhysicalKey.Comma] = keyCode.R,        // <
            [PhysicalKey.Period] = keyCode.T,       // >
            [PhysicalKey.Slash] = keyCode.C,        // ?
            [PhysicalKey.Semicolon] = keyCode.Z,    // :
            [PhysicalKey.Quote] = keyCode.P,        // "
        };

        /// <summary>
        /// Resolve a key event to the Spectrum keys it presses. Returns false for keys the emulator
        /// doesn't know. <paramref name="symbolShift"/> is true when Symbol Shift must be held as well;
        /// <paramref name="dropCapsShift"/> when the PC Shift should NOT be forwarded (it was only
        /// used to select a punctuation symbol).
        /// </summary>
        public static bool TryMap(PhysicalKey physical, bool shiftHeld, out keyCode key, out bool symbolShift, out bool dropCapsShift)
        {
            symbolShift = false;
            dropCapsShift = false;

            if (shiftHeld && SymbolShifted.TryGetValue(physical, out key))
            {
                symbolShift = true;
                dropCapsShift = true;
                return true;
            }
            if (SymbolUnshifted.TryGetValue(physical, out key))
            {
                symbolShift = true;
                return true;
            }
            return Direct.TryGetValue(physical, out key);
        }
    }
}
