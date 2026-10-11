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
            PumpUntil(() => d.VfdBlock.Text == "BLK 01/12");
            Assert.Equal("000", d.VfdCounter.Text);
            Assert.Equal("LOADED OVERSCAN.TAP", d.VfdTitle.Text); // status messages borrow the info line

            Press(d.PlayKey);
            PumpUntil(() => d.VfdMode.Text == "PLAY");

            // The counter only moves while the tape runs. Fast forward so it gets going quickly.
            Press(d.TurboKey);
            PumpUntil(() => w.Session.Tape.Counter >= 2, 30000);
            Press(d.TurboKey);
            PumpUntil(() => d.VfdCounter.Text != "000");
            Capture(w, "deck-playing");

            Press(d.StopKey);
            PumpUntil(() => d.VfdMode.Text == "STOP");
            string stopped = d.VfdCounter.Text;
            Thread.Sleep(300);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(stopped, d.VfdCounter.Text);

            Press(d.RewindKey);
            PumpUntil(() => d.VfdCounter.Text == "000");

            Press(d.PauseKey);
            Assert.True(w.Session.IsPaused);
            Assert.True(d.PowerLed.Blink);
            Press(d.PauseKey);
            Assert.False(w.Session.IsPaused);

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

            // A click on a selector knob steps it on: ACB -> ABC.
            Assert.Equal(1, s.Audio.StereoSoundMode);
            Point stereo = CentreOf(d.StereoKnob, w);
            w.MouseDown(stereo, MouseButton.Left);
            w.MouseUp(stereo, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, s.Audio.StereoSoundMode);
            Assert.True(w.StereoAbc.IsChecked);
            Assert.Equal("ABC", d.VfdStereo.Text);

            // The wheel turns the volume pot.
            int volume = s.Audio.Volume;
            w.MouseWheel(CentreOf(d.VolumeKnob, w), new Vector(0, 1));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(volume + 5, s.Audio.Volume);

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
        public void Each_deck_shows_and_hides_from_the_menu_and_the_screen_keeps_its_size()
        {
            MainWindow w = Boot();
            var d = w.Deck;
            Rect before = w.Display.Bounds;
            double ratio = before.Width / before.Height;
            Assert.InRange(ratio, 352.0 / 296 - 0.02, 352.0 / 296 + 0.02); // fitted with both decks showing
            Assert.True(d.CassetteDeck.IsVisible && d.ReceiverDeck.IsVisible);
            Assert.Equal(d.CassetteDeck.Bounds.Width, d.ReceiverDeck.Bounds.Width); // stacked flush, like real hi-fi
            Assert.True(d.CassetteDeck.IsVisualAncestorOf(d.PlayKey));
            Assert.True(d.CassetteDeck.IsVisualAncestorOf(d.VolumeKnob)); // in constant use, so up top
            Assert.True(d.CassetteDeck.IsVisualAncestorOf(d.MachineKnob));
            Assert.True(d.ReceiverDeck.IsVisualAncestorOf(d.StereoKnob));
            Capture(w, "decks-both");

            void AssertScreenUnchanged()
            {
                Assert.InRange(w.Display.Bounds.Height, before.Height - 1.5, before.Height + 1.5);
                Assert.InRange(w.Display.Bounds.Width, before.Width - 1.5, before.Width + 1.5);
            }

            // Cassette deck off: the receiver stays, and the status bar takes over the deck's display.
            w.DeckItem.Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.False(d.CassetteDeck.IsVisible);
            Assert.True(d.ReceiverDeck.IsVisible);
            Assert.True(w.StatusBar.IsVisible);
            Assert.False(w.DeckItem.IsChecked);
            Assert.False(w.Session.Settings.Render.ShowDeck);
            AssertScreenUnchanged();

            // Receiver off too: the shelf goes.
            w.ReceiverItem.Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.False(w.DeckShelf.IsVisible);
            Assert.False(w.ReceiverItem.IsChecked);
            Assert.False(w.Session.Settings.Render.ShowReceiver);
            AssertScreenUnchanged();

            // Cassette deck alone.
            w.DeckItem.Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(d.CassetteDeck.IsVisible);
            Assert.False(d.ReceiverDeck.IsVisible);
            Assert.False(w.StatusBar.IsVisible);
            AssertScreenUnchanged();
            Capture(w, "decks-cassette-only");

            w.ReceiverItem.Command.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.True(d.ReceiverDeck.IsVisible);
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
        public void The_machine_knob_switches_only_on_a_deliberate_turn()
        {
            MainWindow w = Boot();
            var knob = w.Deck.MachineKnob;
            Point at = CentreOf(knob, w);
            int reported = 0;
            knob.ValueChangedByUser += (_, __) => reported++;

            // Scrolling past it does nothing.
            w.MouseWheel(at, new Vector(0, 1));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, reported);

            // A drag reports only where it is let go: 48K -> (128K) -> 128KE, one switch not two.
            w.MouseDown(at, MouseButton.Left);
            w.MouseMove(at + Up(w, 25));
            Assert.Equal(1, knob.Value);
            Assert.Equal(0, reported);
            w.MouseMove(at + Up(w, 46));
            w.MouseUp(at + Up(w, 46), MouseButton.Left);
            Assert.Equal(1, reported);
            PumpUntil(() => w.Session.Model == SpeccyCommon.MachineModel._128ke, 10000);
            Assert.True(w.Machine128ke.IsChecked);

            // A click steps on to the next one.
            w.MouseDown(at, MouseButton.Left);
            w.MouseUp(at, MouseButton.Left);
            PumpUntil(() => w.Session.Model == SpeccyCommon.MachineModel._plus3, 10000);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, knob.Value);
            w.Close();
        }
    }
}
