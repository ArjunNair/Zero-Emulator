using SpeccyCommon;

namespace Speccy
{
    class ULA : IODevice
    {
        protected const int BORDER_BIT = 0x07;
        protected const int EAR_BIT = 0x10;
        protected const int MIC_BIT = 0x08;
        protected const int TAPE_BIT = 0x40;
        public const short MIN_SOUND_VOL = 0;
        public const short MAX_SOUND_VOL = short.MaxValue / 2;

        // Keyboard lines
        protected int[] keyLine = { 255, 255, 255, 255, 255, 255, 255, 255 };
        protected int BorderColour { get; set; }

        // Tape management
        protected bool PulseLevelLow { get; set; }
        public bool TapeEdgeDetected = false;
        public bool TapeBitWasFlipped = false;
        
        public SPECCY_DEVICE DeviceID { get { return SPECCY_DEVICE.ULA_48K; } }

        public bool Responded { get; set; }
        public bool Issue2Keyboard { get; set; }
        public int LastULAOut { get; set; }
        public int LastBeeperOut { get; set; }
        public int BeeperOut { get; set; }

        public byte In(ushort port) {
            byte result = 0xff;
            if ((port & 0x1) == 0) {
                Responded = true;
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

                if (TapeEdgeDetected) {
                    if (PulseLevelLow) {
                        result &= (~(TAPE_BIT) & 0xff);    //reset is EAR off
                    }
                    else {
                        result |= (TAPE_BIT); //set is EAR on
                    }
                }
                else {
                    if (Issue2Keyboard) {
                        if ((LastULAOut & (EAR_BIT + MIC_BIT)) == 0) {
                            result &= (~(TAPE_BIT) & 0xff);
                        }
                        else
                            result |= TAPE_BIT;
                    }
                    else {
                        if ((LastULAOut & EAR_BIT) == 0) {
                            result &= (~(TAPE_BIT) & 0xff);
                        }
                        else
                            result |= TAPE_BIT;
                    }
                }
            }

            return result;
        }

        public void Reset() {

        }

        public void Out(ushort port, byte val) {
            Responded = false;
            if ((port & 0x1) == 0)    //Even address, so update ULA
            {
                Responded = true;
                LastULAOut = val;

                //if (BorderColour != (val & BORDER_BIT))
                //    UpdateScreenBuffer(cpu.t_states);

                //needsPaint = true; //useful while debugging as it renders line by line
                BorderColour = val & BORDER_BIT;  //The LSB 3 bits of val hold the border colour
                int beepVal = val & EAR_BIT;

                if (!TapeEdgeDetected) {

                    if (beepVal != LastBeeperOut) {

                        if ((beepVal) == 0) {
                            BeeperOut = MIN_SOUND_VOL;
                        }
                        else {
                            BeeperOut = MAX_SOUND_VOL;
                        }

                        if ((val & MIC_BIT) != 0)   //Boost slightly if MIC is on
                            BeeperOut += (short)(MAX_SOUND_VOL * 0.2f);

                        LastBeeperOut = beepVal;
                    }
                }
            }
        }

        public void RegisterDevice(zx_spectrum speccyModel) {
            speccyModel.io_devices.Remove(this);
            speccyModel.io_devices.Add(this);
        }

        public void UnregisterDevice(zx_spectrum speccyModel) {
            speccyModel.io_devices.Remove(this);
        }

        public void FlipTapePulseLevel() {
            PulseLevelLow = !PulseLevelLow;
            TapeBitWasFlipped = true;
            BeeperOut = PulseLevelLow ? short.MinValue >> 1 : 0;
        }
    }
}
