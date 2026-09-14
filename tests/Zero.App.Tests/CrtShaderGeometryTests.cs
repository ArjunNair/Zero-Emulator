using System;
using Avalonia;
using SkiaSharp;
using Xunit;
using Zero.App.Controls;

namespace Zero.App.Tests
{
    /// <summary>
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
            Assert.True(CrtShader.IsAvailable, "the CRT shader did not compile");

            using var frame = new SKBitmap(new SKImageInfo(FrameWidth, FrameHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
            using (var c = new SKCanvas(frame)) c.Clear(new SKColor(205, 205, 205));

            var source = new Rect(48, 48, FrameWidth - 96, FrameHeight - 48 - 56);  // border fully cropped
            var dest = new Rect(0, 0, Width, Height);
            var op = new CrtDrawOperation(dest, frame, source, dest, options, true, null, 0f);

            using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul));
            op.DrawShaded(surface.Canvas, CrtShader.Effect, Width, Height,
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
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
    }
}
