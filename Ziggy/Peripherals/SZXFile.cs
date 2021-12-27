using System;
using System.IO;
using System.Runtime.InteropServices;
using zlib;

namespace Peripherals
{
    public class ByteUtililty {
        public static byte[] RawSerialize(object anything) {
            int rawsize = Marshal.SizeOf(anything);
            IntPtr buffer = Marshal.AllocHGlobal(rawsize);
            Marshal.StructureToPtr(anything, buffer, false);
            byte[] rawdatas = new byte[rawsize];
            Marshal.Copy(buffer, rawdatas, 0, rawsize);
            Marshal.FreeHGlobal(buffer);
            return rawdatas;
        }

        /// <summary>
        /// converts byte[] to struct
        /// </summary>
        public static T RawDeserialize<T>(byte[] rawData, int position) {
            int rawsize = Marshal.SizeOf(typeof(T));
            if (rawsize > rawData.Length - position)
                throw new ArgumentException("Not enough data to fill struct. Array length from position: " + (rawData.Length - position) + ", Struct length: " + rawsize);
            IntPtr buffer = Marshal.AllocHGlobal(rawsize);
            Marshal.Copy(rawData, position, buffer, rawsize);
            T retobj = (T)Marshal.PtrToStructure(buffer, typeof(T));
            Marshal.FreeHGlobal(buffer);
            return retobj;
        }
    }

    //Supports SZX 1.4 specification
    public class SZXFile
    {
        public enum ZXTYPE
        {
            ZXSTMID_16K = 0,
            ZXSTMID_48K,
            ZXSTMID_128K,
            ZXSTMID_PLUS2,
            ZXSTMID_PLUS2A,
            ZXSTMID_PLUS3,
            ZXSTMID_PLUS3E,
            ZXSTMID_PENTAGON128,
            ZXSTMID_TC2048,
            ZXSTMID_TC2068,
            ZXSTMID_SCORPION,
            ZXSTMID_SE,
            ZXSTMID_TS2068,
            ZXSTMID_PENTAGON512,
            ZXSTMID_PENTAGON1024,
            ZXSTMID_128KE
        }

        public const int ZXSTZF_EILAST = 1;
        public const int ZXSTZF_HALTED = 2;
        public const int ZXSTRF_COMPRESSED = 1;
        public const int ZXSTKF_ISSUE2 = 1;
        public const int ZXSTMF_ALTERNATETIMINGS = 1;

        public const int SZX_VERSION_SUPPORTED_MAJOR = 1;
        public const int SZX_VERSION_SUPPORTED_MINOR = 4;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZXST_Header
        {
            public uint Magic;
            public byte MajorVersion;
            public byte MinorVersion;
            public byte MachineId;
            public byte Flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZXST_Creator
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public char[] CreatorName;

            public short MajorVersion;
            public short MinorVersion;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public byte[] Data;
        }

        // Block Header. Each real block starts
        // with this header.
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZXST_Block
        {
            public uint Id;
            public uint Size;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZXST_Z80Regs
        {
            public ushort AF, BC, DE, HL;
            public ushort AF1, BC1, DE1, HL1;
            public ushort IX, IY, SP, PC;
            public byte I;
            public byte R;
            public byte IFF1, IFF2;
            public byte IM;
            public uint CyclesStart;
            public byte HoldIntReqCycles;
            public byte Flags;
            public ushort MemPtr;
            //public byte BitReg;
            //public byte Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZXST_SpecRegs
        {
            public byte Border;
            public byte x7ffd;
            public byte pagePort; //either 0x1ffd (+2A/+3) or 0xeff7 (Pentagon 1024)
            public byte Fe;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZXST_RAMPage
        {
            public ushort wFlags;
            public byte chPageNo;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZXST_AYState
        {
            public byte cFlags;
            public byte currentRegister;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] chRegs;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZXST_Keyboard
        {
            public uint Flags;
            public byte KeyboardJoystick;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZXST_Tape
        {
            public ushort currentBlockNo;
            public ushort flags;
            public int uncompressedSize;
            public int compressedSize;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public char[] fileExtension;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZXST_Plus3Disk
        {
            public byte numDrives;
            public byte motorOn;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZXST_DiskFile
        {
            public ushort flags;
            public byte driveNum;
            public int uncompressedSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ZXST_PaletteBlock
        {
            public byte flags;
            public byte currentRegister;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] paletteRegs;
        }

        public byte[][] RAM_BANK = new byte[16][];       //Contents of the 8192*16 ram banks

        public ZXST_Header header;
        public ZXST_Creator creator;
        public ZXST_Z80Regs z80Regs;
        public ZXST_SpecRegs specRegs;
        public ZXST_Keyboard keyboard;
        public ZXST_AYState ayState;
        public ZXST_Tape tape;
        public ZXST_Plus3Disk plus3Disk;
        public ZXST_DiskFile[] plus3DiskFile;
        public ZXST_PaletteBlock palette;

        public byte[] embeddedTapeData;
        public String externalTapeFile;

        public byte numDrivesPresent = 0;
        public bool InsertTape = false;
        public bool[] InsertDisk;
        public String[] externalDisk;
        public bool paletteLoaded = false;

        public string GetID(uint id) {
            byte[] b = System.BitConverter.GetBytes(id);
            string idString = String.Format("{0}{1}{2}{3}", (char)b[0], (char)b[1], (char)b[2], (char)b[3]);
            return idString;
        }

        private uint GetUIntFromString(string data) {
            byte[] carray = System.Text.ASCIIEncoding.UTF8.GetBytes(data);
            uint val = BitConverter.ToUInt32(carray, 0);
            return val;
        }

        public bool LoadSZX(ref byte[] buffer) {
            if (buffer.Length == 0)
                return false; //something bad happened!

            for (int f = 0; f < 16; f++)
                RAM_BANK[f] = new byte[8192];

            GCHandle pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            //Read in the szx header to begin proceedings
            header = (ZXST_Header)Marshal.PtrToStructure(pinnedBuffer.AddrOfPinnedObject(),
                                                                     typeof(ZXST_Header));

            if (header.MajorVersion != 1) {
                pinnedBuffer.Free();
                return false;
            }

            string formatName = GetID(header.Magic);
            int bufferCounter = Marshal.SizeOf(header);

            while (bufferCounter < buffer.Length) {
                //Read the block info
                ZXST_Block block = (ZXST_Block)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferCounter),
                                                                     typeof(ZXST_Block));

                bufferCounter += Marshal.SizeOf(block);
                string blockID = GetID(block.Id);
                switch (blockID) {
                    case "SPCR":
                    //Read the ZXST_SpecRegs structure
                    specRegs = (ZXST_SpecRegs)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferCounter),
                                                                         typeof(ZXST_SpecRegs));
                    break;

                    case "Z80R":
                    //Read the ZXST_SpecRegs structure
                    z80Regs = (ZXST_Z80Regs)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferCounter),
                                                                         typeof(ZXST_Z80Regs));
                    break;

                    case "KEYB":
                    //Read the ZXST_SpecRegs structure
                    keyboard = (ZXST_Keyboard)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferCounter),
                                                                         typeof(ZXST_Keyboard));
                    break;

                    case "AY\0\0":
                    ayState = (ZXST_AYState)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferCounter),
                                                                        typeof(ZXST_AYState));
                    break;

                    case "+3\0\0":
                    plus3Disk = (ZXST_Plus3Disk)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferCounter),
                                                                        typeof(ZXST_Plus3Disk));

                    numDrivesPresent = plus3Disk.numDrives;
                    plus3DiskFile = new ZXST_DiskFile[plus3Disk.numDrives];
                    externalDisk = new String[plus3Disk.numDrives];
                    InsertDisk = new bool[plus3Disk.numDrives];
                    break;

                    case "DSK\0":
                    ZXST_DiskFile df = (ZXST_DiskFile)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferCounter),
                                                                        typeof(ZXST_DiskFile));

                    plus3DiskFile[df.driveNum] = df;
                    InsertDisk[df.driveNum] = true;

                    int offset2 = bufferCounter + Marshal.SizeOf(df);
                    char[] file = new char[df.uncompressedSize];
                    Array.Copy(buffer, offset2, file, 0, df.uncompressedSize); //leave out the \0 terminator
                    externalDisk[df.driveNum] = new String(file);
                    break;

                    case "TAPE":
                    tape = (ZXST_Tape)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferCounter),
                                                                        typeof(ZXST_Tape));
                    InsertTape = true;

                    //Embedded tape file
                    if ((tape.flags & 1) != 0) {
                        int offset = bufferCounter + Marshal.SizeOf(tape);
                        //Compressed?
                        if ((tape.flags & 2) != 0) {
                            MemoryStream compressedData = new MemoryStream(buffer, offset, tape.compressedSize);
                            MemoryStream uncompressedData = new MemoryStream();
                            using (ZInputStream zipStream = new ZInputStream(compressedData)) {
                                byte[] tempBuffer = new byte[2048];
                                int bytesUnzipped = 0;
                                while ((bytesUnzipped = zipStream.read(tempBuffer, 0, 2048)) > 0) {
                                    uncompressedData.Write(tempBuffer, 0, bytesUnzipped);
                                }
                                embeddedTapeData = uncompressedData.ToArray();
                                compressedData.Close();
                                uncompressedData.Close();
                            }
                        }
                        else {
                            embeddedTapeData = new byte[tape.compressedSize];
                            Array.Copy(buffer, offset, embeddedTapeData, 0, tape.compressedSize);
                        }
                    }
                    else //external tape file
                  {
                        int offset = bufferCounter + Marshal.SizeOf(tape);
                        char[] file2 = new char[tape.compressedSize - 1];
                        Array.Copy(buffer, offset, file2, 0, tape.compressedSize - 1); //leave out the \0 terminator
                        externalTapeFile = new String(file2);
                    }
                    break;

                    case "RAMP":
                    //Read the ZXST_SpecRegs structure
                    ZXST_RAMPage ramPages = (ZXST_RAMPage)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferCounter),
                                                                         typeof(ZXST_RAMPage));
                    if (ramPages.wFlags == ZXSTRF_COMPRESSED) {
                        int offset = bufferCounter + Marshal.SizeOf(ramPages);
                        int compressedSize = ((int)block.Size - (Marshal.SizeOf(ramPages)));//  - Marshal.SizeOf(block) - 1 ));
                        MemoryStream compressedData = new MemoryStream(buffer, offset, compressedSize);
                        MemoryStream uncompressedData = new MemoryStream();
                        using (ZInputStream zipStream = new ZInputStream(compressedData)) {
                            byte[] tempBuffer = new byte[2048];
                            int bytesUnzipped = 0;
                            while ((bytesUnzipped = zipStream.read(tempBuffer, 0, 2048)) > 0) {
                                uncompressedData.Write(tempBuffer, 0, bytesUnzipped);
                            }
                            byte[] pageData = uncompressedData.ToArray();
                            {
                                Array.Copy(pageData, 0, RAM_BANK[ramPages.chPageNo * 2], 0, 8192);
                                Array.Copy(pageData, 0 + 8192, RAM_BANK[ramPages.chPageNo * 2 + 1], 0, 8192);
                            }
                            compressedData.Close();
                            uncompressedData.Close();
                        }
                    }
                    else {
                        int bufferOffset = bufferCounter + Marshal.SizeOf(ramPages);
                        {
                            Array.Copy(buffer, bufferOffset, RAM_BANK[ramPages.chPageNo * 2], 0, 8192);
                            Array.Copy(buffer, bufferOffset + 8192, RAM_BANK[ramPages.chPageNo * 2 + 1], 0, 8192);
                        }
                    }
                    break;

                    case "PLTT":
                    palette = (ZXST_PaletteBlock)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferCounter),
                                                                         typeof(ZXST_PaletteBlock));

                    paletteLoaded = true;
                    break;

                    default: //unrecognised block, so skip to next
                    break;
                }

                bufferCounter += (int)block.Size; //Move to next block
            }
            pinnedBuffer.Free();
            return true;
        }

        public bool LoadSZX(Stream fs) {
            bool loaded = false;
            using (BinaryReader r = new BinaryReader(fs)) {
                int bytesToRead = (int)fs.Length;

                byte[] buffer = new byte[bytesToRead];
                int bytesRead = r.Read(buffer, 0, bytesToRead);
                loaded = LoadSZX(ref buffer);
            }
            return loaded;
        }

        public bool LoadSZX(string filename) {
            bool readSZX = false;
            try {
                using (FileStream fs = new FileStream(filename, FileMode.Open)) {
                    readSZX = LoadSZX(fs);
                }
            } catch {
                readSZX = false;
            }
            return readSZX;
        }

        public void SaveSZX(string filename) {
            using (FileStream fs = new FileStream(filename, FileMode.Create)) {
                byte[] szxData = GetSZXData();
                fs.Write(szxData, 0, szxData.Length);
            }
        }

        public byte[] GetSZXData() {
            byte[] szxData = null;
            using (MemoryStream ms = new MemoryStream(1000)) {
                using (BinaryWriter r = new BinaryWriter(ms)) {
                    byte[] buf;
                    ZXST_Block block = new ZXST_Block();

                    buf = ByteUtililty.RawSerialize(header); //header is filled in by the callee machine
                    r.Write(buf);

                    block.Id = GetUIntFromString("CRTR");
                    block.Size = (uint)Marshal.SizeOf(creator);
                    buf = ByteUtililty.RawSerialize(block);
                    r.Write(buf);
                    buf = ByteUtililty.RawSerialize(creator);
                    r.Write(buf);

                    block.Id = GetUIntFromString("Z80R");
                    block.Size = (uint)Marshal.SizeOf(z80Regs);
                    buf = ByteUtililty.RawSerialize(block);
                    r.Write(buf);
                    buf = ByteUtililty.RawSerialize(z80Regs);
                    r.Write(buf);

                    block.Id = GetUIntFromString("SPCR");
                    block.Size = (uint)Marshal.SizeOf(specRegs);
                    buf = ByteUtililty.RawSerialize(block);
                    r.Write(buf);
                    buf = ByteUtililty.RawSerialize(specRegs);
                    r.Write(buf);

                    block.Id = GetUIntFromString("KEYB");
                    block.Size = (uint)Marshal.SizeOf(keyboard);
                    buf = ByteUtililty.RawSerialize(block);
                    r.Write(buf);
                    buf = ByteUtililty.RawSerialize(keyboard);
                    r.Write(buf);

                    if (paletteLoaded) {
                        block.Id = GetUIntFromString("PLTT");
                        block.Size = (uint)Marshal.SizeOf(palette);
                        buf = ByteUtililty.RawSerialize(block);
                        r.Write(buf);
                        buf = ByteUtililty.RawSerialize(palette);
                        r.Write(buf);
                    }
                    if (header.MachineId > (byte)ZXTYPE.ZXSTMID_48K) {
                        block.Id = GetUIntFromString("AY\0\0");
                        block.Size = (uint)Marshal.SizeOf(ayState);
                        buf = ByteUtililty.RawSerialize(block);
                        r.Write(buf);
                        buf = ByteUtililty.RawSerialize(ayState);
                        r.Write(buf);
                        byte[] ram = new byte[16384];
                        for (int f = 0; f < 8; f++) {
                            ZXST_RAMPage ramPage = new ZXST_RAMPage();
                            ramPage.chPageNo = (byte)f;
                            ramPage.wFlags = 0; //not compressed
                            block.Id = GetUIntFromString("RAMP");
                            block.Size = (uint)(Marshal.SizeOf(ramPage) + 16384);
                            buf = ByteUtililty.RawSerialize(block);
                            r.Write(buf);
                            buf = ByteUtililty.RawSerialize(ramPage);
                            r.Write(buf);
                            for (int g = 0; g < 8192; g++) {
                                ram[g] = (byte)(RAM_BANK[f * 2][g] & 0xff);
                                ram[g + 8192] = (byte)(RAM_BANK[f * 2 + 1][g] & 0xff);
                            }
                            r.Write(ram);
                        }
                    } else //48k
                    {
                        byte[] ram = new byte[16384];
                        //page 0
                        ZXST_RAMPage ramPage = new ZXST_RAMPage();
                        ramPage.chPageNo = 0;
                        ramPage.wFlags = 0; //not compressed
                        block.Id = GetUIntFromString("RAMP");
                        block.Size = (uint)(Marshal.SizeOf(ramPage) + 16384);
                        buf = ByteUtililty.RawSerialize(block);
                        r.Write(buf);
                        buf = ByteUtililty.RawSerialize(ramPage);
                        r.Write(buf);
                        for (int g = 0; g < 8192; g++) {
                            //me am angry.. poda thendi... saree vangi tharamattaaai?? poda! nonsense! style moonji..madiyan changu..malayalam ariyatha
                            //Lol! That's my wife cursing me for spending my time on this crap instead of her. Such a sweetie pie!
                            ram[g] = (byte)(RAM_BANK[0][g] & 0xff);
                            ram[g + 8192] = (byte)(RAM_BANK[1][g] & 0xff);
                        }
                        r.Write(ram);

                        //page 2
                        ramPage.chPageNo = 2;
                        ramPage.wFlags = 0; //not compressed
                        //ramPage.chData = new byte[16384];
                        // Array.Copy(RAM_BANK[2 * 2], 0, ramPage.chData, 0, 8192);
                        //Array.Copy(RAM_BANK[2 * 2 + 1], 0, ramPage.chData, 8192, 8192);
                        block.Id = GetUIntFromString("RAMP");
                        block.Size = (uint)(Marshal.SizeOf(ramPage) + 16384);
                        buf = ByteUtililty.RawSerialize(block);
                        r.Write(buf);
                        buf = ByteUtililty.RawSerialize(ramPage);
                        r.Write(buf);
                        for (int g = 0; g < 8192; g++) {
                            ram[g] = (byte)(RAM_BANK[ramPage.chPageNo * 2][g] & 0xff);
                            ram[g + 8192] = (byte)(RAM_BANK[ramPage.chPageNo * 2 + 1][g] & 0xff);
                        }
                        r.Write(ram);

                        //page 5
                        ramPage.chPageNo = 5;
                        ramPage.wFlags = 0; //not compressed
                        block.Id = GetUIntFromString("RAMP");
                        block.Size = (uint)(Marshal.SizeOf(ramPage) + 16384);
                        buf = ByteUtililty.RawSerialize(block);
                        r.Write(buf);
                        buf = ByteUtililty.RawSerialize(ramPage);
                        r.Write(buf);
                        for (int g = 0; g < 8192; g++) {
                            ram[g] = (byte)(RAM_BANK[ramPage.chPageNo * 2][g] & 0xff);
                            ram[g + 8192] = (byte)(RAM_BANK[ramPage.chPageNo * 2 + 1][g] & 0xff);
                        }
                        r.Write(ram);
                    }
                    if (InsertTape) {
                        tape = new ZXST_Tape();
                        block.Id = GetUIntFromString("TAPE");

                        char[] ext = System.IO.Path.GetExtension(externalTapeFile).ToLower().ToCharArray();
                        tape.fileExtension = new char[16];
                        for (int f = 1; f < ext.Length; f++)
                            tape.fileExtension[f - 1] = ext[f];

                        externalTapeFile = externalTapeFile + char.MinValue; //add a null terminator
                        tape.flags = 0;
                        tape.currentBlockNo = 0;
                        tape.compressedSize = externalTapeFile.Length;
                        tape.uncompressedSize = externalTapeFile.Length;
                        block.Size = (uint)Marshal.SizeOf(tape) + (uint)tape.uncompressedSize;
                        buf = ByteUtililty.RawSerialize(block);
                        r.Write(buf);
                        buf = ByteUtililty.RawSerialize(tape);
                        r.Write(buf);
                     
                        char[] tapeName = externalTapeFile.ToCharArray();

                        r.Write(tapeName);
                    }
                }

                szxData = ms.ToArray();
            }
            return szxData;
        }

    }
}