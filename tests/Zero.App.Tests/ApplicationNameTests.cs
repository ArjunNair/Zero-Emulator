using Avalonia;
using Avalonia.Headless.XUnit;
using Xunit;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.App.Tests
{
    public class ApplicationNameTests
    {
        public ApplicationNameTests()
        {
            MainWindow.SettingsLoader = () =>
            {
                var s = new EmulatorSettings();
                s.Paths.Roms = TestPaths.RomDir;
                s.Emulation.PauseOnFocusLost = false;
                s.Audio.Mute = true;
                return s;
            };
        }

        [AvaloniaFact]
        public void The_application_is_called_Zero_X()
        {
            // Avalonia names an application "Avalonia Application" unless told otherwise, and that is
            // what macOS puts in the menu bar beside the apple and what Linux uses for the window
            // manager class. The packaged .app carries the right name in its Info.plist, so this only
            // shows when the app is run straight from the build -- which is most of the time here.
            Assert.Equal("Zero X", Application.Current?.Name);
        }

        [AvaloniaFact]
        public void The_window_is_titled_the_same_as_the_application()
        {
            // The name in the menu bar and the name on the window came from different places and
            // drifted apart once before: Avalonia's default sat in one while the other said Zero.
            var w = new MainWindow();
            w.Show();
            Assert.Equal(Application.Current?.Name, w.Title);
            w.Close();
        }
    }
}
