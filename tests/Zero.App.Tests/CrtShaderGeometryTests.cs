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
        public void Each_panel_tapers_towards_the_corners_without_a_step()
        {
            // Two panels meet at every corner, so a corner that is lit as strongly as a side carries
            // a third reflection there -- the picture turned back on itself diagonally -- as bright
            // as the two straight ones beside it. Each panel is brightest opposite the middle of the
            // screen and fades towards its ends, so the corner gets only the tail of both.
            //
            // Fading, though, not stopping: the first thing tried here was measuring the light from
            // the nearest edge alone, which left the corners black and the surround reading as four
            // separate panels with gaps between them.
            var options = new CrtShaderOptions { Enabled = true, Curvature = 0.2f, EdgeLight = 1f };
            using SKBitmap bmp = Render(options);

            double middle = Band(bmp, Width / 2 - 40, Width / 2 + 40, Height - 10, Height);
            double corner = Band(bmp, 0, 20, Height - 20, Height);
            Assert.True(corner < middle * 0.5,
                $"the corner is lit like the middle of a panel: {corner:F1} against {middle:F1}");

            // Walk the bottom of the frame from the middle out to the corner: it must fall away, not
            // drop off a cliff.
            double biggest = 0, previous = Band(bmp, Width / 2, Width / 2 + 10, Height - 10, Height);
            for (int x = Width / 2 - 10; x >= 10; x -= 10)
            {
                double here = Band(bmp, x, x + 10, Height - 10, Height);
                biggest = Math.Max(biggest, Math.Abs(here - previous));
                previous = here;
            }

            Assert.True(biggest < 12.0,
                $"the surround steps by {biggest:F1} of 255 on the way to the corner");
        }


        [Fact]
        public void The_panel_mirrors_the_picture_across_its_depth()
        {
            // Along a panel the light already follows the picture: each row of the left panel takes
            // its own row. Across one it must too, or the edge pixel repeats outwards to the frame
            // and a dark pixel at the edge of the picture draws a bar. Three single-pixel columns at
            // the very left of the screen: reflected, the panel shows red, then green, then blue
            // going outwards. Clamped to the edge, it would be red the whole way.
            using var frame = new SKBitmap(new SKImageInfo(FrameWidth, FrameHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
            using (var c = new SKCanvas(frame))
            {
                c.Clear(new SKColor(0x40, 0x40, 0x40));
                SKColor[] bars = { SKColors.Red, SKColors.Lime, SKColors.Blue };
                for (int i = 0; i < bars.Length; i++)
                {
                    using var paint = new SKPaint { Color = bars[i] };
                    c.DrawRect(new SKRect(48 + i, 48, 48 + i + 1, 48 + 192), paint);
                }
            }

            var options = new CrtShaderOptions { Enabled = true, Curvature = 0.2f, EdgeLight = 1f };
            using SKBitmap bmp = Render(options, frame, smooth: true);

            bool green = false, blue = false;
            for (int x = 0; x < 40; x++)
            {
                SKColor c = bmp.GetPixel(x, Height / 2);
                if (c.Green > c.Red && c.Green > c.Blue) green = true;
                if (c.Blue > c.Red && c.Blue > c.Green) blue = true;
            }

            Assert.True(green && blue,
                "the panel does not mirror the picture across its depth: the second and third columns "
                + $"of the screen never reach it (green seen: {green}, blue seen: {blue})");
        }

        [Theory]
        [InlineData(true)]    // PixelSmoothing on
        [InlineData(false)]   // and off, which is the default
        public void A_cropped_away_border_never_reaches_the_screen(bool smooth)
        {
            // A red border round a white screen, with the border cropped out of view. The frame still
            // carries its border and the tile mode clamps to the whole of it, so a sample taken
            // exactly on the edge of the visible part lands on the boundary between the last visible
            // pixel and the first cropped-away one, and rounding decides which comes back -- one way
            // at the left and top, the other at the right and bottom. Every sample is held half a
            // pixel inside to stop that, so the outermost column of picture must stay neutral.
            using SKBitmap frame = Frame(new SKColor(0xC0, 0x00, 0x00), new SKColor(0xC0, 0xC0, 0xC0));
            var options = new CrtShaderOptions { Enabled = true, Curvature = 0.2f, EdgeLight = 1f };
            using SKBitmap bmp = Render(options, frame, smooth);

            // The screen is grey and the border red, and the border is not shown. So nothing in the
            // output may be red: not the picture, not the surround, not the rim between them.
            int worstX = 0, worstY = 0, worst = 0;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    SKColor c = bmp.GetPixel(x, y);
                    int tint = c.Red - Math.Max(c.Green, c.Blue);
                    if (tint > worst) { worst = tint; worstX = x; worstY = y; }
                }

            Assert.True(worst <= 3,
                $"the hidden border tints the output at {worstX},{worstY} by {worst} of 255");
        }

        [Fact]
        public void The_cabinet_frames_the_picture_and_pushes_it_in()
        {
            // The cabinet is the moulded fascia the screen sits in. It has to take room from the
            // picture to exist, which is the trade: the opening shrinks by the width of the frame.
            var bare = new CrtShaderOptions { Enabled = true, Curvature = 0.2f, EdgeLight = 1f };
            var framed = new CrtShaderOptions { Enabled = true, Curvature = 0.2f, EdgeLight = 1f, Bezel = 0.12f };

            using SKBitmap without = Render(bare);
            using SKBitmap with = Render(framed);

            // Walk in along the middle row to where the picture starts, in each.
            int Opening(SKBitmap bmp)
            {
                for (int x = 0; x < Width / 2; x++)
                {
                    SKColor c = bmp.GetPixel(x, Height / 2);
                    if ((c.Red * 30 + c.Green * 59 + c.Blue * 11) / 100 > 150) return x;
                }
                return Width / 2;
            }

            int bareEdge = Opening(without), framedEdge = Opening(with);
            Assert.True(framedEdge > bareEdge + Width * 0.05,
                $"the cabinet did not push the picture in: it starts at {framedEdge} against {bareEdge}");

            // And the frame itself is there: a band outside the opening that is neither picture nor
            // the black of an empty window, and the same on all four sides.
            double left = Band(with, 6, 16, Height / 2 - 30, Height / 2 + 30);
            double right = Band(with, Width - 16, Width - 6, Height / 2 - 30, Height / 2 + 30);
            double top = Band(with, Width / 2 - 30, Width / 2 + 30, 6, 16);
            foreach ((string name, double v) in new[] { ("left", left), ("right", right), ("top", top) })
                Assert.True(v > 8 && v < 90, $"the {name} of the cabinet does not read as a frame: {v:F1}");
        }
    }
}
