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
            Press(d.TurboKey);
            PumpUntil(() => w.Session.Tape.Counter >= 2, 30000);
            Press(d.TurboKey);
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
        public void Recording_an_rzx_lights_rec_and_rzx_and_the_machine_row_follows_the_model()
        {
            MainWindow w = Boot();
            var d = w.Deck;
            Assert.False(d.Vfd48k.Classes.Contains("off"));
            Assert.True(d.Vfd128k.Classes.Contains("off"));

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

            // The 128KE lights 128K and its E.
            w.Machine128ke.Command.Execute(null);
            PumpUntil(() => !d.Vfd128kE.Classes.Contains("off"));
            Assert.False(d.Vfd128k.Classes.Contains("off"));
            Assert.True(d.Vfd48k.Classes.Contains("off"));
            Capture(w, "deck-128ke");
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

            // The rest are on deck B, the receiver.
            Press(d.DeckBKey);
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

            // Dragging the CPU knob up two stops picks 4x.
            Point cpu = CentreOf(d.CpuKnob, w);
            w.MouseDown(cpu, MouseButton.Left);
            w.MouseMove(cpu + Up(w, 30));
            w.MouseMove(cpu + Up(w, 50));
            w.MouseUp(cpu + Up(w, 50), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(4, s.Emulation.CpuMultiplier);
            Assert.True(w.Cpu4.IsChecked);

            Press(d.MuteKey);
            Assert.False(s.Audio.Mute); // the tests boot muted
            Assert.False(d.MuteKey.IsChecked);

            Press(d.TurboKey);
            Assert.True(s.Emulation.EmulationSpeed > 1);
            Assert.True(w.FullSpeedItem.IsChecked);
            Press(d.TurboKey);

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
        public void At_2x_the_decks_are_enlarged_to_fill_the_window()
        {
            MainWindow w = Boot();
            double natural = w.Deck.DesiredSize.Width;
            double shown = w.DeckViewbox.Bounds.Width;
            var screen = w.Screens?.ScreenFromWindow(w) ?? w.Screens?.Primary;
            _output.WriteLine($"natural {natural:F0} shown {shown:F0} screen {screen?.WorkingArea} scaling {screen?.Scaling}");
            Assert.True(shown <= natural * MainWindow.MaxDeckScale + 1, "the decks grew past their limit");
            Assert.True(shown <= w.ClientSize.Width, "the decks are wider than the window");
            // Unless the screen is too short for the enlarged decks, they grow to fill the 2x
            // window's width or to their limit, whichever comes first.
            if (screen == null || screen.WorkingArea.Height / screen.Scaling > 1000)
            {
                double room = w.DeckShelf.Bounds.Width - w.DeckShelf.Padding.Left - w.DeckShelf.Padding.Right;
                Assert.True(shown > natural, $"decks were not enlarged ({shown / natural:F2}x)");
                Assert.InRange(shown, Math.Min(room, natural * MainWindow.MaxDeckScale) - 1.5, room + 0.5);
            }
            // And the screen is still sized exactly, with the decks enlarged.
            Assert.InRange(w.Display.Bounds.Height, 592 - 1.5, 592 + 1.5);
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
            Assert.True(d.CassetteDeck.IsVisualAncestorOf(d.MachineSwitch));
            Assert.True(d.ReceiverDeck.IsVisualAncestorOf(d.StereoKnob));
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
        public void The_housing_fader_sets_the_cabinet_and_both_faders_say_what_they_are_at()
        {
            MainWindow w = Boot();
            var d = w.Deck;
            CrtSettings crt = w.Session.Settings.Render.Crt;
            Press(d.DeckBKey);
            Dispatcher.UIThread.RunJobs();

            double before = crt.Bezel;
            w.MouseWheel(CentreOf(d.HousingFader, w), new Vector(0, 1));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(before + 0.05 * DeckPanel.MaxHousing, crt.Bezel, 4);
            Assert.Equal((float)crt.Bezel, w.Display.CrtOptions.Bezel, 4);

            Assert.Equal($"Television housing: {crt.Bezel:P0} of the window", ToolTip.GetTip(d.HousingFader));
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

            // Out of the box gamepad 1 plays a Kempston joystick.
            Assert.True(d.JoystickGamepadKey.IsChecked == true && d.JoystickCursorKey.IsChecked == false && d.JoystickNoneKey.IsChecked == false);
            Assert.Equal(0, d.JoystickSwitch.Value);

            // The joystick switch picks what the gamepad plays.
            Click(Stop(d.JoystickSwitch, 2));
            Assert.Equal(3, input.Gamepad1Emulates);
            Assert.False(input.EnableKeyboardJoystick);
            Assert.True(w.Pad1Sinclair2.IsChecked);

            // Played by nothing: the gamepad lets go, and the joystick switch dims but keeps its place.
            Press(d.JoystickNoneKey);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, input.Gamepad1Emulates);
            Assert.False(input.EnableKeyboardJoystick);
            Assert.True(w.Pad1None.IsChecked);
            Assert.Equal(2, d.JoystickSwitch.Value);
            Assert.True(d.JoystickSwitch.Opacity < 1);

            // Played by the cursor keys, which take up the same joystick.
            Press(d.JoystickCursorKey);
            Dispatcher.UIThread.RunJobs();
            Assert.True(input.EnableKeyboardJoystick);
            Assert.Equal(3, input.KeyboardJoystickType);
            Assert.Equal(0, input.Gamepad1Emulates);
            Assert.True(w.KeyJoyItem.IsChecked && w.KeyJoySinclair2.IsChecked);
            Assert.True(d.JoystickCursorKey.IsChecked == true && d.JoystickGamepadKey.IsChecked == false && d.JoystickNoneKey.IsChecked == false);
            Assert.Equal(1, d.JoystickSwitch.Opacity);
            Capture(w, "deck-joystick");
            w.Close();
        }

        [AvaloniaFact]
        public void The_machine_switch_changes_machine_only_when_let_go()
        {
            MainWindow w = Boot();
            var slide = w.Deck.MachineSwitch;
            Point Stop(int i) => slide.TranslatePoint(slide.StopPoint(i), w) ?? throw new InvalidOperationException("not in window");
            int reported = 0;
            slide.ValueChangedByUser += (_, __) => reported++;

            // Scrolling past it does nothing.
            w.MouseWheel(Stop(0), new Vector(0, 1));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, reported);

            // A slide reports only where it is let go: 48K -> (128K) -> 128KE, one switch not two.
            w.MouseDown(Stop(0), MouseButton.Left);
            w.MouseMove(Stop(1));
            Assert.Equal(1, slide.Value);
            Assert.Equal(0, reported);
            w.MouseMove(Stop(2));
            w.MouseUp(Stop(2), MouseButton.Left);
            Assert.Equal(1, reported);
            // It stays where it was let go while the session switches (the deck refreshes as the
            // switch is asked for), not back on 48K meanwhile.
            Assert.Equal(2, slide.Value);
            PumpUntil(() => w.Session.Model == SpeccyCommon.MachineModel._128ke, 10000);
            Assert.True(w.Machine128ke.IsChecked);
            Capture(w, "deck-machine-switch");

            // A click on another stop moves it there.
            w.MouseDown(Stop(3), MouseButton.Left);
            w.MouseUp(Stop(3), MouseButton.Left);
            PumpUntil(() => w.Session.Model == SpeccyCommon.MachineModel._plus3, 10000);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, slide.Value);
            Assert.Equal(2, reported);
            w.Close();
        }
    }
}
