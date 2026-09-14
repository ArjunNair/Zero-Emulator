using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
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
    /// The window chrome must stay legible under every theme and appearance. A hard-coded black
    /// window background once hid the whole menu bar when the app stopped forcing dark mode, and no
    /// test noticed, because the menu items still existed and reported the right text.
    /// </summary>
    public class ChromeVisibilityTests
    {
        private readonly ITestOutputHelper _output;

        public ChromeVisibilityTests(ITestOutputHelper output)
        {
            _output = output;
            MainWindow.SettingsLoader = () =>
            {
                var s = new EmulatorSettings();
                s.Paths.Roms = TestPaths.RomDir;
                s.Emulation.PauseOnFocusLost = false;
                s.Audio.Mute = true;
                return s;
            };
        }

        /// <summary>Pixels in the strip that differ clearly from its most common colour, i.e. drawn text.</summary>
        private static unsafe int ContrastingPixels(WriteableBitmap bmp, int top, int bottom)
        {
            using ILockedFramebuffer fb = bmp.Lock();
            var counts = new Dictionary<uint, int>();
            var rows = new List<uint[]>();
            int width = fb.Size.Width;
            for (int y = top; y < bottom && y < fb.Size.Height; y++)
            {
                var row = new uint[width];
                uint* line = (uint*)((byte*)fb.Address + y * fb.RowBytes);
                for (int x = 0; x < width; x++)
                {
                    uint px = line[x] | 0xFF000000;
                    row[x] = px;
                    counts[px] = counts.TryGetValue(px, out int c) ? c + 1 : 1;
                }
                rows.Add(row);
            }
            if (counts.Count == 0) return 0;

            uint dominant = 0; int best = -1;
            foreach (KeyValuePair<uint, int> kv in counts)
                if (kv.Value > best) { best = kv.Value; dominant = kv.Key; }

            int Channel(uint p, int shift) => (int)((p >> shift) & 0xFF);
            int different = 0;
            foreach (uint[] row in rows)
                foreach (uint px in row)
                {
                    int delta = Math.Abs(Channel(px, 0) - Channel(dominant, 0))
                              + Math.Abs(Channel(px, 8) - Channel(dominant, 8))
                              + Math.Abs(Channel(px, 16) - Channel(dominant, 16));
                    if (delta > 120) different++;
                }
            return different;
        }

        [AvaloniaFact]
        public void Menu_bar_and_status_bar_are_legible()
        {
            var w = new MainWindow();
            w.Show();
            long target = w.Session.FrameCount + 20;
            while (w.Session.FrameCount < target) { Thread.Sleep(10); Dispatcher.UIThread.RunJobs(); }
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            using WriteableBitmap frame = w.CaptureRenderedFrame();
            int menuBottom = (int)Math.Round(w.MenuBar.Bounds.Height);
            Assert.True(menuBottom > 8, $"menu bar is only {menuBottom}px tall");

            int menuInk = ContrastingPixels(frame, 1, menuBottom - 1);
            int statusTop = frame.PixelSize.Height - 20;
            int statusInk = ContrastingPixels(frame, statusTop, frame.PixelSize.Height - 1);
            _output.WriteLine($"theme={Environment.GetEnvironmentVariable("ZERO_THEME")} menu ink={menuInk} status ink={statusInk}");

            // Seven menu titles and a status line: if the text matches its background, these collapse to nearly zero.
            Assert.True(menuInk > 200, $"menu bar text is not visible against its background (only {menuInk} contrasting pixels)");
            Assert.True(statusInk > 100, $"status bar text is not visible against its background (only {statusInk} contrasting pixels)");
            w.Close();
        }
    }
}
