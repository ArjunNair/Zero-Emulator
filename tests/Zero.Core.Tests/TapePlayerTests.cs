using System.Collections.Generic;
using System.Linq;
using Peripherals;
using Xunit;
using static Zero.Core.Tests.TapeBuilders;

namespace Zero.Core.Tests
{
    /// <summary>The tape state machine, played on its own: when each edge comes, and where it goes next.</summary>
    public class TapePlayerTests
    {
        private static readonly ushort[] S0 = { 100, 100 };
        private static readonly ushort[] S1 = { 200, 200 };

        private static TapePlayer Pzx(params byte[][] blocks) => new TapePlayer(PZXFile.Load(TapeBuilders.Pzx(blocks)));
        private static TapePlayer Tzx(params byte[][] blocks) => new TapePlayer(TZXFile.Load(TapeBuilders.Tzx(blocks)));

        // ------------------------------------------------------------------ PZX

        [Fact]
        public void Puls_starts_low_and_flips_at_the_end_of_each_pulse()
        {
            var p = Pzx(Puls((3, 100), (1, 50)));
            var edges = Edges(p, out TapeEdge seen);
            Assert.Equal(new[] { (100L, 1), (200L, 0), (300L, 1), (350L, 0) }, edges);
            Assert.True(seen.HasFlag(TapeEdge.EndOfTape));
            Assert.False(p.IsActive);
        }

        [Fact]
        public void Zero_length_pulses_flip_without_taking_time()
        {
            var p = Pzx(Puls((3, 0), (1, 100)));
            var edges = Edges(p, out _);
            Assert.Equal(new[] { (100L, 0) }, edges); // three flips leave it high, then one pulse
        }

        [Fact]
        public void Long_pulses_keep_their_full_duration()
        {
            var edges = Edges(Pzx(Puls((1, 100000))), out _);
            Assert.Equal(new[] { (100000L, 1) }, edges);
        }

        [Fact]
        public void Data_starts_at_its_level_and_plays_its_tail()
        {
            var edges = Edges(Pzx(Data(1, 2, 50, S0, S1, 0x80)), out _);
            // a 1 (two 200s), a 0 (two 100s), then the 50 T tail
            Assert.Equal(new[] { (200L, 0), (400L, 1), (500L, 0), (600L, 1), (650L, 0) }, edges);
        }

        [Fact]
        public void The_last_data_on_the_tape_without_a_tail_gets_a_closing_edge()
        {
            var edges = Edges(Pzx(Data(1, 1, 0, S0, S1, 0x00)), out _);
            Assert.Equal(new[] { (100L, 0), (200L, 1), (7200L, 0) }, edges);
        }

        [Fact]
        public void Pause_holds_its_level_and_a_zero_pause_only_sets_it()
        {
            Assert.Equal(new[] { (1000L, 0), (1100L, 1) }, Edges(Pzx(Paus(1, 1000), Puls((1, 100))), out _));
            Assert.Equal(new[] { (100L, 1) }, Edges(Pzx(Paus(1, 0), Puls((1, 100))), out _));
        }

        [Fact]
        public void Stop_for_48K_stops_only_a_48K_machine()
        {
            var p = Pzx(Puls((1, 100)), Stop(1), Puls((1, 100)));
            Assert.Equal(200L, Edges(p, out TapeEdge seen).Last().time);
            Assert.False(seen.HasFlag(TapeEdge.Stop));

            p = Pzx(Puls((1, 100)), Stop(1), Puls((1, 100)));
            p.Is48K = true;
            Edges(p, out seen);
            Assert.True(seen.HasFlag(TapeEdge.Stop));
            Assert.Equal(2, p.BlockIndex); // parked on the stop block
            p.Begin();
            Assert.Equal(3, p.BlockIndex); // and Play goes on past it
        }

        // ------------------------------------------------------------------ TZX

        [Fact]
        public void Turbo_data_plays_pilot_syncs_bits_and_a_pause_by_the_tzx_rule()
        {
            var p = Tzx(Turbo(100, 30, 40, 50, 60, 2, 1, 2, 0x80));
            var edges = Edges(p, out _);
            // TZX starts low and every pulse begins with an edge, so the first pulse is high. One
            // used bit (a 1, two 60s), then 1 ms at the opposite level of the last pulse and low.
            Assert.Equal(new[] {
                (100L, 0), (200L, 1), (230L, 0), (270L, 1), (330L, 0), (390L, 1), (3890L, 0), (7390L, 1)
            }, edges);
        }

        [Fact]
        public void Loop_plays_its_blocks_the_number_of_times_it_says()
        {
            var p = Tzx(B((byte)0x24, (ushort)3), PureTone(100, 1), B((byte)0x25));
            Assert.Equal(new long[] { 100, 100, 100 }, Intervals(Edges(p, out _)));
        }

        [Fact]
        public void Jump_skips_to_the_block_it_names()
        {
            var p = Tzx(B((byte)0x23, (short)2), PureTone(100, 5), PureTone(50, 1));
            Assert.Equal(new long[] { 50 }, Intervals(Edges(p, out _)));
        }

        [Fact]
        public void Call_sequence_plays_each_call_then_carries_on_after_it()
        {
            var p = Tzx(
                B((byte)0x26, (ushort)2, (short)2, (short)4), // 0: call 2, then 4
                PureTone(1000, 1),                            // 1: after the calls
                PureTone(100, 1), B((byte)0x27),              // 2, 3
                PureTone(200, 1), B((byte)0x27));             // 4, 5
            // the calls, then on from block 1, through 2 to 5 again with nothing to return to
            Assert.Equal(new long[] { 100, 200, 1000, 100, 200 }, Intervals(Edges(p, out _)));
        }

        [Theory]
        [InlineData(true, 1)]
        [InlineData(false, 2)]
        public void Stop_if_48K_block(bool is48K, int pulses)
        {
            var p = Tzx(PureTone(100, 1), B((byte)0x2A, 0u), PureTone(100, 1));
            p.Is48K = is48K;
            Assert.Equal(pulses, Edges(p, out _).Count);
        }

        [Fact]
        public void Zero_pause_stops_the_tape_and_play_carries_on_after_it()
        {
            var p = Tzx(PureTone(100, 1), B((byte)0x20, (ushort)0), PureTone(100, 1));
            Edges(p, out TapeEdge seen);
            Assert.True(seen.HasFlag(TapeEdge.Stop));
            Assert.Equal(1, p.BlockIndex);
            Assert.Single(Edges(p, out seen));
            Assert.True(seen.HasFlag(TapeEdge.EndOfTape));
        }

        [Fact]
        public void Set_level_is_the_level_before_the_next_pulse_s_edge()
        {
            // TZX: the level is set high, so the first pulse's edge takes it low.
            var p = Tzx(B((byte)0x2B, 1u, (byte)1), PureTone(100, 2));
            var edges = Edges(p, out _);
            Assert.Equal(new[] { (100L, 1), (200L, 0) }, edges);
        }

        [Fact]
        public void Stopping_in_data_starts_it_again_from_its_pilot()
        {
            var p = Tzx(Turbo(100, 30, 40, 50, 60, 4, 8, 0, 0xFF, 0xFF));
            p.Begin();
            while (p.Phase != TapePhase.Data)
            {
                p.AddTime(p.TStatesToNextEdge);
                p.NextEdge();
            }
            p.Stop();
            p.Begin();
            Assert.Equal(TapePhase.Pilot, p.Phase);
            Assert.Equal(0, p.BlockIndex);
        }

        // ------------------------------------------------------------------ the same edges, either format

        [Fact]
        public void A_tzx_standard_block_and_the_same_tap_as_pzx_play_the_same_edges()
        {
            byte[] block = { 0xFF, 0x12, 0x34, 0xA5, 0x00 };
            byte[] tap = new byte[block.Length + 2];
            tap[0] = (byte)block.Length;
            block.CopyTo(tap, 2);

            var pzx = Intervals(Edges(new TapePlayer(PZXFile.Load(TapFile.ToPZX(tap, 0))), out _));
            var tzx = Intervals(Edges(Tzx(Standard(0, block)), out _));

            // Pilot, two syncs, two pulses a bit: up to there the formats agree. After it, the PZX
            // has its tail pulse and the TZX its closing edge.
            int signal = TapFile.ShortLeaderCount + 2 + block.Length * 8 * 2;
            Assert.Equal(pzx.Take(signal), tzx.Take(signal));
        }

        // ------------------------------------------------------------------ loader acceleration

        [Fact]
        public void TryReadByte_plays_a_byte_at_once()
        {
            var p = Pzx(Data(1, 16, 0, S0, S1, 0xA5, 0x3C));
            p.Begin();
            Assert.True(p.TryReadByte(out byte b, out long t, out _));
            Assert.Equal(0xA5, b);
            Assert.Equal(4 * 400 + 4 * 200, t);
            Assert.Equal(8, p.SymbolIndex);
            Assert.True(p.TryReadByte(out b, out _, out _));
            Assert.Equal(0x3C, b);
        }

        [Fact]
        public void SkipPhase_plays_a_pilot_through()
        {
            var p = Tzx(Turbo(100, 30, 40, 50, 60, 6, 8, 0, 0x00));
            p.Begin();
            Assert.Equal(TapePhase.Pilot, p.Phase);
            Assert.Equal(600, p.SkipPhase(out _));
            Assert.Equal(TapePhase.Sync, p.Phase);
        }

        [Fact]
        public void SeekData_passes_pauses_and_pilots_but_not_other_data()
        {
            // header and data: PZXT, then PULS DATA PAUS twice
            byte[] tap = { 3, 0, 0x00, 0xAA, 0xAA, 3, 0, 0xFF, 0x55, 0xAA };
            var p = new TapePlayer(PZXFile.Load(TapFile.ToPZX(tap, 500)));
            Assert.NotNull(p.SeekData(d => d.IsStandardRom));
            Assert.Equal(2, p.BlockIndex);
            p.EndFlashLoad(-1, false, out _);
            Assert.NotNull(p.SeekData(d => d.IsStandardRom)); // past the PAUS and the next PULS
            Assert.Equal(5, p.BlockIndex);

            // Turbo data in the way: flash loading must not skip it to reach later data.
            var t = Tzx(Turbo(100, 30, 40, 1710, 855, 4, 8, 0, 0x00), Standard(0, 0xFF));
            Assert.Null(t.SeekData(d => d.IsStandardRom));
            Assert.Equal(0, t.BlockIndex);
        }

        [Fact]
        public void SeekData_finds_the_data_inside_a_tzx_block_and_parks_after_it()
        {
            var p = Tzx(Standard(1000, 0x00, 0x01), Standard(1000, 0xFF, 0x02));
            DataSegment d = p.SeekData(x => x.IsStandardRom);
            Assert.Equal(new byte[] { 0x00, 0x01 }, d.Data);
            p.EndFlashLoad(-1, false, out _);
            d = p.SeekData(x => x.IsStandardRom); // past the first block's pause
            Assert.Equal(new byte[] { 0xFF, 0x02 }, d.Data);
            Assert.Equal(1, p.BlockIndex);
        }
    }
}
