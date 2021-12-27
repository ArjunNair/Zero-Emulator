using DirectInput = Microsoft.DirectX.DirectInput;
using System.Runtime.InteropServices;

namespace ZeroWin {
    public class JoystickController {
        public enum EXECUTION_STATE : uint {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
        }

        internal class NativeMethods {
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);
        }

        public DirectInput.Device joystick = null;
        public DirectInput.JoystickState state;
        public static System.Collections.Generic.List<DirectInput.DeviceInstance> joystickList = new System.Collections.Generic.List<DirectInput.DeviceInstance>();
        public string name;
        public bool isInitialized = false;
        private int numPOVs = 0;
        private int SliderCount = 0;

        //public Dictionary<int, int> buttonMap = new Dictionary<int, int>();
        public int[] buttonMap = new int[0];

        //Specifies which button will act as the 'Fire' button.
        //No key will be assigned to this button in the buttonmap list above (i.e. it will remain -1).
        public int fireButtonIndex = 0;

        public static void EnumerateJosticks() {
            joystickList.Clear();
            DirectInput.DeviceList deviceList = DirectInput.Manager.GetDevices(DirectInput.DeviceClass.GameControl, DirectInput.EnumDevicesFlags.AttachedOnly);
            foreach(DirectInput.DeviceInstance di in deviceList) {
                joystickList.Add(di);
            }
        }

        public static string[] GetDeviceNames() {
            // DirectInput dinput = new DirectInput();
            // DirectInput.DeviceList deviceList = DirectInput.Manager.GetDevices(DirectInput.DeviceClass.GameControl, DirectInput.EnumDevicesFlags.AttachedOnly);
            string[] names = new string[joystickList.Count];
            int deviceCount = 0;

            foreach(DirectInput.DeviceInstance di in joystickList) {
                names[deviceCount++] = di.InstanceName;

            }
            return names;
        }

        public bool InitJoystick(Form1 zw, int deviceNum) {
            //DirectInput dinput = new DirectInput();
            //System.Collections.Generic.IList<DeviceInstance> deviceList = dinput.GetDevices(DeviceClass.GameController, DeviceEnumerationFlags.AttachedOnly);
            //DirectInput.DeviceList deviceList = DirectInput.Manager.GetDevices(DirectInput.DeviceClass.GameControl, DirectInput.EnumDevicesFlags.AttachedOnly);
            try {
                joystick = new DirectInput.Device(joystickList[deviceNum].InstanceGuid);
                joystick.SetCooperativeLevel(zw, DirectInput.CooperativeLevelFlags.NonExclusive | DirectInput.CooperativeLevelFlags.Background);
                joystick.SetDataFormat(DirectInput.DeviceDataFormat.Joystick);
                name = joystickList[deviceNum].ProductName;
            }
            catch(Microsoft.DirectX.DirectInput.InputException de) {
                System.Windows.Forms.MessageBox.Show("Couldn't connect to joystick!", "Joystick Problem", System.Windows.Forms.MessageBoxButtons.OK);
                return false;
            }
            foreach(DirectInput.DeviceObjectInstance deviceObject in joystick.Objects) {
                if((deviceObject.ObjectId & (int)DirectInput.DeviceObjectTypeFlags.Axis) != 0)
                    joystick.Properties.SetRange(DirectInput.ParameterHow.ById,
                                                    deviceObject.ObjectId,
                                                    new DirectInput.InputRange(-1000, 1000));

                //joystick.Properties.SetDeadZone(
                //                       DirectInput.ParameterHow.ById,
                //                       deviceObject.ObjectId,
                //                       2000);
            }
            // acquire the device
            try {
                joystick.Acquire();
            }
            catch(Microsoft.DirectX.DirectInput.InputException de) {
                System.Windows.Forms.MessageBox.Show(de.Message, "Joystick Error", System.Windows.Forms.MessageBoxButtons.OK);
                return false;
            }

            //Initially no keys are mapped to buttons on the controller.
            /*for (int f = 0; f < joystick.Caps.NumberButtons; f++) {
                if (!buttonMap.ContainsKey(f))
                    buttonMap.Add(f, -1);
            }*/
            buttonMap = new int[joystick.Caps.NumberButtons];
            for(int f = 0; f < buttonMap.Length; f++)
                buttonMap[f] = -1;
            fireButtonIndex = 0; //Button 0 on the controller is 'fire' by default
            isInitialized = true;
            return true;
        }

        public void Update() {
            if(!isInitialized || joystick == null)
                return;

            //Prevent windows going to sleep. Certain input devices don't seem to hint windows to prevent suspend mode, apparently.
            NativeMethods.SetThreadExecutionState(EXECUTION_STATE.ES_DISPLAY_REQUIRED);
            try {
                joystick.Poll();
                state = joystick.CurrentJoystickState;
            }
            catch(Microsoft.DirectX.DirectInput.InputException de) {
                System.Windows.Forms.MessageBox.Show("The connection to the joystick has been lost.", "Joystick Problem", System.Windows.Forms.MessageBoxButtons.OK);
                isInitialized = false;
            }
        }

        public void Release() {
            if(joystick != null) {
                joystick.Unacquire();
                joystick.Dispose();
            }
            joystick = null;
            isInitialized = false;
            // buttonMap.Clear();
        }
    }
}
