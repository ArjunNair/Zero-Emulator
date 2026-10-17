using System;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Zero.App.Controls
{
    /// <summary>
    /// Text on the deck's display, drawn pixel by pixel in the ZX Spectrum's own character shapes.
    ///
    /// The shapes come from the 48K ROM Zero already ships (<see cref="LoadCharacterSet"/>), so there
    /// is no font file. They are drawn narrower than the Spectrum's 8x8 cells -- the feel of its
    /// lettering at the width the display has room for -- with each pixel solid. (Round dots, as on a
    /// dot-matrix VFD, were tried and dropped: at the deck's size they blurred into illegibility.) Lit
    /// and unlit come from the styles, as for any other control: Foreground for the colour, Effect
    /// for the glow.
    /// </summary>
    public sealed class VfdText : Control
    {
        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<VfdText, string>(nameof(Text), "");

        public static readonly StyledProperty<IBrush> ForegroundProperty =
            AvaloniaProperty.Register<VfdText, IBrush>(nameof(Foreground), Brushes.White);

        public static readonly StyledProperty<bool> TrimProperty =
            AvaloniaProperty.Register<VfdText, bool>(nameof(Trim));

        public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public IBrush Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

        /// <summary>Cut text too long for the space with an ellipsis, instead of asking for more room.</summary>
        public bool Trim { get => GetValue(TrimProperty); set => SetValue(TrimProperty, value); }

        // A pixel of the character set, on screen: a little narrower than tall, which is what brings
        // the Spectrum's broad letters down to the display's width.
        private const double PixelWidth = 0.8, PixelHeight = 1.0;
        private const int Columns = 7, Rows = 7;           // the ROM's 8x8 cells, less their empty edges
        private const double Advance = Columns * PixelWidth;
        private const double Height = Rows * PixelHeight;

        // Raised when the character set arrives, so text drawn with the fallback redraws in it.
        private static event Action StyleChanged;

        // 96 characters from space, 8 rows each, as in the ROM at 0x3D00.
        private static byte[] _charset;

        /// <summary>
        /// Read the character set from a 48K ROM image (at 0x3D00). Without one, the display falls
        /// back to the UI's own monospace font.
        /// </summary>
        public static void LoadCharacterSet(string romPath)
        {
            try
            {
                byte[] rom = File.ReadAllBytes(romPath);
                if (rom.Length >= 0x3D00 + 768)
                {
                    _charset = new byte[768];
                    Array.Copy(rom, 0x3D00, _charset, 0, 768);
                    StyleChanged?.Invoke();
                }
            }
            catch (Exception) { /* keep whatever we had, or the fallback */ }
        }

        /// <summary>The characters the Spectrum doesn't have, drawn in its style (8 rows, top bit leftmost).</summary>
        private static byte[] Extra(char c) => c switch
        {
            '▶' => new byte[] { 0, 0x20, 0x30, 0x38, 0x3C, 0x38, 0x30, 0x20 },
            '■' => new byte[] { 0, 0, 0x3C, 0x3C, 0x3C, 0x3C, 0, 0 },
            '●' => new byte[] { 0, 0, 0x18, 0x3C, 0x3C, 0x18, 0, 0 },
            '…' => new byte[] { 0, 0, 0, 0, 0, 0, 0x54, 0 },
            _ => null
        };

        private static byte[] Glyph(char c)
        {
            byte[] extra = Extra(c);
            if (extra != null) return extra;
            int index = c - 32;
            if (index < 0 || index >= 96) index = 0;   // anything else draws as a space
            var glyph = new byte[8];
            Array.Copy(_charset, index * 8, glyph, 0, 8);
            return glyph;
        }

        static VfdText()
        {
            AffectsRender<VfdText>(TextProperty, ForegroundProperty, TrimProperty);
            AffectsMeasure<VfdText>(TextProperty, TrimProperty);
        }

        public VfdText()
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            StyleChanged += OnStyleChanged;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            StyleChanged -= OnStyleChanged;
            base.OnDetachedFromVisualTree(e);
        }

        private void OnStyleChanged() { InvalidateMeasure(); InvalidateVisual(); }

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = (Text?.Length ?? 0) * Advance;
            // Trimmed text never asks for more than it is given, so a long message can't widen the deck.
            if (Trim && !double.IsInfinity(availableSize.Width)) width = Math.Min(width, availableSize.Width);
            return new Size(width, Height + 1);
        }

        /// <summary>The characters that fit, ending in an ellipsis when some had to go.</summary>
        private string Fitted(double width)
        {
            string text = Text ?? "";
            if (!Trim || text.Length * Advance <= width + 0.01) return text;
            int fit = Math.Max(0, (int)(width / Advance) - 1);
            return text.Substring(0, Math.Min(fit, text.Length)) + "…";
        }

        public override void Render(DrawingContext ctx)
        {
            string text = Fitted(Bounds.Width);
            if (text.Length == 0) return;
            if (_charset == null) { RenderFallback(ctx, text); return; }

            double top = Math.Round((Bounds.Height - Height) / 2);
            var pixels = new StreamGeometry();
            using (StreamGeometryContext g = pixels.Open())
            {
                for (int i = 0; i < text.Length; i++)
                {
                    byte[] glyph = Glyph(text[i]);
                    for (int row = 0; row < Rows; row++)
                    {
                        byte bits = glyph[row + 1];                       // the ROM's top row is always empty
                        for (int col = 0; col < Columns; col++)
                        {
                            if ((bits & (0x80 >> col)) == 0) continue;
                            double x = i * Advance + col * PixelWidth, y = top + row * PixelHeight;
                            AddPixel(g, x, y);
                        }
                    }
                }
            }
            ctx.DrawGeometry(Foreground, null, pixels);
        }

        private static void AddPixel(StreamGeometryContext g, double x, double y)
        {
            g.BeginFigure(new Point(x, y), true);
            g.LineTo(new Point(x + PixelWidth, y));
            g.LineTo(new Point(x + PixelWidth, y + PixelHeight));
            g.LineTo(new Point(x, y + PixelHeight));
            g.EndFigure(true);
        }

        private void RenderFallback(DrawingContext ctx, string text)
        {
            var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Menlo, Consolas, DejaVu Sans Mono, Courier New"), Height + 1, Foreground);
            ctx.DrawText(formatted, new Point(0, (Bounds.Height - formatted.Height) / 2));
        }
    }
}
