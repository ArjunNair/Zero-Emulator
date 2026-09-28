using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Xunit;
using Zero.Emulation;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.App.Tests
{
    /// <summary>The RZX menu items only make sense in certain states; these pin which.</summary>
    public class RzxMenuTests
    {
        public RzxMenuTests()
        {
            MainWindow.SettingsLoader = () =>
            {
                var s = new EmulatorSettings();
                s.Paths.Roms = TestPaths.RomDir;
                s.Emulation.PauseOnFocusLost = false;
                s.Audio.Mute = true;
                return s;
            };
        }

        private static void WaitFrames(MainWindow w, int frames)
        {
            long target = w.Session.FrameCount + frames;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (w.Session.FrameCount < target)
            {
                if (sw.ElapsedMilliseconds > 60000) throw new TimeoutException("frames stopped coming");
                System.Threading.Thread.Sleep(10);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            }
        }

        [AvaloniaFact]
        public void Recording_items_are_disabled_until_a_recording_is_running()
        {
            var w = new MainWindow();
            w.Show();
            WaitFrames(w, 60);
            w.RefreshMenuState();

            Assert.True(w.RzxRecordItem.IsEnabled);
            Assert.True(w.RZXRecordContinueItem.IsEnabled);
            Assert.False(w.RZXRecordStopItem.IsEnabled);
            Assert.False(w.RzxBookmarkItem.IsEnabled);
            Assert.False(w.RzxRollbackItem.IsEnabled);
            Assert.False(w.RzxFinishItem.IsEnabled);
            Assert.False(w.RzxDiscardItem.IsEnabled);
            w.Close();
        }

        [AvaloniaFact]
        public void Recording_flips_the_menu_and_the_status_bar_over()
        {
            var w = new MainWindow();
            w.Show();
            WaitFrames(w, 60);
            string path = Path.Combine(Path.GetTempPath(), "zero_menu_" + Guid.NewGuid().ToString("N") + ".rzx");
            try
            {
                Assert.True(w.Session.StartRzxRecordingAsync(path).Result);
                WaitFrames(w, 5);
                w.RefreshMenuState();

                Assert.False(w.RzxRecordItem.IsEnabled); // no recording on top of a recording
                Assert.False(w.RZXRecordContinueItem.IsEnabled);
                Assert.True(w.RZXRecordStopItem.IsEnabled);
                Assert.True(w.RzxBookmarkItem.IsEnabled);
                Assert.True(w.RzxRollbackItem.IsEnabled);
                Assert.True(w.RzxFinishItem.IsEnabled);
                Assert.True(w.RzxDiscardItem.IsEnabled);
                Assert.Contains("● REC", w.StatusMachine.Text);

                Assert.Equal(path, w.Session.FinishRzxRecordingAsync().Result);
                WaitFrames(w, 5);
                w.RefreshMenuState();

                Assert.True(w.RzxRecordItem.IsEnabled);
                Assert.False(w.RZXRecordStopItem.IsEnabled);
                Assert.False(w.RzxFinishItem.IsEnabled);
                Assert.DoesNotContain("● REC", w.StatusMachine.Text);
            }
            finally
            {
                try { File.Delete(path); } catch { }
                w.Close();
            }
        }

        [AvaloniaFact]
        public void The_bookmark_keys_are_shortcuts_on_every_platform()
        {
            var w = new MainWindow();
            w.Show();
            foreach (bool mac in new[] { true, false })
            {
                Assert.True(w.HandleShortcut(Press(Avalonia.Input.Key.F10, Avalonia.Input.KeyModifiers.None), mac));
                Assert.True(w.HandleShortcut(Press(Avalonia.Input.Key.F10, Avalonia.Input.KeyModifiers.Shift), mac));
            }
            w.Close();
        }

        private static Avalonia.Input.KeyEventArgs Press(Avalonia.Input.Key key, Avalonia.Input.KeyModifiers mods) =>
            new Avalonia.Input.KeyEventArgs { Key = key, KeyModifiers = mods, RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent };
    }
}
