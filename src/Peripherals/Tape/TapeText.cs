using System;
using System.Text;

namespace Peripherals
{
    // Block list text shared by every tape format.
    public static class TapeText
    {
        // Describe a block of data: a ROM header's file type and name, or its size in bytes. bits
        // is how many bits of data the block plays. A block the ROM can load (rom) starts with a
        // flag byte and ends with a checksum, which aren't counted.
        public static string DescribeData(byte[] data, long bits, bool rom) {
            long bytes = (bits + 7) / 8;
            if (rom && bytes >= 2)
                bytes -= 2;
            string size = bytes.ToString() + " bytes";
            if (bits / 8 != 19 || data.Length < 19 || data[0] != 0)
                return size;

            //Check checksum to ensure it's a standard header
            byte checksum = 0;
            for (int x = 0; x < 18; x++)
                checksum ^= data[x];
            if (checksum != data[18])
                return size;

            string name = Latin(data, 2, 10);
            switch (data[1]) {
                case 0: {
                    string info = "Program: \"" + name + "\"";
                    //32768 and up means no autostart; any line below, 0 included, runs
                    ushort line = BitConverter.ToUInt16(data, 14);
                    if (line < 32768)
                        info += " LINE " + line.ToString();
                    return info;
                }
                case 1:
                    return "Num Array: \"" + name + "\"" + "  " + Convert.ToChar(data[15] - 32) + "(" + data[12].ToString() + ")";
                case 2:
                    return "Char Array: \"" + name + "\"" + "  " + Convert.ToChar(data[15] - 96) + "$(" + data[12].ToString() + ")";
                case 3: {
                    ushort start = BitConverter.ToUInt16(data, 14);
                    ushort length = BitConverter.ToUInt16(data, 12);
                    return "Bytes: \"" + name + "\"" + " CODE " + start.ToString() + "," + length.ToString();
                }
                default:
                    return size;
            }
        }

        // A pause's length in milliseconds.
        public static string DescribePause(long tstates) =>
            Math.Round(tstates / 3500.0).ToString() + " ms";

        // A tone: so many pulses of one length.
        public static string DescribeTone(long count, long tstates) =>
            count.ToString() + " pulses of " + tstates.ToString() + " T";

        // Pulses of different lengths: how many there are, or the tone if they're all the same.
        public static string DescribePulses(PulseRun[] runs) {
            if (runs.Length == 1)
                return DescribeTone(runs[0].Count, runs[0].Duration);
            long count = 0;
            foreach (PulseRun r in runs) count += r.Count;
            return count.ToString() + " pulses";
        }

        // Bytes as one character each, as the Spectrum's file names are stored.
        public static string Latin(byte[] data, int from, int length) {
            var s = new StringBuilder();
            for (int f = from; f < from + length && f < data.Length; f++)
                s.Append((char)data[f]);
            return s.ToString();
        }
    }
}
