// µPD765A floppy disk controller, as wired into the ZX Spectrum +3.
//
// A C# port of fdc765 by Andre Leiradella (https://github.com/leiradel/fdc765, MIT licence),
// itself a C port of the freeware fdc765.dll x86 assembly source the Windows build of Zero used.
//
// Ported the way fdc765 itself was: as directly as possible, so the behaviour stays the DLL's.
// The C kept the x86 registers (a union per register), the flags (zero and carry), a small stack,
// and one big switch standing in for the assembly's computed jumps; all of that is kept here, in
// unsafe code over unmanaged memory so the pointer arithmetic carries straight across. Read it
// next to the C, not as idiomatic C#.
//
// Where this differs from the C, it is to fix a problem the C port introduced or to stay inside
// memory C# will not let us overrun. Each such place is marked "Port fix".
//
// One thing the C does not have is marked "Port addition": a count of drive selections, for the
// emulator's drive lights.
//
// Copyright 2025 Andre Leiradella (fdc765 C port). MIT licence:
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software
// and associated documentation files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions: The above copyright notice
// and this permission notice shall be included in all copies or substantial portions of the
// Software. THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable CS0162 // unreachable code: the fall-through gotos mirror the C exactly
#pragma warning disable CS0164 // unreferenced labels: every case keeps its label, as in the C

namespace Peripherals
{
    public sealed unsafe class Fdc765 : IDisposable
    {
        // ------------------------------------------------------------------ data structures (fdc765.h)

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DiskInfoBlock
        {
            public fixed byte DiskInfo[34];
            public fixed byte pad1[14];
            public byte NumTracks;
            public byte NumSides;
            public ushort TrackSize;
            public fixed byte pad2[204];
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TrackInfoBlock
        {
            public fixed byte TrackData[13];       // "Track-Info\r\n"
            public fixed byte pad1[3];
            public byte TrackNum;
            public byte SideNum;
            public fixed byte pad2[2];
            public byte SectorSize;
            public byte NumSectors;
            public byte GapLength;
            public byte FillerByte;
            public fixed byte SectorInfoList[232]; // 8 bytes a sector
            public fixed byte SectorData[32768];
        }

        // The C's bool fields are bytes here: the state machine assigns and compares them as numbers.
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct DiskUnit
        {
            public byte* DiskArrayPtr;     // the whole disk image, in memory
            public nuint DiskArrayLen;     // bytes of it that are the image (what is written back)
            public nuint DiskAllocLen;     // bytes allocated: the image plus a zeroed margin
            public byte DiskInserted;
            public byte ContentsChanged;   // written to since inserted
            public byte WriteProtect;
            public byte EDSK;              // was an extended DSK (converted to plain DSK on insert)
            public byte DriveStateChanged;
            public byte CTK;               // physical track the head is over
            public byte CHEAD;             // head in use for this command
            public byte CSR;               // sector the head is over
            public byte SeekDone;
            public DiskInfoBlock DiskBlock;
            public TrackInfoBlock TrackBlock;
        }

        private const uint FDCReadData = 0, FDCReadDeletedData = 1, FDCReadTrack = 2;
        private const uint FDCWriteData = 0, FDCWriteDeletedData = 1;

        // Port fix: the C sizes this 16384, but a sector can be 32768 (N = 8) and the random-data
        // path copies a whole sector into it.
        private const uint RandomDataSize = 32768;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Controller
        {
            public byte* FDC_RCVDLoc;
            public byte* FDC_SENDLoc;
            public uint CurrentSectorSize;
            public byte* CurrentSectorData;
            public byte* CurrentSectorInfo;
            public uint SectorToCPUReturn;
            public uint CPUToSectorReturn;

            public uint FDCVector;          // where the next data-port access resumes
            public uint FDCReturn;
            public uint FDCBufferReturn;

            public uint CurrentFDDArrayPtr;

            public DiskUnit* UnitPtr;
            public DiskUnit* SeekUnitPtr;

            public uint BytesSaved;

            public uint PhysicalSectorSize;
            public uint AvailableSectorData;
            public uint MultipleSectorPick;

            public ushort FDCCmdPC;
            public ushort FDC_RCVDCnt;
            public ushort FDC_SENDCnt;

            public byte SelectedUnit;

            public byte LED;
            public byte MainStatusReg;      // read from port 2FFD
            public byte Byte_3FFD;          // byte through the data register (port 3FFD)
            public byte ST0;
            public byte ST1;                // ST1 and ST2 are read and written as a pair: keep them together
            public byte ST2;
            public byte ST3;
            public byte SeekResult;
            public byte TSEError;
            public byte ST2DAMBit;

            public byte IndexHoleCount;
            public byte LastFDCCmd;
            public byte FDCRandomSeed;
            public byte DAM_Mask;
            public byte SectorsRead;
            public byte OverRunCounter;
            public byte MotorOffTimer;
            public byte NewMotorState;
            public byte MotorState;

            public byte DTL_BytesSent;
            public byte ValidTrack;
            public byte OverRunTest;
            public byte OverRunError;

            public byte MultiSectorRead;

            public byte SectorsTransferred;
            public byte NumParams;
            public byte NumResults;
            public byte OriginalR;
            public byte RetCSR0;

            public byte CurrentSectorNumber;
            public byte DskRndMethod;

            public DiskUnit FDDUnit0;
            public DiskUnit FDDUnit1;

            public uint ReadMode;
            public uint WriteMode;

            // FDCCommandByte must be followed directly by FDCParameters: the state machine writes
            // results "past" the command byte into the parameters, as the C does.
            public byte FDCCommandByte;
            public fixed byte FDCParameters[32];
            public fixed byte FDCResults[32];
            public fixed byte FDCRandomData[(int)RandomDataSize];
        }

        // ------------------------------------------------------------------ the emulated x86 (fdc765.c)

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        private struct Reg
        {
            [FieldOffset(0)] public byte l;
            [FieldOffset(1)] public byte h;
            [FieldOffset(0)] public ushort x;
            [FieldOffset(0)] public uint e;
            [FieldOffset(0)] public byte* u8;
            [FieldOffset(0)] public DiskUnit* disk;
        }

        [InlineArray(8)]
        private struct RegStack { private Reg _first; }

        private struct Context
        {
            public Reg eax, ebx, ecx, edx;
            public Reg esi, edi, esp;
            public RegStack stack;
            public bool zero, carry;
        }

        private static void PUSH(Context* c, Reg r) => c->stack[(int)c->esp.e++] = r;
        private static Reg POP(Context* c) => c->stack[(int)--c->esp.e];

        private static void AND(Context* c, ref byte l, uint r) { l = (byte)(l & r); c->zero = l == 0; c->carry = false; }
        private static void AND(Context* c, ref ushort l, uint r) { l = (ushort)(l & r); c->zero = l == 0; c->carry = false; }
        private static void AND(Context* c, ref uint l, uint r) { l &= r; c->zero = l == 0; c->carry = false; }
        private static void OR(Context* c, ref byte l, uint r) { l = (byte)(l | r); c->zero = l == 0; c->carry = false; }
        private static void OR(Context* c, ref uint l, uint r) { l |= r; c->zero = l == 0; c->carry = false; }
        private static void XOR(Context* c, ref byte l, uint r) { l = (byte)(l ^ r); c->zero = l == 0; c->carry = false; }
        private static void XOR(Context* c, ref ushort l, uint r) { l = (ushort)(l ^ r); c->zero = l == 0; c->carry = false; }
        private static void XOR(Context* c, ref uint l, uint r) { l ^= r; c->zero = l == 0; c->carry = false; }
        private static void SHL(Context* c, ref byte l, int r) => l = (byte)(l << r);
        private static void SHL(Context* c, ref uint l, int r) => l <<= r;
        private static void SHR(Context* c, ref byte l, int r) => l = (byte)(l >> r);
        private static void INC(Context* c, ref byte v) { v++; c->zero = v == 0; }
        private static void INC(Context* c, ref uint v) { v++; c->zero = v == 0; }
        private static void INC(Context* c, ref byte* v) { v++; c->zero = v == null; }
        private static void DEC(Context* c, ref byte v) { v--; c->zero = v == 0; }
        private static void DEC(Context* c, ref ushort v) { v--; c->zero = v == 0; }
        private static void DEC(Context* c, ref uint v) { v--; c->zero = v == 0; }
        private static void ADD(Context* c, ref byte l, uint r) { l = (byte)(l + r); c->zero = l == 0; c->carry = l < r; }
        private static void ADD(Context* c, ref uint l, uint r) { l += r; c->zero = l == 0; c->carry = l < r; }
        private static void SUB(Context* c, ref uint l, uint r) { c->carry = l < r; l -= r; c->zero = l == 0; }
        private static void CMP(Context* c, uint l, uint r) { c->zero = l == r; c->carry = l < r; }
        private static void TEST(Context* c, uint l, uint r) { c->zero = (l & r) == 0; c->carry = false; }

        private static ushort READW(byte* p) => (ushort)(p[0] | p[1] << 8);
        private static void WRITEW(byte* p, uint v) { p[0] = (byte)v; p[1] = (byte)(v >> 8); }
        private static uint READDW(byte* p) => (uint)(p[0] | p[1] << 8 | p[2] << 16 | p[3] << 24);
        private static void WRITEDW(byte* p, uint v) { p[0] = (byte)v; p[1] = (byte)(v >> 8); p[2] = (byte)(v >> 16); p[3] = (byte)(v >> 24); }

        private static void rep_movsb(Context* c)
        {
            Buffer.MemoryCopy(c->esi.u8, c->edi.u8, c->ecx.e, c->ecx.e);
            c->edi.u8 += c->ecx.e;
            c->esi.u8 += c->ecx.e;
            c->ecx.e = 0;
        }

        private static readonly byte* TrackInfoSignature = AllocSignature();
        // Port fix: somewhere harmless to point at when a damaged image asks for a track it
        // does not contain, instead of reading off the end of it.
        private static readonly byte* EmptyTrack = (byte*)NativeMemory.AllocZeroed((nuint)sizeof(TrackInfoBlock) + 0x10000);

        private static byte* AllocSignature()
        {
            byte* p = (byte*)NativeMemory.AllocZeroed(16);
            ReadOnlySpan<byte> sig = "Track-Info"u8;
            for (int i = 0; i < sig.Length; i++) p[i] = sig[i];
            return p;
        }

        // ------------------------------------------------------------------ instance

        /// <summary>
        /// The controller. Port fix: the C reached it through whichever register held it, and in
        /// one place (the random sector data) carried on doing so after reusing that register as a
        /// buffer pointer. There is only ever one, so every access goes through this instead.
        /// </summary>
        private Controller* C;
        private readonly FileStream[] _files = new FileStream[2];
        private readonly string[] _fileNames = new string[2];

        /// <summary>Called when a read or write command starts (the DLL's "active" callback).</summary>
        public Action ActiveCallback { get; set; }

        /// <summary>Called with the command and parameter bytes as each command is taken.</summary>
        public Action<byte[]> CommandCallback { get; set; }

        public Fdc765()
        {
            if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException("fdc765 assumes a little-endian host.");
            C = (Controller*)NativeMemory.AllocZeroed((nuint)sizeof(Controller));
            Context ctx = default;
            LowLevelInitialise(&ctx);
        }

        public void Dispose()
        {
            if (C == null) return;
            EjectDisk(0);
            EjectDisk(1);
            NativeMemory.Free(C);
            C = null;
        }

        // ------------------------------------------------------------------ high level interface

        public void SetRandomMethod(byte rndMethod)
        {
            if (rndMethod == 255 || rndMethod <= 2) C->DskRndMethod = rndMethod;
        }

        public void SetMotorState(byte value)
        {
            C->NewMotorState = (byte)((value & 8) >> 3);
            // when the motor is turned off we still need a simple timer method where we will accept
            // further FDC read commands. some disks require this behaviour, such as Scrabble Deluxe
            if (C->MotorState == 1 && C->NewMotorState == 0) C->MotorOffTimer = 3;
            C->MotorState = C->NewMotorState;
        }

        public bool MotorOn => C->MotorState != 0;

        public byte StatusPortRead()
        {
            if (C->OverRunTest == 1)
            {
                if (C->OverRunCounter == 0)
                {
                    Context ctx = default;
                    C->OverRunTest = 0;
                    C->OverRunError = 1;
                    AND(&ctx, ref C->ST0, 0x3f);
                    OR(&ctx, ref C->ST0, 0x40);          // AT
                    OR(&ctx, ref C->ST1, 0x10);          // OverRun (Lost Data)
                    AND(&ctx, ref C->MainStatusReg, 0xdf); // clear Execution mode
                    // do after clearing execution mode in MSR, fixes Italia 1990
                    C->FDCVector = C->FDCReturn;
                    Run(&ctx, C->FDCReturn);
                }
                else C->OverRunCounter--;
            }
            return C->MainStatusReg;
        }

        public byte DataPortRead()
        {
            byte msr = (byte)(C->MainStatusReg & 192);
            if (msr != 192) return msr;
            Context ctx = default;
            Run(&ctx, C->FDCVector);
            return C->Byte_3FFD;
        }

        public void DataPortWrite(byte dataByte)
        {
            C->Byte_3FFD = dataByte;
            if ((C->MainStatusReg & 192) != 128) return;
            Context ctx = default;
            Run(&ctx, C->FDCVector);
        }

        public void ResetDevice()
        {
            Context ctx = default;
            LowLevelInitialise(&ctx);
        }

        /// <summary>
        /// Insert a DSK or EDSK image. Opened for writing when it can be, and read-only (write
        /// protected) when it cannot; an EDSK is always write protected, as in the DLL.
        /// </summary>
        public bool InsertDisk(string fileName, byte unit)
        {
            EjectDisk(unit); // close any open disk on this unit
            unit &= 1;
            DiskUnit* d = Unit(unit);
            d->EDSK = 0;
            d->DiskInserted = 0;
            d->ContentsChanged = 0;
            d->WriteProtect = 0;

            FileStream fs;
            try { fs = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read); }
            catch (Exception)
            {
                d->WriteProtect = 1;
                try { fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); }
                catch (Exception) { return false; }
            }

            long length = fs.Length;
            if (length < 0x100 || length > 16 * 1024 * 1024) { fs.Dispose(); return false; }

            // Port fix: a margin of zeroes past the image. The C allocated it exactly, but reads a
            // whole track at a time and the EDSK conversion looks one entry past its tables.
            nuint alloc = (nuint)length + 0x20000;
            byte* image = (byte*)NativeMemory.AllocZeroed(alloc);
            var span = new Span<byte>(image, (int)length);
            int read = 0;
            while (read < length)
            {
                int n = fs.Read(span.Slice(read));
                if (n <= 0) break;
                read += n;
            }
            if (read != length) { NativeMemory.Free(image); fs.Dispose(); return false; }

            bool plain = span.StartsWith("MV - CPC"u8);
            bool extended = span.StartsWith("EXTENDED"u8);
            if (!plain && !extended) { NativeMemory.Free(image); fs.Dispose(); return false; }

            _files[unit] = fs;
            _fileNames[unit] = fileName;
            d->DiskArrayPtr = image;
            d->DiskArrayLen = (nuint)length;
            d->DiskAllocLen = alloc;
            d->DiskInserted = 1;
            d->DriveStateChanged = 1;

            if (extended && !EDsk2Dsk(d)) { EjectDisk(unit); return false; }

            Buffer.MemoryCopy(d->DiskArrayPtr, &d->DiskBlock, sizeof(DiskInfoBlock), sizeof(DiskInfoBlock));
            d->ContentsChanged = 0;
            Context ctx = default;
            LowLevelInitialise(&ctx);
            return true;
        }

        public void EjectDisk(byte unit)
        {
            unit &= 1;
            DiskUnit* d = Unit(unit);
            if (_files[unit] != null)
            {
                WriteCurrentDisk(unit);
                if (d->DiskArrayPtr != null)
                {
                    NativeMemory.Free(d->DiskArrayPtr);
                    d->DiskArrayPtr = null;
                }
                _files[unit].Dispose();
                _files[unit] = null;
                _fileNames[unit] = null;
                d->DiskInserted = 0;
                d->DriveStateChanged = 1;
            }
            Context ctx = default;
            LowLevelInitialise(&ctx);
        }

        public bool DiskInserted(byte unit) => Unit((byte)(unit & 1))->DiskInserted != 0;

        public bool WriteProtected(byte unit) => Unit((byte)(unit & 1))->WriteProtect != 0;

        /// <summary>The image file in a drive, or null.</summary>
        public string DiskFileName(byte unit) => _fileNames[unit & 1];

        /// <summary>Write a drive's image back to its file if the Spectrum has changed it.</summary>
        public void Flush(byte unit) => WriteCurrentDisk((byte)(unit & 1));

        /// <summary>
        /// The drive the controller last addressed: by a read or write, or failing that a seek or
        /// recalibrate. The +3's one motor line runs both drives, so this is what tells them apart.
        /// </summary>
        public int ActiveUnit
        {
            get
            {
                DiskUnit* unit = C->UnitPtr != null ? C->UnitPtr : C->SeekUnitPtr;
                return unit == &C->FDDUnit1 ? 1 : 0;
            }
        }

        /// <summary>Current track of a drive, for display.</summary>
        public byte CurrentTrack(byte unit) => Unit((byte)(unit & 1))->CTK;

        // ------------------------------------------------------------------ low level functions

        private DiskUnit* Unit(byte unit) => (unit & 1) == 0 ? &C->FDDUnit0 : &C->FDDUnit1;

        private void LowLevelInitialise(Context* ctx)
        {
            C->MotorState = 0;
            C->FDCVector = case_FDC_NewCommand;
            C->MainStatusReg = 0x80;
            C->FDDUnit0.SeekDone = 0;
            C->FDDUnit1.SeekDone = 0;
            C->FDDUnit0.CTK = 0;
            C->FDDUnit1.CTK = 0;
            C->FDDUnit0.CHEAD = 0;
            C->FDDUnit1.CHEAD = 0;
            OR(ctx, ref C->ST3, 0x20);
            C->FDCRandomSeed = 0;
        }

        private void FDCCommandCallback(Context* ctx, byte numCmdBytes)
        {
            Action<byte[]> callback = CommandCallback;
            if (callback == null) return;
            var bytes = new byte[numCmdBytes];
            byte* p = &C->FDCCommandByte;
            for (int i = 0; i < numCmdBytes; i++) bytes[i] = p[i];
            callback(bytes);
        }

        private void GetUnitPtr(Context* ctx, byte unit)
        {
            _selections[unit & 1]++;   // Port addition: see Selections
            ctx->eax.disk = Unit(unit);
        }

        private readonly int[] _selections = new int[2];

        /// <summary>
        /// Port addition, for the drive lights: how many commands have selected this drive (a read,
        /// write, Read ID, seek, recalibrate or Sense Drive Status). Between commands the µPD765
        /// polls the drives, which on the +3 (only US0 wired) flips the select line between A and
        /// B; during a command it holds the one selected. The controller has no timing, so a count
        /// that has moved says that drive was held selected since it was last looked at.
        /// </summary>
        public int Selections(int unit) => _selections[unit & 1];

        private void SetFastDisk(Context* ctx) => ActiveCallback?.Invoke();

        private void WriteCurrentDisk(byte unit)
        {
            DiskUnit* d = Unit(unit);
            FileStream fs = _files[unit];
            if (fs == null || d->WriteProtect != 0 || d->ContentsChanged == 0) return;
            // Port fix: the C wrote from wherever the file position was left, which after reading the
            // image on insert is its end, so the disk was appended to itself instead of updated.
            fs.Position = 0;
            fs.Write(new ReadOnlySpan<byte>(d->DiskArrayPtr, (int)d->DiskArrayLen));
            fs.Flush();
            d->ContentsChanged = 0;
        }

        /// <summary>Convert an extended DSK in place to a plain DSK with every track the largest size.</summary>
        private bool EDsk2Dsk(DiskUnit* d)
        {
            byte* src = d->DiskArrayPtr;
            d->EDSK = 1;
            byte numTracks = src[0x30];
            byte numSides = src[0x31];
            int tracks = numTracks * numSides;

            // walk the track size table (from $34) for the largest; each entry counts 256 bytes.
            // The loops run one past the end, as the C does; the zeroed margin covers it.
            uint maxTrackLen = 0;
            for (int i = 0; i <= tracks; i++)
                if (src[0x34 + i] > maxTrackLen) maxTrackLen = src[0x34 + i];
            maxTrackLen <<= 8;

            nuint alloc = (nuint)(tracks * maxTrackLen + 256 + 100000); // plus the DLL's safe space
            byte* dsk = (byte*)NativeMemory.AllocZeroed(alloc);

            Buffer.MemoryCopy(src + 0x22, dsk + 0x22, 14, 14);   // $22 to $2f
            dsk[0x30] = numTracks;
            dsk[0x31] = numSides;
            WRITEW(dsk + 0x32, maxTrackLen);

            uint edskOffset = 0x100;
            for (uint f = 0; f <= (uint)tracks; f++)
            {
                uint dskOffset = f * maxTrackLen + 0x100;
                if (edskOffset + 0x100 > d->DiskAllocLen) break; // Port fix: past the image and its margin
                byte numSectors = src[edskOffset + 0x15];
                byte trackBlocks = src[f + 0x34];
                if (trackBlocks == 0) continue;
                if (numSectors > 0)
                {
                    // the track info block and sector info list
                    uint infoLen = (uint)numSectors * 8 + 0x18;
                    if (dskOffset + infoLen > alloc) break;
                    Buffer.MemoryCopy(src + edskOffset, dsk + dskOffset, infoLen, infoLen);

                    // then the sectors, which in an EDSK can each be a different length
                    dskOffset += 0x100;
                    uint dOffset = edskOffset + 0x100;
                    for (uint g = 0; g <= numSectors; g++)
                    {
                        uint sectorLen = READW(src + g * 8 + 30 + edskOffset);
                        if (sectorLen > 0)
                        {
                            if (dOffset + sectorLen > d->DiskAllocLen || dskOffset + sectorLen > alloc) break;
                            Buffer.MemoryCopy(src + dOffset, dsk + dskOffset, sectorLen, sectorLen);
                            dOffset += sectorLen;
                        }
                        dskOffset += sectorLen;
                    }
                }
                edskOffset += (uint)trackBlocks << 8;
            }

            NativeMemory.Free(src);
            d->DiskArrayPtr = dsk;
            d->DiskAllocLen = alloc;
            d->WriteProtect = 1;
            return true;
        }

        private void Run(Context* ctx, uint label)
        {
        again:
            switch (label)
            {
            // --------------------------------------------------------------------------------
            case case_FDC_NewCommand: label_FDC_NewCommand:
                C->NumParams = 0;
                C->NumResults = 0;
                C->SectorsTransferred = 0;

                ctx->eax.l = C->Byte_3FFD;
                C->FDCCommandByte = ctx->eax.l;

                ctx->eax.l = C->FDCCommandByte;
                AND(ctx, ref ctx->eax.e, 31);                 // mask command bits

                C->LastFDCCmd = ctx->eax.l;    // for debugging purposes only

                // a Sense Interrupt Status command must be sent after a Seek or Recalibrate interrupt,
                // otherwise the FDC will consider the next command to be an Invalid Command.

                // Breaks New Zealand Story
                // ctx->ecx.e = C->FDDUnit0;
                // .if     (ctx->eax.l != 8) && (ctx->ecx.e->TFDDUnit.SeekDone == 1)
                // ctx->ecx.e->TFDDUnit.SeekDone = 0;
                // goto label_FDC_Invalid;
                // .endif

                switch (ctx->eax.l & 31) {
                    case  0: goto label_FDC_Invalid;
                    case  1: goto label_FDC_Invalid;
                    case  2: goto label_FDC_ReadTrack;               // 2   LED on
                    case  3: goto label_FDC_Specify;                 // 3
                    case  4: goto label_FDC_SenseDriveStatus;        // 4
                    case  5: goto label_FDC_WriteData;               // 5   LED on
                    case  6: goto label_FDC_ReadData;                // 6   LED on
                    case  7: goto label_FDC_Recalibrate;             // 7
                    case  8: goto label_FDC_SenseInterruptStatus;    // 8
                    case  9: goto label_FDC_WriteDeletedData;        // 9   LED on
                    case 10: goto label_FDC_ReadSectorID;            // 10  LED on
                    case 11: goto label_FDC_Invalid;
                    case 12: goto label_FDC_ReadDeletedData;         // 12  LED on
                    case 13: goto label_FDC_FormatTrack;             // 13  LED on
                    case 14: goto label_FDC_Invalid;
                    case 15: goto label_FDC_Seek;                    // 15
                    case 16: goto label_FDC_Version;                 // 16
                    case 17: goto label_FDC_ScanEqual;               // 17
                    case 18: goto label_FDC_Invalid;
                    case 19: goto label_FDC_Invalid;
                    case 20: goto label_FDC_Invalid;
                    case 21: goto label_FDC_Invalid;
                    case 22: goto label_FDC_Invalid;
                    case 23: goto label_FDC_Invalid;
                    case 24: goto label_FDC_Invalid;
                    case 25: goto label_FDC_ScanLowOrEqual;          // 25
                    case 26: goto label_FDC_Invalid;
                    case 27: goto label_FDC_Invalid;
                    case 28: goto label_FDC_Invalid;
                    case 29: goto label_FDC_ScanHighOrEqual;         // 29
                    case 30: goto label_FDC_Invalid;
                    case 31: goto label_FDC_Invalid;
                }

                goto label_InitFDC; // fall through
            case case_InitFDC: label_InitFDC:
                C->LED = 0;
                C->FDCVector = case_FDC_NewCommand;
                C->OverRunTest = 0;
                C->OverRunError = 0;
                C->MainStatusReg = 128; // FDC is ready for a new command

                // call command callback with no cmd executing
                C->FDCCommandByte = 0; // no command executing
                FDCCommandCallback(ctx, 1);
                return;

            // this subroutine traps standard errors in FDC commands
            // only used for commands which take full 9 byte command bytes
            // CC0,CC1,C,H,R,N,EOT,GPL,DTL bytes.

            case case_TrapStandardErrors: label_TrapStandardErrors:
                C->TSEError = 0;

                GetUnitPtr(ctx, C->FDCParameters[0]); // CC1
                C->UnitPtr = ctx->eax.disk;  // ptr to currently selected unit
                ctx->ebx.disk = ctx->eax.disk;

                C->ST0 = 0;
                C->ST1 = 0;

                CMP(ctx, ctx->ebx.disk->DiskInserted, 0);
                if (ctx->zero) goto label_TSE_NotReady;              // no disk in drive
                CMP(ctx, C->MotorState, 1);
                if (ctx->zero) goto label_TSE_1;                     // jump if motor is running

                CMP(ctx, C->MotorOffTimer, 0);     // if the motor off timer is zero
                if (ctx->zero) goto label_TSE_NotReady;              // then the drive is not ready

                DEC(ctx, ref C->MotorOffTimer);        // else decrement the motor timer
                goto label_TSE_1;                     // and accept the command

            case case_TSE_NotReady: label_TSE_NotReady:

                C->TSEError = 1;
                OR(ctx, ref C->ST0, 0x8);       // FDD is in the not-ready state
                AND(ctx, ref C->ST3, 0xdf);
                goto label_TSE_Quit;

            case case_TSE_1: label_TSE_1:
                AND(ctx, ref C->ST3, 0xfb);
                AND(ctx, ref C->ST0, 0xfb);
                ctx->eax.l = C->FDCParameters[0]; // CC1
                SHR(ctx, ref ctx->eax.l, 2);                         // HD >> bit 0
                AND(ctx, ref ctx->eax.l, 1);                         // mask off the HD value
                ctx->ebx.disk->CHEAD = ctx->eax.l;               // set current head for this unit
                CMP(ctx, ctx->eax.l, 0);
                if (ctx->zero) goto label_TSE_2;                        // branch forward if H = 0

                OR(ctx, ref C->ST3, 0x4);
                OR(ctx, ref C->ST0, 0x4);
                CMP(ctx, ctx->ebx.disk->DiskBlock.NumSides, 2);   // HD can be 1 if the disk is double sided
                if (ctx->zero) goto label_TSE_2;

                AND(ctx, ref C->ST3, 0xfb);
                AND(ctx, ref C->ST0, 0xfb);
                ctx->ebx.disk->CHEAD = 0;                // else current head is reset to zero
                C->TSEError = 1;          // and signal the error
                OR(ctx, ref C->ST0, 0x8);
                // fallthrough

                goto label_TSE_2; // fall through
            case case_TSE_2: label_TSE_2:
                // any further tests required will go here
                // fallthrough
        
                goto label_TSE_Quit; // fall through
            case case_TSE_Quit: label_TSE_Quit:
                CMP(ctx, C->TSEError, 1);
                if (!ctx->zero) goto label_TSE_4;
                OR(ctx, ref C->ST0, 0x40);   // abnormal termination of command
                // fallthrough

                goto label_TSE_4; // fall through
            case case_TSE_4: label_TSE_4:
                return;

            // ######################################################################

            case case_FDC_ReadData: label_FDC_ReadData:
                C->ReadMode = FDCReadData;
                C->DAM_Mask = 0;        // set Data Address mask for normal data sectors

                // entry point for READ_DATA, READ_DELETED_DATA and READ_TRACK commands
                // receives the parameters for the FDC commands and then jumps to the main sector reading code
                // fallthrough

                goto label_FDC_ReadDataEntry; // fall through
            case case_FDC_ReadDataEntry: label_FDC_ReadDataEntry:
                SetFastDisk(ctx);

                C->FDCReturn = case_FDC_ReadData1;
                ctx->ecx.x = 8;                                   // expect 8 bytes
                goto label_ReceiveCommandBytes;

            case case_FDC_ReadData1: label_FDC_ReadData1:
                FDCCommandCallback(ctx, 9);

                ctx->eax.l = C->FDCParameters[3];     // R param
                C->OriginalR = ctx->eax.l;                     // preserve R value

                C->FDCBufferReturn = case_FDC_ReadData2;
                goto label_ReadSectorData;                         // jump to the main sector reading code

            case case_FDC_ReadData2: label_FDC_ReadData2:
                Run(ctx, case_InitFDC);
                return;

            // ######################################################################

            case case_FDC_ReadDeletedData: label_FDC_ReadDeletedData:
                C->ReadMode = FDCReadDeletedData;
                C->DAM_Mask = 64;       // set Data Address mask for deleted data sectors
                goto label_FDC_ReadDataEntry;

            // ######################################################################

            case case_FDC_WriteData: label_FDC_WriteData:
                C->WriteMode = FDCWriteData;
                C->DAM_Mask = 0;
                // fallthrough

                goto label_FDC_WriteDataEntry; // fall through
            case case_FDC_WriteDataEntry: label_FDC_WriteDataEntry:
                SetFastDisk(ctx);

                C->FDCReturn = case_FDC_WriteData1;
                ctx->ecx.x = 8;                   // expect 8 bytes
                goto label_ReceiveCommandBytes;

            case case_FDC_WriteData1: label_FDC_WriteData1:
                FDCCommandCallback(ctx, 9);

                ctx->eax.l = C->FDCParameters[3]; // R param
                C->OriginalR = ctx->eax.l;                // preserve R value

                C->FDCBufferReturn = case_FDC_WriteData2;
                goto label_WriteSectorData;

            case case_FDC_WriteData2: label_FDC_WriteData2:
                Run(ctx, case_InitFDC);
                return;


            // ######################################################################

            case case_FDC_WriteDeletedData: label_FDC_WriteDeletedData:
                C->WriteMode = FDCWriteDeletedData;
                C->DAM_Mask = 64;
                goto label_FDC_WriteDataEntry;

            // ######################################################################

            case case_FDC_ReadTrack: label_FDC_ReadTrack:
                C->ReadMode = FDCReadTrack;
                goto label_FDC_ReadDataEntry;

            // ######################################################################

            case case_FDC_ReadSectorID: label_FDC_ReadSectorID:
                SetFastDisk(ctx);

                C->FDCReturn = case_FDC_ReadSectorID1;
                ctx->ecx.x = 1;                   // expect 1 byte
                goto label_ReceiveCommandBytes;

            case case_FDC_ReadSectorID1: label_FDC_ReadSectorID1:
                // FDCCommandCallback(ctx, 2);

                OR(ctx, ref C->MainStatusReg, 16);       // FDC is busy
                C->ST2 = 0;
                Run(ctx, case_TrapStandardErrors);
                CMP(ctx, C->TSEError, 1);
                if (!ctx->zero) goto label_FDC_RSSkip1;

                ctx->esi.u8 = &ctx->ebx.disk->TrackBlock.SectorInfoList[0];
                goto label_ReadID_Results;

            case case_FDC_RSSkip1: label_FDC_RSSkip1:
                Run(ctx, case_ReadCurrTrack);

                CMP(ctx, C->ValidTrack, 1);
                if (ctx->zero) goto label_Read_ID_1;

                // this is not a valid track number
                C->FDCResults[0] = 0;    // ST0
                C->FDCResults[1] = 1;    // ST1 = MA (1)
                ctx->eax.l = ctx->ebx.disk->CTK;                      // C
                C->FDCResults[3] = ctx->eax.l;
                goto label_ReadID_SendResults;

            case case_Read_ID_1: label_Read_ID_1:
                ctx->esi.u8 = &ctx->ebx.disk->TrackBlock.SectorInfoList[0];
                ctx->ecx.u8 = &ctx->ebx.disk->TrackBlock.SectorData[0];

                // after a seek/recalibrate we return the sector ID for the first disk sector
                // on the new track until RetSCR0 counts down to 0
                CMP(ctx, C->RetCSR0, 0);
                if (ctx->zero) goto label_RSNoSec0;

                DEC(ctx, ref C->RetCSR0);
                ctx->ebx.disk->CSR = 0;
                // fallthrough

                goto label_RSNoSec0; // fall through
            case case_RSNoSec0: label_RSNoSec0:
                ctx->edx.l = ctx->ebx.disk->CSR;
                INC(ctx, ref ctx->edx.l);
                CMP(ctx, ctx->edx.l, ctx->ebx.disk->TrackBlock.NumSectors);
                if (ctx->carry) goto label_RSJmp1;
                XOR(ctx, ref ctx->edx.l, ctx->edx.l);
                // fallthrough

                goto label_RSJmp1; // fall through
            case case_RSJmp1: label_RSJmp1:
                ctx->ebx.disk->CSR = ctx->edx.l;
                OR(ctx, ref ctx->edx.l, ctx->edx.l);
                if (ctx->zero) goto label_FDC_RdScDone;
                // fallthrough

                goto label_FDC_RdScL1; // fall through
            case case_FDC_RdScL1: label_FDC_RdScL1:
                Run(ctx, case_SkipNextSector);
                DEC(ctx, ref ctx->edx.l);
                if (!ctx->zero) goto label_FDC_RdScL1;
                // fallthrough

                goto label_FDC_RdScDone; // fall through
            case case_FDC_RdScDone: label_FDC_RdScDone:
                AND(ctx, ref C->ST0, 0x3f);  // normal termination
                // fallthrough

                goto label_ReadID_Results; // fall through
            case case_ReadID_Results: label_ReadID_Results:
                AND(ctx, ref C->ST0, 0xfc);
                ctx->eax.l = C->FDCParameters[0];
                AND(ctx, ref ctx->eax.l, 3);
                OR(ctx, ref C->ST0, ctx->eax.l);

                ctx->edx.u8 = &C->FDCResults[0];
                ctx->eax.l = C->ST0;               // ST0
                ctx->edx.u8[0] = ctx->eax.l;
                ctx->eax.x = READW(ctx->esi.u8 + 4);  // ST1, ST2

                // Epyx 21 fix,
                // we don't read the sector data in a read sector ID command,
                // so we can't have a CRC error in the sector data reported.
                AND(ctx, ref ctx->eax.l, 255 - 32);           // ST1
                AND(ctx, ref ctx->eax.h, 255 - 32);           // ST2

                WRITEW(ctx->edx.u8 + 1, ctx->eax.x);
                ctx->eax.e = READDW(ctx->esi.u8);  // C,H,R,N
                WRITEDW(ctx->edx.u8 + 3, ctx->eax.e);
                // fallthrough

                goto label_ReadID_SendResults; // fall through
            case case_ReadID_SendResults: label_ReadID_SendResults:
                // clone CHRN from Result bytes into the Command bytes in order to return Result phase bytes early in a Command Callback
                ctx->eax.e = READDW(&C->FDCResults[3]);
                WRITEDW(&C->FDCCommandByte + 2, ctx->eax.e);

                FDCCommandCallback(ctx, 6);   // 2 command bytes + CHRN early Results phase bytes);

                // return Results phase bytes to CPU
                C->FDCReturn = case_FDC_ReadSectorID2;
                ctx->esi.u8 = &C->FDCResults[0];
                ctx->ecx.x = 7;             // 7 bytes of result data
                C->NumResults = 7;
                goto label_FDC_SendData;    // transfer data to CPU

            case case_FDC_ReadSectorID2: label_FDC_ReadSectorID2:
                Run(ctx, case_InitFDC);
                return;

            // ######################################################################

            case case_FDC_FormatTrack: label_FDC_FormatTrack:
                Run(ctx, case_InitFDC);
                return;

            // ######################################################################

            case case_FDC_ScanEqual: label_FDC_ScanEqual:
                Run(ctx, case_InitFDC);
                return;

            // ######################################################################

            case case_FDC_ScanLowOrEqual: label_FDC_ScanLowOrEqual:
                Run(ctx, case_InitFDC);
                return;

            // ######################################################################

            case case_FDC_ScanHighOrEqual: label_FDC_ScanHighOrEqual:
                Run(ctx, case_InitFDC);
                return;

            // ######################################################################

            // Command = 7
            case case_FDC_Recalibrate: label_FDC_Recalibrate:
                AND(ctx, ref C->ST0, 0xdf);
                C->FDCReturn = case_FDC_Recalibrate1;
                ctx->ecx.x = 1;                   // expect 1 byte
                goto label_ReceiveCommandBytes;

            case case_FDC_Recalibrate1: label_FDC_Recalibrate1:
                FDCCommandCallback(ctx, 2);

                GetUnitPtr(ctx, C->FDCParameters[0]);
                C->SeekUnitPtr = ctx->eax.disk;
                ctx->ebx.disk = ctx->eax.disk;
                // fallthrough

                goto label_FDC_Recalibrate2; // fall through
            case case_FDC_Recalibrate2: label_FDC_Recalibrate2:
                ctx->ebx.disk->CTK = 0;
                OR(ctx, ref C->ST3, 0x10);
                AND(ctx, ref C->ST0, 0x3f);
                OR(ctx, ref C->ST0, 0x20);
                C->SeekResult = 0x20;         // Normal Termination of Recalibrate Command
                // fallthrough

                goto label_FDC_RecExit; // fall through
            case case_FDC_RecExit: label_FDC_RecExit:
                ctx->ebx.disk->SeekDone = 1;

                Run(ctx, case_InitFDC);
                return;

            // ######################################################################

            // Command = 8
            case case_FDC_SenseInterruptStatus: label_FDC_SenseInterruptStatus:
                FDCCommandCallback(ctx, 1);

                OR(ctx, ref C->MainStatusReg, 16);       // FDC is busy
                ctx->esi.u8 = &C->FDCResults[0];

                ctx->ebx.disk = C->SeekUnitPtr;       // drive unit which was issued a Seek/Recalibrate command
                if (ctx->ebx.disk == null) {
                    ctx->ebx.disk = &C->FDDUnit0;            // safeguard
                }

                CMP(ctx, ctx->ebx.disk->SeekDone, 1);      // interrupt caused by seek completion?
                if (!ctx->zero) goto label_SeekNotDone;

                C->RetCSR0 = 9;
                ctx->ebx.disk->SeekDone = 0;
                ctx->eax.l = C->SeekResult;          // result from seek command
                ctx->ecx.l = ctx->ebx.disk->CHEAD;
                SHL(ctx, ref ctx->ecx.l, 2);
                OR(ctx, ref ctx->eax.l, ctx->ecx.l);
                ctx->ecx.x = 2;                   // 2 result bytes after a seek
                C->NumResults = 2;
                goto label_FDC_SenseCont;

            case case_SeekNotDone: label_SeekNotDone:
                ctx->ecx.x = 1;                   // else only 1 result byte (ST0)
                C->NumResults = 1;
                CMP(ctx, ctx->ebx.disk->DriveStateChanged, 1);
                if (!ctx->zero) goto label_NoDiskChange;

                ctx->ebx.disk->DriveStateChanged = 0;
                ctx->eax.l = 0xc0;                 // Ready Line Changed state, either polarity
                goto label_FDC_SenseCont;

            case case_NoDiskChange: label_NoDiskChange:
                ctx->eax.l = 0x80;
                // fallthrough

                goto label_FDC_SenseCont; // fall through
            case case_FDC_SenseCont: label_FDC_SenseCont:
                ctx->edx.disk = &C->FDDUnit1;
                if (ctx->ebx.disk == ctx->edx.disk) {      // offset FDDUnit1
                    OR(ctx, ref ctx->eax.l, 1);        // set unit 1 bit in result
                }

                C->ST0 = ctx->eax.l;
                ctx->esi.u8[0] = ctx->eax.l;
                ctx->eax.l = ctx->ebx.disk->CTK;
                ctx->esi.u8[1] = ctx->eax.l;

                C->FDCReturn = case_FDC_SenseInterruptStatus1;
                goto label_FDC_SendData;

            case case_FDC_SenseInterruptStatus1: label_FDC_SenseInterruptStatus1:
                Run(ctx, case_InitFDC);
                return;

            // ######################################################################

            case case_FDC_Specify: label_FDC_Specify:
                C->FDCReturn = case_FDC_Specify1;
                ctx->ecx.x = 2;                   // expect 2 bytes
                goto label_ReceiveCommandBytes;

            case case_FDC_Specify1: label_FDC_Specify1:
                FDCCommandCallback(ctx, 3);

                AND(ctx, ref C->ST0, 0x3f);

                Run(ctx, case_InitFDC);
                return;

            // ######################################################################

            case case_FDC_SenseDriveStatus: label_FDC_SenseDriveStatus:
                C->FDCReturn = case_FDC_SenseDriveStatus1;
                ctx->ecx.x = 1;                   // expect 1 byte
                goto label_ReceiveCommandBytes;

            case case_FDC_SenseDriveStatus1: label_FDC_SenseDriveStatus1:
                FDCCommandCallback(ctx, 2);

                OR(ctx, ref C->MainStatusReg, 16);       // FDC is busy
                C->ST3 = 0x40;                // assume write-protected here

                GetUnitPtr(ctx, C->FDCParameters[0]);
                ctx->ebx = ctx->eax;

                ctx->eax.l = C->FDCParameters[0];
                AND(ctx, ref ctx->eax.l, 3);
                OR(ctx, ref C->ST3, ctx->eax.l);                 // Unit
                // fallthrough

                goto label_FDC_SDS1; // fall through
            case case_FDC_SDS1: label_FDC_SDS1:
                ctx->ecx.l = ctx->ebx.disk->CHEAD;
                SHL(ctx, ref ctx->ecx.l, 2);
                OR(ctx, ref C->ST3, ctx->ecx.l);

                CMP(ctx, ctx->ebx.disk->CTK, 0);
                if (!ctx->zero) goto label_FDC_SDS2;
                OR(ctx, ref C->ST3, 0x10);    // Track 0 signal
                // fallthrough

                goto label_FDC_SDS2; // fall through
            case case_FDC_SDS2: label_FDC_SDS2:
                CMP(ctx, ctx->ebx.disk->DiskInserted, 1);
                if (!ctx->zero) goto label_FDC_SDSResults;

                CMP(ctx, C->MotorState, 1);     // Motor on?
                if (!ctx->zero) goto label_FDC_SDSResults;
                OR(ctx, ref C->ST3, 0x20);    // Drive is Ready

                CMP(ctx, ctx->ebx.disk->WriteProtect, 1);
                if (ctx->zero) goto label_FDC_SDSResults;
                AND(ctx, ref C->ST3, 0xbf);          // clear write-protected bit
                // fallthrough

                goto label_FDC_SDSResults; // fall through
            case case_FDC_SDSResults: label_FDC_SDSResults:
                ctx->esi.u8 = &C->FDCResults[0];
                ctx->eax.l = C->ST3;
                ctx->esi.u8[0] = ctx->eax.l;

                C->FDCReturn = case_FDC_SenseDriveStatus2;
                ctx->ecx.x = 1;
                C->NumResults = 1;
                goto label_FDC_SendData;

            case case_FDC_SenseDriveStatus2: label_FDC_SenseDriveStatus2:
                AND(ctx, ref C->ST0, 0x3f);

                Run(ctx, case_InitFDC);
                return;

            // ######################################################################

            case case_FDC_Seek: label_FDC_Seek:
                C->FDCReturn = case_FDC_Seek1;
                ctx->ecx.x = 2;                   // expect 2 bytes
                goto label_ReceiveCommandBytes;

            case case_FDC_Seek1: label_FDC_Seek1:
                FDCCommandCallback(ctx, 3);

                C->SeekResult = 0x20;         // normal termination of seek

                GetUnitPtr(ctx, C->FDCParameters[0]);
                C->SeekUnitPtr = ctx->eax.disk;
                ctx->ebx = ctx->eax;

                ctx->eax.l = C->FDCParameters[1];  // C parameter
                CMP(ctx, ctx->eax.l, ctx->ebx.disk->DiskBlock.NumTracks);
                if (ctx->carry) goto label_STrk_Valid;            // jump if seeking a valid cylinder

                C->SeekResult = 0x60;         // abnormal termination of seek

                // if seeking beyond the final cylinder then stop at final cylinder
                ctx->eax.l = ctx->ebx.disk->DiskBlock.NumTracks;
                DEC(ctx, ref ctx->eax.l);
                // fallthrough

                goto label_STrk_Valid; // fall through
            case case_STrk_Valid: label_STrk_Valid:
                ctx->ebx.disk->CTK = ctx->eax.l;           // update current track head is over
                ctx->ebx.disk->CSR = 0;
                AND(ctx, ref C->ST0, 0x1b);    // Normal termination, clear HD bit
                OR(ctx, ref C->ST0, 0x20);    // seek complete

                ctx->eax.l = ctx->ebx.disk->CHEAD;
                SHL(ctx, ref ctx->eax.l, 2);
                OR(ctx, ref C->ST0, ctx->eax.l);           // set HD bit

                ctx->ebx.disk->SeekDone = 1;

                Run(ctx, case_InitFDC);
                return;

            // ######################################################################

            case case_FDC_Version: label_FDC_Version:
                FDCCommandCallback(ctx, 1);

                ctx->esi.u8 = &C->FDCResults[0];
                ctx->eax.l = 0x80;    // $80 = uPD765A identifier
                C->ST0 = ctx->eax.l;
                ctx->esi.u8[0] = ctx->eax.l;

                OR(ctx, ref C->MainStatusReg, 16);       // FDC is busy
                C->FDCReturn = case_FDC_Version1;
                ctx->ecx.x = 1;
                C->NumResults = 1;
                goto label_FDC_SendData;

            case case_FDC_Version1: label_FDC_Version1:
                Run(ctx, case_InitFDC);
                return;

            // ######################################################################

            case case_FDC_Invalid: label_FDC_Invalid:
                FDCCommandCallback(ctx, 1);

                ctx->esi.u8 = &C->FDCResults[0];
                ctx->eax.l = C->ST0;
                AND(ctx, ref ctx->eax.l, 0x3f);
                OR(ctx, ref ctx->eax.l, 0x80);
                C->ST0 = ctx->eax.l;
                ctx->esi.u8[0] = ctx->eax.l;

                OR(ctx, ref C->MainStatusReg, 16);       // FDC is busy
                C->FDCReturn = case_FDC_Invalid1;
                ctx->ecx.x = 1;
                C->NumResults = 1;
                goto label_FDC_SendData;

            case case_FDC_Invalid1: label_FDC_Invalid1:
                Run(ctx, case_InitFDC);
                return;

            // ######################################################################

            // Low Level Floppy Disc Controller Functions

            // ######################################################################

            // CX = number of command bytes to receive
            case case_ReceiveCommandBytes: label_ReceiveCommandBytes:
                ctx->edx.u8 = &C->FDCParameters[0];             // address for command bytes
                C->NumParams = ctx->ecx.l;                     // number of FDC command paramater bytes to receive
                // fallthrough

            // CPU->FDC
            // FDC receives CX bytes from Z80, and stores them at [EDX]
            // byte received is in Byte_3FFD var

                goto label_FDC_ReceiveData; // fall through
            case case_FDC_ReceiveData: label_FDC_ReceiveData:
                C->FDC_RCVDCnt = ctx->ecx.x;
                C->FDC_RCVDLoc = ctx->edx.u8;
                C->FDCVector = case_FDC_ReceiveDataLoop;
                AND(ctx, ref C->MainStatusReg, 0x3f);
                OR(ctx, ref C->MainStatusReg, 0x80);
                return;

            case case_FDC_ReceiveDataLoop: label_FDC_ReceiveDataLoop:
                ctx->edx.u8 = C->FDC_RCVDLoc;
                ctx->eax.l = C->Byte_3FFD;
                *ctx->edx.u8 = ctx->eax.l;
                INC(ctx, ref C->FDC_RCVDLoc);
                DEC(ctx, ref C->FDC_RCVDCnt);
                if (ctx->zero) goto label_FDC_ReceiveDataEnd;
                return;

            case case_FDC_ReceiveDataEnd: label_FDC_ReceiveDataEnd:
                ctx->eax.e = C->FDCReturn;
                C->FDCVector = ctx->eax.e;
                { label = ctx->eax.e; goto again; }

            // ######################################################################

            // FDC->CPU
            // FDC sends CX bytes from [ESI] to Z80
            case case_FDC_SendData: label_FDC_SendData:
                C->FDC_SENDCnt = ctx->ecx.x;
                C->FDC_SENDLoc = ctx->esi.u8;
                C->FDCVector = case_FDC_SendData1;
                OR(ctx, ref C->MainStatusReg, 0xc0);
                return;

            case case_FDC_SendData1: label_FDC_SendData1:
                ctx->esi.u8 = C->FDC_SENDLoc;
                ctx->eax.l = *ctx->esi.u8;
                C->Byte_3FFD = ctx->eax.l;
                INC(ctx, ref C->FDC_SENDLoc);
                DEC(ctx, ref C->FDC_SENDCnt);
                if (!ctx->zero) goto label_FDC_SendData2;

                ctx->eax.e = C->FDCReturn;
                C->FDCVector = ctx->eax.e;
                { label = ctx->eax.e; goto again; }

            case case_FDC_SendData2: label_FDC_SendData2:
                C->OverRunTest = 1;
                C->OverRunCounter = 64;
                return;

            // ######################################################################

            // End of Cylinder (bit 7 in ST1) is set if:
            // 1. sector data is read completely. (i.e. no other errors occur like no data)
            // 2. sector being read is same specified by EOT
            // 3. terminal count is not received

            case case_ReadSectorData: label_ReadSectorData:
                XOR(ctx, ref ctx->eax.l, ctx->eax.l);
                C->ST0 = ctx->eax.l;
                C->ST1 = ctx->eax.l;
                C->ST2 = ctx->eax.l;
                C->SectorsRead = ctx->eax.l;  // for Read Track only

                ctx->eax.l = C->FDCParameters[3];          // R
                CMP(ctx, ctx->eax.l, C->FDCParameters[5]); // EOT
                C->MultiSectorRead = (byte)(ctx->zero ? 0 : 1);   // if R != EOT then this is a multi-sector transfer

                Run(ctx, case_TrapStandardErrors);
                CMP(ctx, C->TSEError, 1);
                if (!ctx->zero) goto label_ReadSectorData_1;

                ctx->eax.e = READDW(&C->FDCParameters[1]);   // copy C,H,R,N from command
                WRITEDW(&C->FDCResults[3], ctx->eax.e);      // into results buffer
                goto label_ReturnSectorRWResults;         // and exit returning the error

            case case_ReadSectorData_1: label_ReadSectorData_1:
                OR(ctx, ref C->MainStatusReg, 32 + 16);      // enter Execution mode + busy

                C->IndexHoleCount = 0;
                C->DTL_BytesSent = 0;

                Run(ctx, case_ReadCurrTrack);

                CMP(ctx, C->ReadMode, FDCReadTrack);
                if (!ctx->zero) goto label_InitReadSector;

                ctx->ebx.disk->CSR = 0xFF;  // Read Track cmd always starts from first physical sector
                // fallthrough

            // locate physical sector the head is currently over
                goto label_InitReadSector; // fall through
            case case_InitReadSector: label_InitReadSector:
                ctx->esi.u8 = &ctx->ebx.disk->TrackBlock.SectorInfoList[0];
                ctx->ecx.u8 = &ctx->ebx.disk->TrackBlock.SectorData[0];

                ctx->edx.l = ctx->ebx.disk->CSR;
                INC(ctx, ref ctx->edx.l);
                CMP(ctx, ctx->edx.l, ctx->ebx.disk->TrackBlock.NumSectors);
                if (ctx->carry) goto label_IRSJmp1;
                XOR(ctx, ref ctx->edx.l, ctx->edx.l);
                // fallthrough

                goto label_IRSJmp1; // fall through
            case case_IRSJmp1: label_IRSJmp1:
                ctx->ebx.disk->CSR = ctx->edx.l;
                OR(ctx, ref ctx->edx.l, ctx->edx.l);
                if (ctx->zero) goto label_IRSDone;
                // fallthrough

                goto label_IRSLoop; // fall through
            case case_IRSLoop: label_IRSLoop:
                Run(ctx, case_SkipNextSector);
                DEC(ctx, ref ctx->edx.l);
                if (!ctx->zero) goto label_IRSLoop;
                // fallthrough

                goto label_IRSDone; // fall through
            case case_IRSDone: label_IRSDone:
                C->CurrentSectorData = ctx->ecx.u8;    // ptr to this sector's data
                C->CurrentSectorInfo = ctx->esi.u8;    // ptr to this sector's info
                Run(ctx, case_GetSectorSize);
                C->CurrentSectorSize = ctx->eax.e;    // this sector's size (in bytes)
                C->ST2DAMBit = 0;              // assume no DAM error at this stage
                // fallthrough

                goto label_LocateReadSector; // fall through
            case case_LocateReadSector: label_LocateReadSector:
                ctx->esi.u8 = C->CurrentSectorInfo;

                ctx->eax.e = READDW(ctx->esi.u8);           // copy CHRN from the DSK's sectorinfo
                WRITEDW(&C->FDCResults[3], ctx->eax.e);   // to Results buffer

                ctx->eax.x = READW(&ctx->esi.u8[4]);  // ctx->eax.l=ST1, ctx->eax.h=ST2
                AND(ctx, ref ctx->eax.x, 0x2125);             // DE, ND or MA in ST1, DD or MD in ST2
                WRITEW(&C->ST1, ctx->eax.x);    // DAM in ST2 ignored at this point

                CMP(ctx, C->ValidTrack, 1);      // basically - is this track formatted?
                if (ctx->zero) goto label_Read_CompareSectorID;

                // this is not a valid track number
                C->ST0 = 0x40;           // AT
                C->ST1 = 1;             // MA
                ctx->eax.l = ctx->ebx.disk->CTK;
                CMP(ctx, ctx->eax.l, C->FDCParameters[1]);    // C
                if (ctx->zero) goto label_FDCR_SameC;
                ctx->eax.h = 0x10;            // No Cylinder
                CMP(ctx, ctx->eax.l, 0xff);
                if (!ctx->zero) goto label_FDCR_NotBadC;
                ctx->eax.h = 2;              // Bad Cylinder
                // fallthrough

                goto label_FDCR_NotBadC; // fall through
            case case_FDCR_NotBadC: label_FDCR_NotBadC:
                OR(ctx, ref C->ST2, ctx->eax.h);
                // fallthrough

                goto label_FDCR_SameC; // fall through
            case case_FDCR_SameC: label_FDCR_SameC:
                ctx->eax.e = READDW(&C->FDCParameters[1]); // copy CHRN from the FDC command
                ctx->eax.l = ctx->ebx.disk->CTK;                         // replacing C with the current physical track number
                WRITEDW(&C->FDCResults[3], ctx->eax.e);    // to Results buffer
                goto label_ReturnSectorRWResults;         // and exit returning the error

            // is this the sector that we're searching for?
            case case_Read_CompareSectorID: label_Read_CompareSectorID:
                CMP(ctx, C->ReadMode, FDCReadTrack);
                if (ctx->zero) goto label_Rd_TransferData;                     // transfer anyway if Read Track command

                ctx->eax.e = READDW(ctx->esi.u8);                 // ctx->eax.e = CHRN from sectorinfo
                CMP(ctx, ctx->eax.e, READDW(&C->FDCParameters[1])); // is this the sector we are looking for?
                if (!ctx->zero) goto label_SkipReadSector;

                // if this is a multisector read command then we ignore disk errors per sector
                CMP(ctx, C->MultiSectorRead, 1);
                if (!ctx->zero) goto label_Rd_NotMS1;    // branch if not a multisector read cmd

                WRITEW(&C->ST1, 0x4000);
                goto label_Read_CheckDAM;

            // if we already have an error here then we ignore the DAM bit in the results phase
            // the DAM bit already in ST2 (from DSK file) is ignored in this test

            case case_Rd_NotMS1: label_Rd_NotMS1:
                XOR(ctx, ref ctx->eax.l, ctx->eax.l);
                C->ST2DAMBit = ctx->eax.l;
                OR(ctx, ref ctx->eax.l, C->ST1);
                OR(ctx, ref ctx->eax.l, C->ST2);
                AND(ctx, ref ctx->eax.l, 0x3f);
                if (!ctx->zero) goto label_Rd_IgnoreDAM;  // also need to avoid setting AT in ST0
                // fallthrough

            // now we have to verify the Data Address Marks (found in bit 6 of ST2),
            // DAM_Mask = 0 for ReadData and 64 for ReadDeletedData

                goto label_Read_CheckDAM; // fall through
            case case_Read_CheckDAM: label_Read_CheckDAM:
                ctx->eax.l = ctx->esi.u8[5];            // ST2 from sectorinfo
                AND(ctx, ref ctx->eax.l, 64);
                XOR(ctx, ref ctx->eax.l, C->DAM_Mask);
                if (ctx->zero) goto label_Rd_TransferData;      // DAM bits verify so we transfer the data

                // if DAM doesn't match then we either,
                // skip this sector if SK is set or,
                // return the Control Mark error (bit 6 in ST2 again) and transfer the data

                TEST(ctx, C->FDCCommandByte, 32);
                if (!ctx->zero) goto label_SkipReadSector;          // SK=1 so just skip this sector

                C->ST2DAMBit = 64;             // set ST2 Control Mark bit
                OR(ctx, ref C->ST0, 0x40);                  // abnormal termination of command
                // fallthrough

                goto label_Rd_IgnoreDAM; // fall through
            case case_Rd_IgnoreDAM: label_Rd_IgnoreDAM:
                C->SectorToCPUReturn = case_Rd_Skip0;
                goto label_SectorDataToCPU;

            case case_Rd_Skip0: label_Rd_Skip0:
                OR(ctx, ref C->ST1, 0x80);                  // end of cylinder
                goto label_ReturnSectorRWResults;   // and exit returning the error

            // we have found the requested sector so transfer the sector data to the CPU
            case case_Rd_TransferData: label_Rd_TransferData:
                INC(ctx, ref C->SectorsRead);    // sector counter for Read Track command
                C->SectorToCPUReturn = case_LFRS_1;
                goto label_SectorDataToCPU;

            // check disk error fields for this sector
            case case_LFRS_1: label_LFRS_1:
                TEST(ctx, C->MainStatusReg, 0x20);  // execution mode ended? (overrun/lost data condition in status port read)
                if (ctx->zero) goto label_ReturnSectorRWResults;          // exit returning the error

                ctx->esi.u8 = C->CurrentSectorInfo;
                TEST(ctx, READW(&C->ST1), 0x2125);   // DE, ND or MA in ST1, DD or MD in ST2?
                if (ctx->zero) goto label_LFRS_3;

                C->ST0 = 0x40;                // AT
                OR(ctx, ref C->ST1, 0x80);                // end of cylinder

                CMP(ctx, C->ReadMode, FDCReadTrack);
                if (ctx->zero) goto label_LFRS_3;
                goto label_ReturnSectorRWResults;

            // when R = EOT then we have read all requested sectors so return result bytes
            case case_LFRS_3: label_LFRS_3:
                ctx->eax.l = ctx->esi.u8[2];
                CMP(ctx, C->ReadMode, FDCReadTrack);
                if (!ctx->zero) goto label_NotReadTrk1;

                // Read Track complete?
                ctx->eax.l = C->SectorsRead;
                CMP(ctx, ctx->eax.l, C->FDCParameters[5]); // EOT
                if (ctx->zero) goto label_Read_Com1;  // terminate command

                // any more sectors to read on this track?
                CMP(ctx, ctx->eax.l, ctx->ebx.disk->TrackBlock.NumSectors);
                if (ctx->carry) goto label_LFRS_4;  // continue reading if sectors available

                // else sectors expired before reaching EOT sector count
                // in Read Track command

                // so we terminate the command and enter the Results stage
                // (perhaps set different error flags?)
                goto label_Read_Com1;

            case case_NotReadTrk1: label_NotReadTrk1:
                CMP(ctx, ctx->eax.l, C->FDCParameters[5]); // EOT
                if (!ctx->zero) goto label_LFRS_4;
                // fallthrough

                goto label_Read_Com1; // fall through
            case case_Read_Com1: label_Read_Com1:
                C->ST0 = 0x40;
                C->ST1 = 0x80;

                ctx->eax.e = READDW(ctx->esi.u8);             // copy CHRN
                ctx->eax.l = ctx->ebx.disk->CTK;
                WRITEDW(&C->FDCResults[3], ctx->eax.e);     // to Results buffer

                goto label_ReturnSectorRWResults;

            // else increment R parameter and search for the next sector to continue reading,
            // we search from the next physical sector (CSR is incremented by @InitReadSector)
            // each sector in a multi-sector transfer is treated as an entirely separate
            // sector read so we reset the IndexHoleCount back to zero again

            case case_LFRS_4: label_LFRS_4:
                INC(ctx, ref C->FDCParameters[3]);  // R parameter
                C->IndexHoleCount = 0;           // 1st revolution for this sector read
                goto label_InitReadSector;

            case case_SkipReadSector: label_SkipReadSector:
                Run(ctx, case_AdvanceSectorPtrs);

                INC(ctx, ref ctx->ebx.disk->CSR);                // move to the next physical sector
                ctx->eax.l = ctx->ebx.disk->CSR;
                CMP(ctx, ctx->eax.l, ctx->ebx.disk->TrackBlock.NumSectors);
                if (ctx->carry) goto label_LocateReadSector;

                ctx->ebx.disk->CSR = 0xFF;             // InitReadSector increments this to zero
                INC(ctx, ref C->IndexHoleCount);
                CMP(ctx, C->IndexHoleCount, 2);   // search for two disk revolutions
                if (ctx->carry) goto label_InitReadSector;

                ctx->ebx.disk->CSR = 0;

                // if we get this far then the sector could not be found
                C->CurrentSectorInfo -= 8;
                OR(ctx, ref C->ST1, 4);              // No Data
                AND(ctx, ref C->ST0, 0x3f);
                OR(ctx, ref C->ST0, 0x40);      // AT
                ctx->eax.l = 1;               // maybe should be the first sector ID?
                C->FDCResults[5] = ctx->eax.l;   // set final R result
                goto label_ReturnSectorRWResults;

            // ######################################################################

            case case_SectorDataToCPU: label_SectorDataToCPU:
                ctx->esi.u8 = C->CurrentSectorInfo;

                ctx->ecx.l = C->FDCParameters[4]; // N value
                if (ctx->ecx.l > 8) {
                    ctx->ecx.l = 8;                       // max N = 8 (32K sector)
                }

                if (ctx->ecx.l == 0) {
                    // if N = 0 (use DTL bytes)
                    XOR(ctx, ref ctx->ecx.e, ctx->ecx.e);
                    ctx->ecx.l = C->FDCParameters[7]; // DTL from command
                    if (ctx->ecx.l > 128) {
                        ctx->ecx.l = 128;                     // DTL max bytes = 128
                    }
                    ctx->edx.e = ctx->ecx.e;                            // ecx & edx = DTL bytes available
                    C->DTL_BytesSent = 1;
                }
                else {
                    ctx->eax.e = 128;
                    SHL(ctx, ref ctx->eax.e, ctx->ecx.l);
                    ctx->ecx.e = ctx->eax.e;                            // ecx = physical sectorsize based on N value
                    ctx->edx.e = ctx->ecx.e;                            // edx = bytes of available sector data
    
                    if (ctx->ebx.disk->EDSK == 1) {
                        ctx->edx.e = READW(&ctx->esi.u8[6]);       // edx = available sector data from EDsk sectorinfo
                    }
                }

                if (ctx->ecx.e > 32768) {
                    ctx->ecx.e = 32768;
                }

                C->PhysicalSectorSize = ctx->ecx.e;      // = (128 Shl N) or DTL bytes
                C->AvailableSectorData = ctx->edx.e;

                ctx->eax.l = ctx->esi.u8[1];                         // H
                CMP(ctx, ctx->eax.l, C->FDCParameters[2]); // matching H params?
                if (!ctx->zero) goto label_SDTC_RandomData;

                ctx->eax.x = READW(&ctx->esi.u8[4]);                // fetch ST1 & ST2 from sectorinfo
                AND(ctx, ref ctx->eax.x, 0x2020);                           // mask data error bits for ST1 & ST2
                CMP(ctx, ctx->eax.x, 0x2020);                           // if both data error bits set
                if (ctx->zero) goto label_SDTC_RandomData;                    // then return a randomised sector

                ctx->esi.u8 = C->CurrentSectorData;
                goto label_SDTC_TransferData;

            case case_SDTC_RandomData: label_SDTC_RandomData:
                // ctx->ecx.e = physical sector size
                // ctx->edx.e = available sector data

                if (ctx->edx.e > ctx->ecx.e) {                   // multiple sector data available?
                    //push    ctx->ecx.e
                    //push    ctx->edx.e
                    //invoke  IntDiv, ctx->edx.e, ctx->ecx.e    // get number of multiple sectors available
                    //pop     ctx->edx.e
                    //pop     ctx->ecx.e
                    ctx->eax.e = ctx->edx.e / ctx->ecx.e;

                    CMP(ctx, ctx->eax.e, 2);
                    if (ctx->carry) goto label_SDTC_NormalRandom;  // normal random method if less than 2 sectors are available

                    INC(ctx, ref C->MultipleSectorPick);
                    if (C->MultipleSectorPick >= ctx->eax.e) {
                        C->MultipleSectorPick = 0;
                    }

                    //push    ctx->ecx.e
                    //invoke  IntMul, C->MultipleSectorPick, ctx->ecx.e
                    //pop     ctx->ecx.e
                    ctx->eax.e = C->MultipleSectorPick * ctx->ecx.e;

                    ctx->esi.u8 = C->CurrentSectorData;
                    ctx->esi.u8 += ctx->eax.e;
                    goto label_SDTC_TransferData;
                }
                // available sector data <= physical sector size
                goto label_SDTC_NormalRandom;

            case case_SDTC_NormalRandom: label_SDTC_NormalRandom:
                {
                    PUSH(ctx, ctx->edi);
                    PUSH(ctx, ctx->ecx);
                    ctx->esi.u8 = C->CurrentSectorData;
                    ctx->edi.u8 = &C->FDCRandomData[0];
                    uint available = Math.Min(ctx->edx.e, RandomDataSize);
                    ctx->ecx.e = available;                     // copy available sector data

                    rep_movsb(ctx);
                    ctx->ecx = POP(ctx);

                    // if required, top up sector data with random bytes
                    uint wanted = Math.Min(ctx->ecx.e, RandomDataSize);
                    ctx->ecx.e = wanted > available ? wanted - available : 0;   // top-up bytes required

                    ctx->eax.l = C->FDCRandomSeed;
                    ctx->eax.h = 3;
                    if (C->DskRndMethod == 255) {
                        XOR(ctx, ref ctx->eax.x, ctx->eax.x);
                    }

                    while  (ctx->ecx.e > 0) {
                        *ctx->edi.u8++ = ctx->eax.l;
                        ADD(ctx, ref ctx->eax.l, ctx->eax.h);
                        DEC(ctx, ref ctx->ecx.e);
                    }
                    C->FDCRandomSeed = ctx->eax.l;
                    ctx->edi = POP(ctx);

                    ctx->esi.u8 = &C->FDCRandomData[0];
                    ctx->ecx.e = C->PhysicalSectorSize;

                    // for N >= 6 sectors, transfer data now
                    CMP(ctx, ctx->ecx.e, 8192);
                    if (!ctx->carry) goto label_SDTC_TransferData;

                    CMP(ctx, C->DskRndMethod, 255);
                    if (ctx->zero) goto label_SDTC_TransferData;

                    // else now we need different methods of setting the random byte in this data stream
                    ctx->eax.l = C->FDCRandomSeed;
                    ADD(ctx, ref ctx->eax.l, 0x03);
                    C->FDCRandomSeed = ctx->eax.l;

                    ctx->eax.h = C->DskRndMethod;
                    if (ctx->eax.h == 0) {
                        // auto-sense random method
                        ctx->eax.h = 2;   // assume to randomise the first byte of sector data

                        // test for Dixon's Premiere Collection (disk 1)
                        if (READDW(ctx->esi.u8) == 0x1ce2ae94) {
                            CMP(ctx, READDW(&ctx->esi.u8[4]), 0x80a40824);
                            if (ctx->zero) goto label_SkipAutoSense;
                        }

                        // test for Dixon's Premiere Collection (disk 2)
                        if (READDW(ctx->esi.u8) == 0xaac6f5b5) {
                            CMP(ctx, READDW(&ctx->esi.u8[4]), 0x2a041840);
                            if (ctx->zero) goto label_SkipAutoSense;
                        }

                        // test for Hopping Mad
                        if (READDW(ctx->esi.u8) == 0x92831270) {
                            CMP(ctx, READDW(&ctx->esi.u8[4]), 0x9134d31);
                            if (ctx->zero) goto label_SkipAutoSense;
                        }

                        ctx->eax.h = 1;   // else randomise the final byte of sector data
                    }

            label_SkipAutoSense:
                    if (ctx->eax.h == 1) {
                        // randomise the final byte of sector data
                        AND(ctx, ref ctx->ecx.e, 0xffff);
                        ctx->esi.u8[ctx->ecx.e - 1] = ctx->eax.l;
                    }
                    else {
                        // randomise the first byte of sector data (Dixon's Premiere Collection, etc)
                        *ctx->esi.u8 = ctx->eax.l;
                    }
                }
                // fallthrough

                goto label_SDTC_TransferData; // fall through
            case case_SDTC_TransferData: label_SDTC_TransferData:
                if (C->SectorsTransferred > 0) {
                    FDCCommandCallback(ctx, 9);   // send a new callback for each successive sector read);
                }

                // transfer CX bytes from [ESI] to Z80
                C->UnitPtr = ctx->ebx.disk;          // preserve FDD Unit ptr
                C->FDCReturn = case_SectorDataToCPU_Done;
                goto label_FDC_SendData;               // transfer data to CPU

            case case_SectorDataToCPU_Done: label_SectorDataToCPU_Done:
                INC(ctx, ref C->SectorsTransferred);    // count number of sectors sent each command

                ctx->ebx.disk = C->UnitPtr;          // restore FDD Unit ptr
                ctx->eax.e = C->SectorToCPUReturn;
                { label = ctx->eax.e; goto again; }

            // ######################################################################

            case case_AdvanceSectorPtrs: label_AdvanceSectorPtrs:
                ctx->esi.u8 = C->CurrentSectorInfo;
                ctx->eax.e = READW(&ctx->esi.u8[6]);           // size of sectordata in edsk
                CMP(ctx, ctx->ebx.disk->EDSK, 1);
                if (ctx->zero) goto label_SkipSectorEDSK;
                ctx->eax.e = C->CurrentSectorSize;    // size of sectordata in dsk
                // fallthrough

                goto label_SkipSectorEDSK; // fall through
            case case_SkipSectorEDSK: label_SkipSectorEDSK:
                C->CurrentSectorData += ctx->eax.e;
                C->CurrentSectorInfo += 8;
                return;

            // ######################################################################

            // disk writing is incomplete and will only work with standard
            // +3DOS single-sector writes and only to normal DSK files.

            case case_WriteSectorData: label_WriteSectorData:
                XOR(ctx, ref ctx->eax.l, ctx->eax.l);
                C->ST0 = ctx->eax.l;
                C->ST1 = ctx->eax.l;
                C->ST2 = ctx->eax.l;

                C->ST2DAMBit = ctx->eax.l;

                Run(ctx, case_TrapStandardErrors);
                CMP(ctx, ctx->ebx.disk->WriteProtect, 1);
                if (!ctx->zero) goto label_WSD_WProt;        // jump if not write-protected

                C->TSEError = 1;     // force the error
                AND(ctx, ref C->ST0, 0x3f);
                OR(ctx, ref C->ST0, 0x40);           // AT
                OR(ctx, ref C->ST1, 2);             // write-protected
                // fallthrough

                goto label_WSD_WProt; // fall through
            case case_WSD_WProt: label_WSD_WProt:
                CMP(ctx, C->TSEError, 1);
                if (!ctx->zero) goto label_WriteSectorData_1;

                ctx->eax.e = READDW(&C->FDCParameters[1]);   // copy C,H,R,N from command
                WRITEDW(&C->FDCResults[3], ctx->eax.e);      // into results buffer
                goto label_ReturnSectorRWResults;         // and exit returning the error

            case case_WriteSectorData_1: label_WriteSectorData_1:
                OR(ctx, ref C->MainStatusReg, 32 + 16);      // enter Execution mode + busy

                Run(ctx, case_ReadCurrTrack);

                Run(ctx, case_GetSectorSize);
                C->CurrentSectorSize = ctx->eax.e;                   // this sector's size (in bytes)

                PUSH(ctx, ctx->esi);
                ctx->esi.u8 = &ctx->ebx.disk->TrackBlock.SectorData[0];
                C->CurrentSectorData = ctx->esi.u8;     // ptr to this sector's data
                ctx->esi.u8 = &ctx->ebx.disk->TrackBlock.SectorInfoList[0];
                C->CurrentSectorInfo = ctx->esi.u8; // ptr to this sector's info
                ctx->esi = POP(ctx);
                C->CurrentSectorNumber = 0;
                // fallthrough

                goto label_LocateFirstWriteSector; // fall through
            case case_LocateFirstWriteSector: label_LocateFirstWriteSector:
                ctx->esi.u8 = C->CurrentSectorInfo;
                ctx->eax.l = ctx->esi.u8[2];          // sector ID (R)
                CMP(ctx, ctx->eax.l, C->FDCParameters[3]); // is this the sector we are looking for?
                if (ctx->carry) goto label_SkipWriteSector;     // skip if not
                CMP(ctx, ctx->eax.l, C->FDCParameters[5]);
                if (!ctx->zero && !ctx->carry) goto label_SkipWriteSector;     // skip if not

                C->CPUToSectorReturn = case_LFWS_1;
                goto label_CPUDataToSector;

            case case_LFWS_1: label_LFWS_1:
                ctx->eax.l = C->FDCParameters[4]; // N value
                CMP(ctx, ctx->eax.l, 0);
                if (ctx->zero) goto label_ReturnSectorRWResults;
                // fallthrough

                goto label_SkipWriteSector; // fall through
            case case_SkipWriteSector: label_SkipWriteSector:
                C->CurrentSectorInfo += 8;
                ctx->eax.e = C->CurrentSectorSize;
                C->CurrentSectorData += ctx->eax.e;

                INC(ctx, ref C->CurrentSectorNumber);
                ctx->eax.l = C->CurrentSectorNumber;
                CMP(ctx, ctx->eax.l, ctx->ebx.disk->TrackBlock.NumSectors);
                if (!ctx->zero) goto label_LocateFirstWriteSector;

                goto label_ReturnSectorRWResults;

            case case_CPUDataToSector: label_CPUDataToSector:
                ctx->eax.l = C->FDCParameters[4]; // N value
                CMP(ctx, ctx->eax.l, 0);
                if (!ctx->zero) goto label_CDTS_1;

                XOR(ctx, ref ctx->ecx.x, ctx->ecx.x);
                ctx->ecx.l = C->FDCParameters[7]; // DTL from command
                goto label_CDTS_2;

            case case_CDTS_1: label_CDTS_1:
                ctx->ecx.e = C->CurrentSectorSize;
                // fallthrough

                goto label_CDTS_2; // fall through
            case case_CDTS_2: label_CDTS_2:
                ctx->edx.u8 = C->CurrentSectorData;
                C->UnitPtr = ctx->ebx.disk;             // preserve FDD Unit ptr
                C->FDCReturn = case_CPUDataToSector_1;
                goto label_FDC_ReceiveData;    // receive new sector data from CPU

            case case_CPUDataToSector_1: label_CPUDataToSector_1:
                ctx->ebx.disk = C->UnitPtr;             // restore FDD Unit ptr
                Run(ctx, case_WriteCurrTrack);
                // fallthrough

                goto label_CPUDataToSector_Done; // fall through
            case case_CPUDataToSector_Done: label_CPUDataToSector_Done:
                ctx->eax.e = C->CPUToSectorReturn;
                { label = ctx->eax.e; goto again; }

            // ######################################################################

            case case_ReturnSectorRWResults: label_ReturnSectorRWResults:
                PUSH(ctx, ctx->eax);
                ctx->edx.u8 = &C->FDCResults[0];

                AND(ctx, ref C->ST0, 0xfb);
                ctx->eax.l = ctx->ebx.disk->CHEAD;
                SHL(ctx, ref ctx->eax.l, 2);
                OR(ctx, ref C->ST0, ctx->eax.l);

                AND(ctx, ref C->ST0, 0xfc);
                ctx->eax.l = C->FDCParameters[0];
                AND(ctx, ref ctx->eax.l, 3);
                OR(ctx, ref C->ST0, ctx->eax.l);            // insert unit number

                ctx->eax.l = C->ST0;      // ST0
                ctx->edx.u8[0] = ctx->eax.l;

                ctx->eax.l = C->ST1;
                ctx->edx.u8[1] = ctx->eax.l;

                ctx->eax.l = C->ST2;
                AND(ctx, ref ctx->eax.l, 0xbf);
                OR(ctx, ref ctx->eax.l, C->ST2DAMBit);
                C->ST2 = ctx->eax.l;
                ctx->eax.l = C->ST2;
                ctx->edx.u8[2] = ctx->eax.l;
                ctx->eax = POP(ctx);

                C->OverRunError = 0;
                AND(ctx, ref C->MainStatusReg, 0xdf); // execution phase has ended and result phase has started
                C->FDCReturn = case_FDCBuff_ReturnSectorResults;
                ctx->esi.u8 = &C->FDCResults[0];
                ctx->ecx.x = 7;             // 7 bytes of result data
                C->NumResults = 7;
                goto label_FDC_SendData;    // transfer data to CPU

            case case_FDCBuff_ReturnSectorResults: label_FDCBuff_ReturnSectorResults:
                ctx->eax.e = C->FDCBufferReturn;
                { label = ctx->eax.e; goto again; }

            // ######################################################################

            case case_SkipNextSector: label_SkipNextSector:
                XOR(ctx, ref ctx->eax.e, ctx->eax.e);
                ctx->eax.x = READW(&ctx->esi.u8[6]);     // size of EDSK sector data
                ctx->esi.u8 += 8;          // next SectorInfo entry in SectorInfoList

                CMP(ctx, ctx->ebx.disk->EDSK, 1);
                if (ctx->zero) goto label_SkNxtSec1;

                Run(ctx, case_GetSectorSize); // size of DSK sector data
                // fallthrough

                goto label_SkNxtSec1; // fall through
            case case_SkNxtSec1: label_SkNxtSec1:
                ctx->ecx.u8 += ctx->eax.e;
                return;

            // ######################################################################

            case case_GetSectorSize: label_GetSectorSize:
                PUSH(ctx, ctx->ecx);
                ctx->ecx.l = ctx->ebx.disk->TrackBlock.SectorSize;
                ctx->eax.e = 128;
                SHL(ctx, ref ctx->eax.e, ctx->ecx.l);
                CMP(ctx, ctx->eax.e, 8192);
                if (ctx->carry) goto label_GSS_Exit;
                ctx->eax.e = 6144;
                // fallthrough

                goto label_GSS_Exit; // fall through
            case case_GSS_Exit: label_GSS_Exit:
                ctx->ecx = POP(ctx);
                return;

            // ######################################################################

            case case_ReadCurrTrack: label_ReadCurrTrack:
                Run(ctx, case_LocateTrack);    // ctx->esi.u8 points to current track data

                PUSH(ctx, ctx->edi);
                ctx->edi.u8 = &ctx->ebx.disk->TrackBlock.TrackData[0];
                ctx->ecx.e = ctx->ebx.disk->DiskBlock.TrackSize;
                if (ctx->ecx.e > (uint)sizeof(TrackInfoBlock)) {
                    ctx->ecx.e = (uint)sizeof(TrackInfoBlock);
                }

                rep_movsb(ctx);

                ctx->edi.u8 = &ctx->ebx.disk->TrackBlock.TrackData[0];
                ctx->esi.u8 = TrackInfoSignature;
                ctx->edx.l = 0;
                ctx->ecx.l = 10;
                // fallthrough

                goto label_VTrk_Loop; // fall through
            case case_VTrk_Loop: label_VTrk_Loop:
                ctx->eax.l = *ctx->edi.u8;
                CMP(ctx, ctx->eax.l, *ctx->esi.u8);
                if (!ctx->zero) goto label_VTrk_Exit;
                INC(ctx, ref ctx->esi.u8);
                INC(ctx, ref ctx->edi.u8);
                DEC(ctx, ref ctx->ecx.l);
                if (!ctx->zero) goto label_VTrk_Loop;
                ctx->edx.l = 1;
                // fallthrough

                goto label_VTrk_Exit; // fall through
            case case_VTrk_Exit: label_VTrk_Exit:
                ctx->edi = POP(ctx);
                C->ValidTrack = ctx->edx.l;
                return;

            // ######################################################################

            case case_WriteCurrTrack: label_WriteCurrTrack:
                ctx->ebx.disk->ContentsChanged = 1;
                Run(ctx, case_LocateTrack);    // ctx->esi.u8 points to current track data

                PUSH(ctx, ctx->edi);
                ctx->edi.u8 = ctx->esi.u8;         // edi=track data in FDDUnit0
                ctx->esi.u8 = &ctx->ebx.disk->TrackBlock.TrackData[0];
                ctx->ecx.x = ctx->ebx.disk->DiskBlock.TrackSize;
                AND(ctx, ref ctx->ecx.e, 0xffff);

                rep_movsb(ctx);

                ctx->edi = POP(ctx);
                return;

            // ######################################################################

            // sets ctx->esi.u8 to the start of the current track data in FDDUnit0 array
            case case_LocateTrack: label_LocateTrack:
                XOR(ctx, ref ctx->eax.e, ctx->eax.e);
                XOR(ctx, ref ctx->edx.e, ctx->edx.e);
                ctx->edx.x = ctx->ebx.disk->DiskBlock.TrackSize;
                AND(ctx, ref ctx->edx.e, 65535);
                ctx->ecx.l = ctx->ebx.disk->CTK;      // current physical track head is over
                INC(ctx, ref ctx->ecx.l);
                // fallthrough

                goto label_LocTrk1; // fall through
            case case_LocTrk1: label_LocTrk1:
                DEC(ctx, ref ctx->ecx.l);
                if (ctx->zero) goto label_LockTrkDone;
                ADD(ctx, ref ctx->eax.e, ctx->edx.e);
                goto label_LocTrk1;

            case case_LockTrkDone: label_LockTrkDone:
                CMP(ctx, ctx->ebx.disk->DiskBlock.NumSides, 2);
                if (!ctx->zero) goto label_LocSingleSide;
                SHL(ctx, ref ctx->eax.e, 1);

                CMP(ctx, ctx->ebx.disk->CHEAD, 1);
                if (!ctx->zero) goto label_LocSingleSide;
                ADD(ctx, ref ctx->eax.e, ctx->edx.e);                  // add DiskBlock.TrackSize to skip to the interleaved track
                // fallthrough

                goto label_LocSingleSide; // fall through
            case case_LocSingleSide: label_LocSingleSide:
                ADD(ctx, ref ctx->eax.e, 0x100);    // and add the sizeof DiskInfoBlock
                ctx->esi.u8 = ctx->ebx.disk->DiskArrayPtr;
                ctx->esi.u8 += ctx->eax.e;
                if ((nuint)ctx->eax.e + ctx->ebx.disk->DiskBlock.TrackSize > ctx->ebx.disk->DiskAllocLen)
                    ctx->esi.u8 = EmptyTrack;
                return;
            }
        }

        // ------------------------------------------------------------------ jump targets (the C's case enum)

        private const uint case_AdvanceSectorPtrs = 0;
        private const uint case_CDTS_1 = 1;
        private const uint case_CDTS_2 = 2;
        private const uint case_command = 3;
        private const uint case_CPUDataToSector = 4;
        private const uint case_CPUDataToSector_1 = 5;
        private const uint case_CPUDataToSector_Done = 6;
        private const uint case_eax = 7;
        private const uint case_edi = 8;
        private const uint case_FDC_FormatTrack = 9;
        private const uint case_FDC_Invalid = 10;
        private const uint case_FDC_Invalid1 = 11;
        private const uint case_FDC_NewCommand = 12;
        private const uint case_FDC_RdScDone = 13;
        private const uint case_FDC_RdScL1 = 14;
        private const uint case_FDC_ReadData = 15;
        private const uint case_FDC_ReadData1 = 16;
        private const uint case_FDC_ReadData2 = 17;
        private const uint case_FDC_ReadDataEntry = 18;
        private const uint case_FDC_ReadDeletedData = 19;
        private const uint case_FDC_ReadSectorID = 20;
        private const uint case_FDC_ReadSectorID1 = 21;
        private const uint case_FDC_ReadSectorID2 = 22;
        private const uint case_FDC_ReadTrack = 23;
        private const uint case_FDC_Recalibrate = 24;
        private const uint case_FDC_Recalibrate1 = 25;
        private const uint case_FDC_Recalibrate2 = 26;
        private const uint case_FDC_ReceiveData = 27;
        private const uint case_FDC_ReceiveDataEnd = 28;
        private const uint case_FDC_ReceiveDataLoop = 29;
        private const uint case_FDC_RecExit = 30;
        private const uint case_FDC_RSSkip1 = 31;
        private const uint case_FDC_ScanEqual = 32;
        private const uint case_FDC_ScanHighOrEqual = 33;
        private const uint case_FDC_ScanLowOrEqual = 34;
        private const uint case_FDC_SDS1 = 35;
        private const uint case_FDC_SDS2 = 36;
        private const uint case_FDC_SDSResults = 37;
        private const uint case_FDC_Seek = 38;
        private const uint case_FDC_Seek1 = 39;
        private const uint case_FDC_SendData = 40;
        private const uint case_FDC_SendData1 = 41;
        private const uint case_FDC_SendData2 = 42;
        private const uint case_FDC_SenseCont = 43;
        private const uint case_FDC_SenseDriveStatus = 44;
        private const uint case_FDC_SenseDriveStatus1 = 45;
        private const uint case_FDC_SenseDriveStatus2 = 46;
        private const uint case_FDC_SenseInterruptStatus = 47;
        private const uint case_FDC_SenseInterruptStatus1 = 48;
        private const uint case_FDC_Specify = 49;
        private const uint case_FDC_Specify1 = 50;
        private const uint case_FDC_Version = 51;
        private const uint case_FDC_Version1 = 52;
        private const uint case_FDC_WriteData = 53;
        private const uint case_FDC_WriteData1 = 54;
        private const uint case_FDC_WriteData2 = 55;
        private const uint case_FDC_WriteDataEntry = 56;
        private const uint case_FDC_WriteDeletedData = 57;
        private const uint case_FDCBuff_ReturnSectorResults = 58;
        private const uint case_FDCR_NotBadC = 59;
        private const uint case_FDCR_SameC = 60;
        private const uint case_GetSectorSize = 61;
        private const uint case_GSS_Exit = 62;
        private const uint case_InitFDC = 63;
        private const uint case_InitReadSector = 64;
        private const uint case_IRSDone = 65;
        private const uint case_IRSJmp1 = 66;
        private const uint case_IRSLoop = 67;
        private const uint case_LFRS_1 = 68;
        private const uint case_LFRS_3 = 69;
        private const uint case_LFRS_4 = 70;
        private const uint case_LFWS_1 = 71;
        private const uint case_LocateFirstWriteSector = 72;
        private const uint case_LocateReadSector = 73;
        private const uint case_LocateTrack = 74;
        private const uint case_LockTrkDone = 75;
        private const uint case_LocSingleSide = 76;
        private const uint case_LocTrk1 = 77;
        private const uint case_NoDiskChange = 78;
        private const uint case_NotReadTrk1 = 79;
        private const uint case_Rd_IgnoreDAM = 80;
        private const uint case_Rd_NotMS1 = 81;
        private const uint case_Rd_Skip0 = 82;
        private const uint case_Rd_TransferData = 83;
        private const uint case_Read_CheckDAM = 84;
        private const uint case_Read_Com1 = 85;
        private const uint case_Read_CompareSectorID = 86;
        private const uint case_Read_ID_1 = 87;
        private const uint case_ReadCurrTrack = 88;
        private const uint case_ReadID_Results = 89;
        private const uint case_ReadID_SendResults = 90;
        private const uint case_ReadSectorData = 91;
        private const uint case_ReadSectorData_1 = 92;
        private const uint case_ReceiveCommandBytes = 93;
        private const uint case_ReturnSectorRWResults = 94;
        private const uint case_RSJmp1 = 95;
        private const uint case_RSNoSec0 = 96;
        private const uint case_SDTC_NormalRandom = 97;
        private const uint case_SDTC_RandomData = 98;
        private const uint case_SDTC_TransferData = 99;
        private const uint case_SectorDataToCPU = 100;
        private const uint case_SectorDataToCPU_Done = 101;
        private const uint case_SeekNotDone = 102;
        private const uint case_SkipNextSector = 103;
        private const uint case_SkipReadSector = 104;
        private const uint case_SkipSectorEDSK = 105;
        private const uint case_SkipWriteSector = 106;
        private const uint case_SkNxtSec1 = 107;
        private const uint case_STrk_Valid = 108;
        private const uint case_TrapStandardErrors = 109;
        private const uint case_TSE_1 = 110;
        private const uint case_TSE_2 = 111;
        private const uint case_TSE_4 = 112;
        private const uint case_TSE_NotReady = 113;
        private const uint case_TSE_Quit = 114;
        private const uint case_VTrk_Exit = 115;
        private const uint case_VTrk_Loop = 116;
        private const uint case_WriteCurrTrack = 117;
        private const uint case_WriteSectorData = 118;
        private const uint case_WriteSectorData_1 = 119;
        private const uint case_WSD_WProt = 120;
    }
}
