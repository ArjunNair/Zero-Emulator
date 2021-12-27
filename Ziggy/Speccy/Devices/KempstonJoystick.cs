using SpeccyCommon;

namespace Speccy {
    public class KempstonJoystick : IODevice, SpeccyDevice {
        //The original Kempston used port #1f to decode.
        //Various clones however used port #df.
        public bool UsePort1F = false;

        //Bits: 0 = button 3, 1 = button 2, 3 = button 1, 4 = up, 5 = down, 6 = left, 7 = right
        //On the Kempston, the active bits are set to 1 (high).
        public byte JoystickState = 0;

        public SPECCY_DEVICE DeviceID { get { return SPECCY_DEVICE.KEMPSTON_JOYSTICK; } }

        public bool Responded { get; set; }

        //Respond to top 3 bits (11100000 = port $1f decoding) or bit 5 (port $d4 decoding) being reset.
        public byte In(ushort port) {
            Responded = true;
            if(UsePort1F) {
                if((port & 0x1f) == 0x1f)
                    return JoystickState;
            }
            else if((port & 0xdf) == 0xdf)
                return JoystickState;

            Responded = false;
            return 0xff; 
        }

        public void Reset() {
            JoystickState = 0;
        }
        public void Out(ushort port, byte val) {
            Responded = false;
        }

        public void SetState(byte newState) {
            JoystickState = newState;
        }

        public void RegisterDevice(zx_spectrum speccyModel) {
            speccyModel.io_devices.Remove(this);
            speccyModel.io_devices.Add(this);
        }

        public void UnregisterDevice(zx_spectrum speccyModel) {
            speccyModel.io_devices.Remove(this);
        }

    }
}
