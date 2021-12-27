using DirectInput = Microsoft.DirectX.DirectInput;

namespace ZeroWin
{
    public class MouseController {
        private Microsoft.DirectX.DirectInput.Device mouse = null;
        public int sensitivity = 1;
        Form1 ziggyWin;
        //Mouse mouse = null;
        public int MouseX
        {
            get;
            set;
        }

        public int MouseY
        {
            get;
            set;
        }

        public bool MouseLeftButtonDown
        {
            get;
            set;
        }

        public bool MouseRightButtonDown
        {
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
            if(mouse != null) {

                try {
                    DirectInput.MouseState state = mouse.CurrentMouseState;

                    MouseX = state.X / sensitivity;
                    MouseY = state.Y / sensitivity;
                    byte[] buttons = state.GetMouseButtons();
                    MouseLeftButtonDown = buttons[0] > 0;//state.IsPressed(0);
                    MouseRightButtonDown = buttons[1] > 0;//state.IsPressed(1);
                }
                catch(System.Exception e) {
                    ziggyWin.EnableMouse(false);
                }
            }
        }

        public void Release() {
            if(mouse != null) {
                mouse.Unacquire();
                mouse.Dispose();
            }
            mouse = null;
        }
    }
}
