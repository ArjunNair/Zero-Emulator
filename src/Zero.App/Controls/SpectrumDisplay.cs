using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Zero.Emulation.Host;

namespace Zero.App.Controls
{
    /// <summary>
    /// Shows the emulator's frame buffer. The emulation thread never touches this control; the
    /// window copies the latest <see cref="VideoFrame"/> into a WriteableBitmap on the UI thread
    /// (~100k pixels per frame, comfortably within Skia's software path) and we scale it to fit.
    /// </summary>
    public sealed class SpectrumDisplay : Control
    {
        private WriteableBitmap _bitmap;
        private SkiaSharp.SKBitmap _shaderFrame;   // only kept while the CRT shader is running
        private readonly CrtSurfaceCache _crtSurfaces = new CrtSurfaceCache();
        private readonly CrtSurfaceCache _crtGlow = new CrtSurfaceCache();
        private readonly System.Diagnostics.Stopwatch _crtClock = System.Diagnostics.Stopwatch.StartNew();
        private int _borderCrop;
        private ISolidColorBrush _surround = Brushes.Black;

        public static readonly StyledProperty<bool> SmoothProperty =
            AvaloniaProperty.Register<SpectrumDisplay, bool>(nameof(Smooth));

        public static readonly StyledProperty<bool> KeepAspectRatioProperty =
            AvaloniaProperty.Register<SpectrumDisplay, bool>(nameof(KeepAspectRatio), true);

        public static readonly StyledProperty<bool> IntegerScalingProperty =
            AvaloniaProperty.Register<SpectrumDisplay, bool>(nameof(IntegerScaling));

        public bool Smooth { get => GetValue(SmoothProperty); set => SetValue(SmoothProperty, value); }
        public bool KeepAspectRatio { get => GetValue(KeepAspectRatioProperty); set => SetValue(KeepAspectRatioProperty, value); }
        public bool IntegerScaling { get => GetValue(IntegerScalingProperty); set => SetValue(IntegerScalingProperty, value); }

        // Spectrum border geometry: 48 px left/right/top, 56 px bottom around the 256x192 paper.
        private const int SideBorder = 48, BottomBorder = 56;

        /// <summary>
        /// Border pixels to hide on the left, right and top edges (0 = full border, 48 = none). The bottom
        /// edge is cropped in the same proportion of its 56 px so "none" really leaves none.
        /// </summary>
        public int BorderCrop
        {
            get => _borderCrop;
            set { _borderCrop = Math.Clamp(value, 0, SideBorder); InvalidateVisual(); }
        }

        public PixelSize FrameSize => _bitmap?.PixelSize ?? new PixelSize(352, 296);

        /// <summary>The part of the frame actually shown after border cropping.</summary>
        public Rect SourceRect
        {
            get
            {
                PixelSize f = FrameSize;
                int side = Math.Min(_borderCrop, Math.Min(f.Width, f.Height) / 2 - 1);
                int bottom = side * BottomBorder / SideBorder;
                return new Rect(side, side, f.Width - 2 * side, f.Height - side - bottom);
            }
        }

        public long FramesPresented { get; private set; }

        private CrtShaderOptions _crtOptions;

        /// <summary>CRT shader settings. Assigning repaints at once, so it works while paused too.</summary>
        public CrtShaderOptions CrtOptions
        {
            get => _crtOptions;
            set
            {
                if (_crtOptions.Equals(value)) return;
                _crtOptions = value;
                InvalidateVisual();
            }
        }

        private bool UseShader => CrtOptions.Any && CrtShader.IsAvailable;

        /// <summary>Screen pixels per Spectrum pixel at the current window size (1 until first render).</summary>
        public double Scale { get; private set; } = 1;

        static SpectrumDisplay()
        {
            AffectsRender<SpectrumDisplay>(SmoothProperty, KeepAspectRatioProperty, IntegerScalingProperty);
        }

        public SpectrumDisplay()
        {
            ClipToBounds = true;
            Focusable = true;
            ApplyInterpolation();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == SmoothProperty)
                ApplyInterpolation();
        }

        // Must not be called from Render(): changing RenderOptions invalidates the visual.
        private void ApplyInterpolation()
        {
            RenderOptions.SetBitmapInterpolationMode(this, Smooth ? BitmapInterpolationMode.HighQuality : BitmapInterpolationMode.None);
        }

        /// <summary>UI thread: copy a frame into the bitmap and schedule a repaint.</summary>
        public unsafe void Present(VideoFrame frame)
        {
            if (frame == null || frame.Width <= 0 || frame.Height <= 0) return;

            if (_bitmap == null || _bitmap.PixelSize.Width != frame.Width || _bitmap.PixelSize.Height != frame.Height)
            {
                _bitmap?.Dispose();
                _bitmap = new WriteableBitmap(new PixelSize(frame.Width, frame.Height), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Opaque);
            }

            using (ILockedFramebuffer fb = _bitmap.Lock())
            {
                int[] src = frame.Pixels;
                byte* basePtr = (byte*)fb.Address;
                int w = frame.Width;
                for (int y = 0; y < frame.Height; y++)
                {
                    int* dst = (int*)(basePtr + y * fb.RowBytes);
                    int rowStart = y * w;
                    for (int x = 0; x < w; x++)
                        dst[x] = src[rowStart + x] | unchecked((int)0xFF000000); // core emits 0x00RRGGBB
                }
            }

            // Letterbox areas take the current border colour so odd window shapes look like a TV, not a defect.
            uint rgb = (uint)frame.Pixels[0] & 0xFFFFFF;
            if (_surround.Color.ToUInt32() != (0xFF000000u | rgb))
                _surround = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF000000u | rgb));

            // The Skia copy is kept whether or not the shader is running, so switching the effects on
            // shows the current picture immediately instead of waiting for the next frame.
            if (_shaderFrame == null || _shaderFrame.Width != frame.Width || _shaderFrame.Height != frame.Height)
            {
                _shaderFrame?.Dispose();
                _shaderFrame = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(frame.Width, frame.Height,
                    SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Opaque));
            }
            unsafe
            {
                int* dst = (int*)_shaderFrame.GetPixels();
                int[] src = frame.Pixels;
                for (int i = 0; i < src.Length; i++) dst[i] = src[i] | unchecked((int)0xFF000000);
            }

            FramesPresented++;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            context.FillRectangle(_bitmap == null ? Brushes.Black : _surround, new Rect(Bounds.Size));
            if (_bitmap == null) return;

            Rect source = SourceRect;

            Rect dest;
            if (KeepAspectRatio)
            {
                double scale = Math.Min(Bounds.Width / source.Width, Bounds.Height / source.Height);
                if (IntegerScaling && scale >= 1) scale = Math.Floor(scale);
                double w = source.Width * scale, h = source.Height * scale;
                dest = new Rect((Bounds.Width - w) / 2, (Bounds.Height - h) / 2, w, h);
            }
            else
            {
                dest = new Rect(Bounds.Size);
            }

            Scale = dest.Width / source.Width;

            if (UseShader && _shaderFrame != null)
            {
                context.Custom(new CrtDrawOperation(new Rect(Bounds.Size), _shaderFrame, source, dest, CrtOptions, Smooth, _crtSurfaces, (float)_crtClock.Elapsed.TotalSeconds, _crtGlow));
                return;
            }
            context.DrawImage(_bitmap, source, dest);
        }
    }
}
