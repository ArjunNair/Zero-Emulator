using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using Zero.Emulation.Settings;

namespace Zero.App.Tests
{
    /// <summary>
    /// Dialogs size themselves to their content, so a theme with wider metrics (Pipboy's monospace
    /// face, for instance) cannot cut labels off. These tests run under whichever theme ZERO_THEME
    /// names, so the suite is repeated per theme.
    /// </summary>
    public class DialogSizingTests
    {
        private static EmulatorSettings Settings() => new EmulatorSettings();

        public static TheoryData<string> DialogNames => new TheoryData<string> { "Options", "Gamepad", "LoadBinary", "TapeDeck" };

        private static Window Create(string name)
        {
            switch (name)
            {
                case "Options": return new Windows.OptionsWindow(Settings(), null);
                case "Gamepad": return new Windows.GamepadWindow(Settings(), null, 0);
                case "LoadBinary": return new Windows.LoadBinaryWindow(null, false);
                default: return new Windows.TapeDeckWindow(null, null);
            }
        }

        private static void Layout(Window w)
        {
            w.Show();
            Dispatcher.UIThread.RunJobs();
            w.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Dispatcher.UIThread.RunJobs();
        }

        [AvaloniaTheory]
        [MemberData(nameof(DialogNames))]
        public void Dialog_shows_all_of_its_content(string name)
        {
            Window w = Create(name);
            Layout(w);

            var root = w.Content as Control;
            Assert.NotNull(root);
            // DesiredSize includes the root's own margin, so compare against the client area, not its bounds.
            Assert.True(w.ClientSize.Width + 1 >= root.DesiredSize.Width,
                $"{name}: content wants {root.DesiredSize.Width:F0}px of width but the window offers {w.ClientSize.Width:F0}px");

            // Any label narrower than its text is a label the user reads with its tail cut off. The
            // natural width is measured on a copy, because an explicit Width makes DesiredSize agree
            // with the truncated size and hides the problem.
            foreach (TextBlock label in w.GetVisualDescendants().OfType<TextBlock>())
            {
                if (label.Bounds.Width <= 0 || label.TextTrimming != TextTrimming.None) continue;
                if (label.TextWrapping != TextWrapping.NoWrap) continue;
                var natural = new TextBlock
                {
                    Text = label.Text, FontFamily = label.FontFamily, FontSize = label.FontSize,
                    FontWeight = label.FontWeight, FontStyle = label.FontStyle
                };
                natural.Measure(Size.Infinity);
                Assert.True(label.Bounds.Width + 1 >= natural.DesiredSize.Width,
                    $"{name}: '{label.Text}' is {label.Bounds.Width:F0}px wide but its text needs {natural.DesiredSize.Width:F0}px");
            }
            w.Close();
        }

        [AvaloniaFact]
        public void Options_keeps_one_height_across_its_tabs()
        {
            var w = new Windows.OptionsWindow(Settings(), null);
            Layout(w);
            double first = 0;
            for (int tab = 0; tab < w.Tabs.ItemCount; tab++)
            {
                w.Tabs.SelectedIndex = tab;
                Dispatcher.UIThread.RunJobs();
                w.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Dispatcher.UIThread.RunJobs();
                double h = w.Tabs.Bounds.Height;
                if (tab == 0) first = h;
                // A floor on the tab body keeps the window from jumping as tabs are selected.
                Assert.True(Math.Abs(h - first) < 80, $"tab {tab} is {h:F0}px tall against {first:F0}px for the first");
            }
            w.Close();
        }
    }
}
