using System;
using System.IO;
using Peripherals;
using SpeccyCommon;

namespace Speccy
{
    public class Pentagon_128k : Speccy.zx_spectrum
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

        private int adjustedScreenHeight = 0;

        int leftBorderOffset = 24;
        int rightBorderOffset = 8;
        int topBorderOffset = 8;
       
        int adjustedBorderLeftWidth = 0;
        int adjustedBorderRightWidth = 0;
        int adjustedBorderTopHeight = 0;
        int adjustedScanlineWidth = 0;

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
            return 0;// -32;
        }

        //The display offset of the speccy screen wrt to emulator window in vertical direction.
        //Useful for "centering" unorthodox screen dimensions like the Pentagon that has different top & bottom border height.
        public override int GetOriginOffsetY() {
            return 0;// 32;
        }

        public override int GetTotalScreenWidth()
        {
            return ScreenWidth +  adjustedBorderLeftWidth + adjustedBorderRightWidth;
        }

        public override int GetTotalScreenHeight()
        {
            return ScreenHeight + adjustedBorderTopHeight + BorderBottomHeight;
        }

        public Pentagon_128k(IntPtr handle, bool lateTimingModel)
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

            adjustedBorderLeftWidth = BorderLeftWidth - leftBorderOffset;
            adjustedBorderRightWidth = BorderRightWidth - rightBorderOffset;
            adjustedBorderTopHeight = BorderTopHeight - topBorderOffset;
            adjustedScanlineWidth = adjustedBorderLeftWidth + adjustedBorderRightWidth + ScreenWidth;
            adjustedScreenHeight = adjustedBorderTopHeight + ScreenHeight + BorderBottomHeight;

            
            ScreenBuffer = new int[adjustedScanlineWidth * adjustedBorderTopHeight //48 lines of border
                                             + adjustedScanlineWidth * ScreenHeight //border + main + border of 192 lines
                                             + adjustedScanlineWidth * BorderBottomHeight]; //56 lines of border

            keyBuffer = new bool[(int)keyCode.LAST];

            attr = new short[DisplayLength];  //6144 bytes of display memory will be mapped

            EnableAY(true);
            Reset(true);

            wdDrive.DiskInitialise();
        }

        public override void Reset(bool coldBoot)
        {
            base.Reset(true);
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
            contendedBankPagedIn = false;
            borderColour = 0;
            pagingDisabled = false;
            showShadowScreen = false;
            contentionStartPeriod = 14337;
            contentionEndPeriod = contentionStartPeriod + (ScreenHeight * TstatesPerScanline); //57324 + LateTiming;
            lowROMis48K = false;
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
            foreach (var ad in audio_devices) {
                ad.Reset();
            }
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

                        if ((pixelData & 0x80) != 0) {
                            ScreenBuffer[ULAByteCtr++] = ink;
                        } else {
                            ScreenBuffer[ULAByteCtr++] = paper;
                        }
                        pixelData <<= 1;

                        if ((pixelData & 0x80) != 0) {
                            ScreenBuffer[ULAByteCtr++] = ink;
                        } else {
                            ScreenBuffer[ULAByteCtr++] = paper;
                        }
                        pixelData <<= 1;

                        pixelsWritten += 2;
                        if (pixelsWritten >= 8)
                            pixelsWritten = 0;

                    } else if (tstateToDisp[lastTState] == 1) {
                        bor = AttrColors[borderColour];

                        ScreenBuffer[ULAByteCtr++] = bor;
                        ScreenBuffer[ULAByteCtr++] = bor;

                    }

                    lastTState++;
                }
            }
        }
        public override void EnableAY(bool val) {
            HasAYSound = val;
            if (val) {
                AY_8192 ay_device = new AY_8192();
                ay_device.UseLastOutForIN = true;
                AddDevice(ay_device);
            }
            else {
                RemoveDevice(SPECCY_DEVICE.AY_3_8912);
            }
        }

        public override void Shutdown() {
            base.Shutdown();
            wdDrive.DiskShutdown();
        }

        public override void BuildContentionTable() {
            //build top half of tstateToDisp table
            //vertical retrace period
            int t = 0;

            for (; t < ActualULAStart; t++)
                tstateToDisp[t] = 0;

            for (; t < ActualULAStart + (topBorderOffset * TstatesPerScanline); t++)
                tstateToDisp[t] = 0;

            //next 48 are actual border
            while (t < ActualULAStart + (TstateAtTop)) {
                int g = 0;
                int k = 0;

                for (; g < leftBorderOffset / 2; g++, k++ )
                    tstateToDisp[t++] = 0;

                for (g = 0; g < adjustedScanlineWidth / 2; g++, k++)
                        tstateToDisp[t++] = 1;

                for (g = 0; g < TstatesPerScanline - 188; g++, k++)
                    tstateToDisp[t++] = 0;
            }

            //build middle half
            int _x = 0;
            int _y = 0;
            int scrval = 2;

            while (t < ActualULAStart + (TstateAtTop) + (ScreenHeight * TstatesPerScanline)) {
                int g = 0;

                for (; g < leftBorderOffset / 2; g++)
                    tstateToDisp[t++] = 0;

                for (g = 0; g < adjustedBorderLeftWidth / 2; g++)
                    tstateToDisp[t++] = 1;

                for (g = 0; g < (ScreenWidth) / 2; g++) {
                    //Map screenaddr to tstate
                    if (g % 4 == 0) {
                        scrval = (((((_y & 0xc0) >> 3) | (_y & 0x07) | (0x40)) << 8)) | (((_x >> 3) & 0x1f) | ((_y & 0x38) << 2));
                        _x += 8;
                    }
                    tstateToDisp[t++] = (short)scrval;
                }
                _y++;

                for (g = 0; g < adjustedBorderRightWidth / 2; g++)
                    tstateToDisp[t++] = 1;

                for (g = 0; g < TstatesPerScanline - 188; g++)
                    tstateToDisp[t++] = 0;
            }

            //build bottom half
            while (t < ActualULAStart + (TstateAtTop) + (ScreenHeight * TstatesPerScanline) + (TstateAtBottom)) {
                int g = 0;
                int k = 0;

                for (; g < leftBorderOffset / 2; g++, k++ )
                    tstateToDisp[t++] = 0;

                for (g = 0; g < adjustedScanlineWidth / 2; g++, k++)
                        tstateToDisp[t++] = 1;

                for (g = 0; g < TstatesPerScanline - 188; g++, k++)
                    tstateToDisp[t++] = 0;
            }
        }

        public override bool IsContended(int addr) {
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

            cpu.t_states++;

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
                    }
                    else
                        result = wdDrive.ReadSystemReg();
                }
            }
            else if (lowBitReset) {   //Even address, so get input
                if (!externalSingleStep) {
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
                }

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
            else
            if (!lowBitReset) {
                foreach (var d in io_devices) {
                    result = d.In(port);
                    if (d.Responded) {
                        base.In(port, result);
                        break;
                    }
                }
            }

            cpu.t_states += 3;
            return (result);
        }

        private void Out_7ffd(int val) {
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
                    contendedBankPagedIn = false;
                    break;

                case 5: //Bank 5
                    PageReadPointer[6] = RAMpage[(int)RAM_BANK.FIVE_LOW];
                    PageReadPointer[7] = RAMpage[(int)RAM_BANK.FIVE_HIGH];
                    PageWritePointer[6] = RAMpage[(int)RAM_BANK.FIVE_LOW];
                    PageWritePointer[7] = RAMpage[(int)RAM_BANK.FIVE_HIGH];
                    BankInPage3 = "Bank 5";
                    contendedBankPagedIn = false;
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

                borderColour = val & BORDER_BIT;  //The LSB 3 bits of val hold the border colour
                int beepVal = val & (EAR_BIT | MIC_BIT);

                if (!tapeIsPlaying) {
                    if (beepVal != lastSoundOut) {
                        if (beepVal == 0) {
                            soundOut = MIN_SOUND_VOL;
                        } 
                        if ((val & EAR_BIT) != 0) {
                            soundOut = MAX_SOUND_VOL;
                        }

                        if ((val & MIC_BIT) != 0)   //Boost slightly if MIC is on
                            soundOut += (short)(MAX_SOUND_VOL * 0.2f);

                        lastSoundOut = beepVal;
                    }
                }
            }

            if (trDosPagedIn) {
                if ((port & 0x3) == 0x3) {
                    byte v = (byte)val;
                    if ((port & 0x80) == 0) {
                        switch (port & 0x60) {
                            case 0x00:
                            wdDrive.WriteCommandReg(v, (ushort)cpu.regs.PC);
                            if (((v >> 5) & 7) >= 4) {
                                diskDriveState &= ~(1 << 4);
                                OnDiskEvent(new DiskEventArgs(diskDriveState));
                            }
                            else {
                                diskDriveState |= (1 << 4);
                                OnDiskEvent(new DiskEventArgs(diskDriveState));
                            }

                            break;

                            case 0x20:
                            wdDrive.WriteTrackReg(v);
                            break;

                            case 0x40:
                            wdDrive.WriteSectorReg(v);
                            break;

                            case 0x60:
                            wdDrive.WriteDataReg(v);
                            break;
                        }
                    }
                    else {
                        wdDrive.WriteSystemReg(v);
                    }
                }
            }

            foreach (var d in io_devices) {
                d.Out(port, val);
            }

            //Memory paging activate
            if ((port & 0x8002) == 0) //Are bits 1 and 15 reset?
            {
                Out_7ffd(val);
            }

            cpu.t_states += 3;
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

                if (((SNA_128K)sna).TR_DOS != 0) {
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

            Out(0xfffd, z80.PORT_FFFD); //Setup the sound chip
            Out(0x7ffd, z80.PORT_7FFD); //Perform a dummy Out to setup the remaining stuff!
        }
    }
}