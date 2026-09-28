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
    /// Stop leaves a recording resumable; Continue picks it up and the finished file replays as one
    /// take. The interesting half is across sessions, which is what Continue is for and the only way
    /// to catch state that a single long-lived machine happens to still be holding.
    /// </summary>
    [Collection("PZXFile static state")]
    public class RzxContinueTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _path = Path.Combine(Path.GetTempPath(), "zero_cont_" + Guid.NewGuid().ToString("N") + ".rzx");
        private readonly System.Collections.Generic.List<EmulatorSession> _sessions = new System.Collections.Generic.List<EmulatorSession>();
        private readonly System.Collections.Generic.List<string> _errors = new System.Collections.Generic.List<string>();

        public RzxContinueTests(ITestOutputHelper output) { _output = output; }

        public void Dispose()
        {
            foreach (EmulatorSession s in _sessions) s.Dispose();
            try { File.Delete(_path); } catch { }
        }

        /// <summary>A fresh machine, as if the emulator had just been launched.</summary>
        private EmulatorSession NewSession()
        {
            var s = new EmulatorSession(new EmulatorSettings())
            {
                RomDirectory = TestPaths.RomDir,
                AudioFactory = () => new NullAudioOutput()
            };
            s.Error += e => { lock (_errors) _errors.Add(e); _output.WriteLine("ERROR: " + e); };
            _sessions.Add(s);
            s.Start();
            return s;
        }

        private static async Task RunFrames(EmulatorSession s, int frames)
        {
            long target = s.FrameCount + frames;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (s.FrameCount < target)
            {
                if (sw.ElapsedMilliseconds > 30000) throw new TimeoutException("frames stopped coming");
                await Task.Delay(4);
            }
        }

        private static Task<string> Screen(EmulatorSession s) => s.InvokeAsync(() => ScreenText.Dump(s.Machine));

        private static async Task Press(EmulatorSession s, keyCode key)
        {
            s.Keyboard.SetKey(key, true);
            await RunFrames(s, 6);
            s.Keyboard.SetKey(key, false);
            await RunFrames(s, 6);
        }

        private static async Task<string> Replay(EmulatorSession s, string path)
        {
            Assert.True(await s.LoadFileAsync(path));
            Assert.Equal(EmulatorState.PlayingRzx, s.State);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (s.State == EmulatorState.PlayingRzx && sw.Elapsed < TimeSpan.FromMinutes(2))
                await Task.Delay(20);
            Assert.Equal(EmulatorState.Running, s.State); // ran to the end rather than stalling
            return await Screen(s);
        }

        [Fact]
        public async Task A_recording_resumed_in_a_new_session_replays_as_one_take()
        {
            // First sitting: type 1, then stop without finalising.
            EmulatorSession first = NewSession();
            await RunFrames(first, 250);
            Assert.True(await first.StartRzxRecordingAsync(_path));
            await Press(first, keyCode._1);
            first.StopRzxRecording();
            await RunFrames(first, 5);
            Assert.False(first.IsRecordingRzx);
            Assert.Equal(EmulatorState.Running, first.State);
            long stoppedSize = new FileInfo(_path).Length;
            Assert.True(stoppedSize > 0);

            // Second sitting, brand new machine: resume and type 2.
            EmulatorSession second = NewSession();
            await RunFrames(second, 150);
            Assert.True(await second.ContinueRzxRecordingAsync(_path));
            await RunFrames(second, 5);
            Assert.True(second.IsRecordingRzx);
            Assert.Equal(EmulatorState.RecordingRzx, second.State); // the status bar reads off this

            await Press(second, keyCode._2);
            await RunFrames(second, 30);
            string recorded = await Screen(second);
            Assert.Contains("12", recorded); // both sittings' keys are on screen

            Assert.Equal(_path, await second.FinishRzxRecordingAsync());
            Assert.False(second.IsRecordingRzx);

            // Third sitting: the finished file replays both halves as one recording.
            string replayed = await Replay(NewSession(), _path);
            _output.WriteLine(replayed);
            Assert.Equal(recorded, replayed);
            Assert.Empty(_errors);
        }

        [Fact]
        public async Task Continuing_refuses_a_finalised_recording()
        {
            EmulatorSession s = NewSession();
            await RunFrames(s, 250);
            Assert.True(await s.StartRzxRecordingAsync(_path));
            await Press(s, keyCode._1);
            Assert.Equal(_path, await s.FinishRzxRecordingAsync()); // finalised: no continue snapshot

            Assert.False(await s.ContinueRzxRecordingAsync(_path));
            Assert.False(s.IsRecordingRzx);            // not left half-open
            Assert.Equal(EmulatorState.Running, s.State);
            Assert.Single(_errors);

            long before = s.FrameCount;                // and the machine carries on unharmed
            await RunFrames(s, 30);
            Assert.True(s.FrameCount > before);
            Assert.Contains("1", await Screen(s));     // still showing what was typed before the refusal
        }

        [Fact]
        public async Task A_reset_while_recording_keeps_the_file()
        {
            EmulatorSession s = NewSession();
            await RunFrames(s, 250);
            Assert.True(await s.StartRzxRecordingAsync(_path));
            await Press(s, keyCode._1);

            s.Reset(false);                            // cannot be recorded, so the take has to end
            await RunFrames(s, 10);

            Assert.False(s.IsRecordingRzx);
            Assert.Equal(EmulatorState.Running, s.State);
            Assert.True(File.Exists(_path));           // finished and kept, never silently deleted
            Assert.True(new FileInfo(_path).Length > 0);

            string replayed = await Replay(NewSession(), _path);
            Assert.Contains("1", replayed);            // and what it captured is intact
            Assert.Empty(_errors);
        }

        [Fact]
        public async Task Discarding_still_deletes_the_file()
        {
            EmulatorSession s = NewSession();
            await RunFrames(s, 200);
            Assert.True(await s.StartRzxRecordingAsync(_path));
            await RunFrames(s, 20);

            s.DiscardRzxRecording();
            await RunFrames(s, 5);

            Assert.False(s.IsRecordingRzx);
            Assert.False(File.Exists(_path));          // only the explicit discard throws work away
            Assert.Empty(_errors);
        }
    }
}
