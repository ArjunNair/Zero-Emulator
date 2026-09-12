using System;
using Avalonia;

namespace Zero.App
{
    internal static class Program
    {
        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        [STAThread]
        public static int Main(string[] args) =>
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
}
