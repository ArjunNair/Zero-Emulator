using SpeccyCommon;

namespace Speccy {
    public class KempstonMouse: IODevice {

        public byte MouseX;
        public byte MouseY;
        public byte MouseButton;

        public SPECCY_DEVICE DeviceID { get { return SPECCY_DEVICE.KEMPSTON_MOUSE; } }
        public bool Responded { get; set; }

        public byte In(ushort port) {
            byte result = 0xff;
            Responded = true;

            if (port == 64479)
                result = (byte)(MouseX % 0xff);     //X ranges from 0 to 255
            else if (port == 65503)
                result = (byte)(MouseY % 0xff);     //Y ranges from 0 to 255
            else if (port == 64223)
                result = MouseButton;// MouseButton;
            else
                Responded = false;

            return result; 
        }

        public void Out(ushort port, byte val) {
            Responded = false;
        }

        public void Reset() {
            MouseX = 0;
            MouseY = 0;
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
