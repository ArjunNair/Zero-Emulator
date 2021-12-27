using System;
using System.IO;
using Peripherals;
using SpeccyCommon;

namespace Speccy
{
    public class zx_plus3 : Speccy.zx_spectrum
    {
        //Disk emulation related stuff
        protected bool[] diskInserted = { false, false };
        private UDP765 udpDrive = new UDP765();

        public zx_plus3(IntPtr handle, bool lateTimingModel)
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

            ScreenBuffer = new int[ScanLineWidth * BorderTopHeight //48 lines of border
                                              + ScanLineWidth * ScreenHeight //border + main + border of 192 lines
                                              + ScanLineWidth * BorderBottomHeight]; //56 lines of border

            LastScanlineColor = new int[ScanLineWidth];
            keyBuffer = new bool[(int)keyCode.LAST];

            attr = new short[DisplayLength];  //6144 bytes of display memory will be mapped

            EnableAY(true);
            Reset(true);
            udpDrive.DiskInitialise();
        }

        public override void Reset(bool coldBoot)
        {
            lock (lockThis) {
                special64KRAM = false;
                udpDrive.DiskReset();
                base.Reset(coldBoot);

                contentionStartPeriod = 14361;// The +3 didn't have late timings!
                contentionEndPeriod = contentionStartPeriod + (ScreenHeight * TstatesPerScanline); //57324 + LateTiming;

                PageReadPointer[0] = ROMpage[0];
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
                ActualULAStart = 14366 - 24 - (TstatesPerScanline * BorderTopHeight);
                lastTState = ActualULAStart;
                BuildAttributeMap();
                BuildContentionTable();

                foreach (var ad in audio_devices) {
                    ad.Reset();
                }
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

            if (_unit < 2) {
                diskInserted[_unit] = false;
            }
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

        public override byte In(ushort port) {
            base.In(port);

            if (isPlayingRZX) {
                if (rzx.inputCount < rzx.frame.inputCount) {
                    if (rzx.frame.inputs == null) {
                        //TODO: Show message box to the user.
                        System.Windows.Forms.MessageBox.Show("Invalid RZX frame. Expected: " + rzx.frame.inputCount.ToString() + " . Actual: 0", "RZX playback error", System.Windows.Forms.MessageBoxButtons.OK);
                    }
                    else {
                        rzxIN = rzx.frame.inputs[rzx.inputCount];

                    }
                }
                rzx.inputCount++;
                return rzxIN;
            }

            byte result = 0xff;

            bool lowBitReset = (port & 0x01) == 0;

            cpu.t_states++; //T2

            bool device_responded = false;

            if (!lowBitReset) {
                foreach (var d in io_devices) {
                    result = d.In(port);
                    if (d.Responded) {
                        device_responded = true;
                        base.In(port, result);
                    }
                }
            }

            if (!device_responded) {
                if (lowBitReset)    //Even address, so get input
                {
                    if ((port & 0x8000) == 0)
                        result &= (byte)keyLine[7];

                    if ((port & 0x4000) == 0)
                        result &= (byte)keyLine[6];

                    if ((port & 0x2000) == 0)
                        result &= (byte)keyLine[5];

                    if ((port & 0x1000) == 0)
                        result &= (byte)keyLine[4];

                    if ((port & 0x800) == 0)
                        result &= (byte)keyLine[3];

                    if ((port & 0x400) == 0)
                        result &= (byte)keyLine[2];

                    if ((port & 0x200) == 0)
                        result &= (byte)keyLine[1];

                    if ((port & 0x100) == 0)
                        result &= (byte)keyLine[0];

                    result = (byte)(result & 0x1f); //mask out lower 4 bits
                    result = (byte)(result | 0xa0); //set bit 5 & 7 to 1

                    if (tapeIsPlaying) {
                        if (pulseLevel == 0) {
                            result &= (~(TAPE_BIT) & 0xff);    //reset is EAR off
                        }
                        else {
                            result |= (TAPE_BIT); //set is EAR on
                        }
                    }
                    else if ((lastFEOut & EAR_BIT) == 0) {
                        result &= (~(TAPE_BIT) & 0xff);
                    }
                    else
                        result |= TAPE_BIT;
                }
                else {                
                    if ((port & 0xF002) == 0x2000) //Is bit 12 set and bits 13,14,15 and 1 reset?
                        result = udpDrive.DiskStatusRead();
                    else if ((port & 0xF002) == 0x3000)
                        result = udpDrive.DiskReadByte();
                    else if ((port & 0xF002) == 0x0) {
                        if (pagingDisabled)
                            result = 0x1;
                        else
                            result = 0xff;
                    }
                }
            }
            cpu.t_states += 3;
            base.In(port, result);
            return (result);
        }

        private void NormalPaging(int val) {
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
                    contendedBankPagedIn = false;
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
                    contendedBankPagedIn = false;
                    break;

                case 4: //Bank 4
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.FOUR_LOW];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.FOUR_HIGH];

                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.FOUR_LOW];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.FOUR_HIGH];
                    BankInPage3 = "Bank 4";
                    contendedBankPagedIn = true;
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
                    contendedBankPagedIn = true;
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
                    UpdateScreenBuffer(cpu.t_states);

                showShadowScreen = true;
                screen = GetPageData(7); //Bank 7
            } else {
                if (showShadowScreen)
                    UpdateScreenBuffer(cpu.t_states);

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
                        PageReadPointer[0] = RAMpage[(int)RAM_BANK.ZERO_LOW];   //Bank 0
                        PageReadPointer[1] = RAMpage[(int)RAM_BANK.ZERO_HIGH];   //Bank 0
                        PageReadPointer[2] = RAMpage[(int)RAM_BANK.ONE_LOW];  //Bank 1
                        PageReadPointer[3] = RAMpage[(int)RAM_BANK.ONE_HIGH];  //Bank 1
                        PageReadPointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW];   //Bank 2
                        PageReadPointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH];   //Bank 2
                        PageReadPointer[6] = RAMpage[(int)RAM_BANK.THREE_LOW];   //Bank 3
                        PageReadPointer[7] = RAMpage[(int)RAM_BANK.THREE_HIGH];   //Bank 3

                        PageWritePointer[0] = RAMpage[(int)RAM_BANK.ZERO_LOW];   //Bank 0
                        PageWritePointer[1] = RAMpage[(int)RAM_BANK.ZERO_HIGH];   //Bank 0
                        PageWritePointer[2] = RAMpage[(int)RAM_BANK.ONE_LOW];  //Bank 1
                        PageWritePointer[3] = RAMpage[(int)RAM_BANK.ONE_HIGH];  //Bank 1
                        PageWritePointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW];   //Bank 2
                        PageWritePointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH];   //Bank 2
                        PageWritePointer[6] = RAMpage[(int)RAM_BANK.THREE_LOW];   //Bank 3
                        PageWritePointer[7] = RAMpage[(int)RAM_BANK.THREE_HIGH];   //Bank 3
                        BankInPage0 = "Bank 0";
                        BankInPage1 = "Bank 1";
                        BankInPage2 = "Bank 2";
                        BankInPage3 = "Bank 3";
                        contendedBankPagedIn = false;

                        break;

                    case 1:
                        PageReadPointer[0] = RAMpage[(int)RAM_BANK.FOUR_LOW];  //Bank 4
                        PageReadPointer[1] = RAMpage[(int)RAM_BANK.FOUR_HIGH];  //Bank 4
                        PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];  //Bank 5
                        PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];  //Bank 5
                        PageReadPointer[4] = RAMpage[(int)RAM_BANK.SIX_LOW];   //Bank 6
                        PageReadPointer[5] = RAMpage[(int)RAM_BANK.SIX_HIGH];   //Bank 6
                        PageReadPointer[6] = RAMpage[(int)RAM_BANK.SEVEN_LOW];   //Bank 7
                        PageReadPointer[7] = RAMpage[(int)RAM_BANK.SEVEN_HIGH];   //Bank 7

                        PageWritePointer[0] = RAMpage[(int)RAM_BANK.FOUR_LOW];  //Bank 4
                        PageWritePointer[1] = RAMpage[(int)RAM_BANK.FOUR_HIGH];  //Bank 4
                        PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];  //Bank 5
                        PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];  //Bank 5
                        PageWritePointer[4] = RAMpage[(int)RAM_BANK.SIX_LOW];   //Bank 6
                        PageWritePointer[5] = RAMpage[(int)RAM_BANK.SIX_HIGH];   //Bank 6
                        PageWritePointer[6] = RAMpage[(int)RAM_BANK.SEVEN_LOW];   //Bank 7
                        PageWritePointer[7] = RAMpage[(int)RAM_BANK.SEVEN_HIGH];   //Bank 7
                        BankInPage0 = "Bank 4";
                        BankInPage1 = "Bank 5";
                        BankInPage2 = "Bank 6";
                        BankInPage3 = "Bank 7";
                        contendedBankPagedIn = true;

                        break;

                    case 2:
                        PageReadPointer[0] = RAMpage[(int)RAM_BANK.FOUR_LOW];  //Bank 4
                        PageReadPointer[1] = RAMpage[(int)RAM_BANK.FOUR_HIGH];  //Bank 4
                        PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];  //Bank 5
                        PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];  //Bank 5
                        PageReadPointer[4] = RAMpage[(int)RAM_BANK.SIX_LOW];   //Bank 6
                        PageReadPointer[5] = RAMpage[(int)RAM_BANK.SIX_HIGH];   //Bank 6
                        PageReadPointer[6] = RAMpage[(int)RAM_BANK.THREE_LOW];   //Bank 3
                        PageReadPointer[7] = RAMpage[(int)RAM_BANK.THREE_HIGH];   //Bank 3

                        PageWritePointer[0] = RAMpage[(int)RAM_BANK.FOUR_LOW];  //Bank 4
                        PageWritePointer[1] = RAMpage[(int)RAM_BANK.FOUR_HIGH];  //Bank 4
                        PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];  //Bank 5
                        PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];  //Bank 5
                        PageWritePointer[4] = RAMpage[(int)RAM_BANK.SIX_LOW];   //Bank 6
                        PageWritePointer[5] = RAMpage[(int)RAM_BANK.SIX_HIGH];   //Bank 6
                        PageWritePointer[6] = RAMpage[(int)RAM_BANK.THREE_LOW];   //Bank 3
                        PageWritePointer[7] = RAMpage[(int)RAM_BANK.THREE_HIGH];   //Bank 3
                        BankInPage0 = "Bank 4";
                        BankInPage1 = "Bank 5";
                        BankInPage2 = "Bank 6";
                        BankInPage3 = "Bank 3";
                        contendedBankPagedIn = false;

                        break;

                    case 3:
                        PageReadPointer[0] = RAMpage[(int)RAM_BANK.FOUR_LOW];  //Bank 4
                        PageReadPointer[1] = RAMpage[(int)RAM_BANK.FOUR_HIGH];  //Bank 4
                        PageReadPointer[2] = RAMpage[(int)RAM_BANK.SEVEN_LOW];  //Bank 7
                        PageReadPointer[3] = RAMpage[(int)RAM_BANK.SEVEN_HIGH];  //Bank 7
                        PageReadPointer[4] = RAMpage[(int)RAM_BANK.SIX_LOW];   //Bank 6
                        PageReadPointer[5] = RAMpage[(int)RAM_BANK.SIX_HIGH];   //Bank 6
                        PageReadPointer[6] = RAMpage[(int)RAM_BANK.THREE_LOW];   //Bank 3
                        PageReadPointer[7] = RAMpage[(int)RAM_BANK.THREE_HIGH];   //Bank 3

                        PageWritePointer[0] = RAMpage[(int)RAM_BANK.FOUR_LOW];  //Bank 4
                        PageWritePointer[1] = RAMpage[(int)RAM_BANK.FOUR_HIGH];  //Bank 4
                        PageWritePointer[2] = RAMpage[(int)RAM_BANK.SEVEN_LOW];  //Bank 7
                        PageWritePointer[3] = RAMpage[(int)RAM_BANK.SEVEN_HIGH];  //Bank 7
                        PageWritePointer[4] = RAMpage[(int)RAM_BANK.SIX_LOW];   //Bank 6
                        PageWritePointer[5] = RAMpage[(int)RAM_BANK.SIX_HIGH];   //Bank 6
                        PageWritePointer[6] = RAMpage[(int)RAM_BANK.THREE_LOW];   //Bank 3
                        PageWritePointer[7] = RAMpage[(int)RAM_BANK.THREE_HIGH];   //Bank 3
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
                PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];  //Bank 5
                PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];  //Bank 5
                PageReadPointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW];   //Bank 2
                PageReadPointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH];   //Bank 2

                PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];  //Bank 5
                PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];  //Bank 5
                PageWritePointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW];   //Bank 2
                PageWritePointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH];   //Bank 2
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
                    UpdateScreenBuffer(cpu.t_states);

                showShadowScreen = true;
                screen = GetPageData(7); //Bank 7
            } else {
                if (showShadowScreen)
                    UpdateScreenBuffer(cpu.t_states);

                showShadowScreen = false;
                screen = GetPageData(5); //Bank 5
            }
        }

        public override void Out(ushort port, byte val) {
            base.Out(port, val);
            bool lowBitReset = (port & 0x01) == 0;

            cpu.t_states++;

            //ULA activate
            if (lowBitReset)    //Even address, so update ULA
            {
                lastFEOut = val;

                if (borderColour != (val & BORDER_BIT))
                    UpdateScreenBuffer(cpu.t_states);

                cpu.t_states++; //Fix for rotatrix demo!

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
                cpu.t_states += 2;
            } else {
                cpu.t_states += 3;
                bool device_responded = false;
                foreach (var d in io_devices) {
                    d.Out(port, val);
                    if (d.Responded) {
                        device_responded = true;
                        break;
                    }
                }
                if (!device_responded) {
                    //Memory paging activate
                    if ((port & 0xC002) == 0x4000) //Are bits 1 and 15 reset and bit 14 set?
                    {
                        Out_7ffd(val);
                    }
                    else
                    //Extra Memory Paging feature activate
                    if ((port & 0xF002) == 0x1000) //Is bit 12 set and bits 13,14,15 and 1 reset?
                    {
                        Out_1ffd(val);
                    }
                    else if ((port & 0xF002) == 0x3000) //Is bit 12 set and bits 13,14,15 and 1 reset?
                    { 
                        udpDrive.DiskWriteByte((byte)(val & 0xff));
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

            base.UseSNA(sna);
            if (sna is SNA_128K) {
                cpu.regs.PC = ((SNA_128K)sna).PC;
                byte val = ((SNA_128K)sna).PORT_7FFD;

                for (int f = 0; f < 16; f++) {
                    Array.Copy(((SNA_128K)sna).RAM_BANK[f], 0, RAMpage[f], 0, 8192);
                }

                PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];
                PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];
                PageReadPointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW];
                PageReadPointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH];

                PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW];
                PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH];
                PageWritePointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW];
                PageWritePointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH];

                //Check if we are halted.
                if (PeekByteNoContend(cpu.regs.PC) == 0x76) {
                    cpu.is_halted = true;
                    CorrectPCForHalt();
                }

                Out(0x7ffd, val); //Perform a dummy Out to setup the remaining stuff!
            }
        }

        public override void UseSZX(SZXFile szx) {
            lock (lockThis) {
                base.UseSZX(szx);
                for (int i = 0; i < audio_devices.Count; i++) {
                    if (audio_devices[i] is AY_8192) {
                        AY_8192 ay_device = (AY_8192)(audio_devices[i]);
                        ay_device.SelectedRegister = szx.ayState.currentRegister;
                        ay_device.SetRegisters(szx.ayState.chRegs);
                        audio_devices[i] = ay_device;
                    }
                }
                Out(0x0ffe, szx.specRegs.Fe);

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
                contendedBankPagedIn = false;
                Out(0x1ffd, szx.specRegs.pagePort);
                Out(0x7ffd, szx.specRegs.x7ffd); //Perform a dummy Out to setup the remaining stuff!
               
                borderColour = szx.specRegs.Border;
                cpu.t_states = (int)szx.z80Regs.CyclesStart;
            }
        }

        public override void UseZ80(Z80_SNAPSHOT z80) {
            base.UseZ80(z80);

            Issue2Keyboard = false;

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
            contendedBankPagedIn = false;

            //Check if we are halted.
            if (PeekByteNoContend(cpu.regs.PC) == 0x76) {
                cpu.is_halted = true;
                CorrectPCForHalt();
            }

            for (int i = 0; i < audio_devices.Count; i++) {
                if (audio_devices[i] is AY_8192) {
                    AY_8192 ay_device = (AY_8192)(audio_devices[i]);
                    ay_device.SetRegisters(z80.AY_REGS);
                    audio_devices[i] = ay_device;
                }
            }
            Out(0x1ffd, z80.PORT_1FFD); //Perform a dummy Out to setup the remaining stuff!
            Out(0xfffd, z80.PORT_FFFD); //Setup the sound chip
            Out(0x7ffd, z80.PORT_7FFD); //Perform a dummy Out to setup the remaining stuff!
            
        }
    }
}