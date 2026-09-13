using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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
        private int _borderCrop;

        public static readonly StyledProperty<bool> SmoothProperty =
            AvaloniaProperty.Register<SpectrumDisplay, bool>(nameof(Smooth));

        public static readonly StyledProperty<bool> KeepAspectRatioProperty =
            AvaloniaProperty.Register<SpectrumDisplay, bool>(nameof(KeepAspectRatio), true);

        public static readonly StyledProperty<bool> IntegerScalingProperty =
            AvaloniaProperty.Register<SpectrumDisplay, bool>(nameof(IntegerScaling));

        public bool Smooth { get => GetValue(SmoothProperty); set => SetValue(SmoothProperty, value); }
        public bool KeepAspectRatio { get => GetValue(KeepAspectRatioProperty); set => SetValue(KeepAspectRatioProperty, value); }
        public bool IntegerScaling { get => GetValue(IntegerScalingProperty); set => SetValue(IntegerScalingProperty, value); }

        /// <summary>Pixels of border to hide on each edge.</summary>
        public int BorderCrop
        {
            get => _borderCrop;
            set { _borderCrop = Math.Max(0, value); InvalidateVisual(); }
        }

        public PixelSize FrameSize => _bitmap?.PixelSize ?? new PixelSize(352, 296);

        public long FramesPresented { get; private set; }

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

            FramesPresented++;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
            if (_bitmap == null) return;

            int crop = Math.Min(_borderCrop, Math.Min(_bitmap.PixelSize.Width, _bitmap.PixelSize.Height) / 2 - 1);
            var source = new Rect(crop, crop, _bitmap.PixelSize.Width - 2 * crop, _bitmap.PixelSize.Height - 2 * crop);

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
            context.DrawImage(_bitmap, source, dest);
        }
    }
}
