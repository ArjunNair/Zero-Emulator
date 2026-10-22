using System;
using System.Collections.Generic;

namespace Peripherals
{
    // What happened while the player moved on, for the machine to act on.
    [Flags]
    public enum TapeEdge
    {
        None = 0,
        LevelChanged = 1, // the level flipped at least once: update the EAR bit
        BlockChanged = 2, // a new block started (or the tape stopped on one)
        Stop = 4,         // a stop block: stop the motor
        EndOfTape = 8     // ran off the end: stop the motor
    }

    // Plays a TapeImage: the state machine for every tape format. The machine adds emulated time
    // with AddTime and, while EdgeDue, takes the next edge with NextEdge. Each edge ends the
    // current pulse, flipping the level, and arms the next one: within a segment, then the block's
    // next segment, then the next block by the tape's control flow (jumps, loops, calls, stops).
    //
    // Time works as the PZX player always has: pending is time run since the last edge was
    // armed, and arming takes it off the new pulse, so a late edge is made up on the next one.
    public sealed class TapePlayer
    {
        private readonly TapeImage image;
        private readonly List<TapeBlock> blocks;

        private long pending; // T-states run since the current pulse was armed
        private long edge;    // the current pulse's length, less what it was armed late by
        private uint armedLength; // the current pulse's whole length

        // Where the next Begin() starts: a block, and a segment in it.
        private int resumeBlock;
        private int resumeSegment;

        // The playing segment.
        private int cursorBlock = -1;
        private int segIndex = -1;
        private TapeSegment segment;
        private int runIndex;
        private uint runPulse;
        private long symbolIndex;
        private int symbolPulse;
        private Symbol symbol;
        private int dataStage; // 0 symbols, 1 tail next, 2 done

        // TZX control flow
        private readonly Stack<(int start, int left)> loops = new Stack<(int, int)>();
        private int callBlock = -1;
        private int callIndex;

        public TapePlayer(TapeImage image) {
            this.image = image ?? TapeImage.Empty;
            blocks = this.image.Blocks;
            Rewind();
        }

        public TapeImage Image => image;

        // The block playing, or the one the tape is parked on.
        public int BlockIndex { get; private set; }

        public int Level { get; private set; }

        public TapePhase Phase { get; private set; } = TapePhase.Idle;

        // Set by the machine: stop blocks for 48K machines only stop it on one.
        public bool Is48K { get; set; }

        // A pulse is armed: the tape is producing a signal.
        public bool IsActive { get; private set; }

        public bool EdgeDue => IsActive && pending >= edge;

        // Stopped at the end of the tape: Begin would play nothing.
        public bool AtEnd => !IsActive && resumeBlock >= blocks.Count;

        public long TStatesToNextEdge => IsActive ? Math.Max(0, edge - pending) : 0;

        // The data segment playing, or null.
        public DataSegment CurrentData => IsActive ? segment as DataSegment : null;

        // In CurrentData: the symbol (bit) whose pulse is playing.
        public long SymbolIndex => symbolIndex;

        // In CurrentData: which of the symbol's pulses is playing, from 0.
        public int PulseInSymbol => symbolPulse - 1;

        public void AddTime(long tstates) => pending += tstates;

        // Forget time run so far: the next pulse armed gets its full length.
        public void ClearClock() => pending = 0;

        // ------------------------------------------------------------------ transport

        // Back to the start, stopped.
        public void Rewind() {
            Halt();
            Phase = TapePhase.Idle;
            Level = image.InitialLevel;
            pending = 0;
            edge = 0;
            Cue(0);
        }

        // Park the tape at the start of a block. While playing, the block that is playing
        // finishes first, and the one after the cued block follows it.
        public void Cue(int block) {
            BlockIndex = Math.Clamp(block, 0, Math.Max(0, blocks.Count - 1));
            resumeBlock = BlockIndex;
            resumeSegment = 0;
            loops.Clear();
            callBlock = -1;
        }

        // The motor stopped mid-tape. The next Begin plays the segment that was
        // playing again from its start, or from the pilot before it if it was data, so a loader
        // that missed the start can lock onto it again.
        public void Stop() {
            if (IsActive && cursorBlock >= 0 && cursorBlock < blocks.Count) {
                int s = segIndex;
                TapeSegment[] segments = blocks[cursorBlock].Segments;
                if (s >= 0 && s < segments.Length && segments[s] is DataSegment)
                    while (s > 0 && IsLeadIn(segments[s - 1])) s--;
                resumeBlock = BlockIndex;
                resumeSegment = cursorBlock == BlockIndex ? Math.Max(0, s) : 0;
            } else if (resumeBlock >= blocks.Count) {
                resumeBlock = BlockIndex; // ran off the end: play the last block again
                resumeSegment = 0;
            }
            Halt();
        }

        // Start the motor where the tape is parked.
        public TapeEdge Begin() {
            TapeEdge flags = TapeEdge.None;
            StartAt(resumeBlock, resumeSegment, ref flags);
            return flags;
        }

        // The deck's Play: start where the tape is parked, or on past it when it's parked on a stop
        // block, and with no time run.
        public TapeEdge Replay() {
            pending = 0;
            if (resumeBlock == BlockIndex && resumeSegment == 0 && BlockIndex < blocks.Count && IsStopBlock(blocks[BlockIndex]))
                resumeBlock++;
            if (resumeBlock >= blocks.Count && blocks.Count > 0) {
                resumeBlock = BlockIndex;
                resumeSegment = 0;
            }
            return Begin();
        }

        // The pulse armed is over: flip the level and arm the next.
        public TapeEdge NextEdge() {
            TapeEdge flags = TapeEdge.None;
            if (!IsActive) return flags;
            pending -= edge;
            edge = 0;
            Flip(ref flags);
            Arm(ref flags);
            return flags;
        }

        // ------------------------------------------------------------------ loader acceleration

        // Play the rest of the current phase at once (a pilot tone, say), as if that much time had
        // passed. Returns the T-states skipped.
        public long SkipPhase(out TapeEdge flags) {
            flags = TapeEdge.None;
            long t = 0;
            TapePhase phase = Phase;
            while (IsActive && Phase == phase) {
                t += TStatesToNextEdge;
                pending = edge;
                flags |= NextEdge();
            }
            return t;
        }

        // Read the next 8 symbols of a binary data segment as a byte, playing their pulses at once.
        // The tape must be at the start of a symbol. False, and nothing played, otherwise.
        public bool TryReadByte(out byte value, out long tstates, out TapeEdge flags) {
            value = 0;
            tstates = 0;
            flags = TapeEdge.None;
            DataSegment data = CurrentData;
            if (data == null || !data.IsBinary || PulseInSymbol != 0 || symbolIndex + 8 > data.SymbolCount)
                return false;

            long start = symbolIndex;
            for (int i = 0; i < 8; i++)
                value = (byte)((value << 1) | data.SymbolAt(start + i));
            while (IsActive && segment == data && dataStage == 0 && symbolIndex < start + 8) {
                tstates += TStatesToNextEdge;
                pending = edge;
                flags |= NextEdge();
            }
            return true;
        }

        // Where the tape is, pulse by pulse, for flash loading's loader handlers.

        // The pulse segment playing (pilot, sync, pause, raw pulses), or null.
        public PulseSegment CurrentPulses => IsActive ? segment as PulseSegment : null;

        // The block whose signal is playing, or null.
        public TapeBlock CurrentBlock => IsActive && cursorBlock >= 0 ? blocks[cursorBlock] : null;

        // The segment playing, and its place in CurrentBlock.
        public TapeSegment CurrentSegment => IsActive ? segment : null;
        public int SegmentIndex => segIndex;

        // In CurrentPulses: the run playing, and its pulses so far, this one included.
        public int RunIndex => runIndex;
        public uint PulseInRun => runPulse;

        // The whole length of the pulse playing, in T-states; 0 when nothing plays.
        public uint CurrentPulseLength => IsActive ? armedLength : 0;

        // The playing pulse is the last of its run: a tone cut short.
        public void EndRun() {
            if (IsActive && segment is PulseSegment p && runIndex < p.Runs.Length)
                runPulse = p.Runs[runIndex].Count;
        }

        // End the playing pulse now, as if its time had passed.
        public TapeEdge FinishPulse() {
            if (!IsActive) return TapeEdge.None;
            pending = edge;
            return NextEdge();
        }

        // End the playing data symbol (bit) now, its remaining pulses' edges and all.
        // edges is how many there were.
        public TapeEdge FinishSymbol(out int edges) {
            edges = 0;
            TapeEdge flags = TapeEdge.None;
            DataSegment data = CurrentData;
            if (data == null) return flags;
            long start = symbolIndex;
            while (IsActive && segment == data && dataStage == 0 && symbolIndex == start) {
                flags |= FinishPulse();
                edges++;
            }
            return flags;
        }

        // Straight on to the start of the next block, by the tape's control flow.
        public TapeEdge ForwardOneBlock() {
            TapeEdge flags = TapeEdge.None;
            pending = 0;
            StartAt(BlockIndex + 1, 0, ref flags);
            return flags;
        }

        // Turn the level over with no edge in the signal: a loader handler putting the EAR bit
        // where the loader's own code expects it after skipping that code.
        public void InvertLevel() => Level ^= 1;

        // ------------------------------------------------------------------ the state machine

        private void Halt() {
            IsActive = false;
            segment = null;
            segIndex = -1;
            cursorBlock = -1;
        }

        private static bool IsStopBlock(TapeBlock b) =>
            b.Control == TapeControlKind.Stop || b.Control == TapeControlKind.StopIf48K;

        // A pilot, sync or pause: what comes before data, which loaders lock onto or wait out.
        private static bool IsLeadIn(TapeSegment seg) {
            if (!(seg is PulseSegment p)) return false;
            foreach (PulseRun r in p.Runs)
                if (r.Phase == TapePhase.Samples || r.Phase == TapePhase.Data) return false;
            return true;
        }

        private void Flip(ref TapeEdge flags) {
            Level ^= 1;
            flags |= TapeEdge.LevelChanged;
        }

        private void ApplyEdge(Edge rule, ref TapeEdge flags) {
            switch (rule) {
                case Edge.Toggle: Flip(ref flags); break;
                case Edge.Low: if (Level != 0) Flip(ref flags); break;
                case Edge.High: if (Level != 1) Flip(ref flags); break;
            }
        }

        private void StartAt(int block, int seg, ref TapeEdge flags) {
            Halt();
            if (seg > 0 && block >= 0 && block < blocks.Count && seg < blocks[block].Segments.Length) {
                BlockIndex = block;
                cursorBlock = block;
                flags |= TapeEdge.BlockChanged;
                BeginSegment(seg, ref flags);
            } else if (!EnterBlock(seg > 0 ? block + 1 : block, ref flags)) {
                return;
            }
            Arm(ref flags);
        }

        // Arm the next pulse, moving through segments and blocks as they run out. A pulse with no
        // length, or one the tape has already run past, is over at once: it flips and the next is
        // tried. False once the tape has stopped or run out.
        private bool Arm(ref TapeEdge flags) {
            while (true) {
                if (segment != null && NextPulse(out uint duration, out Edge rule, out TapePhase phase)) {
                    ApplyEdge(rule, ref flags);
                    Phase = phase;
                    armedLength = duration;
                    if (duration > 0) {
                        long diff = duration - pending;
                        if (diff > 0) {
                            edge = diff;
                            pending = 0;
                            IsActive = true;
                            return true;
                        }
                        pending = -diff;
                    }
                    Flip(ref flags);
                    continue;
                }

                if (segment != null && segIndex + 1 < blocks[cursorBlock].Segments.Length) {
                    BeginSegment(segIndex + 1, ref flags);
                    continue;
                }

                if (!EnterBlock(BlockIndex + 1, ref flags))
                    return false;
            }
        }

        private void BeginSegment(int s, ref TapeEdge flags) {
            segIndex = s;
            segment = blocks[cursorBlock].Segments[s];
            runIndex = 0;
            runPulse = 0;
            symbolIndex = 0;
            symbolPulse = 0;
            symbol = null;
            dataStage = 0;
            ApplyEdge(segment.Entry, ref flags);
        }

        // Start the block at index, or the first block with a signal that control flow leads to
        // from there. False when that's a stop, or the end of the tape.
        private bool EnterBlock(int index, ref TapeEdge flags) {
            segment = null;
            segIndex = -1;
            int guard = blocks.Count * 4 + 64;
            while (true) {
                if (index < 0 || index >= blocks.Count || guard-- <= 0) {
                    BlockIndex = Math.Max(0, blocks.Count - 1);
                    resumeBlock = blocks.Count;
                    resumeSegment = 0;
                    StopHere(TapeEdge.EndOfTape, ref flags);
                    return false;
                }

                TapeBlock block = blocks[index];
                BlockIndex = index;
                flags |= TapeEdge.BlockChanged;

                switch (block.Control) {
                    case TapeControlKind.Stop:
                    case TapeControlKind.StopIf48K:
                        if (block.Control == TapeControlKind.StopIf48K && !Is48K) {
                            index++;
                            continue;
                        }
                        resumeBlock = index + 1;
                        resumeSegment = 0;
                        StopHere(TapeEdge.Stop, ref flags);
                        return false;

                    case TapeControlKind.SetLevel:
                        if (Level != block.Level) Flip(ref flags);
                        index++;
                        continue;

                    case TapeControlKind.None:
                        if (block.Segments.Length == 0) {
                            index++;
                            continue;
                        }
                        cursorBlock = index;
                        BeginSegment(0, ref flags);
                        return true;

                    default:
                        index = FollowControl(index);
                        continue;
                }
            }
        }

        // Where a jump, loop or call block sends the tape.
        private int FollowControl(int index) {
            TapeBlock block = blocks[index];
            switch (block.Control) {
                case TapeControlKind.Jump:
                    return block.Offset == 0 ? index + 1 : index + block.Offset;

                case TapeControlKind.LoopStart:
                    loops.Push((index + 1, Math.Max(1, block.Count)));
                    return index + 1;

                case TapeControlKind.LoopEnd:
                    if (loops.Count > 0) {
                        var (start, left) = loops.Pop();
                        if (left > 1) {
                            loops.Push((start, left - 1));
                            return start;
                        }
                    }
                    return index + 1;

                case TapeControlKind.Call:
                    if (block.Offsets.Length == 0) return index + 1;
                    callBlock = index;
                    callIndex = 0;
                    return index + block.Offsets[0];

                case TapeControlKind.Return:
                    if (callBlock < 0) return index + 1;
                    int[] offsets = blocks[callBlock].Offsets;
                    if (++callIndex < offsets.Length) return callBlock + offsets[callIndex];
                    int after = callBlock + 1;
                    callBlock = -1;
                    return after;

                default:
                    return index + 1;
            }
        }

        private void StopHere(TapeEdge why, ref TapeEdge flags) {
            Halt();
            Phase = TapePhase.Stopped;
            flags |= why;
        }

        // The next pulse of the playing segment: false once it has none left.
        private bool NextPulse(out uint duration, out Edge rule, out TapePhase phase) {
            duration = 0;
            rule = Edge.Keep;
            phase = TapePhase.Idle;

            if (segment is PulseSegment pulses) {
                PulseRun[] runs = pulses.Runs;
                while (runIndex < runs.Length) {
                    PulseRun run = runs[runIndex];
                    if (runPulse < run.Count) {
                        rule = runPulse == 0 ? run.FirstEdge : Edge.Keep;
                        duration = run.Duration;
                        phase = run.Phase;
                        runPulse++;
                        return true;
                    }
                    runIndex++;
                    runPulse = 0;
                }
                return false;
            }

            if (!(segment is DataSegment data))
                return false;

            while (dataStage == 0) {
                if (symbolIndex >= data.SymbolCount) {
                    dataStage = 1;
                    break;
                }
                if (symbolPulse == 0) {
                    int v = data.SymbolAt(symbolIndex);
                    symbol = v < data.Symbols.Length ? data.Symbols[v] : null;
                }
                if (symbol != null && symbolPulse < symbol.Pulses.Length) {
                    rule = symbolPulse == 0 ? symbol.FirstEdge : Edge.Keep;
                    duration = symbol.Pulses[symbolPulse];
                    phase = TapePhase.Data;
                    symbolPulse++;
                    return true;
                }
                symbolIndex++;
                symbolPulse = 0;
            }

            if (dataStage == 1) {
                dataStage = 2;
                phase = TapePhase.Tail;
                if (data.Tail > 0) {
                    duration = data.Tail;
                    return true;
                }
                // A tape whose last data has no tail pulse would leave its last bit unfinished, so
                // one last edge ends it. Not in the PZX spec, where a zero tail has no effect.
                if (cursorBlock == blocks.Count - 1 && segIndex == blocks[cursorBlock].Segments.Length - 1) {
                    duration = 3500 * 2;
                    return true;
                }
            }
            return false;
        }
    }
}
