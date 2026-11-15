using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using Zero.App.Controls;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.App.Tests
{
    /// <summary>
    /// The hi-fi deck under the screen: its lights follow the session, its keys and knobs drive it,
    /// and showing or hiding it leaves the emulated screen the size it was.
    /// </summary>
    public class DeckPanelTests
    {
        private readonly ITestOutputHelper _output;

        public DeckPanelTests(ITestOutputHelper output)
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

        private static void PumpUntil(Func<bool> done, int timeoutMs = 5000)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!done())
            {
                if (sw.ElapsedMilliseconds > timeoutMs) throw new TimeoutException("condition not met in " + timeoutMs + " ms");
                Thread.Sleep(10);
                Dispatcher.UIThread.RunJobs();
            }
        }

        private static MainWindow Boot()
        {
            var w = new MainWindow();
            w.Show();
            long target = w.Session.FrameCount + 20;
            PumpUntil(() => w.Session.FrameCount >= target, 30000);
            Dispatcher.UIThread.RunJobs();
            return w;
        }

        private static void Press(Button key) => key.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        /// <summary>Where a control's centre lands in window coordinates, through the deck's Viewbox.</summary>
        private static Point CentreOf(Control c, Window w) =>
            c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w) ?? throw new InvalidOperationException("not in window");

        /// <summary>A drag of this many deck pixels, in window pixels: the decks are scaled to fit.</summary>
        private static Vector Up(MainWindow w, double deckPixels) =>
            new Vector(0, -deckPixels * w.DeckViewbox.Bounds.Width / w.Deck.DesiredSize.Width);

        private static NativeMenuItem MenuItem(Window w, params string[] path)
        {
            NativeMenu menu = NativeMenu.GetMenu(w);
            NativeMenuItem item = null;
            foreach (string header in path)
            {
                item = menu.Items.OfType<NativeMenuItem>().Single(i => i.Header == header);
                menu = item.Menu;
            }
            return item;
        }

        private void Capture(MainWindow w, string name)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using WriteableBitmap bmp = w.CaptureRenderedFrame();
            string path = Path.Combine(MainWindowTests.ScreenshotDir, name + ".png");
            bmp.Save(path);
            _output.WriteLine("screenshot: " + path);
        }

        [Theory]
        [InlineData(3, 12, "03/12")]
        [InlineData(42, 99, "42/99")]
        [InlineData(7, 100, "007/100")]
        [InlineData(42, 1234, "0042/1234")]
        [InlineData(12000, 12000, "9999/9999")]
        public void Block_readout_widens_to_four_digits_for_long_tapes(int block, int count, string shown)
        {
            Assert.Equal(shown, DeckPanel.BlockReadout(block, count));
        }

        [AvaloniaFact]
        public void Lights_and_display_follow_the_tape_and_the_keys_drive_it()
        {
            MainWindow w = Boot();
            var d = w.Deck;
            Assert.True(w.DeckShelf.IsVisible);
            Assert.False(w.StatusBar.IsVisible); // the deck takes its place
            Assert.True(d.PowerLed.IsLit);
            Assert.False(d.RecLamp.IsLit);
            Assert.StartsWith("v", d.VersionText.Text);
            Assert.Equal("000", d.VfdCounter.Text);
            Assert.False(d.PlayKey.IsEnabled);

            string tap = Path.Combine(TestPaths.ProgramsDir, "Demos", "Overscan.tap");
            Assert.True(w.Session.LoadFileAsync(tap).Result);
            PumpUntil(() => d.VfdBlock.Text == "01/13"); // the PZXT header row, then PULS/DATA/PAUS for each of 4 TAP blocks
            Assert.Equal("000", d.VfdCounter.Text);
            Assert.Equal("LOADED OVERSCAN.TAP", d.VfdTitle.Text); // status messages borrow the info line

            Press(d.PlayKey);
            PumpUntil(() => !d.VfdPlay.Classes.Contains("off"));
            Assert.True(d.VfdStop.Classes.Contains("off"));
            Assert.True(d.VfdRzx.Classes.Contains("off"));     // it is the tape playing, not a recording

            // The counter only moves while the tape runs. Fast forward so it gets going quickly.
            w.FullSpeedItem.Command.Execute(null);
            PumpUntil(() => w.Session.Tape.Counter >= 2, 30000);
            w.FullSpeedItem.Command.Execute(null);
            PumpUntil(() => d.VfdCounter.Text != "000");
            Capture(w, "deck-playing");

            Press(d.StopKey);
            PumpUntil(() => !d.VfdStop.Classes.Contains("off"));
            Assert.True(d.VfdPlay.Classes.Contains("off"));
            string stopped = d.VfdCounter.Text;
            Thread.Sleep(300);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(stopped, d.VfdCounter.Text);

            Press(d.RewindKey);
            PumpUntil(() => d.VfdCounter.Text == "000");

            // Pausing latches the pause key down and blinks the power light.
            Press(d.PauseKey);
            Assert.True(w.Session.IsPaused);
            Assert.True(d.PauseKey.IsChecked);
            Assert.True(d.PowerLed.Blink);
            Assert.True(w.PauseItem.IsChecked);
            Press(d.PauseKey);
            Assert.False(w.Session.IsPaused);
            Assert.False(d.PauseKey.IsChecked);
            Assert.False(d.PowerLed.Blink);

            Press(d.EjectKey);
            PumpUntil(() => d.VfdTitle.Text == "NO TAPE", 10000);
            Assert.Equal("000", d.VfdCounter.Text);
            w.Close();
        }

        [AvaloniaFact]
        public void Reset_resets_and_holding_it_hard_resets()
        {
            MainWindow w = Boot();
            var d = w.Deck;
            Point reset = CentreOf(d.ResetKey, w);

            // Held: a hard reset once the hold is long enough, and none more on letting go.
            w.MouseDown(reset, MouseButton.Left);
            PumpUntil(() => d.VfdTitle.Text == "HARD RESET", 3000);
            w.MouseUp(reset, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            // Tapped: a soft reset, so no hard-reset message.
            d.ShowMessage(null);
            w.MouseDown(reset, MouseButton.Left);
            w.MouseUp(reset, MouseButton.Left);
            Thread.Sleep((int)MainWindow.HardResetHold.TotalMilliseconds + 200);
            Dispatcher.UIThread.RunJobs();
            Assert.NotEqual("HARD RESET", d.VfdTitle.Text);
            w.Close();
        }

        [AvaloniaFact]
        public void The_display_shows_the_disks_in_the_plus3_drives()
        {
            MainWindow w = Boot();
            var d = w.Deck;
            Assert.True(d.VfdDiskA.Classes.Contains("off"));        // a 48K has no drives
            Assert.False(w.DiskEjectA.IsEnabled);
            Assert.False(d.DriveALed.IsLit || d.VfdLedA.Classes.Contains("dim"));  // nor any lights

            string dir = Path.Combine(Path.GetTempPath(), "zero-deck-disk-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string disk = Path.Combine(dir, "games.dsk");
                Peripherals.DskImage.WriteBlankPlus3(disk);
                Assert.True(w.Session.InsertDiskAsync(0, disk).Result); // switches to the +3
                PumpUntil(() => d.VfdDiskA.Text == "A: GAMES");
                Assert.Equal("B: --", d.VfdDiskB.Text);
                Assert.False(d.VfdDiskA.Classes.Contains("off"));
                Assert.True(d.VfdDiskB.Classes.Contains("off"));      // empty: unlit
                // At rest drive A glows dim (the controller polls the drives by turns); a command
                // makes its drive bright for a moment. An empty B stands for no drive B fitted, so it
                // stays dark, even while booting looks for one.
                PumpUntil(() => w.Session.DiskLightOf(0) == Zero.Emulation.DiskLight.Dim, 15000);
                w.RefreshMenuState();
                Dispatcher.UIThread.RunJobs();
                Assert.True(d.VfdLedA.Classes.Contains("dim"));
                Assert.False(d.VfdLedA.Classes.Contains("lit"));
                Assert.True(d.DriveALed.IsLit && d.DriveALed.Dim);
                Assert.False(d.VfdLedB.Classes.Contains("dim") || d.VfdLedB.Classes.Contains("lit"));
                Assert.False(d.DriveBLed.IsLit);

                // The receiver's drive fronts: A occupied and named, B empty.
                Assert.True(d.DriveADisk.IsVisible);
                Assert.Equal("GAMES", d.DriveAName.Text);
                Assert.False(d.DriveBDisk.IsVisible);
                Assert.Equal("NO DISK", d.DriveBName.Text);
                Assert.True(d.DriveBName.Classes.Contains("empty"));
                Assert.False(d.DriveAName.Classes.Contains("empty"));
                w.DeckBItem.Command.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Capture(w, "deck-drives");
                w.DeckAItem.Command.Execute(null);
                Assert.True(w.DiskEjectA.IsEnabled);
                Assert.False(w.DiskEjectB.IsEnabled);
                Capture(w, "deck-disk");

                // The drive's own eject button takes it out.
                Press(d.DriveAEject);
                PumpUntil(() => d.VfdDiskA.Text == "A: --");
                Assert.True(d.VfdDiskA.Classes.Contains("off"));
                Assert.False(d.DriveADisk.IsVisible);
                Assert.False(w.DiskEjectA.IsEnabled);
            }
            finally
            {
                w.Close();
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [AvaloniaFact]
        public void The_pentagon_shows_four_drives_without_the_decks_growing()
        {
            MainWindow w = Boot();
            var d = w.Deck;
            Size deckSize = d.DesiredSize;
            double shelf = w.DeckShelf.Bounds.Height;
            Rect screen = w.Display.Bounds;
            Assert.False(d.VfdDrivesCD.IsVisible);                   // a 48K shows the +3's two, unlit
            Assert.False(d.DriveCBay.IsVisible);
            Assert.False(w.DiskInsertC.IsVisible);

            string dir = Path.Combine(Path.GetTempPath(), "zero-deck-trd-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string games = Path.Combine(dir, "games.trd"), inferno = Path.Combine(dir, "Inferno Demo Disk.trd");
                Peripherals.TrdImage.WriteBlank(games);
                Peripherals.TrdImage.WriteBlank(inferno);
                Assert.True(w.Session.InsertDiskAsync(0, games).Result);   // switches to the Pentagon
                Assert.True(w.Session.InsertDiskAsync(2, inferno).Result);
                PumpUntil(() => d.VfdDiskC.Text == "C: INFERNO DEMO DISK");

                // The display: A and B in the first column, C and D in the second.
                Assert.True(d.VfdDrivesCD.IsVisible);
                Assert.Equal("A: GAMES", d.VfdDiskA.Text);
                Assert.Equal("B: --", d.VfdDiskB.Text);
                Assert.Equal("D: --", d.VfdDiskD.Text);
                Assert.False(d.VfdDiskA.Classes.Contains("off"));
                Assert.True(d.VfdDiskB.Classes.Contains("off"));
                Assert.False(d.VfdDiskC.Classes.Contains("off"));
                Assert.True(d.VfdDiskD.Classes.Contains("off"));

                // The receiver: four bays, two by two.
                Assert.True(d.DriveCBay.IsVisible && d.DriveDBay.IsVisible);
                Assert.True(d.DriveADisk.IsVisible && d.DriveCDisk.IsVisible);
                Assert.False(d.DriveBDisk.IsVisible || d.DriveDDisk.IsVisible);
                Assert.Equal("INFERNO DEMO DISK", d.DriveCName.Text);
                Assert.Equal("NO DISK", d.DriveDName.Text);
                Assert.Equal(d.DriveABay.Bounds.Top, d.DriveCBay.Bounds.Top, 1);
                Assert.True(d.DriveCBay.Bounds.Left > d.DriveABay.Bounds.Right);

                // Neither deck grows, so the screen keeps its size.
                d.Measure(Size.Infinity);
                Assert.Equal(deckSize.Height, d.DesiredSize.Height, 1);
                Assert.Equal(deckSize.Width, d.DesiredSize.Width, 1);
                Assert.Equal(shelf, w.DeckShelf.Bounds.Height, 1);
                Assert.Equal(screen.Height, w.Display.Bounds.Height, 1.5);

                // The menu has drives C and D too.
                Assert.True(w.DiskInsertC.IsVisible && w.DiskEjectD.IsVisible);
                Assert.True(w.DiskEjectC.IsEnabled);
                Assert.False(w.DiskEjectD.IsEnabled);
                Capture(w, "deck-pentagon-a");
                w.DeckBItem.Command.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Capture(w, "deck-pentagon-b");

                // Drive C's own eject button takes it out.
                Press(d.DriveCEject);
                PumpUntil(() => d.VfdDiskC.Text == "C: --");
                Assert.False(d.DriveCDisk.IsVisible);
                Assert.False(w.DiskEjectC.IsEnabled);

                // Back to the +3: two bays again, full width.
                w.Session.SwitchMachine(SpeccyCommon.MachineModel._plus3);
                PumpUntil(() => w.Session.Model == SpeccyCommon.MachineModel._plus3);
                w.RefreshMenuState();
                Dispatcher.UIThread.RunJobs();
                Assert.False(d.VfdDrivesCD.IsVisible);
                Assert.False(d.DriveCBay.IsVisible);
                Assert.False(w.DiskInsertC.IsVisible);
                w.DeckAItem.Command.Execute(null);
            }
            finally
            {
                w.Close();
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [AvaloniaFact]
        public void A_pentagon_drive_light_stays_on_while_its_head_is_loaded_then_goes_out()
        {
            Func<EmulatorSettings> loader = MainWindow.SettingsLoader;
            MainWindow.SettingsLoader = () => { EmulatorSettings s = loader(); s.Tape.AutoLoad = true; return s; };
            MainWindow w = Boot();
            var d = w.Deck;
            string dir = Path.Combine(Path.GetTempPath(), "zero-deck-hld-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string disk = Path.Combine(dir, "games.trd");
                Peripherals.TrdImage.WriteBlank(disk);
                Assert.True(w.Session.LoadFileAsync(disk).Result);  // boots TR-DOS, which reads drive A
                PumpUntil(() => d.DriveALed.IsLit, 15000);

                // Lit steady, on the bay and on the display together.
                Assert.True(d.VfdLedA.Classes.Contains("lit"));
                Assert.False(d.DriveALed.Blink);

                // It goes out once the head unloads, three seconds after the controller goes idle.
                // (On the way, TR-DOS's delay loop selects drive D for a moment with the head
                // loaded, which lights D's light instead, as on the real interface.)
                PumpUntil(() => w.Session.BusyDiskUnit == -1, 15000);
                w.RefreshMenuState();
                Dispatcher.UIThread.RunJobs();
                Assert.False(d.DriveALed.IsLit || d.DriveBLed.IsLit || d.DriveCLed.IsLit || d.DriveDLed.IsLit);
                Assert.False(d.VfdLedA.Classes.Contains("lit"));
            }
            finally
            {
                MainWindow.SettingsLoader = loader;
                w.Close();
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        [AvaloniaFact]
        public void A_long_message_is_trimmed_rather_than_widening_the_deck()
        {
            MainWindow w = Boot();
            double width = w.Deck.CassetteDeck.Bounds.Width;
            Rect screen = w.Display.Bounds;
            w.Deck.ShowMessage(new string('X', 300));
            w.RefreshMenuState();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(width, w.Deck.CassetteDeck.Bounds.Width, 1);
            Assert.Equal(screen, w.Display.Bounds);
            w.Close();
        }

        [AvaloniaFact]
        public void Recording_an_rzx_lights_rec_and_rzx()
        {
            MainWindow w = Boot();
            var d = w.Deck;

            string dir = Path.Combine(Path.GetTempPath(), "zero-deck-rzx-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Assert.True(w.Session.StartRzxRecordingAsync(Path.Combine(dir, "run.rzx")).Result);
                w.RefreshMenuState();
                PumpUntil(() => !d.VfdRzx.Classes.Contains("off"));
                Assert.False(d.VfdRec.Classes.Contains("off"));     // REC serves the recording too
                Assert.True(d.VfdPlay.Classes.Contains("off"));
                Capture(w, "deck-rzx-rec");
                w.Session.DiscardRzxRecording();
                w.RefreshMenuState();
                PumpUntil(() => d.VfdRzx.Classes.Contains("off"));
                Assert.True(d.VfdRec.Classes.Contains("off"));
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
            w.Close();
        }

        [AvaloniaFact]
        public void The_power_key_quits()
        {
            Func<EmulatorSettings> loader = MainWindow.SettingsLoader;
            MainWindow.SettingsLoader = () => { EmulatorSettings s = loader(); s.Emulation.ConfirmOnExit = false; return s; };
            MainWindow w = Boot();
            bool closed = false;
            w.Closed += (_, __) => closed = true;
            Press(w.Deck.PowerKey);
            PumpUntil(() => closed);
        }

        [AvaloniaFact]
        public void Knobs_and_keys_change_the_same_settings_as_the_menus()
        {
            MainWindow w = Boot();
            var d = w.Deck;
            EmulatorSettings s = w.Session.Settings;

            // The wheel turns the volume pot, on deck A.
            int volume = s.Audio.Volume;
            w.MouseWheel(CentreOf(d.VolumeKnob, w), new Vector(0, 1));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(volume + 5, s.Audio.Volume);

            // Mute is under it.
            Assert.True(d.CassetteDeck.IsVisualAncestorOf(d.MuteKey));
            Press(d.MuteKey);
            Assert.False(s.Audio.Mute); // the tests boot muted
            Assert.False(d.MuteKey.IsChecked);

            // Stereo is on deck C, with the picture.
            Press(d.DeckCKey);
            Dispatcher.UIThread.RunJobs();

            // A click on a selector knob steps it on: ACB -> ABC.
            Assert.Equal(1, s.Audio.StereoSoundMode);
            Point stereo = CentreOf(d.StereoKnob, w);
            w.MouseDown(stereo, MouseButton.Left);
            w.MouseUp(stereo, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, s.Audio.StereoSoundMode);
            Assert.True(w.StereoAbc.IsChecked);
            Assert.Equal("ABC", d.VfdStereo.Text);

            // Under it, the key that gives the 48K an AY chip; the display says when there is one.
            Assert.True(d.VideoDeck.IsVisualAncestorOf(d.Ay48kKey));
            Assert.False(s.Audio.EnableAYFor48K);
            Assert.Contains("off", d.VfdAy.Classes);
            Press(d.Ay48kKey);
            Assert.True(s.Audio.EnableAYFor48K);
            Assert.True(w.Ay48kItem.IsChecked);
            Assert.True(d.Ay48kKey.IsChecked);
            PumpUntil(() => !d.VfdAy.Classes.Contains("off"));
            Press(d.Ay48kKey);
            Assert.False(s.Audio.EnableAYFor48K);
            PumpUntil(() => d.VfdAy.Classes.Contains("off"));

            // Full speed has no key on the decks; the display still says when it is on.
            w.FullSpeedItem.Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain("off", d.VfdTurbo.Classes);
            w.FullSpeedItem.Command.Execute(null);

            bool crt = s.Render.Crt.Enabled;
            Press(d.CrtKey);
            Assert.Equal(!crt, s.Render.Crt.Enabled);
            Assert.Equal(!crt, w.CrtItem.IsChecked);

            // The menus are reflected back onto the deck.
            w.StereoMono.Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, d.StereoKnob.Value);
            w.Close();
        }

        [AvaloniaFact]
        public void The_decks_scale_with_the_window_to_stay_as_wide_as_the_picture()
        {
            MainWindow w = Boot();
            var screen = w.Screens?.ScreenFromWindow(w) ?? w.Screens?.Primary;
            double usable = screen == null ? double.PositiveInfinity : screen.WorkingArea.Height / screen.Scaling;
            double pad = w.DeckShelf.Padding.Left + w.DeckShelf.Padding.Right;
            double Room() => w.DeckShelf.Bounds.Width - pad;
            double Shown() => w.DeckViewbox.Bounds.Width;
            double PictureWidth() => Math.Min(w.Display.Bounds.Width, w.Display.Bounds.Height * 352 / 296);
            void Settle() { Dispatcher.UIThread.RunJobs(); w.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
            _output.WriteLine($"natural {w.Deck.DesiredSize.Width:F0} shown {Shown():F0} screen {screen?.WorkingArea} scaling {screen?.Scaling}");

            // At 2x the decks fill the window's width, and the screen is still sized exactly.
            Assert.InRange(Shown(), Room() - 1.5, Room() + 0.5);
            if (usable > 1000) Assert.InRange(w.Display.Bounds.Height, 592 - 1.5, 592 + 1.5);
            double at2x = Shown();
            Assert.True(w.Scale2.IsChecked && !w.Scale4.IsChecked);

            // At 4x they are as much bigger as the picture is: still filling the window, not held
            // back to some limit. The window is the size asked for whatever the screen's; where the
            // system trims it to fit (macOS does), it is the short, wide window further down.
            MenuItem(w, "View", "Window Size", "4x").Command.Execute(null);
            Settle();
            Assert.True(w.Scale4.IsChecked && !w.Scale2.IsChecked, "the Window Size menu does not tick the size chosen");
            _output.WriteLine($"4x: window {w.ClientSize} display {w.Display.Bounds} decks {Shown():F0}");
            Assert.InRange(Shown(), Room() - 1.5, Room() + 0.5);
            Assert.InRange(Shown() + pad, PictureWidth() - 3, PictureWidth() + 3);
            Assert.True(Shown() > at2x * 1.4, $"the decks did not grow with the window ({Shown() / at2x:F2}x)");
            Assert.InRange(w.Display.Bounds.Height, 1184 - 1.5, 1184 + 1.5);
            Assert.InRange(w.Display.Bounds.Width, 1408 - 1.5, 1408 + 1.5);
            Capture(w, "decks-4x");

            // A window dragged short and wide: the picture is held to its height, and the decks to
            // the picture's width rather than the window's.
            w.Width = 1100;
            w.Height = 560;
            Settle();
            _output.WriteLine($"short: window {w.ClientSize} display {w.Display.Bounds} decks {Shown():F0}");
            Assert.True(Shown() < Room() - 100, "the decks filled a window too short for a picture that wide");
            Assert.InRange(Shown() + pad, PictureWidth() - 3, PictureWidth() + 3);
            Capture(w, "decks-short-window");

            // Beside the picture the window is black, like the television's housing, not the
            // Spectrum's border colour: that would show as a light strip down either side of the set.
            Assert.True(w.Display.CrtOptions.Bezel > 0);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using (WriteableBitmap shot = w.CaptureRenderedFrame())
            using (var pixels = shot.Lock())
            {
                Point beside = w.Display.TranslatePoint(new Point(20, w.Display.Bounds.Height / 2), w) ?? default;
                double scale = shot.PixelSize.Width / w.ClientSize.Width;
                int x = (int)(beside.X * scale), y = (int)(beside.Y * scale);
                uint pixel;
                unsafe { pixel = *(uint*)((byte*)pixels.Address + y * pixels.RowBytes + x * 4); }
                Assert.True((pixel & 0x00FFFFFF) == 0, $"beside the housing the window is #{pixel & 0x00FFFFFF:X6}, not black");
            }

            // Dragged tall and narrow: the decks fill the width, like the picture.
            w.Width = 600;
            w.Height = 900;
            Settle();
            Assert.InRange(Shown(), Room() - 1.5, Room() + 0.5);
            w.Close();
        }

        [AvaloniaFact]
        public void One_deck_shows_at_a_time_and_the_screen_keeps_its_size()
        {
            MainWindow w = Boot();
            var d = w.Deck;
            Rect screen = w.Display.Bounds;
            double ratio = screen.Width / screen.Height;
            Assert.InRange(ratio, 352.0 / 296 - 0.02, 352.0 / 296 + 0.02); // fitted with the decks showing
            Assert.True(d.CassetteDeck.IsVisualAncestorOf(d.PlayKey));
            Assert.True(d.CassetteDeck.IsVisualAncestorOf(d.VolumeKnob));
            Assert.True(d.CassetteDeck.IsVisualAncestorOf(d.MachineKnob));
            Assert.True(d.ReceiverDeck.IsVisualAncestorOf(d.MultifaceKey));
            Assert.True(d.ReceiverDeck.IsVisualAncestorOf(d.DriveAEject));

            // Deck A to start with: the cassette deck shows, the receiver does not.
            Assert.Equal(0, d.SelectedDeck);
            Assert.True(d.DeckAKey.IsChecked == true && d.DeckBKey.IsChecked == false);
            Assert.Equal(1, d.CassetteDeck.Opacity);
            Assert.Equal(0, d.ReceiverDeck.Opacity);
            Assert.False(d.ReceiverDeck.IsHitTestVisible);
            Capture(w, "decks-a");
            double shelf = w.DeckShelf.Bounds.Height;

            void AssertScreenUnchanged()
            {
                Assert.InRange(w.Display.Bounds.Height, screen.Height - 1.5, screen.Height + 1.5);
                Assert.InRange(w.Display.Bounds.Width, screen.Width - 1.5, screen.Width + 1.5);
            }

            // Deck B: the receiver instead, in the same space.
            Press(d.DeckBKey);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, d.SelectedDeck);
            Assert.True(d.DeckAKey.IsChecked == false && d.DeckBKey.IsChecked == true);
            Assert.Equal(0, d.CassetteDeck.Opacity);
            Assert.Equal(1, d.ReceiverDeck.Opacity);
            Assert.False(d.CassetteDeck.IsHitTestVisible);
            Assert.Equal(1, w.Session.Settings.Render.ActiveDeck);
            Assert.True(w.DeckBItem.IsChecked);
            Assert.Equal(shelf, w.DeckShelf.Bounds.Height);
            AssertScreenUnchanged();
            Capture(w, "decks-b");

            // Pressing the lit key leaves it lit; the menu picks too.
            Press(d.DeckBKey);
            Dispatcher.UIThread.RunJobs();
            Assert.True(d.DeckBKey.IsChecked == true);
            w.DeckAItem.Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, d.SelectedDeck);

            // Hiding the decks gives way to the status bar, and the screen stays the size it was.
            w.DeckItem.Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.False(w.DeckShelf.IsVisible);
            Assert.True(w.StatusBar.IsVisible);
            Assert.False(w.Session.Settings.Render.ShowDeck);
            AssertScreenUnchanged();

            w.DeckItem.Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(w.DeckShelf.IsVisible);
            Assert.False(w.StatusBar.IsVisible);
            AssertScreenUnchanged();
            w.Close();
        }

        [AvaloniaFact]
        public void At_1x_the_deck_gives_way_to_the_status_bar()
        {
            MainWindow w = Boot();
            NativeMenuItem oneX = MenuItem(w, "View", "Window Size", "1x");
            oneX.Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.False(w.DeckShelf.IsVisible);
            Assert.True(w.StatusBar.IsVisible);
            Assert.False(w.DeckItem.IsEnabled);
            Assert.True(w.Session.Settings.Render.ShowDeck); // still wanted at larger sizes
            Assert.InRange(w.Display.Bounds.Height, 296 - 1.5, 296 + 1.5);
            if (w.MinWidth <= 352) // macOS: no in-window menu bar holding the window wide, so no side bars
                Assert.InRange(w.Display.Bounds.Width, 352 - 1.5, 352 + 1.5);
            Capture(w, "deck-1x");

            MenuItem(w, "View", "Window Size", "2x").Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(w.DeckShelf.IsVisible);
            Assert.False(w.StatusBar.IsVisible);
            Assert.InRange(w.Display.Bounds.Height, 592 - 1.5, 592 + 1.5);
            w.Close();
        }

        [AvaloniaFact]
        public void The_decks_can_be_silver()
        {
            MainWindow w = Boot();
            var d = w.Deck;
            Assert.Equal(DeckFinish.Black, d.Finish);

            // Chosen in Options.
            w.Session.Settings.Render.DeckFinish = "Silver";
            w.ApplyDeckFinish();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(DeckFinish.Silver, d.Finish);
            Assert.Contains("silver", d.Classes);
            Assert.All(d.GetVisualDescendants().OfType<DeckFaceplate>(), p => Assert.Equal(DeckFinish.Silver, p.Finish));
            Capture(w, "decks-a-silver");
            Press(d.DeckBKey);
            Dispatcher.UIThread.RunJobs();
            Capture(w, "decks-b-silver");

            w.Session.Settings.Render.DeckFinish = "Black";
            w.ApplyDeckFinish();
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain("silver", d.Classes);
            Assert.All(d.GetVisualDescendants().OfType<DeckFaceplate>(), p => Assert.Equal(DeckFinish.Black, p.Finish));
            w.Close();
        }

        [AvaloniaFact]
        public void The_faders_set_the_crt_and_say_what_they_are_at()
        {
            MainWindow w = Boot();
            var d = w.Deck;
            CrtSettings crt = w.Session.Settings.Render.Crt;
            Press(d.DeckCKey);   // the faders are on the video deck
            Dispatcher.UIThread.RunJobs();

            double before = crt.EdgeLight;
            w.MouseWheel(CentreOf(d.EdgeFader, w), new Vector(0, 1));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(before + 0.05, crt.EdgeLight, 4);
            Assert.Equal((float)crt.EdgeLight, w.Display.CrtOptions.EdgeLight, 4);

            Assert.Equal($"Edge light on the housing: {crt.EdgeLight:P0}", ToolTip.GetTip(d.EdgeFader));
            Assert.Equal($"Screen curvature: {crt.Curvature:P0}", ToolTip.GetTip(d.CurveFader));
            w.Close();
        }

        [AvaloniaFact]
        public void The_joystick_switches_set_what_plays_which_joystick()
        {
            MainWindow w = Boot();
            var d = w.Deck;
            InputSettings input = w.Session.Settings.Input;
            void Click(Point at) { w.MouseDown(at, MouseButton.Left); w.MouseUp(at, MouseButton.Left); Dispatcher.UIThread.RunJobs(); }
            Point Stop(DeckSlideSwitch slide, int i) => slide.TranslatePoint(slide.StopPoint(i), w) ?? throw new InvalidOperationException("not in window");

            // Out of the box gamepad 1 plays a Kempston joystick, and the display names it.
            Assert.True(d.JoystickGamepadKey.IsChecked == true && d.JoystickCursorKey.IsChecked == false && d.JoystickNoneKey.IsChecked == false);
            Assert.Equal(0, d.JoystickSwitch.Value);
            Assert.Equal("KEMP", d.VfdJoystick.Text);
            Assert.DoesNotContain("off", d.VfdJoystick.Classes);

            // The joystick switch picks what the gamepad plays.
            Click(Stop(d.JoystickSwitch, 2));
            Assert.Equal(3, input.Gamepad1Emulates);
            Assert.False(input.EnableKeyboardJoystick);
            Assert.True(w.Pad1Sinclair2.IsChecked);
            Assert.Equal("SIN2", d.VfdJoystick.Text);

            // Played by nothing: the gamepad lets go, and the joystick switch dims but keeps its place.
            Press(d.JoystickNoneKey);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, input.Gamepad1Emulates);
            Assert.False(input.EnableKeyboardJoystick);
            Assert.True(w.Pad1None.IsChecked);
            Assert.Equal(2, d.JoystickSwitch.Value);
            Assert.True(d.JoystickSwitch.Opacity < 1);
            Assert.Contains("off", d.VfdJoystick.Classes);   // its legend goes out

            // Played by the cursor keys, which take up the same joystick.
            Press(d.JoystickCursorKey);
            Dispatcher.UIThread.RunJobs();
            Assert.True(input.EnableKeyboardJoystick);
            Assert.Equal(3, input.KeyboardJoystickType);
            Assert.Equal(0, input.Gamepad1Emulates);
            Assert.True(w.KeyJoyItem.IsChecked && w.KeyJoySinclair2.IsChecked);
            Assert.True(d.JoystickCursorKey.IsChecked == true && d.JoystickGamepadKey.IsChecked == false && d.JoystickNoneKey.IsChecked == false);
            Assert.Equal(1, d.JoystickSwitch.Opacity);
            Assert.Equal("SIN2", d.VfdJoystick.Text);
            Assert.DoesNotContain("off", d.VfdJoystick.Classes);

            // The mouse has a legend of its own, lit while the Kempston mouse is on.
            Assert.Contains("off", d.VfdMouse.Classes);
            w.MouseItem.Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(input.EnableKempstonMouse);
            Assert.DoesNotContain("off", d.VfdMouse.Classes);
            Capture(w, "deck-joystick");
            w.Close();
        }

        [AvaloniaFact]
        public void The_machine_knob_changes_the_machine()
        {
            MainWindow w = Boot();
            var knob = w.Deck.MachineKnob;
            Point centre = CentreOf(knob, w);
            int reported = 0;
            knob.ValueChangedByUser += (_, __) => reported++;
            Assert.Equal(0, knob.Value);

            // A click steps it on a stop: 48K -> 128K.
            w.MouseDown(centre, MouseButton.Left);
            w.MouseUp(centre, MouseButton.Left);
            Assert.Equal(1, reported);
            // It stays where it was put while the session switches (the deck refreshes as the
            // switch is asked for), not back on 48K meanwhile.
            Assert.Equal(1, knob.Value);
            PumpUntil(() => w.Session.Model == SpeccyCommon.MachineModel._128k, 10000);
            Assert.True(w.Machine128k.IsChecked);
            PumpUntil(() => !w.Deck.VfdAy.Classes.Contains("off"));   // the 128K has an AY chip built in
            Capture(w, "deck-machine-knob");

            // The wheel turns it a stop too: 128K -> +2.
            w.MouseWheel(centre, new Vector(0, 1));
            PumpUntil(() => w.Session.Model == SpeccyCommon.MachineModel._plus2, 10000);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, knob.Value);
            Assert.Equal(2, reported);

            // The menus are reflected back onto it.
            w.MachinePlus3.Command.Execute(null);
            PumpUntil(() => w.Session.Model == SpeccyCommon.MachineModel._plus3, 10000);
            PumpUntil(() => knob.Value == 4);
            Assert.Equal(2, reported);
            w.Close();
        }

        [AvaloniaFact]
        public void The_video_deck_holds_the_picture_keys_and_the_peripherals_sit_on_the_receiver()
        {
            MainWindow w = Boot();
            var d = w.Deck;

            // The picture controls moved from the receiver to deck C; the Multiface switch joined the
            // receiver, and its NMI key sits beside reset on the cassette deck.
            Assert.True(d.VideoDeck.IsVisualAncestorOf(d.CrtKey));
            Assert.True(d.VideoDeck.IsVisualAncestorOf(d.ScanKey));
            Assert.True(d.VideoDeck.IsVisualAncestorOf(d.CurveFader));
            Assert.True(d.VideoDeck.IsVisualAncestorOf(d.EdgeFader));
            Assert.True(d.VideoDeck.IsVisualAncestorOf(d.StereoKnob));   // sound sits with the picture
            Assert.False(d.ReceiverDeck.IsVisualAncestorOf(d.CrtKey));
            Assert.True(d.ReceiverDeck.IsVisualAncestorOf(d.MultifaceKey));
            Assert.True(d.CassetteDeck.IsVisualAncestorOf(d.NmiKey));

            Press(d.DeckCKey);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, d.SelectedDeck);
            Assert.True(d.DeckAKey.IsChecked == false && d.DeckBKey.IsChecked == false && d.DeckCKey.IsChecked == true);
            Assert.Equal(1, d.VideoDeck.Opacity);
            Assert.Equal(0, d.CassetteDeck.Opacity);
            Assert.Equal(0, d.ReceiverDeck.Opacity);
            Assert.False(d.ReceiverDeck.IsHitTestVisible);
            Assert.Equal(2, w.Session.Settings.Render.ActiveDeck);
            Assert.True(w.DeckCItem.IsChecked);
            Assert.False(w.DeckAItem.IsChecked);

            // The rest of the CRT settings are here too, and set what Options does.
            CrtSettings crt = w.Session.Settings.Render.Crt;
            // In the headless window a tooltip opens as an overlay over the controls beside it, so the
            // pointer is taken off the deck after each gesture to close it.
            void Away() { w.MouseMove(new Point(10, 10)); Dispatcher.UIThread.RunJobs(); }
            void Click(Point at) { w.MouseDown(at, MouseButton.Left); w.MouseUp(at, MouseButton.Left); Dispatcher.UIThread.RunJobs(); Away(); }
            Point Stop(DeckSlideSwitch slide, int i) => slide.TranslatePoint(slide.StopPoint(i), w) ?? throw new InvalidOperationException("not in window");
            foreach (Control c in new Control[] { d.VignetteKey, d.TubeSwitch, d.EdgeFader, d.HousingFader, d.FinishSwitch })
                Assert.True(d.VideoDeck.IsVisualAncestorOf(c));
            Assert.Equal(crt.EdgeLight, d.EdgeFader.Value, 4);
            Assert.Equal(crt.Bezel / DeckPanel.MaxHousing, d.HousingFader.Value, 4);

            bool vignette = crt.Vignette;
            Press(d.VignetteKey);
            Assert.Equal(!vignette, crt.Vignette);
            Assert.Equal(!vignette, d.VignetteKey.IsChecked);
            Assert.Equal(crt.Vignette ? 0.6f : 0f, w.Display.CrtOptions.Vignette, 4);

            Click(Stop(d.TubeSwitch, 1));
            Assert.Equal("Trinitron", crt.Tube);
            Assert.True(w.Display.CrtOptions.Trinitron);

            double housing = crt.Bezel;
            w.MouseWheel(CentreOf(d.HousingFader, w), new Vector(0, 1));
            Dispatcher.UIThread.RunJobs();
            Away();
            Assert.Equal(housing + 0.05 * DeckPanel.MaxHousing, crt.Bezel, 4);
            Assert.Equal((float)crt.Bezel, w.Display.CrtOptions.Bezel, 4);
            Assert.Equal($"Television housing: {crt.Bezel:P0} of the window", ToolTip.GetTip(d.HousingFader));

            // With CRT effects off they dim, all but scanlines.
            Press(d.CrtKey);
            Assert.False(d.EdgeFader.IsEnabled || d.VignetteKey.IsEnabled || d.TubeSwitch.IsEnabled);
            Assert.True(d.ScanKey.IsEnabled);
            Press(d.CrtKey);
            Assert.True(d.EdgeFader.IsEnabled);

            // The picture's own settings, as the View menu has them: the pixel filter and the border.
            // A click on either knob steps it on.
            Assert.True(d.VideoDeck.IsVisualAncestorOf(d.FilterKnob) && d.VideoDeck.IsVisualAncestorOf(d.BorderKnob));
            Assert.Equal(2, d.FilterKnob.Value);   // sharp, out of the box
            Click(CentreOf(d.FilterKnob, w));      // round to none
            Assert.False(w.Session.Settings.Render.PixelSmoothing);
            Assert.False(w.Display.Smooth);
            Assert.True(w.FilterNone.IsChecked && !w.FilterSharp.IsChecked);
            w.FilterSmooth.Command.Execute(null);   // and the menu turns the knob
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(1, d.FilterKnob.Value);

            Assert.Equal(0, d.BorderKnob.Value);
            Click(CentreOf(d.BorderKnob, w));
            Assert.Equal(24, w.Session.Settings.Render.BorderCrop);
            Assert.Equal(24, w.Display.BorderCrop);
            Assert.True(w.BorderMedium.IsChecked);
            w.BorderFull.Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, d.BorderKnob.Value);

            // Changing the border resizes the window, and the deck under the pointer, so a drag on
            // its knob turns it freely and the border changes only where it is let go.
            Point border = CentreOf(d.BorderKnob, w);
            w.MouseDown(border, MouseButton.Left);
            w.MouseMove(border + Up(w, 30));
            w.MouseMove(border + Up(w, 50));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, d.BorderKnob.Value);
            Assert.Equal(0, w.Session.Settings.Render.BorderCrop);
            w.MouseUp(border + Up(w, 50), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Away();
            Assert.Equal(48, w.Session.Settings.Render.BorderCrop);
            Assert.True(w.BorderNone.IsChecked);
            w.BorderFull.Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, d.BorderKnob.Value);

            Capture(w, "deck-video");

            // The finish switch dresses the decks, as Options does.
            Assert.Equal(0, d.FinishSwitch.Value);
            Click(Stop(d.FinishSwitch, 1));
            Assert.Equal("Silver", w.Session.Settings.Render.DeckFinish);
            Assert.Equal(DeckFinish.Silver, d.Finish);
            Assert.Equal(1, d.FinishSwitch.Value);
            Capture(w, "deck-video-silver");
            w.Close();
        }
    }
}
