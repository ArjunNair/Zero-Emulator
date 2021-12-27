using System;

namespace Peripherals
{
    public class PZX_TapeInfo
    {
        private String block;
        private String more = null;
        public bool IsStandardBlock = false;

        public String Block {
            get { return block; }
            set { block = value; }
        }

        public String Info {
            get { return more; }
            set { more = value; }
        }
    }

    public static class PZXFile
    {
        public class Block
        {
            public string tag;
            public uint size;
        }

        public class PZXT_Header : Block
        {
            public byte MajorVersion;
            public byte MinorVersion;
            public string Title = null;
            public string Publisher = null;
            public System.Collections.Generic.List<String> Authors = new System.Collections.Generic.List<string>();
            public string YearOfPublication = null;
            public string Language = null;
            public string Type = null;
            public string Price = null;
            public string ProtectionScheme = null;
            public string Origin = null;
            public System.Collections.Generic.List<string> Comments = new System.Collections.Generic.List<string>();
        }

        public class Pulse
        {
            public ushort count;
            public uint duration;
        }

        public class PULS_Block : Block
        {
            public System.Collections.Generic.List<Pulse> pulse = new System.Collections.Generic.List<Pulse>();
        }

        public class DATA_Block : Block
        {
            public uint initialPulseLevel;
            public uint count;
            public ushort tail;
            public byte p0;
            public byte p1;
            public System.Collections.Generic.List<ushort> s0 = new System.Collections.Generic.List<ushort>();
            public System.Collections.Generic.List<ushort> s1 = new System.Collections.Generic.List<ushort>();
            public System.Collections.Generic.List<byte> data = new System.Collections.Generic.List<byte>();
        }

        public class PAUS_Block : Block
        {
            public int initialPulseLevel;
            public uint duration;
        }

        public class BRWS_Block : Block
        {
            public string text;
        }

        public class STOP_Block : Block
        {
            public ushort flag;
        }

        // PZX_Tape tapeInfo;
        public static System.ComponentModel.BindingList<PZX_TapeInfo> tapeBlockInfo = new System.ComponentModel.BindingList<PZX_TapeInfo>();

        //All the blocks in the tape
        public static System.Collections.Generic.List<Block> blocks = new System.Collections.Generic.List<Block>();

        public static string GetTag(uint id) {
            byte[] b = System.BitConverter.GetBytes(id);
            string idString = String.Format("{0}{1}{2}{3}", (char)b[0], (char)b[1], (char)b[2], (char)b[3]);
            return idString;
        }

        public static string GetString(byte[] array, ref int _counter, uint limit) {
            string newString = null;
            while ((_counter < limit) && (array[_counter] != 0)) {
                char c = (char)(array[_counter++]);
                newString += c;
            }
            _counter++; //point to next valid data
            return newString;
        }

        public static PZXT_Header GetHeader(byte[] _buffer, int _counter, uint size) {
            PZXT_Header header = new PZXT_Header();

            uint baseCount = (uint)_counter;
            header.MajorVersion = _buffer[_counter++];
            header.MinorVersion = _buffer[_counter++];

            //Only Version 1 files supported ATM
            if (header.MajorVersion != 1)
                return null;

            while (_counter < baseCount + size) {
                string info = GetString(_buffer, ref _counter, baseCount + size);

                if (info == null)
                    continue;

                //if we haven't read the header do that now
                if (header.Title == null) {
                    header.Title = info;
                    if (header.Title == null)
                        header.Title = "No title found!";
                    continue;
                }

                switch (info) {
                    case "Publisher":
                        header.Publisher = GetString(_buffer, ref _counter, baseCount + size);
                        break;

                    case "Author":
                        string author = GetString(_buffer, ref _counter, baseCount + size);
                        header.Authors.Add(author);
                        break;

                    case "Year":
                        header.YearOfPublication = GetString(_buffer, ref _counter, baseCount + size);
                        break;

                    case "Language":
                        header.Language = GetString(_buffer, ref _counter, baseCount + size);
                        break;

                    case "Type":
                        header.Type = GetString(_buffer, ref _counter, baseCount + size);
                        break;

                    case "Price":
                        header.Price = GetString(_buffer, ref _counter, baseCount + size);
                        break;

                    case "Protection":
                        header.ProtectionScheme = GetString(_buffer, ref _counter, baseCount + size);
                        break;

                    case "Origin":
                        header.Origin = GetString(_buffer, ref _counter, baseCount + size);
                        break;

                    case "Comment":
                        string comment = GetString(_buffer, ref _counter, baseCount + size);
                        header.Comments.Add(comment);
                        break;

                    default:
                        break;
                }
            }
            return header;
        }

        public static PULS_Block GetPulse(byte[] _buffer, int _counter, uint size) {
            PULS_Block block = new PULS_Block();

            int baseCount = _counter;

            while (_counter < baseCount + size) {
                Pulse p = new Pulse();
                p.count = 1;
                p.duration = (System.BitConverter.ToUInt16(_buffer, _counter));
                _counter += 2;

                if (p.duration > 0x8000) {
                    p.count = (ushort)(p.duration & 0x7FFF);
                    p.duration = System.BitConverter.ToUInt16(_buffer, _counter);
                    _counter += 2;
                }
                if (p.duration >= 0x8000) {
                    p.duration &= 0x7FFF;
                    p.duration <<= 16;
                    p.duration |= System.BitConverter.ToUInt16(_buffer, _counter);

                    _counter += 2;
                }

                block.pulse.Add(p);
            }
            return block;
        }

        public static DATA_Block GetData(byte[] _buffer, int _counter, uint size) {
            DATA_Block block = new DATA_Block();
            int baseCount = _counter;

            while (_counter < baseCount + size) {
                block.count = System.BitConverter.ToUInt32(_buffer, _counter);
                block.initialPulseLevel = (uint)((block.count & 0x80000000) == 0 ? 0 : 1);
                block.count = (uint)(block.count & 0x7FFFFFFF);
                _counter += 4;
                block.tail = System.BitConverter.ToUInt16(_buffer, _counter);
                _counter += 2;
                block.p0 = _buffer[_counter++];
                block.p1 = _buffer[_counter++];
                for (int i = 0; i < block.p0; i++) {
                    ushort s = System.BitConverter.ToUInt16(_buffer, _counter);
                    _counter += 2;
                    block.s0.Add(s);
                }
                for (int i = 0; i < block.p1; i++) {
                    ushort s = System.BitConverter.ToUInt16(_buffer, _counter);
                    _counter += 2;
                    block.s1.Add(s);
                }
                for (int i = 0; i < Math.Ceiling((decimal)block.count / 8); i++) {
                    byte b = _buffer[_counter++];
                    block.data.Add(b);
                }
            }
            return block;
        }
        public static bool LoadPZX(ref byte[] buffer) {
            blocks.Clear();
            tapeBlockInfo.Clear();

            if (buffer.Length == 0)
                return false; //something bad happened!

            int counter = 0;

            while (counter < buffer.Length) {
                //Read tag first (in a really lame way)
                string blockTag = null;
                for (int i = 0; i < 4; i++) {
                    blockTag += (char)(buffer[counter++]);
                }

                uint blockSize = System.BitConverter.ToUInt32(buffer, counter);
                counter += 4;

                switch (blockTag) {
                    case "PZXT":
                    PZXT_Header header = GetHeader(buffer, counter, blockSize);
                    header.tag = "PZXT Header";
                    header.size = blockSize;
                    blocks.Add(header);
                    break;

                    case "PULS":
                    PULS_Block pblock = GetPulse(buffer, counter, blockSize);
                    pblock.tag = "PULS";
                    pblock.size = blockSize;
                    blocks.Add(pblock);
                    break;

                    case "DATA":
                    DATA_Block dblock = GetData(buffer, counter, blockSize);
                    dblock.tag = "DATA";
                    dblock.size = blockSize;
                    blocks.Add(dblock);
                    break;

                    case "PAUS":
                    PAUS_Block pauseBlock = new PAUS_Block();
                    pauseBlock.tag = "PAUS";
                    uint d = System.BitConverter.ToUInt32(buffer, counter);
                    pauseBlock.initialPulseLevel = ((d & 0x80000000) == 0 ? 0 : 1);
                    pauseBlock.duration = (d & 0x7FFFFFFF);
                    pauseBlock.size = blockSize;
                    blocks.Add(pauseBlock);
                    break;

                    case "BRWS":
                    BRWS_Block brwsBlock = new BRWS_Block();
                    brwsBlock.tag = "BRWS";
                    int baseCount = counter;
                    brwsBlock.text = GetString(buffer, ref counter, (uint)counter + blockSize);
                    brwsBlock.size = blockSize;
                    counter = baseCount;
                    blocks.Add(brwsBlock);
                    break;

                    case "STOP":
                    STOP_Block stopBlock = new STOP_Block();
                    stopBlock.tag = "STOP";
                    stopBlock.flag = System.BitConverter.ToUInt16(buffer, counter);
                    stopBlock.size = blockSize;
                    blocks.Add(stopBlock);
                    break;

                    default:
                    break;
                }
                counter += (int)blockSize;
            }
            return true;
        }

        public static bool LoadPZX(System.IO.Stream fs) {
            blocks.Clear();
            tapeBlockInfo.Clear();
            using (System.IO.BinaryReader r = new System.IO.BinaryReader(fs)) {
                int bytesToRead = (int)fs.Length;

                byte[] buffer = new byte[bytesToRead];
                int bytesRead = r.Read(buffer, 0, bytesToRead);

                if (bytesRead == 0)
                    return false; //something bad happened!

                return LoadPZX(ref buffer);
            }
        }

        public static bool LoadPZX(string filename) {
            bool readPZX;
            using (System.IO.FileStream fs = new System.IO.FileStream(filename, System.IO.FileMode.Open)) {
                readPZX = LoadPZX(fs);
            }
            return readPZX;
        }
        
        private static String GetStringFromData(byte[] _buffer, int _from, int _length) {
            System.Text.StringBuilder s = new System.Text.StringBuilder();
            for (int f = _from; f < _from + _length; f++) {
                s.Append((char)_buffer[f]);
            }
            return s.ToString();
        }

        public static void ReadTapeInfo(String filename) {
            for (int f = 0; f < PZXFile.blocks.Count; f++) {
                PZX_TapeInfo info = new PZX_TapeInfo();

                if (PZXFile.blocks[f] is PZXFile.BRWS_Block) {
                    info.Info = ((PZXFile.BRWS_Block)PZXFile.blocks[f]).text;
                } else if (PZXFile.blocks[f] is PZXFile.PAUS_Block) {
                    info.Info = ((PZXFile.PAUS_Block)PZXFile.blocks[f]).duration.ToString() + " t-states   (" +
                                 Math.Ceiling(((double)(((PZXFile.PAUS_Block)PZXFile.blocks[f]).duration) / (double)(69888 * 50))).ToString() + " secs)";
                } else if (PZXFile.blocks[f] is PZXFile.PULS_Block) {
                    info.Info = ((PZXFile.PULS_Block)PZXFile.blocks[f]).pulse[0].duration.ToString() + " t-states   ";
                } else if (PZXFile.blocks[f] is PZXFile.STOP_Block) {
                    info.Info = "Stop the tape.";
                } else if (PZXFile.blocks[f] is PZXFile.DATA_Block) {

                    PZXFile.DATA_Block _data = (PZXFile.DATA_Block)PZXFile.blocks[f];
                    int d = (int)(_data.count / 8);

                    //Determine if it's a standard data block suitable for flashloading
                    //Taken from PZX FAQ:
                    // In the common case of the standard loaders you would simply test that 
                    //each sequence consists of two non-zero pulses, and that the total duration 
                    // of the sequence s0 is less than the total duration of sequence s1
                    if ((_data.p0 == 2 && _data.p1 == 2) &&
                        (_data.s0[0] != 0 && _data.s0[1] != 0 && _data.s1[0] != 0 && _data.s1[1] != 0) &&
                        (_data.s0[0] + _data.s0[1] < _data.s1[0] + _data.s1[1]))
                        info.IsStandardBlock = true;

                    //Check for standard header
                    if ((d == 19) && (_data.data[0] == 0)) {
                        //Check checksum to ensure it's a standard header
                        byte checksum = 0;
                        for (int x = 0; x < _data.data.Count - 1; x++) {
                            checksum ^= _data.data[x];
                        }

                        if (checksum == _data.data[18]) {
                            int type = _data.data[1];
                            if (type == 0) {
                                String _name = GetStringFromData(_data.data.ToArray(), 2, 10);
                                info.Info = "Program: \"" + _name + "\"";
                                ushort _line = System.BitConverter.ToUInt16(_data.data.ToArray(), 14);
                                if (_line > 0)
                                    info.Info += " LINE " + _line.ToString();
                            } else if (type == 1) {
                                String _name = GetStringFromData(_data.data.ToArray(), 2, 10);
                                info.Info = "Num Array: \"" + _name + "\"" + "  " + Convert.ToChar(_data.data[15] - 32) + "(" + _data.data[12].ToString() + ")";
                            } else if (type == 2) {
                                String _name = GetStringFromData(_data.data.ToArray(), 2, 10);
                                info.Info = "Char Array: \"" + _name + "\"" + "  " + Convert.ToChar(_data.data[15] - 96) + "$(" + _data.data[12].ToString() + ")";
                            } else if (type == 3) {
                                String _name = GetStringFromData(_data.data.ToArray(), 2, 10);
                                info.Info = "Bytes: \"" + _name + "\"";
                                ushort _start = System.BitConverter.ToUInt16(_data.data.ToArray(), 14);
                                ushort _length = System.BitConverter.ToUInt16(_data.data.ToArray(), 12);
                                info.Info += " CODE " + _start.ToString() + "," + _length.ToString();
                            } else {
                                info.Info = ((PZXFile.DATA_Block)PZXFile.blocks[f]).count.ToString() + " bits  (" + Math.Ceiling((double)(((PZXFile.DATA_Block)PZXFile.blocks[f]).count) / (double)8).ToString() + " bytes)";
                            }
                        } else
                            info.Info = "";
                    } else
                        info.Info = ((PZXFile.DATA_Block)PZXFile.blocks[f]).count.ToString() + " bits  (" + Math.Ceiling((double)(((PZXFile.DATA_Block)PZXFile.blocks[f]).count) / (double)8).ToString() + " bytes)";
                }
                else if (PZXFile.blocks[f] is PZXFile.PZXT_Header) {
                    //info.Info = ((PZXFile.PZXT_Header)(PZXFile.blocks[f])).Title;
                    continue;
                }

                info.Block = PZXFile.blocks[f].tag;
                tapeBlockInfo.Add(info);
            }
        }
    }
}