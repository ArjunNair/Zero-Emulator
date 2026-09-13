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

namespace Zero.App.Tests
{
    public class TapeDeckWindowTests
    {
        private readonly ITestOutputHelper _output;
        public TapeDeckWindowTests(ITestOutputHelper output)
        {
            _output = output;
            MainWindow.SettingsLoader = () => { var s = new EmulatorSettings(); s.Paths.Roms = TestPaths.RomDir; s.Emulation.PauseOnFocusLost = false; s.Audio.Mute = true; return s; };
        }

        [AvaloniaFact]
        public void Tape_deck_lists_blocks_and_follows_transport()
        {
            var w = new MainWindow();
            w.Show();
            long t = w.Session.FrameCount + 20;
            while (w.Session.FrameCount < t) { Thread.Sleep(10); Dispatcher.UIThread.RunJobs(); }

            string tap = Path.Combine(TestPaths.ProgramsDir, "Demos", "Overscan.tap");
            Assert.True(w.Session.LoadFileAsync(tap).Result);
            var deck = new Windows.TapeDeckWindow(w.Session, () => System.Threading.Tasks.Task.CompletedTask);
            deck.Show(w);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal("Overscan", deck.TitleText.Text);
            Assert.Equal(3 * 4, deck.BlockList.ItemCount); // (PULS+DATA+PAUS) x 4 TAP blocks; the PZXT header is not a playable block
            Assert.True(deck.PlayButton.IsEnabled);
            Assert.False(deck.StopButton.IsEnabled);

            w.Session.InvokeAsync(w.Session.Tape.Play).Wait();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!deck.StopButton.IsEnabled && sw.ElapsedMilliseconds < 3000) { Thread.Sleep(10); Dispatcher.UIThread.RunJobs(); }
            Assert.True(deck.StopButton.IsEnabled);
            Assert.Contains("Playing", deck.StatusText.Text);

            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using (var bmp = deck.CaptureRenderedFrame())
            {
                string path = Path.Combine(MainWindowTests.ScreenshotDir, "tape-deck.png");
                bmp.Save(path);
                _output.WriteLine("screenshot: " + path);
            }
            deck.Close();
            w.Close();
        }
    }
}

namespace Zero.App.Tests
{
    public class OptionsWindowTests
    {
        [AvaloniaFact]
        public void Ok_writes_edits_back_and_flags_rom_changes()
        {
            var settings = new EmulatorSettings();
            var w = new Windows.OptionsWindow(settings, null);
            w.Show();
            Dispatcher.UIThread.RunJobs();

            w.CpuMultiplier.Value = 4;
            w.Gamepad2.SelectedIndex = 2;
            w.ConfirmOnExit.IsChecked = false;
            w.OkButton.Command = null;
            w.OkButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.True(w.Accepted);
            Assert.False(w.RomsChanged);
            Assert.Equal(4, settings.Emulation.CpuMultiplier);
            Assert.Equal(2, settings.Input.Gamepad2Emulates);
            Assert.False(settings.Emulation.ConfirmOnExit);

            var w2 = new Windows.OptionsWindow(settings, null);
            w2.Show();
            w2.Rom48k.Text = "custom48.rom";
            w2.OkButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(w2.RomsChanged);
            Assert.Equal("custom48.rom", settings.Roms.Rom48k);
        }

        [AvaloniaFact]
        public void Cancel_leaves_settings_untouched()
        {
            var settings = new EmulatorSettings();
            var w = new Windows.OptionsWindow(settings, null);
            w.Show();
            w.CpuMultiplier.Value = 9;
            w.CancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(w.Accepted);
            Assert.Equal(1, settings.Emulation.CpuMultiplier);
        }
    }
}

namespace Zero.App.Tests
{
    public class TapeMetadataDisplayTests
    {
        [Fact]
        public void Describe_metadata_joins_available_fields()
        {
            var m = new Zero.Emulation.Tape.TapeMetadata { Publisher = "Bug-Byte", Authors = new[] { "Matthew Smith" }, Year = "1983", Comments = new[] { "Original release" } };
            string text = Windows.TapeDeckWindow.DescribeMetadata(m);
            Assert.Equal("by Matthew Smith  ·  Bug-Byte  ·  1983\nOriginal release", text);
            Assert.Equal("", Windows.TapeDeckWindow.DescribeMetadata(new Zero.Emulation.Tape.TapeMetadata()));
        }
    }
}

namespace Zero.App.Tests
{
    public class GamepadWindowTests
    {
        [AvaloniaFact]
        public void Ok_stores_edited_binding()
        {
            var settings = new EmulatorSettings();
            var w = new Windows.GamepadWindow(settings, null, 1);
            w.Show();
            Dispatcher.UIThread.RunJobs();
            w.SetAction(Zero.Emulation.Input.GamepadButtons.Guide, "Key:Q");
            w.OkButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(w.Accepted);
            Assert.Equal("Key:Q", settings.Input.Gamepad2Buttons.ActionFor(Zero.Emulation.Input.GamepadButtons.Guide));
            Assert.Equal("Fire 1", settings.Input.Gamepad1Buttons.ActionFor(Zero.Emulation.Input.GamepadButtons.South));
        }
    }

    public class MouseCaptureTests
    {
        public MouseCaptureTests()
        {
            MainWindow.SettingsLoader = () =>
            {
                var s = new EmulatorSettings();
                s.Paths.Roms = TestPaths.RomDir; s.Emulation.PauseOnFocusLost = false; s.Audio.Mute = true;
                s.Input.EnableKempstonMouse = true;
                return s;
            };
        }

        [AvaloniaFact]
        public void Click_captures_moves_feed_the_mouse_and_escape_releases()
        {
            var w = new MainWindow();
            w.Show();
            long t = w.Session.FrameCount + 20;
            while (w.Session.FrameCount < t) { Thread.Sleep(10); Dispatcher.UIThread.RunJobs(); }

            var origin = w.Display.Bounds.TopLeft;
            var p = new Avalonia.Point(origin.X + 100, origin.Y + 100);
            w.MouseDown(p, MouseButton.Left); w.MouseUp(p, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(w.MouseCaptured);

            byte x0 = w.Session.InvokeAsync(() => w.Session.KempstonMouseDevice.MouseX).Result;
            w.MouseMove(new Avalonia.Point(p.X + 40, p.Y));
            Dispatcher.UIThread.RunJobs();
            t = w.Session.FrameCount + 3;
            while (w.Session.FrameCount < t) { Thread.Sleep(10); Dispatcher.UIThread.RunJobs(); }
            byte x1 = w.Session.InvokeAsync(() => w.Session.KempstonMouseDevice.MouseX).Result;
            Assert.NotEqual(x0, x1);

            w.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            w.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.False(w.MouseCaptured);
            w.Close();
        }
    }
}

namespace Zero.App.Tests
{
    public class ToolWindowTests
    {
        private readonly ITestOutputHelper _output;
        public ToolWindowTests(ITestOutputHelper output) { _output = output; }

        [AvaloniaFact]
        public void Keyboard_window_shows_picture_and_keywords()
        {
            var w = new Windows.KeyboardWindow(null);
            w.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(w.KeyboardImage.Source);
            Assert.Equal("ABS", w.KeywordBox.SelectedItem);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using (var bmp = w.CaptureRenderedFrame())
            {
                string path = Path.Combine(MainWindowTests.ScreenshotDir, "keyboard.png");
                bmp.Save(path);
                _output.WriteLine("screenshot: " + path);
            }
            w.Close();
        }

        [AvaloniaFact]
        public void Load_binary_window_validates_input()
        {
            var w = new Windows.LoadBinaryWindow(null, false);
            w.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.False(w.BankRadio.IsEnabled); // no session: treated like a 48K
            w.GoButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(w.ErrorText.IsVisible);
            Assert.Contains("Choose a file", w.ErrorText.Text);
            w.FileBox.Text = "/definitely/not/here.bin";
            w.GoButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("not found", w.ErrorText.Text);
            Assert.Equal(-1, w.BytesTransferred);
            w.Close();
        }
    }
}
