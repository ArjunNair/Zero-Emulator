using System;
using Cpu;

namespace Speccy
{
    // The tape as Spin's loader handlers see it: its live state, and the moves they make on it.
    // zx_spectrum implements it over Peripherals.TapePlayer.
    public interface IFlashTape
    {
        // Spin's tape state: 0 pilot, 1 first sync, 2 second sync, 3 data, 4 pause, 5 done, 6 anything else.
        int State { get; }
        // The TZX block ID of the block playing.
        byte BlockId { get; }
        // Pulses of the pilot tone left, the playing one included (of a pulse sequence: its pulses left).
        int PulseToneLen { get; }
        // The length of the playing run's pulses.
        uint PulseLen { get; }
        // The playing pulse's whole length (Spin's AimTStates); 0 when nothing plays.
        uint AimTStates { get; }
        // T-states until the playing pulse ends (Spin's AimTStates - TotalTs).
        long ToNextEdge { get; }
        // The byte of the block's data playing, from 0, and how many there are.
        int DataPos { get; }
        int DataLen { get; }
        byte CurDataByte { get; }
        // The playing bit's mask in CurDataByte.
        byte BitCounter { get; }
        // The playing bit's first edge has gone by (Spin's PulsesDone = 1).
        bool EdgeGone { get; }
        // An edge came and the loader hasn't seen it (Spin's EdgeTrigger), and its acknowledgement.
        bool EdgeTrigger { get; set; }
        bool EdgeTriggerAck { get; set; }
        // The EAR bit as an IN would read it now.
        bool EarLevel { get; }
        // The level changed since the last IN: the loader is yet to see that edge.
        bool EdgeUnseen { get; }
        // Turn an edge the loader hasn't seen back, so EAR is where the loader last saw it: for a
        // handler about to drop the rest of the signal it came in, edge and all.
        void TakeBackUnseenEdge();
        bool Playing { get; }

        // Play this many T-states of tape (Spin's UpdateTZXState); negative takes time back.
        void Update(long tstates);
        // End the playing pulse now, and the tone with it if it's a pilot.
        void EndToneAndFinishPulse();
        // End the playing pulse now.
        void FinishPulse();
        // The playing bit's value, then that bit played out with one edge, as Spin's are.
        int ConsumeTapeBit();
        // Flip EAR and latch the edge for the loader to see.
        void MarkEdge();
        // Flip EAR as the edge the loader was waiting on, already taken.
        void ConsumeEdge();
        // On to the start of the next block.
        void ForwardOneBlock();
        // Auto play: start the tape for a loader. Whether it's playing now.
        bool EnsurePlaying();
    }

    // Flash loading: known tape loaders recognised by their code around an IN A,(n), and their
    // work done for them: pilots skipped, bytes put straight into memory, decryptors run out.
    // A loader not recognised is left to edge loading.
    //
    // Ported from Spin 2's ZXFlashLoad (ZXDunny's Pascal port of ProcINA_N and the code around
    // it in ZXSpin, by Mark Boyd), with the author's permission. The loader handlers are as they
    // are there; Spin's tape live state is reached through IFlashTape.
    public sealed class FlashLoader
    {
        private const int LID_ROM = 1, LID_MS = 2, LID_BLEEPLOAD = 3, LID_SPEEDLOCK = 4,
            LID_ALKATRAZ = 5, LID_LOADER4 = 6, LID_LOADER6 = 7, LID_LOADER7 = 8,
            LID_LOADER8 = 9, LID_LOADER9 = 10, LID_TWISTER = 11, LID_ULTRA = 12,
            LID_ROMMEL = 13, LID_WOODY = 14, LID_DECRYPTION = 15, LID_ATF = 16,
            LID_BASIL = 17, LID_S2 = 18, LID_LONEWOLF = 19,
            // zqloader (github.com/oxidaan/zqloader) by generation, and Spin 2's own zqturbo.
            LID_ZQLOADER1 = 20, LID_ZQLOADER20 = 21, LID_ZQLOADER21 = 22, LID_ZQTURBO = 23;

        private const byte FlagC = 0x01, FlagN = 0x02, FlagZ = 0x40;

        private readonly struct LoaderDef
        {
            public readonly int Id;
            public readonly int Offset; // bytes back from the PC after the IN
            public readonly uint[] Data;
            public readonly uint[] Mask;

            public LoaderDef(int id, int offset, params uint[] dataAndMask) {
                Id = id;
                Offset = offset;
                Data = new uint[dataAndMask.Length / 2];
                Mask = new uint[dataAndMask.Length / 2];
                for (int i = 0; i < Data.Length; i++) {
                    Data[i] = dataAndMask[i * 2];
                    Mask[i] = dataAndMask[i * 2 + 1];
                }
            }
        }

        // Each signature: (data, mask) dwords read little-endian from PC - Offset.
        private static readonly LoaderDef[] Loaders = {
            new LoaderDef(LID_ROM, 6, 0x003EC804, 0x00FFFFFF, 0xD01FFEDB, 0xFFFFFFFF, 0x2820E6A9, 0xFFFFFFFF, 0x000000F3, 0x000000FF),
            new LoaderDef(LID_MS, 10, 0x20040006, 0xFFFFFFFF, 0xFF3E2401, 0xFFFFFFFF, 0xE6A9FEDB, 0xFFFFFFFF, 0x79F32840, 0xFFFFFFFF),
            new LoaderDef(LID_BLEEPLOAD, 6, 0x003EC804, 0x00FFFFFF, 0x001FFEDB, 0x00FFFFFF, 0x2820E6A9, 0xFFFFFFFF, 0x000000F3, 0x000000FF),
            new LoaderDef(LID_SPEEDLOCK, 6, 0x003EC804, 0x00FFFFFF, 0xA91FFEDB, 0xFFFFFFFF, 0xF42820E6, 0xFFFFFFFF),
            new LoaderDef(LID_ALKATRAZ, 8, 0x00032004, 0x00FFFFFF, 0xFEDB0000, 0xFFFF0000, 0xE6A9C81F, 0xFFFFFFFF, 0x00F12820, 0x00FFFFFF),
            new LoaderDef(LID_LOADER4, 6, 0x003EC804, 0x00FFFFFF, 0xA91FFEDB, 0xFFFFFFFF, 0xF32820E6, 0xFFFFFFFF),
            new LoaderDef(LID_LOADER6, 15, 0x000A2004, 0x00FFFFFF, 0, 0, 0, 0, 0x1FFEDB00, 0xFFFFFF00, 0x20E6A9C8, 0xFFFFFFFF, 0x0000EA28, 0x0000FFFF),
            new LoaderDef(LID_LOADER7, 6, 0x003EC804, 0x00FFFFFF, 0xE6A9FEDB, 0xFFFFFFFF, 0x2800D840, 0xFF00FFFF, 0x000000F3, 0x000000FF),
            new LoaderDef(LID_LOADER8, 14, 0x00092004, 0x00FFFFFF, 0, 0, 0xC9A70000, 0xFFFF0000, 0xC81FFEDB, 0xFFFFFFFF, 0x0000E6A9, 0x0000FFFF),
            new LoaderDef(LID_LOADER9, 6, 0x7F3EC804, 0xFFFFFFFF, 0x1FCBFEDB, 0xFFFFFFFF, 0x00E6A9D0, 0x00FFFFFF, 0x0000F228, 0x0000FFFF),
            new LoaderDef(LID_TWISTER, 4, 0xFEDB7F3E, 0xFFFFFFFF, 0x2040E6A9, 0xFFFFFFFF, 0xF4200504, 0xFFFFFFFF),
            new LoaderDef(LID_ULTRA, 4, 0xFEDBC804, 0xFFFFFFFF, 0x2840E6A9, 0xFFFFFFFF, 0x000000F7, 0x000000FF),
            new LoaderDef(LID_ROMMEL, 8, 0x0000CA04, 0x0000FFFF, 0xFEDB7F3E, 0xFFFFFFFF, 0x20E6A91F, 0xFFFFFFFF, 0x0000F228, 0x0000FFFF),
            new LoaderDef(LID_WOODY, 7, 0x3E002804, 0xFF00FFFF, 0x1FFEDB7F, 0xFFFFFFFF, 0x2820E6A9, 0xFFFFFFFF, 0x000000F3, 0x000000FF),
            new LoaderDef(LID_DECRYPTION, 8, 0x4F00003A, 0xFF0000FF, 0xFEDB7F3E, 0xFFFFFFFF, 0x28A940E6, 0xFFFFFFFF, 0x00002A0E, 0x0000FFFF,
                          0x0000222C, 0x0000FFFF, 0x40E62F79, 0xFFFFFFFF, 0x10000032, 0xFF0000FF, 0x000000E3, 0x000000FF),
            // The JP Z target in the third dword moves with relocation: match the LD A,C after it.
            new LoaderDef(LID_ATF, 4, 0xFEDBC805, 0xFFFFFFFF, 0xCA40E6A9, 0xFFFFFFFF, 0x00790000, 0x00FF0000),
            new LoaderDef(LID_BASIL, 12, 0xC601E67B, 0xFFFFFFFF, 0x2EFED30C, 0xFFFFFFFF, 0xFEDB2C00, 0xFFFFFFFF, 0x0000CAA4, 0x0000FFFF,
                          0xFED3003E, 0xFFFF00FF, 0xA4FEDB2C, 0xFFFFFFFF),
            new LoaderDef(LID_S2, 6, 0x9FD3AFF3, 0xFFFFFFFF, 0x003A9FDB, 0x00FFFFFF, 0x28C9FE00, 0xFFFFFF00),
            // Lone Wolf III: the first IN of its relocated two-edge reader. The handler checks the rest.
            new LoaderDef(LID_LONEWOLF, 12, 0x3FCB3FCB, 0xFFFFFFFF, 0x0C40064F, 0xFFFFFFFF, 0xFEDB1628, 0xFFFFFFFF, 0x0000CAA0, 0x0000FFFF,
                          0xDB0D280C, 0xFFFFFFFF, 0x00C2A0FE, 0x00FFFFFF, 0x4FEDAF00, 0xFFFFFF00, 0xC937D979, 0xFFFFFFFF),
            new LoaderDef(LID_ZQLOADER1, 6, 0x7F3EC805, 0xFFFFFFFF, 0xE6A9FEDB, 0xFFFFFFFF, 0x79F52840, 0xFFFFFFFF, 0xFED34FAC, 0xFFFFFFFF),
            new LoaderDef(LID_ZQLOADER20, 4, 0xFEDBC805, 0xFFFFFFFF, 0x2840E6A9, 0xFFFFFFFF, 0x4FAC79F7, 0xFFFFFFFF, 0xBB78FED3, 0xFFFFFFFF),
            new LoaderDef(LID_ZQLOADER21, 4, 0xFEDBC805, 0xFFFFFFFF, 0xF828A3A9, 0xFFFFFFFF, 0xD34FAC79, 0xFFFFFFFF, 0x00FE78FE, 0x00FFFFFF),
            new LoaderDef(LID_ZQTURBO, 2, 0xA3AAFEDB, 0xFFFFFFFF, 0xF8100320, 0xFFFFFFFF, 0x57AC7AC9, 0xFFFFFFFF, 0xFE78FED3, 0xFFFFFFFF),
        };

        // What happened to one IN.
        public struct Result
        {
            public bool Matched;      // a known loader's code
            public bool Handled;      // its handler did the work: PC and registers are set
            public bool Accelerated;  // the tape was moved on, but the IN is still to complete
            public bool SkipPortRead; // with Handled: A is set too, don't read the port
            public bool BypassGeneric; // the loader must not be edge loaded either
        }

        private readonly Z80 cpu;
        private readonly IFlashTape tape;
        private readonly Func<ushort, byte> read;
        private readonly Action<int, int> write;

        // Technician Ted
        private int tedBit;
        private bool tedBitValid;
        private ushort tedReaderStart;

        // An address whose code the loader replaces when it is reached (Ted's reader); -1: none.
        public int TrapPC { get; private set; } = -1;

        public FlashLoader(Z80 cpu, IFlashTape tape, Func<ushort, byte> read, Action<int, int> write) {
            this.cpu = cpu;
            this.tape = tape;
            this.read = read;
            this.write = write;
        }

        public void Reset() {
            tedBit = 0;
            tedBitValid = false;
            TrapPC = -1;
        }

        // ------------------------------------------------------------------ memory

        private byte Rd(int a) => read((ushort)(a & 0xFFFF));
        private ushort RdW(int a) => (ushort)(Rd(a) | Rd(a + 1) << 8);
        private uint RdD(int a) => (uint)(Rd(a) | Rd(a + 1) << 8 | Rd(a + 2) << 16 | Rd(a + 3) << 24);
        private void Wr(int a, int v) => write(a & 0xFFFF, v & 0xFF);
        private void WrW(int a, int v) { Wr(a, v); Wr(a + 1, v >> 8); }
        private static ushort W(int v) => (ushort)(v & 0xFFFF);

        private bool MatchMemory(int address, params byte[] values) {
            for (int i = 0; i < values.Length; i++)
                if (Rd(address + i) != values[i])
                    return false;
            return true;
        }

        // Scan forward from PC+1 to JumpCode, then past it and its operand.
        private bool SkipPC(ref ushort target, byte jumpCode) {
            int safety = 0;
            do {
                target++;
                if (++safety > 64) return false;
            } while (Rd(target) != jumpCode);
            target += 2;
            return true;
        }

        // ------------------------------------------------------------------ the IN

        // An IN A,(n) from port is running, with PC just past it. Recognise the loader and, with
        // flash on, let its handler do the work. speedLoading: edge or flash loading is on.
        public Result ProcessPortRead(byte port, bool speedLoading, bool flash, bool autoTape) {
            var res = new Result();
            ushort pc = cpu.regs.PC;
            if (Rd(pc - 2) != 0xDB) return res;
            if ((port & 1) != 0 && port != 159) return res;

            // Port $9F is the Speedlock 2/3 decryptor's, and has a list of its own.
            LoaderDef cur = default;
            bool matched = false;
            foreach (LoaderDef l in Loaders) {
                if ((port == 159) != (l.Id == LID_S2))
                    continue;
                matched = true;
                for (int j = 0; j < l.Data.Length; j++) {
                    if ((RdD(pc - l.Offset + j * 4) & l.Mask[j]) != l.Data[j]) {
                        matched = false;
                        break;
                    }
                }
                if (matched) {
                    cur = l;
                    break;
                }
            }

            bool playing = tape.Playing;
            // The ROM loader starts the tape on the IN that returns to $056F, so that its 1 s
            // LD-WAIT delay can be skipped.
            if (matched && cur.Id == LID_ROM && RdW(cpu.regs.SP) == 0x056F && autoTape && !playing)
                playing = tape.EnsurePlaying();

            // These end at Spin's @Normal_INA, not @LoaderMain: never edge loaded once matched.
            if (matched && flash)
                res.BypassGeneric = cur.Id == LID_MS || cur.Id == LID_DECRYPTION || cur.Id == LID_S2;

            res.Matched = matched;
            if (!matched || !speedLoading || !playing)
                return res;

            if (tape.State == 4) {
                // A known loader in a pause gets only to the end of the pause's current 1 ms:
                // loaders use that time, so the pause isn't collapsed.
                long elapsed = tape.AimTStates - tape.ToNextEdge;
                long toEnd = 3500 - elapsed % 3500;
                if (toEnd > tape.ToNextEdge) toEnd = tape.ToNextEdge;
                if (toEnd > 0) tape.Update(toEnd);
            }

            if (!flash)
                return res;

            int state = tape.State;
            byte block = tape.BlockId;
            bool dataBlock = block == 0x10 || block == 0x11 || block == 0x12 || block == 0x14 || block == 0x19;
            if (((state == 0 || state == 3) && dataBlock)
                || cur.Id == LID_ATF || cur.Id == LID_TWISTER || cur.Id == LID_S2
                || cur.Id == LID_ZQLOADER1 || cur.Id == LID_ZQLOADER20 || cur.Id == LID_ZQLOADER21 || cur.Id == LID_ZQTURBO) {
                if (HandleLoaderSpecifics(cur.Id, ref res.Accelerated, ref res.SkipPortRead))
                    res.Handled = true;
            }
            return res;
        }

        // ------------------------------------------------------------------ shared pieces

        private bool TrySkipROMPilot(int offset) {
            ushort stackAddr = RdW(cpu.regs.SP + offset);
            if (Rd(stackAddr - 5) != 6) // LD B,n
                return false;

            uint ebx = RdD(stackAddr) & 0x00FF00FF;
            uint ecx = RdD(stackAddr + 4) & 0xFF00FFFF;

            // Two loops, a few bytes apart in the ROM, both skipped here but landing in different
            // phases. LD-LEADER counts the pilot ($3E LD A,n / $B8 CP B): the skip lands on
            // LD-SYNC, which measures the syncs itself. Alkatraz's shape is LD-SYNC ($78 LD A,B /
            // $FE $D4 CP $D4): the skip lands past the syncs, in the data.
            bool isSyncLoop = ebx == 0x00780030 && ecx == 0xCD0030D4;
            bool isRom = ebx == 0x003E0030 && (ecx == 0x240030B8 || ecx == 0x250030B8);
            if (!isRom && !isSyncLoop)
                return false;

            int cx = 10;
            byte b8 = Rd(stackAddr + 8);
            if (b8 != 176 && b8 != 32) {
                if (b8 != 194) return false;
                cx++;
            }

            // Return past the pilot loop.
            cpu.regs.SP = W(cpu.regs.SP + offset);
            ushort parentRet = RdW(cpu.regs.SP);
            WrW(cpu.regs.SP, parentRet + cx);

            // The pilot loop only leaves with its INC H / DEC H counter at 0: some loaders go on
            // to use H as their checksum.
            if (!isSyncLoop)
                cpu.regs.H = 0;

            ushort target;
            if (isSyncLoop) {
                // Its sync loop was skipped too: play the tape on to the data.
                for (int safety = 0; tape.State < 3 && safety < 1024 && tape.Playing; safety++)
                    tape.EndToneAndFinishPulse();
                // The loader resumes inside its byte loop, waiting on the edge: latch it.
                tape.MarkEdge();
                target = cpu.regs.PC;
                SkipPC(ref target, 0x28);
                cpu.regs.PC = target;
                return true;
            }

            // The pilot loop was skipped but the syncs are still the loader's to measure: end the
            // tone with the pulse playing, so both syncs are still to come.
            tape.EndToneAndFinishPulse();
            // The edge the loader was waiting on is taken, not left latched: a latched one would
            // make its next measurement, the first sync, come back as nothing.
            tape.EdgeTrigger = false;
            tape.EdgeTriggerAck = true;
            target = cpu.regs.PC;
            SkipPC(ref target, 0x28);
            cpu.regs.PC = target;
            return true;
        }

        // Spin's @Loader_B_Down: a loop counting B down to an edge, played out on the tape.
        private void HandleDownCounterLoader(int loopTsAfterFirst) {
            int loopTs = 7; // the first, partial, pass
            if (tape.EdgeTrigger) {
                tape.EdgeTriggerAck = true;
                return;
            }
            while (true) {
                if (loopTs > 0) tape.Update(loopTs);
                if (tape.EdgeTrigger) {
                    tape.EdgeTriggerAck = true;
                    break;
                }
                cpu.regs.B = (byte)(cpu.regs.B - 1);
                if (cpu.regs.B == 0) break;
                loopTs = loopTsAfterFirst;
            }
            cpu.regs.B = (byte)(cpu.regs.B + 1);
            tape.Update(-4);
        }

        private int ConsumeTapeBit() => tape.ConsumeTapeBit();

        // Flash a block to (IX) for DE bytes, leaving the block's last byte for FastByteLoader.
        private void ExecuteFlashLoader(byte flXor, byte flAdd, int ixStep) {
            ushort stackVal = RdW(cpu.regs.SP);
            int bytesToLoad = tape.DataLen - tape.DataPos - 1;
            if (bytesToLoad > 0 && cpu.regs.DE != 0) {
                // Bounded by DE and by the bytes left in the block: running on past the block
                // would read its pause and the next block's pilot as data.
                while (cpu.regs.DE > 0) {
                    int b = 0;
                    for (int i = 0; i < 8; i++)
                        b = (b << 1) | ConsumeTapeBit();
                    cpu.regs.H ^= (byte)b;
                    b = ((b ^ flXor) + flAdd) & 0xFF;
                    Wr(cpu.regs.IX, b);
                    cpu.regs.IX = W(cpu.regs.IX + ixStep);
                    cpu.regs.DE--;
                    if (--bytesToLoad <= 0) break;
                }
            }
            WrW(cpu.regs.SP, stackVal);
        }

        // Spin's @FastByteLoader: one byte from the tape into L, the edge loop skipped.
        private void FastByteLoader() {
            // A loader slow between bytes reaches this IN with the first bit's first edge gone by.
            bool edgeGone = tape.EdgeGone;
            int b = 0;
            for (int i = 0; i < 8; i++)
                b = (b << 1) | ConsumeTapeBit();
            if ((b & 1) != 0)
                cpu.regs.B = 0xFF;
            b = (b >> 1) | 0x80;
            cpu.regs.L = (byte)b;
            // The loader goes on as if it had just seen an edge, so EAR has to agree with its C
            // again: flip it, unless the bits' own edges already left it there.
            if (edgeGone) {
                tape.EdgeTrigger = false;
                tape.EdgeTriggerAck = true;
            } else
                tape.ConsumeEdge();
            ushort target = cpu.regs.PC;
            SkipPC(ref target, 0x28);
            cpu.regs.PC = target;
        }

        // ------------------------------------------------------------------ named loaders

        private bool HandleLoaderSpecifics(int loader, ref bool accelerated, ref bool skipPortRead) {
            ushort stackAddr, innerRet, callTarget, parentRet, target;
            int cx;
            switch (loader) {
                case LID_ZQLOADER1:
                case LID_ZQLOADER20:
                case LID_ZQLOADER21:
                case LID_ZQTURBO:
                    if (TryHandleZQBytes(loader)) {
                        accelerated = true;
                        skipPortRead = true;
                        return true;
                    }
                    return false;

                case LID_LONEWOLF:
                    if (TryHandleLoneWolfByte()) {
                        accelerated = true;
                        skipPortRead = true;
                        return true;
                    }
                    return false;

                case LID_MS:
                    return false; // just the normal IN

                case LID_ATF:
                    if (TryHandleATFByte()) {
                        accelerated = true;
                        skipPortRead = true;
                        return true;
                    }
                    // ATF goes on to test EAR itself: move the tape on, and let the IN complete.
                    HandleDownCounterLoader(11 + 4 + 7 + 10 + 4 + 5);
                    accelerated = true;
                    return false;

                case LID_TWISTER:
                    HandleDownCounterLoader(11 + 4 + 7 + 7 + 4 + 12 + 7);
                    accelerated = true;
                    return false;

                case LID_ROMMEL:
                case LID_WOODY:
                case LID_LOADER4:
                    return false; // edge loaded

                case LID_ALKATRAZ:
                case LID_LOADER6:
                case LID_LOADER7:
                case LID_LOADER8:
                case LID_LOADER9:
                    if (tape.State <= 1)
                        return TrySkipROMPilot(0); // the ROM's pilot loop
                    if (tape.State == 3 && cpu.regs.L == 1) { // L: the bit counter's sentinel
                        cpu.regs.SP = W(cpu.regs.SP + 2);
                        FastByteLoader();
                        return true;
                    }
                    return false;

                case LID_SPEEDLOCK:
                    return HandleSpeedlock();

                case LID_ROM:
                case LID_BLEEPLOAD:
                    if (tape.State == 3) {
                        if (loader == LID_ROM && TryHandleTedEdge()) {
                            accelerated = true;
                            return false;
                        }
                        innerRet = RdW(cpu.regs.SP);
                        if (Rd(innerRet - 3) != 0xCD) return false;
                        callTarget = RdW(innerRet - 2);
                        // Two call shapes: InnerRet+1, and Skool Daze's InnerRet+3.
                        if (callTarget != W(innerRet + 1) && callTarget != W(innerRet + 3)) return false;
                        if (cpu.regs.L != 1) return false; // not the byte's last bit yet
                        if (tape.DataPos + 1 >= tape.DataLen) return false;
                        cpu.regs.SP = W(cpu.regs.SP + 2);
                        parentRet = RdW(cpu.regs.SP);
                        // Bulk loading assumes DE counts the bytes left down to 0: true for the
                        // ROM's shape, not for Skool Daze's, whose loop leaves by another test.
                        if (callTarget == W(innerRet + 1) && ((byte)cpu.regs.AF_ & 64) != 0) {
                            if ((Rd(parentRet - 26) == 0xDD && Rd(parentRet - 25) == 0x75)
                                || (Rd(parentRet - 31) == 0xDD && Rd(parentRet - 30) == 0x75))
                                ExecuteFlashLoader(0, 0, 1);
                        }
                        FastByteLoader();
                        return true;
                    }
                    if (tape.State <= 1) {
                        stackAddr = RdW(cpu.regs.SP);
                        // LD-WAIT's 1 s and LD-LEADER skipped: on to LD-SYNC.
                        if (stackAddr == 0x056F) {
                            WrW(cpu.regs.SP, 0x058F);
                            for (int safety = 0; tape.State < 1 && safety < 100000 && tape.Playing; safety++)
                                tape.EndToneAndFinishPulse();
                            tape.MarkEdge();
                            target = cpu.regs.PC;
                            SkipPC(ref target, 0x28);
                            cpu.regs.PC = target;
                            accelerated = true;
                            return true;
                        }
                        if (TrySkipROMPilot(2)) {
                            accelerated = true;
                            return true;
                        }
                    }
                    return false;

                case LID_ULTRA:
                    if (tape.State <= 1)
                        return TrySkipROMPilot(2);
                    if (tape.State == 3 && cpu.regs.L == 1) {
                        innerRet = RdW(cpu.regs.SP);
                        if (Rd(innerRet - 3) != 0xCD) return false;
                        callTarget = RdW(innerRet - 2);
                        if (callTarget != W(innerRet + 1)) return false;
                        if (tape.DataPos + 1 >= tape.DataLen) return false;
                        cpu.regs.SP = W(cpu.regs.SP + 2);
                        // Ultracarga loads with DEC IX: descending.
                        if (((byte)cpu.regs.AF_ & 64) != 0)
                            ExecuteFlashLoader(0, 0, -1);
                        FastByteLoader();
                        return true;
                    }
                    return false;

                case LID_BASIL:
                    // Only the data: the pilot is left to edge loading.
                    if (tape.State != 3 || cpu.regs.C != 1) return false;
                    innerRet = RdW(cpu.regs.SP);
                    if (RdW(innerRet - 5) != 0x010E) return false; // LD C,1
                    if (RdD(innerRet + 2) != 0xD211CB3F) return false;
                    if (tape.DataPos + 1 >= tape.DataLen) return false;
                    {
                        int b = 0;
                        for (int i = 0; i < 8; i++)
                            b = (b << 1) | ConsumeTapeBit();
                        // L and A are Basil's counters: a 1 in bit 0 gives the longest edge, $FE.
                        if ((b & 1) != 0)
                            cpu.regs.L = 0xFE;
                        cpu.regs.A = cpu.regs.L;
                        cpu.regs.C = (byte)((b >> 1) | 0x80);
                        // Basil needs EAR left as it is.
                        tape.EdgeTrigger = false;
                        tape.EdgeTriggerAck = true;
                        // On to its RET.
                        target = cpu.regs.PC;
                        for (int safety = 0; Rd(target) != 0xC9 && safety < 4096; safety++)
                            target++;
                        cpu.regs.PC = target;
                        skipPortRead = true;
                        return true;
                    }

                case LID_DECRYPTION:
                    return HandleDecryption();

                case LID_S2:
                    return HandleS2();
            }
            return false;
        }

        private bool HandleSpeedlock() {
            ushort target;
            if (tape.State == 0) {
                ushort stackAddr = RdW(cpu.regs.SP);
                // The Speedlock pilot loop, by a longer signature.
                if (RdD(stackAddr - 7) != 0x163EC406 || RdD(stackAddr) != 0xD63EE130 || RdD(stackAddr + 4) != 0x06F238B8)
                    return false;

                // Speedlock 3 decrypts its beep tones through the same loop: only a real pilot
                // (a long pure tone of the right pulse) is skipped, or the key stream is cut short.
                bool late = RdW(stackAddr + 21) == 0x21FD;
                if (late && tape.BlockId == 0x12 && (tape.PulseLen < 2060 || tape.PulseLen > 2276 || tape.PulseToneLen < 200))
                    return false;

                int cx = late ? 76 : 61; // 61: early (Underwurlde, Knight Lore, Daley Thompson)
                ushort parentRet = RdW(cpu.regs.SP);
                WrW(cpu.regs.SP, parentRet + cx);

                // The rest of this tone is dropped, so an edge in it the loader hasn't seen goes
                // too, or EAR is left an edge out from the loader's own idea of it and the speed
                // check reads every bit wrong (Knight Lore, when an edge falls just before this IN).
                tape.TakeBackUnseenEdge();

                // On to pure data, or a pulse sequence of more than three pulses.
                for (int safety = 0; safety < 4096; safety++) {
                    tape.ForwardOneBlock();
                    if (!tape.Playing) break;
                    if (tape.BlockId == 0x14) {
                        // The loader's speed check measures this block's first half pulse: the
                        // edge is taken, not latched, or that measurement comes back as nothing.
                        tape.ConsumeEdge();
                        break;
                    }
                    if (tape.BlockId == 0x13) {
                        if (tape.PulseToneLen == 2 || tape.PulseToneLen == 3)
                            continue; // the clicking leader
                        // Two pulses, the pair the loader's own pilot routine would have measured.
                        if (tape.AimTStates > 0) tape.FinishPulse();
                        if (tape.AimTStates > 0) tape.FinishPulse();
                        tape.ConsumeEdge();
                        break;
                    }
                }

                target = cpu.regs.PC;
                SkipPC(ref target, 0x28);
                cpu.regs.PC = target;
                return true;
            }

            if (tape.State != 3 || cpu.regs.L != 1) // L: the bit counter's sentinel
                return false;
            ushort innerRet = RdW(cpu.regs.SP);
            if (Rd(innerRet - 3) != 0xCD)
                return false;
            ushort callTarget = RdW(innerRet - 2);
            if (callTarget == W(innerRet + 1)) {
                // The single edge routine
                cpu.regs.SP = W(cpu.regs.SP + 2);
                FastByteLoader();
                return true;
            }

            // The double edge routine: the bulk of the block.
            int cxVal;
            if (callTarget == RdW(innerRet + 6))
                cxVal = 8;
            else if (Rd(innerRet + 1) == 0xCD)
                cxVal = 4;
            else
                return false;

            // The hidden loading screen's attributes, as the loader would copy them (that shape only).
            if (cxVal == 4 && Rd(0x5AFF) == 0 && Rd(0x82FF) != 0)
                for (int i = 0; i < 768; i++)
                    Wr(0x5800 + i, Rd(0x8000 + i));

            // The decryption keys, from the loader's code.
            byte flXor = 0, flAdd = 0;
            if ((RdD(innerRet - 25) & 0xFFFF00FF) == 0xC6AD003E) {
                flXor = Rd(innerRet - 24);
                flAdd = Rd(innerRet - 21);
            }
            ExecuteFlashLoader(flXor, flAdd, 1);
            innerRet = RdW(cpu.regs.SP);
            WrW(cpu.regs.SP, innerRet + cxVal);
            FastByteLoader(); // the last byte
            return true;
        }

        private bool TryHandleATFByte() {
            if (tape.State != 3 || tape.BitCounter != 0x80 || cpu.regs.E != 8)
                return false;

            // ATF's byte reader calls ld_bits, whose first edge is a CALL to ld_edge: only the
            // first edge of the first bit, with both return addresses on the stack.
            ushort edgeWait = W(cpu.regs.PC - 4);
            ushort byteStart = W(edgeWait + 24);
            if (RdW(cpu.regs.SP) != W(edgeWait - 10) || RdW(cpu.regs.SP + 2) != W(byteStart + 5))
                return false;
            // LD E,8 / CALL ld_bits; JP Z,byte_bad / LD A,B; CP $ED / CCF / RL D; DEC E / JP NZ,byte_bit
            if ((RdD(byteStart) & 0x00FFFFFF) != 0x00CD081E
                || (RdD(byteStart + 5) & 0xFF0000FF) != 0x780000CA
                || RdD(byteStart + 9) != 0xCB3FEDFE
                || (RdD(byteStart + 13) & 0x00FFFFFF) != 0x00C21D12)
                return false;

            byte raw = tape.CurDataByte;
            // The byte's sixteen edges, each with the loader's border update after it. ATF's data
            // is inverted on the tape: its down-counter and CP $ED / CCF turn the bits back.
            for (int i = 0; i < 16; i++) {
                if (!tape.EdgeTrigger) {
                    long ts = tape.ToNextEdge;
                    tape.Update(ts == 0 ? 1 : ts);
                    if (!tape.EdgeTrigger) return false;
                }
                tape.EdgeTrigger = false;
                tape.EdgeTriggerAck = true;
                cpu.regs.C = (byte)(((((cpu.regs.C + 1) & 0x47) ^ 0x40) | 8) & 0xFF);
            }

            cpu.regs.D = (byte)~raw;
            cpu.regs.E = 0;
            cpu.regs.B = (raw & 1) != 0 ? (byte)0xE8 : (byte)0xF4;
            cpu.regs.A = cpu.regs.B;
            cpu.regs.F = (byte)(FlagZ | FlagN | ((raw & 0x80) == 0 ? FlagC : 0));
            ushort ret = RdW(cpu.regs.SP + 4);
            cpu.regs.SP = W(cpu.regs.SP + 6);
            cpu.regs.PC = ret;
            return true;
        }

        private bool TryHandleLoneWolfByte() {
            if (tape.State != 3) return false;

            // PC is just after the reader's first IN A,($FE): work back from there.
            ushort readerStart = W(cpu.regs.PC - 22);
            ushort byteReader = W(readerStart - 21);

            // The whole two-edge reader: only its two JP operands may move, and they must point
            // back to its own loop heads.
            if (!MatchMemory(readerStart, 0xD9, 0x7A, 0xAB, 0x57, 0xF6, 0x08, 0xD3, 0xFE,
                             0xED, 0x5F, 0xCB, 0x3F, 0xCB, 0x3F, 0x4F, 0x06,
                             0x40, 0x0C, 0x28, 0x16, 0xDB, 0xFE, 0xA0, 0xCA)) return false;
            if (RdW(readerStart + 24) != W(readerStart + 17)) return false;
            if (!MatchMemory(readerStart + 26, 0x0C, 0x28, 0x0D, 0xDB, 0xFE, 0xA0, 0xC2)) return false;
            if (RdW(readerStart + 33) != W(readerStart + 26)) return false;
            if (!MatchMemory(readerStart + 35, 0xAF, 0xED, 0x4F, 0x79, 0xD9, 0x37, 0xC9)) return false;
            if (!MatchMemory(readerStart + 42, 0xD9, 0x3A)) return false;
            if (!MatchMemory(readerStart + 46, 0xB7, 0xC8, 0x3E, 0x04, 0xC3)) return false;

            // The eight-bit wrapper, calling this reader and the cipher step checked below.
            if (!MatchMemory(byteReader, 0x1E, 0x08, 0xCD)) return false;
            if (RdW(byteReader + 3) != readerStart) return false;
            if (Rd(byteReader + 5) != 0xD6) return false;
            if (!MatchMemory(byteReader + 7, 0x3F, 0x9F, 0xCB, 0x12, 0xD9, 0xCD)) return false;
            ushort cipherStep = RdW(byteReader + 13);
            if (!MatchMemory(byteReader + 15, 0xD9, 0x1D, 0x20, 0xEF, 0x7A, 0xC9)) return false;

            if (!MatchMemory(cipherStep, 0xAC, 0xF2)) return false;
            if (RdW(cipherStep + 2) != W(cipherStep + 13)) return false;
            if (!MatchMemory(cipherStep + 4, 0x7C, 0xEE, 0x08, 0x67, 0x7D, 0xEE, 0x10, 0x6F,
                             0x37, 0xCB, 0x15, 0xCB, 0x14, 0xC9)) return false;

            // Only the wrapper's first bit, from its three known callers.
            ushort returnAddress = RdW(cpu.regs.SP);
            if (returnAddress != W(byteReader + 5)) return false;
            if ((byte)cpu.regs.DE_ != 8) return false; // EXX is in force inside the reader
            ushort parentReturn = RdW(cpu.regs.SP + 2);
            if (Rd(parentReturn - 3) != 0xCD || RdW(parentReturn - 2) != byteReader) return false;
            if (!(MatchMemory(parentReturn, 0xDD, 0x77, 0x00, 0xDD, 0x23, 0x0B, 0x78, 0xB1, 0x20, 0xF3)
                  || Rd(parentReturn) == 0xBD || Rd(parentReturn) == 0xBC)) return false;

            // The block's last byte is the real reader's.
            if (tape.DataPos + 1 >= tape.DataLen) return false;

            byte raw = 0;
            bool finalCarry = false;
            for (int i = 0; i < 8; i++) {
                byte bit = (byte)ConsumeTapeBit();
                raw = (byte)((raw << 1) | bit);
                // The wrapper's EXX / CALL CipherStep / EXX: XOR H; maybe the H and L taps and SCF;
                // RL L; RL H. Inside the reader's EXX, the active HL is that state.
                byte a = bit != 0 ? (byte)0xFF : (byte)0;
                byte h = cpu.regs.H, l = cpu.regs.L;
                int carry = 0;
                if (((a ^ h) & 0x80) != 0) {
                    h ^= 0x08;
                    l ^= 0x10;
                    carry = 1;
                }
                finalCarry = (l & 0x80) != 0;
                l = (byte)((l << 1) | carry);
                carry = finalCarry ? 1 : 0;
                finalCarry = (h & 0x80) != 0;
                h = (byte)((h << 1) | carry);
                cpu.regs.H = h;
                cpu.regs.L = l;
            }

            // All eight RL D / DEC E done on the inactive pair, the register sets swapped back,
            // and out of both CALLs with A holding the byte.
            cpu.regs.DE_ = (ushort)(raw << 8);
            cpu.exx();
            cpu.regs.A = raw;
            cpu.regs.F = (byte)(0x42 | (finalCarry ? FlagC : 0)); // the last DEC E: Z and N
            cpu.regs.SP = W(cpu.regs.SP + 4);
            cpu.regs.PC = parentReturn;
            return true;
        }

        // zqloader's turbo blocks (and Spin 2's zqturbo): a bit is one edge, a 1 when the loader
        // polled EAR more than a few times before it came. The loader's own poll timing is run
        // against the tape, so whatever the real loop would read, this reads too. Whole bytes go
        // to memory with the loader's pointer, count and checksum; the block's last byte is left
        // to the real loop. Entered at a byte's first poll only.
        private bool TryHandleZQBytes(int loader) {
            if (tape.State > 3) return false;
            ushort inAt = W(cpu.regs.PC - 2);
            ushort byteAt, bitAt, target;
            var gaps = new int[8];   // IN to IN, after bit i to the next's first
            var retOfs = new int[8]; // v1: each bit's CALL return, from ByteAt
            int poll, byteStartTs, thr, top, kMax;
            byte state;

            switch (loader) {
                case LID_ZQTURBO:
                    byteAt = W(inAt - 3);
                    bitAt = W(inAt - 1);
                    if (!MatchMemory(byteAt, 0x2E, 0x01, 0x41, 0xDB, 0xFE, 0xAA, 0xA3, 0x20, 0x03,
                                     0x10, 0xF8, 0xC9, 0x7A, 0xAC, 0x57, 0xD3, 0xFE, 0x78, 0xFE)) return false;
                    if (!MatchMemory(byteAt + 20, 0xCB, 0x15, 0xD2) || RdW(byteAt + 23) != bitAt
                        || !MatchMemory(byteAt + 25, 0x7D, 0xD9, 0x77, 0x23, 0x83, 0x07, 0x5F, 0x0B, 0x78, 0xB1, 0xD9, 0x20, 0xDA)) return false;
                    thr = Rd(byteAt + 19);
                    top = cpu.regs.C;
                    if (cpu.regs.L != 1 || cpu.regs.B != top) return false;
                    kMax = top;
                    poll = 39;
                    for (int i = 0; i < 7; i++) gaps[i] = 87;
                    gaps[7] = 157;
                    byteStartTs = 11; // LD L,1 / LD B,C
                    state = cpu.regs.D;
                    break;

                case LID_ZQLOADER20:
                case LID_ZQLOADER21:
                    bitAt = W(inAt - 3);
                    byteAt = W(bitAt - 2);
                    if (!MatchMemory(byteAt, 0x2E, 0x01, 0x42, 0x05, 0xC8, 0xDB, 0xFE, 0xA9)) return false;
                    if (loader == LID_ZQLOADER21) {
                        if (!MatchMemory(byteAt + 8, 0xA3, 0x28, 0xF8, 0x79, 0xAC, 0x4F, 0xD3, 0xFE, 0x78, 0xFE)) return false;
                        thr = Rd(byteAt + 18);
                        target = W(byteAt + 19);
                        poll = 40;
                    } else {
                        if (!MatchMemory(byteAt + 8, 0xE6, 0x40, 0x28, 0xF7, 0x79, 0xAC, 0x4F, 0xD3, 0xFE, 0x78, 0xBB)) return false;
                        thr = cpu.regs.E;
                        target = W(byteAt + 19);
                        poll = 43;
                    }
                    if (!MatchMemory(target, 0xCB, 0x15, 0xD2) || RdW(target + 3) != bitAt
                        || !MatchMemory(target + 5, 0x7D, 0xD9, 0x77, 0x83, 0x07, 0x5F, 0xED, 0xA1, 0xD9, 0xEA)
                        || RdW(target + 15) != byteAt) return false;
                    top = cpu.regs.D;
                    if (cpu.regs.L != 1 || cpu.regs.B != top - 1) return false;
                    kMax = top - 1;
                    for (int i = 0; i < 7; i++) gaps[i] = 91;
                    gaps[7] = 155;
                    byteStartTs = 20; // LD L,1 / LD B,D / DEC B / RET Z
                    state = cpu.regs.C;
                    break;

                default: {
                    // v1: in the edge routine, CALLed from a byte's first bit.
                    if (!MatchMemory(inAt - 4, 0x05, 0xC8, 0x3E, 0x7F, 0xDB, 0xFE, 0xA9, 0xE6, 0x40, 0x28, 0xF5,
                                     0x79, 0xAC, 0x4F, 0xD3, 0xFE, 0xC9)) return false;
                    target = W(inAt - 2); // the CALLs' target
                    ushort retAt = RdW(cpu.regs.SP);
                    byteAt = W(retAt - 4);
                    bitAt = 0;
                    // Eight bits: LD B,D / CALL / [RET Z] / LD A,B / CP E / RL L, with EXX /
                    // INC HL / DEC DE / EXX after the fourth and no RET Z after the fourth and
                    // eighth; then the store and JP NZ ByteAt.
                    int o = 0;
                    for (int bit = 0; bit < 8; bit++) {
                        if (!MatchMemory(byteAt + o, 0x42, 0xCD) || RdW(byteAt + o + 2) != target) return false;
                        retOfs[bit] = o + 4;
                        o += 4;
                        if (bit != 3 && bit != 7) {
                            if (Rd(byteAt + o) != 0xC8) return false;
                            o++;
                        }
                        if (!MatchMemory(byteAt + o, 0x78, 0xBB, 0xCB, 0x15)) return false;
                        o += 4;
                        if (bit == 3) {
                            if (!MatchMemory(byteAt + o, 0xD9, 0x23, 0x1B, 0xD9)) return false;
                            o += 4;
                        }
                    }
                    if (!MatchMemory(byteAt + o, 0x7D, 0xD9, 0x77, 0x81, 0x4F, 0x7A, 0xB3, 0xD9, 0xC2)
                        || RdW(byteAt + o + 9) != byteAt) return false;
                    thr = cpu.regs.E;
                    top = cpu.regs.D;
                    if (cpu.regs.B != top) return false;
                    kMax = top;
                    poll = 50;
                    for (int i = 0; i < 7; i++) gaps[i] = 111;
                    gaps[3] = 126; // EXX / INC HL / DEC DE / EXX
                    gaps[7] = 151; // the store
                    byteStartTs = 28; // LD B,D / CALL / LD A,$7F
                    state = cpu.regs.C;
                    break;
                }
            }

            int Count16() => loader == LID_ZQLOADER1 ? cpu.regs.DE_ : cpu.regs.BC_;
            if (Count16() < 2) return false; // the block's last byte: the real loop's

            int BitOf(int p) {
                int b = (loader == LID_ZQLOADER20 || loader == LID_ZQLOADER21) ? top - p : top - (p - 1);
                return b < thr ? 1 : 0;
            }
            void SetState(byte v) {
                if (loader == LID_ZQTURBO) cpu.regs.D = v; else cpu.regs.C = v;
            }

            byte toggle = cpu.regs.H;
            bool expect = (state & 0x40) != 0;
            int k = 1, curBit = 0;
            byte v = 0;

            // Back to the real loop before poll k + 1 of the bit (v: its bits so far).
            void HandBackMidBit() {
                cpu.regs.L = (byte)((1 << curBit) | v);
                switch (loader) {
                    case LID_ZQTURBO:
                        cpu.regs.B = (byte)(top - k);
                        cpu.regs.PC = inAt;
                        tape.Update(poll);
                        break;
                    case LID_ZQLOADER20:
                    case LID_ZQLOADER21:
                        cpu.regs.B = (byte)(top - k);
                        cpu.regs.PC = W(bitAt + 1);
                        tape.Update(poll - 9); // DEC B / RET Z first
                        break;
                    default:
                        // Inside the edge routine's CALL from this bit's place.
                        cpu.regs.L = v;
                        cpu.regs.B = (byte)(top - (k - 1));
                        WrW(cpu.regs.SP, byteAt + retOfs[curBit]);
                        cpu.regs.PC = W(inAt - 4);
                        tape.Update(poll - 16); // DEC B / RET Z / LD A,$7F first
                        break;
                }
            }

            // From here the tape moves: whatever happens, the loader gets the state the real one would be in.
            while (Count16() > 1) {
                v = 0;
                for (curBit = 0; curBit < 8; curBit++) {
                    k = 1;
                    while (tape.EarLevel == expect) {
                        if (k >= kMax - 1) {
                            SetState(state);
                            HandBackMidBit();
                            return true;
                        }
                        tape.Update(poll);
                        k++;
                    }
                    expect = !expect;
                    state ^= toggle;
                    tape.EdgeTrigger = false;
                    tape.EdgeTriggerAck = true;
                    v = (byte)((v << 1) | BitOf(k));
                    if (curBit == 3 && loader == LID_ZQLOADER1) {
                        cpu.regs.HL_ = W(cpu.regs.HL_ + 1);
                        cpu.regs.DE_ = W(cpu.regs.DE_ - 1);
                    }
                    if (curBit < 7) tape.Update(gaps[curBit]);
                }

                // The store.
                Wr(cpu.regs.HL_, v);
                if (loader == LID_ZQLOADER1) {
                    cpu.regs.BC_ = (ushort)((cpu.regs.BC_ & 0xFF00) | (byte)(cpu.regs.BC_ + v));
                } else {
                    cpu.regs.HL_ = W(cpu.regs.HL_ + 1);
                    byte e = (byte)((byte)cpu.regs.DE_ + v);
                    e = (byte)((e << 1) | (e >> 7));
                    cpu.regs.DE_ = (ushort)((cpu.regs.DE_ & 0xFF00) | e);
                    cpu.regs.BC_ = W(cpu.regs.BC_ - 1);
                }
                if (Count16() > 1) tape.Update(gaps[7]);
            }

            // Back to the real loop at the last byte's start.
            SetState(state);
            cpu.regs.L = 1;
            if (loader == LID_ZQLOADER1)
                cpu.regs.SP = W(cpu.regs.SP + 2); // out of the CALL
            cpu.regs.PC = byteAt;
            tape.Update(gaps[7] - byteStartTs);
            return true;
        }

        // ------------------------------------------------------------------ Technician Ted

        private void RunTedTablePass(byte count) {
            byte b_ = count;
            do {
                byte old = Rd(cpu.regs.HL_);
                byte c_ = (byte)cpu.regs.BC_;
                byte a = (byte)(old - c_);
                bool carry = old < c_;
                Wr(cpu.regs.HL_, a);
                a = (byte)cpu.regs.HL_;
                if (carry) {
                    Wr(cpu.regs.DE_, a);
                    cpu.regs.DE_ = (ushort)((cpu.regs.DE_ & 0xFF00) | (byte)((byte)cpu.regs.DE_ + 1));
                }
                a = (byte)(a + 6);
                cpu.regs.HL_ = (ushort)((cpu.regs.HL_ & 0xFF00) | a);
                b_--;
                cpu.regs.BC_ = (ushort)((b_ << 8) | (byte)cpu.regs.BC_);
            } while (b_ != 0);
        }

        private bool AdvanceTedEdge() {
            if (!tape.EdgeTrigger) {
                long ts = tape.ToNextEdge;
                tape.Update(ts == 0 ? 1 : ts);
            }
            if (!tape.EdgeTrigger) return false;
            tape.EdgeTrigger = false;
            tape.EdgeTriggerAck = true;
            return true;
        }

        // The machine reached TrapPC: Ted's reader, called from its main data loop,
        // run as its result. False when it should run as written.
        public bool OnTrapPC() {
            if (tape.State != 3 || cpu.regs.PC != tedReaderStart
                || RdD(tedReaderStart) != 0x806B11D9 || RdD(tedReaderStart + 4) != 0x06802921 || RdD(tedReaderStart + 8) != 0x8351CD05)
                return false;

            ushort ret = RdW(cpu.regs.SP);
            // Only its main eight-bit data loop: setup and verification call it differently.
            if (ret != W(tedReaderStart + 0x41)) return false;

            bool one = (tape.CurDataByte & tape.BitCounter) != 0;
            cpu.regs.DE_ = 0x806B;
            cpu.regs.HL_ = 0x8029;
            RunTedTablePass(5);
            if (!AdvanceTedEdge()) return false;
            RunTedTablePass(6);
            if (!AdvanceTedEdge()) return false;

            cpu.regs.B = one ? (byte)0xD0 : (byte)0xC0;
            cpu.regs.A = cpu.regs.C;
            cpu.regs.F |= FlagC;
            cpu.regs.SP = W(cpu.regs.SP + 2);
            cpu.regs.PC = ret;
            // The reader's M1 fetches, for R, without spending its loading time.
            cpu.regs.R = (byte)((cpu.regs.R & 0x80) | ((cpu.regs.R + 150) & 0x7F));
            return true;
        }

        private bool TryHandleTedEdge() {
            if (tape.State != 3) return false;

            // Ted relocates the ROM's single-edge tail with two screen decompression passes
            // around it. The first edge returns into that reader, the second to its caller.
            ushort edgeStart = W(cpu.regs.PC - 6);
            ushort readerStart = W(edgeStart - 0x22);
            if (RdD(readerStart) != 0x806B11D9 || RdD(readerStart + 4) != 0x06802921 || RdD(readerStart + 8) != 0x8351CD05
                || RdD(edgeStart) != 0x7F3EC804 || RdD(edgeStart + 4) != 0xD01FFEDB || RdD(edgeStart + 8) != 0x2820E6A9)
                return false;

            if (TrapPC < 0) {
                tedReaderStart = readerStart;
                TrapPC = readerStart;
            }

            ushort retAddr = RdW(cpu.regs.SP);
            bool firstEdge = retAddr == W(edgeStart - 0x16);
            bool secondEdge = Rd(retAddr - 3) == 0xCD && RdW(retAddr - 2) == readerStart;
            if (!firstEdge && !secondEdge) return false;

            ushort outerRet = firstEdge ? RdW(cpu.regs.SP + 2) : retAddr;
            // Ted's data-bit callers all return to JP NC,$0000; its last loop has CP (HL) / NOP / INC D.
            if ((RdD(outerRet) & 0x00FFFFFF) != 0x000000D2 && RdD(outerRet) != 0x3E1400BE)
                return false;

            if (firstEdge && (RdD(outerRet) & 0x00FFFFFF) == 0x000000D2) {
                tedBit = (tape.CurDataByte & tape.BitCounter) != 0 ? 1 : 0;
                tedBitValid = true;
            }

            // Take exactly the edge this call waits for.
            if (tape.EdgeTrigger) {
                tape.EdgeTrigger = false;
                tape.EdgeTriggerAck = true;
            } else {
                long ts = tape.ToNextEdge;
                tape.Update(ts == 0 ? 1 : ts);
                if (!tape.EdgeTrigger) return false;
                tape.EdgeTrigger = false;
                tape.EdgeTriggerAck = true;
            }

            if (secondEdge && tedBitValid) {
                // The caller's CP $CB,B: above $CB is a one. Only the timing count is replaced.
                cpu.regs.B = tedBit != 0 ? (byte)0xD0 : (byte)0xC0;
                tedBitValid = false;
            }
            if (secondEdge && RdD(outerRet) == 0x3E1400BE) {
                // The post-data loop takes an edge pair only with the count above $EA.
                cpu.regs.B = 0xF0;
                tedBitValid = false;
            }
            return true;
        }

        // ------------------------------------------------------------------ decryptors

        // Spin's relocS3: past the Speedlock decryption beeps.
        private bool HandleDecryption() {
            int i2 = 0;
            if (RdW(cpu.regs.PC + 810) == 16384) i2 = 805;
            else if (RdW(cpu.regs.PC + 839) == 16384) i2 = 834;
            if (i2 == 0) return false;

            ushort callTarget = RdW(cpu.regs.PC + i2 + 26);
            cpu.regs.PC = W(cpu.regs.PC + i2);
            int tst = i2 == 805 ? callTarget - (cpu.regs.PC - 135) : callTarget - (cpu.regs.PC - 141);
            for (int i = 65535; i >= 60000; i--)
                Wr(i, Rd((i - tst) & 0xFFFF));
            cpu.regs.PC = W(cpu.regs.PC + tst);
            return true;
        }

        // Spin's decryptS2: Speedlock 2 and 3's decryption run out at once.
        private bool HandleS2() {
            ushort start = RdW(cpu.regs.PC + 0x1F);
            for (int i = start; i <= 65535; i++) {
                cpu.regs.E ^= Rd(i);
                Wr(i, cpu.regs.E);
            }

            cpu.regs.PC = W(cpu.regs.PC + 42);
            if (Rd(cpu.regs.PC) != 17) return false;
            start = RdW(cpu.regs.PC + 1);
            for (int i = start; i <= 65535; i++)
                Wr(i, (((Rd(i) ^ (i >> 8)) - (i & 255)) ^ (i & 255)) + (i >> 8));

            cpu.regs.PC = W(cpu.regs.PC + 16);
            if (Rd(cpu.regs.PC) != 33) return false;
            byte flXor = Rd(cpu.regs.PC + 10);
            start = RdW(cpu.regs.PC + 1);
            for (int i = start; i <= 65535; i++)
                Wr(i, Rd(i) ^ flXor);

            cpu.regs.PC = W(cpu.regs.PC + 23);
            if (Rd(cpu.regs.PC) != 33) return false;
            cpu.regs.PC = W(cpu.regs.PC + 11);
            if (Rd(cpu.regs.PC) != 33) return false;

            start = RdW(cpu.regs.PC + 1);
            for (int i = start; i <= 65535; i++) {
                int b = Rd(i);
                Wr(i, (b >> 1) | ((b & 1) << 7)); // RRC
            }

            cpu.regs.PC = W(cpu.regs.PC + 12);

            // Find where it goes on to.
            int callTarget = 0;
            for (int i = cpu.regs.PC; i <= 65500; i++) {
                if (RdW(i) == 0x21DD && RdD(i + 4) == 0xCD001111 && RdD(i + 10) == 0x82FF32AF) {
                    if (Rd(i - 3) == 49) { callTarget = i - 3; break; }        // Speedlock 2
                    if (RdW(i - 0x15) == 0x31F3) { callTarget = i - 0x15; break; } // Speedlock 3
                }
            }
            if (callTarget == 0) return false;

            int reloc = 0;
            if (RdW(callTarget - 507) == 0x14F3)
                reloc = RdW(callTarget + 0x1F) - ((callTarget - 507) & 0xFFFF);
            else if (RdW(callTarget - 485) == 0x3EF3) // early Speedlock 2 (Mag Max)
                reloc = RdW(callTarget + 0x1F) - ((callTarget - 485) & 0xFFFF);
            else if (RdW(callTarget - 464) == 0x3EF3) // early Speedlock 3 (Gryzor)
                reloc = RdW(callTarget + 0x31) - ((callTarget - 464) & 0xFFFF);
            if (reloc == 0) return false;

            for (int i = 0; i < 6912; i++)
                Wr(16384 + i, 0);
            for (int i = 65535; i >= cpu.regs.PC; i--)
                Wr(i + reloc, Rd(i));
            cpu.regs.PC = W(callTarget + reloc);
            Wr(0x82FF, 0);

            // On to the pilot tone.
            int j;
            do {
                while ((tape.AimTStates < 2060 || tape.AimTStates > 2276) && tape.AimTStates > 0)
                    tape.Update(tape.ToNextEdge);
                j = 0;
                while (tape.AimTStates >= 2060 && tape.AimTStates <= 2276 && tape.AimTStates > 0) {
                    tape.Update(tape.ToNextEdge);
                    if (++j > 223) break;
                }
            } while (j <= 223 && tape.AimTStates != 0);
            return true;
        }
    }
}
