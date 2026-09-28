using System;
using System.IO;
using System.Threading.Tasks;
using SpeccyCommon;
using Xunit;
using Xunit.Abstractions;
using Zero.Emulation;
using Zero.Emulation.Host;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.Core.Tests
{
    /// <summary>
    /// Round-trips a recording: drive the machine by hand, record it, then replay the file and check
    /// the replay lands on the same screen. A recording that drops its inputs, or drifts by a frame,
    /// cannot pass this — the replay ends up somewhere else.
    /// </summary>
    [Collection("PZXFile static state")]
    public class RzxRecordingTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly EmulatorSession _session;
        private readonly System.Collections.Generic.List<string> _errors = new System.Collections.Generic.List<string>();
        private readonly string _path = Path.Combine(Path.GetTempPath(), "zero_rec_" + Guid.NewGuid().ToString("N") + ".rzx");

        public RzxRecordingTests(ITestOutputHelper output)
        {
            _output = output;
            _session = new EmulatorSession(new EmulatorSettings())
            {
                RomDirectory = TestPaths.RomDir,
                AudioFactory = () => new NullAudioOutput()
            };
            _session.Error += e => { lock (_errors) _errors.Add(e); _output.WriteLine("ERROR: " + e); };
        }

        public void Dispose()
        {
            _session.Dispose();
            try { File.Delete(_path); } catch { }
        }

        private async Task RunFrames(int frames, int timeoutMs = 30000)
        {
            long target = _session.FrameCount + frames;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (_session.FrameCount < target)
            {
                if (sw.ElapsedMilliseconds > timeoutMs) throw new TimeoutException("frames stopped coming");
                await Task.Delay(5);
            }
        }

        private Task<string> Screen() => _session.InvokeAsync(() => ScreenText.Dump(_session.Machine));

        /// <summary>Hold a key down long enough for the ROM's keyboard scan to see it, then let go.</summary>
        private async Task Press(keyCode key)
        {
            _session.Keyboard.SetKey(key, true);
            await RunFrames(6);
            _session.Keyboard.SetKey(key, false);
            await RunFrames(6);
        }

        [Fact]
        public async Task Records_typing_and_replays_to_the_same_screen()
        {
            _session.Start();
            await RunFrames(250); // boot to the copyright message
            string boot = await Screen();

            Assert.True(await _session.StartRzxRecordingAsync(_path));
            Assert.Equal(EmulatorState.RecordingRzx, _session.State);
            Assert.True(_session.IsRecordingRzx);

            foreach (keyCode key in new[] { keyCode._1, keyCode._2, keyCode._3, keyCode._4 })
                await Press(key);
            await RunFrames(40);

            string recorded = await Screen();
            Assert.NotEqual(boot, recorded); // the keys really did reach the machine

            string written = await _session.FinishRzxRecordingAsync();
            Assert.Equal(_path, written);
            Assert.Equal(EmulatorState.Running, _session.State);
            Assert.False(_session.IsRecordingRzx);
            Assert.True(new FileInfo(_path).Length > 0);

            // Replay it. The recording carries its own opening snapshot, so this starts from scratch.
            Assert.True(await _session.LoadFileAsync(_path));
            Assert.Equal(EmulatorState.PlayingRzx, _session.State);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (_session.State == EmulatorState.PlayingRzx && sw.Elapsed < TimeSpan.FromMinutes(2))
                await Task.Delay(20);

            string replayed = await Screen();
            _output.WriteLine(replayed);
            Assert.Equal(EmulatorState.Running, _session.State); // played to the end, not stalled
            Assert.Equal(recorded, replayed);
            Assert.Empty(_errors); // "Invalid RZX frame" would mean the replay drifted
        }

        [Fact]
        public async Task Discarding_a_recording_removes_the_file()
        {
            _session.Start();
            await RunFrames(120);
            Assert.True(await _session.StartRzxRecordingAsync(_path));
            await RunFrames(30);
            Assert.True(File.Exists(_path));

            _session.DiscardRzxRecording();
            await RunFrames(5);

            Assert.False(_session.IsRecordingRzx);
            Assert.Equal(EmulatorState.Running, _session.State);
            Assert.False(File.Exists(_path));
            Assert.Empty(_errors);
        }

        [Fact]
        public async Task Recording_to_an_unwritable_path_reports_an_error_and_keeps_running()
        {
            _session.Start();
            await RunFrames(120);
            string bad = Path.Combine(Path.GetTempPath(), "zero_no_such_dir_" + Guid.NewGuid().ToString("N"), "x.rzx");

            Assert.False(await _session.StartRzxRecordingAsync(bad));
            Assert.False(_session.IsRecordingRzx);
            Assert.Equal(EmulatorState.Running, _session.State);
            Assert.Single(_errors);

            await RunFrames(30); // the machine is unharmed
            Assert.Contains("© 1982 Sinclair Research Ltd", await Screen());
        }

        [Fact]
        public async Task A_rollback_returns_the_machine_to_the_bookmark()
        {
            _session.Start();
            await RunFrames(250);
            Assert.True(await _session.StartRzxRecordingAsync(_path));
            await RunFrames(20);

            _session.InsertRzxBookmark();
            await RunFrames(5);
            string atBookmark = await Screen();

            foreach (keyCode key in new[] { keyCode._7, keyCode._8, keyCode._9 })
                await Press(key);
            Assert.NotEqual(atBookmark, await Screen()); // typing moved us on

            _session.RollbackRzx();
            await RunFrames(5);

            Assert.Equal(atBookmark, await Screen());
            Assert.True(_session.IsRecordingRzx); // still recording after the rewind
            Assert.Equal(EmulatorState.RecordingRzx, _session.State);
            Assert.Empty(_errors);
        }
    }
}
