using System;
using System.IO;
using Peripherals;

namespace Speccy
{
    public class zxPlus3 : Speccy.zxmachine
    {
        //Disk emulation related stuff
        protected bool[] diskInserted = { false, false };
        private UDP765 udpDrive = new UDP765();

        public zxPlus3(IntPtr handle, bool lateTimingModel)
            : base(handle, lateTimingModel) {
            FrameLength = 70908;
            InterruptPeriod = 32;
            clockSpeed = 3.54690;
            model = MachineModel._plus3;

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
            borderColour = 0;
            ScanLineWidth = BorderLeftWidth + ScreenWidth + BorderRightWidth;

            TstatesPerScanline = 228;
            TstateAtTop = BorderTopHeight * TstatesPerScanline;
            TstateAtBottom = BorderBottomHeight * TstatesPerScanline;
            tstateToDisp = new short[FrameLength];
            /*
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

            BankInPage0 = ROM_128_BAS;
            BankInPage1 = "Bank 5";
            BankInPage2 = "Bank 2";
            BankInPage3 = "Bank 0";
            lowROMis48K = false;
            contendedBankPagedIn = false;
            contendedBankIn8000 = false;

            pagingDisabled = false;
            showShadowScreen = false;

            Random rand = new Random();

            //Fill memory with zero to simulate hard reset
            for (int i = DisplayStart; i < 65535; ++i)
                PokeByteNoContend(i, 0);

            screen = GetPageData(5); //Bank 5 is a copy of the screen

            screenByteCtr = DisplayStart;
            ULAByteCtr = 0;

            ActualULAStart = 14364 - 24 - (TstatesPerScanline * BorderTopHeight);
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
            udpDrive.DiskInitialise();
        }

        public override void Reset() {
            lock (lockThis) {
                special64KRAM = false;
                udpDrive.DiskReset();
                base.Reset();

                contentionStartPeriod = 14360;// The +3 didn't have late timings!
                contentionEndPeriod = contentionStartPeriod + (ScreenHeight * TstatesPerScanline); //57324 + LateTiming;

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
                lowROMis48K = false;
                contendedBankPagedIn = false;

                borderColour = 0;
                pagingDisabled = false;
                // Out(0x1ffd, 0);
                showShadowScreen = false;

                Random rand = new Random();

                //Fill memory with zero to simulate hard reset
                for (int i = DisplayStart; i < DisplayStart + 6912; ++i)
                    PokeByteNoContend(i, rand.Next(255));

                screen = GetPageData(5); //Bank 5 is a copy of the screen
                screenByteCtr = DisplayStart;
                ULAByteCtr = 0;
                ActualULAStart = 14365 - 24 - (TstatesPerScanline * BorderTopHeight);
                lastTState = ActualULAStart;
                BuildAttributeMap();

                BuildContentionTable();
            }
        }

        public override void DiskInsert(string filename, byte _unit) {
            if (diskInserted[_unit])
                udpDrive.DiskEject(_unit);

            base.DiskInsert(filename, _unit);
            udpDrive.DiskInsert(filename, _unit);
            diskInserted[_unit] = true;
        }

        public override void DiskEject(byte _unit) {
            base.DiskEject(_unit);
            udpDrive.DiskEject(_unit);

            diskInserted[_unit] = false;
        }

        public override void Shutdown() {
            base.Shutdown();
            udpDrive.DiskShutdown();
        }

        public override void BuildContentionTable() {
            int t = contentionStartPeriod;
            while (t < contentionEndPeriod) {
                contentionTable[t++] = 1;
                contentionTable[t++] = 0;
                //for 128 t-states
                for (int i = 0; i < 128; i += 8) {
                    contentionTable[t++] = 7;
                    contentionTable[t++] = 6;
                    contentionTable[t++] = 5;
                    contentionTable[t++] = 4;
                    contentionTable[t++] = 3;
                    contentionTable[t++] = 2;
                    contentionTable[t++] = 1;
                    contentionTable[t++] = 0;
                }
                t += (TstatesPerScanline - 128) - 2;
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

            totalTStates++; //T2

            //Kempston joystick
            if ((port & 0xe0) == 0) {
                if (HasKempstonJoystick && !externalSingleStep) {
                    Contend(port, 1, 3);
                    result = joystickState[(int)JoysticksEmulated.KEMPSTON];
                }
            } else
                if (lowBitReset)    //Even address, so get input
            {
                    // totalTStates += 3;

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
                        if ((lastFEOut & 0x10) == 0) {
                            result &= ~(0x40);
                        } else
                            result |= 0x40;
                } else {
                    //totalTStates += 3;
                    if ((port & 0xc002) == 0xc000) //AY register activate on Port FFFD
                {
                        result = aySound.PortRead();
                    } else
                        if ((port & 0xc002) == 0x8000) //Port BFFD also activates AY on the +3/2A
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
                            //Disk status read
                            if ((port & 0xF002) == 0x2000) //Is bit 12 set and bits 13,14,15 and 1 reset?
                {
                                result = udpDrive.DiskStatusRead();
                            } else
                                //Disk read byte
                                if ((port & 0xF002) == 0x3000) {
                                    result = udpDrive.DiskReadByte();
                                } else if ((port & 0xF002) == 0x0) {
                                    if (pagingDisabled)
                                        result = 0x1;
                                    else
                                        result = 0xff;
                                }
                }
            totalTStates += 3;
            base.In(port, result & 0xff);
            return (result & 0xff);
        }

        private void NormalPaging(int val) {
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
                    contendedBankPagedIn = true;
                    break;

                case 5: //Bank 5
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.FIVE_1];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.FIVE_2];

                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.FIVE_1];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.FIVE_2];
                    BankInPage3 = "Bank 5";
                    contendedBankPagedIn = true;
                    break;

                case 6: //Bank 6
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.SIX_1];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.SIX_2];

                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.SIX_1];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.SIX_2];
                    BankInPage3 = "Bank 6";
                    contendedBankPagedIn = true;
                    break;

                case 7: //Bank 7
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.SEVEN_1];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.SEVEN_2];

                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.SEVEN_1];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.SEVEN_2];
                    BankInPage3 = "Bank 7";
                    contendedBankPagedIn = true;
                    break;
            }

            //ROM select
            int romSelect = ((last1ffdOut & 0x04) >> 1) | ((val >> 4) & 0x01);
            PageReadPointer[0] = ROMpage[romSelect * 2];
            PageReadPointer[1] = ROMpage[romSelect * 2 + 1];

            PageWritePointer[0] = JunkMemory[0];
            PageWritePointer[1] = JunkMemory[1];
            if (romSelect == 3)
                lowROMis48K = true;
            else
                lowROMis48K = false;
            //Debugging string for monitor
            BankInPage0 = (romSelect > 1 ? romSelect == 3 ? ROM_48_BAS : ROM_PLUS3_DOS : romSelect == 1 ? ROM_128_SYN : ROM_128_BAS);

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
        }

        private void Out_1ffd(int val) {
            if (pagingDisabled)
                return;

            last1ffdOut = val;

            udpDrive.DiskMotorState((byte)(val & 0xff));

            if ((val & 0x8) != 0) {
                diskDriveState |= (1 << 4);
                OnDiskEvent(new DiskEventArgs(diskDriveState));
            } else {
                diskDriveState &= ~(1 << 4);
                OnDiskEvent(new DiskEventArgs(diskDriveState));
            }

            if ((val & 0x1) != 0) //Paging mode = special?
            {
                special64KRAM = true;
                lowROMis48K = false;
                int mapSelect = (val & 0x6) >> 1;
                switch (mapSelect) {
                    case 0:
                        PageReadPointer[0] = RAMpage[(int)RAM_BANK.ZERO_1];   //Bank 0
                        PageReadPointer[1] = RAMpage[(int)RAM_BANK.ZERO_2];   //Bank 0
                        PageReadPointer[2] = RAMpage[(int)RAM_BANK.ONE_1];  //Bank 1
                        PageReadPointer[3] = RAMpage[(int)RAM_BANK.ONE_2];  //Bank 1
                        PageReadPointer[4] = RAMpage[(int)RAM_BANK.TWO_1];   //Bank 2
                        PageReadPointer[5] = RAMpage[(int)RAM_BANK.TWO_2];   //Bank 2
                        PageReadPointer[6] = RAMpage[(int)RAM_BANK.THREE_1];   //Bank 3
                        PageReadPointer[7] = RAMpage[(int)RAM_BANK.THREE_2];   //Bank 3

                        PageWritePointer[0] = RAMpage[(int)RAM_BANK.ZERO_1];   //Bank 0
                        PageWritePointer[1] = RAMpage[(int)RAM_BANK.ZERO_2];   //Bank 0
                        PageWritePointer[2] = RAMpage[(int)RAM_BANK.ONE_1];  //Bank 1
                        PageWritePointer[3] = RAMpage[(int)RAM_BANK.ONE_2];  //Bank 1
                        PageWritePointer[4] = RAMpage[(int)RAM_BANK.TWO_1];   //Bank 2
                        PageWritePointer[5] = RAMpage[(int)RAM_BANK.TWO_2];   //Bank 2
                        PageWritePointer[6] = RAMpage[(int)RAM_BANK.THREE_1];   //Bank 3
                        PageWritePointer[7] = RAMpage[(int)RAM_BANK.THREE_2];   //Bank 3
                        BankInPage0 = "Bank 0";
                        BankInPage1 = "Bank 1";
                        BankInPage2 = "Bank 2";
                        BankInPage3 = "Bank 3";
                        contendedBankPagedIn = false;

                        break;

                    case 1:
                        PageReadPointer[0] = RAMpage[(int)RAM_BANK.FOUR_1];  //Bank 4
                        PageReadPointer[1] = RAMpage[(int)RAM_BANK.FOUR_2];  //Bank 4
                        PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];  //Bank 5
                        PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];  //Bank 5
                        PageReadPointer[4] = RAMpage[(int)RAM_BANK.SIX_1];   //Bank 6
                        PageReadPointer[5] = RAMpage[(int)RAM_BANK.SIX_2];   //Bank 6
                        PageReadPointer[6] = RAMpage[(int)RAM_BANK.SEVEN_1];   //Bank 7
                        PageReadPointer[7] = RAMpage[(int)RAM_BANK.SEVEN_2];   //Bank 7

                        PageWritePointer[0] = RAMpage[(int)RAM_BANK.FOUR_1];  //Bank 4
                        PageWritePointer[1] = RAMpage[(int)RAM_BANK.FOUR_2];  //Bank 4
                        PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];  //Bank 5
                        PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];  //Bank 5
                        PageWritePointer[4] = RAMpage[(int)RAM_BANK.SIX_1];   //Bank 6
                        PageWritePointer[5] = RAMpage[(int)RAM_BANK.SIX_2];   //Bank 6
                        PageWritePointer[6] = RAMpage[(int)RAM_BANK.SEVEN_1];   //Bank 7
                        PageWritePointer[7] = RAMpage[(int)RAM_BANK.SEVEN_2];   //Bank 7
                        BankInPage0 = "Bank 4";
                        BankInPage1 = "Bank 5";
                        BankInPage2 = "Bank 6";
                        BankInPage3 = "Bank 7";
                        contendedBankPagedIn = true;

                        break;

                    case 2:
                        PageReadPointer[0] = RAMpage[(int)RAM_BANK.FOUR_1];  //Bank 4
                        PageReadPointer[1] = RAMpage[(int)RAM_BANK.FOUR_2];  //Bank 4
                        PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];  //Bank 5
                        PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];  //Bank 5
                        PageReadPointer[4] = RAMpage[(int)RAM_BANK.SIX_1];   //Bank 6
                        PageReadPointer[5] = RAMpage[(int)RAM_BANK.SIX_2];   //Bank 6
                        PageReadPointer[6] = RAMpage[(int)RAM_BANK.THREE_1];   //Bank 3
                        PageReadPointer[7] = RAMpage[(int)RAM_BANK.THREE_2];   //Bank 3

                        PageWritePointer[0] = RAMpage[(int)RAM_BANK.FOUR_1];  //Bank 4
                        PageWritePointer[1] = RAMpage[(int)RAM_BANK.FOUR_2];  //Bank 4
                        PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];  //Bank 5
                        PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];  //Bank 5
                        PageWritePointer[4] = RAMpage[(int)RAM_BANK.SIX_1];   //Bank 6
                        PageWritePointer[5] = RAMpage[(int)RAM_BANK.SIX_2];   //Bank 6
                        PageWritePointer[6] = RAMpage[(int)RAM_BANK.THREE_1];   //Bank 3
                        PageWritePointer[7] = RAMpage[(int)RAM_BANK.THREE_2];   //Bank 3
                        BankInPage0 = "Bank 4";
                        BankInPage1 = "Bank 5";
                        BankInPage2 = "Bank 6";
                        BankInPage3 = "Bank 3";
                        contendedBankPagedIn = false;

                        break;

                    case 3:
                        PageReadPointer[0] = RAMpage[(int)RAM_BANK.FOUR_1];  //Bank 4
                        PageReadPointer[1] = RAMpage[(int)RAM_BANK.FOUR_2];  //Bank 4
                        PageReadPointer[2] = RAMpage[(int)RAM_BANK.SEVEN_1];  //Bank 7
                        PageReadPointer[3] = RAMpage[(int)RAM_BANK.SEVEN_2];  //Bank 7
                        PageReadPointer[4] = RAMpage[(int)RAM_BANK.SIX_1];   //Bank 6
                        PageReadPointer[5] = RAMpage[(int)RAM_BANK.SIX_2];   //Bank 6
                        PageReadPointer[6] = RAMpage[(int)RAM_BANK.THREE_1];   //Bank 3
                        PageReadPointer[7] = RAMpage[(int)RAM_BANK.THREE_2];   //Bank 3

                        PageWritePointer[0] = RAMpage[(int)RAM_BANK.FOUR_1];  //Bank 4
                        PageWritePointer[1] = RAMpage[(int)RAM_BANK.FOUR_2];  //Bank 4
                        PageWritePointer[2] = RAMpage[(int)RAM_BANK.SEVEN_1];  //Bank 7
                        PageWritePointer[3] = RAMpage[(int)RAM_BANK.SEVEN_2];  //Bank 7
                        PageWritePointer[4] = RAMpage[(int)RAM_BANK.SIX_1];   //Bank 6
                        PageWritePointer[5] = RAMpage[(int)RAM_BANK.SIX_2];   //Bank 6
                        PageWritePointer[6] = RAMpage[(int)RAM_BANK.THREE_1];   //Bank 3
                        PageWritePointer[7] = RAMpage[(int)RAM_BANK.THREE_2];   //Bank 3
                        BankInPage0 = "Bank 4";
                        BankInPage1 = "Bank 7";
                        BankInPage2 = "Bank 6";
                        BankInPage3 = "Bank 3";
                        contendedBankPagedIn = false;

                        break;
                }
            } else //normal mode
            {
                special64KRAM = false;
                //bit 2 of val is high bit and bit 4 of 0x7ffd out is low bit
                //int romSelect = ((val & 0x04) >> 1) | ((last7ffdOut & 0x10) >> 4);
                //PagePointer[0] = ROMpage[romSelect * 2];
                //PagePointer[1] = ROMpage[romSelect * 2 + 1];
                PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];  //Bank 5
                PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];  //Bank 5
                PageReadPointer[4] = RAMpage[(int)RAM_BANK.TWO_1];   //Bank 2
                PageReadPointer[5] = RAMpage[(int)RAM_BANK.TWO_2];   //Bank 2

                PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_1];  //Bank 5
                PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_2];  //Bank 5
                PageWritePointer[4] = RAMpage[(int)RAM_BANK.TWO_1];   //Bank 2
                PageWritePointer[5] = RAMpage[(int)RAM_BANK.TWO_2];   //Bank 2
                BankInPage1 = "Bank 5";
                BankInPage2 = "Bank 2";

                NormalPaging(last7ffdOut);
            }
        }

        private void Out_7ffd(int val) {
            if (pagingDisabled)
                return;

            last7ffdOut = val;

            //Check bit 5 for paging disable
            if ((val & 0x20) != 0) {
                pagingDisabled = true;
            }

            if (!special64KRAM) {
                NormalPaging(val);
                return;
            }
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
        }

        public override void Out(int port, int val) {
            base.Out(port, val);
            bool lowBitReset = (port & 0x01) == 0;

            totalTStates++;

            //ULA activate
            if (lowBitReset)    //Even address, so update ULA
            {
                lastFEOut = val;

                if (borderColour != (val & BORDER_BIT))
                    UpdateScreenBuffer(totalTStates);

                totalTStates++; //Fix for rotatrix demo!

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
            } else {
                //AY register activate
                if ((port & 0xC002) == 0xC000) {
                    aySound.SelectedRegister = val & 0x0F;
                } else
                    //AY data
                    if ((port & 0xC002) == 0x8000) {
                        lastAYPortOut = val;
                        aySound.PortWrite(val);
                    } else
                        //Memory paging activate
                        if ((port & 0xC002) == 0x4000) //Are bits 1 and 15 reset and bit 14 set?
                        {
                            Out_7ffd(val);
                        } else
                            //Extra Memory Paging feature activate
                            if ((port & 0xF002) == 0x1000) //Is bit 12 set and bits 13,14,15 and 1 reset?
                            {
                                Out_1ffd(val);
                            } else
                                //Disk write port
                                if ((port & 0xF002) == 0x3000) //Is bit 12 set and bits 13,14,15 and 1 reset?
                                {
                                    udpDrive.DiskWriteByte((byte)(val & 0xff));
                                } else
                                    //ULA Plus
                                    if (port == 0xbf3b) {
                                        int mode = (val & 0xc0) >> 6;

                                        //mode group
                                        if (mode == 1) {
                                            ULAGroupMode = 1;
                                        } else if (mode == 0) //palette group
                    {
                                            ULAGroupMode = 0;
                                            ULAPaletteGroup = val & 0x3f;
                                        }
                                    } else
                                        if (port == 0xff3b) {
                                            if (lastULAPlusOut != val)
                                                UpdateScreenBuffer(totalTStates);

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
                                        }
            }
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
                byte[] buffer = new byte[65536];
                int bytesRead = r.Read(buffer, 0, 65536);

                if ((bytesRead == 0) || (bytesRead < 65536))
                    return false; //something bad happened!

                for (int g = 0; g < 8; g++)
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

                Out(0x7ffd, val); //Perform a dummy Out to setup the remaining stuff!
            }
        }

        public override void UseSZX(SZXLoader szx) {
            lock (lockThis) {
                base.UseSZX(szx);
                aySound.SelectedRegister = szx.ayState.currentRegister;
                aySound.SetRegisters(szx.ayState.chRegs);
                Out(0x0ffe, szx.specRegs.Fe);

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
                contendedBankPagedIn = false;
                Out(0x7ffd, szx.specRegs.x7ffd); //Perform a dummy Out to setup the remaining stuff!
                Out(0x1ffd, szx.specRegs.pagePort);
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

            szx.header.MachineId = (byte)SZXLoader.ZXTYPE.ZXSTMID_PLUS3;
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
            szx.specRegs.pagePort = (byte)last1ffdOut;

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
            Issue2Keyboard = z80.ISSUE2;

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
            contendedBankPagedIn = false;
            for (int f = 0; f < 16; f++)
                aySound.SetRegisters(z80.AY_REGS);

            Out(0xfffd, z80.PORT_FFFD); //Setup the sound chip
            Out(0x7ffd, z80.PORT_7FFD); //Perform a dummy Out to setup the remaining stuff!
            Out(0x1ffd, z80.PORT_1FFD); //Perform a dummy Out to setup the remaining stuff!

            borderColour = z80.BORDER;
            totalTStates = z80.TSTATES;
        }
    }
}