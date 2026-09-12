using System;
using System.IO;
using System.Threading;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Xunit;
using Zero.TestSupport;
using Zero.Emulation.Settings;

namespace Zero.App.Tests
{
    /// <summary>
    /// Drives the real main window on Avalonia's headless platform: the emulation thread runs for
    /// real, frames are presented through the real display control, key events go through the real
    /// key map, and the rendered window is captured to PNG for eyeballing.
    /// </summary>
    public class MainWindowTests
    {
        private readonly ITestOutputHelper _output;

        public MainWindowTests(ITestOutputHelper output)
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

        public static string ScreenshotDir
        {
            get
            {
                string dir = Environment.GetEnvironmentVariable("ZERO_SCREENSHOT_DIR");
                if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Path.GetTempPath(), "zero-screenshots");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static void PumpUntil(Func<bool> done, int timeoutMs)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!done())
            {
                if (sw.ElapsedMilliseconds > timeoutMs) throw new TimeoutException("condition not met in " + timeoutMs + " ms");
                Thread.Sleep(10);
                Dispatcher.UIThread.RunJobs();
            }
        }

        private static void WaitFrames(MainWindow w, long frames)
        {
            long target = w.Session.FrameCount + frames;
            PumpUntil(() => w.Session.FrameCount >= target, 60000);
        }

        private string Capture(MainWindow w, string name)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using (WriteableBitmap bmp = w.CaptureRenderedFrame())
            {
                string path = Path.Combine(ScreenshotDir, name + ".png");
                bmp.Save(path);
                _output.WriteLine("screenshot: " + path);
                return path;
            }
        }

        private static void Tap(MainWindow w, PhysicalKey key, RawInputModifiers mods = RawInputModifiers.None)
        {
            w.KeyPressQwerty(key, mods);
            Dispatcher.UIThread.RunJobs();
            WaitFrames(w, 4); // let the ROM's keyboard scan see the key down
            w.KeyReleaseQwerty(key, mods);
            Dispatcher.UIThread.RunJobs();
            WaitFrames(w, 4);
        }

        [AvaloniaFact]
        public void Window_boots_48k_and_renders_frames()
        {
            var w = new MainWindow();
            w.Show();
            WaitFrames(w, 250);
            PumpUntil(() => w.Display.FramesPresented > 10, 10000);

            string screen = w.Session.InvokeAsync(() => ScreenText.Dump(w.Session.Machine)).Result;
            Assert.Contains("© 1982 Sinclair Research Ltd", screen);
            _output.WriteLine("audio fallback: " + w.AudioFallback);

            string shot = Capture(w, "boot-48k");
            Assert.True(new FileInfo(shot).Length > 1000);
            w.Close();
        }

        [AvaloniaFact]
        public void Typing_through_avalonia_reaches_the_spectrum()
        {
            var w = new MainWindow();
            w.Show();
            WaitFrames(w, 250);
            w.Display.Focus();

            // J gives the LOAD keyword; Symbol Shift (Ctrl) + P types a quote.
            Tap(w, PhysicalKey.J);
            w.KeyPressQwerty(PhysicalKey.ControlLeft, RawInputModifiers.Control);
            Tap(w, PhysicalKey.P, RawInputModifiers.Control);
            Tap(w, PhysicalKey.P, RawInputModifiers.Control);
            w.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
            // Shift+1 is "!" on a PC keyboard -> Symbol Shift + 1 on the Spectrum.
            w.KeyPressQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.Shift);
            Tap(w, PhysicalKey.Digit1, RawInputModifiers.Shift);
            w.KeyReleaseQwerty(PhysicalKey.ShiftLeft, RawInputModifiers.None);
            WaitFrames(w, 10);

            string screen = w.Session.InvokeAsync(() => ScreenText.Dump(w.Session.Machine)).Result;
            _output.WriteLine(screen);
            Assert.Contains("LOAD \"\"!", screen);
            Capture(w, "typed-load");
            w.Close();
        }

        [AvaloniaFact]
        public void Menu_switches_machine_and_status_bar_follows()
        {
            var w = new MainWindow();
            w.Show();
            WaitFrames(w, 20);
            w.Session.SwitchMachine(SpeccyCommon.MachineModel._128k);
            WaitFrames(w, 250);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(SpeccyCommon.MachineModel._128k, w.Session.Model);
            Assert.Contains("128K", w.StatusMachine.Text);
            string screen = w.Session.InvokeAsync(() => ScreenText.Dump(w.Session.Machine)).Result;
            Assert.Contains("128 BASIC", screen);
            Capture(w, "boot-128k");
            w.Close();
        }

        [AvaloniaFact]
        public void Loading_a_tap_via_session_shows_in_status()
        {
            var w = new MainWindow();
            w.Show();
            WaitFrames(w, 20);
            string tap = Path.Combine(TestPaths.ProgramsDir, "Demos", "Overscan.tap");
            Assert.True(w.Session.LoadFileAsync(tap).Result);
            PumpUntil(() => w.StatusTape.Text != null && w.StatusTape.Text.Contains("Overscan"), 5000);
            WaitFrames(w, 400);
            Capture(w, "overscan-loaded");
            w.Close();
        }
    }
}
