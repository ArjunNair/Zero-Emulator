using System;
using System.Collections.Generic;

namespace Peripherals
{
    // The format-neutral tape that TZX, PZX and CSW images all decode into, and that TapePlayer plays.
    //
    // Level rule (PZX's, which Zero has always played): every pulse holds the level for its
    // duration and flips it when it ends. A pulse may first apply an Edge rule, which is how a
    // block forces its starting level. TZX's rule, an edge at the start of every pulse, is the same
    // stream seen one pulse earlier: a TZX "current level" of L is a level of !L here, waiting for
    // the next pulse. The TZX decoder maps onto this rule; see TZXFile.

    // What happens to the level before a pulse starts.
    public enum Edge : byte
    {
        Keep,   // hold what the last pulse left (the usual case: the flip already happened)
        Toggle, // flip once more, undoing the last pulse's flip
        Low,    // force low
        High    // force high
    }

    // The kind of signal playing, for edge loading and future loader acceleration.
    public enum TapePhase : byte
    {
        Idle,    // between blocks, or a block with no signal
        Pilot,   // a repeated pulse: a leader tone
        Sync,    // single pulses: syncs, or any short pulse sequence
        Data,    // the pulses of data bits (or symbols)
        Tail,    // the pulse after the last data bit
        Pause,   // silence between blocks
        Samples, // a raw recording: direct recording or CSW
        Stopped  // the tape stopped or ran out
    }

    // Count pulses of Duration T-states.
    public readonly struct PulseRun
    {
        public readonly uint Count;
        public readonly uint Duration;
        // Applied before the run's first pulse only; the rest keep the level.
        public readonly Edge FirstEdge;
        public readonly TapePhase Phase;

        public PulseRun(uint count, uint duration, Edge firstEdge, TapePhase phase) {
            Count = count;
            Duration = duration;
            FirstEdge = firstEdge;
            Phase = phase;
        }

        public long TStates => (long)Count * Duration;
    }

    public abstract class TapeSegment
    {
        // Applied to the level as the segment starts, before its first pulse.
        public Edge Entry = Edge.Keep;

        public abstract long DurationTStates { get; }
    }

    // Pilot, sync, pause or raw pulses: anything that isn't data bits.
    public sealed class PulseSegment : TapeSegment
    {
        public PulseRun[] Runs = Array.Empty<PulseRun>();

        public override long DurationTStates {
            get {
                long t = 0;
                foreach (PulseRun r in Runs) t += r.TStates;
                return t;
            }
        }
    }

    // One data symbol: a bit's (or a TZX $19 symbol's) pulses.
    public sealed class Symbol
    {
        public Edge FirstEdge = Edge.Keep;
        public ushort[] Pulses = Array.Empty<ushort>();

        public Symbol(Edge firstEdge, params ushort[] pulses) {
            FirstEdge = firstEdge;
            Pulses = pulses;
        }

        public long TStates {
            get {
                long t = 0;
                foreach (ushort p in Pulses) t += p;
                return t;
            }
        }
    }

    // A stream of symbols, BitsPerSymbol bits each, read MSB first from
    // Data. Ordinary data has two symbols (0 and 1) of one bit each.
    public sealed class DataSegment : TapeSegment
    {
        public byte[] Data = Array.Empty<byte>();
        public long SymbolCount;
        public int BitsPerSymbol = 1;
        public Symbol[] Symbols = Array.Empty<Symbol>();
        public uint Tail;

        // Two one-bit symbols: bytes can be read off it.
        public bool IsBinary => Symbols.Length == 2 && BitsPerSymbol == 1;

        // What the ROM loader can read, so flash loading may take it: two non-zero pulses per bit,
        // and a 0 bit shorter than a 1 bit (the PZX FAQ's test for standard data).
        public bool IsStandardRom {
            get {
                if (!IsBinary) return false;
                ushort[] s0 = Symbols[0].Pulses, s1 = Symbols[1].Pulses;
                return s0.Length == 2 && s1.Length == 2 && s0[0] != 0 && s0[1] != 0 && s1[0] != 0 && s1[1] != 0
                       && s0[0] + s0[1] < s1[0] + s1[1]
                       && Symbols[0].FirstEdge == Edge.Keep && Symbols[1].FirstEdge == Edge.Keep;
            }
        }

        // Bytes of whole data, for a binary segment.
        public int ByteCount => (int)Math.Min(Data.Length, (SymbolCount * BitsPerSymbol + 7) / 8);

        public int SymbolAt(long index) {
            int value = 0;
            long bit = index * BitsPerSymbol;
            for (int i = 0; i < BitsPerSymbol; i++, bit++) {
                long at = bit >> 3;
                int b = at < Data.Length ? (Data[at] >> (7 - (int)(bit & 7))) & 1 : 0;
                value = (value << 1) | b;
            }
            return value;
        }

        public override long DurationTStates {
            get {
                long t = Tail;
                long[] symbolTime = new long[Symbols.Length];
                for (int i = 0; i < Symbols.Length; i++) symbolTime[i] = Symbols[i].TStates;
                for (long s = 0; s < SymbolCount; s++) {
                    int v = SymbolAt(s);
                    if (v < symbolTime.Length) t += symbolTime[v];
                }
                return t;
            }
        }
    }

    public enum TapeControlKind : byte
    {
        None,       // nothing to do: plays its segments, if any
        Stop,       // stop the tape
        StopIf48K,  // stop the tape on a 48K machine only
        SetLevel,   // set the level (Level holds it, already in this model's terms)
        Jump,       // carry on at block Index + Offset
        LoopStart,  // repeat the blocks up to the next LoopEnd Count times
        LoopEnd,
        Call,       // play each of Offsets in turn, from block Index + Offset to its Return
        Return
    }

    public sealed class TapeBlock
    {
        // The TZX block ID, or the nearest TZX block for another format's (a PZX PULS of a tone is
        // $12, other pulses $13; DATA is $14). Loader handlers go by it, as Spin's do.
        public byte Id;
        // The block list's name for it: "Data", "PULS", "Pause" and so on.
        public string Kind = "";
        // The block list's details: a header's name, a pause's length, a text block's text.
        public string Info = "";
        public TapeSegment[] Segments = Array.Empty<TapeSegment>();

        public TapeControlKind Control = TapeControlKind.None;
        public int Level;               // SetLevel
        public int Offset;              // Jump
        public int Count;               // LoopStart
        public int[] Offsets = Array.Empty<int>(); // Call

        // True for a block that is only text or a marker, to skip when cueing.
        public bool HasSignal => Segments.Length > 0;

        public long DurationTStates {
            get {
                long t = 0;
                foreach (TapeSegment s in Segments) t += s.DurationTStates;
                return t;
            }
        }
    }

    // Descriptive fields from a PZXT header or a TZX archive info block (all optional).
    public sealed class TapeInfo
    {
        public string Title;
        public string Publisher;
        public List<string> Authors = new List<string>();
        public string Year;
        public string Language;
        public string Type;
        public string Price;
        public string Protection;
        public string Origin;
        public List<string> Comments = new List<string>();
    }

    public sealed class TapeImage
    {
        public static readonly TapeImage Empty = new TapeImage();

        // One entry per block in the file, so the block list's rows line up with it.
        public List<TapeBlock> Blocks = new List<TapeBlock>();
        // Null when the tape has no PZXT header or TZX archive info.
        public TapeInfo Info;
        // The level before the first pulse. A TZX tape starts low by its rule, which is high here.
        public int InitialLevel;
    }
}
