using System;
using System.Collections.Generic;
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
    /// The spec lets a recording carry several input blocks separated by snapshots, for multiload
    /// games: each snapshot says what the machine is before the block that follows it. No such file
    /// ships with the repo, so this builds one by splicing two independent takes together, which is
    /// the only way to catch a player that applies the first snapshot and ignores the rest.
    /// </summary>
    [Collection("PZXFile static state")]
    public class RzxMultiloadTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly List<EmulatorSession> _sessions = new List<EmulatorSession>();
        private readonly List<string> _files = new List<string>();
        private readonly List<string> _errors = new List<string>();

        public RzxMultiloadTests(ITestOutputHelper output) { _output = output; }

        public void Dispose()
        {
            foreach (EmulatorSession s in _sessions) s.Dispose();
            foreach (string f in _files) { try { File.Delete(f); } catch { } }
        }

        private string TempFile()
        {
            string p = Path.Combine(Path.GetTempPath(), "zero_ml_" + Guid.NewGuid().ToString("N") + ".rzx");
            _files.Add(p);
            return p;
        }

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

        /// <summary>Record one finalised take, typing <paramref name="during"/> while it runs.</summary>
        private async Task<string> RecordTake(string path, keyCode[] before, keyCode[] during)
        {
            EmulatorSession s = NewSession();
            await RunFrames(s, 250);
            foreach (keyCode k in before) await Press(s, k);   // shapes the state the snapshot captures
            Assert.True(await s.StartRzxRecordingAsync(path));
            foreach (keyCode k in during) await Press(s, k);
            await RunFrames(s, 20);
            Assert.Equal(path, await s.FinishRzxRecordingAsync());
            return await Screen(s);
        }

        /// <summary>(id, offset, size) for each RZX block after the 10-byte file header.</summary>
        private static List<(byte Id, int Offset, int Size)> Blocks(byte[] file)
        {
            var blocks = new List<(byte, int, int)>();
            int off = 10;
            while (off + 5 <= file.Length)
            {
                int size = BitConverter.ToInt32(file, off + 1);
                if (size <= 0) break;
                blocks.Add((file[off], off, size));
                off += size;
            }
            return blocks;
        }

        private static byte[] Block(byte[] file, byte id)
        {
            foreach ((byte Id, int Offset, int Size) b in Blocks(file))
                if (b.Id == id)
                    return file[b.Offset..(b.Offset + b.Size)];
            throw new InvalidOperationException("no 0x" + id.ToString("x2") + " block");
        }

        [Fact]
        public async Task A_snapshot_between_two_input_blocks_is_applied()
        {
            // Two takes whose machines differ: the second types 7 8 9 before recording starts, so its
            // snapshot is load-bearing — replaying its input against the first take's state diverges.
            string first = TempFile(), second = TempFile();
            await RecordTake(first, before: new keyCode[0], during: new[] { keyCode._1 });
            string secondEnding = await RecordTake(second,
                before: new[] { keyCode._7, keyCode._8, keyCode._9 },
                during: new[] { keyCode._2 });

            byte[] a = File.ReadAllBytes(first), b = File.ReadAllBytes(second);
            const byte creator = 0x10, snapshot = 0x30, irb = 0x80;
            string spliced = TempFile();
            using (var w = new BinaryWriter(File.Create(spliced)))
            {
                w.Write(a[..10]);                 // file header
                w.Write(Block(a, creator));
                w.Write(Block(a, snapshot));      // load 1
                w.Write(Block(a, irb));
                w.Write(Block(b, snapshot));      // load 2 — the one that used to be dropped
                w.Write(Block(b, irb));
            }

            List<(byte Id, int Offset, int Size)> layout = Blocks(File.ReadAllBytes(spliced));
            Assert.Equal(new byte[] { creator, snapshot, irb, snapshot, irb }, layout.ConvertAll(x => x.Id));

            EmulatorSession player = NewSession();
            await RunFrames(player, 150);
            Assert.True(await player.LoadFileAsync(spliced));
            Assert.Equal(EmulatorState.PlayingRzx, player.State);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (player.State == EmulatorState.PlayingRzx && sw.Elapsed < TimeSpan.FromMinutes(2))
                await Task.Delay(20);
            Assert.Equal(EmulatorState.Running, player.State);

            string played = await Screen(player);
            _output.WriteLine(played);
            // Ends where the second take ended. Skipping its snapshot lands on the first take's
            // machine with the second take's keys replayed into it, which is a different screen.
            Assert.Equal(secondEnding, played);
            Assert.Empty(_errors);
        }

        [Fact]
        public async Task A_trailing_snapshot_does_not_disturb_the_end_of_playback()
        {
            // What Stop leaves behind: CREATOR SNAPSHOT IRB SNAPSHOT. That last block is a resume
            // point, not a load, and applying it would overwrite the state playback just reached.
            string path = TempFile();
            EmulatorSession s = NewSession();
            await RunFrames(s, 250);
            Assert.True(await s.StartRzxRecordingAsync(path));
            await Press(s, keyCode._1);
            await RunFrames(s, 20);
            string recorded = await Screen(s);
            s.StopRzxRecording();
            await RunFrames(s, 5);

            List<(byte Id, int Offset, int Size)> layout = Blocks(File.ReadAllBytes(path));
            Assert.Equal(0x30, layout[layout.Count - 1].Id); // unfinalised: ends on a snapshot

            EmulatorSession player = NewSession();
            await RunFrames(player, 150);
            Assert.True(await player.LoadFileAsync(path));
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (player.State == EmulatorState.PlayingRzx && sw.Elapsed < TimeSpan.FromMinutes(2))
                await Task.Delay(20);

            Assert.Equal(EmulatorState.Running, player.State);
            Assert.Equal(recorded, await Screen(player));
            Assert.Empty(_errors);
        }
    }
}
