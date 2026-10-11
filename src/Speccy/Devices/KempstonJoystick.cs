using SpeccyCommon;

namespace Speccy {
    public class KempstonJoystick : IODevice {
        //The original Kempston used port #1f to decode.
        //Various clones however used port #df.
        public bool UsePort1F = false;

        //Bits: 0 = button 3, 1 = button 2, 3 = button 1, 4 = up, 5 = down, 6 = left, 7 = right
        //On the Kempston, the active bits are set to 1 (high).
        public byte JoystickState = 0;

        public SPECCY_DEVICE DeviceID { get { return SPECCY_DEVICE.KEMPSTON_JOYSTICK; } }
        public int Priority => UsePort1F ? 50: 0;

        public bool Responded { get; set; }

        //Respond to top 3 bits (11100000 = port $1f decoding) or bit 5 (port $d4 decoding) being reset.
        public byte In(ushort port) {
            Responded = true;
            if(UsePort1F) {
                if((port & 0xe0) == 0)      //A5, A6 and A7 all low: a fully decoded $1F
                    return JoystickState;
            }
            else if((port & 0x20) == 0)     //A5 low only, as the one-chip interface did: $1F and $DF both answer
                return JoystickState;

            Responded = false;
            return 0xff; 
        }

        public void Reset() {
            JoystickState = 0;
        }
        public void Out(ushort port, byte val) {
            if(!UsePort1F && (port & 0x20) == 0)     //A5 low only, as the one-chip interface did: $1F and $DF both answer
                Responded = true;
            else
                Responded = false;
        }

        public void SetState(byte newState) {
            JoystickState = newState;
        }

        public void UnregisterDevice(zx_spectrum speccyModel) {
            speccyModel.io_devices.Remove(this);
        }

    }
}
