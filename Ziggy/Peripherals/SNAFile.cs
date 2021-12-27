using System;
using System.Runtime.InteropServices;

namespace Peripherals
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SNA_HEADER
    {
        public byte I;                  //I Register
        public ushort HL_, DE_, BC_, AF_;  //Alternate registers
        public ushort HL, DE, BC, IY, IX;  //16 bit main registers
        public byte IFF2;               //Interrupt enabled? (bit 2 on/off)
        public byte R;                  //R Register
        public ushort AF, SP;              //AF and SP register
        public byte IM;                 //Interupt Mode
        public byte BORDER;             //Border colour
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class SNA_SNAPSHOT
    {
        public byte TYPE;                      //0 = 48k, 1 = 128;
        public SNA_HEADER HEADER;              //The above header
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class SNA_48K : SNA_SNAPSHOT
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 49152)]
        public byte[] RAM;              //Contents of the RAM
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public class SNA_128K : SNA_SNAPSHOT
    {
        public ushort PC;                                  //PC Register
        public byte PORT_7FFD;                          //Current state of port 7ffd
        public byte TR_DOS;                             //Is TR DOS ROM paged in?
        public byte[][] RAM_BANK = new byte[16][];        //Contents of the 8192*16 ram banks
    }

    public class SNAFile
    {
        //Will return a filled snapshot structure from buffer
        public static SNA_SNAPSHOT LoadSNA(ref byte[] buffer) {
            SNA_SNAPSHOT snapshot;
   
            if (buffer.Length == 0)
                return null; //something bad happened!

            if (buffer.Length == 49179) {
                snapshot = new SNA_48K();
                snapshot.TYPE = 0;
            }
            else if (buffer.Length == 131103 || buffer.Length == 147487) {
                snapshot = new SNA_128K();
                snapshot.TYPE = 1;
            }
            else
                return null;

            snapshot.HEADER.I = buffer[0];
            snapshot.HEADER.HL_ = (ushort)(buffer[1] | (buffer[2] << 8));
            snapshot.HEADER.DE_ = (ushort)(buffer[3] | (buffer[4] << 8));
            snapshot.HEADER.BC_ = (ushort)(buffer[5] | (buffer[6] << 8));
            snapshot.HEADER.AF_ = (ushort)(buffer[7] | (buffer[8] << 8));

            snapshot.HEADER.HL = (ushort)(buffer[9] | (buffer[10] << 8));
            snapshot.HEADER.DE = (ushort)(buffer[11] | (buffer[12] << 8));
            snapshot.HEADER.BC = (ushort)(buffer[13] | (buffer[14] << 8));
            snapshot.HEADER.IY = (ushort)(buffer[15] | (buffer[16] << 8));
            snapshot.HEADER.IX = (ushort)(buffer[17] | (buffer[18] << 8));

            snapshot.HEADER.IFF2 = buffer[19];
            snapshot.HEADER.R = buffer[20];
            snapshot.HEADER.AF = (ushort)(buffer[21] | (buffer[22] << 8));
            snapshot.HEADER.SP = (ushort)(buffer[23] | (buffer[24] << 8));
            snapshot.HEADER.IM = buffer[25];
            snapshot.HEADER.BORDER = (byte)(buffer[26] & 0x07);

            //48k snapshot
            if (snapshot.TYPE == 0) {
                ((SNA_48K)snapshot).RAM = new byte[49152];
                Array.Copy(buffer, 27, ((SNA_48K)snapshot).RAM, 0, 49152);
            }
            else {
                //128k snapshot
                for (int f = 0; f < 16; f++) {
                    ((SNA_128K)snapshot).RAM_BANK[f] = new byte[8192];
                }

                //Copy ram bank 5
                Array.Copy(buffer, 27, ((SNA_128K)snapshot).RAM_BANK[10], 0, 8192);
                Array.Copy(buffer, 27 + 8192, ((SNA_128K)snapshot).RAM_BANK[11], 0, 8192);

                //Copy ram bank 2
                Array.Copy(buffer, 27 + 16384, ((SNA_128K)snapshot).RAM_BANK[4], 0, 8192);
                Array.Copy(buffer, 27 + 16384 + 8192, ((SNA_128K)snapshot).RAM_BANK[5], 0, 8192);

                ((SNA_128K)snapshot).PORT_7FFD = buffer[49181]; //we'll load this in earlier 'cos we need it now!

                int BankInPage4 = ((SNA_128K)snapshot).PORT_7FFD & 0x07;

                //Copy currently paged in bank (actually we don't care here 'cos we're simply filling in all the b(l)anks)
                Array.Copy(buffer, 27 + 16384 + 16384, ((SNA_128K)snapshot).RAM_BANK[BankInPage4 * 2], 0, 8192);
                Array.Copy(buffer, 27 + 16384 + 16384 + 8192, ((SNA_128K)snapshot).RAM_BANK[BankInPage4 * 2 + 1], 0, 8192);

                ((SNA_128K)snapshot).PC = (ushort)(buffer[49179] | (buffer[49180] << 8));

                ((SNA_128K)snapshot).TR_DOS = buffer[49182];

                int t = 0;
                for (int f = 0; f < 8; f++) {
                    if (f == 5 || f == 2 || f == BankInPage4)
                        continue;

                    Array.Copy(buffer, 49183 + 16384 * t, ((SNA_128K)snapshot).RAM_BANK[f * 2], 0, 8192);
                    Array.Copy(buffer, 49183 + 16384 * t + 8192, ((SNA_128K)snapshot).RAM_BANK[f * 2 + 1], 0, 8192);
                    t++;
                }
            }
            return snapshot;
        }

        //Will return a filled snapshot structure from file stream
        public static SNA_SNAPSHOT LoadSNA(System.IO.Stream fs) {
            SNA_SNAPSHOT snapshot;
            using (System.IO.BinaryReader r = new System.IO.BinaryReader(fs)) {
                byte[] buffer = new byte[fs.Length];
                int bytesRead = r.Read(buffer, 0, (int)fs.Length);
                snapshot = LoadSNA(ref buffer);
            }
            return snapshot;
        }

        //Will return a filled snapshot structure from file
        public static SNA_SNAPSHOT LoadSNA(string filename) {
            SNA_SNAPSHOT sna;
            using (System.IO.FileStream fs = new System.IO.FileStream(filename, System.IO.FileMode.Open)) {
                byte[] buffer = new byte[fs.Length];
                int bytesRead = fs.Read(buffer, 0, (int)fs.Length);
                sna = LoadSNA(ref buffer);
            }
            return sna;
        }

        public static void SaveSNA(string filename, SNA_SNAPSHOT sna) {

            using (System.IO.FileStream fs = new System.IO.FileStream(filename, System.IO.FileMode.Create)) {

                byte[] bytes = ByteUtililty.RawSerialize(sna.HEADER);
                fs.Write(bytes, 0, bytes.Length);

                if (sna is SNA_48K)
                        fs.Write(((SNA_48K)sna).RAM, 0, ((SNA_48K)sna).RAM.Length);
                else {
                        //Write speccy banks 5, 2 and n which are pre-prepared in snapshot ram 0 to 5
                        for (int f = 0; f < 6; f++)
                            fs.Write(((SNA_128K)sna).RAM_BANK[f], 0, 8192);

                        bytes = ByteUtililty.RawSerialize(((SNA_128K)sna).PC);
                        fs.Write(bytes, 0, bytes.Length);
                        fs.WriteByte(((SNA_128K)sna).PORT_7FFD);
                        fs.WriteByte(((SNA_128K)sna).TR_DOS);

                        //Write remaining banks
                        for (int f = 6; f < 16; f++)
                            fs.Write(((SNA_128K)sna).RAM_BANK[f], 0, 8192);
                }
            }
        }
    }
}