using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using System.Linq;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using Avalonia.Media;

namespace Zero.App.Tests
{
    /// <summary>Renders a magnified sheet of common controls for eyeballing style changes.</summary>
    public class ControlProbeSheet
    {
        [AvaloniaFact]
        public void Render_probe_sheet()
        {
            var items = new Control[]
            {
                new Button { Content = "Cancel", MinWidth = 90 },
                new Button { Content = "OK", MinWidth = 90, Classes = { "accent" } },
                new Button { Content = "Browse\u2026", MinWidth = 90 },
                new Button { Content = "\u25B6 Play", MinWidth = 90 },
                new CheckBox { Content = "Fast loading" },
                new ComboBox { ItemsSource = new[] { "Kempston" }, SelectedIndex = 0, Width = 160 },
                new TextBox { Text = "roms", Width = 160 },
            };
            var stack = new StackPanel { Spacing = 6, Margin = new Thickness(10) };
            foreach (Control c in items) { c.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left; stack.Children.Add(c); }
            var scaled = new LayoutTransformControl { Child = stack, LayoutTransform = new ScaleTransform(4, 4) };
            var w = new Window { Content = scaled, Width = 900, Height = 1100, Background = Brushes.White };
            w.Show();
            Dispatcher.UIThread.RunJobs();
            w.Measure(new Size(900, 1100)); w.Arrange(new Rect(0, 0, 900, 1100));
            Dispatcher.UIThread.RunJobs();
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using (var bmp = w.CaptureRenderedFrame())
                bmp.Save(System.IO.Path.Combine(MainWindowTests.ScreenshotDir, "probe-sheet.png"));
            w.Close();
        }

    }
}
