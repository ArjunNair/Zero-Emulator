using System;
using Cpu;
using Peripherals;

namespace Speccy
{
    /// <summary>
    /// Edge loading: finds a tape loader's edge-sampling loop and skips it ahead to the next edge.
    ///
    /// A loader waits for an edge by polling the EAR port in a tight loop, counting the passes in
    /// one register. Seen from outside, that's the same IN A,($FE) coming round every few dozen
    /// T-states with exactly one register stepped by 1 and nothing else changed. Once a loop has
    /// shown that eight times in a row (and a ninth to confirm), <see cref="Skip"/> works out how
    /// many passes the loop would make before the edge arrives, steps the register that many times
    /// and hands back the tape time they would have taken, in one go.
    ///
    /// Adapted from Spin's generic edge loader (NewEdgeLoader), including Spin 2's cache of
    /// confirmed loops and its check that stops skipping a loop that doesn't leave on an edge.
    /// </summary>
    public sealed class EdgeLoader
    {
        // The registers a loop may count in. A is left out: it must stay the same, as it holds
        // the port's high byte for the IN. The shadow set is watched too, so an EXX, which
        // changes several at once, is never mistaken for a count.
        private const int RegisterCount = 17;
        private const int NoRegister = -1;

        private const int MaxGap = 128;     // a pass longer than this is no edge loop
        private const int PassesToVerify = 8;
        private const int UlaPort = 0xFE;

        private readonly Z80 cpu;
        private readonly byte[] last = new byte[RegisterCount];
        private byte lastA;

        private long lastTime = long.MinValue;
        private int lastPC = -1;
        private int lastExx;

        private int track = NoRegister; // the register counting the passes
        private int sign;               // +1 or -1 a pass
        private int count;              // passes seen so far, in a row
        private long period;            // T-states a pass

        private bool verified;
        private bool doNotAccelerate;
        private long lastConfirmed = long.MinValue; // when a confirmed loop was last seen
        private bool checkNextPass;

        private struct Loop
        {
            public bool Valid;
            public int PC;
            public int Register;
            public int Sign;
            public long Period;
            public int ExxParity;
            public TapePhase Phase;
            public bool DoNotAccelerate;
        }

        // Loaders often poll from more than one place, or the same place with a different counter
        // in each phase, so a few confirmed loops are kept rather than learnt again every time.
        private readonly Loop[] cache = new Loop[4];
        private int nextSlot;
        private int cached = -1;

        public EdgeLoader(Z80 cpu) {
            this.cpu = cpu;
        }

        /// <summary>The loop at the IN just observed is a confirmed loader loop.</summary>
        public bool Verified => verified;

        /// <summary>It may be skipped ahead: confirmed, and not one that carries on past an edge.</summary>
        public bool CanAccelerate => verified && !doNotAccelerate && period > 0;

        /// <summary>
        /// A loader is at work: its confirmed loop came round within the last frame. Between visits
        /// it is handling the edge it saw and waiting for the next, so its delays are loading
        /// delays; once it stops polling, the program's own countdowns are its own again.
        /// </summary>
        public bool LoaderActive(long now, long window = OneFrame) =>
            lastConfirmed != long.MinValue && now - lastConfirmed <= window;

        public const long OneFrame = 69888;

        /// <summary>T-states a pass of the confirmed loop takes.</summary>
        public long Period => period;

        /// <summary>Forget everything learnt: a new tape, or a reset.</summary>
        public void Reset() {
            lastTime = long.MinValue;
            lastConfirmed = long.MinValue;
            lastPC = -1;
            track = NoRegister;
            sign = 0;
            count = 0;
            period = 0;
            verified = false;
            doNotAccelerate = false;
            checkNextPass = false;
            cached = -1;
            Array.Clear(cache, 0, cache.Length);
            nextSlot = 0;
        }

        /// <summary>
        /// An IN A,(n) from an even port is about to run at <paramref name="pc"/> (the address of
        /// its operand), <paramref name="now"/> T-states into emulation. Learns from it, and
        /// returns whether it is a confirmed loader loop.
        /// </summary>
        public bool Observe(long now, int pc, byte port, TapePhase phase) {
            long gap = lastTime == long.MinValue ? long.MaxValue : now - lastTime;
            lastTime = now;
            int exx = cpu.ExxCount;

            // A loop confirmed before, here, in this phase of the tape, with the register set the
            // same way round (an odd number of EXXs since swaps what each register holds).
            int hit = -1;
            if (phase != TapePhase.Stopped) {
                for (int k = 0; k < cache.Length; k++) {
                    if (!cache[k].Valid || cache[k].PC != pc)
                        continue;
                    if (cache[k].Phase == phase) {
                        if (((cache[k].ExxParity ^ exx) & 1) == 0)
                            hit = k;
                        else
                            cache[k].Valid = false;
                    }
                    break;
                }
            }

            if (hit >= 0) {
                verified = true;
                track = cache[hit].Register;
                sign = cache[hit].Sign;
                period = cache[hit].Period;
                doNotAccelerate = cache[hit].DoNotAccelerate;
                lastPC = pc;
                lastExx = exx;
                cached = hit;
            } else if (pc != lastPC || exx != lastExx || phase == TapePhase.Stopped) {
                verified = false;
                doNotAccelerate = false;
                checkNextPass = false;
                cached = -1;
            }

            // The pass after a skip: a loop that leaves on an edge comes back here only after
            // doing something with it. Back again a single pass later, it didn't leave: it counts
            // through edges, and skipping to them changes what it counts. Leave it alone.
            if (checkNextPass) {
                checkNextPass = false;
                if (pc == lastPC && gap == period) {
                    doNotAccelerate = true;
                    if (cached >= 0) cache[cached].DoNotAccelerate = true;
                }
            }

            if (!verified)
                Learn(gap, pc, exx, port, phase);
            if (verified)
                lastConfirmed = now;
            return verified;
        }

        // Watch for one register, and only that one, stepping the same way on every pass.
        private void Learn(long gap, int pc, int exx, byte port, TapePhase phase) {
            if (gap > MaxGap || pc != lastPC || exx != lastExx) {
                lastPC = pc;
                lastExx = exx;
                count = 0;
                sign = 0;
                track = NoRegister;
                lastA = cpu.regs.A;
                for (int r = 0; r < RegisterCount; r++)
                    last[r] = Get(r);
                return;
            }

            if (cpu.regs.A != lastA || port != UlaPort) {
                lastPC = -1; // not a tape loop: start again on the next IN
                return;
            }

            int changed = 0, which = NoRegister, delta = 0;
            for (int r = 0; r < RegisterCount; r++) {
                byte now = Get(r);
                if (now == last[r])
                    continue;
                changed++;
                which = r;
                delta = (sbyte)(byte)(now - last[r]); // 255 to 0 is a step of +1
                last[r] = now;
            }

            if (changed != 1) {
                count = 0;
                sign = 0;
                track = NoRegister;
                return;
            }
            if (which != track) {
                count = 0;
                sign = 0;
                track = which;
                return;
            }
            if (count < PassesToVerify) {
                if (sign == 0) {
                    if (delta == 1 || delta == -1) {
                        sign = delta;
                        count++;
                    }
                } else if (delta == sign) {
                    count++;
                } else {
                    count = 0;
                    sign = 0;
                }
                return;
            }

            if (delta != sign) {
                count = 0;
                sign = 0;
                return;
            }

            // Eight passes in a row, and this ninth: it's a loader loop.
            verified = true;
            doNotAccelerate = false;
            period = gap;
            Remember(pc, exx, phase);
        }

        private void Remember(int pc, int exx, TapePhase phase) {
            int slot = -1;
            for (int k = 0; k < cache.Length; k++) {
                if (!cache[k].Valid || cache[k].PC == pc) {
                    slot = k;
                    break;
                }
            }
            if (slot < 0)
                slot = nextSlot;
            cache[slot] = new Loop {
                Valid = true, PC = pc, Register = track, Sign = sign, Period = period,
                ExxParity = exx & 1, Phase = phase, DoNotAccelerate = false
            };
            nextSlot = (slot + 1) % cache.Length;
            cached = slot;
        }

        /// <summary>
        /// Skip the confirmed loop to the pass that sees an edge <paramref name="tstatesToEdge"/>
        /// T-states away: step its register for every pass, and return the tape time they take.
        /// The register stops short of running out (255 counting up, 1 counting down), so the
        /// loader's own timeout still happens when no edge comes.
        /// </summary>
        public long Skip(long tstatesToEdge) {
            if (!CanAccelerate || track == NoRegister || tstatesToEdge < 0)
                return 0;
            long passes = (tstatesToEdge + period - 1) / period;
            int value = Get(track);
            long room = sign > 0 ? 255 - value : value - 1;
            passes = Math.Max(0, Math.Min(passes, room));
            if (passes == 0)
                return 0;
            Set(track, (byte)(value + passes * sign));
            last[track] = Get(track);
            return passes * period;
        }

        /// <summary>The skip reached an edge: watch the next pass for a loop that didn't leave.</summary>
        public void EdgeTaken() => checkNextPass = true;

        // B C D E H L IXh IXl IYh IYl A' B' C' D' E' H' L'
        private byte Get(int r) {
            switch (r) {
                case 0: return cpu.regs.B;
                case 1: return cpu.regs.C;
                case 2: return cpu.regs.D;
                case 3: return cpu.regs.E;
                case 4: return cpu.regs.H;
                case 5: return cpu.regs.L;
                case 6: return cpu.regs.IXH;
                case 7: return cpu.regs.IXL;
                case 8: return cpu.regs.IYH;
                case 9: return cpu.regs.IYL;
                case 10: return (byte)(cpu.regs.AF_ >> 8);
                case 11: return (byte)(cpu.regs.BC_ >> 8);
                case 12: return (byte)cpu.regs.BC_;
                case 13: return (byte)(cpu.regs.DE_ >> 8);
                case 14: return (byte)cpu.regs.DE_;
                case 15: return (byte)(cpu.regs.HL_ >> 8);
                default: return (byte)cpu.regs.HL_;
            }
        }

        private void Set(int r, byte v) {
            switch (r) {
                case 0: cpu.regs.B = v; break;
                case 1: cpu.regs.C = v; break;
                case 2: cpu.regs.D = v; break;
                case 3: cpu.regs.E = v; break;
                case 4: cpu.regs.H = v; break;
                case 5: cpu.regs.L = v; break;
                case 6: cpu.regs.IXH = v; break;
                case 7: cpu.regs.IXL = v; break;
                case 8: cpu.regs.IYH = v; break;
                case 9: cpu.regs.IYL = v; break;
                case 10: cpu.regs.AF_ = (ushort)((cpu.regs.AF_ & 0x00FF) | (v << 8)); break;
                case 11: cpu.regs.BC_ = (ushort)((cpu.regs.BC_ & 0x00FF) | (v << 8)); break;
                case 12: cpu.regs.BC_ = (ushort)((cpu.regs.BC_ & 0xFF00) | v); break;
                case 13: cpu.regs.DE_ = (ushort)((cpu.regs.DE_ & 0x00FF) | (v << 8)); break;
                case 14: cpu.regs.DE_ = (ushort)((cpu.regs.DE_ & 0xFF00) | v); break;
                case 15: cpu.regs.HL_ = (ushort)((cpu.regs.HL_ & 0x00FF) | (v << 8)); break;
                default: cpu.regs.HL_ = (ushort)((cpu.regs.HL_ & 0xFF00) | v); break;
            }
        }
    }
}
