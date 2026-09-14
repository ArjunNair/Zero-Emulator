using System;
using Avalonia;
using Avalonia.Headless;
using Zero.App.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Zero.App.Tests
{
    public class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            // Pin the theme: otherwise these tests would render under whatever the developer has
            // configured in their own settings file.
            Environment.SetEnvironmentVariable("ZERO_THEME", Environment.GetEnvironmentVariable("ZERO_THEME") ?? "Fluent");
            return Build();
        }

        private static AppBuilder Build() => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
