using System;
using System.IO;
using Peripherals;

namespace Speccy
{
    public class zx128 : Speccy.zxmachine
    {
            public zx128(IntPtr handle, bool lateTimingModel)
            : base(handle, lateTimingModel) {
            FrameLength = 70908;
            InterruptPeriod = 36;
            clockSpeed = 3.54690;
            model = MachineModel._128k;

            contentionTable = new byte[70930];
            floatingBusTable = new short[70930];
            for (int f = 0; f < 70930; f++)
                floatingBusTable[f] = -1;

            CharRows = 24;
            CharCols = 32;
            ScreenWidth = 256;
            ScreenHeight = 192;
            BorderTopHeight = 48;
            BorderBottomHeight = 56;
            BorderLeftWidth = 48;
            BorderRightWidth = 48;
            DisplayStart = 16384;
            DisplayLength = 6144;
            AttributeStart = 22528;
            AttributeLength = 768;
            borderColour = 7;
            ScanLineWidth = BorderLeftWidth + ScreenWidth + BorderRightWidth;

            TstatesPerScanline = 228;
            TstateAtTop = BorderTopHeight * TstatesPerScanline;
            TstateAtBottom = BorderBottomHeight * TstatesPerScanline;
            tstateToDisp = new short[FrameLength];

            ScreenBuffer = new int[ScanLineWidth * BorderTopHeight //48 lines of border
                                              + ScanLineWidth * ScreenHeight //border + main + border of 192 lines
                                              + ScanLineWidth * BorderBottomHeight]; //56 lines of border

            keyBuffer = new bool[(int)keyCode.LAST];
            attr = new short[DisplayLength];  //6144 bytes of display memory will be mapped
            Reset(true);
        }

        public override void Reset(bool coldBoot) {
            base.Reset(coldBoot);
            contentionStartPeriod = 14361 + LateTiming;
            contentionEndPeriod = contentionStartPeriod + (ScreenHeight * TstatesPerScanline); //57324 + LateTiming;

            PageReadPointer[0] = ROMpage[0];  //128 editor default!
            PageReadPointer[1] = ROMpage[1];
            PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];  //Bank 5
            PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];  //Bank 5
            PageReadPointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW];   //Bank 2
            PageReadPointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH];   //Bank 2
            PageReadPointer[6] = RAMpage[(int)RAM_BANK.ZERO_LOW];   //Bank 0
            PageReadPointer[7] = RAMpage[(int)RAM_BANK.ZERO_HIGH];   //Bank 0

            PageWritePointer[0] = JunkMemory[0];
            PageWritePointer[1] = JunkMemory[1];
            PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];  //Bank 5
            PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];  //Bank 5
            PageWritePointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW];   //Bank 2
            PageWritePointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH];   //Bank 2
            PageWritePointer[6] = RAMpage[(int)RAM_BANK.ZERO_LOW];   //Bank 0
            PageWritePointer[7] = RAMpage[(int)RAM_BANK.ZERO_HIGH];   //Bank 0

            BankInPage0 = ROM_128_BAS;
            BankInPage1 = "Bank 5";
            BankInPage2 = "Bank 2";
            BankInPage3 = "Bank 0";
            lowROMis48K = false;
            contendedBankPagedIn = false;
            pagingDisabled = false;
            showShadowScreen = false;

            Random rand = new Random();
            //Fill memory with random stuff to simulate hard reset
            for (int i = DisplayStart; i < DisplayStart + 6912; ++i)
                PokeByteNoContend(i, rand.Next(255));
            screen = GetPageData(5); //Bank 5 is a copy of the screen

            ActualULAStart = 14366 - 24 - (TstatesPerScanline * BorderTopHeight) + LateTiming;
            lastTState = ActualULAStart;
            BuildAttributeMap();

            BuildContentionTable();
        }

        public override void BuildContentionTable() {
            int t = contentionStartPeriod;
            while (t < contentionEndPeriod) {
                //for 128 t-states
                for (int i = 0; i < 128; i += 8) {
                    contentionTable[t++] = 6;
                    contentionTable[t++] = 5;
                    contentionTable[t++] = 4;
                    contentionTable[t++] = 3;
                    contentionTable[t++] = 2;
                    contentionTable[t++] = 1;
                    contentionTable[t++] = 0;
                    contentionTable[t++] = 0;
                }
                t += (TstatesPerScanline - 128);
            }

            //build top half of tstateToDisp table
            //vertical retrace period
            for (t = 0; t < ActualULAStart; t++)
                tstateToDisp[t] = 0;

            //next 48 are actual border
            while (t < ActualULAStart + (TstateAtTop)) {
                for (int g = 0; g < 176; g++)
                    tstateToDisp[t++] = 1;

                for (int g = 176; g < TstatesPerScanline; g++)
                    tstateToDisp[t++] = 0;
            }

            //build middle half
            int _x = 0;
            int _y = 0;
            int scrval = 2;
            while (t < ActualULAStart + (TstateAtTop) + (ScreenHeight * TstatesPerScanline)) {
                for (int g = 0; g < 24; g++)
                    tstateToDisp[t++] = 1;

                for (int g = 24; g < 24 + 128; g++) {
                    //Map screenaddr to tstate
                    if (g % 4 == 0) {
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
            while (h < contentionEndPeriod + 3) {
                for (int j = 0; j < 128; j += 8) {
                    floatingBusTable[h] = tstateToDisp[h + 2];
                    floatingBusTable[h + 1] = attr[(tstateToDisp[h + 2] - 16384)];
                    floatingBusTable[h + 2] = tstateToDisp[h + 2 + 4];
                    floatingBusTable[h + 3] = attr[(tstateToDisp[h + 2 + 4] - 16384)];
                    h += 8;
                }
                h += TstatesPerScanline - 128;
            }

            //build bottom half
            while (t < ActualULAStart + (TstateAtTop) + (ScreenHeight * TstatesPerScanline) + (TstateAtBottom)) {
                for (int g = 0; g < 176; g++)
                    tstateToDisp[t++] = 1;

                for (int g = 176; g < TstatesPerScanline; g++)
                    tstateToDisp[t++] = 0;
            }
        }

        public override bool IsContended(int addr) {
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

        //public override void Contend(int _addr, int _time, int _count)
        //{
        //    bool addrIsContended = IsContended(_addr);
        //    for (int f = 0; f < _count; f++)
        //    {
        //        if (addrIsContended)
        //        {
        //            totalTStates += contentionTable[totalTStates];
        //        }
        //        totalTStates += _time;
        //    }
        //}

        public override int In(int port) {
            base.In(port);

            if (isPlayingRZX) {
                if (rzx.inputCount < rzx.frame.inputCount)
                    rzxIN = rzx.frame.inputs[rzx.inputCount++];
                //rzxIN = rzx.frame.inputs[rzx.inputCount++];
                return rzxIN;
            }

            int result = 0xff;
            //bool portIsContended = IsContended(port);

            bool lowBitReset = ((port & 0x01) == 0);

            Contend(port);
            totalTStates++; //T2

            //Kempston joystick
            if (HasKempstonJoystick && IsKempstonActive(port)) {
                if (!externalSingleStep) {
                    Contend(port, 1, 3);
                    result = joystickState[(int)JoysticksEmulated.KEMPSTON];
                }
            }
            else if (lowBitReset) {   //Even address, so get input
                    totalTStates += contentionTable[totalTStates]; //C:3

                    if (!externalSingleStep) {
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
                    }

                    result = result & 0x1f; //mask out lower 4 bits
                    result = result | 0xa0; //set bit 5 & 7 to 1

                    if (tapeIsPlaying) {
                        if (tapeBit == 0) {
                            result &= ~(TAPE_BIT);    //reset is EAR ON
                        } else {
                            result |= (TAPE_BIT); //set is EAR Off
                        }
                    }
                    else if ((lastFEOut & 0x10) == 0) {
                            result &= ~(0x40);
                        }
                        else
                            result |= 0x40;
                }
                else {
                    Contend(port, 1, 3); //T2, T3

                    if ((port & 0xc002) == 0xc000) //AY register activate
                        result = aySound.PortRead();
                    else if (HasKempstonMouse) {//Kempston Mouse
                        if (port == 64479)
                            result = MouseX % 0xff;     //X ranges from 0 to 255
                        else if (port == 65503)
                            result = MouseY % 0xff;     //Y ranges from 0 to 255
                        else if (port == 64223)
                            result = MouseButton;// MouseButton;
                    }
                    else {          //return floating bus (also handles port 0x7ffd)
                        int _tstates = totalTStates - 1; //floating bus is sampled on the last cycle

                        //if we're on the top or bottom border return 0xff
                        if ((_tstates < contentionStartPeriod) || (_tstates > contentionEndPeriod))
                            result = 0xff;
                        else {
                            if (floatingBusTable[_tstates] < 0)
                                result = 0xff;
                            else
                                result = PeekByteNoContend(floatingBusTable[_tstates]);
                        }

                        if ((port & 0x8002) == 0) //Memory paging
                        {
                            int tempTStates = totalTStates;
                            Out_7ffd(port, result);
                            totalTStates = tempTStates;
                        }
                    }
                }
            totalTStates += 3;
            base.In(port, result & 0xff);
            return (result & 0xff);
        }

        private void Out_7ffd(int port, int val) {
            Contend(port, 1, 3);

            //Aug 18. 2012
            if ((val & 0x08) != 0) {
                if (!showShadowScreen)
                    UpdateScreenBuffer(totalTStates);

                showShadowScreen = true;
                screen = GetPageData(7); //Bank 7
            }
            else {
                if (showShadowScreen)
                    UpdateScreenBuffer(totalTStates);

                showShadowScreen = false;
                screen = GetPageData(5); //Bank 5
            }

            if (pagingDisabled)
                return;

            last7ffdOut = val;
            //Bits 0 to 2 select the RAM page
            switch (val & 0x07) {
                case 0: //Bank 0
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.ZERO_LOW];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.ZERO_HIGH];

                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.ZERO_LOW];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.ZERO_HIGH];

                    BankInPage3 = "Bank 0";
                    contendedBankPagedIn = false;
                    break;

                case 1: //Bank 1
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.ONE_LOW];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.ONE_HIGH];

                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.ONE_LOW];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.ONE_HIGH];
                    BankInPage3 = "Bank 1";
                    contendedBankPagedIn = true;
                    break;

                case 2: //Bank 2
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.TWO_LOW];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.TWO_HIGH];

                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.TWO_LOW];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.TWO_HIGH];
                    BankInPage3 = "Bank 2";
                    contendedBankPagedIn = false;
                    break;

                case 3: //Bank 3
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.THREE_LOW];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.THREE_HIGH];

                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.THREE_LOW];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.THREE_HIGH];
                    BankInPage3 = "Bank 3";
                    contendedBankPagedIn = true;
                    break;

                case 4: //Bank 4
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.FOUR_LOW];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.FOUR_HIGH];

                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.FOUR_LOW];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.FOUR_HIGH];
                    BankInPage3 = "Bank 4";
                    contendedBankPagedIn = false;
                    break;

                case 5: //Bank 5
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.FIVE_LOW];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.FIVE_HIGH];

                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.FIVE_LOW];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.FIVE_HIGH];
                    BankInPage3 = "Bank 5";
                    contendedBankPagedIn = true;
                    break;

                case 6: //Bank 6
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.SIX_LOW];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.SIX_HIGH];

                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.SIX_LOW];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.SIX_HIGH];
                    BankInPage3 = "Bank 6";
                    contendedBankPagedIn = false;
                    break;

                case 7: //Bank 7
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.SEVEN_LOW];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.SEVEN_HIGH];

                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.SEVEN_LOW];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.SEVEN_HIGH];
                    BankInPage3 = "Bank 7";
                    contendedBankPagedIn = true;
                    break;
            }

            //ROM select
            if ((val & 0x10) != 0) {
                //48k basic
                PageReadPointer[0] = ROMpage[2];
                PageReadPointer[1] = ROMpage[3];
                PageWritePointer[0] = JunkMemory[0];
                PageWritePointer[1] = JunkMemory[1];
                BankInPage0 = ROM_48_BAS;
                lowROMis48K = true;
            }
            else {
                //128k basic
                PageReadPointer[0] = ROMpage[0];
                PageReadPointer[1] = ROMpage[1];
                PageWritePointer[0] = JunkMemory[0];
                PageWritePointer[1] = JunkMemory[1];
                BankInPage0 = ROM_128_BAS;
                lowROMis48K = false;
            }

            //Check bit 5 for paging disable
            if ((val & 0x20) != 0) {
                pagingDisabled = true;
            }
        }

        public override void Out(int port, int val) {
            base.Out(port, val);
            bool portIsContended = IsContended(port);
            bool lowBitReset = (port & 0x01) == 0;

            Contend(port, 1, 1);        //N:1 || C:1

            int tempTStates = totalTStates;
            int highestTStates = totalTStates;

            //ULA activate
            if (lowBitReset)    //Even address, so update ULA
            {
                lastFEOut = val;
                totalTStates += contentionTable[totalTStates];

                if (borderColour != (val & BORDER_BIT))
                    UpdateScreenBuffer(totalTStates);

                borderColour = val & BORDER_BIT;  //The LSB 3 bits of val hold the border colour
                int beepVal = val & (EAR_BIT);// + MIC_BIT);

                if (!tapeIsPlaying) {
                    if (beepVal != lastSoundOut) {

                        if ((beepVal) == 0)
                            soundOut = MIN_SOUND_VOL;
                        else
                            soundOut = MAX_SOUND_VOL;

                        if ((val & MIC_BIT) != 0)   //Boost slightly if MIC is on
                            soundOut += (short)(MAX_SOUND_VOL * 0.2f);

                        lastSoundOut = beepVal;
                    }
                }
                totalTStates += 3;

                if (totalTStates > highestTStates)
                    highestTStates = totalTStates;

                totalTStates = tempTStates;
            }

            //AY register activate
            if ((port & 0xC002) == 0xC000) {
                tempTStates = totalTStates;
                aySound.SelectedRegister = val & 0x0F;
                totalTStates += 3;

                if (totalTStates > highestTStates)
                    highestTStates = totalTStates;

                totalTStates = tempTStates;
            }

            //AY data
            if ((port & 0xC002) == 0x8000) {
                tempTStates = totalTStates;
                lastAYPortOut = val;
                aySound.PortWrite(val);
                totalTStates += 3;

                if (totalTStates > highestTStates)
                    highestTStates = totalTStates;

                totalTStates = tempTStates;
            }

            //Memory paging activate
            if ((port & 0x8002) == 0) //Are bits 1 and 15 reset?
            {
                tempTStates = totalTStates;

                Out_7ffd(port, val);

                if (totalTStates > highestTStates)
                    highestTStates = totalTStates;

                totalTStates = tempTStates;
            }

            //ULA Plus
            if (port == 0xbf3b) {
                tempTStates = totalTStates;
                totalTStates += contentionTable[totalTStates];  //C:3

                int mode = (val & 0xc0) >> 6;

                //mode group
                if (mode == 1) {
                    ULAGroupMode = 1;
                }
                else if (mode == 0) //palette group
                {
                    ULAGroupMode = 0;
                    ULAPaletteGroup = val & 0x3f;
                }

                //T3, T4
                totalTStates += 3;
                if (totalTStates > highestTStates)
                    highestTStates = totalTStates;

                totalTStates = tempTStates;
            }

            if (port == 0xff3b) {
                tempTStates = totalTStates;

                if (lastULAPlusOut != val)
                    UpdateScreenBuffer(totalTStates);

                totalTStates += contentionTable[totalTStates];  //T2
                lastULAPlusOut = val;

                if (ULAGroupMode == 1) {
                    ULAPaletteEnabled = (val & 0x01) != 0;
                }
                else {
                    int bl = val & 0x01;
                    int bm = bl;
                    int bh = (val & 0x02) >> 1;
                    val >>= 2;
                    int rl = val & 0x01;
                    int rm = (val & 0x02) >> 1;
                    int rh = (val & 0x04) >> 2;
                    val >>= 3;
                    int gl = val & 0x01;
                    int gm = (val & 0x02) >> 1;
                    int gh = (val & 0x04) >> 2;
                    int bgr = ( //each byte built as hmlhmlml bits from original 3 bit colour value
                                (((rh << 7) | (rm << 6) | (rl << 5) | (rh << 4) | (rm << 3) | (rl << 2) | (rm << 1) | (rl)) << 16)
                                | (((gh << 7) | (gm << 6) | (gl << 5) | (gh << 4) | (gm << 3) | (gl << 2) | (gm << 1) | (gl)) << 8)
                                | (((bh << 7) | (bm << 6) | (bl << 5) | (bh << 4) | (bm << 3) | (bl << 2) | (bm << 1) | (bl)))
                                );
                    ULAPlusColours[ULAPaletteGroup] = bgr;
                }

                totalTStates += 3;

                if (totalTStates > highestTStates)
                    highestTStates = totalTStates;

                totalTStates = tempTStates;
            }

            if (portIsContended && !lowBitReset)
                Contend(port, 1, 3);
            else
                totalTStates += 3;
            

            if (totalTStates > highestTStates)
                highestTStates = totalTStates;

            totalTStates = highestTStates;
        }

        public override bool LoadROM(string path, string file) {
            FileStream fs;

            String filename = path + file;

            //Check if we can find the ROM file!
            try {
                fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            }
            catch {
                return false;
            }

            fs = new FileStream(filename, FileMode.Open, FileAccess.Read);

            using (BinaryReader r = new BinaryReader(fs)) {
                //int bytesRead = ReadBytes(r, mem, 0, 16384);
                byte[] buffer = new byte[16384 * 2];
                int bytesRead = r.Read(buffer, 0, 16384 * 2);

                if (bytesRead == 0)
                    return false; //something bad happened!

                for (int g = 0; g < 4; g++)
                    for (int f = 0; f < 8192; ++f)
                        ROMpage[g][f] = (buffer[f + 8192 * g]);
            }
            fs.Close();
            return true;
        }

        public override void UseSNA(SNA_SNAPSHOT sna) {
            if (sna == null)
                return;

            if (sna is SNA_128K) {
                I = sna.HEADER.I;
                _HL = sna.HEADER.HL_;
                _DE = sna.HEADER.DE_;
                _BC = sna.HEADER.BC_;
                _AF = sna.HEADER.AF_;

                HL = sna.HEADER.HL;
                DE = sna.HEADER.DE;
                BC = sna.HEADER.BC;
                IY = sna.HEADER.IY;
                IX = sna.HEADER.IX;

                IFF1 = ((sna.HEADER.IFF2 & 0x04) != 0);
                _R = sna.HEADER.R;
                AF = sna.HEADER.AF;
                SP = sna.HEADER.SP;
                interruptMode = sna.HEADER.IM;
                borderColour = sna.HEADER.BORDER;
                PC = ((SNA_128K)sna).PC;

                int val = ((SNA_128K)sna).PORT_7FFD;

                for (int f = 0; f < 16; f++)
                    Array.Copy(((SNA_128K)sna).RAM_BANK[f], 0, RAMpage[f], 0, 8192);

                PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];
                PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];
                PageReadPointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW];
                PageReadPointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH];

                PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];
                PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];
                PageWritePointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW];
                PageWritePointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH];

                Out(0x7ffd, val); //Perform a dummy Out to setup the remaining stuff!
            }
        }

        public override void UseSZX(SZXFile szx) {
            lock (lockThis) {
                base.UseSZX(szx);
                aySound.SelectedRegister = szx.ayState.currentRegister;
                aySound.SetRegisters(szx.ayState.chRegs);
                LateTiming = szx.header.Flags & 0x1;
                PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];
                PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];
                PageReadPointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW];
                PageReadPointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH];
                PageReadPointer[6] = RAMpage[(int)RAM_BANK.ZERO_LOW];
                PageReadPointer[7] = RAMpage[(int)RAM_BANK.ZERO_HIGH];

                PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];
                PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];
                PageWritePointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW];
                PageWritePointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH];
                PageWritePointer[6] = RAMpage[(int)RAM_BANK.ZERO_LOW];
                PageWritePointer[7] = RAMpage[(int)RAM_BANK.ZERO_HIGH];

                Out(0x7ffd, szx.specRegs.x7ffd); //Perform a dummy Out to setup the remaining stuff!
                borderColour = szx.specRegs.Border;
                totalTStates = (int)szx.z80Regs.CyclesStart;
            }
        }

        public override void UseZ80(Z80_SNAPSHOT z80) {
            I = z80.I;
            _HL = z80.HL_;
            _DE = z80.DE_;
            _BC = z80.BC_;
            _AF = z80.AF_;

            HL = z80.HL;
            DE = z80.DE;
            BC = z80.BC;
            IY = z80.IY;
            IX = z80.IX;

            IFF1 = z80.IFF1;
            _R = z80.R;
            AF = z80.AF;
            SP = z80.SP;
            interruptMode = z80.IM;
            PC = z80.PC;
            Issue2Keyboard = z80.ISSUE2;

            for (int f = 0; f < 16; f++) {
                Array.Copy(z80.RAM_BANK[f], 0, RAMpage[f], 0, 8192);
            }

            PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];
            PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];
            PageReadPointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW];
            PageReadPointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH];
            PageReadPointer[6] = RAMpage[(int)RAM_BANK.ZERO_LOW];
            PageReadPointer[7] = RAMpage[(int)RAM_BANK.ZERO_HIGH];

            PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];
            PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];
            PageWritePointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW];
            PageWritePointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH];
            PageWritePointer[6] = RAMpage[(int)RAM_BANK.ZERO_LOW];
            PageWritePointer[7] = RAMpage[(int)RAM_BANK.ZERO_HIGH];

            for (int f = 0; f < 16; f++)
                aySound.SetRegisters(z80.AY_REGS);

            Out(0xfffd, z80.PORT_FFFD); //Setup the sound chip
            Out(0x7ffd, z80.PORT_7FFD); //Perform a dummy Out to setup the remaining stuff!

            borderColour = z80.BORDER;
            totalTStates = z80.TSTATES;
        }
    }
}