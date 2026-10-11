using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Zero.App.Controls
{
    /// <summary>
    /// The deck's front panel: black brushed aluminium with a bevelled edge and a screw in each
    /// corner. Draws behind its child, which lays out inside <see cref="Decorator.Padding"/>.
    /// </summary>
    public sealed class DeckFaceplate : Decorator
    {
        private const double Corner = 5;

        public override void Render(DrawingContext ctx)
        {
            var r = new Rect(Bounds.Size);
            if (r.Width <= 0 || r.Height <= 0) return;

            var body = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0x2E, 0x30, 0x34), 0),
                    new GradientStop(Color.FromRgb(0x1E, 0x1F, 0x22), 0.5),
                    new GradientStop(Color.FromRgb(0x15, 0x16, 0x18), 1)
                }
            };
            ctx.DrawRectangle(body, new Pen(Brushes.Black, 1), r.Deflate(0.5), Corner, Corner);

            // Brushing: faint horizontal streaks of varying strength, the same every time.
            using (ctx.PushClip(new RoundedRect(r.Deflate(1.5), Corner - 1)))
            {
                var rng = new Random(1982);
                for (double y = 2; y < r.Height - 2; y += 1)
                {
                    byte a = (byte)rng.Next(0, 14);
                    bool light = rng.Next(2) == 0;
                    var pen = new Pen(new SolidColorBrush(light ? Color.FromArgb(a, 255, 255, 255) : Color.FromArgb((byte)(a * 2), 0, 0, 0)), 1);
                    double x0 = rng.NextDouble() * r.Width * 0.3;
                    double x1 = r.Width - rng.NextDouble() * r.Width * 0.3;
                    ctx.DrawLine(pen, new Point(x0, y + 0.5), new Point(x1, y + 0.5));
                }
            }

            // Bevel: a lit top edge and a shadowed bottom one.
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 1),
                new Point(Corner, 1.5), new Point(r.Width - Corner, 1.5));
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), 1),
                new Point(Corner, r.Height - 1.5), new Point(r.Width - Corner, r.Height - 1.5));

            double inset = 6.5;
            DrawScrew(ctx, new Point(inset, inset), 20);
            DrawScrew(ctx, new Point(r.Width - inset, inset), 75);
            DrawScrew(ctx, new Point(inset, r.Height - inset), 130);
            DrawScrew(ctx, new Point(r.Width - inset, r.Height - inset), 160);
        }

        private static void DrawScrew(DrawingContext ctx, Point c, double slotDegrees)
        {
            const double radius = 2.6;
            var head = new RadialGradientBrush
            {
                GradientOrigin = new RelativePoint(0.35, 0.3, RelativeUnit.Relative),
                Center = new RelativePoint(0.35, 0.3, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0x8A, 0x8D, 0x93), 0),
                    new GradientStop(Color.FromRgb(0x2C, 0x2D, 0x31), 1)
                }
            };
            ctx.DrawEllipse(head, new Pen(Brushes.Black, 0.6), c, radius, radius);
            double a = slotDegrees * Math.PI / 180;
            var d = new Vector(Math.Cos(a), Math.Sin(a)) * (radius * 0.8);
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x12)), 0.9), c - d, c + d);
        }
    }
}
