using System;
using System.IO;

namespace Peripherals
{
    /// <summary>
    /// Converts a .TAP image to an in-memory PZX 1.0 stream using standard ROM loader timings.
    /// Managed replacement for the native tap2pzx (pzx_tools.dll); the output is byte-compatible
    /// with Patrik Rak's tap2pzx so <see cref="PZXFile.LoadPZX"/> consumes it unchanged.
    ///
    /// TAP layout: repeated [u16 length][length bytes], where the first byte of each block is the
    /// flag (0x00 header / 0xFF data) and the last is the XOR checksum. Both are part of the data
    /// stream that gets encoded, so no special handling is needed here.
    /// </summary>
    public static class TapFile
    {
        // Standard ROM loader timings, in T-states.
        public const ushort LeaderCycles = 2168;
        public const ushort ShortLeaderCount = 3223; // data blocks (flag >= 128)
        public const ushort LongLeaderCount = 8063;  // header blocks (flag < 128)
        public const ushort Sync1Cycles = 667;
        public const ushort Sync2Cycles = 735;
        public const ushort Bit0Cycles = 855;
        public const ushort Bit1Cycles = 1710;
        public const ushort TailCycles = 945;
        public const uint MillisecondCycles = 3500;

        private const uint TagPZXT = 0x54585A50; // "PZXT" little-endian
        private const uint TagPULS = 0x534C5550; // "PULS"
        private const uint TagDATA = 0x41544144; // "DATA"
        private const uint TagPAUS = 0x53554150; // "PAUS"

        /// <summary>
        /// Convert TAP bytes to PZX bytes.
        /// </summary>
        /// <param name="tap">Raw .tap file contents.</param>
        /// <param name="pauseMilliseconds">Silence inserted after every block (tap2pzx -p). 0 disables.</param>
        /// <returns>PZX image, or null if the input is not a well-formed TAP.</returns>
        public static byte[] ToPZX(byte[] tap, uint pauseMilliseconds = 500)
        {
            if (tap == null || tap.Length < 2)
                return null;

            using (var ms = new MemoryStream(tap.Length * 2 + 64))
            using (var w = new BinaryWriter(ms))
            {
                // PZXT header: version only, no metadata strings.
                WriteBlockHeader(w, TagPZXT, 2);
                w.Write((byte)1);
                w.Write((byte)0);

                int pos = 0;
                int blocksWritten = 0;
                while (pos + 2 <= tap.Length)
                {
                    int size = tap[pos] | (tap[pos + 1] << 8);
                    pos += 2;

                    if (size == 0)
                        continue;

                    if (pos + size > tap.Length)
                        return null; // truncated block

                    byte flag = tap[pos];

                    // PULS: leader + two sync pulses.
                    ushort leaderCount = flag < 128 ? LongLeaderCount : ShortLeaderCount;
                    WriteBlockHeader(w, TagPULS, 2 + 2 + 2 + 2);
                    w.Write((ushort)(0x8000 | leaderCount));
                    w.Write(LeaderCycles);
                    w.Write(Sync1Cycles);
                    w.Write(Sync2Cycles);

                    // DATA: initial level high, 2 pulses per bit, standard bit cells, tail pulse.
                    uint bitCount = (uint)size * 8;
                    uint dataBlockSize = 4 + 2 + 1 + 1 + 4 + 4 + (uint)size;
                    WriteBlockHeader(w, TagDATA, dataBlockSize);
                    w.Write(0x80000000u | bitCount);
                    w.Write(TailCycles);
                    w.Write((byte)2);
                    w.Write((byte)2);
                    w.Write(Bit0Cycles); w.Write(Bit0Cycles);
                    w.Write(Bit1Cycles); w.Write(Bit1Cycles);
                    w.Write(tap, pos, size);

                    if (pauseMilliseconds > 0)
                    {
                        uint duration = pauseMilliseconds * MillisecondCycles;
                        WriteBlockHeader(w, TagPAUS, 4);
                        w.Write(duration & 0x7FFFFFFFu); // level low
                    }

                    pos += size;
                    blocksWritten++;
                }

                if (blocksWritten == 0)
                    return null;

                w.Flush();
                return ms.ToArray();
            }
        }

        private static void WriteBlockHeader(BinaryWriter w, uint tag, uint size)
        {
            w.Write(tag);
            w.Write(size);
        }
    }
}
