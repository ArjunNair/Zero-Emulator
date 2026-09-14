using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Zero.App.Tests
{
    /// <summary>
    /// Avalonia's Button defaults to Stretch content alignment, which parks the label at the top left
    /// of the button instead of centring it. AppStyles corrects that; these tests keep it corrected.
    /// </summary>
    public class ButtonAlignmentTests
    {
        private static (double h, double v) Offsets(Control container, Control label)
        {
            Point p = label.TranslatePoint(new Point(0, 0), container) ?? new Point();
            double left = p.X, right = container.Bounds.Width - p.X - label.Bounds.Width;
            double top = p.Y, bottom = container.Bounds.Height - p.Y - label.Bounds.Height;
            return (left - right, top - bottom);
        }

        private static void Layout(Window w)
        {
            w.Show();
            Dispatcher.UIThread.RunJobs();
            w.Measure(new Size(600, 400));
            w.Arrange(new Rect(0, 0, 600, 400));
            Dispatcher.UIThread.RunJobs();
        }

        [AvaloniaTheory]
        [InlineData("OK", 90)]
        [InlineData("Cancel", 90)]
        [InlineData("Browse…", 90)]
        [InlineData("Defaults", 120)]
        public void Button_label_is_centred(string caption, double minWidth)
        {
            var button = new Button { Content = caption, MinWidth = minWidth };
            var w = new Window { Content = new StackPanel { Children = { button } }, Width = 600, Height = 400 };
            Layout(w);

            TextBlock label = button.GetVisualDescendants().OfType<TextBlock>().Single();
            (double h, double v) = Offsets(button, label);
            Assert.InRange(h, -1.5, 1.5); // horizontally centred
            Assert.InRange(v, -1.5, 1.5); // vertically centred
            w.Close();
        }

        [AvaloniaFact]
        public void Dialog_buttons_are_centred()
        {
            var dialog = new Windows.LoadBinaryWindow(null, false);
            Layout(dialog);
            // Only plain push buttons: CheckBox and RadioButton derive from Button and put their label
            // beside the indicator on purpose.
            foreach (Button b in dialog.GetVisualDescendants().OfType<Button>().Where(b => !(b is Avalonia.Controls.Primitives.ToggleButton)))
            {
                TextBlock label = b.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
                if (label == null || b.Bounds.Width <= 0) continue;
                (double h, double v) = Offsets(b, label);
                Assert.InRange(h, -1.5, 1.5);
                Assert.InRange(v, -1.5, 1.5);
            }
            dialog.Close();
        }
    }
}
