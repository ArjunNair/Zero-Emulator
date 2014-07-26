

using System;
using System.IO;


namespace Speccy
{
    //Encapsulates the capabilities of the machine
    

    public class zxmachine
    {
        protected Z80Core cpu;
        public const bool ENABLE_SOUND = false;
        public bool isROMprotected = true;

        //16x8k flat RAM bank (because of issues with pointers in c#) + 1 dummy bank
        //public int[] RAMpage = new int[1024*8*16 + 1024*8]; //0 to 65536 effectively + dummy bank of 8192 bytes
        public int[][] RAMpage = new int[16][]; //16 pages of 8192 bytes each

        //4x8k flat ROM bank
        //public int[] ROMpage = new int[1024 * 8 * 4];
        public int[][] ROMpage = new int[4][];

        //8 "pointers" to the pages
        //NOTE: In the case of +3, Pages 0 and 1 *can* point to a RAMpage. In other cases they point to a 
        //ROMpage. To differentiate which is being pointed to, the +3 machine employs the specialMode boolean.

        //public int[] PagePointer = new int[8]; 
        public int[][] PagePointer = new int[8][];
        public int[] ScreenBuffer;
        public int[] screen;

        public enum keyCode
        {
            Q, W, E, R, T, Y, U, I, O, P, A, S, D, F, G, H, J, K, L, Z, X, C, V, B, N, M,
            _0, _1, _2, _3, _4, _5, _6, _7, _8, _9,
            SPACE, SHIFT, CTRL, ALT, TAB, CAPS, ENTER, BACK, 
            DEL, INS, HOME, END, PGUP, PGDOWN, NUMLOCK,
            ESC, PRINT_SCREEN, SCROLL_LOCK, PAUSE_BREAK,
            TILDE, EXCLAMATION, AT, HASH, DOLLAR, PERCENT, CARAT,
            AMPERSAND, ASTERISK, LBRACKET, RBRACKET, HYPHEN, PLUS, VBAR,
            LCURLY, RCURLY, COLON, DQUOTE, LESS_THAN, GREATER_THAN, QMARK,
            UNDER_SCORE, EQUALS, BSLASH, LSQUARE, RSQUARE, SEMI_COLON,
            APOSTROPHE, COMMA, STOP, FSLASH, LEFT, RIGHT, UP, DOWN,
            F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
            LAST
        };

        public bool[] keyBuffer;

        public zxmachine(IntPtr handle)
        {
           
            cpu = new Z80Core();
            ROMpage[0] = new int[8192];
            ROMpage[1] = new int[8192];
            ROMpage[2] = new int[8192];
            ROMpage[3] = new int[8192];
            RAMpage[0] = new int[8192];
            RAMpage[1] = new int[8192];
            RAMpage[2] = new int[8192];
            RAMpage[3] = new int[8192];
            RAMpage[4] = new int[8192];
            RAMpage[5] = new int[8192];
            RAMpage[6] = new int[8192];
            RAMpage[7] = new int[8192];
            RAMpage[8] = new int[8192];
            RAMpage[9] = new int[8192];
            RAMpage[10] = new int[8192];
            RAMpage[11] = new int[8192];
            RAMpage[12] = new int[8192];
            RAMpage[13] = new int[8192];
            RAMpage[14] = new int[8192];
            RAMpage[15] = new int[8192];

           

        }

        public int GetPageAddress(int page)
        {
            return 8192 * page;
        }

        public int[] GetPageData(int page)
        {
            return PagePointer[page];
        }

        public virtual void Reset()
        {
            cpu.Reset();
        }

        public virtual void Shutdown()
        {
            cpu.Shutdown();
        }

        public virtual int PeekByte(int addr)
        {
            return 0;
        }

        public virtual void PokeByte(int addr, int val)
        {

        }

        public virtual void Run()
        {
            for (; ; )
            {
                cpu.Process();
            }
        }

        public virtual int In(int port)
        {
            return 0xff;
        }

        public virtual void UpdateScreenBuffer()
        {
        }

        public virtual void Out(int port, int val)
        {
            
        }

        public virtual bool LoadROM(string filename)
        {
             return true;
        }

        public virtual void UpdateInput()
        {
        }

        public virtual void OutSound() { }
        public virtual void UpdateAudio() { }
        public virtual void LoadTape(string filename) {}
        public virtual void LoadSnapshot(string filename) {}


    }

    //Implements a zx 48k machine
    public class zx48: Speccy.zxmachine
    {
       // private SoundManager beeper;

        int[] keyLine = {255, 255, 255, 255, 255, 255, 255, 255 };
        bool CapsLockOn = false;

        private double clockSpeed;
        private const int ScreenWidth = 256;
        private const int ScreenHeight = 192;

        private const int BorderHeight = 48;
        private const int BorderWidth = 48;

        private const int ScanLineWidth = BorderWidth * 2 + ScreenWidth;

        private const int CharRows = 24;
        private const int CharCols = 32;

        private const int DisplayStart = 16384;
        private const int DisplayLength = 6144;
        private int AttributeStart = 22528;
        private const int AttributeOffset = 6144;
        private const int AttributeLength = 768;

        private const int sat = 238;
   
        private static int[] AttrColors = {
                                             0x000000,            // Black
                                             0x0000CA,            // Red
                                             0xCA0000,            // Blue
                                             0xCA00A0,            // Magenta
                                             0x00CA00,            // Green
                                             0x00CACA,            // Yellow
                                             0xCACA00,            // Cyan
                                             0xC5C7C5,            // White
                                             0x000000,            // Bright Black
                                             0x0000FF,            // Bright Red
                                             0xFF0000,            // Bright Blue    
                                             0xFF00FF,            // Bright Magenta
                                             0x00FF00,            // Bright Green
                                             0x00FFFF,            // Bright Yellow
                                             0xFFFF00,            // Bright Cyan
                                             0xFFFFFF            // Bright White
                                             };


        private int borderColour = 7;   //Used by the screen update routine to output border colour
        private int oldBorderColour = 255;
        private int frameCount = 0;
        private int soundOut = 0;

        public zx48(IntPtr handle): base(handle)
        {
            clockSpeed = 3.50000;
            cpu.Machine = this;
            cpu.interruptPeriod = 32;
            cpu.interruptTime = 69998;
            
           /* PagePointer[0] = 0; 
            PagePointer[1] = 1;
            PagePointer[2] = 5;
            PagePointer[3] = 6;
            PagePointer[4] = 2;
            PagePointer[5] = 3;
            PagePointer[6] = 0; 
            PagePointer[7] = 1; 
            */

            PagePointer[0] = ROMpage[0];
            PagePointer[1] = ROMpage[1];
            PagePointer[2] = RAMpage[5];
            PagePointer[3] = RAMpage[6];
            PagePointer[4] = RAMpage[2];
            PagePointer[5] = RAMpage[3];
            PagePointer[6] = RAMpage[0];
            PagePointer[7] = RAMpage[1];

            Random rand = new Random();
            //Fill memory with random stuff to simulate hard reset
            for (int i = 16384; i < 65535; ++i)
                PokeByte(i, rand.Next(255));

            ScreenBuffer = new int[(48 + 256 + 48) * 48 //48 lines of border
                                              +((48+256+48) * 192) //border + main + border of 192 lines
                                              + (48 + 256 + 48) * 48]; //48 lines of border
            keyBuffer = new bool[(int)keyCode.LAST];
            screen = GetPageData(2);
            //beeper = new SoundManager(handle);
            
        }

        public override void Reset()
        {
            cpu.Reset();
            //if (!beeper.initialised && ENABLE_SOUND)
             //   beeper.Initialise();
        }

        public override void Run()
        {
             cpu.Process();
        }


        public override void Shutdown()
        {
            cpu.Shutdown();
        }

        public override int In(int port)
        {
            int result = 0xff;
            
            port = port & 0xffff;

            if ((port & 0x01) == 0)    //Even address, so get input
            {
                if ((port & 0x8000) == 0)
                        result &=  keyLine[7];
                        
                if ((port & 0x4000) == 0)
                        result &=  keyLine[6];
                    
                if ((port & 0x2000) == 0)
                        result &= keyLine[5];
                
                if ((port & 0x1000) == 0)
                         result &=  keyLine[4];

                if ((port & 0x800) == 0)
                         result &= keyLine[3];

                if ((port & 0x400) == 0)
                         result &= keyLine[2];
                         
                if ((port & 0x200) == 0)
                         result &= keyLine[1];

                if ((port & 0x100) == 0)
                         result &= keyLine[0];
               
            }
            return (result & 0xff);
        }

        public override void Out(int port, int val)
        {
            if ((port & 0x01) == 0)    //Even address, so update ULA
            {
                borderColour = val & 0x07;  //The LSB 3 bits of val hold the border colour

                if ((val & 0x010) != 0)
                    soundOut = int.MaxValue/20000;
                else
                    soundOut = 0;
            }

        }

        public override void UpdateScreenBuffer()
        {
            int lineIndex = 0;
            int tempVar;
            //First draw the top lines of border
            if (borderColour != oldBorderColour)
            {
                while (lineIndex < (ScanLineWidth) * BorderHeight)
                {
                    ScreenBuffer[lineIndex++] = (AttrColors[borderColour]);
                }
            }
            else
            {
                lineIndex = (ScanLineWidth) * BorderHeight;
            }

           // int[] screen = GetPageData(2);
            int screenAddress = 16384;//GetPageAddress(10);    //Actually bank 5 if 16k pages, but since we are on 8k pages...

           AttributeStart = screenAddress + AttributeOffset;

            //Draw the middle half
            while (lineIndex < ((ScanLineWidth * BorderHeight + ScanLineWidth * ScreenHeight)))
            {
                //draw pixels of left border
                if (borderColour != oldBorderColour)
                {
                    for (int f = 0; f < BorderWidth; ++f)
                    {
                        ScreenBuffer[lineIndex++] = (AttrColors[borderColour]);
                    }
                }
                else
                {
                    lineIndex = (ScanLineWidth * BorderHeight + ScanLineWidth * ScreenHeight);
                }

                tempVar = lineIndex;

                //int addrH = screenAddress >> 8; //div by 256
                //int addrL = screenAddress % 256;

                int addrH = screenAddress >> 8; //div by 256
                int addrL = screenAddress % 256;

                int pixelY = (addrH & 0x07);
                pixelY |= (addrL & (0xE0)) >> 2;
                pixelY |= (addrH & (0x18)) << 3;

                lineIndex = (16944 + (ScanLineWidth * pixelY));

                int attrIndex_Y = AttributeStart + ((pixelY >> 3) << 5);// pixel/8 * 32

                //draw the main screen
                for (int f = 0; f < 32; ++f)
                {
                    //screenAddress += (uint)f;
                    addrL = screenAddress % 256;
                    int pixelX = addrL & (0x1F);
                    
                    int attrVal = PeekByte(attrIndex_Y + pixelX);
                    int pixelData = screen[screenAddress++ - 16384];
                    //int pixelData = PeekByte(screenAddress++);

                    int bright = (attrVal & 0x40) >> 7;

                    for (int g = 0; g < 8; ++g)
                    {
                        if ((pixelData & 0x80) != 0)
                        {
                            //Plot ink
                            ScreenBuffer[lineIndex++] =   AttrColors[(attrVal & 0x07) + bright];
                        }
                        else
                        {
                            //Plot Paper
                            ScreenBuffer[lineIndex++] = AttrColors[((attrVal >> 3) & 0x7) + bright];
                        }
                        
                        pixelData <<= 1;
                    }       
                }

                //draw pixels of right border
                if (borderColour != oldBorderColour)
                {
                    for (int f = 0; f < BorderWidth; ++f)
                    {
                        ScreenBuffer[lineIndex++] = AttrColors[borderColour];
                    }
                }
                else
                {
                    lineIndex += BorderWidth;
                }
            }

            tempVar = lineIndex;
            //Draw the bottom lines of border
            if (borderColour != oldBorderColour)
            {
                while (lineIndex < (tempVar + ScanLineWidth * BorderHeight))
                {
                    ScreenBuffer[lineIndex++] = (AttrColors[borderColour]);
                }
                oldBorderColour = borderColour;
            }
            else
            {
                lineIndex = tempVar + ScanLineWidth * BorderHeight;
            }

            //Update flash attributes
            frameCount++;
            if (frameCount >= 15)
            {
                InvertFlashAttributes();
                frameCount = 0;
            }
          }

        private void InvertFlashAttributes()
        {
            //int attrAddr = GetPageAddress(10) + AttributeOffset;
            int attrAddr = 16384 + AttributeOffset;
            //for (int f = AttributeStart; f < AttributeStart + AttributeLength; f++)
            for (int f = 0; f < AttributeLength; f++)
            {
                //int attrVal = RAMpage[attrAddr + f]; 
                int attrVal = PeekByte(attrAddr + f);
                if ((attrVal & 0x80) != 0)
                {
                    int ink = attrVal & 0x07;
                    int paper = (attrVal >> 3) & 0x07;
                    //RAMpage[attrAddr + f] = (attrVal & 0xC0) | (ink << 3) | paper;
                    PokeByte(attrAddr + f, (attrVal & 0xC0) | (ink << 3) | paper);
                }
            }
        }

        public override bool LoadROM(string path)
        {
            FileStream fs;
            String filename = path + "\\48k.rom";

            //Check if we can find the ROM file!
            try
            {
                fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            }
            catch
            {
               return false;
            }
            
            fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            using (BinaryReader r = new BinaryReader(fs))
            {
                //int bytesRead = ReadBytes(r, mem, 0, 16384);
                byte[] buffer = new byte[16384];
                int bytesRead = r.Read(buffer, 0, 16384);

                if (bytesRead == 0)
                    return false; //something bad happened!

                for (int f = 0; f < 8192; ++f)
                {
                    ROMpage[0][f] = (buffer[f] + 256) & 0xff;
                }

                for (int f = 0; f < 8192; ++f)
                {
                    ROMpage[1][f] = (buffer[8192+f] + 256) & 0xff;
                }
            }
            fs.Close();
            return true;
        }

        public override void UpdateInput()
        {

            #region  Row 0: fefe - CAPS SHIFT, Z, X, C , V
            if (keyBuffer[(int)keyCode.SHIFT])
            {
               keyLine[0] = keyLine[0] & ~(0x1);
            }
            else 
            {
                keyLine[0] = keyLine[0] | (0x1);
            }

            if (keyBuffer[(int)keyCode.Z])
            {
               keyLine[0] = keyLine[0] & ~(0x02);
            }
            else 
            {
                keyLine[0] = keyLine[0] | (0x02);
            }

            if (keyBuffer[(int)keyCode.X])
            {
               keyLine[0] = keyLine[0] & ~(0x04);
            }
            else 
            {
                keyLine[0] = keyLine[0] | (0x04);
            }

            if (keyBuffer[(int)keyCode.C])
            {
                keyLine[0] = keyLine[0] & ~(0x08);
            }
            else 
            {
                keyLine[0] = keyLine[0] | (0x08);
            }

            if (keyBuffer[(int)keyCode.V])
            {
                keyLine[0] = keyLine[0] & ~(0x10);
            }
            else 
            {
                keyLine[0] = keyLine[0] | (0x10);
            }
            #endregion

            #region Row 1: fdfe - A, S, D, F, G
            if (keyBuffer[(int)keyCode.A])
            {
                keyLine[1] = keyLine[1] & ~(0x1);
            }
            else 
            {
                keyLine[1] = keyLine[1] | (0x1);
            }

            if (keyBuffer[(int)keyCode.S])
            {
                    keyLine[1] = keyLine[1] & ~(0x02);
            }
            else 
            {
                keyLine[1] = keyLine[1] | (0x02);
            }

            if (keyBuffer[(int)keyCode.D])
            {
                keyLine[1] = keyLine[1] & ~(0x04);
            }
            else 
            {
                keyLine[1] = keyLine[1] | (0x04);
            }

            if (keyBuffer[(int)keyCode.F])
            {
                keyLine[1] = keyLine[1] & ~(0x08);
            }
            else 
            {
                keyLine[1] = keyLine[1] | (0x08);
            }

            if (keyBuffer[(int)keyCode.G])
            {
                keyLine[1] = keyLine[1] & ~(0x10);
            }
            else 
            {
                keyLine[1] = keyLine[1] | (0x10);
            }
            #endregion

            #region Row 2: fbfe - Q, W, E, R, T
            if (keyBuffer[(int)keyCode.Q])
            {
                keyLine[2] = keyLine[2] & ~(0x1);
            }
            else 
            {
                keyLine[2] = keyLine[2] | (0x1);
            }

            if (keyBuffer[(int)keyCode.W])
            {
                keyLine[2] = keyLine[2] & ~(0x02);
            }
            else
            {
                keyLine[2] = keyLine[2] | (0x02);
            }

            if (keyBuffer[(int)keyCode.E])
            {
                keyLine[2] = keyLine[2] & ~(0x04);
            }
            else 
            {
                keyLine[2] = keyLine[2] | (0x04);
            }

            if (keyBuffer[(int)keyCode.R])
            {
                keyLine[2] = keyLine[2] & ~(0x08);
            }
            else 
            {
                keyLine[2] = keyLine[2] | (0x08);
            }

            if (keyBuffer[(int)keyCode.T])
            {
                keyLine[2] = keyLine[2] & ~(0x10);
            }
            else 
            {
                keyLine[2] = keyLine[2] |(0x10);
            }
            #endregion

            #region Row 3: f7fe - 1, 2, 3, 4, 5
            if (keyBuffer[(int)keyCode._1])
            {
                keyLine[3] = keyLine[3] & ~(0x1);
            }
            else 
            {
                keyLine[3] = keyLine[3] | (0x1);
            }

            if (keyBuffer[(int)keyCode._2])
            {
                keyLine[3] = keyLine[3] & ~(0x02);
            }
            else
            {
                keyLine[3] = keyLine[3] | (0x02);
            }

            if (keyBuffer[(int)keyCode._3])
            {
                keyLine[3] = keyLine[3] & ~(0x04);
            }
            else 
            {
                keyLine[3] = keyLine[3] | (0x04);
            }

            if (keyBuffer[(int)keyCode._4])
            {
                keyLine[3] = keyLine[3] & ~(0x08);
            }
            else 
            {
                keyLine[3] = keyLine[3] | (0x08);
            }

            if (keyBuffer[(int)keyCode._5])
            {
                keyLine[3] = keyLine[3] & ~(0x10);
            }
            else 
            {
                keyLine[3] = keyLine[3] | (0x10);
            }
            #endregion

            #region Row 4: effe - 0, 9, 8, 7, 6 
            if (keyBuffer[(int)keyCode._0])
            {
                    keyLine[4] = keyLine[4] & ~(0x1);
            }
            else
            {
                keyLine[4] = keyLine[4] | (0x1);
            }

            if (keyBuffer[(int)keyCode._9])
            {
                keyLine[4] = keyLine[4] & ~(0x02);
            }
            else
            {
                keyLine[4] = keyLine[4] | (0x02);
            }

            if (keyBuffer[(int)keyCode._8])
            {
                keyLine[4] = keyLine[4] & ~(0x04);
            }
            else 
            {
                keyLine[4] = keyLine[4] | (0x04);
            }

            if (keyBuffer[(int)keyCode._7])
            {
                 keyLine[4] = keyLine[4] & ~(0x08);
            }
            else 
            {
                keyLine[4] = keyLine[4] | (0x08);
            }

            if (keyBuffer[(int)keyCode._6])
            {
                 keyLine[4] = keyLine[4] & ~(0x10);
            }
            else
            {
                keyLine[4] = keyLine[4] | (0x10);
            }
            #endregion

            #region Row 5: dffe - P, O, I, U, Y 
            if (keyBuffer[(int)keyCode.P])
            {
                keyLine[5] = keyLine[5] & ~(0x1);
            }
            else 
            {
                keyLine[5] = keyLine[5] | (0x1);
            }

            if (keyBuffer[(int)keyCode.O])
            {
                keyLine[5] = keyLine[5] & ~(0x02);
            }
            else
            {
                keyLine[5] = keyLine[5] | (0x02);
            }

            if (keyBuffer[(int)keyCode.I])
            {
                keyLine[5] = keyLine[5] & ~(0x04);
            }
            else
            {
                keyLine[5] = keyLine[5] | (0x04);
            }

            if (keyBuffer[(int)keyCode.U])
            {
                keyLine[5] = keyLine[5] & ~(0x08);
            }
            else
            {
                keyLine[5] = keyLine[5] | (0x08);
            }

            if (keyBuffer[(int)keyCode.Y])
            {
                keyLine[5] = keyLine[5] & ~(0x10);
            }
            else
            {
                keyLine[5] = keyLine[5] | (0x10);
            }
            #endregion

            #region Row 6: bffe - ENTER, L, K, J, H
            if (keyBuffer[(int)keyCode.ENTER])
            {
               // cpu.loggingEnabled = true;
                keyLine[6] = keyLine[6] & ~(0x1);
            }
            else
            {
                keyLine[6] = keyLine[6] | (0x1);
            }

            if (keyBuffer[(int)keyCode.L])
            {
                keyLine[6] = keyLine[6] & ~(0x02);
               
            }
            else
            {
                keyLine[6] = keyLine[6] | (0x02);
            }

            if (keyBuffer[(int)keyCode.K])
            {
                keyLine[6] = keyLine[6] & ~(0x04);
            }
            else
            {
                keyLine[6] = keyLine[6] | (0x04);
            }

            if (keyBuffer[(int)keyCode.J])
            {
                 keyLine[6] = keyLine[6] & ~(0x08);
            }
            else
            {
                keyLine[6] = keyLine[6] | (0x08);
            }

            if (keyBuffer[(int)keyCode.H])
            {
                keyLine[6] = keyLine[6] & ~(0x10);
            }
            else
            {
                keyLine[6] = keyLine[6] | (0x10);
            }
            #endregion

            #region Row 7: 7ffe - SPACE, SYMBOL SHIFT, M, N, B
            if (keyBuffer[(int)keyCode.SPACE])
            {
                keyLine[7] = keyLine[7] & ~(0x1);
            }
            else
            {
                keyLine[7] = keyLine[7] | (0x1);
            }

            if (keyBuffer[(int)keyCode.CTRL])
            {
                keyLine[7] = keyLine[7] & ~(0x02);
            }
            else
            {
                keyLine[7] = keyLine[7] | (0x02);
            }

            if (keyBuffer[(int)keyCode.M])
            {
                keyLine[7] = keyLine[7] & ~(0x04);
            }
            else
            {
                keyLine[7] = keyLine[7] | (0x04);
            }

            if (keyBuffer[(int)keyCode.N])
            {
                keyLine[7] = keyLine[7] & ~(0x08);
            }
            else
            {
                keyLine[7] = keyLine[7] | (0x08);
            }

            if (keyBuffer[(int)keyCode.B])
            {
                keyLine[7] = keyLine[7] & ~(0x10);
            }
            else
            {
                keyLine[7] = keyLine[7] | (0x010);
            }
            #endregion

            #region Misc utility key functions
            //Check for caps lock key
            if (keyBuffer[(int)keyCode.CAPS])
            {
                CapsLockOn = !CapsLockOn;
                keyBuffer[(int)keyCode.CAPS] = false;
            }

            if (CapsLockOn)
            {
                keyLine[0] = keyLine[0] & ~(0x1);
            }
               
            //Check if backspace key has been pressed (Caps Shift + 0 equivalent)
            if (keyBuffer[(int)keyCode.BACK])
            {
                keyLine[0] = keyLine[0] & ~(0x1);
                keyLine[4] = keyLine[0] & ~(0x1);
            }

            //Check if left cursor key has been pressed (Caps Shift + 5)
            if (keyBuffer[(int)keyCode.LEFT])
            {
                keyLine[0] = keyLine[0] & ~(0x1);
                keyLine[3] = keyLine[3] & ~(0x10);
            }

            //Check if right cursor key has been pressed (Caps Shift + 8)
            if (keyBuffer[(int)keyCode.RIGHT])
            {
                keyLine[0] = keyLine[0] & ~(0x1);
                keyLine[4] = keyLine[4] & ~(0x04);
            }

            //Check if up cursor key has been pressed (Caps Shift + 7)
            if (keyBuffer[(int)keyCode.UP])
            {
                keyLine[0] = keyLine[0] & ~(0x1);
                keyLine[4] = keyLine[4] & ~(0x08);
            }

            //Check if down cursor key has been pressed (Caps Shift + 6)
            if (keyBuffer[(int)keyCode.DOWN])
            {
                keyLine[0] = keyLine[0] & ~(0x1);
                keyLine[4] = keyLine[4] & ~(0x08);
            }
            #endregion

        }

        public override void OutSound()
        {
            //beeper.AddSample(soundOut);
        }

        public override void UpdateAudio()
        {
            //beeper.Update();
        }

        public override void LoadSnapshot(string filename)
        {
            if (filename.EndsWith(".sna"))
            {
                LoadSNA(filename);
            }
            else
                if (filename.EndsWith(".z80"))
                {
                    LoadZ80(filename);
                }
                else
                    return;
        }

        public override void LoadTape(string filename)
        {
            if (filename.EndsWith(".tap"))
            {
                LoadTAP(filename);
            }
            else
                if (filename.EndsWith(".tzx"))
                {
                    //LoadTZX(filename);
                }
                else
                    return;
        }

        public bool LoadTAP(string filename)
        {
            return true;
        }

        public override int PeekByte(int addr)
        {
           // int page = (addr & 65535) >> 13;
            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;
            /*
            if (page < 2)
            {
                return (ROMpage[(PagePointer[page] << 13) + (addr % 8192)]); 
            }
            else
                return (RAMpage[(PagePointer[page] << 13) + (addr % 8192)]); 
        
             */
            return PagePointer[page][offset];
        }

        public override void PokeByte(int addr, int b)
        {
            /*
            int page = (addr & 65535) >> 13;

            if (page < 2)
            {
                if (!isROMprotected)
                    ROMpage[(PagePointer[page] << 13) + (addr % 8192)] = b & 0xff;
            }
            else
                RAMpage[(PagePointer[page] << 13) + (addr % 8192)] = b & 0xff;
            */
            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;

            if (page < 2 && isROMprotected)
                return;

            PagePointer[page][offset] = b & 0xff;
        }

        public bool LoadZ80(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open))
            {
                using (BinaryReader r = new BinaryReader(fs))
                {
                    int bytesToRead = (int)fs.Length;

                    byte[] buffer = new byte[bytesToRead];
                    int bytesRead = r.Read(buffer, 0, bytesToRead);

                    if (bytesRead == 0)
                        return false; //something bad happened!

                    cpu.A = buffer[0];
                    cpu.F = buffer[1];
                    cpu.BC = (buffer[2] | (buffer[3] << 8));
                    cpu.HL = (buffer[4] | (buffer[5] << 8));
                    cpu.PC = (buffer[6] | (buffer[7] << 8));
                    cpu.SP = (buffer[8] | (buffer[9] << 8));
                    cpu.I = buffer[10];
                    cpu.R = buffer[11];


                    byte byte12 = buffer[12];
                    if (byte12 == 255)
                        byte12 = 1;

                    cpu.R = cpu.R | ((byte12 & 0x01) << 7);
                    borderColour = (byte12 >> 1) & 0x07;
                    bool isCompressed = ((byte12 & 20) != 0);

                    cpu.DE = (buffer[13] | (buffer[14] << 8));
                    cpu._BC = (buffer[15] | (buffer[16] << 8));
                    cpu._DE = (buffer[17] | (buffer[18] << 8));
                    cpu._HL = (buffer[19] | (buffer[20] << 8));
                    cpu._AF = ((buffer[21] << 8) | buffer[22]);

                    cpu.IY = (buffer[23] | (buffer[24] << 8));
                    cpu.IX = (buffer[25] | (buffer[26] << 8));

                    cpu.IFF1 = (buffer[27] != 0);
                    cpu.IFF2 = (buffer[28] != 0);

                    byte byte29 = buffer[29];

                    cpu.interruptMode = (byte29 & 0x3);

                    if (cpu.PC == 0)
                    {
                        if (LoadZ80V23(buffer)) //Load version 2 or 3 of z80
                            return true;
                        else
                            return false;
                    }

                    int screenAddr = GetPageAddress(10);

                    if (!isCompressed)
                    {
                        for (int f = 0; f < 49152; ++f)
                        {
                            PokeByte(screenAddr + f, (buffer[f + 30]));
                        }
                    }
                    else
                    {
                        bool done = false;
                        int byteCounter = 30;
                        int memCounter = 0;

                        while (!done)
                        {
                            int bite = buffer[byteCounter++];
                            if (bite == 0)
                            {
                                //check if this is the end marker
                                if (buffer[byteCounter++] == 0xED)
                                {
                                    if (buffer[byteCounter++] == 0xED)
                                    {
                                        if (buffer[byteCounter++] == 0)
                                        {
                                            done = true;
                                            continue;
                                        }
                                    }
                                }
                            }
                            else
                                if (bite == 0xED)
                                {
                                    if (buffer[byteCounter++] == 0xED)
                                    {
                                        int dataLength = buffer[byteCounter++];
                                        int data = buffer[byteCounter++];

                                        //compressed data
                                        for (int f = 0; f < dataLength; f++)
                                        {
                                            //RAMpage[16384 + (memCounter++)] = data;
                                            PokeByte(screenAddr + (memCounter++), data);
                                        }
                                    }
                                }

                            //RAMpage[16384 + (memCounter++)] = buffer[byteCounter++];
                            PokeByte(screenAddr + (memCounter++), buffer[byteCounter++]);
                        }
                    }

                }
                                    
            }
            return true;
        }

        public bool LoadZ80V23(byte[] buffer)
        {
            //First read-in the bytes common to version 2 & 3
            int headerLength = buffer[30];
            cpu.PC = (buffer[32] | (buffer[33] << 8));
            
            if (buffer[34] != 0)
            {
                return false; //only 48k supported ATM
            }

            int counter = 32 + headerLength;

            //Version specific loads
            if (headerLength == 23)
            {
                //Load version 2

            }
            else
            {
                //Load version 3

            }
            
            //Load rest of the data
            while (counter < buffer.Length)
            {
                //Get length of data block
                int dataLength = buffer[counter] | (buffer[counter+1] << 8);
                counter += 2;
                //Get memory page (0 = 48krom, 4 = 0x8000 - 0xbfff, 
                //              5 = 0xc000 - 0xffff, 8 = 0x4000 - 0x7fff)
                
                int page = buffer[counter++];
                int memStart = 16384;   //By default assume we're loading above ROM

                if (page == 0)
                    memStart = 0;       //We're overwriting the ROM!
                else if (page == 4)
                    memStart = 0x8000;
                else if (page == 5)
                    memStart = 0xC000;
                else if (page == 8)
                    memStart = 0x4000;

                int screenAddr = GetPageAddress(10);

                if (dataLength == 0xffff)   //Uncompressed data
                {
                    for (int f = 0; f < 16384; f++) //16K of data (always)
                    {
                        PokeByte(screenAddr + f, buffer[counter++]);
                    }
                }
                else //Compressed data
                {
                    int dataCounter = 0;
                    int dataBlockOffset = counter;
                    while (dataCounter < dataLength)
                    {
                        int bite = buffer[counter++];

                        if (bite == 0xED)
                        {
                            if (buffer[counter++] == 0xED)
                            {
                                int dataSize = buffer[counter++];
                                int data = buffer[counter++];

                                //compressed data
                                for (int f = 0; f < dataSize; f++)
                                {
                                    PokeByte(memStart++, data);
                                }
                            }
                            else
                            {
                                PokeByte(memStart++, bite);
                                PokeByte(memStart++, buffer[counter - 1]);
                            }
                        }
                        else
                        {
                            PokeByte(memStart++, bite);
                        }

                        dataCounter = counter - dataBlockOffset;
                    }
                }
            }
            return true;

        }

        public bool LoadSNA(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open))
            {
                using (BinaryReader r = new BinaryReader(fs))
                {
                    int bytesToRead = (int)fs.Length;

                    byte[] buffer = new byte[bytesToRead];
                    int bytesRead = r.Read(buffer, 0, bytesToRead);

                    if (bytesRead == 0)
                        return false; //something bad happened!

                    cpu.I = buffer[0];
                    cpu._HL = buffer[1] | (buffer[2] << 8);
                    cpu._DE = buffer[3] | (buffer[4] << 8);
                    cpu._BC = buffer[5] | (buffer[6] << 8);
                    cpu._AF = buffer[7] | (buffer[8] << 8);

                    cpu.HL = buffer[9] | (buffer[10] << 8);
                    cpu.DE = buffer[11] | (buffer[12] << 8);
                    cpu.BC = buffer[13] | (buffer[14] << 8);
                    cpu.IY = buffer[15] | (buffer[16] << 8);
                    cpu.IX = buffer[17] | (buffer[18] << 8);

                    cpu.IFF1 = ((buffer[19] & 0x04) != 0);
                    cpu.R = buffer[20];
                    cpu.AF = buffer[21] | (buffer[22] << 8);
                    cpu.SP = buffer[23] | (buffer[24] << 8);
                    cpu.interruptMode = buffer[25];
                    borderColour = buffer[26];

                    int screenAddr = 16384;// GetPageAddress(10);

                    for (int f = 0; f < 49152 ; ++f)
                    {
                        PokeByte(screenAddr + f, (buffer[f + 27]));
                    }
                    cpu.PC = cpu.PeekWord(cpu.SP);
                    cpu.SP += 2;
                    return true;
                }
            }
        }
    }

    public class zx128 : Speccy.zxmachine
    {
        //private SoundManager beeper;

        int[] keyLine = {255, 255, 255, 255, 255, 255, 255, 255 };
        bool CapsLockOn = false;

        private double clockSpeed;
        private const int ScreenWidth = 256;
        private const int ScreenHeight = 192;

        private const int BorderHeight = 48;
        private const int BorderWidth = 48;

        private const int ScanLineWidth = BorderWidth * 2 + ScreenWidth;

        private const int CharRows = 24;
        private const int CharCols = 32;

        private const int DisplayStart = 16384;
        private const int DisplayLength = 6144;
        private int AttributeStart = 22528;
        private const int AttributeOffset = 6144;
        private const int AttributeLength = 768;

        private const int sat = 238;
   
        private static int[] AttrColors = {
                                                                 0x000000,            // Black
                                                                 0x0000CA,            // Red
                                                                 0xCA0000,            // Blue
                                                                 0xCA00A0,            // Magenta
                                                                 0x00CA00,            // Green
                                                                 0x00CACA,            // Yellow
                                                                 0xCACA00,            // Cyan
                                                                 0xC5C7C5,            // White
                                                                 0x000000,            // Bright Black
                                                                 0x0000FF,            // Bright Red
                                                                 0xFF0000,            // Bright Blue    
                                                                 0xFF00FF,            // Bright Magenta
                                                                 0x00FF00,            // Bright Green
                                                                 0x00FFFF,            // Bright Yellow
                                                                 0xFFFF00,            // Bright Cyan
                                                                 0xFFFFFF            // Bright White
                                             };


        private int borderColour = 7;   //Used by the screen update routine to output border colour
        private int oldBorderColour = 255;
        private int frameCount = 0;
        private int soundOut = 0;

        private bool pagingDisabled = false;    //depends on bit 5 of the value output to port (whose 1st and 15th bits are reset)
        private bool showShadowScreen = false;

        public zx128(IntPtr handle): base(handle)
        {
            cpu.interruptTime = 70908;
            cpu.interruptPeriod = 32;
            clockSpeed = 3.54690;
            cpu.Machine = this;
                    
            /*
            PagePointer[0] = 0;  //128 editor default!
            PagePointer[1] = 1;
            PagePointer[2] = 5;
            PagePointer[3] = 6;
            PagePointer[4] = 2;
            PagePointer[5] = 3;
            PagePointer[6] = 0; 
            PagePointer[7] = 1; 
            */


            PagePointer[0] = ROMpage[0];  //128 editor default!
            PagePointer[1] = ROMpage[1];
            PagePointer[2] = RAMpage[5];
            PagePointer[3] = RAMpage[6];
            PagePointer[4] = RAMpage[2];
            PagePointer[5] = RAMpage[3];
            PagePointer[6] = RAMpage[0];
            PagePointer[7] = RAMpage[1];

            ScreenBuffer = new int[(48 + 256 + 48) * 48 //48 lines of border
                                              +((48+256+48) * 192) //border + main + border of 192 lines
                                              + (48 + 256 + 48) * 48]; //48 lines of border
            keyBuffer = new bool[(int)keyCode.LAST];

            //beeper = new SoundManager(handle);
            
        }

        public override void Reset()
        {
            //PagePointer[0] = 0;
            //PagePointer[1] = 1;
            PagePointer[0] = ROMpage[0];
            PagePointer[1] = ROMpage[1];
            cpu.Reset();
            pagingDisabled = false;
           // if (!beeper.initialised && ENABLE_SOUND)
            //    beeper.Initialise();
        }

        public override void Run()
        {
             cpu.Process();
        }


        public override void Shutdown()
        {
            cpu.Shutdown();
        }

        public override int In(int port)
        {
            int result = 0xff;
            
            port = port & 0xffff;

            if ((port & 0x01) == 0)    //Even address, so get input
            {
                if ((port & 0x8000) == 0)
                        result &=  keyLine[7];
                        
                if ((port & 0x4000) == 0)
                        result &=  keyLine[6];
                    
                if ((port & 0x2000) == 0)
                        result &= keyLine[5];
                
                if ((port & 0x1000) == 0)
                         result &=  keyLine[4];

                if ((port & 0x800) == 0)
                         result &= keyLine[3];

                if ((port & 0x400) == 0)
                         result &= keyLine[2];
                         
                if ((port & 0x200) == 0)
                         result &= keyLine[1];

                if ((port & 0x100) == 0)
                         result &= keyLine[0];
               
            }
            return (result & 0xff);
        }

        public override void Out(int port, int val)
        {
            if ((port & 0x01) == 0)    //Even address, so update ULA
            {
                
                borderColour = val & 0x07;  //The LSB 3 bits of val hold the border colour

                if ((val & 0x010) != 0)
                    soundOut = int.MaxValue/20000;
                else
                    soundOut = 0;
            }

            //Are bits 1 and 15 reset?
            if ((port & 0x8002) == 0 && !pagingDisabled)
            {
                //Check bit 5 for paging disable
                if ((val & 0x20) != 0)
                {
                    pagingDisabled = true;
                    return;
                }

                //Bits 0 to 3 select the RAM page
               /* switch (val & 0x07)
                {
                    case 0:
                        PagePointer[6] = 0;
                        PagePointer[7] = 1;
                        break;
                    case 1:
                        PagePointer[6] = 2;
                        PagePointer[7] = 3;
                        break;
                    case 2:
                        PagePointer[6] = 4;
                        PagePointer[7] = 5;
                        break;
                    case 3:
                        PagePointer[6] = 6;
                        PagePointer[7] = 7;
                        break;
                    case 4:
                        PagePointer[6] = 8;
                        PagePointer[7] = 9;
                        break;
                    case 5:
                        PagePointer[6] = 10;
                        PagePointer[7] = 11;
                        break;
                    case 6:
                        PagePointer[6] = 12;
                        PagePointer[7] = 13;
                        break;
                    case 7:
                        PagePointer[6] = 14;
                        PagePointer[7] = 15;
                        break;
                }
                */
                if ((val & 0x08) != 0)
                    showShadowScreen = true;
                else
                    showShadowScreen = false;

                //ROM select
               /* if ((val & 0x10) != 0)
                {
                    PagePointer[0] = 2;
                    PagePointer[1] = 3;
                }
                else
                {
                    PagePointer[0] = 0;
                    PagePointer[1] = 1;
                }
                 */   
            }
        }

        public override void UpdateScreenBuffer()
        {
            uint lineIndex = 0;
            uint tempVar;
            //First draw the top lines of border
            if (borderColour != oldBorderColour)
            {
                while (lineIndex < (ScanLineWidth) * BorderHeight)
                {
                    ScreenBuffer[lineIndex++] = (AttrColors[borderColour]);
                }
            }
            else
            {
                lineIndex = (ScanLineWidth) * BorderHeight;
            }

            uint screenAddress = (uint)GetPageAddress(10);

            if (showShadowScreen)
                screenAddress = (uint)GetPageAddress(14);

            AttributeStart = (int)screenAddress + AttributeOffset;

            //Draw the middle half
            while (lineIndex < ((ScanLineWidth * BorderHeight + ScanLineWidth * ScreenHeight)))
            {
                //draw pixels of left border
                if (borderColour != oldBorderColour)
                {
                    for (int f = 0; f < BorderWidth; ++f)
                    {
                        ScreenBuffer[lineIndex++] = (AttrColors[borderColour]);
                    }
                }
                else
                {
                    lineIndex = (ScanLineWidth * BorderHeight + ScanLineWidth * ScreenHeight);
                }

                tempVar = lineIndex;

                uint addrH = screenAddress / 256;
                uint addrL = screenAddress % 256;

                uint pixelY = (addrH & 0x07);
                pixelY |= (addrL & (0xE0)) >> 2;
                pixelY |= (addrH & (0x18)) << 3;

                lineIndex = (16944 + (ScanLineWidth * pixelY));

                int attrIndex_Y = AttributeStart + (32 * (int)(pixelY / 8));

                //draw the main screen
                for (int f = 0; f < 32; ++f)
                {
                    //screenAddress += (uint)f;
                    addrL = screenAddress % 256;
                    uint pixelX = addrL & (0x1F);
                    //int attrVal = RAMpage[attrIndex_Y + pixelX];
                    int attrVal = PeekByte(attrIndex_Y + (int)pixelX);
                    //int pixelData = RAMpage[screenAddress++];
                    int pixelData = PeekByte((int)(screenAddress++));

                    int bright = 0;

                    if ((attrVal & 0x40) != 0)
                    {
                        bright = 8;

                    }

                    for (int g = 0; g < 8; ++g)
                    {
                       
                        if ((pixelData & 0x80) != 0)
                        {
                            //Plot ink
                            ScreenBuffer[lineIndex++] = AttrColors[(attrVal & 0x07) + bright];
                        }
                        else
                        {
                            //Plot Paper
                            ScreenBuffer[lineIndex++] = AttrColors[((attrVal >> 3) & 0x7) + bright];
                        }
                        
                        pixelData <<= 1;
                    }       
                }

                //draw pixels of right border
                if (borderColour != oldBorderColour)
                {
                    for (int f = 0; f < BorderWidth; ++f)
                    {
                        ScreenBuffer[lineIndex++] = AttrColors[borderColour];
                    }
                }
                else
                {
                    lineIndex += BorderWidth;
                }

            }

            tempVar = lineIndex;
            //Draw the bottom lines of border
            if (borderColour != oldBorderColour)
            {
                while (lineIndex < (tempVar + ScanLineWidth * BorderHeight))
                {
                    ScreenBuffer[lineIndex++] = (AttrColors[borderColour]);
                }
                oldBorderColour = borderColour;
            }
            else
            {
                lineIndex = tempVar + ScanLineWidth * BorderHeight;
            }

            //Update flash attributes
            frameCount++;
            if (frameCount >= 15)
            {
                InvertFlashAttributes();
                frameCount = 0;
            }

          }

        private void InvertFlashAttributes()
        {
            for (int f = AttributeStart; f < AttributeStart + AttributeLength; f++)
            {
                //int attrVal = RAMpage[f];
                int attrVal = PeekByte(f);
                if ((attrVal & 0x80) != 0)
                {
                    int ink = attrVal & 0x07;
                    int paper = (attrVal >> 3) & 0x07;
                    //RAMpage[f] = (attrVal & 0xC0) | (ink << 3) | paper;
                    PokeByte(f, (attrVal & 0xC0) | (ink << 3) | paper);
                }
            }
        }

        public override bool LoadROM(string path)
        {
            FileStream fs;
            
            String filename = path + "\\128k.rom";

            //Check if we can find the ROM file!
            try
            {
                fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            }
            catch
            {
               return false;
            }
            
            fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            using (BinaryReader r = new BinaryReader(fs))
            {
                //int bytesRead = ReadBytes(r, mem, 0, 16384);
                byte[] buffer = new byte[16384*2];
                int bytesRead = r.Read(buffer, 0, 16384 * 2);

                if (bytesRead == 0)
                    return false; //something bad happened!

                for (int f = 0; f < 16384 * 2; ++f)
                {
                    //ROMpage[f] = (buffer[f] + 256) & 0xff;
                }

            }
            fs.Close();
            return true;
        }

        public override void UpdateInput()
        {

            #region  Row 0: fefe - CAPS SHIFT, Z, X, C , V
            if (keyBuffer[(int)keyCode.SHIFT])
            {
               keyLine[0] = keyLine[0] & ~(0x1);
            }
            else 
            {
                keyLine[0] = keyLine[0] | (0x1);
            }

            if (keyBuffer[(int)keyCode.Z])
            {
               keyLine[0] = keyLine[0] & ~(0x02);
            }
            else 
            {
                keyLine[0] = keyLine[0] | (0x02);
            }

            if (keyBuffer[(int)keyCode.X])
            {
               keyLine[0] = keyLine[0] & ~(0x04);
            }
            else 
            {
                keyLine[0] = keyLine[0] | (0x04);
            }

            if (keyBuffer[(int)keyCode.C])
            {
                keyLine[0] = keyLine[0] & ~(0x08);
            }
            else 
            {
                keyLine[0] = keyLine[0] | (0x08);
            }

            if (keyBuffer[(int)keyCode.V])
            {
                keyLine[0] = keyLine[0] & ~(0x10);
            }
            else 
            {
                keyLine[0] = keyLine[0] | (0x10);
            }
            #endregion

            #region Row 1: fdfe - A, S, D, F, G
            if (keyBuffer[(int)keyCode.A])
            {
                keyLine[1] = keyLine[1] & ~(0x1);
            }
            else 
            {
                keyLine[1] = keyLine[1] | (0x1);
            }

            if (keyBuffer[(int)keyCode.S])
            {
                    keyLine[1] = keyLine[1] & ~(0x02);
            }
            else 
            {
                keyLine[1] = keyLine[1] | (0x02);
            }

            if (keyBuffer[(int)keyCode.D])
            {
                keyLine[1] = keyLine[1] & ~(0x04);
            }
            else 
            {
                keyLine[1] = keyLine[1] | (0x04);
            }

            if (keyBuffer[(int)keyCode.F])
            {
                keyLine[1] = keyLine[1] & ~(0x08);
            }
            else 
            {
                keyLine[1] = keyLine[1] | (0x08);
            }

            if (keyBuffer[(int)keyCode.G])
            {
                keyLine[1] = keyLine[1] & ~(0x10);
            }
            else 
            {
                keyLine[1] = keyLine[1] | (0x10);
            }
            #endregion

            #region Row 2: fbfe - Q, W, E, R, T
            if (keyBuffer[(int)keyCode.Q])
            {
                keyLine[2] = keyLine[2] & ~(0x1);
            }
            else 
            {
                keyLine[2] = keyLine[2] | (0x1);
            }

            if (keyBuffer[(int)keyCode.W])
            {
                keyLine[2] = keyLine[2] & ~(0x02);
            }
            else
            {
                keyLine[2] = keyLine[2] | (0x02);
            }

            if (keyBuffer[(int)keyCode.E])
            {
                keyLine[2] = keyLine[2] & ~(0x04);
            }
            else 
            {
                keyLine[2] = keyLine[2] | (0x04);
            }

            if (keyBuffer[(int)keyCode.R])
            {
                keyLine[2] = keyLine[2] & ~(0x08);
            }
            else 
            {
                keyLine[2] = keyLine[2] | (0x08);
            }

            if (keyBuffer[(int)keyCode.T])
            {
                keyLine[2] = keyLine[2] & ~(0x10);
            }
            else 
            {
                keyLine[2] = keyLine[2] |(0x10);
            }
            #endregion

            #region Row 3: f7fe - 1, 2, 3, 4, 5
            if (keyBuffer[(int)keyCode._1])
            {
                keyLine[3] = keyLine[3] & ~(0x1);
            }
            else 
            {
                keyLine[3] = keyLine[3] | (0x1);
            }

            if (keyBuffer[(int)keyCode._2])
            {
                keyLine[3] = keyLine[3] & ~(0x02);
            }
            else
            {
                keyLine[3] = keyLine[3] | (0x02);
            }

            if (keyBuffer[(int)keyCode._3])
            {
                keyLine[3] = keyLine[3] & ~(0x04);
            }
            else 
            {
                keyLine[3] = keyLine[3] | (0x04);
            }

            if (keyBuffer[(int)keyCode._4])
            {
                keyLine[3] = keyLine[3] & ~(0x08);
            }
            else 
            {
                keyLine[3] = keyLine[3] | (0x08);
            }

            if (keyBuffer[(int)keyCode._5])
            {
                keyLine[3] = keyLine[3] & ~(0x10);
            }
            else 
            {
                keyLine[3] = keyLine[3] | (0x10);
            }
            #endregion

            #region Row 4: effe - 0, 9, 8, 7, 6 
            if (keyBuffer[(int)keyCode._0])
            {
                    keyLine[4] = keyLine[4] & ~(0x1);
            }
            else
            {
                keyLine[4] = keyLine[4] | (0x1);
            }

            if (keyBuffer[(int)keyCode._9])
            {
                keyLine[4] = keyLine[4] & ~(0x02);
            }
            else
            {
                keyLine[4] = keyLine[4] | (0x02);
            }

            if (keyBuffer[(int)keyCode._8])
            {
                keyLine[4] = keyLine[4] & ~(0x04);
            }
            else 
            {
                keyLine[4] = keyLine[4] | (0x04);
            }

            if (keyBuffer[(int)keyCode._7])
            {
                 keyLine[4] = keyLine[4] & ~(0x08);
            }
            else 
            {
                keyLine[4] = keyLine[4] | (0x08);
            }

            if (keyBuffer[(int)keyCode._6])
            {
                 keyLine[4] = keyLine[4] & ~(0x10);
            }
            else
            {
                keyLine[4] = keyLine[4] | (0x10);
            }
            #endregion

            #region Row 5: dffe - P, O, I, U, Y 
            if (keyBuffer[(int)keyCode.P])
            {
                keyLine[5] = keyLine[5] & ~(0x1);
            }
            else 
            {
                keyLine[5] = keyLine[5] | (0x1);
            }

            if (keyBuffer[(int)keyCode.O])
            {
                keyLine[5] = keyLine[5] & ~(0x02);
            }
            else
            {
                keyLine[5] = keyLine[5] | (0x02);
            }

            if (keyBuffer[(int)keyCode.I])
            {
                keyLine[5] = keyLine[5] & ~(0x04);
            }
            else
            {
                keyLine[5] = keyLine[5] | (0x04);
            }

            if (keyBuffer[(int)keyCode.U])
            {
                keyLine[5] = keyLine[5] & ~(0x08);
            }
            else
            {
                keyLine[5] = keyLine[5] | (0x08);
            }

            if (keyBuffer[(int)keyCode.Y])
            {
                keyLine[5] = keyLine[5] & ~(0x10);
            }
            else
            {
                keyLine[5] = keyLine[5] | (0x10);
            }
            #endregion

            #region Row 6: bffe - ENTER, L, K, J, H
            if (keyBuffer[(int)keyCode.ENTER])
            {
               // cpu.loggingEnabled = true;
                keyLine[6] = keyLine[6] & ~(0x1);
            }
            else
            {
                keyLine[6] = keyLine[6] | (0x1);
            }

            if (keyBuffer[(int)keyCode.L])
            {
                keyLine[6] = keyLine[6] & ~(0x02);
               
            }
            else
            {
                keyLine[6] = keyLine[6] | (0x02);
            }

            if (keyBuffer[(int)keyCode.K])
            {
                keyLine[6] = keyLine[6] & ~(0x04);
            }
            else
            {
                keyLine[6] = keyLine[6] | (0x04);
            }

            if (keyBuffer[(int)keyCode.J])
            {
                 keyLine[6] = keyLine[6] & ~(0x08);
            }
            else
            {
                keyLine[6] = keyLine[6] | (0x08);
            }

            if (keyBuffer[(int)keyCode.H])
            {
                keyLine[6] = keyLine[6] & ~(0x10);
            }
            else
            {
                keyLine[6] = keyLine[6] | (0x10);
            }
            #endregion

            #region Row 7: 7ffe - SPACE, SYMBOL SHIFT, M, N, B
            if (keyBuffer[(int)keyCode.SPACE])
            {
                keyLine[7] = keyLine[7] & ~(0x1);
            }
            else
            {
                keyLine[7] = keyLine[7] | (0x1);
            }

            if (keyBuffer[(int)keyCode.CTRL])
            {
                keyLine[7] = keyLine[7] & ~(0x02);
            }
            else
            {
                keyLine[7] = keyLine[7] | (0x02);
            }

            if (keyBuffer[(int)keyCode.M])
            {
                keyLine[7] = keyLine[7] & ~(0x04);
            }
            else
            {
                keyLine[7] = keyLine[7] | (0x04);
            }

            if (keyBuffer[(int)keyCode.N])
            {
                keyLine[7] = keyLine[7] & ~(0x08);
            }
            else
            {
                keyLine[7] = keyLine[7] | (0x08);
            }

            if (keyBuffer[(int)keyCode.B])
            {
                keyLine[7] = keyLine[7] & ~(0x10);
            }
            else
            {
                keyLine[7] = keyLine[7] | (0x010);
            }
            #endregion

            #region Misc utility key functions
            //Check for caps lock key
            if (keyBuffer[(int)keyCode.CAPS])
            {
                CapsLockOn = !CapsLockOn;
                keyBuffer[(int)keyCode.CAPS] = false;
            }

            if (CapsLockOn)
            {
                keyLine[0] = keyLine[0] & ~(0x1);
            }
               
            //Check if backspace key has been pressed (Caps Shift + 0 equivalent)
            if (keyBuffer[(int)keyCode.BACK])
            {
                keyLine[0] = keyLine[0] & ~(0x1);
                keyLine[4] = keyLine[0] & ~(0x1);
            }

            //Check if left cursor key has been pressed (Caps Shift + 5)
            if (keyBuffer[(int)keyCode.LEFT])
            {
                keyLine[0] = keyLine[0] & ~(0x1);
                keyLine[3] = keyLine[3] & ~(0x10);
            }

            //Check if right cursor key has been pressed (Caps Shift + 8)
            if (keyBuffer[(int)keyCode.RIGHT])
            {
                keyLine[0] = keyLine[0] & ~(0x1);
                keyLine[4] = keyLine[4] & ~(0x04);
            }

            //Check if up cursor key has been pressed (Caps Shift + 7)
            if (keyBuffer[(int)keyCode.UP])
            {
                keyLine[0] = keyLine[0] & ~(0x1);
                keyLine[4] = keyLine[4] & ~(0x08);
            }

            //Check if down cursor key has been pressed (Caps Shift + 6)
            if (keyBuffer[(int)keyCode.DOWN])
            {
                keyLine[0] = keyLine[0] & ~(0x1);
                keyLine[4] = keyLine[4] & ~(0x08);
            }
            #endregion

        }

        public override void OutSound()
        {
           // beeper.AddSample(soundOut);
        }

        public override void UpdateAudio()
        {
           // beeper.Update();
        }

        public override void LoadSnapshot(string filename)
        {
            if (filename.EndsWith(".sna"))
            {
                LoadSNA(filename);
            }
            else
                if (filename.EndsWith(".z80"))
                {
                    LoadZ80(filename);
                }
                else
                    return;
        }

        public override void LoadTape(string filename)
        {
            if (filename.EndsWith(".tap"))
            {
                LoadTAP(filename);
            }
            else
                if (filename.EndsWith(".tzx"))
                {
                    //LoadTZX(filename);
                }
                else
                    return;
        }

        public bool LoadTAP(string filename)
        {
            return true;
        }

        public override int PeekByte(int addr)
        {
            int page = (addr & 65535) >> 13;

            if (page < 2)
            {
                // return (ROMpage[(PagePointer[page] << 13) + (addr % 8192)]); 
            }
           // else
                //return (RAMpage[(PagePointer[page] << 13) + (addr % 8192)]); 
                return 0;
         }

        public override void PokeByte(int addr, int b)
        {
            int page = (addr & 65535) >> 13;

            if (page < 2)
            {
                //if (!isROMprotected)
                 //   ROMpage[(PagePointer[page] << 13) + (addr % 8192)] = b & 0xff;
            }
           // else
                //RAMpage[(PagePointer[page] << 13) + (addr % 8192)] = b & 0xff;
            
        }

        public bool LoadZ80(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open))
            {
                using (BinaryReader r = new BinaryReader(fs))
                {
                    int bytesToRead = (int)fs.Length;

                    byte[] buffer = new byte[bytesToRead];
                    int bytesRead = r.Read(buffer, 0, bytesToRead);

                    if (bytesRead == 0)
                        return false; //something bad happened!

                    cpu.A = buffer[0];
                    cpu.F = buffer[1];
                    cpu.BC = (buffer[2] | (buffer[3] << 8));
                    cpu.HL = (buffer[4] | (buffer[5] << 8));
                    cpu.PC = (buffer[6] | (buffer[7] << 8));
                    cpu.SP = (buffer[8] | (buffer[9] << 8));
                    cpu.I = buffer[10];
                    cpu.R = buffer[11];


                    byte byte12 = buffer[12];
                    if (byte12 == 255)
                        byte12 = 1;

                    cpu.R = cpu.R | ((byte12 & 0x01) << 7);
                    borderColour = (byte12 >> 1) & 0x07;
                    bool isCompressed = ((byte12 & 20) != 0);

                    cpu.DE = (buffer[13] | (buffer[14] << 8));
                    cpu._BC = (buffer[15] | (buffer[16] << 8));
                    cpu._DE = (buffer[17] | (buffer[18] << 8));
                    cpu._HL = (buffer[19] | (buffer[20] << 8));
                    cpu._AF = ((buffer[21] << 8) | buffer[22]);

                    cpu.IY = (buffer[23] | (buffer[24] << 8));
                    cpu.IX = (buffer[25] | (buffer[26] << 8));

                    cpu.IFF1 = (buffer[27] != 0);
                    cpu.IFF2 = (buffer[28] != 0);

                    byte byte29 = buffer[29];

                    cpu.interruptMode = (byte29 & 0x3);

                    if (cpu.PC == 0)
                    {
                        if (LoadZ80V23(buffer)) //Load version 2 or 3 of z80
                            return true;
                        else
                            return false;
                    }

                    if (!isCompressed)
                    {
                        for (int f = 0; f < 49152; ++f)
                        {
                            //    RAMpage[16384 + f] = (buffer[f + 30]);
                               PokeByte(16384 + f, (buffer[f + 30]));
                        }
                    }
                    else
                    {
                        bool done = false;
                        int byteCounter = 30;
                        int memCounter = 0;

                        while (!done)
                        {
                            int bite = buffer[byteCounter++];
                            if (bite == 0)
                            {
                                //check if this is the end marker
                                if (buffer[byteCounter++] == 0xED)
                                {
                                    if (buffer[byteCounter++] == 0xED)
                                    {
                                        if (buffer[byteCounter++] == 0)
                                        {
                                            done = true;
                                            continue;
                                        }
                                    }
                                }
                            }
                            else
                                if (bite == 0xED)
                                {
                                    if (buffer[byteCounter++] == 0xED)
                                    {
                                        int dataLength = buffer[byteCounter++];
                                        int data = buffer[byteCounter++];

                                        //compressed data
                                        for (int f = 0; f < dataLength; f++)
                                        {
                                            //RAMpage[16384 + (memCounter++)] = data;
                                            PokeByte(16384 + (memCounter++), data);
                                        }
                                    }
                                }

                            //RAMpage[16384 + (memCounter++)] = buffer[byteCounter++];
                            PokeByte(16384 + (memCounter++), buffer[byteCounter++]);
                        }
                    }

                }
                                    
            }
            return true;
        }

        public bool LoadZ80V23(byte[] buffer)
        {
            //First read-in the bytes common to version 2 & 3
            int headerLength = buffer[30];
            cpu.PC = (buffer[32] | (buffer[33] << 8));
            
            if (buffer[34] != 0)
            {
                return false; //only 48k supported ATM
            }

            int counter = 32 + headerLength;

            //Version specific loads
            if (headerLength == 23)
            {
                //Load version 2

            }
            else
            {
                //Load version 3

            }
            
            //Load rest of the data
            while (counter < buffer.Length)
            {
                //Get length of data block
                int dataLength = buffer[counter] | (buffer[counter+1] << 8);
                counter += 2;
                //Get memory page (0 = 48krom, 4 = 0x8000 - 0xbfff, 
                //              5 = 0xc000 - 0xffff, 8 = 0x4000 - 0x7fff)
                
                int page = buffer[counter++];
                int memStart = 16384;   //By default assume we're loading above ROM

                if (page == 0)
                    memStart = 0;       //We're overwriting the ROM!
                else if (page == 4)
                    memStart = 0x8000;
                else if (page == 5)
                    memStart = 0xC000;
                else if (page == 8)
                    memStart = 0x4000;

                if (dataLength == 0xffff)   //Uncompressed data
                {
                    for (int f = 0; f < 16384; f++) //16K of data (always)
                    {
                        //RAMpage[memStart + f] = buffer[counter++];
                    }
                }
                else //Compressed data
                {
                    int dataCounter = 0;
                    int dataBlockOffset = counter;
                    while (dataCounter < dataLength)
                    {
                        int bite = buffer[counter++];

                        if (bite == 0xED)
                        {
                            if (buffer[counter++] == 0xED)
                            {
                                int dataSize = buffer[counter++];
                                int data = buffer[counter++];

                                //compressed data
                                for (int f = 0; f < dataSize; f++)
                                {
                                    //RAMpage[memStart++] = data;
                                }
                            }
                            else
                            {
                               // RAMpage[memStart++] = bite;
                               // RAMpage[memStart++] = buffer[counter - 1];
                            }
                        }
                        else
                        {
                            //RAMpage[memStart++] = bite;
                        }

                        dataCounter = counter - dataBlockOffset;
                    }
                }
            }
            return true;

        }

        public bool LoadSNA(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open))
            {
                using (BinaryReader r = new BinaryReader(fs))
                {
                    int bytesToRead = (int)fs.Length;

                    //if 48k snapshot, switch to 48k mode
                    if (bytesToRead == 49179)
                    {
                     /*   PagePointer[0] = 2;
                        PagePointer[1] = 3;
                        PagePointer[2] = 5;
                        PagePointer[3] = 6;
                        PagePointer[4] = 2;
                        PagePointer[5] = 3;
                        PagePointer[6] = 0;
                        PagePointer[7] = 1; 
                    */}

                    byte[] buffer = new byte[bytesToRead];
                    int bytesRead = r.Read(buffer, 0, bytesToRead);

                    if (bytesRead == 0)
                        return false; //something bad happened!

                    cpu.I = buffer[0];
                    cpu._HL = buffer[1] | (buffer[2] << 8);
                    cpu._DE = buffer[3] | (buffer[4] << 8);
                    cpu._BC = buffer[5] | (buffer[6] << 8);
                    cpu._AF = buffer[7] | (buffer[8] << 8);

                    cpu.HL = buffer[9] | (buffer[10] << 8);
                    cpu.DE = buffer[11] | (buffer[12] << 8);
                    cpu.BC = buffer[13] | (buffer[14] << 8);
                    cpu.IY = buffer[15] | (buffer[16] << 8);
                    cpu.IX = buffer[17] | (buffer[18] << 8);

                    cpu.IFF1 = ((buffer[19] & 0x04) != 0);
                    cpu.R = buffer[20];
                    cpu.AF = buffer[21] | (buffer[22] << 8);
                    cpu.SP = buffer[23] | (buffer[24] << 8);
                    cpu.interruptMode = buffer[25];
                    borderColour = buffer[26];

                    for (int f = 0; f < 49152 ; ++f)
                    {
                        //RAMpage[16384 + f] = (buffer[f + 27]);
                        PokeByte(16384 + f, (buffer[f + 27]));
                    }
                    cpu.PC = cpu.PeekWord(cpu.SP);
                    cpu.SP += 2;
                    return true;
                }
            }
        }
    }
}
