using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;

namespace Zero.App.Controls
{
    /// <summary>
    /// A rotary knob in the style of an A/V receiver: a knurled skirt under a spun-aluminium cap with
    /// a black pointer. Continuous by default (a volume pot); give it <see cref="Positions"/> and
    /// it becomes a detented selector with a printed legend at each stop.
    ///
    /// Drag up or right to turn it clockwise, or use the wheel. A click on a selector without a drag
    /// steps it to the next position, wrapping round, since a knob is not obviously draggable.
    /// Only those gestures raise <see cref="ValueChangedByUser"/>, so the owner can set
    /// <see cref="Value"/> to reflect the current settings without echoing them back.
    /// </summary>
    public sealed class DeckKnob : Control
    {
        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<DeckKnob, double>(nameof(Value), coerce: CoerceValue);

        public static readonly StyledProperty<double> MinimumProperty =
            AvaloniaProperty.Register<DeckKnob, double>(nameof(Minimum));

        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<DeckKnob, double>(nameof(Maximum), 1.0);

        public static readonly StyledProperty<double> SmallChangeProperty =
            AvaloniaProperty.Register<DeckKnob, double>(nameof(SmallChange), 0.05);

        public static readonly StyledProperty<bool> CommitOnReleaseProperty =
            AvaloniaProperty.Register<DeckKnob, bool>(nameof(CommitOnRelease));

        public static readonly StyledProperty<string> PositionsProperty =
            AvaloniaProperty.Register<DeckKnob, string>(nameof(Positions));

        public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        /// <summary>How far one wheel notch turns a continuous knob.</summary>
        public double SmallChange { get => GetValue(SmallChangeProperty); set => SetValue(SmallChangeProperty, value); }

        /// <summary>
        /// Comma-separated legends for a detented selector, e.g. "MONO,ACB,ABC". The value is then
        /// the index of the selected stop and Minimum/Maximum are ignored.
        /// </summary>
        public string Positions { get => GetValue(PositionsProperty); set => SetValue(PositionsProperty, value); }

        /// <summary>
        /// For a knob whose every change is costly (the machine selector resets the Spectrum): a drag
        /// turns it freely but reports only the stop it is let go on, and the wheel does nothing,
        /// so scrolling past it cannot change anything.
        /// </summary>
        public bool CommitOnRelease { get => GetValue(CommitOnReleaseProperty); set => SetValue(CommitOnReleaseProperty, value); }

        /// <summary>True while the user has hold of the knob; the owner should not move it then.</summary>
        public bool IsInteracting => _dragging;

        /// <summary>Raised when the user turns the knob; never for a programmatic change.</summary>
        public event EventHandler<double> ValueChangedByUser;

        private string[] _legends = Array.Empty<string>();
        private bool _dragging, _dragged;
        private Point _pressPoint;
        private double _pressValue;

        // Pixels of drag per full sweep (continuous) and per stop (selector).
        private const double DragSpan = 160, DragPerStop = 22;

        static DeckKnob()
        {
            AffectsRender<DeckKnob>(ValueProperty, MinimumProperty, MaximumProperty, PositionsProperty);
            FocusableProperty.OverrideDefaultValue<DeckKnob>(false);
            CursorProperty.OverrideDefaultValue<DeckKnob>(new Cursor(StandardCursorType.SizeNorthSouth));
        }

        private static double CoerceValue(AvaloniaObject o, double v)
        {
            var k = (DeckKnob)o;
            if (k.IsSelector) return Math.Clamp(Math.Round(v), 0, k._legends.Length - 1);
            return Math.Clamp(v, Math.Min(k.Minimum, k.Maximum), Math.Max(k.Minimum, k.Maximum));
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == PositionsProperty)
            {
                _legends = string.IsNullOrWhiteSpace(Positions)
                    ? Array.Empty<string>()
                    : Positions.Split(',', StringSplitOptions.TrimEntries);
                CoerceValue(ValueProperty);
            }
            else if (change.Property == MinimumProperty || change.Property == MaximumProperty)
                CoerceValue(ValueProperty);
        }

        public bool IsSelector => _legends.Length > 1;

        /// <summary>Total swing of the pointer in degrees: 270 for a pot, 60 per stop for a selector.</summary>
        private double Sweep => IsSelector ? Math.Min(270, (_legends.Length - 1) * 60) : 270;

        /// <summary>Where the value sits along the sweep, 0..1.</summary>
        private double Fraction
        {
            get
            {
                if (IsSelector) return Value / (_legends.Length - 1);
                double span = Maximum - Minimum;
                return span == 0 ? 0 : (Value - Minimum) / span;
            }
        }

        /// <summary>Pointer angle in degrees, clockwise from twelve o'clock.</summary>
        private double AngleFor(double fraction) => -Sweep / 2 + Sweep * fraction;

        // ------------------------------------------------------------------ interaction

        private void SetByUser(double value)
        {
            double before = Value;
            Value = value;
            if (Value != before && !(CommitOnRelease && _dragging)) ValueChangedByUser?.Invoke(this, Value);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            _dragging = true;
            _dragged = false;
            _pressPoint = e.GetPosition(this);
            _pressValue = Value;
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (!_dragging) return;
            Point p = e.GetPosition(this);
            double travel = (_pressPoint.Y - p.Y) + (p.X - _pressPoint.X);
            if (!_dragged && Math.Abs(travel) < 3) return;
            _dragged = true;
            if (IsSelector) SetByUser(_pressValue + Math.Round(travel / DragPerStop));
            else SetByUser(_pressValue + travel / DragSpan * (Maximum - Minimum));
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (!_dragging) return;
            _dragging = false;
            e.Pointer.Capture(null);
            if (!_dragged && IsSelector) SetByUser((Value + 1) % _legends.Length);
            else if (CommitOnRelease && Value != _pressValue) ValueChangedByUser?.Invoke(this, Value);
            e.Handled = true;
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            if (_dragging && CommitOnRelease) Value = _pressValue; // abandoned: put it back
            _dragging = false;
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            double notches = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
            if (notches == 0 || CommitOnRelease) return;
            double step = IsSelector ? 1 : SmallChange;
            SetByUser(Value + Math.Sign(notches) * step);
            e.Handled = true;
        }

        // ------------------------------------------------------------------ drawing

        private static readonly IBrush TickBrush = new SolidColorBrush(Color.FromRgb(0x8C, 0x8F, 0x96));
        private static readonly IBrush LegendBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0x9D, 0xA3));
        private static readonly IBrush LegendLitBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x4A));
        private static readonly Color PointerColor = Color.FromRgb(0x14, 0x14, 0x16);

        private static Point OnCircle(Point c, double radius, double degrees)
        {
            double a = degrees * Math.PI / 180;
            return new Point(c.X + radius * Math.Sin(a), c.Y - radius * Math.Cos(a));
        }

        public override void Render(DrawingContext ctx)
        {
            double size = Math.Min(Bounds.Width, Bounds.Height);
            if (size <= 0) return;
            var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
            // Selectors keep a ring outside the skirt for their legends; pots only need their ticks.
            double r = size / 2 - (IsSelector ? 14 : 6);
            if (r < 4) return;

            DrawScale(ctx, c, r);

            // Drop shadow onto the faceplate.
            ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), null, new Point(c.X, c.Y + 1.6), r + 0.8, r + 0.8);

            // Knurled skirt.
            var skirt = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0x6A, 0x6D, 0x73), 0),
                    new GradientStop(Color.FromRgb(0x2A, 0x2B, 0x2F), 0.6),
                    new GradientStop(Color.FromRgb(0x16, 0x17, 0x19), 1)
                }
            };
            ctx.DrawEllipse(skirt, new Pen(Brushes.Black, 0.8), c, r, r);
            var grip = new Pen(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)), 0.7);
            int ridges = Math.Max(24, (int)(r * 3));
            for (int i = 0; i < ridges; i++)
            {
                double deg = 360.0 * i / ridges;
                ctx.DrawLine(grip, OnCircle(c, r * 0.8, deg), OnCircle(c, r - 0.6, deg));
            }

            // Spun-aluminium cap: a diagonal sheen plus faint machining rings.
            double cap = r * 0.76;
            var capBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.15, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.85, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(0xF2, 0xF3, 0xF5), 0),
                    new GradientStop(Color.FromRgb(0xB9, 0xBC, 0xC1), 0.45),
                    new GradientStop(Color.FromRgb(0xD5, 0xD7, 0xDA), 0.6),
                    new GradientStop(Color.FromRgb(0x7E, 0x81, 0x87), 1)
                }
            };
            ctx.DrawEllipse(capBrush, new Pen(new SolidColorBrush(Color.FromRgb(0x4A, 0x4C, 0x51)), 0.8), c, cap, cap);
            var ring = new Pen(new SolidColorBrush(Color.FromArgb(28, 0, 0, 0)), 0.6);
            for (double rr = cap * 0.2; rr < cap; rr += Math.Max(1.4, cap / 9))
                ctx.DrawEllipse(null, ring, c, rr, rr);

            // Pointer.
            double angle = AngleFor(Fraction);
            var pointer = new Pen(new SolidColorBrush(PointerColor), Math.Max(0.7, r * 0.035), lineCap: PenLineCap.Round);
            ctx.DrawLine(pointer, OnCircle(c, cap * 0.35, angle), OnCircle(c, cap * 0.86, angle));
        }

        private void DrawScale(DrawingContext ctx, Point c, double r)
        {
            var tick = new Pen(TickBrush, 1, lineCap: PenLineCap.Round);
            if (!IsSelector)
            {
                const int ticks = 11;
                for (int i = 0; i < ticks; i++)
                {
                    double deg = AngleFor(i / (double)(ticks - 1));
                    double outer = i == 0 || i == ticks - 1 ? r + 5 : r + 3.5;
                    ctx.DrawLine(tick, OnCircle(c, r + 1.5, deg), OnCircle(c, outer, deg));
                }
                return;
            }

            FontFamily family = GetValue(TextElement.FontFamilyProperty);
            var typeface = new Typeface(family, FontStyle.Normal, FontWeight.SemiBold);
            for (int i = 0; i < _legends.Length; i++)
            {
                double deg = AngleFor(i / (double)(_legends.Length - 1));
                ctx.DrawLine(tick, OnCircle(c, r + 1.5, deg), OnCircle(c, r + 3.5, deg));
                bool selected = i == (int)Value;
                var text = new FormattedText(_legends[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    typeface, 7.5, selected ? LegendLitBrush : LegendBrush);
                // Anchor each legend on its outward side, so long ones lean away from the knob.
                double a = deg * Math.PI / 180;
                Point at = OnCircle(c, r + 5, deg);
                ctx.DrawText(text, new Point(at.X + (Math.Sin(a) - 1) * text.Width / 2,
                                             at.Y - (Math.Cos(a) + 1) * text.Height / 2));
            }
        }
    }
}
