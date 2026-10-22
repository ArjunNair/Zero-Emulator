using System.IO;
using System.IO.Compression;
using System.Linq;
using Peripherals;
using Xunit;
using static Zero.Core.Tests.TapeBuilders;

namespace Zero.Core.Tests
{
    // Decoding TZX and CSW images into the tape model.
    public class TzxFileTests
    {
        private static TapeImage Load(params byte[][] blocks) => TZXFile.Load(Tzx(blocks));

        private static PulseRun[] Runs(TapeBlock b, int segment = 0) => ((PulseSegment)b.Segments[segment]).Runs;

        [Fact]
        public void Rejects_a_file_that_is_not_tzx()
        {
            Assert.Null(TZXFile.Load(new byte[0]));
            Assert.Null(TZXFile.Load(System.Text.Encoding.ASCII.GetBytes("ZXTape?\x1A\x01\x14")));
            Assert.Null(TZXFile.Load(Tzx())); // no blocks
        }

        [Theory]
        [InlineData(0x00, 8063)]
        [InlineData(0x7F, 8063)]
        [InlineData(0x80, 3223)]
        [InlineData(0xFF, 3223)]
        public void Standard_data_gets_the_rom_s_leader_for_its_flag(byte flag, int pilot)
        {
            var tape = Load(Standard(1000, flag, 0x01, 0x02));
            TapeBlock b = tape.Blocks[0];
            Assert.Equal("Data", b.Kind);
            Assert.Equal(3, b.Segments.Length); // pilot and syncs, data, pause
            Assert.Equal((uint)pilot, Runs(b)[0].Count);
            Assert.Equal(TapFile.LeaderCycles, Runs(b)[0].Duration);
            var data = (DataSegment)b.Segments[1];
            Assert.True(data.IsStandardRom);
            Assert.Equal(24, data.SymbolCount);
            Assert.Equal(1000u * 3500, (uint)b.Segments[2].DurationTStates);
            Assert.Equal(1, tape.InitialLevel);
        }

        [Fact]
        public void Standard_header_is_described_like_pzx()
        {
            byte[] header = new byte[19];
            header[1] = 3; // bytes
            System.Text.Encoding.ASCII.GetBytes("screen    ").CopyTo(header, 2);
            header[12] = 0x00; header[13] = 0x1B; // length 6912
            header[14] = 0x00; header[15] = 0x40; // start 16384
            for (int i = 0; i < 18; i++) header[18] ^= header[i];
            Assert.Equal("Bytes: \"screen    \" CODE 16384,6912", Load(Standard(0, header)).Blocks[0].Info);
        }

        [Fact]
        public void Turbo_and_pure_data_play_only_the_used_bits_of_the_last_byte()
        {
            var tape = Load(Turbo(2000, 600, 700, 800, 1600, 1000, 3, 0, 0xAA, 0xBB),
                            B((byte)0x14, (ushort)500, (ushort)1000, (byte)5, (ushort)0, U24(2), (byte)1, (byte)2));
            Assert.Equal(8 + 3, ((DataSegment)tape.Blocks[0].Segments[1]).SymbolCount);
            Assert.Equal(1000u, Runs(tape.Blocks[0])[0].Count);
            var pure = (DataSegment)tape.Blocks[1].Segments.Single();
            Assert.Equal(8 + 5, pure.SymbolCount);
            Assert.Equal(new ushort[] { 500, 500 }, pure.Symbols[0].Pulses);
        }

        [Fact]
        public void Tone_and_pulse_sequence()
        {
            var tape = Load(PureTone(2168, 3223), B((byte)0x13, (byte)2, (ushort)667, (ushort)735));
            Assert.Equal(new PulseRun(3223, 2168, Edge.Keep, TapePhase.Pilot), Runs(tape.Blocks[0]).Single());
            Assert.Equal(new uint[] { 667, 735 }, Runs(tape.Blocks[1]).Select(r => r.Duration));
            Assert.All(Runs(tape.Blocks[1]), r => Assert.Equal(TapePhase.Sync, r.Phase));
        }

        [Fact]
        public void Direct_recording_runs_of_samples_force_their_level()
        {
            var tape = Load(B((byte)0x15, (ushort)10, (ushort)0, (byte)8, U24(1), (byte)0xF0));
            var runs = Runs(tape.Blocks[0]);
            Assert.Equal(2, runs.Length);
            Assert.Equal((Edge.High, 40u), (runs[0].FirstEdge, runs[0].Duration));
            Assert.Equal((Edge.Low, 40u), (runs[1].FirstEdge, runs[1].Duration));
        }

        [Fact]
        public void Generalized_data_maps_its_symbol_flags_to_the_model()
        {
            byte[] body = B(
                (ushort)0,                         // pause
                1u, (byte)1, (byte)1,              // TOTP, NPP, ASP
                8u, (byte)2, (byte)4,              // TOTD, NPD, ASD (2 bits a symbol)
                (byte)0, (ushort)2168,             // pilot symbol 0
                (byte)0, (ushort)100,              // pilot: symbol 0 x 100
                (byte)0, (ushort)855, (ushort)855, // data symbols: flags 0..3
                (byte)1, (ushort)1710, (ushort)0,  // a zero pulse ends a symbol early
                (byte)2, (ushort)300, (ushort)300,
                (byte)3, (ushort)400, (ushort)400,
                (byte)0x1B, (byte)0xE4);           // 8 symbols of 2 bits: 0 1 2 3 3 2 1 0
            var tape = Load(B((byte)0x19, (uint)body.Length, body));
            TapeBlock b = tape.Blocks[0];
            Assert.Equal(new PulseRun(100, 2168, Edge.Keep, TapePhase.Pilot), Runs(b).Single());
            var data = (DataSegment)b.Segments[1];
            Assert.Equal(2, data.BitsPerSymbol);
            Assert.Equal(new[] { Edge.Keep, Edge.Toggle, Edge.Low, Edge.High }, data.Symbols.Select(s => s.FirstEdge));
            Assert.Equal(new ushort[] { 1710 }, data.Symbols[1].Pulses);
            Assert.Equal(new[] { 0, 1, 2, 3, 3, 2, 1, 0 }, Enumerable.Range(0, 8).Select(i => data.SymbolAt(i)));
        }

        [Fact]
        public void Csw_block_decodes_rle_pulses()
        {
            byte[] rle = { 10, 0, 0x20, 0x4E, 0, 0, 5 }; // 10, 20000, 5 samples
            byte[] body = B((ushort)0, U24(3500000), (byte)1, 3u, rle);
            var tape = Load(B((byte)0x18, (uint)body.Length, body));
            Assert.Equal(new uint[] { 10, 20000, 5 }, Runs(tape.Blocks[0]).Select(r => r.Duration));
            Assert.All(Runs(tape.Blocks[0]), r => Assert.Equal(TapePhase.Samples, r.Phase));
        }

        [Fact]
        public void Control_blocks()
        {
            var tape = Load(
                B((byte)0x20, (ushort)0),
                B((byte)0x20, (ushort)500),
                B((byte)0x23, (short)-1),
                B((byte)0x24, (ushort)5), B((byte)0x25),
                B((byte)0x26, (ushort)1, (short)2), B((byte)0x27),
                B((byte)0x2A, 0u),
                B((byte)0x2B, 1u, (byte)0));
            var b = tape.Blocks;
            Assert.Equal(TapeControlKind.Stop, b[0].Control);
            Assert.Equal(500L * 3500, b[1].DurationTStates);
            Assert.Equal((TapeControlKind.Jump, -1), (b[2].Control, b[2].Offset));
            Assert.Equal((TapeControlKind.LoopStart, 5), (b[3].Control, b[3].Count));
            Assert.Equal(TapeControlKind.LoopEnd, b[4].Control);
            Assert.Equal(new[] { 2 }, b[5].Offsets);
            Assert.Equal(TapeControlKind.Return, b[6].Control);
            Assert.Equal(TapeControlKind.StopIf48K, b[7].Control);
            Assert.Equal((TapeControlKind.SetLevel, 1), (b[8].Control, b[8].Level)); // TZX low is high here
        }

        [Fact]
        public void Info_blocks_are_read_past_whole_so_the_next_block_still_decodes()
        {
            byte[] archive = B((byte)3, (byte)0x00, (byte)4, "Game", (byte)0x02, (byte)9, "Ann\rBob X", (byte)0xFF, (byte)2, "hi");
            byte[] select = B((byte)1, (short)1, (byte)4, "Side");
            var tape = Load(
                B((byte)0x21, (byte)5, "Group"), B((byte)0x22),
                B((byte)0x28, (ushort)select.Length, select),
                B((byte)0x30, (byte)4, "Text"),
                B((byte)0x31, (byte)5, (byte)3, "Msg"),
                B((byte)0x32, (ushort)archive.Length, archive),
                B((byte)0x33, (byte)1, (byte)0, (byte)0, (byte)0),
                B((byte)0x34, new byte[8]),
                B((byte)0x35, "CustomInfo", 2u, (byte)1, (byte)2),
                B((byte)0x40, (byte)0, U24(2), (byte)1, (byte)2),
                B((byte)0x5A, "XTape!", (byte)0x1A, (byte)1, (byte)20),
                B((byte)0x4B, 3u, (byte)1, (byte)2, (byte)3), // unknown: skipped by its length
                PureTone(100, 7));

            Assert.Equal(13, tape.Blocks.Count);
            Assert.Equal(("Group", "Group"), (tape.Blocks[0].Kind, tape.Blocks[0].Info));
            Assert.Equal("Side", tape.Blocks[2].Info);
            Assert.Equal("Text", tape.Blocks[3].Info);
            Assert.Equal("Msg", tape.Blocks[4].Info);
            Assert.Equal("Game", tape.Info.Title);
            Assert.Equal(new[] { "Ann", "Bob X" }, tape.Info.Authors);
            Assert.Equal(new[] { "hi" }, tape.Info.Comments);
            Assert.Equal("CustomInfo", tape.Blocks[8].Info);
            Assert.Equal(7u, Runs(tape.Blocks[12]).Single().Count);
            Assert.All(tape.Blocks.Take(12), x => Assert.False(x.HasSignal));
        }

        [Fact]
        public void A_truncated_file_keeps_the_blocks_before_the_damage()
        {
            byte[] file = Tzx(PureTone(100, 2), Standard(0, 1, 2, 3, 4));
            var tape = TZXFile.Load(file.Take(file.Length - 2).ToArray());
            Assert.Single(tape.Blocks);
        }

        // ------------------------------------------------------------------ CSW files

        private static byte[] CswV1(int polarity, params byte[] rle) =>
            B("Compressed Square Wave", (byte)0x1A, (byte)1, (byte)1, (ushort)44100, (byte)1, (byte)polarity,
              (byte)0, (byte)0, (byte)0, rle);

        [Fact]
        public void Csw_v1_starts_at_its_polarity_and_carries_rounding()
        {
            var tape = CSWFile.Load(CswV1(1, 10, 10, 10));
            var runs = Runs(tape.Blocks.Single());
            Assert.Equal(Edge.High, runs[0].FirstEdge);
            // 10 samples at 44.1 kHz is 793.65 T: rounding carries, so three pulses total 2381
            Assert.Equal(2381, runs.Sum(r => r.TStates));
        }

        [Fact]
        public void Csw_v2_z_rle()
        {
            byte[] rle = { 20, 0, 0x10, 0x27, 0, 0, 20 };
            var packed = new MemoryStream();
            using (var z = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true))
                z.Write(rle);
            byte[] file = B("Compressed Square Wave", (byte)0x1A, (byte)2, (byte)0, 3500000u, 3u, (byte)2, (byte)0,
                            (byte)0, new byte[16], packed.ToArray());
            var runs = Runs(CSWFile.Load(file).Blocks.Single());
            Assert.Equal(Edge.Low, runs[0].FirstEdge);
            Assert.Equal(new[] { 20L, 10000L, 20L }, runs.SelectMany(r => Enumerable.Repeat((long)r.Duration, (int)r.Count)));
        }

        [Fact]
        public void Csw_rejects_other_files()
        {
            Assert.Null(CSWFile.Load(new byte[64]));
            Assert.Null(CSWFile.Load(new byte[0]));
        }
    }
}
