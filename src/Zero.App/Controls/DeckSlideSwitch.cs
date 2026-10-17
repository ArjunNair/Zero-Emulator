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
    /// A detented slide switch, like a tape-type or band selector: a slot in the faceplate with a
    /// legend over each stop and a metal thumb that snaps from one to the next.
    ///
    /// Every change is costly (the machine selector resets the Spectrum), so the thumb follows a drag
    /// freely but <see cref="ValueChangedByUser"/> reports only the stop it is let go on. A click on a
    /// stop or its legend moves it there. The wheel does nothing, so scrolling past it is harmless.
    /// Programmatic changes to <see cref="Value"/> are never reported.
    /// </summary>
    public sealed class DeckSlideSwitch : Control
    {
        public static readonly StyledProperty<int> ValueProperty =
            AvaloniaProperty.Register<DeckSlideSwitch, int>(nameof(Value), coerce: CoerceValue);

        public static readonly StyledProperty<string> PositionsProperty =
            AvaloniaProperty.Register<DeckSlideSwitch, string>(nameof(Positions));

        public static readonly StyledProperty<double> InsetProperty =
            AvaloniaProperty.Register<DeckSlideSwitch, double>(nameof(Inset), double.NaN);

        /// <summary>What the thumb is made of, to match the deck: black plastic, or brushed metal on silver.</summary>
        public static readonly StyledProperty<DeckFinish> FinishProperty =
            AvaloniaProperty.Register<DeckSlideSwitch, DeckFinish>(nameof(Finish));
        public DeckFinish Finish { get => GetValue(FinishProperty); set => SetValue(FinishProperty, value); }

        /// <summary>The stops' legends; set by the deck's finish.</summary>
        public static readonly StyledProperty<IBrush> LegendBrushProperty =
            AvaloniaProperty.Register<DeckSlideSwitch, IBrush>(nameof(LegendBrush), new SolidColorBrush(Color.FromRgb(0x9A, 0x9D, 0xA3)));
        public IBrush LegendBrush { get => GetValue(LegendBrushProperty); set => SetValue(LegendBrushProperty, value); }
        /// <summary>The legend of the selected stop.</summary>
        public static readonly StyledProperty<IBrush> LegendLitBrushProperty =
            AvaloniaProperty.Register<DeckSlideSwitch, IBrush>(nameof(LegendLitBrush), new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x4A)));
        public IBrush LegendLitBrush { get => GetValue(LegendLitBrushProperty); set => SetValue(LegendLitBrushProperty, value); }
        /// <summary>The detent marks under the slot.</summary>
        public static readonly StyledProperty<IBrush> TickBrushProperty =
            AvaloniaProperty.Register<DeckSlideSwitch, IBrush>(nameof(TickBrush), new SolidColorBrush(Color.FromRgb(0x7A, 0x7D, 0x84)));
        public IBrush TickBrush { get => GetValue(TickBrushProperty); set => SetValue(TickBrushProperty, value); }

        /// <summary>The index of the selected stop.</summary>
        public int Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

        /// <summary>Comma-separated legends, one per stop, e.g. "48K,128K,+3".</summary>
        public string Positions { get => GetValue(PositionsProperty); set => SetValue(PositionsProperty, value); }

        /// <summary>
        /// How far the end stops sit in from the edges. Left unset, as near as their legends allow;
        /// set it the same on switches stacked together so their slots start and end level.
        /// </summary>
        public double Inset { get => GetValue(InsetProperty); set => SetValue(InsetProperty, value); }

        /// <summary>True while the user has hold of the thumb; the owner should not move it then.</summary>
        public bool IsInteracting => _dragging;

        /// <summary>Raised when the user lets go of the thumb on a different stop.</summary>
        public event EventHandler<int> ValueChangedByUser;

        private string[] _legends = Array.Empty<string>();
        private FormattedText[] _texts = Array.Empty<FormattedText>();
        private double[] _stops = Array.Empty<double>();
        private bool _dragging;
        private int _pressValue;
        private double _grabOffset;

        private const double LegendSize = 7.5, LegendGap = 4, ThumbWidth = 11, ThumbHeight = 12, MinPitch = 18;

        static DeckSlideSwitch()
        {
            AffectsRender<DeckSlideSwitch>(ValueProperty, LegendLitBrushProperty, TickBrushProperty, FinishProperty);
            AffectsMeasure<DeckSlideSwitch>(PositionsProperty, InsetProperty, LegendBrushProperty);
            FocusableProperty.OverrideDefaultValue<DeckSlideSwitch>(false);
            CursorProperty.OverrideDefaultValue<DeckSlideSwitch>(new Cursor(StandardCursorType.Hand));
        }

        private static int CoerceValue(AvaloniaObject o, int v) =>
            Math.Clamp(v, 0, Math.Max(0, ((DeckSlideSwitch)o)._legends.Length - 1));

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
            else if (change.Property == TextElement.FontFamilyProperty)
                InvalidateMeasure();
        }

        // ------------------------------------------------------------------ layout

        private FormattedText Legend(string text, bool lit) =>
            new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(GetValue(TextElement.FontFamilyProperty), FontStyle.Normal, FontWeight.SemiBold),
                LegendSize, lit ? LegendLitBrush : LegendBrush);

        private double SlotY => (_texts.Length > 0 ? _texts[0].Height : LegendSize) + LegendGap + ThumbHeight / 2;

        // How far the end stops sit in from the edges: enough for the thumb and for half their legends.
        private double LeftEnd => Math.Max(Math.Max(ThumbWidth / 2, _texts[0].Width / 2), double.IsNaN(Inset) ? 0 : Inset);
        private double RightEnd => Math.Max(Math.Max(ThumbWidth / 2, _texts[^1].Width / 2), double.IsNaN(Inset) ? 0 : Inset);

        /// <summary>
        /// As narrow as the legends allow: evenly spaced stops, as close together as they can be
        /// without the legends touching.
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            _texts = Array.ConvertAll(_legends, s => Legend(s, false));
            int n = _texts.Length;
            if (n == 0) return default;
            double pitch = MinPitch;
            for (int i = 0; i + 1 < n; i++)
                pitch = Math.Max(pitch, (_texts[i].Width + _texts[i + 1].Width) / 2 + 3);
            return new Size(Math.Ceiling(LeftEnd + (n - 1) * pitch + RightEnd), Math.Ceiling(SlotY + ThumbHeight / 2 + 1.5));
        }

        /// <summary>Spread the stops evenly over the width given, so a stretched switch spans it.</summary>
        protected override Size ArrangeOverride(Size finalSize)
        {
            int n = _texts.Length;
            _stops = new double[n];
            if (n == 0) return finalSize;
            double span = finalSize.Width - LeftEnd - RightEnd;
            for (int i = 0; i < n; i++) _stops[i] = LeftEnd + (n > 1 ? span * i / (n - 1) : 0);
            return finalSize;
        }

        /// <summary>Where a stop sits, in this control's coordinates (on the slot).</summary>
        public Point StopPoint(int index) => new Point(_stops[Math.Clamp(index, 0, _stops.Length - 1)], SlotY);

        private int NearestStop(double x)
        {
            int best = 0;
            for (int i = 1; i < _stops.Length; i++)
                if (Math.Abs(_stops[i] - x) < Math.Abs(_stops[best] - x)) best = i;
            return best;
        }

        // ------------------------------------------------------------------ interaction

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (_stops.Length == 0 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            double x = e.GetPosition(this).X;
            _pressValue = Value;
            // Grab the thumb where it was hit; a press elsewhere moves it to that stop first.
            if (Math.Abs(x - _stops[Value]) > ThumbWidth / 2) Value = NearestStop(x);
            _grabOffset = x - _stops[Value];
            _dragging = true;
            e.Pointer.Capture(this);
            e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (!_dragging) return;
            Value = NearestStop(e.GetPosition(this).X - _grabOffset);
            e.Handled = true;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (!_dragging) return;
            _dragging = false;
            e.Pointer.Capture(null);
            if (Value != _pressValue) ValueChangedByUser?.Invoke(this, Value);
            e.Handled = true;
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            if (_dragging) Value = _pressValue; // abandoned: put it back
            _dragging = false;
        }

        // ------------------------------------------------------------------ drawing


        public override void Render(DrawingContext ctx)
        {
            int n = _stops.Length;
            if (n == 0) return;
            double y = SlotY;

            // A legend over each stop, the selected one lit.
            for (int i = 0; i < n; i++)
            {
                FormattedText text = i == Value ? Legend(_legends[i], true) : _texts[i];
                ctx.DrawText(text, new Point(_stops[i] - text.Width / 2, 0));
            }

            // Detent marks under the slot.
            var tick = new Pen(TickBrush, 0.8);
            foreach (double s in _stops)
            {
                double x = Math.Round(s) + 0.5;
                ctx.DrawLine(tick, new Point(x, y + 3), new Point(x, y + 5.5));
            }

            // The slot: a dark recess the thumb's length past each end, with a lit lower lip.
            var slot = new Rect(_stops[0] - ThumbWidth / 2, y - 2, _stops[n - 1] - _stops[0] + ThumbWidth, 4);
            ctx.DrawRectangle(Brushes.Black, null, slot, 2, 2);
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), 0.6),
                new Point(slot.Left + 1.5, slot.Bottom + 0.4), new Point(slot.Right - 1.5, slot.Bottom + 0.4));

            // The thumb: a block of black plastic, or brushed metal on silver, with grip ridges across it.
            double cx = _stops[Value];
            var thumb = new Rect(cx - ThumbWidth / 2, y - ThumbHeight / 2, ThumbWidth, ThumbHeight);
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), null, thumb.Translate(new Vector(0, 1.2)), 1.5, 1.5);
            var metal = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = Finish == DeckFinish.Silver
                    ? new GradientStops
                    {
                        new GradientStop(Color.FromRgb(0xEE, 0xEF, 0xF1), 0),
                        new GradientStop(Color.FromRgb(0xA9, 0xAC, 0xB1), 0.55),
                        new GradientStop(Color.FromRgb(0x6E, 0x71, 0x77), 1)
                    }
                    : new GradientStops
                    {
                        new GradientStop(Color.FromRgb(0x52, 0x54, 0x5A), 0),
                        new GradientStop(Color.FromRgb(0x26, 0x27, 0x2B), 0.55),
                        new GradientStop(Color.FromRgb(0x12, 0x13, 0x15), 1)
                    }
            };
            bool silver = Finish == DeckFinish.Silver;
            ctx.DrawRectangle(metal, new Pen(new SolidColorBrush(silver ? Color.FromRgb(0x3A, 0x3C, 0x40) : Color.FromRgb(0x05, 0x05, 0x06)), 0.8), thumb, 1.5, 1.5);
            // Grip ridges: dark grooves in metal, light edges catching the light on black plastic.
            var ridge = new Pen(new SolidColorBrush(silver ? Color.FromArgb(70, 0, 0, 0) : Color.FromArgb(38, 255, 255, 255)), 0.6);
            for (double x = thumb.Left + 2.5; x < thumb.Right - 2; x += 2)
                ctx.DrawLine(ridge, new Point(x, thumb.Top + 2), new Point(x, thumb.Bottom - 2));
        }
    }
}
