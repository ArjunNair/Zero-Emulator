using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Speccy;

namespace ZeroWin
{
    public enum MachineModel
    {
        _16k,
        _48k,
        _128k,
        _128ke,
        _plus2,
        _plus3,
        _pentagon
    };

    public partial class Form1 : Form
    {
        //static Font contextMenuFont = new Font("Tahoma",8);

        ZRenderer dxWindow;
        Monitor debugger;
        Options optionWindow;
        AboutBox1 aboutWindow;
        //ZLibrary library;
        TapeDeck tapeDeck;
        LoadBinary loadBinaryDialog;
        Trainer_Wizard trainerWiz;
        Infoseeker infoseekWiz;
        public ZeroConfig config = new ZeroConfig();
        private PrecisionTimer timer = new PrecisionTimer();
        private ComponentAce.Compression.ZipForge.ZipForge archiver;
        private MouseController mouse = new MouseController();

        private JoystickController joystick1 = new JoystickController();
        private JoystickController joystick2 = new JoystickController();

        private int joystick1Index = -1;     //Index of the PC joystick selected by user
        private int joystick2Index = -1;     //Index of the PC joystick selected by user

        private int joystick1MapIndex = 0;  //Maps to one of the JoysticksEmulated enum in speccy.cs
        private int joystick2MapIndex = 0;  //Maps to one of the JoysticksEmulated enum in speccy.cs
       
        public Speccy.zxmachine zx;
    
        bool capsLockOn = false;
        bool shiftIsPressed = false;
        bool altIsPressed = false;
        bool ctrlIsPressed = false;

        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LCTRL = 0xA2;
        private const int VK_RCTRL = 0xA3;
        private const int VK_PRNTSCRN = 0x2C;
        private const int VK_LEFT = 0x25;
        private const int VK_UP = 0x26;
        private const int VK_RIGHT = 0x27;
        private const int VK_DOWN = 0x28;
        private const int VK_ALT = 0xA4;

        private int frameCount = 0;
        private double lastTime = 0;
        private double fpsFrame = 0;

        public bool invokeMonitor = false;
        public bool pauseEmulation = false;
        public bool tapeFastLoad = false;
        private Point mouseOrigin;
        private Point mouseMoveDiff;
        private Point mouseOldPos;
        private Point oldWindowPosition = new Point();
        private int oldWindowSize = -1;
        public String recentFolder = ".";

        //LED Indicator states
        public bool showTapeIndicator = false;
        public bool showDiskIndicator = false;
        public bool showDownloadIndicator = false;
        private int downloadIndicatorTimeout = 0;

        bool romLoaded = false;
        private string[] diskArchivePath = {null, null, null, null};    //Any temp disk file created by the archiver from a .zip file

        //The grayscale version of spectrum pallette
        protected int[] GrayPalette = {
                                             0x000000,            // Black
                                             0x171717,            // Red
                                             0x3C3C3C,            // Blue
                                             0x474747,            // Magenta
                                             0x777777,            // Green
                                             0xB3B3B3,            // Yellow
                                             0x8E8E8E,            // Cyan
                                             0xC6C6C6,            // White
                                             0x000000,            // Bright Black
                                             0x1D1D1D,            // Bright Red
                                             0x4C4C4C,            // Bright Blue    
                                             0x696969,            // Bright Magenta
                                             0x969696,            // Bright Green
                                             0xE2E2E2,            // Bright Yellow
                                             0xB3B3B3,            // Bright Cyan
                                             0xffffff             // Bright White
                                          };
        public enum keyCode
        {
            Q, W, E, R, T, Y, U, I, O, P, A, S, D, F, G, H, J, K, L, Z, X, C, V, B, N, M,
            _0, _1, _2, _3, _4, _5, _6, _7, _8, _9,
            SPACE, SHIFT, CTRL, ALT, TAB, CAPS,ENTER, BACK, 
            DEL, INS, HOME, END, PGUP, PGDOWN, NUMLOCK,
            ESC, PRINT_SCREEN, SCROLL_LOCK, PAUSE_BREAK,
            TILDE, EXCLAMATION, AT, HASH, DOLLAR, PERCENT, CARAT,
            AMPERSAND, ASTERISK, LBRACKET, RBRACKET, HYPHEN, PLUS, VBAR,
            LCURLY, RCURLY, COLON, DQUOTE, LESS_THAN, GREATER_THAN, QMARK,
            UNDER_SCORE, EQUALS, BSLASH, LSQUARE, RSQUARE, SEMI_COLON,
            APOSTROPHE, COMMA, STOP, FSLASH, LEFT, RIGHT, UP, DOWN,
            F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
            LAST
        };

        public TimeSpan TimeoutToHide { get; private set; }
        public DateTime LastMouseMove { get; private set; }
        public bool CursorIsHidden { get; private set; }

        public void OnFileDownloadEvent(Object sender)
        {
            showDownloadIndicator = true;
            downloadIndicatorTimeout = 250; //250 speccy frames ~5 seconds
        }

        public void DiskMotorEvent(Object sender, DiskEventArgs e)
        {
            int driveState = e.EventType;
            if ((driveState & 0x1) != 0)
                insertDiskAToolStripMenuItem.Text = "Eject Disk in A:";
            else
                insertDiskAToolStripMenuItem.Text = "Insert Disk in A:";

            if ((driveState & 0x2) != 0)
                insertDiskBToolStripMenuItem.Text = "Eject Disk in B:";
            else
                insertDiskBToolStripMenuItem.Text = "Insert Disk in B:";

            if ((driveState & 0x4) != 0)
                insertDiskCToolStripMenuItem.Text = "Eject Disk in C:";
            else
                insertDiskCToolStripMenuItem.Text = "Insert Disk in C:";

            if ((driveState & 0x8) != 0)
                insertDiskDToolStripMenuItem.Text = "Eject Disk in D:";
            else
                insertDiskDToolStripMenuItem.Text = "Insert Disk in D:";

            if ((driveState & 0x10) == 0)
                showDiskIndicator = false;
            else
                showDiskIndicator = true;
        }

        public Form1()
        {          
            InitializeComponent();
            toolTip1.Active = false;
            toolTip1.UseAnimation = false;
            toolTip1.UseFading = false;
            toolTip1.ShowAlways = false;

            //seems to stop the stutter when tooltip appears in fullscreen mode...
            toolTip1.IsBalloon = true; 

            this.Load += new System.EventHandler(this.Form1_Load);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.panel1_MouseMove);
            
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.Opaque, true);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.Form_MouseDown);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Form_MouseUp);
            
            TimeoutToHide = TimeSpan.FromSeconds(5);
         
            
            panel1.SendToBack();
            this.Icon = ZeroWin.Properties.Resources.ZeroIcon;
        }

        protected override void OnActivated(EventArgs e)
        {
            pauseEmulation = false;
            base.OnActivated(e);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            
            if (config.PauseOnFocusLost)
                if (!(AppHasFocus))
                {
                    pauseEmulation = true;
                }
            base.OnDeactivate(e);
        }

        public static class Native
        {
            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
            public struct Message
            {
                public IntPtr hWnd;
                [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)]
                public int msg;
                public IntPtr wParam;
                public IntPtr lParam;
                [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)]
                public uint time;
                public System.Drawing.Point p;
            }

            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            [System.Security.SuppressUnmanagedCodeSecurity, System.Runtime.InteropServices.DllImport("User32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
            public static extern bool PeekMessage(out Message msg, IntPtr hWnd,
                [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)]
                uint messageFilterMin,
                [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)]
                uint messageFilterMax,
                [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)]
                uint flags);

            [System.Runtime.InteropServices.DllImport("User32.dll")]
            public static extern IntPtr GetForegroundWindow();
            [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, ExactSpelling = true)]
            public static extern short GetAsyncKeyState([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.I4)] int vkey);
        }

        private bool AppStillIdle
        {
            get
            {
                Native.Message msg;
                return !Native.PeekMessage(out msg, IntPtr.Zero, 0, 0, 0);
            }
        }

        public bool AppHasFocus
        {
            get { 
                if ((Native.GetForegroundWindow() == this.Handle) || (Native.GetForegroundWindow() == tapeDeck.Handle))
                    return true;
                else
                if ((debugger != null) && (!debugger.IsDisposed))
                    if (Native.GetForegroundWindow() == debugger.Handle)
                        return true;
                return false;
            }
        }

        public void ForceScreenUpdate()
        {
            dxWindow.Invalidate();
        }

        private int GetSpectrumModelIndex(string speccyModel)
        {
            int modelIndex = 0;
            switch (speccyModel)
            {
                case "ZX Spectrum 48k":
                    modelIndex = 0;
                    break;

                case "ZX Spectrum 128k":
                    modelIndex = 1;
                    break;

                case "ZX Spectrum 128ke":
                    modelIndex = 2;
                    break;

                case "ZX Spectrum +3":
                    modelIndex = 3;
                    break;

                case "Pentagon 128k":
                    modelIndex = 4;
                    break;
            }
            return modelIndex;
        }

        private void HandleKey2Joy(int key, bool pressed)
        {
            if (pressed)
            {
                switch (key)
                {
                    case ((int)keyCode.RIGHT):
                        if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                            zx.joystickState[config.Key2JoystickType] |= zxmachine.JOYSTICK_MOVE_RIGHT;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                            zx.keyBuffer[(int)keyCode._2] = true;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                            zx.keyBuffer[(int)keyCode._7] = true;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                            zx.keyBuffer[(int)keyCode._8] = true;
                        break;
                    case ((int)keyCode.LEFT):
                        if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                            zx.joystickState[config.Key2JoystickType] |= zxmachine.JOYSTICK_MOVE_LEFT;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                            zx.keyBuffer[(int)keyCode._1] = true;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                            zx.keyBuffer[(int)keyCode._6] = true;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                            zx.keyBuffer[(int)keyCode._5] = true;
                        break;
                    case ((int)keyCode.DOWN):
                        if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                            zx.joystickState[config.Key2JoystickType] |= zxmachine.JOYSTICK_MOVE_DOWN;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                            zx.keyBuffer[(int)keyCode._3] = true;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                            zx.keyBuffer[(int)keyCode._8] = true;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                            zx.keyBuffer[(int)keyCode._7] = true;
                        break;
                    case ((int)keyCode.UP):
                        if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                            zx.joystickState[config.Key2JoystickType] |= zxmachine.JOYSTICK_MOVE_UP;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                            zx.keyBuffer[(int)keyCode._4] = true;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                            zx.keyBuffer[(int)keyCode._9] = true;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                            zx.keyBuffer[(int)keyCode._6] = true;
                        break;
                    case (255): //proxy for alt
                        if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                            zx.joystickState[config.Key2JoystickType] |= zxmachine.JOYSTICK_BUTTON_1;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                            zx.keyBuffer[(int)keyCode._5] = true;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                            zx.keyBuffer[(int)keyCode._0] = true;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                            zx.keyBuffer[(int)keyCode._0] = true;
                        break;
                }
            }
            else
            {
                switch (key)
                {
                    case ((int)keyCode.RIGHT):
                        if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                            zx.joystickState[config.Key2JoystickType] &= ~((int)(zxmachine.JOYSTICK_MOVE_RIGHT));
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                            zx.keyBuffer[(int)keyCode._2] = false;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                            zx.keyBuffer[(int)keyCode._7] = false;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                            zx.keyBuffer[(int)keyCode._8] = false;
                        break;
                    case ((int)keyCode.LEFT):
                        if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                            zx.joystickState[config.Key2JoystickType] &= ~((int)(zxmachine.JOYSTICK_MOVE_LEFT));
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                            zx.keyBuffer[(int)keyCode._1] = false;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                            zx.keyBuffer[(int)keyCode._6] = false;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                            zx.keyBuffer[(int)keyCode._5] = false;
                        break;
                    case ((int)keyCode.DOWN):
                        if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                            zx.joystickState[config.Key2JoystickType]&= ~((int)(zxmachine.JOYSTICK_MOVE_DOWN));
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                            zx.keyBuffer[(int)keyCode._3] = false;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                            zx.keyBuffer[(int)keyCode._8] = false;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                            zx.keyBuffer[(int)keyCode._7] = false;
                        break;
                    case ((int)keyCode.UP):
                        if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                            zx.joystickState[config.Key2JoystickType] &= ~((int)(zxmachine.JOYSTICK_MOVE_UP));
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                            zx.keyBuffer[(int)keyCode._4] = false;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                            zx.keyBuffer[(int)keyCode._9] = false;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                            zx.keyBuffer[(int)keyCode._6] = false;
                        break;
                    case (255): //proxy for alt
                        if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                            zx.joystickState[config.Key2JoystickType]&= ~((int)(zxmachine.JOYSTICK_BUTTON_1));
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                            zx.keyBuffer[(int)keyCode._5] = false;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                            zx.keyBuffer[(int)keyCode._0] = false;
                        else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                            zx.keyBuffer[(int)keyCode._0] = false;
                        break;
                }
            }
            /*
            if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
            {
                byte bitf = 0;
                if (rightArrowPressed)
                {
                    bitf |= zxmachine.JOYSTICK_MOVE_RIGHT;
                }
                else if (leftArrowPressed)
                {
                    bitf |= zxmachine.JOYSTICK_MOVE_LEFT;
                }

                if (downArrowPressed)
                    bitf |= zxmachine.JOYSTICK_MOVE_DOWN;
                else if (upArrowPressed)
                    bitf |= zxmachine.JOYSTICK_MOVE_UP;

                if (altIsPressed)
                    bitf |= zxmachine.JOYSTICK_BUTTON_1;

                zx.joystickState[config.Key2JoystickType] = bitf;
            }
            else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
            {
                if (rightArrowPressed)
                {
                    zx.keyBuffer[(int)keyCode._2] = true;
                }
                else
                    zx.keyBuffer[(int)keyCode._2] = false;

                 if (leftArrowPressed)
                {
                    zx.keyBuffer[(int)keyCode._1] = true;
                }
                else
                {
                    zx.keyBuffer[(int)keyCode._1] = false;
                    
                }

                if (downArrowPressed)
                    zx.keyBuffer[(int)keyCode._4] = true;
                else
                    zx.keyBuffer[(int)keyCode._4] = false;
                
                if (upArrowPressed)
                   zx.keyBuffer[(int)keyCode._3] = true;
                else
                {
                    zx.keyBuffer[(int)keyCode._3] = false;
                }

                if (altIsPressed)
                    zx.keyBuffer[(int)keyCode._5] = true;
                else
                    zx.keyBuffer[(int)keyCode._5] = false;
            }
            else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
            {
                if (rightArrowPressed)
                {
                    zx.keyBuffer[(int)keyCode._7] = true;
                }
                else
                     zx.keyBuffer[(int)keyCode._7] = false;

                if (leftArrowPressed)
                {
                    zx.keyBuffer[(int)keyCode._6] = true;
                }
                else
                {
                    zx.keyBuffer[(int)keyCode._6] = false;
                }

                if (downArrowPressed)
                    zx.keyBuffer[(int)keyCode._8] = true;
                else
                    zx.keyBuffer[(int)keyCode._8] = false;
               
                if (upArrowPressed)
                    zx.keyBuffer[(int)keyCode._9] = true;
                else
                {
                    
                    zx.keyBuffer[(int)keyCode._9] = false;
                }

                if (altIsPressed)
                    zx.keyBuffer[(int)keyCode._0] = true;
                else
                    zx.keyBuffer[(int)keyCode._0] = false;
            }
            else if (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
            {
                if (rightArrowPressed)
                {
                    zx.keyBuffer[(int)keyCode._8] = true;
                }
                else
                     zx.keyBuffer[(int)keyCode._8] = false;
                
                if (leftArrowPressed)
                {
                    zx.keyBuffer[(int)keyCode._5] = true;
                }
                else
                {
                   
                    zx.keyBuffer[(int)keyCode._5] = false;
                }

                if (downArrowPressed)
                    zx.keyBuffer[(int)keyCode._7] = true;
                else
                    zx.keyBuffer[(int)keyCode._7] = false;
                
                if (upArrowPressed)
                    zx.keyBuffer[(int)keyCode._6] = true;
                else
                {
                    
                    zx.keyBuffer[(int)keyCode._6] = false;
                }

                if (altIsPressed)
                    zx.keyBuffer[(int)keyCode._0] = true;
                else
                    zx.keyBuffer[(int)keyCode._0] = false;
            }*/
        }

        private void HandleJoystick(JoystickController joystick, int joystickType)
        {
            if (joystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
            {
                byte bitf = 0;
                if (joystick.state.X > 100)
                {
                    bitf |= zxmachine.JOYSTICK_MOVE_RIGHT;
                }
                else if (joystick.state.X < -100)
                {
                    bitf |= zxmachine.JOYSTICK_MOVE_LEFT;
                }

                if (joystick.state.Y > 100)
                    bitf |= zxmachine.JOYSTICK_MOVE_DOWN;
                else if (joystick.state.Y < -100)
                    bitf |= zxmachine.JOYSTICK_MOVE_UP;

                if (joystick.state.IsPressed(joystick.fireButtonIndex))
                    bitf |= zxmachine.JOYSTICK_BUTTON_1;

                zx.joystickState[joystickType] = bitf;
            }
            else if (joystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
            {
                if (joystick.state.X > 100)
                {
                    zx.keyBuffer[(int)keyCode._2] = true;
                }
                else if (joystick.state.X < -100)
                {
                    zx.keyBuffer[(int)keyCode._1] = true;
                }
                else
                {
                    zx.keyBuffer[(int)keyCode._1] = false;
                    zx.keyBuffer[(int)keyCode._2] = false;
                }

                if (joystick.state.Y < -100)
                {
                    zx.keyBuffer[(int)keyCode._4] = true;
                }
                else if (joystick.state.Y > 100)
                {
                    zx.keyBuffer[(int)keyCode._3] = true;
                }
                else
                {
                    zx.keyBuffer[(int)keyCode._3] = false;
                    zx.keyBuffer[(int)keyCode._4] = false;
                }

                if (joystick.state.IsPressed(joystick.fireButtonIndex))
                    zx.keyBuffer[(int)keyCode._5] = true;
                else
                    zx.keyBuffer[(int)keyCode._5] = false;
            }
            else if (joystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
            {
                if (joystick.state.X > 100)
                {
                    zx.keyBuffer[(int)keyCode._7] = true; ;
                }
                else if (joystick.state.X < -100)
                {
                    zx.keyBuffer[(int)keyCode._6] = true;
                }
                else
                {
                    zx.keyBuffer[(int)keyCode._6] = false;
                    zx.keyBuffer[(int)keyCode._7] = false;
                }

                if (joystick.state.Y < -100)
                {
                    zx.keyBuffer[(int)keyCode._9] = true;
                }
                else if (joystick.state.Y > 100)
                {
                    zx.keyBuffer[(int)keyCode._8] = true;
                }
                else
                {
                    zx.keyBuffer[(int)keyCode._8] = false;
                    zx.keyBuffer[(int)keyCode._9] = false;
                }

                if (joystick.state.IsPressed(joystick.fireButtonIndex))
                    zx.keyBuffer[(int)keyCode._0] = true;
                else
                    zx.keyBuffer[(int)keyCode._0] = false;
            }
            else if (joystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
            {
                if (joystick.state.X > 100)
                {
                    zx.keyBuffer[(int)keyCode._8] = true; ;
                }
                else if (joystick.state.X < -100)
                {
                    zx.keyBuffer[(int)keyCode._5] = true;
                }
                else
                {
                    zx.keyBuffer[(int)keyCode._5] = false;
                    zx.keyBuffer[(int)keyCode._8] = false;
                }

                if (joystick.state.Y < -100)
                {
                    zx.keyBuffer[(int)keyCode._7] = true;
                }
                else if (joystick.state.Y > 100)
                {
                    zx.keyBuffer[(int)keyCode._6] = true;
                }
                else
                {
                    zx.keyBuffer[(int)keyCode._7] = false;
                    zx.keyBuffer[(int)keyCode._6] = false;
                }

                if (joystick.state.IsPressed(joystick.fireButtonIndex))
                    zx.keyBuffer[(int)keyCode._0] = true;
                else
                    zx.keyBuffer[(int)keyCode._0] = false;
            }

            //Extra buttonmap handling
            foreach (System.Collections.Generic.KeyValuePair<int, int> pair in joystick.buttonMap)
            {
                if (pair.Key == joystick.fireButtonIndex)
                    continue; //ignore the fire button as we've handled it above already

                if (pair.Value < 0)
                    continue;
                if (joystick.state.IsPressed(pair.Key))
                    zx.keyBuffer[pair.Value] = true;
                else
                    zx.keyBuffer[pair.Value] = false;
            }
        }

        public void OnApplicationIdle(object sender, EventArgs e)
        {
            while (AppStillIdle && !pauseEmulation)
            {
                TimeSpan elapsed = DateTime.Now - LastMouseMove;
                if (config.FullScreen)
                {
                    if (!CursorIsHidden && !panel3.Visible && (elapsed >= TimeoutToHide))
                    {
                        Cursor.Hide();
                        CursorIsHidden = true;
                    }
                    
                    if (Cursor.Position.Y < panel3.Height)
                    {
                        if (!panel3.Visible)
                        {
                            panel3.Visible = true;
                        }
                       
                        return;
                    }
                    else
                    {
                        panel3.Visible = false;
                    }
                }


                lastTime = timer.TimeInMilliseconds();
                if (zx.doRun )
                    zx.Run();

                fpsFrame = timer.TimeInMilliseconds() - lastTime;// timer.DurationInMilliseconds;

                frameCount++;

                if (zx.HasKempstonMouse)
                {
                    mouse.UpdateMouse();
                    zx.MouseX += (byte)(mouse.MouseX / config.MouseSensitivity);
                    zx.MouseY -= (byte)(mouse.MouseY / config.MouseSensitivity);
                    zx.MouseButton = 0xff;
                    if (mouse.MouseLeftButtonDown)
                        zx.MouseButton = (byte)(zx.MouseButton & (~0x2));
                    if (mouse.MouseRightButtonDown)
                        zx.MouseButton = (byte)(zx.MouseButton & (~0x1)); 
                }

                if (joystick1Index >= 0)
                {
                    joystick1.Update();
                   
                    if (joystick1.state != null)
                    {
                        HandleJoystick(joystick1, joystick1MapIndex);
                    }
                }

                if (joystick2Index >= 0)
                {
                    joystick2.Update();
                    if (joystick2.state != null)
                    {
                        HandleJoystick(joystick2, joystick2MapIndex);
                    }
                }

                if (zx.tapeIsPlaying && tapeFastLoad)
                    if (frameCount < 30)
                        zx.needsPaint = false;

              

                //If we have sound we'll synchronize with the audio
                if (config.EnableSound )
                {
                  
                   // for (; ;)
                   // {
                   //     //if (!(zx.tapeIsPlaying && tapeFastLoad))
                   //       if ( zx.AudioDone())
                   //         break;
                   //     System.Threading.Thread.Sleep(1);
                   // }
                 
                    
                }
                else //we'll try and synch to ~60Hz framerate (50Hz makes it run slightly slower than audio synch)
                {
                    if ((fpsFrame) < 19 && !((zx.tapeIsPlaying && tapeFastLoad)))
                    {
                        double sleepTime = ((19 - fpsFrame));
                        System.Threading.Thread.Sleep((int)sleepTime);
                    }
                }
              
                if (zx.needsPaint)
                {
                   // dxWindow.Invalidate();
                    if (showDownloadIndicator)
                    {
                        downloadIndicatorTimeout--;
                        if (downloadIndicatorTimeout <= 0)
                            showDownloadIndicator = false;
                    }
                    frameCount = 0;
                   // zx.needsPaint = false;
                }
                System.Threading.Thread.Sleep(1);
            }
        }

        public string GetConfigData(StreamReader sr, string section, string data)
        {
            String readStr = "dummy";

            while (readStr != section)
            {
                if (sr.EndOfStream == true)
                {
                    System.Windows.Forms.MessageBox.Show("Invalid config file!", "Config file error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                    return "error";
                }
                readStr = sr.ReadLine();
            }

            while (true)
            {
                readStr = sr.ReadLine();
                if (readStr.IndexOf(data) >= 0)
                    break;
                if (sr.EndOfStream == true)
                {
                    System.Windows.Forms.MessageBox.Show("Invalid config file!", "Config file error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                    return "error";
                }
            }

            int startIndex = readStr.IndexOf("=") + 1;
            String dataString = readStr.Substring(startIndex, readStr.Length - startIndex);

            return dataString;
        }

        private bool LoadROM(String romName)
        {
            romName = "\\" + romName;
         
            //First try to load from the path saved in the config file
            romLoaded = zx.LoadROM(config.PathRoms, romName);

            //Next try the application startup path (useful if running off USB)
            if (!romLoaded)
            {
                romLoaded = zx.LoadROM(Application.StartupPath + "\\roms\\", romName);
                
                //Aha! This worked so update the path in config file
                if (romLoaded)
                    config.PathRoms = Application.StartupPath + "\\roms\\";
            }
            while (!romLoaded)
            {
                System.Windows.Forms.MessageBox.Show("Zero couldn't find a valid ROM file.\nSelect a folder to look for ROMs.",
                            "ROM file missing!", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);

                if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
                {
                    config.PathRoms = folderBrowserDialog1.SelectedPath;
                    romLoaded = zx.LoadROM(config.PathRoms, romName);
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show("Unfortunately, Zero cannot work without a valid ROM file.\nIt will now exit.",
                            "Unable to continue!", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                    break;
                }
            }

            return romLoaded;
        }

        protected override void OnKeyDown(KeyEventArgs keyEvent)
        {
            shiftIsPressed = (((Native.GetAsyncKeyState(VK_LSHIFT) & 0x8000) | (Native.GetAsyncKeyState(VK_RSHIFT) & 0x8000)) != 0); //|| ((Native.GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0);// (keyEvent.KeyCode & Keys.Shift) != 0;
            ctrlIsPressed = (((keyEvent.Modifiers & Keys.Control) != 0)); //((Native.GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0) || 
            altIsPressed = (keyEvent.Modifiers & Keys.Alt) != 0;

            if (!zx.keyBuffer[(int)keyCode.ALT] && altIsPressed)
                if (config.EnableKey2Joy)
                    HandleKey2Joy(255, true);

            zx.keyBuffer[(int)keyCode.SHIFT] = shiftIsPressed;
            zx.keyBuffer[(int)keyCode.CTRL] = ctrlIsPressed;
            zx.keyBuffer[(int)keyCode.ALT] = altIsPressed;

            switch (keyEvent.KeyCode)
            {
                case Keys.A:
                    zx.keyBuffer[(int)keyCode.A] = true;
                    break;

                case Keys.B:
                    zx.keyBuffer[(int)keyCode.B] = true;
                    break;

                case Keys.C:
                    zx.keyBuffer[(int)keyCode.C] = true;
                    break;

                case Keys.D:
                    zx.keyBuffer[(int)keyCode.D] = true;
                    break;

                case Keys.E:
                    zx.keyBuffer[(int)keyCode.E] = true;
                    break;

                case Keys.F:
                    zx.keyBuffer[(int)keyCode.F] = true;
                    break;

                case Keys.G:
                    zx.keyBuffer[(int)keyCode.G] = true;
                    break;

                case Keys.H:
                    zx.keyBuffer[(int)keyCode.H] = true;
                    break;

                case Keys.I:
                    zx.keyBuffer[(int)keyCode.I] = true;
                    break;

                case Keys.J:
                    zx.keyBuffer[(int)keyCode.J] = true;
                    break;

                case Keys.K:
                    zx.keyBuffer[(int)keyCode.K] = true;
                    break;

                case Keys.L:
                    zx.keyBuffer[(int)keyCode.L] = true;
                    break;

                case Keys.M:
                    zx.keyBuffer[(int)keyCode.M] = true;
                    break;

                case Keys.N:
                    zx.keyBuffer[(int)keyCode.N] = true;
                    break;

                case Keys.O:
                    if (altIsPressed)
                    {
                        fileButton_Click(this, null);
                    }
                    else
                        zx.keyBuffer[(int)keyCode.O] = true;
                    break;

                case Keys.P:
                    zx.keyBuffer[(int)keyCode.P] = true;
                    break;

                case Keys.Q:
                    zx.keyBuffer[(int)keyCode.Q] = true;
                    break;

                case Keys.R:
                    zx.keyBuffer[(int)keyCode.R] = true;
                    break;

                case Keys.S:
                    if (altIsPressed)
                    {
                        saveSnapshotMenuItem_Click(this, null);
                    }
                    else
                        zx.keyBuffer[(int)keyCode.S] = true;
                    break;

                case Keys.T:
                    zx.keyBuffer[(int)keyCode.T] = true;
                    break;

                case Keys.U:
                    zx.keyBuffer[(int)keyCode.U] = true;
                    break;

                case Keys.V:
                    zx.keyBuffer[(int)keyCode.V] = true;
                    break;

                case Keys.W:
                    zx.keyBuffer[(int)keyCode.W] = true;
                    break;

                case Keys.X:
                    zx.keyBuffer[(int)keyCode.X] = true;
                    break;

                case Keys.Y:
                    zx.keyBuffer[(int)keyCode.Y] = true;
                    break;

                case Keys.Z:
                    zx.keyBuffer[(int)keyCode.Z] = true;
                    break;

                case Keys.D0:
                    if (altIsPressed)
                        size100ToolStripMenuItem_Click(this, null);
                    else
                        zx.keyBuffer[(int)keyCode._0] = true;
                    break;

                case Keys.D1:
                    zx.keyBuffer[(int)keyCode._1] = true;
                    break;

                case Keys.D2:
                    zx.keyBuffer[(int)keyCode._2] = true;
                    break;

                case Keys.D3:
                    zx.keyBuffer[(int)keyCode._3] = true;
                    break;

                case Keys.D4:
                    zx.keyBuffer[(int)keyCode._4] = true;
                    break;

                case Keys.D5:
                    zx.keyBuffer[(int)keyCode._5] = true;
                    break;

                case Keys.D6:
                    zx.keyBuffer[(int)keyCode._6] = true;
                    break;

                case Keys.D7:
                    zx.keyBuffer[(int)keyCode._7] = true;
                    break;

                case Keys.D8:
                    zx.keyBuffer[(int)keyCode._8] = true;
                    break;
                    
                case Keys.D9:
                    zx.keyBuffer[(int)keyCode._9] = true;
                    break;

                case Keys.Enter:
                    if (altIsPressed)
                        fullScreenToolStripMenuItem_Click(this, null);
                    else
                        zx.keyBuffer[(int)keyCode.ENTER] = true;
                    break;
              
                case Keys.Space:
                    zx.keyBuffer[(int)keyCode.SPACE] = true;
                    break;

                case Keys.PrintScreen:
                    screenshotMenuItem1_Click(this, null);
                    break;

                case Keys.Up:
                    
                    if (config.EnableKey2Joy)
                        HandleKey2Joy((int)keyCode.UP, true);
                    else
                        zx.keyBuffer[(int)keyCode.UP] = true;
                    break;

                case Keys.Left:
                   
                    if (config.EnableKey2Joy)
                        HandleKey2Joy((int)keyCode.LEFT, true);
                    else
                        zx.keyBuffer[(int)keyCode.LEFT] = true;
                    break;

                case Keys.Right:
                    
                    if (config.EnableKey2Joy)
                        HandleKey2Joy((int)keyCode.RIGHT, true);
                    else
                        zx.keyBuffer[(int)keyCode.RIGHT] = true;
                    break;

                case Keys.Down:
                   
                    if (config.EnableKey2Joy)
                        HandleKey2Joy((int)keyCode.DOWN, true);
                    else
                        zx.keyBuffer[(int)keyCode.DOWN] = true;
                    break;

                case Keys.Back:
                    zx.keyBuffer[(int)keyCode.BACK] = true;
                    break;

                #region Convenience Key Press Emulation
                case Keys.OemPeriod:
                    if (ctrlIsPressed)
                    {
                        zx.keyBuffer[(int)keyCode.T] = true;
                        zx.keyBuffer[(int)keyCode.CTRL] = true;
                    }
                    else
                    {
                        zx.keyBuffer[(int)keyCode.CTRL] = true;
                        zx.keyBuffer[(int)keyCode.M] = true;
                    }
                    break;

                case Keys.Oemcomma:
                    if (ctrlIsPressed)
                    {
                        zx.keyBuffer[(int)keyCode.CTRL] = true;
                        zx.keyBuffer[(int)keyCode.R] = true;
                    }
                    else
                    {
                        zx.keyBuffer[(int)keyCode.CTRL] = true;
                        zx.keyBuffer[(int)keyCode.N] = true;
                    }
                    break;

                case Keys.OemSemicolon:
                    if (ctrlIsPressed) //colon
                    {
                        zx.keyBuffer[(int)keyCode.CTRL] = true;
                        zx.keyBuffer[(int)keyCode.Z] = true;
                        zx.keyBuffer[(int)keyCode.SHIFT] = false; //confuses speccy otherwise!
                    }
                    else
                    {
                        zx.keyBuffer[(int)keyCode.CTRL] = true;
                        zx.keyBuffer[(int)keyCode.O] = true;
                    }
                    break;

                case Keys.OemQuotes:
                    if (ctrlIsPressed) //double quotes
                    {
                        zx.keyBuffer[(int)keyCode.CTRL] = true;
                        zx.keyBuffer[(int)keyCode.P] = true;
                        zx.keyBuffer[(int)keyCode.SHIFT] = false; //confuses speccy otherwise!
                    }
                    else
                    {
                        zx.keyBuffer[(int)keyCode.CTRL] = true;
                        zx.keyBuffer[(int)keyCode._7] = true;
                        zx.keyBuffer[(int)keyCode.SHIFT] = false; //confuses speccy otherwise!
                    }
                    break;

                case Keys.Oem4: //brace open
                    if (ctrlIsPressed)
                    {
                        zx.keyBuffer[(int)keyCode.CTRL] = true;
                        zx.PokeByteNoContend(23617, 1);
                        zx.keyBuffer[(int)keyCode.F] = true;
                        zx.keyBuffer[(int)keyCode.Y] = false;
                    }
                    else
                    {
                        zx.keyBuffer[(int)keyCode.CTRL] = true;
                        zx.PokeByteNoContend(23617, 1);
                        zx.keyBuffer[(int)keyCode.Y] = true;
                        zx.keyBuffer[(int)keyCode.F] = false;
                    }
                    break;

                case Keys.Oem6: //brace close
                    if (ctrlIsPressed)
                    {
                        zx.keyBuffer[(int)keyCode.CTRL] = true;
                        zx.PokeByteNoContend(23617, 1);
                        zx.keyBuffer[(int)keyCode.G] = true;
                        zx.keyBuffer[(int)keyCode.U] = false;
                    }
                    else
                    {
                        zx.keyBuffer[(int)keyCode.CTRL] = true;
                        zx.PokeByteNoContend(23617, 1);
                        zx.keyBuffer[(int)keyCode.U] = true;
                        zx.keyBuffer[(int)keyCode.G] = false;
                    }
                    break;

                case Keys.OemMinus:
                    if (altIsPressed)
                        toolStripMenuItem1_Click(this, null);
                    else
                    {
                        if (ctrlIsPressed)
                        {
                            zx.keyBuffer[(int)keyCode.CTRL] = true;
                            zx.keyBuffer[(int)keyCode._0] = true;
                        }
                        else
                        {
                            zx.keyBuffer[(int)keyCode.CTRL] = true;
                            zx.keyBuffer[(int)keyCode.J] = true;
                        }
                    }
                    break;
                case Keys.Oemplus:
                    if (altIsPressed)
                        toolStripMenuItem5_Click_1(this, null);
                    else
                    {
                        if (ctrlIsPressed)
                        {
                            zx.keyBuffer[(int)keyCode.CTRL] = true;
                            zx.keyBuffer[(int)keyCode.K] = true;
                        }
                        else
                        {
                            zx.keyBuffer[(int)keyCode.CTRL] = true;
                            zx.keyBuffer[(int)keyCode.L] = true;
                        }
                    }
                    break;
                case Keys.OemPipe:
                    if (ctrlIsPressed)
                    {
                        zx.keyBuffer[(int)keyCode.CTRL] = true;
                        zx.PokeByteNoContend(23617, 1);
                        zx.keyBuffer[(int)keyCode.S] = true;
                        zx.keyBuffer[(int)keyCode.D] = false;
                    }
                    else
                    {
                        zx.keyBuffer[(int)keyCode.CTRL] = true;
                        zx.PokeByteNoContend(23617, 1);
                        zx.keyBuffer[(int)keyCode.D] = true;
                        zx.keyBuffer[(int)keyCode.S] = false;
                    }
                    break;
                
                case Keys.Oemtilde:
                    zx.keyBuffer[(int)keyCode.CTRL] = true;
                    zx.PokeByteNoContend(23617, 1);
                    zx.keyBuffer[(int)keyCode.A] = true;
                    break;
                #endregion

                case Keys.F6:
                    if (!zx.HasKempstonMouse)
                    {
                        mouse.AcquireMouse(this);
                        zx.HasKempstonMouse = true;
                    }
                    break;
                case Keys.F7:
                    if (zx.HasKempstonMouse)
                    {
                        mouse.ReleaseMouse();
                        zx.HasKempstonMouse = false;
                    }
                    break;
                case Keys.F5:
                    if (!altIsPressed)
                    {
                        screenshotMenuItem1_Click(this, null);
                    }
                    break;

                case Keys.Escape:
                    pauseEmulationESCToolStripMenuItem_Click(this, null);
                    break;

                case Keys.ControlKey:
                    {
                        zx.keyBuffer[(int)keyCode.CTRL] = true;
                    }
                    break;

                default:
                    if (keyEvent.KeyValue == 191) //frontslash
                    {
                        if (ctrlIsPressed) //question mark
                        {
                            zx.keyBuffer[(int)keyCode.CTRL] = true;
                            zx.keyBuffer[(int)keyCode.C] = true;
                            zx.keyBuffer[(int)keyCode.SHIFT] = false; //confuses speccy otherwise!
                        }
                        else
                        {
                            zx.keyBuffer[(int)keyCode.CTRL] = true;
                            zx.keyBuffer[(int)keyCode.V] = true;
                        }
                    }
                    break;
            }
        }

        protected override void OnKeyUp(KeyEventArgs keyEvent)
        {
           // if (ctrlIsPressed)
           //     zx.PokeByteNoContend(23617, 0);

            switch (keyEvent.KeyCode)
            {
                case Keys.A:
                    zx.keyBuffer[(int)keyCode.A] = false;
                    break;

                case Keys.B:
                    zx.keyBuffer[(int)keyCode.B] = false;
                    break;

                case Keys.C:
                    zx.keyBuffer[(int)keyCode.C] = false;
                    break;

                case Keys.D:
                    zx.keyBuffer[(int)keyCode.D] = false;
                    break;

                case Keys.E:
                    zx.keyBuffer[(int)keyCode.E] = false;
                    break;

                case Keys.F:
                    zx.keyBuffer[(int)keyCode.F] = false;
                    break;

                case Keys.G:
                    zx.keyBuffer[(int)keyCode.G] = false;
                    break;

                case Keys.H:
                    zx.keyBuffer[(int)keyCode.H] = false;
                    break;

                case Keys.I:
                    zx.keyBuffer[(int)keyCode.I] = false;
                    break;

                case Keys.J:
                    zx.keyBuffer[(int)keyCode.J] = false;
                    break;

                case Keys.K:
                    zx.keyBuffer[(int)keyCode.K] = false;
                    break;

                case Keys.L:
                    zx.keyBuffer[(int)keyCode.L] = false;
                    break;

                case Keys.M:
                    zx.keyBuffer[(int)keyCode.M] = false;
                    break;

                case Keys.N:
                    zx.keyBuffer[(int)keyCode.N] = false;
                    break;

                case Keys.O:
                    zx.keyBuffer[(int)keyCode.O] = false;
                    break;

                case Keys.P:
                    zx.keyBuffer[(int)keyCode.P] = false;
                    break;

                case Keys.Q:
                    zx.keyBuffer[(int)keyCode.Q] = false;
                    break;

                case Keys.R:
                    zx.keyBuffer[(int)keyCode.R] = false;
                    break;

                case Keys.S:
                    zx.keyBuffer[(int)keyCode.S] = false;
                    break;
                case Keys.T:
                    zx.keyBuffer[(int)keyCode.T] = false;
                    break;

                case Keys.U:
                    zx.keyBuffer[(int)keyCode.U] = false;
                    break;

                case Keys.V:
                    zx.keyBuffer[(int)keyCode.V] = false;
                    break;

                case Keys.W:
                    zx.keyBuffer[(int)keyCode.W] = false;
                    break;

                case Keys.X:
                    zx.keyBuffer[(int)keyCode.X] = false;
                    break;

                case Keys.Y:
                    zx.keyBuffer[(int)keyCode.Y] = false;
                    break;

                case Keys.Z:
                    zx.keyBuffer[(int)keyCode.Z] = false;
                    break;

                case Keys.D0:
                    zx.keyBuffer[(int)keyCode._0] = false;
                    break;

                case Keys.D1:
                    zx.keyBuffer[(int)keyCode._1] = false;
                    break;

                case Keys.D2:
                    zx.keyBuffer[(int)keyCode._2] = false;
                    break;

                case Keys.D3:
                    zx.keyBuffer[(int)keyCode._3] = false;
                    break;

                case Keys.D4:
                    zx.keyBuffer[(int)keyCode._4] = false;
                    break;

                case Keys.D5:
                    zx.keyBuffer[(int)keyCode._5] = false;
                    break;

                case Keys.D6:
                    zx.keyBuffer[(int)keyCode._6] = false;
                    break;

                case Keys.D7:
                    zx.keyBuffer[(int)keyCode._7] = false;
                    break;

                case Keys.D8:
                    zx.keyBuffer[(int)keyCode._8] = false;
                    break;

                case Keys.D9:
                    zx.keyBuffer[(int)keyCode._9] = false;
                    break;

                case Keys.Enter:
                    zx.keyBuffer[(int)keyCode.ENTER] = false;
                    break;

                case Keys.Space:
                    zx.keyBuffer[(int)keyCode.SPACE] = false;
                    break;

                //case Keys.ControlKey:
                //    zx.keyBuffer[(int)keyCode.CTRL] = false;
                //    break;

                //case Keys.ShiftKey:
                //    zx.keyBuffer[(int)keyCode.SHIFT] = false;
                //    shiftIsPressed = false;
                //    break;

                case Keys.Up:
                  
                    if (config.EnableKey2Joy)
                        HandleKey2Joy((int)keyCode.UP, false);
                    else
                        zx.keyBuffer[(int)keyCode.UP] = false;
                    break;

                case Keys.Left:
                   
                    if (config.EnableKey2Joy)
                        HandleKey2Joy((int)keyCode.LEFT, false);
                    else
                        zx.keyBuffer[(int)keyCode.LEFT] = false;
                    break;

                case Keys.Right:
                    
                    if (config.EnableKey2Joy)
                        HandleKey2Joy((int)keyCode.RIGHT, false);
                    else
                        zx.keyBuffer[(int)keyCode.RIGHT] = false;
                    break;

                case Keys.Down:
                    
                    if (config.EnableKey2Joy)
                        HandleKey2Joy((int)keyCode.DOWN, false);
                    else
                        zx.keyBuffer[(int)keyCode.DOWN] = false;

                    break;

                case Keys.Back:
                    zx.keyBuffer[(int)keyCode.BACK] = false;
                    break;

                case Keys.CapsLock:
                    zx.keyBuffer[(int)keyCode.CAPS] = true;
                    capsLockOn = !capsLockOn;
                    break;

                case Keys.OemPeriod:
                    zx.keyBuffer[(int)keyCode.CTRL] = false;
                    zx.keyBuffer[(int)keyCode.M] = false;
                    zx.keyBuffer[(int)keyCode.T] = false;
                    break;

                case Keys.Oemcomma:
                    zx.keyBuffer[(int)keyCode.R] = false;
                    zx.keyBuffer[(int)keyCode.CTRL] = false;
                    zx.keyBuffer[(int)keyCode.N] = false;
                    break;

                case Keys.OemQuotes:
                        zx.keyBuffer[(int)keyCode.P] = false;
                        zx.keyBuffer[(int)keyCode.CTRL] = false;
                        zx.keyBuffer[(int)keyCode._7] = false;
                    break;

                case Keys.OemSemicolon:
                        zx.keyBuffer[(int)keyCode.Z] = false;
                        zx.keyBuffer[(int)keyCode.CTRL] = false;
                        zx.keyBuffer[(int)keyCode.O] = false;
                    break;
            
                case Keys.OemBackslash:
                        zx.keyBuffer[(int)keyCode.C] = false;
                        zx.keyBuffer[(int)keyCode.CTRL] = false;
                        zx.keyBuffer[(int)keyCode.V] = false;
                    break;

                case Keys.Oem4: //brace open
                        zx.keyBuffer[(int)keyCode.CTRL] = false;
                        zx.PokeByteNoContend(23617, 0);
                        zx.keyBuffer[(int)keyCode.F] = false;
                        zx.keyBuffer[(int)keyCode.Y] = false;
                    break;

                case Keys.Oem6: //brace close
                        zx.keyBuffer[(int)keyCode.CTRL] = false;
                        zx.PokeByteNoContend(23617, 0);
                        zx.keyBuffer[(int)keyCode.U] = false;
                        zx.keyBuffer[(int)keyCode.G] = false;
                    break;

                case Keys.OemMinus:
                    zx.keyBuffer[(int)keyCode.CTRL] = false;
                    zx.keyBuffer[(int)keyCode._0] = false;
                    zx.keyBuffer[(int)keyCode.J] = false;
                    break;

                case Keys.Oemplus:
                    zx.keyBuffer[(int)keyCode.CTRL] = false;
                    zx.keyBuffer[(int)keyCode.K] = false;
                    zx.keyBuffer[(int)keyCode.L] = false;
                    break;

                case Keys.OemPipe:
                    zx.keyBuffer[(int)keyCode.CTRL] = false;
                    zx.PokeByteNoContend(23617, 0);
                    zx.keyBuffer[(int)keyCode.S] = false;
                    zx.keyBuffer[(int)keyCode.D] = false;
                    break;

                case Keys.Oemtilde:
                    zx.keyBuffer[(int)keyCode.CTRL] = false;
                    zx.PokeByteNoContend(23617, 0);
                    zx.keyBuffer[(int)keyCode.A] = false;
                    break;

                case Keys.F12:
                    zx.keyBuffer[(int)keyCode.F12] = false;
                    break;

                case Keys.ControlKey:
                   // if (shiftIsPressed)
                    {
                        zx.keyBuffer[(int)keyCode.CTRL] = false;
                      //  zx.keyBuffer[(int)keyCode.SHIFT] = false;
                    }
                    break;

                default:
                    if (keyEvent.KeyValue == 191) //frontslash
                    {
                            zx.keyBuffer[(int)keyCode.CTRL] = false;
                            zx.keyBuffer[(int)keyCode.C] = false;
                            zx.keyBuffer[(int)keyCode.V] = false;
                    }
                    break;
            }
            shiftIsPressed = (Native.GetAsyncKeyState(VK_LSHIFT) & 0x8000) != 0;// (keyEvent.KeyCode & Keys.Shift) != 0;
            ctrlIsPressed = (((Native.GetAsyncKeyState(VK_RSHIFT) & 0x8000) != 0) || ((keyEvent.Modifiers & Keys.Control) != 0));
            altIsPressed = (keyEvent.Modifiers & Keys.Alt) != 0;
            zx.keyBuffer[(int)keyCode.SHIFT] = shiftIsPressed;
           // zx.keyBuffer[(int)keyCode.CTRL] = ctrlIsPressed;
            if (zx.keyBuffer[(int)keyCode.ALT] && !altIsPressed)
                if (config.EnableKey2Joy)
                    HandleKey2Joy(255, false);
            zx.keyBuffer[(int)keyCode.ALT] = altIsPressed;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            //base.OnPaintBackground(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            //dxWindow.Invalidate();
        }

        void ChangeZXPalette(string newPalette)
        {
            config.PaletteMode = newPalette;
            switch (config.PaletteMode)
            {
                case "Grayscale":
                    zx.ULAPlusEnabled = false;
                    zx.SetPalette(GrayPalette);
                    break;
                case "ULA Plus":
                    zx.ULAPlusEnabled = true;
                    zx.SetPalette(zx.NormalColors);
                    break;
                default:
                    zx.ULAPlusEnabled = false;
                    zx.SetPalette(zx.NormalColors);
                    break;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            //Show the confirmation box only if it's not an invalid ROM exit event
            if (config.ConfirmOnExit && romLoaded)
            {
                if (System.Windows.Forms.MessageBox.Show("Are you sure you want to exit?",
                           "Confirm Exit", System.Windows.Forms.MessageBoxButtons.YesNo,
                           System.Windows.Forms.MessageBoxIcon.Question) == DialogResult.No)
                
                    e.Cancel = true;
                
            }
            base.OnFormClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            
            for (int f = 0; f < 4; f++)
                if (diskArchivePath[f] != null)
                {
                    zx.DiskEject((byte)f);
                    File.Delete(diskArchivePath[f]);
                }

            if (config != null)
            {
                config.TapeAutoStart = tapeDeck.DoTapeAutoStart;
                config.TapeAutoRewind = tapeDeck.DoTapeAutoRewind;
                config.TapeEdgeLoad = tapeDeck.DoTapeEdgeLoad;
                config.TapeAccelerateLoad = tapeDeck.DoTapeAccelerateLoad;

                config.Save();
            }

            joystick1.Release();
            joystick2.Release();

            mouse.ReleaseMouse();
            if (zx != null)
                zx.Shutdown();
            if (dxWindow != null)
                dxWindow.Shutdown();

            //Close any open archived disk files
           
            base.OnClosed(e);
        }

        private void Form1_Load(object sender, System.EventArgs e)
        {
            this.BringToFront();

            //Load configuration
            config.ApplicationPath = Application.StartupPath;
            config.Load();

            romLoaded = false;
            recentFolder = config.PathGames;
            
            //Load the ROM
            switch (config.CurrentSpectrumModel)
            {
                case "ZX Spectrum 48k":
                    config.Model = MachineModel._48k;
                    zx = new zx48(this.Handle, config.UseLateTimings);
                    zx.Issue2Keyboard = config.UseIssue2Keyboard;
                    zx.Reset();
                    zx48ktoolStripMenuItem1.Checked = true;
                    diskMenuItem3.Enabled = false;
                    romLoaded = LoadROM(config.Current48kROM);
                    break;

                case "ZX Spectrum 128ke":
                    config.Model = MachineModel._128ke;
                    zx = new zx128e(this.Handle, config.UseLateTimings);
                    zx.Issue2Keyboard = config.UseIssue2Keyboard;
                    zx.Reset();
                    zXSpectrumToolStripMenuItem.Checked = true;
                    diskMenuItem3.Enabled = false;
                    romLoaded = LoadROM(config.current128keRom);
                    break;

                case "ZX Spectrum 128k":
                    config.Model = MachineModel._128k;
                    zx = new zx128(this.Handle, config.UseLateTimings);
                    zx.Reset();
                    zXSpectrum128KToolStripMenuItem.Checked = true;
                    diskMenuItem3.Enabled = false;
                    romLoaded = LoadROM(config.current128kRom);
                    break;

                case "Pentagon 128k":
                    config.Model = MachineModel._pentagon;
                    zx = new Pentagon128K(this.Handle, config.UseLateTimings);
                    zx.Reset();
                    pentagon128KToolStripMenuItem.Checked = true;
                    romLoaded = LoadROM(config.currentPentagonRom);
                    diskMenuItem3.Enabled = true;
                    insertDiskCToolStripMenuItem.Enabled = true;
                    insertDiskDToolStripMenuItem.Enabled = true;
                    break;
                case "ZX Spectrum +3":
                    config.Model = MachineModel._plus3;
                    zx = new zxPlus3(this.Handle, config.UseLateTimings);
                    zx.Reset();
                    zxSpectrum3ToolStripMenuItem.Checked = true;
                    romLoaded = LoadROM(config.currentPlus3Rom);
                    diskMenuItem3.Enabled = true;
                    insertDiskCToolStripMenuItem.Enabled = false;
                    insertDiskDToolStripMenuItem.Enabled = false;
                    break;
            }

            if (!romLoaded)
            {
                this.Close();
                return;
            }

            volumeTrackBar1.Value = config.Volume;
            volumeTrackBar1_Scroll(this, null);

            zx.SetEmulationSpeed(config.EmulationSpeed);
            zx.EnableAY(config.EnableAYFor48K);
            zx.SetStereoSound(config.StereoSoundOption);

            tapeDeck = new TapeDeck(this);
            tapeDeck.DoTapeAutoStart = config.TapeAutoStart;
            tapeDeck.DoTapeAutoRewind = config.TapeAutoRewind;
            tapeDeck.DoTapeEdgeLoad = config.TapeEdgeLoad;
            tapeDeck.DoTapeAccelerateLoad = config.TapeAccelerateLoad;
            
            zx.DiskEvent += new DiskEventHandler(DiskMotorEvent);
            try
            {
                dxWindow = new ZRenderer(this, panel1.Width, panel1.Height);
            }
            catch (System.TypeInitializationException dxex)
            {
                MessageBox.Show(dxex.InnerException.Message, "Wrong DirectX version.", MessageBoxButtons.OK);
                
                return;
            }
           
            dxWindow.ContextMenuStrip = contextMenuStrip1;
            if (config.UseDirectX)
                directXToolStripMenuItem_Click(this, null);
            else
                gDIToolStripMenuItem_Click(this, null);

            dxWindow.Location = new Point(panel4.Width, panel2.Height);
            dxWindow.SetSize(panel1.Width, panel1.Height);
            panel1.Location = new Point(panel4.Width, panel2.Height);
            dxWindow.MouseMove += new MouseEventHandler(Form1_MouseMove);
            this.Controls.Add(dxWindow);
            panel1.Enabled = false;
            panel1.Hide();
            panel1.SendToBack();
            dxWindow.BringToFront();
            dxWindow.Focus();
            dxWindow.ShowScanlines = interlaceToolStripMenuItem.Checked;

            AdjustWindowSize();
            if (config.FullScreen)
                GoFullscreen(true);
            switch (config.PaletteMode)
            {
                case "Grayscale":
                    grayscaleToolStripMenuItem_Click_1(this, null);
                    break;
                case "ULA Plus":
                    uLAPlusToolStripMenuItem_Click(this, null);
                    break;
                default:
                    normalToolStripMenuItem_Click_1(this, null);
                    break;
            }
            
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            if (commandLineArgs.Length > 1)
            {
                LoadZXFile(commandLineArgs[1]);
            }
            System.GC.Collect(); //probably redundant...
            zx.Start();
        }
      
        private void Form_MouseDown(object sender, MouseEventArgs e)
        {
           mouseOrigin.X = e.X;
           mouseOrigin.Y = e.Y;
        }

        private void Form_MouseUp(object sender, MouseEventArgs e)
        {
            if (volumeTrackBar1.Visible)
            {
                volumeTrackBar1.Hide();
            }
        }

        private void Form_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Top += e.Y - mouseOrigin.Y;
                this.Left += e.X - mouseOrigin.X;
            }
        }

        private void machineButton_Click(object sender, EventArgs e)
        {
            contextMenuStrip2.Show(machineButton, 0, machineButton.Size.Height);
        }


        //Load file
        private void fileButton_Click(object sender, EventArgs e)
        {
            

            openFileMenuItem1_Click(sender, e);
        }

        private void directXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //If directX isn't available, try initialising it one more time
            if (!dxWindow.DirectXReady)
                dxWindow.InitDirectX(dxWindow.Width, dxWindow.Height);

            //If we have directX available to us, switch to it.
            if (dxWindow.DirectXReady)
            {
                dxWindow.EnableDirectX = true;
                gDIToolStripMenuItem.Checked = false;
                directXToolStripMenuItem.Checked = true;
                dxWindow.Focus();
                config.UseDirectX = true;
                interlaceToolStripMenuItem.Enabled = true;
                if (interlaceToolStripMenuItem.Checked)
                    dxWindow.ShowScanlines = true;
                else
                    dxWindow.ShowScanlines = false;
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("Zero was unable to switch to DirectX mode.\nIt will now continue in GDI mode.",
                           "DirectX Error", System.Windows.Forms.MessageBoxButtons.OK,
                           System.Windows.Forms.MessageBoxIcon.Exclamation);
                dxWindow.EnableDirectX = false;
                gDIToolStripMenuItem.Checked = true;
                directXToolStripMenuItem.Checked = false;
                dxWindow.Focus();
            }
            
        }

        private void gDIToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dxWindow.EnableDirectX = false;
            directXToolStripMenuItem.Checked = false;
            gDIToolStripMenuItem.Checked = true;
            dxWindow.Focus();
            config.UseDirectX = false;
            interlaceToolStripMenuItem.Enabled = false;
        }

        private void kToolStripMenuItem_Click(object sender, EventArgs e)
        {
            zx.Reset();
            dxWindow.Focus();
        }

        private void kToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            zXSpectrumToolStripMenuItem_Click(sender, e);
            dxWindow.Focus();
        }

        private void kToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            zXSpectrum128KToolStripMenuItem_Click(sender, e);
            dxWindow.Focus();
         
        }

        private void panel1_MouseDown(object sender, MouseEventArgs e)
        {
            Form_MouseDown(sender, e);
            dxWindow.Focus();
        }

        private void panel1_MouseMove(object sender, MouseEventArgs e)
        {
            Form1_MouseMove(sender, e);
            dxWindow.Focus();
        }

        private void panel1_MouseUp(object sender, MouseEventArgs e)
        {
            Form_MouseUp(sender, e);
            dxWindow.Focus();
        }

        //100% window size
        private void size100ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (config.FullScreen)
                GoFullscreen(false);

            config.WindowSize = 0;
            AdjustWindowSize();
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            return false;
        }
      
      /*  protected override bool ProcessCmdKey(ref 
              System.Windows.Forms.Message m,
              System.Windows.Forms.Keys k)
        {
            // detect the pushing (Msg) of Enter Key (k)
             
            if (m.Msg == 256)
            {
                if (k == System.Windows.Forms.Keys.Enter)
                {
                    zx.keyBuffer[(int)keyCode.ENTER] = true;
                    return true;
                }

                if (k == System.Windows.Forms.Keys.Up)
                {
                    zx.keyBuffer[(int)keyCode.UP] = true;
                    upArrowPressed = true;
                    return true;
                }

                if (k == System.Windows.Forms.Keys.Down)
                {
                    zx.keyBuffer[(int)keyCode.DOWN] = true;
                    downArrowPressed = true;
                    return true;
                }

                if (k == System.Windows.Forms.Keys.Left)
                {
                    zx.keyBuffer[(int)keyCode.LEFT] = true;
                    leftArrowPressed = true;
                    return true;
                }

                if (k == System.Windows.Forms.Keys.Right)
                {
                    zx.keyBuffer[(int)keyCode.RIGHT] = true;
                    rightArrowPressed = true;
                    return true;
                }
            }
            // if not pushing Enter Key,

            // then process the signal as usual
            return base.ProcessCmdKey(ref m, k);
        }
        */
        //Monitor
        private void monitorButton_Click(object sender, EventArgs e)
        {
            if (debugger == null || debugger.IsDisposed)
            {
                //zx.doRun = false;
                debugger = new Monitor(this);
                
                //debugger.SetState(1);
                //debugger.UpdateMachineInfo();
                //debugger.Show();
                debugger.dbState = Monitor.MonitorState.STEPIN;
            }

            if (!debugger.Visible)
            {
            
                //zx.doRun = false;
                debugger.dbState = Monitor.MonitorState.STEPIN;
                debugger.ReSyncWithZX();
                //debugger.SetState(1);
                //debugger.UpdateMachineInfo();
                //debugger.Show();
            }
        }
 
        //Normal palette
        private void normalToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            ChangeZXPalette("Normal");
            grayscaleToolStripMenuItem.Checked = false;
            normalToolStripMenuItem.Checked = true;
            uLAPlusToolStripMenuItem.Checked = false;
            dxWindow.Focus();
        }

        //Gray palette
        private void grayscaleToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            ChangeZXPalette("Grayscale");
            normalToolStripMenuItem.Checked = false;
            grayscaleToolStripMenuItem.Checked = true;
            uLAPlusToolStripMenuItem.Checked = false;
            dxWindow.Focus();
        }

        //48k select
        public void zx48ktoolStripMenuItem1_Click(object sender, EventArgs e)
        {
            zx.Pause();
            ChangeSpectrumModel(MachineModel._48k);
            config.CurrentSpectrumModel = "ZX Spectrum 48k";
            zx48ktoolStripMenuItem1.Checked = true;
            zXSpectrumToolStripMenuItem.Checked = false;
            zXSpectrum128KToolStripMenuItem.Checked = false;
            zxSpectrum3ToolStripMenuItem.Checked = false;
            pentagon128KToolStripMenuItem.Checked = false;
            showTapeIndicator = false;
            dxWindow.Focus();
            zx.Resume();
        }

        //Spectrum 128Ke
        public void zXSpectrumToolStripMenuItem_Click(object sender, EventArgs e)
        {
            zx.Pause();
            ChangeSpectrumModel(MachineModel._128ke);
            config.CurrentSpectrumModel = "ZX Spectrum 128ke";
            zXSpectrumToolStripMenuItem.Checked = true;
            zx48ktoolStripMenuItem1.Checked = false;
            zXSpectrum128KToolStripMenuItem.Checked = false;
            zxSpectrum3ToolStripMenuItem.Checked = false;
            pentagon128KToolStripMenuItem.Checked = false;
            showTapeIndicator = false;
            dxWindow.Focus();
            zx.Resume();
        }

        public void zXSpectrum128KToolStripMenuItem_Click(object sender, EventArgs e)
        {
            zx.Pause();
            ChangeSpectrumModel(MachineModel._128k);
            config.CurrentSpectrumModel = "ZX Spectrum 128k";
            zx48ktoolStripMenuItem1.Checked = false;
            zXSpectrum128KToolStripMenuItem.Checked = true;
            zXSpectrumToolStripMenuItem.Checked = false;
            zxSpectrum3ToolStripMenuItem.Checked = false;
            pentagon128KToolStripMenuItem.Checked = false;
            showTapeIndicator = false;
            dxWindow.Focus();
            zx.Resume();
        }

        private void zxSpectrum3ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            zx.Pause();
            ChangeSpectrumModel(MachineModel._plus3);
            config.CurrentSpectrumModel = "ZX Spectrum +3";
            zx48ktoolStripMenuItem1.Checked = false;
            zXSpectrum128KToolStripMenuItem.Checked = false;
            zXSpectrumToolStripMenuItem.Checked = false;
            zxSpectrum3ToolStripMenuItem.Checked = true;
            pentagon128KToolStripMenuItem.Checked = false;
            showTapeIndicator = false;
            dxWindow.Focus();
            zx.Resume();
        }


        private void pentagon128KToolStripMenuItem_Click(object sender, EventArgs e)
        {
            zx.Pause();
            ChangeSpectrumModel(MachineModel._pentagon);
            config.CurrentSpectrumModel = "Pentagon 128k";
            zx48ktoolStripMenuItem1.Checked = false;
            zXSpectrum128KToolStripMenuItem.Checked = false;
            zXSpectrumToolStripMenuItem.Checked = false;
            zxSpectrum3ToolStripMenuItem.Checked = false;
            pentagon128KToolStripMenuItem.Checked = true;
            showTapeIndicator = false;
            dxWindow.Focus();
            zx.Resume();
        }

        private void ChangeSpectrumModel(MachineModel _model)
        {
            dxWindow.Suspend();
            if (debugger != null)
            {
                debugger.DeRegisterAllEvents();
                debugger.DeSyncWithZX();
            }
            if (tapeDeck != null)
            {
                tapeDeck.UnRegisterEventHooks();
            }
            showDiskIndicator = false;
            zx.DiskEvent -= new DiskEventHandler(DiskMotorEvent);
            zx.Shutdown();
            zx = null;
            System.GC.Collect();
            config.Model = _model;
            Directory.SetCurrentDirectory(Application.StartupPath);

            switch (config.Model)
            {
                case MachineModel._48k:
                    config.Model = MachineModel._48k;
                    zx = new zx48(this.Handle, config.UseLateTimings);
                    romLoaded = LoadROM(config.Current48kROM);
                    diskMenuItem3.Enabled = false;
                    break;
                case MachineModel._128k:
                    config.Model = MachineModel._128k;
                    zx = new zx128(this.Handle, config.UseLateTimings);
                    romLoaded = LoadROM(config.Current128kROM);
                    diskMenuItem3.Enabled = false;
                    break;
                case MachineModel._128ke:
                    config.Model = MachineModel._128ke;
                    zx = new zx128e(this.Handle, config.UseLateTimings);
                    romLoaded = LoadROM(config.current128keRom);
                    diskMenuItem3.Enabled = false;
                    break;
                case MachineModel._plus3:
                    config.Model = MachineModel._plus3;
                    zx = new zxPlus3(this.Handle, config.UseLateTimings);
                    romLoaded = LoadROM(config.currentPlus3Rom);
                    diskMenuItem3.Enabled = true;
                    insertDiskCToolStripMenuItem.Enabled = false;
                    insertDiskDToolStripMenuItem.Enabled = false;
                    break;
                case MachineModel._pentagon:
                    config.Model = MachineModel._pentagon;
                    zx = new Pentagon128K(this.Handle, config.UseLateTimings);
                    romLoaded = LoadROM(config.currentPentagonRom);
                    insertDiskCToolStripMenuItem.Enabled = true;
                    insertDiskDToolStripMenuItem.Enabled = true;
                    break;
            }
            if (!romLoaded)
            {
                this.Close();
                return;
            }
            
            //Some models like the Pentagon don't have the same screen width as the normal speccy
            //so we have to adjust the window size when switching to them and vice-versa
            if ((zx.GetTotalScreenWidth() != dxWindow.ScreenWidth) || (zx.GetTotalScreenHeight() != dxWindow.ScreenHeight))
            {
                bool fs = config.FullScreen;
                GoFullscreen(false);
                AdjustWindowSize();
                if (fs)
                    GoFullscreen(true);
            }

            volumeTrackBar1.Value = config.Volume;
            volumeTrackBar1_Scroll(this, null);

            zx.SetEmulationSpeed(config.EmulationSpeed);
            zx.EnableAY(config.EnableAYFor48K);
            zx.SetStereoSound(config.StereoSoundOption);
            zx.Reset();
            if ((joystick2MapIndex == (int)zxmachine.JoysticksEmulated.KEMPSTON) || (joystick1MapIndex == (int)zxmachine.JoysticksEmulated.KEMPSTON))
                zx.HasKempstonJoystick = true;
            else
                zx.HasKempstonJoystick = false;

            ChangeZXPalette(config.PaletteMode);
           
            if (!config.EnableSound)
                zx.MuteSound();
            if (debugger != null)
            {
                debugger.ReRegisterAllEvents();
                debugger.ReSyncWithZX();
                debugger.UpdateMachineInfo();
            }

            if (tapeDeck != null)
            {
                tapeDeck.RegisterEventHooks();
            }
            zx.DiskEvent += new DiskEventHandler(DiskMotorEvent);
          
            dxWindow.Resume();
        }

        private void panel2_MouseDown(object sender, MouseEventArgs e)
        {
            Form_MouseDown(sender, e);
            dxWindow.Focus();
        }

        private void panel2_MouseMove(object sender, MouseEventArgs e)
        {
            Form_MouseMove(sender, e);
            dxWindow.Focus();
        }

        private void panel4_MouseDown(object sender, MouseEventArgs e)
        {
            Form_MouseDown(sender, e);
            dxWindow.Focus();
        }

        private void panel4_MouseMove(object sender, MouseEventArgs e)
        {
            Form_MouseMove(sender, e);
            dxWindow.Focus();
        }

        //options button
        private void optionsButton_Click(object sender, EventArgs e)
        {
            if ((optionWindow == null) || (optionWindow.IsDisposed))
                optionWindow = new Options(this);
            pauseEmulation = true;
            zx.Pause();
            PreOptionsWindowShow();
            if (optionWindow.ShowDialog(this) == DialogResult.OK)
            {
                PostOptionsWindowShow();
            }
            pauseEmulation = false;
            zx.Resume();
            dxWindow.Focus();
        }

        private void PreOptionsWindowShow()
        {
            optionWindow.RomToUse48k = config.Current48kROM;
            optionWindow.RomToUse128k = config.Current128kROM;
            optionWindow.RomToUse128ke = config.Current128keROM;
            optionWindow.RomToUsePlus3 = config.CurrentPlus3ROM;
            optionWindow.RomToUsePentagon = config.CurrentPentagonROM;
            optionWindow.SpectrumModel = GetSpectrumModelIndex(config.CurrentSpectrumModel);
            optionWindow.UseIssue2Keyboard = config.UseIssue2Keyboard;
            optionWindow.UseDirectX = config.UseDirectX;
            optionWindow.InterlacedMode = config.EnableInterlacedOverlay;
            optionWindow.EnableKey2Joy = config.EnableKey2Joy;
            optionWindow.Key2JoyStickType = config.Key2JoystickType;
            optionWindow.FileAssociatePZX = config.AccociatePZXFiles;
            optionWindow.FileAssociateTZX = config.AccociateTZXFiles;
            optionWindow.FileAssociateTAP = config.AccociateTAPFiles;
            optionWindow.FileAssociateSNA = config.AccociateSNAFiles;
            optionWindow.FileAssociateSZX = config.AccociateSZXFiles;
            optionWindow.FileAssociateZ80 = config.AccociateZ80Files;
            optionWindow.FileAssociateDSK = config.AccociateDSKFiles;
            optionWindow.FileAssociateTRD = config.AccociateTRDFiles;
            optionWindow.FileAssociateSCL = config.AccociateSCLFiles;
            optionWindow.EmulationSpeed = config.EmulationSpeed;
            optionWindow.SpeakerSetup = config.StereoSoundOption;
            optionWindow.MouseSensitivity = config.MouseSensitivity;

            switch (config.PaletteMode)
            {
                case "Grayscale":
                    optionWindow.Palette = 1;
                    break;

                case "ULA Plus":
                    optionWindow.Palette = 2;
                    break;

                default:
                    optionWindow.Palette = 0;
                    break;
            }

            optionWindow.borderSize = config.BorderSize/24;
            optionWindow.UseLateTimings = config.UseLateTimings;// (zx.LateTiming != 0 ? true : false);
            optionWindow.PauseOnFocusChange = config.PauseOnFocusLost;
            optionWindow.ConfirmOnExit = config.ConfirmOnExit;
            optionWindow.EnableAYFor48K = config.EnableAYFor48K;
            optionWindow.Joystick1Choice = joystick1Index + 1;
            optionWindow.Joystick2Choice = joystick2Index + 1;
            optionWindow.HighCompatibilityMode = config.HighCompatibilityMode;
            optionWindow.RomPath = config.PathRoms;
            optionWindow.GamePath = config.PathGames;
            optionWindow.Joystick1EmulationChoice = joystick1MapIndex;
            optionWindow.Joystick2EmulationChoice = joystick2MapIndex;
        }

        private void PostOptionsWindowShow()
        {
            config.UseIssue2Keyboard = optionWindow.UseIssue2Keyboard;
            config.UseLateTimings = (optionWindow.UseLateTimings);// == true ? 1 : 0);
            config.UseDirectX = optionWindow.UseDirectX;
            config.PathRoms = optionWindow.RomPath;
            config.PathGames = optionWindow.GamePath;
            interlaceToolStripMenuItem.Checked = optionWindow.InterlacedMode;
            config.StereoSoundOption = optionWindow.SpeakerSetup;
            config.MouseSensitivity = optionWindow.MouseSensitivity;
            config.EnableAYFor48K = optionWindow.EnableAYFor48K;
            config.HighCompatibilityMode = optionWindow.HighCompatibilityMode;
            config.EnableKey2Joy =  optionWindow.EnableKey2Joy;
            config.Key2JoystickType = optionWindow.Key2JoyStickType;
            if (config.UseDirectX)
                directXToolStripMenuItem_Click(this, null);
            else
                gDIToolStripMenuItem_Click(this, null);

            if (config.BorderSize != (optionWindow.borderSize * 24))
            {
                config.BorderSize = optionWindow.borderSize * 24;
                AdjustWindowSize();
            }
            if (config.EmulationSpeed != optionWindow.EmulationSpeed)
            {
                config.EmulationSpeed = optionWindow.EmulationSpeed;
                zx.SetEmulationSpeed(config.EmulationSpeed);
            }
            switch (optionWindow.Palette)
            {
                case 1:
                    grayscaleToolStripMenuItem_Click_1(this, null);
                    break;

                case 2:
                    uLAPlusToolStripMenuItem_Click(this, null);
                    break;
                default:
                    normalToolStripMenuItem_Click_1(this, null);
                    break;
            }

            if ((optionWindow.SpectrumModel != GetSpectrumModelIndex(config.CurrentSpectrumModel))
                 || config.Current48kROM != optionWindow.RomToUse48k || config.current128kRom != optionWindow.RomToUse128k
                 || config.Current128keROM != optionWindow.RomToUse128ke
                 || config.CurrentPlus3ROM != optionWindow.RomToUsePlus3
                 || config.CurrentPentagonROM != optionWindow.RomToUsePentagon)
            {
                config.Current48kROM = optionWindow.RomToUse48k;
                config.current128kRom = optionWindow.RomToUse128k;
                config.Current128keROM = optionWindow.RomToUse128ke;
                config.CurrentPlus3ROM = optionWindow.RomToUsePlus3;
                config.CurrentPentagonROM = optionWindow.RomToUsePentagon;
              
                switch (optionWindow.SpectrumModel)
                {
                    case 0:
                        zx48ktoolStripMenuItem1_Click(this, null);   
                        break;

                    case 1:
                        zXSpectrum128KToolStripMenuItem_Click(this, null);
                        break;

                    case 2:
                        zXSpectrumToolStripMenuItem_Click(this, null);
                        break;

                    case 3:
                        zxSpectrum3ToolStripMenuItem_Click(this, null);
                        break;

                    case 4:
                        pentagon128KToolStripMenuItem_Click(this, null);
                        break;
                }
            }

         
            zx.Issue2Keyboard = config.UseIssue2Keyboard;
            config.ConfirmOnExit = optionWindow.ConfirmOnExit;
            config.PauseOnFocusLost= optionWindow.PauseOnFocusChange;
          
            zx.SetStereoSound(config.StereoSoundOption); //Also sets ACB/ABC config internally
            zx.EnableAY(config.EnableAYFor48K);
            joystick1Index = optionWindow.Joystick1Choice - 1;

            if (joystick1Index >= 0)
                joystick1.InitJoystick(this, joystick1Index);  

            joystick2Index = optionWindow.Joystick2Choice - 1;
            if (joystick2Index >= 0)
                joystick2.InitJoystick(this,joystick2Index);  
            joystick1MapIndex = optionWindow.Joystick1EmulationChoice;
            joystick2MapIndex = optionWindow.Joystick2EmulationChoice;
            if ((joystick2MapIndex == (int)zxmachine.JoysticksEmulated.KEMPSTON) || (joystick1MapIndex == (int)zxmachine.JoysticksEmulated.KEMPSTON))
                zx.HasKempstonJoystick = true;
            else
                zx.HasKempstonJoystick = false;
            
            CheckFileAssociations();
            optionWindow.Dispose();
        }

        //Need this to command the windows explorer shell to refresh icon cache
        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

        public void CheckFileAssociations()
        {
            string param = "";
            string assoc = "";
            bool iconsChanged = false;

            if (optionWindow.FileAssociateDSK != config.AccociateDSKFiles)
            {
                assoc = (optionWindow.FileAssociateDSK ? " 1.dsk" : " 0.dsk");
                iconsChanged = true;
                param += assoc;
            }

            if (optionWindow.FileAssociateTRD != config.AccociateTRDFiles)
            {
                assoc = (optionWindow.FileAssociateTRD ? " 1.trd" : " 0.trd");
                iconsChanged = true;
                param += assoc;
            }

            if (optionWindow.FileAssociateSCL != config.AccociateSCLFiles)
            {
                assoc = (optionWindow.FileAssociateSCL ? " 1.scl" : " 0.scl");
                iconsChanged = true;
                param += assoc;
            }

            if (optionWindow.FileAssociatePZX != config.AccociatePZXFiles)
            {
                assoc = (optionWindow.FileAssociatePZX ? " 1.pzx" : " 0.pzx");
                
                iconsChanged = true;
                param += assoc;
            }

            if (optionWindow.FileAssociateTZX != config.AccociateTZXFiles)
            {
                assoc = (optionWindow.FileAssociateTZX ? " 1.tzx" : " 0.tzx");
               
                iconsChanged = true;
                param += assoc;
            }

            if (optionWindow.FileAssociateTAP != config.AccociateTAPFiles)
            {
                assoc = (optionWindow.FileAssociateTAP ? " 1.tap" : " 0.tap");
                
                iconsChanged = true;
                param += assoc;
            }

            if (optionWindow.FileAssociateSNA != config.AccociateSNAFiles)
            {
                assoc = (optionWindow.FileAssociateSNA ? " 1.sna" : " 0.sna");
                
                iconsChanged = true;
                param += assoc;
            }

            if (optionWindow.FileAssociateSZX != config.AccociateSZXFiles)
            {
                assoc = (optionWindow.FileAssociateSZX ? " 1.szx" : " 0.szx");
                
                iconsChanged = true;
                param += assoc;
            }

            if (optionWindow.FileAssociateZ80 != config.AccociateZ80Files)
            {
                assoc = (optionWindow.FileAssociateZ80 ? " 1.z80" : " 0.z80");
                iconsChanged = true;
                param += assoc;
            }

            int exitCode = -1;
            //Force icon refresh in windows explorer shell
            if (iconsChanged)
            {
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.UseShellExecute = true;
                startInfo.WorkingDirectory = Application.StartupPath;
                startInfo.FileName = "ZeroFileAssociater";
                startInfo.Arguments = param;
                startInfo.Verb = "runas";
                startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden; //run silent, run deep...
                try
                {
                    System.Diagnostics.Process p = System.Diagnostics.Process.Start(startInfo);
                    if (p != null)
                        p.WaitForExit();
                    exitCode = p.ExitCode;
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "ZeroFileAssociater launch failed!", MessageBoxButtons.OK);
                    return;
                }
            }

            //file associations updated successfully!
            if (exitCode == 0)
            {
                config.AccociatePZXFiles = optionWindow.FileAssociatePZX;
                config.AccociateTZXFiles = optionWindow.FileAssociateTZX;
                config.AccociateTAPFiles = optionWindow.FileAssociateTAP;
                config.AccociateSNAFiles = optionWindow.FileAssociateSNA;
                config.AccociateSZXFiles = optionWindow.FileAssociateSZX;
                config.AccociateZ80Files = optionWindow.FileAssociateZ80;
                config.AccociateDSKFiles = optionWindow.FileAssociateDSK;
                config.AccociateDSKFiles = optionWindow.FileAssociateTRD;
                config.AccociateDSKFiles = optionWindow.FileAssociateSCL;
                
                SHChangeNotify(0x8000000, 0x1000, IntPtr.Zero, IntPtr.Zero);
                MessageBox.Show("File associations updated successfully!", "File associations", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        private void aboutButton_Click(object sender, EventArgs e)
        {
            if ((aboutWindow == null) || (aboutWindow.IsDisposed))
                aboutWindow = new AboutBox1(this);
            aboutWindow.ShowDialog(this);
            dxWindow.Focus();
            aboutWindow.Dispose();
        }

        private void label1_Click(object sender, EventArgs e)
        {
            dxWindow.Focus();
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            showDiskIndicator = false;
            zx.Reset();
            dxWindow.Focus();
        }

        private void renderingToolStripMenuItem_Paint(object sender, PaintEventArgs e)
        {
               
        }

        //power off
        private void powerButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void hardResetToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            showDiskIndicator = false;
            switch (config.CurrentSpectrumModel)
            {
                case "ZX Spectrum 48k":
                    zx48ktoolStripMenuItem1_Click(this, null);
                    break;
                case "ZX Spectrum 128ke":
                    zXSpectrumToolStripMenuItem_Click(this, null);
                    break;
                case "ZX Spectrum 128k":
                    zXSpectrum128KToolStripMenuItem_Click(this, null);
                    break;
                case "ZX Spectrum +3":
                    zxSpectrum3ToolStripMenuItem_Click(this, null);
                    break;
                case "Pentagon 128k":
                    pentagon128KToolStripMenuItem_Click(this, null);
                    break;
            }
            dxWindow.Focus();
        }

        private void soundButton_Click(object sender, EventArgs e)
        {

        }

        private void GoFullscreen(bool full)
        {
            if (full)
            {
                config.FullScreen = true;
                toolStripMenuItem5.Enabled = false;
                toolStripMenuItem1.Enabled = false;

                if (dxWindow.EnableDirectX)
                {
                //    this.SuspendLayout();
                    oldWindowPosition = this.Location;
                    dxWindow.EnableFullScreen = true;
                    this.Location = new Point(0, 0);
                    //this.Size = new Size(800, 600);
                    
                    this.FormBorderStyle = FormBorderStyle.None;
                    this.WindowState = FormWindowState.Maximized;
                    Screen currentScreen = Screen.FromRectangle(this.RectangleToScreen(ClientRectangle));
                    
                    Region r = new Region(new Rectangle(0, 0, currentScreen.Bounds.Width, currentScreen.Bounds.Height));
                    this.Region = r;
                    pane1.Hide();
                    panel1.Hide();
                    panel2.Hide();
                    panel4.Hide();
                    panel5.Hide();
                    panel3.BackgroundImage = null;
                    panel3.Location = new Point(0, 0);
                    panel3.Size = new Size(currentScreen.Bounds.Width, panel3.Height);
                    panel3.BackgroundImage = ZeroWin.Properties.Resources.bottomPane;//ZiggyWin.Properties.Resources.bottom_panel200;
                    panel3.BackgroundImageLayout = ImageLayout.Stretch;

                    //Maintain 4:3 aspect ration when full screen
                    if (Screen.PrimaryScreen.Bounds.Height < Screen.PrimaryScreen.Bounds.Width)
                    {
                        float aspectXScale = (Screen.PrimaryScreen.Bounds.Height * 4.0f) / (Screen.PrimaryScreen.Bounds.Width * 3.0f);
                        int newScreenWidth = (int)(Screen.PrimaryScreen.Bounds.Width * aspectXScale);
                        dxWindow.Location = new Point((Screen.PrimaryScreen.Bounds.Width - newScreenWidth) / 2, 0);
                        dxWindow.SetSize(newScreenWidth, Screen.PrimaryScreen.Bounds.Height);
                    }
                    else //Not tested!!!
                    {
                        float aspectYScale =  (Screen.PrimaryScreen.Bounds.Width * 3.0f)/ (Screen.PrimaryScreen.Bounds.Height * 4.0f);
                        int newScreenHeight = (int)(Screen.PrimaryScreen.Bounds.Height * aspectYScale);
                        dxWindow.Location = new Point(Screen.PrimaryScreen.Bounds.Width, (Screen.PrimaryScreen.Bounds.Height - newScreenHeight) / 2);
                        dxWindow.SetSize(Screen.PrimaryScreen.Bounds.Width, newScreenHeight);
                    }
                    dxWindow.Focus();
                   // panel3.BringToFront();
                    oldWindowSize = config.WindowSize;
                    config.WindowSize = 0;
                    Point cursorPos = Cursor.Position;
                    cursorPos.Y = this.PointToScreen(panel3.Location).Y;
                    Cursor.Position = cursorPos;
                 //   this.ResumeLayout();
                }
                else
                {
                    this.SuspendLayout();
                    oldWindowPosition = this.Location;
                    dxWindow.EnableFullScreen = true;
                    this.Location = new Point(0, 0);
                    //this.Size = new Size(800, 600);
                    this.FormBorderStyle = FormBorderStyle.None;
                    this.WindowState = FormWindowState.Maximized;
                    Screen currentScreen = Screen.FromRectangle(this.RectangleToScreen(ClientRectangle));
                    Region r = new Region(new Rectangle(0, 0, currentScreen.Bounds.Width, currentScreen.Bounds.Height));
                    this.Region = r;
                    
                    pane1.Hide();
                    panel1.Hide();
                    panel2.Hide();
                    panel4.Hide();
                    panel5.Hide();
                    panel3.BackgroundImage = null;
                    panel3.Location = new Point(0, 0);
                    panel3.Size = new Size(currentScreen.Bounds.Width, panel3.Height);
                    panel3.BackgroundImage = ZeroWin.Properties.Resources.bottomPane;// ZiggyWin.Properties.Resources.bottom_panel200;
                    panel3.BackgroundImageLayout = ImageLayout.Stretch;
                    dxWindow.Location = new Point(0, 0);
                    dxWindow.SetSize(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);

                    dxWindow.Focus();
                    //panel3.BringToFront();
                    oldWindowSize = config.WindowSize;
                    config.WindowSize = 0;
                    Point cursorPos = Cursor.Position;
                    cursorPos.Y = this.PointToScreen(panel3.Location).Y;
                    Cursor.Position = cursorPos;
                    this.ResumeLayout();
                }
                dxWindow.LEDIndicatorPosition = new Point(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            }
            else
            {
                config.FullScreen = false;
                toolStripMenuItem5.Enabled = false;
                toolStripMenuItem1.Enabled = false;
                if (dxWindow.EnableDirectX)
                {
                    dxWindow.EnableFullScreen = false;
                    this.FormBorderStyle = FormBorderStyle.None;
                    this.WindowState = FormWindowState.Normal;
                    this.Location = oldWindowPosition;
                    panel1.Dock = DockStyle.None;
                    dxWindow.Dock = DockStyle.None;
                    config.WindowSize = oldWindowSize;
                    AdjustWindowSize();
                  
                    pane1.Show();
                    panel2.Show();
                    panel3.Show();
                    panel4.Show();
                    panel5.Show();
                }
                else
                {
                    dxWindow.EnableFullScreen = false;
                    this.FormBorderStyle = FormBorderStyle.None;
                    this.WindowState = FormWindowState.Normal;
                    panel1.Dock = DockStyle.None;
                    dxWindow.Dock = DockStyle.None;
                    this.Location = oldWindowPosition;
                    config.WindowSize = oldWindowSize;
                    AdjustWindowSize();
                    pane1.Show();
                    panel2.Show();
                    panel3.Show();
                    panel4.Show();
                    panel5.Show();
                }
                if (CursorIsHidden)
                {
                    Cursor.Show();
                    CursorIsHidden = false;
                }
            }
        }
        private void fullScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoFullscreen(!config.FullScreen);
        }

        private void uLAPlusToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeZXPalette("ULA Plus");
            normalToolStripMenuItem.Checked = false;
            grayscaleToolStripMenuItem.Checked = false;
            uLAPlusToolStripMenuItem.Checked = true;
            dxWindow.Focus();
        }

        private void powerButton_MouseEnter(object sender, EventArgs e)
        {
            powerButton.Image = null;
           // button6.BackgroundImage = Image.FromFile(Application.StartupPath + @"\images\powerGlow2.png");
            powerButton.Image = ZeroWin.Properties.Resources.powerIconGlow;
        }

        private void powerButton_MouseLeave(object sender, EventArgs e)
        {
            powerButton.Image = null;
            //button6.BackgroundImage = Image.FromFile(Application.StartupPath + @"\images\powerDim2.png");
            powerButton.Image = ZeroWin.Properties.Resources.powerIcon;
        }

        private void PauseEmulation(bool val)
        {
            pauseEmulation = val;
            pauseEmulationESCToolStripMenuItem.Checked = pauseEmulation;
            if (pauseEmulation)
                zx.Pause();
            else
                zx.Resume();
        }

        private void pauseEmulationESCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PauseEmulation(!pauseEmulationESCToolStripMenuItem.Checked);
          
        }

        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            powerButton_Click(this, null);
        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            mouseMoveDiff.X = e.Location.X - mouseOldPos.X;
            mouseMoveDiff.Y = e.Location.Y - mouseOldPos.Y;
            
            LastMouseMove = DateTime.Now;
            if (config.FullScreen)
            {
                if ((Math.Abs(mouseMoveDiff.X)) > 5 || (Math.Abs(mouseMoveDiff.Y) > 5))
                {
                    if (CursorIsHidden)
                    {
                        Cursor.Show();
                        CursorIsHidden = false;
                    }
                }
            }
            mouseOldPos = e.Location;
        }

        private void libraryButton_Click(object sender, EventArgs e)
        {
            //library.Show(); 
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                LoadZXFile(files[0]);
            }
        }

        private void UseSZX(SZXLoader szx)
        {
            if (szx.header.MachineId == (int)Speccy.SZXLoader.ZXTYPE.ZXSTMID_48K)
            {
                zx48ktoolStripMenuItem1_Click(this, null);
            }
            else if (szx.header.MachineId == (int)Speccy.SZXLoader.ZXTYPE.ZXSTMID_128K)
            {
                if (config.HighCompatibilityMode)
                    zXSpectrumToolStripMenuItem_Click(this, null);
                else if (config.Model == MachineModel._128k)
                    zXSpectrum128KToolStripMenuItem_Click(this, null);
                else
                    zXSpectrumToolStripMenuItem_Click(this, null);
            }
            else if (szx.header.MachineId == (int)Speccy.SZXLoader.ZXTYPE.ZXSTMID_PENTAGON128)
            {
                pentagon128KToolStripMenuItem_Click(this, null);
            }
            else if (szx.header.MachineId == (int)Speccy.SZXLoader.ZXTYPE.ZXSTMID_PLUS3)
            {
                zxSpectrum3ToolStripMenuItem_Click(this, null);
            }

            zx.UseSZX(szx);

            //Check if file is required in tape deck
            if (szx.InsertTape)
            {
                //External tape file?
                if (szx.tape.flags == 0)
                {
                    String s = "The following tape is expected in the tape deck:\n" + szx.externalTapeFile + "\n\nDo you wish to browse for this file?";
                    if (MessageBox.Show(s, "File request", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        openFileDialog1.InitialDirectory = recentFolder;
                        openFileDialog1.FileName = "";
                        openFileDialog1.Filter = "Tapes (*.pzx, *.tap, *.tzx, *.csw)|*.pzx;*.tap;*.tzx;*.csw|ZIP Archive (*.zip)|*.zip";
                        if (openFileDialog1.ShowDialog() == DialogResult.OK)
                        {
                            LoadZXFile(openFileDialog1.FileName);
                        }
                        recentFolder = Path.GetDirectoryName(openFileDialog1.FileName);
                    }
                }
                else
                {
                    System.IO.Stream s = new System.IO.MemoryStream(szx.embeddedTapeData);
                    tapeDeck.InsertTape("", s);
                }
            }

            //Check if any disks needs to be inserted
            for (int f = 0; f < szx.numDrivesPresent; f++)
            {
                if (szx.InsertDisk[f])
                {
                    String s = String.Format("The following disk is expected in drive {0}:\n{1}\n\nDo you wish to browse for this file?", f, szx.externalDisk[f]);
                    if (MessageBox.Show(s, "File request", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        OpenDiskFile((byte)f);
                    }
                }
            }
        }

        public void LoadSZX(Stream fs)
        {
            SZXLoader szx = new SZXLoader();
            szx.LoadSZX(fs);
            UseSZX(szx);
        }

        public void LoadSZX(string filename)
        {
            SZXLoader szx = new SZXLoader();
            szx.LoadSZX(filename);
            UseSZX(szx);
        }

        public void SaveSZX()
        {
            
        }

        private void UseSNA(SNA_SNAPSHOT sna)
        {
            if (sna == null)
                return;

            if (sna is SNA_48K)
            {
                zx48ktoolStripMenuItem1_Click(this, null);

            }
            else if (sna is SNA_128K)
            {
                // if (((SNA_128K)sna).TR_DOS)
                pentagon128KToolStripMenuItem_Click(this, null);
                /*else
                if (config.HighCompatibilityMode)
                    zXSpectrumToolStripMenuItem_Click(this, null);
                else if (config.Model == MachineModel._128k)
                    zXSpectrum128KToolStripMenuItem_Click(this, null);
                else
                    zXSpectrumToolStripMenuItem_Click(this, null);*/
            }
            zx.UseSNA(sna);
        }

        public void LoadSNA(Stream fs)
        {
            SNA_SNAPSHOT sna = SNALoader.LoadSNA(fs);
            UseSNA(sna);
        }

        public void LoadSNA(string filename)
        {
            SNA_SNAPSHOT sna = SNALoader.LoadSNA(filename);
            UseSNA(sna);
        }

        private void UseRZX(RZXLoader rzx)
        {
            String ext = new String(rzx.snap.extension).ToLower();
            bool playRZX = true;
            if (ext == "sna\0")
                LoadSNA(new MemoryStream(rzx.snapshotData));
            else if (ext == "z80\0")
                LoadZ80(new MemoryStream(rzx.snapshotData));
            else if (ext == "szx\0")
                LoadSZX(new MemoryStream(rzx.snapshotData));
            else
                playRZX = false;

            if (playRZX)
                zx.InitRZX(rzx);
        }

        public void LoadRZX(Stream fs)
        {
            RZXLoader rzx = new RZXLoader();
            rzx.LoadRZX(fs);
            UseRZX(rzx);
        }

        public void LoadRZX(string filename)
        {
            RZXLoader rzx = new RZXLoader();
            rzx.LoadRZX(filename);
            UseRZX(rzx);
        }

        private void UseZ80(Z80_SNAPSHOT z80)
        {
            if (z80 == null)
                return;

            if (z80.TYPE == 0)
            {
                zx48ktoolStripMenuItem1_Click(this, null);
            }
            else if (z80.TYPE == 1)
            {

                if (config.HighCompatibilityMode)
                    zXSpectrumToolStripMenuItem_Click(this, null);
                else if (config.Model == MachineModel._128k)
                    zXSpectrum128KToolStripMenuItem_Click(this, null);
                else
                    zXSpectrumToolStripMenuItem_Click(this, null);
            }
            else if (z80.TYPE == 2)
                zxSpectrum3ToolStripMenuItem_Click(this, null);
            else if (z80.TYPE == 3)
                pentagon128KToolStripMenuItem_Click(this, null);

            zx.UseZ80(z80);
        }

        public void LoadZ80(Stream fs)
        {
            Z80_SNAPSHOT z80 = Z80Loader.LoadZ80(fs);
            UseZ80(z80);
        }

        public void LoadZ80(string filename)
        {
            Z80_SNAPSHOT z80 = Z80Loader.LoadZ80(filename);
            UseZ80(z80);
        }

        private void OpenZXArchive(String zipFileName)
        {
            if (archiver == null)
                archiver = new ComponentAce.Compression.ZipForge.ZipForge();
            archiver.FileName = zipFileName;
            ComponentAce.Compression.Archiver.ArchiveItem archiveItem =
                new ComponentAce.Compression.Archiver.ArchiveItem();

            System.Collections.Generic.List<String> fileNameAndSizeList = new System.Collections.Generic.List<string>();
            String fileToOpen = "";
            try
            {
                archiver.OpenArchive(FileMode.Open);
                
               // string[] fileNameAndSizeList = new string[archiver.FileCount * 2];
                if (archiver.FindFirst("*.*", ref archiveItem))
                {
                    do
                    {
                        if (archiveItem.FileName != "")
                        {
                            String ext = archiveItem.FileName.Substring(archiveItem.FileName.Length - 3).ToLower();
                            if (ext == "sna" | ext == "z80" || ext == "szx" || ext == "pzx" || ext == "tzx"
                                    || ext == "tap" || ext == "csw" || ext == "dsk" || ext == "trd" || ext == "scl" || ext == "rzx")
                            {
                                fileNameAndSizeList.Add(archiveItem.FileName);
                                fileNameAndSizeList.Add((archiveItem.UncompressedSize).ToString());
                            }
                        }
                    }
                    while (archiver.FindNext(ref archiveItem));
                }

                if (fileNameAndSizeList.Count == 0)
                {
                    MessageBox.Show("Couldn't find any suitable file to load in this archive!", "No suitable file", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (fileNameAndSizeList.Count == 2)
                {
                    fileToOpen = fileNameAndSizeList[0];
                }
                else if (fileNameAndSizeList.Count > 2)
                {
                    ArchiveHandler archiveHandler = new ArchiveHandler(fileNameAndSizeList.ToArray());
                    if (archiveHandler.ShowDialog() != DialogResult.OK)
                    {
                        fileNameAndSizeList.Clear();
                        archiver.CloseArchive();
                        return;
                    }
                    fileToOpen = archiveHandler.FileToOpen;
                }

                String ext2 = fileToOpen.Substring(fileToOpen.Length - 3).ToLower();
                
                System.IO.MemoryStream stream = new MemoryStream();
               
                archiver.Options.CreateDirs = false;
                if ((ext2 == "dsk") || (ext2 == "trd") || (ext2 == "scl"))
                {
                    if (diskArchivePath[0] != null)
                    {
                        zx.DiskEject(0);
                        File.Delete(diskArchivePath[0]);
                    }
                    byte[] tempBuffer;
                    archiver.ExtractToBuffer(fileToOpen, out tempBuffer);
                    diskArchivePath[0] = Application.StartupPath + @"/tempDiskA." + ext2;
                    File.WriteAllBytes(diskArchivePath[0], tempBuffer);
                    LoadDSK(diskArchivePath[0], 0);
                    fileNameAndSizeList.Clear();
                    archiver.CloseArchive();
                    return;
                }
                archiver.ExtractToStream(fileToOpen, stream);
                stream.Position = 0;
                switch (ext2)
                {
                    case "rzx":
                        LoadRZX(stream);
                        break;
                    case "sna":
                        LoadSNA(stream);
                        if (tapeDeck.Visible)
                            tapeDeck.Hide();
                        break;
                    case "szx":
                        LoadSZX(stream);
                        if (tapeDeck.Visible)
                            tapeDeck.Hide();
                        break;
                    case "z80":
                        LoadZ80(stream);
                        if (tapeDeck.Visible)
                            tapeDeck.Hide();
                        break;
                    case "pzx":
                        tapeDeck.InsertTape(fileToOpen, stream);
                        //tapeDeck.Show();
                        break;
                    case "tzx":
                    case "tap":
                    case "csw":
                        archiver.BaseDir = Application.StartupPath;
                        //Tricky this. First extract file to local folder then perform usual operations..
                        archiver.Options.CreateDirs = false;
                        archiver.ExtractFiles(fileToOpen);
                        //Call the external pzx tool to convert tzx to pzx
                        System.Diagnostics.Process proc = new System.Diagnostics.Process();
                        proc.EnableRaisingEvents = false;
                        string pzxfile = fileToOpen.Substring(0, fileToOpen.Length - 3);
                        pzxfile = pzxfile + "pzx";

                        if (ext2 == "tzx")
                            proc.StartInfo.FileName = Application.StartupPath + @"\tzx2pzx";
                        else if (ext2 == "tap")
                            proc.StartInfo.FileName = Application.StartupPath + @"\tap2pzx";
                        else
                            proc.StartInfo.FileName = Application.StartupPath + @"\csw2pzx";

                        proc.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                        proc.StartInfo.Arguments = "\"" + archiver.BaseDir + "\\" + fileToOpen + "\" -o \"" + archiver.BaseDir + "\\" + pzxfile + "\"";
                        proc.Start();
                        proc.WaitForExit();

                        //now call pzx loader with correct extension
                        if (System.IO.File.Exists(archiver.BaseDir + "\\" + pzxfile))
                        {
                            tapeDeck.InsertTape(archiver.BaseDir + "\\" + pzxfile);
                           // tapeDeck.Show();

                            //Finally delete PZX file to avoid littering disk with multiple copies of same game.
                            File.Delete(archiver.BaseDir + "\\" + pzxfile);
                            File.Delete(archiver.BaseDir + "\\" + fileToOpen);
                        }
                        else
                            MessageBox.Show("File not found.", "The PZX file is missing!", MessageBoxButtons.OK);
                        break;
                }
            }
            catch (Exception e)
            {
                System.Windows.Forms.MessageBox.Show(e.Message,
                        "Archive failure", System.Windows.Forms.MessageBoxButtons.OK);
            }

            fileNameAndSizeList.Clear();
            archiver.CloseArchive();
            archiver.Dispose();
            archiver = null;
        }

        private void  LoadDSK(string filename, byte _unit)
        {

            String ext = filename.Substring(filename.Length - 3).ToLower();
            if (ext == "dsk")
            {
                if (!(zx is zxPlus3))
                    zxSpectrum3ToolStripMenuItem_Click(this, null);
            }
            else if (ext == "trd")
            {
                if (!(zx is Pentagon128K))
                    pentagon128KToolStripMenuItem_Click(this, null);
            }
            zx.DiskInsert(filename, _unit);
        }

        private void LoadZXFile(string filename)
        {
            String ext = System.IO.Path.GetExtension(filename).ToLower();

            if (ext == ".rzx")
            {
                LoadRZX(filename);
                if (tapeDeck.Visible)
                    tapeDeck.Hide();
            }
            else
            if ((ext == ".dsk") || (ext == ".trd") || (ext == ".scl"))
            {
                LoadDSK(filename, 0);
            }
            else
            if (ext == ".sna")
            {
                LoadSNA(filename);
                if (tapeDeck.Visible)
                    tapeDeck.Hide();
            }
            else if (ext == ".z80")
            {
                LoadZ80(filename);
                if (tapeDeck.Visible)
                    tapeDeck.Hide();
            }
            else if (ext == ".szx")
            {
                LoadSZX(filename);
                if (tapeDeck.Visible)
                    tapeDeck.Hide();
            }
            else if (ext == ".pzx")
            {
                tapeDeck.InsertTape(filename);
                //tapeDeck.Show();
            }
            else if ((ext == ".tzx") || (ext == ".tap") || (ext == ".csw"))
            {
                //Call the external pzx tool to convert tzx to pzx
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.EnableRaisingEvents = false;

                string pzxfile = filename.Substring(0, filename.Length - 3);
                pzxfile = pzxfile + "pzx";

                if (ext == ".tzx")
                    proc.StartInfo.FileName = Application.StartupPath + @"\tzx2pzx";
                else if (ext == ".tap")
                    proc.StartInfo.FileName = Application.StartupPath + @"\tap2pzx";
                else
                    proc.StartInfo.FileName = Application.StartupPath + @"\csw2pzx";

                proc.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                proc.StartInfo.Arguments = "\"" + filename + "\" -o \"" + pzxfile + "\"";
                proc.Start();
                proc.WaitForExit();

                //now call pzx loader with correct extension
                if (System.IO.File.Exists(pzxfile))
                {
                    tapeDeck.InsertTape(pzxfile);
                   // tapeDeck.Show();

                    //Finally delete PZX file to avoid littering disk with multiple copies of same game.
                    File.Delete(pzxfile);
                }
                else
                    MessageBox.Show("Couldn't open the file.", "PZX not found", MessageBoxButtons.OK);
            }
            else if (ext == ".zip") //handle zip archives
            {
                pauseEmulation = true;
                OpenZXArchive(filename);
                pauseEmulation = false;
            }
            else
                System.Windows.Forms.MessageBox.Show("Sorry, but Zero doesn't recognise this file format.",
                   "Unsupported Format", System.Windows.Forms.MessageBoxButtons.OK);
        }

        private void volumeTrackBar1_Scroll(object sender, EventArgs e)
        {
            zx.SetSoundVolume(volumeTrackBar1.Value / 50.0f); //scale from 0 to 2.0f
            config.Volume = volumeTrackBar1.Value;
            toolTip1.SetToolTip(volumeTrackBar1, "Volume  " + config.Volume + "%");
            if (config.Volume == 0)
            {
                soundButton.BackgroundImage = null;
                zx.MuteSound();
                soundButton.BackgroundImage = ZeroWin.Properties.Resources.soundOff;
                config.EnableSound = false;
            }
            else
            {
                if (!config.EnableSound)
                {
                    soundButton.BackgroundImage = null;
                    zx.ResumeSound();
                    soundButton.BackgroundImage = ZeroWin.Properties.Resources.soundOn;
                    config.EnableSound = true;
                }
            }
        }

        private void interlaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dxWindow.ShowScanlines = interlaceToolStripMenuItem.Checked;
        }

        private void screenshotMenuItem1_Click(object sender, EventArgs e)
        {
            saveFileDialog1.InitialDirectory = config.PathScreenshots;
            saveFileDialog1.Title = "Save Screenshot";
            saveFileDialog1.FileName = "";
            saveFileDialog1.Filter = "Bitmap (*.bmp)|*.bmp|PNG (*.png)|*.png|JPEG (*.jpeg)|*.jpeg|GIF (*.gif)|*.gif";
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                Bitmap screenshot = dxWindow.GetScreen();
                switch (saveFileDialog1.FilterIndex)
                {
                    case 0:
                        screenshot.Save(saveFileDialog1.FileName, System.Drawing.Imaging.ImageFormat.Bmp);
                        break;
                    case 1:
                        screenshot.Save(saveFileDialog1.FileName, System.Drawing.Imaging.ImageFormat.Png);
                        break;
                    case 2:
                        screenshot.Save(saveFileDialog1.FileName, System.Drawing.Imaging.ImageFormat.Jpeg);
                        break;
                    case 3:
                        screenshot.Save(saveFileDialog1.FileName, System.Drawing.Imaging.ImageFormat.Gif);
                        break;
                }

                MessageBox.Show("Screenshot has been saved to disk.", "Screenshot taken", MessageBoxButtons.OK);
            }
        }

        private void loadBinaryMenuItem1_Click(object sender, EventArgs e)
        {
            loadBinaryDialog = new LoadBinary(this, true);
            loadBinaryDialog.Show();

        }

        private void saveBinaryMenuItem5_Click(object sender, EventArgs e)
        {
            loadBinaryDialog = new LoadBinary(this, false);
            loadBinaryDialog.Show();
        }

        private void saveSnapshotMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog1.Title = "Save Snapshot";
            saveFileDialog1.FileName = "";
            saveFileDialog1.Filter = "SZX snapshot|*.szx";

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                zx.SaveSZX(saveFileDialog1.FileName);
                MessageBox.Show("Snapshot saved successfully!", "File saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void openFileMenuItem1_Click(object sender, EventArgs e)
        {
            zx.Pause();
            
            dxWindow.Suspend();
            openFileDialog1.InitialDirectory = recentFolder;
            openFileDialog1.Title = "Choose a file";
            openFileDialog1.FileName = "";
            openFileDialog1.Filter = "All supported files|*.szx;*.sna;*.z80;*.pzx;*.tzx;*.tap;*.csw;*.rzx;*.zip;*.dsk;*.trd;*.scl|Snapshots (*.szx, *.sna, *.z80)|*.szx; *.sna;*.z80|Tapes (*.pzx, *.tap, *.tzx, *.csw)|*.pzx;*.tap;*.tzx;*.csw|Disks (*.dsk, *.trd, *.scl)|*.dsk;*.trd;*.scl|ZIP Archive (*.zip)|*.zip|RZX (*.rzx)|*.rzx";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if (debugger != null)
                {
                    debugger.Close();
                    debugger.Dispose();
                    debugger = null;
                }

                LoadZXFile(openFileDialog1.FileName);
                if (debugger != null)
                {
                    debugger = new Monitor(this);
                    debugger.Show();
                }
                recentFolder = Path.GetDirectoryName(openFileDialog1.FileName);
            }
    
            dxWindow.Resume();
            zx.Resume();
            dxWindow.Focus();
        }

        private void trainerToolStripMenuItem_Click(object sender, EventArgs e)
        {
           
        }

        private void AdjustWindowSize()
        {
            //if (config.FullScreen)
            //    GoFullscreen(false);

            this.Hide();
            //dxWindow.EnableFullScreen = false;

            fullScreenToolStripMenuItem.Checked = false;

            int speccyWidth = zx.GetTotalScreenWidth();
            int speccyHeight = zx.GetTotalScreenHeight();
            int totalClientWidth = speccyWidth + 40;
            int totalClientHeight = speccyHeight + 56;

            int adjustWidth = (speccyWidth * config.WindowSize) / 100;
            int adjustHeight = (speccyHeight * config.WindowSize) / 100;

            int borderAdjust = config.BorderSize + (config.BorderSize * config.WindowSize) / 100;
                      
            panel2.BackgroundImage = ZeroWin.Properties.Resources.topPane;
            panel4.BackgroundImage = ZeroWin.Properties.Resources.leftPane3;
            panel3.BackgroundImage = ZeroWin.Properties.Resources.bottomPane;
            //panel5.BackgroundImage = ZiggyWin.Properties.Resources.rightPane12;

            panel5.BackgroundImage = null;

            this.Size = new Size(totalClientWidth + adjustWidth - (2 * borderAdjust), totalClientHeight + adjustHeight - (2 * borderAdjust));
            Bitmap bmpForRegion = new Bitmap(this.Size.Width, this.Size.Height);
            Graphics bmpg = Graphics.FromImage(bmpForRegion);
            bmpg.Clear(Color.FromArgb(255, 153, 255));

            panel4.Location = new Point(0, 0);
            panel4.Size = new Size(20, totalClientHeight + adjustHeight - (2 * borderAdjust));
            panel4.DrawToBitmap(bmpForRegion, new Rectangle(panel4.Location, panel4.Size));

            panel2.Size = new Size(speccyWidth + adjustWidth - (2 * borderAdjust), 28);
            panel2.Location = new Point(panel4.Width, 0);
            panel2.DrawToBitmap(bmpForRegion, new Rectangle(panel2.Location, panel2.Size));

            panel3.Location = new Point(panel4.Width, panel4.Height - panel3.Height);
            panel3.Size = new Size(panel2.Width, panel2.Height);
            panel3.DrawToBitmap(bmpForRegion, new Rectangle(panel3.Location, panel3.Size));

            panel5.Location = new Point(panel3.Width + panel4.Width, 0);
            panel5.Size = new Size(20, totalClientHeight + adjustHeight - (2 * borderAdjust));

            pane1.Location = new Point(panel3.Width + panel4.Width, 0);
            pane1.Size = new Size(20, totalClientHeight + adjustHeight - (2 * borderAdjust));
            pane1.DrawToBitmap(bmpForRegion, new Rectangle(pane1.Location, pane1.Size));

            panel1.Location = new Point(panel4.Width, panel2.Height);
            panel1.Size = new Size(pane1.Location.X - panel1.Location.X , panel3.Location.Y - panel1.Location.Y);
            
            
            panel1.DrawToBitmap(bmpForRegion, new Rectangle(panel1.Location, panel1.Size));
            this.Region = GetRegion(bmpForRegion, Color.FromArgb(255, 153, 255));

            dxWindow.Location = new Point(panel4.Width - borderAdjust, panel2.Height - borderAdjust);
            dxWindow.SetSize(speccyWidth + adjustWidth, speccyHeight + adjustHeight);
            dxWindow.SendToBack();

            //dxWindow.TapeIndicatorPosition = new Point(dxWindow.Width - panel1.Width, dxWindow.Height - panel3.Height);
            dxWindow.LEDIndicatorPosition = new Point(pane1.Location.X - dxWindow.Location.X, panel3.Location.Y - dxWindow.Location.Y);
            dxWindow.Focus();
            dxWindow.Invalidate();
            this.CenterToScreen();
            this.Show();

            if ((totalClientWidth + ((totalClientWidth * (config.WindowSize + 50)) / 100) - (2 * borderAdjust) > Screen.PrimaryScreen.Bounds.Width) ||
                ((totalClientHeight + ((totalClientHeight * (config.WindowSize + 50)) / 100) - (2 * borderAdjust)) > Screen.PrimaryScreen.Bounds.Height))
            {
                toolStripMenuItem5.Enabled = false;
            }
             else
                 toolStripMenuItem5.Enabled = true;

            if (config.WindowSize == 0)
                 toolStripMenuItem1.Enabled = false;
             else
                 toolStripMenuItem1.Enabled = true;
            bmpForRegion.Dispose();
            bmpForRegion = null;
            bmpg.Dispose();
        }

        private void toolStripMenuItem5_Click_1(object sender, EventArgs e)
        {
            if (config.WindowSize < 500)
                config.WindowSize += 50; //Increase window size by 50% of normal

            AdjustWindowSize();
        }

        private void panel4_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            e.Graphics.DrawImage(panel4.BackgroundImage, panel4.Location.X, panel4.Location.Y, panel4.Width, panel4.Height);
            
        }

        private void panel5_Paint(object sender, PaintEventArgs e)
        {
           // e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
           // e.Graphics.DrawImage(ZiggyWin.Properties.Resources.rightPane11, panel5.Location.X, panel5.Location.Y, panel5.Width, panel5.Height);
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (config.WindowSize > 0)
                config.WindowSize -= 50;

            AdjustWindowSize();
        }

        private Region GetRegion(Bitmap _img, Color color)
        {
            Color _matchColor = Color.FromArgb(color.R, color.G, color.B);
            System.Drawing.Region rgn = new Region();
            rgn.MakeEmpty();
            Rectangle rc = new Rectangle(0, 0, 0, 0);
            bool inimage = false;

            for (int y = 0; y < _img.Height; y++)
            {
                for (int x = 0; x < _img.Width; x++)
                {
                    Color imgPixel = _img.GetPixel(x, y);
                    if (!inimage)
                    {
                        if (imgPixel != _matchColor)
                        {
                            inimage = true;
                            rc.X = x;
                            rc.Y = y;
                            rc.Height = 1;
                        }
                    }
                    else
                    {
                        if (imgPixel == _matchColor)
                        {
                            inimage = false;
                            rc.Width = x - rc.X;
                            rgn.Union(rc);
                        }
                    }
                }

                if (inimage)
                {
                    inimage = false;
                    rc.Width = _img.Width - rc.X;
                    rgn.Union(rc);
                }
            }
            return rgn;
        }

        private void contextMenuStrip1_VisibleChanged(object sender, EventArgs e)
        {
            pauseEmulation = contextMenuStrip1.Visible;
            if (CursorIsHidden)
            {
                CursorIsHidden = false;
                Cursor.Show();
            }
        }

        private void paneRight_MouseDown(object sender, MouseEventArgs e)
        {
            Form_MouseDown(sender, e);
            dxWindow.Focus();
        }

        private void paneRight_MouseMove(object sender, MouseEventArgs e)
        {
            Form_MouseMove(sender, e);
            dxWindow.Focus();
        }

 
        private void pictureBox1_Click(object sender, EventArgs e)
        {
            if ((aboutWindow == null) || (aboutWindow.IsDisposed))
                aboutWindow = new AboutBox1(this);
            aboutWindow.ShowDialog(this);
            dxWindow.Focus();
        }

        private void machineButton_MouseEnter(object sender, EventArgs e)
        {
            machineButton.BackgroundImage = ZeroWin.Properties.Resources.newBaseIconGlow;
        }

        private void machineButton_MouseLeave(object sender, EventArgs e)
        {
            machineButton.BackgroundImage = ZeroWin.Properties.Resources.newBaseIcon;
        }

        private void fileButton_MouseEnter(object sender, EventArgs e)
        {
            fileButton.BackgroundImage = ZeroWin.Properties.Resources.newBaseIconGlow;
        }

        private void fileButton_MouseLeave(object sender, EventArgs e)
        {
            fileButton.BackgroundImage = ZeroWin.Properties.Resources.newBaseIcon;
        }

        private void monitorButton_MouseEnter(object sender, EventArgs e)
        {
            monitorButton.BackgroundImage = ZeroWin.Properties.Resources.newBaseIconGlow;
        }

        private void monitorButton_MouseLeave(object sender, EventArgs e)
        {
            monitorButton.BackgroundImage = ZeroWin.Properties.Resources.newBaseIcon;
        }

        private void optionsButton_MouseEnter(object sender, EventArgs e)
        {
            optionsButton.BackgroundImage = ZeroWin.Properties.Resources.newBaseIconGlow;
        }

        private void optionsButton_MouseLeave(object sender, EventArgs e)
        {
            optionsButton.BackgroundImage = ZeroWin.Properties.Resources.newBaseIcon;
        }

        private void volumeTrackBar1_MouseLeave(object sender, EventArgs e)
        {
            volumeTrackBar1.Hide();
            
        }

        private void soundButton_MouseHover(object sender, EventArgs e)
        {
            volumeTrackBar1.Width = 80 + (config.FullScreen ? 200 : config.WindowSize);
            volumeTrackBar1.Location = new Point(soundButton.Location.X - volumeTrackBar1.Width + soundButton.Width + 5, soundButton.Location.Y);
            volumeTrackBar1.Refresh();
            volumeTrackBar1.Show();
            volumeTrackBar1.Capture = false;
           // Point cursorPos = new Point(this.PointToScreen(volumeTrackBar1.Location).X + 30 , Cursor.Position.Y);
            Point cursorPos = new Point(panel3.PointToScreen(volumeTrackBar1.Location).X + (volumeTrackBar1.Value * volumeTrackBar1.Width) / 100, Cursor.Position.Y);
            Cursor.Position = cursorPos;
        }

        private bool OpenDiskFile(byte _unit)
        {
            bool isError = false;

            openFileDialog1.InitialDirectory = recentFolder;
            openFileDialog1.Title = "Choose a file";
            openFileDialog1.FileName = "";
            if (zx is zxPlus3)
                openFileDialog1.Filter = "All supported files|*.dsk; *.zip|+3 Disks (*.dsk)|*.dsk|ZIP Archive (*.zip)|*.zip";
            else
                openFileDialog1.Filter = "All supported files|*.trd;*.scl;*.zip|Beta Disks (*.trd, *.scl)|*.trd;*.scl|ZIP Archive (*.zip)|*.zip";

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if (debugger != null)
                {
                    debugger.Close();
                    debugger.Dispose();
                    debugger = null;
                }

                String ext = System.IO.Path.GetExtension(openFileDialog1.FileName).ToLower();
                if (ext == ".zip") //handle zip archives
                {
                    if (archiver == null)
                        archiver = new ComponentAce.Compression.ZipForge.ZipForge();
                    pauseEmulation = true;
                    archiver.FileName = openFileDialog1.FileName;
                    ComponentAce.Compression.Archiver.ArchiveItem archiveItem =
                        new ComponentAce.Compression.Archiver.ArchiveItem();

                    String fileToOpen = "";
                    try
                    {
                        archiver.OpenArchive(FileMode.Open);
                        System.Collections.Generic.List<String> fileNameAndSizeList = new System.Collections.Generic.List<string>();
                        
                        if (archiver.FindFirst("*.*", ref archiveItem))
                        {
                            do
                            {
                                if (archiveItem.FileName != "")
                                {
                                    String ext2 = archiveItem.FileName.Substring(archiveItem.FileName.Length - 3).ToLower();
                                    if (zx is zxPlus3)
                                    {
                                        if (ext2 == "dsk")
                                        {
                                            fileNameAndSizeList.Add(archiveItem.FileName);
                                            fileNameAndSizeList.Add((archiveItem.UncompressedSize).ToString());
                                        }
                                    }
                                    else if (zx is Pentagon128K)
                                    {
                                        if ((ext2 == "trd") || (ext2 == "scl"))
                                        {
                                            fileNameAndSizeList.Add(archiveItem.FileName);
                                            fileNameAndSizeList.Add((archiveItem.UncompressedSize).ToString());
                                        }
                                    }
                                }
                            }
                            while (archiver.FindNext(ref archiveItem));
                        }

                        if (fileNameAndSizeList.Count == 0)
                        {
                            MessageBox.Show("Couldn't find any suitable file to load in this archive.", "No suitable file", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            isError = true;
                        }
                        else
                        {
                            if (fileNameAndSizeList.Count == 2)
                            {
                                fileToOpen = fileNameAndSizeList[0];
                            }
                            else if (fileNameAndSizeList.Count > 2)
                            {
                                ArchiveHandler archiveHandler = new ArchiveHandler(fileNameAndSizeList.ToArray());
                                if (archiveHandler.ShowDialog() == DialogResult.OK)
                                {
                                    fileToOpen = archiveHandler.FileToOpen;
                                }
                                else
                                    isError = true;
                            }

                            if (!isError)
                            {
                                String ext2 = fileToOpen.Substring(fileToOpen.Length - 3).ToLower();

                                System.IO.MemoryStream stream = new MemoryStream();

                                archiver.Options.CreateDirs = false;
                                // if ((ext2 == "dsk") || (ext2 == "trd") || (ext2 == "scl"))
                                if (diskArchivePath[_unit] != null)
                                    File.Delete(diskArchivePath[_unit]);
                                 
                                byte[] tempBuffer;
                                archiver.ExtractToBuffer(fileToOpen, out tempBuffer);
                                diskArchivePath[_unit] = Application.StartupPath + @"/tempDisk" + _unit.ToString() + "." + ext2;
                                File.WriteAllBytes(diskArchivePath[_unit], tempBuffer);
                                fileToOpen = diskArchivePath[_unit];

                                LoadDSK(fileToOpen, _unit); //All is well then!
                            }
                        }

                        // We don't need the opened archive info anymore, so clean up
                        fileNameAndSizeList.Clear();
                        archiver.CloseArchive();
                        archiver.Dispose();
                        archiver = null;
                    }
                    catch (Exception e)
                    {
                        System.Windows.Forms.MessageBox.Show(e.Message,
                                "Archive failure", System.Windows.Forms.MessageBoxButtons.OK);
                        isError = true;
                    }

                    pauseEmulation = false;
                }
                else
                   LoadDSK(openFileDialog1.FileName, _unit);

                if (debugger != null)
                {
                    debugger = new Monitor(this);
                    debugger.Show(); 
                }

                recentFolder = Path.GetDirectoryName(openFileDialog1.FileName);
            }
            return isError;
        }

        private void insertDiskAToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (zx.DisInserted(0))
            {
                zx.DiskEject(0);
            }
            else
            {
                OpenDiskFile(0);
            }
        }

        private void insertDiskBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (zx.DisInserted(1))
            {
                zx.DiskEject(1);
            }
            else
            {
                OpenDiskFile(1);
            }
        }

        private void insertDiskCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (zx.DisInserted(2))
            {
                zx.DiskEject(2);
            }
            else
            {
                OpenDiskFile(2);
            }
        }

        private void insertDiskDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (zx.DisInserted(3))
            {
                zx.DiskEject(3);
            }
            else
            {
                OpenDiskFile(3);
            }
        }

        private void searchOnlineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if ((infoseekWiz == null) || (infoseekWiz.IsDisposed))
            {
                infoseekWiz = new Infoseeker();
                infoseekWiz.DownloadCompleteEvent += new FileDownloadHandler(OnFileDownloadEvent);
            }
            pauseEmulation = true;
            infoseekWiz.ShowDialog();

            pauseEmulation = false;
        }

        private void tapeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tapeDeck != null)
            {
                tapeDeck.Show();
                return;
            }
        }

        private void cheatHelperToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if ((trainerWiz == null) || (trainerWiz.IsDisposed))
                trainerWiz = new Trainer_Wizard(this);
            pauseEmulation = true;
            trainerWiz.ShowDialog(this);

            pauseEmulation = false;
            trainerWiz.Dispose();
            this.BringToFront();
        }

        private void toolboxButton_Click(object sender, EventArgs e)
        {
            contextMenuStrip3.Show(toolboxButton, 0, toolboxButton.Size.Height);
        }

        private void toolboxButton_MouseLeave(object sender, EventArgs e)
        {
            toolboxButton.BackgroundImage = ZeroWin.Properties.Resources.newBaseIcon;
        }

        private void toolboxButton_MouseEnter(object sender, EventArgs e)
        {
            toolboxButton.BackgroundImage = ZeroWin.Properties.Resources.newBaseIconGlow;
        }

        private void aboutZeroToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (aboutWindow == null)
                aboutWindow = new AboutBox1(this);
            aboutWindow.ShowDialog(this);
            dxWindow.Focus();
        }

   
    }
}
