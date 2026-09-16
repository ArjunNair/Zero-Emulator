using System;
using System.Globalization;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.App.Tests
{
    public class StatusFpsTests
    {
        public StatusFpsTests()
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

        [AvaloniaFact]
        public void The_rate_shown_is_what_the_machine_ran_not_what_the_screen_drew()
        {
            var w = new MainWindow();
            w.Show();

            // Let the machine run without ever letting a frame reach the screen: no render timer tick
            // here, and a frame whose predecessor is still queued is dropped rather than held. The
            // reading has to come from the machine, so it must survive that.
            DateTime start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalSeconds < 1.3) Thread.Sleep(20);

            long ran = w.Session.FrameCount;
            long drew = w.Display.FramesPresented;
            w.UpdateStatusBar();

            string shown = ((TextBlock)w.FindControl<TextBlock>("StatusFps")).Text;
            Assert.EndsWith("fps", shown);
            double fps = double.Parse(shown.Replace(" fps", ""), CultureInfo.InvariantCulture);

            Assert.True(ran > 40, $"the machine barely ran: {ran} frames");
            Assert.True(drew < ran / 2, $"frames reached the screen after all, so this proves nothing: {drew} of {ran}");
            Assert.True(fps > 30, $"the rate shown followed the screen, not the machine: {fps:F0} fps "
                                  + $"from {ran} frames run and {drew} drawn");
            w.Close();
        }
    }
}
