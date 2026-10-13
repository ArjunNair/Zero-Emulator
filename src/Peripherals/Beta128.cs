// WD1793-compatible floppy disk controller for the Beta 128 disk interface (Pentagon-clone TR-DOS
// support), operating on flat .trd disk images held entirely in memory.
//
// A C# port of ZXBeta128.pas from Paul Dunn's Spin emulator, used in Zero with his permission.
// It was tuned against a disassembly of SpecEmu's wd1793.dll (the DLL the Windows build of Zero
// used) and against the TR-DOS 5.03 ROM; the comments citing either are the Pascal's own.
//
// Ported as directly as the languages allow, so the behaviour stays the Pascal's: the same
// fields, the same command handlers in the same order. What differs:
//  - The disk-format helpers (blank disks, SCL to TRD, the system sector) are in TrdImage.
//  - The port entry points drop their unused Port argument, and the unused clock and
//    fast-loading fields are gone.
//  - DiskModified, FlushAll, HeadLoaded and ElapseFrames are additions, for the session and the deck.
//  - The head load line (HLD), which the Pascal does not have, follows the WD1793 data sheet:
//    see "From the data sheet".
//  - Single-sided disks are told from double-sided ones of the same size, and an SCL is never
//    written back over a .trd that is already there: see "Port fix".
//  - The system register's bit 7 is the WD1793's interrupt request, and a read the CPU stops
//    servicing ends in Lost Data, both as the wd1793.dll source does them: see "From the DLL".
//
// Known gaps, as in the Pascal: no timing (BUSY/DRQ change only as bytes move), Read Track does
// nothing, sectors are always 16 x 256 bytes a track, and there is no MGT/+D interface.

using System;
using System.IO;

namespace Peripherals
{
    public sealed class Beta128
    {
        // Status register bits. Meaning of bits 1/2/4/5 DIFFERS between Type I
        // (Restore/Seek/Step/Step-In/Step-Out) and Type II/III (Read/Write
        // Sector, Read Address, Read/Write Track) commands -- same bit position,
        // different signal, per the WD1793 datasheet. ReadStatus's comment says
        // which set applies for whichever command last ran.
        public const byte ST_BUSY         = 0x01;
        public const byte ST_INDEX        = 0x02;  // Type I only
        public const byte ST_DRQ          = 0x02;  // Type II/III only
        public const byte ST_TRACK0       = 0x04;  // Type I only
        public const byte ST_LOSTDATA     = 0x04;  // Type II/III only
        public const byte ST_CRCERROR     = 0x08;
        public const byte ST_SEEKERROR    = 0x10;  // Type I only
        public const byte ST_RNF          = 0x10;  // Type II/III only (record/sector not found)
        public const byte ST_HEADLOADED   = 0x20;  // Type I only
        public const byte ST_WRITEPROTECT = 0x40;
        public const byte ST_NOTREADY     = 0x80;

        private const int SectorsPerTrack = TrdImage.SectorsPerTrack;
        private const int SectorSize      = TrdImage.SectorSize;
        private const int TrackSize       = TrdImage.TrackSize;
        public const int WriteTrackLength = 6250;   // real wd1793.dll transfer size

        public const int BetaDriveCount = 4;        // WD1793 drive-select field is 2 bits

        private enum TransferKind { None, Sector, ReadAddress, WriteTrack }

        private struct Drive
        {
            public byte[] Data;           // whole disk image, flat, track-major then side then sector
            public bool Loaded;
            public bool WriteProtected;
            public string FileName;
            public string SclFileName;    // the SCL it came from, until it is first written back
            public bool Modified;
            public int Tracks;
            public int Sides;
            public int CurrentTrack;      // this drive's own physical head position
        }

        private readonly Drive[] FDrive = new Drive[BetaDriveCount];
        private int FSelectedDrive;   // 0..3, from system register bits 0-1
        private int FSide;            // 0/1, from system register bit 4 (inverted -- see WriteSystemRegister)
        private bool FHLT;            // system register bit 3 ("block HLT", normally set)
        private bool FDDEN;           // system register bit 5 (density) -- cosmetic only, FM vs MFM
                                      // encoding differences aren't modelled since .trd is a flat
                                      // logical-sector dump, not a bit-level track image.

        // WD1793 register file
        private byte FTrackReg;
        private byte FSectorReg;
        private byte FDataReg;
        private byte FStatus;
        private int FStepDirection;   // +1/-1, remembered across plain Step commands
        private bool FLastWasTypeI;   // selects which half of the bit-1/2/4/5 meaning ReadStatus uses

        // Type I (Restore/Seek/Step) commands complete synchronously
        // Type II/III in-progress data transfer
        private bool FTransferActive;
        private bool FTransferIsWrite;
        private bool FTransferMultiple;
        private TransferKind FTransferKind;
        private int FTransferPos;      // 0..FTransferLength, byte offset within FTransferBuffer
        private int FTransferLength;   // 6, SectorSize, or WriteTrackLength
        private readonly byte[] FTransferBuffer = new byte[WriteTrackLength];
        // BUSY/DRQ for a Type II/III transfer are asserted synchronously and
        // stay exactly as set until the transfer completes or is abandoned --
        // no elapsed-time gating of any kind.

        // From the DLL: the WD1793's INTRQ line, which the Beta 128 puts on bit 7 of
        // the system register. Raised when a command ends, lowered by reading the
        // status register or writing a new command (wd1793.dll's SetINTRQ, cleared in
        // wd1793_ReadStatusReg and wd1793_ExecuteCommand).
        private bool FIntrq;

        // From the DLL: with no timing, "the CPU was too slow" is counted in polls of
        // the system register instead. A read that is polled this many times without
        // the CPU taking its byte is abandoned with Lost Data (wd1793.dll's
        // OverRunCount, reloaded with 64 as each byte is read).
        public const int LostDataPolls = 64;
        private int FOverrunCount;

        /// <summary>For debugging: called with (track, side, sector, isWrite) as each sector moves.</summary>
        public Action<int, int, int, bool> OnSectorTransfer;

        public Beta128()
        {
            Reset();
        }

        public void Reset()
        {
            FSelectedDrive = 0;
            FSide = 0;
            FHLT = true;
            FDDEN = true;
            FTrackReg = 0;
            FSectorReg = 1;
            FDataReg = 0;
            FStatus = 0;
            FStepDirection = 1;
            FLastWasTypeI = true;
            FTransferActive = false;
            FTransferKind = TransferKind.None;
            FIntrq = false;
            FOverrunCount = LostDataPolls;
            FHLD = false;
            FHldIdleFrames = 0;
            for (int i = 0; i < BetaDriveCount; i++)
                FDrive[i].CurrentTrack = 0;
        }

        private int DriveIndex => FSelectedDrive;

        private bool HasDisk => FDrive[DriveIndex].Loaded;

        private int SectorOffset(int track, int side, int sector)
        {
            // Standard .trd layout: both sides of a track stored consecutively
            // before the next track (matches the physical stepping order -- the
            // head moves once per track and reads/writes both sides while there).
            return ((track * FDrive[DriveIndex].Sides) + side) * TrackSize
                 + (sector - 1) * SectorSize;   // WD1793 sector numbers are 1-based
        }

        // ===========================================================================
        // Disk image management
        // ===========================================================================

        public bool InsertDisk(byte unitNo, string fileName)
        {
            if (unitNo >= BetaDriveCount) return false;
            EjectDisk(unitNo);
            if (!File.Exists(fileName)) return false;

            bool isSCL = string.Equals(Path.GetExtension(fileName), ".scl", StringComparison.OrdinalIgnoreCase);

            if (isSCL)
            {
                // Transparent SCL support: convert to a TRD image in memory on insert
                // (same approach SpecEmu/the real wd1793.dll take for their own
                // SCL2TRD) rather than requiring a separate "convert" step. SCL has no
                // room for TR-DOS's free-space bookkeeping, so any changes get saved
                // out as a sibling .trd rather than overwriting the source .scl.
                //
                // Port fix: the sibling .trd's name is chosen when the disk is first
                // written back, not here, and never one already taken -- game.trd,
                // else game (1).trd, and so on -- so a .trd that was already there, or
                // another drive's copy of the same SCL, is never overwritten.
                byte[] sclBytes;
                try
                {
                    sclBytes = File.ReadAllBytes(fileName);
                    if (sclBytes.Length <= 0) return false;
                }
                catch (Exception) { return false; }

                if (!TrdImage.ConvertSclToTrd(sclBytes, out byte[] trdBytes)) return false;

                FDrive[unitNo].Sides = 2;
                FDrive[unitNo].Tracks = trdBytes.Length / (SectorsPerTrack * 2 * SectorSize);
                FDrive[unitNo].Data = trdBytes;

                FDrive[unitNo].Loaded = true;
                FDrive[unitNo].FileName = "";
                FDrive[unitNo].SclFileName = fileName;
                FDrive[unitNo].Modified = false;
                FDrive[unitNo].CurrentTrack = 0;
                FDrive[unitNo].WriteProtected = false;
                return true;
            }

            try
            {
                long size = new FileInfo(fileName).Length;
                if (size <= 0 || size % SectorSize != 0) return false;
                long totalSectors = size / SectorSize;

                // Infer geometry from file size -- deliberately not from the
                // embedded system-sector "disk type" byte, which real disk-creation
                // tools don't always set correctly/consistently; file size is
                // unambiguous. Double-sided is tried first since it's by far the
                // most common real-world .trd layout.
                //
                // Port fix: size alone is not unambiguous. A single-sided disk with an
                // even number of tracks (40 or 80, the usual ones) is the same size as
                // a double-sided one with half as many, and read as that its sectors
                // came from the wrong places. So when the size fits both, a disk type
                // byte that says single-sided settles it, provided the track count it
                // gives is one the drive can reach.
                byte[] data = File.ReadAllBytes(fileName);
                bool fitsDoubleSided = totalSectors % (SectorsPerTrack * 2) == 0;
                bool fitsSingleSided = totalSectors % SectorsPerTrack == 0;
                if (fitsDoubleSided && !(SaysSingleSided(data) && totalSectors / SectorsPerTrack <= MaxTrack + 1))
                {
                    FDrive[unitNo].Sides = 2;
                    FDrive[unitNo].Tracks = (int)(totalSectors / (SectorsPerTrack * 2));
                }
                else if (fitsSingleSided)
                {
                    FDrive[unitNo].Sides = 1;
                    FDrive[unitNo].Tracks = (int)(totalSectors / SectorsPerTrack);
                }
                else
                    return false;  // not a whole number of sectors-per-track -- not a valid .trd

                FDrive[unitNo].Data = data;
            }
            catch (Exception) { return false; }

            FDrive[unitNo].Loaded = true;
            FDrive[unitNo].FileName = fileName;
            FDrive[unitNo].SclFileName = null;
            FDrive[unitNo].Modified = false;
            FDrive[unitNo].CurrentTrack = 0;
            FDrive[unitNo].WriteProtected = (File.GetAttributes(fileName) & FileAttributes.ReadOnly) != 0;
            return true;
        }

        // The furthest a Seek or Step takes the head (the real wd1793.dll's clamp).
        private const int MaxTrack = 0x52;

        /// <summary>The system sector's disk type byte says one side (40 or 80 tracks).</summary>
        private static bool SaysSingleSided(byte[] data)
        {
            int at = TrdImage.SystemSectorIndex * SectorSize + TrdImage.TRDSYS_DiskType;
            return at < data.Length && (data[at] == TrdImage.DiskType_80_1 || data[at] == TrdImage.DiskType_40_1);
        }

        public void EjectDisk(byte unitNo)
        {
            if (unitNo >= BetaDriveCount) return;
            FlushDisk(unitNo);
            FDrive[unitNo].Loaded = false;
            FDrive[unitNo].FileName = "";
            FDrive[unitNo].SclFileName = null;
            FDrive[unitNo].Data = null;
        }

        public bool DiskInserted(byte unitNo) => unitNo < BetaDriveCount && FDrive[unitNo].Loaded;

        /// <summary>
        /// The file the disk is written back to. For an SCL that is a .trd beside it, named when
        /// the disk is first written back; until then, the SCL itself.
        /// </summary>
        public string DiskFileName(byte unitNo)
        {
            if (unitNo >= BetaDriveCount) return "";
            return string.IsNullOrEmpty(FDrive[unitNo].FileName) && FDrive[unitNo].SclFileName != null
                ? FDrive[unitNo].SclFileName
                : FDrive[unitNo].FileName;
        }

        public bool DiskWriteProtected(byte unitNo) => unitNo < BetaDriveCount && FDrive[unitNo].WriteProtected;

        public void SetWriteProtected(byte unitNo, bool value)
        {
            if (unitNo < BetaDriveCount)
                FDrive[unitNo].WriteProtected = value;
        }

        /// <summary>Written to since it went in (or since it was last flushed).</summary>
        public bool DiskModified(byte unitNo) => unitNo < BetaDriveCount && FDrive[unitNo].Loaded && FDrive[unitNo].Modified;

        public void FlushDisk(byte unitNo)
        {
            if (unitNo >= BetaDriveCount) return;
            if (!FDrive[unitNo].Loaded) return;
            if (!FDrive[unitNo].Modified) return;
            if (string.IsNullOrEmpty(FDrive[unitNo].FileName) && FDrive[unitNo].SclFileName != null)
                FDrive[unitNo].FileName = TrdImage.FreeTrdNameFor(FDrive[unitNo].SclFileName);
            if (string.IsNullOrEmpty(FDrive[unitNo].FileName)) return;
            try
            {
                File.WriteAllBytes(FDrive[unitNo].FileName, FDrive[unitNo].Data ?? Array.Empty<byte>());
                FDrive[unitNo].Modified = false;
            }
            catch (Exception) { }
        }

        /// <summary>Write back every changed disk (the Pascal's destructor).</summary>
        public void FlushAll()
        {
            for (int i = 0; i < BetaDriveCount; i++)
                FlushDisk((byte)i);
        }

        // ===========================================================================
        // WD1793 command dispatch
        // ===========================================================================

        public void WriteCommand(byte value)
        {
            // A new command write terminates whatever Type II/III transfer was still
            // in progress -- real WD1793 behaviour (the CPU is expected to only write
            // a new command once the previous one has completed/DRQ serviced, but if
            // it doesn't, the old operation is simply abandoned, same as a real Force
            // Interrupt would do to it). Type I (Restore/Seek/Step) commands need no
            // such abandon-logic any more -- they complete synchronously, in full,
            // before this procedure returns, so
            // there is never anything of theirs left pending for a following command
            // to clobber.
            FTransferActive = false;
            FTransferKind = TransferKind.None;
            FIntrq = false;   // From the DLL: a new command lowers INTRQ
            LoadHeadFor(value);

            int nibble = (value >> 4) & 0x0F;
            switch (nibble)
            {
                case 0: DoRestore(value); break;
                case 1: DoSeek(value); break;
                case 2: case 3: DoStep(value, FStepDirection); break;
                case 4: case 5: DoStep(value, 1); break;
                case 6: case 7: DoStep(value, -1); break;
                case 8: case 9: DoReadSector(value); break;
                case 10: case 11: DoWriteSector(value); break;
                case 12: DoReadAddress(value); break;
                case 13: DoForceInterrupt(value); break;
                case 14: DoReadTrack(value); break;
                case 15: DoWriteTrack(value); break;
            }

            // From the DLL: a command that is already over -- every Type I command, and
            // any Type II/III one that failed before transferring a byte -- interrupts
            // now. Force Interrupt only does when asked to by its condition bits ($D0
            // ends a command quietly; $D8 and the others interrupt).
            if (!FTransferActive && (nibble != 13 || (value & 0x0F) != 0))
                FIntrq = true;
        }

        // Restore/Seek/Step/StepIn/StepOut all complete synchronously, in full,
        // within this single call

        private void DoRestore(byte cmd)
        {
            FLastWasTypeI = true;
            FDrive[DriveIndex].CurrentTrack = 0;
            FTrackReg = 0;   // Restore always updates the Track Register
            FStatus = ST_TRACK0;
            if ((cmd & 0x08) != 0) FStatus |= ST_HEADLOADED;
            if (!HasDisk) FStatus |= ST_NOTREADY;
        }

        private void DoSeek(byte cmd)
        {
            FLastWasTypeI = true;
            int target = FDataReg;   // target track is whatever's in the Data register
            if (target > 0x52) target = 0x52;   // real wd1793.dll clamps the target to 82 (VA 0x40ce1f)
            FDrive[DriveIndex].CurrentTrack = target;
            FTrackReg = (byte)target;   // Seek always updates the Track Register
            FStatus = 0;
            if (target == 0) FStatus |= ST_TRACK0;
            if ((cmd & 0x08) != 0) FStatus |= ST_HEADLOADED;
            if (!HasDisk) FStatus |= ST_NOTREADY;
        }

        private void DoStep(byte cmd, int direction)
        {
            FLastWasTypeI = true;
            FStepDirection = direction;

            int newTrack = FDrive[DriveIndex].CurrentTrack + direction;
            if (newTrack < 0) newTrack = 0;
            if (newTrack > 0x52) newTrack = 0x52;
            FDrive[DriveIndex].CurrentTrack = newTrack;
            if ((cmd & 0x10) != 0) FTrackReg = (byte)newTrack;   // 'T' bit -- see the datasheet's Step format

            FStatus = 0;
            if (newTrack == 0) FStatus |= ST_TRACK0;
            if ((cmd & 0x08) != 0) FStatus |= ST_HEADLOADED;
            if (!HasDisk) FStatus |= ST_NOTREADY;
        }

        private void DoForceInterrupt(byte cmd)
        {
            // Unconditionally terminates whatever was running and clears BUSY
            FTransferActive = false;
            FTransferKind = TransferKind.None;
            FStatus = (byte)(FStatus & ~ST_BUSY);
            if (!HasDisk) FStatus |= ST_NOTREADY;
        }

        private void DoReadSector(byte cmd)
        {
            FLastWasTypeI = false;
            FStatus = 0;
            if (!HasDisk)
            {
                FStatus = ST_NOTREADY;
                return;
            }
            if (FSectorReg < 1 || FSectorReg > SectorsPerTrack)
            {
                FStatus = ST_RNF;
                return;
            }
            LoadSectorIntoBuffer();
            StartSectorTransfer(false, (cmd & 0x10) != 0);
        }

        private void DoWriteSector(byte cmd)
        {
            FLastWasTypeI = false;
            FStatus = 0;
            if (!HasDisk)
            {
                FStatus = ST_NOTREADY;
                return;
            }
            if (FDrive[DriveIndex].WriteProtected)
            {
                FStatus = ST_WRITEPROTECT;
                return;
            }
            if (FSectorReg < 1 || FSectorReg > SectorsPerTrack)
            {
                FStatus = ST_RNF;
                return;
            }
            Array.Clear(FTransferBuffer, 0, SectorSize);
            StartSectorTransfer(true, (cmd & 0x10) != 0);
        }

        private void DoReadAddress(byte cmd)
        {
            // Returns a 6-byte ID field (Track,Side,Sector,Length-code,CRC1,CRC2) via
            // the data register, one byte per DRQ
            FLastWasTypeI = false;
            FStatus = 0;
            if (!HasDisk)
            {
                FStatus = ST_NOTREADY;
                return;
            }
            FTransferBuffer[0] = (byte)FDrive[DriveIndex].CurrentTrack;
            FTransferBuffer[1] = (byte)FSide;
            FTransferBuffer[2] = FSectorReg;
            FTransferBuffer[3] = 1;   // length code 1 = 256-byte sectors
            FTransferBuffer[4] = 0;   // CRC bytes -- not meaningful for a flat logical
            FTransferBuffer[5] = 0;   // image with no real per-sector CRC data
            FSectorReg = FTransferBuffer[0];   // Read Address also loads the sector
                                               // register from the track byte of the
                                               // ID field just read
            FTransferActive = true;
            FTransferIsWrite = false;
            FTransferMultiple = false;
            FTransferKind = TransferKind.ReadAddress;
            FTransferPos = 0;
            FTransferLength = 6;
            FOverrunCount = LostDataPolls;
            FStatus = ST_BUSY | ST_DRQ;
        }

        private void DoReadTrack(byte cmd)
        {
            // Real FORMAT-verification tooling aside, TR-DOS software essentially
            // never issues this
            FLastWasTypeI = false;
            FStatus = 0;
            if (!HasDisk) FStatus = ST_NOTREADY;
        }

        private void DoWriteTrack(byte cmd)
        {
            FLastWasTypeI = false;
            FStatus = 0;
            if (!HasDisk)
            {
                FStatus = ST_NOTREADY;
                return;
            }
            if (FDrive[DriveIndex].WriteProtected)
            {
                FStatus = ST_WRITEPROTECT;
                return;
            }

            // The WD1793 command format is 11110E00. The reference DLL accepts either
            // value of E but rejects the other flag bits rather than starting a data
            // transfer (Write Track handler VA 0x40d338/0x40d3a6).
            if ((cmd & 0x0B) != 0) return;

            Array.Clear(FTransferBuffer);
            FTransferActive = true;
            FTransferIsWrite = true;
            FTransferMultiple = false;
            FTransferKind = TransferKind.WriteTrack;
            FTransferPos = 0;
            FTransferLength = WriteTrackLength;
            FStatus = ST_BUSY | ST_DRQ;
        }

        private void StartSectorTransfer(bool isWrite, bool multiple)
        {
            FTransferActive = true;
            FTransferIsWrite = isWrite;
            FTransferMultiple = multiple;
            FTransferKind = TransferKind.Sector;
            FTransferPos = 0;
            FTransferLength = SectorSize;
            FOverrunCount = LostDataPolls;
            FStatus = ST_BUSY | ST_DRQ;
        }

        private void LoadSectorIntoBuffer()
        {
            int offset = SectorOffset(FDrive[DriveIndex].CurrentTrack, FSide, FSectorReg);
            byte[] data = FDrive[DriveIndex].Data;
            if (offset >= 0 && offset + SectorSize <= data.Length)
                Array.Copy(data, offset, FTransferBuffer, 0, SectorSize);
            else
                Array.Clear(FTransferBuffer, 0, SectorSize);
            OnSectorTransfer?.Invoke(FDrive[DriveIndex].CurrentTrack, FSide, FSectorReg, false);
        }

        private void StoreBufferToSector()
        {
            int offset = SectorOffset(FDrive[DriveIndex].CurrentTrack, FSide, FSectorReg);
            byte[] data = FDrive[DriveIndex].Data;
            if (offset >= 0 && offset + SectorSize <= data.Length)
            {
                Array.Copy(FTransferBuffer, 0, data, offset, SectorSize);
                FDrive[DriveIndex].Modified = true;
            }
            OnSectorTransfer?.Invoke(FDrive[DriveIndex].CurrentTrack, FSide, FSectorReg, true);
        }

        private void CommitWriteTrack()
        {
            // The real DLL buffers a whole 6250-byte revolution before parsing it.
            // An ID is usable only when its size code describes this image's fixed
            // 256-byte sectors and its generated-CRC marker ($F7) is present. C and H
            // are deliberately not used for addressing: formatting writes beneath the
            // currently selected physical head, while a flat TRD cannot store CHRN.
            int i = 0;
            byte idSector = 0;
            bool haveID = false;
            while (i < FTransferLength)
            {
                if (FTransferBuffer[i] == 0xFE)
                {
                    if (i + 5 >= FTransferLength) break;
                    idSector = FTransferBuffer[i + 3];
                    haveID = idSector >= 1 && idSector <= SectorsPerTrack
                          && FTransferBuffer[i + 4] == 1
                          && FTransferBuffer[i + 5] == 0xF7;
                    i += 6;
                    continue;
                }

                if (FTransferBuffer[i] == 0xFB && haveID)
                {
                    int dataEnd = i + 1 + SectorSize;
                    if (dataEnd >= FTransferLength) break;
                    ref Drive drive = ref FDrive[DriveIndex];
                    if (FTransferBuffer[dataEnd] == 0xF7
                        && drive.CurrentTrack >= 0
                        && drive.CurrentTrack < drive.Tracks
                        && FSide >= 0 && FSide < drive.Sides)
                    {
                        int offset = SectorOffset(drive.CurrentTrack, FSide, idSector);
                        if (offset >= 0 && offset + SectorSize <= drive.Data.Length)
                        {
                            Array.Copy(FTransferBuffer, i + 1, drive.Data, offset, SectorSize);
                            drive.Modified = true;
                            OnSectorTransfer?.Invoke(drive.CurrentTrack, FSide, idSector, true);
                        }
                    }
                    haveID = false;
                    i = dataEnd + 1;  // skip data so marker-like bytes inside it are inert
                    continue;
                }

                i++;
            }
        }

        private void FinishTransfer()
        {
            EndTransfer();
            if (!FTransferActive) FIntrq = true;   // From the DLL: the command is over (not moving on to the next sector)
        }

        // The Pascal's FinishTransfer, unchanged.
        private void EndTransfer()
        {
            FTransferActive = false;
            FTransferKind = TransferKind.None;
            if (FTransferMultiple)
            {
                // 'm' flag: automatically continue onto the next sector rather than
                // completing -- real WD1793 behaviour, used by TR-DOS's own
                // multi-sector load routines.
                FSectorReg++;
                if (FSectorReg <= SectorsPerTrack)
                {
                    if (FTransferIsWrite)
                    {
                        if (FDrive[DriveIndex].WriteProtected)
                        {
                            FStatus = ST_WRITEPROTECT;
                            return;
                        }
                        Array.Clear(FTransferBuffer, 0, SectorSize);
                        StartSectorTransfer(true, true);
                    }
                    else
                    {
                        LoadSectorIntoBuffer();
                        StartSectorTransfer(false, true);
                    }
                    return;
                }
                // Ran off the end of the track -- real hardware sets Record Not Found
                // here rather than silently wrapping to track+1/sector 1.
                FStatus = ST_RNF;
                return;
            }
            FStatus = 0;
            if (!HasDisk) FStatus |= ST_NOTREADY;
        }

        // ===========================================================================
        // Status register
        // ===========================================================================

        public byte ReadStatus()
        {
            // Bits 1/2/4/5 mean different things depending on whether the last
            // command was Type I or Type II/III -- see the ST_* constants' own
            // comments. FStatus carries the correct bit values for whichever command
            // set them, except for two things recomputed live on every read rather
            // than cached at command-issue time -- confirmed by disassembling the
            // real wd1793.dll (spin1_src\wd1793.dll, wd1793_ReadStatusReg, RVA
            // 0xd45c), since no source for it exists anywhere:
            //  - NOT READY reflects the drive's *current* disk-present state, not
            //    whatever it was when the last command ran, so ejecting/inserting a
            //    disk while idle is visible on the very next status read.
            //  - INDEX (Type I only, bit 1) toggles on every read while a Type I
            //    command is the active context, simulating the rotating index hole.
            //    Real Beta128 clones don't wire up a real "disk present" sensor onto
            //    this signal -- the motor/index line runs regardless of whether media
            //    is actually loaded, and TR-DOS detects "no disk" indirectly via RNF/
            //    CRC errors during an actual read, not via INDEX or NOT READY. This
            //    is NOT gated on HasDisk (confirmed by cross-checking the real TR-DOS
            //    5.03 ROM's own boot-time hardware probe, sub_3dadh @ $3DAD, which
            //    polls exactly this bit waiting for it to toggle before proceeding to
            //    its command line, and by the user confirming Spin1/SpecEmu both
            //    reach the TR-DOS command line with no disk inserted -- gating this on
            //    HasDisk, as an earlier version of this fix did, broke that). Real
            //    trackloaders (common in demoscene code that drives the WD1793
            //    directly, bypassing TR-DOS) can also poll this bit as a rotation-
            //    sync signal; it was previously hardcoded to always read 0, which
            //    would hang either kind of loop forever.
            if (FLastWasTypeI)
                FStatus ^= ST_INDEX;

            FIntrq = false;   // From the DLL: reading the status register lowers INTRQ

            // DRQ/BUSY (Type II/III) are returned exactly as the command/data-register
            // handlers last set them -- no elapsed-time gating. See this unit's header
            // comment for the wd1793.dll disassembly evidence that the real
            // ReadStatusReg handler has no timing logic at all.
            if (HasDisk)
                return (byte)(FStatus & ~ST_NOTREADY);
            return (byte)(FStatus | ST_NOTREADY);
        }

        public byte ReadTrack() => FTrackReg;

        public void WriteTrack(byte value) => FTrackReg = value;

        public byte ReadSectorReg() => FSectorReg;

        public void WriteSectorReg(byte value) => FSectorReg = value;

        public byte ReadData()
        {
            if (FTransferActive && !FTransferIsWrite)
            {
                byte result = FTransferBuffer[FTransferPos];
                FTransferPos++;
                FOverrunCount = LostDataPolls;
                if (FTransferPos >= FTransferLength)
                    FinishTransfer();
                return result;
            }
            return FDataReg;
        }

        public void WriteData(byte value)
        {
            FDataReg = value;
            if (FTransferActive && FTransferIsWrite)
            {
                FTransferBuffer[FTransferPos] = value;
                FTransferPos++;
                if (FTransferPos >= FTransferLength)
                {
                    switch (FTransferKind)
                    {
                        case TransferKind.Sector: StoreBufferToSector(); break;
                        case TransferKind.WriteTrack: CommitWriteTrack(); break;
                    }
                    FinishTransfer();
                }
            }
        }

        public void WriteSystemRegister(byte value)
        {
            // Bit layout confirmed against Fuse's peripherals/disk/beta.c
            // (beta_sp_write) -- see this unit's header comment.
            FSelectedDrive = value & 0x03;                     // bits 0-1: drive select
            FHLT = (value & 0x08) != 0;                        // bit 3: block HLT, normally set
            FSide = 1;
            if ((value & 0x10) != 0) FSide = 0;                // bit 4: side select, INVERTED (set = side 0)
            FDDEN = (value & 0x20) != 0;                       // bit 5: density (0=FM, 1=MFM)
        }

        public byte ReadSystemRegister()
        {
            // Real Beta128 hardware relocates the WD1793's DRQ and (inverted) BUSY
            // signals onto bits 6/7 of this port's READ value -- a separate signal
            // path from the normal status register at $1F, confirmed by disassembling
            // the real TR-DOS 5.03 ROM:
            // its own sector-transfer byte-pump loop (sub_3fe5h/sub_3fcah in the
            // programandala-net/tr-dos disassembly) polls `IN A,($FF)` / `AND $C0`
            // in a tight loop instead of re-reading $1F, pumping one byte via
            // INI/OUTI once bit 6 (DRQ) is set, and stopping via `RET M` (i.e. once
            // bit 7 alone is set, meaning "not busy / done"). Previously unimplemented
            // (this port only had a write handler, for the system register itself),
            // which meant that loop could never see either bit change and gave up
            // immediately without transferring a single byte -- TR-DOS then correctly
            // (from its own perspective) concluded the drive wasn't responding and
            // reported "no disk", even though the command had been accepted fine.
            //
            // From the DLL: bit 7 is INTRQ itself rather than "not busy". The same
            // while a command runs, but it stays down after a status read or a quiet
            // Force Interrupt, as on the real interface. And each poll during a read
            // counts towards Lost Data: see FOverrunCount.
            if (FTransferActive && !FTransferIsWrite && --FOverrunCount <= 0)
                LoseData();

            byte result = 0;
            if (FTransferActive && (FStatus & ST_DRQ) != 0)
                result |= 0x40;   // bit 6: DRQ -- a byte is ready to transfer
            if (FIntrq)
                result |= 0x80;   // bit 7: INTRQ -- the command is over
            return result;
        }

        // From the DLL: the CPU stopped taking a read's bytes. The command ends there,
        // with Lost Data, as wd1793_ReadSystemReg does it (including for a multi-sector
        // read, which goes no further). Writes are never timed out, as in the DLL.
        private void LoseData()
        {
            FTransferActive = false;
            FTransferKind = TransferKind.None;
            FStatus = (byte)((FStatus | ST_LOSTDATA) & ~(ST_BUSY | ST_DRQ));
            FIntrq = true;
        }

        // UI hooks for a disk-activity LED.
        public bool Busy => FTransferActive;
        public int SelectedDrive => FSelectedDrive;

        // From the data sheet: the head load output, HLD. On the Beta 128 it also enables the
        // drive select lines and starts the drive motor, so the selected drive is spinning, and
        // its light on, only while HLD is active. HLD comes on with any Type II or III command,
        // and with a Type I command whose h (head load) or V (verify) flag is set. It goes off
        // with a Type I command that has neither, or once the controller has been idle for 15
        // index pulses -- 15 revolutions, three seconds at 300 rpm. (FD179X data sheet,
        // Western Digital 1979, "Head Positioning"; Beta Disc/TRDOS, Аппаратные средства, ch. 1.)
        private bool FHLD;
        private int FHldIdleFrames;

        public const int FramesPerRevolution = 10;    // 300 rpm at 50 frames a second
        public const int HeadUnloadRevolutions = 15;

        /// <summary>HLD: the head is loaded, so the selected drive is selected, spinning and lit.</summary>
        public bool HeadLoaded => FHLD;

        /// <summary>
        /// Let emulated time pass (the controller otherwise has none): an idle controller unloads
        /// the head after <see cref="HeadUnloadRevolutions"/> revolutions.
        /// </summary>
        public void ElapseFrames(int frames)
        {
            if (!FHLD || FTransferActive) return;   // only idle revolutions count
            FHldIdleFrames += frames;
            if (FHldIdleFrames >= HeadUnloadRevolutions * FramesPerRevolution)
                FHLD = false;
        }

        // From the data sheet: what a command does to HLD, before it runs. A Type II or III
        // command finds the drive not ready before it loads the head, so an empty drive stays
        // dark. Force Interrupt leaves HLD as it is.
        private void LoadHeadFor(byte command)
        {
            FHldIdleFrames = 0;
            int nibble = (command >> 4) & 0x0F;
            if (nibble < 8)
                FHLD = (command & 0x0C) != 0;     // Type I: h (bit 3) or V (bit 2)
            else if (nibble != 13 && HasDisk)
                FHLD = true;                      // Type II and III
        }
    }
}
