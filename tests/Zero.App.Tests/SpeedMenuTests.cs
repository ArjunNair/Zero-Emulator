using System.Threading;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;
using Zero.Emulation;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.App.Tests
{
    /// <summary>
    /// Two knobs that were previously one. CPU Speed overclocks the Z80 and leaves the machine
    /// running at fifty frames a second; Full Speed switches the pacing off and lets it run flat out.
    /// </summary>
    public class SpeedMenuTests
    {
        public SpeedMenuTests()
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

        private static void Click(NativeMenuItem item) => item.Command.Execute(null);

        [AvaloniaFact]
        public void CPU_Speed_overclocks_the_processor_and_leaves_the_frame_rate_alone()
        {
            var w = new MainWindow();
            w.Show();
            while (w.Session.FrameCount < 10) { Thread.Sleep(10); Dispatcher.UIThread.RunJobs(); }

            Click(w.Cpu4);
            Thread.Sleep(80);
            Assert.Equal(4, w.Session.Settings.Emulation.CpuMultiplier);
            Assert.Equal(4, w.Session.Machine.cpuMultiplier);

            // The frame is still 69888 T states: more instructions fit inside it, the Spectrum does
            // not run faster. So the frame skipping must be left where it was.
            Assert.Equal(1, w.Session.Settings.Emulation.EmulationSpeed);
            w.Close();
        }

        [AvaloniaFact]
        public void Full_Speed_lifts_the_pacing_and_leaves_the_processor_alone()
        {
            var w = new MainWindow();
            w.Show();
            while (w.Session.FrameCount < 10) { Thread.Sleep(10); Dispatcher.UIThread.RunJobs(); }

            Assert.False(w.FullSpeedItem.IsChecked);
            Click(w.FullSpeedItem);
            Thread.Sleep(80);

            Assert.True(w.FullSpeedItem.IsChecked);
            Assert.Equal(MainWindow.FullSpeedFrames, w.Session.Settings.Emulation.EmulationSpeed);
            Assert.Equal(MainWindow.FullSpeedFrames, w.Session.Machine.emulationSpeed);
            Assert.Equal(1, w.Session.Settings.Emulation.CpuMultiplier);   // the Z80 is untouched

            Click(w.FullSpeedItem);
            Thread.Sleep(80);
            Assert.False(w.FullSpeedItem.IsChecked);
            Assert.Equal(1, w.Session.Settings.Emulation.EmulationSpeed);
            w.Close();
        }

        [AvaloniaFact]
        public void The_overclock_is_capped_where_it_stops_doing_anything()
        {
            // An instruction can never charge less than one T state, so once the multiplier is high
            // enough that they all charge exactly one, a frame holds its own T state count of them
            // and no more. Measured on the 128Ke, 8x, 10x and 14x all came out at 3.95x -- the same
            // as each other and barely above 4x. A setting saved before the cap has to come back
            // inside it rather than pretend to be doing something.
            MainWindow.SettingsLoader = () =>
            {
                var s = new EmulatorSettings();
                s.Paths.Roms = TestPaths.RomDir;
                s.Emulation.PauseOnFocusLost = false;
                s.Audio.Mute = true;
                s.Emulation.CpuMultiplier = 10;      // as an older config would hold it
                return s;
            };
            var w = new MainWindow();
            w.Show();
            while (w.Session.FrameCount < 10) { Thread.Sleep(10); Dispatcher.UIThread.RunJobs(); }

            Assert.Equal(EmulatorSession.MaxCpuMultiplier, w.Session.Machine.cpuMultiplier);
            w.Close();
        }
    }
}
