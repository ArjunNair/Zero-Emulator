using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using Zero.Emulation.Tape;

namespace Zero.Core.Tests
{
    public class TapeDeckTests
    {
        /// <summary>A minimal PZX: header with metadata strings, then one pulse block.</summary>
        private static byte[] BuildPzx(params string[] headerStrings)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                var body = new List<byte> { 1, 0 };
                for (int i = 0; i < headerStrings.Length; i++)
                {
                    body.AddRange(Encoding.ASCII.GetBytes(headerStrings[i]));
                    if (i < headerStrings.Length - 1) body.Add(0);
                }
                w.Write(Encoding.ASCII.GetBytes("PZXT")); w.Write(body.Count); w.Write(body.ToArray());
                w.Write(Encoding.ASCII.GetBytes("PULS")); w.Write(4); w.Write((ushort)0x8000 | 8063); w.Write((ushort)2168);
                return ms.ToArray();
            }
        }

        [Fact]
        public void Insert_reads_pzx_header_metadata()
        {
            var deck = new TapeDeck();
            byte[] pzx = BuildPzx("Manic Miner", "Publisher", "Bug-Byte", "Author", "Matthew Smith", "Year", "1983", "Comment", "Original release");

            Assert.True(deck.Insert("test.pzx", pzx));
            Assert.Equal("Manic Miner", deck.Title);
            Assert.Equal("Bug-Byte", deck.Metadata.Publisher);
            Assert.Equal("Matthew Smith", Assert.Single(deck.Metadata.Authors));
            Assert.Equal("1983", deck.Metadata.Year);
            Assert.Equal("Original release", Assert.Single(deck.Metadata.Comments));
            Assert.True(deck.Metadata.HasDetails);
            Assert.Single(deck.Blocks);

            deck.Eject();
            Assert.False(deck.IsInserted);
            Assert.False(deck.Metadata.HasDetails);
            Assert.Equal("", deck.Title);
        }

        [Fact]
        public void Tap_without_header_falls_back_to_file_name()
        {
            var deck = new TapeDeck();
            byte[] tap = File.ReadAllBytes(Path.Combine(Zero.TestSupport.TestPaths.ProgramsDir, "Demos", "Overscan.tap"));
            Assert.True(deck.Insert("Overscan.tap", tap));
            Assert.Equal("Overscan", deck.Title);
            Assert.False(deck.Metadata.HasDetails);
            Assert.Equal(12, deck.Blocks.Count);
        }
    }
}
