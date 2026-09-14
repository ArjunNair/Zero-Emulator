using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Zero.App.Styles;
using Zero.Emulation.Settings;

namespace Zero.App
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            // The theme has to be in place before any window is created. ZERO_THEME lets the tests and
            // the preview tooling pin one; otherwise it comes from the user's settings.
            string theme = Environment.GetEnvironmentVariable("ZERO_THEME");
            if (string.IsNullOrWhiteSpace(theme))
            {
                try { theme = EmulatorSettings.Load().Render.UiTheme; }
                catch (Exception) { theme = ThemeCatalog.Fluent; }
            }
            ThemeCatalog.Apply(this, theme);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                string fileToOpen = desktop.Args?.FirstOrDefault(a => !a.StartsWith("-", StringComparison.Ordinal));
                desktop.MainWindow = new MainWindow(fileToOpen);
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
