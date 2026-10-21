using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Zero.Emulation;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.Core.Tests
{
    /// <summary>
    /// The ROM reads only the bytes its header asks for. Some tapes carry more in the same block,
    /// straight after, for the program just loaded to read with its own loader (Raxoft's Wizball
    /// keeps its whole game there). Fast loading used to skip the rest of the block, so the loader
    /// found no tape and the game never loaded.
    /// </summary>
    [Collection("PZXFile static state")]
    public class FlashLoadTests
    {
        // PZX: PZXT, PULS, DATA (header), PULS, DATA (program). TZX: a standard block for each.
        [Theory]
        [InlineData(".pzx", 4)]
        [InlineData(".tzx", 1)]
        public async Task Fast_loading_plays_on_after_the_bytes_the_rom_asked_for(string format, int DataBlockIndex)
        {
            byte[] program = { 0x00, 0x0A, 0x02, 0x00, 0xEA, 0x0D }; // 10 REM
            byte[] extra = new byte[1000];
            new Random(1).NextBytes(extra);
            string path = Path.Combine(Path.GetTempPath(), "zero_flash_" + Guid.NewGuid().ToString("N") + format);
            File.WriteAllBytes(path, format == ".pzx" ? BuildTape(program, extra) : BuildTzx(program, extra));
            try
            {
                var settings = new EmulatorSettings();
                settings.Tape.AutoLoad = true;
                settings.Tape.FastLoad = true;
                settings.Tape.RomTraps = true;
                using var session = new EmulatorSession(settings)
                {
                    RomDirectory = TestPaths.RomDir,
                    AudioFactory = () => new NullAudioOutput()
                };

                // Look on the emulation thread at the first frame after the program block loaded.
                var outcome = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                long frames = 0;
                session.FrameReady += () =>
                {
                    var zx = session.Machine;
                    if (++frames == 5) session.LoadFile(path);
                    if (zx.blockCounter == DataBlockIndex) outcome.TrySetResult(zx.tapeIsPlaying);
                    else if (frames > 3000) outcome.TrySetException(new TimeoutException("the program block never loaded"));
                };
                session.Start();

                Task done = await Task.WhenAny(outcome.Task, Task.Delay(30000));
                Assert.True(done == outcome.Task, "timed out");
                Assert.True(await outcome.Task, "the tape stopped instead of playing the rest of the block");
            }
            finally { File.Delete(path); }
        }

        /// <summary>
        /// Fast loading with auto play off: only the ROM trap moves the tape on. Each block of a
        /// TAP comes as pilot, data and a pause, so after the first block the trap has a pause
        /// before the next data; it used to give up there and leave the ROM waiting for a tape
        /// that never played.
        /// </summary>
        [Fact]
        public async Task Fast_loading_reads_every_block_of_a_tap_without_playing_it()
        {
            const int LastDataBlock = 11; // PZXT, then PULS DATA PAUS for each of 4 blocks
            var settings = new EmulatorSettings();
            settings.Tape.AutoLoad = true;
            settings.Tape.FastLoad = true;
            settings.Tape.AutoPlay = false;
            settings.Tape.EdgeLoad = false;
            settings.Tape.RomTraps = true;
            using var session = new EmulatorSession(settings)
            {
                RomDirectory = TestPaths.RomDir,
                AudioFactory = () => new NullAudioOutput()
            };

            string path = Path.Combine(TestPaths.ProgramsDir, "Demos", "Overscan.tap");
            var outcome = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            long frames = 0;
            session.FrameReady += () =>
            {
                var zx = session.Machine;
                if (++frames == 5) session.LoadFile(path);
                if (zx.tapeIsPlaying) outcome.TrySetResult("the tape played");
                else if (zx.blockCounter == LastDataBlock) outcome.TrySetResult("");
                else if (frames > 3000) outcome.TrySetResult($"stuck at block {zx.blockCounter}");
            };
            session.Start();

            Task done = await Task.WhenAny(outcome.Task, Task.Delay(30000));
            Assert.True(done == outcome.Task, "timed out");
            Assert.Equal("", await outcome.Task);
        }

        /// <summary>A real TZX of standard blocks flash-loads its first program.</summary>
        [Fact]
        public async Task Fast_loading_reads_a_tzx()
        {
            var settings = new EmulatorSettings();
            settings.Tape.AutoLoad = true;
            settings.Tape.FastLoad = true;
            settings.Tape.AutoPlay = false;
            settings.Tape.EdgeLoad = false;
            settings.Tape.RomTraps = true;
            using var session = new EmulatorSession(settings)
            {
                RomDirectory = TestPaths.RomDir,
                AudioFactory = () => new NullAudioOutput()
            };

            // Block 0 is a text block, then a program's header (1) and its data (2).
            string path = Path.Combine(TestPaths.ProgramsDir, "Horizons - Side A.tzx");
            var outcome = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            long frames = 0;
            session.FrameReady += () =>
            {
                var zx = session.Machine;
                if (++frames == 5) session.LoadFile(path);
                if (zx.blockCounter >= 2 || frames > 3000) outcome.TrySetResult(zx.blockCounter);
            };
            session.Start();

            Task done = await Task.WhenAny(outcome.Task, Task.Delay(30000));
            Assert.True(done == outcome.Task, "timed out");
            Assert.True(await outcome.Task >= 2, "the program block never loaded");
            Assert.Equal("Horizons - Side A", await session.InvokeAsync(() => session.Tape.Title));
        }

        private static (byte[] header, byte[] data) RomBlocks(byte[] program, byte[] extra)
        {
            var header = new List<byte> { 0x00, 0x00 };                         // flag, type: program
            header.AddRange(Encoding.ASCII.GetBytes("flashtest "));
            header.AddRange(new byte[] { (byte)program.Length, 0, 0x00, 0x80, (byte)program.Length, 0 }); // no autostart

            var data = new List<byte> { 0xFF };
            data.AddRange(program);
            var withExtra = new List<byte>(WithParity(data));
            withExtra.AddRange(extra);
            return (WithParity(header), withExtra.ToArray());
        }

        /// <summary>The same tape as <see cref="BuildTape"/>, as two TZX standard speed blocks.</summary>
        private static byte[] BuildTzx(byte[] program, byte[] extra)
        {
            var (header, data) = RomBlocks(program, extra);
            return TapeBuilders.Tzx(TapeBuilders.Standard(0, header), TapeBuilders.Standard(0, data));
        }

        /// <summary>A program header, then a block holding the program and <paramref name="extra"/> after it.</summary>
        private static byte[] BuildTape(byte[] program, byte[] extra)
        {
            var header = new List<byte> { 0x00, 0x00 };                         // flag, type: program
            header.AddRange(Encoding.ASCII.GetBytes("flashtest "));
            header.AddRange(new byte[] { (byte)program.Length, 0, 0x00, 0x80, (byte)program.Length, 0 }); // no autostart

            var data = new List<byte> { 0xFF };
            data.AddRange(program);

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            byte[] pzxt = { 1, 0 };
            w.Write(Encoding.ASCII.GetBytes("PZXT")); w.Write(pzxt.Length); w.Write(pzxt);
            WriteBlock(w, 8063, WithParity(header), Array.Empty<byte>());
            WriteBlock(w, 3223, WithParity(data), extra);
            return ms.ToArray();
        }

        private static byte[] WithParity(List<byte> bytes)
        {
            byte parity = 0;
            foreach (byte b in bytes) parity ^= b;
            var all = new List<byte>(bytes) { parity };
            return all.ToArray();
        }

        /// <summary>Pilot and sync pulses, then a standard-speed DATA block.</summary>
        private static void WriteBlock(BinaryWriter w, int pilotPulses, byte[] romBytes, byte[] extra)
        {
            w.Write(Encoding.ASCII.GetBytes("PULS")); w.Write(8);
            w.Write((ushort)(0x8000 | pilotPulses)); w.Write((ushort)2168);
            w.Write((ushort)667); w.Write((ushort)735);

            int bytes = romBytes.Length + extra.Length;
            w.Write(Encoding.ASCII.GetBytes("DATA")); w.Write(8 + 8 + bytes);
            w.Write((uint)(bytes * 8));
            w.Write((ushort)945);
            w.Write((byte)2); w.Write((byte)2);
            w.Write((ushort)855); w.Write((ushort)855);
            w.Write((ushort)1710); w.Write((ushort)1710);
            w.Write(romBytes); w.Write(extra);
        }
    }
}
