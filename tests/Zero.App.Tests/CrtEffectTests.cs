using System;
using System.Threading;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Xunit;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.App.Tests
{
    /// <summary>
    /// The CRT overlay is cosmetic, so the only thing worth asserting is that turning it on actually
    /// changes the pixels over the emulated screen, and that turning it off puts them back.
    /// </summary>
    public class CrtEffectTests
    {
        public CrtEffectTests()
        {
            MainWindow.SettingsLoader = () =>
            {
                var s = new EmulatorSettings();
                s.Paths.Roms = TestPaths.RomDir;
                s.Emulation.PauseOnFocusLost = false;
                s.Audio.Mute = true;
                s.Render.Crt = new CrtSettings { Enabled = false, Scanlines = true, Vignette = true };
                return s;
            };
        }

        private static unsafe long Fingerprint(WriteableBitmap bmp, int top, int bottom)
        {
            using ILockedFramebuffer fb = bmp.Lock();
            long sum = 0;
            for (int y = top; y < bottom && y < fb.Size.Height; y++)
            {
                uint* line = (uint*)((byte*)fb.Address + y * fb.RowBytes);
                for (int x = 0; x < fb.Size.Width; x += 3) sum += line[x] & 0xFFFFFF;
            }
            return sum;
        }

        private static long Capture(MainWindow w, string save = null)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using WriteableBitmap frame = w.CaptureRenderedFrame();
            if (save != null) frame.Save(System.IO.Path.Combine(MainWindowTests.ScreenshotDir, save + ".png"));
            // Sample the emulated screen, below the menu bar and above the status bar.
            return Fingerprint(frame, 60, frame.PixelSize.Height - 40);
        }

        [AvaloniaFact]
        public void Turning_the_overlay_on_changes_the_screen_and_off_restores_it()
        {
            var w = new MainWindow();
            w.Show();
            long target = w.Session.FrameCount + 250; // past the ROM memory test, so the screen has a picture on it
            while (w.Session.FrameCount < target) { Thread.Sleep(10); Dispatcher.UIThread.RunJobs(); }
            w.Session.Pause();          // freeze the emulation so only the overlay can change pixels
            Thread.Sleep(50);
            Dispatcher.UIThread.RunJobs();

            long plain = Capture(w, "crt-off");

            w.Session.Settings.Render.Crt.Enabled = true;
            w.ApplyCrtSettings();
            long withEffects = Capture(w, "crt-on");

            w.Session.Settings.Render.Crt.Enabled = false;
            w.ApplyCrtSettings();
            long plainAgain = Capture(w);

            Assert.True(w.CrtLayer != null);
            Assert.NotEqual(plain, withEffects);
            Assert.Equal(plain, plainAgain);
            w.Close();
        }
    }
}
