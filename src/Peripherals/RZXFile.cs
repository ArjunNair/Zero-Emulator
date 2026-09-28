#define NEW_RZX_METHODS

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO.Compression;
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
        //Raised for file errors the host may want to surface (replaces MessageBox in the library).
        public event Action<string> OnError;

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
        private byte[] fileBuffer;
        private ZLibStream zInflater;   //frame data inflater during playback
        private ZLibStream zDeflater;   //frame data deflater during recording
        private GCHandle pinnedBuffer;
        private bool isReading = false;
        private bool isReadingIRB = false;
        public int fetchCount;
        public ushort inputCount;
        private int snapsLoaded = 0;

        private bool isFileInRecordingMode = false;

        //Used for rollbacks
        private int currentBookmark = 0;
        private List<RollbackBookmark> bookmarks = new List<RollbackBookmark>();

        public RZX_Frame frame;
        public List<RZX_Frame> frames = new List<RZX_Frame>();
        
        public int NumFramesPlayed {
            get {return totalFramesPlayed;}
        }

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
                File.Delete(Path.Combine(Path.GetTempPath(), tempFrameInfoFile));
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
            return rzxArgs.info.blocks;
        }

        private void CloseIRB() {
            if (isCompressedFrames)
                CloseZStream();

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

                using (ZLibStream zipStream = new ZLibStream(compressedData, CompressionMode.Decompress)) {
                    byte[] tempBuffer = new byte[2048];
                    int bytesUnzipped = 0;

                    while ((bytesUnzipped = zipStream.Read(tempBuffer, 0, 2048)) > 0)
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
            

            // If the last block is a snapshot, then we are mid-recording and need to skip the snapshot
            // so that we can continue recording from the last block.
            while (rzxFileReader.BaseStream.Position < rzxFileReader.BaseStream.Length) {

                //Read in the block header
                if (rzxFileReader.Read(fileBuffer, 0, 5) < 5)
                    return false;

                RZX_Block block = (RZX_Block)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(fileBuffer, 0), typeof(RZX_Block));
                int blockSize = Marshal.SizeOf(block);  // Size of the block header (5 bytes)
                int blockDataSize = (int)block.size - blockSize; // Size of the block data (excluding the header)


                switch (block.id) {
                    case (int)RZX_BlockType.SNAPSHOT:
                        {
                            RZXFileEventArgs rzxArgs = new RZXFileEventArgs();
                            rzxArgs.blockID = RZX_BlockType.SNAPSHOT;
                            rzxArgs.snapData = new RZXSnapshotData();
                            rzxArgs.rzxInstance = this;
                            snap = ReadSnapshot(blockDataSize, out rzxArgs.snapData.data);

                            rzxArgs.snapData.extension = new String(snap.extension).ToLower();

                            if (isFileInRecordingMode && snapsLoaded > 0)
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

                            if (RZXFileEventHandler != null)
                                RZXFileEventHandler(rzxArgs);

                            byte[] tempInfo = new byte[blockDataSize - recordSize];
                            rzxFileReader.Read(tempInfo, 0, blockDataSize - recordSize);

                            try {
                                FileStream fs = new FileStream(Path.Combine(Path.GetTempPath(), tempFrameInfoFile), FileMode.Create);
                                fs.Write(tempInfo, 0, tempInfo.Length);
                                fs.Flush();
                                fs.Close();
                            }
                            catch {
                                OnError?.Invoke("There was an error processing the RZX File!");
                                return false;
                            }

                            frameInfoFile = new FileStream(Path.Combine(Path.GetTempPath(), tempFrameInfoFile), FileMode.Open);
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

        private int ReadFromZStream(BinaryReader reader, ref byte[] buffer, int numBytesToRead) {
            if (zInflater == null)
                return 0;

            int total = 0;
            while (total < numBytesToRead) {
                int n = zInflater.Read(buffer, total, numBytesToRead - total);
                if (n <= 0)
                    break;
                total += n;
            }
            return total;
        }

        private int WriteToZStream(byte[] buffer, int numBytesToWrite) {
            if (zDeflater == null)
                return 0;

            zDeflater.Write(buffer, 0, numBytesToWrite);
            return numBytesToWrite;
        }

        private int CloseZStream() {
            if (zDeflater != null) {
                zDeflater.Dispose(); //flushes pending deflate output and the zlib trailer into rzxFile
                zDeflater = null;
            }
            if (zInflater != null) {
                zInflater.Dispose();
                zInflater = null;
            }
            return 0;
        }

        private bool OpenZStream(FileStream file, long offset, bool isRead) {
            CloseZStream();
            file.Seek(offset, SeekOrigin.Begin);

            if (isRead) {
                zInflater = new ZLibStream(file, CompressionMode.Decompress, true);
                isReading = true;
            }
            else {
                zDeflater = new ZLibStream(file, CompressionLevel.Optimal, true);
                isReading = false;
            }
            return true;
        }

        public bool ContinueRecording(string filename) {
            if (!OpenFile(filename)) {
                return false;
            }

            RZXSnapshotData snapShotData = new RZXSnapshotData();
            int snapCount = 0;
            long snapFilePosition = 0;

            while (rzxFileReader.BaseStream.Position != rzxFileReader.BaseStream.Length) {
                // Read in the block info:
                // Byte 0: Block ID
                // Bytes 1-4: Block Size (including the 5 bytes of block info)
                if (rzxFileReader.Read(fileBuffer, 0, 5) < 5) {
                    Close();
                    return false;
                }

                RZX_Block block = (RZX_Block)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(fileBuffer, 0), typeof(RZX_Block));
                int blockSize = Marshal.SizeOf(block); // Size of the block header (5 bytes)
                int blockDataSize = (int)block.size - blockSize; // Size of the block data (excluding the header)

                switch (block.id) {
                    case (int)RZX_BlockType.CREATOR:
                        rzxFileReader.Read(fileBuffer, 0, blockDataSize);
                        creator = (RZX_Creator)Marshal.PtrToStructure(Marshal.UnsafeAddrOfPinnedArrayElement(fileBuffer, 0),
                                typeof(RZX_Creator));
                        break;

                    case (int)RZX_BlockType.SNAPSHOT: {
                            snapFilePosition = rzxFile.Position - 5;
                            snapCount += 1;
                            snap = ReadSnapshot(blockDataSize, out snapShotData.data);
                        }
                        break;

                    default: //unrecognised block, so skip to next
                        rzxFileReader.Read(fileBuffer, 0, blockDataSize); // Advance file pointer to the next block
                        break;
                }
            }
            Close();

            string c = new string(creator.author);

            // For safety we only allow continue recording if the RZX file was created by Zero X Emulator and has at least 2 snapshots (start and continue)
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
                rzxArgs.snapData = snapShotData;
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
            System.Version asmVersion = typeof(RZXFile).Assembly.GetName().Version ?? new System.Version(0, 9);
            string[] version = new string[] { asmVersion.Major.ToString(), asmVersion.Minor.ToString() };

            creator = new RZX_Creator();
            creator.author = "Zero X Emulator    \0".ToCharArray();
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
                OnError?.Invoke("There was an error when trying to create a new recording.");
                return false;
            }
            inputs = new List<byte>();
            return true;
        }

        private bool ReadFile() {
            var blocks = Scan();
            isFileInRecordingMode = blocks.Count > 0 && blocks[blocks.Count - 1].id == (int)RZX_BlockType.SNAPSHOT;
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
                using (ZLibStream outZStream = new ZLibStream(outMemoryStream, CompressionLevel.Optimal, true))
                    using (Stream inMemoryStream = new MemoryStream(snapshotData)) {
                        CopyStream(inMemoryStream, outZStream);
                        outZStream.Dispose(); //ZLibStream only emits the trailer on dispose
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