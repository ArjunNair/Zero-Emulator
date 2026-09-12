using System.IO;
using System.Linq;
using Peripherals;
using Xunit;

namespace Zero.Core.Tests
{
    public class TapFileTests
    {
        private static int CountTapBlocks(byte[] tap)
        {
            int pos = 0, n = 0;
            while (pos + 2 <= tap.Length)
            {
                int size = tap[pos] | (tap[pos + 1] << 8);
                pos += 2 + size;
                if (size > 0) n++;
            }
            return n;
        }

        [Fact]
        public void Converts_sample_tap_to_loadable_pzx()
        {
            byte[] tap = File.ReadAllBytes(Path.Combine(TestPaths.ProgramsDir, "Demos", "Overscan.tap"));
            byte[] pzx = TapFile.ToPZX(tap, 500);

            Assert.NotNull(pzx);
            Assert.Equal("PZXT", System.Text.Encoding.ASCII.GetString(pzx, 0, 4));
            Assert.True(PZXFile.LoadPZX(ref pzx));

            // header + (PULS + DATA + PAUS) per TAP block
            int blocks = CountTapBlocks(tap);
            Assert.Equal(1 + 3 * blocks, PZXFile.blocks.Count);
        }

        [Fact]
        public void Header_block_uses_long_leader_and_data_block_short_leader()
        {
            // one header block (flag 0x00) and one data block (flag 0xFF), 1 payload byte each
            byte[] tap = { 3, 0, 0x00, 0xAA, 0xAA, 3, 0, 0xFF, 0x55, 0xAA };
            byte[] pzx = TapFile.ToPZX(tap, 0);
            Assert.True(PZXFile.LoadPZX(ref pzx));

            var puls = PZXFile.blocks.OfType<PZXFile.PULS_Block>().ToList();
            Assert.Equal(2, puls.Count);
            Assert.Equal(TapFile.LongLeaderCount, puls[0].pulse[0].count);
            Assert.Equal(TapFile.LeaderCycles, (ushort)puls[0].pulse[0].duration);
            Assert.Equal(TapFile.ShortLeaderCount, puls[1].pulse[0].count);

            var data = PZXFile.blocks.OfType<PZXFile.DATA_Block>().ToList();
            Assert.Equal(2, data.Count);
            Assert.Equal(24u, data[0].count);
            Assert.Equal(1u, data[0].initialPulseLevel);
            Assert.Equal(new byte[] { 0x00, 0xAA, 0xAA }, data[0].data.ToArray());
            Assert.Empty(PZXFile.blocks.OfType<PZXFile.PAUS_Block>());
        }

        [Theory]
        [InlineData(new byte[0])]
        [InlineData(new byte[] { 5 })]
        [InlineData(new byte[] { 10, 0, 1, 2 })] // truncated block
        public void Rejects_malformed_input(byte[] tap)
        {
            Assert.Null(TapFile.ToPZX(tap));
        }
    }
}
