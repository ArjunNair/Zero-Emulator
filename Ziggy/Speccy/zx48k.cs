using System;
using System.IO;
using Peripherals;

namespace Speccy
{
    //Implements a zx 48k machine
    public class zx48 : Speccy.zxmachine
    {
        public zx48(IntPtr handle, bool lateTimingModel)
            : base(handle, lateTimingModel) {
            model = MachineModel._48k;
            InterruptPeriod = 32;
            FrameLength = 69888;

            clockSpeed = 3.50000;

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

            TstatesPerScanline = 224;
            TstateAtTop = BorderTopHeight * TstatesPerScanline;
            TstateAtBottom = BorderBottomHeight * TstatesPerScanline;
            tstateToDisp = new short[FrameLength];

            ScreenBuffer = new int[ScanLineWidth * BorderTopHeight //48 lines of border
                                              + ScanLineWidth * ScreenHeight //border + main + border of 192 lines
                                              + ScanLineWidth * BorderBottomHeight]; //56 lines of border

            keyBuffer = new bool[(int)keyCode.LAST];

            attr = new short[DisplayLength]; //6144 bytes of display memory will be mapped
            lastSoundOut = 0;
            soundOut = 0;
            averagedSound = 0;
            soundCounter = 0;
            ayIsAvailable = false;
            aySound.Reset();
            Reset(true);
        }

        public override void Reset(bool coldBoot) {
            base.Reset(coldBoot);
            contentionStartPeriod = 14335 + LateTiming;
            contentionEndPeriod = contentionStartPeriod + (ScreenHeight * TstatesPerScanline);//57324 + LateTiming;

            PageReadPointer[0] = ROMpage[0];
            PageReadPointer[1] = ROMpage[1];
            PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_1]; //Bank 5
            PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_2]; //Bank 5
            PageReadPointer[4] = RAMpage[(int)RAM_BANK.TWO_1]; //Bank 2
            PageReadPointer[5] = RAMpage[(int)RAM_BANK.TWO_2]; //Bank 2
            PageReadPointer[6] = RAMpage[(int)RAM_BANK.ZERO_1]; //Bank 0
            PageReadPointer[7] = RAMpage[(int)RAM_BANK.ZERO_2]; //Bank 0

            PageWritePointer[0] = JunkMemory[0];
            PageWritePointer[1] = JunkMemory[1];
            PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_1]; //Bank 5
            PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_2]; //Bank 5
            PageWritePointer[4] = RAMpage[(int)RAM_BANK.TWO_1]; //Bank 2
            PageWritePointer[5] = RAMpage[(int)RAM_BANK.TWO_2]; //Bank 2
            PageWritePointer[6] = RAMpage[(int)RAM_BANK.ZERO_1]; //Bank 0
            PageWritePointer[7] = RAMpage[(int)RAM_BANK.ZERO_2]; //Bank 0

            Random rand = new Random();
            //Fill memory with random stuff to simulate hard reset
            for (int i = DisplayStart; i < DisplayStart + 6912; ++i)
                PokeByteNoContend(i, rand.Next(255));

            screen = GetPageData(5);
            screenByteCtr = DisplayStart;
            ULAByteCtr = 0;

            ActualULAStart = 14340 - 24 - (TstatesPerScanline * BorderTopHeight) + LateTiming;
            lastTState = ActualULAStart;
            BuildAttributeMap();
            BuildContentionTable();

            lowROMis48K = true;
            BankInPage0 = "48K ROM";
            BankInPage1 = "-----";
            BankInPage2 = "-----";
            BankInPage3 = "-----";
            contendedBankPagedIn = false;
            showShadowScreen = false;
            pagingDisabled = false;
        }

        public override bool IsContended(int addr) {
            //int addr_hb = (addr & 0xff00) >> 8;
            // if ((addr > 16383) && (addr < 32768))
            if ((addr & 49152) == 16384)
                return true;

            return false;
        }

        //_addr = address to test for contention,
        //_count = how many times to contend
        //_time = how much time to tack on at end of each _count
        //public override void Contend(int _addr, int _time, int _count)
        //{
        //   // bool addrIsContended = IsContended(_addr);
        //    if (IsContended(_addr))
        //    {
        //        for (int f = 0; f < _count; f++)
        //        {
        //            totalTStates += contentionTable[totalTStates];
        //            totalTStates += _time;
        //        }

        //    }
        //    else
        //        totalTStates += (_time * _count);
        //}

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
                t += (TstatesPerScanline - 128); //24 tstates of right border + left border + 48 tstates of retrace
            }

            //build top half of tstateToDisp table
            //vertical retrace period
            for (t = 0; t < ActualULAStart; t++)
                tstateToDisp[t] = 0;

            //next 48 are actual border
            while (t < ActualULAStart + (TstateAtTop)) {
                //border(24t) + screen (128t) + border(24t) = 176 tstates
                for (int g = 0; g < 176; g++)
                    tstateToDisp[t++] = 1;

                //horizontal retrace
                for (int g = 176; g < TstatesPerScanline; g++)
                    tstateToDisp[t++] = 0;
            }

            //build middle half of display
            int _x = 0;
            int _y = 0;
            int scrval = 2;
            while (t < ActualULAStart + (TstateAtTop) + (ScreenHeight * TstatesPerScanline)) {
                //left border
                for (int g = 0; g < 24; g++)
                    tstateToDisp[t++] = 1;

                //screen
                for (int g = 24; g < 24 + 128; g++) {
                    //Map screenaddr to tstate
                    if (g % 4 == 0) {
                        scrval = (((((_y & 0xc0) >> 3) | (_y & 0x07) | (0x40)) << 8)) | (((_x >> 3) & 0x1f) | ((_y & 0x38) << 2));
                        _x += 8;
                    }
                    tstateToDisp[t++] = (short)scrval;
                }
                _y++;

                //right border
                for (int g = 24 + 128; g < 24 + 128 + 24; g++)
                    tstateToDisp[t++] = 1;

                //horizontal retrace
                for (int g = 24 + 128 + 24; g < 24 + 128 + 24 + 48; g++)
                    tstateToDisp[t++] = 0;
            }

            int h = contentionStartPeriod + 3;
            while (h < contentionEndPeriod + 3) {
                for (int j = 0; j < 128; j += 8) {
                    floatingBusTable[h] = tstateToDisp[h + 2];                    //screen address
                    floatingBusTable[h + 1] = attr[(tstateToDisp[h + 2] - 16384)];  //attr address
                    floatingBusTable[h + 2] = tstateToDisp[h + 2 + 4];             //screen address + 1
                    floatingBusTable[h + 3] = attr[(tstateToDisp[h + 2 + 4] - 16384)]; //attr address + 1
                    h += 8;
                }
                h += TstatesPerScanline - 128;
            }

            //build bottom border
            while (t < ActualULAStart + (TstateAtTop) + (ScreenHeight * TstatesPerScanline) + (TstateAtBottom)) {
                //border(24t) + screen (128t) + border(24t) = 176 tstates
                for (int g = 0; g < 176; g++)
                    tstateToDisp[t++] = 1;

                //horizontal retrace
                for (int g = 176; g < TstatesPerScanline; g++)
                    tstateToDisp[t++] = 0;
            }
        }

        // Contention| LowBitReset| Result
        //-----------------------------------------
        // No        | No         | N:4
        // No        | Yes        | N:1 C:3
        // Yes       | Yes        | C:1 C:3
        // Yes       | No         | C:1 C:1 C:1 C:1
        public override int In(int port) {
            base.In(port);

            if (isPlayingRZX) {
                if (rzxInputCount < rzxFrame.inputCount) {
                    rzxIN = rzxFrame.inputs[rzxInputCount++];
                }
                return rzxIN;
            }
            int result = 0xff;
            //bool portIsContended = IsContended(port);
            bool lowBitReset = (port & 0x01) == 0;
            //T1
            //Contend(port); //N:1 || C:1
            Contend(port);
            totalTStates++; //T2

            //Kempston joystick
            if (HasKempstonJoystick && ((port & 0xe0) == 0)) {
                if (!externalSingleStep) {
                    Contend(port, 1, 3);
                    result = joystickState[(int)JoysticksEmulated.KEMPSTON];
                }
            } else //ULA Plus
                if (ULAPlusEnabled && (port == 0xff3b)) {
                    Contend(port, 3, 1); //Contend once;  add 3 tstates
                    result = lastULAPlusOut;
                } else
                    if (lowBitReset)    //Even address, so get input
                    {
                        totalTStates += contentionTable[totalTStates]; //C:3
                        //Contend(port, 3, 1); //Contend once;  add 3 tstates
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
                        } else {
                            if (Issue2Keyboard) {
                                if ((lastFEOut & (EAR_BIT + MIC_BIT)) == 0) {
                                    result &= ~(TAPE_BIT);
                                } else
                                    result |= TAPE_BIT;
                            } else {
                                if ((lastFEOut & EAR_BIT) == 0) {
                                    result &= ~(TAPE_BIT);
                                } else
                                    result |= TAPE_BIT;
                            }
                        }
                        totalTStates += 3;
                    } else {
                        Contend(port, 1, 3); //T2, T3

                        if (ayIsAvailable && ((port & 0xc002) == 0xc000)) //AY register activate
                        {
                            result = aySound.PortRead();
                        } else if (HasKempstonMouse)//Kempston Mouse
                        {
                            if (port == 64479)
                                result = MouseX % 0xff;     //X ranges from 0 to 255
                            else if (port == 65503)
                                result = MouseY % 0xff;     //Y ranges from 0 to 255
                            else if (port == 64223)
                                result = MouseButton;// MouseButton;
                        } else
                        //Unused port, return floating bus
                        {
                            int _tstates = totalTStates - 1; //the floating bus is read on the last t-state

                            //if we're on the top or bottom border return 0xff
                            if ((_tstates < contentionStartPeriod) || (_tstates > contentionEndPeriod))
                                result = 0xff;
                            else {
                                if (floatingBusTable[_tstates] < 0)
                                    result = 0xff;
                                else
                                    result = PeekByteNoContend(floatingBusTable[_tstates]);
                            }
                        }
                    }

            base.In(port, result & 0xff);
            return (result & 0xff);
        }

        // Contention| LowBitReset| Result
        //-----------------------------------------
        // No        | No         | N:4
        // No        | Yes        | N:1 C:3
        // Yes       | Yes        | C:1 C:3
        // Yes       | No         | C:1 C:1 C:1 C:1

        public override void Out(int port, int val) {
            base.Out(port, val);

            bool lowBitReset = ((port & 0x01) == 0);

            //T1
            //Contend(port, 1, 1);        //N:1 || C:1
            Contend(port);
            totalTStates++;

            int tempTStates = totalTStates;
            int highestTStates = totalTStates;

            if (lowBitReset)    //Even address, so update ULA
            {
                lastFEOut = val;
                totalTStates += contentionTable[totalTStates];  //T2

                if (borderColour != (val & BORDER_BIT))
                    UpdateScreenBuffer(totalTStates);

                //needsPaint = true; //useful while debugging as it renders line by line
                borderColour = val & BORDER_BIT;  //The LSB 3 bits of val hold the border colour
                int beepVal = val & (EAR_BIT + MIC_BIT);
                if (!tapeIsPlaying) {
                    if (beepVal != lastSoundOut) {
                        if ((beepVal) == 0) {
                            soundOut = MIN_SOUND_VOL;
                        } else {
                            soundOut = (short)(MAX_SOUND_VOL * 0.5f);
                        }
                        if ((val & MIC_BIT) != 0)   //Boost slightly if MIC is on
                            soundOut += (short)(MAX_SOUND_VOL * 0.2f);
                        lastSoundOut = beepVal;
                    }
                }

                //T3, T4
                totalTStates += 3;

                if (totalTStates > highestTStates)
                    highestTStates = totalTStates;

                totalTStates = tempTStates;
            }
            //else
            if (ULAPlusEnabled) {
                //ULA Plus
                if (port == 0xbf3b) {
                    // UpdateScreenBuffer(totalTStates);
                    tempTStates = totalTStates;
                    totalTStates += contentionTable[totalTStates];  //C:3

                    int mode = (val & 0xc0) >> 6;

                    //mode group
                    if (mode == 1) {
                        ULAGroupMode = 1;
                    } else if (mode == 0) //palette group
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
                //else
                if (port == 0xff3b) {
                    tempTStates = totalTStates;

                    if (lastULAPlusOut != val)
                        UpdateScreenBuffer(totalTStates);

                    totalTStates += contentionTable[totalTStates];  //T2
                    lastULAPlusOut = val;

                    if (ULAGroupMode == 1) {
                        ULAPaletteEnabled = (val & 0x01) != 0;
                    } else {
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
            }
            //AY register activate
            if (ayIsAvailable) {
                if ((port & 0xc002) == 0xc000) {
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
                    aySound.PortWrite(val);
                    totalTStates += 3;

                    if (totalTStates > highestTStates)
                        highestTStates = totalTStates;

                    totalTStates = tempTStates;
                }
            }
            // tempTStates = totalTStates;
            if (IsContended(port) && !lowBitReset) {
                Contend(port, 1, 3);
            } else {
                totalTStates += 3;
            }

            if (totalTStates > highestTStates)
                highestTStates = totalTStates;

            totalTStates = highestTStates;
        }

        public override void EnableAY(bool val) {
            ayIsAvailable = val;
            aySound.Reset();
        }

        //public override void ULAUpdateStart()
        //{
        //    ULAByteCtr = 0;
        //    screenByteCtr = DisplayStart;
        //    lastTState = ActualULAStart;
        //    needsPaint = true;
        //}
        /*
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
                //it again in the  process loop on framelength overflow.
                needsPaint = true;
            }

            //the additional 1 tstate is required to get correct number of bytes to output in ircontention.sna
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

                        //Replacing the below with PeekByteNoContend(attr[screenByteCtr - 16384]);
                        //also works
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

                        if (ULAPlusEnabled && ULAPaletteEnabled)
                        {
                            ink = ULAPlusColours[((flashBitOn ? 1:0) * 2 + (bright !=0 ? 1:0)) * 16 + (lastAttrValue & 0x07)];
                            paper = ULAPlusColours[((flashBitOn ? 1 : 0) * 2 + (bright != 0 ? 1 : 0)) * 16 + ((lastAttrValue >> 3) & 0x7) + 8];
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
                    lastTState += 4 ;
                }
            }
        }
        */

        public override bool LoadROM(string path, string file) {
            FileStream fs;
            String filename = path + file;

            //Check if we can find the ROM file!
            try {
                fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            } catch {
                return false;
            }

            fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            using (BinaryReader r = new BinaryReader(fs)) {
                //int bytesRead = ReadBytes(r, mem, 0, 16384);
                byte[] buffer = new byte[16384];
                int bytesRead = r.Read(buffer, 0, 16384);

                if (bytesRead == 0)
                    return false; //something bad happened!

                for (int g = 0; g < 2; g++)
                    for (int f = 0; f < 8192; ++f) {
                        ROMpage[g][f] = (buffer[f + 8192 * g]);
                    }
            }
            fs.Close();
            return true;
        }

        public override void UseSNA(SNA_SNAPSHOT sna) {
            if (sna == null)
                return;

            if (sna is SNA_48K) {
                lock (lockThis) {
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
                    if (IFF1)
                        lastOpcodeWasEI = 1;        //force ignore re-triggered interrupts
                    _R = sna.HEADER.R;
                    AF = sna.HEADER.AF;
                    SP = sna.HEADER.SP;
                    interruptMode = sna.HEADER.IM;
                    borderColour = sna.HEADER.BORDER;

                    int screenAddr = DisplayStart;

                    for (int f = 0; f < 49152; ++f) {
                        PokeByteNoContend(screenAddr + f, ((SNA_48K)sna).RAM[f]);
                    }
                    PC = PeekWordNoContend(SP);
                    SP += 2;
                }
            }
        }

        public override void UseSZX(SZXLoader szx) {
            lock (lockThis) {
                base.UseSZX(szx);
                Out(0x0ffe, szx.specRegs.Fe);
                borderColour = szx.specRegs.Border;
                LateTiming = szx.header.Flags & 0x1;
                PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];
                PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];
                PageReadPointer[4] = RAMpage[(int)RAM_BANK.TWO_1];
                PageReadPointer[5] = RAMpage[(int)RAM_BANK.TWO_2];
                PageReadPointer[6] = RAMpage[(int)RAM_BANK.ZERO_1];
                PageReadPointer[7] = RAMpage[(int)RAM_BANK.ZERO_2];

                PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];
                PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];
                PageWritePointer[4] = RAMpage[(int)RAM_BANK.TWO_1];
                PageWritePointer[5] = RAMpage[(int)RAM_BANK.TWO_2];
                PageWritePointer[6] = RAMpage[(int)RAM_BANK.ZERO_1];
                PageWritePointer[7] = RAMpage[(int)RAM_BANK.ZERO_2];

                if (szx.paletteLoaded) {
                    if (szx.palette.flags == 1) {
                        ULAPlusEnabled = true;
                        ULAPaletteEnabled = true;
                    } else {
                        ULAPaletteEnabled = false;
                    }
                    ULAPaletteGroup = szx.palette.currentRegister;
                    for (int f = 0; f < 64; f++) {
                        byte val = szx.palette.paletteRegs[f];
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
                        ULAPlusColours[f] = bgr;
                        Out(0xbf3b, szx.specRegs.Fe);
                    }
                }
                totalTStates = (int)szx.z80Regs.CyclesStart;
            }
        }

        public override void UseZ80(Z80_SNAPSHOT z80) {
            lock (lockThis) {
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
                borderColour = z80.BORDER;
                Issue2Keyboard = z80.ISSUE2;

                EnableAY(z80.AY_FOR_48K);

                //Copy AY regs
                if (z80.AY_FOR_48K) {
                    for (int f = 0; f < 16; f++)
                        aySound.SetRegisters(z80.AY_REGS);

                    Out(0xfffd, z80.PORT_FFFD); //Setup the sound chip
                }

                for (int f = 0; f < 16; f++) {
                    Array.Copy(z80.RAM_BANK[f], 0, RAMpage[f], 0, 8192);
                }

                PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];
                PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];
                PageReadPointer[4] = RAMpage[(int)RAM_BANK.TWO_1];
                PageReadPointer[5] = RAMpage[(int)RAM_BANK.TWO_2];
                PageReadPointer[6] = RAMpage[(int)RAM_BANK.ZERO_1];
                PageReadPointer[7] = RAMpage[(int)RAM_BANK.ZERO_2];

                PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];
                PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];
                PageWritePointer[4] = RAMpage[(int)RAM_BANK.TWO_1];
                PageWritePointer[5] = RAMpage[(int)RAM_BANK.TWO_2];
                PageWritePointer[6] = RAMpage[(int)RAM_BANK.ZERO_1];
                PageWritePointer[7] = RAMpage[(int)RAM_BANK.ZERO_2];

                totalTStates = z80.TSTATES;
            }
        }
    }
}