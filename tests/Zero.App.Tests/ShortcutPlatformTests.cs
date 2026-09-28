using System.Collections.Generic;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using Zero.Emulation.Settings;

namespace Zero.App.Tests
{
    /// <summary>
    /// Off the Mac the command modifier is Control, which the Spectrum needs as Symbol Shift: the
    /// README's own recipe for LOAD "" is J then Ctrl+P twice. These drive the Windows/Linux branch
    /// of the shortcut handler from a Mac, which is the only place it gets exercised.
    /// </summary>
    public class ShortcutPlatformTests
    {
        public ShortcutPlatformTests()
        {
            MainWindow.SettingsLoader = () =>
            {
                var s = new EmulatorSettings();
                s.Paths.Roms = Zero.TestSupport.TestPaths.RomDir;
                s.Emulation.PauseOnFocusLost = false;
                s.Audio.Mute = true;
                return s;
            };
        }

        /// <summary>The letters whose Symbol Shift pairing types something worth having.</summary>
        public static IEnumerable<object[]> CommandKeys => new[]
        {
            new object[] { Key.O },        // Symbol Shift + O is ;
            new object[] { Key.P },        // Symbol Shift + P is " — LOAD "" needs this one
            new object[] { Key.S },        // NOT
            new object[] { Key.R },        // <
            new object[] { Key.M },        // .
            new object[] { Key.F },        // TO
            new object[] { Key.Q },        // <=
            new object[] { Key.OemComma },
        };

        private static KeyEventArgs Press(Key key, KeyModifiers mods) =>
            new KeyEventArgs { Key = key, KeyModifiers = mods, RoutedEvent = InputElement.KeyDownEvent };

        [AvaloniaTheory]
        [MemberData(nameof(CommandKeys))]
        public void Control_combinations_are_not_shortcuts_off_the_mac(Key key)
        {
            var w = new MainWindow();
            w.Show();
            Assert.False(w.HandleShortcut(Press(key, KeyModifiers.Control), isMac: false));
            w.Close();
        }

        /// <summary>The subset whose action is a harmless toggle, so a test may actually fire it.</summary>
        public static IEnumerable<object[]> HarmlessCommandKeys => new[]
        {
            new object[] { Key.P }, new object[] { Key.M }, new object[] { Key.F }, new object[] { Key.R },
        };

        [AvaloniaTheory]
        [MemberData(nameof(HarmlessCommandKeys))]
        public void The_same_combinations_stay_shortcuts_on_the_mac(Key key)
        {
            var w = new MainWindow();
            w.Show();
            Assert.True(w.HandleShortcut(Press(key, KeyModifiers.Meta), isMac: true));
            w.Close();
        }

        [AvaloniaFact]
        public void Function_keys_are_shortcuts_on_every_platform()
        {
            var w = new MainWindow();
            w.Show();
            foreach (Key key in new[] { Key.F1, Key.F4, Key.F7, Key.F8, Key.F11 })
            {
                Assert.True(w.HandleShortcut(Press(key, KeyModifiers.None), isMac: false), key.ToString());
                Assert.True(w.HandleShortcut(Press(key, KeyModifiers.None), isMac: true), key.ToString());
            }
            w.Close();
        }
    }
}
