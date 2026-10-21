using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Zero.Emulation;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.Core.Tests
{
    /// <summary>
    /// Edge loading must load exactly what loading at normal speed does, only sooner: the ROM
    /// loader reads every bit from the pulses, so a skip that counts its passes wrong shows up as
    /// a wrong byte or a loading error. With edge loading off, auto play must still start the tape.
    /// </summary>
    [Collection("PZXFile static state")]
    public class EdgeLoadingTests
    {
        private const int ProgSysVar = 23635; // PROG: where BASIC's program starts

        [Fact]
        public async Task Edge_loading_loads_the_same_bytes_as_normal_loading_in_fewer_frames()
        {
            byte[] program = Program(1500);
            string path = Path.Combine(Path.GetTempPath(), "zero_edge_" + Guid.NewGuid().ToString("N") + ".tzx");
            File.WriteAllBytes(path, Tape(program));
            try
            {
                long normal = (await FramesToLoad(path, program, edgeLoad: false)).frames;
                long edge = (await FramesToLoad(path, program, edgeLoad: true)).frames;
                Assert.True(edge < normal * 0.8, $"edge loading took {edge} frames, normal loading {normal}");
            }
            finally { File.Delete(path); }
        }

        /// <summary>
        /// At 1x the machine keeps to real time on its audio clock, but not while edge loading is
        /// at work: that's what it's for. And once loading is done, real time again, straight away.
        /// </summary>
        [Fact]
        public async Task Edge_loading_runs_unpaced_and_real_time_comes_back_after()
        {
            byte[] program = Program(1500);
            string path = Path.Combine(Path.GetTempPath(), "zero_edge_" + Guid.NewGuid().ToString("N") + ".tzx");
            File.WriteAllBytes(path, Tape(program));
            try
            {
                var (frames, seconds, after) = await FramesToLoad(path, program, edgeLoad: true, paced: true);
                Assert.True(seconds < frames / 50.0 * 0.6, $"{frames} frames took {seconds:F1}s: loading kept to real time");
                Assert.InRange(after, 0.5, 2.5); // the next 50 frames, at about 1 s
            }
            finally { File.Delete(path); }
        }

        /// <summary>
        /// Load the tape with auto load and auto play: the frames until the program is in memory,
        /// the seconds they took, and the seconds the 50 frames after took.
        /// </summary>
        private static async Task<(long frames, double seconds, double after)> FramesToLoad(string path, byte[] program, bool edgeLoad, bool paced = false)
        {
            var settings = new EmulatorSettings();
            settings.Tape.AutoLoad = true;
            settings.Tape.AutoPlay = true;
            settings.Tape.EdgeLoad = edgeLoad;
            settings.Tape.FastLoad = false;
            using var session = new EmulatorSession(settings)
            {
                RomDirectory = TestPaths.RomDir,
                AudioFactory = () => paced ? new Zero.Emulation.Host.TimerPacedAudioOutput() : new NullAudioOutput()
            };

            var outcome = new TaskCompletionSource<(long, double, double)>(TaskCreationOptions.RunContinuationsAsynchronously);
            long frames = 0, inserted = 0, loadedFrames = 0, loadedAt = 0;
            double loadSeconds = 0;
            var clock = new System.Diagnostics.Stopwatch();
            session.FrameReady += () =>
            {
                var zx = session.Machine;
                if (++frames == 5) { session.LoadFile(path); inserted = frames; clock.Start(); }
                if (loadedAt > 0)
                {
                    if (frames == loadedAt + 50) outcome.TrySetResult((loadedFrames, loadSeconds, clock.Elapsed.TotalSeconds - loadSeconds));
                    return;
                }
                if (inserted == 0 || frames % 10 != 0) return;
                string screen = ScreenText.Dump(zx);
                if (screen.Contains("loading error"))
                    outcome.TrySetException(new Exception($"tape loading error (edge loading {edgeLoad})"));
                else if (zx.blockCounter == 1 && !zx.tapeIsPlaying && Loaded(zx, program))
                {
                    loadedFrames = frames - inserted;
                    loadSeconds = clock.Elapsed.TotalSeconds;
                    loadedAt = frames;
                }
                else if (frames > 6000)
                    outcome.TrySetException(new TimeoutException(
                        $"not loaded (edge loading {edgeLoad}): block {zx.blockCounter}, playing {zx.tapeIsPlaying}"));
            };
            session.Start();

            Task done = await Task.WhenAny(outcome.Task, Task.Delay(60000));
            Assert.True(done == outcome.Task, "timed out");
            return await outcome.Task;
        }

        private static bool Loaded(Speccy.zx_spectrum zx, byte[] program)
        {
            int prog = zx.PeekByteNoContend(ProgSysVar) | zx.PeekByteNoContend(ProgSysVar + 1) << 8;
            for (int i = 0; i < program.Length; i++)
                if (zx.PeekByteNoContend((ushort)(prog + i)) != program[i])
                    return false;
            return true;
        }

        /// <summary>10 REM and printable characters: lists cleanly when the ROM shows it after loading.</summary>
        private static byte[] Program(int length)
        {
            var rnd = new Random(7);
            var line = new List<byte> { 0xEA }; // REM
            for (int i = 0; i < length; i++) line.Add((byte)rnd.Next(32, 127));
            line.Add(0x0D);
            var program = new List<byte> { 0x00, 0x0A, (byte)line.Count, (byte)(line.Count >> 8) };
            program.AddRange(line);
            return program.ToArray();
        }

        /// <summary>A program header (no autostart) and the program, as two standard TZX blocks.</summary>
        private static byte[] Tape(byte[] program)
        {
            var header = new List<byte> { 0x00, 0x00 };
            header.AddRange(Encoding.ASCII.GetBytes("edgetest  "));
            int n = program.Length;
            header.AddRange(new byte[] { (byte)n, (byte)(n >> 8), 0x00, 0x80, (byte)n, (byte)(n >> 8) });
            var data = new List<byte> { 0xFF };
            data.AddRange(program);
            return TapeBuilders.Tzx(TapeBuilders.Standard(1000, WithParity(header)), TapeBuilders.Standard(1000, WithParity(data)));
        }

        private static byte[] WithParity(List<byte> bytes)
        {
            byte parity = 0;
            foreach (byte b in bytes) parity ^= b;
            return bytes.Append(parity).ToArray();
        }
    }
}
