using System;
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
        public float Reflection;
        /// <summary>How much of the window the cabinet round the screen takes, 0 for none.</summary>
        public float Bezel;
        /// <summary>Light from the picture spilling onto the surround, instead of plain black.</summary>
        public float EdgeLight;
        public float Scanlines;
        public float Vignette;
        public float Flicker;
        public float Noise;

        public bool Any => Enabled && (Curvature > 0 || Glow > 0 || Reflection > 0 || Bezel > 0 || EdgeLight > 0
                                       || Scanlines > 0 || Vignette > 0 || Flicker > 0 || Noise > 0);

        public bool Equals(CrtShaderOptions other) =>
            Enabled == other.Enabled && Curvature.Equals(other.Curvature) && Glow.Equals(other.Glow)
            && Reflection.Equals(other.Reflection) && Bezel.Equals(other.Bezel) && EdgeLight.Equals(other.EdgeLight)
            && Scanlines.Equals(other.Scanlines) && Vignette.Equals(other.Vignette)
            && Flicker.Equals(other.Flicker) && Noise.Equals(other.Noise);
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
uniform float time;         // seconds, for the effects that move
uniform float curvature;
uniform float glow;
uniform float reflection;
uniform float bezel;        // share of the window given to the housing the screen sits in
uniform float edgeLight;
uniform float scanline;
uniform float vignette;
uniform float flicker;
uniform float noise;

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
    float2 c = c0 * (1.0 + curvature * dot(c0, c0) * 0.25);   // bulge the glass

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
    half4 col = src.eval(p);

    if (glow > 0.0) {                                 // phosphor bleeding into the neighbouring lines
        float rows = max(srcSize.y, 1.0);
        float step = dest.y / rows;
        half4 bleed = src.eval(clamp(p - float2(0.0, step), lo, hi)) * 0.6
                    + src.eval(clamp(p + float2(0.0, step), lo, hi)) * 0.4;
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
    if (flicker > 0.0) {                              // mains hum on the brightness
        picture *= 1.0 - flicker * 0.12 * (0.5 + 0.5 * sin(time * 37.0));
    }
    if (noise > 0.0) {
        float n = hash(floor(p) + floor(time * 24.0));
        picture += half3(half((n - 0.5) * noise * 0.18));
    }
    if (reflection > 0.0) {                           // a soft sheen across the glass
        // Measured in units of the screen's height, so the highlight stays round whatever shape the
        // window is rather than stretching with it, and faded out before the rim. A highlight that
        // runs off an edge lights that edge instead of falling away, which is what made the left and
        // top of the screen brighter than the right and bottom.
        float2 q = (uv - float2(0.32, 0.26)) * float2(dest.x / max(dest.y, 1.0), 1.0);
        float sheen = clamp(1.0 - length(q) * 2.2, 0.0, 1.0);
        float contain = smoothstep(0.0, 0.45, 1.0 - max(abs(c.x), abs(c.y)));
        picture += half3(half(sheen * sheen * contain * reflection * 0.3));
    }

    // Blend across the rim rather than switching at it, or the curve stair-steps. 'pixel' is one
    // output pixel expressed in this -1..1 space; three of them is the narrowest band that still
    // reads as smooth once the software path scales its output up.
    float beyond = distance(c, inside);
    float pixel = 2.0 / max(min(dest.x, dest.y), 1.0);
    float rim = smoothstep(0.0, pixel * 3.0, beyond);

    // The surround, lit by the picture it frames -- as a mirror of it. Clamping to the glass gives
    // each place in the surround the picture directly in front of it, which is right along a panel
    // but not across one: the edge pixel then repeats outwards, all the way to the frame, and a dark
    // pixel at the edge of the picture draws a bar. Reflecting instead of clamping steps a pixel back
    // into the picture for every pixel out across the panel, so the panel carries the edge of the
    // picture turned back on itself. At the glass the two agree, so the seam stays invisible.
    //
    // 'reach' is how far out we are as a fraction of the panel's own depth: 0 against the glass, 1 at
    // the frame. A plain distance leaves the corners dark, because the curve cuts a corner about four
    // times deeper than it cuts the sides, so light fading over the width of a side panel has run out
    // long before it crosses a corner.
    float2 qc = c0;
    for (int k = 0; k < 2; ++k) {
        float wq = 1.0 + curvature * dot(qc, qc) * 0.25;
        qc = clamp(qc * wq, -1.0, 1.0) / wq;
    }
    float2 t = clamp((abs(c0) - abs(qc)) / max(outer - abs(qc), float2(0.0001, 0.0001)), 0.0, 1.0);

    // The housing is four flat faces meeting at mitres, the way a moulding is cut. Which face a
    // point belongs to is whichever it is further past in units of that face's own depth, so the
    // seam between two of them runs out from the corner of the screen to the corner of the window.
    float2 w = c0 * opening;                 // back to -1..1 across the window
    float sideFace = step(t.y, t.x);         // 1 on the left and right faces, 0 on the top and bottom
    float depth = mix(t.y, t.x, sideFace);   // into this face: 0 against the glass, 1 at the window

    // Each face takes a slightly different shade, as though lit from above: that difference is the
    // only thing that makes a mitre visible, and without it the housing is one flat rectangle.
    float shade = sideFace > 0.5 ? (w.x < 0.0 ? 0.013 : -0.013) : (w.y < 0.0 ? 0.030 : -0.018);
    half3 housing = half3(half(0.055 + shade));
    housing += half3(half(0.045 * exp(-depth * 26.0)));   // the lip round the opening, standing proud

    // The screen's own light on it, close in. It falls away far faster than it used to: over the
    // depth of a housing rather than the width of a thin panel, it washed the whole moulding out.
    // Each face's light also fades towards its ends, or the two meeting at a corner both light it
    // and the corner carries a third reflection -- the picture turned back on itself diagonally.
    float2 along = exp(-2.2 * w * w);        // 1 opposite the middle of a face, small towards its ends
    float sides = exp(-t.x * 3.2) * along.y;     // light from the left and right edges
    float ends = exp(-t.y * 3.2) * along.x;      // and from the top and bottom
    float2 reflected = clamp(2.0 * inside - c, -1.0, 1.0);
    float2 mirror = clamp((reflected * 0.5 + 0.5) * dest, lo, hi);

    // The brighter of the two, not the one belonging to this face. Which face a point is on switches
    // hard at the mitre -- that is what draws the seam -- but light does not, and picking the light
    // that way with it put a hard edge down the diagonal of every corner.
    half3 spill = housing + srcSoft.eval(mirror).rgb * half(max(sides, ends)) * edgeLight;

    return half4(mix(picture, spill, half(rim)), 1.0);
}";


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
        private SKSurface _surface;
        private int _width, _height;

        public SKSurface GetOrCreate(int width, int height)
        {
            if (_surface != null && _width == width && _height == height) return _surface;
            _surface?.Dispose();
            _surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            _width = width;
            _height = height;
            return _surface;
        }

        public void Dispose()
        {
            _surface?.Dispose();
            _surface = null;
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
        private readonly float _time;

        public CrtDrawOperation(Rect bounds, SKBitmap frame, Rect source, Rect dest, CrtShaderOptions options, bool smooth, CrtSurfaceCache cache, float time, CrtSurfaceCache soft = null)
        {
            _cache = cache;
            _soft = soft;
            _time = time;
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
        /// A sixth-size copy of the visible part of the frame, for the housing to reflect.
        ///
        /// Light off a moulding is diffuse, and reflecting the picture pixel for pixel gives back a
        /// second, sharp copy of whatever is at the edge of the screen. Blurring it at the source
        /// costs one small draw, where blurring it at the sampling site costs a tap for every pixel
        /// of housing and brings its own trouble with it.
        ///
        /// It holds only the visible part, so the cropped-away border cannot reach it at all.
        /// </summary>
        private SKImage SoftCopy(out SKSurface owned)
        {
            int w = Math.Max(1, (int)Math.Round(_source.Width / 6));
            int h = Math.Max(1, (int)Math.Round(_source.Height / 6));

            SKSurface surface = _soft?.GetOrCreate(w, h);
            owned = surface == null
                ? SKSurface.Create(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul))
                : null;                                  // the cache owns it; do not dispose it here
            surface ??= owned;

            using SKImage whole = SKImage.FromBitmap(_frame);
            surface.Canvas.DrawImage(whole,
                new SKRect((float)_source.X, (float)_source.Y, (float)_source.Right, (float)_source.Bottom),
                new SKRect(0, 0, w, h),
                // A wide resampler, not plain bilinear: bilinear reads four pixels whatever the
                // reduction, so shrinking this far with it drops most of the picture on the floor
                // instead of averaging it, and single characters land whole on single pixels.
                new SKSamplingOptions(SKCubicResampler.Mitchell));
            return surface.Snapshot();
        }

        /// <summary>Runs the shader over a rectangle of the given size, starting at the canvas origin.</summary>
        internal void DrawShaded(SKCanvas canvas, SKRuntimeEffect effect, double width, double height, SKSamplingOptions sampling)
        {
            // Map output pixels back onto the visible part of the frame.
            float sx = (float)(width / _source.Width);
            float sy = (float)(height / _source.Height);
            var local = SKMatrix.CreateScaleTranslation(sx, sy, (float)(-_source.X * sx), (float)(-_source.Y * sy));

            using SKShader source = _frame.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, sampling, local);
            using SKImage soft = SoftCopy(out SKSurface owned);
            using SKSurface disposeWithUs = owned;
            using SKShader softSource = soft.ToShader(
                SKShaderTileMode.Clamp, SKShaderTileMode.Clamp,
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None),
                SKMatrix.CreateScale((float)(width / soft.Width), (float)(height / soft.Height)));
            var uniforms = new SKRuntimeEffectUniforms(effect)
            {
                ["dest"] = new[] { (float)width, (float)height },
                ["srcOrigin"] = new[] { (float)_source.X, (float)_source.Y },
                ["srcSize"] = new[] { (float)_source.Width, (float)_source.Height },
                ["time"] = _time,
                ["curvature"] = _options.Curvature,
                ["glow"] = _options.Glow,
                ["reflection"] = _options.Reflection,
                ["bezel"] = _options.Bezel,
                ["edgeLight"] = _options.EdgeLight,
                ["scanline"] = _options.Scanlines,
                ["vignette"] = _options.Vignette,
                ["flicker"] = _options.Flicker,
                ["noise"] = _options.Noise,
            };
            var children = new SKRuntimeEffectChildren(effect) { ["src"] = source, ["srcSoft"] = softSource };

            using SKShader shader = effect.ToShader(uniforms, children);
            using var paint = new SKPaint { Shader = shader };
            canvas.DrawRect(new SKRect(0, 0, (float)width, (float)height), paint);
        }
    }
}
