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
