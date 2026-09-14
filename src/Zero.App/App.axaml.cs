using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Zero.App
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)
                && Environment.GetEnvironmentVariable("ZERO_NO_MAC_STYLES") != "1")
                Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Zero/")) { Source = new Uri("avares://Zero/Styles/MacStyles.axaml") });
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
