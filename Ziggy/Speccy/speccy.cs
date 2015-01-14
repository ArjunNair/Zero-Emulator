using System;
using System.ComponentModel;
using Peripherals;

namespace Speccy
{
    public static class SpectrumCharSet
    {
        public static string[] Keywords = {  "RND", "INKEY$", "PI", "FN", "POINT", "SCREEN$", "ATTR",
                                            "AT", "TAB", "VAL$", "CODE", "VAL", "LEN", "SIN", "COS",
                                            "TAN", "ASN", "ACS", "ATN", "LN", "EXP", "INT", "SQR",
                                            "SGN", "ABS", "PEEK", "IN", "USR", "STR$","CHR$", "NOT",
                                            "BIN", "OR", "AND", "<=", ">=", "<>", "LINE", "THEN",
                                            "TO", "STEP", "DEF FN", "CAT", "FORMAT", "MOVE", "ERASE",
                                            "OPEN #", "CLOSE #", "MERGE", "VERIFY", "BEEP", "CIRCLE",
                                            "INK", "PAPER", "FLASH", "BRIGHT", "INVERSE", "OVER",
                                            "OUT", "LPRINT", "LLIST", "STOP", "READ", "DATA", "RESTORE",
                                            "NEW", "BORDER", "CONTINUE", "DIM", "REM", "FOR", "GO TO",
                                            "GO SUB", "INPUT", "LOAD", "LIST", "LET", "PAUSE", "NEXT",
                                            "POKE", "PRINT", "PLOT", "RUN", "SAVE", "RANDOMIZE", "IF",
                                            "CLS", "DRAW", "CLEAR", "RETURN", "COPY"};
    };

    //Matches SZX snapshot machine identifiers
    public enum MachineModel
    {
        _16k,
        _48k,
        _128k,
        _plus2,
        _plus2A,
        _plus3,
        _plus3E,
        _pentagon,
        _SE = 11,
        _NTSC48k = 15,
        _128ke = 16
    };

    //Handy enum for Monitor
    public enum SPECCY_EVENT
    {
        [Description("PC")]
        OPCODE_PC,
        [Description("HL")]
        OPCODE_HL,
        [Description("BC")]
        OPCODE_BC,
        [Description("DE")]
        OPCODE_DE,
        [Description("A")]
        OPCODE_A,
        [Description("IX")]
        OPCODE_IX,
        [Description("IY")]
        OPCODE_IY,
        [Description("SP")]
        OPCODE_SP,
        [Description("Memory Write")]
        MEMORY_WRITE,
        [Description("Memory Read")]
        MEMORY_READ,
        [Description("Memory Execute")]
        MEMORY_EXECUTE,
        [Description("Port Write")]
        PORT_WRITE,
        [Description("Port Read")]
        PORT_READ,
        [Description("ULA Write")]
        ULA_WRITE,
        [Description("ULA Read")]
        ULA_READ,
        [Description("Retriggered Interrupt")]
        RE_INTTERUPT,
        [Description("Interrupt")]
        INTTERUPT,
        [Description("Frame Start")]
        FRAME_START,
        [Description("Frame End")]
        FRAME_END
    }

    //Handy enum for the tape deck
    public enum TapeEventType
    {
        START_TAPE,
        STOP_TAPE,
        EDGE_LOAD,
        SAVE_TAP,
        CLOSE_TAP,
        FLASH_LOAD,
        NEXT_BLOCK
    }

    public enum RAM_BANK
    {
        ZERO_1 = 0,
        ZERO_2,
        ONE_1,
        ONE_2,
        TWO_1,
        TWO_2,
        THREE_1,
        THREE_2,
        FOUR_1,
        FOUR_2,
        FIVE_1,
        FIVE_2,
        SIX_1,
        SIX_2,
        SEVEN_1,
        SEVEN_2,
        EIGHT_1,
        EIGHT_2
    }

    #region Delegates and args for speccy related events (used by monitor)
    public class MemoryEventArgs : EventArgs
    {
        private int addr, val;

        public MemoryEventArgs(int _addr, int _val) {
            this.addr = _addr;
            this.val = _val;
        }

        public int Address {
            get {
                return addr;
            }
        }

        public int Byte {
            get {
                return val;
            }
        }
    }

    public class OpcodeExecutedEventArgs : EventArgs { }

    public class DiskEventArgs : EventArgs
    {
        //Lower 4 bits indicate whether a disk is present in drives A,B,C,D
        //bit 4 = motor state
        public int EventType { get; set; }

        public DiskEventArgs(int _type) {
            EventType = _type;
        }
    }

    public class TapeEventArgs : EventArgs
    {
        private TapeEventType type;

        public TapeEventArgs(TapeEventType _type) {
            type = _type;
        }

        public TapeEventType EventType {
            get { return type; }
        }
    }

    public class PortIOEventArgs : EventArgs
    {
        private int port, val;
        private bool isWrite;

        public PortIOEventArgs(int _port, int _val, bool _write) {
            port = _port;
            val = _val;
            isWrite = _write;
        }

        public int Port {
            get { return port; }
        }

        public int Value {
            get { return val; }
        }

        public bool IsWrite {
            get { return isWrite; }
        }
    }

    public class StateChangeEventArgs : EventArgs
    {
        private SPECCY_EVENT eventType;

        public StateChangeEventArgs(SPECCY_EVENT _eventType)
        {
            eventType = _eventType;
        }

        public SPECCY_EVENT EventType
        {
            get { return eventType; }
        }
    }

    public delegate void MemoryWriteEventHandler(object sender, MemoryEventArgs e);

    public delegate void MemoryReadEventHandler(object sender, MemoryEventArgs e);

    public delegate void MemoryExecuteEventHandler(object sender, MemoryEventArgs e);

    public delegate void OpcodeExecutedEventHandler(object sender);

    public delegate void TapeEventHandler(object sender, TapeEventArgs e);

    public delegate void PortIOEventHandler(object sender, PortIOEventArgs e);

    public delegate void DiskEventHandler(object sender, DiskEventArgs e);

    public delegate void StateChangeEventHandler(object sender, StateChangeEventArgs e);

    public delegate void PopStackEventHandler(object sender, int addr);

    public delegate void PushStackEventHandler(object sender, int addr);

    public delegate void FrameStartEventHandler(object sender);

    public delegate void FrameEndEventHandler(object sender);
    #endregion

    /// <summary>
    /// zxmachine is the heart of speccy emulation.
    /// It includes core execution, ula, sound, input and interrupt handling
    /// </summary>
    public abstract class zxmachine : Z80Core
    {
        #region Event handlers for the Monitor, primarily
        public event MemoryWriteEventHandler MemoryWriteEvent;
        public event MemoryReadEventHandler MemoryReadEvent;
        public event MemoryExecuteEventHandler MemoryExecuteEvent;
        public event OpcodeExecutedEventHandler OpcodeExecutedEvent;
        public event TapeEventHandler TapeEvent;
        public event PortIOEventHandler PortEvent;
        public event DiskEventHandler DiskEvent;
        public event StateChangeEventHandler StateChangeEvent;
        public event PopStackEventHandler PopStackEvent;
        public event PushStackEventHandler PushStackEvent;
        public event FrameEndEventHandler FrameEndEvent;
        public event FrameStartEventHandler FrameStartEvent;

        protected virtual void OnFrameEndEvent()
        {
            if (FrameEndEvent != null)
                FrameEndEvent(this);
        }

        protected virtual void OnFrameStartEvent()
        {
            if (FrameStartEvent != null)
                FrameStartEvent(this);
        }

        protected virtual void OnMemoryWriteEvent(MemoryEventArgs e) {
            if (MemoryWriteEvent != null)
                MemoryWriteEvent(this, e);
        }

        protected virtual void OnMemoryReadEvent(MemoryEventArgs e) {
            if (MemoryReadEvent != null)
                MemoryReadEvent(this, e);
        }

        protected virtual void OnMemoryExecuteEvent(MemoryEventArgs e) {
            if (MemoryExecuteEvent != null)
                MemoryExecuteEvent(this, e);
        }

        public virtual void OnOpcodeExecutedEvent() {
            if (OpcodeExecutedEvent != null)
                OpcodeExecutedEvent(this);
        }

        protected virtual void OnTapeEvent(TapeEventArgs e) {
            if (TapeEvent != null)
                TapeEvent(this, e);
        }

        protected virtual void OnDiskEvent(DiskEventArgs e) {
            if (DiskEvent != null)
                DiskEvent(this, e);
        }

        protected virtual void OnPortEvent(PortIOEventArgs e) {
            if (PortEvent != null)
                PortEvent(this, e);
        }

        protected virtual void OnStateChangeEvent(StateChangeEventArgs e) {
            if (StateChangeEvent != null)
                StateChangeEvent(this, e);
        }

        protected virtual void OnPopStackEvent(int addr) {
            //if (callStackList.Count > 0)
            //    callStackList.RemoveAt(0);
            //if (PopStackEvent != null)
            //    PopStackEvent(this, addr);
        }

        protected virtual void OnPushStackEvent(int addr, int val) {
            //callStackList.Insert(0, new CallStack(addr, val));
            //if (PushStackEvent != null)
            //    PushStackEvent(this, addr);
        }
        #endregion

        private IntPtr mainHandle;


        protected int[] keyLine = { 255, 255, 255, 255, 255, 255, 255, 255 };

        public int[] AttrColors = new int[16];

        /// <summary>
        /// The regular speccy palette
        /// </summary>
        public int[] NormalColors = {
                                             0x000000,            // Blacks
                                             0x0000C0,            // Red
                                             0xC00000,            // Blue
                                             0xC000C0,            // Magenta
                                             0x00C000,            // Green
                                             0x00C0C0,            // Yellow
                                             0xC0C000,            // Cyan
                                             0xC0C0C0,            // White
                                             0x000000,            // Bright Black
                                             0x0000F0,            // Bright Red
                                             0xF00000,            // Bright Blue
                                             0xF000F0,            // Bright Magenta
                                             0x00F000,            // Bright Green
                                             0x00F0F0,            // Bright Yellow
                                             0xF0F000,            // Bright Cyan
                                             0xF0F0F0             // Bright White
                                    };

        /// <summary>
        /// The ULA+ Palette
        /// </summary>
        // 4 x 16 array = 64 colours
        // Following values taken from generic colour palette from ULA plus site
        public int[] ULAPlusColours = new int[64] { 0x000000, 0x404040, 0xff0000,0xff6a00,0xffd800,0xb6ff00,0x4cff00,0x00ff21,
                                                    0x00ff90,0x00ffff,0x0094ff,0x0026ff,0x4800ff,0xb200ff,0xff00dc,0xff006e,
                                                    0xffffff,0x808080,0x7f0000,0x7f3300,0x7f6a00,0x5b7f00,0x267f00,0x007f0e,
                                                    0x007f46,0x007f7f,0x004a7f,0x00137f,0x21007f,0x57007f,0x7f006e,0x7f0037,
                                                    0xa0a0a0,0x303030,0xff7f7f,0xffb27f,0xffe97f,0xdaff7f,0xa5ff7f,0x7fff8e,
                                                    0x7fffc5,0x7fffff,0x7fc9ff,0x7f92ff,0xa17fff,0xd67fff,0xff7fed,0xff7fb6,
                                                    0xc0c0c0,0x606060,0x7f3f3f,0x7f593f,0x7f743f,0x6d7f3f,0x527f3f,0x3f7f47,
                                                    0x3f7f62,0x3f7f7f,0x3f647f,0x3f497f,0x503f7f,0x6b3f7f,0x7f3f76,0x7f3f5b
                                                  };


        //ULA Plus support
        public bool ULAPlusEnabled = false;
        protected int ULAGroupMode = 0; //0 = palette group, 1 = mode group
        protected int ULAPaletteGroup = 0;
        public bool ULAPaletteEnabled = false;

        //Misc variables
        protected int opcode = 0;
        protected int val, addr;
        public bool isROMprotected = true;  //not really used ATM
        public bool needsPaint = false;     //Raised when the ULA has finished painting the entire screen
        protected bool CapsLockOn = false;

        //Sound
        public const short MIN_SOUND_VOL = 0;
        public const short MAX_SOUND_VOL = short.MaxValue / 2;
        private short[] soundSamples = new short[882 * 2]; //882 samples, 2 channels, 2 bytes per channel (short)
        public ZeroSound.SoundManager beeper;
        public const bool ENABLE_SOUND = false;
        protected int averagedSound = 0;
        protected short soundCounter = 0;
        protected int lastSoundOut = 0;
        public short soundOut = 0;
        protected int soundTStatesToSample = 79;
        private float soundVolume = 0f;        //cached reference used when beeper instance is recreated.
        private short soundSampleCounter = 0;

        //Threading stuff (not used)
        public bool doRun = true;           //z80 executes only when true. Mainly for debugging purpose.

        //Important ports
        protected int lastFEOut = 0;        //The ULA Port
        protected int last7ffdOut = 0;      //Paging port on 128k/+2/+3/Pentagon
        protected int last1ffdOut = 0;      //Paging + drive motor port on +3
        protected int lastAYPortOut = 0;    //AY sound port
        protected int lastULAPlusOut = 0;

        //Port 0xfe constants
        protected const int BORDER_BIT = 0x07;
        protected const int EAR_BIT = 0x10;
        protected const int MIC_BIT = 0x08;
        protected const int TAPE_BIT = 0x40;

        //Machine properties
        protected double clockSpeed;        //the CPU clock speed of the machine
        protected int TstatesPerScanline;   //total # tstates in one scanline
        protected int ScanLineWidth;        //total # pixels in one scanline
        protected int CharRows;             //total # chars in one PRINT row
        protected int CharCols;             //total # chars in one PRINT col
        protected int ScreenWidth;          //total # pixels in one display row
        protected int ScreenHeight;         //total # pixels in one display col
        protected int BorderTopHeight;      //total # pixels in top border
        protected int BorderBottomHeight;   //total # pixels in bottom border
        protected int BorderLeftWidth;      //total # pixels of width of left border
        protected int BorderRightWidth;     //total # pixels of width of right border
        protected int DisplayStart;         //memory address of display start
        protected int DisplayLength;        //total # bytes of display memory
        protected int AttributeStart;       //memory address of attribute start
        protected int AttributeLength;      //total # bytes of attribute memory
        protected bool ULASnowEffect = true;
        public bool Issue2Keyboard = false; //Only of use for 48k & 16k machines.
        public int LateTiming = 0;       //Some machines have late timings. This affects contention and has to be factored in.

        //Utility strings
        protected const string ROM_128_BAS = "128K BAS";
        protected const string ROM_48_BAS = "48K BAS";
        protected const string ROM_128_SYN = "128K Syn";
        protected const string ROM_PLUS3_DOS = "+3 DOS";
        protected const string ROM_TR_DOS = "TR DOS";

        //The monitor needs to know these states so are public
        public string BankInPage3 = "-----";
        public string BankInPage2 = "-----";
        public string BankInPage1 = "-----";
        public string BankInPage0 = ROM_48_BAS;
        public bool monitorIsRunning = false;

        //List of addresses in the call stack (not used ATM)
        /*
        public class CallStack
        {
            private int address;
            private int data;

            public int Stack {
                get { return address; }
                set { address = value; }
            }

            public int Data {
                get { return data; }
                set { data = value; }
            }

            public CallStack(int addr, int val) {
                address = addr; data = val;
            }
        }

        public BindingList<CallStack> callStackList = new BindingList<CallStack>();
        */
        
        //Paging
        protected bool lowROMis48K = true;
        protected bool trDosPagedIn = false ;//TR DOS is swapped in only if the lower ROM is 48k.
        protected bool special64KRAM = false;  //for +3
        public bool contendedBankPagedIn = false;
        public bool showShadowScreen = false;
        public bool pagingDisabled = false;    //on 128k, depends on bit 5 of the value output to port (whose 1st and 15th bits are reset)

        //The cpu needs access to this so are public
        public int InterruptPeriod;             //Number of t-states to hold down /INT
        public int FrameLength;                 //Number of t-states of in 1 frame before interrupt is fired.
        private byte FrameCount = 0;            //Used to keep tabs on tape play time out period.

        //Contention related stuff
        protected int contentionStartPeriod;              //t-state at which to start applying contention
        protected int contentionEndPeriod;                //t-state at which to end applying contention
        protected byte[] contentionTable;                  //tstate-memory contention delay mapping

        //Render related stuff
        public int[] ScreenBuffer;                        //buffer for the windows side rasterizer
        protected byte[] screen;                           //display memory (16384 for 48k)
        protected short[] attr;                           //attribute memory lookup (mapped 1:1 to screen for convenience)
        protected short[] tstateToDisp;                   //tstate-display mapping
        protected short[] floatingBusTable;               //table that stores tstate to screen/attr addresses values
        protected int lastTState;                         //tstate at which last render update took place
        protected int elapsedTStates;                     //tstates elapsed since last render update
        protected int ActualULAStart;                     //tstate of top left raster pixel
        protected int screenByteCtr;                      //offset into display memory based on current tstate
        protected int ULAByteCtr;                         //offset into current pixel of rasterizer
        protected int borderColour;                       //Used by the screen update routine to output border colour
        protected bool flashOn = false;

        //For floating bus implementation
        protected int lastPixelValue;                     //last 8-bit bitmap read from display memory
        protected int lastAttrValue;                      //last 8-bit attr val read from attribute memory
        protected int lastPixelValuePlusOne;              //last 8-bit bitmap read from display memory+1
        protected int lastAttrValuePlusOne;               //last 8-bit attr val read from attribute memory+1

        //For 4 bright levels ULA artifacting (gamma ramping). DOESN'T WORK!
        //protected bool pixelIsPaper = false;

        //These variables are used to create a screen display box (non border area).
        protected int TtateAtLeft, TstateWidth, TstateAtTop,
                      TstateHeight, TstateAtRight, TstateAtBottom;

        //16x8k flat RAM bank (because of issues with pointers in c#) + 1 dummy bank
        protected byte[][] RAMpage = new byte[16][]; //16 pages of 8192 bytes each

        //8x8k flat ROM bank
        protected byte[][] ROMpage = new byte[8][];

        //For writing to ROM space
        protected byte[][] JunkMemory = new byte[2][]; 

        //8 "pointers" to the pages
        //NOTE: In the case of +3, Pages 0 and 1 *can* point to a RAMpage. In other cases they point to a
        //ROMpage. To differentiate which is being pointed to, the +3 machine employs the specialMode boolean.
        protected byte[][] PageReadPointer = new byte[8][];
        protected byte[][] PageWritePointer = new byte[8][];

        //Tape edge detection variables
        public String tapeFilename = "";
        private int tape_detectionCount = 0;
        private int tape_PC = 0;
        private int tape_PCatLastIn = 0;
        private int tape_whichRegToCheck = 0;
        private int tape_regValue = 0;
        private bool tape_edgeDetectorRan = false;
        private int tape_tstatesSinceLastIn = 0;
        private int tape_tstatesStep, tape_diff;
        private int tape_A, tape_B, tape_C, tape_D, tape_E, tape_H, tape_L;
        public bool edgeLoadTapes = false;
        public bool flashLoadTapes = true;
        public bool tapeBitWasFlipped = false;
        public bool tapeBitFlipAck = false;
        public bool tape_AutoPlay = false;
        public bool tape_AutoStarted = false;
        public bool tape_readToPlay = false;
        private const int TAPE_TIMEOUT = 50;// 69888 * 10;
        private int tape_stopTimeOut = TAPE_TIMEOUT;
        private byte tape_FrameCount = 0;
        public int tapeTStates = 0;
        public uint edgeDuration = 0;
        public bool tapeIsPlaying = false;
        public int tapeBit = 0;

        //Tape loading
        public const int PULSE_LOW = 0;
        public const int PULSE_HIGH = 1;
        public int blockCounter = 0;
        public bool tapePresent = false;
        public bool isPlaying = false;
        private int pulseCounter = 0;
        private int repeatCount = 0;
        private int bitCounter = 0;
        private int dataCounter = 0;
        private byte dataByte = 0;
        private int currentBit = 0;
        private int pulse = 0;
        private bool isPauseBlockPreproccess = false; //To ensure previous edge is finished correctly
        public PZXLoader.Block currentBlock;

        //AY support
        protected bool ayIsAvailable = true;
        protected AYSound aySound = new AYSound();
        protected int ayTStates = 0;
        protected const int AY_SAMPLE_RATE = 16;

        //Handy enum for various keys
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

        public enum JoysticksEmulated
        {
            NONE,
            KEMPSTON,
            SINCLAIR1,
            SINCLAIR2,
            CURSOR,
            LAST
        };

        //This holds the key lines used by the speccy for input
        public bool[] keyBuffer;

        public bool externalSingleStep = false;

        public int joystickType = 0; //A bit field of above joysticks to emulate (usually not more than 2).

        ////Each joystickState corresponds to an emulated joystick
        //Bits: 0 = button 3, 1 = button 2, 3 = button 1, 4 = up, 5 = down, 6 = left, 7 = right
        public int[] joystickState = new int[(int)JoysticksEmulated.LAST];

        public const byte JOYSTICK_MOVE_RIGHT = 0x1;
        public const byte JOYSTICK_MOVE_LEFT = 0x2;
        public const byte JOYSTICK_MOVE_DOWN = 0x4;
        public const byte JOYSTICK_MOVE_UP = 0x8;
        public const byte JOYSTICK_BUTTON_1 = 0x10;
        public const byte JOYSTICK_BUTTON_2 = 0x20;
        public const byte JOYSTICK_BUTTON_3 = 0x40;
        public const byte JOYSTICK_BUTTON_4 = 0x80;

        public bool HasKempstonJoystick {
            get;
            set;
        }

        //Mouse state
        public byte MouseX {
            get;
            set;
        }

        public byte MouseY {
            get;
            set;
        }

        //Mouse buttons are stored as a bitfield.
        //Bit 0 = Button 1 (right), Bit 1 = Button 2 (left), etc.
        public byte MouseButton {
            get;
            set;
        }

        public bool HasKempstonMouse {
            get;
            set;
        }

        //RZX Playback & Recording
        protected class RollbackBookmark
        {
            public SZXLoader snapshot;
            public int frameIndex;
        };

        public int rzxFrameCount;
        protected int rzxFetchCount;
        protected int rzxInputCount;
        protected byte rzxIN;
        public RZXLoader rzx;
        protected RZXLoader.RZX_Frame rzxFrame;
        protected System.Collections.Generic.List<RollbackBookmark> rzxBookmarks = new System.Collections.Generic.List<RollbackBookmark>();
        protected System.Collections.Generic.List<byte> rzxInputs = new System.Collections.Generic.List<byte>();
        public bool isPlayingRZX = false;
        public bool isRecordingRZX = false;
        protected int rzxCurrentBookmark = 0;

        //Disk related stuff
        protected int diskDriveState = 0;

        //Thread related stuff (not used ATM)
        private System.Threading.Thread emulationThread;
        public bool isSuspended = true;
        public System.Object lockThis = new System.Object(); //used to synchronise emulation with methods that change emulation state
        public System.Object lockThis2 = new System.Object(); //used by monitor/emulation

        public MachineModel model;
        private int emulationSpeed;
        public bool isResetOver = false;
        private const int MAX_CPU_SPEED = 500;

        //How long should we wait after speccy reset before signalling that it's safe to assume so.
        private int resetFrameTarget = 0;
        private int resetFrameCounter = 0;

        public void Start() {
            doRun = true;
            return;//THREAD
            isSuspended = false;
            if (!emulationThread.IsAlive) {
                emulationThread = new System.Threading.Thread(new System.Threading.ThreadStart(Run));
                emulationThread.Name = @"Emulation Thread";
                emulationThread.Priority = System.Threading.ThreadPriority.AboveNormal;
            }
            emulationThread.Start();
        }

        public void Pause() {
            //beeper.Stop();
            return; //THREAD
            if (isSuspended) // || !doRun)
                return;
            isSuspended = true;
            doRun = false;
            emulationThread.Join();

            //emulationThread.Suspend();
        }

        public void Resume() {
            //beeper.Play();
            return;//THREAD
            if (!isSuspended)// || doRun)
                return;
            doRun = true;
            isSuspended = false;
            if (!emulationThread.IsAlive) {
                emulationThread = new System.Threading.Thread(new System.Threading.ThreadStart(Run));
                emulationThread.Name = @"Emulation Thread";
                emulationThread.Priority = System.Threading.ThreadPriority.AboveNormal;
            }
            if (emulationThread.ThreadState != System.Threading.ThreadState.WaitSleepJoin)
                emulationThread.Start();
        }

        public void SetEmulationSpeed(int speed) {
            if (speed == 0) {
                speed = MAX_CPU_SPEED;
                soundTStatesToSample = 79;
                beeper.SetVolume(2.0f);
            } else {
                beeper.SetVolume(soundVolume);
                soundTStatesToSample = (int)((FrameLength * (50.0f * (speed / 100.0f))) / 44100.0f);
            }

            emulationSpeed = (speed - 100) / 100; //0 = normal.
        }

        public virtual int GetTotalScreenWidth() {
            return ScreenWidth + BorderLeftWidth + BorderRightWidth;
        }

        public virtual int GetTotalScreenHeight() {
            return ScreenHeight + BorderTopHeight + BorderBottomHeight;
        }

        //The display offset of the speccy screen wrt to emulator window in horizontal direction.
        //Useful for "centering" unorthodox screen dimensions like the Pentagon that has different left & right border width.
        public virtual int GetOriginOffsetX() {
            return 0;
        }

        //The display offset of the speccy screen wrt to emulator window in vertical direction.
        //Useful for "centering" unorthodox screen dimensions like the Pentagon that has different top & bottom border height.
        public virtual int GetOriginOffsetY() {
            return 0;
        }

        public void PlaybackRZX(RZXLoader _rzx) {
            isRecordingRZX = false;
            rzx = _rzx;
            rzxFrameCount = 0;
            rzxFetchCount = 0;
            rzxInputCount = 0;
            rzxFrame = rzx.frames[0];
            isPlayingRZX = true;
            totalTStates = (int)rzx.record.tstatesAtStart;
        }

        public void RecordRZX(RZXLoader _rzx) {
            isRecordingRZX = true;
            isPlayingRZX = false;
            rzx = _rzx;
            rzxInputs = new System.Collections.Generic.List<byte>();
            rzxFrameCount = 0;
            rzxFetchCount = 0;
            rzxInputCount = 0;
        }

        public void InsertBookmark() {
            if (isRecordingRZX) {
                rzxFrame = new RZXLoader.RZX_Frame();
                rzxFrame.inputCount = (ushort)rzxInputs.Count;
                rzxFrame.instructionCount = (ushort)rzxFetchCount;
                rzxFrame.inputs = rzxInputs.ToArray();
                rzx.frames.Add(rzxFrame);
                rzxFetchCount = 0;
                rzxInputCount = 0;
                rzxInputs = new System.Collections.Generic.List<byte>();
                RollbackBookmark bookmark = new RollbackBookmark();
                bookmark.frameIndex = rzx.frames.Count;
                bookmark.snapshot = CreateSZX();
                rzxBookmarks.Add(bookmark);
                rzxCurrentBookmark = rzxBookmarks.Count - 1;
            }
        }

        public void RollbackRZX() {
            if (rzxBookmarks.Count > 0) {
                RollbackBookmark bookmark = rzxBookmarks[rzxCurrentBookmark];
                //if less than 2 seconds have passed since last bookmark, revert to an even earlier bookmark
                if ((rzx.frames.Count - bookmark.frameIndex) / 50 < 2) {
                    if (rzxCurrentBookmark > 0) {
                        rzxBookmarks.Remove(bookmark);
                        rzxCurrentBookmark--;
                    }
                    bookmark = rzxBookmarks[rzxCurrentBookmark];
                }
                UseSZX(bookmark.snapshot);
                rzx.frames.RemoveRange(bookmark.frameIndex, rzx.frames.Count - bookmark.frameIndex);
                rzxFetchCount = 0;
                rzxInputCount = 0;
                rzxInputs = new System.Collections.Generic.List<byte>();
            }
        }

        public void DiscardRZX() {
            isRecordingRZX = false;
            isPlayingRZX = false;
            rzxBookmarks.Clear();
            rzxInputs.Clear();
        }

        public void SaveRZX(string filename, bool doFinalise) {
            if (isRecordingRZX) {
                if (doFinalise) {
                    if (rzx.snapshotData.Length > 1)
                        rzx.snapshotData[1] = null;
                } else
                    rzx.snapshotData[1] = CreateSZX().GetSZXData();

                isPlayingRZX = false;
                isRecordingRZX = false;
                rzxBookmarks.Clear();
                rzx.SaveRZX(filename, doFinalise);
            }
        }

        public void StartRecordingRZX() {
            isPlayingRZX = false;

            rzx = new RZXLoader();
            rzx.record.tstatesAtStart = (uint)totalTStates;
            rzx.record.flags |= 0x2; //Frames are compressed.
            rzx.snapshotData[0] = CreateSZX().GetSZXData();
            isRecordingRZX = true;
        }

        public bool ContinueRecordingRZX() {
            if (rzx.snapshotData[1] == null)
                return false;
            isPlayingRZX = false;
            isRecordingRZX = true;
            return true;
        }

        public virtual void DiskInsert(string filename, byte _unit) {
            diskDriveState |= (1 << _unit);
            OnDiskEvent(new DiskEventArgs(diskDriveState));
        }

        public virtual void DiskEject(byte _unit) {
            diskDriveState &= ~(1 << _unit);
            OnDiskEvent(new DiskEventArgs(diskDriveState));
        }

        public bool DiskInserted(byte _unit) {
            if ((diskDriveState & (1 << _unit)) != 0)
                return true;

            return false;
        }

        public zxmachine(IntPtr handle, bool lateTimingModel) {
            mainHandle = handle;
            JunkMemory[0] = new byte[8192];
            JunkMemory[1] = new byte[8192];
            ROMpage[0] = new byte[8192];
            ROMpage[1] = new byte[8192];
            ROMpage[2] = new byte[8192];
            ROMpage[3] = new byte[8192];
            ROMpage[4] = new byte[8192];  //4, 5, 6 and 7
            ROMpage[5] = new byte[8192];  //are used by the +3 and +2A models.
            ROMpage[6] = new byte[8192];
            ROMpage[7] = new byte[8192];
            RAMpage[0] = new byte[8192];  //Bank 0
            RAMpage[1] = new byte[8192];  //Bank 0
            RAMpage[2] = new byte[8192];  //Bank 1
            RAMpage[3] = new byte[8192];  //Bank 1
            RAMpage[4] = new byte[8192];  //Bank 2
            RAMpage[5] = new byte[8192];  //Bank 2
            RAMpage[6] = new byte[8192];  //Bank 3
            RAMpage[7] = new byte[8192];  //Bank 3
            RAMpage[8] = new byte[8192];  //Bank 4
            RAMpage[9] = new byte[8192];  //Bank 4
            RAMpage[10] = new byte[8192]; //Bank 5
            RAMpage[11] = new byte[8192]; //Bank 5
            RAMpage[12] = new byte[8192]; //Bank 6
            RAMpage[13] = new byte[8192]; //Bank 6
            RAMpage[14] = new byte[8192]; //Bank 7
            RAMpage[15] = new byte[8192]; //Bank 7
            AttrColors = NormalColors;
            LateTiming = (lateTimingModel ? 1 : 0);
            tapeBitWasFlipped = false;

            //THREAD
            //lock (lockThis)
            {
                beeper = new ZeroSound.SoundManager(handle, 16, 2, 44100);
                beeper.Play();
            }

            //THREAD
            //emulationThread = new System.Threading.Thread(new System.Threading.ThreadStart(Run));
            //emulationThread.Name = @"Emulation Thread";
            //emulationThread.Priority = System.Threading.ThreadPriority.AboveNormal;

            //During warm start, all registers are set to 0xffff
            //http://worldofspectrum.org/forums/showthread.php?t=34574&page=3
            interruptMode = 0;
            I = 0;
            R = 0;
            _R = 0;
            MemPtr = 0;

            PC = 0;
            SP = 0xffff;
            IY = 0xffff;
            IX = 0xffff;

            AF = 0xffff;
            BC = 0xffff;
            DE = 0xffff;
            HL = 0xffff;

            exx();
            ex_af_af();

            AF = 0xffff;
            BC = 0xffff;
            DE = 0xffff;
            HL = 0xffff;
        }

        public void ResetTapeEdgeDetector()
        {
            tape_detectionCount = 0;
            tape_PC = 0;
            tape_PCatLastIn = 0;
            tape_whichRegToCheck = 0;
            tape_regValue = 0;
            tape_edgeDetectorRan = false;
            tape_tstatesSinceLastIn = 0;
            tapeBitWasFlipped = false;
            tapeBitFlipAck = false;

            tape_readToPlay = false;
            tape_FrameCount = 0;
            tapeTStates = 0;
            edgeDuration = 0;
            tapeIsPlaying = false;
            tapeBit = 0;

            blockCounter = 0;
            isPlaying = false;
            pulseCounter = 0;
            repeatCount = 0;
            bitCounter = 0;
            dataCounter = 0;
            dataByte = 0;
            currentBit = 0;
            pulse = 0;
        }

        protected void FlashLoadTape() {
            if (!flashLoadTapes)
                return;

            //if (TapeEvent != null)
            //    OnTapeEvent(new TapeEventArgs(TapeEventType.FLASH_LOAD));
            DoTapeEvent(new TapeEventArgs(TapeEventType.FLASH_LOAD));
        }
        //Reads next instruction from address pointed to by PC
        public int FetchInstruction() {
            R++;
            if (isPlayingRZX || isRecordingRZX) {
                rzxFetchCount++;
            }
            int b = GetOpcode(PC);
            PC = (PC + 1) & 0xffff;
            totalTStates++; //effectively, totalTStates + 4 because PeekByte does the other 3
            return b;
        }

        //Returns the actual memory address of a page
        public int GetPageAddress(int page) {
            return 8192 * page;
        }

        //Returns the memory data at a page
        public byte[] GetPageData(int page) {
            return RAMpage[page * 2];
        }

        //Resets the speccy
        public virtual void Reset(bool coldBoot) {
            isResetOver = false;
            isPlayingRZX = false;
            isRecordingRZX = false;

            DoTapeEvent(new TapeEventArgs(TapeEventType.STOP_TAPE));
           
            //All registers are set to 0xffff during a cold boot
            //http://worldofspectrum.org/forums/showthread.php?t=34574&page=3
            if (coldBoot)
            {
                SP = 0xffff;
                IY = 0xffff;
                IX = 0xffff;

                AF = 0xffff;
                BC = 0xffff;
                DE = 0xffff;
                HL = 0xffff;

                exx();
                ex_af_af();

                AF = 0xffff;
                BC = 0xffff;
                DE = 0xffff;
                HL = 0xffff;
            }

            PC = 0;
            interruptMode = 0;
            I = 0;
            R = 0;
            _R = 0;
            MemPtr = 0;

            tapeBitWasFlipped = false;
            resetOver = false;
            totalTStates = 0;
            timeToOutSound = 0;
            ULAPaletteEnabled = false;
            HaltOn = false;
            runningInterrupt = false;
            lastOpcodeWasEI = 0;
            ULAByteCtr = 0;
            last1ffdOut = 0;
            last7ffdOut = 0;
            lastAYPortOut = 0;
            lastFEOut = 0;
            lastULAPlusOut = 0;
            lastTState = 0;
            elapsedTStates = 0;
            tstates = 0;
            frameCount = 0;
            IFF1 = false;
            IFF2 = false;
            isPlaying = false;
            pulseCounter = 0;
            repeatCount = 0;
            bitCounter = 0;
            dataCounter = 0;
            dataByte = 0;
            currentBit = 0;
            currentBlock = null;
            pulse = 0;
            isPauseBlockPreproccess = false; //To ensure previous edge is finished correctly
            ////////////////////////////

            averagedSound = 0;
            aySound = new AYSound(); //aySound.reset doesn't work so well...
            flashOn = false;

            //We jiggle the wait period after resetting so that FRAMES/RANDOMIZE works randomly enough on the speccy.
            resetFrameTarget = new Random().Next(40, 90);
        }

        //Updates the tape state
        public void UpdateTapeState(int tstates) {
            if (tapeIsPlaying && !tape_edgeDetectorRan) {
                tapeTStates += tstates;
                while (tapeTStates >= edgeDuration) 
                {
                    tapeTStates = (int)(tapeTStates - edgeDuration);
                    DoTapeEvent(new TapeEventArgs(TapeEventType.EDGE_LOAD));
                }
            }
            tape_edgeDetectorRan = false;
        }

        //Shutsdown the speccy
        public virtual void Shutdown() {
            //THREAD
            //if (!isSuspended)
            //{
            //    doRun = false;
            //    emulationThread.Join();
            //    emulationThread = null;
            // }
            lock (lockThis) {
                beeper.Shutdown();
                beeper = null;
                contentionTable = null;
                floatingBusTable = null;
                ScreenBuffer = null;
                screen = null;
                attr = null;
                tstateToDisp = null;
                RAMpage = null;
                ROMpage = null;
                PageReadPointer = null;
                PageWritePointer = null;
                keyBuffer = null;
                isRecordingRZX = false;
                isPlayingRZX = false;
            }
        }

        //Re-engineered SpecEmu version. Works a treat!
        private void CheckEdgeLoader2() {
            //Return if not tape is inserted in Tape Deck
            if (!tape_readToPlay)
                return;

            if (tapeIsPlaying) {
                if (PC == tape_PCatLastIn) {
                    if (tape_AutoPlay) {
                        tape_stopTimeOut = TAPE_TIMEOUT;
                        tape_AutoStarted = true;
                    }

                    if (edgeLoadTapes) {
                        if (tapeBitWasFlipped) {
                            tapeBitFlipAck = true;
                            tapeBitWasFlipped = false;
                        } else {
                            //bool doLoop = false;
                            switch (tape_whichRegToCheck) {
                                case 1:
                                    tape_regValue = A;
                                    break;

                                case 2:
                                    tape_regValue = B;
                                    break;

                                case 3:
                                    tape_regValue = C;
                                    break;

                                case 4:
                                    tape_regValue = D;
                                    break;

                                case 5:
                                    tape_regValue = E;
                                    break;

                                case 6:
                                    tape_regValue = H;
                                    break;

                                case 7:
                                    tape_regValue = L;
                                    break;

                                default:
                                    //doLoop = false;
                                    return;
                            }
                            tapeTStates += (totalTStates - oldTStates);
                            tape_edgeDetectorRan = true;
                            while (!((tape_regValue == 255) || (tape_regValue == 1))) {

                                if (tapeTStates >= edgeDuration) {
                                    tapeTStates = (int)(tapeTStates - edgeDuration);

                                    DoTapeEvent(new TapeEventArgs(TapeEventType.EDGE_LOAD));
                                }

                                if (tapeBitWasFlipped) {
                                    tapeBitFlipAck = true;
                                    break;
                                }
                                tapeTStates += tape_tstatesStep;
                                switch (tape_whichRegToCheck) {
                                    case 1:
                                        A += tape_diff;
                                        tape_regValue = A;
                                        break;

                                    case 2:
                                        B += tape_diff;
                                        tape_regValue = B;
                                        break;

                                    case 3:
                                        C += tape_diff;
                                        tape_regValue = C;
                                        break;

                                    case 4:
                                        D += tape_diff;
                                        tape_regValue = D;
                                        break;

                                    case 5:
                                        E += tape_diff;
                                        tape_regValue = E;
                                        break;

                                    case 6:
                                        H += tape_diff;
                                        tape_regValue = H;
                                        break;

                                    case 7:
                                        L += tape_diff;
                                        tape_regValue = L;
                                        break;

                                    default:
                                        //doLoop = false;
                                        break;
                                }
                            }
                        }
                    }
                    tape_tstatesSinceLastIn = totalTStates;
                }
            } else {
                if (FrameCount != tape_FrameCount)
                    tape_detectionCount = 0;

                int elapsedTapeTstates = totalTStates - tape_tstatesSinceLastIn;
                if (((elapsedTapeTstates > 0) && (elapsedTapeTstates < 96)) && (PC == tape_PC)) {
                    tape_tstatesStep = elapsedTapeTstates;
                    //which reg has changes since last IN
                    int numRegsThatHaveChanged = 0;
                    tape_diff = 0;

                    if (tape_A != A) {
                        tape_regValue = A;
                        tape_whichRegToCheck = 1;
                        tape_diff = tape_A - A;
                        numRegsThatHaveChanged++;
                    }
                    if (tape_B != B) {
                        tape_regValue = B;
                        tape_whichRegToCheck = 2;
                        tape_diff = tape_B - B;
                        numRegsThatHaveChanged++;
                    }
                    if (tape_C != C) {
                        tape_regValue = C;
                        tape_whichRegToCheck = 3;
                        tape_diff = tape_C - C;
                        numRegsThatHaveChanged++;
                    }
                    if (tape_D != D) {
                        tape_regValue = D;
                        tape_whichRegToCheck = 4;
                        tape_diff = tape_D - D;
                        numRegsThatHaveChanged++;
                    }
                    if (tape_E != E) {
                        tape_regValue = E;
                        tape_whichRegToCheck = 5;
                        tape_diff = tape_E - E;
                        numRegsThatHaveChanged++;
                    }
                    if (tape_H != H) {
                        tape_regValue = H;
                        tape_whichRegToCheck = 6;
                        tape_diff = tape_H - H;
                        numRegsThatHaveChanged++;
                    }
                    if (tape_L != L) {
                        tape_regValue = L;
                        tape_whichRegToCheck = 7;
                        tape_diff = tape_L - L;
                        numRegsThatHaveChanged++;
                    }

                    tape_A = A;
                    tape_B = B;
                    tape_C = C;
                    tape_D = D;
                    tape_E = E;
                    tape_H = H;
                    tape_L = L;

                    tape_diff *= -1;
                    if (numRegsThatHaveChanged == 1) //Has only 1 reg changed?
                    {
                        if (Math.Abs(tape_diff) == 1)    //Changed only by +1 or -1
                        {
                            tape_detectionCount++;
                            if (tape_detectionCount >= 8) //Is the above true 8 times?
                            {
                                if (tape_AutoPlay) {
                                    tapeIsPlaying = true;
                                    tape_AutoStarted = true;
                                    tape_stopTimeOut = TAPE_TIMEOUT;
                                    //if (TapeEvent != null)
                                    //    OnTapeEvent(new TapeEventArgs(TapeEventType.START_TAPE)); //Yes! Start tape!
                                    DoTapeEvent(new TapeEventArgs(TapeEventType.START_TAPE));
                                    tapeTStates = 0;
                                }
                                tape_PCatLastIn = PC;
                            }
                        }
                    }
                }
                tape_tstatesSinceLastIn = totalTStates;
                tape_FrameCount = FrameCount;
                tape_A = A;
                tape_B = B;
                tape_C = C;
                tape_D = D;
                tape_E = E;
                tape_H = H;
                tape_L = L;
                tape_PC = PC;
            }
        }

        //The main loop which executes opcodes repeatedly till 1 frame (69888 tstates)
        //has been generated.
        private int NO_PAINT_REP = 100;

        public void Run() {
            for (int rep = 0; rep < (tapeIsPlaying && edgeLoadTapes ? NO_PAINT_REP : 1); rep++)
            {
                while (doRun)
                {
                    //Raise event for debugger
                    if (OpcodeExecutedEvent != null)
                    {
                        // lock (lockThis2)
                        {
                            //monitorIsRunning = true;
                            OnOpcodeExecutedEvent();
                            //   while (monitorIsRunning)
                            //       System.Threading.Monitor.Wait(lockThis2);
                        }
                    }

                    lock (lockThis)
                    {
                        #region Tape Save Trap
                        //Tape Save trap
                        if (PC == 0x04d1)
                        {
                            //Trap the tape only if lower ROM is 48k!
                            if (lowROMis48K)
                            {
                                if (TapeEvent != null)
                                    OnTapeEvent(new TapeEventArgs(TapeEventType.SAVE_TAP));
                                IX = IX + DE;
                                DE = 0;
                                PC = 1342;
                                ResetKeyboard();
                            }
                        }
                        #endregion Tape Deck events

                        if (isPlayingRZX && doRun)
                            ProcessRZX();
                        else
                            if (doRun)
                                Process();

                    } //lock

                    if (needsPaint)
                    {
                        #region Tape Deck event for stopping tape on tape play timeout

                        if (tapeIsPlaying)
                        {
                            if (tape_AutoPlay && tape_AutoStarted)
                            {
                                if (!(isPauseBlockPreproccess && (edgeDuration > 0) && and_32_Or_64))
                                {
                                    if (tape_stopTimeOut <= 0)
                                    {
                                        // if (TapeEvent != null)
                                        //     OnTapeEvent(new TapeEventArgs(TapeEventType.STOP_TAPE)); //stop the tape!
                                        DoTapeEvent(new TapeEventArgs(TapeEventType.STOP_TAPE));
                                        tape_AutoStarted = false;
                                    }
                                    else
                                        tape_stopTimeOut--;
                                }
                            }
                        }

                        #endregion Tape Deck event for stopping tape on tape play timeout
                        FrameCount++;
                        if (FrameCount > 50)
                        {
                            FrameCount = 0;
                        }

                        if (!externalSingleStep)
                        {
                            while (!beeper.FinishedPlaying() && !tapeIsPlaying)
                                System.Threading.Thread.Sleep(1);
                        }

                        if (tapeIsPlaying && rep != NO_PAINT_REP - 1)
                        {
                            needsPaint = false;
                            System.Threading.Thread.Sleep(0); //TO DO: Remove?
                        }
                        break;
                    }

                    if (externalSingleStep)
                        break;
                } //run loop
                
            }
        }

        //In r, (C)
        public int In() {
            int result = In(BC);
            /*
            SetNeg(false);
            SetParity(parity[result]);
            SetSign((result & F_SIGN) != 0);
            SetZero(result == 0);
            SetHalf(false);
            SetF3((result & F_3) != 0);
            SetF5((result & F_5) != 0);

            return result;*/
            F = ( F & F_CARRY) | sz53p[result];
            return result;
        }

        //Sets the sound volume of the beeper/ay
        public void SetSoundVolume(float vol) {
            soundVolume = vol;
            beeper.SetVolume(vol);
        }

        //Turns off the sound
        public void MuteSound(bool isMute) {
            if (isMute)
                beeper.SetVolume(0.0f);
            else
                beeper.SetVolume(soundVolume);
        }

        //Turns on the sound
        public void ResumeSound() {
            beeper.Play();
        }

        //Same as PeekByte, except specifically for opcode fetches in order to
        //trigger Memory Execute in debugger.
        public byte GetOpcode(int addr) {
            addr &= 0xffff;
            //Contend(addr, 3, 1);
            if (IsContended(addr)) {
                totalTStates += contentionTable[totalTStates];
            }

            totalTStates += 3;

            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;
            byte _b = PageReadPointer[page][offset];

            //This call flags a memory change event for the debugger
            if (MemoryExecuteEvent != null)
                OnMemoryExecuteEvent(new MemoryEventArgs(addr, _b));

            return _b;
        }

        //Returns the byte at a given 16 bit address (can be contended)
        public byte PeekByte(int addr) {
            addr &= 0xffff;
            //Contend(addr, 3, 1);
            if (IsContended(addr)) {
                totalTStates += contentionTable[totalTStates];
            }

            totalTStates += 3;

            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;
            byte _b = PageReadPointer[page][offset];

            //This call flags a memory change event for the debugger
            if (MemoryReadEvent != null)
                OnMemoryReadEvent(new MemoryEventArgs(addr, _b));

            return _b;
        }

        //Returns the byte at a given 16 bit address (can be contended)
        public virtual void PokeByte(int addr, int _b) {
            addr &= 0xffff;
            byte b = (byte)(_b & 0xff);

            //This call flags a memory change event for the debugger
            if (MemoryWriteEvent != null)
                OnMemoryWriteEvent(new MemoryEventArgs(addr, _b & 0xff));

            if (IsContended(addr)) {
                totalTStates += contentionTable[totalTStates];
            }
            totalTStates += 3;
            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;

            if (((addr & 49152) == 16384) && (PageReadPointer[page][offset] != b)) {
                UpdateScreenBuffer(totalTStates);
            }

            PageWritePointer[page][offset] = b;
        }

        //Returns the byte at a given 16 bit address with no contention
        public byte PeekByteNoContend(int addr) {
            addr &= 0xffff;
            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;
            byte _b = PageReadPointer[page][offset];
            return _b;
        }

        //Returns a word at a given 16 bit address with no contention
        public int PeekWordNoContend(int addr) {
            return (PeekByteNoContend(addr) + (PeekByteNoContend(addr + 1) << 8));
        }

        //Pokes a 16 bit value at given address. Contention applies.
        public void PokeWord(int addr, int w) {
            PokeByte(addr, w);
            PokeByte((addr + 1), w >> 8);
        }

        //Returns a 16 bit value from given address. Contention applies.
        public int PeekWord(int addr) {
            return (PeekByte(addr)) | (PeekByte(addr + 1) << 8);
        }

        //The stack is pushed in low byte, high byte form
        public void PushStack(int val) {
            SP = (SP - 2) & 0xffff;
            PokeByte((SP + 1) & 0xffff, val >> 8);
            PokeByte(SP, val & 0xff);
            //if (PushStackEvent != null)
            //    OnPushStackEvent(SP, val);
        }

        public int PopStack() {
            int val = (PeekByte(SP)) | (PeekByte(SP + 1) << 8);
            SP = (SP + 2) & 0xffff;
            //if (PopStackEvent != null)
            //    OnPopStackEvent(val);
            return val;
        }

        //Pokes a byte at a specific bank and offset
        //The offset input can be upto 16384, the bank and offset are adjusted
        //automatically.
        public void PokeRAMPage(int bank, int offset, byte val) {
            int indx = offset / 8192;
            RAMpage[bank * 2 + indx][offset % 8192] = val;
        }

        //Returns the byte at a given 16 bit address with no contention
        public void PokeByteNoContend(int addr, int b) {
            addr &= 0xffff;
            b &= 0xff;

            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;

            // if (page < 2 && isROMprotected && !special64KRAM)
            //     return;

            PageWritePointer[page][offset] = (byte)b;
        }

        //Returns a value from a port (can be contended)
        public virtual int In(int port) {
            //Raise a port I/O event
            if (PortEvent != null)
                OnPortEvent(new PortIOEventArgs(port, 0, false));

            return 0;
        }

        //Used purely to raise an event with the debugger for IN with a specific value
        public virtual void In(int port, int val) {
            //Raise a port I/O event
            if (PortEvent != null)
                OnPortEvent(new PortIOEventArgs(port, val, false));

            if (isRecordingRZX) {
                rzxInputs.Add((byte)val);
            }
        }

        //Outputs a value to a port (can be contended)
        //The base call is used only to raise memory events
        public virtual void Out(int port, int val) {
            //Raise a port I/O event
            if (PortEvent != null)
                OnPortEvent(new PortIOEventArgs(port, val, true));
        }

        //Updates the state of the renderer
        //This was abstracted earlier but the logic seems to work with all models, so...
        public virtual void UpdateScreenBuffer(int _tstates) {
            if (_tstates < ActualULAStart) {
                return;
            } else if (_tstates >= FrameLength) {
                _tstates = FrameLength - 1;
                //Since we're updating the entire screen here, we don't need to re-paint
                //it again in the  process loop on framelength overflow.

                needsPaint = true;
            }

            //the additional 1 tstate is required to get correct number of bytes to output in ircontention.sna
            elapsedTStates = (_tstates + 1 - lastTState);

            //It takes 4 tstates to write 1 byte. Or, 2 pixels per t-state.

            int numBytes = (elapsedTStates >> 2) + ((elapsedTStates % 4) > 0 ? 1 : 0);

            int pixelData;
            int pixel2Data = 0xff;
            int attrData;
            int attr2Data;
            int bright;
            int ink;
            int paper;
            int flash;

            for (int i = 0; i < numBytes; i++) {
                if (tstateToDisp[lastTState] > 1) {
                   // for (int p = 0; p < 2; p++) {
                        screenByteCtr = tstateToDisp[lastTState] - 16384; //adjust for actual screen offset
                  
                        pixelData = screen[screenByteCtr];
                        attrData = screen[attr[screenByteCtr] - 16384];

                        lastPixelValue = pixelData;
                        lastAttrValue = attrData;
                       /* if ((I & 0x40) == 0x40) {
                            {
                                if (p == 0) {
                                    screenByteCtr = (screenByteCtr + 16384) | (R & 0xff);
                                    pixel2Data = screenByteCtr & 0xff;
                                }
                                else
                                    screenByteCtr = (screenByteCtr + 16384) | (pixel2Data);
                                pixelData = screen[(screenByteCtr - 1) - 16384];
                                lastAttrValue = screen[attr[(screenByteCtr - 1) - 16384] - 16384];
                            }
                        }
                        */
                        bright = (attrData & 0x40) >> 3;
                        flash = (attrData & 0x80) >> 7;
                        ink = (attrData & 0x07);
                        paper = ((attrData >> 3) & 0x7);
                        int paletteInk = AttrColors[ink + bright];
                        int palettePaper = AttrColors[paper + bright];

                        if (flashOn && (flash != 0)) //swap paper and ink when flash is on
                        {
                            int temp = paletteInk;
                            paletteInk = palettePaper;
                            palettePaper = temp;
                        }

                        if (ULAPlusEnabled && ULAPaletteEnabled) {
                            paletteInk = ULAPlusColours[(((flash << 1) + (bright >> 3)) << 4) + ink]; //(flash*2 + bright) * 16 + ink
                            palettePaper = ULAPlusColours[(((flash << 1) + (bright >> 3)) << 4) + paper + 8]; //(flash*2 + bright) * 16 + paper + 8
                        }

                        for (int a = 0; a < 8; ++a) {
                            if ((pixelData & 0x80) != 0) {
                                ScreenBuffer[ULAByteCtr++] = paletteInk;
                                lastAttrValue = ink;
                                //pixelIsPaper = false;
                            } else {
                                /* Gamma ramping
                                int p = palettePaper;
                                if (!pixelIsPaper)
                                {
                                    if (!((paper & 0x07) - (lastAttrValue & 0x7) < 0) && ((paper & 0x07) > 0))
                                        p = AttrColors[paper + 8];
                                }
                                ScreenBuffer[ULAByteCtr++] = p;
                                */
                                ScreenBuffer[ULAByteCtr++] = palettePaper;
                                lastAttrValue = paper;
                               // pixelIsPaper = true;
                            }
                            pixelData <<= 1;
                        }                 
                    // pixelData = lastPixelValue;
                } else if (tstateToDisp[lastTState] == 1) {
                    int bor;
                    if (ULAPlusEnabled && ULAPaletteEnabled) {
                        bor = ULAPlusColours[borderColour + 8];
                    } else
                        bor = AttrColors[borderColour];

                    for (int g = 0; g < 8; g++)
                        ScreenBuffer[ULAByteCtr++] = bor;
                }
                lastTState += 4;
            }
        }

        public unsafe void UpdateScreenBuffer2(int _tstates) {
            if (_tstates < ActualULAStart) {
                return;
            } else if (_tstates >= FrameLength) {
                _tstates = FrameLength - 1;
                //Since we're updating the entire screen here, we don't need to re-paint
                //it again in the  process loop on framelength overflow.
                needsPaint = true;
            }
            //the additional 1 tstate is required to get correct number of bytes to output in ircontention.sna
            elapsedTStates = (_tstates + 1 - lastTState);

            //It takes 4 tstates to write 1 byte.
            int numBytes = (elapsedTStates >> 2) + ((elapsedTStates % 4) > 0 ? 1 : 0);
            {
                int pixelData = 0;
                int attrData = 0;
                int bright = 0;
                int ink = 0;
                int paper = 0;
                bool flashBitOn = false;
                fixed (int* p = ScreenBuffer) {
                    for (int i = 0; i < numBytes; i++) {
                        if (tstateToDisp[lastTState] > 1) {
                            screenByteCtr = tstateToDisp[lastTState] - 16384; //adjust for actual screen offset

                            pixelData = screen[screenByteCtr];
                            attrData = screen[attr[screenByteCtr] - 16384];

                            lastPixelValue = pixelData;
                            lastAttrValue = attrData;
                            /*if ((I & 192) == 192)
                            {
                                if (screenByteCtr % 2 != 0)
                                {
                                    pixelData = screen[(screenByteCtr-1) - 16384];
                                    lastAttrValue = screen[attr[(screenByteCtr-1) - 16384] - 16384];
                                }
                            }
                            */
                            bright = (attrData & 0x40) >> 3;
                            flashBitOn = (attrData & 0x80) != 0;
                            ink = AttrColors[(attrData & 0x07) + bright];
                            paper = AttrColors[((attrData >> 3) & 0x7) + bright];

                            if (flashOn && flashBitOn) //swap paper and ink when flash is on
                            {
                                int temp = ink;
                                ink = paper;
                                paper = temp;
                            }

                            if (ULAPlusEnabled && ULAPaletteEnabled) {
                                ink = ULAPlusColours[((flashBitOn ? 1 : 0) << 1 + (bright != 0 ? 1 : 0)) << 4 + (attrData & 0x07)];
                                paper = ULAPlusColours[((flashBitOn ? 1 : 0) << 1 + (bright != 0 ? 1 : 0)) << 4 + ((attrData >> 3) & 0x7) + 8];
                            }

                            lock (this) {
                                for (int a = 0; a < 8; ++a) {
                                    if ((pixelData & 0x80) != 0) {
                                        *(p + ULAByteCtr) = ink;
                                    } else {
                                        *(p + ULAByteCtr) = paper;
                                    }
                                    pixelData <<= 1;
                                    ULAByteCtr++;
                                }
                            }
                        } else if (tstateToDisp[lastTState] == 1) {
                            int bor;
                            if (ULAPlusEnabled && ULAPaletteEnabled) {
                                bor = ULAPlusColours[borderColour];
                            } else
                                bor = AttrColors[borderColour];
                            lock (this) {
                                for (int g = 0; g < 8; g++) {
                                    *(p + ULAByteCtr) = bor;
                                    ULAByteCtr++;
                                }
                            }
                        }
                        lastTState += 4;
                    }
                }
            }
        }

        //Loads in the ROM for the machine
        public abstract bool LoadROM(string path, string filename);

        //Updates the state of all inputs from the user
        public void UpdateInput() {

            #region Row 0: fefe - CAPS SHIFT, Z, X, C , V

            if (keyBuffer[(int)keyCode.SHIFT]) {
                keyLine[0] = keyLine[0] & ~(0x1);
            } else {
                keyLine[0] = keyLine[0] | (0x1);
            }

            if (keyBuffer[(int)keyCode.Z]) {
                keyLine[0] = keyLine[0] & ~(0x02);
            } else {
                keyLine[0] = keyLine[0] | (0x02);
            }

            if (keyBuffer[(int)keyCode.X]) {
                keyLine[0] = keyLine[0] & ~(0x04);
            } else {
                keyLine[0] = keyLine[0] | (0x04);
            }

            if (keyBuffer[(int)keyCode.C]) {
                keyLine[0] = keyLine[0] & ~(0x08);
            } else {
                keyLine[0] = keyLine[0] | (0x08);
            }

            if (keyBuffer[(int)keyCode.V]) {
                keyLine[0] = keyLine[0] & ~(0x10);
            } else {
                keyLine[0] = keyLine[0] | (0x10);
            }

            #endregion Row 0: fefe - CAPS SHIFT, Z, X, C , V

            #region Row 1: fdfe - A, S, D, F, G

            if (keyBuffer[(int)keyCode.A]) {
                keyLine[1] = keyLine[1] & ~(0x1);
            } else {
                keyLine[1] = keyLine[1] | (0x1);
            }

            if (keyBuffer[(int)keyCode.S]) {
                keyLine[1] = keyLine[1] & ~(0x02);
            } else {
                keyLine[1] = keyLine[1] | (0x02);
            }

            if (keyBuffer[(int)keyCode.D]) {
                keyLine[1] = keyLine[1] & ~(0x04);
            } else {
                keyLine[1] = keyLine[1] | (0x04);
            }

            if (keyBuffer[(int)keyCode.F]) {
                keyLine[1] = keyLine[1] & ~(0x08);
            } else {
                keyLine[1] = keyLine[1] | (0x08);
            }

            if (keyBuffer[(int)keyCode.G]) {
                keyLine[1] = keyLine[1] & ~(0x10);
            } else {
                keyLine[1] = keyLine[1] | (0x10);
            }

            #endregion Row 1: fdfe - A, S, D, F, G

            #region Row 2: fbfe - Q, W, E, R, T

            if (keyBuffer[(int)keyCode.Q]) {
                keyLine[2] = keyLine[2] & ~(0x1);
            } else {
                keyLine[2] = keyLine[2] | (0x1);
            }

            if (keyBuffer[(int)keyCode.W]) {
                keyLine[2] = keyLine[2] & ~(0x02);
            } else {
                keyLine[2] = keyLine[2] | (0x02);
            }

            if (keyBuffer[(int)keyCode.E]) {
                keyLine[2] = keyLine[2] & ~(0x04);
            } else {
                keyLine[2] = keyLine[2] | (0x04);
            }

            if (keyBuffer[(int)keyCode.R]) {
                keyLine[2] = keyLine[2] & ~(0x08);
            } else {
                keyLine[2] = keyLine[2] | (0x08);
            }

            if (keyBuffer[(int)keyCode.T]) {
                keyLine[2] = keyLine[2] & ~(0x10);
            } else {
                keyLine[2] = keyLine[2] | (0x10);
            }

            #endregion Row 2: fbfe - Q, W, E, R, T

            #region Row 3: f7fe - 1, 2, 3, 4, 5

            if (keyBuffer[(int)keyCode._1]) {
                keyLine[3] = keyLine[3] & ~(0x1);
            } else {
                keyLine[3] = keyLine[3] | (0x1);
            }

            if (keyBuffer[(int)keyCode._2]) {
                keyLine[3] = keyLine[3] & ~(0x02);
            } else {
                keyLine[3] = keyLine[3] | (0x02);
            }

            if (keyBuffer[(int)keyCode._3]) {
                keyLine[3] = keyLine[3] & ~(0x04);
            } else {
                keyLine[3] = keyLine[3] | (0x04);
            }

            if (keyBuffer[(int)keyCode._4]) {
                keyLine[3] = keyLine[3] & ~(0x08);
            } else {
                keyLine[3] = keyLine[3] | (0x08);
            }

            if (keyBuffer[(int)keyCode._5]) {
                keyLine[3] = keyLine[3] & ~(0x10);
            } else {
                keyLine[3] = keyLine[3] | (0x10);
            }

            #endregion Row 3: f7fe - 1, 2, 3, 4, 5

            #region Row 4: effe - 0, 9, 8, 7, 6

            if (keyBuffer[(int)keyCode._0]) {
                keyLine[4] = keyLine[4] & ~(0x1);
            } else {
                keyLine[4] = keyLine[4] | (0x1);
            }

            if (keyBuffer[(int)keyCode._9]) {
                keyLine[4] = keyLine[4] & ~(0x02);
            } else {
                keyLine[4] = keyLine[4] | (0x02);
            }

            if (keyBuffer[(int)keyCode._8]) {
                keyLine[4] = keyLine[4] & ~(0x04);
            } else {
                keyLine[4] = keyLine[4] | (0x04);
            }

            if (keyBuffer[(int)keyCode._7]) {
                keyLine[4] = keyLine[4] & ~(0x08);
            } else {
                keyLine[4] = keyLine[4] | (0x08);
            }

            if (keyBuffer[(int)keyCode._6]) {
                keyLine[4] = keyLine[4] & ~(0x10);
            } else {
                keyLine[4] = keyLine[4] | (0x10);
            }

            #endregion Row 4: effe - 0, 9, 8, 7, 6

            #region Row 5: dffe - P, O, I, U, Y

            if (keyBuffer[(int)keyCode.P]) {
                keyLine[5] = keyLine[5] & ~(0x1);
            } else {
                keyLine[5] = keyLine[5] | (0x1);
            }

            if (keyBuffer[(int)keyCode.O]) {
                keyLine[5] = keyLine[5] & ~(0x02);
            } else {
                keyLine[5] = keyLine[5] | (0x02);
            }

            if (keyBuffer[(int)keyCode.I]) {
                keyLine[5] = keyLine[5] & ~(0x04);
            } else {
                keyLine[5] = keyLine[5] | (0x04);
            }

            if (keyBuffer[(int)keyCode.U]) {
                keyLine[5] = keyLine[5] & ~(0x08);
            } else {
                keyLine[5] = keyLine[5] | (0x08);
            }

            if (keyBuffer[(int)keyCode.Y]) {
                keyLine[5] = keyLine[5] & ~(0x10);
            } else {
                keyLine[5] = keyLine[5] | (0x10);
            }

            #endregion Row 5: dffe - P, O, I, U, Y

            #region Row 6: bffe - ENTER, L, K, J, H

            if (keyBuffer[(int)keyCode.ENTER]) {
                keyLine[6] = keyLine[6] & ~(0x1);
            } else {
                keyLine[6] = keyLine[6] | (0x1);
            }

            if (keyBuffer[(int)keyCode.L]) {
                keyLine[6] = keyLine[6] & ~(0x02);
            } else {
                keyLine[6] = keyLine[6] | (0x02);
            }

            if (keyBuffer[(int)keyCode.K]) {
                keyLine[6] = keyLine[6] & ~(0x04);
            } else {
                keyLine[6] = keyLine[6] | (0x04);
            }

            if (keyBuffer[(int)keyCode.J]) {
                keyLine[6] = keyLine[6] & ~(0x08);
            } else {
                keyLine[6] = keyLine[6] | (0x08);
            }

            if (keyBuffer[(int)keyCode.H]) {
                keyLine[6] = keyLine[6] & ~(0x10);
            } else {
                keyLine[6] = keyLine[6] | (0x10);
            }

            #endregion Row 6: bffe - ENTER, L, K, J, H

            #region Row 7: 7ffe - SPACE, SYMBOL SHIFT, M, N, B

            if (keyBuffer[(int)keyCode.SPACE]) {
                keyLine[7] = keyLine[7] & ~(0x1);
            } else {
                keyLine[7] = keyLine[7] | (0x1);
            }

            if (keyBuffer[(int)keyCode.CTRL]) {
                keyLine[7] = keyLine[7] & ~(0x02);
            } else {
                keyLine[7] = keyLine[7] | (0x02);
            }

            if (keyBuffer[(int)keyCode.M]) {
                keyLine[7] = keyLine[7] & ~(0x04);
            } else {
                keyLine[7] = keyLine[7] | (0x04);
            }

            if (keyBuffer[(int)keyCode.N]) {
                keyLine[7] = keyLine[7] & ~(0x08);
            } else {
                keyLine[7] = keyLine[7] | (0x08);
            }

            if (keyBuffer[(int)keyCode.B]) {
                keyLine[7] = keyLine[7] & ~(0x10);
            } else {
                keyLine[7] = keyLine[7] | (0x010);
            }

            #endregion Row 7: 7ffe - SPACE, SYMBOL SHIFT, M, N, B

            #region Misc utility key functions

            //Check for caps lock key
            if (keyBuffer[(int)keyCode.CAPS]) {
                CapsLockOn = !CapsLockOn;
                keyBuffer[(int)keyCode.CAPS] = false;
            }

            if (CapsLockOn) {
                keyLine[0] = keyLine[0] & ~(0x1);
            }

            //Check if backspace key has been pressed (Caps Shift + 0 equivalent)
            if (keyBuffer[(int)keyCode.BACK]) {
                keyLine[0] = keyLine[0] & ~(0x1);
                keyLine[4] = keyLine[0] & ~(0x1);
            }

            //Check if left cursor key has been pressed (Caps Shift + 5)
            if (keyBuffer[(int)keyCode.LEFT]) {
                keyLine[0] = keyLine[0] & ~(0x1);
                keyLine[3] = keyLine[3] & ~(0x10);
            }

            //Check if right cursor key has been pressed (Caps Shift + 8)
            if (keyBuffer[(int)keyCode.RIGHT]) {
                keyLine[0] = keyLine[0] & ~(0x1);
                keyLine[4] = keyLine[4] & ~(0x04);
            }

            //Check if up cursor key has been pressed (Caps Shift + 7)
            if (keyBuffer[(int)keyCode.UP]) {
                keyLine[0] = keyLine[0] & ~(0x1);
                keyLine[4] = keyLine[4] & ~(0x08);
            }

            //Check if down cursor key has been pressed (Caps Shift + 6)
            if (keyBuffer[(int)keyCode.DOWN]) {
                keyLine[0] = keyLine[0] & ~(0x1);
                keyLine[4] = keyLine[4] & ~(0x10);
            }

            #endregion Misc utility key functions

            #region Function key presses

            #endregion Function key presses
        }

        //Resets the state of all the keys
        public void ResetKeyboard() {
            for (int f = 0; f < keyBuffer.Length; f++)
                keyBuffer[f] = false;

            for (int f = 0; f < 8; f++)
                keyLine[f] = 255;
        }

        //Adds a sound sample. This is called every 79 tstates from Process()
        public void OutSound() {
            averagedSound /= soundCounter;

            if (ayIsAvailable) {
                while (aySound.soundSampleCounter < 4)
                    aySound.SampleAY();

                aySound.EndSampleAY();
            }
            // byte[] tmp = System.BitConverter.GetBytes(aySound.averagedChannelSamples[0]);
            soundSamples[soundSampleCounter++] = (short)(aySound.averagedChannelSamples[0] + averagedSound);
            soundSamples[soundSampleCounter++] = (short)(aySound.averagedChannelSamples[1] + averagedSound);
            //tmp = System.BitConverter.GetBytes(aySound.averagedChannelSamples[1]);
            //soundSamples[soundSampleCounter++] = tmp[0];
            //soundSamples[soundSampleCounter++] = tmp[1];
            aySound.averagedChannelSamples[0] = 0;
            aySound.averagedChannelSamples[1] = 0;
            aySound.averagedChannelSamples[2] = 0;
            averagedSound = 0;
            soundCounter = 0;

            if (soundSampleCounter >= soundSamples.Length) {
                //aySound.EndSampleAY(averagedSound);

                soundSampleCounter = 0;
                beeper.PlayBuffer(ref soundSamples);
            }
        }

        //Updates audio state, called from Process()
        private void UpdateAudio(int dt) {
            if (ayIsAvailable) {
                ayTStates += dt;

                while (ayTStates >= AY_SAMPLE_RATE) {
                    aySound.Update();
                    ayTStates -= AY_SAMPLE_RATE;
                    aySound.SampleAY();
                }
            }
            averagedSound += soundOut;
            soundCounter++;
        }

        //Sets the speccy state to that of the SNA file
        public abstract void UseSNA(SNA_SNAPSHOT sna);

        //Sets the speccy state to that of the SNA file
        public virtual void UseSZX(SZXLoader szx) {
            I = szx.z80Regs.I;
            _HL = szx.z80Regs.HL1;
            _DE = szx.z80Regs.DE1;
            _BC = szx.z80Regs.BC1;
            _AF = szx.z80Regs.AF1;
            HL = szx.z80Regs.HL;
            DE = szx.z80Regs.DE;
            BC = szx.z80Regs.BC;
            IY = szx.z80Regs.IY;
            IX = szx.z80Regs.IX;
            IFF1 = (szx.z80Regs.IFF1 != 0);
            _R = szx.z80Regs.R;
            AF = szx.z80Regs.AF;
            SP = szx.z80Regs.SP;
            interruptMode = szx.z80Regs.IM;
            PC = szx.z80Regs.PC;
            lastOpcodeWasEI = (byte)((szx.z80Regs.Flags & SZXLoader.ZXSTZF_EILAST) != 0 ? 2 : 0);
            HaltOn = (szx.z80Regs.Flags & SZXLoader.ZXSTZF_HALTED) != 0;
            Issue2Keyboard = (szx.keyboard.Flags & SZXLoader.ZXSTKF_ISSUE2) != 0;
            
            //disabled till I work out how to load the damn palette table back
            /*
            if (szx.paletteLoaded)
            {
                ULAPlusEnabled = true;
                ULAPaletteEnabled = szx.palette.flags > 0 ? true : false;
                ULAPaletteGroup = szx.palette.currentRegister;

            }*/

            if (szx.header.MinorVersion > (byte)3)
                MemPtr = szx.z80Regs.MemPtr;
            else
                MemPtr = szx.z80Regs.MemPtr & 0xff;
            for (int f = 0; f < 16; f++) {
                Array.Copy(szx.RAM_BANK[f], 0, RAMpage[f], 0, 8192);
            }
        }

        private uint GetUIntFromString(string data) {
            byte[] carray = System.Text.ASCIIEncoding.UTF8.GetBytes(data);
            uint val = BitConverter.ToUInt32(carray, 0);
            return val;
        }

        //Saves machine state to a SZX file
        public virtual void SaveSZX(String filename) {
            CreateSZX().SaveSZX(filename);
        }

        private SZXLoader CreateSZX() {
            SZXLoader szx = new SZXLoader();
            szx.header = new SZXLoader.ZXST_Header();
            szx.creator = new SZXLoader.ZXST_Creator();
            szx.z80Regs = new SZXLoader.ZXST_Z80Regs();
            szx.specRegs = new SZXLoader.ZXST_SpecRegs();
            szx.keyboard = new SZXLoader.ZXST_Keyboard();

            for (int f = 0; f < 16; f++)
                szx.RAM_BANK[f] = new byte[8192];
            szx.header.MachineId = (byte)model;
            szx.header.Magic = GetUIntFromString("ZXST");
            szx.header.MajorVersion = 1;
            szx.header.MinorVersion = 4;
            szx.header.Flags |= (byte)LateTiming;
            szx.creator.CreatorName = "Zero Spectrum Emulator by Arjun ".ToCharArray();
            szx.creator.MajorVersion = SZXLoader.SZX_VERSION_SUPPORTED_MAJOR;
            szx.creator.MinorVersion = SZXLoader.SZX_VERSION_SUPPORTED_MINOR;
            if (Issue2Keyboard)
                szx.keyboard.Flags |= SZXLoader.ZXSTKF_ISSUE2;
            szx.keyboard.KeyboardJoystick |= 8;
            szx.z80Regs.AF = (ushort)AF;
            szx.z80Regs.AF1 = (ushort)_AF;
            szx.z80Regs.BC = (ushort)BC;
            szx.z80Regs.BC1 = (ushort)_BC;
            szx.z80Regs.MemPtr = (ushort)MemPtr;
            szx.z80Regs.CyclesStart = (uint)totalTStates;
            szx.z80Regs.DE = (ushort)DE;
            szx.z80Regs.DE1 = (ushort)_DE;
            if (lastOpcodeWasEI != 0)
                szx.z80Regs.Flags |= SZXLoader.ZXSTZF_EILAST;
            else if (HaltOn)
                szx.z80Regs.Flags |= SZXLoader.ZXSTZF_HALTED;
            szx.z80Regs.HL = (ushort)HL;
            szx.z80Regs.HL1 = (ushort)_HL;
            szx.z80Regs.I = (byte)I;
            szx.z80Regs.IFF1 = (byte)(IFF1 ? 1 : 0);
            szx.z80Regs.IFF1 = (byte)(IFF2 ? 1 : 0);
            szx.z80Regs.IM = (byte)interruptMode;
            szx.z80Regs.IX = (ushort)IX;
            szx.z80Regs.IY = (ushort)IY;
            szx.z80Regs.PC = (ushort)PC;
            szx.z80Regs.R = (byte)R;
            szx.z80Regs.SP = (ushort)SP;
            szx.specRegs.Border = (byte)borderColour;
            szx.specRegs.Fe = (byte)lastFEOut;
            szx.specRegs.pagePort = (byte)last1ffdOut;
            szx.specRegs.x7ffd = (byte)last7ffdOut;

            if (ayIsAvailable) {
                szx.ayState = new SZXLoader.ZXST_AYState();
                szx.ayState.cFlags = 0;
                szx.ayState.currentRegister = (byte)aySound.SelectedRegister;
                szx.ayState.chRegs = aySound.GetRegisters();
            }

            for (int f = 0; f < 16; f++) {
                Array.Copy(RAMpage[f], 0, szx.RAM_BANK[f], 0, 8192);
            }

            if (tape_readToPlay && (tapeFilename != "")) {
                szx.InsertTape = true;
                szx.externalTapeFile = tapeFilename;
            }
            if (ULAPlusEnabled) {
                szx.palette = new SZXLoader.ZXST_PaletteBlock();
                szx.palette.paletteRegs = new byte[64];
                szx.paletteLoaded = true;
                szx.palette.flags = (byte)(ULAPaletteEnabled ? 1 : 0);
                szx.palette.currentRegister = (byte)ULAPaletteGroup;
                for (int f = 0; f < 64; f++) {
                    int rgb = ULAPlusColours[f];
                    int bbyte = (rgb & 0xff);
                    int gbyte = (rgb >> 8) & 0xff;
                    int rbyte = (rgb >> 16) & 0xff;
                    int bl = bbyte & 0x1;
                    int bm = bl;
                    int bh = (bbyte >> 4) & 0x1;
                    int gl = (gbyte & 0x1);
                    int gm = (gbyte >> 1) & 0x1;
                    int gh = (gbyte >> 4) & 0x1;
                    int rl = (rbyte & 0x1);
                    int rm = (rbyte >> 1) & 0x1;
                    int rh = (rbyte >> 4) & 0x1;
                    byte val = (byte)(((gh << 7) | (gm << 6) | (gl << 5)) | ((rh << 4) | (rm << 3) | (rl << 2)) | ((bh << 1) | bl));
                    szx.palette.paletteRegs[f] = val;
                }
            }

            return szx;
        }

        //Enable/disable stereo sound for AY playback
        public void SetStereoSound(int val) {
            if (val == 0)
                aySound.StereoSound = false;
            else {
                aySound.StereoSound = true;
                if (val == 1)
                    aySound.SetSpeakerACB(true);
                else
                    aySound.SetSpeakerACB(false);
            }
        }

        //Enables/Disables AY sound
        public virtual void EnableAY(bool val) {
        }

        //Sets the speccy state to that of the Z80 file
        public abstract void UseZ80(Z80_SNAPSHOT z80);

        //Sets up the contention table for the machine
        public abstract void BuildContentionTable();

        //Builds the tstate to attribute map used for floating bus
        public void BuildAttributeMap() {
            int start = DisplayStart;

            for (int f = 0; f < DisplayLength; f++, start++) {
                int addrH = start >> 8; //div by 256
                int addrL = start % 256;

                int pixelY = (addrH & 0x07);
                pixelY |= (addrL & (0xE0)) >> 2;
                pixelY |= (addrH & (0x18)) << 3;

                int attrIndex_Y = AttributeStart + ((pixelY >> 3) << 5);// pixel/8 * 32

                addrL = start % 256;
                int pixelX = addrL & (0x1F);

                attr[f] = (short)(attrIndex_Y + pixelX);
            }
        }

        //Resets the render state everytime an interrupt is generated
        public void ULAUpdateStart() {
            ULAByteCtr = 0;
            screenByteCtr = DisplayStart;
            lastTState = ActualULAStart;
            needsPaint = true;
        }

        //Returns true if the given address should be contended, false otherwise
        public abstract bool IsContended(int addr);

        //Contends the machine for a given address (_addr)
        public void Contend(int _addr) {
            if (IsContended(_addr) && model != MachineModel._plus3) {
                totalTStates += contentionTable[totalTStates];
            }
        }

        //Contends the machine for a given address (_addr) for n tstates (_time) for x times (_count)
        public void Contend(int _addr, int _time, int _count) {
            if (IsContended(_addr) && model != MachineModel._plus3) {
                for (int f = 0; f < _count; f++) {
                    totalTStates += contentionTable[totalTStates] + _time;
                }
            } else
                totalTStates += _count * _time;
        }

        //Changes the spectrum palette to the one provided
        public void SetPalette(int[] newPalette) {
            AttrColors = newPalette;
        }

        //Returns true if the last submitted buffer has finished playing
        //public bool AudioDone()
        //{
        //    return beeper.FinishedPlaying();
        //}

        //Loads the ULAPlus palette
        public bool LoadULAPlusPalette(string filename) {
            using (System.IO.FileStream fs = new System.IO.FileStream(filename, System.IO.FileMode.Open)) {
                using (System.IO.BinaryReader r = new System.IO.BinaryReader(fs)) {
                    int bytesToRead = (int)fs.Length;

                    if (bytesToRead > 63)
                        return false; //not a 64 byte palette file

                    byte[] buffer = new byte[bytesToRead];
                    int bytesRead = r.Read(buffer, 0, bytesToRead);

                    if (bytesRead == 0)
                        return false; //something bad happened!

                    for (int f = 0; f < 64; ++f)
                        ULAPlusColours[f] = buffer[f];

                    PC = PeekWordNoContend(SP);
                    SP += 2;
                    return true;
                }
            }
        }

        public void NextRZXFrame() {
            rzxFrameCount++;
            rzxFetchCount = 0;
            rzxInputCount = 0;
            totalTStates = 0;
            if (rzxFrameCount < rzx.frames.Count) {
                rzxFrame = rzx.frames[rzxFrameCount];
            } else
                isPlayingRZX = false;
        }

        public void EndRZXFrame() {
            if (IFF1) {
                R++;
                Interrupt();
            }

            if (rzxInputCount < rzxFrame.inputCount)
                rzxInputCount = 0;

            if (totalTStates >= FrameLength) {
                int deltaSoundT = FrameLength - totalTStates;
                timeToOutSound += deltaSoundT;
                totalTStates = FrameLength; //generate interrupt
            }

            if (!needsPaint)
                UpdateScreenBuffer(FrameLength - 1);

            frameCount++;

            if (frameCount > 15) {
                flashOn = !flashOn;
                frameCount = 0;
            }
            ULAUpdateStart();
            NextRZXFrame();
        }

        public void ProcessRZX() {
            R++;

            if (rzxFetchCount > rzx.frames[rzxFrameCount].instructionCount - 1) {
                EndRZXFrame();
            }

            rzxFetchCount++;
            oldTStates = totalTStates;
            opcode = GetOpcode(PC);
            PC = (PC + 1) & 0xffff;
            totalTStates++; //effectively, totalTStates + 4 because PeekByte does the other 3

            Execute();

            deltaTStates = totalTStates - oldTStates;

            //// Change CPU speed///////////////////////
            if (emulationSpeed > 9) {
                deltaTStates /= emulationSpeed;
                if (deltaTStates < 1)
                    deltaTStates = 0;// (tapeIsPlaying ? 0 : 1); //tape loading likes 0, sound emulation likes 1. WTF?

                totalTStates = oldTStates + deltaTStates;
                if (tapeIsPlaying)
                    soundTStatesToSample = 79;
            }
            /////////////////////////////////////////////////
            timeToOutSound += deltaTStates;

            UpdateTapeState(deltaTStates);
            UpdateAudio(deltaTStates);

            //Update sound every 79 tstates
            while (timeToOutSound >= soundTStatesToSample) {
                averagedSound /= soundCounter;

                if (ayIsAvailable) {
                    while (aySound.soundSampleCounter < 4)
                        aySound.SampleAY();

                    aySound.EndSampleAY();
                }
                if (timeToOutSound >= soundTStatesToSample) {
                    soundSamples[soundSampleCounter++] = (short)(aySound.averagedChannelSamples[0] + averagedSound);
                    soundSamples[soundSampleCounter++] = (short)(aySound.averagedChannelSamples[1] + averagedSound);

                    if (soundSampleCounter >= soundSamples.Length) {
                        byte[] sndbuf = beeper.LockBuffer();
                        if (sndbuf != null) {
                            System.Buffer.BlockCopy(soundSamples, 0, sndbuf, 0, sndbuf.Length);
                            beeper.UnlockBuffer(sndbuf);
                        }
                        soundSampleCounter = 0;// (short)(soundSampleCounter - (soundSamples.Length));
                    }
                    timeToOutSound -= soundTStatesToSample;
                };
                aySound.averagedChannelSamples[0] = 0;
                aySound.averagedChannelSamples[1] = 0;
                aySound.averagedChannelSamples[2] = 0;
                averagedSound = 0;
                soundCounter = 0;
            }

            if (totalTStates >= FrameLength) {
                int deltaSoundT = FrameLength - totalTStates;
                timeToOutSound += deltaSoundT;
                totalTStates -= FrameLength;
            }
        }

        //The heart of the speccy. Executes opcodes till 69888 tstates (1 frame) have passed
        public void Process() {
            //Handle re-triggered interrupts!
            if (IFF1 && (lastOpcodeWasEI == 0) && (totalTStates < InterruptPeriod)) {
                if (isRecordingRZX) {
                    rzxFrame = new RZXLoader.RZX_Frame();
                    rzxFrame.inputCount = (ushort)rzxInputs.Count;
                    rzxFrame.instructionCount = (ushort)rzxFetchCount;
                    rzxFrame.inputs = rzxInputs.ToArray();
                    rzx.frames.Add(rzxFrame);
                    rzxFetchCount = 0;
                    rzxInputCount = 0;
                    rzxInputs = new System.Collections.Generic.List<byte>();
                }

                if (StateChangeEvent != null)
                    OnStateChangeEvent(new StateChangeEventArgs(SPECCY_EVENT.RE_INTTERUPT));
                Interrupt();
            }

            if (lastOpcodeWasEI > 0)
                lastOpcodeWasEI--;

            //Check if TR DOS needs to be swapped for Pentagon 128k.
            //TR DOS is swapped in when PC >= 15616 and swapped out when PC > 16383.
            if (model == MachineModel._pentagon) {
                if (trDosPagedIn) {
                    if (PC > 0x3FFF) {
                        if ((last7ffdOut & 0x10) != 0) {
                            //48k basic
                            PageReadPointer[0] = ROMpage[2];
                            PageReadPointer[1] = ROMpage[3];
                            PageWritePointer[0] = JunkMemory[0];
                            PageWritePointer[1] = JunkMemory[1];
                            BankInPage0 = ROM_48_BAS;
                            lowROMis48K = true;
                        } else {
                            //128k basic
                            PageReadPointer[0] = ROMpage[0];
                            PageReadPointer[1] = ROMpage[1];
                            PageWritePointer[0] = JunkMemory[0];
                            PageWritePointer[1] = JunkMemory[1];
                            BankInPage0 = ROM_128_BAS;
                            lowROMis48K = false;
                        }
                        trDosPagedIn = false;
                    }
                }
                else if (lowROMis48K) {
                     if ((PC >> 8) == 0x3d) {
                        PageReadPointer[0] = ROMpage[4];
                        PageReadPointer[1] = ROMpage[5];
                        PageWritePointer[0] = JunkMemory[0];
                        PageWritePointer[1] = JunkMemory[1];
                        trDosPagedIn = true;
                        BankInPage0 = ROM_TR_DOS;
                    }
                } 
            }
            //Inlined version of FetchInstruction()
            R++;

            if (isRecordingRZX)
                rzxFetchCount++;

            oldTStates = totalTStates;
            opcode = GetOpcode(PC);

            PC = (PC + 1) & 0xffff;
            totalTStates++; //effectively, totalTStates + 4 because PeekByte does the other 3
            Execute();
            deltaTStates = totalTStates - oldTStates;
            
            //// Change CPU speed///////////////////////
            if (emulationSpeed > 0 && !tapeIsPlaying /*!isPauseBlockPreproccess*/) {
                deltaTStates /= emulationSpeed;
                if (deltaTStates < 1)
                    deltaTStates = (tapeIsPlaying ? 0 : 1); //tape loading likes 0, sound emulation likes 1. WTF?

                totalTStates = oldTStates + deltaTStates;
                if (tapeIsPlaying)
                   soundTStatesToSample = 79;
            }
            /////////////////////////////////////////////////
            timeToOutSound += deltaTStates;

            //UpdateTape
            if (tapeIsPlaying && !tape_edgeDetectorRan) {
                tapeTStates += deltaTStates;
                while (tapeTStates >= edgeDuration) {
                    tapeTStates = (int)(tapeTStates - edgeDuration);
                    DoTapeEvent(new TapeEventArgs(TapeEventType.EDGE_LOAD));
                }
            }
            tape_edgeDetectorRan = false;

            //Update Sound
            if (!externalSingleStep) {
                if (ayIsAvailable) {
                    ayTStates += deltaTStates;

                    while (ayTStates >= AY_SAMPLE_RATE) {
                        aySound.Update();
                        ayTStates -= AY_SAMPLE_RATE;
                        aySound.SampleAY();
                    }
                }
                averagedSound += soundOut;
                soundCounter++;

                //Update sound every 79 tstates
                if (timeToOutSound >= soundTStatesToSample) {
                    averagedSound /= soundCounter;

                    if (ayIsAvailable) {
                        while (aySound.soundSampleCounter < 4)
                            aySound.SampleAY();
                        aySound.EndSampleAY();
                    }

                    while (timeToOutSound >= soundTStatesToSample) {
                        soundSamples[soundSampleCounter++] = (short)(aySound.averagedChannelSamples[0] + averagedSound);
                        soundSamples[soundSampleCounter++] = (short)(aySound.averagedChannelSamples[1] + averagedSound);

                        //This second while is required to maintain sound quality when running music heavy demos like
                        //Fire&Ice and Rage on the Pentagon!
                        while (soundSampleCounter >= soundSamples.Length) {
                            byte[] sndbuf = beeper.LockBuffer();
                            if (sndbuf != null) {
                                System.Buffer.BlockCopy(soundSamples, 0, sndbuf, 0, sndbuf.Length);
                                beeper.UnlockBuffer(sndbuf);
                            }
                            soundSampleCounter = 0;
                        }
                        timeToOutSound -= soundTStatesToSample;
                    };
                    aySound.averagedChannelSamples[0] = 0;
                    aySound.averagedChannelSamples[1] = 0;
                    aySound.averagedChannelSamples[2] = 0;
                    averagedSound = 0;
                    soundCounter = 0;

                }
            }
            //End of frame?
            if (totalTStates >= FrameLength) {
                //If machine has already repainted the entire screen,
                //somewhere midway through execution, we can skip this.
                if (!needsPaint)
                    UpdateScreenBuffer(FrameLength);

                OnFrameEndEvent();

                totalTStates -= FrameLength;

                frameCount++;

                if (frameCount > 15) {
                    flashOn = !flashOn;
                    frameCount = 0;
                }

                ULAUpdateStart();
                UpdateInput();

                if (isRecordingRZX) {
                    rzxFrame = new RZXLoader.RZX_Frame();
                    rzxFrame.inputCount = (ushort)rzxInputs.Count;
                    rzxFrame.instructionCount = (ushort)rzxFetchCount;
                    rzxFrame.inputs = rzxInputs.ToArray();
                    rzx.frames.Add(rzxFrame);
                    rzxFetchCount = 0;
                    rzxInputCount = 0;
                    rzxInputs = new System.Collections.Generic.List<byte>();
                }

                //Need interrupt?
                if (IFF1 && lastOpcodeWasEI == 0) {
                    R++;

                    if (StateChangeEvent != null)
                        OnStateChangeEvent(new StateChangeEventArgs(SPECCY_EVENT.INTTERUPT));

                    Interrupt();
                }
            }
        }

        //Executes a single opcode
        public void Execute() {
            disp = 0;
            //Massive switch-case to decode the instructions!
            switch (opcode) {

                #region NOP

                case 0x00: //NOP
                    // Log("NOP");
                    break;

                #endregion NOP

                # region 16 bit load operations (LD rr, nn)
                /** LD rr, nn (excluding DD prefix) **/
                case 0x01: //LD BC, nn
                    BC = PeekWord(PC);
                    // Log(String.Format("LD BC, {0,-6:X}", BC));
                    PC += 2;
                    break;

                case 0x11:  //LD DE, nn
                    DE = PeekWord(PC);
                    // Log(String.Format("LD DE, {0,-6:X}", DE));
                    PC += 2;
                    break;

                case 0x21:  //LD HL, nn
                    HL = PeekWord(PC);
                    // Log(String.Format("LD HL, {0,-6:X}", HL));
                    PC += 2;
                    break;

                case 0x2A:  //LD HL, (nn)
                    disp = PeekWord(PC);
                    HL = PeekWord(disp);
                    // Log(String.Format("LD HL, ({0,-6:X})", disp));
                    PC += 2;
                    MemPtr = disp + 1;
                    break;

                case 0x31:  //LD SP, nn
                    SP = PeekWord(PC);
                    // Log(String.Format("LD SP, {0,-6:X}", SP));
                    PC += 2;
                    break;

                case 0xF9:  //LD SP, HL
                    //if (model == MachineModel._plus3)
                    //    totalTStates += 2;
                    //else
                    Contend(IR, 1, 2);
                    // Log("LD SP, HL");
                    SP = HL;
                    break;
                #endregion

                #region 16 bit increments (INC rr)
                /** INC rr **/
                case 0x03:  //INC BC
                    //if (model == MachineModel._plus3)
                    //    totalTStates += 2;
                    //else
                    Contend(IR, 1, 2);
                    // Log("INC BC");
                    BC++;
                    break;

                case 0x13:  //INC DE
                    //if (model == MachineModel._plus3)
                    //    totalTStates += 2;
                    //else
                    Contend(IR, 1, 2);
                    // Log("INC DE");
                    DE++;
                    break;

                case 0x23:  //INC HL
                    // if (model == MachineModel._plus3)
                    //     totalTStates += 2;
                    // else
                    Contend(IR, 1, 2);
                    // Log("INC HL");
                    HL++;
                    break;

                case 0x33:  //INC SP
                    //if (model == MachineModel._plus3)
                    //    totalTStates += 2;
                    //else
                    Contend(IR, 1, 2);
                    // Log("INC SP");
                    SP++;
                    break;
                #endregion INC rr

                #region 8 bit increments (INC r)
                /** INC r + INC (HL) **/
                case 0x04:  //INC B
                    B = Inc(B);
                    // Log("INC B");
                    break;

                case 0x0C:  //INC C
                    C = Inc(C);
                    // Log("INC C");
                    break;

                case 0x14:  //INC D
                    D = Inc(D);
                    // Log("INC D");
                    break;

                case 0x1C:  //INC E
                    E = Inc(E);
                    // Log("INC E");
                    break;

                case 0x24:  //INC H
                    H = Inc(H);
                    // Log("INC H");
                    break;

                case 0x2C:  //INC L
                    L = Inc(L);
                    // Log("INC L");
                    break;

                case 0x34:  //INC (HL)
                    val = PeekByte(HL);
                    val = Inc(val);
                    Contend(HL, 1, 1);
                    PokeByte(HL, val);
                    // Log("INC (HL)");
                    break;

                case 0x3C:  //INC A
                    // Log("INC A");
                    A = Inc(A);
                    break;
                #endregion

                #region 8 bit decrement (DEC r)
                /** DEC r + DEC (HL)**/
                case 0x05: //DEC B
                    // Log("DEC B");
                    B = Dec(B);
                    break;

                case 0x0D:    //DEC C
                    // Log("DEC C");
                    C = Dec(C);
                    break;

                case 0x15:  //DEC D
                    // Log("DEC D");
                    D = Dec(D);
                    break;

                case 0x1D:  //DEC E
                    // Log("DEC E");
                    E = Dec(E);
                    break;

                case 0x25:  //DEC H
                    // Log("DEC H");
                    H = Dec(H);
                    break;

                case 0x2D:  //DEC L
                    // Log("DEC L");
                    L = Dec(L);
                    break;

                case 0x35:  //DEC (HL)
                    // Log("DEC (HL)");
                    //val = PeekByte(HL);
                    val = Dec(PeekByte(HL));
                    Contend(HL, 1, 1);
                    PokeByte(HL, val);
                    break;

                case 0x3D:  //DEC A
                    // Log("DEC A");
                    A = Dec(A);
                    //Deck DEC A short circuit (from SpecEmu's source with permission from Woody)
                    if (tapeIsPlaying && edgeLoadTapes)
                        if (PeekByteNoContend(PC) == 0x20)
                            if (PeekByteNoContend(PC + 1) == 0xfd) {
                                if (A != 0) {
                                    int _a = A;
                                    _a--;
                                    _a <<= 4;
                                    _a += 4 + 7 + 12;
                                    tapeTStates += _a;
                                    PC += 2;
                                    A = 0;
                                    F |= 64;
                                }
                    }
                    break;
                #endregion

                #region 16 bit decrements
                /** DEC rr **/
                case 0x0B:  //DEC BC
                    // Log("DEC BC");
                    //if (model == MachineModel._plus3)
                    //    totalTStates += 2;
                    //else
                    Contend(IR, 1, 2);
                    BC--;
                    break;

                case 0x1B:  //DEC DE
                    // Log("DEC DE");
                    //if (model == MachineModel._plus3)
                    //    totalTStates += 2;
                    //else
                    Contend(IR, 1, 2);
                    DE--;
                    break;

                case 0x2B:  //DEC HL
                    // Log("DEC HL");
                    //if (model == MachineModel._plus3)
                    //    totalTStates += 2;
                    //else
                    Contend(IR, 1, 2);
                    HL--;
                    break;

                case 0x3B:  //DEC SP
                    // Log("DEC SP");
                    //if (model == MachineModel._plus3)
                    //    totalTStates += 2;
                    //else
                    Contend(IR, 1, 2);
                    SP--;
                    break;
                #endregion

                #region Immediate load operations (LD (nn), r)
                /** LD (rr), r + LD (nn), HL  + LD (nn), A **/
                case 0x02: //LD (BC), A
                    // Log("LD (BC), A");
                    PokeByte(BC, A);
                    MemPtr = (((BC + 1) & 0xff) | (A << 8));
                    break;

                case 0x12:  //LD (DE), A
                    // Log("LD (DE), A");
                    PokeByte(DE, A);
                    MemPtr = (((DE + 1) & 0xff) | (A << 8));
                    break;

                case 0x22:  //LD (nn), HL
                    addr = PeekWord(PC);
                    // Log(String.Format("LD ({0,-6:X}), HL", addr));

                    PokeWord(addr, HL);
                    MemPtr = addr + 1;
                    PC += 2;
                    break;

                case 0x32:  //LD (nn), A
                    addr = PeekWord(PC);
                    // Log(String.Format("LD ({0,-6:X}), A", addr));

                    PokeByte(addr, A);
                    MemPtr = (((addr + 1) & 0xff) | (A << 8));
                    PC += 2;
                    break;

                case 0x36:  //LD (HL), n
                    val = PeekByte(PC);
                    // Log(String.Format("LD (HL), {0,-6:X}", val));

                    PokeByte(HL, val);
                    PC += 1;
                    break;
                #endregion

                #region Indirect load operations (LD r, r)
                /** LD r, r **/
                case 0x06: //LD B, n
                    B = PeekByte(PC);
                    // Log(String.Format("LD B, {0,-6:X}", B));

                    PC += 1;
                    break;

                case 0x0A:  //LD A, (BC)
                    A = PeekByte(BC);
                    MemPtr = BC + 1;
                    // Log("LD A, (BC)");

                    break;

                case 0x0E:  //LD C, n
                    C = PeekByte(PC);
                    // Log(String.Format("LD C, {0,-6:X}", C));

                    PC += 1;
                    break;

                case 0x16:  //LD D,n
                    D = PeekByte(PC);
                    // Log(String.Format("LD D, {0,-6:X}", D));

                    PC += 1;
                    break;

                case 0x1A:  //LD A,(DE)
                    // Log("LD A, (DE)");
                    A = PeekByte(DE);
                    MemPtr = DE + 1;
                    break;

                case 0x1E:  //LD E,n
                    E = PeekByte(PC);
                    // Log(String.Format("LD E, {0,-6:X}", E));

                    PC += 1;
                    break;

                case 0x26:  //LD H,n
                    H = PeekByte(PC);
                    // Log(String.Format("LD H, {0,-6:X}", H));

                    PC += 1;
                    break;

                case 0x2E:  //LD L,n
                    L = PeekByte(PC);
                    // Log(String.Format("LD L, {0,-6:X}", L));

                    PC += 1;
                    break;

                case 0x3A:  //LD A,(nn)
                    addr = PeekWord(PC);
                    // Log(String.Format("LD A, ({0,-6:X})", addr));
                    MemPtr = addr + 1;
                    A = PeekByte(addr);
                    PC += 2;
                    break;

                case 0x3E:  //LD A,n
                    A = PeekByte(PC);
                    // Log(String.Format("LD A, {0,-6:X}", A));

                    PC += 1;
                    break;

                case 0x40:  //LD B,B
                    // Log("LD B, B");
                    B = B;
                    break;

                case 0x41:  //LD B,C
                    // Log("LD B, C");
                    B = C;
                    break;

                case 0x42:  //LD B,D
                    // Log("LD B, D");
                    B = D;
                    break;

                case 0x43:  //LD B,E
                    // Log("LD B, E");
                    B = E;
                    break;

                case 0x44:  //LD B,H
                    // Log("LD B, H");
                    B = H;
                    break;

                case 0x45:  //LD B,L
                    // Log("LD B, L");
                    B = L;
                    break;

                case 0x46:  //LD B,(HL)
                    // Log("LD B, (HL)");
                    B = PeekByte(HL);
                    break;

                case 0x47:  //LD B,A
                    // Log("LD B, A");
                    B = A;
                    break;

                case 0x48:  //LD C,B
                    // Log("LD C, B");
                    C = B;
                    break;

                case 0x49:  //LD C,C
                    // Log("LD C, C");
                    C = C;
                    break;

                case 0x4A:  //LD C,D
                    // Log("LD C, D");
                    C = D;
                    break;

                case 0x4B:  //LD C,E
                    // Log("LD C, E");
                    C = E;
                    break;

                case 0x4C:  //LD C,H
                    // Log("LD C, H");
                    C = H;
                    break;

                case 0x4D:  //LD C,L
                    // Log("LD C, L");
                    C = L;
                    break;

                case 0x4E:  //LD C, (HL)
                    // Log("LD C, (HL)");
                    C = PeekByte(HL);
                    break;

                case 0x4F:  //LD C,A
                    // Log("LD C, A");
                    C = A;
                    break;

                case 0x50:  //LD D,B
                    // Log("LD D, B");
                    D = B;
                    break;

                case 0x51:  //LD D,C
                    // Log("LD D, C");
                    D = C;
                    break;

                case 0x52:  //LD D,D
                    // Log("LD D, D");
                    D = D;
                    break;

                case 0x53:  //LD D,E
                    // Log("LD D, E");
                    D = E;
                    break;

                case 0x54:  //LD D,H
                    // Log("LD D, H");
                    D = H;
                    break;

                case 0x55:  //LD D,L
                    // Log("LD D, L");
                    D = L;
                    break;

                case 0x56:  //LD D,(HL)
                    // Log("LD D, (HL)");
                    D = PeekByte(HL);
                    break;

                case 0x57:  //LD D,A
                    // Log("LD D, A");
                    D = A;
                    break;

                case 0x58:  //LD E,B
                    // Log("LD E, B");
                    E = B;
                    break;

                case 0x59:  //LD E,C
                    // Log("LD E, C");
                    E = C;
                    break;

                case 0x5A:  //LD E,D
                    // Log("LD E, D");
                    E = D;
                    break;

                case 0x5B:  //LD E,E
                    // Log("LD E, E");
                    E = E;
                    break;

                case 0x5C:  //LD E,H
                    // Log("LD E, H");
                    E = H;
                    break;

                case 0x5D:  //LD E,L
                    // Log("LD E, L");
                    E = L;
                    break;

                case 0x5E:  //LD E,(HL)
                    // Log("LD E, (HL)");
                    E = PeekByte(HL);
                    break;

                case 0x5F:  //LD E,A
                    // Log("LD E, A");
                    E = A;
                    break;

                case 0x60:  //LD H,B
                    // Log("LD H, B");
                    H = B;
                    break;

                case 0x61:  //LD H,C
                    // Log("LD H, C");
                    H = C;
                    break;

                case 0x62:  //LD H,D
                    // Log("LD H, D");
                    H = D;
                    break;

                case 0x63:  //LD H,E
                    // Log("LD H, E");
                    H = E;
                    break;

                case 0x64:  //LD H,H
                    // Log("LD H, H");
                    H = H;
                    break;

                case 0x65:  //LD H,L
                    // Log("LD H, L");
                    H = L;
                    break;

                case 0x66:  //LD H,(HL)
                    // Log("LD H, (HL)");
                    H = PeekByte(HL);
                    break;

                case 0x67:  //LD H,A
                    // Log("LD H, A");
                    H = A;
                    break;

                case 0x68:  //LD L,B
                    // Log("LD L, B");
                    L = B;
                    break;

                case 0x69:  //LD L,C
                    // Log("LD L, C");
                    L = C;
                    break;

                case 0x6A:  //LD L,D
                    // Log("LD L, D");
                    L = D;
                    break;

                case 0x6B:  //LD L,E
                    // Log("LD L, E");
                    L = E;
                    break;

                case 0x6C:  //LD L,H
                    // Log("LD L, H");
                    L = H;
                    break;

                case 0x6D:  //LD L,L
                    // Log("LD L, L");
                    L = L;
                    break;

                case 0x6E:  //LD L,(HL)
                    // Log("LD L, (HL)");
                    L = PeekByte(HL);
                    break;

                case 0x6F:  //LD L,A
                    // Log("LD L, A");
                    L = A;
                    break;

                case 0x70:  //LD (HL),B
                    // Log("LD (HL), B");
                    PokeByte(HL, B);
                    break;

                case 0x71:  //LD (HL),C
                    // Log("LD (HL), C");
                    PokeByte(HL, C);
                    break;

                case 0x72:  //LD (HL),D
                    // Log("LD (HL), D");
                    PokeByte(HL, D);
                    break;

                case 0x73:  //LD (HL),E
                    // Log("LD (HL), E");
                    PokeByte(HL, E);
                    break;

                case 0x74:  //LD (HL),H
                    // Log("LD (HL), H");
                    PokeByte(HL, H);
                    break;

                case 0x75:  //LD (HL),L
                    // Log("LD (HL), L");
                    PokeByte(HL, L);
                    break;

                case 0x77:  //LD (HL),A
                    // Log("LD (HL), A");
                    PokeByte(HL, A);
                    break;

                case 0x78:  //LD A,B
                    // Log("LD A, B");
                    A = B;
                    break;

                case 0x79:  //LD A,C
                    // Log("LD A, C");
                    A = C;
                    break;

                case 0x7A:  //LD A,D
                    // Log("LD A, D");
                    A = D;
                    break;

                case 0x7B:  //LD A,E
                    // Log("LD A, E");
                    A = E;
                    break;

                case 0x7C:  //LD A,H
                    // Log("LD A, H");
                    A = H;
                    break;

                case 0x7D:  //LD A,L
                    // Log("LD A, L");
                    A = L;
                    break;

                case 0x7E:  //LD A,(HL)
                    // Log("LD A, (HL)");
                    A = PeekByte(HL);
                    break;

                case 0x7F:  //LD A,A
                    // Log("LD A, A");
                    A = A;
                    break;
                #endregion

                #region Rotates on Accumulator
                /** Accumulator Rotates **/
                case 0x07: //RLCA
                    // Log("RLCA");
                    bool ac = (A & F_SIGN) != 0; //save the msb bit

                    if (ac) {
                        A = ((A << 1) | F_CARRY) & 0xff;
                    } else {
                        A = (A << 1) & 0xff;
                    }
                    SetF3((A & F_3) != 0);
                    SetF5((A & F_5) != 0);
                    SetCarry(ac);
                    SetHalf(false);
                    SetNeg(false);
                    break;

                case 0x0F:  //RRCA
                    // Log("RRCA");

                    ac = (A & F_CARRY) != 0; //save the lsb bit

                    if (ac) {
                        A = (A >> 1) | F_SIGN;
                    } else {
                        A = A >> 1;
                    }

                    SetF3((A & F_3) != 0);
                    SetF5((A & F_5) != 0);
                    SetCarry(ac);
                    SetHalf(false);
                    SetNeg(false);
                    break;

                case 0x17:  //RLA
                    // Log("RLA");
                    ac = ((A & F_SIGN) != 0);

                    int msb = F & F_CARRY;

                    if (msb != 0) {
                        A = ((A << 1) | F_CARRY);
                    } else {
                        A = (A << 1);
                    }
                    SetF3((A & F_3) != 0);
                    SetF5((A & F_5) != 0);
                    SetCarry(ac);
                    SetHalf(false);
                    SetNeg(false);
                    break;

                case 0x1F:  //RRA
                    // Log("RRA");
                    ac = (A & F_CARRY) != 0; //save the lsb bit
                    int lsb = F & F_CARRY;

                    if (lsb != 0) {
                        A = (A >> 1) | F_SIGN;
                    } else {
                        A = A >> 1;
                    }
                    SetF3((A & F_3) != 0);
                    SetF5((A & F_5) != 0);
                    SetCarry(ac);
                    SetHalf(false);
                    SetNeg(false);
                    break;
                #endregion

                #region Exchange operations (EX)
                /** Exchange operations **/
                case 0x08:     //EX AF, AF'
                    // Log("EX AF, AF'");
                    ex_af_af();
                    break;

                case 0xD9:   //EXX
                    // Log("EXX");
                    exx();
                    break;

                case 0xE3:  //EX (SP), HL
                    // Log("EX (SP), HL");
                    //int temp = HL;
                    addr = PeekWord(SP);
                    Contend(SP + 1, 1, 1);
                    PokeByte((SP + 1) & 0xffff, HL >> 8);
                    PokeByte(SP, HL & 0xff);
                    Contend(SP, 1, 2);
                    HL = addr;
                    MemPtr = HL;
                    break;

                case 0xEB:  //EX DE, HL
                    // Log("EX DE, HL");
                    int temp = DE;
                    DE = HL;
                    HL = temp;
                    break;
                #endregion

                #region 16 bit addition to HL (Add HL, rr)
                /** Add HL, rr **/
                case 0x09:     //ADD HL, BC
                    // Log("ADD HL, BC");
                    //if (model == MachineModel._plus3)
                    //    totalTStates += 7;
                    //else
                    Contend(IR, 1, 7);

                    MemPtr = HL + 1;
                    HL = Add_RR(HL, BC);

                    break;

                case 0x19:    //ADD HL, DE
                    // Log("ADD HL, DE");
                    //if (model == MachineModel._plus3)
                    //    totalTStates += 7;
                    //else
                    Contend(IR, 1, 7);
                    MemPtr = HL + 1;
                    HL = Add_RR(HL, DE);

                    break;

                case 0x29:  //ADD HL, HL
                    // Log("ADD HL, HL");
                    //if (model == MachineModel._plus3)
                    //    totalTStates += 7;
                    //else
                    Contend(IR, 1, 7);
                    MemPtr = HL + 1;
                    HL = Add_RR(HL, HL);
                    break;

                case 0x39:  //ADD HL, SP
                    // Log("ADD HL, SP");
                    //if (model == MachineModel._plus3)
                    //    totalTStates += 7;
                    //else
                    Contend(IR, 1, 7);
                    MemPtr = HL + 1;
                    HL = Add_RR(HL, SP);
                    break;
                #endregion

                #region 8 bit addition to accumulator (Add r, r)
                /*** ADD r, r ***/
                case 0x80:  //ADD A,B
                    // Log("ADD A, B");
                    Add_R(B);
                    break;

                case 0x81:  //ADD A,C
                    // Log("ADD A, C");
                    Add_R(C);
                    break;

                case 0x82:  //ADD A,D
                    // Log("ADD A, D");
                    Add_R(D);
                    break;

                case 0x83:  //ADD A,E
                    // Log("ADD A, E");
                    Add_R(E);
                    break;

                case 0x84:  //ADD A,H
                    // Log("ADD A, H");
                    Add_R(H);
                    break;

                case 0x85:  //ADD A,L
                    // Log("ADD A, L");
                    Add_R(L);
                    break;

                case 0x86:  //ADD A, (HL)
                    // Log("ADD A, (HL)");
                    Add_R(PeekByte(HL));
                    break;

                case 0x87:  //ADD A, A
                    // Log("ADD A, A");
                    Add_R(A);
                    break;

                case 0xC6:  //ADD A, n
                    disp = PeekByte(PC);
                    // Log(String.Format("ADD A, {0,-6:X}", disp));
                    Add_R(disp);
                    PC++;
                    break;
                #endregion

                #region Add to accumulator with carry (Adc A, r)
                /** Adc a, r **/
                case 0x88:  //ADC A,B
                    // Log("ADC A, B");
                    Adc_R(B);
                    break;

                case 0x89:  //ADC A,C
                    // Log("ADC A, C");
                    Adc_R(C);
                    break;

                case 0x8A:  //ADC A,D
                    // Log("ADC A, D");
                    Adc_R(D);
                    break;

                case 0x8B:  //ADC A,E
                    // Log("ADC A, E");
                    Adc_R(E);
                    break;

                case 0x8C:  //ADC A,H
                    // Log("ADC A, H");
                    Adc_R(H);
                    break;

                case 0x8D:  //ADC A,L
                    // Log("ADC A, L");
                    Adc_R(L);
                    break;

                case 0x8E:  //ADC A,(HL)
                    // Log("ADC A, (HL)");
                    Adc_R(PeekByte(HL));
                    break;

                case 0x8F:  //ADC A,A
                    // Log("ADC A, A");
                    Adc_R(A);
                    break;

                case 0xCE:  //ADC A, n
                    disp = PeekByte(PC);
                    // Log(String.Format("ADC A, {0,-6:X}", disp));
                    Adc_R(disp);
                    PC += 1;
                    break;
                #endregion

                #region 8 bit subtraction from accumulator(SUB r)
                case 0x90:  //SUB B
                    // Log("SUB B");
                    Sub_R(B);
                    break;

                case 0x91:  //SUB C
                    // Log("SUB C");
                    Sub_R(C);
                    break;

                case 0x92:  //SUB D
                    // Log("SUB D");
                    Sub_R(D);
                    break;

                case 0x93:  //SUB E
                    // Log("SUB E");
                    Sub_R(E);
                    break;

                case 0x94:  //SUB H
                    // Log("SUB H");
                    Sub_R(H);
                    break;

                case 0x95:  //SUB L
                    // Log("SUB L");
                    Sub_R(L);
                    break;

                case 0x96:  //SUB (HL)
                    // Log("SUB (HL)");
                    Sub_R(PeekByte(HL));
                    break;

                case 0x97:  //SUB A
                    // Log("SUB A");
                    Sub_R(A);
                    break;

                case 0xD6:  //SUB n
                    disp = PeekByte(PC);
                    // Log(String.Format("SUB {0,-6:X}", disp));
                    Sub_R(disp);
                    PC += 1;
                    break;
                #endregion

                #region 8 bit subtraction from accumulator with carry(SBC A, r)
                case 0x98:  //SBC A, B
                    // Log("SBC A, B");
                    Sbc_R(B);
                    break;

                case 0x99:  //SBC A, C
                    // Log("SBC A, C");
                    Sbc_R(C);
                    break;

                case 0x9A:  //SBC A, D
                    // Log("SBC A, D");
                    Sbc_R(D);
                    break;

                case 0x9B:  //SBC A, E
                    // Log("SBC A, E");
                    Sbc_R(E);
                    break;

                case 0x9C:  //SBC A, H
                    // Log("SBC A, H");
                    Sbc_R(H);
                    break;

                case 0x9D:  //SBC A, L
                    // Log("SBC A, L");
                    Sbc_R(L);
                    break;

                case 0x9E:  //SBC A, (HL)
                    // Log("SBC A, (HL)");
                    Sbc_R(PeekByte(HL));
                    break;

                case 0x9F:  //SBC A, A
                    // Log("SBC A, A");
                    Sbc_R(A);
                    break;

                case 0xDE:  //SBC A, n
                    disp = PeekByte(PC);
                    // Log(String.Format("SBC A, {0,-6:X}", disp));
                    Sbc_R(disp);
                    PC += 1;
                    break;
                #endregion

                #region Relative Jumps (JR / DJNZ)
                /*** Relative Jumps ***/
                case 0x10:  //DJNZ n
                    Contend(IR, 1, 1);
                    disp = GetDisplacement(PeekByte(PC));
                    // Log(String.Format("DJNZ {0,-6:X}", PC + disp + 1));
                    B--;
                    if (B != 0) {
                        Contend(PC, 1, 5);
                        PC += disp;
                        MemPtr = PC + 1;
                    }
                    PC++;

                    break;

                case 0x18:  //JR n
                    disp = GetDisplacement(PeekByte(PC));
                    // Log(String.Format("JR {0,-6:X}", PC + disp + 1));
                    Contend(PC, 1, 5);
                    PC += disp;
                    PC++;
                    MemPtr = PC;
                    break;

                case 0x20:  //JRNZ n
                    disp = GetDisplacement(PeekByte(PC));
                    // Log(String.Format("JR NZ, {0,-6:X}", PC + disp + 1));
                    if ((F & F_ZERO) == 0) {
                        Contend(PC, 1, 5);
                        PC += disp;
                        MemPtr = PC + 1;
                    }
                    PC++;
                    break;

                case 0x28:  //JRZ n
                    disp = GetDisplacement(PeekByte(PC));
                    // Log(String.Format("JR Z, {0,-6:X}", PC + disp + 1));

                    if ((F & F_ZERO) != 0) {
                        Contend(PC, 1, 5);
                        PC += disp;
                        MemPtr = PC + 1;
                    }
                    PC++;
                    break;

                case 0x30:  //JRNC n
                    disp = GetDisplacement(PeekByte(PC));
                    // Log(String.Format("JR NC, {0,-6:X}", PC + disp + 1));

                    if ((F & F_CARRY) == 0) {
                        Contend(PC, 1, 5);
                        PC += disp;
                        MemPtr = PC + 1;
                    }
                    PC++;
                    break;

                case 0x38:  //JRC n
                    disp = GetDisplacement(PeekByte(PC));
                    // Log(String.Format("JR C, {0,-6:X}", PC + disp + 1));

                    if ((F & F_CARRY) != 0) {
                        Contend(PC, 1, 5);
                        PC += disp;
                        MemPtr = PC + 1;
                    }
                    PC++;
                    break;
                #endregion

                #region Direct jumps (JP)
                /*** Direct jumps ***/
                case 0xC2:  //JPNZ nn
                    disp = PeekWord(PC);
                    // Log(String.Format("JP NZ, {0,-6:X}", disp));
                    if ((F & F_ZERO) == 0) {
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    MemPtr = disp;
                    break;

                case 0xC3:  //JP nn
                    disp = PeekWord(PC);
                    // Log(String.Format("JP {0,-6:X}", disp));
                    PC = disp;
                    MemPtr = disp;
                    break;

                case 0xCA:  //JPZ nn
                    disp = PeekWord(PC);
                    // Log(String.Format("JP Z, {0,-6:X}", disp));
                    if ((F & F_ZERO) != 0) {
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    MemPtr = disp;
                    break;

                case 0xD2:  //JPNC nn
                    disp = PeekWord(PC);
                    // Log(String.Format("JP NC, {0,-6:X}", disp));
                    if ((F & F_CARRY) == 0) {
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    MemPtr = disp;
                    break;

                case 0xDA:  //JPC nn
                    disp = PeekWord(PC);
                    // Log(String.Format("JP C, {0,-6:X}", disp));
                    if ((F & F_CARRY) != 0) {
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    MemPtr = disp;
                    break;

                case 0xE2:  //JP PO nn
                    disp = PeekWord(PC);
                    // Log(String.Format("JP PO, {0,-6:X}", disp));
                    if ((F & F_PARITY) == 0) {
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    MemPtr = disp;
                    break;

                case 0xE9:  //JP (HL)
                    // Log("JP (HL)");
                    //PC = PeekWord(HL);
                    PC = HL;
                    //  MemPtr = PC;
                    break;

                case 0xEA:  //JP PE nn
                    disp = PeekWord(PC);
                    // Log(String.Format("JP PE, {0,-6:X}", disp));
                    if ((F & F_PARITY) != 0) {
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    MemPtr = disp;
                    break;

                case 0xF2:  //JP P nn
                    disp = PeekWord(PC);
                    // Log(String.Format("JP P, {0,-6:X}", disp));
                    if ((F & F_SIGN) == 0) {
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    MemPtr = disp;
                    break;

                case 0xFA:  //JP M nn
                    disp = PeekWord(PC);
                    // Log(String.Format("JP M, {0,-6:X}", disp));
                    if ((F & F_SIGN) != 0) {
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    MemPtr = disp;
                    break;
                #endregion

                #region Compare instructions (CP)
                /*** Compare instructions **/
                case 0xB8:  //CP B
                    // Log("CP B");
                    Cp_R(B);
                    break;

                case 0xB9:  //CP C
                    // Log("CP C");
                    Cp_R(C);
                    break;

                case 0xBA:  //CP D
                    // Log("CP D");
                    Cp_R(D);
                    break;

                case 0xBB:  //CP E
                    // Log("CP E");
                    Cp_R(E);
                    break;

                case 0xBC:  //CP H
                    // Log("CP H");
                    Cp_R(H);
                    break;

                case 0xBD:  //CP L
                    // Log("CP L");
                    Cp_R(L);
                    break;

                case 0xBE:  //CP (HL)
                    // Log("CP (HL)");
                    val = PeekByte(HL);
                    Cp_R(val);
                    break;

                case 0xBF:  //CP A
                    // Log("CP A");
                    Cp_R(A);
                    if (tape_readToPlay)
                        if (PC == 0x56b)
                            FlashLoadTape();
                    break;

                case 0xFE:  //CP n
                    disp = PeekByte(PC);
                    // Log(String.Format(String.Format("CP {0,-6:X}", disp)));
                    Cp_R(disp);
                    PC += 1;
                    break;
                #endregion

                #region Carry Flag operations
                /*** Carry Flag operations ***/
                case 0x37:  //SCF
                    // Log("SCF");
                    SetCarry(true);
                    SetNeg(false);
                    SetHalf(false);
                    SetF3((A & F_3) != 0);
                    SetF5((A & F_5) != 0);
                    break;

                case 0x3F:  //CCF
                    // Log("CCF");

                    SetF3((A & F_3) != 0);
                    SetF5((A & F_5) != 0);
                    SetHalf((F & F_CARRY) != 0);
                    SetNeg(false);
                    SetCarry(((F & F_CARRY) != 0) ? false : true);

                    break;
                #endregion

                #region Bitwise AND (AND r)
                case 0xA0:  //AND B
                    // Log("AND B");
                    And_R(B);
                    break;

                case 0xA1:  //AND C
                    // Log("AND C");
                    And_R(C);
                    break;

                case 0xA2:  //AND D
                    // Log("AND D");
                    And_R(D);
                    break;

                case 0xA3:  //AND E
                    // Log("AND E");
                    And_R(E);
                    break;

                case 0xA4:  //AND H
                    // Log("AND H");
                    And_R(H);
                    break;

                case 0xA5:  //AND L
                    // Log("AND L");
                    And_R(L);
                    break;

                case 0xA6:  //AND (HL)
                    // Log("AND (HL)");
                    And_R(PeekByte(HL));
                    break;

                case 0xA7:  //AND A
                    // Log("AND A");
                    And_R(A);
                    break;

                case 0xE6:  //AND n
                    disp = PeekByte(PC);
                    // Log(String.Format("AND {0,-6:X}", disp));
                    And_R(disp);

                    PC++;
                    break;
                #endregion

                #region Bitwise XOR (XOR r)
                case 0xA8: //XOR B
                    // Log("XOR B");
                    Xor_R(B);
                    break;

                case 0xA9: //XOR C
                    // Log("XOR C");
                    Xor_R(C);
                    break;

                case 0xAA: //XOR D
                    // Log("XOR D");
                    Xor_R(D);
                    break;

                case 0xAB: //XOR E
                    // Log("XOR E");
                    Xor_R(E);
                    break;

                case 0xAC: //XOR H
                    // Log("XOR H");
                    Xor_R(H);
                    break;

                case 0xAD: //XOR L
                    // Log("XOR L");
                    Xor_R(L);
                    break;

                case 0xAE: //XOR (HL)
                    // Log("XOR (HL)");
                    Xor_R(PeekByte(HL));
                    break;

                case 0xAF: //XOR A
                    // Log("XOR A");
                    Xor_R(A);
                    break;

                case 0xEE:  //XOR n
                    disp = PeekByte(PC);
                    // Log(String.Format("XOR {0,-6:X}", disp));
                    Xor_R(disp);
                    PC++;
                    break;

                #endregion

                #region Bitwise OR (OR r)
                case 0xB0:  //OR B
                    // Log("OR B");
                    Or_R(B);
                    break;

                case 0xB1:  //OR C
                    // Log("OR C");
                    Or_R(C);
                    break;

                case 0xB2:  //OR D
                    // Log("OR D");
                    Or_R(D);
                    break;

                case 0xB3:  //OR E
                    // Log("OR E");
                    Or_R(E);
                    break;

                case 0xB4:  //OR H
                    // Log("OR H");
                    Or_R(H);
                    break;

                case 0xB5:  //OR L
                    // Log("OR L");
                    Or_R(L);
                    break;

                case 0xB6:  //OR (HL)
                    // Log("OR (HL)");
                    Or_R(PeekByte(HL));
                    break;

                case 0xB7:  //OR A
                    // Log("OR A");
                    Or_R(A);
                    break;

                case 0xF6:  //OR n
                    disp = PeekByte(PC);
                    // Log(String.Format("OR {0,-6:X}", disp));
                    Or_R(disp);
                    PC++;
                    break;
                #endregion

                #region Return instructions
                case 0xC0:  //RET NZ
                    // Log("RET NZ");
                    Contend(IR, 1, 1);
                    if ((F & F_ZERO) == 0) {
                        PC = PopStack();
                        MemPtr = PC;
                    }
                    break;

                case 0xC8:  //RET Z
                    // Log("RET Z");
                    Contend(IR, 1, 1);
                    if ((F & F_ZERO) != 0) {
                        PC = PopStack();
                        MemPtr = PC;
                    }
                    break;

                case 0xC9:  //RET
                    // Log("RET");
                    PC = PopStack();
                    MemPtr = PC;
                    break;

                case 0xD0:  //RET NC
                    // Log("RET NC");
                    Contend(IR, 1, 1);
                    if ((F & F_CARRY) == 0) {
                        PC = PopStack();
                        MemPtr = PC;
                    }
                    break;

                case 0xD8:  //RET C
                    // Log("RET C");
                    Contend(IR, 1, 1);
                    if ((F & F_CARRY) != 0) {
                        PC = PopStack();
                        MemPtr = PC;
                    }
                    break;

                case 0xE0:  //RET PO
                    // Log("RET PO");
                    Contend(IR, 1, 1);
                    if ((F & F_PARITY) == 0) {
                        PC = PopStack();
                        MemPtr = PC;
                    }
                    break;

                case 0xE8:  //RET PE
                    // Log("RET PE");
                    Contend(IR, 1, 1);
                    if ((F & F_PARITY) != 0) {
                        PC = PopStack();
                        MemPtr = PC;
                    }
                    break;

                case 0xF0:  //RET P
                    // Log("RET P");
                    Contend(IR, 1, 1);
                    if ((F & F_SIGN) == 0) {
                        PC = PopStack();
                        MemPtr = PC;
                    }
                    break;

                case 0xF8:  //RET M
                    // Log("RET M");
                    Contend(IR, 1, 1);
                    if ((F & F_SIGN) != 0) {
                        PC = PopStack();
                        MemPtr = PC;
                    }
                    break;
                #endregion

                #region POP/PUSH instructions
                case 0xC1:  //POP BC
                    // Log("POP BC");
                    BC = PopStack();
                    break;

                case 0xC5:  //PUSH BC
                    // Log("PUSH BC");
                    Contend(IR, 1, 1);
                    PushStack(BC);
                    break;

                case 0xD1:  //POP DE
                    // Log("POP DE");

                    DE = PopStack();
                    break;

                case 0xD5:  //PUSH DE
                    // Log("PUSH DE");
                    Contend(IR, 1, 1);
                    PushStack(DE);
                    break;

                case 0xE1:  //POP HL
                    // Log("POP HL");
                    HL = PopStack();
                    break;

                case 0xE5:  //PUSH HL
                    // Log("PUSH HL");
                    Contend(IR, 1, 1);
                    PushStack(HL);
                    break;

                case 0xF1:  //POP AF
                    // Log("POP AF");
                    AF = PopStack();
                    break;

                case 0xF5:  //PUSH AF
                    // Log("PUSH AF");
                    Contend(IR, 1, 1);
                    PushStack(AF);
                    break;
                #endregion

                #region CALL instructions
                case 0xC4:  //CALL NZ, nn
                    disp = PeekWord(PC);
                    MemPtr = disp;
                    // Log(String.Format("CALL NZ, {0,-6:X}", disp));
                    if ((F & F_ZERO) == 0) {
                        Contend(PC + 1, 1, 1);
                        PushStack(PC + 2);
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    break;

                case 0xCC:  //CALL Z, nn
                    disp = PeekWord(PC);
                    MemPtr = disp;
                    // Log(String.Format("CALL Z, {0,-6:X}", disp));
                    if ((F & F_ZERO) != 0) {
                        Contend(PC + 1, 1, 1);
                        PushStack(PC + 2);
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    break;

                case 0xCD:  //CALL nn
                    disp = PeekWord(PC);
                    MemPtr = disp;
                    // Log(String.Format("CALL {0,-6:X}", disp));
                    Contend(PC + 1, 1, 1);
                    PushStack(PC + 2);
                    PC = disp;
                    break;

                case 0xD4:  //CALL NC, nn
                    disp = PeekWord(PC);
                    MemPtr = disp;
                    // Log(String.Format("CALL NC, {0,-6:X}", disp));
                    if ((F & F_CARRY) == 0) {
                        Contend(PC + 1, 1, 1);
                        PushStack(PC + 2);
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    break;

                case 0xDC:  //CALL C, nn
                    disp = PeekWord(PC);
                    MemPtr = disp;
                    // Log(String.Format("CALL C, {0,-6:X}", disp));
                    if ((F & F_CARRY) != 0) {
                        Contend(PC + 1, 1, 1);
                        PushStack(PC + 2);
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    break;

                case 0xE4:  //CALL PO, nn
                    disp = PeekWord(PC);
                    MemPtr = disp;
                    // Log(String.Format("CALL PO, {0,-6:X}", disp));
                    if ((F & F_PARITY) == 0) {
                        Contend(PC + 1, 1, 1);
                        PushStack(PC + 2);
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    break;

                case 0xEC:  //CALL PE, nn
                    disp = PeekWord(PC);
                    MemPtr = disp;
                    // Log(String.Format("CALL PE, {0,-6:X}", disp));
                    if ((F & F_PARITY) != 0) {
                        Contend(PC + 1, 1, 1);
                        PushStack(PC + 2);
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    break;

                case 0xF4:  //CALL P, nn
                    disp = PeekWord(PC);
                    MemPtr = disp;
                    // Log(String.Format("CALL P, {0,-6:X}", disp));
                    if ((F & F_SIGN) == 0) {
                        Contend(PC + 1, 1, 1);
                        PushStack(PC + 2);
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    break;

                case 0xFC:  //CALL M, nn
                    disp = PeekWord(PC);
                    MemPtr = disp;
                    // Log(String.Format("CALL M, {0,-6:X}", disp));
                    if ((F & F_SIGN) != 0) {
                        Contend(PC + 1, 1, 1);
                        PushStack(PC + 2);
                        PC = disp;
                    } else {
                        PC += 2;
                    }
                    break;
                #endregion

                #region Restart instructions (RST n)
                case 0xC7:  //RST 0x00
                    // Log("RST 00");
                    Contend(IR, 1, 1);
                    PushStack(PC);
                    PC = 0x00;
                    MemPtr = PC;
                    break;

                case 0xCF:  //RST 0x08
                    // Log("RST 08");
                    Contend(IR, 1, 1);
                    PushStack(PC);
                    PC = 0x08;
                    MemPtr = PC;
                    break;

                case 0xD7:  //RST 0x10
                    // Log("RST 10");
                    Contend(IR, 1, 1);
                    PushStack(PC);
                    PC = 0x10;
                    MemPtr = PC;
                    break;

                case 0xDF:  //RST 0x18
                    // Log("RST 18");
                    Contend(IR, 1, 1);
                    PushStack(PC);
                    PC = 0x18;
                    MemPtr = PC;
                    break;

                case 0xE7:  //RST 0x20
                    // Log("RST 20");
                    Contend(IR, 1, 1);
                    PushStack(PC);
                    PC = 0x20;
                    MemPtr = PC;
                    break;

                case 0xEF:  //RST 0x28
                    // Log("RST 28");
                    Contend(IR, 1, 1);
                    PushStack(PC);
                    PC = 0x28;
                    MemPtr = PC;
                    break;

                case 0xF7:  //RST 0x30
                    // Log("RST 30");
                    Contend(IR, 1, 1);
                    PushStack(PC);
                    PC = 0x30;
                    MemPtr = PC;
                    break;

                case 0xFF:  //RST 0x38
                    // Log("RST 38");
                    Contend(IR, 1, 1);
                    PushStack(PC);
                    PC = 0x38;
                    MemPtr = PC;
                    break;
                #endregion

                #region IN A, (n)
                case 0xDB:  //IN A, (n)
                    
                    disp = PeekByte(PC);
                    int port = (A << 8) | disp;
                    if (((disp & 0x1) == 0) &&  and_32_Or_64)
                        CheckEdgeLoader2();
          
                    // Log(String.Format("IN A, ({0:X})", disp));
                    MemPtr = (A << 8) + disp + 1;
                    A = In(port);
                    //Auto-load tape routine kicks in here
                    //ULA port?
   
                    and_32_Or_64 = false;
                    PC++;
                    break;
                #endregion

                #region OUT (n), A
                case 0xD3:  //OUT (n), A
                    disp = PeekByte(PC);
                    // Log(String.Format("OUT ({0:X}), A", disp));
                    Out(disp | (A << 8), A);
                    MemPtr = ((disp + 1) & 0xff) | (A << 8);
                    PC++;
                    break;
                #endregion

                #region Decimal Adjust Accumulator (DAA)
                case 0x27:  //DAA
                    // Log("DAA");
                    DAA();
                    break;
                #endregion

                #region Complement (CPL)
                case 0x2f:  //CPL
                    // Log("CPL");
                    A = A ^ 0xff;
                    SetF3((A & F_3) != 0);
                    SetF5((A & F_5) != 0);
                    SetNeg(true);
                    SetHalf(true);
                    break;
                #endregion

                #region Halt (HALT) - TO BE CHECKED!
                case 0x76:  //HALT
                    // Log("HALT");
                    HaltOn = true;
                    PC--;
                    break;
                #endregion

                #region Interrupts
                case 0xF3:  //DI
                    // Log("DI");
                    IFF1 = false;
                    IFF2 = false;
                    break;

                case 0xFB:  //EI
                    // Log("EI");
                    IFF1 = true;
                    IFF2 = true;
                    lastOpcodeWasEI = 1;

                    break;
                #endregion

                #region Opcodes with CB prefix
                case 0xCB:
                    switch (opcode = FetchInstruction()) {
                        #region Rotate instructions
                        case 0x00: //RLC B
                            // Log("RLC B");
                            B = Rlc_R(B);
                            break;

                        case 0x01: //RLC C
                            // Log("RLC C");
                            C = Rlc_R(C);
                            break;

                        case 0x02: //RLC D
                            // Log("RLC D");
                            D = Rlc_R(D);
                            break;

                        case 0x03: //RLC E
                            // Log("RLC E");
                            E = Rlc_R(E);
                            break;

                        case 0x04: //RLC H
                            // Log("RLC H");
                            H = Rlc_R(H);
                            break;

                        case 0x05: //RLC L
                            // Log("RLC L");
                            L = Rlc_R(L);
                            break;

                        case 0x06: //RLC (HL)
                            // Log("RLC (HL)");
                            disp = PeekByte(HL);
                            Contend(HL, 1, 1);
                            PokeByte(HL, Rlc_R(disp));
                            break;

                        case 0x07: //RLC A
                            // Log("RLC A");
                            A = Rlc_R(A);
                            break;

                        case 0x08: //RRC B
                            // Log("RRC B");
                            B = Rrc_R(B);
                            break;

                        case 0x09: //RRC C
                            // Log("RRC C");
                            C = Rrc_R(C);
                            break;

                        case 0x0A: //RRC D
                            // Log("RRC D");
                            D = Rrc_R(D);
                            break;

                        case 0x0B: //RRC E
                            // Log("RRC E");
                            E = Rrc_R(E);
                            break;

                        case 0x0C: //RRC H
                            // Log("RRC H");
                            H = Rrc_R(H);
                            break;

                        case 0x0D: //RRC L
                            // Log("RRC L");
                            L = Rrc_R(L);
                            break;

                        case 0x0E: //RRC (HL)
                            // Log("RRC (HL)");
                            disp = PeekByte(HL);
                            Contend(HL, 1, 1);
                            PokeByte(HL, Rrc_R(disp));
                            break;

                        case 0x0F: //RRC A
                            // Log("RRC A");
                            A = Rrc_R(A);
                            break;

                        case 0x10: //RL B
                            // Log("RL B");
                            B = Rl_R(B);
                            break;

                        case 0x11: //RL C
                            // Log("RL C");
                            C = Rl_R(C);
                            break;

                        case 0x12: //RL D
                            // Log("RL D");
                            D = Rl_R(D);
                            break;

                        case 0x13: //RL E
                            // Log("RL E");
                            E = Rl_R(E);
                            break;

                        case 0x14: //RL H
                            // Log("RL H");
                            H = Rl_R(H);
                            break;

                        case 0x15: //RL L
                            // Log("RL L");
                            L = Rl_R(L);
                            break;

                        case 0x16: //RL (HL)
                            // Log("RL (HL)");
                            disp = PeekByte(HL);
                            Contend(HL, 1, 1);
                            PokeByte(HL, Rl_R(disp));
                            break;

                        case 0x17: //RL A
                            // Log("RL A");
                            A = Rl_R(A);
                            break;

                        case 0x18: //RR B
                            // Log("RR B");
                            B = Rr_R(B);
                            break;

                        case 0x19: //RR C
                            // Log("RR C");
                            C = Rr_R(C);
                            break;

                        case 0x1A: //RR D
                            // Log("RR D");
                            D = Rr_R(D);
                            break;

                        case 0x1B: //RR E
                            // Log("RR E");
                            E = Rr_R(E);
                            break;

                        case 0x1C: //RR H
                            // Log("RR H");
                            H = Rr_R(H);
                            break;

                        case 0x1D: //RR L
                            // Log("RR L");
                            L = Rr_R(L);
                            break;

                        case 0x1E: //RR (HL)
                            // Log("RR (HL)");
                            disp = PeekByte(HL);
                            Contend(HL, 1, 1);
                            PokeByte(HL, Rr_R(disp));
                            break;

                        case 0x1F: //RR A
                            // Log("RR A");
                            A = Rr_R(A);
                            break;
                        #endregion

                        #region Register shifts
                        case 0x20:  //SLA B
                            // Log("SLA B");
                            B = Sla_R(B);
                            break;

                        case 0x21:  //SLA C
                            // Log("SLA C");
                            C = Sla_R(C);
                            break;

                        case 0x22:  //SLA D
                            // Log("SLA D");
                            D = Sla_R(D);
                            break;

                        case 0x23:  //SLA E
                            // Log("SLA E");
                            E = Sla_R(E);
                            break;

                        case 0x24:  //SLA H
                            // Log("SLA H");
                            H = Sla_R(H);
                            break;

                        case 0x25:  //SLA L
                            // Log("SLA L");
                            L = Sla_R(L);
                            break;

                        case 0x26:  //SLA (HL)
                            // Log("SLA (HL)");
                            disp = PeekByte(HL);
                            Contend(HL, 1, 1);
                            PokeByte(HL, Sla_R(disp));
                            break;

                        case 0x27:  //SLA A
                            // Log("SLA A");
                            A = Sla_R(A);
                            break;

                        case 0x28:  //SRA B
                            // Log("SRA B");
                            B = Sra_R(B);
                            break;

                        case 0x29:  //SRA C
                            // Log("SRA C");
                            C = Sra_R(C);
                            break;

                        case 0x2A:  //SRA D
                            // Log("SRA D");
                            D = Sra_R(D);
                            break;

                        case 0x2B:  //SRA E
                            // Log("SRA E");
                            E = Sra_R(E);
                            break;

                        case 0x2C:  //SRA H
                            // Log("SRA H");
                            H = Sra_R(H);
                            break;

                        case 0x2D:  //SRA L
                            // Log("SRA L");
                            L = Sra_R(L);
                            break;

                        case 0x2E:  //SRA (HL)
                            // Log("SRA (HL)");
                            disp = PeekByte(HL);
                            Contend(HL, 1, 1);
                            PokeByte(HL, Sra_R(disp));
                            break;

                        case 0x2F:  //SRA A
                            // Log("SRA A");
                            A = Sra_R(A);
                            break;

                        case 0x30:  //SLL B
                            // Log("SLL B");
                            B = Sll_R(B);
                            break;

                        case 0x31:  //SLL C
                            // Log("SLL C");
                            C = Sll_R(C);
                            break;

                        case 0x32:  //SLL D
                            // Log("SLL D");
                            D = Sll_R(D);
                            break;

                        case 0x33:  //SLL E
                            // Log("SLL E");
                            E = Sll_R(E);
                            break;

                        case 0x34:  //SLL H
                            // Log("SLL H");
                            H = Sll_R(H);
                            break;

                        case 0x35:  //SLL L
                            // Log("SLL L");
                            L = Sll_R(L);
                            break;

                        case 0x36:  //SLL (HL)
                            // Log("SLL (HL)");
                            disp = PeekByte(HL);
                            Contend(HL, 1, 1);
                            PokeByte(HL, Sll_R(disp));
                            break;

                        case 0x37:  //SLL A
                            // Log("SLL A");
                            //tstates += 8;
                            A = Sll_R(A);
                            break;

                        case 0x38:  //SRL B
                            // Log("SRL B");
                            //tstates += 8;
                            B = Srl_R(B);
                            break;

                        case 0x39:  //SRL C
                            // Log("SRL C");
                            //tstates += 8;
                            C = Srl_R(C);
                            break;

                        case 0x3A:  //SRL D
                            // Log("SRL D");
                            //tstates += 8;
                            D = Srl_R(D);
                            break;

                        case 0x3B:  //SRL E
                            // Log("SRL E");
                            //tstates += 8;
                            E = Srl_R(E);
                            break;

                        case 0x3C:  //SRL H
                            // Log("SRL H");
                            //tstates += 8;
                            H = Srl_R(H);
                            break;

                        case 0x3D:  //SRL L
                            // Log("SRL L");
                            //tstates += 8;
                            L = Srl_R(L);
                            break;

                        case 0x3E:  //SRL (HL)
                            // Log("SRL (HL)");
                            disp = PeekByte(HL);
                            Contend(HL, 1, 1);
                            PokeByte(HL, Srl_R(disp));
                            break;

                        case 0x3F:  //SRL A
                            // Log("SRL A");
                            A = Srl_R(A);
                            break;
                        #endregion

                        #region Bit test operation (BIT b, r)
                        case 0x40:  //BIT 0, B
                            // Log("BIT 0, B");
                            Bit_R(0, B);
                            break;

                        case 0x41:  //BIT 0, C
                            // Log("BIT 0, C");
                            Bit_R(0, C);
                            break;

                        case 0x42:  //BIT 0, D
                            // Log("BIT 0, D");
                            Bit_R(0, D);
                            break;

                        case 0x43:  //BIT 0, E
                            // Log("BIT 0, E");
                            Bit_R(0, E);
                            break;

                        case 0x44:  //BIT 0, H
                            // Log("BIT 0, H");
                            Bit_R(0, H);
                            break;

                        case 0x45:  //BIT 0, L
                            // Log("BIT 0, L");
                            Bit_R(0, L);
                            break;

                        case 0x46:  //BIT 0, (HL)
                            // Log("BIT 0, (HL)");
                            Bit_R(0, PeekByte(HL));
                            Contend(HL, 1, 1);
                            SetF3((MemPtr & MEMPTR_11) != 0);
                            SetF5((MemPtr & MEMPTR_13) != 0);
                            break;

                        case 0x47:  //BIT 0, A
                            // Log("BIT 0, A");
                            Bit_R(0, A);
                            break;

                        case 0x48:  //BIT 1, B
                            // Log("BIT 1, B");
                            Bit_R(1, B);
                            break;

                        case 0x49:  //BIT 1, C
                            // Log("BIT 1, C");
                            Bit_R(1, C);
                            break;

                        case 0x4A:  //BIT 1, D
                            // Log("BIT 1, D");
                            Bit_R(1, D);
                            break;

                        case 0x4B:  //BIT 1, E
                            // Log("BIT 1, E");
                            Bit_R(1, E);
                            break;

                        case 0x4C:  //BIT 1, H
                            // Log("BIT 1, H");
                            Bit_R(1, H);
                            break;

                        case 0x4D:  //BIT 1, L
                            // Log("BIT 1, L");
                            Bit_R(1, L);
                            break;

                        case 0x4E:  //BIT 1, (HL)
                            // Log("BIT 1, (HL)");
                            Bit_R(1, PeekByte(HL));
                            Contend(HL, 1, 1);
                            SetF3((MemPtr & MEMPTR_11) != 0);
                            SetF5((MemPtr & MEMPTR_13) != 0);
                            break;

                        case 0x4F:  //BIT 1, A
                            // Log("BIT 1, A");
                            Bit_R(1, A);
                            break;

                        case 0x50:  //BIT 2, B
                            // Log("BIT 2, B");
                            Bit_R(2, B);
                            break;

                        case 0x51:  //BIT 2, C
                            // Log("BIT 2, C");
                            Bit_R(2, C);
                            break;

                        case 0x52:  //BIT 2, D
                            // Log("BIT 2, D");
                            Bit_R(2, D);
                            break;

                        case 0x53:  //BIT 2, E
                            // Log("BIT 2, E");
                            Bit_R(2, E);
                            break;

                        case 0x54:  //BIT 2, H
                            // Log("BIT 2, H");
                            Bit_R(2, H);
                            break;

                        case 0x55:  //BIT 2, L
                            // Log("BIT 2, L");
                            Bit_R(2, L);
                            break;

                        case 0x56:  //BIT 2, (HL)
                            // Log("BIT 2, (HL)");
                            Bit_R(2, PeekByte(HL));
                            Contend(HL, 1, 1);
                            SetF3((MemPtr & MEMPTR_11) != 0);
                            SetF5((MemPtr & MEMPTR_13) != 0);
                            break;

                        case 0x57:  //BIT 2, A
                            // Log("BIT 2, A");
                            Bit_R(2, A);
                            break;

                        case 0x58:  //BIT 3, B
                            // Log("BIT 3, B");
                            Bit_R(3, B);
                            break;

                        case 0x59:  //BIT 3, C
                            // Log("BIT 3, C");
                            Bit_R(3, C);
                            break;

                        case 0x5A:  //BIT 3, D
                            // Log("BIT 3, D");
                            Bit_R(3, D);
                            break;

                        case 0x5B:  //BIT 3, E
                            // Log("BIT 3, E");
                            Bit_R(3, E);
                            break;

                        case 0x5C:  //BIT 3, H
                            // Log("BIT 3, H");
                            Bit_R(3, H);
                            break;

                        case 0x5D:  //BIT 3, L
                            // Log("BIT 3, L");
                            Bit_R(3, L);
                            break;

                        case 0x5E:  //BIT 3, (HL)
                            // Log("BIT 3, (HL)");
                            Bit_R(3, PeekByte(HL));
                            Contend(HL, 1, 1);
                            SetF3((MemPtr & MEMPTR_11) != 0);
                            SetF5((MemPtr & MEMPTR_13) != 0);
                            break;

                        case 0x5F:  //BIT 3, A
                            // Log("BIT 3, A");
                            Bit_R(3, A);
                            break;

                        case 0x60:  //BIT 4, B
                            // Log("BIT 4, B");
                            Bit_R(4, B);
                            break;

                        case 0x61:  //BIT 4, C
                            // Log("BIT 4, C");
                            Bit_R(4, C);
                            break;

                        case 0x62:  //BIT 4, D
                            // Log("BIT 4, D");
                            Bit_R(4, D);
                            break;

                        case 0x63:  //BIT 4, E
                            // Log("BIT 4, E");
                            Bit_R(4, E);
                            break;

                        case 0x64:  //BIT 4, H
                            // Log("BIT 4, H");
                            Bit_R(4, H);
                            break;

                        case 0x65:  //BIT 4, L
                            // Log("BIT 4, L");
                            Bit_R(4, L);
                            break;

                        case 0x66:  //BIT 4, (HL)
                            // Log("BIT 4, (HL)");
                            Bit_R(4, PeekByte(HL));
                            Contend(HL, 1, 1);
                            SetF3((MemPtr & MEMPTR_11) != 0);
                            SetF5((MemPtr & MEMPTR_13) != 0);
                            break;

                        case 0x67:  //BIT 4, A
                            // Log("BIT 4, A");
                            Bit_R(4, A);
                            break;

                        case 0x68:  //BIT 5, B
                            // Log("BIT 5, B");
                            Bit_R(5, B);
                            break;

                        case 0x69:  //BIT 5, C
                            // Log("BIT 5, C");
                            Bit_R(5, C);
                            break;

                        case 0x6A:  //BIT 5, D
                            // Log("BIT 5, D");
                            Bit_R(5, D);
                            break;

                        case 0x6B:  //BIT 5, E
                            // Log("BIT 5, E");
                            Bit_R(5, E);
                            break;

                        case 0x6C:  //BIT 5, H
                            // Log("BIT 5, H");
                            Bit_R(5, H);
                            break;

                        case 0x6D:  //BIT 5, L
                            // Log("BIT 5, L");
                            Bit_R(5, L);
                            break;

                        case 0x6E:  //BIT 5, (HL)
                            // Log("BIT 5, (HL)");
                            Bit_R(5, PeekByte(HL));
                            Contend(HL, 1, 1);
                            SetF3((MemPtr & MEMPTR_11) != 0);
                            SetF5((MemPtr & MEMPTR_13) != 0);
                            break;

                        case 0x6F:  //BIT 5, A
                            // Log("BIT 5, A");
                            Bit_R(5, A);
                            break;

                        case 0x70:  //BIT 6, B
                            // Log("BIT 6, B");
                            Bit_R(6, B);
                            break;

                        case 0x71:  //BIT 6, C
                            // Log("BIT 6, C");
                            Bit_R(6, C);
                            break;

                        case 0x72:  //BIT 6, D
                            // Log("BIT 6, D");
                            Bit_R(6, D);
                            break;

                        case 0x73:  //BIT 6, E
                            // Log("BIT 6, E");
                            Bit_R(6, E);
                            break;

                        case 0x74:  //BIT 6, H
                            // Log("BIT 6, H");
                            Bit_R(6, H);
                            break;

                        case 0x75:  //BIT 6, L
                            // Log("BIT 6, L");
                            Bit_R(6, L);
                            break;

                        case 0x76:  //BIT 6, (HL)
                            // Log("BIT 6, (HL)");
                            Bit_R(6, PeekByte(HL));
                            Contend(HL, 1, 1);
                            SetF3((MemPtr & MEMPTR_11) != 0);
                            SetF5((MemPtr & MEMPTR_13) != 0);
                            break;

                        case 0x77:  //BIT 6, A
                            // Log("BIT 6, A");
                            Bit_R(6, A);
                            break;

                        case 0x78:  //BIT 7, B
                            // Log("BIT 7, B");
                            Bit_R(7, B);
                            break;

                        case 0x79:  //BIT 7, C
                            // Log("BIT 7, C");
                            Bit_R(7, C);
                            break;

                        case 0x7A:  //BIT 7, D
                            // Log("BIT 7, D");
                            Bit_R(7, D);
                            break;

                        case 0x7B:  //BIT 7, E
                            // Log("BIT 7, E");
                            Bit_R(7, E);
                            break;

                        case 0x7C:  //BIT 7, H
                            // Log("BIT 7, H");
                            Bit_R(7, H);
                            break;

                        case 0x7D:  //BIT 7, L
                            // Log("BIT 7, L");
                            Bit_R(7, L);
                            break;

                        case 0x7E:  //BIT 7, (HL)
                            // Log("BIT 7, (HL)");
                            Bit_R(7, PeekByte(HL));
                            Contend(HL, 1, 1);
                            SetF3((MemPtr & MEMPTR_11) != 0);
                            SetF5((MemPtr & MEMPTR_13) != 0);
                            break;

                        case 0x7F:  //BIT 7, A
                            // Log("BIT 7, A");
                            Bit_R(7, A);
                            break;
                        #endregion

                        #region Reset bit operation (RES b, r)
                        case 0x80:  //RES 0, B
                            // Log("RES 0, B");
                            B = Res_R(0, B);
                            break;

                        case 0x81:  //RES 0, C
                            // Log("RES 0, C");
                            C = Res_R(0, C);
                            break;

                        case 0x82:  //RES 0, D
                            // Log("RES 0, D");
                            D = Res_R(0, D);
                            break;

                        case 0x83:  //RES 0, E
                            // Log("RES 0, E");
                            E = Res_R(0, E);
                            break;

                        case 0x84:  //RES 0, H
                            // Log("RES 0, H");
                            H = Res_R(0, H);
                            break;

                        case 0x85:  //RES 0, L
                            // Log("RES 0, L");
                            L = Res_R(0, L);
                            break;

                        case 0x86:  //RES 0, (HL)
                            disp = PeekByte(HL);
                            // Log("RES 0, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Res_R(0, disp));
                            break;

                        case 0x87:  //RES 0, A
                            // Log("RES 0, A");
                            A = Res_R(0, A);
                            break;

                        case 0x88:  //RES 1, B
                            // Log("RES 1, B");
                            B = Res_R(1, B);
                            break;

                        case 0x89:  //RES 1, C
                            // Log("RES 1, C");
                            C = Res_R(1, C);
                            break;

                        case 0x8A:  //RES 1, D
                            // Log("RES 1, D");
                            D = Res_R(1, D);
                            break;

                        case 0x8B:  //RES 1, E
                            // Log("RES 1, E");
                            E = Res_R(1, E);
                            break;

                        case 0x8C:  //RES 1, H
                            // Log("RES 1, H");
                            //tstates += 8;
                            H = Res_R(1, H);
                            break;

                        case 0x8D:  //RES 1, L
                            // Log("RES 1, L");
                            L = Res_R(1, L);
                            break;

                        case 0x8E:  //RES 1, (HL)
                            disp = PeekByte(HL);
                            // Log("RES 1, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Res_R(1, disp));
                            break;

                        case 0x8F:  //RES 1, A
                            // Log("RES 1, A");
                            A = Res_R(1, A);
                            break;

                        case 0x90:  //RES 2, B
                            // Log("RES 2, B");
                            B = Res_R(2, B);
                            break;

                        case 0x91:  //RES 2, C
                            // Log("RES 2, C");
                            C = Res_R(2, C);
                            break;

                        case 0x92:  //RES 2, D
                            // Log("RES 2, D");
                            D = Res_R(2, D);
                            break;

                        case 0x93:  //RES 2, E
                            // Log("RES 2, E");
                            E = Res_R(2, E);
                            break;

                        case 0x94:  //RES 2, H
                            // Log("RES 2, H");
                            H = Res_R(2, H);
                            break;

                        case 0x95:  //RES 2, L
                            // Log("RES 2, L");
                            L = Res_R(2, L);
                            break;

                        case 0x96:  //RES 2, (HL)
                            disp = PeekByte(HL);
                            // Log("RES 2, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Res_R(2, disp));
                            break;

                        case 0x97:  //RES 2, A
                            // Log("RES 2, A");
                            A = Res_R(2, A);
                            break;

                        case 0x98:  //RES 3, B
                            // Log("RES 3, B");
                            B = Res_R(3, B);
                            break;

                        case 0x99:  //RES 3, C
                            // Log("RES 3, C");
                            C = Res_R(3, C);
                            break;

                        case 0x9A:  //RES 3, D
                            // Log("RES 3, D");
                            D = Res_R(3, D);
                            break;

                        case 0x9B:  //RES 3, E
                            // Log("RES 3, E");
                            E = Res_R(3, E);
                            break;

                        case 0x9C:  //RES 3, H
                            // Log("RES 3, H");
                            H = Res_R(3, H);
                            break;

                        case 0x9D:  //RES 3, L
                            // Log("RES 3, L");
                            L = Res_R(3, L);
                            break;

                        case 0x9E:  //RES 3, (HL)
                            disp = PeekByte(HL);
                            // Log("RES 3, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Res_R(3, disp));
                            break;

                        case 0x9F:  //RES 3, A
                            // Log("RES 3, A");
                            A = Res_R(3, A);
                            break;

                        case 0xA0:  //RES 4, B
                            // Log("RES 4, B");
                            B = Res_R(4, B);
                            break;

                        case 0xA1:  //RES 4, C
                            // Log("RES 4, C");
                            C = Res_R(4, C);
                            break;

                        case 0xA2:  //RES 4, D
                            // Log("RES 4, D");
                            D = Res_R(4, D);
                            break;

                        case 0xA3:  //RES 4, E
                            // Log("RES 4, E");
                            E = Res_R(4, E);
                            break;

                        case 0xA4:  //RES 4, H
                            // Log("RES 4, H");
                            H = Res_R(4, H);
                            break;

                        case 0xA5:  //RES 4, L
                            // Log("RES 4, L");
                            L = Res_R(4, L);
                            break;

                        case 0xA6:  //RES 4, (HL)
                            disp = PeekByte(HL);
                            // Log("RES 4, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Res_R(4, disp));
                            break;

                        case 0xA7:  //RES 4, A
                            // Log("RES 4, A");
                            A = Res_R(4, A);
                            break;

                        case 0xA8:  //RES 5, B
                            // Log("RES 5, B");
                            B = Res_R(5, B);
                            break;

                        case 0xA9:  //RES 5, C
                            // Log("RES 5, C");
                            C = Res_R(5, C);
                            break;

                        case 0xAA:  //RES 5, D
                            // Log("RES 5, D");
                            D = Res_R(5, D);
                            break;

                        case 0xAB:  //RES 5, E
                            // Log("RES 5, E");
                            E = Res_R(5, E);
                            break;

                        case 0xAC:  //RES 5, H
                            // Log("RES 5, H");
                            H = Res_R(5, H);
                            break;

                        case 0xAD:  //RES 5, L
                            // Log("RES 5, L");
                            L = Res_R(5, L);
                            break;

                        case 0xAE:  //RES 5, (HL)
                            disp = PeekByte(HL);
                            // Log("RES 5, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Res_R(5, disp));
                            break;

                        case 0xAF:  //RES 5, A
                            // Log("RES 5, A");
                            A = Res_R(5, A);
                            break;

                        case 0xB0:  //RES 6, B
                            // Log("RES 6, B");
                            B = Res_R(6, B);
                            break;

                        case 0xB1:  //RES 6, C
                            // Log("RES 6, C");
                            C = Res_R(6, C);
                            break;

                        case 0xB2:  //RES 6, D
                            // Log("RES 6, D");
                            D = Res_R(6, D);
                            break;

                        case 0xB3:  //RES 6, E
                            // Log("RES 6, E");
                            E = Res_R(6, E);
                            break;

                        case 0xB4:  //RES 6, H
                            // Log("RES 6, H");
                            H = Res_R(6, H);
                            break;

                        case 0xB5:  //RES 6, L
                            // Log("RES 6, L");
                            L = Res_R(6, L);
                            break;

                        case 0xB6:  //RES 6, (HL)
                            disp = PeekByte(HL);
                            // Log("RES 6, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Res_R(6, disp));
                            break;

                        case 0xB7:  //RES 6, A
                            // Log("RES 6, A");
                            A = Res_R(6, A);
                            break;

                        case 0xB8:  //RES 7, B
                            // Log("RES 7, B");
                            B = Res_R(7, B);
                            break;

                        case 0xB9:  //RES 7, C
                            // Log("RES 7, C");
                            C = Res_R(7, C);
                            break;

                        case 0xBA:  //RES 7, D
                            // Log("RES 7, D");
                            D = Res_R(7, D);
                            break;

                        case 0xBB:  //RES 7, E
                            // Log("RES 7, E");
                            E = Res_R(7, E);
                            break;

                        case 0xBC:  //RES 7, H
                            // Log("RES 7, H");
                            H = Res_R(7, H);
                            break;

                        case 0xBD:  //RES 7, L
                            // Log("RES 7, L");
                            L = Res_R(7, L);
                            break;

                        case 0xBE:  //RES 7, (HL)
                            disp = PeekByte(HL);
                            // Log("RES 7, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Res_R(7, disp));
                            break;

                        case 0xBF:  //RES 7, A
                            // Log("RES 7, A");
                            A = Res_R(7, A);
                            break;
                        #endregion

                        #region Set bit operation (SET b, r)
                        case 0xC0:  //SET 0, B
                            // Log("SET 0, B");
                            B = Set_R(0, B);
                            break;

                        case 0xC1:  //SET 0, C
                            // Log("SET 0, C");
                            C = Set_R(0, C);
                            break;

                        case 0xC2:  //SET 0, D
                            // Log("SET 0, D");
                            D = Set_R(0, D);
                            break;

                        case 0xC3:  //SET 0, E
                            // Log("SET 0, E");
                            E = Set_R(0, E);
                            break;

                        case 0xC4:  //SET 0, H
                            // Log("SET 0, H");
                            H = Set_R(0, H);
                            break;

                        case 0xC5:  //SET 0, L
                            // Log("SET 0, L");
                            L = Set_R(0, L);
                            break;

                        case 0xC6:  //SET 0, (HL)
                            disp = PeekByte(HL);
                            // Log("SET 0, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Set_R(0, disp));
                            break;

                        case 0xC7:  //SET 0, A
                            // Log("SET 0, A");
                            A = Set_R(0, A);
                            break;

                        case 0xC8:  //SET 1, B
                            // Log("SET 1, B");
                            B = Set_R(1, B);
                            break;

                        case 0xC9:  //SET 1, C
                            // Log("SET 1, C");
                            C = Set_R(1, C);
                            break;

                        case 0xCA:  //SET 1, D
                            // Log("SET 1, D");
                            D = Set_R(1, D);
                            break;

                        case 0xCB:  //SET 1, E
                            // Log("SET 1, E");
                            E = Set_R(1, E);
                            break;

                        case 0xCC:  //SET 1, H
                            // Log("SET 1, H");
                            H = Set_R(1, H);
                            break;

                        case 0xCD:  //SET 1, L
                            // Log("SET 1, L");
                            L = Set_R(1, L);
                            break;

                        case 0xCE:  //SET 1, (HL)
                            disp = PeekByte(HL);
                            // Log("SET 1, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Set_R(1, disp));
                            break;

                        case 0xCF:  //SET 1, A
                            // Log("SET 1, A");
                            A = Set_R(1, A);
                            break;

                        case 0xD0:  //SET 2, B
                            // Log("SET 2, B");
                            B = Set_R(2, B);
                            break;

                        case 0xD1:  //SET 2, C
                            // Log("SET 2, C");
                            C = Set_R(2, C);
                            break;

                        case 0xD2:  //SET 2, D
                            // Log("SET 2, D");
                            D = Set_R(2, D);
                            break;

                        case 0xD3:  //SET 2, E
                            // Log("SET 2, E");
                            E = Set_R(2, E);
                            break;

                        case 0xD4:  //SET 2, H
                            // Log("SET 2, H");
                            H = Set_R(2, H);
                            break;

                        case 0xD5:  //SET 2, L
                            // Log("SET 2, L");
                            L = Set_R(2, L);
                            break;

                        case 0xD6:  //SET 2, (HL)
                            disp = PeekByte(HL);
                            // Log("SET 2, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Set_R(2, disp));
                            break;

                        case 0xD7:  //SET 2, A
                            // Log("SET 2, A");
                            A = Set_R(2, A);
                            break;

                        case 0xD8:  //SET 3, B
                            // Log("SET 3, B");
                            B = Set_R(3, B);
                            break;

                        case 0xD9:  //SET 3, C
                            // Log("SET 3, C");
                            C = Set_R(3, C);
                            break;

                        case 0xDA:  //SET 3, D
                            // Log("SET 3, D");
                            D = Set_R(3, D);
                            break;

                        case 0xDB:  //SET 3, E
                            // Log("SET 3, E");
                            E = Set_R(3, E);
                            break;

                        case 0xDC:  //SET 3, H
                            // Log("SET 3, H");
                            H = Set_R(3, H);
                            break;

                        case 0xDD:  //SET 3, L
                            // Log("SET 3, L");
                            L = Set_R(3, L);
                            break;

                        case 0xDE:  //SET 3, (HL)
                            disp = PeekByte(HL);
                            // Log("SET 3, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Set_R(3, disp));
                            break;

                        case 0xDF:  //SET 3, A
                            // Log("SET 3, A");
                            A = Set_R(3, A);
                            break;

                        case 0xE0:  //SET 4, B
                            // Log("SET 4, B");
                            B = Set_R(4, B);
                            break;

                        case 0xE1:  //SET 4, C
                            // Log("SET 4, C");
                            C = Set_R(4, C);
                            break;

                        case 0xE2:  //SET 4, D
                            // Log("SET 4, D");
                            D = Set_R(4, D);
                            break;

                        case 0xE3:  //SET 4, E
                            // Log("SET 4, E");
                            E = Set_R(4, E);
                            break;

                        case 0xE4:  //SET 4, H
                            // Log("SET 4, H");
                            H = Set_R(4, H);
                            break;

                        case 0xE5:  //SET 4, L
                            // Log("SET 4, L");
                            L = Set_R(4, L);
                            break;

                        case 0xE6:  //SET 4, (HL)
                            disp = PeekByte(HL);
                            // Log("SET 4, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Set_R(4, disp));
                            break;

                        case 0xE7:  //SET 4, A
                            // Log("SET 4, A");
                            A = Set_R(4, A);
                            break;

                        case 0xE8:  //SET 5, B
                            // Log("SET 5, B");
                            B = Set_R(5, B);
                            break;

                        case 0xE9:  //SET 5, C
                            // Log("SET 5, C");
                            C = Set_R(5, C);
                            break;

                        case 0xEA:  //SET 5, D
                            // Log("SET 5, D");
                            D = Set_R(5, D);
                            break;

                        case 0xEB:  //SET 5, E
                            // Log("SET 5, E");
                            E = Set_R(5, E);
                            break;

                        case 0xEC:  //SET 5, H
                            // Log("SET 5, H");
                            H = Set_R(5, H);
                            break;

                        case 0xED:  //SET 5, L
                            // Log("SET 5, L");
                            L = Set_R(5, L);
                            break;

                        case 0xEE:  //SET 5, (HL)
                            disp = PeekByte(HL);
                            // Log("SET 5, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Set_R(5, disp));
                            break;

                        case 0xEF:  //SET 5, A
                            // Log("SET 5, A");
                            A = Set_R(5, A);
                            break;

                        case 0xF0:  //SET 6, B
                            // Log("SET 6, B");
                            B = Set_R(6, B);
                            break;

                        case 0xF1:  //SET 6, C
                            // Log("SET 6, C");
                            C = Set_R(6, C);
                            break;

                        case 0xF2:  //SET 6, D
                            // Log("SET 6, D");
                            D = Set_R(6, D);
                            break;

                        case 0xF3:  //SET 6, E
                            // Log("SET 6, E");
                            E = Set_R(6, E);
                            break;

                        case 0xF4:  //SET 6, H
                            // Log("SET 6, H");
                            H = Set_R(6, H);
                            break;

                        case 0xF5:  //SET 6, L
                            // Log("SET 6, L");
                            L = Set_R(6, L);
                            break;

                        case 0xF6:  //SET 6, (HL)
                            disp = PeekByte(HL);
                            // Log("SET 6, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Set_R(6, disp));
                            break;

                        case 0xF7:  //SET 6, A
                            // Log("SET 6, A");
                            A = Set_R(6, A);
                            break;

                        case 0xF8:  //SET 7, B
                            // Log("SET 7, B");
                            B = Set_R(7, B);
                            break;

                        case 0xF9:  //SET 7, C
                            // Log("SET 7, C");
                            C = Set_R(7, C);
                            break;

                        case 0xFA:  //SET 7, D
                            // Log("SET 7, D");
                            D = Set_R(7, D);
                            break;

                        case 0xFB:  //SET 7, E
                            // Log("SET 7, E");
                            E = Set_R(7, E);
                            break;

                        case 0xFC:  //SET 7, H
                            // Log("SET 7, H");
                            H = Set_R(7, H);
                            break;

                        case 0xFD:  //SET 7, L
                            // Log("SET 7, L");
                            L = Set_R(7, L);
                            break;

                        case 0xFE:  //SET 7, (HL)
                            disp = PeekByte(HL);
                            // Log("SET 7, (HL)");
                            Contend(HL, 1, 1);
                            PokeByte(HL, Set_R(7, disp));
                            break;

                        case 0xFF:  //SET 7, A
                            // Log("SET 7, A");
                            A = Set_R(7, A);
                            break;
                        #endregion

                        //  default:
                        //      String msg = "ERROR: Could not handle DD " + opcode.ToString();
                        //      MessageBox.Show(msg, "Opcode handler",
                        //                  MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                        //      break;
                    }
                    break;
                #endregion

                #region Opcodes with DD prefix (includes DDCB)
                case 0xDD:
                    switch (opcode = FetchInstruction()) {
                        #region Addition instructions
                        case 0x09:  //ADD IX, BC
                            // Log("ADD IX, BC");
                            Contend(IR, 1, 7);
                            MemPtr = IX + 1;
                            IX = Add_RR(IX, BC);
                            break;

                        case 0x19:  //ADD IX, DE
                            // Log("ADD IX, DE");
                            Contend(IR, 1, 7);
                            MemPtr = IX + 1;
                            IX = Add_RR(IX, DE);
                            break;

                        case 0x29:  //ADD IX, IX
                            // Log("ADD IX, IX");
                            Contend(IR, 1, 7);
                            MemPtr = IX + 1;
                            IX = Add_RR(IX, IX);
                            break;

                        case 0x39:  //ADD IX, SP
                            // Log("ADD IX, SP");
                            Contend(IR, 1, 7);
                            MemPtr = IX + 1;
                            IX = Add_RR(IX, SP);
                            break;

                        case 0x84:  //ADD A, IXH
                            // Log("ADD A, IXH");
                            Add_R(IXH);
                            break;

                        case 0x85:  //ADD A, IXL
                            // Log("ADD A, IXL");
                            Add_R(IXL);
                            break;

                        case 0x86:  //Add A, (IX+d)
                            disp = GetDisplacement(PeekByte(PC));
                            int offset = IX + disp; //The displacement required
                            // Log(string.Format("ADD A, (IX + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            Add_R(PeekByte(offset));
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x8C:  //ADC A, IXH
                            // Log("ADC A, IXH");
                            Adc_R(IXH);
                            break;

                        case 0x8D:  //ADC A, IXL
                            // Log("ADC A, IXL");
                            Adc_R(IXL);
                            break;

                        case 0x8E: //ADC A, (IX+d)
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IX + disp; //The displacement required
                            // Log(string.Format("ADC A, (IX + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            Adc_R(PeekByte(offset));
                            MemPtr = offset;
                            PC++;
                            break;
                        #endregion

                        #region Subtraction instructions
                        case 0x94:  //SUB A, IXH
                            // Log("SUB A, IXH");
                            Sub_R(IXH);
                            break;

                        case 0x95:  //SUB A, IXL
                            // Log("SUB A, IXL");
                            Sub_R(IXL);
                            break;

                        case 0x96:  //SUB (IX + d)
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IX + disp; //The displacement required
                            // Log(string.Format("SUB (IX + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            Sub_R(PeekByte(offset));
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0x9C:  //SBC A, IXH
                            // Log("SBC A, IXH");
                            Sbc_R(IXH);
                            break;

                        case 0x9D:  //SBC A, IXL
                            // Log("SBC A, IXL");
                            Sbc_R(IXL);
                            break;

                        case 0x9E:  //SBC A, (IX + d)
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IX + disp; //The displacement required
                            // Log(string.Format("SBC A, (IX + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            MemPtr = offset;
                            Sbc_R(PeekByte(offset));
                            PC++;
                            break;
                        #endregion

                        #region Increment/Decrements
                        case 0x23:  //INC IX
                            // Log("INC IX");
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 2;
                            //else
                            Contend(IR, 1, 2);
                            IX++;
                            break;

                        case 0x24:  //INC IXH
                            // Log("INC IXH");
                            IXH = Inc(IXH);
                            break;

                        case 0x25:  //DEC IXH
                            // Log("DEC IXH");
                            IXH = Dec(IXH);
                            break;

                        case 0x2B:  //DEC IX
                            // Log("DEC IX");
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 2;
                            //else
                            Contend(IR, 1, 2);
                            IX--;
                            break;

                        case 0x2C:  //INC IXL
                            // Log("INC IXL");
                            IXL = Inc(IXL);
                            break;

                        case 0x2D:  //DEC IXL
                            // Log("DEC IXL");
                            IXL = Dec(IXL);
                            break;

                        case 0x34:  //INC (IX + d)
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IX + disp; //The displacement required
                            // Log(string.Format("INC (IX + {0:X})", disp));
                            Contend(PC, 1, 5);
                            disp = Inc(PeekByte(offset));
                            Contend(offset, 1, 1);
                            PokeByte(offset, disp);
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0x35:  //DEC (IX + d)
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IX + disp; //The displacement required
                            // Log(string.Format("DEC (IX + {0:X})", disp));
                            Contend(PC, 1, 5);
                            disp = Dec(PeekByte(offset));
                            Contend(offset, 1, 1);
                            PokeByte(offset, disp);
                            MemPtr = offset;
                            PC++;
                            break;
                        #endregion

                        #region Bitwise operators

                        case 0xA4:  //AND IXH
                            // Log("AND IXH");
                            And_R(IXH);
                            break;

                        case 0xA5:  //AND IXL
                            // Log("AND IXL");
                            And_R(IXL);
                            break;

                        case 0xA6:  //AND (IX + d)
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IX + disp; //The displacement required
                            // Log(string.Format("AND (IX + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            And_R(PeekByte(offset));
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0xAC:  //XOR IXH
                            // Log("XOR IXH");
                            Xor_R(IXH);
                            break;

                        case 0xAD:  //XOR IXL
                            // Log("XOR IXL");
                            Xor_R(IXL);
                            break;

                        case 0xAE:  //XOR (IX + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("XOR (IX + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            Xor_R(PeekByte(offset));
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0xB4:  //OR IXH
                            // Log("OR IXH");
                            Or_R(IXH);
                            break;

                        case 0xB5:  //OR IXL
                            // Log("OR IXL");
                            Or_R(IXL);
                            break;

                        case 0xB6:  //OR (IX + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("OR (IX + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            Or_R(PeekByte(offset));
                            MemPtr = offset;
                            PC++;
                            break;
                        #endregion

                        #region Compare operator
                        case 0xBC:  //CP IXH
                            // Log("CP IXH");
                            Cp_R(IXH);
                            break;

                        case 0xBD:  //CP IXL
                            // Log("CP IXL");
                            Cp_R(IXL);
                            break;

                        case 0xBE:  //CP (IX + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("CP (IX + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            Cp_R(PeekByte(offset));
                            MemPtr = offset;
                            PC++;
                            break;
                        #endregion

                        #region Load instructions
                        case 0x21:  //LD IX, nn
                            // Log(string.Format("LD IX, {0,-6:X}", PeekWord(PC)));
                            IX = PeekWord(PC);
                            PC += 2;
                            break;

                        case 0x22:  //LD (nn), IX
                            // Log(string.Format("LD ({0:X}), IX", PeekWord(PC)));
                            addr = PeekWord(PC);
                            PokeWord(addr, IX);
                            PC += 2;
                            MemPtr = addr + 1;
                            break;

                        case 0x26:  //LD IXH, n
                            // Log(string.Format("LD IXH, {0:X}", PeekByte(PC)));
                            IXH = PeekByte(PC);
                            PC++;
                            break;

                        case 0x2A:  //LD IX, (nn)
                            // Log(string.Format("LD IX, ({0:X})", PeekWord(PC)));
                            addr = PeekWord(PC);
                            IX = PeekWord(addr);
                            MemPtr = addr + 1;
                            PC += 2;
                            break;

                        case 0x2E:  //LD IXL, n
                            // Log(string.Format("LD IXL, {0:X}", PeekByte(PC)));
                            IXL = PeekByte(PC);
                            PC++;
                            break;

                        case 0x36:  //LD (IX + d), n
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("LD (IX + {0:X}), {1,-6:X}", disp, PeekByte(PC + 1)));
                            disp = PeekByte(PC + 1);
                            // if (model == MachineModel._plus3)
                            //    totalTStates += 2;
                            //else
                            Contend(PC + 1, 1, 2);

                            PokeByte(offset, disp);
                            MemPtr = offset;
                            PC += 2;
                            break;

                        case 0x44:  //LD B, IXH
                            // Log("LD B, IXH");
                            B = IXH;
                            break;

                        case 0x45:  //LD B, IXL
                            // Log("LD B, IXL");
                            B = IXL;
                            break;

                        case 0x46:  //LD B, (IX + d)
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IX + disp; //The displacement required
                            // Log(string.Format("LD B, (IX + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            // else
                            Contend(PC, 1, 5);
                            B = PeekByte(offset);
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0x4C:  //LD C, IXH
                            // Log("LD C, IXH");
                            C = IXH;
                            break;

                        case 0x4D:  //LD C, IXL
                            // Log("LD C, IXL");
                            C = IXL;
                            break;

                        case 0x4E:  //LD C, (IX + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("LD C, (IX + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            C = PeekByte(offset);
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0x54:  //LD D, IXH
                            // Log("LD D, IXH");
                            //tstates += 4;
                            D = IXH;
                            break;

                        case 0x55:  //LD D, IXL
                            // Log("LD D, IXL");
                            //tstates += 4;
                            D = IXL;
                            break;

                        case 0x56:  //LD D, (IX + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("LD D, (IX + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            D = PeekByte(offset);
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0x5C:  //LD E, IXH
                            // Log("LD E, IXH");
                            //tstates += 4;
                            E = IXH;
                            break;

                        case 0x5D:  //LD E, IXL
                            // Log("LD E, IXL");
                            //tstates += 4;
                            E = IXL;
                            break;

                        case 0x5E:  //LD E, (IX + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("LD E, (IX + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            E = PeekByte(offset);
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0x60:  //LD IXH, B
                            // Log("LD IXH, B");
                            //tstates += 4;
                            IXH = B;
                            break;

                        case 0x61:  //LD IXH, C
                            // Log("LD IXH, C");
                            //tstates += 4;
                            IXH = C;
                            break;

                        case 0x62:  //LD IXH, D
                            // Log("LD IXH, D");
                            //tstates += 4;
                            IXH = D;
                            break;

                        case 0x63:  //LD IXH, E
                            // Log("LD IXH, E");
                            //tstates += 4;
                            IXH = E;
                            break;

                        case 0x64:  //LD IXH, IXH
                            // Log("LD IXH, IXH");
                            //tstates += 4;
                            IXH = IXH;
                            break;

                        case 0x65:  //LD IXH, IXL
                            // Log("LD IXH, IXL");
                            //tstates += 4;
                            IXH = IXL;
                            break;

                        case 0x66:  //LD H, (IX + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("LD H, (IX + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            H = PeekByte(offset);
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0x67:  //LD IXH, A
                            // Log("LD IXH, A");
                            //tstates += 4;
                            IXH = A;
                            break;

                        case 0x68:  //LD IXL, B
                            // Log("LD IXL, B");
                            //tstates += 4;
                            IXL = B;
                            break;

                        case 0x69:  //LD IXL, C
                            // Log("LD IXL, C");
                            //tstates += 4;
                            IXL = C;
                            break;

                        case 0x6A:  //LD IXL, D
                            // Log("LD IXL, D");
                            //tstates += 4;
                            IXL = D;
                            break;

                        case 0x6B:  //LD IXL, E
                            // Log("LD IXL, E");
                            //tstates += 4;
                            IXL = E;
                            break;

                        case 0x6C:  //LD IXL, IXH
                            // Log("LD IXL, IXH");
                            //tstates += 4;
                            IXL = IXH;
                            break;

                        case 0x6D:  //LD IXL, IXL
                            // Log("LD IXL, IXL");
                            //tstates += 4;
                            IXL = IXL;
                            break;

                        case 0x6E:  //LD L, (IX + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("LD L, (IX + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            L = PeekByte(offset);
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0x6F:  //LD IXL, A
                            // Log("LD IXL, A");
                            //tstates += 4;
                            IXL = A;
                            break;

                        case 0x70:  //LD (IX + d), B
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("LD (IX + {0:X}), B", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            PokeByte(offset, B);
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0x71:  //LD (IX + d), C
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("LD (IX + {0:X}), C", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            PokeByte(offset, C);
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0x72:  //LD (IX + d), D
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("LD (IX + {0:X}), D", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            PokeByte(offset, D);
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0x73:  //LD (IX + d), E
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("LD (IX + {0:X}), E", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            PokeByte(offset, E);
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0x74:  //LD (IX + d), H
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("LD (IX + {0:X}), H", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            PokeByte(offset, H);
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0x75:  //LD (IX + d), L
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("LD (IX + {0:X}), L", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            PokeByte(offset, L);
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0x77:  //LD (IX + d), A
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("LD (IX + {0:X}), A", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            PokeByte(offset, A);
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0x7C:  //LD A, IXH
                            // Log("LD A, IXH");
                            A = IXH;
                            break;

                        case 0x7D:  //LD A, IXL
                            // Log("LD A, IXL");
                            A = IXL;
                            break;

                        case 0x7E:  //LD A, (IX + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IX + disp; //The displacement required
                            // Log(string.Format("LD A, (IX + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            A = PeekByte(offset);
                            MemPtr = offset;
                            PC++;
                            break;

                        case 0xF9:  //LD SP, IX
                            // Log("LD SP, IX");
                            Contend(IR, 1, 2);
                            SP = IX;
                            break;
                        #endregion

                        #region All DDCB instructions
                        case 0xCB:
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IX + disp; //The displacement required
                            PC++;
                            opcode = GetOpcode(PC);      //The opcode comes after the offset byte!
                            Contend(PC, 1, 2);
                            PC++;
                            disp = PeekByte(offset);
                            Contend(offset, 1, 1);
                            // if ((opcode >= 0x40) && (opcode <= 0x7f))
                            MemPtr = offset;

                            switch (opcode) {
                                case 0x00: //LD B, RLC (IX+d)
                                    // Log(string.Format("LD B, RLC (IX + {0:X})", disp));
                                    B = Rlc_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x01: //LD C, RLC (IX+d)
                                    // Log(string.Format("LD C, RLC (IX + {0:X})", disp));
                                    C = Rlc_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x02: //LD D, RLC (IX+d)
                                    // Log(string.Format("LD D, RLC (IX + {0:X})", disp));
                                    D = Rlc_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x03: //LD E, RLC (IX+d)
                                    // Log(string.Format("LD E, RLC (IX + {0:X})", disp));
                                    E = Rlc_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x04: //LD H, RLC (IX+d)
                                    // Log(string.Format("LD H, RLC (IX + {0:X})", disp));
                                    H = Rlc_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x05: //LD L, RLC (IX+d)
                                    // Log(string.Format("LD L, RLC (IX + {0:X})", disp));
                                    L = Rlc_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x06:  //RLC (IX + d)
                                    // Log(string.Format("RLC (IX + {0:X})", disp));
                                    PokeByte(offset, Rlc_R(disp));
                                    break;

                                case 0x07: //LD A, RLC (IX+d)
                                    // Log(string.Format("LD A, RLC (IX + {0:X})", disp));
                                    A = Rlc_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x08: //LD B, RRC (IX+d)
                                    // Log(string.Format("LD B, RRC (IX + {0:X})", disp));
                                    B = Rrc_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x09: //LD C, RRC (IX+d)
                                    // Log(string.Format("LD C, RRC (IX + {0:X})", disp));
                                    C = Rrc_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x0A: //LD D, RRC (IX+d)
                                    // Log(string.Format("LD D, RRC (IX + {0:X})", disp));
                                    D = Rrc_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x0B: //LD E, RRC (IX+d)
                                    // Log(string.Format("LD E, RRC (IX + {0:X})", disp));
                                    E = Rrc_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x0C: //LD H, RRC (IX+d)
                                    // Log(string.Format("LD H, RRC (IX + {0:X})", disp));
                                    H = Rrc_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x0D: //LD L, RRC (IX+d)
                                    // Log(string.Format("LD L, RRC (IX + {0:X})", disp));
                                    L = Rrc_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x0E:  //RRC (IX + d)
                                    // Log(string.Format("RRC (IX + {0:X})", disp));
                                    PokeByte(offset, Rrc_R(disp));
                                    break;

                                case 0x0F: //LD A, RRC (IX+d)
                                    // Log(string.Format("LD A, RRC (IX + {0:X})", disp));
                                    A = Rrc_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x10: //LD B, RL (IX+d)
                                    // Log(string.Format("LD B, RL (IX + {0:X})", disp));
                                    B = Rl_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x11: //LD C, RL (IX+d)
                                    // Log(string.Format("LD C, RL (IX + {0:X})", disp));
                                    C = Rl_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x12: //LD D, RL (IX+d)
                                    // Log(string.Format("LD D, RL (IX + {0:X})", disp));
                                    D = Rl_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x13: //LD E, RL (IX+d)
                                    // Log(string.Format("LD E, RL (IX + {0:X})", disp));
                                    E = Rl_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x14: //LD H, RL (IX+d)
                                    // Log(string.Format("LD H, RL (IX + {0:X})", disp));
                                    H = Rl_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x15: //LD L, RL (IX+d)
                                    // Log(string.Format("LD L, RL (IX + {0:X})", disp));
                                    L = Rl_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x16:  //RL (IX + d)
                                    // Log(string.Format("RL (IX + {0:X})", disp));
                                    PokeByte(offset, Rl_R(disp));

                                    break;

                                case 0x17: //LD A, RL (IX+d)
                                    // Log(string.Format("LD A, RL (IX + {0:X})", disp));
                                    A = Rl_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x18: //LD B, RR (IX+d)
                                    // Log(string.Format("LD B, RR (IX + {0:X})", disp));
                                    B = Rr_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x19: //LD C, RR (IX+d)
                                    // Log(string.Format("LD C, RR (IX + {0:X})", disp));
                                    C = Rr_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x1A: //LD D, RR (IX+d)
                                    // Log(string.Format("LD D, RR (IX + {0:X})", disp));
                                    D = Rr_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x1B: //LD E, RR (IX+d)
                                    // Log(string.Format("LD E, RR (IX + {0:X})", disp));
                                    E = Rr_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x1C: //LD H, RR (IX+d)
                                    // Log(string.Format("LD H, RR (IX + {0:X})", disp));
                                    H = Rr_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x1D: //LD L, RR (IX+d)
                                    // Log(string.Format("LD L, RR (IX + {0:X})", disp));
                                    L = Rr_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x1E:  //RR (IX + d)
                                    // Log(string.Format("RR (IX + {0:X})", disp));
                                    PokeByte(offset, Rr_R(disp));
                                    break;

                                case 0x1F: //LD A, RR (IX+d)
                                    // Log(string.Format("LD A, RR (IX + {0:X})", disp));
                                    A = Rr_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x20: //LD B, SLA (IX+d)
                                    // Log(string.Format("LD B, SLA (IX + {0:X})", disp));
                                    B = Sla_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x21: //LD C, SLA (IX+d)
                                    // Log(string.Format("LD C, SLA (IX + {0:X})", disp));
                                    C = Sla_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x22: //LD D, SLA (IX+d)
                                    // Log(string.Format("LD D, SLA (IX + {0:X})", disp));
                                    D = Sla_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x23: //LD E, SLA (IX+d)
                                    // Log(string.Format("LD E, SLA (IX + {0:X})", disp));
                                    E = Sla_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x24: //LD H, SLA (IX+d)
                                    // Log(string.Format("LD H, SLA (IX + {0:X})", disp));
                                    H = Sla_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x25: //LD L, SLA (IX+d)
                                    // Log(string.Format("LD L, SLA (IX + {0:X})", disp));
                                    L = Sla_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x26:  //SLA (IX + d)
                                    // Log(string.Format("SLA (IX + {0:X})", disp));
                                    PokeByte(offset, Sla_R(disp));
                                    break;

                                case 0x27: //LD A, SLA (IX+d)
                                    // Log(string.Format("LD A, SLA (IX + {0:X})", disp));
                                    A = Sla_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x28: //LD B, SRA (IX+d)
                                    // Log(string.Format("LD B, SRA (IX + {0:X})", disp));
                                    B = Sra_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x29: //LD C, SRA (IX+d)
                                    // Log(string.Format("LD C, SRA (IX + {0:X})", disp));
                                    C = Sra_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x2A: //LD D, SRA (IX+d)
                                    // Log(string.Format("LD D, SRA (IX + {0:X})", disp));
                                    D = Sra_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x2B: //LD E, SRA (IX+d)
                                    // Log(string.Format("LD E, SRA (IX + {0:X})", disp));
                                    E = Sra_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x2C: //LD H, SRA (IX+d)
                                    // Log(string.Format("LD H, SRA (IX + {0:X})", disp));
                                    H = Sra_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x2D: //LD L, SRA (IX+d)
                                    // Log(string.Format("LD L, SRA (IX + {0:X})", disp));
                                    L = Sra_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x2E:  //SRA (IX + d)
                                    // Log(string.Format("SRA (IX + {0:X})", disp));
                                    PokeByte(offset, Sra_R(disp));
                                    break;

                                case 0x2F: //LD A, SRA (IX+d)
                                    // Log(string.Format("LD A, SRA (IX + {0:X})", disp));
                                    A = Sra_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x30: //LD B, SLL (IX+d)
                                    // Log(string.Format("LD B, SLL (IX + {0:X})", disp));
                                    B = Sll_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x31: //LD C, SLL (IX+d)
                                    // Log(string.Format("LD C, SLL (IX + {0:X})", disp));
                                    C = Sll_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x32: //LD D, SLL (IX+d)
                                    // Log(string.Format("LD D, SLL (IX + {0:X})", disp));
                                    D = Sll_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x33: //LD E, SLL (IX+d)
                                    // Log(string.Format("LD E, SLL (IX + {0:X})", disp));
                                    E = Sll_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x34: //LD H, SLL (IX+d)
                                    // Log(string.Format("LD H, SLL (IX + {0:X})", disp));
                                    H = Sll_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x35: //LD L, SLL (IX+d)
                                    // Log(string.Format("LD L, SLL (IX + {0:X})", disp));
                                    L = Sll_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x36:  //SLL (IX + d)
                                    // Log(string.Format("SLL (IX + {0:X})", disp));
                                    PokeByte(offset, Sll_R(disp));
                                    break;

                                case 0x37: //LD A, SLL (IX+d)
                                    // Log(string.Format("LD A, SLL (IX + {0:X})", disp));
                                    A = Sll_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x38: //LD B, SRL (IX+d)
                                    // Log(string.Format("LD B, SRL (IX + {0:X})", disp));
                                    B = Srl_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x39: //LD C, SRL (IX+d)
                                    // Log(string.Format("LD C, SRL (IX + {0:X})", disp));
                                    C = Srl_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x3A: //LD D, SRL (IX+d)
                                    // Log(string.Format("LD D, SRL (IX + {0:X})", disp));
                                    D = Srl_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x3B: //LD E, SRL (IX+d)
                                    // Log(string.Format("LD E, SRL (IX + {0:X})", disp));
                                    E = Srl_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x3C: //LD H, SRL (IX+d)
                                    // Log(string.Format("LD H, SRL (IX + {0:X})", disp));
                                    H = Srl_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x3D: //LD L, SRL (IX+d)
                                    // Log(string.Format("LD L, SRL (IX + {0:X})", disp));
                                    L = Srl_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x3E:  //SRL (IX + d)
                                    // Log(string.Format("SRL (IX + {0:X})", disp));
                                    PokeByte(offset, Srl_R(disp));
                                    break;

                                case 0x3F: //LD A, SRL (IX+d)
                                    // Log(string.Format("LD A, SRL (IX + {0:X})", disp));
                                    A = Srl_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x40:  //BIT 0, (IX + d)
                                case 0x41:  //BIT 0, (IX + d)
                                case 0x42:  //BIT 0, (IX + d)
                                case 0x43:  //BIT 0, (IX + d)
                                case 0x44:  //BIT 0, (IX + d)
                                case 0x45:  //BIT 0, (IX + d)
                                case 0x46:  //BIT 0, (IX + d)
                                case 0x47:  //BIT 0, (IX + d)
                                    // Log(string.Format("BIT 0, (IX + {0:X})", disp));
                                    Bit_R(0, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x48:  //BIT 1, (IX + d)
                                case 0x49:  //BIT 1, (IX + d)
                                case 0x4A:  //BIT 1, (IX + d)
                                case 0x4B:  //BIT 1, (IX + d)
                                case 0x4C:  //BIT 1, (IX + d)
                                case 0x4D:  //BIT 1, (IX + d)
                                case 0x4E:  //BIT 1, (IX + d)
                                case 0x4F:  //BIT 1, (IX + d)
                                    // Log(string.Format("BIT 1, (IX + {0:X})", disp));
                                    Bit_R(1, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x50:  //BIT 2, (IX + d)
                                case 0x51:  //BIT 2, (IX + d)
                                case 0x52:  //BIT 2, (IX + d)
                                case 0x53:  //BIT 2, (IX + d)
                                case 0x54:  //BIT 2, (IX + d)
                                case 0x55:  //BIT 2, (IX + d)
                                case 0x56:  //BIT 2, (IX + d)
                                case 0x57:  //BIT 2, (IX + d)
                                    // Log(string.Format("BIT 2, (IX + {0:X})", disp));
                                    Bit_R(2, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x58:  //BIT 3, (IX + d)
                                case 0x59:  //BIT 3, (IX + d)
                                case 0x5A:  //BIT 3, (IX + d)
                                case 0x5B:  //BIT 3, (IX + d)
                                case 0x5C:  //BIT 3, (IX + d)
                                case 0x5D:  //BIT 3, (IX + d)
                                case 0x5E:  //BIT 3, (IX + d)
                                case 0x5F:  //BIT 3, (IX + d)
                                    // Log(string.Format("BIT 3, (IX + {0:X})", disp));
                                    Bit_R(3, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x60:  //BIT 4, (IX + d)
                                case 0x61:  //BIT 4, (IX + d)
                                case 0x62:  //BIT 4, (IX + d)
                                case 0x63:  //BIT 4, (IX + d)
                                case 0x64:  //BIT 4, (IX + d)
                                case 0x65:  //BIT 4, (IX + d)
                                case 0x66:  //BIT 4, (IX + d)
                                case 0x67:  //BIT 4, (IX + d)
                                    // Log(string.Format("BIT 4, (IX + {0:X})", disp));
                                    Bit_R(4, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x68:  //BIT 5, (IX + d)
                                case 0x69:  //BIT 5, (IX + d)
                                case 0x6A:  //BIT 5, (IX + d)
                                case 0x6B:  //BIT 5, (IX + d)
                                case 0x6C:  //BIT 5, (IX + d)
                                case 0x6D:  //BIT 5, (IX + d)
                                case 0x6E:  //BIT 5, (IX + d)
                                case 0x6F:  //BIT 5, (IX + d)
                                    // Log(string.Format("BIT 5, (IX + {0:X})", disp));
                                    Bit_R(5, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x70://BIT 6, (IX + d)
                                case 0x71://BIT 6, (IX + d)
                                case 0x72://BIT 6, (IX + d)
                                case 0x73://BIT 6, (IX + d)
                                case 0x74://BIT 6, (IX + d)
                                case 0x75://BIT 6, (IX + d)
                                case 0x76://BIT 6, (IX + d)
                                case 0x77:  //BIT 6, (IX + d)
                                    // Log(string.Format("BIT 6, (IX + {0:X})", disp));
                                    Bit_R(6, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x78:  //BIT 7, (IX + d)
                                case 0x79:  //BIT 7, (IX + d)
                                case 0x7A:  //BIT 7, (IX + d)
                                case 0x7B:  //BIT 7, (IX + d)
                                case 0x7C:  //BIT 7, (IX + d)
                                case 0x7D:  //BIT 7, (IX + d)
                                case 0x7E:  //BIT 7, (IX + d)
                                case 0x7F:  //BIT 7, (IX + d)
                                    // Log(string.Format("BIT 7, (IX + {0:X})", disp));
                                    Bit_R(7, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x80: //LD B, RES 0, (IX+d)
                                    // Log(string.Format("LD B, RES 0, (IX + {0:X})", disp));
                                    B = Res_R(0, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x81: //LD C, RES 0, (IX+d)
                                    // Log(string.Format("LD C, RES 0, (IX + {0:X})", disp));
                                    C = Res_R(0, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x82: //LD D, RES 0, (IX+d)
                                    // Log(string.Format("LD D, RES 0, (IX + {0:X})", disp));
                                    D = Res_R(0, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x83: //LD E, RES 0, (IX+d)
                                    // Log(string.Format("LD E, RES 0, (IX + {0:X})", disp));
                                    E = Res_R(0, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x84: //LD H, RES 0, (IX+d)
                                    // Log(string.Format("LD H, RES 0, (IX + {0:X})", disp));
                                    H = Res_R(0, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x85: //LD L, RES 0, (IX+d)
                                    // Log(string.Format("LD L, RES 0, (IX + {0:X})", disp));
                                    L = Res_R(0, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x86:  //RES 0, (IX + d)
                                    // Log(string.Format("RES 0, (IX + {0:X})", disp));
                                    PokeByte(offset, Res_R(0, disp));
                                    break;

                                case 0x87: //LD A, RES 0, (IX+d)
                                    // Log(string.Format("LD A, RES 0, (IX + {0:X})", disp));
                                    A = Res_R(0, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x88: //LD B, RES 1, (IX+d)
                                    // Log(string.Format("LD B, RES 1, (IX + {0:X})", disp));
                                    B = Res_R(1, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x89: //LD C, RES 1, (IX+d)
                                    // Log(string.Format("LD C, RES 1, (IX + {0:X})", disp));
                                    C = Res_R(1, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x8A: //LD D, RES 1, (IX+d)
                                    // Log(string.Format("LD D, RES 1, (IX + {0:X})", disp));
                                    D = Res_R(1, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x8B: //LD E, RES 1, (IX+d)
                                    // Log(string.Format("LD E, RES 1, (IX + {0:X})", disp));
                                    E = Res_R(1, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x8C: //LD H, RES 1, (IX+d)
                                    // Log(string.Format("LD H, RES 1, (IX + {0:X})", disp));
                                    H = Res_R(1, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x8D: //LD L, RES 1, (IX+d)
                                    // Log(string.Format("LD L, RES 1, (IX + {0:X})", disp));
                                    L = Res_R(1, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x8E:  //RES 1, (IX + d)
                                    // Log(string.Format("RES 1, (IX + {0:X})", disp));
                                    PokeByte(offset, Res_R(1, disp));
                                    break;

                                case 0x8F: //LD A, RES 1, (IX+d)
                                    // Log(string.Format("LD A, RES 1, (IX + {0:X})", disp));
                                    A = Res_R(1, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x90: //LD B, RES 2, (IX+d)
                                    // Log(string.Format("LD B, RES 2, (IX + {0:X})", disp));
                                    B = Res_R(2, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x91: //LD C, RES 2, (IX+d)
                                    // Log(string.Format("LD C, RES 2, (IX + {0:X})", disp));
                                    C = Res_R(2, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x92: //LD D, RES 2, (IX+d)
                                    // Log(string.Format("LD D, RES 2, (IX + {0:X})", disp));
                                    D = Res_R(2, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x93: //LD E, RES 2, (IX+d)
                                    // Log(string.Format("LD E, RES 2, (IX + {0:X})", disp));
                                    E = Res_R(2, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x94: //LD H, RES 2, (IX+d)
                                    // Log(string.Format("LD H, RES 2, (IX + {0:X})", disp));
                                    H = Res_R(2, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x95: //LD L, RES 2, (IX+d)
                                    // Log(string.Format("LD L, RES 2, (IX + {0:X})", disp));
                                    L = Res_R(2, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x96:  //RES 2, (IX + d)
                                    // Log(string.Format("RES 2, (IX + {0:X})", disp));
                                    PokeByte(offset, Res_R(2, disp));
                                    break;

                                case 0x97: //LD A, RES 2, (IX+d)
                                    // Log(string.Format("LD A, RES 2, (IX + {0:X})", disp));
                                    A = Res_R(2, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x98: //LD B, RES 3, (IX+d)
                                    // Log(string.Format("LD B, RES 3, (IX + {0:X})", disp));
                                    B = Res_R(3, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x99: //LD C, RES 3, (IX+d)
                                    // Log(string.Format("LD C, RES 3, (IX + {0:X})", disp));
                                    C = Res_R(3, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x9A: //LD D, RES 3, (IX+d)
                                    // Log(string.Format("LD D, RES 3, (IX + {0:X})", disp));
                                    D = Res_R(3, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x9B: //LD E, RES 3, (IX+d)
                                    // Log(string.Format("LD E, RES 3, (IX + {0:X})", disp));
                                    E = Res_R(3, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x9C: //LD H, RES 3, (IX+d)
                                    // Log(string.Format("LD H, RES 3, (IX + {0:X})", disp));
                                    H = Res_R(3, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x9D: //LD L, RES 3, (IX+d)
                                    // Log(string.Format("LD L, RES 3, (IX + {0:X})", disp));
                                    L = Res_R(3, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x9E:  //RES 3, (IX + d)
                                    // Log(string.Format("RES 3, (IX + {0:X})", disp));
                                    PokeByte(offset, Res_R(3, disp));
                                    break;

                                case 0x9F: //LD A, RES 3, (IX+d)
                                    // Log(string.Format("LD A, RES 3, (IX + {0:X})", disp));
                                    A = Res_R(3, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xA0: //LD B, RES 4, (IX+d)
                                    // Log(string.Format("LD B, RES 4, (IX + {0:X})", disp));
                                    B = Res_R(4, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xA1: //LD C, RES 4, (IX+d)
                                    // Log(string.Format("LD C, RES 4, (IX + {0:X})", disp));
                                    C = Res_R(4, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xA2: //LD D, RES 4, (IX+d)
                                    // Log(string.Format("LD D, RES 4, (IX + {0:X})", disp));
                                    D = Res_R(4, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xA3: //LD E, RES 4, (IX+d)
                                    // Log(string.Format("LD E, RES 4, (IX + {0:X})", disp));
                                    E = Res_R(4, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xA4: //LD H, RES 4, (IX+d)
                                    // Log(string.Format("LD H, RES 4, (IX + {0:X})", disp));
                                    H = Res_R(4, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xA5: //LD L, RES 4, (IX+d)
                                    // Log(string.Format("LD L, RES 4, (IX + {0:X})", disp));
                                    L = Res_R(4, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xA6:  //RES 4, (IX + d)
                                    // Log(string.Format("RES 4, (IX + {0:X})", disp));
                                    PokeByte(offset, Res_R(4, disp));
                                    break;

                                case 0xA7: //LD A, RES 4, (IX+d)
                                    // Log(string.Format("LD A, RES 4, (IX + {0:X})", disp));
                                    A = Res_R(4, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xA8: //LD B, RES 5, (IX+d)
                                    // Log(string.Format("LD B, RES 5, (IX + {0:X})", disp));
                                    B = Res_R(5, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xA9: //LD C, RES 5, (IX+d)
                                    // Log(string.Format("LD C, RES 5, (IX + {0:X})", disp));
                                    C = Res_R(5, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xAA: //LD D, RES 5, (IX+d)
                                    // Log(string.Format("LD D, RES 5, (IX + {0:X})", disp));
                                    D = Res_R(5, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xAB: //LD E, RES 5, (IX+d)
                                    // Log(string.Format("LD E, RES 5, (IX + {0:X})", disp));
                                    E = Res_R(5, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xAC: //LD H, RES 5, (IX+d)
                                    // Log(string.Format("LD H, RES 5, (IX + {0:X})", disp));
                                    H = Res_R(5, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xAD: //LD L, RES 5, (IX+d)
                                    // Log(string.Format("LD L, RES 5, (IX + {0:X})", disp));
                                    L = Res_R(5, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xAE:  //RES 5, (IX + d)
                                    // Log(string.Format("RES 5, (IX + {0:X})", disp));
                                    PokeByte(offset, Res_R(5, disp));
                                    break;

                                case 0xAF: //LD A, RES 5, (IX+d)
                                    // Log(string.Format("LD A, RES 5, (IX + {0:X})", disp));
                                    A = Res_R(5, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xB0: //LD B, RES 6, (IX+d)
                                    // Log(string.Format("LD B, RES 6, (IX + {0:X})", disp));
                                    B = Res_R(6, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xB1: //LD C, RES 6, (IX+d)
                                    // Log(string.Format("LD C, RES 6, (IX + {0:X})", disp));
                                    C = Res_R(6, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xB2: //LD D, RES 6, (IX+d)
                                    // Log(string.Format("LD D, RES 6, (IX + {0:X})", disp));
                                    D = Res_R(5, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xB3: //LD E, RES 6, (IX+d)
                                    // Log(string.Format("LD E, RES 6, (IX + {0:X})", disp));
                                    E = Res_R(6, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xB4: //LD H, RES 5, (IX+d)
                                    // Log(string.Format("LD H, RES 6, (IX + {0:X})", disp));
                                    H = Res_R(6, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xB5: //LD L, RES 5, (IX+d)
                                    // Log(string.Format("LD L, RES 6, (IX + {0:X})", disp));
                                    L = Res_R(6, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xB6:  //RES 6, (IX + d)
                                    // Log(string.Format("RES 6, (IX + {0:X})", disp));
                                    PokeByte(offset, Res_R(6, disp));
                                    break;

                                case 0xB7: //LD A, RES 5, (IX+d)
                                    // Log(string.Format("LD A, RES 6, (IX + {0:X})", disp));
                                    A = Res_R(6, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xB8: //LD B, RES 7, (IX+d)
                                    // Log(string.Format("LD B, RES 7, (IX + {0:X})", disp));
                                    B = Res_R(7, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xB9: //LD C, RES 7, (IX+d)
                                    // Log(string.Format("LD C, RES 7, (IX + {0:X})", disp));
                                    C = Res_R(7, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xBA: //LD D, RES 7, (IX+d)
                                    // Log(string.Format("LD D, RES 7, (IX + {0:X})", disp));
                                    D = Res_R(7, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xBB: //LD E, RES 7, (IX+d)
                                    // Log(string.Format("LD E, RES 7, (IX + {0:X})", disp));
                                    E = Res_R(7, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xBC: //LD H, RES 7, (IX+d)
                                    // Log(string.Format("LD H, RES 7, (IX + {0:X})", disp));
                                    H = Res_R(7, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xBD: //LD L, RES 7, (IX+d)
                                    // Log(string.Format("LD L, RES 7, (IX + {0:X})", disp));
                                    L = Res_R(7, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xBE:  //RES 7, (IX + d)
                                    // Log(string.Format("RES 7, (IX + {0:X})", disp));
                                    PokeByte(offset, Res_R(7, disp));
                                    break;

                                case 0xBF: //LD A, RES 7, (IX+d)
                                    // Log(string.Format("LD A, RES 7, (IX + {0:X})", disp));
                                    A = Res_R(7, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xC0: //LD B, SET 0, (IX+d)
                                    // Log(string.Format("LD B, SET 0, (IX + {0:X})", disp));
                                    B = Set_R(0, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xC1: //LD C, SET 0, (IX+d)
                                    // Log(string.Format("LD C, SET 0, (IX + {0:X})", disp));
                                    C = Set_R(0, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xC2: //LD D, SET 0, (IX+d)
                                    // Log(string.Format("LD D, SET 0, (IX + {0:X})", disp));
                                    D = Set_R(0, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xC3: //LD E, SET 0, (IX+d)
                                    // Log(string.Format("LD E, SET 0, (IX + {0:X})", disp));
                                    E = Set_R(0, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xC4: //LD H, SET 0, (IX+d)
                                    // Log(string.Format("LD H, SET 0, (IX + {0:X})", disp));
                                    H = Set_R(0, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xC5: //LD L, SET 0, (IX+d)
                                    // Log(string.Format("LD L, SET 0, (IX + {0:X})", disp));
                                    L = Set_R(0, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xC6:  //SET 0, (IX + d)
                                    // Log(string.Format("SET 0, (IX + {0:X})", disp));
                                    PokeByte(offset, Set_R(0, disp));
                                    break;

                                case 0xC7: //LD A, SET 0, (IX+d)
                                    // Log(string.Format("LD A, SET 0, (IX + {0:X})", disp));
                                    A = Set_R(0, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xC8: //LD B, SET 1, (IX+d)
                                    // Log(string.Format("LD B, SET 1, (IX + {0:X})", disp));
                                    B = Set_R(1, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xC9: //LD C, SET 0, (IX+d)
                                    // Log(string.Format("LD C, SET 1, (IX + {0:X})", disp));
                                    C = Set_R(1, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xCA: //LD D, SET 1, (IX+d)
                                    // Log(string.Format("LD D, SET 1, (IX + {0:X})", disp));
                                    D = Set_R(1, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xCB: //LD E, SET 1, (IX+d)
                                    // Log(string.Format("LD E, SET 1, (IX + {0:X})", disp));
                                    E = Set_R(1, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xCC: //LD H, SET 1, (IX+d)
                                    // Log(string.Format("LD H, SET 1, (IX + {0:X})", disp));
                                    H = Set_R(1, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xCD: //LD L, SET 1, (IX+d)
                                    // Log(string.Format("LD L, SET 1, (IX + {0:X})", disp));
                                    L = Set_R(1, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xCE:  //SET 1, (IX + d)
                                    // Log(string.Format("SET 1, (IX + {0:X})", disp));
                                    PokeByte(offset, Set_R(1, disp));
                                    break;

                                case 0xCF: //LD A, SET 1, (IX+d)
                                    // Log(string.Format("LD A, SET 1, (IX + {0:X})", disp));
                                    A = Set_R(1, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xD0: //LD B, SET 2, (IX+d)
                                    // Log(string.Format("LD B, SET 2, (IX + {0:X})", disp));
                                    B = Set_R(2, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xD1: //LD C, SET 2, (IX+d)
                                    // Log(string.Format("LD C, SET 2, (IX + {0:X})", disp));
                                    C = Set_R(2, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xD2: //LD D, SET 2, (IX+d)
                                    // Log(string.Format("LD D, SET 2, (IX + {0:X})", disp));
                                    D = Set_R(2, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xD3: //LD E, SET 2, (IX+d)
                                    // Log(string.Format("LD E, SET 2, (IX + {0:X})", disp));
                                    E = Set_R(2, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xD4: //LD H, SET 21, (IX+d)
                                    // Log(string.Format("LD H, SET 2, (IX + {0:X})", disp));
                                    H = Set_R(2, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xD5: //LD L, SET 2, (IX+d)
                                    // Log(string.Format("LD L, SET 2, (IX + {0:X})", disp));
                                    L = Set_R(2, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xD6:  //SET 2, (IX + d)
                                    // Log(string.Format("SET 2, (IX + {0:X})", disp));
                                    PokeByte(offset, Set_R(2, disp));
                                    break;

                                case 0xD7: //LD A, SET 2, (IX+d)
                                    // Log(string.Format("LD A, SET 2, (IX + {0:X})", disp));
                                    A = Set_R(2, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xD8: //LD B, SET 3, (IX+d)
                                    // Log(string.Format("LD B, SET 3, (IX + {0:X})", disp));
                                    B = Set_R(3, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xD9: //LD C, SET 3, (IX+d)
                                    // Log(string.Format("LD C, SET 3, (IX + {0:X})", disp));
                                    C = Set_R(3, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xDA: //LD D, SET 3, (IX+d)
                                    // Log(string.Format("LD D, SET 3, (IX + {0:X})", disp));
                                    D = Set_R(3, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xDB: //LD E, SET 3, (IX+d)
                                    // Log(string.Format("LD E, SET 3, (IX + {0:X})", disp));
                                    E = Set_R(3, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xDC: //LD H, SET 21, (IX+d)
                                    // Log(string.Format("LD H, SET 3, (IX + {0:X})", disp));
                                    H = Set_R(3, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xDD: //LD L, SET 3, (IX+d)
                                    // Log(string.Format("LD L, SET 3, (IX + {0:X})", disp));
                                    L = Set_R(3, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xDE:  //SET 3, (IX + d)
                                    // Log(string.Format("SET 3, (IX + {0:X})", disp));
                                    PokeByte(offset, Set_R(3, disp));
                                    break;

                                case 0xDF: //LD A, SET 3, (IX+d)
                                    // Log(string.Format("LD A, SET 3, (IX + {0:X})", disp));
                                    A = Set_R(3, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xE0: //LD B, SET 4, (IX+d)
                                    // Log(string.Format("LD B, SET 4, (IX + {0:X})", disp));
                                    B = Set_R(4, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xE1: //LD C, SET 4, (IX+d)
                                    // Log(string.Format("LD C, SET 4, (IX + {0:X})", disp));
                                    C = Set_R(4, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xE2: //LD D, SET 4, (IX+d)
                                    // Log(string.Format("LD D, SET 4, (IX + {0:X})", disp));
                                    D = Set_R(4, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xE3: //LD E, SET 4, (IX+d)
                                    // Log(string.Format("LD E, SET 4, (IX + {0:X})", disp));
                                    E = Set_R(4, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xE4: //LD H, SET 4, (IX+d)
                                    // Log(string.Format("LD H, SET 4, (IX + {0:X})", disp));
                                    H = Set_R(4, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xE5: //LD L, SET 3, (IX+d)
                                    // Log(string.Format("LD L, SET 4, (IX + {0:X})", disp));
                                    L = Set_R(4, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xE6:  //SET 4, (IX + d)
                                    // Log(string.Format("SET 4, (IX + {0:X})", disp));
                                    PokeByte(offset, Set_R(4, disp));
                                    break;

                                case 0xE7: //LD A, SET 4, (IX+d)
                                    // Log(string.Format("LD A, SET 4, (IX + {0:X})", disp));
                                    A = Set_R(4, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xE8: //LD B, SET 5, (IX+d)
                                    // Log(string.Format("LD B, SET 5, (IX + {0:X})", disp));
                                    B = Set_R(5, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xE9: //LD C, SET 5, (IX+d)
                                    // Log(string.Format("LD C, SET 5, (IX + {0:X})", disp));
                                    C = Set_R(5, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xEA: //LD D, SET 5, (IX+d)
                                    // Log(string.Format("LD D, SET 5, (IX + {0:X})", disp));
                                    D = Set_R(5, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xEB: //LD E, SET 5, (IX+d)
                                    // Log(string.Format("LD E, SET 5, (IX + {0:X})", disp));
                                    E = Set_R(5, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xEC: //LD H, SET 5, (IX+d)
                                    // Log(string.Format("LD H, SET 5, (IX + {0:X})", disp));
                                    H = Set_R(5, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xED: //LD L, SET 5, (IX+d)
                                    // Log(string.Format("LD L, SET 5, (IX + {0:X})", disp));
                                    L = Set_R(5, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xEE:  //SET 5, (IX + d)
                                    // Log(string.Format("SET 5, (IX + {0:X})", disp));
                                    PokeByte(offset, Set_R(5, disp));
                                    break;

                                case 0xEF: //LD A, SET 5, (IX+d)
                                    // Log(string.Format("LD A, SET 5, (IX + {0:X})", disp));
                                    A = Set_R(5, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xF0: //LD B, SET 6, (IX+d)
                                    // Log(string.Format("LD B, SET 6, (IX + {0:X})", disp));
                                    B = Set_R(6, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xF1: //LD C, SET 6, (IX+d)
                                    // Log(string.Format("LD C, SET 6, (IX + {0:X})", disp));
                                    C = Set_R(6, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xF2: //LD D, SET 6, (IX+d)
                                    // Log(string.Format("LD D, SET 6, (IX + {0:X})", disp));
                                    D = Set_R(6, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xF3: //LD E, SET 6, (IX+d)
                                    // Log(string.Format("LD E, SET 6, (IX + {0:X})", disp));
                                    E = Set_R(6, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xF4: //LD H, SET 6, (IX+d)
                                    // Log(string.Format("LD H, SET 6, (IX + {0:X})", disp));
                                    H = Set_R(6, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xF5: //LD L, SET 6, (IX+d)
                                    // Log(string.Format("LD L, SET 6, (IX + {0:X})", disp));
                                    L = Set_R(6, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xF6:  //SET 6, (IX + d)
                                    // Log(string.Format("SET 6, (IX + {0:X})", disp));
                                    PokeByte(offset, Set_R(6, disp));
                                    break;

                                case 0xF7: //LD A, SET 6, (IX+d)
                                    // Log(string.Format("LD A, SET 6, (IX + {0:X})", disp));
                                    A = Set_R(6, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xF8: //LD B, SET 7, (IX+d)
                                    // Log(string.Format("LD B, SET 7, (IX + {0:X})", disp));
                                    B = Set_R(7, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xF9: //LD C, SET 7, (IX+d)
                                    // Log(string.Format("LD C, SET 7, (IX + {0:X})", disp));
                                    C = Set_R(7, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xFA: //LD D, SET 7, (IX+d)
                                    // Log(string.Format("LD D, SET 7, (IX + {0:X})", disp));
                                    D = Set_R(7, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xFB: //LD E, SET 7, (IX+d)
                                    // Log(string.Format("LD E, SET 7, (IX + {0:X})", disp));
                                    E = Set_R(7, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xFC: //LD H, SET 7, (IX+d)
                                    // Log(string.Format("LD H, SET 7, (IX + {0:X})", disp));
                                    H = Set_R(7, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xFD: //LD L, SET 7, (IX+d)
                                    // Log(string.Format("LD L, SET 7, (IX + {0:X})", disp));
                                    L = Set_R(7, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xFE:  //SET 7, (IX + d)
                                    // Log(string.Format("SET 7, (IX + {0:X})", disp));
                                    PokeByte(offset, Set_R(7, disp));
                                    break;

                                case 0xFF: //LD A, SET 7, (IX + D)
                                    A = Set_R(7, disp);
                                    PokeByte(offset, A);
                                    break;

                                default:
                                    String msg = "ERROR: Could not handle DDCB " + opcode.ToString();
                                    System.Windows.Forms.MessageBox.Show(msg, "Opcode handler",
                                                System.Windows.Forms.MessageBoxButtons.OKCancel, System.Windows.Forms.MessageBoxIcon.Error);
                                    break;
                            }
                            break;
                        #endregion

                        #region Pop/Push instructions
                        case 0xE1:  //POP IX
                            // Log("POP IX");
                            IX = PopStack();
                            break;

                        case 0xE5:  //PUSH IX
                            // Log("PUSH IX");
                            Contend(IR, 1, 1);
                            PushStack(IX);
                            break;
                        #endregion

                        #region Exchange instruction
                        case 0xE3:  //EX (SP), IX
                            // Log("EX (SP), IX");
                            //disp = IX;
                            addr = PeekWord(SP);
                            Contend(SP + 1, 1, 1);
                            PokeByte((SP + 1) & 0xffff, IX >> 8);
                            PokeByte(SP, IX & 0xff);
                            Contend(SP, 1, 2);
                            IX = addr;
                            MemPtr = IX;
                            break;
                        #endregion

                        #region Jump instruction
                        case 0xE9:  //JP (IX)
                            // Log("JP (IX)");
                            PC = IX;
                            break;
                        #endregion

                        //  case 0xED:
                        //     MessageBox.Show("DD ED encountered!", "Opcode handler",
                        //                 MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                        //      break;

                        default:
                            //According to Sean's doc: http://z80.info/z80sean.txt
                            //If a DDxx or FDxx instruction is not listed, it should operate as
                            //without the DD or FD prefix, and the DD or FD prefix itself should
                            //operate as a NOP.
                            Execute();  //Try to excute it as a normal instruction then
                            break;
                    }
                    break;
                #endregion

                #region Opcodes with ED prefix
                case 0xED:
                    opcode = FetchInstruction();
                    if (opcode < 0x40) {
                        break;
                    } else
                        switch (opcode) {
                            case 0x40: //IN B, (C)
                                // Log("IN B, (C)");
                                B = In();
                                break;

                            case 0x41: //Out (C), B
                                // Log("OUT (C), B");
                                Out(BC, B);
                                break;

                            case 0x42:  //SBC HL, BC
                                // Log("SBC HL, BC");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 7;
                                //else
                                Contend(IR, 1, 7);
                                MemPtr = HL + 1;
                                Sbc_RR(BC);
                                break;

                            case 0x43:  //LD (nn), BC
                                disp = PeekWord(PC);
                                // Log(String.Format("LD ({0:X}), BC", disp));
                                PokeWord(disp, BC);
                                MemPtr = disp + 1;
                                PC += 2;
                                break;

                            case 0x44:  //NEG
                                // Log("NEG");
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x45:  //RETN
                                // Log("RET N");
                                PC = PopStack();
                                IFF1 = IFF2;
                                MemPtr = PC;
                                break;

                            case 0x46:  //IM0
                                // Log("IM 0");
                                interruptMode = 0;
                                break;

                            case 0x47:  //LD I, A
                                // Log("LD I, A");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 1;
                                //else
                                Contend(IR, 1, 1);
                                I = A;
                                break;

                            case 0x48: //IN C, (C)
                                // Log("IN C, (C)");
                                C = In();
                                //tstates = 0;
                                break;

                            case 0x49: //Out (C), C
                                // Log("OUT (C), C");
                                Out(BC, C);
                                //tstates = 0;
                                break;

                            case 0x4A:  //ADC HL, BC
                                // Log("ADC HL, BC");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 7;
                                //else
                                Contend(IR, 1, 7);
                                MemPtr = HL + 1;
                                Adc_RR(BC);
                                break;

                            case 0x4B:  //LD BC, (nn)
                                disp = PeekWord(PC);
                                // Log(String.Format("LD BC, ({0:X})", disp));
                                BC = PeekWord(disp);
                                MemPtr = disp + 1;
                                PC += 2;
                                break;

                            case 0x4C:  //NEG
                                // Log("NEG");
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x4D:  //RETI
                                // Log("RETI");
                                PC = PopStack();
                                IFF1 = IFF2;
                                MemPtr = PC;
                                break;

                            case 0x4F:  //LD R, A
                                // Log("LD R, A");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 1;
                                //else
                                Contend(IR, 1, 1);
                                _R = A;
                                break;

                            case 0x50: //IN D, (C)
                                // Log("IN D, (C)");
                                D = In();
                                //tstates = 0;
                                break;

                            case 0x51: //Out (C), D
                                // Log("OUT (C), D");
                                Out(BC, D);
                                break;

                            case 0x52:  //SBC HL, DE
                                // Log("SBC HL, DE");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 7;
                                //else
                                Contend(IR, 1, 7);
                                MemPtr = HL + 1;
                                Sbc_RR(DE);
                                break;

                            case 0x53:  //LD (nn), DE
                                disp = PeekWord(PC);
                                // Log(String.Format("LD ({0:X}), DE", disp));
                                PokeWord(disp, DE);
                                MemPtr = disp + 1;
                                PC += 2;
                                break;

                            case 0x54:  //NEG
                                // Log("NEG");
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x55:  //RETN
                                // Log("RETN");
                                PC = PopStack();
                                IFF1 = IFF2;
                                MemPtr = PC;
                                break;

                            case 0x56:  //IM1
                                // Log("IM 1");
                                interruptMode = 1;
                                break;

                            case 0x57:  //LD A, I
                                // Log("LD A, I");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 1;
                                //else
                                Contend(IR, 1, 1);
                                A = I;

                                SetNeg(false);
                                SetHalf(false);
                                SetParity(IFF2);
                                SetSign((A & F_SIGN) != 0);
                                SetZero(A == 0);
                                SetF3((A & F_3) != 0);
                                SetF5((A & F_5) != 0);
                                break;

                            case 0x58: //IN E, (C)
                                // Log("IN E, (C)");
                                E = In();
                                //tstates = 0;
                                break;

                            case 0x59: //Out (C), E
                                // Log("OUT (C), E");
                                Out(BC, E);
                                tstates = 0;
                                break;

                            case 0x5A:  //ADC HL, DE
                                // Log("ADC HL, DE");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 7;
                                //else
                                Contend(IR, 1, 7);
                                MemPtr = HL + 1;
                                Adc_RR(DE);
                                break;

                            case 0x5B:  //LD DE, (nn)
                                disp = PeekWord(PC);
                                // Log(String.Format("LD DE, ({0:X})", disp));
                                DE = PeekWord(disp);
                                MemPtr = disp + 1;
                                PC += 2;
                                break;

                            case 0x5C:  //NEG
                                // Log("NEG");
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x5D:  //RETN
                                // Log("RETN");
                                PC = PopStack();
                                IFF1 = IFF2;
                                MemPtr = PC;
                                break;

                            case 0x5E:  //IM2
                                // Log("IM 2");
                                interruptMode = 2;
                                break;

                            case 0x5F:  //LD A, R
                                // Log("LD A, R");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 1;
                                //else
                                Contend(IR, 1, 1);
                                A = R;
                                SetNeg(false);
                                SetHalf(false);
                                SetParity(IFF2);
                                SetSign((A & F_SIGN) != 0);
                                SetZero(A == 0);
                                SetF3((A & F_3) != 0);
                                SetF5((A & F_5) != 0);
                                break;

                            case 0x60: //IN H, (C)
                                // Log("IN H, (C)");
                                H = In();
                                break;

                            case 0x61: //Out (C), H
                                // Log("OUT (C), H");
                                Out(BC, H);
                                break;

                            case 0x62:  //SBC HL, HL
                                // Log("SBC HL, HL");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 7;
                                //else
                                Contend(IR, 1, 7);
                                MemPtr = HL + 1;
                                Sbc_RR(HL);
                                break;

                            case 0x63:  //LD (nn), HL
                                disp = PeekWord(PC);
                                // Log(String.Format("LD ({0:X}), HL", disp));
                                PokeWord(disp, HL);
                                MemPtr = disp + 1;
                                PC += 2;
                                break;

                            case 0x64:  //NEG
                                // Log("NEG");
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x65:  //RETN
                                // Log("RETN");
                                PC = PopStack();
                                IFF1 = IFF2;
                                MemPtr = PC;
                                break;

                            case 0x67:  //RRD
                                // Log("RRD");
                                temp = A;
                                int data = PeekByte(HL);
                                A = (A & 0xf0) | (data & 0x0f);
                                data = (data >> 4) | (temp << 4);
                                Contend(HL, 1, 4);
                                PokeByte(HL, data);
                                MemPtr = HL + 1;
                                SetSign((A & F_SIGN) != 0);
                                SetF3((A & F_3) != 0);
                                SetF5((A & F_5) != 0);
                                SetZero(A == 0);
                                // SetParity(GetParity(A));
                                SetParity(parity[A]);
                                SetHalf(false);
                                SetNeg(false);
                                break;

                            case 0x68: //IN L, (C)
                                // Log("IN L, (C)");
                                L = In();
                                break;

                            case 0x69: //Out (C), L
                                // Log("OUT (C), L");
                                Out(BC, L);
                                break;

                            case 0x6A:  //ADC HL, HL
                                // Log("ADC HL, HL");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 7;
                                //else
                                Contend(IR, 1, 7);
                                MemPtr = HL + 1;
                                Adc_RR(HL);
                                break;

                            case 0x6B:  //LD HL, (nn)
                                disp = PeekWord(PC);
                                // Log(String.Format("LD HL, ({0:X})", disp));
                                HL = PeekWord(disp);
                                MemPtr = disp + 1;
                                PC += 2;
                                break;

                            case 0x6C:  //NEG
                                // Log("NEG");
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x6D:  //RETN
                                // Log("RETN");
                                PC = PopStack();
                                IFF1 = IFF2;
                                MemPtr = PC;
                                break;

                            case 0x6F:  //RLD
                                // Log("RLD");
                                temp = A;
                                data = PeekByte(HL);
                                A = (A & 0xf0) | (data >> 4);
                                data = (data << 4) | (temp & 0x0f);
                                Contend(HL, 1, 4);
                                PokeByte(HL, data & 0xff);
                                MemPtr = HL + 1;
                                SetSign((A & F_SIGN) != 0);
                                SetF3((A & F_3) != 0);
                                SetF5((A & F_5) != 0);
                                SetZero(A == 0);
                                // SetParity(GetParity(A)); // Not sure what to do here!
                                SetParity(parity[A]);
                                SetHalf(false);
                                SetNeg(false);
                                break;

                            case 0x70:  //IN (C)
                                // Log("IN (C)");
                                In();
                                //tstates = 0;
                                break;

                            case 0x71:
                                // Log("OUT (C), 0");
                                Out(BC, 0);
                                break;

                            case 0x72:  //SBC HL, SP
                                // Log("SBC HL, SP");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 7;
                                //else
                                Contend(IR, 1, 7);
                                MemPtr = HL + 1;
                                Sbc_RR(SP);
                                break;

                            case 0x73:  //LD (nn), SP
                                disp = PeekWord(PC);
                                // Log(String.Format("LD ({0:X}), SP", disp));
                                PokeWord(disp, SP);
                                MemPtr = disp + 1;
                                PC += 2;
                                break;

                            case 0x74:  //NEG
                                // Log("NEG");
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x75:  //RETN
                                // Log("RETN");
                                PC = PopStack();
                                IFF1 = IFF2;
                                MemPtr = PC;
                                break;

                            case 0x76:  //IM 1
                                // Log("IM 1");
                                interruptMode = 1;
                                break;

                            case 0x78:  //IN A, (C)
                                // Log("IN A, (C)");
                                MemPtr = BC + 1;
                                A = In();
                                break;

                            case 0x79: //Out (C), A
                                // Log("OUT (C), A");
                                MemPtr = BC + 1;
                                Out(BC, A);
                                break;

                            case 0x7A:  //ADC HL, SP
                                // Log("ADC HL, SP");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 7;
                                //else
                                Contend(IR, 1, 7);
                                MemPtr = HL + 1;
                                Adc_RR(SP);
                                break;

                            case 0x7B:  //LD SP, (nn)
                                disp = PeekWord(PC);
                                // Log(String.Format("LD SP, ({0:X})", disp));
                                SP = PeekWord(disp);
                                MemPtr = disp + 1;
                                PC += 2;
                                break;

                            case 0x7C:  //NEG
                                // Log("NEG");
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x7D:  //RETN
                                // Log("RETN");
                                PC = PopStack();
                                IFF1 = IFF2;
                                MemPtr = PC;
                                break;

                            case 0x7E:  //IM 2
                                // Log("IM 2");
                                interruptMode = 2;
                                break;

                            case 0xA0:  //LDI
                                // Log("LDI");
                                disp = PeekByte(HL);
                                PokeByte(DE, disp);
                                Contend(DE, 1, 2);
                                SetF3(((disp + A) & F_3) != 0);
                                SetF5(((disp + A) & F_NEG) != 0);
                                HL++;
                                DE++;
                                BC--;
                                SetNeg(false);
                                SetHalf(false);
                                SetParity(BC != 0);
                                break;

                            case 0xA1:  //CPI
                                // Log("CPI");
                                disp = PeekByte(HL);
                                bool lastCarry = ((F & F_CARRY) != 0);
                                Cp_R(disp);
                                Contend(HL, 1, 5);
                                HL++;
                                BC--;

                                MemPtr++;
                                SetCarry(lastCarry);
                                SetParity(BC != 0);
                                SetF3((((A - disp - ((F & F_HALF) >> 4)) & 0xff) & F_3) != 0);
                                SetF5((((A - disp - ((F & F_HALF) >> 4)) & 0xff) & F_NEG) != 0);
                                break;

                            case 0xA2:  //INI
                                // Log("INI");
                                Contend(IR, 1, 1);
                                int result = In();
                                PokeByte(HL, result);
                                MemPtr = BC + 1;
                                B = Dec(B);
                                HL++;
                                SetNeg((result & F_SIGN) != 0);
                                SetCarry(((((C + 1) & 0xff) + result) > 0xff));
                                SetHalf((F & F_CARRY) != 0);

                                SetParity(parity[(((result + ((C + 1) & 0xff)) & 0x7) ^ B)]);
                                break;

                            case 0xA3:  //OUTI
                                Contend(IR, 1, 1);
                                // Log("OUTI");

                                B = Dec(B);
                                MemPtr = BC + 1;
                                disp = PeekByte(HL);
                                Out(BC, disp);

                                HL++;
                                SetNeg((disp & F_SIGN) != 0);
                                SetCarry((disp + L) > 0xff);
                                SetHalf((F & F_CARRY) != 0);

                                SetParity(parity[(((disp + L) & 0x7) ^ B)]);
                                //SetZero(B == 0);
                                break;

                            case 0xA8:  //LDD
                                // Log("LDD");
                                disp = PeekByte(HL);
                                PokeByte(DE, disp);
                                Contend(DE, 1, 2);
                                SetF3(((disp + A) & F_3) != 0);
                                SetF5(((disp + A) & F_NEG) != 0);
                                HL--;
                                DE--;
                                BC--;
                                SetNeg(false);
                                SetHalf(false);
                                SetParity(BC != 0);
                                break;

                            case 0xA9:  //CPD
                                // Log("CPD");
                                lastCarry = ((F & F_CARRY) != 0);
                                disp = PeekByte(HL);
                                Cp_R(disp);
                                Contend(HL, 1, 5);
                                HL--;
                                BC--;
                                SetParity(BC != 0);
                                SetF3((((A - disp - ((F & F_HALF) >> 4)) & 0xff) & F_3) != 0);
                                SetF5((((A - disp - ((F & F_HALF) >> 4)) & 0xff) & F_NEG) != 0);
                                SetCarry(lastCarry);
                                MemPtr--;
                                break;

                            case 0xAA:  //IND
                                // Log("IND");
                                Contend(IR, 1, 1);
                                result = In();
                                PokeByte(HL, result);
                                MemPtr = BC - 1;
                                B = Dec(B); ;
                                HL--;
                                SetNeg((result & F_SIGN) != 0);
                                SetCarry(((((C - 1) & 0xff) + result) > 0xff));
                                SetHalf((F & F_CARRY) != 0);

                                SetParity(parity[(((result + ((C - 1) & 0xff)) & 0x7) ^ B)]);
                                break;

                            case 0xAB:  //OUTD
                                // Log("OUTD");
                                Contend(IR, 1, 1);

                                B = Dec(B);
                                MemPtr = BC - 1;

                                disp = PeekByte(HL);
                                Out(BC, disp);

                                HL--;

                                SetNeg((disp & F_SIGN) != 0);
                                SetCarry((disp + L) > 0xff);
                                SetHalf((F & F_CARRY) != 0);

                                SetParity(parity[(((disp + L) & 0x7) ^ B)]);
                                break;

                            case 0xB0:  //LDIR
                                // Log("LDIR");
                                disp = PeekByte(HL);
                                PokeByte(DE, disp);
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 2;
                                //else

                                Contend(DE, 1, 2);
                                SetF3(((disp + A) & F_3) != 0);
                                SetF5(((disp + A) & F_NEG) != 0);
                                SetNeg(false);
                                SetHalf(false);
                                if (BC != 1) {
                                    MemPtr = PC - 1; //points to B0 byte
                                }

                                BC--;
                                if (BC != 0) {
                                    //if (model == MachineModel._plus3)
                                    //    totalTStates += 5;
                                    //else
                                    Contend(DE, 1, 5);
                                    PC -= 2;
                                }

                                SetParity(BC != 0);
                                HL++;
                                DE++;

                                break;

                            case 0xB1:  //CPIR
                                // Log("CPIR");
                                lastCarry = ((F & F_CARRY) != 0);
                                disp = PeekByte(HL);
                                Cp_R(disp);
                                Contend(HL, 1, 5);
                                if ((BC == 1) || (A == disp)) {
                                    MemPtr++;
                                } else {
                                    MemPtr = PC - 1;
                                }
                                BC--;
                                SetCarry(lastCarry);
                                SetParity(BC != 0);
                                SetF3((((A - disp - ((F & F_HALF) >> 4)) & 0xff) & F_3) != 0);
                                SetF5((((A - disp - ((F & F_HALF) >> 4)) & 0xff) & F_NEG) != 0);
                                if ((BC != 0) && ((F & F_ZERO) == 0)) {
                                    Contend(HL, 1, 5);
                                    PC -= 2;
                                }
                                HL++;

                                break;

                            case 0xB2:  //INIR
                                // Log("INIR");
                                Contend(IR, 1, 1);
                                result = In();
                                PokeByte(HL, result);
                                MemPtr = BC + 1;
                                B = Dec(B); ;
                                HL++;
                                if (B != 0) {
                                    Contend(HL, 1, 5);
                                    PC -= 2;
                                }

                                SetNeg((result & F_SIGN) != 0);
                                SetCarry(((((C + 1) & 0xff) + result) > 0xff));
                                SetHalf((F & F_CARRY) != 0);

                                SetParity(parity[(((result + ((C + 1) & 0xff)) & 0x7) ^ B)]);
                                break;

                            case 0xB3:  //OTIR
                                // Log("OTIR");
                                Contend(IR, 1, 1);
                                B = Dec(B);
                                MemPtr = BC + 1;

                                disp = PeekByte(HL);
                                Out(BC, disp);
                                if (B != 0) {
                                    Contend(BC, 1, 5);
                                    PC -= 2;
                                }

                                HL++;
                                SetNeg((disp & F_SIGN) != 0);
                                SetCarry((disp + L) > 0xff);
                                SetHalf((F & F_CARRY) != 0);

                                SetParity(parity[(((disp + L) & 0x7) ^ B)]);
                                break;

                            case 0xB8:  //LDDR
                                // Log("LDDR");
                                disp = PeekByte(HL);
                                PokeByte(DE, disp);
                                Contend(DE, 1, 2);

                                SetF3(((disp + A) & F_3) != 0);
                                SetF5(((disp + A) & F_NEG) != 0);
                                SetNeg(false);
                                SetHalf(false);
                                if (BC != 1) {
                                    MemPtr = PC - 1;
                                }

                                BC--;
                                if (BC != 0) {
                                    Contend(DE, 1, 5);
                                    PC -= 2;
                                }

                                SetParity(BC != 0);
                                HL--;
                                DE--;

                                break;

                            case 0xB9:  //CPDR
                                // Log("CPDR");
                                lastCarry = ((F & F_CARRY) != 0);
                                disp = PeekByte(HL);
                                Cp_R(disp);
                                Contend(HL, 1, 5);
                                if ((BC == 1) || (A == disp)) {
                                    MemPtr--;
                                } else {
                                    MemPtr = PC - 1;
                                }

                                BC--;
                                SetCarry(lastCarry);
                                SetParity(BC != 0);
                                SetF3((((A - disp - ((F & F_HALF) >> 4)) & 0xff) & F_3) != 0);
                                SetF5((((A - disp - ((F & F_HALF) >> 4)) & 0xff) & F_NEG) != 0);
                                if ((BC != 0) && ((F & F_ZERO) == 0)) {
                                    Contend(HL, 1, 5);
                                    PC -= 2;
                                }
                                HL--;
                                break;

                            case 0xBA:  //INDR
                                // Log("INDR");
                                Contend(IR, 1, 1);
                                result = In();
                                PokeByte(HL, result);
                                MemPtr = BC - 1;
                                B = Dec(B);
                                if (B != 0) {
                                    Contend(HL, 1, 5);
                                    PC -= 2;
                                }
                                HL--;
                                SetNeg((result & F_SIGN) != 0);
                                SetCarry(((((C - 1) & 0xff) + result) > 0xff));
                                SetHalf((F & F_CARRY) != 0);
                                SetParity(parity[(((result + ((C - 1) & 0xff)) & 0x7) ^ B)]);
                                break;

                            case 0xBB:  //OTDR
                                // Log("OTDR");
                                Contend(IR, 1, 1);
                                B = Dec(B);
                                MemPtr = BC - 1;

                                disp = PeekByte(HL);
                                Out(BC, disp);

                                if (B != 0) {
                                    Contend(BC, 1, 5);
                                    PC -= 2;
                                }

                                HL--;
                                SetNeg((disp & F_SIGN) != 0);
                                SetCarry((disp + L) > 0xff);
                                SetHalf((F & F_CARRY) != 0);

                                SetParity(parity[(((disp + L) & 0x7) ^ B)]);
                                break;

                            default:
                                //According to Sean's doc: http://z80.info/z80sean.txt
                                //If an EDxx instruction is not listed, it should operate as two NOPs.
                                break;  //Carry on to next instruction then
                        }
                    break;
                #endregion

                #region Opcodes with FD prefix (includes FDCB)
                case 0xFD:
                    switch (opcode = FetchInstruction()) {
                        #region Addition instructions
                        case 0x09:  //ADD IY, BC
                            // Log("ADD IY, BC");
                            Contend(IR, 1, 7);
                            MemPtr = IY + 1;
                            IY = Add_RR(IY, BC);
                            break;

                        case 0x19:  //ADD IY, DE
                            // Log("ADD IY, DE");
                            Contend(IR, 1, 7);
                            MemPtr = IY + 1;
                            IY = Add_RR(IY, DE);
                            break;

                        case 0x29:  //ADD IY, IY
                            // Log("ADD IY, IY");
                            Contend(IR, 1, 7);
                            MemPtr = IY + 1;
                            IY = Add_RR(IY, IY);
                            break;

                        case 0x39:  //ADD IY, SP
                            // Log("ADD IY, SP");
                            Contend(IR, 1, 7);
                            MemPtr = IY + 1;
                            IY = Add_RR(IY, SP);
                            break;

                        case 0x84:  //ADD A, IYH
                            // Log("ADD A, IYH");
                            Add_R(IYH);
                            break;

                        case 0x85:  //ADD A, IYL
                            // Log("ADD A, IYL");
                            Add_R(IYL);
                            break;

                        case 0x86:  //Add A, (IY+d)
                            disp = GetDisplacement(PeekByte(PC));
                            int offset = IY + disp; //The displacement required
                            // Log(string.Format("ADD A, (IY + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            Add_R(PeekByte(offset));
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x8C:  //ADC A, IYH
                            // Log("ADC A, IYH");
                            Adc_R(IYH);
                            break;

                        case 0x8D:  //ADC A, IYL
                            // Log("ADC A, IYL");
                            Adc_R(IYL);
                            break;

                        case 0x8E: //ADC A, (IY+d)
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IY + disp; //The displacement required
                            // Log(string.Format("ADC A, (IY + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            Adc_R(PeekByte(offset));
                            PC++;
                            MemPtr = offset;
                            break;
                        #endregion

                        #region Subtraction instructions
                        case 0x94:  //SUB A, IYH
                            // Log("SUB A, IYH");
                            Sub_R(IYH);
                            break;

                        case 0x95:  //SUB A, IYL
                            // Log("SUB A, IYL");
                            Sub_R(IYL);
                            break;

                        case 0x96:  //SUB (IY + d)
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IY + disp; //The displacement required
                            // Log(string.Format("SUB (IY + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            Sub_R(PeekByte(offset));
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x9C:  //SBC A, IYH
                            // Log("SBC A, IYH");
                            Sbc_R(IYH);
                            break;

                        case 0x9D:  //SBC A, IYL
                            // Log("SBC A, IYL");
                            Sbc_R(IYL);
                            break;

                        case 0x9E:  //SBC A, (IY + d)
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IY + disp; //The displacement required
                            // Log(string.Format("SBC A, (IY + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            Sbc_R(PeekByte(offset));
                            PC++;
                            MemPtr = offset;
                            break;
                        #endregion

                        #region Increment/Decrements
                        case 0x23:  //INC IY
                            // Log("INC IY");
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 2;
                            //else
                            Contend(IR, 1, 2);
                            IY++;
                            break;

                        case 0x24:  //INC IYH
                            // Log("INC IYH");
                            IYH = Inc(IYH);
                            break;

                        case 0x25:  //DEC IYH
                            // Log("DEC IYH");
                            IYH = Dec(IYH);
                            break;

                        case 0x2B:  //DEC IY
                            // Log("DEC IY");
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 2;
                            //else
                            Contend(IR, 1, 2);
                            IY--;
                            break;

                        case 0x2C:  //INC IYL
                            // Log("INC IYL");
                            IYL = Inc(IYL);
                            break;

                        case 0x2D:  //DEC IYL
                            // Log("DEC IYL");
                            IYL = Dec(IYL);
                            break;

                        case 0x34:  //INC (IY + d)
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IY + disp; //The displacement required
                            // Log(string.Format("INC (IY + {0:X})", disp));
                            Contend(PC, 1, 5);
                            disp = Inc(PeekByte(offset));
                            Contend(offset, 1, 1);
                            PokeByte(offset, disp);
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x35:  //DEC (IY + d)
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IY + disp; //The displacement required
                            // Log(string.Format("DEC (IY + {0:X})", disp));
                            Contend(PC, 1, 5);
                            disp = Dec(PeekByte(offset));
                            Contend(offset, 1, 1);
                            PokeByte(offset, disp);
                            PC++;
                            MemPtr = offset;
                            break;
                        #endregion

                        #region Bitwise operators

                        case 0xA4:  //AND IYH
                            // Log("AND IYH");
                            And_R(IYH);
                            break;

                        case 0xA5:  //AND IYL
                            // Log("AND IYL");
                            And_R(IYL);
                            break;

                        case 0xA6:  //AND (IY + d)
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IY + disp; //The displacement required
                            // Log(string.Format("AND (IY + {0:X})", disp));
                            // if (model == MachineModel._plus3)
                            //     totalTStates += 5;
                            // else
                            Contend(PC, 1, 5);
                            And_R(PeekByte(offset));
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0xAC:  //XOR IYH
                            // Log("XOR IYH");
                            Xor_R(IYH);
                            break;

                        case 0xAD:  //XOR IYL
                            // Log("XOR IYL");
                            Xor_R(IYL);
                            break;

                        case 0xAE:  //XOR (IY + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("XOR (IY + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            Xor_R(PeekByte(offset));
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0xB4:  //OR IYH
                            // Log("OR IYH");
                            Or_R(IYH);
                            break;

                        case 0xB5:  //OR IYL
                            // Log("OR IYL");
                            Or_R(IYL);
                            break;

                        case 0xB6:  //OR (IY + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("OR (IY + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            Or_R(PeekByte(offset));
                            PC++;
                            MemPtr = offset;
                            break;
                        #endregion

                        #region Compare operator
                        case 0xBC:  //CP IYH
                            // Log("CP IYH");
                            Cp_R(IYH);
                            break;

                        case 0xBD:  //CP IYL
                            // Log("CP IYL");
                            Cp_R(IYL);
                            break;

                        case 0xBE:  //CP (IY + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("CP (IY + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            Cp_R(PeekByte(offset));
                            PC++;
                            MemPtr = offset;
                            break;
                        #endregion

                        #region Load instructions
                        case 0x21:  //LD IY, nn
                            // Log(string.Format("LD IY, {0,-6:X}", PeekWord(PC)));
                            IY = PeekWord(PC);
                            PC += 2;
                            break;

                        case 0x22:  //LD (nn), IY
                            // Log(string.Format("LD ({0:X}), IY", PeekWord(PC)));
                            addr = PeekWord(PC);
                            PokeWord(addr, IY);
                            PC += 2;
                            MemPtr = addr + 1;
                            break;

                        case 0x26:  //LD IYH, n
                            // Log(string.Format("LD IYH, {0:X}", PeekByte(PC)));
                            IYH = PeekByte(PC);
                            PC++;
                            break;

                        case 0x2A:  //LD IY, (nn)
                            // Log(string.Format("LD IY, ({0:X})", PeekWord(PC)));
                            addr = PeekWord(PC);
                            IY = PeekWord(addr);
                            PC += 2;
                            MemPtr = addr + 1;
                            break;

                        case 0x2E:  //LD IYL, n
                            // Log(string.Format("LD IYL, {0:X}", PeekByte(PC)));
                            IYL = PeekByte(PC);
                            PC++;
                            break;

                        case 0x36:  //LD (IY + d), n
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("LD (IY + {0:X}), {1,-6:X}", disp, PeekByte(PC + 1)));
                            disp = PeekByte(PC + 1);
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 2;
                            //else
                            Contend(PC + 1, 1, 2);
                            PokeByte(offset, disp);
                            PC += 2;
                            MemPtr = offset;
                            break;

                        case 0x44:  //LD B, IYH
                            // Log("LD B, IYH");
                            B = IYH;
                            break;

                        case 0x45:  //LD B, IYL
                            // Log("LD B, IYL");
                            B = IYL;
                            break;

                        case 0x46:  //LD B, (IY + d)
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IY + disp; //The displacement required
                            // Log(string.Format("LD B, (IY + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);

                            B = PeekByte(offset);
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x4C:  //LD C, IYH
                            // Log("LD C, IYH");
                            C = IYH;
                            break;

                        case 0x4D:  //LD C, IYL
                            // Log("LD C, IYL");
                            C = IYL;
                            break;

                        case 0x4E:  //LD C, (IY + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("LD C, (IY + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            C = PeekByte(offset);
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x54:  //LD D, IYH
                            // Log("LD D, IYH");
                            //tstates += 4;
                            D = IYH;
                            break;

                        case 0x55:  //LD D, IYL
                            // Log("LD D, IYL");
                            //tstates += 4;
                            D = IYL;
                            break;

                        case 0x56:  //LD D, (IY + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("LD D, (IY + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            D = PeekByte(offset);
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x5C:  //LD E, IYH
                            // Log("LD E, IYH");
                            //tstates += 4;
                            E = IYH;
                            break;

                        case 0x5D:  //LD E, IYL
                            // Log("LD E, IYL");
                            //tstates += 4;
                            E = IYL;
                            break;

                        case 0x5E:  //LD E, (IY + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("LD E, (IY + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            E = PeekByte(offset);
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x60:  //LD IYH, B
                            // Log("LD IYH, B");
                            //tstates += 4;
                            IYH = B;
                            break;

                        case 0x61:  //LD IYH, C
                            // Log("LD IYH, C");
                            //tstates += 4;
                            IYH = C;
                            break;

                        case 0x62:  //LD IYH, D
                            // Log("LD IYH, D");
                            //tstates += 4;
                            IYH = D;
                            break;

                        case 0x63:  //LD IYH, E
                            // Log("LD IYH, E");
                            //tstates += 4;
                            IYH = E;
                            break;

                        case 0x64:  //LD IYH, IYH
                            // Log("LD IYH, IYH");
                            //tstates += 4;
                            IYH = IYH;
                            break;

                        case 0x65:  //LD IYH, IYL
                            // Log("LD IYH, IYL");
                            //tstates += 4;
                            IYH = IYL;
                            break;

                        case 0x66:  //LD H, (IY + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("LD H, (IY + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            H = PeekByte(offset);
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x67:  //LD IYH, A
                            // Log("LD IYH, A");
                            //tstates += 4;
                            IYH = A;
                            break;

                        case 0x68:  //LD IYL, B
                            // Log("LD IYL, B");
                            //tstates += 4;
                            IYL = B;
                            break;

                        case 0x69:  //LD IYL, C
                            // Log("LD IYL, C");
                            //tstates += 4;
                            IYL = C;
                            break;

                        case 0x6A:  //LD IYL, D
                            // Log("LD IYL, D");
                            //tstates += 4;
                            IYL = D;
                            break;

                        case 0x6B:  //LD IYL, E
                            // Log("LD IYL, E");
                            //tstates += 4;
                            IYL = E;
                            break;

                        case 0x6C:  //LD IYL, IYH
                            // Log("LD IYL, IYH");
                            //tstates += 4;
                            IYL = IYH;
                            break;

                        case 0x6D:  //LD IYL, IYL
                            // Log("LD IYL, IYL");
                            //tstates += 4;
                            IYL = IYL;
                            break;

                        case 0x6E:  //LD L, (IY + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("LD L, (IY + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            L = PeekByte(offset);
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x6F:  //LD IYL, A
                            // Log("LD IYL, A");
                            //tstates += 4;
                            IYL = A;
                            break;

                        case 0x70:  //LD (IY + d), B
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("LD (IY + {0:X}), B", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            PokeByte(offset, B);
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x71:  //LD (IY + d), C
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("LD (IY + {0:X}), C", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            PokeByte(offset, C);
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x72:  //LD (IY + d), D
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("LD (IY + {0:X}), D", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            PokeByte(offset, D);
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x73:  //LD (IY + d), E
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("LD (IY + {0:X}), E", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            PokeByte(offset, E);
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x74:  //LD (IY + d), H
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("LD (IY + {0:X}), H", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            PokeByte(offset, H);
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x75:  //LD (IY + d), L
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("LD (IY + {0:X}), L", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            PokeByte(offset, L);
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x77:  //LD (IY + d), A
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("LD (IY + {0:X}), A", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            PokeByte(offset, A);
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0x7C:  //LD A, IYH
                            // Log("LD A, IYH");
                            A = IYH;
                            break;

                        case 0x7D:  //LD A, IYL
                            // Log("LD A, IYL");
                            A = IYL;
                            break;

                        case 0x7E:  //LD A, (IY + d)
                            disp = GetDisplacement(PeekByte(PC));

                            offset = IY + disp; //The displacement required
                            // Log(string.Format("LD A, (IY + {0:X})", disp));
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 5;
                            //else
                            Contend(PC, 1, 5);
                            A = PeekByte(offset);
                            PC++;
                            MemPtr = offset;
                            break;

                        case 0xF9:  //LD SP, IY
                            // Log("LD SP, IY");
                            Contend(IR, 1, 2);
                            SP = IY;
                            break;
                        #endregion

                        #region All FDCB instructions
                        case 0xCB:
                            disp = GetDisplacement(PeekByte(PC));
                            offset = IY + disp; //The displacement required
                            PC++;
                            opcode = GetOpcode(PC);      //The opcode comes after the offset byte!
                            Contend(PC, 1, 2);
                            PC++;
                            disp = PeekByte(offset);
                            Contend(offset, 1, 1);
                            // if ((opcode >= 0x40) && (opcode <= 0x7f))
                            MemPtr = offset;

                            switch (opcode) {
                                case 0x00: //LD B, RLC (IY+d)
                                    // Log(string.Format("LD B, RLC (IY + {0:X})", disp));
                                    B = Rlc_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x01: //LD C, RLC (IY+d)
                                    // Log(string.Format("LD C, RLC (IY + {0:X})", disp));
                                    C = Rlc_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x02: //LD D, RLC (IY+d)
                                    // Log(string.Format("LD D, RLC (IY + {0:X})", disp));
                                    D = Rlc_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x03: //LD E, RLC (IY+d)
                                    // Log(string.Format("LD E, RLC (IY + {0:X})", disp));
                                    E = Rlc_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x04: //LD H, RLC (IY+d)
                                    // Log(string.Format("LD H, RLC (IY + {0:X})", disp));
                                    H = Rlc_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x05: //LD L, RLC (IY+d)
                                    // Log(string.Format("LD L, RLC (IY + {0:X})", disp));
                                    L = Rlc_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x06:  //RLC (IY + d)
                                    // Log(string.Format("RLC (IY + {0:X})", disp));
                                    PokeByte(offset, Rlc_R(disp));
                                    break;

                                case 0x07: //LD A, RLC (IY+d)
                                    // Log(string.Format("LD A, RLC (IY + {0:X})", disp));
                                    A = Rlc_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x08: //LD B, RRC (IY+d)
                                    // Log(string.Format("LD B, RRC (IY + {0:X})", disp));
                                    B = Rrc_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x09: //LD C, RRC (IY+d)
                                    // Log(string.Format("LD C, RRC (IY + {0:X})", disp));
                                    C = Rrc_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x0A: //LD D, RRC (IY+d)
                                    // Log(string.Format("LD D, RRC (IY + {0:X})", disp));
                                    D = Rrc_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x0B: //LD E, RRC (IY+d)
                                    // Log(string.Format("LD E, RRC (IY + {0:X})", disp));
                                    E = Rrc_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x0C: //LD H, RRC (IY+d)
                                    // Log(string.Format("LD H, RRC (IY + {0:X})", disp));
                                    H = Rrc_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x0D: //LD L, RRC (IY+d)
                                    // Log(string.Format("LD L, RRC (IY + {0:X})", disp));
                                    L = Rrc_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x0E:  //RRC (IY + d)
                                    // Log(string.Format("RRC (IY + {0:X})", disp));
                                    PokeByte(offset, Rrc_R(disp));
                                    break;

                                case 0x0F: //LD A, RRC (IY+d)
                                    // Log(string.Format("LD A, RRC (IY + {0:X})", disp));
                                    A = Rrc_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x10: //LD B, RL (IY+d)
                                    // Log(string.Format("LD B, RL (IY + {0:X})", disp));
                                    B = Rl_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x11: //LD C, RL (IY+d)
                                    // Log(string.Format("LD C, RL (IY + {0:X})", disp));
                                    C = Rl_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x12: //LD D, RL (IY+d)
                                    // Log(string.Format("LD D, RL (IY + {0:X})", disp));
                                    D = Rl_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x13: //LD E, RL (IY+d)
                                    // Log(string.Format("LD E, RL (IY + {0:X})", disp));
                                    E = Rl_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x14: //LD H, RL (IY+d)
                                    // Log(string.Format("LD H, RL (IY + {0:X})", disp));
                                    H = Rl_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x15: //LD L, RL (IY+d)
                                    // Log(string.Format("LD L, RL (IY + {0:X})", disp));
                                    L = Rl_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x16:  //RL (IY + d)
                                    // Log(string.Format("RL (IY + {0:X})", disp));
                                    PokeByte(offset, Rl_R(disp));

                                    break;

                                case 0x17: //LD A, RL (IY+d)
                                    // Log(string.Format("LD A, RL (IY + {0:X})", disp));
                                    A = Rl_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x18: //LD B, RR (IY+d)
                                    // Log(string.Format("LD B, RR (IY + {0:X})", disp));
                                    B = Rr_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x19: //LD C, RR (IY+d)
                                    // Log(string.Format("LD C, RR (IY + {0:X})", disp));
                                    C = Rr_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x1A: //LD D, RR (IY+d)
                                    // Log(string.Format("LD D, RR (IY + {0:X})", disp));
                                    D = Rr_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x1B: //LD E, RR (IY+d)
                                    // Log(string.Format("LD E, RR (IY + {0:X})", disp));
                                    E = Rr_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x1C: //LD H, RR (IY+d)
                                    // Log(string.Format("LD H, RR (IY + {0:X})", disp));
                                    H = Rr_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x1D: //LD L, RRC (IY+d)
                                    // Log(string.Format("LD L, RR (IY + {0:X})", disp));
                                    L = Rr_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x1E:  //RR (IY + d)
                                    // Log(string.Format("RR (IY + {0:X})", disp));
                                    PokeByte(offset, Rr_R(disp));
                                    break;

                                case 0x1F: //LD A, RRC (IY+d)
                                    // Log(string.Format("LD A, RR (IY + {0:X})", disp));
                                    A = Rr_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x20: //LD B, SLA (IY+d)
                                    // Log(string.Format("LD B, SLA (IY + {0:X})", disp));
                                    B = Sla_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x21: //LD C, SLA (IY+d)
                                    // Log(string.Format("LD C, SLA (IY + {0:X})", disp));
                                    C = Sla_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x22: //LD D, SLA (IY+d)
                                    // Log(string.Format("LD D, SLA (IY + {0:X})", disp));
                                    D = Sla_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x23: //LD E, SLA (IY+d)
                                    // Log(string.Format("LD E, SLA (IY + {0:X})", disp));
                                    E = Sla_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x24: //LD H, SLA (IY+d)
                                    // Log(string.Format("LD H, SLA (IY + {0:X})", disp));
                                    H = Sla_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x25: //LD L, SLA (IY+d)
                                    // Log(string.Format("LD L, SLA (IY + {0:X})", disp));
                                    L = Sla_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x26:  //SLA (IY + d)
                                    // Log(string.Format("SLA (IY + {0:X})", disp));
                                    PokeByte(offset, Sla_R(disp));
                                    break;

                                case 0x27: //LD A, SLA (IY+d)
                                    // Log(string.Format("LD A, SLA (IY + {0:X})", disp));
                                    A = Sla_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x28: //LD B, SRA (IY+d)
                                    // Log(string.Format("LD B, SRA (IY + {0:X})", disp));
                                    B = Sra_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x29: //LD C, SRA (IY+d)
                                    // Log(string.Format("LD C, SRA (IY + {0:X})", disp));
                                    C = Sra_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x2A: //LD D, SRA (IY+d)
                                    // Log(string.Format("LD D, SRA (IY + {0:X})", disp));
                                    D = Sra_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x2B: //LD E, SRA (IY+d)
                                    // Log(string.Format("LD E, SRA (IY + {0:X})", disp));
                                    E = Sra_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x2C: //LD H, SRA (IY+d)
                                    // Log(string.Format("LD H, SRA (IY + {0:X})", disp));
                                    H = Sra_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x2D: //LD L, SRA (IY+d)
                                    // Log(string.Format("LD L, SRA (IY + {0:X})", disp));
                                    L = Sra_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x2E:  //SRA (IY + d)
                                    // Log(string.Format("SRA (IY + {0:X})", disp));
                                    PokeByte(offset, Sra_R(disp));
                                    break;

                                case 0x2F: //LD A, SRA (IY+d)
                                    // Log(string.Format("LD A, SRA (IY + {0:X})", disp));
                                    A = Sra_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x30: //LD B, SLL (IY+d)
                                    // Log(string.Format("LD B, SLL (IY + {0:X})", disp));
                                    B = Sll_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x31: //LD C, SLL (IY+d)
                                    // Log(string.Format("LD C, SLL (IY + {0:X})", disp));
                                    C = Sll_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x32: //LD D, SLL (IY+d)
                                    // Log(string.Format("LD D, SLL (IY + {0:X})", disp));
                                    D = Sll_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x33: //LD E, SLL (IY+d)
                                    // Log(string.Format("LD E, SLL (IY + {0:X})", disp));
                                    E = Sll_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x34: //LD H, SLL (IY+d)
                                    // Log(string.Format("LD H, SLL (IY + {0:X})", disp));
                                    H = Sll_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x35: //LD L, SLL (IY+d)
                                    // Log(string.Format("LD L, SLL (IY + {0:X})", disp));
                                    L = Sll_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x36:  //SLL (IY + d)
                                    // Log(string.Format("SLL (IY + {0:X})", disp));
                                    PokeByte(offset, Sll_R(disp));
                                    break;

                                case 0x37: //LD A, SLL (IY+d)
                                    // Log(string.Format("LD A, SLL (IY + {0:X})", disp));
                                    A = Sll_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x38: //LD B, SRL (IY+d)
                                    // Log(string.Format("LD B, SRL (IY + {0:X})", disp));
                                    B = Srl_R(disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x39: //LD C, SRL (IY+d)
                                    // Log(string.Format("LD C, SRL (IY + {0:X})", disp));
                                    C = Srl_R(disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x3A: //LD D, SRL (IY+d)
                                    // Log(string.Format("LD D, SRL (IY + {0:X})", disp));
                                    D = Srl_R(disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x3B: //LD E, SRL (IY+d)
                                    // Log(string.Format("LD E, SRL (IY + {0:X})", disp));
                                    E = Srl_R(disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x3C: //LD H, SRL (IY+d)
                                    // Log(string.Format("LD H, SRL (IY + {0:X})", disp));
                                    H = Srl_R(disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x3D: //LD L, SRL (IY+d)
                                    // Log(string.Format("LD L, SRL (IY + {0:X})", disp));
                                    L = Srl_R(disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x3E:  //SRL (IY + d)
                                    // Log(string.Format("SRL (IY + {0:X})", disp));
                                    PokeByte(offset, Srl_R(disp));
                                    break;

                                case 0x3F: //LD A, SRL (IY+d)
                                    // Log(string.Format("LD A, SRL (IY + {0:X})", disp));
                                    A = Srl_R(disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x40:  //BIT 0, (IY + d)
                                case 0x41:  //BIT 0, (IY + d)
                                case 0x42:  //BIT 0, (IY + d)
                                case 0x43:  //BIT 0, (IY + d)
                                case 0x44:  //BIT 0, (IY + d)
                                case 0x45:  //BIT 0, (IY + d)
                                case 0x46:  //BIT 0, (IY + d)
                                case 0x47:  //BIT 0, (IY + d)
                                    // Log(string.Format("BIT 0, (IY + {0:X})", disp));
                                    Bit_R(0, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x48:  //BIT 1, (IY + d)
                                case 0x49:  //BIT 1, (IY + d)
                                case 0x4A:  //BIT 1, (IY + d)
                                case 0x4B:  //BIT 1, (IY + d)
                                case 0x4C:  //BIT 1, (IY + d)
                                case 0x4D:  //BIT 1, (IY + d)
                                case 0x4E:  //BIT 1, (IY + d)
                                case 0x4F:  //BIT 1, (IY + d)
                                    // Log(string.Format("BIT 1, (IY + {0:X})", disp));
                                    Bit_R(1, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x50:  //BIT 2, (IY + d)
                                case 0x51:  //BIT 2, (IY + d)
                                case 0x52:  //BIT 2, (IY + d)
                                case 0x53:  //BIT 2, (IY + d)
                                case 0x54:  //BIT 2, (IY + d)
                                case 0x55:  //BIT 2, (IY + d)
                                case 0x56:  //BIT 2, (IY + d)
                                case 0x57:  //BIT 2, (IY + d)
                                    // Log(string.Format("BIT 2, (IY + {0:X})", disp));
                                    Bit_R(2, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x58:  //BIT 3, (IY + d)
                                case 0x59:  //BIT 3, (IY + d)
                                case 0x5A:  //BIT 3, (IY + d)
                                case 0x5B:  //BIT 3, (IY + d)
                                case 0x5C:  //BIT 3, (IY + d)
                                case 0x5D:  //BIT 3, (IY + d)
                                case 0x5E:  //BIT 3, (IY + d)
                                case 0x5F:  //BIT 3, (IY + d)
                                    // Log(string.Format("BIT 3, (IY + {0:X})", disp));
                                    Bit_R(3, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x60:  //BIT 4, (IY + d)
                                case 0x61:  //BIT 4, (IY + d)
                                case 0x62:  //BIT 4, (IY + d)
                                case 0x63:  //BIT 4, (IY + d)
                                case 0x64:  //BIT 4, (IY + d)
                                case 0x65:  //BIT 4, (IY + d)
                                case 0x66:  //BIT 4, (IY + d)
                                case 0x67:  //BIT 4, (IY + d)
                                    // Log(string.Format("BIT 4, (IY + {0:X})", disp));
                                    Bit_R(4, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x68:  //BIT 5, (IY + d)
                                case 0x69:  //BIT 5, (IY + d)
                                case 0x6A:  //BIT 5, (IY + d)
                                case 0x6B:  //BIT 5, (IY + d)
                                case 0x6C:  //BIT 5, (IY + d)
                                case 0x6D:  //BIT 5, (IY + d)
                                case 0x6E:  //BIT 5, (IY + d)
                                case 0x6F:  //BIT 5, (IY + d)
                                    // Log(string.Format("BIT 5, (IY + {0:X})", disp));
                                    Bit_R(5, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x70://BIT 6, (IY + d)
                                case 0x71://BIT 6, (IY + d)
                                case 0x72://BIT 6, (IY + d)
                                case 0x73://BIT 6, (IY + d)
                                case 0x74://BIT 6, (IY + d)
                                case 0x75://BIT 6, (IY + d)
                                case 0x76://BIT 6, (IY + d)
                                case 0x77:  //BIT 6, (IY + d)
                                    // Log(string.Format("BIT 6, (IY + {0:X})", disp));
                                    Bit_R(6, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x78:  //BIT 7, (IY + d)
                                case 0x79:  //BIT 7, (IY + d)
                                case 0x7A:  //BIT 7, (IY + d)
                                case 0x7B:  //BIT 7, (IY + d)
                                case 0x7C:  //BIT 7, (IY + d)
                                case 0x7D:  //BIT 7, (IY + d)
                                case 0x7E:  //BIT 7, (IY + d)
                                case 0x7F:  //BIT 7, (IY + d)
                                    // Log(string.Format("BIT 7, (IY + {0:X})", disp));
                                    Bit_R(7, disp);
                                    SetF3((MemPtr & MEMPTR_11) != 0);
                                    SetF5((MemPtr & MEMPTR_13) != 0);
                                    break;

                                case 0x80: //LD B, RES 0, (IY+d)
                                    // Log(string.Format("LD B, RES 0, (IY + {0:X})", disp));
                                    B = Res_R(0, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x81: //LD C, RES 0, (IY+d)
                                    // Log(string.Format("LD C, RES 0, (IY + {0:X})", disp));
                                    C = Res_R(0, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x82: //LD D, RES 0, (IY+d)
                                    // Log(string.Format("LD D, RES 0, (IY + {0:X})", disp));
                                    D = Res_R(0, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x83: //LD E, RES 0, (IY+d)
                                    // Log(string.Format("LD E, RES 0, (IY + {0:X})", disp));
                                    E = Res_R(0, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x84: //LD H, RES 0, (IY+d)
                                    // Log(string.Format("LD H, RES 0, (IY + {0:X})", disp));
                                    H = Res_R(0, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x85: //LD L, RES 0, (IY+d)
                                    // Log(string.Format("LD L, RES 0, (IY + {0:X})", disp));
                                    L = Res_R(0, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x86:  //RES 0, (IY + d)
                                    // Log(string.Format("RES 0, (IY + {0:X})", disp));
                                    PokeByte(offset, Res_R(0, disp));
                                    break;

                                case 0x87: //LD A, RES 0, (IY+d)
                                    // Log(string.Format("LD A, RES 0, (IY + {0:X})", disp));
                                    A = Res_R(0, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x88: //LD B, RES 1, (IY+d)
                                    // Log(string.Format("LD B, RES 1, (IY + {0:X})", disp));
                                    B = Res_R(1, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x89: //LD C, RES 1, (IY+d)
                                    // Log(string.Format("LD C, RES 1, (IY + {0:X})", disp));
                                    C = Res_R(1, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x8A: //LD D, RES 1, (IY+d)
                                    // Log(string.Format("LD D, RES 1, (IY + {0:X})", disp));
                                    D = Res_R(1, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x8B: //LD E, RES 1, (IY+d)
                                    // Log(string.Format("LD E, RES 1, (IY + {0:X})", disp));
                                    E = Res_R(1, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x8C: //LD H, RES 1, (IY+d)
                                    // Log(string.Format("LD H, RES 1, (IY + {0:X})", disp));
                                    H = Res_R(1, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x8D: //LD L, RES 1, (IY+d)
                                    // Log(string.Format("LD L, RES 1, (IY + {0:X})", disp));
                                    L = Res_R(1, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x8E:  //RES 1, (IY + d)
                                    // Log(string.Format("RES 1, (IY + {0:X})", disp));
                                    PokeByte(offset, Res_R(1, disp));
                                    break;

                                case 0x8F: //LD A, RES 1, (IY+d)
                                    // Log(string.Format("LD A, RES 1, (IY + {0:X})", disp));
                                    A = Res_R(1, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x90: //LD B, RES 2, (IY+d)
                                    // Log(string.Format("LD B, RES 2, (IY + {0:X})", disp));
                                    B = Res_R(2, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x91: //LD C, RES 2, (IY+d)
                                    // Log(string.Format("LD C, RES 2, (IY + {0:X})", disp));
                                    C = Res_R(2, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x92: //LD D, RES 2, (IY+d)
                                    // Log(string.Format("LD D, RES 2, (IY + {0:X})", disp));
                                    D = Res_R(2, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x93: //LD E, RES 2, (IY+d)
                                    // Log(string.Format("LD E, RES 2, (IY + {0:X})", disp));
                                    E = Res_R(2, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x94: //LD H, RES 2, (IY+d)
                                    // Log(string.Format("LD H, RES 2, (IY + {0:X})", disp));
                                    H = Res_R(2, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x95: //LD L, RES 2, (IY+d)
                                    // Log(string.Format("LD L, RES 2, (IY + {0:X})", disp));
                                    L = Res_R(2, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x96:  //RES 2, (IY + d)
                                    // Log(string.Format("RES 2, (IY + {0:X})", disp));
                                    PokeByte(offset, Res_R(2, disp));
                                    break;

                                case 0x97: //LD A, RES 2, (IY+d)
                                    // Log(string.Format("LD A, RES 2, (IY + {0:X})", disp));
                                    A = Res_R(2, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0x98: //LD B, RES 3, (IY+d)
                                    // Log(string.Format("LD B, RES 3, (IY + {0:X})", disp));
                                    B = Res_R(3, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0x99: //LD C, RES 3, (IY+d)
                                    // Log(string.Format("LD C, RES 3, (IY + {0:X})", disp));
                                    C = Res_R(3, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0x9A: //LD D, RES 3, (IY+d)
                                    // Log(string.Format("LD D, RES 3, (IY + {0:X})", disp));
                                    D = Res_R(3, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0x9B: //LD E, RES 3, (IY+d)
                                    // Log(string.Format("LD E, RES 3, (IY + {0:X})", disp));
                                    E = Res_R(3, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0x9C: //LD H, RES 3, (IY+d)
                                    // Log(string.Format("LD H, RES 3, (IY + {0:X})", disp));
                                    H = Res_R(3, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0x9D: //LD L, RES 3, (IY+d)
                                    // Log(string.Format("LD L, RES 3, (IY + {0:X})", disp));
                                    L = Res_R(3, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0x9E:  //RES 3, (IY + d)
                                    // Log(string.Format("RES 3, (IY + {0:X})", disp));
                                    PokeByte(offset, Res_R(3, disp));
                                    break;

                                case 0x9F: //LD A, RES 3, (IY+d)
                                    // Log(string.Format("LD A, RES 3, (IY + {0:X})", disp));
                                    A = Res_R(3, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xA0: //LD B, RES 4, (IY+d)
                                    // Log(string.Format("LD B, RES 4, (IY + {0:X})", disp));
                                    B = Res_R(4, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xA1: //LD C, RES 4, (IY+d)
                                    // Log(string.Format("LD C, RES 4, (IY + {0:X})", disp));
                                    C = Res_R(4, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xA2: //LD D, RES 4, (IY+d)
                                    // Log(string.Format("LD D, RES 4, (IY + {0:X})", disp));
                                    D = Res_R(4, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xA3: //LD E, RES 4, (IY+d)
                                    // Log(string.Format("LD E, RES 4, (IY + {0:X})", disp));
                                    E = Res_R(4, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xA4: //LD H, RES 4, (IY+d)
                                    // Log(string.Format("LD H, RES 4, (IY + {0:X})", disp));
                                    H = Res_R(4, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xA5: //LD L, RES 4, (IY+d)
                                    // Log(string.Format("LD L, RES 4, (IY + {0:X})", disp));
                                    L = Res_R(4, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xA6:  //RES 4, (IY + d)
                                    // Log(string.Format("RES 4, (IY + {0:X})", disp));
                                    PokeByte(offset, Res_R(4, disp));
                                    break;

                                case 0xA7: //LD A, RES 4, (IY+d)
                                    // Log(string.Format("LD A, RES 4, (IY + {0:X})", disp));
                                    A = Res_R(4, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xA8: //LD B, RES 5, (IY+d)
                                    // Log(string.Format("LD B, RES 5, (IY + {0:X})", disp));
                                    B = Res_R(5, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xA9: //LD C, RES 5, (IY+d)
                                    // Log(string.Format("LD C, RES 5, (IY + {0:X})", disp));
                                    C = Res_R(5, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xAA: //LD D, RES 5, (IY+d)
                                    // Log(string.Format("LD D, RES 5, (IY + {0:X})", disp));
                                    D = Res_R(5, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xAB: //LD E, RES 5, (IY+d)
                                    // Log(string.Format("LD E, RES 5, (IY + {0:X})", disp));
                                    E = Res_R(5, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xAC: //LD H, RES 5, (IY+d)
                                    // Log(string.Format("LD H, RES 5, (IY + {0:X})", disp));
                                    H = Res_R(5, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xAD: //LD L, RES 5, (IY+d)
                                    // Log(string.Format("LD L, RES 5, (IY + {0:X})", disp));
                                    L = Res_R(5, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xAE:  //RES 5, (IY + d)
                                    // Log(string.Format("RES 5, (IY + {0:X})", disp));
                                    PokeByte(offset, Res_R(5, disp));
                                    break;

                                case 0xAF: //LD A, RES 5, (IY+d)
                                    // Log(string.Format("LD A, RES 5, (IY + {0:X})", disp));
                                    A = Res_R(5, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xB0: //LD B, RES 6, (IY+d)
                                    // Log(string.Format("LD B, RES 6, (IY + {0:X})", disp));
                                    B = Res_R(6, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xB1: //LD C, RES 6, (IY+d)
                                    // Log(string.Format("LD C, RES 6, (IY + {0:X})", disp));
                                    C = Res_R(6, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xB2: //LD D, RES 6, (IY+d)
                                    // Log(string.Format("LD D, RES 6, (IY + {0:X})", disp));
                                    D = Res_R(5, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xB3: //LD E, RES 6, (IY+d)
                                    // Log(string.Format("LD E, RES 6, (IY + {0:X})", disp));
                                    E = Res_R(6, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xB4: //LD H, RES 5, (IY+d)
                                    // Log(string.Format("LD H, RES 6, (IY + {0:X})", disp));
                                    H = Res_R(6, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xB5: //LD L, RES 5, (IY+d)
                                    // Log(string.Format("LD L, RES 6, (IY + {0:X})", disp));
                                    L = Res_R(6, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xB6:  //RES 6, (IY + d)
                                    // Log(string.Format("RES 6, (IY + {0:X})", disp));
                                    PokeByte(offset, Res_R(6, disp));
                                    break;

                                case 0xB7: //LD A, RES 5, (IY+d)
                                    // Log(string.Format("LD A, RES 6, (IY + {0:X})", disp));
                                    A = Res_R(6, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xB8: //LD B, RES 7, (IY+d)
                                    // Log(string.Format("LD B, RES 7, (IY + {0:X})", disp));
                                    B = Res_R(7, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xB9: //LD C, RES 7, (IY+d)
                                    // Log(string.Format("LD C, RES 7, (IY + {0:X})", disp));
                                    C = Res_R(7, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xBA: //LD D, RES 7, (IY+d)
                                    // Log(string.Format("LD D, RES 7, (IY + {0:X})", disp));
                                    D = Res_R(7, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xBB: //LD E, RES 7, (IY+d)
                                    // Log(string.Format("LD E, RES 7, (IY + {0:X})", disp));
                                    E = Res_R(7, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xBC: //LD H, RES 7, (IY+d)
                                    // Log(string.Format("LD H, RES 7, (IY + {0:X})", disp));
                                    H = Res_R(7, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xBD: //LD L, RES 7, (IY+d)
                                    // Log(string.Format("LD L, RES 7, (IY + {0:X})", disp));
                                    L = Res_R(7, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xBE:  //RES 7, (IY + d)
                                    // Log(string.Format("RES 7, (IY + {0:X})", disp));
                                    PokeByte(offset, Res_R(7, disp));
                                    break;

                                case 0xBF: //LD A, RES 7, (IY+d)
                                    // Log(string.Format("LD A, RES 7, (IY + {0:X})", disp));
                                    A = Res_R(7, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xC0: //LD B, SET 0, (IY+d)
                                    // Log(string.Format("LD B, SET 0, (IY + {0:X})", disp));
                                    B = Set_R(0, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xC1: //LD C, SET 0, (IY+d)
                                    // Log(string.Format("LD C, SET 0, (IY + {0:X})", disp));
                                    C = Set_R(0, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xC2: //LD D, SET 0, (IY+d)
                                    // Log(string.Format("LD D, SET 0, (IY + {0:X})", disp));
                                    D = Set_R(0, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xC3: //LD E, SET 0, (IY+d)
                                    // Log(string.Format("LD E, SET 0, (IY + {0:X})", disp));
                                    E = Set_R(0, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xC4: //LD H, SET 0, (IY+d)
                                    // Log(string.Format("LD H, SET 0, (IY + {0:X})", disp));
                                    H = Set_R(0, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xC5: //LD L, SET 0, (IY+d)
                                    // Log(string.Format("LD L, SET 0, (IY + {0:X})", disp));
                                    L = Set_R(0, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xC6:  //SET 0, (IY + d)
                                    // Log(string.Format("SET 0, (IY + {0:X})", disp));
                                    PokeByte(offset, Set_R(0, disp));
                                    break;

                                case 0xC7: //LD A, SET 0, (IY+d)
                                    // Log(string.Format("LD A, SET 0, (IY + {0:X})", disp));
                                    A = Set_R(0, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xC8: //LD B, SET 1, (IY+d)
                                    // Log(string.Format("LD B, SET 1, (IY + {0:X})", disp));
                                    B = Set_R(1, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xC9: //LD C, SET 0, (IY+d)
                                    // Log(string.Format("LD C, SET 1, (IY + {0:X})", disp));
                                    C = Set_R(1, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xCA: //LD D, SET 1, (IY+d)
                                    // Log(string.Format("LD D, SET 1, (IY + {0:X})", disp));
                                    D = Set_R(1, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xCB: //LD E, SET 1, (IY+d)
                                    // Log(string.Format("LD E, SET 1, (IY + {0:X})", disp));
                                    E = Set_R(1, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xCC: //LD H, SET 1, (IY+d)
                                    // Log(string.Format("LD H, SET 1, (IY + {0:X})", disp));
                                    H = Set_R(1, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xCD: //LD L, SET 1, (IY+d)
                                    // Log(string.Format("LD L, SET 1, (IY + {0:X})", disp));
                                    L = Set_R(1, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xCE:  //SET 1, (IY + d)
                                    // Log(string.Format("SET 1, (IY + {0:X})", disp));
                                    PokeByte(offset, Set_R(1, disp));
                                    break;

                                case 0xCF: //LD A, SET 1, (IY+d)
                                    // Log(string.Format("LD A, SET 1, (IY + {0:X})", disp));
                                    A = Set_R(1, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xD0: //LD B, SET 2, (IY+d)
                                    // Log(string.Format("LD B, SET 2, (IY + {0:X})", disp));
                                    B = Set_R(2, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xD1: //LD C, SET 2, (IY+d)
                                    // Log(string.Format("LD C, SET 2, (IY + {0:X})", disp));
                                    C = Set_R(2, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xD2: //LD D, SET 2, (IY+d)
                                    // Log(string.Format("LD D, SET 2, (IY + {0:X})", disp));
                                    D = Set_R(2, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xD3: //LD E, SET 2, (IY+d)
                                    // Log(string.Format("LD E, SET 2, (IY + {0:X})", disp));
                                    E = Set_R(2, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xD4: //LD H, SET 21, (IY+d)
                                    // Log(string.Format("LD H, SET 2, (IY + {0:X})", disp));
                                    H = Set_R(2, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xD5: //LD L, SET 2, (IY+d)
                                    // Log(string.Format("LD L, SET 2, (IY + {0:X})", disp));
                                    L = Set_R(2, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xD6:  //SET 2, (IY + d)
                                    // Log(string.Format("SET 2, (IY + {0:X})", disp));
                                    PokeByte(offset, Set_R(2, disp));
                                    break;

                                case 0xD7: //LD A, SET 2, (IY+d)
                                    // Log(string.Format("LD A, SET 2, (IY + {0:X})", disp));
                                    A = Set_R(2, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xD8: //LD B, SET 3, (IY+d)
                                    // Log(string.Format("LD B, SET 3, (IY + {0:X})", disp));
                                    B = Set_R(3, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xD9: //LD C, SET 3, (IY+d)
                                    // Log(string.Format("LD C, SET 3, (IY + {0:X})", disp));
                                    C = Set_R(3, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xDA: //LD D, SET 3, (IY+d)
                                    // Log(string.Format("LD D, SET 3, (IY + {0:X})", disp));
                                    D = Set_R(3, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xDB: //LD E, SET 3, (IY+d)
                                    // Log(string.Format("LD E, SET 3, (IY + {0:X})", disp));
                                    E = Set_R(3, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xDC: //LD H, SET 21, (IY+d)
                                    // Log(string.Format("LD H, SET 3, (IY + {0:X})", disp));
                                    H = Set_R(3, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xDD: //LD L, SET 3, (IY+d)
                                    // Log(string.Format("LD L, SET 3, (IY + {0:X})", disp));
                                    L = Set_R(3, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xDE:  //SET 3, (IY + d)
                                    // Log(string.Format("SET 3, (IY + {0:X})", disp));
                                    PokeByte(offset, Set_R(3, disp));
                                    break;

                                case 0xDF: //LD A, SET 3, (IY+d)
                                    // Log(string.Format("LD A, SET 3, (IY + {0:X})", disp));
                                    A = Set_R(3, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xE0: //LD B, SET 4, (IY+d)
                                    // Log(string.Format("LD B, SET 4, (IY + {0:X})", disp));
                                    B = Set_R(4, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xE1: //LD C, SET 4, (IY+d)
                                    // Log(string.Format("LD C, SET 4, (IY + {0:X})", disp));
                                    C = Set_R(4, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xE2: //LD D, SET 4, (IY+d)
                                    // Log(string.Format("LD D, SET 4, (IY + {0:X})", disp));
                                    D = Set_R(4, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xE3: //LD E, SET 4, (IY+d)
                                    // Log(string.Format("LD E, SET 4, (IY + {0:X})", disp));
                                    E = Set_R(4, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xE4: //LD H, SET 4, (IY+d)
                                    // Log(string.Format("LD H, SET 4, (IY + {0:X})", disp));
                                    H = Set_R(4, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xE5: //LD L, SET 3, (IY+d)
                                    // Log(string.Format("LD L, SET 4, (IY + {0:X})", disp));
                                    L = Set_R(4, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xE6:  //SET 4, (IY + d)
                                    // Log(string.Format("SET 4, (IY + {0:X})", disp));
                                    PokeByte(offset, Set_R(4, disp));
                                    break;

                                case 0xE7: //LD A, SET 4, (IY+d)
                                    // Log(string.Format("LD A, SET 4, (IY + {0:X})", disp));
                                    A = Set_R(4, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xE8: //LD B, SET 5, (IY+d)
                                    // Log(string.Format("LD B, SET 5, (IY + {0:X})", disp));
                                    B = Set_R(5, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xE9: //LD C, SET 5, (IY+d)
                                    // Log(string.Format("LD C, SET 5, (IY + {0:X})", disp));
                                    C = Set_R(5, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xEA: //LD D, SET 5, (IY+d)
                                    // Log(string.Format("LD D, SET 5, (IY + {0:X})", disp));
                                    D = Set_R(5, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xEB: //LD E, SET 5, (IY+d)
                                    // Log(string.Format("LD E, SET 5, (IY + {0:X})", disp));
                                    E = Set_R(5, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xEC: //LD H, SET 5, (IY+d)
                                    // Log(string.Format("LD H, SET 5, (IY + {0:X})", disp));
                                    H = Set_R(5, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xED: //LD L, SET 5, (IY+d)
                                    // Log(string.Format("LD L, SET 5, (IY + {0:X})", disp));
                                    L = Set_R(5, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xEE:  //SET 5, (IY + d)
                                    // Log(string.Format("SET 5, (IY + {0:X})", disp));
                                    PokeByte(offset, Set_R(5, disp));
                                    break;

                                case 0xEF: //LD A, SET 5, (IY+d)
                                    // Log(string.Format("LD A, SET 5, (IY + {0:X})", disp));
                                    A = Set_R(5, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xF0: //LD B, SET 6, (IY+d)
                                    // Log(string.Format("LD B, SET 6, (IY + {0:X})", disp));
                                    B = Set_R(6, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xF1: //LD C, SET 6, (IY+d)
                                    // Log(string.Format("LD C, SET 6, (IY + {0:X})", disp));
                                    C = Set_R(6, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xF2: //LD D, SET 6, (IY+d)
                                    // Log(string.Format("LD D, SET 6, (IY + {0:X})", disp));
                                    D = Set_R(6, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xF3: //LD E, SET 6, (IY+d)
                                    // Log(string.Format("LD E, SET 6, (IY + {0:X})", disp));
                                    E = Set_R(6, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xF4: //LD H, SET 6, (IY+d)
                                    // Log(string.Format("LD H, SET 6, (IY + {0:X})", disp));
                                    H = Set_R(6, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xF5: //LD L, SET 6, (IY+d)
                                    // Log(string.Format("LD L, SET 6, (IY + {0:X})", disp));
                                    L = Set_R(6, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xF6:  //SET 6, (IY + d)
                                    // Log(string.Format("SET 6, (IY + {0:X})", disp));
                                    PokeByte(offset, Set_R(6, disp));
                                    break;

                                case 0xF7: //LD A, SET 6, (IY+d)
                                    // Log(string.Format("LD A, SET 6, (IY + {0:X})", disp));
                                    A = Set_R(6, disp);
                                    PokeByte(offset, A);
                                    break;

                                case 0xF8: //LD B, SET 7, (IY+d)
                                    // Log(string.Format("LD B, SET 7, (IY + {0:X})", disp));
                                    B = Set_R(7, disp);
                                    PokeByte(offset, B);
                                    break;

                                case 0xF9: //LD C, SET 7, (IY+d)
                                    // Log(string.Format("LD C, SET 7, (IY + {0:X})", disp));
                                    C = Set_R(7, disp);
                                    PokeByte(offset, C);
                                    break;

                                case 0xFA: //LD D, SET 7, (IY+d)
                                    // Log(string.Format("LD D, SET 7, (IY + {0:X})", disp));
                                    D = Set_R(7, disp);
                                    PokeByte(offset, D);
                                    break;

                                case 0xFB: //LD E, SET 7, (IY+d)
                                    // Log(string.Format("LD E, SET 7, (IY + {0:X})", disp));
                                    E = Set_R(7, disp);
                                    PokeByte(offset, E);
                                    break;

                                case 0xFC: //LD H, SET 7, (IY+d)
                                    // Log(string.Format("LD H, SET 7, (IY + {0:X})", disp));
                                    H = Set_R(7, disp);
                                    PokeByte(offset, H);
                                    break;

                                case 0xFD: //LD L, SET 7, (IY+d)
                                    // Log(string.Format("LD L, SET 7, (IY + {0:X})", disp));
                                    L = Set_R(7, disp);
                                    PokeByte(offset, L);
                                    break;

                                case 0xFE:  //SET 7, (IY + d)
                                    // Log(string.Format("SET 7, (IY + {0:X})", disp));
                                    PokeByte(offset, Set_R(7, disp));
                                    break;

                                case 0xFF: //LD A, SET 7, (IY + D)
                                    A = Set_R(7, disp);
                                    PokeByte(offset, A);
                                    break;

                                default:
                                    String msg = "ERROR: Could not handle FDCB " + opcode.ToString();
                                    System.Windows.Forms.MessageBox.Show(msg, "Opcode handler",
                                                System.Windows.Forms.MessageBoxButtons.OKCancel, System.Windows.Forms.MessageBoxIcon.Error);
                                    break;
                            }
                            break;
                        #endregion

                        #region Pop/Push instructions
                        case 0xE1:  //POP IY
                            // Log("POP IY");
                            IY = PopStack();
                            break;

                        case 0xE5:  //PUSH IY
                            // Log("PUSH IY");
                            Contend(IR, 1, 1);
                            PushStack(IY);
                            break;
                        #endregion

                        #region Exchange instruction
                        case 0xE3:  //EX (SP), IY
                            {
                                // Log("EX (SP), IY");
                                addr = PeekWord(SP);
                                Contend(SP + 1, 1, 1);
                                PokeByte((SP + 1) & 0xffff, IY >> 8);
                                PokeByte(SP, IY & 0xff);
                                Contend(SP, 1, 2);
                                IY = addr;
                                MemPtr = IY;
                                break;
                            }
                        #endregion

                        #region Jump instruction
                        case 0xE9:  //JP (IY)
                            // Log("JP (IY)");
                            PC = IY;
                            break;
                        #endregion

                        default:
                            //According to Sean's doc: http://z80.info/z80sean.txt
                            //If a DDxx or FDxx instruction is not listed, it should operate as
                            //without the DD or FD prefix, and the DD or FD prefix itself should
                            //operate as a NOP.
                            Execute();      //Try and execute it as a normal instruction then
                            break;
                    }
                    break;
                #endregion
            }
        }

        //Processes an interrupt
        public void Interrupt() {
            //Disable interrupts
            IFF1 = false;
            IFF2 = false;

            if (HaltOn) {
                HaltOn = false;
                PC++;
            }

            int oldT = totalTStates;
            if (interruptMode < 2) //IM0 = IM1 for our purpose
            {
                //When interrupts are enabled we can be sure that the reset sequence is over.
                //However, it actually takes a few more frames before the speccy reaches the copyright message,
                //so we have to wait a bit.
                if (!isResetOver)
                {
                    resetFrameCounter++;
                    if (resetFrameCounter > resetFrameTarget)
                    {
                        isResetOver = true;
                        resetFrameCounter = 0;
                    }
                }
                //Perform a RST 0x038
                PushStack(PC);
                totalTStates += 7;
                PC = 0x38;
                MemPtr = PC;
            } else    //IM 2
            {
                int ptr = (I << 8) | 0xff;
                PushStack(PC);
                PC = PeekWord(ptr);
                totalTStates += 7;
                MemPtr = PC;
            }
            int deltaT = totalTStates - oldT;
            timeToOutSound += deltaTStates;
            UpdateAudio(deltaT);
            UpdateTapeState(deltaT);
        }

        public void TapeStopped(bool cancelCallback = false) {
            tapeIsPlaying = false;

            if (pulse != 0)
                FlipTapeBit();
            if (TapeEvent != null && !cancelCallback)
                OnTapeEvent(new TapeEventArgs(TapeEventType.STOP_TAPE)); //stop the tape!
        }

        private void FlipTapeBit() {
            pulse = 1 - pulse;
            tapeBitWasFlipped = true;
            tapeBitFlipAck = false;
            tapeBit = pulse;
            if (pulse == 0) {
                soundOut = 0;
            } else
                soundOut = short.MinValue >> 1; //half
        }

        public void NextPZXBlock() {
            while (true) {
                blockCounter++;
                if (blockCounter >= PZXLoader.blocks.Count) {
                    blockCounter--;
                    tape_readToPlay = false;
                    TapeStopped();
                    return;
                }

                currentBlock = PZXLoader.blocks[blockCounter];
               
                if (currentBlock is PZXLoader.PULS_Block) {
                    //Initialise for PULS loading
                    pulseCounter = -1;
                    repeatCount = -1;

                    //Pulse is low by default for PULS blocks
                    if (pulse != 0)
                        FlipTapeBit();
                   
                    //Process pulse if there is one
                    if (!NextPULS()) {
                        continue; //No? Next block please!
                    } else
                        break;
                } else if (currentBlock is PZXLoader.DATA_Block) {
                    pulseCounter = 0;
                    bitCounter = -1;
                    dataCounter = -1;
                    if (pulse != (((PZXLoader.DATA_Block)currentBlock).initialPulseLevel))
                        FlipTapeBit();

                    if (!NextDataBit()) {
                        continue;
                    } else
                        break;
                } else if (currentBlock is PZXLoader.PAUS_Block) {
                    //Would have been nice to skip PAUS blocks when doing fast loading
                    //but some loaders like Auf Wiedersehen Monty (Kixx) rely on the pause
                    //length to do processing during loading. In this case, fill in the
                    //loading screen.

                    //Ensure previous edge is finished correctly by flipping the edge one last time
                    //edgeDuration = (35000 * 2);
                    //isPauseBlockPreproccess = true;
                     PZXLoader.PAUS_Block block = (PZXLoader.PAUS_Block)currentBlock;
                    if (block.initialPulseLevel != pulse)
                        FlipTapeBit();
                   
                    edgeDuration = (block.duration);
                    int diff = (int)edgeDuration - tapeTStates;
                    if (diff > 0) {
                        edgeDuration = (uint)diff;
                        tapeTStates = 0;
                        isPauseBlockPreproccess = true;
                        break;
                    } else {
                        tapeTStates = -diff;
                    }
                    continue;
                } else if ((currentBlock is PZXLoader.STOP_Block)) {
                    TapeStopped();
                   // if (ziggyWin.zx.keyBuffer[(int)ZeroWin.Form1.keyCode.ALT])
                   //     ziggyWin.saveSnapshotMenuItem_Click(this, null);
                    break;
                }
            }
            if (TapeEvent != null)
                OnTapeEvent(new TapeEventArgs(TapeEventType.NEXT_BLOCK));
            //dataGridView1.Rows[blockCounter - 1].Selected = true;
           // dataGridView1.CurrentCell = dataGridView1.Rows[blockCounter - 1].Cells[0];
        }

        public bool NextPULS() {
            PZXLoader.PULS_Block block = (PZXLoader.PULS_Block)currentBlock;

            while (pulseCounter < block.pulse.Count - 1) {
                pulseCounter++; //b'cos pulseCounter is -1 when it reaches here initially
                repeatCount = block.pulse[pulseCounter].count;
                if ((block.pulse[pulseCounter].duration == 0)) {
                    if ((repeatCount & 0x01) != 0) 
                        FlipTapeBit(); //Flip ear bit if odd
                    continue; //next pulse
                }
                if (block.pulse[pulseCounter].duration > 0) {
                    int diff = block.pulse[pulseCounter].duration - tapeTStates;
                    if (diff > 0) {
                        edgeDuration = (uint)diff;
                        tapeTStates = 0;
                        return true;
                    } else
                        tapeTStates = -diff;
                }
                FlipTapeBit();
                repeatCount--;
                if (repeatCount <= 0)
                    continue;
                edgeDuration = (block.pulse[pulseCounter].duration);
                return true;
            }

            //All pulses done!
            return false;
        }

        public bool NextDataBit() {
            PZXLoader.DATA_Block block = (PZXLoader.DATA_Block)currentBlock;

            //Bits left for processing?
            while (bitCounter < block.count - 1) {
                bitCounter++;
                if (bitCounter % 8 == 0) {
                    //All 8 bits done so get next byte
                    dataCounter++;
                    if (dataCounter < block.data.Count) {
                        dataByte = block.data[dataCounter];
                    }
                } else
                    dataByte = (byte)((dataByte << 1) & 0xff);

                currentBit = ((dataByte & 0x80) == 0 ? 0 : 1);
                pulseCounter = 0;

                if (currentBit == 0) {
                    if (block.s0[pulseCounter] == 0) {
                        continue;
                    } else {
                        edgeDuration = (block.s0[pulseCounter]);
                        //return true;
                    }
                } else {
                    if (block.s1[pulseCounter] == 0) {
                        continue;
                    } else {
                        edgeDuration = (block.s1[pulseCounter]);
                       // return true;
                    }
                }

                int diff = (int)edgeDuration - tapeTStates;
                if (diff > 0) {
                    edgeDuration = (uint)diff;
                    tapeTStates = 0;
                    return true;
                } else
                    tapeTStates = -diff;

                FlipTapeBit();
            }

            //All bits done. Now do the tail pulse to finish off things
            if (block.tail > 0) {
                currentBit = -1;
                edgeDuration = (block.tail);
                int diff = (int)edgeDuration - tapeTStates;
                if (diff > 0) {
                    edgeDuration = (uint)diff;
                    tapeTStates = 0;
                    return true;
                } else
                    tapeTStates = -diff;

                FlipTapeBit();
            }/* else {
                //HACK: Sometimes a tape might have it's last tail pulse missing.
                //In case it's the last block in the tape, it's best to flip the tape bit
                //a last time to ensure that the process is terminated properly.
                if (blockCounter == PZXLoader.blocks.Count - 1) {
                    currentBit = -1;
                    edgeDuration = (3500 * 2);
                    return true;
                }
            }*/

            return false;
        }

        private void FlashLoad()
        {
            if (blockCounter < 0)
                blockCounter = 0;

            PZXLoader.Block currBlock = PZXLoader.blocks[blockCounter];

            if (!(currBlock is PZXLoader.PULS_Block))
                blockCounter++;

            if (blockCounter >= PZXLoader.tapeBlockInfo.Count)
            {
                blockCounter--;
                tape_readToPlay = false;
                TapeStopped();
                return;
            }

            if (!PZXLoader.tapeBlockInfo[blockCounter].IsStandardBlock)
            {
                if (!(currBlock is PZXLoader.PULS_Block))
                    blockCounter--;
                return;
            }

            PZXLoader.DATA_Block dataBlock = (PZXLoader.DATA_Block)PZXLoader.blocks[blockCounter + 1];
            edgeDuration = (1000);
            //if (pulse != dataBlock.initialPulseLevel)
            //    FlipTapeBit();
            H = 0;
            int byteCounter = dataBlock.data.Count;
            int dataIndex = 0;
            bool loadStageFlagByte = true;
            while (true)
            {
                if (byteCounter == 0)
                {
                    A = C & 32;
                    B = 0;
                    F = 0x50; //01010000b
                    break;
                }
                byteCounter--;
                L = dataBlock.data[dataIndex++];
                H ^= L;
                if (DE == 0)
                {
                    A = H;
                    Cp_R(1);
                    break;
                }
                if (loadStageFlagByte)
                {
                    loadStageFlagByte = false;
                    A = (_AF >> 8) & 0xff;
                    Xor_R(L);
                    if ((F & 0x040) == 0)
                        break;
                }
                else
                {
                    PokeByteNoContend(IX++, L);
                    DE--;
                }
            }
            PC = PopStack();
            MemPtr = PC;
            blockCounter++;
            if (blockCounter >= PZXLoader.blocks.Count)
            {
                blockCounter--;
                tape_readToPlay = false;
                TapeStopped();
                return;
            }
        }

        private void DoTapeEvent(Speccy.TapeEventArgs e) {
            if (tapeBitFlipAck)
                tapeBitWasFlipped = false;

            if (e.EventType == Speccy.TapeEventType.EDGE_LOAD) {
                FlipTapeBit();

                #region PULS

                if (currentBlock is PZXLoader.PULS_Block) {
                    PZXLoader.PULS_Block block = (PZXLoader.PULS_Block)currentBlock;
                    repeatCount--;
                    //progressBar1.Value += progressStep;
                    //Need to repeat?
                   // if (repeatCount < block.pulse[pulseCounter].count) {
                     if (repeatCount > 0) {
                       edgeDuration = (block.pulse[pulseCounter].duration);
                    } else {
                        if (!NextPULS()) //All pulses done for the block?
                        {
                            NextPZXBlock();
                            return;
                        }
                    }
                    return;
                }
                #endregion PULS

                #region DATA
 else if (currentBlock is PZXLoader.DATA_Block) {
                    PZXLoader.DATA_Block block = (PZXLoader.DATA_Block)currentBlock;

                    //Are we done with pulses for a certain sequence?
                    if (currentBit == 0) {
                        pulseCounter++;
                        if (pulseCounter < block.s0.Count) {
                            edgeDuration = (block.s0[pulseCounter]);
                        } else {
                            //All pulses done for this bit so fetch next bit
                            if (!NextDataBit()) {
                                NextPZXBlock();
                                return;
                            }
                        }
                    } else if (currentBit == 1) {
                        pulseCounter++;
                        if (pulseCounter < block.s1.Count) {
                            edgeDuration = (block.s1[pulseCounter]);
                        } else {
                            //All pulses done for this bit so fetch next bit
                            if (!NextDataBit()) {
                                NextPZXBlock();
                                return;
                            }
                        }
                    } else //we were doing the tail!
                    {
                        NextPZXBlock();
                        return;
                    }
                    return;
                }

                #endregion DATA

                #region PAUS
 else if (currentBlock is PZXLoader.PAUS_Block) {
                    isPauseBlockPreproccess = false;
                    NextPZXBlock();
                    return;
                }
/*
 else if (currentBlock is PZXLoader.PAUS_Block) {
                    if (isPauseBlockPreproccess) {
                        PZXLoader.PAUS_Block block = (PZXLoader.PAUS_Block)currentBlock;
                        isPauseBlockPreproccess = false;
                        edgeDuration = (block.duration);
                        if (pulse != block.initialPulseLevel)
                            FlipTapeBit();
                    } else {
                        NextPZXBlock();
                    }
                    return;
                }
                */
                #endregion PAUS
            } else if (e.EventType == Speccy.TapeEventType.STOP_TAPE) //stop
            {
                TapeStopped();
                blockCounter--;

            } else if (e.EventType == Speccy.TapeEventType.START_TAPE) {
                if (TapeEvent != null)
                    OnTapeEvent(new TapeEventArgs(TapeEventType.START_TAPE));

                NextPZXBlock();
               
            } else if (e.EventType == Speccy.TapeEventType.FLASH_LOAD) {
                FlashLoad();
            }
        }
    }

}