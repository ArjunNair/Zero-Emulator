using System;
//using System.Collections.Generic;
//using System.Text;
using System.IO;
using ZiggySound;

namespace Speccy
{
    public class zx128 : Speccy.zxmachine
    {
        //private SoundManager beeper;
        private AYSound aySound = new AYSound();
        private int ayTStates = 0;
        private const int AY_SAMPLE_RATE = 16;
        private int lastSoundOut = 0;
        private float averagedSound = 0;
        private float ayLastSoundOut;
        private int soundCounter = 0;
        //private int lastFEOut = 0;

        //Port 0xfe constants
        private const int BORDER_BIT = 0x07;
        private const int EAR_BIT = 0x10;
        private const int MIC_BIT = 0x08;
        private const int TAPE_BIT = 0x40;

        public zx128(IntPtr handle, int lateTimingModel)
            : base(handle, lateTimingModel)
        {
            FrameLength = 70908;
            InterruptPeriod = 48;
            clockSpeed = 3.54690;
            cpu.Machine = this;
            
            contentionTable = new int[70930];
            floatingBusTable = new short[70930];
            for (int f = 0; f < 70930; f++)
                floatingBusTable[f] = -1;

            CharRows = 24;
            CharCols = 32;
            ScreenWidth = 256;
            ScreenHeight = 192;
            BorderTopHeight = 48;
            BorderBottomHeight = 56;
            BorderWidth = 48;
            DisplayStart = 16384;
            DisplayLength = 6144;
            AttributeStart = 22528;
            AttributeLength = 768;
            borderColour = 7;
            ScanLineWidth = BorderWidth * 2 + ScreenWidth;

            TstatesPerScanline = 228;
            TstateAtTop = BorderTopHeight * TstatesPerScanline;
            TstateAtBottom = BorderBottomHeight * TstatesPerScanline;
            tstateToDisp = new short[FrameLength];

            contentionStartPeriod = 14361 + LateTiming;
            contentionEndPeriod = contentionStartPeriod + (ScreenHeight * TstatesPerScanline); //57324 + LateTiming;

            PagePointer[0] = ROMpage[0];  //128 editor default!
            PagePointer[1] = ROMpage[1]; 
            PagePointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];  //Bank 5
            PagePointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];  //Bank 5
            PagePointer[4] = RAMpage[(int)RAM_BANK.TWO_1];   //Bank 2
            PagePointer[5] = RAMpage[(int)RAM_BANK.TWO_2];   //Bank 2
            PagePointer[6] = RAMpage[(int)RAM_BANK.ZERO_1];   //Bank 0
            PagePointer[7] = RAMpage[(int)RAM_BANK.ZERO_2];   //Bank 0

            contendedBankPagedIn = false;
            RomInPage1 = "128k ROM";
            BankInPage4 = "Bank 0";
            contendedBankPagedIn = false;
            pagingDisabled = false;
            showShadowScreen = false;

            Random rand = new Random();
            //Fill memory with random stuff to simulate hard reset
            for (int i = DisplayStart; i < 65535; ++i)
                PokeByteNoContend(i, rand.Next(255));

            ScreenBuffer = new int[(BorderWidth + ScreenWidth + BorderWidth) * BorderTopHeight //48 lines of border
                                              + ((BorderWidth + ScreenWidth + BorderWidth) * ScreenHeight) //border + main + border of 192 lines
                                              + (BorderWidth + ScreenWidth + BorderWidth) * BorderBottomHeight]; //56 lines of border

            keyBuffer = new bool[(int)keyCode.LAST];

            screen = GetPageData(5); //Bank 5 is a copy of the screen

            attr = new short[DisplayLength];  //6144 bytes of display memory will be mapped
            screenByteCtr = DisplayStart;
            ULAByteCtr = 0;
            
            ActualULAStart = 14366 - 24 - (TstatesPerScanline * BorderTopHeight);
            lastTState = ActualULAStart;
            BuildAttributeMap();

            BuildContentionTable();
            aySound.Reset();
            beeper = new SoundManager(handle, 32, 2, 44100);
            beeper.Play();
        }

        public override void Reset()
        {
            //PagePointer[0] = 0;
            //PagePointer[1] = 1;
            PagePointer[0] = ROMpage[0];
            PagePointer[1] = ROMpage[1];
            RomInPage1 = "128k ROM";
            cpu.Reset();
            pagingDisabled = false;
            aySound.Reset();
            // if (!beeper.initialised && ENABLE_SOUND)
            //    beeper.Initialise();
        }

        public override void BuildContentionTable()
        {
            int t = contentionStartPeriod;
            while (t < contentionEndPeriod)
            {
                //for 128 t-states
                for (int i = 0; i < 128; i += 8)
                {
                    for (int g = 6; g >= 0; g--)
                    {
                        contentionTable[t++] = g;
                    }
                    contentionTable[t++] = 0;
                }
                t += (TstatesPerScanline - 128);
            }

            //build top half of tstateToDisp table
            //vertical retrace period
            for (t = 0; t < ActualULAStart; t++)
                tstateToDisp[t] = 0;

            //next 48 are actual border
            while (t < ActualULAStart + (TstateAtTop))
            {
                for (int g = 0; g < 176; g++)
                    tstateToDisp[t++] = 1;

                for (int g = 176; g < TstatesPerScanline; g++)
                    tstateToDisp[t++] = 0;
            }

            //build middle half
            int _x = 0;
            int _y = 0;
            int scrval = 2;
            while (t < ActualULAStart + (TstateAtTop) + (ScreenHeight * TstatesPerScanline))
            {
                for (int g = 0; g < 24; g++)
                    tstateToDisp[t++] = 1;


                for (int g = 24; g < 24 + 128; g++)
                {
                    //Map screenaddr to tstate
                    if (g % 4 == 0)
                    {
                        scrval = (((((_y & 0xc0) >> 3) | (_y & 0x07) | (0x40)) << 8)) | (((_x >> 3) & 0x1f) | ((_y & 0x38) << 2));
                        _x += 8;
                    }
                    tstateToDisp[t++] = (short)scrval;

                }
                _y++;

                for (int g = 24 + 128; g < 24 + 128 + 24; g++)
                    tstateToDisp[t++] = 1;

                for (int g = 24 + 128 + 24; g < 24 + 128 + 24 + 52; g++)
                    tstateToDisp[t++] = 0;
            }

            int h = contentionStartPeriod + 3;
            while (h < contentionEndPeriod + 3)
            {
                for (int j = 0; j < 128; j += 8)
                {
                    floatingBusTable[h] = tstateToDisp[h + 2];
                    floatingBusTable[h + 1] = attr[(tstateToDisp[h + 2] - 16384)];
                    floatingBusTable[h + 2] = tstateToDisp[h + 2 + 4];
                    floatingBusTable[h + 3] = attr[(tstateToDisp[h + 2 + 4] - 16384)];
                    h += 8;
                }
                h += TstatesPerScanline - 128;
            }

            //build bottom half
            while (t < ActualULAStart + (TstateAtTop) + (ScreenHeight * TstatesPerScanline) + (TstateAtBottom))
            {
                for (int g = 0; g < 176; g++)
                    tstateToDisp[t++] = 1;

                for (int g = 176; g < TstatesPerScanline; g++)
                    tstateToDisp[t++] = 0;
            }
        }

        public override void BuildAttributeMap()
        {
            int start = DisplayStart;

            for (int f = 0; f < DisplayLength; f++, start++)
            {
                int addrH = start >> 8; //div by 256
                int addrL = start % 256;

                int pixelY = (addrH & 0x07);
                pixelY |= (addrL & (0xE0)) >> 2;
                pixelY |= (addrH & (0x18)) << 3;

                int attrIndex_Y = AttributeStart + ((pixelY >> 3) << 5);// pixel/8 * 32

                addrL = start % 256;
                int pixelX = addrL & (0x1F);

                attr[f] = (short)(attrIndex_Y + pixelX);
            }
        }

        public override int PeekByteNoContend(int addr)
        {
            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;
            return PagePointer[page][offset];
        }

        public override void PokeByteNoContend(int addr, int b)
        {
            //This call is to mainly raise a memory change event for the debugger
            base.PokeByte(addr, b);

            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;

            if (page < 2 && isROMprotected)
                return;

            PagePointer[page][offset] = b & 0xff;
        }

        public override int PeekWordNoContend(int addr)
        {
            return (PeekByteNoContend(addr) + (PeekByteNoContend((addr + 1) & 0xffff) << 8));

        }

        public override bool IsContended(int addr)
        {
            //int addr_hb = (addr & 0xff00) >> 8;
            addr = addr & 0xc000;

            //Low port contention
            if (addr == 0x4000)
                return true;

            //High port contention
            if ((addr == 0xc000) && contendedBankPagedIn)
                return true;

            return false;
        }

        public override void Contend(int _addr, int _time, int _count)
        {
            bool addrIsContended = IsContended(_addr);
            for (int f = 0; f < _count; f++)
            {
                if (addrIsContended)
                {
                    cpu.totalTStates += contentionTable[cpu.totalTStates];
                }
                cpu.totalTStates += _time;
            }
        }

        public override int In(int port)
        {
            int result = 0xff;

            bool portIsContended = IsContended(port);
            bool lowBitReset = (port & 0x01) == 0;

            //T1
            Contend(port, 1, 1);

            if (lowBitReset)    //Even address, so get input
            {
                Contend(port, 2, 1);

                if ((port & 0x8000) == 0)
                    result &= keyLine[7];

                if ((port & 0x4000) == 0)
                    result &= keyLine[6];

                if ((port & 0x2000) == 0)
                    result &= keyLine[5];

                if ((port & 0x1000) == 0)
                    result &= keyLine[4];

                if ((port & 0x800) == 0)
                    result &= keyLine[3];

                if ((port & 0x400) == 0)
                    result &= keyLine[2];

                if ((port & 0x200) == 0)
                    result &= keyLine[1];   

                if ((port & 0x100) == 0)
                    result &= keyLine[0];

                result = result & 0x1f; //mask out lower 4 bits
                result = result | 0xa0; //set bit 5 & 7 to 1

                if (tapeIsPlaying)
                {
                    if (tapeBit == 0)
                    {
                        result &= ~(TAPE_BIT);    //reset is EAR ON
                    }
                    else
                    {
                        result |= (TAPE_BIT); //set is EAR Off
                    }
                }
                else
                    if ((lastFEOut & 0x10) == 0)
                    {
                        result &= ~(0x40);
                    }
                    else
                        result |= 0x40;
            }
            else
            if ((port & 0xc002) == 0xc000) //AY register activate
            {
                cpu.totalTStates += 2;
                result = aySound.PortRead();
            }
            else //return floating bus (also handles port 0x7ffd)
            {
                Contend(port, 1, 2);

                int _tstates = cpu.totalTStates;

                //if we're on the top or bottom border return 0xff
                if ((_tstates < contentionStartPeriod) || (_tstates > contentionEndPeriod ))
                    result = 0xff;
                else
                {
                    if (floatingBusTable[_tstates] < 0)
                        result = 0xff;
                    else
                        result = PeekByteNoContend(floatingBusTable[_tstates]);
                }
               
                if ((port & 0x8002) == 0) //Memory paging
                {
                    int tempTStates = cpu.totalTStates;
                    Out_7ffd(port, result);
                    cpu.totalTStates = tempTStates;
                }
            }
            cpu.totalTStates = cpu.totalTStates + 1;
            return (result & 0xff);
        }

        private void Out_7ffd(int port, int val)
        {
            Contend(port, 1, 3);

            //UpdateScreenBuffer(cpu.totalTStates);
            if ((val & 0x08) != 0)
            {
                if (!showShadowScreen)
                    UpdateScreenBuffer(cpu.totalTStates);

                showShadowScreen = true;
                screen = GetPageData(7); //Bank 7
            }
            else
            {
                if (showShadowScreen)
                    UpdateScreenBuffer(cpu.totalTStates);

                showShadowScreen = false;
                screen = GetPageData(5); //Bank 5
            }

            if (!pagingDisabled)
            {
                //Bits 0 to 2 select the RAM page
                switch (val & 0x07)
                {
                    case 0: //Bank 0
                        PagePointer[6] = RAMpage[(int)RAM_BANK.ZERO_1];
                        PagePointer[7] = RAMpage[(int)RAM_BANK.ZERO_2];
                        BankInPage4 = "Bank 0";
                        contendedBankPagedIn = false;
                        break;
                    case 1: //Bank 1
                        PagePointer[6] = RAMpage[(int)RAM_BANK.ONE_1];
                        PagePointer[7] = RAMpage[(int)RAM_BANK.ONE_2];
                        BankInPage4 = "Bank 1";
                        contendedBankPagedIn = true;
                        break;
                    case 2: //Bank 2
                        PagePointer[6] = RAMpage[(int)RAM_BANK.TWO_1];
                        PagePointer[7] = RAMpage[(int)RAM_BANK.TWO_2];
                        BankInPage4 = "Bank 2";
                        contendedBankPagedIn = false;
                        break;
                    case 3: //Bank 3
                        PagePointer[6] = RAMpage[(int)RAM_BANK.THREE_1];
                        PagePointer[7] = RAMpage[(int)RAM_BANK.THREE_2];
                        BankInPage4 = "Bank 3";
                        contendedBankPagedIn = true;
                        break;
                    case 4: //Bank 4
                        PagePointer[6] = RAMpage[(int)RAM_BANK.FOUR_1];
                        PagePointer[7] = RAMpage[(int)RAM_BANK.FOUR_2];
                        BankInPage4 = "Bank 4";
                        contendedBankPagedIn = false;
                        break;
                    case 5: //Bank 5
                        PagePointer[6] = RAMpage[(int)RAM_BANK.FIVE_1];
                        PagePointer[7] = RAMpage[(int)RAM_BANK.FIVE_2];
                        BankInPage4 = "Bank 5";
                        contendedBankPagedIn = true;
                        break;
                    case 6: //Bank 6
                        PagePointer[6] = RAMpage[(int)RAM_BANK.SIX_1];
                        PagePointer[7] = RAMpage[(int)RAM_BANK.SIX_2];
                        BankInPage4 = "Bank 6";
                        contendedBankPagedIn = false;
                        break;
                    case 7: //Bank 7
                        PagePointer[6] = RAMpage[(int)RAM_BANK.SEVEN_1];
                        PagePointer[7] = RAMpage[(int)RAM_BANK.SEVEN_2];
                        BankInPage4 = "Bank 7";
                        contendedBankPagedIn = true;
                        break;
                }

                //ROM select
                if ((val & 0x10) != 0)
                {
                    //48k basic
                    PagePointer[0] = ROMpage[2];
                    PagePointer[1] = ROMpage[3];
                    RomInPage1 = "48k ROM";
                }
                else
                {
                    //128k basic
                    PagePointer[0] = ROMpage[0];
                    PagePointer[1] = ROMpage[1];
                    RomInPage1 = "128k ROM";
                }
            }

            //Check bit 5 for paging disable
            if ((val & 0x20) != 0)
            {
                pagingDisabled = true;
            }
        }

        public override void Out(int port, int val)
        {
            bool portIsContended = IsContended(port);
            bool lowBitReset = (port & 0x01) == 0;

            Contend(port, 1, 1);        //N:1 || C:1

            int tempTStates = cpu.totalTStates;
            int highestTStates = cpu.totalTStates;

            //ULA activate
            if (lowBitReset)    //Even address, so update ULA
            {
                lastFEOut = val;
                cpu.totalTStates += contentionTable[cpu.totalTStates];
                UpdateScreenBuffer(cpu.totalTStates);

                borderColour = val & BORDER_BIT;  //The LSB 3 bits of val hold the border colour
                int beepVal = val & (EAR_BIT + MIC_BIT);

                if (!tapeIsPlaying)
                {
                    if (beepVal != lastSoundOut)
                    {
                        if ((beepVal & EAR_BIT) == 0)
                        {
                            soundOut = 0.0f;
                        }
                        else
                        {
                            soundOut = 0.5f;
                        }
                        lastSoundOut = beepVal;
                    }
                }
                cpu.totalTStates += 3;

                if (cpu.totalTStates > highestTStates)
                    highestTStates = cpu.totalTStates;

                cpu.totalTStates = tempTStates;
            }

            //AY register activate
            if ((port & 0xc002) == 0xc000)
            {
                //tempTStates = cpu.totalTStates;
                aySound.SelectedRegister = val & 0x0F; 
                cpu.totalTStates += 3;

                if (cpu.totalTStates > highestTStates)
                    highestTStates = cpu.totalTStates;

                cpu.totalTStates = tempTStates;
            }
           
            //AY data
            if ((port & 0xc002) == 0x8000)
            {
                //tempTStates = cpu.totalTStates;
                aySound.PortWrite(val);
                cpu.totalTStates += 3;

                if (cpu.totalTStates > highestTStates)
                    highestTStates = cpu.totalTStates;

                cpu.totalTStates = tempTStates;
            }
            
            //Memory paging activate
            if ((port & 0x8002) == 0) //Are bits 1 and 15 reset?
            {
                //tempTStates = cpu.totalTStates;

                Out_7ffd(port, val);

                if (cpu.totalTStates > highestTStates)
                    highestTStates = cpu.totalTStates;

                cpu.totalTStates = tempTStates;
            }

            //tempTStates = cpu.totalTStates;

            if (portIsContended && !lowBitReset)
            {
                Contend(port, 1, 3);
            }
            else
            {
                cpu.totalTStates += 3;
            }

            if (cpu.totalTStates > highestTStates)
                highestTStates = cpu.totalTStates;

            cpu.totalTStates = highestTStates;
        }

        public override void UpdateScreenBuffer(int _tstates)
        {
            if (_tstates < ActualULAStart)
            {
                return;
            }
            else if (_tstates >= FrameLength)
            {
                _tstates = FrameLength - 1;
                //Since we're updating the entire screen here, we don't need to re-paint
                //it again in the cpu.process loop on framelength overflow.
                needsPaint = true;
            }

            elapsedTStates = _tstates + 1 - lastTState;

            int numBytes = (elapsedTStates >> 2) + ((elapsedTStates % 4) > 0 ? 1 : 0);
            {
                for (int i = 0; i < numBytes; i++)
                {
                    if (tstateToDisp[lastTState] > 1)
                    {
                        screenByteCtr = tstateToDisp[lastTState];
                        
                        int pixelData = screen[screenByteCtr - 16384];

                        lastPixelValue = pixelData;
                       // lastAttrValue = PeekByteNoContend((!showShadowScreen ? attr[screenByteCtr - 16384] : attr[screenByteCtr - 16384] * 2));
                        lastAttrValue = screen[attr[screenByteCtr - 16384] - 16384];

                        int bright = (lastAttrValue & 0x40) >> 3;
                        bool flashBitOn = (lastAttrValue & 0x80) != 0;
                        int ink = AttrColors[(lastAttrValue & 0x07) + bright];
                        int paper = AttrColors[((lastAttrValue >> 3) & 0x7) + bright];

                        if (flashOn && flashBitOn) //swap paper and ink when flash is on
                        {
                            int temp = ink;
                            ink = paper;
                            paper = temp;
                        }

                        if (ULAPlusEnabled)
                        {
                            ink = ULAPlusColours[((flashBitOn ? 1 : 0) * 2 + bright) * 16 + (lastAttrValue & 0x70)];
                            paper = ULAPlusColours[((flashBitOn ? 1 : 0) * 2 + bright) * 16 + ((lastAttrValue >> 3) & 0x7) + 8];
                        }

                        for (int a = 0; a < 8; ++a)
                        {
                            if ((pixelData & 0x80) != 0)
                            {
                                ScreenBuffer[ULAByteCtr++] = ink;
                            }
                            else
                            {
                                ScreenBuffer[ULAByteCtr++] = paper;
                            }
                            pixelData <<= 1;
                        }
                    }
                    else if (tstateToDisp[lastTState] == 1)
                    {
                        for (int g = 0; g < 8; g++)
                            ScreenBuffer[ULAByteCtr++] = AttrColors[borderColour];
                    }

                    lastTState += 4;
                }
            }
        }

        public override void ULAUpdateStart()
        {
            ULAByteCtr = 0;
            screenByteCtr = DisplayStart;
            lastTState = ActualULAStart;
            needsPaint = true;
        }

        public override void InvertFlashAttributes()
        {
            flashOn = !flashOn;
        }

        public override bool LoadROM(string path, string file)
        {
            FileStream fs;

            String filename = path + file;

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
                byte[] buffer = new byte[16384 * 2];
                int bytesRead = r.Read(buffer, 0, 16384 * 2);

                if (bytesRead == 0)
                    return false; //something bad happened!

                for (int g = 0; g < 4; g++)
                    for (int f = 0; f < 8192; ++f)
                    {
                        ROMpage[g][f] = (buffer[f + 8192 * g]);
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
                keyLine[2] = keyLine[2] | (0x10);
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
                keyLine[4] = keyLine[4] & ~(0x10);
            }
            #endregion

            #region Function key presses
            //Check if F12 is pressed
            if (keyBuffer[(int)keyCode.F12])
            {
                cpu.loggingEnabled = true;
            }

            if (keyBuffer[(int)keyCode.F11])
            {
                cpu.loggingEnabled = false;
            }
            #endregion
        }

        public override void OutSound()
        {
            averagedSound /= soundCounter;
            beeper.AddSample(averagedSound);
            averagedSound = 0;
            soundCounter = 0;
        }

        public override void UpdateAudio(int dt)
        {
            ayTStates += dt;
            
            if (ayTStates >= AY_SAMPLE_RATE)
            {
                aySound.Update();
                ayTStates -= AY_SAMPLE_RATE;
            }
            averagedSound += soundOut;
            soundCounter++;
        }


        public override void LoadTape(string filename)
        {
            if (filename.ToLower().EndsWith(".tap"))
            {
                LoadTAP(filename);
            }
            else
                if (filename.ToLower().EndsWith(".tzx"))
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
            Contend(addr, 3, 1);
            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;

            return PagePointer[page][offset];
        }

        public override void PokeByte(int addr, int b)
        {
            //This call is to mainly raise a memory change event for the debugger
            base.PokeByte(addr, b);

            Contend(addr, 3, 1);

            if (IsContended(addr))
                UpdateScreenBuffer(cpu.totalTStates);

            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;

            if (page < 2 && isROMprotected)
                return;

            PagePointer[page][offset] = b & 0xff;
        }

        public override void UseSNA(SNA_SNAPSHOT sna)
        {
            if (sna == null)
                return;
            
            if (sna is SNA_128K)
            {
                cpu.I = sna.HEADER.I;
                cpu._HL = sna.HEADER.HL_;
                cpu._DE = sna.HEADER.DE_;
                cpu._BC = sna.HEADER.BC_;
                cpu._AF = sna.HEADER.AF_;

                cpu.HL = sna.HEADER.HL;
                cpu.DE = sna.HEADER.DE;
                cpu.BC = sna.HEADER.BC;
                cpu.IY = sna.HEADER.IY;
                cpu.IX = sna.HEADER.IX;

                cpu.IFF1 = ((sna.HEADER.IFF2 & 0x04) != 0);
                cpu._R = sna.HEADER.R;
                cpu.AF = sna.HEADER.AF;
                cpu.SP = sna.HEADER.SP;
                cpu.interruptMode = sna.HEADER.IM;
                borderColour = sna.HEADER.BORDER;
                cpu.PC = ((SNA_128K)sna).PC;

                int val = ((SNA_128K)sna).PORT_7FFD;

                for (int f = 0; f < 16; f++)
                {
                    Array.Copy(((SNA_128K)sna).RAM_BANK[f], 0, RAMpage[f], 0, 8192);
                }

                PagePointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];
                PagePointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];
                PagePointer[4] = RAMpage[(int)RAM_BANK.TWO_1];
                PagePointer[5] = RAMpage[(int)RAM_BANK.TWO_2];
                
                Out(0x7ffd, val); //Perform a dummy Out to setup the remaining stuff!
            }
        }

        public override void UseSZX(SZXLoader szx)
        {
            cpu.I = szx.z80Regs.I;
            cpu._HL = szx.z80Regs.HL1;
            cpu._DE = szx.z80Regs.DE1;
            cpu._BC = szx.z80Regs.BC1;
            cpu._AF = szx.z80Regs.AF1;

            cpu.HL = szx.z80Regs.HL;
            cpu.DE = szx.z80Regs.DE;
            cpu.BC = szx.z80Regs.BC;
            cpu.IY = szx.z80Regs.IY;
            cpu.IX = szx.z80Regs.IX;

            cpu.IFF1 = (szx.z80Regs.IFF1 != 0);
            cpu._R = szx.z80Regs.R;
            cpu.AF = szx.z80Regs.AF;
            cpu.SP = szx.z80Regs.SP;
            cpu.interruptMode = szx.z80Regs.IM;
            cpu.PC = szx.z80Regs.PC;
            cpu.MemPtr = szx.z80Regs.BitReg;
            cpu.lastOpcodeWasEI = (byte)((szx.z80Regs.Flags & Speccy.SZXLoader.ZXSTZF_EILAST) != 0 ? 2:0);
            cpu.HaltOn = false;// (szx.z80Regs.Flags & Speccy.SZXLoader.ZXSTZF_HALTED) != 0;
            Issue2Keyboard = false;// (szx.keyboard.Flags & Speccy.SZXLoader.ZXSTKF_ISSUE2) != 0;
            aySound.SelectedRegister = szx.ayState.currentRegister;
            aySound.SetRegisters(szx.ayState.chRegs);

            for (int f = 0; f < 16; f++)
            {
                Array.Copy(szx.RAM_BANK[f], 0, RAMpage[f], 0, 8192);
            }

            PagePointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];
            PagePointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];
            PagePointer[4] = RAMpage[(int)RAM_BANK.TWO_1];
            PagePointer[5] = RAMpage[(int)RAM_BANK.TWO_2];
            PagePointer[6] = RAMpage[(int)RAM_BANK.ZERO_1];
            PagePointer[7] = RAMpage[(int)RAM_BANK.ZERO_2];

            Out(0x7ffd, szx.specRegs.x7ffd); //Perform a dummy Out to setup the remaining stuff!
            borderColour = szx.specRegs.Border;
            cpu.totalTStates = (int)szx.z80Regs.CyclesStart;

        }

        public override void UseZ80(Z80_SNAPSHOT z80)
        {
            cpu.I = z80.I;
            cpu._HL = z80.HL_;
            cpu._DE = z80.DE_;
            cpu._BC = z80.BC_;
            cpu._AF = z80.AF_;

            cpu.HL = z80.HL;
            cpu.DE = z80.DE;
            cpu.BC = z80.BC;
            cpu.IY = z80.IY;
            cpu.IX = z80.IX;

            cpu.IFF1 = z80.IFF1;
            cpu._R = z80.R;
            cpu.AF = z80.AF;
            cpu.SP = z80.SP;
            cpu.interruptMode = z80.IM;
            cpu.PC = z80.PC;
            Issue2Keyboard = z80.ISSUE2;

            for (int f = 0; f < 16; f++)
            {
                Array.Copy(z80.RAM_BANK[f], 0, RAMpage[f], 0, 8192);
            }

            PagePointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];
            PagePointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];
            PagePointer[4] = RAMpage[(int)RAM_BANK.TWO_1];
            PagePointer[5] = RAMpage[(int)RAM_BANK.TWO_2];
            PagePointer[6] = RAMpage[(int)RAM_BANK.ZERO_1];
            PagePointer[7] = RAMpage[(int)RAM_BANK.ZERO_2];

            Out(0x7ffd, z80.PORT_7FFD); //Perform a dummy Out to setup the remaining stuff!
            borderColour = z80.BORDER;
            cpu.totalTStates = z80.TSTATES;
        }
    }
}
