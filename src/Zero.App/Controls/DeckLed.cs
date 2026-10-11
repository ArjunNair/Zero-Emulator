using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Zero.App.Controls
{
    /// <summary>
    /// A front-panel LED in the style of an old VCR: a domed lens in a dark bezel that glows when
    /// lit and can blink, the way a VCR flashes its pause light.
    /// </summary>
    public sealed class DeckLed : Control
    {

        public static readonly StyledProperty<bool> IsLitProperty =
            AvaloniaProperty.Register<DeckLed, bool>(nameof(IsLit));

        public static readonly StyledProperty<bool> BlinkProperty =
            AvaloniaProperty.Register<DeckLed, bool>(nameof(Blink));

        public static readonly StyledProperty<Color> ColorProperty =
            AvaloniaProperty.Register<DeckLed, Color>(nameof(Color), Color.FromRgb(0x4C, 0xFF, 0x6A));

        public bool IsLit { get => GetValue(IsLitProperty); set => SetValue(IsLitProperty, value); }
        public bool Blink { get => GetValue(BlinkProperty); set => SetValue(BlinkProperty, value); }
        public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

        // One clock for every LED, so blinking lights flash in step like they share a driver.
        private static readonly DispatcherTimer BlinkClock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        private static bool _blinkPhase = true;
        private static event Action BlinkTick;

        static DeckLed()
        {
            AffectsRender<DeckLed>(IsLitProperty, BlinkProperty, ColorProperty);
            BlinkClock.Tick += (_, __) => { _blinkPhase = !_blinkPhase; BlinkTick?.Invoke(); };
        }

        public DeckLed()
        {
            Width = 10;
            Height = 10;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            BlinkTick += OnBlinkTick;
            if (!BlinkClock.IsEnabled) BlinkClock.Start();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            BlinkTick -= OnBlinkTick;
            if (BlinkTick == null) BlinkClock.Stop();
            base.OnDetachedFromVisualTree(e);
        }

        private void OnBlinkTick() { if (Blink && IsLit) InvalidateVisual(); }

        /// <summary>Whether the lens is glowing right now, blink phase included.</summary>
        public bool IsGlowing => IsLit && (!Blink || _blinkPhase);

        public override void Render(DrawingContext ctx)
        {
            double size = Math.Min(Bounds.Width, Bounds.Height);
            if (size <= 0) return;
            var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
            double r = size / 2;
            Color col = Color;
            bool on = IsGlowing;

            // Halo first, so it spills onto the faceplate around the bezel.
            if (on)
            {
                var halo = new RadialGradientBrush
                {
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb(150, col.R, col.G, col.B), 0),
                        new GradientStop(Color.FromArgb(0, col.R, col.G, col.B), 1)
                    }
                };
                ctx.DrawEllipse(halo, null, c, r * 2.1, r * 2.1);
            }

            // Bezel.
            ctx.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0B)),
                new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x3B, 0x3F)), 0.8), c, r, r);

            // Lens: bright core when lit, a dark tinted dome when off.
            Color core = on ? Lerp(col, Colors.White, 0.55) : Lerp(col, Colors.Black, 0.72);
            Color edge = on ? col : Lerp(col, Colors.Black, 0.88);
            var lens = new RadialGradientBrush
            {
                Center = new RelativePoint(0.45, 0.4, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.45, 0.4, RelativeUnit.Relative),
                GradientStops = { new GradientStop(core, 0), new GradientStop(edge, 1) }
            };
            double lr = r * 0.72;
            ctx.DrawEllipse(lens, null, c, lr, lr);

            // Specular glint on the dome.
            ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(on ? (byte)200 : (byte)70, 255, 255, 255)), null,
                new Point(c.X - lr * 0.32, c.Y - lr * 0.36), lr * 0.28, lr * 0.2);
        }

        internal static Color Lerp(Color a, Color b, double t) => Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    }
}
