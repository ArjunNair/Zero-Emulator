using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Peripherals
{
    // Compressed Square Wave recordings: .csw files (v1 and v2), and the same pulse data inside a
    // TZX $18 block. Each value is a pulse's length in samples, stored run-length (RLE) or
    // run-length then zlib-deflated (Z-RLE). A pulse is one level held until the next edge.
    public static class CSWFile
    {
        private const string Signature = "Compressed Square Wave\x1A";
        private const double TapeClock = 3500000.0;

        public const int CompressionRle = 1;
        public const int CompressionZRle = 2;

        // A .csw file as a one-block tape, or null if it isn't one.
        public static TapeImage Load(byte[] file) {
            try {
                return LoadCore(file);
            }
            catch (Exception) {
                return null; //truncated or corrupt
            }
        }

        private static TapeImage LoadCore(byte[] file) {
            if (file == null || file.Length < 0x20)
                return null;
            for (int i = 0; i < Signature.Length; i++)
                if (file[i] != Signature[i])
                    return null;

            int major = file[0x17];
            uint rate;
            int compression, flags, dataStart;
            if (major == 1) {
                rate = BitConverter.ToUInt16(file, 0x19);
                compression = file[0x1B];
                flags = file[0x1C];
                dataStart = 0x20;
            } else if (major == 2) {
                if (file.Length < 0x34)
                    return null;
                rate = BitConverter.ToUInt32(file, 0x19);
                compression = file[0x21];
                flags = file[0x22];
                dataStart = 0x34 + file[0x23];
            } else {
                return null;
            }
            if (dataStart > file.Length)
                return null;

            //The first pulse is at the recording's initial polarity; each one after flips it.
            Edge first = (flags & 1) != 0 ? Edge.High : Edge.Low;
            PulseRun[] runs = DecodePulses(file, dataStart, file.Length - dataStart, compression, rate, first, out long pulses);
            if (runs == null)
                return null;

            var tape = new TapeImage();
            tape.Blocks.Add(new TapeBlock {
                Id = 0x18,
                Kind = "CSW Recording",
                Info = pulses.ToString() + " pulses",
                Segments = new TapeSegment[] { new PulseSegment { Runs = runs } }
            });
            return tape;
        }

        // Decode CSW pulse data to runs of T-states (at 3.5 MHz), carrying each pulse's rounding
        // error into the next. The first pulse takes the edge rule first. Null if the compression
        // type is unknown or the rate is zero.
        public static PulseRun[] DecodePulses(byte[] buffer, int offset, int length, int compression, uint rate,
                                              Edge first, out long pulses) {
            pulses = 0;
            if (rate == 0)
                return null;

            byte[] rle;
            if (compression == CompressionRle) {
                rle = new byte[Math.Max(0, Math.Min(length, buffer.Length - offset))];
                Array.Copy(buffer, offset, rle, 0, rle.Length);
            } else if (compression == CompressionZRle) {
                using (var input = new MemoryStream(buffer, offset, Math.Min(length, buffer.Length - offset)))
                using (var z = new ZLibStream(input, CompressionMode.Decompress))
                using (var output = new MemoryStream()) {
                    z.CopyTo(output);
                    rle = output.ToArray();
                }
            } else {
                return null;
            }

            var runs = new List<PulseRun>();
            double perSample = TapeClock / rate;
            double carry = 0;
            int pos = 0;
            while (pos < rle.Length) {
                uint samples = rle[pos++];
                if (samples == 0) {
                    if (pos + 4 > rle.Length)
                        break;
                    samples = BitConverter.ToUInt32(rle, pos);
                    pos += 4;
                }
                if (samples == 0)
                    continue;

                double exact = samples * perSample + carry;
                uint tstates = (uint)Math.Max(1, Math.Round(exact));
                carry = exact - tstates;

                Edge edge = pulses == 0 ? first : Edge.Keep;
                pulses++;
                int last = runs.Count - 1;
                //A run's edge rule is for its first pulse only, so equal pulses after it join it.
                if (edge == Edge.Keep && last >= 0 && runs[last].Duration == tstates) {
                    PulseRun r = runs[last];
                    runs[last] = new PulseRun(r.Count + 1, r.Duration, r.FirstEdge, TapePhase.Samples);
                } else {
                    runs.Add(new PulseRun(1, tstates, edge, TapePhase.Samples));
                }
            }
            return runs.ToArray();
        }
    }
}
