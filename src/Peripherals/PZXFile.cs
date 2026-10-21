using System;

namespace Peripherals
{
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


        /// <summary>Read a PZX image's blocks. Null if it isn't one, or is too damaged to read.</summary>
        public static System.Collections.Generic.List<Block> Parse(byte[] buffer) {
            try {
                return ParseCore(buffer);
            }
            catch (System.Exception) {
                //Truncated or corrupt image: report failure instead of crashing the caller.
                return null;
            }
        }

        private static System.Collections.Generic.List<Block> ParseCore(byte[] buffer) {
            var blocks = new System.Collections.Generic.List<Block>();

            if (buffer == null || buffer.Length < 8)
                return null; //something bad happened!

            int counter = 0;
            bool first = true;

            //Fewer than 8 bytes left can't hold a block header, so trailing junk is ignored
            while (counter + 8 <= buffer.Length) {
                string blockTag = System.Text.Encoding.ASCII.GetString(buffer, counter, 4);
                uint blockSize = System.BitConverter.ToUInt32(buffer, counter + 4);
                counter += 8;

                //Every PZX file starts with PZXT: that's how we recognise one
                if (first && blockTag != "PZXT")
                    return null;
                first = false;

                //Block runs past the end of the file: stop here and keep what we've read
                if (blockSize > buffer.Length - counter)
                    break;

                switch (blockTag) {
                    case "PZXT":
                    PZXT_Header header = blockSize >= 2 ? GetHeader(buffer, counter, blockSize) : null;
                    if (header == null)
                        return null; //too small, or a major version we don't implement
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
            return blocks.Count > 0 ? blocks : null;
        }

        /// <summary>Read a PZX image as a tape to play. Null if it isn't one.</summary>
        public static TapeImage Load(byte[] buffer) {
            var blocks = Parse(buffer);
            return blocks == null ? null : ToTape(blocks);
        }

        /// <summary>
        /// The tape the blocks play. PZX's level rule is the player's own, so each block maps
        /// straight across: PULS starts low, DATA and PAUS start at the level they give.
        /// </summary>
        public static TapeImage ToTape(System.Collections.Generic.List<Block> blocks) {
            var tape = new TapeImage();
            foreach (Block b in blocks) {
                var t = new TapeBlock { Kind = b.tag };
                switch (b) {
                    case PULS_Block puls: {
                        var runs = new PulseRun[puls.pulse.Count];
                        for (int i = 0; i < runs.Length; i++) {
                            Pulse p = puls.pulse[i];
                            runs[i] = new PulseRun(p.count, p.duration, Edge.Keep, p.count > 1 ? TapePhase.Pilot : TapePhase.Sync);
                        }
                        t.Segments = new TapeSegment[] { new PulseSegment { Entry = Edge.Low, Runs = runs } };
                        t.Info = TapeText.DescribePulses(runs);
                        break;
                    }
                    case DATA_Block data: {
                        byte[] bytes = data.data.ToArray();
                        var segment = new DataSegment {
                            Entry = data.initialPulseLevel != 0 ? Edge.High : Edge.Low,
                            Data = bytes,
                            SymbolCount = data.count,
                            Symbols = new[] { new Symbol(Edge.Keep, data.s0.ToArray()), new Symbol(Edge.Keep, data.s1.ToArray()) },
                            Tail = data.tail
                        };
                        t.Segments = new TapeSegment[] { segment };
                        t.Info = TapeText.DescribeData(bytes, data.count, segment.IsStandardRom);
                        break;
                    }
                    case PAUS_Block pause:
                        t.Segments = new TapeSegment[] {
                            new PulseSegment {
                                Entry = pause.initialPulseLevel != 0 ? Edge.High : Edge.Low,
                                Runs = pause.duration > 0
                                    ? new[] { new PulseRun(1, pause.duration, Edge.Keep, TapePhase.Pause) }
                                    : System.Array.Empty<PulseRun>()
                            }
                        };
                        t.Info = TapeText.DescribePause(pause.duration);
                        break;
                    case STOP_Block stop:
                        //flag 1 = stop only on 48K; 0 and any value we don't know = always stop
                        t.Control = stop.flag == 1 ? TapeControlKind.StopIf48K : TapeControlKind.Stop;
                        break;
                    case BRWS_Block brws:
                        t.Info = brws.text;
                        break;
                    case PZXT_Header header:
                        //The spec names a PZXT browse point by its title, or "Tape start" without one.
                        t.Info = string.IsNullOrEmpty(header.Title) ? "Tape start" : header.Title;
                        tape.Info = new TapeInfo {
                            Title = header.Title, Publisher = header.Publisher, Authors = header.Authors,
                            Year = header.YearOfPublication, Language = header.Language, Type = header.Type,
                            Price = header.Price, Protection = header.ProtectionScheme, Origin = header.Origin,
                            Comments = header.Comments
                        };
                        break;
                }
                tape.Blocks.Add(t);
            }
            return tape;
        }
    }
}
