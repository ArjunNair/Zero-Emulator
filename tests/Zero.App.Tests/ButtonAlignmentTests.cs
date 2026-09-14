using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Zero.App.Tests
{
    /// <summary>
    /// Avalonia's Button defaults to Stretch content alignment: the label is stretched across the
    /// content box and its glyphs drawn at that box's top left, so the caption looks high and left.
    /// AppStyles centres it; these tests keep it centred.
    ///
    /// Note the label is checked for being unstretched as well as centred. A stretched label still
    /// reports a centred *box*, so measuring position alone would not catch a regression.
    /// </summary>
    public class ButtonAlignmentTests
    {
        // Tolerances are in (top - bottom) gap difference, so twice the actual offset. A theme may
        // seat the label a pixel off the geometric centre on purpose, to allow for a bevel or for the
        // ink of a caption without descenders.
        private const double MaxAbove = 2.5, MaxBelow = 3.5;

        private static void Layout(Window w)
        {
            w.Show();
            Dispatcher.UIThread.RunJobs();
            w.Measure(new Size(600, 500));
            w.Arrange(new Rect(0, 0, 600, 500));
            Dispatcher.UIThread.RunJobs();
        }

        private static string Check(Button button)
        {
            TextBlock label = button.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
            if (label == null || button.Bounds.Width <= 0) return null;

            double stretchX = label.Bounds.Width - label.DesiredSize.Width;
            double stretchY = label.Bounds.Height - label.DesiredSize.Height;
            if (stretchX > 1 || stretchY > 1)
                return $"label is stretched ({label.Bounds.Width:F0}x{label.Bounds.Height:F0} vs natural {label.DesiredSize.Width:F0}x{label.DesiredSize.Height:F0}), so its text sits in the corner";

            Point p = label.TranslatePoint(new Point(0, 0), button) ?? default;
            double h = p.X - (button.Bounds.Width - p.X - label.Bounds.Width);
            double v = p.Y - (button.Bounds.Height - p.Y - label.Bounds.Height);
            if (System.Math.Abs(h) > 1.5) return $"label is {h / 2:F1}px off centre horizontally";
            if (v < -MaxAbove) return $"label sits {-v / 2:F1}px above centre";
            if (v > MaxBelow) return $"label sits {v / 2:F1}px below centre";
            return null;
        }

        [AvaloniaTheory]
        [InlineData("OK", 90)]
        [InlineData("Cancel", 90)]
        [InlineData("Browse…", 90)]
        [InlineData("Defaults", 120)]
        public void Button_label_is_centred(string caption, double minWidth)
        {
            var button = new Button { Content = caption, MinWidth = minWidth };
            var w = new Window { Content = new StackPanel { Children = { button } }, Width = 600, Height = 500 };
            Layout(w);
            Assert.Null(Check(button));
            w.Close();
        }

        [AvaloniaFact]
        public void Stretched_content_would_be_reported()
        {
            // Proves the check has teeth: this is exactly the state the app was in before AppStyles.
            var button = new Button
            {
                Content = "Cancel", MinWidth = 90,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch
            };
            var w = new Window { Content = new StackPanel { Children = { button } }, Width = 600, Height = 500 };
            Layout(w);
            Assert.Contains("stretched", Check(button));
            w.Close();
        }

        [AvaloniaFact]
        public void Dialog_buttons_are_centred()
        {
            var dialog = new Windows.LoadBinaryWindow(null, false);
            Layout(dialog);
            // Only plain push buttons: CheckBox and RadioButton derive from Button and put their label
            // beside the indicator on purpose.
            foreach (Button b in dialog.GetVisualDescendants().OfType<Button>()
                         .Where(b => !(b is Avalonia.Controls.Primitives.ToggleButton)))
            {
                string problem = Check(b);
                Assert.True(problem == null, $"'{(b.Content as string) ?? b.Name}': {problem}");
            }
            dialog.Close();
        }
    }

    /// <summary>Fields in a dialog should share one height; a control that keeps Fluent's touch sizing towers over its neighbours.</summary>
    public class FieldSizingTests
    {
        [AvaloniaFact]
        public void Options_fields_share_a_common_height()
        {
            var w = new Windows.OptionsWindow(new Zero.Emulation.Settings.EmulatorSettings(), null);
            w.Show();
            for (int tab = 0; tab < w.Tabs.ItemCount; tab++)
            {
                w.Tabs.SelectedIndex = tab;
                Dispatcher.UIThread.RunJobs();
                w.Measure(new Size(600, 500));
                w.Arrange(new Rect(0, 0, 600, 500));
                Dispatcher.UIThread.RunJobs();

                var heights = w.GetVisualDescendants().OfType<Control>()
                    .Where(c => (c is TextBox || c is ComboBox) && c.Bounds.Height > 0 && c.TemplatedParent == null)
                    .Select(c => c.Bounds.Height)
                    .Distinct()
                    .ToList();
                if (heights.Count == 0) continue;
                Assert.True(heights.Max() - heights.Min() <= 1.0,
                    $"tab {tab}: field heights differ: {string.Join(", ", heights.Select(h => h.ToString("F0")))}");
            }
            w.Close();
        }

        [AvaloniaTheory]
        [InlineData("32768", 32768)]
        [InlineData("0x8000", 32768)]
        [InlineData("$8000", 32768)]
        [InlineData("0x1B00", 6912)]
        [InlineData("  1b00  ", -1)]
        [InlineData("", -1)]
        [InlineData("fish", -1)]
        [InlineData("-4", -1)]
        public void Binary_dialog_parses_decimal_and_hex(string text, int expected)
        {
            bool ok = Windows.LoadBinaryWindow.TryParseNumber(text, out int value);
            if (expected < 0) Assert.False(ok);
            else { Assert.True(ok); Assert.Equal(expected, value); }
        }
    }
}
