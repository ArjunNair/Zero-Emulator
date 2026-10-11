using System.Runtime.CompilerServices;

namespace Zero.App.Tests
{
    internal static class TestDefaults
    {
        /// <summary>
        /// No test may write the real config file (~/Library/Application Support/Zero on macOS). Until
        /// this existed, only the exit-confirmation dialog stood between a test's Close() and a save over
        /// the developer's own settings. Tests that want to see what would be saved swap in a capture.
        /// </summary>
        [ModuleInitializer]
        internal static void KeepTestsAwayFromTheRealConfig() => MainWindow.SettingsSaver = _ => { };
    }
}
