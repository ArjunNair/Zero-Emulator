using System;
using Avalonia;
using SkiaSharp;
using Xunit;
using Zero.App.Controls;

namespace Zero.App.Tests
{
    /// <summary>
    /// The edge light takes the colour of the picture's own edge, one pixel per place in the surround.
    /// Nothing here asks it to spread that colour sideways: a dark edge leaves a dark patch behind it
    /// with a hard side to it, which is what point sampling gives and what this build wants. Tests
    /// that asked for a soft, gathered glow were dropped with the gather itself.
    ///
    /// The CRT effects are cosmetic, but one property of them is not a matter of taste: on a picture
    /// that is the same colour everywhere, the four edges of the screen must come out the same. An
    /// effect that is brighter on one side reads as a defect in the emulator rather than as a TV.
    /// These run the real shader with the real uniform wiring, off the UI thread.
    /// </summary>
    public class CrtShaderGeometryTests
    {
        private const int FrameWidth = 352, FrameHeight = 296;
        private const int Width = 1019, Height = 772;     // a roughly 4:3 window, as the display uses

        /// <summary>Renders a flat grey frame through the shader and returns the result.</summary>
        private static SKBitmap Render(CrtShaderOptions options)
        {
            using SKBitmap frame = Frame(new SKColor(205, 205, 205), new SKColor(205, 205, 205));
            return Render(options, frame, smooth: true);
        }

        /// <summary>A frame with the given border round the given screen, as the core hands one over.</summary>
        private static SKBitmap Frame(SKColor border, SKColor screen)
        {
            var bmp = new SKBitmap(new SKImageInfo(FrameWidth, FrameHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
            using var c = new SKCanvas(bmp);
            c.Clear(border);
            using var paint = new SKPaint { Color = screen };
            c.DrawRect(new SKRect(48, 48, 48 + 256, 48 + 192), paint);
            return bmp;
        }

        private static SKBitmap Render(CrtShaderOptions options, SKBitmap frame, bool smooth)
        {
            Assert.True(CrtShader.IsAvailable, "the CRT shader did not compile");

            var source = new Rect(48, 48, FrameWidth - 96, FrameHeight - 48 - 56);  // border fully cropped
            var dest = new Rect(0, 0, Width, Height);
            var op = new CrtDrawOperation(dest, frame, source, dest, options, smooth, null, 0f);

            using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            op.DrawShaded(surface.Canvas, CrtShader.Effect, Width, Height,
                new SKSamplingOptions(smooth ? SKFilterMode.Linear : SKFilterMode.Nearest, SKMipmapMode.None));
            using SKImage image = surface.Snapshot();
            return SKBitmap.FromImage(image);
        }

        private static double Band(SKBitmap bmp, int x0, int x1, int y0, int y1)
        {
            long total = 0;
            int n = 0;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    SKColor c = bmp.GetPixel(x, y);
                    total += (c.Red * 30 + c.Green * 59 + c.Blue * 11) / 100;
                    n++;
                }
            return (double)total / n;
        }

        /// <summary>
        /// Mean brightness of a strip just inside the glass on each edge, clear of the rim itself and
        /// of the corners. The strips are a fraction of each axis rather than a fixed number of
        /// pixels: the shader works in coordinates normalised to the screen, so on a window that is
        /// not square, 60 px in from the side and 60 px down from the top are not the same place and
        /// comparing them would measure the window's shape instead of the shader.
        /// </summary>
        private static (double Left, double Right, double Top, double Bottom) Edges(SKBitmap bmp)
        {
            const double Near = 0.025, Far = 0.10;   // 2.5%..10% of the way in from each edge
            int lx0 = (int)(Width * Near), lx1 = (int)(Width * Far);
            int ty0 = (int)(Height * Near), ty1 = (int)(Height * Far);
            return (Band(bmp, lx0, lx1, Height / 4, 3 * Height / 4),
                    Band(bmp, Width - lx1, Width - lx0, Height / 4, 3 * Height / 4),
                    Band(bmp, Width / 4, 3 * Width / 4, ty0, ty1),
                    Band(bmp, Width / 4, 3 * Width / 4, Height - ty1, Height - ty0));
        }

        [Fact]
        public void The_glass_highlight_lights_no_edge_more_than_the_others()
        {
            // Turned up to the maximum, so a highlight that reaches an edge cannot be missed. Every
            // other effect is off: this test is about the highlight and nothing else.
            var options = new CrtShaderOptions { Enabled = true, Curvature = 0.2f, Reflection = 1f };
            (double left, double right, double top, double bottom) = Edges(Render(options));

            double spread = Math.Max(Math.Max(left, right), Math.Max(top, bottom))
                          - Math.Min(Math.Min(left, right), Math.Min(top, bottom));
            Assert.True(spread < 2.0,
                $"the highlight lights one edge more than the rest: left={left:F1} right={right:F1} " +
                $"top={top:F1} bottom={bottom:F1} (spread {spread:F1} of 255)");
        }

        [Fact]
        public void The_highlight_is_still_there()
        {
            // The cheap way to pass the test above would be to remove the highlight altogether.
            SKBitmap with = Render(new CrtShaderOptions { Enabled = true, Curvature = 0.2f, Reflection = 1f });
            SKBitmap without = Render(new CrtShaderOptions { Enabled = true, Curvature = 0.2f, Reflection = 0f });
            using (with)
            using (without)
            {
                double lit = Band(with, Width / 4, Width / 2, Height / 5, Height / 2);
                double unlit = Band(without, Width / 4, Width / 2, Height / 5, Height / 2);
                Assert.True(lit - unlit > 5.0, $"the highlight has gone: {lit:F1} vs {unlit:F1}");
            }
        }

        [Fact]
        public void The_edge_light_spills_evenly_on_all_four_sides()
        {
            // The surround is lit by the picture it frames, and a flat picture must light it evenly.
            var options = new CrtShaderOptions { Enabled = true, Curvature = 0.2f, EdgeLight = 1f };
            using SKBitmap bmp = Render(options);
            // Just outside the glass: the corners of the render, where only spill reaches.
            double left = Band(bmp, 0, 6, Height / 4, 3 * Height / 4);
            double right = Band(bmp, Width - 6, Width, Height / 4, 3 * Height / 4);
            double spread = Math.Abs(left - right);
            Assert.True(spread < 2.0, $"the spill is uneven: left={left:F1} right={right:F1}");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void A_dark_block_casts_one_shadow_on_the_surround_and_not_a_fan_of_them(bool smooth)
        {
            // The light in the surround is gathered from a patch of the picture. If that patch is
            // measured in the picture's own pixels, the bulge -- which packs many screen pixels into
            // few rows of picture near the edges -- stretches it into a long step across the screen,
            // and the one shadow breaks into several that spread apart as they travel: the fan of
            // rays. One dark block must leave one dark region behind it, however dark.
            using SKBitmap frame = Frame(new SKColor(0xC0, 0xC0, 0xC0), new SKColor(0xC0, 0xC0, 0xC0));
            using (var c = new SKCanvas(frame))
            using (var black = new SKPaint { Color = SKColors.Black })
                c.DrawRect(new SKRect(48, 48 + 92, 48 + 16, 48 + 100), black);

            var options = new CrtShaderOptions { Enabled = true, Curvature = 0.2f, EdgeLight = 1f };
            using SKBitmap bmp = Render(options, frame, smooth);

            int row = (int)(Height * 96.0 / 192.0);
            var profile = new double[121];
            for (int i = 0; i < profile.Length; i++) profile[i] = Band(bmp, 1, 4, row - 60 + i, row - 59 + i);

            // A dip counts as its own shadow only if the surround climbs back out of it by a couple of
            // levels on both sides, so the gentle ripple of a single soft shadow is not counted twice.
            int shadows = 0;
            for (int i = 1; i < profile.Length - 1; i++)
            {
                if (profile[i] >= profile[i - 1] || profile[i] >= profile[i + 1]) continue;
                double leftPeak = 0, rightPeak = 0;
                for (int k = 0; k < i; k++) leftPeak = Math.Max(leftPeak, profile[k]);
                for (int k = i + 1; k < profile.Length; k++) rightPeak = Math.Max(rightPeak, profile[k]);
                if (Math.Min(leftPeak, rightPeak) - profile[i] > 2.0) shadows++;
            }

            Assert.True(shadows <= 1, $"the block casts {shadows} separate shadows on the surround");
        }

        [Fact]
        public void The_edge_light_fills_the_corners_of_the_surround()
        {
            // The curve cuts a corner of the glass far deeper than it cuts the sides, so light that
            // fades over the width of a side panel has run out long before it crosses a corner. The
            // surround then reads as four lit panels with dark gaps where they meet, rather than as
            // one lit frame. On a picture that is the same colour everywhere, a corner of the frame
            // should be lit much like the middle of a side.
            var options = new CrtShaderOptions { Enabled = true, Curvature = 0.2f, EdgeLight = 1f };
            using SKBitmap bmp = Render(options);

            double side = Band(bmp, 0, 10, Height / 2 - 40, Height / 2 + 40);
            double topLeft = Band(bmp, 0, 10, 0, 40);
            double topRight = Band(bmp, Width - 10, Width, 0, 40);
            double bottomLeft = Band(bmp, 0, 10, Height - 40, Height);

            foreach ((string name, double corner) in new[]
                     { ("top left", topLeft), ("top right", topRight), ("bottom left", bottomLeft) })
                Assert.True(corner > side * 0.7,
                    $"the {name} corner of the surround is unlit next to the sides: {corner:F1} against {side:F1}");
        }

    }
}
