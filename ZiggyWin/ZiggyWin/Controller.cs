using DirectInput = Microsoft.DirectX.DirectInput;

namespace ZeroWin
{
    public class MouseController
    {
        private Microsoft.DirectX.DirectInput.Device mouse = null;
        Form1 ziggyWin;
        //Mouse mouse = null;
        public int MouseX {
            get;
            set;
        }

        public int MouseY {
            get;
            set;
        }

        public bool MouseLeftButtonDown {
            get;
            set;
        }

        public bool MouseRightButtonDown {
            get;
            set;
        }

        public void AcquireMouse(Form1 zw) {
            ziggyWin = zw;
            // DirectInput dinput = new DirectInput();
            //mouse = new Mouse(dinput);
            //CooperativeLevel coopLevel = CooperativeLevel.Exclusive | CooperativeLevel.Foreground;
            mouse = new DirectInput.Device(DirectInput.SystemGuid.Mouse);
            mouse.SetDataFormat(DirectInput.DeviceDataFormat.Mouse);
            DirectInput.CooperativeLevelFlags coopLevel = DirectInput.CooperativeLevelFlags.Exclusive | DirectInput.CooperativeLevelFlags.Foreground;
            mouse.SetCooperativeLevel(ziggyWin, coopLevel);
            mouse.Acquire();
        }

        public void UpdateMouse() {
            if (mouse != null) {

                try
                {
                    DirectInput.MouseState state = mouse.CurrentMouseState;

                    MouseX = state.X;
                    MouseY = state.Y;
                    byte[] buttons = state.GetMouseButtons();
                    MouseLeftButtonDown = buttons[0] > 0;//state.IsPressed(0);
                    MouseRightButtonDown = buttons[1] > 0;//state.IsPressed(1);
                }
                catch (System.Exception e)
                {
                    ziggyWin.EnableMouse(false);
                }
            }
        }

        public void ReleaseMouse() {
            if (mouse != null) {
                mouse.Unacquire();
                mouse.Dispose();
            }
            mouse = null;
        }
    }

    public class JoystickController
    {
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
            foreach (DirectInput.DeviceInstance di in deviceList) {
                joystickList.Add(di);
            }
        }

        public static string[] GetDeviceNames() {
            // DirectInput dinput = new DirectInput();
            // DirectInput.DeviceList deviceList = DirectInput.Manager.GetDevices(DirectInput.DeviceClass.GameControl, DirectInput.EnumDevicesFlags.AttachedOnly);
            string[] names = new string[joystickList.Count];
            int deviceCount = 0;

            foreach (DirectInput.DeviceInstance di in joystickList) {
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
            } catch (Microsoft.DirectX.DirectInput.InputException de) {
                System.Windows.Forms.MessageBox.Show(de.Message, "Joystick Error", System.Windows.Forms.MessageBoxButtons.OK);
                return false;
            }
            foreach (DirectInput.DeviceObjectInstance deviceObject in joystick.Objects) {
                if ((deviceObject.ObjectId & (int)DirectInput.DeviceObjectTypeFlags.Axis) != 0)
                    joystick.Properties.SetRange(DirectInput.ParameterHow.ById,
                                                  deviceObject.ObjectId,
                                                  new DirectInput.InputRange(-1000, 1000));

                //joystick.Properties.SetDeadZone(
                //                       DirectInput.ParameterHow.ById,
                //                       deviceObject.ObjectId,
                //                       2000);
            }
            // acquire the device
            joystick.Acquire();

            //Initially no keys are mapped to buttons on the controller.
            /*for (int f = 0; f < joystick.Caps.NumberButtons; f++) {
                if (!buttonMap.ContainsKey(f))
                    buttonMap.Add(f, -1);
            }*/
            buttonMap = new int[joystick.Caps.NumberButtons];
            for (int f = 0; f < buttonMap.Length; f++)
                buttonMap[f] = -1;
            fireButtonIndex = 0; //Button 0 on the controller is 'fire' by default
            isInitialized = true;
            return true;
        }

        public void Update() {
            //if (joystick.Acquire().IsFailure)
            //    return;

            //if (joystick.Poll().IsFailure)
            //    return;

            state = joystick.CurrentJoystickState;
            //if (SlimDX.Result.Last.IsFailure)
            //    return;
        }

        public void Release() {
            if (joystick != null) {
                joystick.Unacquire();
                joystick.Dispose();
            }
            joystick = null;
            // buttonMap.Clear();
        }

        /*
        public void RemapButtons(Dictionary<int, int> newButtonMap, int newFireButtonIndex)
        {
            buttonMap.Clear();
            foreach (KeyValuePair<int, int> pair in newButtonMap) {
                buttonMap.Add(pair.Key, pair.Value);
            }
            fireButtonIndex = newFireButtonIndex;
        }*/
    }
}