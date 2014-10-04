using System;
using System.IO;
using Peripherals;

namespace Speccy
{
    public class Pentagon128K : Speccy.zxmachine
    {
        //Disk emulation related stuff
        protected bool[] diskInserted = { false, false, false, false };

        private WD1793 wdDrive = new WD1793();
        private int pixelData = 0;
        private int pixelsWritten = 0;

        private int attrData = 0;
        private int bright = 0;
        private int ink = 0;
        private int paper = 0;
        private bool flashBitOn = false;
        private int bor = 0;

        public override void DiskInsert(string filename, byte _unit) {
            if (diskInserted[_unit])
                wdDrive.DiskEject(_unit);

            base.DiskInsert(filename, _unit);
            wdDrive.DiskInsert(filename, _unit);
            diskInserted[_unit] = true;
        }

        public override void DiskEject(byte _unit) {
            base.DiskEject(_unit);
            wdDrive.DiskEject(_unit);

            diskInserted[_unit] = false;
        }

        //The display offset of the speccy screen wrt to emulator window in horizontal direction.
        //Useful for "centering" unorthodox screen dimensions like the Pentagon that has different left & right border width.
        public override int GetOriginOffsetX() {
            return  -32;
        }

        //The display offset of the speccy screen wrt to emulator window in vertical direction.
        //Useful for "centering" unorthodox screen dimensions like the Pentagon that has different top & bottom border height.
        public override int GetOriginOffsetY() {
            return  32;
        }

        public Pentagon128K(IntPtr handle, bool lateTimingModel)
            : base(handle, lateTimingModel) {
            FrameLength = 71680;
            InterruptPeriod = 36;
            clockSpeed = 3.54690;
            model = MachineModel._pentagon;
            //contentionTable = new byte[72000];
            //floatingBusTable = new short[72000];
            //for (int f = 0; f < 72000; f++)
            //    floatingBusTable[f] = -1;
            int TSTATE_OFFSET = 0;
            CharRows = 24;
            CharCols = 32;
            ScreenWidth = 256;
            ScreenHeight = 192;
            BorderTopHeight = 64 - TSTATE_OFFSET;       //on the pentagon the top border is actually 64
            BorderBottomHeight = 48 + TSTATE_OFFSET;    //on the pentagon the bottom border is actually 48
            BorderLeftWidth = 72 - TSTATE_OFFSET;       //on the pentagon the left border is actually 72
            BorderRightWidth = 56 + TSTATE_OFFSET;      //on the pentagon the right border is actually 56
            DisplayStart = 16384;
            DisplayLength = 6144;
            AttributeStart = 22528;
            AttributeLength = 768;
            borderColour = 0;

            //left border + right border + screenwidth
            ScanLineWidth = BorderLeftWidth + BorderRightWidth + ScreenWidth;

            TstatesPerScanline = 224;
            TstateAtTop = BorderTopHeight * TstatesPerScanline;
            TstateAtBottom = BorderBottomHeight * TstatesPerScanline;
            tstateToDisp = new short[FrameLength + 1000];

            /*
            contentionStartPeriod = 14336 + LateTiming;
            contentionEndPeriod = contentionStartPeriod + (ScreenHeight * TstatesPerScanline); //57324 + LateTiming;

            PagePointer[0] = ROMpage[0];  //128 editor default!
            PagePointer[1] = ROMpage[1];
            PagePointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];  //Bank 5
            PagePointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];  //Bank 5
            PagePointer[4] = RAMpage[(int)RAM_BANK.TWO_1];   //Bank 2
            PagePointer[5] = RAMpage[(int)RAM_BANK.TWO_2];   //Bank 2
            PagePointer[6] = RAMpage[(int)RAM_BANK.ZERO_1];   //Bank 0
            PagePointer[7] = RAMpage[(int)RAM_BANK.ZERO_2];   //Bank 0

            BankInPage0 = ROM_128_BAS;
            BankInPage1 = "Bank 5";
            BankInPage2 = "Bank 2";
            BankInPage3 = "Bank 0";
            contendedBankPagedIn = false;

            pagingDisabled = false;
            showShadowScreen = false;

            Random rand = new Random();

            //Fill memory with zero to simulate hard reset
            for (int i = DisplayStart; i < 65535; ++i)
                PokeByteNoContend(i, 0);

            screen = GetPageData(5); //Bank 5 is a copy of the screen

            screenByteCtr = DisplayStart;
            ULAByteCtr = 0;

            ActualULAStart = 3584;// from Russian FAQ!
            lastTState = ActualULAStart;
            BuildAttributeMap();

            BuildContentionTable();
            aySound.Reset();
            beeper = new ZeroSound.SoundManager(handle, 32, 2, 44100);
            beeper.Play();
            */
            ScreenBuffer = new int[ScanLineWidth * BorderTopHeight //48 lines of border
                                              + ScanLineWidth * ScreenHeight //border + main + border of 192 lines
                                              + ScanLineWidth * BorderBottomHeight]; //56 lines of border

            keyBuffer = new bool[(int)keyCode.LAST];

            attr = new short[DisplayLength];  //6144 bytes of display memory will be mapped
            Reset();

            wdDrive.DiskInitialise();
        }

        public override void Reset() {
            base.Reset();
            PageReadPointer[0] = ROMpage[0];
            PageReadPointer[1] = ROMpage[1];
            PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];  //Bank 5
            PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];  //Bank 5
            PageReadPointer[4] = RAMpage[(int)RAM_BANK.TWO_1];   //Bank 2
            PageReadPointer[5] = RAMpage[(int)RAM_BANK.TWO_2];   //Bank 2
            PageReadPointer[6] = RAMpage[(int)RAM_BANK.ZERO_1];   //Bank 0
            PageReadPointer[7] = RAMpage[(int)RAM_BANK.ZERO_2];   //Bank 0
            PageWritePointer[0] = JunkMemory[0];
            PageWritePointer[1] = JunkMemory[1];
            PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];  //Bank 5
            PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];  //Bank 5
            PageWritePointer[4] = RAMpage[(int)RAM_BANK.TWO_1];   //Bank 2
            PageWritePointer[5] = RAMpage[(int)RAM_BANK.TWO_2];   //Bank 2
            PageWritePointer[6] = RAMpage[(int)RAM_BANK.ZERO_1];   //Bank 0
            PageWritePointer[7] = RAMpage[(int)RAM_BANK.ZERO_2];   //Bank 0
            BankInPage0 = ROM_128_BAS;
            BankInPage1 = "Bank 5";
            BankInPage2 = "Bank 2";
            BankInPage3 = "Bank 0";
            contendedBankPagedIn = false;
            borderColour = 0;
            pagingDisabled = false;
            showShadowScreen = false;
            contentionStartPeriod = 14337;
            contentionEndPeriod = contentionStartPeriod + (ScreenHeight * TstatesPerScanline); //57324 + LateTiming;
            Random rand = new Random();

            //Fill memory with zero to simulate hard reset
            for (int i = DisplayStart; i < DisplayStart + 6912; ++i)
                PokeByteNoContend(i, rand.Next(255));
            screen = GetPageData(5); //Bank 5 is a copy of the screen

            screenByteCtr = DisplayStart;
            ULAByteCtr = 0;

            //3616;
            ActualULAStart = 3616; // 17956 - 36 - (TstatesPerScanline * BorderTopHeight); // ;//3584 from Russian FAQ!
            lastTState = ActualULAStart;
            BuildAttributeMap();

            BuildContentionTable();
        }

        public override void UpdateScreenBuffer(int _tstates) {
            if (_tstates < ActualULAStart) {
                return;
            } else if (_tstates >= FrameLength) {
                _tstates = FrameLength - 1;
                //Since we're updating the entire screen here, we don't need to re-paint
                //it again in the  process loop on framelength overflow.
                needsPaint = true;
            }

            //the additional 1 tstate is required to get correct number of bytes to output in ircontention.sna
            elapsedTStates = (_tstates + 1 - lastTState);
            if (elapsedTStates < 0)
                elapsedTStates = 0;
            //if (model == MachineModel._pentagon)
            {
                //int numPixels = (elapsedTStates << 1);//On the pentagon, there's a 2 pixel resolution

                for (int f = 0; f < elapsedTStates; f++) {
                    //read new screen/border pixels
                    if (tstateToDisp[lastTState] > 1) {
                        if (pixelsWritten == 0) {
                            screenByteCtr = tstateToDisp[lastTState] - 16384; //adjust for actual screen offset

                            pixelData = screen[screenByteCtr];
                            attrData = screen[attr[screenByteCtr] - 16384];

                            lastPixelValue = pixelData;
                            lastAttrValue = attrData;

                            bright = (attrData & 0x40) >> 3;
                            flashBitOn = (attrData & 0x80) != 0;
                            ink = AttrColors[(attrData & 0x07) + bright];
                            paper = AttrColors[((attrData >> 3) & 0x7) + bright];

                            if (flashOn && flashBitOn) //swap paper and ink when flash is on
                                {
                                int temp = ink;
                                ink = paper;
                                paper = temp;
                            }
                        }

                        //for (int g = 0; g < 8; g++)
                        {
                            if ((pixelData & 0x80) != 0) {
                                ScreenBuffer[ULAByteCtr++] = ink;
                            } else {
                                ScreenBuffer[ULAByteCtr++] = paper;
                            }
                            pixelData <<= 1;
                        }

                        if ((pixelData & 0x80) != 0) {
                            ScreenBuffer[ULAByteCtr++] = ink;
                        } else {
                            ScreenBuffer[ULAByteCtr++] = paper;
                        }
                        pixelData <<= 1;

                        pixelsWritten += 2;
                        if (pixelsWritten >= 8)
                            pixelsWritten = 0;

                        lastTState++;
                        //lastTState += 4;
                        //f += 3;
                    } else if (tstateToDisp[lastTState] == 1) {
                        bor = AttrColors[borderColour];

                        ScreenBuffer[ULAByteCtr++] = bor;
                        ScreenBuffer[ULAByteCtr++] = bor;
                        lastTState++;
                    } else
                        lastTState++;
                }
            }
        }

        public override void Shutdown() {
            base.Shutdown();
            wdDrive.DiskShutdown();
        }

        public override void BuildContentionTable() {
            /* int t = contentionStartPeriod;
             while (t < contentionEndPeriod)
             {
                 contentionTable[t++] = 1;
                 contentionTable[t++] = 0;
                 //for 128 t-states
                 for (int i = 2; i < 128; i += 8)
                 {
                     for (int g = 7; g >= 0; g--)
                     {
                         contentionTable[t++] = g;
                     }
                 }
                 t += (TstatesPerScanline - 128);
             }
             */
            //build top half of tstateToDisp table
            //vertical retrace period
            int t = 0;
            for (t = 0; t < ActualULAStart; t++)
                tstateToDisp[t] = 0;

            //next 48 are actual border
            while (t < ActualULAStart + (TstateAtTop)) {
                for (int g = 0; g < ScanLineWidth / 2; g++)
                    tstateToDisp[t++] = 1;

                for (int g = ScanLineWidth / 2; g < TstatesPerScanline; g++)
                    tstateToDisp[t++] = 0;
            }

            //build middle half
            int _x = 0;
            int _y = 0;
            int scrval = 2;
            while (t < ActualULAStart + (TstateAtTop) + (ScreenHeight * TstatesPerScanline)) {
                for (int g = 0; g < BorderLeftWidth / 2; g++)
                    tstateToDisp[t++] = 1;

                for (int g = BorderLeftWidth / 2; g < (BorderLeftWidth + ScreenWidth) / 2; g++) {
                    //Map screenaddr to tstate
                    if (g % 4 == 0) {
                        scrval = (((((_y & 0xc0) >> 3) | (_y & 0x07) | (0x40)) << 8)) | (((_x >> 3) & 0x1f) | ((_y & 0x38) << 2));
                        _x += 8;
                    }
                    tstateToDisp[t++] = (short)scrval;
                }
                _y++;

                for (int g = (BorderLeftWidth + ScreenWidth) / 2; g < (BorderLeftWidth + ScreenWidth + BorderRightWidth) / 2; g++)
                    tstateToDisp[t++] = 1;

                for (int g = (BorderLeftWidth + ScreenWidth + BorderRightWidth) / 2; g < TstatesPerScanline; g++)
                    tstateToDisp[t++] = 0;
            }

            //build bottom half
            while (t < ActualULAStart + (TstateAtTop) + (ScreenHeight * TstatesPerScanline) + (TstateAtBottom)) {
                for (int g = 0; g < ScanLineWidth / 2; g++)
                    tstateToDisp[t++] = 1;

                for (int g = ScanLineWidth / 2; g < TstatesPerScanline; g++)
                    tstateToDisp[t++] = 0;
            }
        }

        public override bool IsContended(int addr) {
            //int addr_hb = (addr & 0xff00) >> 8;
            /*   addr = addr & 0xc000;

               //Low port contention
               if (addr == 0x4000)
                   return true;

               //High port contention
               if ((addr == 0xc000) && contendedBankPagedIn)
                   return true;

               if ((addr == 0x8000) && contendedBankIn8000)
                   return true;
               */
            return false;
        }

        public override int In(int port) {
            base.In(port);
            if (isPlayingRZX) {
                if (rzxInputCount < rzxFrame.inputCount) {
                    rzxIN = rzxFrame.inputs[rzxInputCount++];
                }
                return rzxIN;
            }

            int result = 0xff;

            bool lowBitReset = (port & 0x01) == 0;

            totalTStates++;

            if (trDosPagedIn) {
                if ((port & 0x3) == 0x3) {
                    if ((port & 0x80) == 0) {
                        switch (port & 0x60) {
                            case 0x00:
                                result = wdDrive.ReadStatusReg();
                                break;

                            case 0x20:
                                result = wdDrive.ReadTrackReg();
                                break;

                            case 0x40:
                                result = wdDrive.ReadSectorReg();
                                break;

                            case 0x60:
                                result = wdDrive.ReadDataReg();
                                break;
                        }
                    } else {
                        result = wdDrive.ReadSystemReg();
                    }
                }
            } else   //Kempston joystick
                if ((port & 0xe0) == 0) {
                    if (HasKempstonJoystick && !externalSingleStep) {
                        Contend(port, 1, 3);
                        result = joystickState[(int)JoysticksEmulated.KEMPSTON];
                    }
                } 
                else
                    if (lowBitReset)    //Even address, so get input
                    {
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
                        } else
                            if ((lastFEOut & EAR_BIT) == 0) {
                                result &= ~(TAPE_BIT);
                            } else
                                result |= TAPE_BIT;
                    } else
                        if ((port & 0xc002) == 0xc000) //AY register activate
                        {
                            result = lastAYPortOut;
                        } else if (HasKempstonMouse)//Kempston Mouse
                             {
                                if (port == 64479)
                                    result = MouseX % 0xff;     //X ranges from 0 to 255
                                else if (port == 65503)
                                    result = MouseY % 0xff;     //Y ranges from 0 to 255
                                else if (port == 64223)
                                    result = MouseButton;// MouseButton;
                            }
            totalTStates += 3;
            return (result & 0xff);
        }

        private void Out_7ffd(int val) {
            if ((val & 0x08) != 0) {
                if (!showShadowScreen)
                    UpdateScreenBuffer(totalTStates);

                showShadowScreen = true;
                screen = GetPageData(7); //Bank 7
            } else {
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
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.ZERO_1];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.ZERO_2];
                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.ZERO_1];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.ZERO_2];
                    BankInPage3 = "Bank 0";
                    contendedBankPagedIn = false;
                    break;

                case 1: //Bank 1
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.ONE_1];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.ONE_2];
                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.ONE_1];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.ONE_2];
                    BankInPage3 = "Bank 1";
                    contendedBankPagedIn = false;
                    break;

                case 2: //Bank 2
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.TWO_1];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.TWO_2];
                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.TWO_1];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.TWO_2];
                    BankInPage3 = "Bank 2";
                    contendedBankPagedIn = false;
                    break;

                case 3: //Bank 3
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.THREE_1];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.THREE_2];
                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.THREE_1];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.THREE_2];
                    BankInPage3 = "Bank 3";
                    contendedBankPagedIn = false;
                    break;

                case 4: //Bank 4
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.FOUR_1];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.FOUR_2];
                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.FOUR_1];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.FOUR_2];
                    BankInPage3 = "Bank 4";
                    contendedBankPagedIn = false;
                    break;

                case 5: //Bank 5
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.FIVE_1];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.FIVE_2];
                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.FIVE_1];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.FIVE_2];
                    BankInPage3 = "Bank 5";
                    contendedBankPagedIn = false;
                    break;

                case 6: //Bank 6
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.SIX_1];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.SIX_2];
                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.SIX_1];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.SIX_2];
                    BankInPage3 = "Bank 6";
                    contendedBankPagedIn = false;
                    break;

                case 7: //Bank 7
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.SEVEN_1];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.SEVEN_2];
                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.SEVEN_1];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.SEVEN_2];
                    BankInPage3 = "Bank 7";
                    contendedBankPagedIn = false;
                    break;
            }

            //ROM select
            if (!trDosPagedIn) {
                if ((val & 0x10) != 0) {
                    //48k basic
                    PageReadPointer[0] = ROMpage[2];
                    PageReadPointer[1] = ROMpage[3];
                    PageWritePointer[0] = JunkMemory[0];
                    PageWritePointer[1] = JunkMemory[1];
                    BankInPage0 = ROM_48_BAS;
                    lowROMis48K = true;
                } else {
                    //128k basic
                    PageReadPointer[0] = ROMpage[0];
                    PageReadPointer[1] = ROMpage[1];
                    PageWritePointer[0] = JunkMemory[0];
                    PageWritePointer[1] = JunkMemory[1];
                    BankInPage0 = ROM_128_BAS;
                    lowROMis48K = false;
                }
                PageWritePointer[0] = JunkMemory[0];
                PageWritePointer[1] = JunkMemory[1];
            }

            //Check bit 5 for paging disable
            if ((val & 0x20) != 0) {
                pagingDisabled = true;
            }
        }

        public override void Out(int port, int val) {
            base.Out(port, val);
            bool lowBitReset = (port & 0x01) == 0;

            totalTStates++;

            //ULA activate
            if (lowBitReset)    //Even address, so update ULA
            {
                lastFEOut = val;

                totalTStates++;
                if (borderColour != (val & BORDER_BIT))
                    UpdateScreenBuffer(totalTStates);

                borderColour = val & BORDER_BIT;  //The LSB 3 bits of val hold the border colour
                int beepVal = val & (EAR_BIT);// + MIC_BIT);

                if (!tapeIsPlaying) {
                    if (beepVal != lastSoundOut) {
                        if ((beepVal) == 0) {
                            soundOut = MIN_SOUND_VOL;
                        } else {
                            soundOut = MAX_SOUND_VOL;
                        }
                        lastSoundOut = beepVal;
                    }
                }

                totalTStates += 2;
                return;
            }

            if (trDosPagedIn) {
                if ((port & 0x3) == 0x3) {
                    byte v = (byte)val;
                    if ((port & 0x80) == 0) {
                        switch (port & 0x60) {
                            case 0x00:
                                wdDrive.WriteCommandReg(v, (ushort)PC);
                                if (((v >> 5) & 7) >= 4) {
                                    diskDriveState &= ~(1 << 4);
                                    OnDiskEvent(new DiskEventArgs(diskDriveState));
                                } else {
                                    diskDriveState |= (1 << 4);
                                    OnDiskEvent(new DiskEventArgs(diskDriveState));
                                }
                                totalTStates += 3;
                                break;

                            case 0x20:
                                wdDrive.WriteTrackReg(v);
                                totalTStates += 3;
                                break;

                            case 0x40:
                                wdDrive.WriteSectorReg(v);
                                totalTStates += 3;
                                break;

                            case 0x60:
                                wdDrive.WriteDataReg(v);
                                totalTStates += 3;
                                break;
                        }
                    } else {
                        wdDrive.WriteSystemReg(v);
                        totalTStates += 3;
                    }
                    //totalTStates += 3;
                    return;
                }
            }
            //else
            //AY register activate
            if ((port & 0xC002) == 0xC000) {
                aySound.SelectedRegister = val & 0x0F;
                //totalTStates += 3;
            }
            //else
            //AY data
            if ((port & 0xC002) == 0x8000) {
                lastAYPortOut = val;
                aySound.PortWrite(val);
            }
            //else
            //Memory paging activate
            if ((port & 0x8002) == 0) //Are bits 1 and 15 reset?
            {
                Out_7ffd(val);
            }

            totalTStates += 3;
            return;
        }

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
                byte[] buffer = new byte[16384 * 2];
                int bytesRead = r.Read(buffer, 0, 16384 * 2);

                if (bytesRead == 0)
                    return false; //something bad happened!

                for (int g = 0; g < 4; g++)
                    for (int f = 0; f < 8192; ++f) {
                        ROMpage[g][f] = (buffer[f + 8192 * g]);
                    }
            }
            fs.Close();

            //We'll store the TR DOS rom in the upper ROMPages
            filename = path + "trdos.rom";
            try {
                fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            } catch {
                return false;
            }

            fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            using (BinaryReader r = new BinaryReader(fs)) {
                byte[] buffer = new byte[16384];
                int bytesRead = r.Read(buffer, 0, 16384);

                if (bytesRead == 0)
                    return false; //something bad happened!

                for (int g = 4; g < 6; g++)
                    for (int f = 0; f < 8192; ++f) {
                        ROMpage[g][f] = (buffer[f + 8192 * (g - 4)]);
                    }
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

                //Force ignore re-triggered interrupts when loading SNA. Causes Shock Medademo 128k SNA to work incorrectly otherwise.
                if (IFF1)
                    lastOpcodeWasEI = 1;
                _R = sna.HEADER.R;
                AF = sna.HEADER.AF;
                SP = sna.HEADER.SP;
                interruptMode = sna.HEADER.IM;
                borderColour = sna.HEADER.BORDER;
                PC = ((SNA_128K)sna).PC;

                int val = ((SNA_128K)sna).PORT_7FFD;

                for (int f = 0; f < 16; f++) {
                    Array.Copy(((SNA_128K)sna).RAM_BANK[f], 0, RAMpage[f], 0, 8192);
                }

                PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];
                PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];
                PageReadPointer[4] = RAMpage[(int)RAM_BANK.TWO_1];
                PageReadPointer[5] = RAMpage[(int)RAM_BANK.TWO_2];

                PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];
                PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];
                PageWritePointer[4] = RAMpage[(int)RAM_BANK.TWO_1];
                PageWritePointer[5] = RAMpage[(int)RAM_BANK.TWO_2];

                if (((SNA_128K)sna).TR_DOS) {
                    PageReadPointer[0] = ROMpage[4];
                    PageReadPointer[1] = ROMpage[5];
                    PageWritePointer[0] = JunkMemory[0];
                    PageWritePointer[1] = JunkMemory[1];
                    trDosPagedIn = true;
                    BankInPage0 = ROM_TR_DOS;
                }
                Out(0x7ffd, val); //Perform a dummy Out to setup the remaining stuff!
            }
        }

        public override void UseSZX(SZXLoader szx) {
            lock (lockThis) {
                base.UseSZX(szx);
                aySound.SelectedRegister = szx.ayState.currentRegister;
                aySound.SetRegisters(szx.ayState.chRegs);

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

                Out(0x7ffd, szx.specRegs.x7ffd); //Perform a dummy Out to setup the remaining stuff!
                borderColour = szx.specRegs.Border;
                totalTStates = (int)szx.z80Regs.CyclesStart;
            }
        }

        private uint GetUIntFromString(string data) {
            byte[] carray = System.Text.ASCIIEncoding.UTF8.GetBytes(data);
            uint val = BitConverter.ToUInt32(carray, 0);
            return val;
        }

        /*
        public override void SaveSZX(String filename)
        {
            SZXLoader szx = new SZXLoader();
            szx.header = new SZXLoader.ZXST_Header();
            szx.creator = new SZXLoader.ZXST_Creator();
            szx.z80Regs = new SZXLoader.ZXST_Z80Regs();
            szx.specRegs = new SZXLoader.ZXST_SpecRegs();
            szx.keyboard = new SZXLoader.ZXST_Keyboard();
            szx.ayState = new SZXLoader.ZXST_AYState();

            for (int f = 0; f < 16; f++)
                szx.RAM_BANK[f] = new byte[8192];

            szx.header.MachineId = (byte)SZXLoader.ZXTYPE.ZXSTMID_PENTAGON128;
            szx.header.Magic = GetUIntFromString("ZXST");
            szx.header.MajorVersion = 1;
            szx.header.MinorVersion = 3;
            szx.creator.CreatorName = "Zero Spectrum Emulator by Arjun ".ToCharArray();
            szx.creator.MajorVersion = 0;
            szx.creator.MinorVersion = 5;
            if (Issue2Keyboard)
                szx.keyboard.Flags |= Speccy.SZXLoader.ZXSTKF_ISSUE2;
            szx.keyboard.KeyboardJoystick |= 8;
            szx.z80Regs.AF = (ushort)AF;
            szx.z80Regs.AF1 = (ushort)_AF;
            szx.z80Regs.BC = (ushort)BC;
            szx.z80Regs.BC1 = (ushort)_BC;
            szx.z80Regs.BitReg = (byte)MemPtr;
            szx.z80Regs.CyclesStart = (uint)totalTStates;
            szx.z80Regs.DE = (ushort)DE;
            szx.z80Regs.DE1 = (ushort)_DE;
            if (lastOpcodeWasEI != 0)
                szx.z80Regs.Flags |= Speccy.SZXLoader.ZXSTZF_EILAST;
            if (HaltOn)
                szx.z80Regs.Flags |= Speccy.SZXLoader.ZXSTZF_HALTED;
            szx.z80Regs.HL = (ushort)HL;
            szx.z80Regs.HL1 = (ushort)_HL;
            szx.z80Regs.I = (byte)I;
            szx.z80Regs.IFF1 = (byte)(IFF1 ? 1 : 0);
            szx.z80Regs.IFF1 = (byte)(IFF2 ? 1 : 0);
            szx.z80Regs.IM = (byte)interruptMode;
            szx.z80Regs.IX = (ushort)IX;
            szx.z80Regs.IY = (ushort)IY;
            szx.z80Regs.PC = (ushort)PC;
            szx.z80Regs.R = (byte)R;
            szx.z80Regs.SP = (ushort)SP;
            szx.specRegs.Border = (byte)borderColour;
            szx.specRegs.Fe = (byte)lastFEOut;
            szx.specRegs.pagePort = 0;
            szx.specRegs.x7ffd = (byte)last7ffdOut;
            szx.ayState.cFlags = 0;
            szx.ayState.currentRegister = (byte)aySound.SelectedRegister;
            szx.ayState.chRegs = aySound.GetRegisters();

            for (int f = 0; f < 16; f++)
            {
                Array.Copy(RAMpage[f], 0, szx.RAM_BANK[f], 0, 8192);
            }
            szx.SaveSZX(filename);
        }
        */

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
            Issue2Keyboard = false;

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

            for (int f = 0; f < 16; f++)
                aySound.SetRegisters(z80.AY_REGS);

            Out(0xfffd, z80.PORT_FFFD); //Setup the sound chip
            Out(0x7ffd, z80.PORT_7FFD); //Perform a dummy Out to setup the remaining stuff!

            borderColour = z80.BORDER;
            totalTStates = z80.TSTATES;
        }
    }
}