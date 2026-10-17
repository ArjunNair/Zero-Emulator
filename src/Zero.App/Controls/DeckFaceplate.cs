using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Zero.App.Controls
{
    /// <summary>What the decks' faceplates are made of.</summary>
    public enum DeckFinish { Black, Silver }

    /// <summary>
    /// The deck's front panel: brushed aluminium, black or silver, with a bevelled edge and a screw
    /// in each corner. Draws behind its child, which lays out inside <see cref="Decorator.Padding"/>.
    /// </summary>
    public sealed class DeckFaceplate : Decorator
    {
        public static readonly StyledProperty<DeckFinish> FinishProperty =
            AvaloniaProperty.Register<DeckFaceplate, DeckFinish>(nameof(Finish));

        public DeckFinish Finish { get => GetValue(FinishProperty); set => SetValue(FinishProperty, value); }

        private const double Corner = 5;

        static DeckFaceplate()
        {
            AffectsRender<DeckFaceplate>(FinishProperty);
        }

        public override void Render(DrawingContext ctx)
        {
            var r = new Rect(Bounds.Size);
            if (r.Width <= 0 || r.Height <= 0) return;
            bool silver = Finish == DeckFinish.Silver;

            var body = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = silver
                    ? new GradientStops
                    {
                        new GradientStop(Color.FromRgb(0xD8, 0xDB, 0xDF), 0),
                        new GradientStop(Color.FromRgb(0xC2, 0xC6, 0xCB), 0.5),
                        new GradientStop(Color.FromRgb(0xA9, 0xAD, 0xB3), 1)
                    }
                    : new GradientStops
                    {
                        new GradientStop(Color.FromRgb(0x2E, 0x30, 0x34), 0),
                        new GradientStop(Color.FromRgb(0x1E, 0x1F, 0x22), 0.5),
                        new GradientStop(Color.FromRgb(0x15, 0x16, 0x18), 1)
                    }
            };
            ctx.DrawRectangle(body, new Pen(silver ? new SolidColorBrush(Color.FromRgb(0x4A, 0x4C, 0x50)) : Brushes.Black, 1),
                r.Deflate(0.5), Corner, Corner);

            // Brushing: grain running edge to edge, as the brush leaves it, lighter and darker by
            // turns. Each line's strength is blended with the last, so the grain drifts rather than
            // flickering line to line, and it is the same every time and on every plate. Silver
            // shows its grain more, in light and shade alike.
            using (ctx.PushClip(new RoundedRect(r.Deflate(1.5), Corner - 1)))
            {
                var rng = new Random(1982);
                double grain = 0;
                for (double y = 2; y < r.Height - 2; y += 1)
                {
                    grain = grain * 0.55 + (rng.NextDouble() * 2 - 1) * 0.45;
                    Color c = grain >= 0
                        ? Color.FromArgb((byte)(grain * (silver ? 70 : 22)), 255, 255, 255)
                        : Color.FromArgb((byte)(-grain * (silver ? 34 : 40)), 0, 0, 0);
                    ctx.DrawLine(new Pen(new SolidColorBrush(c), 1), new Point(0, y + 0.5), new Point(r.Width, y + 0.5));
                }
            }

            // Bevel: a lit top edge and a shadowed bottom one.
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb((byte)(silver ? 200 : 90), 255, 255, 255)), 1),
                new Point(Corner, 1.5), new Point(r.Width - Corner, 1.5));
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb((byte)(silver ? 90 : 160), 0, 0, 0)), 1),
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
