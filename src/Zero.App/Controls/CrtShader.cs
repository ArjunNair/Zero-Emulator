using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace Zero.App.Controls
{
    /// <summary>How much of each effect the CRT shader applies. All ranges are 0..1.</summary>
    public struct CrtShaderOptions : IEquatable<CrtShaderOptions>
    {
        public bool Enabled;
        public float Curvature;
        public float Glow;
        /// <summary>How much of the window the cabinet round the screen takes, 0 for none.</summary>
        public float Bezel;
        /// <summary>How blurred the picture is before the housing reflects it, 0 for not at all.</summary>
        public float Diffuse;
        /// <summary>Confine the blend between pixels to the boundary between them.</summary>
        public bool Sharp;
        /// <summary>Light from the picture spilling onto the surround, instead of plain black.</summary>
        public float EdgeLight;
        /// <summary>
        /// How round the glass's corners are, in units of half the screen's height; unset is the
        /// standard 0.16. Smaller when little or no border shows, so the corners never cut into the
        /// picture itself.
        /// </summary>
        public float? Corner;
        /// <summary>A Trinitron's glass, curved only from side to side, instead of a sphere.</summary>
        public bool Trinitron;
        public float Scanlines;
        public float Vignette;

        // Sharp counts: it is the shader that places the sample, so the picture has to go through it
        // even when every effect is off.
        public bool Any => Sharp || (Enabled && (Curvature > 0 || Glow > 0 || Bezel > 0
                                     || EdgeLight > 0 || Scanlines > 0 || Vignette > 0));

        public bool Equals(CrtShaderOptions other) =>
            Enabled == other.Enabled && Curvature.Equals(other.Curvature) && Glow.Equals(other.Glow)
            && Bezel.Equals(other.Bezel)
            && Diffuse.Equals(other.Diffuse) && Sharp == other.Sharp && EdgeLight.Equals(other.EdgeLight)
            && Scanlines.Equals(other.Scanlines) && Vignette.Equals(other.Vignette) && Corner == other.Corner && Trinitron == other.Trinitron;
    }

    /// <summary>
    /// The CRT shader, written in SkSL and compiled by Skia at runtime. Skia targets whichever backend
    /// it is already using (Metal, Vulkan, Direct3D) and falls back to its own CPU raster pipeline, so
    /// this needs no OpenGL and always has something to run on.
    /// </summary>
    internal static class CrtShader
    {
        private const string Source = @"
uniform shader src;
uniform shader srcSoft;     // the same picture, blurred, for the housing to reflect
uniform float2 dest;        // destination size in pixels
uniform float2 srcOrigin;   // top-left of the visible part of the frame
uniform float2 srcSize;     // size of the visible part of the frame
uniform float tubeRadius;   // the glass's radius of curvature, in units of half the screen's height; 0 is flat
uniform float tubeCylinder; // 1 for a tube curved only side to side, as a Trinitron is
uniform float glow;
uniform float sharp;        // confine the blend to the pixel boundaries
uniform float bezel;        // share of the window given to the housing the screen sits in
uniform float cornerRadius; // how round the glass's corners are, in units of half the screen's height
uniform float edgeLight;
uniform float scanline;
uniform float vignette;

/// Where to sample the picture so that a blend between neighbouring pixels happens only at the
/// boundary between them, and only across a pixel of the screen.
///
/// Plain bilinear ramps all the way from one pixel's centre to the next, so at four screen pixels to
/// the picture's one every edge in the picture becomes a four pixel gradient -- which is the blur.
/// This holds the sample at the centre through the body of a pixel and turns it over at the seam,
/// keeping the picture crisp while still placing the seams to sub-pixel accuracy, which is what
/// stops a fractional scale from making some pixels wider than others.
float2 atPixel(float2 q) {
    float2 size = dest / max(srcSize, float2(1.0, 1.0));   // screen pixels to one of the picture's
    float2 inPixels = q / size;
    float2 seam = floor(inPixels + 0.5);                   // the boundary, not the centre
    return (seam + clamp((inPixels - seam) * size, -0.5, 0.5)) * size;
}

// How far the glass lies behind its own front at a point (in units of half the screen's height):
// a sphere, or for a Trinitron a cylinder curved only from side to side.
float tubeDepth(float2 h) {
    float r2 = tubeCylinder > 0.5 ? h.x * h.x : dot(h, h);
    return tubeRadius - sqrt(max(tubeRadius * tubeRadius - r2, 0.0));
}

// Where on the picture a point on the window shows, measured across the curved glass: the point is
// carried back along the line of sight onto the glass, with a viewer at a usual distance, and the
// picture is laid evenly along the glass's surface, as the tube's own scan draws it. A few rounds
// of perspective settle where the line meets the glass. Flat glass leaves the point as it is.
float2 onTube(float2 p) {
    if (tubeRadius <= 0.0) return p;
    const float view = 3.0;
    float2 h = p;
    for (int i = 0; i < 4; ++i) h = p * (view + tubeDepth(h)) / view;
    float r = tubeRadius;
    if (tubeCylinder > 0.5) return float2(r * asin(clamp(h.x / r, -1.0, 1.0)), h.y);
    float len = length(h);
    if (len < 0.00001) return h;
    return h / len * (r * asin(clamp(len / r, -1.0, 1.0)));
}

float hash(float2 p) {
    return fract(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

half4 main(float2 xy) {
    float2 uv = xy / dest;
    // The picture is pushed in to leave room for the housing it sits in, so -1..1 is the opening
    // rather than the window and the window edge is at 'outer'. Everything from the glass out to
    // there is housing, lit by the picture; there is no second frame around it. The picture itself
    // still spans the whole of the opening.
    float opening = max(1.0 - bezel, 0.05);
    float outer = 1.0 / opening;
    float2 c0 = (uv * 2.0 - 1.0) / opening;           // -1..1 across the opening, from the centre
    float aspect = dest.x / max(dest.y, 1.0);
    float2 extent = float2(aspect, 1.0);
    // The curved glass: where each point of the opening shows on the picture, scaled so the middle
    // of each edge of the picture meets the middle of each edge of the opening. Further out, along
    // the edges and most at the corners, the curve carries the picture away from the viewer.
    float2 c = onTube(c0 * extent) / float2(onTube(float2(aspect, 0.0)).x, onTube(float2(0.0, 1.0)).y);

    // Keep every sample half a source pixel inside the visible frame. The frame we are handed still
    // has its border attached and the tile mode clamps to the whole of it, so a sample taken exactly
    // on the edge lands on the boundary between the last visible pixel and the first cropped-away
    // border pixel: which one it returns is then decided by rounding, and it rounds differently at
    // the two ends. That is what made the edge light spill the hidden border's colour down one side
    // of the screen and the picture's colour down the other.
    float2 texel = dest / max(srcSize, float2(1.0, 1.0));
    float2 lo = texel * 0.5, hi = dest - texel * 0.5;

    float2 inside = clamp(c, -1.0, 1.0);
    float2 p = clamp((inside * 0.5 + 0.5) * dest, lo, hi);   // nearest point on the glass
    float2 at = sharp > 0.0 ? atPixel(p) : p;
    half4 col = src.eval(at);

    if (glow > 0.0) {                                 // phosphor bleeding into the neighbouring lines
        float rows = max(srcSize.y, 1.0);
        float step = dest.y / rows;
        half4 bleed = src.eval(clamp(at - float2(0.0, step), lo, hi)) * 0.6
                    + src.eval(clamp(at + float2(0.0, step), lo, hi)) * 0.4;
        col = (col + bleed * glow) / (1.0 + glow);    // normalised, so the picture keeps its exposure
    }

    half3 picture = col.rgb;
    if (scanline > 0.0) {
        // One dark band per emulated line, but never more than one per two output pixels: asking for
        // more bands than the surface can hold turns them into moire instead of scanlines.
        float rows = min(max(srcSize.y, 1.0), dest.y * 0.5);
        float row = (p.y / dest.y) * rows;
        picture *= 1.0 - scanline * (0.5 - 0.5 * cos(row * 6.2831853));
    }
    if (vignette > 0.0) {
        picture *= 1.0 - vignette * dot(c, c) * 0.35;
    }
    // Blend across the rim rather than switching at it, or the curve stair-steps. 'pixel' is one
    // output pixel expressed in this -1..1 space; three of them is the narrowest band that still
    // reads as smooth once the software path scales its output up.
    //
    // The glass's corners are rounded, as a tube's are, wherever there is a housing to round them
    // against. Measured with the width scaled to the height, so the corners stay circular whatever
    // shape the window is; 'corner' is their radius in units of half the screen's height.
    float corner = bezel > 0.0 ? cornerRadius : 0.0;
    float2 qr = abs(c * extent) - (extent - corner);
    float beyond = length(max(qr, 0.0)) + min(max(qr.x, qr.y), 0.0) - corner;
    beyond = max(beyond, 0.0);
    float pixel = 2.0 / max(dest.y, 1.0);
    float rim = smoothstep(0.0, pixel * 3.0, beyond);

    // The surround, lit by the picture it frames -- as a mirror of it. Clamping to the glass gives
    // each place in the surround the picture directly in front of it, which is right along a panel
    // but not across one: the edge pixel then repeats outwards, all the way to the frame, and a dark
    // pixel at the edge of the picture draws a bar. Reflecting instead of clamping steps a pixel back
    // into the picture for every pixel out across the panel, so the panel carries the edge of the
    // picture turned back on itself. At the glass the two agree, so the seam stays invisible.
    //
    // How far out a point is, for the light the picture throws on the housing: 0 at the glass, 1 at
    // the window edge, measured from the rounded, curved glass so it runs smoothly round the corners
    // and the light with it. Both distances in units of half the window's height.
    float2 wp = c0 * opening * extent;                                        // window space
    float toWindow = max(min(aspect - abs(wp.x), 1.0 - abs(wp.y)), 0.0);
    float fromGlass = beyond * opening;
    float depth = clamp(fromGlass / max(fromGlass + toWindow, 0.0001), 0.0, 1.0);

    // The cabinet, as on a set of the early eighties: a flat frame round the front, and inside it a
    // rectangular tunnel running back to the tube, its four walls mitred where they meet. The tube
    // sits at the back of the tunnel in a dark mask, its own corners rounded. Lit from above: the
    // top wall faces down into shadow, the bottom one faces up and catches the light, the sides are
    // between. 'tunnel' runs from 0 at the back of the tunnel, round the tube, to 1 at the window
    // edge, on whichever wall a point is on; below 0 is the mask.
    half3 housing = half3(0.03);                                            // the mask round the tube
    float catches = 1.0;            // how much of the picture's light a point can catch: see below
    float reach = exp(-depth * 3.2);    // and how much of it gets that far: see below
    if (bezel > 0.0) {
        // The back of the tunnel meets the tube at the middle of each edge; towards the corners the
        // curve carries the glass away from it, and the mask fills the gap.
        float2 back = opening * extent;
        float2 tw = (abs(wp) - back) / max(extent - back, float2(0.0001, 0.0001));
        float tunnel = max(tw.x, tw.y);

        if (tunnel > 0.0) {
            bool side = tw.x > tw.y;
            float frameStart = 0.45;   // a shallow tunnel inside a broad frame
            // Only the tunnel's walls face the tube and can catch its light, and less the further
            // forward they run; the frame faces the room, so none reaches it.
            catches = 1.0 - smoothstep(frameStart * 0.6, frameStart, tunnel);
            // On the walls the light carries their whole depth, fading as they run forward, rather
            // than dying out halfway along: a wall faces the screen all the way to the frame.
            reach = exp(-(tunnel / frameStart) * 1.0);
            if (tunnel < frameStart) {
                float wall = side ? (wp.x < 0.0 ? 0.06 : 0.05) : (wp.y < 0.0 ? 0.034 : 0.11);
                float forward = 0.65 + 0.35 * tunnel / frameStart;         // darker the further back
                housing = half3(half(wall * forward));
            } else {
                // The frame's flat face, with a stepped inner edge and a rounded outer one.
                float step = exp(-pow((tunnel - frameStart - 0.05) / 0.025, 2.0));
                float rounded = smoothstep(0.96, 1.0, tunnel);
                housing = half3(half((0.065 + 0.06 * step) * (1.0 - 0.45 * rounded)));
            }
        } else {
            housing = half3(0.012);   // the mask round the tube
            reach = 1.0;              // right against the tube, as the back of the tunnel is
        }
    }

    // The screen's own light on it, close in: a faint glow off the glass, falling away across the
    // bezel and the same all the way round, since 'depth' runs smoothly round the corners. Kept
    // faint: at full strength a bright picture -- the Spectrum's white border -- painted a pale band
    // round the glass, where a set's bezel is dark. And only where the cabinet can catch it.
    // Mirrored across the edge of the glass as it really is, rounded corners and all: from the
    // nearest point on that edge, as far back in along its normal as the point is out. Along a
    // straight side that is the plain mirror across it; round a corner the normal turns, so the
    // picture's lines bend round the corner on the housing instead of running on straight past it.
    // With no rounding the corner is a point, and the mirror through it is the point reflection the
    // straight-edged version gave there.
    float2 cp = c * extent;
    float2 centres = extent - corner;
    float2 qe = abs(cp) - centres;
    float2 sgn = float2(cp.x < 0.0 ? -1.0 : 1.0, cp.y < 0.0 ? -1.0 : 1.0);
    float2 normal;
    float2 edgePoint;
    if (qe.x > 0.0 && qe.y > 0.0) {
        float2 centre = sgn * centres;
        float2 away = cp - centre;
        normal = away / max(length(away), 0.0001);
        edgePoint = centre + normal * corner;
    } else if (qe.x > qe.y) {
        normal = float2(sgn.x, 0.0);
        edgePoint = float2(sgn.x * extent.x, cp.y);
    } else {
        normal = float2(0.0, sgn.y);
        edgePoint = float2(cp.x, sgn.y * extent.y);
    }
    float2 reflected = clamp((edgePoint - normal * beyond) / extent, -1.0, 1.0);
    // None of it round the corners, though: there the housing turns away from the screen, and the
    // light fades out as it goes round rather than stopping at a line.
    catches *= 1.0 - smoothstep(0.0, max(corner, 0.06), min(qe.x, qe.y));
    float2 mirror = clamp((reflected * 0.5 + 0.5) * dest, lo, hi);
    half3 spill = housing + srcSoft.eval(mirror).rgb * half(reach * 0.45 * catches) * edgeLight;

    return half4(mix(picture, spill, half(rim)), 1.0);
}";


        /// <summary>
        /// The glass's radius for a curvature setting, in units of half the screen's height, for a
        /// window of the given shape: the radius that carries the corners of the picture out by 30%
        /// of the setting (2.4% at 8%, a portable set's gentle curve; 30% at full). Found by
        /// bisection against the same projection the shader makes. 0 is flat.
        /// </summary>
        internal static float TubeRadiusFor(float curvature, float aspect, bool cylinder)
        {
            if (curvature <= 0.001f) return 0f;
            double target = 0.3 * curvature, lo = 2.0, hi = 1e4;
            if (CornerPull(lo, aspect, cylinder) < target) return (float)lo;    // as curved as a tube goes
            for (int i = 0; i < 50; i++)
            {
                double mid = Math.Sqrt(lo * hi);
                if (CornerPull(mid, aspect, cylinder) > target) lo = mid; else hi = mid;
            }
            return (float)Math.Sqrt(lo * hi);
        }

        // The shader's onTube and tubeDepth, in doubles: how far the curve carries the picture's
        // corner beyond the line of its top edge.
        private static double CornerPull(double r, double aspect, bool cylinder)
        {
            (double, double) OnTube(double px, double py)
            {
                const double view = 3.0;
                double hx = px, hy = py;
                for (int i = 0; i < 4; i++)
                {
                    double r2 = cylinder ? hx * hx : hx * hx + hy * hy;
                    double z = r - Math.Sqrt(Math.Max(r * r - r2, 0));
                    hx = px * (view + z) / view;
                    hy = py * (view + z) / view;
                }
                if (cylinder) return (r * Math.Asin(Math.Clamp(hx / r, -1, 1)), hy);
                double len = Math.Sqrt(hx * hx + hy * hy);
                double arc = r * Math.Asin(Math.Clamp(len / r, -1, 1));
                return (hx / len * arc, hy / len * arc);
            }
            double edge = OnTube(0, 1).Item2;
            return OnTube(aspect, 1).Item2 / edge - 1;
        }

        // Solved once per window shape and setting, not every frame.
        private static (float Curvature, float Aspect, bool Cylinder, float Radius) _radius;

        internal static float TubeRadius(float curvature, float aspect, bool cylinder)
        {
            var key = _radius;
            if (key.Curvature == curvature && Math.Abs(key.Aspect - aspect) < 0.001f && key.Cylinder == cylinder) return key.Radius;
            float r = TubeRadiusFor(curvature, aspect, cylinder);
            _radius = (curvature, aspect, cylinder, r);
            return r;
        }

        private static SKRuntimeEffect _effect;
        private static bool _tried;

        /// <summary>Compiles once. Null if the shader could not be built, which disables the effect.</summary>
        public static SKRuntimeEffect Effect
        {
            get
            {
                if (!_tried)
                {
                    _tried = true;
                    _effect = SKRuntimeEffect.CreateShader(Source, out string errors);
                    if (_effect == null)
                        Zero.Emulation.Trace.Log("CRT shader failed to compile: " + errors);
                }
                return _effect;
            }
        }

        public static string CompileError { get; private set; }

        /// <summary>True when the shader is usable at all (compiled, and Skia is the renderer).</summary>
        public static bool IsAvailable => Effect != null;
    }

    /// <summary>
    /// Holds the offscreen surface the software path draws into, so it is not rebuilt every frame.
    /// Owned by the display control; the draw operation only borrows it.
    /// </summary>
    internal sealed class CrtSurfaceCache : IDisposable
    {
        private readonly List<(int Width, int Height, SKSurface Surface)> _surfaces = new();

        public SKSurface GetOrCreate(int width, int height)
        {
            foreach ((int w, int h, SKSurface surface) in _surfaces)
                if (w == width && h == height) return surface;

            var made = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            // A handful at most: the frame's own size, and the steps the soft copy is halved through.
            // Anything beyond that is a size nobody is asking for any more.
            if (_surfaces.Count >= 6)
            {
                _surfaces[0].Surface.Dispose();
                _surfaces.RemoveAt(0);
            }
            _surfaces.Add((width, height, made));
            return made;
        }

        public void Dispose()
        {
            foreach ((int _, int _, SKSurface surface) in _surfaces) surface.Dispose();
            _surfaces.Clear();
        }
    }

    /// <summary>Draws one frame through <see cref="CrtShader"/> using Avalonia's Skia canvas.</summary>
    internal sealed class CrtDrawOperation : ICustomDrawOperation
    {
        private readonly SKBitmap _frame;
        private readonly Rect _source;
        private readonly Rect _dest;
        private readonly CrtShaderOptions _options;
        private readonly bool _smooth;
        private readonly CrtSurfaceCache _cache;
        private readonly CrtSurfaceCache _soft;

        public CrtDrawOperation(Rect bounds, SKBitmap frame, Rect source, Rect dest, CrtShaderOptions options, bool smooth, CrtSurfaceCache cache, CrtSurfaceCache soft = null)
        {
            _cache = cache;
            _soft = soft;
            Bounds = bounds;
            _frame = frame;
            _source = source;
            _dest = dest;
            _options = options;
            _smooth = smooth;
        }

        public Rect Bounds { get; }

        public bool HitTest(Point p) => Bounds.Contains(p);

        public bool Equals(ICustomDrawOperation other) => false; // the frame changes every time

        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            SKRuntimeEffect effect = CrtShader.Effect;
            var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();
            if (effect == null || lease == null || _frame == null) return;

            using (lease)
            {
                SKCanvas canvas = lease.SkCanvas;
                SKSamplingOptions sampling = _smooth
                    ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None)
                    : new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);

                // On a GPU the shader costs nothing worth measuring, so run it at the size it will be
                // seen at. On Skia's software pipeline the cost is per output pixel (70 ms at 1080p
                // on this machine), so there it runs at the frame's own resolution and is scaled up.
                if (lease.GrContext != null)
                {
                    canvas.Save();
                    canvas.Translate((float)_dest.X, (float)_dest.Y);
                    DrawShaded(canvas, effect, _dest.Width, _dest.Height, sampling);
                    canvas.Restore();
                    return;
                }

                int w = Math.Max(1, (int)Math.Round(_source.Width));
                int h = Math.Max(1, (int)Math.Round(_source.Height));
                SKSurface offscreen = _cache?.GetOrCreate(w, h);
                if (offscreen == null) return;
                DrawShaded(offscreen.Canvas, effect, w, h, sampling);
                using SKImage image = offscreen.Snapshot();
                canvas.DrawImage(image, new SKRect((float)_dest.X, (float)_dest.Y, (float)_dest.Right, (float)_dest.Bottom), sampling);
            }
        }

        /// <summary>
        /// A shrunken copy of the visible part of the frame, for the housing to reflect. How far it
        /// shrinks is how diffuse the reflection is: same size is a mirror, a tenth is a wash.
        ///
        /// Light off a moulding is diffuse, and reflecting the picture pixel for pixel gives back a
        /// second, sharp copy of whatever is at the edge of the screen. Blurring it at the source
        /// costs a few small draws, where blurring it at the sampling site costs a tap for every
        /// pixel of housing and brings its own trouble with it.
        ///
        /// It holds only the visible part, so the cropped-away border cannot reach it at all.
        /// </summary>
        private SKImage SoftCopy(List<IDisposable> trash)
        {
            float shrink = 1f + Math.Clamp(_options.Diffuse, 0f, 1f) * 9f;
            int targetWidth = Math.Max(1, (int)Math.Round(_source.Width / shrink));
            int targetHeight = Math.Max(1, (int)Math.Round(_source.Height / shrink));

            var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
            SKImage current = SKImage.FromBitmap(_frame);
            trash.Add(current);
            var from = new SKRect((float)_source.X, (float)_source.Y, (float)_source.Right, (float)_source.Bottom);
            int width = (int)Math.Round(_source.Width), height = (int)Math.Round(_source.Height);

            // Halve, and halve again, until one more would overshoot. Neither bilinear nor a cubic
            // widens its kernel as the reduction grows -- both read a handful of pixels however far
            // the picture is being shrunk -- so going straight to a tenth aliases the picture instead
            // of averaging it, and a comb of thin lines comes back as a coarser pattern rather than a
            // wash. Each step here is a halving, which they do handle.
            while (width / 2 > targetWidth && height / 2 > targetHeight)
            {
                width /= 2;
                height /= 2;
                current = Step(current, from, width, height, sampling, trash);
                from = new SKRect(0, 0, width, height);
            }

            return Step(current, from, targetWidth, targetHeight, sampling, trash);
        }

        private SKImage Step(SKImage from, SKRect fromRect, int width, int height, SKSamplingOptions sampling,
                             List<IDisposable> trash)
        {
            SKSurface surface = _soft?.GetOrCreate(width, height);
            if (surface == null)
            {
                surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
                trash.Add(surface);
            }
            surface.Canvas.DrawImage(from, fromRect, new SKRect(0, 0, width, height), sampling);
            SKImage made = surface.Snapshot();
            trash.Add(made);
            return made;
        }

        /// <summary>Runs the shader over a rectangle of the given size, starting at the canvas origin.</summary>
        internal void DrawShaded(SKCanvas canvas, SKRuntimeEffect effect, double width, double height, SKSamplingOptions sampling)
        {
            // Map output pixels back onto the visible part of the frame.
            float sx = (float)(width / _source.Width);
            float sy = (float)(height / _source.Height);
            var local = SKMatrix.CreateScaleTranslation(sx, sy, (float)(-_source.X * sx), (float)(-_source.Y * sy));

            using SKShader source = _frame.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, sampling, local);
            // At the sharp end of the slider the copy would be the picture at its own size, so skip
            // making one and reflect the picture itself.
            bool blurred = _options.Diffuse > 0.02f;
            var trash = new List<IDisposable>();
            SKImage soft = blurred ? SoftCopy(trash) : null;
            using SKShader softSource = !blurred ? null : soft.ToShader(
                SKShaderTileMode.Clamp, SKShaderTileMode.Clamp,
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
                SKMatrix.CreateScale((float)(width / soft.Width), (float)(height / soft.Height)));
            var uniforms = new SKRuntimeEffectUniforms(effect)
            {
                ["dest"] = new[] { (float)width, (float)height },
                ["srcOrigin"] = new[] { (float)_source.X, (float)_source.Y },
                ["srcSize"] = new[] { (float)_source.Width, (float)_source.Height },
                ["tubeRadius"] = CrtShader.TubeRadius(_options.Curvature, (float)(width / Math.Max(height, 1.0)), _options.Trinitron),
                ["tubeCylinder"] = _options.Trinitron ? 1f : 0f,
                ["glow"] = _options.Glow,
                ["bezel"] = _options.Bezel,
                ["cornerRadius"] = _options.Corner ?? 0.16f,
                ["sharp"] = _options.Sharp ? 1f : 0f,
                ["edgeLight"] = _options.EdgeLight,
                ["scanline"] = _options.Scanlines,
                ["vignette"] = _options.Vignette,
            };
            var children = new SKRuntimeEffectChildren(effect) { ["src"] = source, ["srcSoft"] = softSource ?? source };

            using (SKShader shader = effect.ToShader(uniforms, children))
            using (var paint = new SKPaint { Shader = shader })
                canvas.DrawRect(new SKRect(0, 0, (float)width, (float)height), paint);

            for (int i = trash.Count - 1; i >= 0; i--) trash[i].Dispose();
        }
    }
}
