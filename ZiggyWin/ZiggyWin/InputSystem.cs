using SpeccyCommon;
using Speccy;
using System;

namespace ZeroWin {
    class InputSystem {
        public KempstonJoystick kempstonJoystick = null;
        public KempstonMouse kempstonMouse = null;

        public int Joystick_1_Index = -1;  //Index of the PC joystick selected by user
        public int Joystick_2_Index = -1;  //Index of the PC joystick selected by user
        public int Joystick_1_MapIndex = 0;  //Maps to one of the JoysticksEmulated enum in speccy.cs
        public int Joystick_2_MapIndex = 0;  //Maps to one of the JoysticksEmulated enum in speccy.cs

        public JoystickController Joystick_1 = new JoystickController();
        public JoystickController Joystick_2 = new JoystickController();
        public byte[] Joystick_1_ButtonMap = new byte[0];
        public byte[] Joystick_2_ButtonMap = new byte[0];

        private MouseController mouseController = new MouseController();
        private Form1 ziggyWin;

        public void ReleaseResources() {
            Joystick_1.Release();
            Joystick_2.Release();
            mouseController.Release();
        }

        public void Init(Form1 zw) {
            ziggyWin = zw;
            JoystickController.EnumerateJosticks();
            string[] joysticks = JoystickController.GetDeviceNames();

            if(!string.IsNullOrEmpty(zw.config.inputDeviceOptions.Joystick1Name)) {
                int i = Array.IndexOf(joysticks, zw.config.inputDeviceOptions.Joystick1Name);

                if(i >= 0) {
                    Joystick_1_Index = i;
                    Joystick_1_MapIndex = zw.config.inputDeviceOptions.Joystick1ToEmulate;
                }
            }

            if(!string.IsNullOrEmpty(zw.config.inputDeviceOptions.Joystick2Name)) {
                int i = Array.IndexOf(joysticks, zw.config.inputDeviceOptions.Joystick2Name);

                if(i >= 0) {
                    Joystick_2_Index = i;
                    Joystick_2_MapIndex = zw.config.inputDeviceOptions.Joystick2ToEmulate;
                }
            }
        }

        public void SetMouseSensitivity(int sensitivity) {
            mouseController.sensitivity = sensitivity;
        }

        public void EnableMouse() {
            if (kempstonMouse != null) {
                ziggyWin.zx.RemoveDevice(SPECCY_DEVICE.KEMPSTON_MOUSE);
            }
            kempstonMouse = new KempstonMouse();
            ziggyWin.zx.AddDevice(kempstonMouse);
            mouseController.AcquireMouse(ziggyWin);
        }

        public void ReleaseMouse() {
            ziggyWin.zx.RemoveDevice(SPECCY_DEVICE.KEMPSTON_MOUSE);
            kempstonMouse = null;
            mouseController.Release();
        }

        public void SetupJoysticks() {

            if(Joystick_1_Index >= 0) {
                Joystick_1.Release();
                Joystick_1.InitJoystick(ziggyWin, Joystick_1_Index);
            }

            if(Joystick_2_Index >= 0) {
                Joystick_2.Release();
                Joystick_2.InitJoystick(ziggyWin, Joystick_2_Index);
            }

            if((Joystick_2_MapIndex == (int)zx_spectrum.JoysticksEmulated.KEMPSTON) || (Joystick_1_MapIndex == (int)zx_spectrum.JoysticksEmulated.KEMPSTON)) {
                EnableJoystick();
            }
            else {
                KempstonJoystick kj = new KempstonJoystick();
                ziggyWin.zx.RemoveDevice(SPECCY_DEVICE.KEMPSTON_JOYSTICK);
                kempstonJoystick = null;
            }

            if((ziggyWin.config.inputDeviceOptions.EnableKey2Joy) && (ziggyWin.config.inputDeviceOptions.Key2JoystickType == (int)zx_spectrum.JoysticksEmulated.KEMPSTON)) {
                EnableJoystick();
            }

            ziggyWin.zx.UseKempstonPort1F = ziggyWin.config.inputDeviceOptions.KempstonUsesPort1F;
        }

        public void EnableJoystick() {
            if((Joystick_2_MapIndex == (int)zx_spectrum.JoysticksEmulated.KEMPSTON) || (Joystick_1_MapIndex == (int)zx_spectrum.JoysticksEmulated.KEMPSTON)) {

                kempstonJoystick = new KempstonJoystick();
                kempstonJoystick.UsePort1F = ziggyWin.config.inputDeviceOptions.KempstonUsesPort1F;
                ziggyWin.zx.AddDevice(kempstonJoystick);
            }
            else {
                ziggyWin.zx.RemoveDevice(SPECCY_DEVICE.KEMPSTON_JOYSTICK);
                kempstonJoystick = null;
            }

            if((ziggyWin.config.inputDeviceOptions.EnableKey2Joy) && (ziggyWin.config.inputDeviceOptions.Key2JoystickType == (int)zx_spectrum.JoysticksEmulated.KEMPSTON)) {

                kempstonJoystick = new KempstonJoystick();
                kempstonJoystick.UsePort1F = ziggyWin.config.inputDeviceOptions.KempstonUsesPort1F;
                ziggyWin.zx.AddDevice(kempstonJoystick);
            }

            ziggyWin.zx.UseKempstonPort1F = ziggyWin.config.inputDeviceOptions.KempstonUsesPort1F;
        }

        public void UpdateInputs() {
            if(Joystick_1_Index >= 0) {
                Joystick_1.Update();
                UpdateJoystickInput( Joystick_1, Joystick_1_MapIndex);
            }

            if(Joystick_2_Index >= 0) {
                Joystick_2.Update();
                UpdateJoystickInput( Joystick_2, Joystick_2_MapIndex);
            }

            if( kempstonMouse != null) {
                mouseController.UpdateMouse();
                kempstonMouse.MouseX += (byte)mouseController.MouseX;
                kempstonMouse.MouseY -= (byte)mouseController.MouseY;
                kempstonMouse.MouseButton = 0xff;
                if(mouseController.MouseLeftButtonDown)
                    kempstonMouse.MouseButton = (byte)(kempstonMouse.MouseButton & (~0x2));
                if(mouseController.MouseRightButtonDown)
                    kempstonMouse.MouseButton = (byte)(kempstonMouse.MouseButton & (~0x1));
            }
        }

        public void UpdateJoystickInput(JoystickController joystick, int joystickType) {
            byte[] buttons = joystick.state.GetButtons();
            if(joystickType == (int)zx_spectrum.JoysticksEmulated.KEMPSTON) {
                byte bitf = 0;
                if(joystick.state.X > 100) {
                    bitf |= SpeccyGlobals.JOYSTICK_MOVE_RIGHT;
                }
                else if(joystick.state.X < -100) {
                    bitf |= SpeccyGlobals.JOYSTICK_MOVE_LEFT;
                }

                if(joystick.state.Y > 100)
                    bitf |= SpeccyGlobals.JOYSTICK_MOVE_DOWN;
                else if(joystick.state.Y < -100)
                    bitf |= SpeccyGlobals.JOYSTICK_MOVE_UP;

                if(buttons[joystick.fireButtonIndex] > 0) // IsPressed(joystick.fireButtonIndex))
                    bitf |= SpeccyGlobals.JOYSTICK_BUTTON_1;

                ziggyWin.zx.joystickState[joystickType] = bitf;
                kempstonJoystick.SetState(bitf);
            }
            else if(joystickType == (int)zx_spectrum.JoysticksEmulated.SINCLAIR2) {
                if(joystick.state.X > 100) {
                    ziggyWin.zx.keyBuffer[(int)keyCode._2] = true;
                }
                else if(joystick.state.X < -100) {
                    ziggyWin.zx.keyBuffer[(int)keyCode._1] = true;
                }
                else {
                    ziggyWin.zx.keyBuffer[(int)keyCode._1] = false;
                    ziggyWin.zx.keyBuffer[(int)keyCode._2] = false;
                }

                if(joystick.state.Y < -100) {
                    ziggyWin.zx.keyBuffer[(int)keyCode._4] = true;
                }
                else if(joystick.state.Y > 100) {
                    ziggyWin.zx.keyBuffer[(int)keyCode._3] = true;
                }
                else {
                    ziggyWin.zx.keyBuffer[(int)keyCode._3] = false;
                    ziggyWin.zx.keyBuffer[(int)keyCode._4] = false;
                }

                if(buttons[joystick.fireButtonIndex] > 0)
                    ziggyWin.zx.keyBuffer[(int)keyCode._5] = true;
                else
                    ziggyWin.zx.keyBuffer[(int)keyCode._5] = false;
            }
            else if(joystickType == (int)zx_spectrum.JoysticksEmulated.SINCLAIR1) {
                if(joystick.state.X > 100) {
                    ziggyWin.zx.keyBuffer[(int)keyCode._7] = true;
                }
                else if(joystick.state.X < -100) {
                    ziggyWin.zx.keyBuffer[(int)keyCode._6] = true;
                }
                else {
                    ziggyWin.zx.keyBuffer[(int)keyCode._6] = false;
                    ziggyWin.zx.keyBuffer[(int)keyCode._7] = false;
                }

                if(joystick.state.Y < -100) {
                    ziggyWin.zx.keyBuffer[(int)keyCode._9] = true;
                }
                else if(joystick.state.Y > 100) {
                    ziggyWin.zx.keyBuffer[(int)keyCode._8] = true;
                }
                else {
                    ziggyWin.zx.keyBuffer[(int)keyCode._8] = false;
                    ziggyWin.zx.keyBuffer[(int)keyCode._9] = false;
                }

                if(buttons[joystick.fireButtonIndex] > 0)
                    ziggyWin.zx.keyBuffer[(int)keyCode._0] = true;
                else
                    ziggyWin.zx.keyBuffer[(int)keyCode._0] = false;
            }
            else if(joystickType == (int)zx_spectrum.JoysticksEmulated.CURSOR) {
                if(joystick.state.X > 100) {
                    ziggyWin.zx.keyBuffer[(int)keyCode._8] = true; ;
                }
                else if(joystick.state.X < -100) {
                    ziggyWin.zx.keyBuffer[(int)keyCode._5] = true;
                }
                else {
                    ziggyWin.zx.keyBuffer[(int)keyCode._5] = false;
                    ziggyWin.zx.keyBuffer[(int)keyCode._8] = false;
                }

                if(joystick.state.Y < -100) {
                    ziggyWin.zx.keyBuffer[(int)keyCode._7] = true;
                }
                else if(joystick.state.Y > 100) {
                    ziggyWin.zx.keyBuffer[(int)keyCode._6] = true;
                }
                else {
                    ziggyWin.zx.keyBuffer[(int)keyCode._7] = false;
                    ziggyWin.zx.keyBuffer[(int)keyCode._6] = false;
                }

                if(buttons[joystick.fireButtonIndex] > 0)
                    ziggyWin.zx.keyBuffer[(int)keyCode._0] = true;
                else
                    ziggyWin.zx.keyBuffer[(int)keyCode._0] = false;
            }
        }
    }
}
