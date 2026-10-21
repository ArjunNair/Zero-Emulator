using System;
using System.Collections.Generic;

namespace Peripherals
{
    /// <summary>
    /// TZX 1.20 images, decoded to a <see cref="TapeImage"/> for <see cref="TapePlayer"/>.
    /// Adapted from the rustzx TZX player (Arjun Nair), as a decoder rather than a streaming player.
    ///
    /// TZX puts an edge at the start of every pulse and keeps a "current level", the level of the
    /// last pulse. The tape model flips at the end of every pulse instead, so its level is always
    /// the opposite of TZX's current level: the same edges, one pulse early. So here:
    /// <list type="bullet">
    /// <item>a tape starts with TZX's level low, which is high in the model;</item>
    /// <item>an ordinary pulse needs no edge rule (the flip already happened);</item>
    /// <item>$2B "set level to L" sets the model's level to !L;</item>
    /// <item>$19's symbol flags 0 (edge) and 1 (no edge) become Keep and Toggle.</item>
    /// </list>
    /// Blocks are read in full; a truncated file keeps the blocks before the damage.
    /// </summary>
    public static class TZXFile
    {
        private const uint Millisecond = TapFile.MillisecondCycles;

        /// <summary>A .tzx file as a tape, or null if it isn't one.</summary>
        public static TapeImage Load(byte[] file) {
            try {
                return LoadCore(file);
            }
            catch (Exception) {
                return null;
            }
        }

        private sealed class Truncated : Exception { }

        // Reads little-endian values, throwing Truncated at the end of what it may read.
        private sealed class Reader
        {
            public readonly byte[] Buf;
            public int Pos;
            public int End;

            public Reader(byte[] buf, int pos, int end) {
                Buf = buf;
                Pos = pos;
                End = end;
            }

            private void Need(long n) {
                if (n < 0 || Pos + n > End) throw new Truncated();
            }

            public byte U8() { Need(1); return Buf[Pos++]; }
            public ushort U16() { Need(2); ushort v = BitConverter.ToUInt16(Buf, Pos); Pos += 2; return v; }
            public short S16() => (short)U16();
            public uint U24() { Need(3); uint v = (uint)(Buf[Pos] | Buf[Pos + 1] << 8 | Buf[Pos + 2] << 16); Pos += 3; return v; }
            public uint U32() { Need(4); uint v = BitConverter.ToUInt32(Buf, Pos); Pos += 4; return v; }

            public byte[] Bytes(long n) {
                Need(n);
                byte[] b = new byte[n];
                Array.Copy(Buf, Pos, b, 0, n);
                Pos += (int)n;
                return b;
            }

            public string Text(long n) => Clean(TapeText.Latin(Bytes(n), 0, (int)n));

            public void Skip(long n) { Need(n); Pos += (int)n; }
        }

        private static string Clean(string s) => s.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();

        private static TapeImage LoadCore(byte[] file) {
            const string Signature = "ZXTape!\x1A";
            if (file == null || file.Length < 10)
                return null;
            for (int i = 0; i < Signature.Length; i++)
                if (file[i] != Signature[i])
                    return null;

            var tape = new TapeImage { InitialLevel = 1 };
            var r = new Reader(file, 10, file.Length);
            while (r.Pos < r.End) {
                int start = r.Pos;
                try {
                    TapeBlock block = ReadBlock(r, tape, tape.Blocks.Count);
                    tape.Blocks.Add(block);
                }
                catch (Truncated) {
                    r.Pos = start;
                    break; //keep the blocks before the damage
                }
            }
            return tape.Blocks.Count > 0 ? tape : null;
        }

        private static TapeBlock ReadBlock(Reader r, TapeImage tape, int index) {
            byte id = r.U8();
            var b = new TapeBlock();
            switch (id) {
                case 0x10: { // standard speed data
                    uint pause = r.U16();
                    int length = r.U16();
                    byte[] data = r.Bytes(length);
                    //Headers (flag below 128) get the long leader, as the ROM saves them.
                    uint pilot = length > 0 && data[0] < 128 ? TapFile.LongLeaderCount : TapFile.ShortLeaderCount;
                    b.Kind = "Data";
                    b.Segments = DataBlock(TapFile.LeaderCycles, pilot, TapFile.Sync1Cycles, TapFile.Sync2Cycles,
                                           TapFile.Bit0Cycles, TapFile.Bit1Cycles, data, (long)length * 8, pause);
                    b.Info = TapeText.DescribeData(data, (long)length * 8, true);
                    break;
                }

                case 0x11: { // turbo speed data
                    ushort pilotLength = r.U16(), sync1 = r.U16(), sync2 = r.U16(), zero = r.U16(), one = r.U16();
                    ushort pilotPulses = r.U16();
                    int usedBits = r.U8();
                    uint pause = r.U16();
                    uint length = r.U24();
                    byte[] data = r.Bytes(length);
                    long bits = Bits(length, usedBits);
                    b.Kind = "Turbo Data";
                    b.Segments = DataBlock(pilotLength, pilotPulses, sync1, sync2, zero, one, data, bits, pause);
                    b.Info = TapeText.DescribeData(data, bits, ((DataSegment)b.Segments[1]).IsStandardRom);
                    break;
                }

                case 0x12: { // pure tone
                    ushort length = r.U16(), count = r.U16();
                    b.Kind = "Pure Tone";
                    b.Segments = Pulses(new PulseRun(count, length, Edge.Keep, count > 1 ? TapePhase.Pilot : TapePhase.Sync));
                    b.Info = TapeText.DescribeTone(count, length);
                    break;
                }

                case 0x13: { // pulse sequence
                    int n = r.U8();
                    var runs = new PulseRun[n];
                    for (int i = 0; i < n; i++)
                        runs[i] = new PulseRun(1, r.U16(), Edge.Keep, TapePhase.Sync);
                    b.Kind = "Pulse Sequence";
                    b.Segments = Pulses(runs);
                    b.Info = n.ToString() + " pulses";
                    break;
                }

                case 0x14: { // pure data
                    ushort zero = r.U16(), one = r.U16();
                    int usedBits = r.U8();
                    uint pause = r.U16();
                    uint length = r.U24();
                    byte[] data = r.Bytes(length);
                    long bits = Bits(length, usedBits);
                    b.Kind = "Pure Data";
                    DataSegment pure = Binary(data, bits, zero, one);
                    var segments = new List<TapeSegment> { pure };
                    AddPause(segments, pause);
                    b.Segments = segments.ToArray();
                    b.Info = TapeText.DescribeData(data, bits, pure.IsStandardRom);
                    break;
                }

                case 0x15: { // direct recording
                    ushort perSample = r.U16();
                    uint pause = r.U16();
                    int usedBits = r.U8();
                    uint length = r.U24();
                    byte[] data = r.Bytes(length);
                    long samples = Bits(length, usedBits);
                    b.Kind = "Direct Recording";
                    var segments = new List<TapeSegment> { Samples(data, samples, perSample) };
                    AddPause(segments, pause);
                    b.Segments = segments.ToArray();
                    b.Info = samples.ToString() + " samples";
                    break;
                }

                case 0x16: // C64 ROM type data (deprecated)
                case 0x17: // C64 turbo data (deprecated)
                    r.Skip(r.U32());
                    b.Kind = id == 0x16 ? "C64 ROM Data" : "C64 Turbo Data";
                    b.Info = "Not played";
                    break;

                case 0x18: { // CSW recording
                    uint length = r.U32();
                    int end = r.Pos + (int)length;
                    if (length < 10 || end > r.End) throw new Truncated();
                    uint pause = r.U16();
                    uint rate = r.U24();
                    int compression = r.U8();
                    r.U32(); // stored pulse count: the data says it anyway
                    PulseRun[] runs = CSWFile.DecodePulses(r.Buf, r.Pos, end - r.Pos, compression, rate, Edge.Keep, out long pulses);
                    r.Pos = end;
                    b.Kind = "CSW Recording";
                    var segments = new List<TapeSegment>();
                    if (runs != null) segments.Add(new PulseSegment { Runs = runs });
                    AddPause(segments, pause);
                    b.Segments = segments.ToArray();
                    b.Info = runs == null ? "Unknown compression" : pulses.ToString() + " pulses";
                    break;
                }

                case 0x19: { // generalized data
                    uint length = r.U32();
                    int end = r.Pos + (int)length;
                    if (end > r.End) throw new Truncated();
                    var inner = new Reader(r.Buf, r.Pos, end);
                    b.Kind = "Generalized Data";
                    b.Segments = GeneralizedData(inner, out string info);
                    b.Info = info;
                    r.Pos = end;
                    break;
                }

                case 0x20: { // pause, or stop the tape
                    uint pause = r.U16();
                    if (pause == 0) {
                        b.Kind = "Stop the Tape";
                        b.Control = TapeControlKind.Stop;
                    } else {
                        b.Kind = "Pause";
                        var segments = new List<TapeSegment>();
                        AddPause(segments, pause);
                        b.Segments = segments.ToArray();
                        b.Info = TapeText.DescribePause(pause * Millisecond);
                    }
                    break;
                }

                case 0x21: // group start
                    b.Kind = "Group";
                    b.Info = r.Text(r.U8());
                    break;

                case 0x22:
                    b.Kind = "Group End";
                    break;

                case 0x23: { // jump to block
                    short offset = r.S16();
                    b.Kind = "Jump";
                    b.Control = TapeControlKind.Jump;
                    b.Offset = offset;
                    b.Info = "To block " + (index + offset + 1).ToString();
                    break;
                }

                case 0x24: { // loop start
                    ushort count = r.U16();
                    b.Kind = "Loop Start";
                    b.Control = TapeControlKind.LoopStart;
                    b.Count = count;
                    b.Info = count.ToString() + " times";
                    break;
                }

                case 0x25:
                    b.Kind = "Loop End";
                    b.Control = TapeControlKind.LoopEnd;
                    break;

                case 0x26: { // call sequence
                    int n = r.U16();
                    var offsets = new int[n];
                    for (int i = 0; i < n; i++) offsets[i] = r.S16();
                    b.Kind = "Call Sequence";
                    b.Control = TapeControlKind.Call;
                    b.Offsets = offsets;
                    b.Info = n.ToString() + " calls";
                    break;
                }

                case 0x27:
                    b.Kind = "Return";
                    b.Control = TapeControlKind.Return;
                    break;

                case 0x28: { // select block: shown, but the tape plays straight through
                    int length = r.U16();
                    var inner = new Reader(r.Buf, r.Pos, r.Pos + length);
                    r.Skip(length);
                    var choices = new List<string>();
                    int n = inner.U8();
                    for (int i = 0; i < n; i++) {
                        inner.S16();
                        choices.Add(inner.Text(inner.U8()));
                    }
                    b.Kind = "Select Block";
                    b.Info = string.Join(" / ", choices);
                    break;
                }

                case 0x2A: // stop the tape if in 48K mode
                    r.Skip(r.U32());
                    b.Kind = "Stop If 48K";
                    b.Control = TapeControlKind.StopIf48K;
                    break;

                case 0x2B: { // set signal level
                    uint length = r.U32();
                    if (length < 1) throw new Truncated();
                    int level = r.U8();
                    r.Skip(length - 1);
                    b.Kind = "Set Level";
                    b.Control = TapeControlKind.SetLevel;
                    b.Level = level == 0 ? 1 : 0; // the model's level is the opposite of TZX's
                    b.Info = level == 0 ? "Low" : "High";
                    break;
                }

                case 0x30: // text description
                    b.Kind = "Text";
                    b.Info = r.Text(r.U8());
                    break;

                case 0x31: // message
                    r.U8(); // seconds to show it for
                    b.Kind = "Message";
                    b.Info = r.Text(r.U8());
                    break;

                case 0x32: { // archive info
                    int length = r.U16();
                    var inner = new Reader(r.Buf, r.Pos, r.Pos + length);
                    r.Skip(length);
                    tape.Info ??= new TapeInfo();
                    b.Kind = "Archive Info";
                    ReadArchiveInfo(inner, tape.Info);
                    break;
                }

                case 0x33: { // hardware type
                    int n = r.U8();
                    r.Skip(n * 3);
                    b.Kind = "Hardware Type";
                    break;
                }

                case 0x34: // emulation info (deprecated)
                    r.Skip(8);
                    b.Kind = "Emulation Info";
                    break;

                case 0x35: { // custom info
                    string name = r.Text(10);
                    r.Skip(r.U32());
                    b.Kind = "Custom Info";
                    b.Info = name;
                    break;
                }

                case 0x40: // snapshot (deprecated)
                    r.U8();
                    r.Skip(r.U24());
                    b.Kind = "Snapshot";
                    b.Info = "Not loaded";
                    break;

                case 0x5A: // glue: another TZX file's header, where two were joined
                    r.Skip(9);
                    b.Kind = "Glue";
                    break;

                default:
                    //From 1.10 every new block starts with its length, so unknown ones can be skipped.
                    r.Skip(r.U32());
                    b.Kind = "Unknown $" + id.ToString("X2");
                    break;
            }
            return b;
        }

        // ------------------------------------------------------------------ building segments

        // Data bits played: all of every byte but the last, which plays only usedBits (1-8).
        private static long Bits(uint length, int usedBits) {
            if (length == 0) return 0;
            if (usedBits < 1 || usedBits > 8) usedBits = 8;
            return (long)(length - 1) * 8 + usedBits;
        }

        private static TapeSegment[] Pulses(params PulseRun[] runs) =>
            new TapeSegment[] { new PulseSegment { Runs = runs } };

        private static DataSegment Binary(byte[] data, long bits, ushort zero, ushort one) =>
            new DataSegment {
                Data = data,
                SymbolCount = bits,
                Symbols = new[] { new Symbol(Edge.Keep, zero, zero), new Symbol(Edge.Keep, one, one) }
            };

        // Pilot, two syncs, data, pause: blocks $10 and $11.
        private static TapeSegment[] DataBlock(ushort pilotLength, uint pilotPulses, ushort sync1, ushort sync2,
                                               ushort zero, ushort one, byte[] data, long bits, uint pause) {
            var segments = new List<TapeSegment> {
                new PulseSegment {
                    Runs = new[] {
                        new PulseRun(pilotPulses, pilotLength, Edge.Keep, TapePhase.Pilot),
                        new PulseRun(1, sync1, Edge.Keep, TapePhase.Sync),
                        new PulseRun(1, sync2, Edge.Keep, TapePhase.Sync)
                    }
                },
                Binary(data, bits, zero, one)
            };
            AddPause(segments, pause);
            return segments.ToArray();
        }

        // A pause by TZX's rule: the last edge is finished with 1 ms at the opposite level of the
        // last pulse, then the level goes low for the rest. In the model's terms the last pulse's
        // flip already gave the opposite level, so: hold 1 ms, force low, hold.
        private static void AddPause(List<TapeSegment> segments, uint milliseconds) {
            if (milliseconds == 0)
                return;
            uint total = milliseconds * Millisecond;
            segments.Add(new PulseSegment {
                Runs = new[] {
                    new PulseRun(1, Millisecond, Edge.Keep, TapePhase.Pause),
                    new PulseRun(1, total - Millisecond, Edge.Low, TapePhase.Pause)
                }
            });
        }

        // $15: each sample is the level for perSample T-states. A run of equal samples is one pulse
        // that forces its level, so the flip after it doesn't matter to the next.
        private static PulseSegment Samples(byte[] data, long samples, ushort perSample) {
            var runs = new List<PulseRun>();
            long i = 0;
            while (i < samples) {
                int level = (data[i >> 3] >> (7 - (int)(i & 7))) & 1;
                long n = 1;
                while (i + n < samples && ((data[(i + n) >> 3] >> (7 - (int)((i + n) & 7))) & 1) == level)
                    n++;
                long t = n * perSample;
                Edge edge = level != 0 ? Edge.High : Edge.Low;
                while (t > 0) {
                    uint part = (uint)Math.Min(t, uint.MaxValue);
                    runs.Add(new PulseRun(1, part, edge, TapePhase.Samples));
                    t -= part;
                }
                i += n;
            }
            return new PulseSegment { Runs = runs.ToArray() };
        }

        // $19: a pilot made of symbols repeated, then data made of symbols, then a pause.
        private static TapeSegment[] GeneralizedData(Reader r, out string info) {
            uint pause = r.U16();
            uint pilotCount = r.U32();
            int pilotPulses = r.U8();
            int pilotSymbols = r.U8();
            if (pilotSymbols == 0) pilotSymbols = 256;
            uint dataCount = r.U32();
            int dataPulses = r.U8();
            int dataSymbols = r.U8();
            if (dataSymbols == 0) dataSymbols = 256;

            var segments = new List<TapeSegment>();

            if (pilotCount > 0) {
                Symbol[] table = ReadSymbols(r, pilotSymbols, pilotPulses);
                var runs = new List<PulseRun>();
                for (uint i = 0; i < pilotCount; i++) {
                    int s = r.U8();
                    uint repeat = r.U16();
                    if (s >= table.Length || repeat == 0) continue;
                    AddSymbolRuns(runs, table[s], repeat);
                }
                segments.Add(new PulseSegment { Runs = runs.ToArray() });
            }

            info = "";
            if (dataCount > 0) {
                Symbol[] table = ReadSymbols(r, dataSymbols, dataPulses);
                int bitsPerSymbol = 0;
                while ((1 << bitsPerSymbol) < dataSymbols) bitsPerSymbol++;
                long bytes = (dataCount * bitsPerSymbol + 7) / 8;
                byte[] data = r.Bytes(bytes);
                var segment = new DataSegment {
                    Data = data,
                    SymbolCount = dataCount,
                    BitsPerSymbol = bitsPerSymbol,
                    Symbols = table
                };
                segments.Add(segment);
                info = bitsPerSymbol == 1 ? TapeText.DescribeData(data, dataCount, segment.IsStandardRom) : dataCount.ToString() + " symbols";
            }

            AddPause(segments, pause);
            return segments.ToArray();
        }

        // Symbol definitions: a flags byte, then up to maxPulses pulses; a zero pulse ends one early.
        private static Symbol[] ReadSymbols(Reader r, int count, int maxPulses) {
            var table = new Symbol[count];
            for (int i = 0; i < count; i++) {
                int flags = r.U8() & 3;
                var pulses = new List<ushort>();
                bool ended = false;
                for (int p = 0; p < maxPulses; p++) {
                    ushort d = r.U16();
                    if (d == 0) ended = true;
                    if (!ended) pulses.Add(d);
                }
                //TZX flag 0 is an edge, which the model's flip has already made; 1 is no edge,
                //which undoes it; 2 and 3 force the pulse low or high.
                Edge edge = flags switch { 0 => Edge.Keep, 1 => Edge.Toggle, 2 => Edge.Low, _ => Edge.High };
                table[i] = new Symbol(edge, pulses.ToArray());
            }
            return table;
        }

        // A pilot symbol repeated: one run when it's a single plain pulse, else each pulse in turn.
        private static void AddSymbolRuns(List<PulseRun> runs, Symbol s, uint repeat) {
            TapePhase phase = repeat > 1 ? TapePhase.Pilot : TapePhase.Sync;
            if (s.Pulses.Length == 0)
                return;
            if (s.Pulses.Length == 1 && s.FirstEdge == Edge.Keep) {
                runs.Add(new PulseRun(repeat, s.Pulses[0], Edge.Keep, phase));
                return;
            }
            for (uint n = 0; n < repeat; n++)
                for (int p = 0; p < s.Pulses.Length; p++)
                    runs.Add(new PulseRun(1, s.Pulses[p], p == 0 ? s.FirstEdge : Edge.Keep, phase));
        }

        // $32: what the archive says about the tape.
        private static void ReadArchiveInfo(Reader r, TapeInfo info) {
            int n = r.U8();
            for (int i = 0; i < n; i++) {
                int id = r.U8();
                int length = r.U8();
                string raw = TapeText.Latin(r.Bytes(length), 0, length);
                string text = Clean(raw);
                switch (id) {
                    case 0x00: info.Title = text; break;
                    case 0x01: info.Publisher = text; break;
                    case 0x02:
                        foreach (string a in raw.Split('\r', '\n'))
                            if (a.Trim().Length > 0) info.Authors.Add(a.Trim());
                        break;
                    case 0x03: info.Year = text; break;
                    case 0x04: info.Language = text; break;
                    case 0x05: info.Type = text; break;
                    case 0x06: info.Price = text; break;
                    case 0x07: info.Protection = text; break;
                    case 0x08: info.Origin = text; break;
                    case 0xFF: info.Comments.Add(text); break;
                }
            }
        }
    }
}
