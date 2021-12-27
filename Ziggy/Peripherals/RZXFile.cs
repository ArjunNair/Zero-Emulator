#define NEW_RZX_METHODS

using System;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using zlib;
using System.Reflection;


namespace Peripherals
{
    public enum RZX_State {
        NONE,
        PLAYBACK,
        RECORDING
    }

    public enum RZX_BlockType {
        CREATOR = 0x10,
        SECURITY_INFO = 0x20,
        SECURITY_SIG = 0x21,
        SNAPSHOT = 0x30,
        RECORD = 0x80,
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RZX_Header {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public char[] signature;

        public byte majorVersion;
        public byte minorVersion;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RZX_Block {
        public byte id;
        public uint size;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RZX_Creator {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public char[] author;

        public ushort majorVersion;
        public ushort minorVersion;
        //custom data of adjusted block size bytes follows
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RZX_Snapshot {
        public uint flags;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public char[] extension;

        public uint uncompressedSize;
        //snapshot data/descriptor of adjusted block size bytes follows
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RZX_SnapshotDescriptor {
        public uint checksum;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RZX_Record {
        public uint numFrames;
        public byte reserved;
        public uint tstatesAtStart;
        public uint flags;
        //sequence of frames follows
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RZX_Frame {
        public ushort instructionCount;
        public ushort inputCount;
        public byte[] inputs;
    }

    public class RZXInfo {
        public RZX_Header header;
        public RZX_Creator creator;
        public List<RZX_Block> blocks;

        public override string ToString() {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(255);
            sb.Append(header.signature);
            sb.Append(" " + header.majorVersion + "." + header.minorVersion + "\nCreated by ");
            sb.Append(new String(creator.author, 0 , creator.author.Length - 1));
            sb.Append( creator.majorVersion + "." + creator.minorVersion);
            sb.Append("\nBlocks:\n");

            foreach(RZX_Block block in blocks) {
                sb.Append("ID: " + block.id);
                sb.Append(", Length: " + block.size);
                sb.Append("\n");
            }
            return sb.ToString();
        }
    }

    public class RZXSnapshotData {
        public String extension;
        public byte[] data;
    }

    public class RZXFileEventArgs {
        public RZXFile rzxInstance;
        public RZXInfo info;
        public RZXSnapshotData snapData;
        public RZX_BlockType blockID;
        public uint tstates;
        public int totalFramesInRecords;
        public bool hasEnded;
    }

    public class RZXFile {
        public System.Action<RZXFileEventArgs> RZXFileEventHandler;
        public RZX_Header header;
        public RZX_Creator creator;
        public RZX_Record record;
        public RZX_Snapshot snap;
        public char[][] snapshotExtension = new char[2][];
        public byte[][] snapshotData = new byte[2][];

        private bool isCompressedFrames = true;

        private BinaryWriter rzxFileWrite;
        private BinaryReader rzxFileReader;
        private BinaryReader frameInfoReader;

        private uint tstatesAtRecordStart = 0;
        private uint frameDataSize = 0;
        public int frameCount = 0;
        private int totalFramesPlayed = 0;
        private RZX_State state = RZX_State.NONE;
        private bool isRecordingBlock = false;

        private FileStream rzxFile;
        private FileStream frameInfoFile;

        private byte snapIndex = 0;
        private long currentRecordFilePos;
        
        private const string rzxSessionContinue = "Zero RZX Continue\0";
        private const string rzxSessionFinal = "Zero RZX Final   \0";
        private const string tempFrameInfoFile = "ZeroRZXFrame_temp.bin";

        //RZX Playback & Recording
        private class RollbackBookmark {
            public SZXFile snapshot;
            public long irbFilePos;
            public uint tstates;
        };

        public List<byte> inputs = new List<byte>();
        private List<byte> oldInputs = new List<byte>();
        private const int ZBUFLEN = 16384;
        private byte[] zBuffer;
        private byte[] fileBuffer;
        private ZStream zStream;
        private GCHandle pinnedBuffer;
        private bool isReading = false;
        private bool isReadingIRB = false;
        private int readBlockIndex = 0;
        public int fetchCount;
        public ushort inputCount;
        private int snapsLoaded = 0;

        //Used for rollbacks
        private int currentBookmark = 0;
        private List<RollbackBookmark> bookmarks = new List<RollbackBookmark>();

        public RZX_Frame frame;
        public List<RZX_Frame> frames = new List<RZX_Frame>();
        
        public int NumFramesPlayed {
            get {return totalFramesPlayed;}
        }

        #region v1
        /*
            public bool LoadRZX(Stream fs) {
                using (BinaryReader r = new BinaryReader(fs, System.Text.Encoding.UTF8)) {
                    int bytesToRead = (int)fs.Length;

                    byte[] buffer = new byte[bytesToRead];
                    int bytesRead = r.Read(buffer, 0, bytesToRead);

                    if (bytesRead == 0)
                        return false; //something bad happened!

                    GCHandle pinnedBuffer = GCHandle.Alloc(buffer, GCHandleType.Pinned);

                    //Read in the szx header to begin proceedings
                    header = (RZX_Header)Marshal.PtrToStructure(pinnedBuffer.AddrOfPinnedObject(),
                                                                             typeof(RZX_Header));

                    String sign = new String(header.signature);
                    if (sign != "RZX!") {
                        pinnedBuffer.Free();
                        return false;
                    }

                    int bufferCounter = Marshal.SizeOf(header);

                    while (bufferCounter < bytesRead) {
                        //Read the block info
                        RZX_Block block = (RZX_Block)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferCounter),
                                                                             typeof(RZX_Block));

                        bufferCounter += Marshal.SizeOf(block);
                        switch (block.id) {
                            case 0x10:
                                creator = (RZX_Creator)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferCounter),
                                                                             typeof(RZX_Creator));
                                break;

                            case 0x30:
                                snap = (RZX_Snapshot)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferCounter),
                                                                             typeof(RZX_Snapshot));

                                int offset = bufferCounter + Marshal.SizeOf(snap);

                                if ((snap.flags & 0x2) != 0) {
                                    int snapSize = (int)block.size - Marshal.SizeOf(snap) - Marshal.SizeOf(block);
                                    MemoryStream compressedData = new MemoryStream(buffer, offset, snapSize);
                                    MemoryStream uncompressedData = new MemoryStream();

                                    using (ZInputStream zipStream = new ZInputStream(compressedData)) {
                                        byte[] tempBuffer = new byte[2048];
                                        int bytesUnzipped = 0;

                                        while ((bytesUnzipped = zipStream.read(tempBuffer, 0, 2048)) > 0) {
                                            uncompressedData.Write(tempBuffer, 0, bytesUnzipped);
                                        }
                                        snapshotData[snapIndex] = uncompressedData.ToArray();
                                        compressedData.Close();
                                        uncompressedData.Close();
                                    }
                                }
                                else {
                                    snapshotData[snapIndex] = new byte[snap.uncompressedSize];
                                    Array.Copy(buffer, offset, snapshotData[snapIndex], 0, snap.uncompressedSize);
                                }

                                snapshotExtension[snapIndex] = snap.extension;
                                snapIndex += (byte)(snapIndex < 1 ? 1 : 0);
                                break;

                            case 0x80:
                                record = (RZX_Record)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(buffer, bufferCounter),
                                                                             typeof(RZX_Record));

                                int offset2 = bufferCounter + Marshal.SizeOf(record);
                                byte[] frameBuffer;
                                if ((record.flags & 0x2) != 0) {
                                    int frameSize = (int)block.size - Marshal.SizeOf(record) - Marshal.SizeOf(block);
                                    MemoryStream compressedData = new MemoryStream(buffer, offset2, frameSize);
                                    MemoryStream uncompressedData = new MemoryStream();
                                    using (ZInputStream zipStream = new ZInputStream(compressedData)) {
                                        byte[] tempBuffer = new byte[2048];
                                        int bytesUnzipped = 0;
                                        while ((bytesUnzipped = zipStream.read(tempBuffer, 0, 2048)) > 0) {
                                            uncompressedData.Write(tempBuffer, 0, bytesUnzipped);
                                        }
                                        frameBuffer = uncompressedData.ToArray();
                                        compressedData.Close();
                                        uncompressedData.Close();
                                    }
                                } else //All frame data is supposed to be compressed, but just in case...
                                {
                                    int frameSize = (int)block.size - Marshal.SizeOf(record) - Marshal.SizeOf(block);
                                    frameBuffer = new byte[frameSize];
                                    Array.Copy(buffer, offset2, frameBuffer, 0, frameSize);
                                }

                                offset2 = 0;
                                for (int f = 0; f < record.numFrames; f++) {
                                    RZX_Frame frame = new RZX_Frame();
                                    try {
                                        frame.instructionCount = BitConverter.ToUInt16(frameBuffer, offset2);
                                        offset2 += 2;
                                        frame.inputCount = BitConverter.ToUInt16(frameBuffer, offset2);
                                        offset2 += 2;
                                        if ((frame.inputCount == 65535)) {
                                            frame.inputCount = frames[frames.Count - 1].inputCount;
                                            frame.inputs = new byte[frame.inputCount];
                                            if (frame.inputCount > 0)
                                                Array.Copy(frames[frames.Count - 1].inputs, 0, frame.inputs, 0, frame.inputCount);
                                        } else if (frame.inputCount > 0) {
                                            frame.inputs = new byte[frame.inputCount];
                                            Array.Copy(frameBuffer, offset2, frame.inputs, 0, frame.inputCount);
                                            offset2 += frame.inputCount;
                                        } else {
                                            frame.inputs = new byte[0];
                                        }
                                        frames.Add(frame);
                                    } catch (Exception e) {
                                        return false;
                                    }
                                }

                                break;

                            default: //unrecognised block, so skip to next
                                break;
                        }
                        bufferCounter += (int)block.size - Marshal.SizeOf(block); //Move to next block
                    }
                    pinnedBuffer.Free();
                }
                return true;
            }

            public bool LoadRZX(string filename) {
                bool readSZX;
                using (FileStream fs = new FileStream(filename, FileMode.Open)) {
                    readSZX = LoadRZX(fs);
                }
                return readSZX;
            }

            public void SaveRZX(string filename) {
                header = new RZX_Header();
                header.majorVersion = 0;
                header.minorVersion = 12;
                header.flags = 0;
                header.signature = "RZX!".ToCharArray();

                creator = new RZX_Creator();
                creator.author = "Zero Emulator      \0".ToCharArray();
                creator.majorVersion = 6;
                creator.minorVersion = 0;

                using (FileStream fs = new FileStream(filename, FileMode.Create)) {
                    using (BinaryWriter r = new BinaryWriter(fs)) {
                        byte[] buf;
                        buf = RawSerialize(header); //header is filled in by the callee machine
                        r.Write(buf);

                        RZX_Block block = new RZX_Block();
                        block.id = 0x10;
                        block.size = (uint)Marshal.SizeOf(creator) + 5;
                        buf = RawSerialize(block);
                        r.Write(buf);
                        buf = RawSerialize(creator);
                        r.Write(buf);

                        for (int f = snapshotData.Length - 1; f >= 0; f--) {

                            if (snapshotData[f] == null)
                                continue;

                            snap = new RZX_Snapshot();
                            snap.extension = "szx\0".ToCharArray();
                            snap.flags |= 0x2;
                            byte[] rawSZXData;
                            snap.uncompressedSize = (uint)snapshotData[f].Length;

                            using (MemoryStream outMemoryStream = new MemoryStream())
                            using (ZOutputStream outZStream = new ZOutputStream(outMemoryStream, zlibConst.Z_DEFAULT_COMPRESSION))
                            using (Stream inMemoryStream = new MemoryStream(snapshotData[f])) {
                                CopyStream(inMemoryStream, outZStream);
                                outZStream.finish();
                                rawSZXData = outMemoryStream.ToArray();
                            }

                            block.id = 0x30;
                            block.size = (uint)Marshal.SizeOf(snap) + (uint)rawSZXData.Length + 5;
                            buf = RawSerialize(block);
                            r.Write(buf);
                            buf = RawSerialize(snap);
                            r.Write(buf);
                            r.Write(rawSZXData);
                        }

                        record.numFrames = (uint)frames.Count;
                        block.id = 0x80;
                        byte[] rawFramesData;
                        using (MemoryStream outMemoryStream = new MemoryStream())
                        using (ZOutputStream outZStream = new ZOutputStream(outMemoryStream, zlibConst.Z_DEFAULT_COMPRESSION)) {
                            foreach (RZX_Frame frame in frames) {
                                using (Stream inMemoryStream = new MemoryStream()) {
                                    BinaryWriter bw = new BinaryWriter(inMemoryStream);
                                    bw.Write(frame.instructionCount);
                                    bw.Write(frame.inputCount);
                                    bw.Write(frame.inputs);
                                    bw.Seek(0, 0);
                                    CopyStream(inMemoryStream, outZStream);
                                    bw.Close();
                                }
                            }

                            outZStream.finish();
                            rawFramesData = outMemoryStream.ToArray();
                        }

                        block.size = (uint)Marshal.SizeOf(record) + (uint)rawFramesData.Length + 5;
                        buf = RawSerialize(block);
                        r.Write(buf);
                        buf = RawSerialize(record);
                        r.Write(buf);
                        r.Write(rawFramesData);
                    }
                }
            }

            public void InitPlayback() {
                frameCount = 0;
                fetchCount = 0;
                inputCount = 0;
                frame = frames[0];
            }

            public void ContinueRecording() {
                snapshotData[0] = snapshotData[1];
                inputs = new List<byte>();
                frameCount = 0;
                fetchCount = 0;
                inputCount = 0;
            }

                public void InsertBookmark(SZXFile szx, List<byte> inputList) {
                    frame = new RZX_Frame();
                    frame.inputCount = (ushort)inputList.Count;
                    frame.instructionCount = (ushort)fetchCount;
                    frame.inputs = inputList.ToArray();
                    frames.Add(frame);
                    fetchCount = 0;
                    inputCount = 0;

                    RollbackBookmark bookmark = new RollbackBookmark();
                    bookmark.frameIndex = frames.Count;
                    bookmark.snapshot = szx;
                    bookmarks.Add(bookmark);
                    currentBookmark = bookmarks.Count - 1;
                }

                public SZXFile Rollback() {
                    if (bookmarks.Count > 0) {
                        RollbackBookmark bookmark = bookmarks[currentBookmark];
                        //if less than 2 seconds have passed since last bookmark, revert to an even earlier bookmark
                        if ((frames.Count - bookmark.frameIndex) / 50 < 2) {
                            if (currentBookmark > 0) {
                                bookmarks.Remove(bookmark);
                                currentBookmark--;
                            }
                            bookmark = bookmarks[currentBookmark];
                        }
                        frames.RemoveRange(bookmark.frameIndex, frames.Count - bookmark.frameIndex);
                        fetchCount = 0;
                        inputCount = 0;
                        inputs = new List<byte>();
                        return bookmark.snapshot;
                    }
                    return null;
                }

                public void Save(string fileName, byte[] data) {
                    snapshotData[1] = data;
                    bookmarks.Clear();
                    SaveRZX(fileName);
                }

                public void StartRecording(byte[] data, int totalTStates) {
                record.tstatesAtStart = (uint)totalTStates;
                record.flags |= 0x2; //Frames are compressed.
                snapshotData[0] = data;
            }

            public bool IsValidSession(string filename) {
                //if (snapshotData[1] == null)
                //    return false;

                if (!OpenFile(filename))
                    return false;

                List<RZX_Block> blocks = Scan();

                string c = new string(creator.author);

                if (!c.Contains("Zero"))
                    return false;

                int snapCount = 0;

                for (int i = 0; i < blocks.Count; i++) {
                    if (blocks[i].id == (int)RZX_BlockType.SNAPSHOT)
                        snapCount++;
                }

                if (snapCount < 2)
                    return false;

                return true;
            }

            public bool NextPlaybackFrame() {
                frameCount++;
                fetchCount = 0;
                inputCount = 0;

                if (frameCount < frames.Count) {
                    frame = frames[frameCount];
                    return true;
                }

                return false;
            }

            public void RecordFrame(List<byte> inputList) {
                frame = new RZX_Frame();
                frame.inputCount = (ushort)inputList.Count;
                frame.instructionCount = (ushort)fetchCount;
                frame.inputs = inputList.ToArray();
                frames.Add(frame);
                fetchCount = 0;
                inputCount = 0;
            }

            public void Discard() {
                bookmarks.Clear();
                inputs.Clear();
            }
            */

        #endregion

        public static void CopyStream(System.IO.Stream input, System.IO.Stream output) {
            byte[] buffer = new byte[2000];
            int len;
            while ((len = input.Read(buffer, 0, 2000)) > 0) {
                output.Write(buffer, 0, len);
            }
            output.Flush();
        }

        private static byte[] RawSerialize(object anything) {
            int rawsize = Marshal.SizeOf(anything);
            IntPtr buffer = Marshal.AllocHGlobal(rawsize);
            Marshal.StructureToPtr(anything, buffer, false);
            byte[] rawdatas = new byte[rawsize];
            Marshal.Copy(buffer, rawdatas, 0, rawsize);
            Marshal.FreeHGlobal(buffer);
            return rawdatas;
        }

        private bool OpenFile(FileStream fs) {
            rzxFile = fs;
            rzxFileReader = new BinaryReader(rzxFile);
            int bytesToRead = (int)rzxFile.Length;

            if (bytesToRead == 0) {
                Close();
                return false; //something bad happened!
            }

            fileBuffer = new byte[bytesToRead];
            rzxFileReader.Read(fileBuffer, 0, 10);

            pinnedBuffer = GCHandle.Alloc(fileBuffer, GCHandleType.Pinned);

            header = (RZX_Header)Marshal.PtrToStructure(pinnedBuffer.AddrOfPinnedObject(),
                                                                     typeof(RZX_Header));

            String sign = new String(header.signature);

            if (sign != "RZX!") {
                Close();
                return false;
            }

            return true;
        }

        private bool OpenFile(string filename) {
            FileStream fs  = new FileStream(filename, FileMode.Open);
            return OpenFile(fs);
        }

        public void SaveSession(byte[] szxData, bool isFinalise) {
            if (isRecordingBlock)
                CloseIRB();

            if (!isFinalise)
                AddSnapshot(szxData);

            rzxFile.SetLength(rzxFile.Position);
            Close();
        }

        public void Close() {
            if (frameInfoReader != null) {
                frameInfoReader.Close();
                frameInfoReader = null;
            }

            if (frameInfoFile != null) {
                frameInfoFile.Close();
                frameInfoFile = null;
                File.Delete(Application.LocalUserAppDataPath + "//" + tempFrameInfoFile);
            }

            if (isRecordingBlock)
                CloseIRB();

            if (!isReading && rzxFileWrite != null) {
                rzxFileWrite.Flush();
                rzxFileWrite.Close();
                rzxFileWrite = null;
            }
            else if (rzxFileReader != null) {
                if (pinnedBuffer.IsAllocated)
                    pinnedBuffer.Free();

                rzxFileReader.Close();
                rzxFileReader = null;
            }

            if (rzxFile != null)
                rzxFile.Close();

            rzxFile = null;
        }

        public List<RZX_Block> Scan() {
            RZXFileEventArgs rzxArgs = new RZXFileEventArgs();
            rzxArgs.info = new RZXInfo();
            rzxArgs.info.header = header;
            rzxArgs.info.blocks = new List<RZX_Block>();

            while (rzxFileReader.BaseStream.Position != rzxFileReader.BaseStream.Length) {
                if (rzxFileReader.Read(fileBuffer, 0, 5) < 1)
                    break;

                RZX_Block block = (RZX_Block)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(fileBuffer, 0), typeof(RZX_Block));
                rzxArgs.info.blocks.Add(block);

                rzxFileReader.Read(fileBuffer, 0, (int)block.size - Marshal.SizeOf(block));

                switch (block.id) {
                    case (int)RZX_BlockType.CREATOR:
                        creator = (RZX_Creator)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(fileBuffer, 0),
                                                                     typeof(RZX_Creator));
                        rzxArgs.info.creator = creator;
                        rzxArgs.blockID = RZX_BlockType.CREATOR;
                        rzxArgs.rzxInstance = this;
                        break;

                    default:
                    case (int)RZX_BlockType.RECORD:
                        RZXFileEventArgs recArgs = new RZXFileEventArgs();
                        record = (RZX_Record)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(fileBuffer, 0),
                                                                        typeof(RZX_Record));
                        rzxArgs.totalFramesInRecords += (int)record.numFrames;
                        break;
                }
            }

            if (RZXFileEventHandler != null)
                RZXFileEventHandler(rzxArgs);

            rzxFile.Seek(10, SeekOrigin.Begin);
            readBlockIndex = 10;
            return rzxArgs.info.blocks;
        }

        private void CloseIRB() {
            if (isCompressedFrames)
                CloseZStream();

            zStream.deflateEnd();

            if (frameCount == 0) {
                rzxFile.Seek(currentRecordFilePos, SeekOrigin.Begin);
                isRecordingBlock = false;
                return;
            }

            long currentPos = rzxFile.Position;
            long len = currentPos - currentRecordFilePos;
            rzxFile.Seek(currentRecordFilePos, SeekOrigin.Begin);

            record = new RZX_Record();
            record.numFrames = (uint)frameCount;

            if (isCompressedFrames)
                record.flags |= 0x2;

            record.tstatesAtStart = tstatesAtRecordStart;

            RZX_Block block = new RZX_Block();
            block.id = 0x80;
            block.size = (uint)len;
            byte[] buf;
            buf = RawSerialize(block);

            rzxFileWrite.Write(buf);
            buf = RawSerialize(record);
            rzxFileWrite.Write(buf);

            rzxFile.Seek(currentPos, SeekOrigin.Begin);
            currentRecordFilePos = currentPos;
            isRecordingBlock = false;
            inputs = new List<byte>();
        }

        private RZX_Snapshot ReadSnapshot(int dataSize, out byte[] snapdata) {
            //Read in the block data
            rzxFileReader.Read(fileBuffer, 0, dataSize);
            RZX_Snapshot snapshot = (RZX_Snapshot)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(fileBuffer, 0),
                                                         typeof(RZX_Snapshot));
            int snapDataOffset = Marshal.SizeOf(snap);

            if ((snapshot.flags & 0x2) != 0) {
                int snapSize = dataSize - snapDataOffset;

                MemoryStream compressedData = new MemoryStream(fileBuffer, snapDataOffset, snapSize);
                MemoryStream uncompressedData = new MemoryStream();

                using (ZInputStream zipStream = new ZInputStream(compressedData)) {
                    byte[] tempBuffer = new byte[2048];
                    int bytesUnzipped = 0;

                    while ((bytesUnzipped = zipStream.read(tempBuffer, 0, 2048)) > 0)
                        uncompressedData.Write(tempBuffer, 0, bytesUnzipped);

                    snapdata = uncompressedData.ToArray();
                    compressedData.Close();
                    uncompressedData.Close();
                }
            }
            else {
                snapdata = new byte[snapshot.uncompressedSize];
                Array.Copy(fileBuffer, snapDataOffset, snapdata, 0, snapshot.uncompressedSize);
            }

            return snapshot;
        }

        private bool SeekIRB() {

            while (rzxFileReader.BaseStream.Position < rzxFileReader.BaseStream.Length) {

                //Read in the block header
                if (rzxFileReader.Read(fileBuffer, 0, 5) < 1)
                    return false;

                RZX_Block block = (RZX_Block)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(fileBuffer, 0), typeof(RZX_Block));
                int blockSize = Marshal.SizeOf(block);
                int blockDataSize = (int)block.size - blockSize;

                readBlockIndex += blockSize;

                switch (block.id) {
                    case (int)RZX_BlockType.SNAPSHOT:
                        {
                            RZXFileEventArgs rzxArgs = new RZXFileEventArgs();
                            rzxArgs.blockID = RZX_BlockType.SNAPSHOT;
                            rzxArgs.snapData = new RZXSnapshotData();
                            rzxArgs.rzxInstance = this;
                            snap = ReadSnapshot(blockDataSize, out rzxArgs.snapData.data);

                            rzxArgs.snapData.extension = new String(snap.extension).ToLower();
                            readBlockIndex += blockDataSize;

                            if (snapsLoaded > 0)
                                break;

                            if (RZXFileEventHandler != null)
                                RZXFileEventHandler(rzxArgs);

                            snapsLoaded++;
                        }
                        return true;

                    case (int)RZX_BlockType.RECORD:
                        {
                            if (frameInfoReader != null)
                                frameInfoReader.Close();

                            if (frameInfoFile != null)
                                frameInfoFile.Close();

                            int recordSize = Marshal.SizeOf(new RZX_Record());
                            rzxFileReader.Read(fileBuffer, 0, recordSize);

                            record = (RZX_Record)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(fileBuffer, 0),
                                                                         typeof(RZX_Record));
                            frameCount = (int)record.numFrames;
                            isReadingIRB = true;

                            RZXFileEventArgs rzxArgs = new RZXFileEventArgs();
                            rzxArgs.blockID = RZX_BlockType.RECORD;
                            rzxArgs.tstates = record.tstatesAtStart;
                            rzxArgs.rzxInstance = this;

                            readBlockIndex += blockDataSize;

                            if (RZXFileEventHandler != null)
                                RZXFileEventHandler(rzxArgs);

                            byte[] tempInfo = new byte[blockDataSize - recordSize];
                            rzxFileReader.Read(tempInfo, 0, blockDataSize - recordSize);

                            try {
                                FileStream fs = new FileStream(Application.LocalUserAppDataPath + "//" + tempFrameInfoFile, FileMode.Create);
                                fs.Write(tempInfo, 0, tempInfo.Length);
                                fs.Flush();
                                fs.Close();
                            }
                            catch {
                                MessageBox.Show("There was an error processing the RZX File!", "Error", MessageBoxButtons.OK);
                                return false;
                            }

                            frameInfoFile = new FileStream(Application.LocalUserAppDataPath + "//" + tempFrameInfoFile, FileMode.Open);
                            frameInfoReader = new BinaryReader(frameInfoFile);

                            if (isCompressedFrames) {
                                currentRecordFilePos = rzxFile.Position;
                                OpenZStream(frameInfoFile, 0, true);
                            }

                        }
                        return true;

                    default: //unrecognised block, so skip to next
                        rzxFileReader.Read(fileBuffer, 0, blockDataSize); //dummy read to advance file pointer
                        break;
                }

                readBlockIndex += blockDataSize; //Move to next block
            }
            return false;
        }

        private void WriteFrame(byte[] inputs, ushort inCount) {
            BinaryWriter bw = new BinaryWriter(rzxFile);
            bw.Write((ushort)fetchCount);
            bw.Write(inCount);

            frameDataSize += (uint)(2 + 2);

            if (inputs != null && inputs.Length > 0) {
                bw.Write(inputs);
                frameDataSize += (uint)(inputs.Length);
            }
        }

        private int ReadFromZStream (BinaryReader reader, ref byte[] buffer, int numBytesToRead) {
            zStream.next_out = buffer;
            zStream.avail_out = numBytesToRead;
            zStream.next_out_index = 0;

            int err = zlibConst.Z_OK;

            while (zStream.avail_out > 0 && err == zlibConst.Z_OK) {

                if (zStream.avail_in == 0) {
                    zStream.avail_in = reader.Read(zBuffer, 0, ZBUFLEN);

                    if (zStream.avail_in == 0)
                        return 0;

                    zStream.next_in = zBuffer;
                    zStream.next_in_index = 0;
                }

               err = zStream.inflate(zlibConst.Z_FINISH);
            }
            return numBytesToRead - zStream.avail_out;
        }

        private int WriteToZStream(byte[] buffer, int numBytesToWrite) {
            int err = zlibConst.Z_OK;
            zStream.avail_in = numBytesToWrite;
            zStream.next_in = buffer;
            zStream.next_in_index = 0;
            
            while (zStream.avail_in > 0 && err == zlibConst.Z_OK) {

                if (zStream.avail_out == 0) {
                    rzxFileWrite.Write(zBuffer, 0, ZBUFLEN);
                    zStream.next_out = zBuffer;
                    zStream.next_out_index = 0;
                    zStream.avail_out = ZBUFLEN;
                }
                err = zStream.deflate(zlibConst.Z_NO_FLUSH);
            }

            return numBytesToWrite - zStream.avail_in;
        }

        private int CloseZStream() {
            int len, err;
            bool done = false;

            zStream.avail_in = 0;

            while (!isReading) {
                len = ZBUFLEN - zStream.avail_out;

                if (len > 0) {
                    rzxFileWrite.Write(zBuffer, 0, len);
                    zStream.next_out = zBuffer;
                    zStream.avail_out = ZBUFLEN;
                    zStream.next_out_index = 0;
                }

                if (done)
                    break;

                err = zStream.deflate(zlibConst.Z_FINISH);
                done = (zStream.avail_out > 0 || err == zlibConst.Z_STREAM_END);
            }

            zBuffer = null;
            return 0;
        }

        private bool OpenZStream(FileStream file, long offset, bool isRead) {
            int err;
            zBuffer = new byte[ZBUFLEN];
            zStream = new ZStream();

            if (isRead) {
                zStream.next_in = zBuffer;
                zStream.next_in_index = 0;
                zStream.avail_in = 0;
                err = zStream.inflateInit();
                isReading = true;
            }
            else {
                err = zStream.deflateInit(zlibConst.Z_DEFAULT_COMPRESSION);
                zStream.next_out = zBuffer;
                zStream.next_out_index = 0;
                isReading = false;
            }

            zStream.avail_out = ZBUFLEN;

            if (err != zlibConst.Z_OK)
                return false;

            file.Seek(offset, SeekOrigin.Begin);
            return true;
        }

        public bool ContinueRecording(string filename) {
            if (!OpenFile(filename))
                return false;

            readBlockIndex = 0;
            RZXSnapshotData[] snapShotData = new RZXSnapshotData[2];
            int snapCount = 0;
            long snapFilePosition = 0;

            while (rzxFileReader.BaseStream.Position != rzxFileReader.BaseStream.Length) {

                //Read in the block header
                if (rzxFileReader.Read(fileBuffer, 0, 5) < 1)
                    return false;

                RZX_Block block = (RZX_Block)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(fileBuffer, 0), typeof(RZX_Block));
                int blockSize = Marshal.SizeOf(block);
                int blockDataSize = (int)block.size - blockSize;

                readBlockIndex += blockSize;

                switch (block.id) {
                    case (int)RZX_BlockType.CREATOR:
                        creator = (RZX_Creator)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(fileBuffer, 0),
                                                                     typeof(RZX_Creator));
                        break;

                    case (int)RZX_BlockType.SNAPSHOT: {
                            snapFilePosition = rzxFile.Position - 5;
                            snap = ReadSnapshot(blockDataSize, out snapShotData[snapCount++].data);
                            readBlockIndex += blockDataSize;
                        }
                        return true;

                    default: //unrecognised block, so skip to next
                        rzxFileReader.Read(fileBuffer, 0, blockDataSize); //dummy read to advance file pointer
                        break;
                }

                readBlockIndex += blockDataSize; //Move to next block
            }
            Close();
            readBlockIndex = 0;

            string c = new string(creator.author);

            if (!c.Contains("Zero"))
                return false;

            if (snapCount < 2)
                return false;

            try {
                rzxFile = new FileStream(filename, FileMode.Open);
                rzxFileWrite = new BinaryWriter(rzxFile);
                state = RZX_State.RECORDING;
                rzxFile.Seek(snapFilePosition, 0); //Prepare to overwrite the old continue snapshot data
            }
            catch {
                return false;
            }

            if (RZXFileEventHandler != null) {
                RZXFileEventArgs rzxArgs = new RZXFileEventArgs();
                rzxArgs.blockID = RZX_BlockType.SNAPSHOT;
                rzxArgs.snapData = snapShotData[1];
                rzxArgs.snapData.extension = new String(snap.extension).ToLower();

                RZXFileEventHandler(rzxArgs);
            }

            inputs = new List<byte>();
            return true;

        }

        public bool Record(string filename) {
            header = new RZX_Header();
            header.majorVersion = 0;
            header.minorVersion = 12;
            header.flags = 0;
            header.signature = "RZX!".ToCharArray();
            string[] version = Application.ProductVersion.Split('.');

            creator = new RZX_Creator();
            creator.author = "Zero Emulator      \0".ToCharArray();
            creator.majorVersion = System.Convert.ToUInt16(version[0]);
            creator.minorVersion = System.Convert.ToUInt16(version[1]);

            frameCount = 0;
            fetchCount = 0;
            inputCount = 0;

            try {
                rzxFile = new FileStream(filename, FileMode.Create);
                rzxFileWrite = new BinaryWriter(rzxFile);
                byte[] buf;
                buf = RawSerialize(header);
                rzxFileWrite.Write(buf);

                RZX_Block block = new RZX_Block();
                block.id = 0x10;
                block.size = (uint)Marshal.SizeOf(creator) + 5;
                buf = RawSerialize(block);
                rzxFileWrite.Write(buf);
                buf = RawSerialize(creator);
                rzxFileWrite.Write(buf);
                state = RZX_State.RECORDING;
            }
            catch (System.IO.IOException e){
                MessageBox.Show("There was an error when trying to create a new recording.", "RZX File error", MessageBoxButtons.OK);
                return false;
            }
            inputs = new List<byte>();
            return true;
        }

        private bool ReadFile() {
            Scan();
            isReading = true;

            if (!SeekIRB())
                return false;

            state = RZX_State.PLAYBACK;
            fetchCount = 0;
            frame = new RZX_Frame();
            frame.inputCount = 0;
            return true;
        }

        public bool Playback(FileStream fs) {
            if (!OpenFile(fs))
                return false;

            totalFramesPlayed = 0;
            return ReadFile();
        }

        public bool Playback(string filename) {
            if (!OpenFile(filename))
                return false;

            totalFramesPlayed = 0;
            return ReadFile();
        }
        public bool NextPlaybackFrame() {
            bool continuePlayback = UpdatePlayback();
            fetchCount = 0;
            inputCount = 0;
            totalFramesPlayed++;
            return continuePlayback;
        }

        public bool UpdatePlayback() {
            if (state != RZX_State.PLAYBACK)
                return false;

            if (isReadingIRB && (fetchCount == 0))
                isReadingIRB = false;

            if (!isReadingIRB) {
                if (!SeekIRB()) {
                    Close();
                    state = RZX_State.NONE;

                    if (RZXFileEventHandler != null) {
                        RZXFileEventArgs rzxArgs = new RZXFileEventArgs();
                        rzxArgs.hasEnded = true;
                        rzxArgs.rzxInstance = this;
                        RZXFileEventHandler(rzxArgs);
                        RZXFileEventHandler = null;
                    }

                    return false;
                }
            }

            if (isCompressedFrames) {
                byte[] buffer = new byte[4];
                bool err = false;
                err = ReadFromZStream(frameInfoReader, ref buffer, 4) <= 0;

                if (!err) {
                    RZX_Frame newFrame = new RZX_Frame();
                    newFrame.instructionCount = BitConverter.ToUInt16(buffer, 0);
                    newFrame.inputCount = BitConverter.ToUInt16(buffer, 2);

                    //Repeat previous frame inputs if inputCount is 65535
                   if (newFrame.inputCount >= 0 && newFrame.inputCount != 65535) {
                        frame = newFrame;
                        if (newFrame.inputCount > 0) {
                            frame.inputs = new byte[frame.inputCount];
                            err = ReadFromZStream(frameInfoReader, ref frame.inputs, frame.inputCount) <= 0;
                        }
                    }
                   else {
                        frame.instructionCount = newFrame.instructionCount;
                    }

                    /*
                    if (newFrame.inputCount > 0 && (newFrame.inputCount != 0xffff)) {
                        frame = newFrame;
                        frame.inputs = new byte[frame.inputCount];
                        err = ReadFromZStream(frameInfoReader, ref frame.inputs, frame.inputCount) <= 0;
                    }
                    else {
                        frame.instructionCount = newFrame.instructionCount;
                        
                        if (frame.inputCount == 0) {
                            frame.inputs = null;
                            frame.inputCount = newFrame.inputCount;
                        }
                    }*/

                    
                    //Repeat previous frame
                    /*if (newFrame.inputCount != 0xffff) {
                       if (newFrame.inputCount > 0) {
                            frame = newFrame;
                            frame.inputs = new byte[frame.inputCount];
                            err = ReadFromZStream(frameInfoReader, ref frame.inputs, frame.inputCount) <= 0;
                        }
                        else {
                            frame = newFrame;
                            frame.inputs = null;
                        }
                    }
                    else {
                        frame.instructionCount = newFrame.instructionCount;
                    }*/
                }

                if (err) {
                    /*
                    Close();
                    state = RZX_State.NONE;

                    if (RZXFileEventHandler != null) {
                        RZXFileEventArgs rzxArgs = new RZXFileEventArgs();
                        rzxArgs.hasEnded = true;
                        RZXFileEventHandler(rzxArgs);
                        RZXFileEventHandler = null;
                    }

                    return false;*/
                    isReadingIRB = false;

                }
            }
            return true;
        }

        public bool UpdateRecording(int tstates) {
            if (state != RZX_State.RECORDING)
                return false;

            if (!isRecordingBlock) {
                currentRecordFilePos = rzxFile.Position;
                tstatesAtRecordStart = (uint)tstates;

                record = new RZX_Record();
                record.numFrames = (uint)frameCount;           //This will be adjusted later when closing the record

                if (isCompressedFrames)
                    record.flags |= 0x2;

                record.tstatesAtStart = tstatesAtRecordStart;

                RZX_Block block = new RZX_Block();
                block.id = 0x80;
                block.size = (uint)Marshal.SizeOf(record) + 5; //This will be adjusted later when closing the record
                byte[] buf;
                buf = RawSerialize(block);

                rzxFileWrite.Write(buf);
                buf = RawSerialize(record);
                rzxFileWrite.Write(buf);

                isRecordingBlock = true;
                frameCount = 0;

                if (isCompressedFrames) 
                    OpenZStream(rzxFile, rzxFile.Position, false);
            }

            ushort inCount = 65535;
                    
            if (oldInputs.Count == inputs.Count) {

                for (int i = 0; i < inputs.Count; i++) {
                    if (inputs[i] != oldInputs[i]) { 
                        inCount = (ushort)inputs.Count;
                        break;
                    }
                }
            }
            else
                inCount = (ushort)inputs.Count;

            byte[] frameHeader = new byte[4];
            frameHeader[0] = (byte)(fetchCount & 0xff);
            frameHeader[1] = (byte)((fetchCount & 0xff00) >> 8);
            frameHeader[2] = (byte)(inCount & 0xff);
            frameHeader[3] = (byte)((inCount & 0xff00) >> 8);

            if (isCompressedFrames)
                WriteToZStream(frameHeader, 4);

            if ((inCount > 0) && (inCount != 65535))
                WriteToZStream(inputs.ToArray(), inputs.Count);

            fetchCount = 0;
            oldInputs.Clear();
            oldInputs = new List<byte>(inputs);
            inputs = new List<byte>();
            frameCount++;

            return true;
        }
        
        public void AddSnapshot(byte[] snapshotData) {
            snap = new RZX_Snapshot();
            snap.extension = "szx\0".ToCharArray();
            snap.flags |= 0x2;
            byte[] rawSZXData;
            snap.uncompressedSize = (uint)snapshotData.Length;

            using (MemoryStream outMemoryStream = new MemoryStream())
                using (ZOutputStream outZStream = new ZOutputStream(outMemoryStream, zlibConst.Z_DEFAULT_COMPRESSION))
                    using (Stream inMemoryStream = new MemoryStream(snapshotData)) {
                        CopyStream(inMemoryStream, outZStream);
                        outZStream.finish();
                        rawSZXData = outMemoryStream.ToArray();
                    }

            RZX_Block block = new RZX_Block();
            block.id = 0x30;
            block.size = (uint)Marshal.SizeOf(snap) + (uint)rawSZXData.Length + 5;
            byte[] buf;
            buf = RawSerialize(block);

            rzxFileWrite.Write(buf);
            buf = RawSerialize(snap);
            rzxFileWrite.Write(buf);
            rzxFileWrite.Write(rawSZXData);
        }

        // File structure when recording with rollbacks is as follows:
        // [Creator Block]
        // [Start snapshot]
        // [IRB Data]
        // ...
        // [Continue Snapshot]
        public void Bookmark(SZXFile szx) {
            if (!isReading) {
                if (isRecordingBlock)
                    CloseIRB();

                RollbackBookmark bookmark = new RollbackBookmark();
                bookmark.snapshot = szx;
                bookmark.tstates = tstatesAtRecordStart;
                bookmark.irbFilePos = currentRecordFilePos;
                bookmarks.Add(bookmark);
                currentBookmark = bookmarks.Count - 1;
            }
        }

        public void Rollback() {
            if (!isReading && isRecordingBlock && bookmarks.Count > 0) {
                RollbackBookmark bookmark = bookmarks[currentBookmark];

                //If less than 2 seconds have passed since last bookmark, revert to an even earlier bookmark
                if (frameCount < 25) {
                    if (currentBookmark > 0) {
                        bookmarks.Remove(bookmark);
                        currentBookmark--;
                    }
                    bookmark = bookmarks[currentBookmark];
                }
                bookmarks.Remove(bookmark);
                currentBookmark--;

                //The current record block is invalid now, so we save it out but we will set it up to be overwritten by subsequent file writes
                CloseIRB();
                rzxFile.Seek(bookmark.irbFilePos, SeekOrigin.Begin);
                currentRecordFilePos = bookmark.irbFilePos;
                RZXFileEventArgs arg = new RZXFileEventArgs();
                arg.blockID = RZX_BlockType.SNAPSHOT;
                arg.tstates = bookmark.tstates;
                arg.snapData = new RZXSnapshotData();
                arg.snapData.extension = "szx\0";
                arg.snapData.data = bookmark.snapshot.GetSZXData();

                if (RZXFileEventHandler != null)
                    RZXFileEventHandler(arg);
            }
        }

    }
}