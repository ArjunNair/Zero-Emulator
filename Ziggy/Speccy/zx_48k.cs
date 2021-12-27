using System;
using System.IO;
using Peripherals;
using SpeccyCommon;
namespace Speccy
{
    //Implements a zx 48k machine
    public class zx_48k : Speccy.zx_spectrum
    {
        public zx_48k(IntPtr handle, bool lateTimingModel)
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

            LastScanlineColor = new int[ScanLineWidth];
            keyBuffer = new bool[(int)keyCode.LAST];

            attr = new short[DisplayLength]; //6144 bytes of display memory will be mapped
            lastSoundOut = 0;
            soundOut = 0;
            averagedSound = 0;
            soundCounter = 0;
            HasAYSound = false;

            Reset(true);
        }

        public override void Reset(bool coldBoot) {
            base.Reset(coldBoot);
            contentionStartPeriod = 14335 + LateTiming;
            contentionEndPeriod = contentionStartPeriod + (ScreenHeight * TstatesPerScanline);//57324 + LateTiming;

            PageReadPointer[0] = ROMpage[0];
            PageReadPointer[1] = ROMpage[1];
            PageReadPointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW]; //Bank 5
            PageReadPointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH]; //Bank 5
            PageReadPointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW]; //Bank 2
            PageReadPointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH]; //Bank 2
            PageReadPointer[6] = RAMpage[(int)RAM_BANK.ZERO_LOW]; //Bank 0
            PageReadPointer[7] = RAMpage[(int)RAM_BANK.ZERO_HIGH]; //Bank 0

            PageWritePointer[0] = JunkMemory[0];
            PageWritePointer[1] = JunkMemory[1];
            PageWritePointer[2] = RAMpage[(int)RAM_BANK.FIVE_LOW]; //Bank 5
            PageWritePointer[3] = RAMpage[(int)RAM_BANK.FIVE_HIGH]; //Bank 5
            PageWritePointer[4] = RAMpage[(int)RAM_BANK.TWO_LOW]; //Bank 2
            PageWritePointer[5] = RAMpage[(int)RAM_BANK.TWO_HIGH]; //Bank 2
            PageWritePointer[6] = RAMpage[(int)RAM_BANK.ZERO_LOW]; //Bank 0
            PageWritePointer[7] = RAMpage[(int)RAM_BANK.ZERO_HIGH]; //Bank 0

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
            foreach (var ad in audio_devices) {
                ad.Reset();
            }
        }

        public override bool IsContended(int addr) {

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
        //            cpu.t_states += contentionTable[cpu.t_states];
        //            cpu.t_states += _time;
        //        }

        //    }
        //    else
        //        cpu.t_states += (_time * _count);
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
        public override byte In(ushort port) {
            base.In(port);

            if (isPlayingRZX) {
                if (rzx.inputCount < rzx.frame.inputCount) {
                    if (rzx.frame.inputs == null) {
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

            ContendPortEarly(port);
            ContendPortLate(port);

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
                if (lowBitReset) {                 //Even address, so get input
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
                    else {
                        if (Issue2Keyboard) {
                            if ((lastFEOut & (EAR_BIT + MIC_BIT)) == 0) {
                                result &= (~(TAPE_BIT) & 0xff);
                            }
                            else
                                result |= TAPE_BIT;
                        }
                        else {
                            if ((lastFEOut & EAR_BIT) == 0) {
                                result &= (~(TAPE_BIT) & 0xff);
                            }
                            else
                                result |= TAPE_BIT;
                        }
                    }
                }
                else {
                    //Unused port, return floating bus
                    int _tstates = cpu.t_states; //the floating bus is read on the last t-state

                    //if we're on the top or bottom border return 0xff
                    if ((_tstates < contentionStartPeriod) || (_tstates > contentionEndPeriod))
                        result = 0xff;
                    else {
                        if (floatingBusTable[_tstates] < 0)
                            result = 0xff;
                        else
                            result = PeekByteNoContend((ushort)(floatingBusTable[_tstates]));
                    }
                }
            }
            cpu.t_states++;
            base.In(port, result);
            return (result);
        }

        // Contention| LowBitReset| Result
        //-----------------------------------------
        // No        | No         | N:4
        // No        | Yes        | N:1 C:3
        // Yes       | Yes        | C:1 C:3
        // Yes       | No         | C:1 C:1 C:1 C:1

        public override void Out(ushort port, byte val) {
            base.Out(port, val);

            bool lowBitReset = ((port & 0x01) == 0);

            //T1
            ContendPortEarly(port);

            if (lowBitReset)    //Even address, so update ULA
            {
                lastFEOut = val;

                if (borderColour != (val & BORDER_BIT))
                    UpdateScreenBuffer(cpu.t_states);

                //needsPaint = true; //useful while debugging as it renders line by line
                borderColour = val & BORDER_BIT;  //The LSB 3 bits of val hold the border colour
                int beepVal = val & (EAR_BIT | MIC_BIT);

                if (!tapeIsPlaying) {

                    if (beepVal != lastSoundOut) {

                        if ((beepVal) == 0) {
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
            else {
                foreach (var d in io_devices) {
                    d.Out(port, val);
                    if (d.Responded) {
                        break;
                    }
                }
            }
            ContendPortLate(port);
            cpu.t_states++;
        }

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

            base.UseSNA(sna);
            if (sna is SNA_48K) {
                lock (lockThis) {
                    int screenAddr = DisplayStart;

                    for (int f = 0; f < 49152; ++f) {
                        PokeByteNoContend(screenAddr + f, ((SNA_48K)sna).RAM[f]);
                    }
                    cpu.regs.PC = PeekWordNoContend(cpu.regs.SP);
                    cpu.regs.SP += 2;
                    //Check if we are halted.
                    if (PeekByteNoContend(cpu.regs.PC) == 0x76) {
                        cpu.is_halted = true;
                        CorrectPCForHalt();
                    }
                }
            }
        }
  
        public override void UseSZX(SZXFile szx) {
            lock (lockThis) {
                base.UseSZX(szx);
                Out(0x0ffe, szx.specRegs.Fe);
                borderColour = szx.specRegs.Border;
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

                if (szx.paletteLoaded) {
                    if (ula_plus == null) {
                        ula_plus = new ULA_Plus();
                        AddDevice(ula_plus);
                    }
                    ula_plus.Enabled = true;
                    if (szx.palette.flags == 1) {                     
                        ula_plus.PaletteEnabled = true;
                    } else if (ula_plus != null) {
                        ula_plus.PaletteEnabled = false;
                    }
                    ula_plus.PaletteGroup = szx.palette.currentRegister;
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
                        ula_plus.Palette[f] = bgr;
                        Out(0xbf3b, szx.specRegs.Fe);
                    }
                }
                cpu.t_states = (int)szx.z80Regs.CyclesStart;
            }
        }

        public override void UseZ80(Z80_SNAPSHOT z80) {
            base.UseZ80(z80);
            lock (lockThis) {

                EnableAY(z80.AY_FOR_48K);

                //Copy AY regs
                if (z80.AY_FOR_48K) {
                    for (int i = 0; i < audio_devices.Count; i++) {
                        if (audio_devices[i] is AY_8192) {
                            AY_8192 ay_device = (AY_8192)(audio_devices[i]);
                            ay_device.SetRegisters(z80.AY_REGS);
                            audio_devices[i] = ay_device;
                        }
                    }

                    Out(0xfffd, z80.PORT_FFFD); //Setup the sound chip
                }

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
            }
        }
    }
}