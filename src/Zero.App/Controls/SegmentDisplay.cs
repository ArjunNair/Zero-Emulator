using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Zero.App.Controls
{
    /// <summary>
    /// Seven-segment digits, as in the counter of a cassette deck's display: slightly slanted, every
    /// segment of every position always there, the unlit ones faint. Shows digits, '-' and spaces.
    /// </summary>
    public sealed class SegmentDisplay : Control
    {
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<SegmentDisplay, string>(nameof(Text), "");

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<SegmentDisplay, IBrush>(nameof(Foreground), Brushes.White);

        public static readonly StyledProperty<IBrush> UnlitProperty =
            AvaloniaProperty.Register<SegmentDisplay, IBrush>(nameof(Unlit), Brushes.Transparent);

        /// <summary>How many positions the display has; the text is right-aligned in them.</summary>
        public static readonly StyledProperty<int> DigitsProperty =
            AvaloniaProperty.Register<SegmentDisplay, int>(nameof(Digits), 3);

        public static readonly StyledProperty<double> DigitHeightProperty =
            AvaloniaProperty.Register<SegmentDisplay, double>(nameof(DigitHeight), 20);

        public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public IBrush Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
        /// <summary>The colour of unlit segments.</summary>
        public IBrush Unlit { get => GetValue(UnlitProperty); set => SetValue(UnlitProperty, value); }
        public int Digits { get => GetValue(DigitsProperty); set => SetValue(DigitsProperty, value); }
        public double DigitHeight { get => GetValue(DigitHeightProperty); set => SetValue(DigitHeightProperty, value); }

        static SegmentDisplay()
        {
            AffectsRender<SegmentDisplay>(TextProperty, ForegroundProperty, UnlitProperty, DigitsProperty, DigitHeightProperty);
            AffectsMeasure<SegmentDisplay>(DigitsProperty, DigitHeightProperty);
        }

        private double DigitWidth => DigitHeight * 0.55;
        private double Thickness => DigitHeight * 0.12;
        private double Gap => DigitHeight * 0.18;           // between positions
        private const double Slant = 0.1;                    // forward lean, as on most decks

        protected override Size MeasureOverride(Size availableSize) =>
            new Size(Digits * (DigitWidth + Gap) - Gap + DigitHeight * Slant, DigitHeight);

        // Segments a (top), b (top right), c (bottom right), d (bottom), e (bottom left), f (top left), g (middle).
        private static int Pattern(char c) => c switch
        {
            '0' => 0b0111111, '1' => 0b0000110, '2' => 0b1011011, '3' => 0b1001111, '4' => 0b1100110,
            '5' => 0b1101101, '6' => 0b1111101, '7' => 0b0000111, '8' => 0b1111111, '9' => 0b1101111,
            '-' => 0b1000000,
            _ => 0
        };

        public override void Render(DrawingContext ctx)
        {
            string text = (Text ?? "").PadLeft(Digits);
            if (text.Length > Digits) text = text.Substring(text.Length - Digits);
            var lit = new StreamGeometry();
            var unlit = new StreamGeometry();
            using (StreamGeometryContext l = lit.Open())
            using (StreamGeometryContext u = unlit.Open())
            {
                for (int i = 0; i < Digits; i++)
                {
                    int pattern = Pattern(text[i]);
                    double left = i * (DigitWidth + Gap);
                    for (int seg = 0; seg < 7; seg++)
                        AddSegment((pattern & (1 << seg)) != 0 ? l : u, seg, left);
                }
            }
            ctx.DrawGeometry(Unlit, null, unlit);
            ctx.DrawGeometry(Foreground, null, lit);
        }

        /// <summary>One segment: a bar with pointed ends, leaning by <see cref="Slant"/>.</summary>
        private void AddSegment(StreamGeometryContext g, int seg, double left)
        {
            double w = DigitWidth, h = DigitHeight, t = Thickness, half = t / 2, inset = t * 0.18;
            double mid = h / 2;
            Point[] bar = seg switch
            {
                0 => Horizontal(left, 0 + half, w, t),             // a
                3 => Horizontal(left, h - half, w, t),             // d
                6 => Horizontal(left, mid, w, t),                  // g
                1 => Vertical(left + w - half, 0 + half, mid, t),  // b
                2 => Vertical(left + w - half, mid, h - half, t),  // c
                4 => Vertical(left + half, mid, h - half, t),      // e
                _ => Vertical(left + half, 0 + half, mid, t),      // f
            };
            g.BeginFigure(Lean(bar[0], h), true);
            for (int k = 1; k < bar.Length; k++) g.LineTo(Lean(bar[k], h));
            g.EndFigure(true);

            Point[] Horizontal(double x, double y, double width, double thick)
            {
                double x0 = x + half + inset, x1 = x + width - half - inset;
                return new[]
                {
                    new Point(x0, y), new Point(x0 + half, y - half), new Point(x1 - half, y - half),
                    new Point(x1, y), new Point(x1 - half, y + half), new Point(x0 + half, y + half)
                };
            }

            Point[] Vertical(double x, double y0, double y1, double thick)
            {
                double a = y0 + inset, b = y1 - inset;
                return new[]
                {
                    new Point(x, a), new Point(x + half, a + half), new Point(x + half, b - half),
                    new Point(x, b), new Point(x - half, b - half), new Point(x - half, a + half)
                };
            }
        }

        private Point Lean(Point p, double h) => new Point(p.X + (h - p.Y) * Slant, p.Y);
    }
}
