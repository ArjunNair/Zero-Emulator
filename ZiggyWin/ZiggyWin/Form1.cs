#define ENABLE_WM_EXCHANGE

using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Speccy;
using Peripherals;

namespace ZeroWin
{
    public partial class Form1 : Form
    {

#if ENABLE_WM_EXCHANGE

        [StructLayout(LayoutKind.Sequential)]
        private struct COPYDATASTRUCT
        {
            [MarshalAs(UnmanagedType.SysInt)]
            public IntPtr dwData;
            [MarshalAs(UnmanagedType.I4)]
            public int cbData;
            [MarshalAs(UnmanagedType.SysInt)]
            public IntPtr lpData;
        }
        [System.Runtime.InteropServices.DllImport("User32.dll")]
        private static extern bool SendMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("User32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int wMsg, IntPtr wParam, IntPtr lParam);

#if _DEBUG
const string WmCpyDta = "WmCpyDta_d.dll";
#else
        const string WmCpyDta = "WmCpyDta.dll";
#endif

        const int WM_COPYDATA = 0x004A;
        const int WM_USER = 0x8000;
        const int WM_KEYDOWN = 0x100;
        const int WM_KEYUP = 0x101;
        const int WM_SYSCOMMAND = 0x0112;
        const int WM_NCLBUTTONDBLCLK = 0x00A3; //double click on a title bar a.k.a. non-client area of the form
        const int WM_MAXIMIZE = 0xF030;

        IntPtr tempHandle;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct EMU_STATE
        {
            public int tstates;
            public short PC;
            public short SP;
            public short BC;
            public short DE;
            public short HL;
            public short AF;
            public short _BC;
            public short _DE;
            public short _HL;
            public short _AF;
            public short IX;
            public short IY;
            public byte I;
            public byte R;
            public byte IM;
        }
#endif

        [System.Runtime.InteropServices.DllImport(@"pzxtools.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern System.IntPtr tzx2pzx(string input_name, ref uint outSize);

        [System.Runtime.InteropServices.DllImport(@"pzxtools.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern System.IntPtr csw2pzx(string input_name, ref uint outSize);

        [System.Runtime.InteropServices.DllImport(@"pzxtools.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern System.IntPtr pzx2wav(string input_name, ref uint outSize);

        [System.Runtime.InteropServices.DllImport(@"pzxtools.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern System.IntPtr tap2pzx(string input_name, ref uint outSize);

        [System.Runtime.InteropServices.DllImport(@"pzxtools.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void pzx_close();

        [System.Runtime.InteropServices.DllImport(@"pzxtools.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void wav_close();

        [System.Runtime.InteropServices.DllImport(@"pzxtools.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern System.IntPtr tzx2pzx_buff(byte[] buff, uint buffSize, ref uint outSize);

        [System.Runtime.InteropServices.DllImport(@"pzxtools.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern System.IntPtr csw2pzx_buff(byte[] buff, uint buffSize, ref uint outSize);

        [System.Runtime.InteropServices.DllImport(@"pzxtools.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern System.IntPtr tap2pzx_buff(byte[] buff, uint buffSize, ref uint outSize);

        enum EMULATOR_STATE
        {
            NONE,
            IDLE,
            PAUSED,
            RESET,
            PLAYING_RZX,
            RECORDING_RZX,
        }

        private EMULATOR_STATE prevState = EMULATOR_STATE.NONE;
        private EMULATOR_STATE state = EMULATOR_STATE.IDLE;
        private ZRenderer dxWindow;
        private Monitor debugger;
        private Options optionWindow;
        private AboutBox1 aboutWindow;
        private ZLibrary library;
        private TapeDeck tapeDeck;
        private LoadBinary loadBinaryDialog;
        private Trainer_Wizard trainerWiz;
        private Infoseeker infoseekWiz;
        private SpectrumKeyboard speccyKeyboard;
        private Tools.BASICImporter basicImporter;
        public ZeroConfig config = new ZeroConfig();
        private PrecisionTimer timer = new PrecisionTimer();
        private ComponentAce.Compression.ZipForge.ZipForge archiver;
        private MouseController mouse = new MouseController();
        private MRUManager mruManager;
        public Logger logger = new Logger();
        public Speccy.zxmachine zx;

        //The pentagon has a different screen size to other speccy's.
        //However, when rendering we will match the normal speccy dimensions by using the offsets below
        private const int PENTAGON_WINDOW_OFFSET_X = 32;

        private const int PENTAGON_WINDOW_OFFSET_Y = 8;

        public JoystickController joystick1 = new JoystickController();
        public JoystickController joystick2 = new JoystickController();
        public byte[] joystick1ButtonMap = new byte[0];
        public byte[] joystick2ButtonMap = new byte[0];
        private ToolStripMenuItem EjectA;
        private ToolStripMenuItem EjectB;
        private ToolStripMenuItem EjectC;
        private ToolStripMenuItem EjectD;

        private int joystick1Index = -1;     //Index of the PC joystick selected by user
        private int joystick2Index = -1;     //Index of the PC joystick selected by user

        private int joystick1MapIndex = 0;  //Maps to one of the JoysticksEmulated enum in speccy.cs
        private int joystick2MapIndex = 0;  //Maps to one of the JoysticksEmulated enum in speccy.cs


        private MachineModel previousMachine = MachineModel._48k;

        private bool capsLockOn = false;
        private bool shiftIsPressed = false;
        private bool altIsPressed = false;
        private bool ctrlIsPressed = false;

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

        private const string ZX_SPECTRUM_48K = "ZX Spectrum 48k";
        private const string ZX_SPECTRUM_128K = "ZX Spectrum 128k";
        private const string ZX_SPECTRUM_128KE = "ZX Spectrum 128ke";
        private const string ZX_SPECTRUM_PLUS3 = "ZX Spectrum +3";
        private const string ZX_SPECTRUM_PENTAGON_128K = "Pentagon 128k";

        private int rzxFramesToPlay = 0;
        private int frameCount = 0;
        private double lastTime = 0;
        private double frameTime = 0;
        private double totalFrameTime = 0;
        private int averageFPS = 50;
        private bool softResetOnly = false;
        private bool isResizing = false;
        private bool isPlayingRZX = false;
        public bool invokeMonitor = false;
        public bool pauseEmulation = false;
        public bool tapeFastLoad = false;
        private Point mouseOrigin;
        private Point mouseMoveDiff;
        private Point mouseOldPos;
        private Point oldWindowPosition = new Point();
        private int oldWindowSize = -1;
        public String recentFolder = ".";
        private String ZeroSessionSnapshotName = "_z0_session.szx";

        //LED Indicator states
        private bool showTapeIndicator = false;
        public bool ShowTapeIndicator
        {
            get { return showTapeIndicator; }
            set
            {
                showTapeIndicator = value;
                tapeStatusLabel.Enabled = showDiskIndicator;
            }
        }

        private bool showDiskIndicator = false;
        public bool ShowDiskIndicator
        {
            get { return showDiskIndicator; }
            set
            {
                showDiskIndicator = value;
                diskStatusLable.Enabled = showDiskIndicator;
            }
        }

        public bool showDownloadIndicator = false;
        private int downloadIndicatorTimeout = 0;

        private bool romLoaded = false;
        private string[] diskArchivePath = { null, null, null, null };    //Any temp disk file created by the archiver from a .zip file
        private int borderAdjust = 0;

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
            SPACE, SHIFT, CTRL, ALT, TAB, CAPS, ENTER, BACK,
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

        private const int SV_LAST_K = 23560; //last pressed key
        private const int SV_FLAGS = 23611;  //bit 5 of FLAGS is set to indicate keypress
        private bool commandLineLaunch = false;
        private bool doAutoLoadTape = false;
        private int autoTapeLoadCounter = 0;
        private string autoLoadFile = null;
        private byte[] AutoLoadTape48Keys = { 239, 34, 34, 13 }; //LOAD "" + Enter

        //For the +3, we have to type in individual keys, which makes it a bit more involved:
        //Cursor DOWN + ENTER (to enter BASIC) then this string: load "t:":load "" + Enter
        private byte[] AutoLoadTapePlus3Keys = { 10, 13, 108, 111, 97, 100, 32, 34, 116, 58, 34, 58, 108, 111, 97, 100, 32, 34, 34, 13 };

        public TimeSpan TimeoutToHide { get; private set; }

        public DateTime LastMouseMove { get; private set; }

        public bool CursorIsHidden { get; private set; }

#if ENABLE_WM_EXCHANGE
        unsafe protected override void WndProc(ref Message message)
        {
            if(message.Msg == WM_SYSCOMMAND)
            {
                if(message.WParam == new IntPtr(WM_MAXIMIZE))
                {
                    GoFullscreen(true);
                    return;
                }
                else if(message.WParam == new IntPtr(0xF120)) //Restore?
                {
                    GoFullscreen(false);
                    return;
                }
            }
            else if(message.Msg == WM_NCLBUTTONDBLCLK)
            {
                GoFullscreen(true);
                return;
            }
            else if(message.Msg == WM_COPYDATA)
            {
                COPYDATASTRUCT data = (COPYDATASTRUCT)
                    message.GetLParam(typeof(COPYDATASTRUCT));

                byte[] b = System.BitConverter.GetBytes((int)(data.dwData));
                string str = String.Format("{3}{2}{1}{0}", (char)b[0], (char)b[1], (char)b[2], (char)b[3]);

                if(str == "PAUS")
                {
                    if(!pauseEmulation)
                        PauseEmulation(true);

                    SendWMCOPYDATA("SUAP", message.WParam, (IntPtr)0, 0);
                }
                else if(str == "SNAP")
                {
                    string snapFile = Marshal.PtrToStringAnsi(data.lpData);
                    LoadZXFile(snapFile);
                    SendWMCOPYDATA("PANS", message.WParam, (IntPtr)0, 0);
                }
                else if(str == "STEP")
                {
                    tempHandle = message.WParam;

                    PostMessage(this.Handle, WM_USER + 2, message.WParam, data.dwData);
                }
            }
            else if(message.Msg == WM_USER + 2)
            {
                zx.externalSingleStep = true;
                zx.Run();
                zx.UpdateScreenBuffer(zx.FrameLength);
                ForceScreenUpdate();
                EMU_STATE emuState = new EMU_STATE();
                emuState.tstates = zx.totalTStates;
                emuState.PC = (short)zx.PC;
                emuState.SP = (short)zx.SP;
                emuState.IX = (short)zx.IX;
                emuState.IY = (short)zx.IY;
                emuState.HL = (short)zx.HL;
                emuState.DE = (short)zx.DE;
                emuState.BC = (short)zx.BC;
                emuState.AF = (short)zx.AF;
                emuState._HL = (short)zx._HL;
                emuState._DE = (short)zx._DE;
                emuState._BC = (short)zx._BC;
                emuState._AF = (short)zx._AF;
                emuState.I = (byte)zx.I;
                emuState.R = (byte)zx.R;
                emuState.IM = (byte)zx.interruptMode;

                IntPtr lpStruct = Marshal.AllocHGlobal(
                    Marshal.SizeOf(emuState));

                Marshal.StructureToPtr(emuState, lpStruct, false);

                int emuStatSize = Marshal.SizeOf(emuState);
                SendWMCOPYDATA("PETS", tempHandle, lpStruct, emuStatSize);
                Marshal.FreeHGlobal(lpStruct);
                zx.externalSingleStep = false;
            }
            base.WndProc(ref message);
        }

        unsafe private void SendWMCOPYDATA(String s, IntPtr _hTarget, IntPtr _lpData, int _size)
        {
            byte[] carray = System.Text.ASCIIEncoding.UTF8.GetBytes(s);
            uint val = BitConverter.ToUInt32(carray, 0);

            IntPtr dwData = (IntPtr)val;

            COPYDATASTRUCT data = new COPYDATASTRUCT();
            data.dwData = dwData;
            data.cbData = _size;
            data.lpData = _lpData;

            IntPtr lpStruct = Marshal.AllocHGlobal(
                Marshal.SizeOf(data));

            Marshal.StructureToPtr(data, lpStruct, false);

            SendMessage(_hTarget, WM_COPYDATA, this.Handle, lpStruct);
            Marshal.FreeHGlobal(lpStruct);
        }
#endif

        //Delegate for MRUManager (Recent files)
        private void MRUOpenFile_handler(object obj, EventArgs evt)
        {
            ToolStripItem item = (obj as ToolStripItem);
            int index = (item.OwnerItem as ToolStripMenuItem).DropDownItems.IndexOf(item);
            string fName = item.Text;
            string fullFilePath = mruManager.GetFullFilePath(index);

            if(!File.Exists(fullFilePath))
            {
                if(MessageBox.Show(string.Format("{0} doesn't exist. Remove from recent " +
                 "files?", fName), "File not found",
                 MessageBoxButtons.YesNo) == DialogResult.Yes)
                    this.mruManager.RemoveRecentFile(fullFilePath);
                return;
            }

            //Move this file to the top
            this.mruManager.RemoveRecentFile(fullFilePath);
            this.mruManager.AddRecentFile(fullFilePath);

            zx.Pause();
            dxWindow.Suspend();
            LoadZXFile(fullFilePath);
            dxWindow.Resume();
            zx.Resume();
            dxWindow.Focus();
        }

        private void CloseInfoseekWiz(object sender, EventArgs e)
        {
            ((System.Timers.Timer)sender).Enabled = false;
            infoseekWiz.Hide();
            bool oldAutoLoadValue = tapeDeck.DoAutoTapeLoad;
            tapeDeck.DoAutoTapeLoad = true;
            LoadZXFile(autoLoadFile);
            tapeDeck.DoAutoTapeLoad = oldAutoLoadValue;
        }

        public void OnFileDownloadEvent(Object sender, AutoLoadArgs arg)
        {
            if(!string.IsNullOrEmpty(arg.filePath))
            {
                if(MessageBox.Show("You've selected a file to auto-load on completion of download. Auto-load now?", "Auto Load", MessageBoxButtons.OKCancel) == System.Windows.Forms.DialogResult.OK)
                {
                    autoLoadFile = arg.filePath;

                    //  DispatcherTimer setup

                    System.Timers.Timer dispatcherTimer = new System.Timers.Timer();
                    dispatcherTimer.Elapsed += new System.Timers.ElapsedEventHandler(CloseInfoseekWiz);
                    dispatcherTimer.Interval = 500;
                    dispatcherTimer.Enabled = true;
                    dispatcherTimer.SynchronizingObject = infoseekWiz;
                    dispatcherTimer.Start();
                }
            }
            else
            {
                showDownloadIndicator = true;
                downloadIndicatorTimeout = 5000;
            }
        }

        public void DiskMotorEvent(Object sender, DiskEventArgs e)
        {
            int driveState = e.EventType;

            if((driveState & 0x10) == 0)
            {
                showDiskIndicator = false;
            }
            else
            {
                showDiskIndicator = true;
            }
        }

        public void RZXCallback(RZXFileEventArgs rzxArgs)
        {

            if(rzxArgs.hasEnded)
            {
                SetEmulationState(EMULATOR_STATE.IDLE);
                UpdateRZXInterface();
                return;
            }

            switch(rzxArgs.blockID)
            {
                case RZX_BlockType.CREATOR:
                Console.WriteLine(rzxArgs.info);
                rzxFramesToPlay = rzxArgs.totalFramesInRecords;
                break;

                case RZX_BlockType.SNAPSHOT:
                if(state == EMULATOR_STATE.RECORDING_RZX)
                    softResetOnly = true;

                if(rzxArgs.snapData.extension == "sna\0")
                    LoadSNA(new MemoryStream(rzxArgs.snapData.data));
                else if(rzxArgs.snapData.extension == "z80\0")
                    LoadZ80(new MemoryStream(rzxArgs.snapData.data));
                else if(rzxArgs.snapData.extension == "szx\0")
                    LoadSZX(new MemoryStream(rzxArgs.snapData.data));
                break;
                case RZX_BlockType.RECORD:
                zx.tstates = (int)rzxArgs.tstates;
                break;
            }

            if(rzxArgs.rzxInstance != null)
                zx.rzx = rzxArgs.rzxInstance;
        }

        public Form1()
        {
            InitializeComponent();
            EjectA = new ToolStripMenuItem("Eject");
            this.EjectA.Click += new EventHandler(EjectA_Click);
            EjectB = new ToolStripMenuItem("Eject");
            this.EjectB.Click += new EventHandler(EjectB_Click);
            EjectC = new ToolStripMenuItem("Eject");
            this.EjectC.Click += new EventHandler(EjectC_Click);
            EjectD = new ToolStripMenuItem("Eject");
            this.EjectD.Click += new EventHandler(EjectD_Click);
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

            TimeoutToHide = TimeSpan.FromSeconds(5);

            panel1.SendToBack();
            //this.Icon = ZeroWin.Properties.Resources.ZeroIcon;
        }

        private void EjectD_Click(object sender, EventArgs e)
        {
            zx.DiskEject(3);
            insertDiskDToolStripMenuItem.Text = "D: -- Empty --";
            insertDiskDToolStripMenuItem.DropDownItems.Clear();
        }

        private void EjectC_Click(object sender, EventArgs e)
        {
            zx.DiskEject(2);
            insertDiskCToolStripMenuItem.Text = "C: -- Empty --";
            insertDiskCToolStripMenuItem.DropDownItems.Clear();
        }

        private void EjectB_Click(object sender, EventArgs e)
        {
            zx.DiskEject(1);
            insertDiskBToolStripMenuItem.Text = "B: -- Empty --";
            insertDiskBToolStripMenuItem.DropDownItems.Clear();
        }

        private void EjectA_Click(object sender, EventArgs e)
        {
            zx.DiskEject(0);
            insertDiskAToolStripMenuItem.Text = "A: -- Empty --";
            insertDiskAToolStripMenuItem.DropDownItems.Clear();
        }

        protected override void OnActivated(EventArgs e)
        {
            //pauseEmulation = false;
            base.OnActivated(e);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            if(config.PauseOnFocusLost)
                if(!(AppHasFocus()))
                {
                    // pauseEmulation = true;
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

        public bool AppHasFocus()
        {
            if((Native.GetForegroundWindow() == this.Handle) || (tapeDeck != null && Native.GetForegroundWindow() == tapeDeck.Handle))
                return true;
            else
                if((debugger != null) && (!debugger.IsDisposed))
                if(Native.GetForegroundWindow() == debugger.Handle)
                    return true;
            return false;
        }

        public void ForceScreenUpdate(bool doFullScreen = false)
        {
            if(doFullScreen)
                zx.UpdateScreenBuffer(zx.FrameLength);
            zx.needsPaint = true;
            System.Threading.Thread.Sleep(1);
        }

        private void SetEmulationState(EMULATOR_STATE newState)
        {
            if(newState == state)
                return;

            prevState = state;
            state = newState;

            switch(state)
            {

                case EMULATOR_STATE.IDLE:
                statusLabelText.Text = "Ready";
                zx.Resume();
                break;

                case EMULATOR_STATE.PAUSED:
                statusLabelText.Text = "Emulation Paused";
                zx.Pause();
                break;

                case EMULATOR_STATE.PLAYING_RZX:
                UpdateRZXInterface();
                break;

                case EMULATOR_STATE.RECORDING_RZX:
                UpdateRZXInterface();
                statusLabelText.Text = "Recording RZX ...";
                break;
            }

        }

        private void UpdateRZXInterface()
        {
            rzxRecordButton.Enabled = !(state == EMULATOR_STATE.PLAYING_RZX || state == EMULATOR_STATE.RECORDING_RZX);
            rzxStopToolStripMenuItem1.Enabled = (state == EMULATOR_STATE.PLAYING_RZX || state == EMULATOR_STATE.RECORDING_RZX);
            rzxFinaliseToolStripMenuItem.Enabled = (state == EMULATOR_STATE.RECORDING_RZX);
            rzxDiscardToolStripMenuItem.Enabled = (state == EMULATOR_STATE.RECORDING_RZX);
            rzxRecordToolStripMenuItem.Enabled = !(state == EMULATOR_STATE.PLAYING_RZX || state == EMULATOR_STATE.RECORDING_RZX);
            rzxContinueSessionToolStripMenuItem.Enabled = !(state == EMULATOR_STATE.PLAYING_RZX || state == EMULATOR_STATE.RECORDING_RZX);
            rzxPlaybackToolStripMenuItem.Enabled = !(state == EMULATOR_STATE.PLAYING_RZX || state == EMULATOR_STATE.RECORDING_RZX);
            rzxInsertBookmarkToolStripMenuItem.Enabled = (state == EMULATOR_STATE.RECORDING_RZX);
        }

        private int GetSpectrumModelIndex(string speccyModel)
        {
            int modelIndex = 0;
            switch(speccyModel)
            {
                case ZX_SPECTRUM_48K:
                modelIndex = 0;
                break;

                case ZX_SPECTRUM_128K:
                modelIndex = 1;
                break;

                case ZX_SPECTRUM_128KE:
                modelIndex = 2;
                break;

                case ZX_SPECTRUM_PLUS3:
                modelIndex = 3;
                break;

                case ZX_SPECTRUM_PENTAGON_128K:
                modelIndex = 4;
                break;
            }
            return modelIndex;
        }

        private void HandleKey2Joy(int key, bool pressed)
        {
            if(pressed)
            {
                switch(key)
                {
                    case ((int)keyCode.RIGHT):
                    if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                        zx.joystickState[config.Key2JoystickType] |= zxmachine.JOYSTICK_MOVE_RIGHT;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                        zx.keyBuffer[(int)keyCode._2] = true;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                        zx.keyBuffer[(int)keyCode._7] = true;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                        zx.keyBuffer[(int)keyCode._8] = true;
                    break;

                    case ((int)keyCode.LEFT):
                    if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                        zx.joystickState[config.Key2JoystickType] |= zxmachine.JOYSTICK_MOVE_LEFT;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                        zx.keyBuffer[(int)keyCode._1] = true;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                        zx.keyBuffer[(int)keyCode._6] = true;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                        zx.keyBuffer[(int)keyCode._5] = true;
                    break;

                    case ((int)keyCode.DOWN):
                    if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                        zx.joystickState[config.Key2JoystickType] |= zxmachine.JOYSTICK_MOVE_DOWN;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                        zx.keyBuffer[(int)keyCode._3] = true;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                        zx.keyBuffer[(int)keyCode._8] = true;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                        zx.keyBuffer[(int)keyCode._6] = true;
                    break;

                    case ((int)keyCode.UP):
                    if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                        zx.joystickState[config.Key2JoystickType] |= zxmachine.JOYSTICK_MOVE_UP;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                        zx.keyBuffer[(int)keyCode._4] = true;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                        zx.keyBuffer[(int)keyCode._9] = true;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                        zx.keyBuffer[(int)keyCode._7] = true;
                    break;

                    case (255): //proxy for Fire
                    if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                        zx.joystickState[config.Key2JoystickType] |= zxmachine.JOYSTICK_BUTTON_1;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                        zx.keyBuffer[(int)keyCode._5] = true;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                        zx.keyBuffer[(int)keyCode._0] = true;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                        zx.keyBuffer[(int)keyCode._0] = true;
                    break;
                }
            }
            else
            {
                switch(key)
                {
                    case ((int)keyCode.RIGHT):
                    if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                        zx.joystickState[config.Key2JoystickType] &= ~((int)(zxmachine.JOYSTICK_MOVE_RIGHT));
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                        zx.keyBuffer[(int)keyCode._2] = false;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                        zx.keyBuffer[(int)keyCode._7] = false;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                        zx.keyBuffer[(int)keyCode._8] = false;
                    break;

                    case ((int)keyCode.LEFT):
                    if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                        zx.joystickState[config.Key2JoystickType] &= ~((int)(zxmachine.JOYSTICK_MOVE_LEFT));
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                        zx.keyBuffer[(int)keyCode._1] = false;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                        zx.keyBuffer[(int)keyCode._6] = false;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                        zx.keyBuffer[(int)keyCode._5] = false;
                    break;

                    case ((int)keyCode.DOWN):
                    if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                        zx.joystickState[config.Key2JoystickType] &= ~((int)(zxmachine.JOYSTICK_MOVE_DOWN));
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                        zx.keyBuffer[(int)keyCode._3] = false;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                        zx.keyBuffer[(int)keyCode._8] = false;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                        zx.keyBuffer[(int)keyCode._6] = false;
                    break;

                    case ((int)keyCode.UP):
                    if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                        zx.joystickState[config.Key2JoystickType] &= ~((int)(zxmachine.JOYSTICK_MOVE_UP));
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                        zx.keyBuffer[(int)keyCode._4] = false;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                        zx.keyBuffer[(int)keyCode._9] = false;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                        zx.keyBuffer[(int)keyCode._7] = false;
                    break;

                    case (255): //proxy for alt
                    if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
                        zx.joystickState[config.Key2JoystickType] &= ~((int)(zxmachine.JOYSTICK_BUTTON_1));
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
                        zx.keyBuffer[(int)keyCode._5] = false;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
                        zx.keyBuffer[(int)keyCode._0] = false;
                    else if(config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
                        zx.keyBuffer[(int)keyCode._0] = false;
                    break;
                }
            }
        }

        private void HandleJoystick(JoystickController joystick, int joystickType)
        {
            byte[] buttons = joystick.state.GetButtons();
            if(joystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON)
            {
                byte bitf = 0;
                if(joystick.state.X > 100)
                {
                    bitf |= zxmachine.JOYSTICK_MOVE_RIGHT;
                }
                else if(joystick.state.X < -100)
                {
                    bitf |= zxmachine.JOYSTICK_MOVE_LEFT;
                }

                if(joystick.state.Y > 100)
                    bitf |= zxmachine.JOYSTICK_MOVE_DOWN;
                else if(joystick.state.Y < -100)
                    bitf |= zxmachine.JOYSTICK_MOVE_UP;

                if(buttons[joystick.fireButtonIndex] > 0) // IsPressed(joystick.fireButtonIndex))
                    bitf |= zxmachine.JOYSTICK_BUTTON_1;

                zx.joystickState[joystickType] = bitf;
            }
            else if(joystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR2)
            {
                if(joystick.state.X > 100)
                {
                    zx.keyBuffer[(int)keyCode._2] = true;
                }
                else if(joystick.state.X < -100)
                {
                    zx.keyBuffer[(int)keyCode._1] = true;
                }
                else
                {
                    zx.keyBuffer[(int)keyCode._1] = false;
                    zx.keyBuffer[(int)keyCode._2] = false;
                }

                if(joystick.state.Y < -100)
                {
                    zx.keyBuffer[(int)keyCode._4] = true;
                }
                else if(joystick.state.Y > 100)
                {
                    zx.keyBuffer[(int)keyCode._3] = true;
                }
                else
                {
                    zx.keyBuffer[(int)keyCode._3] = false;
                    zx.keyBuffer[(int)keyCode._4] = false;
                }

                if(buttons[joystick.fireButtonIndex] > 0)
                    zx.keyBuffer[(int)keyCode._5] = true;
                else
                    zx.keyBuffer[(int)keyCode._5] = false;
            }
            else if(joystickType == (int)zxmachine.JoysticksEmulated.SINCLAIR1)
            {
                if(joystick.state.X > 100)
                {
                    zx.keyBuffer[(int)keyCode._7] = true;
                }
                else if(joystick.state.X < -100)
                {
                    zx.keyBuffer[(int)keyCode._6] = true;
                }
                else
                {
                    zx.keyBuffer[(int)keyCode._6] = false;
                    zx.keyBuffer[(int)keyCode._7] = false;
                }

                if(joystick.state.Y < -100)
                {
                    zx.keyBuffer[(int)keyCode._9] = true;
                }
                else if(joystick.state.Y > 100)
                {
                    zx.keyBuffer[(int)keyCode._8] = true;
                }
                else
                {
                    zx.keyBuffer[(int)keyCode._8] = false;
                    zx.keyBuffer[(int)keyCode._9] = false;
                }

                if(buttons[joystick.fireButtonIndex] > 0)
                    zx.keyBuffer[(int)keyCode._0] = true;
                else
                    zx.keyBuffer[(int)keyCode._0] = false;
            }
            else if(joystickType == (int)zxmachine.JoysticksEmulated.CURSOR)
            {
                if(joystick.state.X > 100)
                {
                    zx.keyBuffer[(int)keyCode._8] = true; ;
                }
                else if(joystick.state.X < -100)
                {
                    zx.keyBuffer[(int)keyCode._5] = true;
                }
                else
                {
                    zx.keyBuffer[(int)keyCode._5] = false;
                    zx.keyBuffer[(int)keyCode._8] = false;
                }

                if(joystick.state.Y < -100)
                {
                    zx.keyBuffer[(int)keyCode._7] = true;
                }
                else if(joystick.state.Y > 100)
                {
                    zx.keyBuffer[(int)keyCode._6] = true;
                }
                else
                {
                    zx.keyBuffer[(int)keyCode._7] = false;
                    zx.keyBuffer[(int)keyCode._6] = false;
                }

                if(buttons[joystick.fireButtonIndex] > 0)
                    zx.keyBuffer[(int)keyCode._0] = true;
                else
                    zx.keyBuffer[(int)keyCode._0] = false;
            }

            //Extra buttonmap handling
            /*
            foreach (System.Collections.Generic.KeyValuePair<int, int> pair in joystick.buttonMap) {
                if (pair.Key == joystick.fireButtonIndex)
                    continue; //ignore the fire button as we've handled it above already

                if (pair.Value < 0)
                    continue;

                zx.keyBuffer[pair.Value] = (buttons[pair.Key] > 0);// joystick.state.IsPressed(pair.Key);
            }*/
        }

        private void AutoTapeLoad()
        {
            if(zx.model == MachineModel._plus3)
            {
                if((zx.PeekByteNoContend(SV_FLAGS) & 0x20) == 0)
                {
                    zx.PokeByteNoContend(SV_LAST_K, AutoLoadTapePlus3Keys[autoTapeLoadCounter]);
                    zx.PokeByteNoContend(SV_FLAGS, zx.PeekByteNoContend(SV_FLAGS) | 0x20);
                    autoTapeLoadCounter++;
                    if(autoTapeLoadCounter >= AutoLoadTapePlus3Keys.Length)
                    {
                        autoTapeLoadCounter = 0;
                        doAutoLoadTape = false;
                    }
                }
            }
            else if(zx.model == MachineModel._48k)
            {
                if((zx.PeekByteNoContend(SV_FLAGS) & 0x20) == 0)
                {
                    zx.PokeByteNoContend(SV_LAST_K, AutoLoadTape48Keys[autoTapeLoadCounter]);
                    zx.PokeByteNoContend(SV_FLAGS, zx.PeekByteNoContend(SV_FLAGS) | 0x20);
                    autoTapeLoadCounter++;
                    if(autoTapeLoadCounter >= AutoLoadTape48Keys.Length)
                    {
                        autoTapeLoadCounter = 0;
                        doAutoLoadTape = false;
                    }
                }
            }
            else
            {
                if((zx.PeekByteNoContend(SV_FLAGS) & 0x20) == 0)
                {
                    zx.PokeByteNoContend(SV_LAST_K, 13);
                    zx.PokeByteNoContend(SV_FLAGS, zx.PeekByteNoContend(SV_FLAGS) | 0x20);
                    doAutoLoadTape = false;
                }
            }
        }

        public void AddKeywordToEditorBuffer(byte token)
        {
            zx.PokeByteNoContend(SV_LAST_K, token);
            zx.PokeByteNoContend(SV_FLAGS, zx.PeekByteNoContend(SV_FLAGS) | 0x20);
        }

        public void OnApplicationIdle(object sender, EventArgs e)
        {
            while(AppStillIdle && !pauseEmulation)
            {
                TimeSpan elapsed = DateTime.Now - LastMouseMove;
                if(config.FullScreen)
                {
                    if(!CursorIsHidden && (elapsed.TotalSeconds >= TimeoutToHide.TotalSeconds))
                    {
                        Cursor.Hide();
                        CursorIsHidden = true;
                        dxWindow.Focus();
                    }
                }

                lastTime = PrecisionTimer.TimeInMilliseconds();

                if(zx.doRun)
                    zx.Run();

                //Start the auto load process only if we aren't in the middle of a reset
                if(zx.isResetOver)
                {
                    //state = EMULATOR_STATE.IDLE;

                    if(doAutoLoadTape)
                        AutoTapeLoad();
                }

                frameTime = PrecisionTimer.TimeInMilliseconds() - lastTime;// timer.DurationInMilliseconds;
                frameCount++;
                totalFrameTime += frameTime;

                if(zx.HasKempstonMouse)
                {
                    mouse.UpdateMouse();
                    zx.MouseX += (byte)(mouse.MouseX / config.MouseSensitivity);
                    zx.MouseY -= (byte)(mouse.MouseY / config.MouseSensitivity);
                    zx.MouseButton = 0xff;
                    if(mouse.MouseLeftButtonDown)
                        zx.MouseButton = (byte)(zx.MouseButton & (~0x2));
                    if(mouse.MouseRightButtonDown)
                        zx.MouseButton = (byte)(zx.MouseButton & (~0x1));
                }

                if(joystick1Index >= 0)
                {
                    joystick1.Update();
                    HandleJoystick(joystick1, joystick1MapIndex);
                }

                if(joystick2Index >= 0)
                {
                    joystick2.Update();
                    HandleJoystick(joystick2, joystick2MapIndex);
                }

                if(!config.EnableSound) //we'll try and synch to ~60Hz framerate (50Hz makes it run slightly slower than audio synch)
                {
                    if((frameTime) < 19 && !((zx.tapeIsPlaying && tapeFastLoad)))
                    {
                        double sleepTime = ((19 - frameTime));
                        System.Threading.Thread.Sleep((int)sleepTime);
                    }
                }

                if(showDownloadIndicator)
                {
                    downloadIndicatorTimeout--;

                    if(downloadIndicatorTimeout <= 0)
                    {
                        fileDownloadStatusLabel.Enabled = false;
                        showDownloadIndicator = false;
                    }
                    else
                        fileDownloadStatusLabel.Enabled = true;
                }

                //tapeStatusLabel.Enabled = showTapeIndicator;
                //diskStatusLable.Enabled = showDiskIndicator;
                rzxPlaybackStatusLabel.Enabled = zx.isPlayingRZX;
                rzxRecordStatusLabel.Enabled = zx.isRecordingRZX;

                if(config.EnableSound)
                    soundStatusLabel.Image = Properties.Resources.sound_high;
                else
                    soundStatusLabel.Image = Properties.Resources.sound_mute;

                if(!dxWindow.EnableFullScreen && totalFrameTime > 1000.0f)
                {
                    switch(state)
                    {

                        case EMULATOR_STATE.PLAYING_RZX:
                        //statusLabelText.Text = "RZX Played: " + (rzxFramesToPlay > 0 ? zx.GetNumRZXFramesPlayed() * 100 / rzxFramesToPlay : 0) + "%";
                        statusLabelText.Text = "Playing RZX";
                        statusProgressBar.Value = (rzxFramesToPlay > 0 ? zx.GetNumRZXFramesPlayed() * 100 / rzxFramesToPlay : 0);
                        break;
                        case EMULATOR_STATE.RECORDING_RZX:
                        statusLabelText.Text = "Recording RZX ...";
                        break;
                        default:
                        frameCount = 0;
                        totalFrameTime = 0;
                        statusLabelText.Text = "FPS: " + Math.Max(0, dxWindow.averageFPS);
                        break;
                    }
                }

                System.Threading.Thread.Sleep(1);
                dxWindow.Invalidate();
            }
        }

        public void EnableMouse(bool isEnabled)
        {
            if(zx.HasKempstonMouse && !isEnabled)
            {
                mouse.ReleaseMouse();
                zx.HasKempstonMouse = false;
                mouseStripStatusLabel.Enabled = false;
            }
            else if(isEnabled)
            {
                if(config.EnableKempstonMouse && !zx.HasKempstonMouse)
                {
                    mouse.AcquireMouse(this);
                    zx.HasKempstonMouse = true;
                    mouseStripStatusLabel.Enabled = true;
                }
            }
        }

        public string GetConfigData(StreamReader sr, string section, string data)
        {
            String readStr = "dummy";

            while(readStr != section)
            {
                if(sr.EndOfStream == true)
                {
                    System.Windows.Forms.MessageBox.Show("Invalid config file!", "Config file error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                    return "error";
                }
                readStr = sr.ReadLine();
            }

            while(true)
            {
                readStr = sr.ReadLine();
                if(readStr.IndexOf(data) >= 0)
                    break;
                if(sr.EndOfStream == true)
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
            logger.Log("Booting ROM: " + romName);

            byte[] romData;
            romLoaded = Utilities.ReadBytesFromFile(config.PathRoms + "\\" + romName, out romData);

            //Next try the application startup path (useful if running off USB)
            if(!romLoaded)
            {
                romLoaded = Utilities.ReadBytesFromFile(Application.StartupPath + "\\roms\\" + romName, out romData);

                //Aha! This worked so update the path in config file
                if(romLoaded)
                    config.PathRoms = Application.StartupPath + "\\roms";
            }

            while(!romLoaded)
            {
                MessageBox.Show("Zero couldn't load the ROM file for the " +
                                Utilities.GetStringFromEnum(zx.model) + ".\nSelect a valid ROM to continue.", "Missing ROM",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                openFileDialog1.InitialDirectory = config.PathRoms;
                openFileDialog1.Title = "Choose a ROM";
                openFileDialog1.FileName = "";
                openFileDialog1.Filter = "All supported files|*.rom;";

                if(openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    romName = openFileDialog1.SafeFileName;
                    config.PathRoms = Path.GetDirectoryName(openFileDialog1.FileName);
                    romLoaded = Utilities.ReadBytesFromFile(config.PathRoms + "\\" + romName, out romData);
                }
                else
                {
                    MessageBox.Show("Unfortunately, Zero cannot work without a valid ROM file.\nIt will now exit.",
                            "Unable to continue!", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                    break;
                }
            }

            if(romLoaded)
            {
                switch(zx.model)
                {
                    case MachineModel._48k:
                    if(romData.Length != 16384)
                        romLoaded = false;
                    else
                    {
                        config.Current48kROM = romName;
                        zx.PokeROMPages(0, 16384, romData);
                    }
                    break;

                    case MachineModel._128k:
                    if(romData.Length != 32768)
                        romLoaded = false;
                    else
                    {
                        config.Current128kROM = romName;
                        zx.PokeROMPages(0, 16384 * 2, romData);
                    }
                    break;

                    case MachineModel._128ke:
                    if(romData.Length != 32768)
                        romLoaded = false;
                    else
                    {
                        config.Current128keROM = romName;
                        zx.PokeROMPages(0, 16384 * 2, romData);
                    }
                    break;

                    case MachineModel._plus3:
                    if(romData.Length != 65536)
                        romLoaded = false;
                    else
                    {
                        config.CurrentPlus3ROM = romName;
                        zx.PokeROMPages(0, 16384 * 4, romData);
                    }
                    break;

                    case MachineModel._pentagon:
                    if(romData.Length != 32768)
                        romLoaded = false;
                    else
                    {
                        zx.PokeROMPages(0, 16384 * 2, romData);
                        string filename = config.PathRoms + "\\" + "trdos.rom";
                        romLoaded = Utilities.ReadBytesFromFile(filename, out romData);

                        if(!romLoaded || romData.Length != 16384)
                        {
                            if(MessageBox.Show("Zero couldn't load the TR DOS image for the " +
                                Utilities.GetStringFromEnum(zx.model) + ".\nThe TR DOS image should be in the same folder as the pentagon ROM and named as 'trdos.rom'.",
                                "TR DOS not found", MessageBoxButtons.OK) == DialogResult.OK)
                                return false;
                        }

                        //TR DOS resides in the top rom pages.
                        zx.PokeROMPages(2, 16384, romData);
                        config.CurrentPentagonROM = romName;
                    }
                    break;
                }
            }
            return romLoaded;
        }

        private bool OldLoadROM(String romName)
        {
            //First try to load from the path saved in the config file
            romLoaded = zx.LoadROM(config.PathRoms + "\\", romName);
            logger.Log("Booting ROM: " + romName);
            //Next try the application startup path (useful if running off USB)
            if(!romLoaded)
            {
                romLoaded = zx.LoadROM(Application.StartupPath + "\\roms\\", romName);

                //Aha! This worked so update the path in config file
                if(romLoaded)
                    config.PathRoms = Application.StartupPath + "\\roms";
            }
            while(!romLoaded)
            {
                MessageBox.Show("Zero couldn't find the '" + romName + "' file for the " +
                                Utilities.GetStringFromEnum(zx.model) + ".\nSelect a valid ROM to continue.", "Missing ROM",
                                MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                openFileDialog1.InitialDirectory = config.PathRoms;
                openFileDialog1.Title = "Choose a ROM";
                openFileDialog1.FileName = "";
                openFileDialog1.Filter = "All supported files|*.rom;";

                if(openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    romName = openFileDialog1.SafeFileName;
                    config.PathRoms = Path.GetDirectoryName(openFileDialog1.FileName);
                    romLoaded = zx.LoadROM(config.PathRoms + "\\", romName);

                    if(romLoaded)
                    {
                        switch(zx.model)
                        {
                            case MachineModel._48k:
                            config.Current48kROM = openFileDialog1.SafeFileName;
                            break;

                            case MachineModel._128k:
                            config.Current128kROM = openFileDialog1.SafeFileName;
                            break;

                            case MachineModel._128ke:
                            config.Current128keROM = openFileDialog1.SafeFileName;
                            break;

                            case MachineModel._plus3:
                            config.CurrentPlus3ROM = openFileDialog1.SafeFileName;
                            break;

                            case MachineModel._pentagon:
                            config.CurrentPentagonROM = openFileDialog1.SafeFileName;
                            break;
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Unfortunately, Zero cannot work without a valid ROM file.\nIt will now exit.",
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

            zx.keyBuffer[(int)keyCode.SHIFT] = shiftIsPressed;
            zx.keyBuffer[(int)keyCode.CTRL] = ctrlIsPressed;
            zx.keyBuffer[(int)keyCode.ALT] = altIsPressed;

            switch(keyEvent.KeyCode)
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
                if(altIsPressed)
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
                if(altIsPressed)
                {
                    saveSnapshotMenuItem_Click(this, null);
                }
                else
                    zx.keyBuffer[(int)keyCode.S] = true;
                break;

                case Keys.T:
                if(altIsPressed)
                {
                    if(tapeDeck.TapeIsInserted)
                    {
                        saveFileDialog1.Title = "Save Tape";
                        saveFileDialog1.FileName = "";
                        saveFileDialog1.Filter = "PZX Tape|*.pzx";

                        if(saveFileDialog1.ShowDialog() == DialogResult.OK)
                        {
                            tapeDeck.SavePZX(saveFileDialog1.FileName);
                        }
                    }
                }
                else
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
                if(altIsPressed)
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
                if(altIsPressed)
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

                if(config.EnableKey2Joy)
                    HandleKey2Joy((int)keyCode.UP, true);

                zx.keyBuffer[(int)keyCode.UP] = true;
                break;

                case Keys.Left:

                if(config.EnableKey2Joy)
                    HandleKey2Joy((int)keyCode.LEFT, true);

                zx.keyBuffer[(int)keyCode.LEFT] = true;
                break;

                case Keys.Right:

                if(config.EnableKey2Joy)
                    HandleKey2Joy((int)keyCode.RIGHT, true);

                zx.keyBuffer[(int)keyCode.RIGHT] = true;
                break;

                case Keys.Down:

                if(config.EnableKey2Joy)
                    HandleKey2Joy((int)keyCode.DOWN, true);

                zx.keyBuffer[(int)keyCode.DOWN] = true;
                break;

                case Keys.Back:
                zx.keyBuffer[(int)keyCode.BACK] = true;
                break;

                case Keys.Tab:
                zx.keyBuffer[(int)keyCode.TAB] = true;

                if(config.EnableKey2Joy)
                    HandleKey2Joy(255, true);
                break;

                #region Convenience Key Press Emulation

                case Keys.OemPeriod:
                if(ctrlIsPressed)
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
                if(ctrlIsPressed)
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
                if(ctrlIsPressed) //colon
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
                if(ctrlIsPressed) //double quotes
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
                if(ctrlIsPressed)
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
                if(ctrlIsPressed)
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
                if(altIsPressed)
                    toolStripMenuItem1_Click(this, null);
                else
                {
                    if(ctrlIsPressed)
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
                if(altIsPressed)
                    toolStripMenuItem5_Click_1(this, null);
                else
                {
                    if(ctrlIsPressed)
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
                if(ctrlIsPressed)
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

                #endregion Convenience Key Press Emulation

                /* case Keys.F5:
                     if (altIsPressed && shiftIsPressed)
                         screenshotMenuItem1_Click(this, null);

                     break;
                 */
                case Keys.F6:
                if(altIsPressed)
                    EnableMouse(false);
                else
                    EnableMouse(true);

                break;

                case Keys.Insert:
                if(zx.isRecordingRZX)
                    insertBookmarkToolStripMenuItem_Click(this, null);

                break;

                case Keys.Delete:
                if(zx.isRecordingRZX)
                    rollbackToolStripMenuItem_Click(this, null);

                break;
                case Keys.F7:
                /*
                if (zx.isRecordingRZX)
                {
                    if (altIsPressed)
                    {
                        rollbackToolStripMenuItem_Click(this, null);
                    }
                    else
                    {
                        insertBookmarkToolStripMenuItem_Click(this, null);
                    }
                }
                break;
                */
                case Keys.Escape:
                pauseEmulationESCToolStripMenuItem_Click(this, null);
                break;

                case Keys.ControlKey:
                {
                    zx.keyBuffer[(int)keyCode.CTRL] = true;
                }
                break;

                default:
                if(keyEvent.KeyValue == 191) //frontslash
                {
                    if(ctrlIsPressed) //question mark
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

            switch(keyEvent.KeyCode)
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

                if(config.EnableKey2Joy)
                    HandleKey2Joy((int)keyCode.UP, false);

                zx.keyBuffer[(int)keyCode.UP] = false;
                break;

                case Keys.Left:

                if(config.EnableKey2Joy)
                    HandleKey2Joy((int)keyCode.LEFT, false);

                zx.keyBuffer[(int)keyCode.LEFT] = false;
                break;

                case Keys.Right:

                if(config.EnableKey2Joy)
                    HandleKey2Joy((int)keyCode.RIGHT, false);

                zx.keyBuffer[(int)keyCode.RIGHT] = false;
                break;

                case Keys.Down:

                if(config.EnableKey2Joy)
                    HandleKey2Joy((int)keyCode.DOWN, false);

                zx.keyBuffer[(int)keyCode.DOWN] = false;
                break;

                case Keys.Back:
                zx.keyBuffer[(int)keyCode.BACK] = false;
                break;

                case Keys.Tab:
                zx.keyBuffer[(int)keyCode.TAB] = false;

                if(config.EnableKey2Joy)
                    HandleKey2Joy(255, false);
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
                if(keyEvent.KeyValue == 191) //frontslash
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

        private void ChangeZXPalette(string newPalette)
        {
            config.PaletteMode = newPalette;
            switch(config.PaletteMode)
            {
                case "Grayscale":
                zx.ULAPlusEnabled = false;
                zx.SetPalette(GrayPalette);
                grayscaleToolStripMenuItem.Checked = true;
                uLAPlusToolStripMenuItem.Checked = false;
                normalToolStripMenuItem.Checked = false;
                break;

                case "ULA Plus":
                zx.ULAPlusEnabled = true;
                zx.SetPalette(zx.NormalColors);
                grayscaleToolStripMenuItem.Checked = false;
                uLAPlusToolStripMenuItem.Checked = true;
                normalToolStripMenuItem.Checked = false;
                break;

                default:
                zx.ULAPlusEnabled = false;
                zx.SetPalette(zx.NormalColors);
                grayscaleToolStripMenuItem.Checked = false;
                uLAPlusToolStripMenuItem.Checked = false;
                normalToolStripMenuItem.Checked = true;
                break;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            //Show the confirmation box only if it's not an invalid ROM exit event and not fullscreen
            if(config.ConfirmOnExit && romLoaded && !config.FullScreen)
            {
                if(System.Windows.Forms.MessageBox.Show("Are you sure you want to exit?",
                           "Confirm Exit", System.Windows.Forms.MessageBoxButtons.YesNo,
                           System.Windows.Forms.MessageBoxIcon.Question) == DialogResult.No)

                    e.Cancel = true;
            }
            base.OnFormClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            logger.Log("Shutting down...", true);

            File.Delete(Application.LocalUserAppDataPath + "//" + "_tempbas.tap");

            if(config != null && config.RestoreLastStateOnStart)
                zx.SaveSZX(Application.LocalUserAppDataPath + "//" + ZeroSessionSnapshotName);

            for(int f = 0; f < 4; f++)
                if(diskArchivePath[f] != null)
                {
                    zx.DiskEject((byte)f);
                    File.Delete(diskArchivePath[f]);
                }

            if((config != null) && (tapeDeck != null))
            {
                config.TapeAutoStart = tapeDeck.DoTapeAutoStart;
                config.TapeAutoLoad = tapeDeck.DoAutoTapeLoad;
                config.TapeEdgeLoad = tapeDeck.DoTapeEdgeLoad;
                config.TapeAccelerateLoad = tapeDeck.DoTapeAccelerateLoad;
                config.TapeInstaLoad = tapeDeck.DoTapeInstaLoad;

                if(joystick1Index >= 0 && joystick1.isInitialized)
                {
                    config.Joystick1Name = joystick1.name;
                    config.Joystick1ToEmulate = joystick1MapIndex;
                }
                else
                {
                    config.Joystick1Name = "";
                    config.Joystick1ToEmulate = 0;
                }

                if(joystick2Index >= 0 && joystick2.isInitialized)
                {
                    config.Joystick2Name = joystick2.name;
                    config.Joystick2ToEmulate = joystick2MapIndex;
                }
                else
                {
                    config.Joystick2Name = "";
                    config.Joystick2ToEmulate = 0;
                }

                config.Save();
            }

            joystick1.Release();
            joystick2.Release();

            mouse.ReleaseMouse();

            if(dxWindow != null)
                dxWindow.Shutdown();

            if(zx != null)
                zx.Shutdown();

            //Close any open archived disk files

            base.OnClosed(e);
        }

        private void Form1_Load(object sender, System.EventArgs e)
        {
            logger.Log("Starting up...");
            this.BringToFront();

            //Setup MRU (Recent files)
            this.mruManager = new MRUManager(
                                //the menu item that will contain the recent files
                                this.recentFilesToolStripMenuItem,
                                //the name of your program
                                "Zero",
                                //the funtion that will be called when a recent file gets clicked.
                                this.MRUOpenFile_handler,
                                //an optional function to call when the user clears the list of recent items
                                null);

            //Load configuration
            config.ApplicationPath = Application.StartupPath;
            config.Load();

            romLoaded = false;
            recentFolder = config.PathGames;

            logger.Log("Checking for command line arguments...");
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            if(commandLineArgs.Length > 1)
            {
                commandLineLaunch = true;
                for(int f = 2; f < commandLineArgs.Length; f++)
                {
                    string command = commandLineArgs[f];
                    if(command.Contains("-fullscreen:"))
                        config.FullScreen = (command.Substring(12) == "on");

                    if(command.Contains("-model:"))
                    {
                        string s = command.Substring(7);
                        if(s == "48k")
                            config.CurrentSpectrumModel = ZX_SPECTRUM_48K;
                        else
                            if(s == "128k")
                            config.CurrentSpectrumModel = ZX_SPECTRUM_128K;
                        else
                                if(s == "128ke")
                            config.CurrentSpectrumModel = ZX_SPECTRUM_128KE;
                        else
                                    if(s == "plus3")
                            config.CurrentSpectrumModel = ZX_SPECTRUM_PLUS3;
                        else
                                        if(s == "pentagon128k")
                            config.CurrentSpectrumModel = ZX_SPECTRUM_128K;
                    }
                    if(command.Contains("-speed:"))
                    {
                        string s = command.Substring(7);
                        config.EmulationSpeed = Convert.ToInt32(s);
                        if(config.EmulationSpeed > 500)
                            config.EmulationSpeed = 499;
                    }
                    if(command.Contains("-renderer:"))
                        config.UseDirectX = !(command.Substring(10) == "gdi");

                    if(command.Contains("-smoothing:"))
                        config.EnablePixelSmoothing = (command.Substring(11) == "on");

                    if(command.Contains("-vsync:"))
                        config.EnableVSync = (command.Substring(7) == "on");

                    if(command.Contains("-palette:"))
                    {
                        string s = command.Substring(9);
                        if(s == "ula+" || s == "ulaplus")
                            config.PaletteMode = "ULA Plus";
                        else if(s == "grayscale")
                            config.PaletteMode = "Grayscale";
                        else
                            config.PaletteMode = "Normal";
                    }
                    if(command.Contains("-scanlines:"))
                        config.EnableInterlacedOverlay = (command.Substring(11) == "on");

                    if(command.Contains("-timing:"))
                        config.UseLateTimings = (command.Substring(8) == "late");

                    if(command.Contains("-visuals:"))
                        config.ShowOnscreenIndicators = (command.Substring(9) == "on");

                    if(command.Contains("-windowsize:"))
                    {
                        string s = command.Substring(12);
                        int size = Convert.ToInt32(s);
                        if(size >= 100 && (size % 50 == 0))
                            config.WindowSize = size - 100;
                    }
                    if(command.Contains("-bordersize:"))
                    {
                        string s = command.Substring(12);
                        if(s == "mini")
                            config.BorderSize = 48;
                        else if(s == "medium")
                            config.BorderSize = 24;
                        else if(s == "full")
                            config.BorderSize = 0;
                    }
                }
            }
            logger.Log("Powering on the speccy...");
            //Load the ROM
            switch(config.CurrentSpectrumModel)
            {
                case ZX_SPECTRUM_48K:
                config.Model = MachineModel._48k;
                zx = new zx48(this.Handle, config.UseLateTimings);
                zx.Issue2Keyboard = config.UseIssue2Keyboard;
                // zx.Reset();
                zxSpectrum48kToolStripMenuItem.Checked = true;
                disksMenuItem.Enabled = false;
                romLoaded = LoadROM(config.Current48kROM);
                break;

                case ZX_SPECTRUM_128KE:
                config.Model = MachineModel._128ke;
                zx = new zx128e(this.Handle, config.UseLateTimings);
                zx.Issue2Keyboard = config.UseIssue2Keyboard;
                //zx.Reset();
                zxSpectrum128keToolStripMenuItem1.Checked = true;
                disksMenuItem.Enabled = false;
                romLoaded = LoadROM(config.current128keRom);
                break;

                case ZX_SPECTRUM_128K:
                config.Model = MachineModel._128k;
                zx = new zx128(this.Handle, config.UseLateTimings);
                //zx.Reset();
                zxSpectrum128kToolStripMenuItem1.Checked = true;
                disksMenuItem.Enabled = false;
                romLoaded = LoadROM(config.current128kRom);
                break;

                case ZX_SPECTRUM_PENTAGON_128K:
                config.Model = MachineModel._pentagon;
                zx = new Pentagon128K(this.Handle, config.UseLateTimings);
                //zx.Reset();
                pentagon128kToolStripMenuItem1.Checked = true;
                romLoaded = LoadROM(config.currentPentagonRom);
                disksMenuItem.Enabled = true;
                insertDiskCToolStripMenuItem.Enabled = true;
                insertDiskDToolStripMenuItem.Enabled = true;
                break;

                case ZX_SPECTRUM_PLUS3:
                config.Model = MachineModel._plus3;
                zx = new zxPlus3(this.Handle, config.UseLateTimings);
                // zx.Reset();
                zxSpectrum3ToolStripMenuItem1.Checked = true;
                romLoaded = LoadROM(config.currentPlus3Rom);
                disksMenuItem.Enabled = true;
                insertDiskCToolStripMenuItem.Enabled = false;
                insertDiskDToolStripMenuItem.Enabled = false;
                break;
            }

            logger.Log("Initializing tape deck...");
            tapeDeck = new TapeDeck(this);
            if(!romLoaded)
            {
                this.Close();
                return;
            }

            zx.SetSoundVolume(config.Volume / 100.0f);
            zx.SetEmulationSpeed(config.EmulationSpeed);
            zx.EnableAY(config.EnableAYFor48K);
            zx.SetStereoSound(config.StereoSoundOption);

            tapeDeck.DoTapeAutoStart = config.TapeAutoStart;
            tapeDeck.DoAutoTapeLoad = config.TapeAutoLoad;
            tapeDeck.DoTapeEdgeLoad = config.TapeEdgeLoad;
            tapeDeck.DoTapeAccelerateLoad = config.TapeAccelerateLoad;
            tapeDeck.DoTapeInstaLoad = config.TapeInstaLoad;

            zx.DiskEvent += new DiskEventHandler(DiskMotorEvent);
            try
            {
                logger.Log("Initializing renderer...");
                dxWindow = new ZRenderer(this, panel1.Width, panel1.Height);
            }
            catch(System.TypeInitializationException dxex)
            {
                MessageBox.Show(dxex.InnerException.Message, "Wrong DirectX version.", MessageBoxButtons.OK);
                return;
            }

            // dxWindow.ContextMenuStrip = contextMenuStrip1;
            if(config.UseDirectX)
                directXToolStripMenuItem_Click(this, null);
            else
                gDIToolStripMenuItem_Click(this, null);

            //dxWindow.Location = new Point(panel4.Width, panel2.Height);
            //dxWindow.SetSize(panel1.Width, panel1.Height);
            dxWindow.MouseMove += new MouseEventHandler(Form1_MouseMove);
            this.Controls.Add(dxWindow);
            panel1.Enabled = false;
            panel1.Hide();
            panel1.SendToBack();
            dxWindow.BringToFront();
            dxWindow.Focus();
            dxWindow.ShowScanlines = interlaceToolStripMenuItem.Checked = config.EnableInterlacedOverlay;
            dxWindow.PixelSmoothing = config.EnablePixelSmoothing;
            pixelToolStripMenuItem.Checked = config.EnablePixelSmoothing;
            dxWindow.EnableVsync = config.EnableVSync;

            logger.Log("Initializing render window...");
            AdjustWindowSize();

            if(config.FullScreen)
                GoFullscreen(true);

            switch(config.PaletteMode)
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

            JoystickController.EnumerateJosticks();
            string[] joysticks = JoystickController.GetDeviceNames();

            if(!string.IsNullOrEmpty(config.Joystick1Name))
            {
                int i = Array.IndexOf(joysticks, config.Joystick1Name);

                if(i >= 0)
                {
                    joystick1Index = i;
                    joystick1MapIndex = config.Joystick1ToEmulate;
                }
            }

            if(!string.IsNullOrEmpty(config.Joystick2Name))
            {
                int i = Array.IndexOf(joysticks, config.Joystick2Name);

                if(i >= 0)
                {
                    joystick2Index = i;
                    joystick2MapIndex = config.Joystick2ToEmulate;
                }
            }

            SetupJoysticks();

            //Any command line parameter will override the restore last state on start functionality
            if(commandLineLaunch)
            {
                LoadZXFile(commandLineArgs[1]);
            }
            else if(config.RestoreLastStateOnStart)
            {
                logger.Log("Restoring last state...");
                if(!LoadSZX(Application.LocalUserAppDataPath + "//" + ZeroSessionSnapshotName))
                {
                    MessageBox.Show("Unable to restore previous session!", "Session Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            zx.Start();

        }

        private void Form_MouseDown(object sender, MouseEventArgs e)
        {
            mouseOrigin.X = e.X;
            mouseOrigin.Y = e.Y;
        }

        private void Form_MouseMove(object sender, MouseEventArgs e)
        {
            if(e.Button == MouseButtons.Left)
            {
                this.Top += e.Y - mouseOrigin.Y;
                this.Left += e.X - mouseOrigin.X;
            }
        }

        //Load file
        private void fileButton_Click(object sender, EventArgs e)
        {
            openFileMenuItem1_Click(sender, e);
        }

        private void directXToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //If directX isn't available, try initialising it one more time
            if(!dxWindow.DirectXReady)
                dxWindow.InitDirectX(dxWindow.Width, dxWindow.Height);

            //If we have directX available to us, switch to it.
            if(dxWindow.DirectXReady)
            {
                dxWindow.EnableDirectX = true;
                gDIToolStripMenuItem.Checked = false;
                directXToolStripMenuItem.Checked = true;
                dxWindow.Focus();
                config.UseDirectX = true;
                interlaceToolStripMenuItem.Enabled = true;
                if(interlaceToolStripMenuItem.Checked)
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
            zx.Reset(false);
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
            //if (pauseEmulation)
            //    return;
            //Form_MouseDown(sender, e);
            //dxWindow.Focus();
        }

        private void panel1_MouseMove(object sender, MouseEventArgs e)
        {
            // if (pauseEmulation)
            //     return;
            // Form1_MouseMove(sender, e);
            //dxWindow.Focus();
        }

        private void panel1_MouseUp(object sender, MouseEventArgs e)
        {
        }

        //100% window size
        private void size100ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(config.FullScreen)
                GoFullscreen(false);

            config.WindowSize = 0;
            AdjustWindowSize();
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            return false;
        }

        protected override bool ProcessCmdKey(ref
              System.Windows.Forms.Message m,
              System.Windows.Forms.Keys k)
        {
            // detect the pushing (Msg) of Enter Key (k)

            // then process the signal as usual
            return base.ProcessCmdKey(ref m, k);
        }

        //Monitor
        private void monitorButton_Click(object sender, EventArgs e)
        {
            ShouldExitFullscreen();

            if(debugger == null || debugger.IsDisposed)
            {
                //zx.doRun = false;
                debugger = new Monitor(this);

                debugger.SetState(Monitor.MonitorState.PAUSE);
                debugger.Show();
                //debugger.dbState = Monitor.MonitorState.STEPIN;
            }

            if(!debugger.Visible)
            {
                //zx.doRun = false;
                //debugger.dbState = Monitor.MonitorState.STEPIN;
                debugger.ReSyncWithZX();
                debugger.SetState(Monitor.MonitorState.PAUSE);
                debugger.Show();
            }
            debugger.BringToFront();
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
            ChangeSpectrumModel(MachineModel._48k);
            config.CurrentSpectrumModel = ZX_SPECTRUM_48K;
            zxSpectrum48kToolStripMenuItem.Checked = true;
            zxSpectrum128keToolStripMenuItem1.Checked = false;
            zxSpectrum128kToolStripMenuItem1.Checked = false;
            zxSpectrum3ToolStripMenuItem1.Checked = false;
            pentagon128kToolStripMenuItem1.Checked = false;
        }

        //Spectrum 128Ke
        public void zXSpectrumToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeSpectrumModel(MachineModel._128ke);
            config.CurrentSpectrumModel = ZX_SPECTRUM_128KE;
            zxSpectrum128keToolStripMenuItem1.Checked = true;
            zxSpectrum48kToolStripMenuItem.Checked = false;
            zxSpectrum128kToolStripMenuItem1.Checked = false;
            zxSpectrum3ToolStripMenuItem1.Checked = false;
            pentagon128kToolStripMenuItem1.Checked = false;
        }

        public void zXSpectrum128KToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeSpectrumModel(MachineModel._128k);
            config.CurrentSpectrumModel = ZX_SPECTRUM_128K;
            zxSpectrum48kToolStripMenuItem.Checked = false;
            zxSpectrum128kToolStripMenuItem1.Checked = true;
            zxSpectrum128keToolStripMenuItem1.Checked = false;
            zxSpectrum3ToolStripMenuItem1.Checked = false;
            pentagon128kToolStripMenuItem1.Checked = false;
        }

        private void zxSpectrum3ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeSpectrumModel(MachineModel._plus3);
            config.CurrentSpectrumModel = ZX_SPECTRUM_PLUS3;
            zxSpectrum48kToolStripMenuItem.Checked = false;
            zxSpectrum128kToolStripMenuItem1.Checked = false;
            zxSpectrum128keToolStripMenuItem1.Checked = false;
            zxSpectrum3ToolStripMenuItem1.Checked = true;
            pentagon128kToolStripMenuItem1.Checked = false;
        }

        private void pentagon128KToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChangeSpectrumModel(MachineModel._pentagon);
            config.CurrentSpectrumModel = ZX_SPECTRUM_PENTAGON_128K;
            zxSpectrum48kToolStripMenuItem.Checked = false;
            zxSpectrum128kToolStripMenuItem1.Checked = false;
            zxSpectrum128keToolStripMenuItem1.Checked = false;
            zxSpectrum3ToolStripMenuItem1.Checked = false;
            pentagon128kToolStripMenuItem1.Checked = true;
        }

        private void ChangeSpectrumModel(MachineModel _model)
        {
            if(softResetOnly)
                return;

            zx.Pause();

            SetEmulationState(EMULATOR_STATE.IDLE);
            UpdateRZXInterface();
            isPlayingRZX = false;

            dxWindow.Suspend();
            softResetOnly = false;

            if(debugger != null)
            {
                debugger.DeRegisterAllEvents();
                debugger.DeSyncWithZX();
            }

            if(tapeDeck != null)
                tapeDeck.UnRegisterEventHooks();

            showDiskIndicator = false;
            zx.DiskEvent -= new DiskEventHandler(DiskMotorEvent);
            zx.Shutdown();
            zx = null;

            System.GC.Collect();

            config.Model = _model;
            Directory.SetCurrentDirectory(Application.StartupPath);

            switch(config.Model)
            {
                case MachineModel._48k:
                config.Model = MachineModel._48k;
                zx = new zx48(this.Handle, config.UseLateTimings);
                romLoaded = LoadROM(config.Current48kROM);
                disksMenuItem.Enabled = false;
                break;

                case MachineModel._128k:
                config.Model = MachineModel._128k;
                zx = new zx128(this.Handle, config.UseLateTimings);
                romLoaded = LoadROM(config.Current128kROM);
                disksMenuItem.Enabled = false;
                break;

                case MachineModel._128ke:
                config.Model = MachineModel._128ke;
                zx = new zx128e(this.Handle, config.UseLateTimings);
                romLoaded = LoadROM(config.current128keRom);
                disksMenuItem.Enabled = false;
                break;

                case MachineModel._plus3:
                config.Model = MachineModel._plus3;
                zx = new zxPlus3(this.Handle, config.UseLateTimings);
                romLoaded = LoadROM(config.currentPlus3Rom);
                disksMenuItem.Enabled = true;
                insertDiskCToolStripMenuItem.Enabled = false;
                insertDiskDToolStripMenuItem.Enabled = false;
                break;

                case MachineModel._pentagon:
                config.Model = MachineModel._pentagon;
                zx = new Pentagon128K(this.Handle, config.UseLateTimings);
                romLoaded = LoadROM(config.currentPentagonRom);
                disksMenuItem.Enabled = true;
                insertDiskCToolStripMenuItem.Enabled = true;
                insertDiskDToolStripMenuItem.Enabled = true;
                break;
            }

            if(!romLoaded)
            {
                this.Close();
                return;
            }

            zx.SetSoundVolume(config.Volume / 100.0f);
            zx.SetEmulationSpeed(config.EmulationSpeed);
            zx.EnableAY(config.EnableAYFor48K);
            zx.SetStereoSound(config.StereoSoundOption);

            if((joystick2MapIndex == (int)zxmachine.JoysticksEmulated.KEMPSTON) || (joystick1MapIndex == (int)zxmachine.JoysticksEmulated.KEMPSTON))
                zx.HasKempstonJoystick = true;
            else
                zx.HasKempstonJoystick = false;

            zx.UseKempstonPort1F = config.KempstonUsesPort1F;

            if((config.EnableKey2Joy) && (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON))
            {
                zx.HasKempstonJoystick = true;
            }

            ChangeZXPalette(config.PaletteMode);

            if(!config.EnableSound)
                zx.MuteSound(true);

            if(debugger != null)
            {
                debugger.ReRegisterAllEvents();
                debugger.ReSyncWithZX();
            }

            if(tapeDeck != null)
                tapeDeck.RegisterEventHooks();

            zx.DiskEvent += new DiskEventHandler(DiskMotorEvent);

            dxWindow.Resume();
            dxWindow.Focus();
            zx.Resume();
            showTapeIndicator = false;
            //Some models like the Pentagon don't have the same screen width as the normal speccy
            //so we have to adjust the window size when switching to them and vice-versa
            if((zx.GetTotalScreenWidth() != dxWindow.ScreenWidth) || (zx.GetTotalScreenHeight() != dxWindow.ScreenHeight))
            {
                //if current model is pentagon then we have switched from a smaller screen size
                /*  if (zx.model == MachineModel._pentagon)
                      AdjustRenderWindow(PENTAGON_WINDOW_OFFSET_X, PENTAGON_WINDOW_OFFSET_Y);
                  else //if we're switching to a smaller screen size from pentagon, negate the offsets
                      AdjustRenderWindow(-PENTAGON_WINDOW_OFFSET_X, -PENTAGON_WINDOW_OFFSET_Y); */
                AdjustWindowSize();
            }
            //hardResetToolStripMenuItem1_Click(null, null);
        }

        //options button
        private void optionsButton_Click(object sender, EventArgs e)
        {
            if((optionWindow == null) || (optionWindow.IsDisposed))
                optionWindow = new Options(this);
            bool oldPause = pauseEmulation;
            pauseEmulation = true;
            zx.Pause();
            PreOptionsWindowShow();
            if(optionWindow.ShowDialog(this) == DialogResult.OK)
            {
                PostOptionsWindowShow();
            }
            pauseEmulation = oldPause;
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
            optionWindow.Key2JoyStickType = config.Key2JoystickType - 1;
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
            optionWindow.EnableKempstonMouse = config.EnableKempstonMouse;
            optionWindow.MouseSensitivity = config.MouseSensitivity;
            optionWindow.PixelSmoothing = dxWindow.PixelSmoothing;
            optionWindow.EnableVSync = config.EnableVSync;
            optionWindow.MaintainAspectRatioInFullScreen = config.MaintainAspectRatioInFullScreen;
            optionWindow.DisableTapeTraps = config.DisableTapeTraps;
            optionWindow.KempstonUsesPort1F = config.KempstonUsesPort1F;

            switch(config.PaletteMode)
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

            optionWindow.borderSize = config.BorderSize / 24;
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
            optionWindow.ShowOnScreenLEDS = config.ShowOnscreenIndicators;
            optionWindow.RestoreLastState = config.RestoreLastStateOnStart;

            if(config.FullScreen)
                optionWindow.windowSize = 0;
            else
                optionWindow.windowSize = config.WindowSize / 50 + 1;
        }

        private void PostOptionsWindowShow()
        {
            config.UseIssue2Keyboard = optionWindow.UseIssue2Keyboard;
            config.UseLateTimings = (optionWindow.UseLateTimings);// == true ? 1 : 0);
            config.UseDirectX = optionWindow.UseDirectX;
            config.PathRoms = optionWindow.RomPath;
            config.PathGames = optionWindow.GamePath;
            config.EnableInterlacedOverlay = optionWindow.InterlacedMode;
            config.StereoSoundOption = optionWindow.SpeakerSetup;
            config.EnableKempstonMouse = optionWindow.EnableKempstonMouse;
            config.MouseSensitivity = optionWindow.MouseSensitivity;
            config.EnableAYFor48K = optionWindow.EnableAYFor48K;
            config.HighCompatibilityMode = optionWindow.HighCompatibilityMode;
            config.EnableKey2Joy = optionWindow.EnableKey2Joy;
            config.Key2JoystickType = optionWindow.Key2JoyStickType + 1;
            config.DisableTapeTraps = optionWindow.DisableTapeTraps;
            config.RestoreLastStateOnStart = optionWindow.RestoreLastState;
            config.ShowOnscreenIndicators = optionWindow.ShowOnScreenLEDS;
            config.MaintainAspectRatioInFullScreen = optionWindow.MaintainAspectRatioInFullScreen;
            dxWindow.ShowScanlines = interlaceToolStripMenuItem.Checked = config.EnableInterlacedOverlay;
            pixelToolStripMenuItem.Checked = config.EnablePixelSmoothing = optionWindow.PixelSmoothing;
            config.KempstonUsesPort1F = optionWindow.KempstonUsesPort1F;

            if(config.EnableVSync != optionWindow.EnableVSync)
            {
                config.EnableVSync = optionWindow.EnableVSync;
                dxWindow.EnableVsync = config.EnableVSync;
                dxWindow.InitDirectX(dxWindow.Width, dxWindow.Height);
            }

            //Remove any previous session info if user doesn't want restore function
            if(!config.RestoreLastStateOnStart)
            {
                if(File.Exists(ZeroSessionSnapshotName))
                    File.Delete(ZeroSessionSnapshotName);
            }

            if(config.UseDirectX)
                directXToolStripMenuItem_Click(this, null);
            else
                gDIToolStripMenuItem_Click(this, null);

            if(config.EmulationSpeed != optionWindow.EmulationSpeed)
            {
                config.EmulationSpeed = optionWindow.EmulationSpeed;
                zx.SetEmulationSpeed(config.EmulationSpeed);
            }
            switch(optionWindow.Palette)
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

            if((optionWindow.SpectrumModel != GetSpectrumModelIndex(config.CurrentSpectrumModel))
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

                switch(optionWindow.SpectrumModel)
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

            zx.tapeTrapsDisabled = config.DisableTapeTraps;
            zx.Issue2Keyboard = config.UseIssue2Keyboard;
            zx.LateTiming = (config.UseLateTimings ? 1 : 0);
            config.ConfirmOnExit = optionWindow.ConfirmOnExit;
            config.PauseOnFocusLost = optionWindow.PauseOnFocusChange;

            zx.SetStereoSound(config.StereoSoundOption); //Also sets ACB/ABC config internally
            zx.EnableAY(config.EnableAYFor48K);

            joystick1Index = optionWindow.Joystick1Choice - 1;
            joystick2Index = optionWindow.Joystick2Choice - 1;

            joystick1MapIndex = optionWindow.Joystick1EmulationChoice;
            joystick2MapIndex = optionWindow.Joystick2EmulationChoice;


            SetupJoysticks();
            //zx.HasKempstonMouse = config.EnableKempstonMouse;
            //dxWindow.Suspend();
            CheckFileAssociations();
            //dxWindow.Resume();
            optionWindow.Dispose();
            bool requiresResizeWindow = false;

            //Are we going windowed from fullscreen?
            if(optionWindow.windowSize != 0 && config.FullScreen)
            {
                config.WindowSize = (optionWindow.windowSize - 1) * 50;
                GoFullscreen(config.FullScreen);
            }
            else if(optionWindow.windowSize == 0 && !config.FullScreen) //or the other way
            {
                GoFullscreen(config.FullScreen);
            }
            else if(optionWindow.windowSize > 0 && (config.WindowSize != (optionWindow.windowSize - 1) * 50)) //or diff window size from previous
            {
                config.WindowSize = (optionWindow.windowSize - 1) * 50;
                requiresResizeWindow = true;
            }

            //Change in border size?
            if(config.BorderSize != (optionWindow.borderSize * 24))
            {
                config.BorderSize = optionWindow.borderSize * 24;
                requiresResizeWindow = true;
            }

            if(requiresResizeWindow)
                AdjustWindowSize();

            dxWindow.PixelSmoothing = config.EnablePixelSmoothing;
        }

        //Need this to command the windows explorer shell to refresh icon cache
        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

        public void CheckFileAssociations()
        {
            string param = "";
            string assoc = "";
            bool iconsChanged = false;

            if(optionWindow.FileAssociateDSK != config.AccociateDSKFiles)
            {
                assoc = (optionWindow.FileAssociateDSK ? " 1.dsk" : " 0.dsk");
                iconsChanged = true;
                param += assoc;
            }

            if(optionWindow.FileAssociateTRD != config.AccociateTRDFiles)
            {
                assoc = (optionWindow.FileAssociateTRD ? " 1.trd" : " 0.trd");
                iconsChanged = true;
                param += assoc;
            }

            if(optionWindow.FileAssociateSCL != config.AccociateSCLFiles)
            {
                assoc = (optionWindow.FileAssociateSCL ? " 1.scl" : " 0.scl");
                iconsChanged = true;
                param += assoc;
            }

            if(optionWindow.FileAssociatePZX != config.AccociatePZXFiles)
            {
                assoc = (optionWindow.FileAssociatePZX ? " 1.pzx" : " 0.pzx");

                iconsChanged = true;
                param += assoc;
            }

            if(optionWindow.FileAssociateTZX != config.AccociateTZXFiles)
            {
                assoc = (optionWindow.FileAssociateTZX ? " 1.tzx" : " 0.tzx");

                iconsChanged = true;
                param += assoc;
            }

            if(optionWindow.FileAssociateTAP != config.AccociateTAPFiles)
            {
                assoc = (optionWindow.FileAssociateTAP ? " 1.tap" : " 0.tap");

                iconsChanged = true;
                param += assoc;
            }

            if(optionWindow.FileAssociateSNA != config.AccociateSNAFiles)
            {
                assoc = (optionWindow.FileAssociateSNA ? " 1.sna" : " 0.sna");

                iconsChanged = true;
                param += assoc;
            }

            if(optionWindow.FileAssociateSZX != config.AccociateSZXFiles)
            {
                assoc = (optionWindow.FileAssociateSZX ? " 1.szx" : " 0.szx");

                iconsChanged = true;
                param += assoc;
            }

            if(optionWindow.FileAssociateZ80 != config.AccociateZ80Files)
            {
                assoc = (optionWindow.FileAssociateZ80 ? " 1.z80" : " 0.z80");
                iconsChanged = true;
                param += assoc;
            }

            int exitCode = -1;
            //Force icon refresh in windows explorer shell
            if(iconsChanged)
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
                    if(p != null)
                        p.WaitForExit();
                    exitCode = p.ExitCode;
                }
                catch(Exception e)
                {
                    MessageBox.Show(e.Message, "ZeroFileAssociater launch failed!", MessageBoxButtons.OK);
                    dxWindow.Focus();
                    return;
                }
            }

            //file associations updated successfully!
            if(exitCode == 0)
            {
                config.AccociatePZXFiles = optionWindow.FileAssociatePZX;
                config.AccociateTZXFiles = optionWindow.FileAssociateTZX;
                config.AccociateTAPFiles = optionWindow.FileAssociateTAP;
                config.AccociateSNAFiles = optionWindow.FileAssociateSNA;
                config.AccociateSZXFiles = optionWindow.FileAssociateSZX;
                config.AccociateZ80Files = optionWindow.FileAssociateZ80;
                config.AccociateDSKFiles = optionWindow.FileAssociateDSK;
                config.AccociateTRDFiles = optionWindow.FileAssociateTRD;
                config.AccociateSCLFiles = optionWindow.FileAssociateSCL;

                SHChangeNotify(0x8000000, 0x1000, IntPtr.Zero, IntPtr.Zero);
                MessageBox.Show("File associations updated successfully!", "File associations", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            dxWindow.Focus();
        }

        private void aboutButton_Click(object sender, EventArgs e)
        {
            if((aboutWindow == null) || (aboutWindow.IsDisposed))
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
            zx.Reset(false);
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
            if(state == EMULATOR_STATE.PLAYING_RZX)
                zx.StopPlaybackRZX();
            else if(state == EMULATOR_STATE.RECORDING_RZX)
            {
                if(MessageBox.Show("This will cause the current recording to be discarded. Proceed?", "Unsaved progress", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    zx.DiscardRZX();
                else
                    return;
            }

            SetEmulationState(EMULATOR_STATE.IDLE);
            UpdateRZXInterface();

            if(isPlayingRZX)
            {
                showTapeIndicator = false;
                switch(previousMachine)
                {
                    case MachineModel._48k:
                    zx48ktoolStripMenuItem1_Click(this, null);
                    break;

                    case MachineModel._128k:
                    zXSpectrum128KToolStripMenuItem_Click(this, null);
                    break;

                    case MachineModel._128ke:
                    zXSpectrumToolStripMenuItem_Click(this, null);
                    break;

                    case MachineModel._plus3:
                    zxSpectrum3ToolStripMenuItem_Click(this, null);
                    break;

                    case MachineModel._pentagon:
                    pentagon128KToolStripMenuItem_Click(this, null);
                    break;
                }
                return;
            }

            dxWindow.Suspend();

            if(debugger != null)
            {
                debugger.DeRegisterAllEvents();
                debugger.DeSyncWithZX();
            }

            if(tapeDeck != null)
                tapeDeck.UnRegisterEventHooks();

            showDiskIndicator = false;
            zx.DiskEvent -= new DiskEventHandler(DiskMotorEvent);
            zx.Reset(false);

            if(debugger != null)
            {
                debugger.ReRegisterAllEvents();
                debugger.ReSyncWithZX();
            }

            if(tapeDeck != null)
                tapeDeck.RegisterEventHooks();

            zx.DiskEvent += new DiskEventHandler(DiskMotorEvent);

            dxWindow.Resume();
            dxWindow.Focus();
        }

        public void ShouldExitFullscreen()
        {
            if(dxWindow.EnableDirectX && config.FullScreen)
                GoFullscreen(false);
        }

        private void GoFullscreen(bool full)
        {
            config.FullScreen = full;

            if(full)
            {
                toolStrip1.Visible = false;
                statusStrip1.Visible = false;
                toolStripMenuItem5.Enabled = false;
                toolStripMenuItem1.Enabled = false;
                LastMouseMove = DateTime.Now;
                this.SuspendLayout();
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                oldWindowPosition = this.Location;
                dxWindow.EnableFullScreen = true;
                oldWindowSize = config.WindowSize;
                config.WindowSize = 0;

                if(dxWindow.EnableDirectX)
                {
                    menuStrip1.Visible = false;
                    dxWindow.InitDirectX(Screen.FromControl(this).Bounds.Width, Screen.FromControl(this).Bounds.Height, false);
                }
                else
                {
                    this.Location = new Point(0, 0);
                    this.WindowState = FormWindowState.Maximized;
                    dxWindow.Location = new Point(0, 0);
                    dxWindow.SetSize(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
                }

                if(!commandLineLaunch)
                {
                    Point cursorPos = Cursor.Position;
                    cursorPos.Y = this.PointToScreen(dxWindow.Location).Y;
                    Cursor.Position = cursorPos;
                }
                else
                {
                    Cursor.Hide();
                    CursorIsHidden = true;
                    Cursor.Position = new Point(0, Screen.PrimaryScreen.Bounds.Height);
                    mouseOldPos.X = Cursor.Position.X;
                    mouseOldPos.Y = Cursor.Position.Y;
                }
                this.ResumeLayout();
                dxWindow.Focus();
            }
            else
            {
                menuStrip1.Visible = true;
                toolStrip1.Visible = true;
                statusStrip1.Visible = true;
                toolStripMenuItem5.Enabled = true;
                toolStripMenuItem1.Enabled = true;
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
                this.WindowState = FormWindowState.Normal;

                dxWindow.EnableFullScreen = false;
                this.Location = oldWindowPosition;
                config.WindowSize = oldWindowSize;
                AdjustWindowSize();

                if(CursorIsHidden)
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

        private void PauseEmulation(bool val)
        {
            pauseEmulation = val;
            dxWindow.EmulationIsPaused = val;
            //pauseEmulationESCToolStripMenuItem.Checked = pauseEmulation;
            dxWindow.Invalidate();
            toolStripMenuItem6.Checked = val;
            toolStripButton4.Checked = val;

            SetEmulationState((pauseEmulation ? EMULATOR_STATE.PAUSED : prevState));

        }


        private void pauseEmulationESCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PauseEmulation(!pauseEmulation);
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
            if(config.FullScreen)
            {
                if((Math.Abs(mouseMoveDiff.X)) > 5 || (Math.Abs(mouseMoveDiff.Y) > 5))
                {
                    if(CursorIsHidden)
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
            if(e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            if(e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                LoadZXFile(files[0]);
            }
        }

        private void UseSZX(SZXFile szx)
        {
            if(szx.header.MachineId == (int)SZXFile.ZXTYPE.ZXSTMID_48K)
            {
                if(config.HighCompatibilityMode)
                {
                    zXSpectrumToolStripMenuItem_Click(this, null);
                    zx.Out(0x7ffd, 0x10);
                    zx.Out(0x1ffd, 0x04);
                    zx.pagingDisabled = true;
                }
                else
                    zx48ktoolStripMenuItem1_Click(this, null);
            }
            else if(szx.header.MachineId == (int)SZXFile.ZXTYPE.ZXSTMID_128K)
            {
                if(config.HighCompatibilityMode)
                    zXSpectrumToolStripMenuItem_Click(this, null);
                else //if (config.Model == MachineModel._128k)
                    zXSpectrum128KToolStripMenuItem_Click(this, null);
                //else
                //    zXSpectrumToolStripMenuItem_Click(this, null);
            }
            else if(szx.header.MachineId == (int)SZXFile.ZXTYPE.ZXSTMID_PENTAGON128)
            {
                pentagon128KToolStripMenuItem_Click(this, null);
            }
            else if(szx.header.MachineId == (int)SZXFile.ZXTYPE.ZXSTMID_PLUS3)
            {
                zxSpectrum3ToolStripMenuItem_Click(this, null);
            }

            //Check if file is required in tape deck
            if(szx.InsertTape)
            {
                //External tape file?
                if(szx.tape.flags == 0)
                {
                    //Try to load the tape silently.
                    //With any luck the externalTapeFile holds the full path to the tape.
                    if(File.Exists(szx.externalTapeFile))
                    {
                        LoadZXFile(szx.externalTapeFile);
                    }
                    else //Nope, not found. Ask the user what to do.
                    {
                        String s = "The following tape is expected in the tape deck:\n" + szx.externalTapeFile + "\n\nDo you wish to browse for this file?";
                        if(MessageBox.Show(s, "File request", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            openFileDialog1.InitialDirectory = recentFolder;
                            openFileDialog1.FileName = "";
                            openFileDialog1.Filter = "Tapes (*.pzx, *.tap, *.tzx, *.csw)|*.pzx;*.tap;*.tzx;*.csw|ZIP Archive (*.zip)|*.zip";
                            if(openFileDialog1.ShowDialog() == DialogResult.OK)
                            {
                                LoadZXFile(openFileDialog1.FileName);
                                recentFolder = Path.GetDirectoryName(openFileDialog1.FileName);
                            }
                        }
                    }
                }
                else
                {
                    System.IO.Stream s = new System.IO.MemoryStream(szx.embeddedTapeData);
                    tapeDeck.InsertTape("", s);
                }
            }

            //Check if any disks needs to be inserted
            for(int f = 0; f < szx.numDrivesPresent; f++)
            {
                if(szx.InsertDisk[f])
                {
                    //Try to load the tape silently.
                    //With any luck the externalTapeFile holds the full path to the tape.
                    if(File.Exists(szx.externalDisk[f]))
                    {
                        InsertDisk(szx.externalDisk[f], (byte)f);
                        zx.DiskInsert(szx.externalDisk[f], (byte)f);
                    }
                    else //No disk found. Ask user what to do.
                    {
                        String s = String.Format("The following disk is expected in drive {0}:\n{1}\n\nDo you wish to browse for this file?", f, szx.externalDisk[f]);
                        if(MessageBox.Show(s, "File request", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            OpenDiskFile((byte)f);
                        }
                    }
                }
            }

            config.UseLateTimings = (szx.header.Flags & 0x1) != 0;
            zx.UseSZX(szx);
            szx = null;
            softResetOnly = false;
        }

        public bool LoadSZX(Stream fs)
        {
            SZXFile szx = new SZXFile();
            bool success = szx.LoadSZX(fs);

            if(success)
                UseSZX(szx);

            return success;
        }

        public bool LoadSZX(string filename)
        {
            SZXFile szx = new SZXFile();
            bool success = szx.LoadSZX(filename);
            if(success)
                UseSZX(szx);

            return success;
        }

        private void UseSNA(SNA_SNAPSHOT sna)
        {
            if(sna == null)
                return;

            if(sna is SNA_48K)
            {
                zx48ktoolStripMenuItem1_Click(this, null);
            }
            else if(sna is SNA_128K)
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
            sna = null;
        }

        public void LoadSNA(Stream fs)
        {
            SNA_SNAPSHOT sna = SNAFile.LoadSNA(fs);
            UseSNA(sna);
        }

        public void LoadSNA(string filename)
        {
            SNA_SNAPSHOT sna = SNAFile.LoadSNA(filename);
            UseSNA(sna);
        }

        private void UseRZX(RZXFile rzx, bool isRecording)
        {
            previousMachine = zx.model;

            byte index = 0;

            if(!isRecording && rzx.snapshotData[1] != null)
                index = 1;

            if(rzx.snapshotData[index] != null)
            {
                String ext = new String(rzx.snapshotExtension[index]).ToLower();

                if(ext == "sna\0")
                    LoadSNA(new MemoryStream(rzx.snapshotData[index]));
                else if(ext == "z80\0")
                    LoadZ80(new MemoryStream(rzx.snapshotData[index]));
                else if(ext == "szx\0")
                    LoadSZX(new MemoryStream(rzx.snapshotData[index]));
                else
                    return;
            }

            if(!isRecording)
            {
                isPlayingRZX = true;
                rzx.RZXFileEventHandler += RZXCallback;
                zx.StartPlaybackRZX(rzx);
                SetEmulationState(EMULATOR_STATE.PLAYING_RZX);
            }
            else
            {
                // zx.ContinueRecordingRZX(rzx);
                SetEmulationState(EMULATOR_STATE.RECORDING_RZX);
            }
        }

        public void LoadRZX(Stream fs, bool isRecording)
        {
            /*
            isPlayingRZX = true;
            RZXFile rzx = new RZXFile();
            rzx.LoadRZX(fs);
            rzx.RZXFileEventHandler += RZXCallback;
            rzx.Playback(fs);
            zx.StartPlaybackRZX(rzx);
            SetEmulationState(EMULATOR_STATE.PLAYING_RZX);
            UseRZX(rzx, isRecording);
            */
        }

        public void LoadRZX(string filename)
        {
            if(state == EMULATOR_STATE.PLAYING_RZX)
                zx.StopPlaybackRZX();
            else if(state == EMULATOR_STATE.RECORDING_RZX)
            {
                if(MessageBox.Show("This will cause the current recording to be discarded. Proceed?", "Unsaved progress", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    zx.DiscardRZX();
                else
                    return;
            }
            isPlayingRZX = true;
            RZXFile rzx = new RZXFile();
            rzx.RZXFileEventHandler += RZXCallback;
            //rzx.LoadRZX(filename);
            rzx.Playback(filename);
            zx.StartPlaybackRZX(rzx);
            SetEmulationState(EMULATOR_STATE.PLAYING_RZX);
            //rzx.LoadRZX(filename);
            //UseRZX(rzx, isRecording);
            statusProgressBar.Value = 0;
        }

        private void UseZ80(Z80_SNAPSHOT z80)
        {
            if(z80 == null)
                return;

            if(z80.TYPE == 0)
            {
                zx48ktoolStripMenuItem1_Click(this, null);
            }
            else if(z80.TYPE == 1)
            {
                if(config.HighCompatibilityMode)
                    zXSpectrumToolStripMenuItem_Click(this, null);
                else //if (config.Model == MachineModel._128k)
                    zXSpectrum128KToolStripMenuItem_Click(this, null);
            }
            else if(z80.TYPE == 2)
                zxSpectrum3ToolStripMenuItem_Click(this, null);
            else if(z80.TYPE == 3)
                pentagon128KToolStripMenuItem_Click(this, null);

            zx.UseZ80(z80);
            z80 = null;
        }

        public void LoadZ80(Stream fs)
        {
            Z80_SNAPSHOT z80 = Z80File.LoadZ80(fs);
            UseZ80(z80);
        }

        public void LoadZ80(string filename)
        {
            Z80_SNAPSHOT z80 = Z80File.LoadZ80(filename);
            UseZ80(z80);
        }

        private void OpenZXArchive(String zipFileName)
        {
            if(archiver == null)
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
                if(archiver.FindFirst("*.*", ref archiveItem))
                {
                    do
                    {
                        if(archiveItem.FileName != "")
                        {
                            String ext = archiveItem.FileName.Substring(archiveItem.FileName.Length - 3).ToLower();
                            if(ext == "sna" | ext == "z80" || ext == "szx" || ext == "pzx" || ext == "tzx"
                                    || ext == "tap" || ext == "csw" || ext == "dsk" || ext == "trd"
                                    || ext == "scl" || ext == "rzx" || ext == "scr")
                            {
                                fileNameAndSizeList.Add(archiveItem.FileName);
                                fileNameAndSizeList.Add((archiveItem.UncompressedSize).ToString());
                            }
                        }
                    }
                    while(archiver.FindNext(ref archiveItem));
                }

                if(fileNameAndSizeList.Count == 0)
                {
                    MessageBox.Show("Couldn't find any suitable file to load in this archive!", "No suitable file", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if(fileNameAndSizeList.Count == 2)
                {
                    fileToOpen = fileNameAndSizeList[0];
                }
                else if(fileNameAndSizeList.Count > 2)
                {
                    ArchiveHandler archiveHandler = new ArchiveHandler(fileNameAndSizeList.ToArray());
                    if(archiveHandler.ShowDialog() != DialogResult.OK)
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
                if((ext2 == "dsk") || (ext2 == "trd") || (ext2 == "scl"))
                {
                    if(diskArchivePath[0] != null)
                    {
                        zx.DiskEject(0);
                        File.Delete(diskArchivePath[0]);
                    }
                    byte[] tempBuffer;
                    archiver.ExtractToBuffer(fileToOpen, out tempBuffer);
                    if(ext2 == "scl")
                    {
                        string _tmpFile = Application.StartupPath + @"/tempDiskA." + ext2;
                        File.WriteAllBytes(_tmpFile, tempBuffer);
                        string _file = SCL2TRD(_tmpFile);
                        if(_file != null)
                        {
                            LoadDSK(_file, 0);
                        }
                        else
                        {
                            MessageBox.Show("Unable to open file!", "File error", MessageBoxButtons.OK);
                        }
                        File.Delete(_tmpFile);
                    }
                    else
                    {
                        diskArchivePath[0] = Application.StartupPath + @"/tempDiskA." + ext2;
                        File.WriteAllBytes(diskArchivePath[0], tempBuffer);
                        LoadDSK(diskArchivePath[0], 0);
                    }
                    fileNameAndSizeList.Clear();
                    archiver.CloseArchive();
                    return;
                }
                archiver.ExtractToStream(fileToOpen, stream);
                stream.Position = 0;
                switch(ext2)
                {
                    case "rzx":
                    LoadRZX(stream, false);
                    break;

                    case "sna":
                    LoadSNA(stream);
                    if(tapeDeck.Visible)
                        tapeDeck.Hide();
                    break;

                    case "szx":
                    LoadSZX(stream);
                    if(tapeDeck.Visible)
                        tapeDeck.Hide();
                    break;

                    case "z80":
                    LoadZ80(stream);
                    if(tapeDeck.Visible)
                        tapeDeck.Hide();
                    break;

                    case "pzx":
                    tapeDeck.InsertTape(fileToOpen, stream);
                    //tapeDeck.Show();
                    if(tapeDeck.DoAutoTapeLoad)
                    {
                        doAutoLoadTape = true;
                        hardResetToolStripMenuItem1_Click(this, null);
                    }
                    break;

                    case "scr":
                    byte[] _buffer = stream.ToArray();

                    if(_buffer.Length > 6912)
                    {
                        MessageBox.Show("This file seems to have an unsupported screen format.", "File error", MessageBoxButtons.OK);
                    }
                    else
                    {
                        for(int f = 0; f < 6912; f++)
                        {
                            zx.PokeByteNoContend(16384 + f, _buffer[f]);
                        }
                    }
                    break;

                    case "tzx":
                    case "tap":
                    case "csw":
                    byte[] tempBuffer = stream.ToArray();
                    //archiver.ExtractToBuffer(fileToOpen, out tempBuffer);
                    IntPtr _p;
                    uint _sz = 0;
                    uint _buffSize = (uint)tempBuffer.Length;
                    if(ext2 == "tzx")
                        _p = tzx2pzx_buff(tempBuffer, _buffSize, ref _sz);
                    else if(ext2 == "tap")
                        _p = tap2pzx_buff(tempBuffer, _buffSize, ref _sz);
                    else
                        _p = csw2pzx_buff(tempBuffer, _buffSize, ref _sz);

                    if(_p == (IntPtr)0)
                    {
                        MessageBox.Show("Unable to open the file.", "File Error", MessageBoxButtons.OK);
                        break;
                    }
                    Byte[] _b = new Byte[_sz];
                    Marshal.Copy(_p, _b, 0, (int)_sz);
                    Stream _st = new MemoryStream(_b);
                    tapeDeck.InsertTape(fileToOpen, _st);
                    pzx_close();
                    fileNameAndSizeList.Clear();
                    archiver.CloseArchive();
                    if(tapeDeck.DoAutoTapeLoad)
                    {
                        doAutoLoadTape = true;
                        hardResetToolStripMenuItem1_Click(this, null);
                    }
                    break;
                    /*
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
                     */
                }
            }
            catch(Exception e)
            {
                System.Windows.Forms.MessageBox.Show(e.Message,
                        "Archive failure", System.Windows.Forms.MessageBoxButtons.OK);
            }

            fileNameAndSizeList.Clear();
            archiver.CloseArchive();
            archiver.Dispose();
            archiver = null;
        }

        //Updates the insert disk file menu
        private void InsertDisk(string filename, byte _unit)
        {
            string[] _files = filename.Split('\\');
            int _fileCnt = _files.Length;
            if(zx.DiskInserted(_unit))
            {
                zx.DiskEject(_unit);
            }

            switch(_unit)
            {
                case 0:
                insertDiskAToolStripMenuItem.Text = _files[_fileCnt - 1];
                insertDiskAToolStripMenuItem.DropDownItems.Add(EjectA);
                break;

                case 1:
                insertDiskBToolStripMenuItem.Text = _files[_fileCnt - 1];
                insertDiskBToolStripMenuItem.DropDownItems.Add(EjectB);
                break;

                case 2:
                insertDiskCToolStripMenuItem.Text = _files[_fileCnt - 1];
                insertDiskCToolStripMenuItem.DropDownItems.Add(EjectC);
                break;

                case 3:
                insertDiskDToolStripMenuItem.Text = _files[_fileCnt - 1];
                insertDiskDToolStripMenuItem.DropDownItems.Add(EjectD);
                break;
            }
        }

        //Loads the disk, switching the machine model if required
        private void LoadDSK(string filename, byte _unit)
        {
            String ext = filename.Substring(filename.Length - 3).ToLower();
            if(ext == "dsk")
            {
                if(!(zx is zxPlus3))
                    zxSpectrum3ToolStripMenuItem_Click(this, null);
            }
            else if(ext == "trd")
            {
                if(!(zx is Pentagon128K))
                    pentagon128KToolStripMenuItem_Click(this, null);
            }
            zx.DiskInsert(filename, _unit);
        }

        private string SCL2TRD(string _filename)
        {
            diskArchivePath[0] = _filename.Substring(0, _filename.Length - 4).ToLower() + "_z0temp.trd";

            int trdCurrentSector = 16;

            FileStream fs;
            try
            {
                fs = new FileStream(_filename, FileMode.Open, FileAccess.Read);
            }
            catch
            {
                return null;
            }

            using(BinaryReader r = new BinaryReader(fs))
            {
                int bytesToRead = (int)fs.Length;

                byte[] data = new byte[bytesToRead];
                int bytesRead = r.Read(data, 0, bytesToRead);

                if(bytesRead == 0)
                    return null; //something bad happened!

                byte fileCount = data[8];
                int startIndex = 0;

                FileStream trd = new FileStream(diskArchivePath[0], FileMode.Create, FileAccess.Write);
                using(BinaryWriter w = new BinaryWriter(trd))
                {
                    System.Collections.Generic.List<byte> fileSectorList = new System.Collections.Generic.List<byte>();
                    for(int index = 0; index < fileCount; index++)
                    {
                        startIndex = 9 + index * 14;

                        ArraySegment<byte> dirEntry = new ArraySegment<byte>(data, startIndex, 14);
                        ArraySegment<byte> filename = new ArraySegment<byte>(data, dirEntry.Offset, 8);
                        byte fileExt = data[dirEntry.Offset + 8];
                        byte fileLength = data[dirEntry.Offset + 13];
                        fileSectorList.Add(fileLength);
                        ArraySegment<byte> trdData = new ArraySegment<byte>(data, dirEntry.Offset, 14);
                        byte startTrack = (byte)(trdCurrentSector / 16);
                        byte startSector = (byte)(trdCurrentSector % 16);
                        trdCurrentSector = trdCurrentSector + fileLength;
                        trd.Write(data, trdData.Offset, trdData.Count);
                        trd.WriteByte(startSector);
                        trd.WriteByte(startTrack);
                    }
                    startIndex += 14;

                    //pad directory entries
                    for(int index = 0; index < (128 - fileCount) * 16; index++)
                        trd.WriteByte(0);

                    //-------------------------------------
                    //specification
                    //-------------------------------------

                    //end of directory entries
                    trd.WriteByte(0);

                    //zero fill 224 bytes
                    for(int index = 0; index < 224; index++)
                        trd.WriteByte(0);
                    //write free sector, track info
                    byte firstFreeTrack = (byte)(trdCurrentSector / 16);
                    byte firstFreeSector = (byte)(trdCurrentSector % 16);
                    trd.WriteByte(firstFreeSector);
                    trd.WriteByte(firstFreeTrack);

                    //write disk type as DD, 80 tracks
                    trd.WriteByte(22);

                    //#write file count
                    trd.WriteByte(fileCount);

                    //number of free sectors
                    int numFreeSectors = ((160 - firstFreeTrack) * 16) - firstFreeSector;
                    trd.Write(BitConverter.GetBytes(numFreeSectors), 0, 2);

                    //TRD DOS ID
                    trd.WriteByte(16);

                    //unused 0 x2
                    trd.WriteByte(0);
                    trd.WriteByte(0);

                    //unused (9 bytes filled with space char)
                    for(int index = 0; index < 9; index++)
                        trd.WriteByte(32);

                    //unused (0)
                    trd.WriteByte(0);

                    //num deleted files
                    trd.WriteByte(0);

                    //Label name (8 chars)
                    trd.Write(Encoding.UTF8.GetBytes("Zero_TRD"), 0, 8);

                    //unused (3 zero bytes)
                    trd.WriteByte(0);
                    trd.WriteByte(0);
                    trd.WriteByte(0);

                    //zero fill of 1792 bytes
                    for(int index = 0; index < 1792; index++)
                        trd.WriteByte(0);

                    //-----------------------------------
                    //file entries
                    //-----------------------------------

                    int sectorOffset = startIndex;
                    int bytesWritten = 4096;
                    for(int index = 0; index < fileCount; index++)
                    {
                        sectorOffset += fileSectorList[index] * 256;
                        ArraySegment<byte> sclFile = new ArraySegment<byte>(data, startIndex, sectorOffset - startIndex);

                        startIndex = sectorOffset;
                        trd.Write(data, sclFile.Offset, sclFile.Count);
                        bytesWritten += sclFile.Count;
                    }

                    //pad out the remaining data on the disk
                    for(int index = 0; index < 655360 - bytesWritten; index++)
                        trd.WriteByte(0);
                }
                trd.Close();
            }
            fs.Close();
            return diskArchivePath[0];
        }

        private void LoadTAPFile(string filename)
        {
            using(FileStream fs = new FileStream(filename, FileMode.Open))
            {
                using(BinaryReader br = new BinaryReader(fs))
                {
                    byte[] buffer = new byte[fs.Length];
                    int bytesRead = br.Read(buffer, 0, (int)fs.Length);
                    if(bytesRead == 0)
                        Console.Write("Failed to read");

                    for(int i = 0; i < buffer.Length; i++)
                    {
                        Console.Write(buffer[i]); Console.Write(" ");
                    }
                }
            }
        }

        public void LoadZXFile(string filename)
        {
            String ext = System.IO.Path.GetExtension(filename).ToLower();

            if(ext == ".scr")
            {
                using(FileStream fs = new FileStream(filename, FileMode.Open))
                {
                    using(BinaryReader br = new BinaryReader(fs))
                    {
                        byte[] buffer = new byte[6912];
                        int bytesRead = br.Read(buffer, 0, 6912);

                        if(fs.Length > 6912 || bytesRead == 0)
                        {
                            MessageBox.Show("This file seems to have an unsupported screen format.", "File error", MessageBoxButtons.OK);
                        }
                        else
                        {
                            for(int f = 0; f < 6912; f++)
                            {
                                zx.PokeByteNoContend(16384 + f, buffer[f]);
                            }
                        }
                    }
                }
            }
            else if(ext == ".rzx")
            {
                LoadRZX(filename);
                if(tapeDeck.Visible)
                    tapeDeck.Hide();
            }
            else if((ext == ".dsk") || (ext == ".trd") || (ext == ".scl"))
            {
                if(diskArchivePath[0] != null)
                {
                    if(zx.DiskInserted(0))
                    {
                        zx.DiskEject(0);
                    }
                    File.Delete(diskArchivePath[0]);
                    diskArchivePath[0] = null;
                }
                if(ext == ".scl")
                {
                    string _file = SCL2TRD(filename);
                    if(_file != null)
                    {
                        InsertDisk(filename, 0);
                        LoadDSK(_file, 0);
                    }
                    else
                    {
                        MessageBox.Show("Unable to open file!", "File error", MessageBoxButtons.OK);
                        return;
                    }
                }
                else
                {
                    InsertDisk(filename, 0);
                    LoadDSK(filename, 0);
                }
            }
            else if(ext == ".sna")
            {
                LoadSNA(filename);
                if(tapeDeck.Visible)
                    tapeDeck.Hide();
            }
            else if(ext == ".z80")
            {
                LoadZ80(filename);
                if(tapeDeck.Visible)
                    tapeDeck.Hide();
            }
            else if(ext == ".szx")
            {
                LoadSZX(filename);
                if(tapeDeck.Visible)
                    tapeDeck.Hide();
            }
            else if(ext == ".pzx")
            {
                tapeDeck.InsertTape(filename);
                //tapeDeck.Show();
                if(tapeDeck.DoAutoTapeLoad)
                {
                    doAutoLoadTape = true;
                    hardResetToolStripMenuItem1_Click(this, null);
                }
            }
            else if (ext == ".bas")
            {
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.UseShellExecute = true;
                startInfo.WorkingDirectory = Application.StartupPath;
                startInfo.FileName = "zmakebas";
                startInfo.Arguments = "-o " + Application.LocalUserAppDataPath + "//_tempbas.tap " + filename;
                startInfo.Verb = "runas";
                startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden; //run silent, run deep...
                int exitCode = -1;
                try
                {
                    System.Diagnostics.Process p = System.Diagnostics.Process.Start(startInfo);
                    if(p != null)
                        p.WaitForExit();
                    exitCode = p.ExitCode;
                }
                catch(Exception e)
                {
                    MessageBox.Show(e.Message, "Operation failed", MessageBoxButtons.OK);
                    dxWindow.Focus();
                    return;
                }
                if (exitCode == 0)
                {
                    IntPtr _p;
                    uint _sz = 0;
                    _p = tap2pzx(Application.LocalUserAppDataPath + "//_tempbas.tap", ref _sz);
                    Byte[] _b = new Byte[_sz];
                    Marshal.Copy(_p, _b, 0, (int)_sz);
                    Stream _st = new MemoryStream(_b);
                    tapeDeck.InsertTape(filename, _st);
                    pzx_close();
                    doAutoLoadTape = true;
                    hardResetToolStripMenuItem1_Click(this, null);
                }
            }
            else if((ext == ".tzx") || (ext == ".tap") || (ext == ".csw"))
            {
                IntPtr _p;
                uint _sz = 0;
                if(ext == ".tzx")
                    _p = tzx2pzx(filename, ref _sz);
                else if(ext == ".tap")
                {
                    _p = tap2pzx(filename, ref _sz);
                }
                else
                    _p = csw2pzx(filename, ref _sz);

                Byte[] _b = new Byte[_sz];
                Marshal.Copy(_p, _b, 0, (int)_sz);
                Stream _st = new MemoryStream(_b);
                tapeDeck.InsertTape(filename, _st);
                pzx_close();

                if(tapeDeck.DoAutoTapeLoad)
                {
                    doAutoLoadTape = true;
                    hardResetToolStripMenuItem1_Click(this, null);
                }
            }
            else if(ext == ".zip") //handle zip archives
            {
                pauseEmulation = true;
                OpenZXArchive(filename);
                pauseEmulation = false;
            }
            else
                System.Windows.Forms.MessageBox.Show("Sorry, but Zero doesn't recognise this file format.",
                   "Unsupported Format", System.Windows.Forms.MessageBoxButtons.OK);
        }

        private void interlaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dxWindow.ShowScanlines = interlaceToolStripMenuItem.Checked;
            config.EnableInterlacedOverlay = dxWindow.ShowScanlines;
        }

        private void screenshotMenuItem1_Click(object sender, EventArgs e)
        {
            ShouldExitFullscreen();

            saveFileDialog1.InitialDirectory = config.PathScreenshots;
            saveFileDialog1.Title = "Save Screenshot";
            saveFileDialog1.FileName = "";
            saveFileDialog1.Filter = "Bitmap (*.bmp)|*.bmp|Spectrum Screen (*.scr)|*.scr|PNG (*.png)|*.png|JPEG (*.jpeg)|*.jpeg|GIF (*.gif)|*.gif";
            if(saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                Bitmap screenshot = dxWindow.GetScreen();
                switch(saveFileDialog1.FilterIndex)
                {
                    case 1:
                    screenshot.Save(saveFileDialog1.FileName, System.Drawing.Imaging.ImageFormat.Bmp);
                    break;

                    case 2:
                    using(System.IO.FileStream scrFile = new System.IO.FileStream(saveFileDialog1.FileName, System.IO.FileMode.Create))
                    {
                        using(System.IO.BinaryWriter r = new System.IO.BinaryWriter(scrFile))
                        {
                            for(int f = 16384; f < 16384 + 6912; f++)
                            {
                                byte data = (byte)(zx.PeekByteNoContend(f));
                                r.Write(data);
                            }
                        }
                    }
                    break;

                    case 3:
                    screenshot.Save(saveFileDialog1.FileName, System.Drawing.Imaging.ImageFormat.Png);
                    break;

                    case 4:
                    screenshot.Save(saveFileDialog1.FileName, System.Drawing.Imaging.ImageFormat.Jpeg);
                    break;

                    case 5:
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

        public void saveSnapshotMenuItem_Click(object sender, EventArgs e)
        {
            ShouldExitFullscreen();

            saveFileDialog1.Title = "Save Snapshot";
            saveFileDialog1.FileName = "";
            saveFileDialog1.Filter = "SZX | *.szx| SNA | *.sna";

            if(saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if(saveFileDialog1.FilterIndex == 1)
                    zx.SaveSZX(saveFileDialog1.FileName);
                else
                    zx.SaveSNA(saveFileDialog1.FileName);

                MessageBox.Show("Snapshot saved!", "File saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void openFileMenuItem1_Click(object sender, EventArgs e)
        {
            ShouldExitFullscreen();

            zx.Pause();
            dxWindow.Suspend();
            openFileDialog1.InitialDirectory = recentFolder;
            openFileDialog1.Title = "Choose a file";
            openFileDialog1.FileName = "";
            openFileDialog1.Filter = "All supported files|*.szx;*.sna;*.z80;*.pzx;*.tzx;*.tap;*.csw;*.rzx;*.zip;*.dsk;*.trd;*.scl;*.scr|Snapshots (*.szx, *.sna, *.z80)|*.szx; *.sna;*.z80|Tapes (*.pzx, *.tap, *.tzx, *.csw)|*.pzx;*.tap;*.tzx;*.csw|Disks (*.dsk, *.trd, *.scl)|*.dsk;*.trd;*.scl|ZIP Archive (*.zip)|*.zip|Action Replay (*.rzx)|*.rzx|Spectrum Screen (*.scr)|*.scr";
            if(openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                this.mruManager.AddRecentFile(openFileDialog1.FileName);
                recentFolder = Path.GetDirectoryName(openFileDialog1.FileName);
                LoadZXFile(openFileDialog1.FileName);
            }

            dxWindow.Resume();
            zx.Resume();
            dxWindow.Focus();
        }

        private void trainerToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        //Adjust the window if switching between models that have different speccy screen sizes
        /* private void AdjustRenderWindow(int x, int y) {
             dxWindow.Suspend();
             int _offsetX = zx.GetOriginOffsetX();
             int _offsetY = zx.GetOriginOffsetY();
             int speccyWidth = zx.GetTotalScreenWidth() - _offsetX;
             int speccyHeight = zx.GetTotalScreenHeight() - _offsetY;
             int dxWindowOffsetX = 10;
             int dxWindowOffsetY = toolStrip1.Location.Y + toolStrip1.Height;

             Rectangle screenRectangle = RectangleToScreen(this.ClientRectangle);

             int titleHeight = screenRectangle.Top - this.Top;
             int totalClientWidth = speccyWidth + dxWindowOffsetX;
             int totalClientHeight = speccyHeight + dxWindowOffsetY + statusStrip1.Height + titleHeight;
             int adjustWidth = speccyWidth * (config.WindowSize / 100);
             int adjustHeight = speccyHeight * (config.WindowSize / 100);

             borderAdjust = config.BorderSize + config.BorderSize * (config.WindowSize / 100);
             this.Size = new Size(totalClientWidth + adjustWidth - (2 * borderAdjust), totalClientHeight + adjustHeight - (2 * borderAdjust));
             //Hack: at the lowest window size the pentagon left border width doesn't match up with the right one,
             //for reasons unknown.
             //All it needs is a 8 pix adjust hence this:
             if (zx.model == MachineModel._pentagon)
                 _offsetX -= 8;

             dxWindow.Location = new Point( _offsetX - borderAdjust, dxWindowOffsetY - _offsetY - borderAdjust);
             dxWindow.SetSize(zx.GetTotalScreenWidth() + adjustWidth, zx.GetTotalScreenHeight() + adjustHeight);
             dxWindow.SendToBack();
             dxWindow.LEDIndicatorPosition = new Point(dxWindow.Location.X, dxWindow.Location.Y);
             dxWindow.Resume();
             dxWindow.Focus();
         }*/

        //The app window, including the emulator window and all the buttons, panels et al
        private void AdjustWindowSize()
        {
            fullScreenToolStripMenuItem.Checked = false;

            int _offsetX = zx.GetOriginOffsetX();
            int _offsetY = zx.GetOriginOffsetY();
            int speccyWidth = zx.GetTotalScreenWidth() - _offsetX;
            int speccyHeight = zx.GetTotalScreenHeight() - _offsetY;
            int dxWindowOffsetX = 10;
            int dxWindowOffsetY = toolStrip1.Location.Y + toolStrip1.Height;

            Rectangle screenRectangle = RectangleToScreen(this.ClientRectangle);

            int titleHeight = screenRectangle.Top - this.Top;
            int totalClientWidth = speccyWidth + dxWindowOffsetX;
            int totalClientHeight = speccyHeight + dxWindowOffsetY + statusStrip1.Height + titleHeight;
            int adjustWidth = (speccyWidth * config.WindowSize / 100);
            int adjustHeight = (speccyHeight * config.WindowSize / 100);

            borderAdjust = config.BorderSize + config.BorderSize * (config.WindowSize / 100);

            //Hack: at the lowest window size the pentagon left border width doesn't match up with the right one,
            //for reasons unknown.
            //All it needs is a 8 pix adjust hence this:
            //if (zx.model == MachineModel._pentagon)
            //    _offsetX -= 8;

            this.Size = new Size(totalClientWidth + adjustWidth - (2 * borderAdjust) + _offsetX * 2, totalClientHeight + adjustHeight - (2 * borderAdjust));
            dxWindow.Location = new Point(_offsetX - borderAdjust, dxWindowOffsetY - _offsetY - borderAdjust);
            dxWindow.SetSize(zx.GetTotalScreenWidth() + adjustWidth, zx.GetTotalScreenHeight() + adjustHeight);
            dxWindow.SendToBack();
            dxWindow.LEDIndicatorPosition = new Point(dxWindow.Location.X, dxWindow.Location.Y);
            dxWindow.Focus();
            dxWindow.Invalidate();

            Rectangle workingArea = Screen.GetWorkingArea(this);

            if((totalClientWidth + (speccyWidth * (config.WindowSize + 50)) / 100 - (2 * borderAdjust) >= workingArea.Width) ||
                ((totalClientHeight + (speccyHeight * (config.WindowSize + 50)) / 100 - (2 * borderAdjust)) >= workingArea.Height))
            {
                toolStripMenuItem5.Enabled = false;
            }
            else
            {
                if(config.WindowSize >= 500)
                    toolStripMenuItem5.Enabled = false;
                else
                    toolStripMenuItem5.Enabled = true;
            }

            if(config.WindowSize == 0)
                toolStripMenuItem1.Enabled = false;
            else
                toolStripMenuItem1.Enabled = true;
        }

        private void toolStripMenuItem5_Click_1(object sender, EventArgs e)
        {
            if(!toolStripMenuItem5.Enabled)
            {
                MessageBox.Show("Emulator window cannot be resized beyond your monitor's maximum screen dimensions.", "Window Size", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            config.WindowSize += 50; //Increase window size by 50% of normal

            AdjustWindowSize();
        }

        private void panel4_Paint(object sender, PaintEventArgs e)
        {
            //e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            //e.Graphics.DrawImage(panel4.BackgroundImage, panel4.Location.X, panel4.Location.Y, panel4.Width, panel4.Height);
        }

        private void panel5_Paint(object sender, PaintEventArgs e)
        {
            // e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            // e.Graphics.DrawImage(ZiggyWin.Properties.Resources.rightPane11, panel5.Location.X, panel5.Location.Y, panel5.Width, panel5.Height);
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if(!toolStripMenuItem1.Enabled)
                return;
            if(config.WindowSize > 0)
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

            for(int y = 0; y < _img.Height; y++)
            {
                for(int x = 0; x < _img.Width; x++)
                {
                    Color imgPixel = _img.GetPixel(x, y);
                    if(!inimage)
                    {
                        if(imgPixel != _matchColor)
                        {
                            inimage = true;
                            rc.X = x;
                            rc.Y = y;
                            rc.Height = 1;
                        }
                    }
                    else
                    {
                        if(imgPixel == _matchColor)
                        {
                            inimage = false;
                            rc.Width = x - rc.X;
                            rgn.Union(rc);
                        }
                    }
                }

                if(inimage)
                {
                    inimage = false;
                    rc.Width = _img.Width - rc.X;
                    rgn.Union(rc);
                }
            }
            return rgn;
        }

        /*
        private void contextMenuStrip1_VisibleChanged(object sender, EventArgs e)
        {
            //pauseEmulation = contextMenuStrip1.Visible;
            if (CursorIsHidden)
            {
                CursorIsHidden = false;
                Cursor.Show();
            }
        }
        */
        private void paneRight_MouseDown(object sender, MouseEventArgs e)
        {
            //if (pauseEmulation)
            //    return;
            //Form_MouseDown(sender, e);
            //dxWindow.Focus();
        }

        private void paneRight_MouseMove(object sender, MouseEventArgs e)
        {
            // if (pauseEmulation)
            //     return;
            // Form_MouseMove(sender, e);
            // dxWindow.Focus();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            if((aboutWindow == null) || (aboutWindow.IsDisposed))
                aboutWindow = new AboutBox1(this);
            aboutWindow.ShowDialog(this);
            dxWindow.Focus();
        }

        private bool OpenDiskFile(byte _unit)
        {
            bool isError = false;

            openFileDialog1.InitialDirectory = recentFolder;
            openFileDialog1.Title = "Choose a file";
            openFileDialog1.FileName = "";
            if(zx is zxPlus3)
                openFileDialog1.Filter = "All supported files|*.dsk; *.zip|+3 Disks (*.dsk)|*.dsk|ZIP Archive (*.zip)|*.zip";
            else
                openFileDialog1.Filter = "All supported files|*.trd;*.scl;*.zip|Beta Disks (*.trd, *.scl)|*.trd;*.scl|ZIP Archive (*.zip)|*.zip";

            if(openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                if(debugger != null)
                {
                    debugger.Close();
                    debugger.Dispose();
                    debugger = null;
                }
                if(diskArchivePath[_unit] != null)
                {
                    File.Delete(diskArchivePath[_unit]);
                    diskArchivePath[_unit] = null;
                }
                String ext = System.IO.Path.GetExtension(openFileDialog1.FileName).ToLower();
                if(ext == ".zip") //handle zip archives
                {
                    if(archiver == null)
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

                        if(archiver.FindFirst("*.*", ref archiveItem))
                        {
                            do
                            {
                                if(archiveItem.FileName != "")
                                {
                                    String ext2 = archiveItem.FileName.Substring(archiveItem.FileName.Length - 3).ToLower();
                                    if(zx is zxPlus3)
                                    {
                                        if(ext2 == "dsk")
                                        {
                                            fileNameAndSizeList.Add(archiveItem.FileName);
                                            fileNameAndSizeList.Add((archiveItem.UncompressedSize).ToString());
                                        }
                                    }
                                    else if(zx is Pentagon128K)
                                    {
                                        if((ext2 == "trd") || (ext2 == "scl"))
                                        {
                                            fileNameAndSizeList.Add(archiveItem.FileName);
                                            fileNameAndSizeList.Add((archiveItem.UncompressedSize).ToString());
                                        }
                                    }
                                }
                            }
                            while(archiver.FindNext(ref archiveItem));
                        }

                        if(fileNameAndSizeList.Count == 0)
                        {
                            MessageBox.Show("Couldn't find any suitable file to load in this archive.", "No suitable file", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            isError = true;
                        }
                        else
                        {
                            if(fileNameAndSizeList.Count == 2)
                            {
                                fileToOpen = fileNameAndSizeList[0];
                            }
                            else if(fileNameAndSizeList.Count > 2)
                            {
                                ArchiveHandler archiveHandler = new ArchiveHandler(fileNameAndSizeList.ToArray());
                                if(archiveHandler.ShowDialog() == DialogResult.OK)
                                {
                                    fileToOpen = archiveHandler.FileToOpen;
                                }
                                else
                                    isError = true;
                            }

                            if(!isError)
                            {
                                String ext2 = fileToOpen.Substring(fileToOpen.Length - 3).ToLower();
                                System.IO.MemoryStream stream = new MemoryStream();
                                archiver.Options.CreateDirs = false;
                                byte[] tempBuffer;
                                archiver.ExtractToBuffer(fileToOpen, out tempBuffer);
                                diskArchivePath[_unit] = Application.StartupPath + @"/tempDisk" + _unit.ToString() + "." + ext2;
                                File.WriteAllBytes(diskArchivePath[_unit], tempBuffer);
                                InsertDisk(fileToOpen, _unit);
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
                    catch(Exception e)
                    {
                        System.Windows.Forms.MessageBox.Show(e.Message,
                                "Archive failure", System.Windows.Forms.MessageBoxButtons.OK);
                        isError = true;
                    }
                    pauseEmulation = false;
                }
                else
                {
                    //string[] _files = openFileDialog1.FileName.Split('\\');
                    //int _fileCnt = _files.Length;
                    InsertDisk(openFileDialog1.FileName, _unit);
                    LoadDSK(openFileDialog1.FileName, _unit);
                }
                if(debugger != null)
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
            OpenDiskFile(0);
        }

        private void insertDiskBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenDiskFile(1);
        }

        private void insertDiskCToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenDiskFile(2);
        }

        private void insertDiskDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenDiskFile(3);
        }

        private void searchOnlineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShouldExitFullscreen();

            if((infoseekWiz == null) || (infoseekWiz.IsDisposed))
            {
                infoseekWiz = new Infoseeker();
                infoseekWiz.DownloadCompleteEvent += new FileDownloadHandler(OnFileDownloadEvent);
            }
            pauseEmulation = true;
            infoseekWiz.Show();
            infoseekWiz.BringToFront();
            pauseEmulation = false;
        }

        private void tapeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(tapeDeck != null)
            {
                tapeDeck.Show();
                return;
            }
        }

        private void cheatHelperToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if((trainerWiz == null) || (trainerWiz.IsDisposed))
            {
                trainerWiz = new Trainer_Wizard(this);
                trainerWiz.Show();
            }

            //pauseEmulation = true;

            //pauseEmulation = false;
            //trainerWiz.Dispose();
            trainerWiz.BringToFront();
        }

        private void aboutZeroToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(aboutWindow == null)
                aboutWindow = new AboutBox1(this);
            aboutWindow.ShowDialog(this);
            dxWindow.Focus();
        }

        private void pixelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dxWindow.PixelSmoothing = pixelToolStripMenuItem.Checked;
            config.EnablePixelSmoothing = dxWindow.PixelSmoothing;
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            ShouldExitFullscreen();

            if(speccyKeyboard == null || speccyKeyboard.IsDisposed)
                speccyKeyboard = new SpectrumKeyboard(this);

            speccyKeyboard.Show();
            speccyKeyboard.BringToFront();
        }

        private void tapeBrowserButton_Click(object sender, EventArgs e)
        {
            ShouldExitFullscreen();

            if(tapeDeck != null)
            {
                tapeDeck.Show();
                tapeDeck.BringToFront();
                return;
            }
        }

        private void rzxPlaybackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            zx.Pause();

            dxWindow.Suspend();
            openFileDialog1.InitialDirectory = recentFolder;
            openFileDialog1.Title = "Choose a file";
            openFileDialog1.FileName = "";
            openFileDialog1.Filter = "Action Replay (*.rzx)|*.rzx";

            if(openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                recentFolder = Path.GetDirectoryName(openFileDialog1.FileName);
                LoadZXFile(openFileDialog1.FileName);
            }

            dxWindow.Resume();
            zx.Resume();
            dxWindow.Focus();
        }

        private void rzxRecordToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShouldExitFullscreen();

            saveFileDialog1.Title = "Save Action Replay";
            saveFileDialog1.FileName = "";
            saveFileDialog1.Filter = "Action Replay|*.rzx";

            if(saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                zx.StartRecordingRZX(saveFileDialog1.FileName, RZXCallback);
                SetEmulationState(EMULATOR_STATE.RECORDING_RZX);
            }
        }

        private void rzxContinueSessionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dxWindow.Suspend();
            openFileDialog1.InitialDirectory = recentFolder;
            openFileDialog1.Title = "Choose a file";
            openFileDialog1.FileName = "";
            openFileDialog1.Filter = "Action Replay (*.rzx)|*.rzx";

            if(openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                recentFolder = Path.GetDirectoryName(openFileDialog1.FileName);
                //LoadRZX(openFileDialog1.FileName, true);

                //if (!zx.IsValidSessionRZX())
                if(!zx.ContinueRZXSession(openFileDialog1.FileName))
                    MessageBox.Show("This is not a valid or recognized recording session.", "Invalid RZX Session", MessageBoxButtons.OK);
                else
                {
                    ForceScreenUpdate(true);
                    SetEmulationState(EMULATOR_STATE.RECORDING_RZX);
                    //  PauseEmulation(true);
                }
            }
            dxWindow.Resume();
            dxWindow.Focus();
        }

        private void rzxStopToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if(zx.isRecordingRZX)
                SaveRZXRecording(false);
            else if(zx.isPlayingRZX)
            {
                zx.StopPlaybackRZX();
                SetEmulationState(EMULATOR_STATE.IDLE);
                UpdateRZXInterface();
            }
        }

        private void rzxFinaliseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(zx.isRecordingRZX)
                SaveRZXRecording(true);
        }

        private void SaveRZXRecording(bool finalise)
        {
            zx.SaveRZX(finalise);
            MessageBox.Show("RZX recording saved successfully!", "File saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            SetEmulationState(EMULATOR_STATE.IDLE);
            UpdateRZXInterface();
        }

        private void rzxDiscardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            zx.DiscardRZX();
            SetEmulationState(EMULATOR_STATE.IDLE);
            UpdateRZXInterface();
        }

        private void insertBookmarkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(zx.isRecordingRZX)
                zx.InsertBookmark();
        }

        private void rzxRollback(object sender, EventArgs e)
        {
            ((System.Timers.Timer)sender).Enabled = false;
            zx.RollbackRZX();
            ForceScreenUpdate(true);
            //PauseEmulation(true);
        }

        private void rollbackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(zx.isRecordingRZX)
            {
                System.Timers.Timer dispatcherTimer = new System.Timers.Timer();
                dispatcherTimer.Elapsed += new System.Timers.ElapsedEventHandler(rzxRollback);
                dispatcherTimer.Interval = 20;
                dispatcherTimer.Enabled = true;
                dispatcherTimer.SynchronizingObject = this;
                dispatcherTimer.Start();
            }
        }

        private void libraryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            library = new ZLibrary();
            library.Show();
        }

        private void contextMenuStrip2_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }

        private void toolStripSeparator13_Click(object sender, EventArgs e)
        {

        }

        private void soundStatusLabel_Click(object sender, EventArgs e)
        {
            zx.MuteSound(config.EnableSound);
            config.EnableSound = !config.EnableSound;
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if(!config.FullScreen && isResizing)
                dxWindow.SetSize(panel1.Width, panel1.Height);
        }

        private void Form1_ResizeBegin(object sender, EventArgs e)
        {
            isResizing = true;
        }

        private void Form1_ResizeEnd(object sender, EventArgs e)
        {
            isResizing = false;
        }

        private void SetupJoysticks()
        {

            if(joystick1Index >= 0)
            {
                joystick1.Release();
                joystick1.InitJoystick(this, joystick1Index);
            }

            if(joystick2Index >= 0)
            {
                joystick2.Release();
                joystick2.InitJoystick(this, joystick2Index);
            }

            if((joystick2MapIndex == (int)zxmachine.JoysticksEmulated.KEMPSTON) || (joystick1MapIndex == (int)zxmachine.JoysticksEmulated.KEMPSTON))
                zx.HasKempstonJoystick = true;
            else
                zx.HasKempstonJoystick = false;

            if((config.EnableKey2Joy) && (config.Key2JoystickType == (int)zxmachine.JoysticksEmulated.KEMPSTON))
            {
                zx.HasKempstonJoystick = true;
            }

            zx.UseKempstonPort1F = config.KempstonUsesPort1F;
        }

        private void statusStrip1_Resize(object sender, EventArgs e)
        {
            statusProgressBar.Width = statusStrip1.Width * 28 / 100;
        }

        private void basicImportToolStripMenuItem3_Click(object sender, EventArgs e)
        {
            basicImporter = new Tools.BASICImporter(this);
            basicImporter.Show();
        }
    }
}