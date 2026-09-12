using Zero.TestSupport;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Speccy;
using SpeccyCommon;
using Xunit;
using Xunit.Abstractions;
using Zero.Emulation;
using Zero.Emulation.Host;
using Zero.Emulation.Settings;

namespace Zero.Core.Tests
{
    /// <summary>Drives the host-neutral session the way a UI shell would, but headless and unpaced.</summary>
    public class EmulatorSessionTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly EmulatorSession _session;
        private readonly System.Collections.Generic.List<string> _errors = new System.Collections.Generic.List<string>();

        public EmulatorSessionTests(ITestOutputHelper output)
        {
            _output = output;
            var settings = new EmulatorSettings();
            settings.Tape.FastLoad = true;
            settings.Tape.AutoLoad = true;
            _session = new EmulatorSession(settings)
            {
                RomDirectory = TestPaths.RomDir,
                AudioFactory = () => new NullAudioOutput()
            };
            _session.Error += e => { lock (_errors) _errors.Add(e); _output.WriteLine("ERROR: " + e); };
        }

        public void Dispose() => _session.Dispose();

        private async Task RunFrames(int frames, int timeoutMs = 30000)
        {
            long target = _session.FrameCount + frames;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (_session.FrameCount < target)
            {
                if (sw.ElapsedMilliseconds > timeoutMs)
                    throw new TimeoutException($"only {_session.FrameCount - (target - frames)} of {frames} frames ran");
                await Task.Delay(5);
            }
        }

        private Task<string> Screen() => _session.InvokeAsync(() => ScreenText.Dump(_session.Machine));

        private Task<bool> ScreenContains(string text) => _session.InvokeAsync(() => ScreenText.Dump(_session.Machine).Contains(text));

        [Fact]
        public async Task Starts_boots_and_produces_frames()
        {
            _session.Start();
            await RunFrames(300);
            Assert.Equal(EmulatorState.Running, _session.State);
            Assert.Equal(MachineModel._48k, _session.Model);
            string screen = await Screen();
            Assert.Contains("© 1982 Sinclair Research Ltd", screen);

            VideoFrame frame = _session.Frames.TryAcquireLatest();
            Assert.NotNull(frame);
            Assert.Equal(352, frame.Width);
            Assert.Equal(296, frame.Height);
            Assert.Equal(0xC0C0C0, frame.Pixels[0] & 0xFFFFFF); // white border
            Assert.Empty(_errors);
        }

        [Fact]
        public async Task Pause_stops_frames_and_resume_continues()
        {
            _session.Start();
            await RunFrames(20);
            _session.Pause();
            await Task.Delay(50);
            long paused = _session.FrameCount;
            await Task.Delay(100);
            Assert.InRange(_session.FrameCount, paused, paused + 1);
            Assert.Equal(EmulatorState.Paused, _session.State);
            _session.Resume();
            await RunFrames(20);
            Assert.Equal(EmulatorState.Running, _session.State);
        }

        [Fact]
        public async Task Switching_machine_changes_model_and_boots()
        {
            _session.Start();
            await RunFrames(5);
            _session.SwitchMachine(MachineModel._128k);
            await RunFrames(300);
            Assert.Equal(MachineModel._128k, _session.Model);
            Assert.Contains("128 BASIC", await Screen());
            Assert.Empty(_errors);
        }

        [Fact]
        public async Task Loads_z80_snapshot_and_switches_to_its_machine()
        {
            _session.Start();
            await RunFrames(5);
            string path = Path.Combine(TestPaths.ProgramsDir, "Demos", "NMI3_48k.z80");
            Assert.True(await _session.LoadFileAsync(path));
            Assert.Equal(MachineModel._48k, _session.Model);
            await RunFrames(50);
            // A running demo has drawn something other than the ROM's boot screen.
            Assert.DoesNotContain("© 1982 Sinclair", await Screen());
            Assert.Empty(_errors);
        }

        [Fact]
        public async Task Inserting_a_tap_autoloads_it()
        {
            _session.Start();
            await RunFrames(5);
            string path = Path.Combine(TestPaths.ProgramsDir, "Demos", "Overscan.tap");
            Assert.True(await _session.LoadFileAsync(path));
            Assert.True(_session.Tape.IsInserted);
            Assert.Equal("Overscan", _session.Tape.Title);

            // Auto-load types LOAD "" after the reset; with flash-load the ROM loader reads the
            // blocks instantly, so within a few seconds of emulated time the program is running.
            bool loaded = false;
            for (int i = 0; i < 40 && !loaded; i++)
            {
                await RunFrames(25);
                string screen = await Screen();
                loaded = !screen.Contains("© 1982 Sinclair") && !screen.Contains("LOAD") && screen.Trim().Length > 0 || _session.InvokeAsync(() => _session.Machine.cpu.regs.PC >= 0x8000).Result;
            }
            _output.WriteLine(await Screen());
            Assert.True(loaded, "tape did not auto-load");
            Assert.Empty(_errors);
        }

        [Fact]
        public async Task Plays_rzx_recording_for_3000_frames_without_desync()
        {
            await PlayRzx("thundercats.rzx", maxFrames: 3000);
        }

        /// <remarks>Full-length replays take 1-3 minutes each; run with --filter "Category=Slow".</remarks>
        [Theory]
        [Trait("Category", "Slow")]
        [InlineData("garfield.rzx")]
        [InlineData("greenberet.rzx")]
        [InlineData("thundercats.rzx")]
        public async Task Plays_rzx_recording_to_the_end_without_desync(string file)
        {
            await PlayRzx(file, maxFrames: long.MaxValue);
        }

        private async Task PlayRzx(string file, long maxFrames)
        {
            _session.Start();
            await RunFrames(5);
            string path = Path.Combine(TestPaths.ProgramsDir, "Action Replay", file);
            Assert.True(await _session.LoadFileAsync(path));
            Assert.Equal(EmulatorState.PlayingRzx, _session.State);

            long start = _session.FrameCount;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (_session.State == EmulatorState.PlayingRzx && _session.FrameCount - start < maxFrames && sw.Elapsed < TimeSpan.FromMinutes(10))
                await Task.Delay(50);

            _output.WriteLine($"{file}: {_session.FrameCount - start} frames in {sw.Elapsed.TotalSeconds:F1}s, model {_session.Model}");
            _output.WriteLine(await Screen());
            if (maxFrames == long.MaxValue)
                Assert.Equal(EmulatorState.Running, _session.State); // recording ran to completion
            Assert.Empty(_errors); // an "Invalid RZX frame" error means CPU timing drifted from the recording
        }

        [Fact]
        public async Task Saves_and_reloads_szx_snapshot()
        {
            _session.Start();
            await RunFrames(200);
            string tmp = Path.Combine(Path.GetTempPath(), "zero_test_" + Guid.NewGuid().ToString("N") + ".szx");
            try
            {
                _session.SaveSnapshot(tmp);
                await _session.InvokeAsync(() => { });
                Assert.True(File.Exists(tmp));
                _session.Reset(true);
                await RunFrames(10);
                Assert.True(await _session.LoadFileAsync(tmp));
                await RunFrames(2);
                Assert.Contains("© 1982 Sinclair Research Ltd", await Screen());
            }
            finally { File.Delete(tmp); }
            Assert.Empty(_errors);
        }

        [Fact]
        public void Settings_round_trip_through_json()
        {
            var s = new EmulatorSettings();
            s.Emulation.Model = MachineModel._pentagon;
            s.Audio.Volume = 77;
            s.AddRecentFile("/tmp/a.tap");
            string file = Path.Combine(Path.GetTempPath(), "zero_settings_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                s.Save(file);
                EmulatorSettings back = EmulatorSettings.Load(file);
                Assert.Equal(MachineModel._pentagon, back.Emulation.Model);
                Assert.Equal(77, back.Audio.Volume);
                Assert.Equal("/tmp/a.tap", Assert.Single(back.RecentFiles));
            }
            finally { File.Delete(file); }
        }
    }
}
