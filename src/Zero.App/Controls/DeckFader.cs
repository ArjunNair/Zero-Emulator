using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Zero.App.Controls
{
    /// <summary>
    /// A horizontal slide fader, as on a receiver's tone or balance controls: a slot in the faceplate
    /// with a scale printed beside it and a ridged metal cap. Value runs 0..1. Like
    /// <see cref="DeckKnob"/>, only the user's own drags, clicks and wheel turns raise
    /// <see cref="ValueChangedByUser"/>.
    /// </summary>
    public sealed class DeckFader : Control
    {
        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<DeckFader, double>(nameof(Value), coerce: (_, v) => Math.Clamp(v, 0, 1));

        public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

        public event EventHandler<double> ValueChangedByUser;

        private const double CapWidth = 9, Inset = CapWidth / 2 + 1;
        private bool _dragging;
        private double _grabOffset;

        static DeckFader()
        {
            AffectsRender<DeckFader>(ValueProperty);
            FocusableProperty.OverrideDefaultValue<DeckFader>(false);
            CursorProperty.OverrideDefaultValue<DeckFader>(new Cursor(StandardCursorType.SizeWestEast));
        }

        private double Track => Math.Max(1, Bounds.Width - 2 * Inset);
        private double CapCentre => Inset + Value * Track;

        private void SetByUser(double value)
        {
            double before = Value;
            Value = value;
            if (Value != before) ValueChangedByUser?.Invoke(this, Value);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            double x = e.GetPosition(this).X;
            // Grab the cap where it was hit; a click elsewhere on the slot jumps it there first.
            if (Math.Abs(x - CapCentre) > CapWidth / 2) SetByUser((x - Inset) / Track);
            _grabOffset = x - CapCentre;
            _dragging = true;
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (!_dragging) return;
            SetByUser((e.GetPosition(this).X - _grabOffset - Inset) / Track);
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (!_dragging) return;
            _dragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            _dragging = false;
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            double notches = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
            if (notches == 0) return;
            SetByUser(Value + Math.Sign(notches) * 0.05);
            e.Handled = true;
        }

        private static readonly IBrush ScaleBrush = new SolidColorBrush(Color.FromRgb(0x7A, 0x7D, 0x84));

        public override void Render(DrawingContext ctx)
        {
            double w = Bounds.Width, h = Bounds.Height;
            if (w <= 2 * Inset || h <= 0) return;
            double mid = h / 2;

            // Scale ticks above and below the slot.
            var tick = new Pen(ScaleBrush, 0.8);
            for (int i = 0; i <= 10; i++)
            {
                double x = Math.Round(Inset + Track * i / 10) + 0.5;
                double len = i % 5 == 0 ? 3 : 1.8;
                ctx.DrawLine(tick, new Point(x, mid - 3.5 - len), new Point(x, mid - 3.5));
                ctx.DrawLine(tick, new Point(x, mid + 3.5), new Point(x, mid + 3.5 + len));
            }

            // The slot: a dark recess with a lit lower lip.
            var slot = new Rect(Inset - 1, mid - 1.5, Track + 2, 3);
            ctx.DrawRectangle(Brushes.Black, null, slot, 1.5, 1.5);
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), 0.6),
                new Point(slot.Left + 1, slot.Bottom + 0.4), new Point(slot.Right - 1, slot.Bottom + 0.4));

            // Cap: brushed metal with ridges and a centre index line.
            double cx = CapCentre;
            double capH = Math.Min(h - 1, 14);
            var cap = new Rect(cx - CapWidth / 2, mid - capH / 2, CapWidth, capH);
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), null, cap.Translate(new Vector(0, 1.2)), 1.5, 1.5);
            var metal = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0xEE, 0xEF, 0xF1), 0),
                    new GradientStop(Color.FromRgb(0xA9, 0xAC, 0xB1), 0.55),
                    new GradientStop(Color.FromRgb(0x6E, 0x71, 0x77), 1)
                }
            };
            ctx.DrawRectangle(metal, new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x3C, 0x40)), 0.8), cap, 1.5, 1.5);
            var ridge = new Pen(new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)), 0.6);
            for (double y = cap.Top + 2.5; y < cap.Bottom - 2; y += 2)
                ctx.DrawLine(ridge, new Point(cap.Left + 1.5, y), new Point(cap.Right - 1.5, y));
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x1E)), 1.2),
                new Point(cx, cap.Top + 1.5), new Point(cx, cap.Bottom - 1.5));
        }
    }
}
