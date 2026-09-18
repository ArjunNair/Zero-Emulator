using Avalonia;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Zero.App.Tests
{
    public class ApplicationNameTests
    {
        [AvaloniaFact]
        public void The_application_is_called_Zero_X()
        {
            // Avalonia names an application "Avalonia Application" unless told otherwise, and that is
            // what macOS puts in the menu bar beside the apple and what Linux uses for the window
            // manager class. The packaged .app carries the right name in its Info.plist, so this only
            // shows when the app is run straight from the build -- which is most of the time here.
            Assert.Equal("Zero X", Application.Current?.Name);
        }
    }
}
