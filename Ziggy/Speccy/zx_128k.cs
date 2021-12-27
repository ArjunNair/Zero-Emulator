using System;
using System.IO;
using Peripherals;
using SpeccyCommon;

namespace Speccy
{
    public class zx_128k : Speccy.zx_spectrum
    {
            public zx_128k(IntPtr handle, bool lateTimingModel)
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

            LastScanlineColor = new int[ScanLineWidth];

            keyBuffer = new bool[(int)keyCode.LAST];
            attr = new short[DisplayLength];  //6144 bytes of display memory will be mapped

            EnableAY(true);
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
            borderColour = 7;

            Random rand = new Random();
            //Fill memory with random stuff to simulate hard reset
            for (int i = DisplayStart; i < DisplayStart + 6912; ++i)
                PokeByteNoContend(i, rand.Next(255));

            screen = GetPageData(5); //Bank 5 is a copy of the screen
            screenByteCtr = DisplayStart;
            ULAByteCtr = 0;

            ActualULAStart = 14366 - 24 - (TstatesPerScanline * BorderTopHeight) + LateTiming;
            lastTState = ActualULAStart;

            BuildAttributeMap();
            BuildContentionTable();

            foreach (var ad in audio_devices) {
                ad.Reset();
            }
            
        }

        public override bool IsContended(int addr) {
            addr = addr & 0xc000;

            //Low port contention
            if (addr == 0x4000)
                return true;

            //High port contention
            if ((addr == 0xc000) && contendedBankPagedIn)
                return true;

            return false;
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

        public override byte In(ushort port) {
            base.In(port);

            if (isPlayingRZX) {
                if(rzx.inputCount < rzx.frame.inputCount) {
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
            bool lowBitReset = ((port & 0x01) == 0);

            ContendPortEarly(port);
            ContendPortLate(port);

            if (lowBitReset) {   //Even address, so get input
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
            else {
                bool device_responded = false;
                foreach (var d in io_devices) {
                    result = d.In(port);
                    if (d.Responded) {
                        device_responded = true;
                        base.In(port, result);
                        break;
                    }
                }

                if (!device_responded) {

                    //return floating bus (also handles port 0x7ffd)
                    int _tstates = cpu.t_states; //floating bus is sampled on the last cycle

                    //if we're on the top or bottom border return 0xff
                    if ((_tstates < contentionStartPeriod) || (_tstates > contentionEndPeriod))
                        result = 0xff;
                    else {
                        if (floatingBusTable[_tstates] < 0)
                            result = 0xff;
                        else
                            result = PeekByteNoContend((ushort)floatingBusTable[_tstates]);
                    }

                    // From the wiki:
                    // Reads from port 0x7ffd cause a crash, as the 128's HAL10H8 chip does not
                    // distinguish between reads and writes to this port, resulting in a floating data bus being used
                    // to set the paging registers.
                    if ((port & 0x8002) == 0) //Memory paging
                    {
                        Out_7ffd(port, result);
                    }
                }
            }
            cpu.t_states++;
          
            base.In(port, result);
            return (result);
        }

        private void Out_7ffd(int port, int val) {
            //Aug 18. 2012
            if ((val & 0x08) != (last7ffdOut & 0x08)) {
                // Needs +2 to get correct ptime.tap result!
                UpdateScreenBuffer(cpu.t_states + 2);

                if (!showShadowScreen) {
                    screen = GetPageData(7); //Bank 7
                }
                else {
                    screen = GetPageData(5); //Bank 5
                }

                showShadowScreen = !showShadowScreen;
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

        public override void Out(ushort port, byte val) {
            base.Out(port, val);
            bool lowBitReset = (port & 0x01) == 0;

            ContendPortEarly(port);

            //ULA activate
            if (lowBitReset)    //Even address, so update ULA
            {
                lastFEOut = val;
                //cpu.t_states += contentionTable[cpu.t_states];
                
                if (borderColour != (val & BORDER_BIT))
                    UpdateScreenBuffer(cpu.t_states);

                borderColour = val & BORDER_BIT;  //The LSB 3 bits of val hold the border colour
                int beepVal = val & (EAR_BIT | MIC_BIT);

                if (!tapeIsPlaying) {
                    if (beepVal != lastSoundOut) {

                        if ((beepVal) == 0)
                            soundOut = MIN_SOUND_VOL;

                        if ((val & EAR_BIT) != 0) {
                            soundOut = MAX_SOUND_VOL;
                        }

                        if ((val & MIC_BIT) != 0)   //Boost slightly if MIC is on
                            soundOut += (short)(MAX_SOUND_VOL * 0.2f);

                        lastSoundOut = beepVal;
                    }
                }
            }
          
            foreach (var d in io_devices) {
                d.Out(port, val);
                if (d.Responded) {
                    break;
                }
            }

            ContendPortLate(port);
            cpu.t_states++;

            if ((port & 0x8002) == 0) //Are bits 1 and 15 reset?
            {
                Out_7ffd(port, val);
            }

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

            base.UseSNA(sna);
            if (sna is SNA_128K) {         
                cpu.regs.PC = ((SNA_128K)sna).PC;
              
                byte val = ((SNA_128K)sna).PORT_7FFD;

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
                for(int i = 0; i < audio_devices.Count; i++) {
                    if (audio_devices[i] is AY_8192) {
                        AY_8192 ay_device = (AY_8192)(audio_devices[i]);
                        ay_device.SelectedRegister = szx.ayState.currentRegister;
                        ay_device.SetRegisters(szx.ayState.chRegs);
                        audio_devices[i] = ay_device;
                    }
                }
                   
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
                cpu.t_states = (int)szx.z80Regs.CyclesStart;
            }
        }

        public override void UseZ80(Z80_SNAPSHOT z80) {
            base.UseZ80(z80);
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