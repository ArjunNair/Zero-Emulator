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
        public static System.Collections.Generic.List<PZX_TapeInfo> tapeBlockInfo = new System.Collections.Generic.List<PZX_TapeInfo>();

        //All the blocks in the tape
        public static System.Collections.Generic.List<Block> blocks = new System.Collections.Generic.List<Block>();

        public static string GetTag(uint id) {
            byte[] b = System.BitConverter.GetBytes(id);
            string idString = String.Format("{0}{1}{2}{3}", (char)b[0], (char)b[1], (char)b[2], (char)b[3]);
            return idString;
        }

        // Reads a string ending at a 0x00 byte or at limit, whichever comes first, as UTF-8.
        // An empty string comes back as "", not null.
        public static string GetString(byte[] array, ref int _counter, uint limit) {
            int start = _counter;
            while (_counter < limit && array[_counter] != 0)
                _counter++;
            string s = System.Text.Encoding.UTF8.GetString(array, start, _counter - start);
            _counter++; //point to next valid data
            return s;
        }

        // Determine if it's a standard data block suitable for flashloading
        // PZX FAQ:
        // In the common case of the standard loaders you would simply test that
        // each sequence consists of two non-zero pulses, and that the total duration
        // of the sequence s0 is less than the total duration of sequence s1
        public static bool IsStandardData(DATA_Block d) =>
            d.p0 == 2 && d.p1 == 2 && d.s0[0] != 0 && d.s0[1] != 0 && d.s1[0] != 0 && d.s1[1] != 0
            && d.s0[0] + d.s0[1] < d.s1[0] + d.s1[1];

        public static PZXT_Header GetHeader(byte[] _buffer, int _counter, uint size) {
            PZXT_Header header = new PZXT_Header();

            uint baseCount = (uint)_counter;
            header.MajorVersion = _buffer[_counter++];
            header.MinorVersion = _buffer[_counter++];

            //Only Version 1 files supported ATM
            if (header.MajorVersion != 1)
                return null;

            bool hasTitle = false;
            while (_counter < baseCount + size) {
                string key = GetString(_buffer, ref _counter, baseCount + size);

                if (!hasTitle) {
                    header.Title = key;
                    hasTitle = true;
                    continue;
                }

                //Read the value before the switch so an unknown key still consumes its value.
                //A missing last value reads as "", as the spec asks.
                string value = GetString(_buffer, ref _counter, baseCount + size);
                switch (key) {
                    case "Publisher":
                        header.Publisher = value;
                        break;

                    case "Author":
                        header.Authors.Add(value);
                        break;

                    case "Year":
                        header.YearOfPublication = value;
                        break;

                    case "Language":
                        header.Language = value;
                        break;

                    case "Type":
                        header.Type = value;
                        break;

                    case "Price":
                        header.Price = value;
                        break;

                    case "Protection":
                        header.ProtectionScheme = value;
                        break;

                    case "Origin":
                        header.Origin = value;
                        break;

                    case "Comment":
                        header.Comments.Add(value);
                        break;

                    default:
                        break; //unknown key: its value is already consumed
                }
            }
            return header;
        }

        public static PULS_Block GetPulse(byte[] _buffer, int _counter, uint size) {
            PULS_Block block = new PULS_Block();
            int end = _counter + (int)size;

            //A single leftover byte can't start a value, so it's ignored

            while (_counter + 2 <= end) {
                Pulse p = new Pulse();
                p.count = 1;
                p.duration = (System.BitConverter.ToUInt16(_buffer, _counter));
                _counter += 2;

                if (p.duration > 0x8000) {
                    if (_counter + 2 > end)
                        return null; // repeat count with no duration after it
                    p.count = (ushort)(p.duration & 0x7FFF);
                    p.duration = System.BitConverter.ToUInt16(_buffer, _counter);
                    _counter += 2;
                }
                if (p.duration >= 0x8000) {
                    if (_counter + 2 > end)
                        return null; // high half of a long duration with no low half
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
            if (size < 8)
                return null; // too small

            DATA_Block block = new DATA_Block();
            uint count = System.BitConverter.ToUInt32(_buffer, _counter);
            block.initialPulseLevel = (uint)((count & 0x80000000) == 0 ? 0 : 1);
            block.count = count & 0x7FFFFFFF;
            block.tail = System.BitConverter.ToUInt16(_buffer, _counter + 4);
            block.p0 = _buffer[_counter + 6];
            block.p1 = _buffer[_counter + 7];
            _counter += 8;

            //The pulse sequences and the data must fit in what the block says it holds
            long dataBytes = (block.count + 7L) / 8;
            if (8 + 2L * (block.p0 + block.p1) + dataBytes > size)
                return null;

            for (int i = 0; i < block.p0; i++, _counter += 2) {
                ushort s = System.BitConverter.ToUInt16(_buffer, _counter);
                block.s0.Add(s);
            }
            for (int i = 0; i < block.p1; i++, _counter += 2) {
                ushort s = System.BitConverter.ToUInt16(_buffer, _counter);
                block.s1.Add(s);
            }
            for (long i = 0; i < dataBytes; i++) {
                byte b = _buffer[_counter++];
                block.data.Add(b);
            }
            return block;
        }

        public static bool LoadPZX(ref byte[] buffer) {
            try {
                return LoadPZXCore(buffer);
            }
            catch (System.Exception) {
                //Truncated or corrupt image: report failure instead of crashing the caller.
                blocks.Clear();
                tapeBlockInfo.Clear();
                return false;
            }
        }

        private static bool LoadPZXCore(byte[] buffer) {
            blocks.Clear();
            tapeBlockInfo.Clear();

            if (buffer == null || buffer.Length < 8)
                return false; //something bad happened!

            int counter = 0;
            bool first = true;

            //Fewer than 8 bytes left can't hold a block header, so trailing junk is ignored
            while (counter + 8 <= buffer.Length) {
                string blockTag = System.Text.Encoding.ASCII.GetString(buffer, counter, 4);
                uint blockSize = System.BitConverter.ToUInt32(buffer, counter + 4);
                counter += 8;

                //Every PZX file starts with PZXT: that's how we recognise one
                if (first && blockTag != "PZXT")
                    return false;
                first = false;

                //Block runs past the end of the file: stop here and keep what we've read
                if (blockSize > buffer.Length - counter)
                    break;

                switch (blockTag) {
                    case "PZXT":
                    PZXT_Header header = blockSize >= 2 ? GetHeader(buffer, counter, blockSize) : null;
                    if (header == null)
                        return false; //too small, or a major version we don't implement
                    header.tag = "PZXT Header";
                    header.size = blockSize;
                    blocks.Add(header);
                    break;

                    case "PULS":
                    PULS_Block pblock = GetPulse(buffer, counter, blockSize);
                    if (pblock == null)
                        break; //malformed: skip it
                    pblock.tag = "PULS";
                    pblock.size = blockSize;
                    blocks.Add(pblock);
                    break;

                    case "DATA":
                    DATA_Block dblock = GetData(buffer, counter, blockSize);
                    if (dblock == null)
                        break; //malformed: skip it
                    dblock.tag = "DATA";
                    dblock.size = blockSize;
                    blocks.Add(dblock);
                    break;

                    case "PAUS":
                    if (blockSize < 4)
                        break; // too small
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
                    if (blockSize < 2)
                        break; // too small
                    STOP_Block stopBlock = new STOP_Block();
                    stopBlock.tag = "STOP";
                    stopBlock.flag = System.BitConverter.ToUInt16(buffer, counter);
                    stopBlock.size = blockSize;
                    blocks.Add(stopBlock);
                    break;

                    default:
                    break; //unknown or custom block: skip it
                }
                counter += (int)blockSize;
            }
            return blocks.Count > 0;
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
                    PZXFile.PULS_Block puls = (PZXFile.PULS_Block)PZXFile.blocks[f];
                    info.Info = puls.pulse.Count > 0 ? puls.pulse[0].duration.ToString() + " t-states   " : "";
                } else if (PZXFile.blocks[f] is PZXFile.STOP_Block) {
                    info.Info = "Stop the tape.";
                } else if (PZXFile.blocks[f] is PZXFile.DATA_Block) {

                    PZXFile.DATA_Block _data = (PZXFile.DATA_Block)PZXFile.blocks[f];
                    int d = (int)(_data.count / 8);
                    info.IsStandardBlock = IsStandardData(_data);

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
                else if (PZXFile.blocks[f] is PZXFile.PZXT_Header header) {
                    //Every block gets a row, so a row's index is the block's index in blocks.
                    //The spec names a PZXT browse point by its title, or "Tape start" without one.
                    info.Info = string.IsNullOrEmpty(header.Title) ? "Tape start" : header.Title;
                }

                info.Block = PZXFile.blocks[f].tag;
                tapeBlockInfo.Add(info);
            }
        }
    }
}
