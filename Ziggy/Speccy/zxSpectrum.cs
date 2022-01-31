
#define NEW_RZX_METHODS

using System;
using Peripherals;
using Cpu;
using SpeccyCommon;
using System.Collections.Generic;

namespace Speccy
{
    /// <summary>
    /// zx_spectrum is the heart of speccy emulation.
    /// It includes core execution, ula, sound, input and interrupt handling
    /// </summary>
    public abstract class zx_spectrum
    {
        #region Event handlers for the Monitor, primarily
        public event MemoryWriteEventHandler MemoryWriteEvent;
        public event MemoryReadEventHandler MemoryReadEvent;
        public event MemoryExecuteEventHandler MemoryExecuteEvent;
        public event OpcodeExecutedEventHandler OpcodeExecutedEvent;
        public event TapeEventHandler TapeEvent;
        public event PortIOEventHandler PortEvent; //Used to raise generic port event (ex debugging)
        public event PortReadEventHandler PortReadEvent;
        public event PortWriteEventHandler PortWriteEvent;
        public event DiskEventHandler DiskEvent;
        public event StateChangeEventHandler StateChangeEvent;
        public event PopStackEventHandler PopStackEvent;
        public event PushStackEventHandler PushStackEvent;
        public event FrameEndEventHandler FrameEndEvent;
        public event FrameStartEventHandler FrameStartEvent;
        public event RZXPlaybackStartEventHandler RZXPlaybackStartEvent;
        public event RZXFrameEndEventHandler RZXFrameEndEvent;

        protected void OnFrameEndEvent()
        {
               FrameEndEvent?.Invoke(this);
        }

        protected void OnFrameStartEvent()
        {
            FrameStartEvent?.Invoke(this);
        }

        protected void OnRZXPlaybackStartEvent() {
            RZXPlaybackStartEvent?.Invoke(this);
        }

        protected void OnMemoryWriteEvent(MemoryEventArgs e) {
            MemoryWriteEvent?.Invoke(this, e);
        }

        protected void OnMemoryReadEvent(MemoryEventArgs e) {
            MemoryReadEvent?.Invoke(this, e);
        }

        protected void OnMemoryExecuteEvent(MemoryEventArgs e) {
            MemoryExecuteEvent?.Invoke(this, e);
        }

        protected void OnTapeEvent(TapeEventArgs e) {
            TapeEvent?.Invoke(this, e);
        }

        protected void OnDiskEvent(DiskEventArgs e) {
            DiskEvent?.Invoke(this, e);
        }

        protected void OnPortEvent(PortIOEventArgs e) {
            PortEvent?.Invoke(this, e);
        }

        protected byte OnPortReadEvent(ushort port) {
            if (PortReadEvent != null)
                return PortReadEvent(this, port);

            return 0xff;
        }

        protected void OnPortWriteEvent(ushort port, byte val) {
            PortWriteEvent?.Invoke(this, port, val);
        }

        #endregion

        private IntPtr mainHandle;


        public Z80 cpu;
        public ULA_Plus ula_plus = new ULA_Plus();
        //public Z80_Registers regs;
        public ZeroSound.SoundManager beeper;
        public List<IODevice> io_devices = new List<IODevice>();
        public List<AudioDevice> audio_devices = new List<AudioDevice>();
        public Dictionary<int, SpeccyDevice> attached_devices = new Dictionary<int, SpeccyDevice>();
        protected Random rnd_generator = new Random();

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

        //Misc variables
        protected int val, addr;
        public bool isROMprotected = true;  //not really used ATM
        public bool needsPaint = false;     //Raised when the ULA has finished painting the entire screen
        protected bool CapsLockOn = false;
        protected int prevT;        //previous cpu t-states
        protected int inputFrameTime = 0;

        //Sound
        public const short MIN_SOUND_VOL = 0;
        public const short MAX_SOUND_VOL = short.MaxValue / 2;
        private short[] soundSamples = new short[882 * 2]; //882 samples, 2 channels, 2 bytes per channel (short)
        
        public const bool ENABLE_SOUND = false;
        protected int averagedSound = 0;
        protected short soundCounter = 0;
        protected int lastSoundOut = 0;
        public short soundOut = 0;
        protected int soundTStatesToSample = 79;
        private float soundVolume = 0f;        //cached reference used when beeper instance is recreated.
        private short soundSampleCounter = 0;
        protected int timeToOutSound = 0;

        //Threading stuff (not used)
        public bool doRun = true;           //z80 executes only when true. Mainly for debugging purpose.

        //Important ports
        protected int lastFEOut = 0;        //The ULA Port
        protected int last7ffdOut = 0;      //Paging port on 128k/+2/+3/Pentagon
        protected int last1ffdOut = 0;      //Paging + drive motor port on +3

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
        protected int flashFrameCount;

        //Contention related stuff
        protected int contentionStartPeriod;              //t-state at which to start applying contention
        protected int contentionEndPeriod;                //t-state at which to end applying contention
        protected byte[] contentionTable;                  //tstate-memory contention delay mapping

        //Render related stuff
        public int[] ScreenBuffer;                        //buffer for the windows side rasterizer
        protected int[] LastScanlineColor;
        protected short lastScanlineColorCounter;
        protected byte[] screen;                           //display memory (16384 for 48k)
        protected short[] attr;                           //attribute memory lookup (mapped 1:1 to screen for convenience)
        protected short[] tstateToDisp;                   //tstate-display mapping
        protected short[] floatingBusTable;               //table that stores tstate to screen/attr addresses values
        protected int deltaTStates;
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
        public bool tape_edgeLoad = false;
        public bool tapeBitWasFlipped = false;
        public bool tapeBitFlipAck = false;
        public bool tape_AutoPlay = false;
        public bool tape_AutoStarted = false;
        public bool tape_readToPlay = false;
        private const int TAPE_TIMEOUT = 100;// 69888 * 10;
        private int tape_stopTimeOut = TAPE_TIMEOUT;
        private byte tape_FrameCount = 0;
        public int tapeTStates = 0;
        public uint edgeDuration = 0;
        public bool tapeIsPlaying = false;
        public int pulseLevel = 0;

        //Tape loading
        public int blockCounter = 0;
        public bool tapePresent = false;
        public bool tape_flashLoad = true;
        public bool tapeTrapsDisabled = false;
        private int pulseCounter = 0;
        private int repeatCount = 0;
        private int bitCounter = 0;
        private byte bitShifter = 0;
        private int dataCounter = 0;
        private byte dataByte = 0;
        private int currentBit = 0;
        private bool isPauseBlockPreproccess = false; //To ensure previous edge is finished correctly
        private bool isProcessingPauseBlock = false;  //Signals if the current pause block is currently being serviced.
        private uint pauseCounter = 0;
        public PZXFile.Block currentBlock;

        //AY support
        public bool HasAYSound
        {
            get; set;
        }

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

        public int joystickType = 0; //A bit field of above joysticks to emulate (usually not more than 2).
        public bool UseKempstonPort1F = false; //If 1f, decoding scheme uses top 3 bits (5,6,7) else only the 5th bit is tested (port d4).
        
        ////Each joystickState corresponds to an emulated joystick
        //Bits: 0 = button 3, 1 = button 2, 3 = button 1, 4 = up, 5 = down, 6 = left, 7 = right
        public int[] joystickState = new int[(int)JoysticksEmulated.LAST];

        //RZX Playback & Recording
        protected class RollbackBookmark
        {
            public SZXFile snapshot;
            public int frameIndex;
        };

        //This holds the key lines used by the speccy for input
        public bool[] keyBuffer;

        //SpecEmu interfacing
        public bool externalSingleStep = false;

        protected byte rzxIN;
        public RZXFile rzx;

        protected System.Collections.Generic.List<byte> rzxInputs = new System.Collections.Generic.List<byte>();
        public bool isPlayingRZX = false;
        public bool isRecordingRZX = false;

        //Disk related stuff
        protected int diskDriveState = 0;

        //Thread related stuff (not used ATM)
        private System.Threading.Thread emulationThread;
        public bool isSuspended = true;
        public System.Object lockThis = new System.Object(); //used to synchronise emulation with methods that change emulation state
        public System.Object lockThis2 = new System.Object(); //used by monitor/emulation

        public MachineModel model;
        public int emulationSpeed;
        public int cpuMultiplier = 1;
        public bool isResetOver = false;
        private const int MAX_CPU_SPEED = 500;

        //How long should we wait after speccy reset before signalling that it's safe to assume so.
        private int resetFrameTarget = 0;
        private int resetFrameCounter = 0;

        // Some handy lambda functions...
        Func<byte, int> GetDisplacement = val => (128 ^ val) - 128;
        //Returns the actual memory address of a page
        public Func<int, int> GetPageAddress = page => 8192 * page;
        //Returns the memory data at a page
        public Func<int, byte[]> GetPageData;
        //Changes the spectrum palette to the one provided
        public Action<int[]> SetPalette;
        // Returns the byte at a given 16 bit address with no contention
        public Func<ushort, byte> PeekByteNoContend;
        // Returns a word at a given 16 bit address with no contention
        public Func<ushort, ushort> PeekWordNoContend;
        // Returns a 16 bit value from given address with contention.
        public Func<ushort, ushort> PeekWord;

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

        public string Disassemble(ushort address) {
            string dis = "";
            ushort PC = address;
            int opcode = PeekByteNoContend(PC);
            if (PC >= 65535) {
                return dis;
            }
            PC++;
            jmp4Undoc:  //We will jump here for undocumented instructions.
            bool jumpForUndoc = false;
            int disp = 0;
            switch (opcode) {

                #region NOP

                case 0x00: //NOP
                dis = String.Format("NOP");
                break;

                #endregion NOP

                #region 16 bit load operations (LD rr, nn)
                /** LD rr, nn (excluding DD prefix) **/
                case 0x01: //LD BC, nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("LD BC, ${0:x}", disp);
                PC += 2;
                break;

                case 0x11:  //LD DE, nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("LD DE, ${0:x}", disp);
                PC += 2;
                break;

                case 0x21:  //LD HL, nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("LD HL, ${0:x}", disp);
                PC += 2;
                break;

                case 0x2A:  //LD HL, (nn)
                disp = PeekWordNoContend(PC);
                dis = String.Format("LD HL, (${0:x})", disp);
                PC += 2;
                break;

                case 0x31:  //LD SP, nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("LD SP, ${0:x}", disp);
                PC += 2;
                break;

                case 0xF9:  //LD SP, HL
                dis = String.Format("LD SP, HL");
                break;
                #endregion

                #region 16 bit increments (INC rr)
                /** INC rr **/
                case 0x03:  //INC BC
                dis = String.Format("INC BC");
                break;

                case 0x13:  //INC DE
                dis = String.Format("INC DE");
                break;

                case 0x23:  //INC HL
                dis = String.Format("INC HL");
                break;

                case 0x33:  //INC SP
                dis = String.Format("INC SP");
                break;
                #endregion INC rr

                #region 8 bit increments (INC r)
                /** INC r + INC (HL) **/
                case 0x04:  //INC B
                dis = String.Format("INC B");
                break;

                case 0x0C:  //INC C
                dis = String.Format("INC C");
                break;

                case 0x14:  //INC D
                dis = String.Format("INC D");
                break;

                case 0x1C:  //INC E
                dis = String.Format("INC E");
                break;

                case 0x24:  //INC H
                dis = String.Format("INC H");
                break;

                case 0x2C:  //INC L
                dis = String.Format("INC L");
                break;

                case 0x34:  //INC (HL)
                dis = String.Format("INC (HL)");
                break;

                case 0x3C:  //INC A
                dis = String.Format("INC A");
                break;
                #endregion

                #region 8 bit decrement (DEC r)
                /** DEC r + DEC (HL)**/
                case 0x05: //DEC B
                dis = String.Format("DEC B");
                break;

                case 0x0D:    //DEC C
                dis = String.Format("DEC C");
                break;

                case 0x15:  //DEC D
                dis = String.Format("DEC D");
                break;

                case 0x1D:  //DEC E
                dis = String.Format("DEC E");
                break;

                case 0x25:  //DEC H
                dis = String.Format("DEC H");
                break;

                case 0x2D:  //DEC L
                dis = String.Format("DEC L");
                break;

                case 0x35:  //DEC (HL)
                dis = String.Format("DEC (HL)");
                break;

                case 0x3D:  //DEC A
                dis = String.Format("DEC A");
                break;
                #endregion

                #region 16 bit decrements
                /** DEC rr **/
                case 0x0B:  //DEC BC
                dis = String.Format("DEC BC");
                break;

                case 0x1B:  //DEC DE
                dis = String.Format("DEC DE");
                break;

                case 0x2B:  //DEC HL
                dis = String.Format("DEC HL");
                break;

                case 0x3B:  //DEC SP
                dis = String.Format("DEC SP");
                break;
                #endregion

                #region Immediate load operations (LD (nn), r)
                /** LD (rr), r + LD (nn), HL  + LD (nn), A **/
                case 0x02: //LD (BC), A
                dis = String.Format("LD (BC), A");
                break;

                case 0x12:  //LD (DE), A
                dis = String.Format("LD (DE), A");
                break;

                case 0x22:  //LD (nn), HL
                disp = PeekWordNoContend(PC);
                dis = String.Format("LD (${0:x}), HL", disp);
                PC += 2;
                break;

                case 0x32:  //LD (nn), A
                disp = PeekWordNoContend(PC);
                dis = String.Format("LD (${0:x}), A", disp);
                PC += 2;
                break;

                case 0x36:  //LD (HL), n
                disp = PeekByteNoContend(PC);
                dis = String.Format("LD (HL), ${0:x}", disp);
                PC += 1;
                break;
                #endregion

                #region Indirect load operations (LD r, r)
                /** LD r, r **/
                case 0x06: //LD B, n
                disp = PeekByteNoContend(PC);
                dis = String.Format("LD B, ${0:x}", disp);
                PC += 1;
                break;

                case 0x0A:  //LD A, (BC)
                dis = String.Format("LD A, (BC)");
                break;

                case 0x0E:  //LD C, n
                disp = PeekByteNoContend(PC);
                dis = String.Format("LD C, ${0:x}", disp);
                PC += 1;
                break;

                case 0x16:  //LD D,n
                disp = PeekByteNoContend(PC);
                dis = String.Format("LD D, ${0:x}", disp);
                PC += 1;
                break;

                case 0x1A:  //LD A,(DE)
                dis = String.Format("LD A, (DE)");
                break;

                case 0x1E:  //LD E,n
                disp = PeekByteNoContend(PC);
                dis = String.Format("LD E, ${0:x}", disp);
                PC += 1;
                break;

                case 0x26:  //LD H,n
                disp = PeekByteNoContend(PC);
                dis = String.Format("LD H, ${0:x}", disp);
                PC += 1;
                break;

                case 0x2E:  //LD L,n
                disp = PeekByteNoContend(PC);
                dis = String.Format("LD L, ${0:x}", disp);
                PC += 1;
                break;

                case 0x3A:  //LD A,(nn)
                disp = PeekWordNoContend(PC);
                dis = String.Format("LD A, (${0:x})", disp);
                PC += 2;
                break;

                case 0x3E:  //LD A,n
                disp = PeekByteNoContend(PC);
                dis = String.Format("LD A, ${0:x}", disp);
                PC += 1;
                break;

                case 0x40:  //LD B,B
                dis = String.Format("LD B, B");
                break;

                case 0x41:  //LD B,C
                dis = String.Format("LD B, C");
                break;

                case 0x42:  //LD B,D
                dis = String.Format("LD B, D");
                break;

                case 0x43:  //LD B,E
                dis = String.Format("LD B, E");
                break;

                case 0x44:  //LD B,H
                dis = String.Format("LD B, H");
                break;

                case 0x45:  //LD B,L
                dis = String.Format("LD B, L");
                break;

                case 0x46:  //LD B,(HL)
                dis = String.Format("LD B, (HL)");
                break;

                case 0x47:  //LD B,A
                dis = String.Format("LD B, A");
                break;

                case 0x48:  //LD C,B
                dis = String.Format("LD C, B");
                break;

                case 0x49:  //LD C,C
                dis = String.Format("LD C, C");
                break;

                case 0x4A:  //LD C,D
                dis = String.Format("LD C, D");
                break;

                case 0x4B:  //LD C,E
                dis = String.Format("LD C, E");
                break;

                case 0x4C:  //LD C,H
                dis = String.Format("LD C, H");
                break;

                case 0x4D:  //LD C,L
                dis = String.Format("LD C, L");
                break;

                case 0x4E:  //LD C, (HL)
                dis = String.Format("LD C, (HL)");
                break;

                case 0x4F:  //LD C,A
                dis = String.Format("LD C, A");
                break;

                case 0x50:  //LD D,B
                dis = String.Format("LD D, B");
                break;

                case 0x51:  //LD D,C
                dis = String.Format("LD D, C");
                break;

                case 0x52:  //LD D,D
                dis = String.Format("LD D, D");
                break;

                case 0x53:  //LD D,E
                dis = String.Format("LD D, E");
                break;

                case 0x54:  //LD D,H
                dis = String.Format("LD D, H");
                break;

                case 0x55:  //LD D,L
                dis = String.Format("LD D, L");
                break;

                case 0x56:  //LD D,(HL)
                dis = String.Format("LD D, (HL)");
                break;

                case 0x57:  //LD D,A
                dis = String.Format("LD D, A");
                break;

                case 0x58:  //LD E,B
                dis = String.Format("LD E, B");
                break;

                case 0x59:  //LD E,C
                dis = String.Format("LD E, C");
                break;

                case 0x5A:  //LD E,D
                dis = String.Format("LD E, D");
                break;

                case 0x5B:  //LD E,E
                dis = String.Format("LD E, E");
                break;

                case 0x5C:  //LD E,H
                dis = String.Format("LD E, H");
                break;

                case 0x5D:  //LD E,L
                dis = String.Format("LD E, L");
                break;

                case 0x5E:  //LD E,(HL)
                dis = String.Format("LD E, (HL)");
                break;

                case 0x5F:  //LD E,A
                dis = String.Format("LD E, A");
                break;

                case 0x60:  //LD H,B
                dis = String.Format("LD H, B");
                break;

                case 0x61:  //LD H,C
                dis = String.Format("LD H, C");
                break;

                case 0x62:  //LD H,D
                dis = String.Format("LD H, D");
                break;

                case 0x63:  //LD H,E
                dis = String.Format("LD H, E");
                break;

                case 0x64:  //LD H,H
                dis = String.Format("LD H, H");
                break;

                case 0x65:  //LD H,L
                dis = String.Format("LD H, L");
                break;

                case 0x66:  //LD H,(HL)
                dis = String.Format("LD H, (HL)");
                break;

                case 0x67:  //LD H,A
                dis = String.Format("LD H, A");
                break;

                case 0x68:  //LD L,B
                dis = String.Format("LD L, B");
                break;

                case 0x69:  //LD L,C
                dis = String.Format("LD L, C");
                break;

                case 0x6A:  //LD L,D
                dis = String.Format("LD L, D");
                break;

                case 0x6B:  //LD L,E
                dis = String.Format("LD L, E");
                break;

                case 0x6C:  //LD L,H
                dis = String.Format("LD L, H");
                break;

                case 0x6D:  //LD L,L
                dis = String.Format("LD L, L");
                break;

                case 0x6E:  //LD L,(HL)
                dis = String.Format("LD L, (HL)");
                break;

                case 0x6F:  //LD L,A
                dis = String.Format("LD L, A");
                break;

                case 0x70:  //LD (HL),B
                dis = String.Format("LD (HL), B");
                break;

                case 0x71:  //LD (HL),C
                dis = String.Format("LD (HL), C");
                break;

                case 0x72:  //LD (HL),D
                dis = String.Format("LD (HL), D");
                break;

                case 0x73:  //LD (HL),E
                dis = String.Format("LD (HL), E");
                break;

                case 0x74:  //LD (HL),H
                dis = String.Format("LD (HL), H");
                break;

                case 0x75:  //LD (HL),L
                dis = String.Format("LD (HL), L");
                break;

                case 0x77:  //LD (HL),A
                dis = String.Format("LD (HL), A");
                break;

                case 0x78:  //LD A,B
                dis = String.Format("LD A, B");
                break;

                case 0x79:  //LD A,C
                dis = String.Format("LD A, C");
                break;

                case 0x7A:  //LD A,D
                dis = String.Format("LD A, D");
                break;

                case 0x7B:  //LD A,E
                dis = String.Format("LD A, E");
                break;

                case 0x7C:  //LD A,H
                dis = String.Format("LD A, H");
                break;

                case 0x7D:  //LD A,L
                dis = String.Format("LD A, L");
                break;

                case 0x7E:  //LD A,(HL)
                dis = String.Format("LD A, (HL)");
                break;

                case 0x7F:  //LD A,A
                dis = String.Format("LD A, A");
                break;
                #endregion

                #region Rotates on Accumulator
                /** Accumulator Rotates **/
                case 0x07: //RLCA
                dis = String.Format("RLCA");
                break;

                case 0x0F:  //RRCA
                dis = String.Format("RRCA");
                break;

                case 0x17:  //RLA
                dis = String.Format("RLA");
                break;

                case 0x1F:  //RRA
                dis = String.Format("RRA");
                break;
                #endregion

                #region Exchange operations (EX)
                /** Exchange operations **/
                case 0x08:     //EX AF, AF'
                dis = String.Format("EX AF, AF'");
                break;

                case 0xD9:   //EXX
                dis = String.Format("EXX");
                break;

                case 0xE3:  //EX (SP), HL
                dis = String.Format("EX (SP), HL");
                break;

                case 0xEB:  //EX DE, HL
                dis = String.Format("EX DE, HL");
                break;
                #endregion

                #region 16 bit addition to HL (Add HL, rr)
                /** Add HL, rr **/
                case 0x09:     //ADD HL, BC
                dis = String.Format("ADD HL, BC");
                break;

                case 0x19:    //ADD HL, DE
                dis = String.Format("ADD HL, DE");
                break;

                case 0x29:  //ADD HL, HL
                dis = String.Format("ADD HL, HL");
                break;

                case 0x39:  //ADD HL, SP
                dis = String.Format("ADD HL, SP");
                break;
                #endregion

                #region 8 bit addition to accumulator (Add r, r)
                /*** ADD r, r ***/
                case 0x80:  //ADD A,B
                dis = String.Format("ADD A, B");
                break;

                case 0x81:  //ADD A,C
                dis = String.Format("ADD A, C");
                break;

                case 0x82:  //ADD A,D
                dis = String.Format("ADD A, D");
                break;

                case 0x83:  //ADD A,E
                dis = String.Format("ADD A, E");
                break;

                case 0x84:  //ADD A,H
                dis = String.Format("ADD A, H");
                break;

                case 0x85:  //ADD A,L
                dis = String.Format("ADD A, L");
                break;

                case 0x86:  //ADD A, (HL)
                dis = String.Format("ADD A, (HL)");
                break;

                case 0x87:  //ADD A, A
                dis = String.Format("ADD A, A");
                break;

                case 0xC6:  //ADD A, n
                disp = PeekByteNoContend(PC);
                dis = String.Format("ADD A, ${0:x}", disp);
                PC++;
                break;
                #endregion

                #region Add to accumulator with carry (Adc A, r)
                /** Adc a, r **/
                case 0x88:  //ADC A,B
                dis = String.Format("ADC A, B");
                break;

                case 0x89:  //ADC A,C
                dis = String.Format("ADC A, C");
                break;

                case 0x8A:  //ADC A,D
                dis = String.Format("ADC A, D");
                break;

                case 0x8B:  //ADC A,E
                dis = String.Format("ADC A, E");
                break;

                case 0x8C:  //ADC A,H
                dis = String.Format("ADC A, H");
                break;

                case 0x8D:  //ADC A,L
                dis = String.Format("ADC A, L");
                break;

                case 0x8E:  //ADC A,(HL)
                dis = String.Format("ADC A, (HL)");
                break;

                case 0x8F:  //ADC A,A
                dis = String.Format("ADC A, A");
                break;

                case 0xCE:  //ADC A, n
                disp = PeekByteNoContend(PC);
                dis = String.Format("ADC A, ${0:x}", disp);
                PC += 1;
                break;
                #endregion

                #region 8 bit subtraction from accumulator(SUB r)
                case 0x90:  //SUB B
                dis = String.Format("SUB B");
                break;

                case 0x91:  //SUB C
                dis = String.Format("SUB C");
                break;

                case 0x92:  //SUB D
                dis = String.Format("SUB D");
                break;

                case 0x93:  //SUB E
                dis = String.Format("SUB E");
                break;

                case 0x94:  //SUB H
                dis = String.Format("SUB H");
                break;

                case 0x95:  //SUB L
                dis = String.Format("SUB L");
                break;

                case 0x96:  //SUB (HL)
                dis = String.Format("SUB (HL)");
                break;

                case 0x97:  //SUB A
                dis = String.Format("SUB A");
                break;

                case 0xD6:  //SUB n
                disp = PeekByteNoContend(PC);
                dis = String.Format("SUB ${0:x}", disp);
                PC += 1;
                break;
                #endregion

                #region 8 bit subtraction from accumulator with carry(SBC A, r)
                case 0x98:  //SBC A, B
                dis = String.Format("SBC A, B");
                break;

                case 0x99:  //SBC A, C
                dis = String.Format("SBC A, C");
                break;

                case 0x9A:  //SBC A, D
                dis = String.Format("SBC A, D");
                break;

                case 0x9B:  //SBC A, E
                dis = String.Format("SBC A, E");
                break;

                case 0x9C:  //SBC A, H
                dis = String.Format("SBC A, H");
                break;

                case 0x9D:  //SBC A, L
                dis = String.Format("SBC A, L");
                break;

                case 0x9E:  //SBC A, (HL)
                dis = String.Format("SBC A, (HL)");
                break;

                case 0x9F:  //SBC A, A
                dis = String.Format("SBC A, A");
                break;

                case 0xDE:  //SBC A, n
                disp = PeekByteNoContend(PC);
                dis = String.Format("SBC A, ${0:x}", disp);
                PC += 1;
                break;
                #endregion

                #region Relative Jumps (JR / DJNZ)
                /*** Relative Jumps ***/
                case 0x10:  //DJNZ n
                disp = GetDisplacement(PeekByteNoContend(PC));
                dis = String.Format("DJNZ ${0:x}", PC + disp + 1);
                PC++;
                break;

                case 0x18:  //JR n
                disp = GetDisplacement(PeekByteNoContend(PC));
                dis = String.Format("JR ${0:x}", PC + disp + 1);
                PC++;
                break;

                case 0x20:  //JRNZ n
                disp = GetDisplacement(PeekByteNoContend(PC));
                dis = String.Format("JR NZ, ${0:x}", PC + disp + 1);
                PC++;
                break;

                case 0x28:  //JRZ n
                disp = GetDisplacement(PeekByteNoContend(PC));
                dis = String.Format("JR Z, ${0:x}", PC + disp + 1);
                PC++;
                break;

                case 0x30:  //JRNC n
                disp = GetDisplacement(PeekByteNoContend(PC));
                dis = String.Format("JR NC, ${0:x}", PC + disp + 1);
                PC++;
                break;

                case 0x38:  //JRC n
                disp = GetDisplacement(PeekByteNoContend(PC));
                dis = String.Format("JR C, ${0:x}", PC + disp + 1);
                PC++;
                break;
                #endregion

                #region Direct jumps (JP)
                /*** Direct jumps ***/
                case 0xC2:  //JPNZ nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("JP NZ, ${0:x}", disp);
                PC += 2;
                break;

                case 0xC3:  //JP nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("JP ${0:x}", disp);
                PC += 2;
                break;

                case 0xCA:  //JPZ nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("JP Z, ${0:x}", disp);
                PC += 2;
                break;

                case 0xD2:  //JPNC nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("JP NC, ${0:x}", disp);
                PC += 2;
                break;

                case 0xDA:  //JPC nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("JP C, ${0:x}", disp);
                PC += 2;
                break;

                case 0xE2:  //JP PO nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("JP PO, ${0:x}", disp);
                PC += 2;
                break;

                case 0xE9:  //JP (HL)
                dis = String.Format("JP (HL)");
                break;

                case 0xEA:  //JP PE nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("JP PE, ${0:x}", disp);
                PC += 2;
                break;

                case 0xF2:  //JP P nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("JP P, ${0:x}", disp);
                PC += 2;
                break;

                case 0xFA:  //JP M nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("JP M, ${0:x}", disp);
                PC += 2;
                break;
                #endregion

                #region Compare instructions (CP)
                /*** Compare instructions **/
                case 0xB8:  //CP B
                dis = String.Format("CP B");
                break;

                case 0xB9:  //CP C
                dis = String.Format("CP C");
                break;

                case 0xBA:  //CP D
                dis = String.Format("CP D");
                break;

                case 0xBB:  //CP E
                dis = String.Format("CP E");
                break;

                case 0xBC:  //CP H
                dis = String.Format("CP H");
                break;

                case 0xBD:  //CP L
                dis = String.Format("CP L");
                break;

                case 0xBE:  //CP (HL)
                dis = String.Format("CP (HL)");
                break;

                case 0xBF:  //CP A
                dis = String.Format("CP A");
                break;

                case 0xFE:  //CP n
                disp = PeekByteNoContend(PC);
                dis = String.Format("CP ${0:x}", disp);
                PC += 1;
                break;
                #endregion

                #region Carry Flag operations
                /*** Carry Flag operations ***/
                case 0x37:  //SCF
                dis = String.Format("SCF");
                break;

                case 0x3F:  //CCF
                dis = String.Format("CCF");
                break;
                #endregion

                #region Bitwise AND (AND r)
                case 0xA0:  //AND B
                dis = String.Format("AND B");
                break;

                case 0xA1:  //AND C
                dis = String.Format("AND C");
                break;

                case 0xA2:  //AND D
                dis = String.Format("AND D");
                break;

                case 0xA3:  //AND E
                dis = String.Format("AND E");
                break;

                case 0xA4:  //AND H
                dis = String.Format("AND H");
                break;

                case 0xA5:  //AND L
                dis = String.Format("AND L");
                break;

                case 0xA6:  //AND (HL)
                dis = String.Format("AND (HL)");
                break;

                case 0xA7:  //AND A
                dis = String.Format("AND A");
                break;

                case 0xE6:  //AND n
                disp = PeekByteNoContend(PC);
                dis = String.Format("AND ${0:x}", disp);
                PC++;
                break;
                #endregion

                #region Bitwise XOR (XOR r)
                case 0xA8: //XOR B
                dis = String.Format("XOR B");
                break;

                case 0xA9: //XOR C
                dis = String.Format("XOR C");
                break;

                case 0xAA: //XOR D
                dis = String.Format("XOR D");
                break;

                case 0xAB: //XOR E
                dis = String.Format("XOR E");
                break;

                case 0xAC: //XOR H
                dis = String.Format("XOR H");
                break;

                case 0xAD: //XOR L
                dis = String.Format("XOR L");
                break;

                case 0xAE: //XOR (HL)
                dis = String.Format("XOR (HL)");
                break;

                case 0xAF: //XOR A
                dis = String.Format("XOR A");
                break;

                case 0xEE:  //XOR n
                disp = PeekByteNoContend(PC);
                dis = String.Format("XOR ${0:x}", disp);
                PC++;
                break;

                #endregion

                #region Bitwise OR (OR r)
                case 0xB0:  //OR B
                dis = String.Format("OR B");
                break;

                case 0xB1:  //OR C
                dis = String.Format("OR C");
                break;

                case 0xB2:  //OR D
                dis = String.Format("OR D");
                break;

                case 0xB3:  //OR E
                dis = String.Format("OR E");
                break;

                case 0xB4:  //OR H
                dis = String.Format("OR H");
                break;

                case 0xB5:  //OR L
                dis = String.Format("OR L");
                break;

                case 0xB6:  //OR (HL)
                dis = String.Format("OR (HL)");
                break;

                case 0xB7:  //OR A
                dis = String.Format("OR A");
                break;

                case 0xF6:  //OR n
                disp = PeekByteNoContend(PC);
                dis = String.Format("OR ${0:x}", disp);
                PC++;
                break;
                #endregion

                #region Return instructions
                case 0xC0:  //RET NZ
                dis = String.Format("RET NZ");
                break;

                case 0xC8:  //RET Z
                dis = String.Format("RET Z");
                break;

                case 0xC9:  //RET
                dis = String.Format("RET");
                break;

                case 0xD0:  //RET NC
                dis = String.Format("RET NC");
                break;

                case 0xD8:  //RET C
                dis = String.Format("RET C");
                break;

                case 0xE0:  //RET PO
                dis = String.Format("RET PO");
                break;

                case 0xE8:  //RET PE
                dis = String.Format("RET PE");
                break;

                case 0xF0:  //RET P
                dis = String.Format("RET P");
                break;

                case 0xF8:  //RET M
                dis = String.Format("RET M");
                break;
                #endregion

                #region POP/PUSH instructions (Fix these for SP overflow later!)
                case 0xC1:  //POP BC
                dis = String.Format("POP BC");
                break;

                case 0xC5:  //PUSH BC
                dis = String.Format("PUSH BC");
                break;

                case 0xD1:  //POP DE
                dis = String.Format("POP DE");
                break;

                case 0xD5:  //PUSH DE
                dis = String.Format("PUSH DE");
                break;

                case 0xE1:  //POP HL
                dis = String.Format("POP HL");
                break;

                case 0xE5:  //PUSH HL
                dis = String.Format("PUSH HL");
                break;

                case 0xF1:  //POP AF
                dis = String.Format("POP AF");
                break;

                case 0xF5:  //PUSH AF
                dis = String.Format("PUSH AF");
                break;
                #endregion

                #region CALL instructions
                case 0xC4:  //CALL NZ, nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("CALL NZ, ${0:x}", disp);
                PC += 2;
                break;

                case 0xCC:  //CALL Z, nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("CALL Z, ${0:x}", disp);
                PC += 2;
                break;

                case 0xCD:  //CALL nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("CALL ${0:x}", disp);
                PC += 2;
                break;

                case 0xD4:  //CALL NC, nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("CALL NC, ${0:x}", disp);
                PC += 2;
                break;

                case 0xDC:  //CALL C, nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("CALL C, ${0:x}", disp);
                PC += 2;
                break;

                case 0xE4:  //CALL PO, nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("CALL PO, ${0:x}", disp);
                PC += 2;
                break;

                case 0xEC:  //CALL PE, nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("CALL PE, ${0:x}", disp);
                PC += 2;
                break;

                case 0xF4:  //CALL P, nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("CALL P, ${0:x}", disp);
                PC += 2;
                break;

                case 0xFC:  //CALL M, nn
                disp = PeekWordNoContend(PC);
                dis = String.Format("CALL M, ${0:x}", disp);
                PC += 2;
                break;
                #endregion

                #region Restart instructions (RST n)
                case 0xC7:  //RST 0x00
                dis = String.Format("RST ${0:x}", 0);
                break;

                case 0xCF:  //RST 0x08
                dis = String.Format("RST ${0:x}", 8);
                break;

                case 0xD7:  //RST 0x10
                dis = String.Format("RST ${0:x}", 16);
                break;

                case 0xDF:  //RST 0x18
                dis = String.Format("RST ${0:x}", 24);
                break;

                case 0xE7:  //RST 0x20
                dis = String.Format("RST ${0:x}", 32);
                break;

                case 0xEF:  //RST 0x28
                dis = String.Format("RST ${0:x}", 40);
                break;

                case 0xF7:  //RST 0x30
                dis = String.Format("RST ${0:x}", 48);
                break;

                case 0xFF:  //RST 0x38
                dis = String.Format("RST ${0:x}", 56);
                break;
                #endregion

                #region IN instructions
                case 0xDB:  //IN A, (n)
                disp = PeekByteNoContend(PC);
                dis = String.Format("IN A, (${0:x})", disp);
                PC++;
                break;
                #endregion

                #region OUT instructions
                case 0xD3:  //OUT (n), A
                disp = PeekByteNoContend(PC);
                dis = String.Format("OUT (${0:x}), A", disp);
                PC++;
                break;
                #endregion

                #region Decimal Adjust Accumulator (DAA)
                case 0x27:  //DAA
                dis = String.Format("DAA");
                break;
                #endregion

                #region Complement (CPL)
                case 0x2f:  //CPL
                dis = String.Format("CPL");
                break;
                #endregion

                #region Halt (HALT) - TO BE CHECKED!
                case 0x76:  //HALT
                dis = String.Format("HALT");
                break;
                #endregion

                #region Interrupts
                case 0xF3:  //DI
                dis = String.Format("DI");
                break;

                case 0xFB:  //EI
                dis = String.Format("EI");
                break;
                #endregion

                #region Opcodes with CB prefix
                case 0xCB:
                switch (opcode = PeekByteNoContend(PC++)) {
                    #region Rotate instructions
                    case 0x00: //RLC B
                    dis = String.Format("RLC B");
                    break;

                    case 0x01: //RLC C
                    dis = String.Format("RLC C");
                    break;

                    case 0x02: //RLC D
                    dis = String.Format("RLC D");
                    break;

                    case 0x03: //RLC E
                    dis = String.Format("RLC E");
                    break;

                    case 0x04: //RLC H
                    dis = String.Format("RLC H");
                    break;

                    case 0x05: //RLC L
                    dis = String.Format("RLC L");
                    break;

                    case 0x06: //RLC (HL)
                    dis = String.Format("RLC (HL)");
                    break;

                    case 0x07: //RLC A
                    dis = String.Format("RLC A");
                    break;

                    case 0x08: //RRC B
                    dis = String.Format("RRC B");
                    break;

                    case 0x09: //RRC C
                    dis = String.Format("RRC C");
                    break;

                    case 0x0A: //RRC D
                    dis = String.Format("RRC D");
                    break;

                    case 0x0B: //RRC E
                    dis = String.Format("RRC E");
                    break;

                    case 0x0C: //RRC H
                    dis = String.Format("RRC H");
                    break;

                    case 0x0D: //RRC L
                    dis = String.Format("RRC L");
                    break;

                    case 0x0E: //RRC (HL)
                    dis = String.Format("RRC (HL)");
                    break;

                    case 0x0F: //RRC A
                    dis = String.Format("RRC A");
                    break;

                    case 0x10: //RL B
                    dis = String.Format("RL B");
                    break;

                    case 0x11: //RL C
                    dis = String.Format("RL C");
                    break;

                    case 0x12: //RL D
                    dis = String.Format("RL D");
                    break;

                    case 0x13: //RL E
                    dis = String.Format("RL E");
                    break;

                    case 0x14: //RL H
                    dis = String.Format("RL H");
                    break;

                    case 0x15: //RL L
                    dis = String.Format("RL L");
                    break;

                    case 0x16: //RL (HL)
                    dis = String.Format("RL (HL)");
                    break;

                    case 0x17: //RL A
                    dis = String.Format("RL A");
                    break;

                    case 0x18: //RR B
                    dis = String.Format("RR B");
                    break;

                    case 0x19: //RR C
                    dis = String.Format("RR C");
                    break;

                    case 0x1A: //RR D
                    dis = String.Format("RR D");
                    break;

                    case 0x1B: //RR E
                    dis = String.Format("RR E");
                    break;

                    case 0x1C: //RR H
                    dis = String.Format("RR H");
                    break;

                    case 0x1D: //RR L
                    dis = String.Format("RR L");
                    break;

                    case 0x1E: //RR (HL)
                    dis = String.Format("RR (HL)");
                    break;

                    case 0x1F: //RR A
                    dis = String.Format("RR A");
                    break;
                    #endregion

                    #region Register shifts
                    case 0x20:  //SLA B
                    dis = String.Format("SLA B");
                    break;

                    case 0x21:  //SLA C
                    dis = String.Format("SLA C");
                    break;

                    case 0x22:  //SLA D
                    dis = String.Format("SLA D");
                    break;

                    case 0x23:  //SLA E
                    dis = String.Format("SLA E");
                    break;

                    case 0x24:  //SLA H
                    dis = String.Format("SLA H");
                    break;

                    case 0x25:  //SLA L
                    dis = String.Format("SLA L");
                    break;

                    case 0x26:  //SLA (HL)
                    dis = String.Format("SLA (HL)");
                    break;

                    case 0x27:  //SLA A
                    dis = String.Format("SLA A");
                    break;

                    case 0x28:  //SRA B
                    dis = String.Format("SRA B");
                    break;

                    case 0x29:  //SRA C
                    dis = String.Format("SRA C");
                    break;

                    case 0x2A:  //SRA D
                    dis = String.Format("SRA D");
                    break;

                    case 0x2B:  //SRA E
                    dis = String.Format("SRA E");
                    break;

                    case 0x2C:  //SRA H
                    dis = String.Format("SRA H");
                    break;

                    case 0x2D:  //SRA L
                    dis = String.Format("SRA L");
                    break;

                    case 0x2E:  //SRA (HL)
                    dis = String.Format("SRA (HL)");
                    break;

                    case 0x2F:  //SRA A
                    dis = String.Format("SRA A");
                    break;

                    case 0x30:  //SLL B
                    dis = String.Format("SLL B");
                    break;

                    case 0x31:  //SLL C
                    dis = String.Format("SLL C");
                    break;

                    case 0x32:  //SLL D
                    dis = String.Format("SLL D");
                    break;

                    case 0x33:  //SLL E
                    dis = String.Format("SLL E");
                    break;

                    case 0x34:  //SLL H
                    dis = String.Format("SLL H");
                    break;

                    case 0x35:  //SLL L
                    dis = String.Format("SLL L");
                    break;

                    case 0x36:  //SLL (HL)
                    dis = String.Format("SLL (HL)");
                    break;

                    case 0x37:  //SLL A
                    dis = String.Format("SLL A");
                    break;

                    case 0x38:  //SRL B
                    dis = String.Format("SRL B");
                    break;

                    case 0x39:  //SRL C
                    dis = String.Format("SRL C");
                    break;

                    case 0x3A:  //SRL D
                    dis = String.Format("SRL D");
                    break;

                    case 0x3B:  //SRL E
                    dis = String.Format("SRL E");
                    break;

                    case 0x3C:  //SRL H
                    dis = String.Format("SRL H");
                    break;

                    case 0x3D:  //SRL L
                    dis = String.Format("SRL L");
                    break;

                    case 0x3E:  //SRL (HL)
                    dis = String.Format("SRL (HL)");
                    break;

                    case 0x3F:  //SRL A
                    dis = String.Format("SRL A");
                    break;
                    #endregion

                    #region Bit test operation (BIT b, r)
                    case 0x40:  //BIT 0, B
                    dis = String.Format("BIT 0, B");
                    //(0, B);
                    break;

                    case 0x41:  //BIT 0, C
                    dis = String.Format("BIT 0, C");
                    //(0, C);
                    break;

                    case 0x42:  //BIT 0, D
                    dis = String.Format("BIT 0, D");
                    //(0, D);
                    break;

                    case 0x43:  //BIT 0, E
                    dis = String.Format("BIT 0, E");
                    //(0, E);
                    break;

                    case 0x44:  //BIT 0, H
                    dis = String.Format("BIT 0, H");
                    //(0, H);
                    break;

                    case 0x45:  //BIT 0, L
                    dis = String.Format("BIT 0, L");
                    //(0, L);
                    break;

                    case 0x46:  //BIT 0, (HL)
                    dis = String.Format("BIT 0, (HL)");
                    //(0, PeekByteNoContend(HL));
                    break;

                    case 0x47:  //BIT 0, A
                    dis = String.Format("BIT 0, A");
                    //(0, A);
                    break;

                    case 0x48:  //BIT 1, B
                    dis = String.Format("BIT 1, B");
                    //(1, B);
                    break;

                    case 0x49:  //BIT 1, C
                    dis = String.Format("BIT 1, C");
                    //(1, C);
                    break;

                    case 0x4A:  //BIT 1, D
                    dis = String.Format("BIT 1, D");
                    //(1, D);
                    break;

                    case 0x4B:  //BIT 1, E
                    dis = String.Format("BIT 1, E");
                    //(1, E);
                    break;

                    case 0x4C:  //BIT 1, H
                    dis = String.Format("BIT 1, H");
                    //(1, H);
                    break;

                    case 0x4D:  //BIT 1, L
                    dis = String.Format("BIT 1, L");
                    //(1, L);
                    break;

                    case 0x4E:  //BIT 1, (HL)
                    dis = String.Format("BIT 1, (HL)");
                    //(1, PeekByteNoContend(HL));
                    break;

                    case 0x4F:  //BIT 1, A
                    dis = String.Format("BIT 1, A");
                    //(1, A);
                    break;

                    case 0x50:  //BIT 2, B
                    dis = String.Format("BIT 2, B");
                    //(2, B);
                    break;

                    case 0x51:  //BIT 2, C
                    dis = String.Format("BIT 2, C");
                    //(2, C);
                    break;

                    case 0x52:  //BIT 2, D
                    dis = String.Format("BIT 2, D");
                    //(2, D);
                    break;

                    case 0x53:  //BIT 2, E
                    dis = String.Format("BIT 2, E");
                    //(2, E);
                    break;

                    case 0x54:  //BIT 2, H
                    dis = String.Format("BIT 2, H");
                    //(2, H);
                    break;

                    case 0x55:  //BIT 2, L
                    dis = String.Format("BIT 2, L");
                    //(2, L);
                    break;

                    case 0x56:  //BIT 2, (HL)
                    dis = String.Format("BIT 2, (HL)");
                    //(2, PeekByteNoContend(HL));
                    break;

                    case 0x57:  //BIT 2, A
                    dis = String.Format("BIT 2, A");
                    //(2, A);
                    break;

                    case 0x58:  //BIT 3, B
                    dis = String.Format("BIT 3, B");
                    //(3, B);
                    break;

                    case 0x59:  //BIT 3, C
                    dis = String.Format("BIT 3, C");
                    //(3, C);
                    break;

                    case 0x5A:  //BIT 3, D
                    dis = String.Format("BIT 3, D");
                    //(3, D);
                    break;

                    case 0x5B:  //BIT 3, E
                    dis = String.Format("BIT 3, E");
                    //(3, E);
                    break;

                    case 0x5C:  //BIT 3, H
                    dis = String.Format("BIT 3, H");
                    //(3, H);
                    break;

                    case 0x5D:  //BIT 3, L
                    dis = String.Format("BIT 3, L");
                    //(3, L);
                    break;

                    case 0x5E:  //BIT 3, (HL)
                    dis = String.Format("BIT 3, (HL)");
                    //(3, PeekByteNoContend(HL));
                    break;

                    case 0x5F:  //BIT 3, A
                    dis = String.Format("BIT 3, A");
                    //(3, A);
                    break;

                    case 0x60:  //BIT 4, B
                    dis = String.Format("BIT 4, B");
                    //(4, B);
                    break;

                    case 0x61:  //BIT 4, C
                    dis = String.Format("BIT 4, C");
                    //(4, C);
                    break;

                    case 0x62:  //BIT 4, D
                    dis = String.Format("BIT 4, D");
                    //(4, D);
                    break;

                    case 0x63:  //BIT 4, E
                    dis = String.Format("BIT 4, E");
                    //(4, E);
                    break;

                    case 0x64:  //BIT 4, H
                    dis = String.Format("BIT 4, H");
                    //(4, H);
                    break;

                    case 0x65:  //BIT 4, L
                    dis = String.Format("BIT 4, L");
                    //(4, L);
                    break;

                    case 0x66:  //BIT 4, (HL)
                    dis = String.Format("BIT 4, (HL)");
                    //(4, PeekByteNoContend(HL));
                    break;

                    case 0x67:  //BIT 4, A
                    dis = String.Format("BIT 4, A");
                    //(4, A);
                    break;

                    case 0x68:  //BIT 5, B
                    dis = String.Format("BIT 5, B");
                    //(5, B);
                    break;

                    case 0x69:  //BIT 5, C
                    dis = String.Format("BIT 5, C");
                    //(5, C);
                    break;

                    case 0x6A:  //BIT 5, D
                    dis = String.Format("BIT 5, D");
                    //(5, D);
                    break;

                    case 0x6B:  //BIT 5, E
                    dis = String.Format("BIT 5, E");
                    //(5, E);
                    break;

                    case 0x6C:  //BIT 5, H
                    dis = String.Format("BIT 5, H");
                    //(5, H);
                    break;

                    case 0x6D:  //BIT 5, L
                    dis = String.Format("BIT 5, L");
                    //(5, L);
                    break;

                    case 0x6E:  //BIT 5, (HL)
                    dis = String.Format("BIT 5, (HL)");
                    //(5, PeekByteNoContend(HL));
                    break;

                    case 0x6F:  //BIT 5, A
                    dis = String.Format("BIT 5, A");
                    //(5, A);
                    break;

                    case 0x70:  //BIT 6, B
                    dis = String.Format("BIT 6, B");
                    //(6, B);
                    break;

                    case 0x71:  //BIT 6, C
                    dis = String.Format("BIT 6, C");
                    //(6, C);
                    break;

                    case 0x72:  //BIT 6, D
                    dis = String.Format("BIT 6, D");
                    //(6, D);
                    break;

                    case 0x73:  //BIT 6, E
                    dis = String.Format("BIT 6, E");
                    //(6, E);
                    break;

                    case 0x74:  //BIT 6, H
                    dis = String.Format("BIT 6, H");
                    //(6, H);
                    break;

                    case 0x75:  //BIT 6, L
                    dis = String.Format("BIT 6, L");
                    //(6, L);
                    break;

                    case 0x76:  //BIT 6, (HL)
                    dis = String.Format("BIT 6, (HL)");
                    //(6, PeekByteNoContend(HL));
                    break;

                    case 0x77:  //BIT 6, A
                    dis = String.Format("BIT 6, A");
                    //(6, A);
                    break;

                    case 0x78:  //BIT 7, B
                    dis = String.Format("BIT 7, B");
                    //(7, B);
                    break;

                    case 0x79:  //BIT 7, C
                    dis = String.Format("BIT 7, C");
                    //(7, C);
                    break;

                    case 0x7A:  //BIT 7, D
                    dis = String.Format("BIT 7, D");
                    //(7, D);
                    break;

                    case 0x7B:  //BIT 7, E
                    dis = String.Format("BIT 7, E");
                    //(7, E);
                    break;

                    case 0x7C:  //BIT 7, H
                    dis = String.Format("BIT 7, H");
                    //(7, H);
                    break;

                    case 0x7D:  //BIT 7, L
                    dis = String.Format("BIT 7, L");
                    //(7, L);
                    break;

                    case 0x7E:  //BIT 7, (HL)
                    dis = String.Format("BIT 7, (HL)");
                    //(7, PeekByteNoContend(HL));
                    break;

                    case 0x7F:  //BIT 7, A
                    dis = String.Format("BIT 7, A");
                    //(7, A);
                    break;
                    #endregion

                    #region Reset bit operation (RES b, r)
                    case 0x80:  //RES 0, B
                    dis = String.Format("RES 0, B");
                    break;

                    case 0x81:  //RES 0, C
                    dis = String.Format("RES 0, C");
                    break;

                    case 0x82:  //RES 0, D
                    dis = String.Format("RES 0, D");
                    break;

                    case 0x83:  //RES 0, E
                    dis = String.Format("RES 0, E");
                    break;

                    case 0x84:  //RES 0, H
                    dis = String.Format("RES 0, H");
                    break;

                    case 0x85:  //RES 0, L
                    dis = String.Format("RES 0, L");
                    break;

                    case 0x86:  //RES 0, (HL)
                    dis = String.Format("RES 0, (HL)");
                    break;

                    case 0x87:  //RES 0, A
                    dis = String.Format("RES 0, A");
                    break;

                    case 0x88:  //RES 1, B
                    dis = String.Format("RES 1, B");
                    break;

                    case 0x89:  //RES 1, C
                    dis = String.Format("RES 1, C");
                    break;

                    case 0x8A:  //RES 1, D
                    dis = String.Format("RES 1, D");
                    break;

                    case 0x8B:  //RES 1, E
                    dis = String.Format("RES 1, E");
                    break;

                    case 0x8C:  //RES 1, H
                    dis = String.Format("RES 1, H");
                    break;

                    case 0x8D:  //RES 1, L
                    dis = String.Format("RES 1, L");
                    break;

                    case 0x8E:  //RES 1, (HL)
                    dis = String.Format("RES 1, (HL)");
                    break;

                    case 0x8F:  //RES 1, A
                    dis = String.Format("RES 1, A");
                    break;

                    case 0x90:  //RES 2, B
                    dis = String.Format("RES 2, B");
                    break;

                    case 0x91:  //RES 2, C
                    dis = String.Format("RES 2, C");
                    break;

                    case 0x92:  //RES 2, D
                    dis = String.Format("RES 2, D");
                    break;

                    case 0x93:  //RES 2, E
                    dis = String.Format("RES 2, E");
                    break;

                    case 0x94:  //RES 2, H
                    dis = String.Format("RES 2, H");
                    break;

                    case 0x95:  //RES 2, L
                    dis = String.Format("RES 2, L");
                    break;

                    case 0x96:  //RES 2, (HL)
                    dis = String.Format("RES 2, (HL)");
                    break;

                    case 0x97:  //RES 2, A
                    dis = String.Format("RES 2, A");
                    break;

                    case 0x98:  //RES 3, B
                    dis = String.Format("RES 3, B");
                    break;

                    case 0x99:  //RES 3, C
                    dis = String.Format("RES 3, C");
                    break;

                    case 0x9A:  //RES 3, D
                    dis = String.Format("RES 3, D");
                    break;

                    case 0x9B:  //RES 3, E
                    dis = String.Format("RES 3, E");
                    break;

                    case 0x9C:  //RES 3, H
                    dis = String.Format("RES 3, H");
                    break;

                    case 0x9D:  //RES 3, L
                    dis = String.Format("RES 3, L");
                    break;

                    case 0x9E:  //RES 3, (HL)
                    dis = String.Format("RES 3, (HL)");
                    break;

                    case 0x9F:  //RES 3, A
                    dis = String.Format("RES 3, A");
                    break;

                    case 0xA0:  //RES 4, B
                    dis = String.Format("RES 4, B");
                    break;

                    case 0xA1:  //RES 4, C
                    dis = String.Format("RES 4, C");
                    break;

                    case 0xA2:  //RES 4, D
                    dis = String.Format("RES 4, D");
                    break;

                    case 0xA3:  //RES 4, E
                    dis = String.Format("RES 4, E");
                    break;

                    case 0xA4:  //RES 4, H
                    dis = String.Format("RES 4, H");
                    break;

                    case 0xA5:  //RES 4, L
                    dis = String.Format("RES 4, L");
                    break;

                    case 0xA6:  //RES 4, (HL)
                    dis = String.Format("RES 4, (HL)");
                    break;

                    case 0xA7:  //RES 4, A
                    dis = String.Format("RES 4, A");
                    break;

                    case 0xA8:  //RES 5, B
                    dis = String.Format("RES 5, B");
                    break;

                    case 0xA9:  //RES 5, C
                    dis = String.Format("RES 5, C");
                    break;

                    case 0xAA:  //RES 5, D
                    dis = String.Format("RES 5, D");
                    break;

                    case 0xAB:  //RES 5, E
                    dis = String.Format("RES 5, E");
                    break;

                    case 0xAC:  //RES 5, H
                    dis = String.Format("RES 5, H");
                    break;

                    case 0xAD:  //RES 5, L
                    dis = String.Format("RES 5, L");
                    break;

                    case 0xAE:  //RES 5, (HL)
                    dis = String.Format("RES 5, (HL)");
                    break;

                    case 0xAF:  //RES 5, A
                    dis = String.Format("RES 5, A");
                    break;

                    case 0xB0:  //RES 6, B
                    dis = String.Format("RES 6, B");
                    break;

                    case 0xB1:  //RES 6, C
                    dis = String.Format("RES 6, C");
                    break;

                    case 0xB2:  //RES 6, D
                    dis = String.Format("RES 6, D");
                    break;

                    case 0xB3:  //RES 6, E
                    dis = String.Format("RES 6, E");
                    break;

                    case 0xB4:  //RES 6, H
                    dis = String.Format("RES 6, H");
                    break;

                    case 0xB5:  //RES 6, L
                    dis = String.Format("RES 6, L");
                    break;

                    case 0xB6:  //RES 6, (HL)
                    dis = String.Format("RES 6, (HL)");
                    break;

                    case 0xB7:  //RES 6, A
                    dis = String.Format("RES 6, A");
                    break;

                    case 0xB8:  //RES 7, B
                    dis = String.Format("RES 7, B");
                    break;

                    case 0xB9:  //RES 7, C
                    dis = String.Format("RES 7, C");
                    break;

                    case 0xBA:  //RES 7, D
                    dis = String.Format("RES 7, D");
                    break;

                    case 0xBB:  //RES 7, E
                    dis = String.Format("RES 7, E");
                    break;

                    case 0xBC:  //RES 7, H
                    dis = String.Format("RES 7, H");
                    break;

                    case 0xBD:  //RES 7, L
                    dis = String.Format("RES 7, L");
                    break;

                    case 0xBE:  //RES 7, (HL)
                    dis = String.Format("RES 7, (HL)");
                    break;

                    case 0xBF:  //RES 7, A
                    dis = String.Format("RES 7, A");
                    break;
                    #endregion

                    #region Set bit operation (SET b, r)
                    case 0xC0:  //SET 0, B
                    dis = String.Format("SET 0, B");
                    break;

                    case 0xC1:  //SET 0, C
                    dis = String.Format("SET 0, C");
                    break;

                    case 0xC2:  //SET 0, D
                    dis = String.Format("SET 0, D");
                    break;

                    case 0xC3:  //SET 0, E
                    dis = String.Format("SET 0, E");
                    break;

                    case 0xC4:  //SET 0, H
                    dis = String.Format("SET 0, H");
                    break;

                    case 0xC5:  //SET 0, L
                    dis = String.Format("SET 0, L");
                    break;

                    case 0xC6:  //SET 0, (HL)
                    dis = String.Format("SET 0, (HL)");
                    break;

                    case 0xC7:  //SET 0, A
                    dis = String.Format("SET 0, A");
                    break;

                    case 0xC8:  //SET 1, B
                    dis = String.Format("SET 1, B");
                    break;

                    case 0xC9:  //SET 1, C
                    dis = String.Format("SET 1, C");
                    break;

                    case 0xCA:  //SET 1, D
                    dis = String.Format("SET 1, D");
                    break;

                    case 0xCB:  //SET 1, E
                    dis = String.Format("SET 1, E");
                    break;

                    case 0xCC:  //SET 1, H
                    dis = String.Format("SET 1, H");
                    break;

                    case 0xCD:  //SET 1, L
                    dis = String.Format("SET 1, L");
                    break;

                    case 0xCE:  //SET 1, (HL)
                    dis = String.Format("SET 1, (HL)");
                    break;

                    case 0xCF:  //SET 1, A
                    dis = String.Format("SET 1, A");
                    break;

                    case 0xD0:  //SET 2, B
                    dis = String.Format("SET 2, B");
                    break;

                    case 0xD1:  //SET 2, C
                    dis = String.Format("SET 2, C");
                    break;

                    case 0xD2:  //SET 2, D
                    dis = String.Format("SET 2, D");
                    break;

                    case 0xD3:  //SET 2, E
                    dis = String.Format("SET 2, E");
                    break;

                    case 0xD4:  //SET 2, H
                    dis = String.Format("SET 2, H");
                    break;

                    case 0xD5:  //SET 2, L
                    dis = String.Format("SET 2, L");
                    break;

                    case 0xD6:  //SET 2, (HL)
                    dis = String.Format("SET 2, (HL)");
                    break;

                    case 0xD7:  //SET 2, A
                    dis = String.Format("SET 2, A");
                    break;

                    case 0xD8:  //SET 3, B
                    dis = String.Format("SET 3, B");
                    break;

                    case 0xD9:  //SET 3, C
                    dis = String.Format("SET 3, C");
                    break;

                    case 0xDA:  //SET 3, D
                    dis = String.Format("SET 3, D");
                    break;

                    case 0xDB:  //SET 3, E
                    dis = String.Format("SET 3, E");
                    break;

                    case 0xDC:  //SET 3, H
                    dis = String.Format("SET 3, H");
                    break;

                    case 0xDD:  //SET 3, L
                    dis = String.Format("SET 3, L");
                    break;

                    case 0xDE:  //SET 3, (HL)
                    dis = String.Format("SET 3, (HL)");
                    break;

                    case 0xDF:  //SET 3, A
                    dis = String.Format("SET 3, A");
                    break;

                    case 0xE0:  //SET 4, B
                    dis = String.Format("SET 4, B");
                    break;

                    case 0xE1:  //SET 4, C
                    dis = String.Format("SET 4, C");
                    break;

                    case 0xE2:  //SET 4, D
                    dis = String.Format("SET 4, D");
                    break;

                    case 0xE3:  //SET 4, E
                    dis = String.Format("SET 4, E");
                    break;

                    case 0xE4:  //SET 4, H
                    dis = String.Format("SET 4, H");
                    break;

                    case 0xE5:  //SET 4, L
                    dis = String.Format("SET 4, L");
                    break;

                    case 0xE6:  //SET 4, (HL)
                    dis = String.Format("SET 4, (HL)");
                    break;

                    case 0xE7:  //SET 4, A
                    dis = String.Format("SET 4, A");
                    break;

                    case 0xE8:  //SET 5, B
                    dis = String.Format("SET 5, B");
                    break;

                    case 0xE9:  //SET 5, C
                    dis = String.Format("SET 5, C");
                    break;

                    case 0xEA:  //SET 5, D
                    dis = String.Format("SET 5, D");
                    break;

                    case 0xEB:  //SET 5, E
                    dis = String.Format("SET 5, E");
                    break;

                    case 0xEC:  //SET 5, H
                    dis = String.Format("SET 5, H");
                    break;

                    case 0xED:  //SET 5, L
                    dis = String.Format("SET 5, L");
                    break;

                    case 0xEE:  //SET 5, (HL)
                    dis = String.Format("SET 5, (HL)");
                    break;

                    case 0xEF:  //SET 5, A
                    dis = String.Format("SET 5, A");
                    break;

                    case 0xF0:  //SET 6, B
                    dis = String.Format("SET 6, B");
                    break;

                    case 0xF1:  //SET 6, C
                    dis = String.Format("SET 6, C");
                    break;

                    case 0xF2:  //SET 6, D
                    dis = String.Format("SET 6, D");
                    break;

                    case 0xF3:  //SET 6, E
                    dis = String.Format("SET 6, E");
                    break;

                    case 0xF4:  //SET 6, H
                    dis = String.Format("SET 6, H");
                    break;

                    case 0xF5:  //SET 6, L
                    dis = String.Format("SET 6, L");
                    break;

                    case 0xF6:  //SET 6, (HL)
                    dis = String.Format("SET 6, (HL)");
                    break;

                    case 0xF7:  //SET 6, A
                    dis = String.Format("SET 6, A");
                    break;

                    case 0xF8:  //SET 7, B
                    dis = String.Format("SET 7, B");
                    break;

                    case 0xF9:  //SET 7, C
                    dis = String.Format("SET 7, C");
                    break;

                    case 0xFA:  //SET 7, D
                    dis = String.Format("SET 7, D");
                    break;

                    case 0xFB:  //SET 7, E
                    dis = String.Format("SET 7, E");
                    break;

                    case 0xFC:  //SET 7, H
                    dis = String.Format("SET 7, H");
                    break;

                    case 0xFD:  //SET 7, L
                    dis = String.Format("SET 7, L");
                    break;

                    case 0xFE:  //SET 7, (HL)
                    dis = String.Format("SET 7, (HL)");
                    break;

                    case 0xFF:  //SET 7, A
                    dis = String.Format("SET 7, A");
                    break;
                    #endregion

                    default:
                    dis = "Unknown DD opcode: " + opcode.ToString();
                    break;
                }
                break;
                #endregion

                #region Opcodes with DD prefix (includes DDCB)
                case 0xDD:
                switch (opcode = PeekByteNoContend(PC++)) {
                    #region Addition instructions
                    case 0x09:  //ADD IX, BC
                    dis = String.Format("ADD IX, BC");
                    break;

                    case 0x19:  //ADD IX, DE
                    dis = String.Format("ADD IX, DE");
                    break;

                    case 0x29:  //ADD IX, IX
                    dis = String.Format("ADD IX, IX");
                    break;

                    case 0x39:  //ADD IX, SP
                    dis = String.Format("ADD IX, SP");
                    break;

                    case 0x84:  //ADD A, IXH
                    dis = String.Format("ADD A, IXH");
                    break;

                    case 0x85:  //ADD A, IXL
                    dis = String.Format("ADD A, IXL");
                    break;

                    case 0x86:  //Add A, (IX+d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("ADD A, (IX + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x8C:  //ADC A, IXH
                    dis = String.Format("ADC A, IXH");
                    break;

                    case 0x8D:  //ADC A, IXL
                    dis = String.Format("ADC A, IXL");
                    break;

                    case 0x8E: //ADC A, (IX+d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("ADC A, (IX + ${0:x})", disp);
                    PC++;
                    break;
                    #endregion

                    #region Subtraction instructions
                    case 0x94:  //SUB A, IXH
                    dis = String.Format("SUB A, IXH");
                    break;

                    case 0x95:  //SUB A, IXL
                    dis = String.Format("SUB A, IXL");
                    break;

                    case 0x96:  //SUB (IX + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("SUB (IX + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x9C:  //SBC A, IXH
                    dis = String.Format("SBC A, IXH");
                    break;

                    case 0x9D:  //SBC A, IXL
                    dis = String.Format("SBC A, IXL");
                    break;

                    case 0x9E:  //SBC A, (IX + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("SBC A, (IX + ${0:x})", disp);
                    PC++;
                    break;
                    #endregion

                    #region Increment/Decrements
                    case 0x23:  //INC IX
                    dis = String.Format("INC IX");
                    break;

                    case 0x24:  //INC IXH
                    dis = String.Format("INC IXH");
                    break;

                    case 0x25:  //DEC IXH
                    dis = String.Format("DEC IXH");
                    break;

                    case 0x2B:  //DEC IX
                    dis = String.Format("DEC IX");
                    break;

                    case 0x2C:  //INC IXL
                    dis = String.Format("INC IXL");
                    break;

                    case 0x2D:  //DEC IXL
                    dis = String.Format("DEC IXL");
                    break;

                    case 0x34:  //INC (IX + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("INC (IX + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x35:  //DEC (IX + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("DEC (IX + ${0:x})", disp);
                    PC++;
                    break;
                    #endregion

                    #region Bitwise operators

                    case 0xA4:  //AND IXH
                    dis = String.Format("AND IXH");
                    break;

                    case 0xA5:  //AND IXL
                    dis = String.Format("AND IXL");
                    break;

                    case 0xA6:  //AND (IX + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("AND (IX + ${0:x})", disp);
                    PC++;
                    break;

                    case 0xAC:  //XOR IXH
                    dis = String.Format("XOR IXH");
                    break;

                    case 0xAD:  //XOR IXL
                    dis = String.Format("XOR IXL");
                    break;

                    case 0xAE:  //XOR (IX + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("XOR (IX + ${0:x})", disp);
                    PC++;
                    break;

                    case 0xB4:  //OR IXH
                    dis = String.Format("OR IXH");
                    break;

                    case 0xB5:  //OR IXL
                    dis = String.Format("OR IXL");
                    break;

                    case 0xB6:  //OR (IX + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("OR (IX + ${0:x})", disp);
                    PC++;
                    break;
                    #endregion

                    #region Compare operator
                    case 0xBC:  //CP IXH
                    dis = String.Format("CP IXH");
                    break;

                    case 0xBD:  //CP IXL
                    dis = String.Format("CP IXL");
                    break;

                    case 0xBE:  //CP (IX + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("CP (IX + ${0:x})", disp & 0xff);
                    PC++;
                    break;
                    #endregion

                    #region Load instructions
                    case 0x21:  //LD IX, nn
                    dis = String.Format("LD IX, ${0:x}", PeekWordNoContend(PC));
                    PC += 2;
                    break;

                    case 0x22:  //LD (nn), IX
                    dis = String.Format("LD (${0:x}), IX", PeekWordNoContend(PC));
                    PC += 2;
                    break;

                    case 0x26:  //LD IXH, n
                    dis = String.Format("LD IXH, ${0:x}", PeekByteNoContend(PC));
                    PC++;
                    break;

                    case 0x2A:  //LD IX, (nn)
                    dis = String.Format("LD IX, (${0:x})", PeekWordNoContend(PC));
                    PC += 2;
                    break;

                    case 0x2E:  //LD IXL, n
                    dis = String.Format("LD IXL, ${0:x}", PeekByteNoContend(PC));
                    PC++;
                    break;

                    case 0x36:  //LD (IX + d), n
                    disp = GetDisplacement(PeekByteNoContend(PC));

                    dis = String.Format("LD (IX + ${0:x}), {1:x}", disp, PeekByteNoContend((ushort)(PC + 1)));
                    PC += 2;
                    break;

                    case 0x44:  //LD B, IXH
                    dis = String.Format("LD B, IXH");
                    break;

                    case 0x45:  //LD B, IXL
                    dis = String.Format("LD B, IXL");
                    break;

                    case 0x46:  //LD B, (IX + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD B, (IX + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x4C:  //LD C, IXH
                    dis = String.Format("LD C, IXH");
                    break;

                    case 0x4D:  //LD C, IXL
                    dis = String.Format("LD C, IXL");
                    break;

                    case 0x4E:  //LD C, (IX + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD C, (IX + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x54:  //LD D, IXH
                    dis = String.Format("LD D, IXH");
                    break;

                    case 0x55:  //LD D, IXL
                    dis = String.Format("LD D, IXL");
                    break;

                    case 0x56:  //LD D, (IX + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD D, (IX + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x5C:  //LD E, IXH
                    dis = String.Format("LD E, IXH");
                    break;

                    case 0x5D:  //LD E, IXL
                    dis = String.Format("LD E, IXL");
                    break;

                    case 0x5E:  //LD E, (IX + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD E, (IX + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x60:  //LD IXH, B
                    dis = String.Format("LD IXH, B");
                    break;

                    case 0x61:  //LD IXH, C
                    dis = String.Format("LD IXH, C");
                    break;

                    case 0x62:  //LD IXH, D
                    dis = String.Format("LD IXH, D");
                    break;

                    case 0x63:  //LD IXH, E
                    dis = String.Format("LD IXH, E");
                    break;

                    case 0x64:  //LD IXH, IXH
                    dis = String.Format("LD IXH, IXH");
                    break;

                    case 0x65:  //LD IXH, IXL
                    dis = String.Format("LD IXH, IXL");
                    break;

                    case 0x66:  //LD H, (IX + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD H, (IX + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x67:  //LD IXH, A
                    dis = String.Format("LD IXH, A");
                    break;

                    case 0x68:  //LD IXL, B
                    dis = String.Format("LD IXL, B");
                    break;

                    case 0x69:  //LD IXL, C
                    dis = String.Format("LD IXL, C");
                    break;

                    case 0x6A:  //LD IXL, D
                    dis = String.Format("LD IXL, D");
                    break;

                    case 0x6B:  //LD IXL, E
                    dis = String.Format("LD IXL, E");
                    break;

                    case 0x6C:  //LD IXL, IXH
                    dis = String.Format("LD IXL, IXH");
                    break;

                    case 0x6D:  //LD IXL, IXL
                    dis = String.Format("LD IXL, IXL");
                    break;

                    case 0x6E:  //LD L, (IX + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD L, (IX + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x6F:  //LD IXL, A
                    dis = String.Format("LD IXL, A");
                    break;

                    case 0x70:  //LD (IX + d), B
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD (IX + ${0:x}), B", disp);
                    PC++;
                    break;

                    case 0x71:  //LD (IX + d), C
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD (IX + ${0:x}), C", disp);
                    PC++;
                    break;

                    case 0x72:  //LD (IX + d), D
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD (IX + ${0:x}), D", disp);
                    PC++;
                    break;

                    case 0x73:  //LD (IX + d), E
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD (IX + ${0:x}), E", disp);
                    PC++;
                    break;

                    case 0x74:  //LD (IX + d), H
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD (IX + ${0:x}), H", disp);
                    PC++;
                    break;

                    case 0x75:  //LD (IX + d), L
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD (IX + ${0:x}), L", disp);
                    PC++;
                    break;

                    case 0x77:  //LD (IX + d), A
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD (IX + ${0:x}), A", disp);
                    PC++;
                    break;

                    case 0x7C:  //LD A, IXH
                    dis = String.Format("LD A, IXH");
                    break;

                    case 0x7D:  //LD A, IXL
                    dis = String.Format("LD A, IXL");
                    break;

                    case 0x7E:  //LD A, (IX + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD A, (IX + ${0:x})", disp);
                    PC++;
                    break;

                    case 0xF9:  //LD SP, IX
                    dis = String.Format("LD SP, IX");
                    break;
                    #endregion

                    #region All DDCB instructions
                    case 0xCB:
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    PC++;
                    opcode = PeekByteNoContend(PC);      //The opcode comes after the offset byte!
                    PC++;
                    switch (opcode) {
                        case 0x00: //LD B, RLC (IX+d)
                        dis = String.Format("LD B, RLC (IX + ${0:x})", disp & 0xff & 0xff);
                        break;

                        case 0x01: //LD C, RLC (IX+d)
                        dis = String.Format("LD C, RLC (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x02: //LD D, RLC (IX+d)
                        dis = String.Format("LD D, RLC (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x03: //LD E, RLC (IX+d)
                        dis = String.Format("LD E, RLC (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x04: //LD H, RLC (IX+d)
                        dis = String.Format("LD H, RLC (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x05: //LD L, RLC (IX+d)
                        dis = String.Format("LD L, RLC (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x06:  //RLC (IX + d)
                        dis = String.Format("RLC (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x07: //LD A, RLC (IX+d)
                        dis = String.Format("LD A, RLC (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x08: //LD B, RRC (IX+d)
                        dis = String.Format("LD B, RRC (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x09: //LD C, RRC (IX+d)
                        dis = String.Format("LD C, RRC (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x0A: //LD D, RRC (IX+d)
                        dis = String.Format("LD D, RRC (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x0B: //LD E, RRC (IX+d)
                        dis = String.Format("LD E, RRC (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x0C: //LD H, RRC (IX+d)
                        dis = String.Format("LD H, RRC (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x0D: //LD L, RRC (IX+d)
                        dis = String.Format("LD L, RRC (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x0E:  //RRC (IX + d)
                        dis = String.Format("RRC (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x0F: //LD A, RRC (IX+d)
                        dis = String.Format("LD A, RRC (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x10: //LD B, RL (IX+d)
                        dis = String.Format("LD B, RL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x11: //LD C, RL (IX+d)
                        dis = String.Format("LD C, RL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x12: //LD D, RL (IX+d)
                        dis = String.Format("LD D, RL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x13: //LD E, RL (IX+d)
                        dis = String.Format("LD E, RL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x14: //LD H, RL (IX+d)
                        dis = String.Format("LD H, RL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x15: //LD L, RL (IX+d)
                        dis = String.Format("LD L, RL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x16:  //RL (IX + d)
                        dis = String.Format("RL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x17: //LD A, RL (IX+d)
                        dis = String.Format("LD A, RL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x18: //LD B, RR (IX+d)
                        dis = String.Format("LD B, RR (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x19: //LD C, RR (IX+d)
                        dis = String.Format("LD C, RR (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x1A: //LD D, RR (IX+d)
                        dis = String.Format("LD D, RR (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x1B: //LD E, RR (IX+d)
                        dis = String.Format("LD E, RR (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x1C: //LD H, RR (IX+d)
                        dis = String.Format("LD H, RR (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x1D: //LD L, RRC (IX+d)
                        dis = String.Format("LD L, RR (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x1E:  //RR (IX + d)
                        dis = String.Format("RR (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x1F: //LD A, RRC (IX+d)
                        dis = String.Format("LD A, RR (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x20: //LD B, SLA (IX+d)
                        dis = String.Format("LD B, SLA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x21: //LD C, SLA (IX+d)
                        dis = String.Format("LD C, SLA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x22: //LD D, SLA (IX+d)
                        dis = String.Format("LD D, SLA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x23: //LD E, SLA (IX+d)
                        dis = String.Format("LD E, SLA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x24: //LD H, SLA (IX+d)
                        dis = String.Format("LD H, SLA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x25: //LD L, SLA (IX+d)
                        dis = String.Format("LD L, SLA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x26:  //SLA (IX + d)
                        dis = String.Format("SLA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x27: //LD A, SLA (IX+d)
                        dis = String.Format("LD A, SLA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x28: //LD B, SRA (IX+d)
                        dis = String.Format("LD B, SRA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x29: //LD C, SRA (IX+d)
                        dis = String.Format("LD C, SRA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x2A: //LD D, SRA (IX+d)
                        dis = String.Format("LD D, SRA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x2B: //LD E, SRA (IX+d)
                        dis = String.Format("LD E, SRA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x2C: //LD H, SRA (IX+d)
                        dis = String.Format("LD H, SRA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x2D: //LD L, SRA (IX+d)
                        dis = String.Format("LD L, SRA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x2E:  //SRA (IX + d)
                        dis = String.Format("SRA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x2F: //LD A, SRA (IX+d)
                        dis = String.Format("LD A, SRA (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x30: //LD B, SLL (IX+d)
                        dis = String.Format("LD B, SLL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x31: //LD C, SLL (IX+d)
                        dis = String.Format("LD C, SLL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x32: //LD D, SLL (IX+d)
                        dis = String.Format("LD D, SLL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x33: //LD E, SLL (IX+d)
                        dis = String.Format("LD E, SLL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x34: //LD H, SLL (IX+d)
                        dis = String.Format("LD H, SLL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x35: //LD L, SLL (IX+d)
                        dis = String.Format("LD L, SLL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x36:  //SLL (IX + d)
                        dis = String.Format("SLL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x37: //LD A, SLL (IX+d)
                        dis = String.Format("LD A, SLL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x38: //LD B, SRL (IX+d)
                        dis = String.Format("LD B, SRL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x39: //LD C, SRL (IX+d)
                        dis = String.Format("LD C, SRL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x3A: //LD D, SRL (IX+d)
                        dis = String.Format("LD D, SRL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x3B: //LD E, SRL (IX+d)
                        dis = String.Format("LD E, SRL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x3C: //LD H, SRL (IX+d)
                        dis = String.Format("LD H, SRL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x3D: //LD L, SRL (IX+d)
                        dis = String.Format("LD L, SRL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x3E:  //SRL (IX + d)
                        dis = String.Format("SRL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x3F: //LD A, SRL (IX+d)
                        dis = String.Format("LD A, SRL (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x40:  //BIT 0, (IX + d)
                        dis = String.Format("BIT 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x41:  //BIT 0, (IX + d)
                        dis = String.Format("BIT 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x42:  //BIT 0, (IX + d)
                        dis = String.Format("BIT 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x43:  //BIT 0, (IX + d)
                        dis = String.Format("BIT 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x44:  //BIT 0, (IX + d)
                        dis = String.Format("BIT 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x45:  //BIT 0, (IX + d)
                        dis = String.Format("BIT 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x46:  //BIT 0, (IX + d)
                        dis = String.Format("BIT 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x47:  //BIT 0, (IX + d)
                        dis = String.Format("BIT 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x48:  //BIT 1, (IX + d)
                        dis = String.Format("BIT 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x49:  //BIT 1, (IX + d)
                        dis = String.Format("BIT 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x4A:  //BIT 1, (IX + d)
                        dis = String.Format("BIT 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x4B:  //BIT 1, (IX + d)
                        dis = String.Format("BIT 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x4C:  //BIT 1, (IX + d)
                        dis = String.Format("BIT 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x4D:  //BIT 1, (IX + d)
                        dis = String.Format("BIT 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x4E:  //BIT 1, (IX + d)
                        dis = String.Format("BIT 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x4F:  //BIT 1, (IX + d)
                        dis = String.Format("BIT 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x50:  //BIT 2, (IX + d)
                        dis = String.Format("BIT 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x51:  //BIT 2, (IX + d)
                        dis = String.Format("BIT 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x52:  //BIT 2, (IX + d)
                        dis = String.Format("BIT 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x53:  //BIT 2, (IX + d)
                        dis = String.Format("BIT 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x54:  //BIT 2, (IX + d)
                        dis = String.Format("BIT 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x55:  //BIT 2, (IX + d)
                        dis = String.Format("BIT 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x56:  //BIT 2, (IX + d)
                        dis = String.Format("BIT 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x57:  //BIT 2, (IX + d)
                        dis = String.Format("BIT 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x58:  //BIT 3, (IX + d)
                        dis = String.Format("BIT 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x59:  //BIT 3, (IX + d)
                        dis = String.Format("BIT 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x5A:  //BIT 3, (IX + d)
                        dis = String.Format("BIT 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x5B:  //BIT 3, (IX + d)
                        dis = String.Format("BIT 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x5C:  //BIT 3, (IX + d)
                        dis = String.Format("BIT 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x5D:  //BIT 3, (IX + d)
                        dis = String.Format("BIT 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x5E:  //BIT 3, (IX + d)
                        dis = String.Format("BIT 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x5F:  //BIT 3, (IX + d)
                        dis = String.Format("BIT 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x60:  //BIT 4, (IX + d)
                        dis = String.Format("BIT 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x61:  //BIT 4, (IX + d)
                        dis = String.Format("BIT 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x62:  //BIT 4, (IX + d)
                        dis = String.Format("BIT 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x63:  //BIT 4, (IX + d)
                        dis = String.Format("BIT 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x64:  //BIT 4, (IX + d)
                        dis = String.Format("BIT 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x65:  //BIT 4, (IX + d)
                        dis = String.Format("BIT 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x66:  //BIT 4, (IX + d)
                        dis = String.Format("BIT 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x67:  //BIT 4, (IX + d)
                        dis = String.Format("BIT 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x68:  //BIT 5, (IX + d)
                        dis = String.Format("BIT 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x69:  //BIT 5, (IX + d)
                        dis = String.Format("BIT 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x6A:  //BIT 5, (IX + d)
                        dis = String.Format("BIT 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x6B:  //BIT 5, (IX + d)
                        dis = String.Format("BIT 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x6C:  //BIT 5, (IX + d)
                        dis = String.Format("BIT 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x6D:  //BIT 5, (IX + d)
                        dis = String.Format("BIT 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x6E:  //BIT 5, (IX + d)
                        dis = String.Format("BIT 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x6F:  //BIT 5, (IX + d)
                        dis = String.Format("BIT 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x70:  //BIT 6, (IX + d)
                        dis = String.Format("BIT 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x71:  //BIT 6, (IX + d)
                        dis = String.Format("BIT 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x72:  //BIT 6, (IX + d)
                        dis = String.Format("BIT 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x73:  //BIT 6, (IX + d)
                        dis = String.Format("BIT 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x74:  //BIT 6, (IX + d)
                        dis = String.Format("BIT 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x75:  //BIT 6, (IX + d)
                        dis = String.Format("BIT 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x76:  //BIT 6, (IX + d)
                        dis = String.Format("BIT 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x77:  //BIT 6, (IX + d)
                        dis = String.Format("BIT 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x78:  //BIT 7, (IX + d)
                        dis = String.Format("BIT 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x79:  //BIT 7, (IX + d)
                        dis = String.Format("BIT 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x7A:  //BIT 7, (IX + d)
                        dis = String.Format("BIT 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x7B:  //BIT 7, (IX + d)
                        dis = String.Format("BIT 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x7C:  //BIT 7, (IX + d)
                        dis = String.Format("BIT 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x7D:  //BIT 7, (IX + d)
                        dis = String.Format("BIT 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x7E:  //BIT 7, (IX + d)
                        dis = String.Format("BIT 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x7F:  //BIT 7, (IX + d)
                        dis = String.Format("BIT 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x80: //LD B, RES 0, (IX+d)
                        dis = String.Format("LD B, RES 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x81: //LD C, RES 0, (IX+d)
                        dis = String.Format("LD C, RES 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x82: //LD D, RES 0, (IX+d)
                        dis = String.Format("LD D, RES 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x83: //LD E, RES 0, (IX+d)
                        dis = String.Format("LD E, RES 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x84: //LD H, RES 0, (IX+d)
                        dis = String.Format("LD H, RES 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x85: //LD L, RES 0, (IX+d)
                        dis = String.Format("LD L, RES 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x86:  //RES 0, (IX + d)
                        dis = String.Format("RES 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x87: //LD A, RES 0, (IX+d)
                        dis = String.Format("LD A, RES 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x88: //LD B, RES 1, (IX+d)
                        dis = String.Format("LD B, RES 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x89: //LD C, RES 1, (IX+d)
                        dis = String.Format("LD C, RES 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x8A: //LD D, RES 1, (IX+d)
                        dis = String.Format("LD D, RES 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x8B: //LD E, RES 1, (IX+d)
                        dis = String.Format("LD E, RES 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x8C: //LD H, RES 1, (IX+d)
                        dis = String.Format("LD H, RES 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x8D: //LD L, RES 1, (IX+d)
                        dis = String.Format("LD L, RES 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x8E:  //RES 1, (IX + d)
                        dis = String.Format("RES 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x8F: //LD A, RES 1, (IX+d)
                        dis = String.Format("LD A, RES 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x90: //LD B, RES 2, (IX+d)
                        dis = String.Format("LD B, RES 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x91: //LD C, RES 2, (IX+d)
                        dis = String.Format("LD C, RES 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x92: //LD D, RES 2, (IX+d)
                        dis = String.Format("LD D, RES 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x93: //LD E, RES 2, (IX+d)
                        dis = String.Format("LD E, RES 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x94: //LD H, RES 2, (IX+d)
                        dis = String.Format("LD H, RES 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x95: //LD L, RES 2, (IX+d)
                        dis = String.Format("LD L, RES 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x96:  //RES 2, (IX + d)
                        dis = String.Format("RES 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x97: //LD A, RES 2, (IX+d)
                        dis = String.Format("LD A, RES 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x98: //LD B, RES 3, (IX+d)
                        dis = String.Format("LD B, RES 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x99: //LD C, RES 3, (IX+d)
                        dis = String.Format("LD C, RES 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x9A: //LD D, RES 3, (IX+d)
                        dis = String.Format("LD D, RES 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x9B: //LD E, RES 3, (IX+d)
                        dis = String.Format("LD E, RES 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x9C: //LD H, RES 3, (IX+d)
                        dis = String.Format("LD H, RES 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x9D: //LD L, RES 3, (IX+d)
                        dis = String.Format("LD L, RES 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x9E:  //RES 3, (IX + d)
                        dis = String.Format("RES 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0x9F: //LD A, RES 3, (IX+d)
                        dis = String.Format("LD A, RES 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xA0: //LD B, RES 4, (IX+d)
                        dis = String.Format("LD B, RES 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xA1: //LD C, RES 4, (IX+d)
                        dis = String.Format("LD C, RES 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xA2: //LD D, RES 4, (IX+d)
                        dis = String.Format("LD D, RES 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xA3: //LD E, RES 4, (IX+d)
                        dis = String.Format("LD E, RES 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xA4: //LD H, RES 4, (IX+d)
                        dis = String.Format("LD H, RES 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xA5: //LD L, RES 4, (IX+d)
                        dis = String.Format("LD L, RES 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xA6:  //RES 4, (IX + d)
                        dis = String.Format("RES 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xA7: //LD A, RES 4, (IX+d)
                        dis = String.Format("LD A, RES 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xA8: //LD B, RES 5, (IX+d)
                        dis = String.Format("LD B, RES 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xA9: //LD C, RES 5, (IX+d)
                        dis = String.Format("LD C, RES 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xAA: //LD D, RES 5, (IX+d)
                        dis = String.Format("LD D, RES 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xAB: //LD E, RES 5, (IX+d)
                        dis = String.Format("LD E, RES 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xAC: //LD H, RES 5, (IX+d)
                        dis = String.Format("LD H, RES 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xAD: //LD L, RES 5, (IX+d)
                        dis = String.Format("LD L, RES 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xAE:  //RES 5, (IX + d)
                        dis = String.Format("RES 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xAF: //LD A, RES 5, (IX+d)
                        dis = String.Format("LD A, RES 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xB0: //LD B, RES 6, (IX+d)
                        dis = String.Format("LD B, RES 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xB1: //LD C, RES 6, (IX+d)
                        dis = String.Format("LD C, RES 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xB2: //LD D, RES 6, (IX+d)
                        dis = String.Format("LD D, RES 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xB3: //LD E, RES 6, (IX+d)
                        dis = String.Format("LD E, RES 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xB4: //LD H, RES 5, (IX+d)
                        dis = String.Format("LD H, RES 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xB5: //LD L, RES 5, (IX+d)
                        dis = String.Format("LD L, RES 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xB6:  //RES 6, (IX + d)
                        dis = String.Format("RES 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xB7: //LD A, RES 5, (IX+d)
                        dis = String.Format("LD A, RES 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xB8: //LD B, RES 7, (IX+d)
                        dis = String.Format("LD B, RES 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xB9: //LD C, RES 7, (IX+d)
                        dis = String.Format("LD C, RES 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xBA: //LD D, RES 7, (IX+d)
                        dis = String.Format("LD D, RES 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xBB: //LD E, RES 7, (IX+d)
                        dis = String.Format("LD E, RES 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xBC: //LD H, RES 7, (IX+d)
                        dis = String.Format("LD H, RES 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xBD: //LD L, RES 7, (IX+d)
                        dis = String.Format("LD L, RES 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xBE:  //RES 7, (IX + d)
                        dis = String.Format("RES 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xBF: //LD A, RES 7, (IX+d)
                        dis = String.Format("LD A, RES 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xC0: //LD B, SET 0, (IX+d)
                        dis = String.Format("LD B, SET 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xC1: //LD C, SET 0, (IX+d)
                        dis = String.Format("LD C, SET 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xC2: //LD D, SET 0, (IX+d)
                        dis = String.Format("LD D, SET 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xC3: //LD E, SET 0, (IX+d)
                        dis = String.Format("LD E, SET 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xC4: //LD H, SET 0, (IX+d)
                        dis = String.Format("LD H, SET 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xC5: //LD L, SET 0, (IX+d)
                        dis = String.Format("LD L, SET 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xC6:  //SET 0, (IX + d)
                        dis = String.Format("SET 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xC7: //LD A, SET 0, (IX+d)
                        dis = String.Format("LD A, SET 0, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xC8: //LD B, SET 1, (IX+d)
                        dis = String.Format("LD B, SET 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xC9: //LD C, SET 0, (IX+d)
                        dis = String.Format("LD C, SET 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xCA: //LD D, SET 1, (IX+d)
                        dis = String.Format("LD D, SET 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xCB: //LD E, SET 1, (IX+d)
                        dis = String.Format("LD E, SET 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xCC: //LD H, SET 1, (IX+d)
                        dis = String.Format("LD H, SET 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xCD: //LD L, SET 1, (IX+d)
                        dis = String.Format("LD L, SET 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xCE:  //SET 1, (IX + d)
                        dis = String.Format("SET 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xCF: //LD A, SET 1, (IX+d)
                        dis = String.Format("LD A, SET 1, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xD0: //LD B, SET 2, (IX+d)
                        dis = String.Format("LD B, SET 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xD1: //LD C, SET 2, (IX+d)
                        dis = String.Format("LD C, SET 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xD2: //LD D, SET 2, (IX+d)
                        dis = String.Format("LD D, SET 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xD3: //LD E, SET 2, (IX+d)
                        dis = String.Format("LD E, SET 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xD4: //LD H, SET 21, (IX+d)
                        dis = String.Format("LD H, SET 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xD5: //LD L, SET 2, (IX+d)
                        dis = String.Format("LD L, SET 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xD6:  //SET 2, (IX + d)
                        dis = String.Format("SET 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xD7: //LD A, SET 2, (IX+d)
                        dis = String.Format("LD A, SET 2, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xD8: //LD B, SET 3, (IX+d)
                        dis = String.Format("LD B, SET 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xD9: //LD C, SET 3, (IX+d)
                        dis = String.Format("LD C, SET 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xDA: //LD D, SET 3, (IX+d)
                        dis = String.Format("LD D, SET 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xDB: //LD E, SET 3, (IX+d)
                        dis = String.Format("LD E, SET 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xDC: //LD H, SET 21, (IX+d)
                        dis = String.Format("LD H, SET 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xDD: //LD L, SET 3, (IX+d)
                        dis = String.Format("LD L, SET 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xDE:  //SET 3, (IX + d)
                        dis = String.Format("SET 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xDF: //LD A, SET 3, (IX+d)
                        dis = String.Format("LD A, SET 3, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xE0: //LD B, SET 4, (IX+d)
                        dis = String.Format("LD B, SET 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xE1: //LD C, SET 4, (IX+d)
                        dis = String.Format("LD C, SET 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xE2: //LD D, SET 4, (IX+d)
                        dis = String.Format("LD D, SET 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xE3: //LD E, SET 4, (IX+d)
                        dis = String.Format("LD E, SET 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xE4: //LD H, SET 4, (IX+d)
                        dis = String.Format("LD H, SET 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xE5: //LD L, SET 3, (IX+d)
                        dis = String.Format("LD L, SET 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xE6:  //SET 4, (IX + d)
                        dis = String.Format("SET 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xE7: //LD A, SET 4, (IX+d)
                        dis = String.Format("LD A, SET 4, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xE8: //LD B, SET 5, (IX+d)
                        dis = String.Format("LD B, SET 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xE9: //LD C, SET 5, (IX+d)
                        dis = String.Format("LD C, SET 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xEA: //LD D, SET 5, (IX+d)
                        dis = String.Format("LD D, SET 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xEB: //LD E, SET 5, (IX+d)
                        dis = String.Format("LD E, SET 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xEC: //LD H, SET 5, (IX+d)
                        dis = String.Format("LD H, SET 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xED: //LD L, SET 5, (IX+d)
                        dis = String.Format("LD L, SET 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xEE:  //SET 5, (IX + d)
                        dis = String.Format("SET 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xEF: //LD A, SET 5, (IX+d)
                        dis = String.Format("LD A, SET 5, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xF0: //LD B, SET 6, (IX+d)
                        dis = String.Format("LD B, SET 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xF1: //LD C, SET 6, (IX+d)
                        dis = String.Format("LD C, SET 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xF2: //LD D, SET 6, (IX+d)
                        dis = String.Format("LD D, SET 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xF3: //LD E, SET 6, (IX+d)
                        dis = String.Format("LD E, SET 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xF4: //LD H, SET 6, (IX+d)
                        dis = String.Format("LD H, SET 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xF5: //LD L, SET 6, (IX+d)
                        dis = String.Format("LD L, SET 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xF6:  //SET 6, (IX + d)
                        dis = String.Format("SET 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xF7: //LD A, SET 6, (IX+d)
                        dis = String.Format("LD A, SET 6, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xF8: //LD B, SET 7, (IX+d)
                        dis = String.Format("LD B, SET 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xF9: //LD C, SET 7, (IX+d)
                        dis = String.Format("LD C, SET 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xFA: //LD D, SET 7, (IX+d)
                        dis = String.Format("LD D, SET 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xFB: //LD E, SET 7, (IX+d)
                        dis = String.Format("LD E, SET 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xFC: //LD H, SET 7, (IX+d)
                        dis = String.Format("LD H, SET 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xFD: //LD L, SET 7, (IX+d)
                        dis = String.Format("LD L, SET 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xFE:  //SET 7, (IX + d)
                        dis = String.Format("SET 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        case 0xFF: //LD A, SET 7, (IX + D)
                        dis = String.Format("LD A, SET 7, (IX + ${0:x})", disp & 0xff);
                        break;

                        default:
                        dis = "Unknown DDCB opcode: " + opcode.ToString();
                        break;
                    }
                    break;
                    #endregion

                    #region Pop/Push instructions
                    case 0xE1:  //POP IX
                    dis = String.Format("POP IX");
                    break;

                    case 0xE5:  //PUSH IX
                    dis = String.Format("PUSH IX");
                    break;
                    #endregion

                    #region Exchange instruction
                    case 0xE3:  //EX (SP), IX
                    dis = String.Format("EX (SP), IX");
                    break;
                    #endregion

                    #region Jump instruction
                    case 0xE9:  //JP (IX)
                    dis = String.Format("JP (IX)");
                    break;
                    #endregion

                    default:
                    //According to Sean's doc: http://z80.info/z80sean.txt
                    //If a DDxx or FDxx instruction is not listed, it should operate as
                    //without the DD or FD prefix, and the DD or FD prefix itself should
                    //operate as a NOP.
                    jumpForUndoc = true;  //Try to excute it as a normal instruction then
                    break;
                }
                break;
                #endregion

                #region Opcodes with ED prefix
                case 0xED:
                opcode = PeekByteNoContend(PC++);
                if (opcode < 0x40) {
                    dis = String.Format("NOP");
                    break;
                }
                else
                    switch (opcode) {
                        case 0x40: //IN B, (C)
                        dis = String.Format("IN B, (C)");
                        break;

                        case 0x41: //Out (C), B
                        dis = String.Format("OUT (C), B");
                        break;

                        case 0x42:  //SBC HL, BC
                        dis = String.Format("SBC HL, BC");
                        break;

                        case 0x43:  //LD (nn), BC
                        disp = PeekWordNoContend(PC);
                        dis = String.Format("LD (${0:x}), BC", disp);
                        PC += 2;
                        break;

                        case 0x44:  //NEG
                        dis = String.Format("NEG");
                        break;

                        case 0x45:  //RETN
                        dis = String.Format("RET N");
                        break;

                        case 0x46:  //IM0
                        dis = String.Format("IM 0");
                        break;

                        case 0x47:  //LD I, A
                        dis = String.Format("LD I, A");
                        break;

                        case 0x48: //IN C, (C)
                        dis = String.Format("IN C, (C)");
                        break;

                        case 0x49: //Out (C), C
                        dis = String.Format("OUT (C), C");
                        break;

                        case 0x4A:  //ADC HL, BC
                        dis = String.Format("ADC HL, BC");
                        break;

                        case 0x4B:  //LD BC, (nn)
                        disp = PeekWordNoContend(PC);
                        dis = String.Format("LD BC, (${0:x})", disp);
                        PC += 2;
                        break;

                        case 0x4C:  //NEG
                        dis = String.Format("NEG");
                        break;

                        case 0x4D:  //RETI
                        dis = String.Format("RETI");
                        break;

                        case 0x4F:  //LD R, A
                        dis = String.Format("LD R, A");
                        break;

                        case 0x50: //IN D, (C)
                        dis = String.Format("IN D, (C)");
                        break;

                        case 0x51: //Out (C), D
                        dis = String.Format("OUT (C), D");
                        break;

                        case 0x52:  //SBC HL, DE
                        dis = String.Format("SBC HL, DE");
                        break;

                        case 0x53:  //LD (nn), DE
                        disp = PeekWordNoContend(PC);
                        dis = String.Format("LD (${0:x}), DE", disp);
                        PC += 2;
                        break;

                        case 0x54:  //NEG
                        dis = String.Format("NEG");
                        break;

                        case 0x55:  //RETN
                        dis = String.Format("RETN");
                        break;

                        case 0x56:  //IM1
                        dis = String.Format("IM 1");
                        break;

                        case 0x57:  //LD A, I
                        dis = String.Format("LD A, I");
                        break;

                        case 0x58: //IN E, (C)
                        dis = String.Format("IN E, (C)");
                        break;

                        case 0x59: //Out (C), E
                        dis = String.Format("OUT (C), E");
                        break;

                        case 0x5A:  //ADC HL, DE
                        dis = String.Format("ADC HL, DE");
                        break;

                        case 0x5B:  //LD DE, (nn)
                        disp = PeekWordNoContend(PC);
                        dis = String.Format("LD DE, (${0:x})", disp);
                        PC += 2;
                        break;

                        case 0x5C:  //NEG
                        dis = String.Format("NEG");
                        break;

                        case 0x5D:  //RETN
                        dis = String.Format("RETN");
                        break;

                        case 0x5E:  //IM2
                        dis = String.Format("IM 2");
                        break;

                        case 0x5F:  //LD A, R
                        dis = String.Format("LD A, R");
                        break;

                        case 0x60: //IN H, (C)
                        dis = String.Format("IN H, (C)");
                        break;

                        case 0x61: //Out (C), H
                        dis = String.Format("OUT (C), H");
                        break;

                        case 0x62:  //SBC HL, HL
                        dis = String.Format("SBC HL, HL");
                        break;

                        case 0x63:  //LD (nn), HL
                        disp = PeekWordNoContend(PC);
                        dis = String.Format("LD (${0:x}), HL", disp);
                        PC += 2;
                        break;

                        case 0x64:  //NEG
                        dis = String.Format("NEG");
                        break;

                        case 0x65:  //RETN
                        dis = String.Format("RETN");
                        break;

                        case 0x67:  //RRD
                        dis = String.Format("RRD");
                        break;

                        case 0x68: //IN L, (C)
                        dis = String.Format("IN L, (C)");
                        break;

                        case 0x69: //Out (C), L
                        dis = String.Format("OUT (C), L");
                        break;

                        case 0x6A:  //ADC HL, HL
                        dis = String.Format("ADC HL, HL");
                        break;

                        case 0x6B:  //LD HL, (nn)
                        disp = PeekWordNoContend(PC);
                        dis = String.Format("LD HL, (${0:x})", disp);
                        PC += 2;
                        break;

                        case 0x6C:  //NEG
                        dis = String.Format("NEG");
                        break;

                        case 0x6D:  //RETN
                        dis = String.Format("RETN");
                        break;

                        case 0x6F:  //RLD
                        dis = String.Format("RLD");
                        break;

                        case 0x70:  //IN (C)
                        dis = String.Format("IN (C)");
                        break;

                        case 0x71:
                        dis = String.Format("OUT (C), 0");
                        break;

                        case 0x72:  //SBC HL, SP
                        dis = String.Format("SBC HL, SP");
                        break;

                        case 0x73:  //LD (nn), SP
                        disp = PeekWordNoContend(PC);
                        dis = String.Format("LD (${0:x}), SP", disp);
                        PC += 2;
                        break;

                        case 0x74:  //NEG
                        dis = String.Format("NEG");
                        break;

                        case 0x75:  //RETN
                        dis = String.Format("RETN");
                        break;

                        case 0x76:  //IM 1
                        dis = String.Format("IM 1");
                        break;

                        case 0x78:  //IN A, (C)
                        dis = String.Format("IN A, (C)");
                        break;

                        case 0x79: //Out (C), A
                        dis = String.Format("OUT (C), A");
                        break;

                        case 0x7A:  //ADC HL, SP
                        dis = String.Format("ADC HL, SP");
                        break;

                        case 0x7B:  //LD SP, (nn)
                        disp = PeekWordNoContend(PC);
                        dis = String.Format("LD SP, (${0:x})", disp);
                        PC += 2;
                        break;

                        case 0x7C:  //NEG
                        dis = String.Format("NEG");
                        break;

                        case 0x7D:  //RETN
                        dis = String.Format("RETN");
                        break;

                        case 0x7E:  //IM 2
                        dis = String.Format("IM 2");
                        break;

                        case 0xA0:  //LDI
                        dis = String.Format("LDI");
                        break;

                        case 0xA1:  //CPI
                        dis = String.Format("CPI");
                        break;

                        case 0xA2:  //INI
                        dis = String.Format("INI");
                        break;

                        case 0xA3:  //OUTI
                        dis = String.Format("OUTI");
                        break;

                        case 0xA8:  //LDD
                        dis = String.Format("LDD");
                        break;

                        case 0xA9:  //CPD
                        dis = String.Format("CPD");
                        break;

                        case 0xAA:  //IND
                        dis = String.Format("IND");
                        break;

                        case 0xAB:  //OUTD
                        dis = String.Format("OUTD");
                        break;

                        case 0xB0:  //LDIR
                        dis = String.Format("LDIR");
                        break;

                        case 0xB1:  //CPIR
                        dis = String.Format("CPIR");
                        break;

                        case 0xB2:  //INIR
                        dis = String.Format("INIR");
                        break;

                        case 0xB3:  //OTIR
                        dis = String.Format("OTIR");
                        break;

                        case 0xB8:  //LDDR
                        dis = String.Format("LDDR");
                        break;

                        case 0xB9:  //CPDR
                        dis = String.Format("CPDR");
                        break;

                        case 0xBA:  //INDR
                        dis = String.Format("INDR");
                        break;

                        case 0xBB:  //OTDR
                        dis = String.Format("OTDR");
                        break;

                        default:
                        //According to Sean's doc: http://z80.info/z80sean.txt
                        //If an EDxx instruction is not listed, it should operate as two NOPs.
                        break; //Carry on to next instruction then
                    }
                break;
                #endregion

                #region Opcodes with FD prefix (includes FDCB)
                case 0xFD:
                switch (opcode = PeekByteNoContend(PC++)) {
                    #region Addition instructions
                    case 0x09:  //ADD IY, BC
                    dis = String.Format("ADD IY, BC");
                    break;

                    case 0x19:  //ADD IY, DE
                    dis = String.Format("ADD IY, DE");
                    break;

                    case 0x29:  //ADD IY, IY
                    dis = String.Format("ADD IY, IY");
                    break;

                    case 0x39:  //ADD IY, SP
                    dis = String.Format("ADD IY, SP");
                    break;

                    case 0x84:  //ADD A, IYH
                    dis = String.Format("ADD A, IYH");
                    break;

                    case 0x85:  //ADD A, IYL
                    dis = String.Format("ADD A, IYL");
                    break;

                    case 0x86:  //Add A, (IY+d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("ADD A, (IY + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x8C:  //ADC A, IYH
                    dis = String.Format("ADC A, IYH");
                    break;

                    case 0x8D:  //ADC A, IYL
                    dis = String.Format("ADC A, IYL");
                    break;

                    case 0x8E: //ADC A, (IY+d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("ADC A, (IY + ${0:x})", disp);
                    PC++;
                    break;
                    #endregion

                    #region Subtraction instructions
                    case 0x94:  //SUB A, IYH
                    dis = String.Format("SUB A, IYH");
                    break;

                    case 0x95:  //SUB A, IYL
                    dis = String.Format("SUB A, IYL");
                    break;

                    case 0x96:  //SUB (IY + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("SUB (IY + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x9C:  //SBC A, IYH
                    dis = String.Format("SBC A, IYH");
                    break;

                    case 0x9D:  //SBC A, IYL
                    dis = String.Format("SBC A, IYL");
                    break;

                    case 0x9E:  //SBC A, (IY + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("SBC A, (IY + ${0:x})", disp);
                    PC++;
                    break;
                    #endregion

                    #region Increment/Decrements
                    case 0x23:  //INC IY
                    dis = String.Format("INC IY");
                    break;

                    case 0x24:  //INC IYH
                    dis = String.Format("INC IYH");
                    break;

                    case 0x25:  //DEC IYH
                    dis = String.Format("DEC IYH");
                    break;

                    case 0x2B:  //DEC IY
                    dis = String.Format("DEC IY");
                    break;

                    case 0x2C:  //INC IYL
                    dis = String.Format("INC IYL");
                    break;

                    case 0x2D:  //DEC IYL
                    dis = String.Format("DEC IYL");
                    break;

                    case 0x34:  //INC (IY + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("INC (IY + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x35:  //DEC (IY + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("DEC (IY + ${0:x})", disp);
                    PC++;
                    break;
                    #endregion

                    #region Bitwise operators

                    case 0xA4:  //AND IYH
                    dis = String.Format("AND IYH");
                    break;

                    case 0xA5:  //AND IYL
                    dis = String.Format("AND IYL");
                    break;

                    case 0xA6:  //AND (IY + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("AND (IY + ${0:x})", disp);
                    PC++;
                    break;

                    case 0xAC:  //XOR IYH
                    dis = String.Format("XOR IYH");
                    break;

                    case 0xAD:  //XOR IYL
                    dis = String.Format("XOR IYL");
                    break;

                    case 0xAE:  //XOR (IY + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("XOR (IY + ${0:x})", disp);
                    PC++;
                    break;

                    case 0xB4:  //OR IYH
                    dis = String.Format("OR IYH");
                    break;

                    case 0xB5:  //OR IYL
                    dis = String.Format("OR IYL");
                    break;

                    case 0xB6:  //OR (IY + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("OR (IY + ${0:x})", disp);
                    PC++;
                    break;
                    #endregion

                    #region Compare operator
                    case 0xBC:  //CP IYH
                    dis = String.Format("CP IYH");
                    break;

                    case 0xBD:  //CP IYL
                    dis = String.Format("CP IYL");
                    break;

                    case 0xBE:  //CP (IY + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("CP (IY + ${0:x})", disp & 0xff);
                    PC++;
                    break;
                    #endregion

                    #region Load instructions
                    case 0x21:  //LD IY, nn
                    dis = String.Format("LD IY, ${0:x}", PeekWordNoContend(PC));
                    PC += 2;
                    break;

                    case 0x22:  //LD (nn), IY
                    dis = String.Format("LD (${0:x}), IY", PeekWordNoContend(PC));
                    PC += 2;
                    break;

                    case 0x26:  //LD IYH, n
                    dis = String.Format("LD IYH, ${0:x}", PeekByteNoContend(PC));
                    PC++;
                    break;

                    case 0x2A:  //LD IY, (nn)
                    dis = String.Format("LD IY, (${0:x})", PeekWordNoContend(PC));
                    PC += 2;
                    break;

                    case 0x2E:  //LD IYL, n
                    dis = String.Format("LD IYL, ${0:x}", PeekByteNoContend(PC));
                    PC++;
                    break;

                    case 0x36:  //LD (IY + d), n
                    disp = GetDisplacement(PeekByteNoContend(PC));

                    dis = String.Format("LD (IY + ${0:x}), {1:x}", disp, PeekByteNoContend((ushort)(PC + 1)));
                    PC += 2;
                    break;

                    case 0x44:  //LD B, IYH
                    dis = String.Format("LD B, IYH");
                    break;

                    case 0x45:  //LD B, IYL
                    dis = String.Format("LD B, IYL");
                    break;

                    case 0x46:  //LD B, (IY + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD B, (IY + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x4C:  //LD C, IYH
                    dis = String.Format("LD C, IYH");
                    break;

                    case 0x4D:  //LD C, IYL
                    dis = String.Format("LD C, IYL");
                    break;

                    case 0x4E:  //LD C, (IY + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD C, (IY + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x54:  //LD D, IYH
                    dis = String.Format("LD D, IYH");
                    break;

                    case 0x55:  //LD D, IYL
                    dis = String.Format("LD D, IYL");
                    break;

                    case 0x56:  //LD D, (IY + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD D, (IY + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x5C:  //LD E, IYH
                    dis = String.Format("LD E, IYH");
                    break;

                    case 0x5D:  //LD E, IYL
                    dis = String.Format("LD E, IYL");
                    break;

                    case 0x5E:  //LD E, (IY + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD E, (IY + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x60:  //LD IYH, B
                    dis = String.Format("LD IYH, B");
                    break;

                    case 0x61:  //LD IYH, C
                    dis = String.Format("LD IYH, C");
                    break;

                    case 0x62:  //LD IYH, D
                    dis = String.Format("LD IYH, D");
                    break;

                    case 0x63:  //LD IYH, E
                    dis = String.Format("LD IYH, E");
                    break;

                    case 0x64:  //LD IYH, IYH
                    dis = String.Format("LD IYH, IYH");
                    break;

                    case 0x65:  //LD IYH, IYL
                    dis = String.Format("LD IYH, IYL");
                    break;

                    case 0x66:  //LD H, (IY + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD H, (IY + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x67:  //LD IYH, A
                    dis = String.Format("LD IYH, A");
                    break;

                    case 0x68:  //LD IYL, B
                    dis = String.Format("LD IYL, B");
                    break;

                    case 0x69:  //LD IYL, C
                    dis = String.Format("LD IYL, C");
                    break;

                    case 0x6A:  //LD IYL, D
                    dis = String.Format("LD IYL, D");
                    break;

                    case 0x6B:  //LD IYL, E
                    dis = String.Format("LD IYL, E");
                    break;

                    case 0x6C:  //LD IYL, IYH
                    dis = String.Format("LD IYL, IYH");
                    break;

                    case 0x6D:  //LD IYL, IYL
                    dis = String.Format("LD IYL, IYL");
                    break;

                    case 0x6E:  //LD L, (IY + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD L, (IY + ${0:x})", disp);
                    PC++;
                    break;

                    case 0x6F:  //LD IYL, A
                    dis = String.Format("LD IYL, A");
                    break;

                    case 0x70:  //LD (IY + d), B
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD (IY + ${0:x}), B", disp);
                    PC++;
                    break;

                    case 0x71:  //LD (IY + d), C
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD (IY + ${0:x}), C", disp);
                    PC++;
                    break;

                    case 0x72:  //LD (IY + d), D
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD (IY + ${0:x}), D", disp);
                    PC++;
                    break;

                    case 0x73:  //LD (IY + d), E
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD (IY + ${0:x}), E", disp);
                    PC++;
                    break;

                    case 0x74:  //LD (IY + d), H
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD (IY + ${0:x}), H", disp);
                    PC++;
                    break;

                    case 0x75:  //LD (IY + d), L
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD (IY + ${0:x}), L", disp);
                    PC++;
                    break;

                    case 0x77:  //LD (IY + d), A
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD (IY + ${0:x}), A", disp);
                    PC++;
                    break;

                    case 0x7C:  //LD A, IYH
                    dis = String.Format("LD A, IYH");
                    break;

                    case 0x7D:  //LD A, IYL
                    dis = String.Format("LD A, IYL");
                    break;

                    case 0x7E:  //LD A, (IY + d)
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    dis = String.Format("LD A, (IY + ${0:x})", disp);
                    PC++;
                    break;

                    case 0xF9:  //LD SP, IY
                    dis = String.Format("LD SP, IY");
                    break;
                    #endregion

                    #region All FDCB instructions
                    case 0xCB:
                    disp = GetDisplacement(PeekByteNoContend(PC));
                    PC++;
                    opcode = PeekByteNoContend(PC);      //The opcode comes after the offset byte!
                    PC++;
                    switch (opcode) {
                        case 0x00: //LD B, RLC (IY+d)
                        dis = String.Format("LD B, RLC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x01: //LD C, RLC (IY+d)
                        dis = String.Format("LD C, RLC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x02: //LD D, RLC (IY+d)
                        dis = String.Format("LD D, RLC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x03: //LD E, RLC (IY+d)
                        dis = String.Format("LD E, RLC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x04: //LD H, RLC (IY+d)
                        dis = String.Format("LD H, RLC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x05: //LD L, RLC (IY+d)
                        dis = String.Format("LD L, RLC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x06:  //RLC (IY + d)
                        dis = String.Format("RLC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x07: //LD A, RLC (IY+d)
                        dis = String.Format("LD A, RLC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x08: //LD B, RRC (IY+d)
                        dis = String.Format("LD B, RRC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x09: //LD C, RRC (IY+d)
                        dis = String.Format("LD C, RRC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x0A: //LD D, RRC (IY+d)
                        dis = String.Format("LD D, RRC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x0B: //LD E, RRC (IY+d)
                        dis = String.Format("LD E, RRC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x0C: //LD H, RRC (IY+d)
                        dis = String.Format("LD H, RRC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x0D: //LD L, RRC (IY+d)
                        dis = String.Format("LD L, RRC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x0E:  //RRC (IY + d)
                        dis = String.Format("RRC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x0F: //LD A, RRC (IY+d)
                        dis = String.Format("LD A, RRC (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x10: //LD B, RL (IY+d)
                        dis = String.Format("LD B, RL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x11: //LD C, RL (IY+d)
                        dis = String.Format("LD C, RL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x12: //LD D, RL (IY+d)
                        dis = String.Format("LD D, RL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x13: //LD E, RL (IY+d)
                        dis = String.Format("LD E, RL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x14: //LD H, RL (IY+d)
                        dis = String.Format("LD H, RL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x15: //LD L, RL (IY+d)
                        dis = String.Format("LD L, RL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x16:  //RL (IY + d)
                        dis = String.Format("RL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x17: //LD A, RL (IY+d)
                        dis = String.Format("LD A, RL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x18: //LD B, RR (IY+d)
                        dis = String.Format("LD B, RR (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x19: //LD C, RR (IY+d)
                        dis = String.Format("LD C, RR (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x1A: //LD D, RR (IY+d)
                        dis = String.Format("LD D, RR (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x1B: //LD E, RR (IY+d)
                        dis = String.Format("LD E, RR (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x1C: //LD H, RR (IY+d)
                        dis = String.Format("LD H, RR (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x1D: //LD L, RRC (IY+d)
                        dis = String.Format("LD L, RR (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x1E:  //RR (IY + d)
                        dis = String.Format("RR (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x1F: //LD A, RRC (IY+d)
                        dis = String.Format("LD A, RR (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x20: //LD B, SLA (IY+d)
                        dis = String.Format("LD B, SLA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x21: //LD C, SLA (IY+d)
                        dis = String.Format("LD C, SLA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x22: //LD D, SLA (IY+d)
                        dis = String.Format("LD D, SLA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x23: //LD E, SLA (IY+d)
                        dis = String.Format("LD E, SLA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x24: //LD H, SLA (IY+d)
                        dis = String.Format("LD H, SLA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x25: //LD L, SLA (IY+d)
                        dis = String.Format("LD L, SLA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x26:  //SLA (IY + d)
                        dis = String.Format("SLA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x27: //LD A, SLA (IY+d)
                        dis = String.Format("LD A, SLA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x28: //LD B, SRA (IY+d)
                        dis = String.Format("LD B, SRA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x29: //LD C, SRA (IY+d)
                        dis = String.Format("LD C, SRA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x2A: //LD D, SRA (IY+d)
                        dis = String.Format("LD D, SRA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x2B: //LD E, SRA (IY+d)
                        dis = String.Format("LD E, SRA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x2C: //LD H, SRA (IY+d)
                        dis = String.Format("LD H, SRA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x2D: //LD L, SRA (IY+d)
                        dis = String.Format("LD L, SRA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x2E:  //SRA (IY + d)
                        dis = String.Format("SRA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x2F: //LD A, SRA (IY+d)
                        dis = String.Format("LD A, SRA (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x30: //LD B, SLL (IY+d)
                        dis = String.Format("LD B, SLL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x31: //LD C, SLL (IY+d)
                        dis = String.Format("LD C, SLL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x32: //LD D, SLL (IY+d)
                        dis = String.Format("LD D, SLL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x33: //LD E, SLL (IY+d)
                        dis = String.Format("LD E, SLL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x34: //LD H, SLL (IY+d)
                        dis = String.Format("LD H, SLL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x35: //LD L, SLL (IY+d)
                        dis = String.Format("LD L, SLL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x36:  //SLL (IY + d)
                        dis = String.Format("SLL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x37: //LD A, SLL (IY+d)
                        dis = String.Format("LD A, SLL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x38: //LD B, SRL (IY+d)
                        dis = String.Format("LD B, SRL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x39: //LD C, SRL (IY+d)
                        dis = String.Format("LD C, SRL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x3A: //LD D, SRL (IY+d)
                        dis = String.Format("LD D, SRL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x3B: //LD E, SRL (IY+d)
                        dis = String.Format("LD E, SRL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x3C: //LD H, SRL (IY+d)
                        dis = String.Format("LD H, SRL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x3D: //LD L, SRL (IY+d)
                        dis = String.Format("LD L, SRL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x3E:  //SRL (IY + d)
                        dis = String.Format("SRL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x3F: //LD A, SRL (IY+d)
                        dis = String.Format("LD A, SRL (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x40:  //BIT 0, (IY + d)
                        dis = String.Format("BIT 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x41:  //BIT 0, (IY + d)
                        dis = String.Format("BIT 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x42:  //BIT 0, (IY + d)
                        dis = String.Format("BIT 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x43:  //BIT 0, (IY + d)
                        dis = String.Format("BIT 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x44:  //BIT 0, (IY + d)
                        dis = String.Format("BIT 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x45:  //BIT 0, (IY + d)
                        dis = String.Format("BIT 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x46:  //BIT 0, (IY + d)
                        dis = String.Format("BIT 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x47:  //BIT 0, (IY + d)
                        dis = String.Format("BIT 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x48:  //BIT 1, (IY + d)
                        dis = String.Format("BIT 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x49:  //BIT 1, (IY + d)
                        dis = String.Format("BIT 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x4A:  //BIT 1, (IY + d)
                        dis = String.Format("BIT 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x4B:  //BIT 1, (IY + d)
                        dis = String.Format("BIT 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x4C:  //BIT 1, (IY + d)
                        dis = String.Format("BIT 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x4D:  //BIT 1, (IY + d)
                        dis = String.Format("BIT 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x4E:  //BIT 1, (IY + d)
                        dis = String.Format("BIT 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x4F:  //BIT 1, (IY + d)
                        dis = String.Format("BIT 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x50:  //BIT 2, (IY + d)
                        dis = String.Format("BIT 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x51:  //BIT 2, (IY + d)
                        dis = String.Format("BIT 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x52:  //BIT 2, (IY + d)
                        dis = String.Format("BIT 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x53:  //BIT 2, (IY + d)
                        dis = String.Format("BIT 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x54:  //BIT 2, (IY + d)
                        dis = String.Format("BIT 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x55:  //BIT 2, (IY + d)
                        dis = String.Format("BIT 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x56:  //BIT 2, (IY + d)
                        dis = String.Format("BIT 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x57:  //BIT 2, (IY + d)
                        dis = String.Format("BIT 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x58:  //BIT 3, (IY + d)
                        dis = String.Format("BIT 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x59:  //BIT 3, (IY + d)
                        dis = String.Format("BIT 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x5A:  //BIT 3, (IY + d)
                        dis = String.Format("BIT 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x5B:  //BIT 3, (IY + d)
                        dis = String.Format("BIT 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x5C:  //BIT 3, (IY + d)
                        dis = String.Format("BIT 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x5D:  //BIT 3, (IY + d)
                        dis = String.Format("BIT 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x5E:  //BIT 3, (IY + d)
                        dis = String.Format("BIT 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x5F:  //BIT 3, (IY + d)
                        dis = String.Format("BIT 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x60:  //BIT 4, (IY + d)
                        dis = String.Format("BIT 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x61:  //BIT 4, (IY + d)
                        dis = String.Format("BIT 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x62:  //BIT 4, (IY + d)
                        dis = String.Format("BIT 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x63:  //BIT 4, (IY + d)
                        dis = String.Format("BIT 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x64:  //BIT 4, (IY + d)
                        dis = String.Format("BIT 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x65:  //BIT 4, (IY + d)
                        dis = String.Format("BIT 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x66:  //BIT 4, (IY + d)
                        dis = String.Format("BIT 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x67:  //BIT 4, (IY + d)
                        dis = String.Format("BIT 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x68:  //BIT 5, (IY + d)
                        dis = String.Format("BIT 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x69:  //BIT 5, (IY + d)
                        dis = String.Format("BIT 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x6A:  //BIT 5, (IY + d)
                        dis = String.Format("BIT 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x6B:  //BIT 5, (IY + d)
                        dis = String.Format("BIT 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x6C:  //BIT 5, (IY + d)
                        dis = String.Format("BIT 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x6D:  //BIT 5, (IY + d)
                        dis = String.Format("BIT 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x6E:  //BIT 5, (IY + d)
                        dis = String.Format("BIT 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x6F:  //BIT 5, (IY + d)
                        dis = String.Format("BIT 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x70:  //BIT 6, (IY + d)
                        dis = String.Format("BIT 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x71:  //BIT 6, (IY + d)
                        dis = String.Format("BIT 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x72:  //BIT 6, (IY + d)
                        dis = String.Format("BIT 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x73:  //BIT 6, (IY + d)
                        dis = String.Format("BIT 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x74:  //BIT 6, (IY + d)
                        dis = String.Format("BIT 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x75:  //BIT 6, (IY + d)
                        dis = String.Format("BIT 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x76:  //BIT 6, (IY + d)
                        dis = String.Format("BIT 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x77:  //BIT 6, (IY + d)
                        dis = String.Format("BIT 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x78:  //BIT 7, (IY + d)
                        dis = String.Format("BIT 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x79:  //BIT 7, (IY + d)
                        dis = String.Format("BIT 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x7A:  //BIT 7, (IY + d)
                        dis = String.Format("BIT 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x7B:  //BIT 7, (IY + d)
                        dis = String.Format("BIT 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x7C:  //BIT 7, (IY + d)
                        dis = String.Format("BIT 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x7D:  //BIT 7, (IY + d)
                        dis = String.Format("BIT 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x7E:  //BIT 7, (IY + d)
                        dis = String.Format("BIT 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x7F:  //BIT 7, (IY + d)
                        dis = String.Format("BIT 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x80: //LD B, RES 0, (IY+d)
                        dis = String.Format("LD B, RES 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x81: //LD C, RES 0, (IY+d)
                        dis = String.Format("LD C, RES 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x82: //LD D, RES 0, (IY+d)
                        dis = String.Format("LD D, RES 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x83: //LD E, RES 0, (IY+d)
                        dis = String.Format("LD E, RES 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x84: //LD H, RES 0, (IY+d)
                        dis = String.Format("LD H, RES 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x85: //LD L, RES 0, (IY+d)
                        dis = String.Format("LD L, RES 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x86:  //RES 0, (IY + d)
                        dis = String.Format("RES 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x87: //LD A, RES 0, (IY+d)
                        dis = String.Format("LD A, RES 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x88: //LD B, RES 1, (IY+d)
                        dis = String.Format("LD B, RES 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x89: //LD C, RES 1, (IY+d)
                        dis = String.Format("LD C, RES 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x8A: //LD D, RES 1, (IY+d)
                        dis = String.Format("LD D, RES 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x8B: //LD E, RES 1, (IY+d)
                        dis = String.Format("LD E, RES 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x8C: //LD H, RES 1, (IY+d)
                        dis = String.Format("LD H, RES 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x8D: //LD L, RES 1, (IY+d)
                        dis = String.Format("LD L, RES 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x8E:  //RES 1, (IY + d)
                        dis = String.Format("RES 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x8F: //LD A, RES 1, (IY+d)
                        dis = String.Format("LD A, RES 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x90: //LD B, RES 2, (IY+d)
                        dis = String.Format("LD B, RES 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x91: //LD C, RES 2, (IY+d)
                        dis = String.Format("LD C, RES 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x92: //LD D, RES 2, (IY+d)
                        dis = String.Format("LD D, RES 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x93: //LD E, RES 2, (IY+d)
                        dis = String.Format("LD E, RES 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x94: //LD H, RES 2, (IY+d)
                        dis = String.Format("LD H, RES 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x95: //LD L, RES 2, (IY+d)
                        dis = String.Format("LD L, RES 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x96:  //RES 2, (IY + d)
                        dis = String.Format("RES 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x97: //LD A, RES 2, (IY+d)
                        dis = String.Format("LD A, RES 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x98: //LD B, RES 3, (IY+d)
                        dis = String.Format("LD B, RES 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x99: //LD C, RES 3, (IY+d)
                        dis = String.Format("LD C, RES 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x9A: //LD D, RES 3, (IY+d)
                        dis = String.Format("LD D, RES 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x9B: //LD E, RES 3, (IY+d)
                        dis = String.Format("LD E, RES 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x9C: //LD H, RES 3, (IY+d)
                        dis = String.Format("LD H, RES 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x9D: //LD L, RES 3, (IY+d)
                        dis = String.Format("LD L, RES 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x9E:  //RES 3, (IY + d)
                        dis = String.Format("RES 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0x9F: //LD A, RES 3, (IY+d)
                        dis = String.Format("LD A, RES 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xA0: //LD B, RES 4, (IY+d)
                        dis = String.Format("LD B, RES 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xA1: //LD C, RES 4, (IY+d)
                        dis = String.Format("LD C, RES 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xA2: //LD D, RES 4, (IY+d)
                        dis = String.Format("LD D, RES 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xA3: //LD E, RES 4, (IY+d)
                        dis = String.Format("LD E, RES 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xA4: //LD H, RES 4, (IY+d)
                        dis = String.Format("LD H, RES 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xA5: //LD L, RES 4, (IY+d)
                        dis = String.Format("LD L, RES 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xA6:  //RES 4, (IY + d)
                        dis = String.Format("RES 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xA7: //LD A, RES 4, (IY+d)
                        dis = String.Format("LD A, RES 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xA8: //LD B, RES 5, (IY+d)
                        dis = String.Format("LD B, RES 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xA9: //LD C, RES 5, (IY+d)
                        dis = String.Format("LD C, RES 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xAA: //LD D, RES 5, (IY+d)
                        dis = String.Format("LD D, RES 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xAB: //LD E, RES 5, (IY+d)
                        dis = String.Format("LD E, RES 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xAC: //LD H, RES 5, (IY+d)
                        dis = String.Format("LD H, RES 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xAD: //LD L, RES 5, (IY+d)
                        dis = String.Format("LD L, RES 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xAE:  //RES 5, (IY + d)
                        dis = String.Format("RES 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xAF: //LD A, RES 5, (IY+d)
                        dis = String.Format("LD A, RES 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xB0: //LD B, RES 6, (IY+d)
                        dis = String.Format("LD B, RES 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xB1: //LD C, RES 6, (IY+d)
                        dis = String.Format("LD C, RES 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xB2: //LD D, RES 6, (IY+d)
                        dis = String.Format("LD D, RES 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xB3: //LD E, RES 6, (IY+d)
                        dis = String.Format("LD E, RES 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xB4: //LD H, RES 5, (IY+d)
                        dis = String.Format("LD H, RES 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xB5: //LD L, RES 5, (IY+d)
                        dis = String.Format("LD L, RES 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xB6:  //RES 6, (IY + d)
                        dis = String.Format("RES 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xB7: //LD A, RES 5, (IY+d)
                        dis = String.Format("LD A, RES 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xB8: //LD B, RES 7, (IY+d)
                        dis = String.Format("LD B, RES 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xB9: //LD C, RES 7, (IY+d)
                        dis = String.Format("LD C, RES 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xBA: //LD D, RES 7, (IY+d)
                        dis = String.Format("LD D, RES 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xBB: //LD E, RES 7, (IY+d)
                        dis = String.Format("LD E, RES 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xBC: //LD H, RES 7, (IY+d)
                        dis = String.Format("LD H, RES 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xBD: //LD L, RES 7, (IY+d)
                        dis = String.Format("LD L, RES 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xBE:  //RES 7, (IY + d)
                        dis = String.Format("RES 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xBF: //LD A, RES 7, (IY+d)
                        dis = String.Format("LD A, RES 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xC0: //LD B, SET 0, (IY+d)
                        dis = String.Format("LD B, SET 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xC1: //LD C, SET 0, (IY+d)
                        dis = String.Format("LD C, SET 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xC2: //LD D, SET 0, (IY+d)
                        dis = String.Format("LD D, SET 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xC3: //LD E, SET 0, (IY+d)
                        dis = String.Format("LD E, SET 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xC4: //LD H, SET 0, (IY+d)
                        dis = String.Format("LD H, SET 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xC5: //LD L, SET 0, (IY+d)
                        dis = String.Format("LD L, SET 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xC6:  //SET 0, (IY + d)
                        dis = String.Format("SET 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xC7: //LD A, SET 0, (IY+d)
                        dis = String.Format("LD A, SET 0, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xC8: //LD B, SET 1, (IY+d)
                        dis = String.Format("LD B, SET 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xC9: //LD C, SET 0, (IY+d)
                        dis = String.Format("LD C, SET 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xCA: //LD D, SET 1, (IY+d)
                        dis = String.Format("LD D, SET 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xCB: //LD E, SET 1, (IY+d)
                        dis = String.Format("LD E, SET 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xCC: //LD H, SET 1, (IY+d)
                        dis = String.Format("LD H, SET 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xCD: //LD L, SET 1, (IY+d)
                        dis = String.Format("LD L, SET 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xCE:  //SET 1, (IY + d)
                        dis = String.Format("SET 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xCF: //LD A, SET 1, (IY+d)
                        dis = String.Format("LD A, SET 1, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xD0: //LD B, SET 2, (IY+d)
                        dis = String.Format("LD B, SET 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xD1: //LD C, SET 2, (IY+d)
                        dis = String.Format("LD C, SET 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xD2: //LD D, SET 2, (IY+d)
                        dis = String.Format("LD D, SET 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xD3: //LD E, SET 2, (IY+d)
                        dis = String.Format("LD E, SET 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xD4: //LD H, SET 21, (IY+d)
                        dis = String.Format("LD H, SET 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xD5: //LD L, SET 2, (IY+d)
                        dis = String.Format("LD L, SET 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xD6:  //SET 2, (IY + d)
                        dis = String.Format("SET 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xD7: //LD A, SET 2, (IY+d)
                        dis = String.Format("LD A, SET 2, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xD8: //LD B, SET 3, (IY+d)
                        dis = String.Format("LD B, SET 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xD9: //LD C, SET 3, (IY+d)
                        dis = String.Format("LD C, SET 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xDA: //LD D, SET 3, (IY+d)
                        dis = String.Format("LD D, SET 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xDB: //LD E, SET 3, (IY+d)
                        dis = String.Format("LD E, SET 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xDC: //LD H, SET 21, (IY+d)
                        dis = String.Format("LD H, SET 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xDD: //LD L, SET 3, (IY+d)
                        dis = String.Format("LD L, SET 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xDE:  //SET 3, (IY + d)
                        dis = String.Format("SET 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xDF: //LD A, SET 3, (IY+d)
                        dis = String.Format("LD A, SET 3, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xE0: //LD B, SET 4, (IY+d)
                        dis = String.Format("LD B, SET 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xE1: //LD C, SET 4, (IY+d)
                        dis = String.Format("LD C, SET 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xE2: //LD D, SET 4, (IY+d)
                        dis = String.Format("LD D, SET 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xE3: //LD E, SET 4, (IY+d)
                        dis = String.Format("LD E, SET 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xE4: //LD H, SET 4, (IY+d)
                        dis = String.Format("LD H, SET 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xE5: //LD L, SET 3, (IY+d)
                        dis = String.Format("LD L, SET 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xE6:  //SET 4, (IY + d)
                        dis = String.Format("SET 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xE7: //LD A, SET 4, (IY+d)
                        dis = String.Format("LD A, SET 4, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xE8: //LD B, SET 5, (IY+d)
                        dis = String.Format("LD B, SET 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xE9: //LD C, SET 5, (IY+d)
                        dis = String.Format("LD C, SET 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xEA: //LD D, SET 5, (IY+d)
                        dis = String.Format("LD D, SET 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xEB: //LD E, SET 5, (IY+d)
                        dis = String.Format("LD E, SET 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xEC: //LD H, SET 5, (IY+d)
                        dis = String.Format("LD H, SET 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xED: //LD L, SET 5, (IY+d)
                        dis = String.Format("LD L, SET 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xEE:  //SET 5, (IY + d)
                        dis = String.Format("SET 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xEF: //LD A, SET 5, (IY+d)
                        dis = String.Format("LD A, SET 5, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xF0: //LD B, SET 6, (IY+d)
                        dis = String.Format("LD B, SET 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xF1: //LD C, SET 6, (IY+d)
                        dis = String.Format("LD C, SET 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xF2: //LD D, SET 6, (IY+d)
                        dis = String.Format("LD D, SET 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xF3: //LD E, SET 6, (IY+d)
                        dis = String.Format("LD E, SET 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xF4: //LD H, SET 6, (IY+d)
                        dis = String.Format("LD H, SET 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xF5: //LD L, SET 6, (IY+d)
                        dis = String.Format("LD L, SET 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xF6:  //SET 6, (IY + d)
                        dis = String.Format("SET 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xF7: //LD A, SET 6, (IY+d)
                        dis = String.Format("LD A, SET 6, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xF8: //LD B, SET 7, (IY+d)
                        dis = String.Format("LD B, SET 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xF9: //LD C, SET 7, (IY+d)
                        dis = String.Format("LD C, SET 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xFA: //LD D, SET 7, (IY+d)
                        dis = String.Format("LD D, SET 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xFB: //LD E, SET 7, (IY+d)
                        dis = String.Format("LD E, SET 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xFC: //LD H, SET 7, (IY+d)
                        dis = String.Format("LD H, SET 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xFD: //LD L, SET 7, (IY+d)
                        dis = String.Format("LD L, SET 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xFE:  //SET 7, (IY + d)
                        dis = String.Format("SET 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        case 0xFF: //LD A, SET 7, (IY + D)
                        dis = String.Format("LD A, SET 7, (IY + ${0:x})", disp & 0xff);
                        break;

                        default:
                        dis = "Unknown FDCB opcode: " + opcode.ToString();
                        break;
                    }
                    break;
                    #endregion

                    #region Pop/Push instructions
                    case 0xE1:  //POP IY
                    dis = String.Format("POP IY");
                    break;

                    case 0xE5:  //PUSH IY
                    dis = String.Format("PUSH IY");
                    break;
                    #endregion

                    #region Exchange instruction
                    case 0xE3:  //EX (SP), IY
                    dis = String.Format("EX (SP), IY");
                    break;
                    #endregion

                    #region Jump instruction
                    case 0xE9:  //JP (IY)
                    dis = String.Format("JP (IY)");
                    break;
                    #endregion
                    default:
                    //According to Sean's doc: http://z80.info/z80sean.txt
                    //If a DDxx or FDxx instruction is not listed, it should operate as
                    //without the DD or FD prefix, and the DD or FD prefix itself should
                    //operate as a NOP.
                    jumpForUndoc = true;
                    break;
                }
                break;
                #endregion
            }
            if (jumpForUndoc)
                goto jmp4Undoc;
            return dis;
        }
        public void SetEmulationSpeed(int speed) {
            /* if (speed == 0) {
                speed = MAX_CPU_SPEED;
                soundTStatesToSample = 79;
                beeper.SetVolume(2.0f);
            } else {
                beeper.SetVolume(soundVolume);
                soundTStatesToSample = (int)((FrameLength * (50.0f * (speed / 100.0f))) / 44100.0f);
            }

            emulationSpeed = (speed - 100) / 100; //0 = normal.*/
            emulationSpeed = speed;
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

        public void StartPlaybackRZX(RZXFile _rzx) {
            rzx = _rzx;
            isRecordingRZX = false;
            isPlayingRZX = rzx.UpdatePlayback();   
            if (RZXPlaybackStartEvent != null) {
                RZXPlaybackStartEvent(this);
            }
        }

        public void StopPlaybackRZX() {
            isPlayingRZX = false;
            rzx.Close();
            rzx = null;
        }

#if !NEW_RZX_METHODS
        public void PlaybackRZX(RZXFile _rzx) {
            isRecordingRZX = false;
            rzx = _rzx;
            rzx.InitPlayback();
            isPlayingRZX = true;
            cpu.t_states = (int)rzx.record.tstatesAtStart;
        }
        
        public void ContinueRecordingRZX(RZXFile _rzx) {
            isRecordingRZX = true;
            isPlayingRZX = false;
            rzx = _rzx;
            rzx.ContinueRecording();
            rzxInputs = new System.Collections.Generic.List<byte>();
        }
#endif    
        public bool ContinueRZXSession(string filename) {
            isRecordingRZX = true;
            isPlayingRZX = false;
            return rzx.ContinueRecording(filename);
        }

        public void InsertBookmark() {
            if (isRecordingRZX) {
                //rzx.InsertBookmark(CreateSZX(), rzxInputs);
                rzx.UpdateRecording(cpu.t_states);
                rzx.Bookmark(CreateSZX());
            }
        }

        public void RollbackRZX() {

            //SZXFile snapshot = rzx.Rollback();
            rzx.UpdateRecording(cpu.t_states);
            rzx.Rollback();
            //if (snapshot != null)
            //    UseSZX(snapshot);
        }

        public void DiscardRZX() {
            isRecordingRZX = false;
            isPlayingRZX = false;
            //rzx.Discard();
        }

        public void SaveRZX(bool doFinalise) {

            if (isRecordingRZX) {
                isPlayingRZX = false;
                isRecordingRZX = false;
                rzx.UpdateRecording(cpu.t_states);
#if NEW_RZX_METHODS
                rzx.SaveSession(CreateSZX().GetSZXData(), doFinalise);

                rzx.Close();
#else
                //rzx.Save(filename, (doFinalise ? null : CreateSZX().GetSZXData()));
#endif
            }
        }

        public void StartRecordingRZX(string filename, System.Action<RZXFileEventArgs> callback) {
            rzx = new RZXFile();
#if NEW_RZX_METHODS
            rzx.RZXFileEventHandler += callback;
            rzx.Record(filename);
            rzx.AddSnapshot(CreateSZX().GetSZXData());
#else
            //rzx.StartRecording(CreateSZX().GetSZXData(), cpu.t_states);
#endif
            isPlayingRZX = false;
            isRecordingRZX = true;

            //rzx.record.tstatesAtStart = (uint)cpu.t_states;
            //rzx.record.flags |= 0x2; //Frames are compressed.
            //rzx.snapshotData[0] = CreateSZX().GetSZXData();

        }

        public bool IsValidSessionRZX() {
            //if (!rzx.IsValidSession())
            //    return false;

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

        public void SetCPUSpeed(int multiple) {
            cpuMultiplier = multiple;
            beeper.SetVolume(soundVolume);
        }

        protected void InitCpu() {
            cpu = new Z80();
            cpu.PeekByte =  new ReadByteCallback(PeekByte);
            cpu.PokeByte = new WriteByteCallback(PokeByte);
            cpu.PeekWord = new ReadWordCallback(PeekWord);
            cpu.PokeWord = new WriteWordCallback(PokeWord);
            cpu.In = new InCallback(In);
            cpu.Out = new OutCallback(Out);
            cpu.Contend = new ContendCallback(Contend);
            cpu.InstructionFetchSignal = new InstructionFetchCallback(() => {
                if (isPlayingRZX || isRecordingRZX) {
                    rzx.fetchCount++;
                };
            }) ;
            cpu.TapeEdgeDetection = new TapeEdgeDetectionCallback(OnTapeEdgeDetection);
            cpu.TapeEdgeDecA = new TapeEdgeDecACallback(OnTapeEdgeDecA);
            cpu.TapeEdgeCpA = new TapeEdgeCpACallback(OnTapeEdgeCpA);
        }

        public zx_spectrum(IntPtr handle, bool lateTimingModel) {
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

            //Initialize the lambdas.
            GetPageData = page => RAMpage[page * 2];
            PeekByteNoContend = addr => PageReadPointer[addr >> 13][addr & 0x1FFF];
            PeekWordNoContend = addr => (ushort)((PeekByteNoContend(addr) | ((PeekByteNoContend((ushort)(addr + 1)) << 8))));
            PeekWord = addr => (ushort)((PeekByte(addr)) | (PeekByte((ushort)(addr + 1)) << 8));
            SetPalette = newPalette => AttrColors = newPalette;

            InitCpu();

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
        }

        protected void FlashLoadTape() {
            if (!tape_flashLoad)
                return;

            //if (TapeEvent != null)
            //    OnTapeEvent(new TapeEventArgs(TapeEventType.FLASH_LOAD));
            DoTapeEvent(new TapeEventArgs(TapeEventType.FLASH_LOAD));
        }

        
        //public byte[] GetPageData(int page) {
        //    return RAMpage[page * 2];
        //}

        public void AddDevice(SpeccyDevice newDevice) {
            RemoveDevice(newDevice.DeviceID);
            attached_devices[(int)newDevice.DeviceID] = newDevice;
            newDevice.RegisterDevice(this);
        }

        public void RemoveDevice(SPECCY_DEVICE deviceId) {
            SpeccyDevice dev;
            if (attached_devices.TryGetValue((int)deviceId, out dev)) {
                dev.UnregisterDevice(this);
            }
        }

        //Resets the speccy
        public virtual void Reset(bool hardReset) {
            isResetOver = false;
            isPlayingRZX = false;
            isRecordingRZX = false;

            DoTapeEvent(new TapeEventArgs(TapeEventType.STOP_TAPE));
           
            //All registers are set to 0xffff during a cold boot
            //http://worldofspectrum.org/forums/showthread.php?t=34574&page=3
            if (hardReset)
            {
                cpu.HardReset();
            }
            else {
                cpu.UserReset();
            }
            cpu.is_halted = false;
            tapeBitWasFlipped = false;
            cpu.t_states = 0;
            timeToOutSound = 0;

            ULAByteCtr = 0;
            last1ffdOut = 0;
            last7ffdOut = 0;
            lastFEOut = 0;
            lastTState = 0;
            elapsedTStates = 0;
            flashFrameCount = 0;

            pulseCounter = 0;
            repeatCount = 0;
            bitCounter = 0;
            dataCounter = 0;
            dataByte = 0;
            currentBit = 0;
            currentBlock = null;
            isPauseBlockPreproccess = false;

            if (ula_plus != null) {
                ula_plus.Reset();
            }
            foreach (var d in io_devices) {
                d.Reset();
            }
            timeToOutSound = 0;
            soundCounter = 0;
            averagedSound = 0;
            flashOn = false;
            lastScanlineColorCounter = 0;

            //We jiggle the wait period after resetting so that FRAMES/RANDOMIZE works randomly enough on the speccy.
            rnd_generator = new Random();
            resetFrameTarget = rnd_generator.Next(40, 90);        
            inputFrameTime = rnd_generator.Next(FrameLength);
        }

        //Updates the tape state
        public void UpdateTapeState(int tstates) {
            if (tapeIsPlaying && !tape_edgeDetectorRan) {
                tapeTStates += tstates;
                while (tapeTStates >= edgeDuration) {
                    tapeTStates = (int)(tapeTStates - edgeDuration);
                    DoTapeEvent(new TapeEventArgs(TapeEventType.EDGE_LOAD));
                }
            }
            tape_edgeDetectorRan = false;
        }

        // Reset tape state
        public void ResetTape() {
            tapeBitFlipAck = false;
            tapeBitWasFlipped = false;
            tapeIsPlaying = false;
            isPauseBlockPreproccess = false;
            isProcessingPauseBlock = false;
            tape_detectionCount = 0;
            tape_diff = 0;
            tape_edgeDetectorRan = false;
            tape_FrameCount = 0;
            tape_PCatLastIn = 0;
            tape_regValue = 0;
            tape_tstatesSinceLastIn = 0;
            tape_tstatesStep = 0;
            tape_whichRegToCheck = 0;
            tape_stopTimeOut = TAPE_TIMEOUT;
            tape_detectionCount = 0;
            tape_AutoStarted = false;
            tape_PC = 0;
            tape_A = tape_B = tape_C = tape_D = tape_E = tape_H = tape_L = 0;
            edgeDuration = 0;
            blockCounter = 0;

            pulseLevel = 0;
            repeatCount = 0;
            bitCounter = 0;
            dataCounter = 0;
            dataByte = 0;
            currentBit = 0;
        }

        //Updates the tape state
        public void UpdateTapePlayback() {
            if (!isProcessingPauseBlock) {
                while (tapeTStates >= edgeDuration) {
                    tapeTStates = (int)(tapeTStates - edgeDuration);

                    DoTapeEvent(new TapeEventArgs(TapeEventType.EDGE_LOAD));
                }
            }
            else
                FlashLoad();
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

        private void OnTapeEdgeDecA() {
            if (tapeIsPlaying && tape_edgeLoad)
                if (PeekByteNoContend(cpu.regs.PC) == 0x20)
                    if (PeekByteNoContend((ushort)(cpu.regs.PC + 1)) == 0xfd) {
                        if (cpu.regs.A != 0) {
                            int _a = cpu.regs.A;
                            _a--;
                            _a <<= 4;
                            _a += 4 + 7 + 12;
                            tapeTStates += _a;
                            cpu.regs.PC += 2;
                            cpu.regs.A = 0;
                            cpu.regs.F |= 64;
                        }
                    }
        }

        private void OnTapeEdgeCpA() {
            if (tape_readToPlay && !tapeTrapsDisabled)
                if(cpu.regs.PC == 0x56b)
                    FlashLoadTape();
        }

        //Re-engineered SpecEmu version. Works a treat!
        private void OnTapeEdgeDetection() {
            //Return if not tape is inserted in Tape Deck
            if (!tape_readToPlay || !tape_edgeLoad)
                return;

            if (tapeIsPlaying) {
                if (cpu.regs.PC == tape_PCatLastIn) {
                    if (tape_AutoPlay) {
                        tape_stopTimeOut = TAPE_TIMEOUT;
                        tape_AutoStarted = true;
                    }

                    if (tape_edgeLoad) {
                        if (tapeBitWasFlipped) {
                            tapeBitFlipAck = true;
                            tapeBitWasFlipped = false;
                            return;
                        } else {
                            //bool doLoop = false;
                            switch (tape_whichRegToCheck) {
                                case 1:
                                    tape_regValue = cpu.regs.A;
                                    break;

                                case 2:
                                    tape_regValue = cpu.regs.B;
                                    break;

                                case 3:
                                    tape_regValue = cpu.regs.C;
                                    break;

                                case 4:
                                    tape_regValue = cpu.regs.D;
                                    break;

                                case 5:
                                    tape_regValue = cpu.regs.E;
                                    break;

                                case 6:
                                    tape_regValue = cpu.regs.H;
                                    break;

                                case 7:
                                    tape_regValue = cpu.regs.L;
                                    break;

                                default:
                                    //doLoop = false;
                                    return;
                            }

                            tape_edgeDetectorRan = true;
                            while (!((tape_regValue == 255) || (tape_regValue == 1))) {
                                if (tapeBitFlipAck)
                                    tapeBitWasFlipped = false;

                                tapeTStates += (cpu.t_states - prevT);

                                 if (tapeBitWasFlipped) {
                                    tapeBitFlipAck = true;
                                    return;
                                }

                                if (tapeTStates >= edgeDuration) {
                                    tapeTStates = (int)(tapeTStates - edgeDuration);

                                    DoTapeEvent(new TapeEventArgs(TapeEventType.EDGE_LOAD));
                                }

                               
                                tapeTStates += tape_tstatesStep;
                                switch (tape_whichRegToCheck) {
                                    case 1:
                                        cpu.regs.A += (byte)tape_diff;
                                        tape_regValue = cpu.regs.A;
                                        break;

                                    case 2:
                                        cpu.regs.B += (byte)tape_diff;
                                        tape_regValue = cpu.regs.B;
                                        break;

                                    case 3:
                                        cpu.regs.C += (byte)tape_diff;
                                        tape_regValue = cpu.regs.C;
                                        break;

                                    case 4:
                                        cpu.regs.D += (byte)tape_diff;
                                        tape_regValue = cpu.regs.D;
                                        break;

                                    case 5:
                                        cpu.regs.E += (byte)tape_diff;
                                        tape_regValue = cpu.regs.E;
                                        break;

                                    case 6:
                                        cpu.regs.H += (byte)tape_diff;
                                        tape_regValue = cpu.regs.H;
                                        break;

                                    case 7:
                                        cpu.regs.L += (byte)tape_diff;
                                        tape_regValue = cpu.regs.L;
                                        break;

                                    default:
                                        //doLoop = false;
                                        break;
                                }
                            }
                        }
                    }
                    tape_tstatesSinceLastIn = cpu.t_states;
                }
            } else {
                if (FrameCount != tape_FrameCount)
                    tape_detectionCount = 0;

                int elapsedTapeTstates = cpu.t_states - tape_tstatesSinceLastIn;
                if (((elapsedTapeTstates > 0) && (elapsedTapeTstates < 96)) && (cpu.regs.PC == tape_PC)) {
                    tape_tstatesStep = elapsedTapeTstates;
                    //which reg has changes since last IN
                    int numRegsThatHaveChanged = 0;
                    tape_diff = 0;

                    if (tape_A != cpu.regs.A) {
                        tape_regValue = cpu.regs.A;
                        tape_whichRegToCheck = 1;
                        tape_diff = tape_A - cpu.regs.A;
                        numRegsThatHaveChanged++;
                    }
                    if (tape_B != cpu.regs.B) {
                        tape_regValue = cpu.regs.B;
                        tape_whichRegToCheck = 2;
                        tape_diff = tape_B - cpu.regs.B;
                        numRegsThatHaveChanged++;
                    }
                    if (tape_C != cpu.regs.C) {
                        tape_regValue = cpu.regs.C;
                        tape_whichRegToCheck = 3;
                        tape_diff = tape_C - cpu.regs.C;
                        numRegsThatHaveChanged++;
                    }
                    if (tape_D != cpu.regs.D) {
                        tape_regValue = cpu.regs.D;
                        tape_whichRegToCheck = 4;
                        tape_diff = tape_D - cpu.regs.D;
                        numRegsThatHaveChanged++;
                    }
                    if (tape_E != cpu.regs.E) {
                        tape_regValue = cpu.regs.E;
                        tape_whichRegToCheck = 5;
                        tape_diff = tape_E - cpu.regs.E;
                        numRegsThatHaveChanged++;
                    }
                    if (tape_H != cpu.regs.H) {
                        tape_regValue = cpu.regs.H;
                        tape_whichRegToCheck = 6;
                        tape_diff = tape_H - cpu.regs.H;
                        numRegsThatHaveChanged++;
                    }
                    if (tape_L != cpu.regs.L) {
                        tape_regValue = cpu.regs.L;
                        tape_whichRegToCheck = 7;
                        tape_diff = tape_L - cpu.regs.L;
                        numRegsThatHaveChanged++;
                    }

                    tape_A = cpu.regs.A;
                    tape_B = cpu.regs.B;
                    tape_C = cpu.regs.C;
                    tape_D = cpu.regs.D;
                    tape_E = cpu.regs.E;
                    tape_H = cpu.regs.H;
                    tape_L = cpu.regs.L;

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
                                tape_PCatLastIn = cpu.regs.PC;
                            }
                        }
                    }
                }
                tape_tstatesSinceLastIn = cpu.t_states;
                tape_FrameCount = FrameCount;
                tape_A = cpu.regs.A;
                tape_B = cpu.regs.B;
                tape_C = cpu.regs.C;
                tape_D = cpu.regs.D;
                tape_E = cpu.regs.E;
                tape_H = cpu.regs.H;
                tape_L = cpu.regs.L;
                tape_PC = cpu.regs.PC;
            }
        }

        //The main loop which executes opcodes repeatedly till 1 frame (69888 tstates)
        //has been generated.
        private int NO_PAINT_REP = 10;

        public void Run() {
            for (int rep = 0; rep < /*(tapeIsPlaying && tape_flashLoad ? NO_PAINT_REP :*/ emulationSpeed; rep++)
            {
                while (doRun)
                {
                    //Raise event for debugger
                    OpcodeExecutedEvent?.Invoke(this);
                    
                    //lock (lockThis)
                    {
#region Tape Save Trap
                        //Tape Save trap is active only if lower ROM is 48k
                        if (cpu.regs.PC == 0x04d1 && !tapeTrapsDisabled && lowROMis48K)
                        {
                            OnTapeEvent(new TapeEventArgs(TapeEventType.SAVE_TAP));
                            cpu.regs.IX = (ushort)(cpu.regs.IX + cpu.regs.DE);
                            cpu.regs.DE = 0;
                            cpu.regs.PC = 1342;
                            ResetKeyboard();
                        }
#endregion Tape Deck events

                        if (isPlayingRZX && doRun)
                            ProcessRZX();
                        else
                            if (doRun)
                                Process();

                    } //lock

                    if (needsPaint) {
                        #region Tape Deck event for stopping tape on tape play timeout

                        if (tapeIsPlaying) {
                            if (tape_AutoPlay && tape_AutoStarted) {
                                if (!(isPauseBlockPreproccess && (edgeDuration > 0) && cpu.and_32_Or_64)) {
                                    if (tape_stopTimeOut <= 0) {
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
                        if (FrameCount >= 50) {
                            FrameCount = 0;
                        }

                        if (!externalSingleStep && emulationSpeed == 1) {
                            while (!beeper.FinishedPlaying() && !tapeIsPlaying)
                                System.Threading.Thread.Sleep(1);
                        }

                        if (emulationSpeed > 1 && rep != emulationSpeed - 1)
                        {
                            needsPaint = false;
                            //System.Threading.Thread.Sleep(1); //TO DO: Remove?
                        }
                    
                        break;
                    }

                    if (externalSingleStep)
                        break;
                } //run loop
            }
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
                cpu.t_states += contentionTable[cpu.t_states];
            }

            cpu.t_states += 3;

            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;
            byte _b = PageReadPointer[page][offset];

            //This call flags a memory change event for the debugger
            if (MemoryExecuteEvent != null)
                OnMemoryExecuteEvent(new MemoryEventArgs(addr, _b));

            return _b;
        }

        //Returns the byte at a given 16 bit address (can be contended)
        public byte PeekByte(ushort addr) {
            //Contend(addr, 3, 1);
            if (IsContended(addr)) {
                cpu.t_states += contentionTable[cpu.t_states];
            }

            cpu.t_states += 3;

            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;
            byte _b = PageReadPointer[page][offset];

            //This call flags a memory change event for the debugger
            if (MemoryReadEvent != null)
                OnMemoryReadEvent(new MemoryEventArgs(addr, _b));

            return _b;
        }

        //Returns the byte at a given 16 bit address (can be contended)
        public virtual void PokeByte(ushort addr, byte b) {
            //This call flags a memory change event for the debugger
            if (MemoryWriteEvent != null)
                OnMemoryWriteEvent(new MemoryEventArgs(addr, b));

            if (IsContended(addr)) {
                cpu.t_states += contentionTable[cpu.t_states];
            }
            cpu.t_states += 3;
            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;

            if (((addr & 49152) == 16384) && (PageReadPointer[page][offset] != b)) {
                UpdateScreenBuffer(cpu.t_states);
            }

            PageWritePointer[page][offset] = b;
        }

        //Pokes a 16 bit value at given address. Contention applies.
        public void PokeWord(ushort addr, ushort w) {
            PokeByte(addr, (byte)(w & 0xff));
            PokeByte((ushort)(addr + 1), (byte)(w >> 8));
        }

        //Pokes bytes from an array into a ram bank.
        public void PokeRAMPage(int bank, int dataLength, byte[] data) {
            for (int f = 0; f < dataLength; f++) {
                int indx = f / 8192;
                RAMpage[bank * 2 + indx][f % 8192] = data[f];
            }
        }

        //Pokes bytes from an array into contiguous rom banks.
        public void PokeROMPages(int bank, int dataLength, byte[] data) {
            for (int f = 0; f < dataLength; f++) {
                int indx = f / 8192;
                ROMpage[bank * 2 + indx][f % 8192] = data[f];
            }
        }

        //Pokes the byte at a given 16 bit address with no contention
        public void PokeByteNoContend(int addr, int b) {
            addr &= 0xffff;
            b &= 0xff;

            int page = (addr) >> 13;
            int offset = (addr) & 0x1FFF;

            PageWritePointer[page][offset] = (byte)b;
        }

        //Pokes  byte from an array at a given 16 bit address with no contention
        public void PokeBytesNoContend(int addr, int dataOffset, int dataLength, byte[] data) {
            int page, offset;

            for (int f = dataOffset; f < dataOffset + dataLength; f++, addr++) {
                addr &= 0xffff;
                page = (addr) >> 13;
                offset = (addr) & 0x1FFF;
                PageWritePointer[page][offset] = data[f];
            }
        }

        //Returns a value from a port (can be contended)
        public virtual byte In(ushort port) {
            //Raise a port I/O event
            if (PortEvent != null)
                OnPortEvent(new PortIOEventArgs(port, 0, false));

            return 0;
        }

        //Used purely to raise an event with the debugger for IN with a specific value
        public virtual void In(ushort port, byte val) {
            //Raise a port I/O event
            if (PortEvent != null)
                OnPortEvent(new PortIOEventArgs(port, val, false));

            if (isRecordingRZX) {
                rzx.inputs.Add(val);
            }
        }

        //Outputs a value to a port (can be contended)
        //The base call is used only to raise memory events
        public virtual void Out(ushort port, byte val) {
            //Raise a port I/O event
            if (PortEvent != null)
                OnPortEvent(new PortIOEventArgs(port, val, true));
        }

        public virtual bool IsKempstonActive(int port) {
            if (UseKempstonPort1F) {
                if ((port & 0xe0) == 0)
                    return true;
            }
            else if ((port & 0x20) == 0)
                return true;

            return false;
        }
        //Updates the state of the renderer
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

                        if (ula_plus.Enabled && ula_plus.PaletteEnabled) {
                            paletteInk = ula_plus.Palette[(((flash << 1) + (bright >> 3)) << 4) + ink]; //(flash*2 + bright) * 16 + ink
                            palettePaper = ula_plus.Palette[(((flash << 1) + (bright >> 3)) << 4) + paper + 8]; //(flash*2 + bright) * 16 + paper + 8
                        }

                        for (int a = 0; a < 8; ++a) {
                            if ((pixelData & 0x80) != 0) {
                                //PAL interlacing
                                //int pal = paletteInk/2 + (0xffffff - LastScanlineColor[lastScanlineColorCounter]/2);
                                //ScreenBuffer[ULAByteCtr++] = pal;
                                //LastScanlineColor[lastScanlineColorCounter++] = paletteInk;
                                ScreenBuffer[ULAByteCtr++] = paletteInk;
                                lastAttrValue = ink;
                            } else {
                                //PAL interlacing
                                //int pal = palettePaper/2 + (0xffffff - LastScanlineColor[lastScanlineColorCounter]/2);
                                //ScreenBuffer[ULAByteCtr++] = pal;
                                //LastScanlineColor[lastScanlineColorCounter++] = palettePaper;
                                ScreenBuffer[ULAByteCtr++] = palettePaper;
                                lastAttrValue = paper;
                            }
                            pixelData <<= 1;
                        }                 
                    // pixelData = lastPixelValue;
                } else if (tstateToDisp[lastTState] == 1) {
                    int bor;
                    if (ula_plus.Enabled && ula_plus.PaletteEnabled) {
                        bor = ula_plus.Palette[borderColour + 8];
                    } else
                        bor = AttrColors[borderColour];

                    for(int g = 0; g < 8; g++) {
                        //PAL interlacing
                        //int pal = bor/2 + (0xffffff -  LastScanlineColor[lastScanlineColorCounter]/2);
                        //ScreenBuffer[ULAByteCtr++] = pal;
                        //LastScanlineColor[lastScanlineColorCounter++] = bor;                        
                        ScreenBuffer[ULAByteCtr++] = bor;
                    }
                }
                lastTState += 4;

                if(lastScanlineColorCounter >= ScanLineWidth)
                    lastScanlineColorCounter = 0;
            }
        }

        // Wrapper for ULA events
        public void UpdateScreenBuffer() {
            UpdateScreenBuffer(cpu.t_states);
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

        //Updates audio state, called from Process()
        private void UpdateAudio(int dt) {
            foreach (var ad in audio_devices) {
                ad.Update(dt);
            }
            averagedSound += soundOut;
            soundCounter++;
        }

        // The HALT behavior is incorrect in most emulators where PC is decremented so
        // that HALT is executed again. The correct behaviour is:
        // "When HALT is low PC has already been incremented and the opcode fetched is for the instruction after HALT.
        // The halt state stops this instruction from being executed and PC from incrementing so this opcode is read
        // again and again until an exit condition occurs. When an interrupt occurs during the halt state PC is pushed
        // unchanged onto the stack as it is already the correct return address."
        // When loading or saving snapshots we need to account for the old behaviour and increment PC so that we emulate
        // correctly post-load or during save.
        protected virtual void CorrectPCForHalt() {
            if (cpu.is_halted) {
                cpu.regs.PC++;
            }
        }

        public virtual void SaveSNA(string filename) {
            SNA_SNAPSHOT snapshot;

            if (model == MachineModel._48k || model == MachineModel._NTSC48k)
                snapshot = new SNA_48K();
            else
                snapshot = new SNA_128K();
                
            snapshot.HEADER.I = (byte)cpu.regs.I;
            snapshot.HEADER.HL_ = (ushort)cpu.regs.HL_;
            snapshot.HEADER.DE_ = (ushort)cpu.regs.DE_;
            snapshot.HEADER.BC_ = (ushort)cpu.regs.BC_;
            snapshot.HEADER.AF_ = (ushort)cpu.regs.AF_;

            snapshot.HEADER.HL = (ushort)cpu.regs.HL;
            snapshot.HEADER.DE = (ushort)cpu.regs.DE;
            snapshot.HEADER.BC = (ushort)cpu.regs.BC;
            snapshot.HEADER.IY = (ushort)cpu.regs.IY;
            snapshot.HEADER.IX = (ushort)cpu.regs.IX;

            snapshot.HEADER.IFF2 = (byte)(cpu.iff_1 ? 1 << 2 : 0);
            snapshot.HEADER.R = (byte)cpu.regs.R;
            snapshot.HEADER.AF = (ushort)cpu.regs.AF;
        
            snapshot.HEADER.IM = (byte)cpu.interrupt_mode;
            snapshot.HEADER.BORDER = (byte)borderColour;

            if (model == MachineModel._48k || model == MachineModel._NTSC48k) {
                ushort snap_pc = cpu.regs.PC;
                if (cpu.is_halted) {
                    snap_pc--;
                }
                cpu.PushStack(snap_pc);
                snapshot.HEADER.SP = (ushort)cpu.regs.SP;

                ((SNA_48K)snapshot).RAM = new byte[49152];

                int screenAddr = DisplayStart;

                for (int f = 0; f < 49152; ++f)
                    ((SNA_48K)snapshot).RAM[f] = PeekByteNoContend((ushort)(screenAddr +f));

                cpu.PopStack(); //Ignore the PC that will be popped.
            }
            else {
                snapshot.HEADER.SP = (ushort)cpu.regs.SP;

                if (cpu.is_halted) {
                    ((SNA_128K)snapshot).PC = (ushort)(cpu.regs.PC - 1);
                }
                else {
                    ((SNA_128K)snapshot).PC = (ushort)cpu.regs.PC;
                }

                ((SNA_128K)snapshot).PORT_7FFD = (byte)last7ffdOut;
                ((SNA_128K)snapshot).TR_DOS = (byte)(trDosPagedIn ? 1 : 0);
                
                for (int f = 0; f < 16; f++)
                    ((SNA_128K)snapshot).RAM_BANK[f] = new byte[8192];

                Array.Copy(RAMpage[(int)RAM_BANK.FIVE_LOW], 0, ((SNA_128K)snapshot).RAM_BANK[0], 0, 8192);
                Array.Copy(RAMpage[(int)RAM_BANK.FIVE_HIGH], 0, ((SNA_128K)snapshot).RAM_BANK[1], 0, 8192);

                Array.Copy(RAMpage[(int)RAM_BANK.TWO_LOW], 0, ((SNA_128K)snapshot).RAM_BANK[2], 0, 8192);
                Array.Copy(RAMpage[(int)RAM_BANK.TWO_HIGH], 0, ((SNA_128K)snapshot).RAM_BANK[3], 0, 8192);

                int BankInPage4 = ((SNA_128K)snapshot).PORT_7FFD & 0x07;

                Array.Copy(RAMpage[BankInPage4 * 2], 0, ((SNA_128K)snapshot).RAM_BANK[4], 0, 8192);
                Array.Copy(RAMpage[BankInPage4 * 2 + 1], 0, ((SNA_128K)snapshot).RAM_BANK[5], 0, 8192);

                int t = 3;
                for (int f = 0; f < 8; f++) {
                    if (f == 5 || f == 2 || f == BankInPage4)
                        continue;

                    Array.Copy(RAMpage[f * 2], 0, ((SNA_128K)snapshot).RAM_BANK[t * 2], 0, 8192);
                    Array.Copy(RAMpage[f * 2 + 1], 0, ((SNA_128K)snapshot).RAM_BANK[t * 2 + 1], 0, 8192);
                    t++;
                }
            }

            SNAFile.SaveSNA(filename, snapshot);
        }
        
        //Sets the speccy state to that of the SNA file
        public virtual void UseSNA(SNA_SNAPSHOT sna) {
            cpu.regs.I = sna.HEADER.I;
            cpu.regs.HL_ = sna.HEADER.HL_;
            cpu.regs.DE_ = sna.HEADER.DE_;
            cpu.regs.BC_ = sna.HEADER.BC_;
            cpu.regs.AF_ = sna.HEADER.AF_;

            cpu.regs.HL = sna.HEADER.HL;
            cpu.regs.DE = sna.HEADER.DE;
            cpu.regs.BC = sna.HEADER.BC;
            cpu.regs.IY = sna.HEADER.IY;
            cpu.regs.IX = sna.HEADER.IX;

            cpu.iff_1 = ((sna.HEADER.IFF2 & 0x04) != 0);

            if (cpu.iff_1)
                cpu.interrupt_count = 1;        //force ignore re-triggered interrupts

            cpu.regs.R = sna.HEADER.R;
            cpu.regs.R_ = (byte)(cpu.regs.R & 0x80);
            cpu.regs.AF = sna.HEADER.AF;
            cpu.regs.SP = sna.HEADER.SP;
            cpu.interrupt_mode = sna.HEADER.IM;
            borderColour = sna.HEADER.BORDER;
        }

        //Sets the speccy state to that of the SNA file
        public virtual void UseSZX(SZXFile szx) {
            cpu.regs.I = szx.z80Regs.I;
            cpu.regs.HL_ = szx.z80Regs.HL1;
            cpu.regs.DE_ = szx.z80Regs.DE1;
            cpu.regs.BC_ = szx.z80Regs.BC1;
            cpu.regs.AF_ = szx.z80Regs.AF1;
            cpu.regs.HL = szx.z80Regs.HL;
            cpu.regs.DE = szx.z80Regs.DE;
            cpu.regs.BC = szx.z80Regs.BC;
            cpu.regs.IY = szx.z80Regs.IY;
            cpu.regs.IX = szx.z80Regs.IX;
            cpu.iff_1 = (szx.z80Regs.IFF1 != 0);
            cpu.regs.R = szx.z80Regs.R;
            cpu.regs.R_ = (byte)(cpu.regs.R & 0x80);
            cpu.regs.AF = szx.z80Regs.AF;
            cpu.regs.SP = szx.z80Regs.SP;
            cpu.interrupt_mode = szx.z80Regs.IM;
            cpu.regs.PC = szx.z80Regs.PC;
            cpu.interrupt_count = (byte)((szx.z80Regs.Flags & SZXFile.ZXSTZF_EILAST) != 0 ? 2 : 0);
            cpu.is_halted = (szx.z80Regs.Flags & SZXFile.ZXSTZF_HALTED) != 0;

            CorrectPCForHalt();

            Issue2Keyboard = (szx.keyboard.Flags & SZXFile.ZXSTKF_ISSUE2) != 0;
            
            if (szx.paletteLoaded)
            {
                if (ula_plus == null) {
                    ula_plus = new ULA_Plus();
                    AddDevice(ula_plus);
                }
                ula_plus.PaletteEnabled = szx.palette.flags > 0 ? true : false;
                ula_plus.PaletteGroup = szx.palette.currentRegister;

                for (int f = 0; f < 64 ; f++)
                {
                    byte val = szx.palette.paletteRegs[f];

                    //3 bits to 8 bits to be stored as hmlhmlml for each color

                    //First get B
                    int bh = (val & 0x2) >> 1;
                    int bl = val & 0x1;
                    int bm = bl;
                    int B = (bh << 7) | (bm << 6) | (bl << 5) | (bh << 4) | (bm << 3) | (bl << 2) | (bm << 1) | bl;

                    //R
                    int rl = (val & 0x4) >> 2;
                    int rm = (val & 0x8) >> 3;
                    int rh = (val & 0x10) >> 4;

                    int R = (rh << 7) | (rm << 6) | (rl << 5) | (rh << 4) | (rm << 3) | (rl << 2) | (rm << 1) | rl;

                    //G
                    int gl = (val & 0x20) >> 5;
                    int gm = (val & 0x40) >> 6;
                    int gh = (val & 0x80) >> 7;

                    int G = (gh << 7) | (gm << 6) | (gl << 5) | (gh << 4) | (gm << 3) | (gl << 2) | (gm << 1) | gl;

                    ula_plus.Palette[f] = (R << 16) | (G << 8) | B;
                }
            }

            if (szx.header.MinorVersion > 3)
                cpu.regs.MemPtr = szx.z80Regs.MemPtr;
            else
                cpu.regs.MemPtr = (ushort)(szx.z80Regs.MemPtr & 0xff);

            for (int f = 0; f < 16; f++) {
                Array.Copy(szx.RAM_BANK[f], 0, RAMpage[f], 0, 8192);
            }
        }

        //Sets the speccy state to that of the Z80 file
        public virtual void UseZ80(Z80_SNAPSHOT z80) 
        {
            cpu.regs.I = z80.I;
            cpu.regs.HL_ = (ushort)z80.HL_;
            cpu.regs.DE_ = (ushort)z80.DE_;
            cpu.regs.BC_ = (ushort)z80.BC_;
            cpu.regs.AF_ = (ushort)z80.AF_;

            cpu.regs.HL = (ushort)z80.HL;
            cpu.regs.DE = (ushort)z80.DE;
            cpu.regs.BC = (ushort)z80.BC;
            cpu.regs.IY = (ushort)z80.IY;
            cpu.regs.IX = (ushort)z80.IX;

            cpu.iff_1 = z80.IFF1;
            cpu.regs.R = z80.R;
            cpu.regs.R_ = (byte)(cpu.regs.R & 0x80);
            cpu.regs.AF = (ushort)z80.AF;
            cpu.regs.SP = (ushort)z80.SP;
            cpu.interrupt_mode = z80.IM;
            cpu.regs.PC = (ushort)z80.PC;
            cpu.t_states = z80.TSTATES % FrameLength;
            borderColour = z80.BORDER;
            Issue2Keyboard = z80.ISSUE2;
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

        private SZXFile CreateSZX() {
            SZXFile szx = new SZXFile();
            szx.header = new SZXFile.ZXST_Header();
            szx.creator = new SZXFile.ZXST_Creator();
            szx.z80Regs = new SZXFile.ZXST_Z80Regs();
            szx.specRegs = new SZXFile.ZXST_SpecRegs();
            szx.keyboard = new SZXFile.ZXST_Keyboard();

            for (int f = 0; f < 16; f++)
                szx.RAM_BANK[f] = new byte[8192];
            szx.header.MachineId = (byte)model;
            szx.header.Magic = GetUIntFromString("ZXST");
            szx.header.MajorVersion = 1;
            szx.header.MinorVersion = 4;
            szx.header.Flags |= (byte)LateTiming;
            szx.creator.CreatorName = "Zero Spectrum Emulator by Arjun ".ToCharArray();
            szx.creator.MajorVersion = SZXFile.SZX_VERSION_SUPPORTED_MAJOR;
            szx.creator.MinorVersion = SZXFile.SZX_VERSION_SUPPORTED_MINOR;
            if (Issue2Keyboard)
                szx.keyboard.Flags |= SZXFile.ZXSTKF_ISSUE2;
            szx.keyboard.KeyboardJoystick |= 8;
            szx.z80Regs.AF = (ushort)cpu.regs.AF;
            szx.z80Regs.AF1 = (ushort)cpu.regs.AF_;
            szx.z80Regs.BC = (ushort)cpu.regs.BC;
            szx.z80Regs.BC1 = (ushort)cpu.regs.BC_;
            szx.z80Regs.MemPtr = (ushort)cpu.regs.MemPtr;
            szx.z80Regs.CyclesStart = (uint)cpu.t_states;
            szx.z80Regs.DE = (ushort)cpu.regs.DE;
            szx.z80Regs.DE1 = (ushort)cpu.regs.DE_;
           
            szx.z80Regs.HL = (ushort)cpu.regs.HL;
            szx.z80Regs.HL1 = (ushort)cpu.regs.HL_;
            szx.z80Regs.I = (byte)cpu.regs.I;
            szx.z80Regs.IFF1 = (byte)(cpu.iff_1 ? 1 : 0);
            szx.z80Regs.IFF2 = (byte)(cpu.iff_2 ? 1 : 0);
            szx.z80Regs.IM = (byte)cpu.interrupt_mode;
            szx.z80Regs.IX = (ushort)cpu.regs.IX;
            szx.z80Regs.IY = (ushort)cpu.regs.IY;
            szx.z80Regs.PC = (ushort)cpu.regs.PC;
            szx.z80Regs.R = (byte)cpu.regs.R;
            szx.z80Regs.SP = (ushort)cpu.regs.SP;
            szx.specRegs.Border = (byte)borderColour;
            szx.specRegs.Fe = (byte)lastFEOut;
            szx.specRegs.pagePort = (byte)last1ffdOut;
            szx.specRegs.x7ffd = (byte)last7ffdOut;

            if (cpu.interrupt_count != 0)
                szx.z80Regs.Flags |= SZXFile.ZXSTZF_EILAST;
            else if (cpu.is_halted) {
                szx.z80Regs.Flags |= SZXFile.ZXSTZF_HALTED;
                szx.z80Regs.PC--;
            }

            foreach (var ad in audio_devices) {
                if (ad is AY_8192) {
                    AY_8192 ay_device = (AY_8192)(ad);
                    szx.ayState = new SZXFile.ZXST_AYState();
                    szx.ayState.cFlags = 0;
                    szx.ayState.currentRegister = (byte)ay_device.SelectedRegister;
                    szx.ayState.chRegs = ay_device.GetRegisters();
                }
            }

            for (int f = 0; f < 16; f++) {
                Array.Copy(RAMpage[f], 0, szx.RAM_BANK[f], 0, 8192);
            }

            if (tapeFilename != "") {
                szx.InsertTape = true;
                szx.externalTapeFile = tapeFilename;
            }
            if (ula_plus.Enabled) {
                szx.palette = new SZXFile.ZXST_PaletteBlock();
                szx.palette.paletteRegs = new byte[64];
                szx.paletteLoaded = true;
                szx.palette.flags = (byte)(ula_plus.PaletteEnabled ? 1 : 0);
                szx.palette.currentRegister = (byte)ula_plus.PaletteGroup;
                for (int f = 0; f < 64; f++) {
                    int rgb = ula_plus.Palette[f];
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
            foreach(var ad in audio_devices) {
                if (val == 0)
                    ad.EnableStereoSound(false);
                else {
                    ad.EnableStereoSound(true);
                    if (val == 1)
                        ad.SetChannelsACB(true);
                    else
                        ad.SetChannelsACB(false);
                }
            }
           
        }

        //Enables/Disables AY sound
        public virtual void EnableAY(bool val) {
            if (model == MachineModel._48k) {
                HasAYSound = val;
                if (val) {
                    AY_8192 ay_device = new AY_8192();
                    AddDevice(ay_device);
                }
                else {
                    RemoveDevice(SPECCY_DEVICE.AY_3_8912);
                }
            }
            else {
                HasAYSound = true;
                AY_8192 ay_device = new AY_8192();
                AddDevice(ay_device);
            }
        }

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
            lastScanlineColorCounter = 0;
            screenByteCtr = DisplayStart;
            lastTState = ActualULAStart;
            needsPaint = true;
        }

        //Returns true if the given address should be contended, false otherwise
        public abstract bool IsContended(int addr);

        //Contends the machine for a given address (_addr)
        public void Contend(int _addr) {
            if (model != MachineModel._plus3 && IsContended(_addr)) {
                cpu.t_states += contentionTable[cpu.t_states];
            }
        }

        //Contends the machine for a given address (_addr) for n tstates (_time) for x times (_count)
        public void Contend(int _addr, int _time, int _count) {
            if (model != MachineModel._plus3 && IsContended(_addr)) {
                for (int f = 0; f < _count; f++) {
                    cpu.t_states += contentionTable[cpu.t_states] + _time;
                }
            } else
                cpu.t_states += _count * _time;
        }

        //IO Contention:
        // Contention| LowBitReset| Result
        //-----------------------------------------
        // No        | No         | N:4
        // No        | Yes        | N:1 C:3
        // Yes       | Yes        | C:1 C:3
        // Yes       | No         | C:1 C:1 C:1 C:1

        //Should never be called on +3
        public void ContendPortEarly(int _addr) {
            if (IsContended(_addr)) {
                cpu.t_states += contentionTable[cpu.t_states];
            }
            cpu.t_states++;
        }

        //Should never be called on +3
        public void ContendPortLate(int _addr) {
            bool lowBitReset = (_addr & 0x01) == 0;

            if (lowBitReset) {
                cpu.t_states += contentionTable[cpu.t_states];
                cpu.t_states += 2;
            }
            else if (IsContended(_addr)) {
                cpu.t_states += contentionTable[cpu.t_states]; cpu.t_states++;
                cpu.t_states += contentionTable[cpu.t_states]; cpu.t_states++;
                cpu.t_states += contentionTable[cpu.t_states];
            }
            else {
                cpu.t_states += 2;
            }
        }

        public void ForceContention(int _addr) {
            if (IsContended(_addr)) {
                cpu.t_states += contentionTable[cpu.t_states]; cpu.t_states++;
                cpu.t_states += contentionTable[cpu.t_states]; cpu.t_states++;
                cpu.t_states += contentionTable[cpu.t_states]; cpu.t_states++;
            }
            else {
                cpu.t_states += 3;
            }
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
                        ula_plus.Palette[f] = buffer[f];

                    cpu.regs.PC = PeekWordNoContend(cpu.regs.SP);
                    cpu.regs.SP += 2;
                    return true;
                }
            }
        }

        public int GetNumRZXFramesPlayed() {
            if (isPlayingRZX)
                return rzx.NumFramesPlayed;

            return 0;
        }

        public void NextRZXFrame() {
            cpu.t_states = 0;
            isPlayingRZX = rzx.NextPlaybackFrame();
        }

        public void EndRZXFrame() {
            if (cpu.iff_1) {
                Interrupt();
            }

            if (cpu.t_states >= FrameLength) {
                int deltaSoundT = FrameLength - cpu.t_states;
                timeToOutSound += deltaSoundT;
                cpu.t_states = (ushort)FrameLength; //generate interrupt
            }

            if (!needsPaint)
                UpdateScreenBuffer(FrameLength - 1);

            flashFrameCount++;

            if (flashFrameCount > 15) {
                flashOn = !flashOn;
                flashFrameCount = 0;
            }

            ULAUpdateStart();
            NextRZXFrame();
        }

        private void PlayAudio() {
            averagedSound /= soundCounter;

            while (timeToOutSound >= soundTStatesToSample) {
                int sumChannel1Output = 0;
                int sumChannel2Output = 0;

                foreach (var ad in audio_devices) {
                    ad.EndSampleFrame();

                    sumChannel1Output += ad.SoundChannel1;
                    sumChannel2Output += ad.SoundChannel2;
                    ad.ResetSamples();
                }
                soundSamples[soundSampleCounter++] = (short)(sumChannel1Output + averagedSound);
                soundSamples[soundSampleCounter++] = (short)(sumChannel2Output + averagedSound);

                if (soundSampleCounter >= soundSamples.Length) {
                    byte[] sndbuf = beeper.LockBuffer();
                    if (sndbuf != null) {
                        System.Buffer.BlockCopy(soundSamples, 0, sndbuf, 0, sndbuf.Length);
                        beeper.UnlockBuffer(sndbuf);
                    }
                    soundSampleCounter = 0;// (short)(soundSampleCounter - (soundSamples.Length));
                }
                timeToOutSound -= soundTStatesToSample;
            }
            averagedSound = 0;
            soundCounter = 0;
        }

        public void ProcessRZX() {
            prevT = cpu.t_states;
            cpu.Step();
            deltaTStates = cpu.t_states - prevT;

            //// Change CPU speed///////////////////////
            if (emulationSpeed > 9) {
                deltaTStates /= emulationSpeed;
                if (deltaTStates < 1)
                    deltaTStates = 0;// (tapeIsPlaying ? 0 : 1); //tape loading likes 0, sound emulation likes 1. WTF?

                cpu.t_states = prevT + deltaTStates;
                if (tapeIsPlaying)
                    soundTStatesToSample = 79;
            }
            /////////////////////////////////////////////////
            timeToOutSound += deltaTStates;
            UpdateAudio(deltaTStates);

            //There is no tape playback in RZX

            averagedSound += soundOut;
            soundCounter++;
            //Update sound every 79 tstates
            if (timeToOutSound >= soundTStatesToSample) {
                PlayAudio();
            }

            if (cpu.t_states >= FrameLength) {
                int deltaSoundT = FrameLength - cpu.t_states;
                timeToOutSound += deltaSoundT;
                cpu.t_states -= FrameLength;
            }

            if (rzx.fetchCount >= rzx.frame.instructionCount) {
                RZXFrameEventArgs e = new RZXFrameEventArgs(rzx.NumFramesPlayed, rzx.fetchCount, rzx.frame.instructionCount, rzx.frame.inputCount, rzx.inputCount);
                if (RZXFrameEndEvent != null) {
                    RZXFrameEndEvent(this, e);
                }
                OnFrameEndEvent();
                EndRZXFrame();
                if (!doRun) {
                    return;
                }
            }
        }

        //The heart of the speccy. Executes opcodes till 69888 tstates (1 frame) have passed
        public void Process() {
            //Handle re-triggered interrupts!
            bool ran_interrupt = false;
            if (cpu.iff_1  && cpu.t_states < InterruptPeriod) {

                if (cpu.interrupt_count == 0) {
                    if (cpu.parityBitNeedsReset) {
                        cpu.SetParity(false);
                    }
                    if (isRecordingRZX) {
#if NEW_RZX_METHODS
                        rzx.UpdateRecording(cpu.t_states);
#else
                    //rzx.RecordFrame(rzxInputs);
#endif
                    }

                    StateChangeEvent?.Invoke(this, new StateChangeEventArgs(SPECCY_EVENT.RE_INTERRUPT));

                    Interrupt();
                    ran_interrupt = true;
                    cpu.parityBitNeedsReset = false;
                }
            }

            if (cpu.interrupt_count > 0)
                cpu.interrupt_count--;

            //Check if TR DOS needs to be swapped for Pentagon 128k.
            //TR DOS is swapped in when PC >= 15616 and swapped out when PC > 16383.
            if (model == MachineModel._pentagon) {
                if (trDosPagedIn) {
                    if (cpu.regs.PC > 0x3FFF) {
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
                     if ((cpu.regs.PC >> 8) == 0x3d) {
                        PageReadPointer[0] = ROMpage[4];
                        PageReadPointer[1] = ROMpage[5];
                        PageWritePointer[0] = JunkMemory[0];
                        PageWritePointer[1] = JunkMemory[1];
                        trDosPagedIn = true;
                        BankInPage0 = ROM_TR_DOS;
                    }
                } 
            }

            if (!ran_interrupt) {
                prevT = cpu.t_states;

                cpu.Step();

                deltaTStates = cpu.t_states - prevT;

                //// Change CPU speed///////////////////////
                if (cpuMultiplier > 1 && !tapeIsPlaying) {
                    deltaTStates /= cpuMultiplier;
                    if (deltaTStates < 1)
                        deltaTStates = (tapeIsPlaying ? 0 : 1); //tape loading likes 0, sound emulation likes 1. WTF?

                    cpu.t_states = prevT + deltaTStates;
                }
                /////////////////////////////////////////////////
                timeToOutSound += deltaTStates;
            }
            
            //UpdateTape
            if (tapeIsPlaying && !tape_edgeDetectorRan) {
                if (!ran_interrupt)
                    tapeTStates += deltaTStates;

                UpdateTapePlayback();
            }

            tape_edgeDetectorRan = false;

            //Update Sound
            if (!externalSingleStep) {
                UpdateAudio(deltaTStates);

                averagedSound += soundOut;
                soundCounter++;
                //Update sound every 79 tstates
                if (timeToOutSound >= soundTStatesToSample) {
                    PlayAudio();
                }
            }

            //Randomize when the keyboard state is updated as in a real speccy (kinda).
            if (cpu.t_states >= inputFrameTime) {
                UpdateInput();
                inputFrameTime = rnd_generator.Next(FrameLength);
            }

            //End of frame?
            if (cpu.t_states >= FrameLength) {
                //If machine has already repainted the entire screen,
                //somewhere midway through execution, we can skip this.
                if (!needsPaint)
                    UpdateScreenBuffer(FrameLength);

                OnFrameEndEvent();

                cpu.t_states -= FrameLength;

                flashFrameCount++;

                if (flashFrameCount > 15) {
                    flashOn = !flashOn;
                    flashFrameCount = 0;
                }

                ULAUpdateStart();

                if (isRecordingRZX) {
                    /*  rzxFrame = new RZXFile.RZX_Frame();
                      rzxFrame.inputCount = (ushort)rzxInputs.Count;
                      rzxFrame.instructionCount = (ushort)rzxFetchCount;
                      rzxFrame.inputs = rzxInputs.ToArray();
                      rzx.frames.Add(rzxFrame);
                      rzxFetchCount = 0;
                      rzxInputCount = 0;*/
#if NEW_RZX_METHODS
                    rzx.UpdateRecording(cpu.t_states);
#else
                    //rzx.RecordFrame(rzxInputs);
#endif
                    //rzxInputs = new System.Collections.Generic.List<byte>();
                }
            }
        }

        //Processes an interrupt
        public void Interrupt() {
            if (cpu.interrupt_mode < 2) //IM0 = IM1 for our purpose
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
            }
            int oldT = cpu.t_states;
            cpu.Interrupt();
            deltaTStates = cpu.t_states - oldT;
            timeToOutSound += deltaTStates; 
            //UpdateAudio(deltaT);
        }

        public void StopTape(bool cancelCallback = false) {
            tapeIsPlaying = false;
            //tape_readToPlay = false;
            //if (pulseLevel != 0)
            //    FlipTapeBit();
            if (TapeEvent != null && !cancelCallback)
                OnTapeEvent(new TapeEventArgs(TapeEventType.STOP_TAPE)); //stop the tape!
        }

        private void FlipTapeBit() {
            pulseLevel = 1 - pulseLevel;

            tapeBitWasFlipped = true;
            tapeBitFlipAck = false;

            if (pulseLevel == 0) {
                soundOut = 0;
            } else
                soundOut = short.MinValue >> 1; //half
        }

        public void NextPZXBlock() {
            while (true) {
                blockCounter++;
                if (blockCounter >= PZXFile.blocks.Count) {
                    blockCounter--;
                    //tape_readToPlay = false;
                    StopTape();
                    return;
                }

                currentBlock = PZXFile.blocks[blockCounter];
               
                if (currentBlock is PZXFile.PULS_Block) {
                    //Initialise for PULS loading
                    pulseCounter = -1;
                    repeatCount = -1;

                    //Pulse is low by default for PULS blocks
                    if (pulseLevel != 0)
                        FlipTapeBit();
                   
                    //Process pulse if there is one
                    if (!NextPULS()) {
                        continue; //No? Next block please!
                    } else
                        break;
                } else if (currentBlock is PZXFile.DATA_Block) {
                    pulseCounter = -1;
                    bitCounter = -1;
                    dataCounter = -1;
                    bitShifter = 0;
                    
                    if (pulseLevel != (((PZXFile.DATA_Block)currentBlock).initialPulseLevel))
                        FlipTapeBit();

                    if (!NextDataBit()) {
                        continue;
                    } else
                        break;
                } else if (currentBlock is PZXFile.PAUS_Block) {
                    //Would have been nice to skip PAUS blocks when doing fast loading
                    //but some loaders like Auf Wiedersehen Monty (Kixx) rely on the pause
                    //length to do processing during loading. In this case, fill in the
                    //loading screen.

                    //Ensure previous edge is finished correctly by flipping the edge one last time
                    //edgeDuration = (35000 * 2);
                    //isPauseBlockPreproccess = true;
                    PZXFile.PAUS_Block block = (PZXFile.PAUS_Block)currentBlock;

                    if (block.initialPulseLevel != pulseLevel)
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
                } else if ((currentBlock is PZXFile.STOP_Block)) {
                    StopTape();
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

            PZXFile.PULS_Block block = (PZXFile.PULS_Block)currentBlock;

            while (pulseCounter < block.pulse.Count - 1) {
                pulseCounter++; //b'cos pulseCounter is -1 when it reaches here initially
                repeatCount = block.pulse[pulseCounter].count;
                if ((block.pulse[pulseCounter].duration == 0) && repeatCount > 1) {
                    if ((repeatCount & 0x01) != 0) 
                         FlipTapeBit();                        
                    continue; //next pulse
                }
                edgeDuration = block.pulse[pulseCounter].duration;

                if (edgeDuration > 0) {
                    int diff = (int)edgeDuration - tapeTStates;
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
                
                return true;
            }
            
            //All pulses done!
            return false;
        }

        public bool NextDataBit() {
            PZXFile.DATA_Block block = (PZXFile.DATA_Block)currentBlock;
           
            //Bits left for processing?
            while (bitCounter < block.count - 1) {
                bitCounter++;
                if (bitShifter == 0) {
                    bitShifter = 0x80;
                    //All 8 bits done so get next byte
                    dataCounter++;
                    if (dataCounter < block.data.Count) {
                        dataByte = block.data[dataCounter];
                    }
                }
                currentBit = ((dataByte & bitShifter) == 0 ? 0 : 1);
                bitShifter >>= 1;
                pulseCounter = 0;
                int numPulses = 0;

                if (currentBit == 0) {
                    edgeDuration = (block.s0[0]);
                    numPulses = block.p0;
                }
                else {
                    edgeDuration = (block.s1[0]);
                    numPulses = block.p1;
                }

                if (numPulses == 0)
                    continue;

                if (edgeDuration > 0) {
                    int diff = (int)edgeDuration - tapeTStates;
                    if(diff > 0) {
                        edgeDuration = (uint)diff;
                        tapeTStates = 0;
                        return true;
                    }
                    else
                        tapeTStates = -diff;
                }

                FlipTapeBit();
            }

            //All bits done. Now do the tail pulse to finish off things
            if (block.tail > 0) {
                currentBit = -1;
                edgeDuration = (block.tail);

                if (edgeDuration > 0) {
                    int diff = (int)edgeDuration - tapeTStates;
                    if (diff > 0) {
                        edgeDuration = (uint)diff;
                        tapeTStates = 0;
                        return true;
                    } else
                        tapeTStates = -diff;
                }
                FlipTapeBit();
            } else {
                //HACK: Sometimes a tape might have its last tail pulse missing.
                //In case it's the last block in the tape, it's best to flip the tape bit
                //a last time to ensure that the process is terminated properly.
                if (blockCounter == PZXFile.blocks.Count - 1) {
                    currentBit = -1;
                    edgeDuration = (3500 * 2);
                    return true;
                }
            }

            return false;
        }

        private void FlashLoad()
        {
            if (blockCounter < 0)
                blockCounter = 0;

            PZXFile.Block currBlock = PZXFile.blocks[blockCounter];


            /*if (currBlock is PZXFile.PAUS_Block) {
                if (!isProcessingPauseBlock) {
                    isProcessingPauseBlock = true;
                    edgeDuration = ((PZXFile.PAUS_Block)currBlock).duration;
                    pauseCounter = edgeDuration;
                    //tapeTStates = 0;
                    return;
                }
                else {
                    pauseCounter -= (uint)tapeTStates;
                    if (pauseCounter > 0)
                        return;
                    else {
                        pauseCounter = 0;
                        isProcessingPauseBlock = false;
                    }
                }
            }*/

            if (!(currBlock is PZXFile.PULS_Block))
                blockCounter++;

            if (blockCounter >= PZXFile.tapeBlockInfo.Count)
            {
                blockCounter--;
                //tape_readToPlay = false;
                StopTape();
                return;
            }

            if (!PZXFile.tapeBlockInfo[blockCounter].IsStandardBlock)
            {
                if (!(currBlock is PZXFile.PULS_Block))
                    blockCounter--;
                return;
            }

            PZXFile.DATA_Block dataBlock = (PZXFile.DATA_Block)PZXFile.blocks[blockCounter + 1];
            edgeDuration = (1000);
            //if (pulseLevel != dataBlock.initialPulseLevel)
            //    FlipTapeBit();
            cpu.regs.H = 0;
            int byteCounter = dataBlock.data.Count;
            int dataIndex = 0;
            bool loadStageFlagByte = true;
            while (true)
            {
                if (byteCounter == 0)
                {
                    cpu.regs.A = (byte)(cpu.regs.C & 32);
                    cpu.regs.B = 0;
                    cpu.regs.F = 0x50; //01010000b
                    break;
                }
                byteCounter--;
                cpu.regs.L = dataBlock.data[dataIndex++];
                cpu.regs.H ^= cpu.regs.L;
                if (cpu.regs.DE == 0)
                {
                    cpu.regs.A = cpu.regs.H;
                    cpu.Cp_R(1);
                    break;
                }
                if (loadStageFlagByte)
                {
                    loadStageFlagByte = false;
                    cpu.regs.A = (byte)(cpu.regs.AF_ >> 8);
                    cpu.Xor_R(cpu.regs.L);
                    if ((cpu.regs.F & 0x040) == 0)
                        break;
                }
                else
                {
                    PokeByteNoContend(cpu.regs.IX++, cpu.regs.L);
                    cpu.regs.DE--;
                }
            }

            //Simulate RET
            cpu.regs.PC = cpu.PopStack();
            cpu.regs.MemPtr = cpu.regs.PC;

            blockCounter++;
            if (blockCounter >= PZXFile.blocks.Count)
            {
                blockCounter--;
                //tape_readToPlay = false;
                StopTape();
                return;
            }
        }

        private void DoTapeEvent(TapeEventArgs e) {
            if (tapeBitFlipAck)
                tapeBitWasFlipped = false;

            if (e.EventType == TapeEventType.EDGE_LOAD) {
                FlipTapeBit();

#region PULS

                if (currentBlock is PZXFile.PULS_Block) {
                    PZXFile.PULS_Block block = (PZXFile.PULS_Block)currentBlock;
                    repeatCount--;
                    //progressBar1.Value += progressStep;
                    //Need to repeat?
                   // if (repeatCount < block.pulse[pulseCounter].count) {
                     if (repeatCount > 0) {
                       edgeDuration = (block.pulse[pulseCounter].duration);
                        int diff = (int)edgeDuration - tapeTStates;
                        if (diff > 0) {
                            edgeDuration = (uint)diff;
                            tapeTStates = 0;
                        }
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
                else if (currentBlock is PZXFile.DATA_Block) {
                    PZXFile.DATA_Block block = (PZXFile.DATA_Block)currentBlock;

                    //Are we done with pulses for a certain sequence?
                    if (currentBit == 0) {
                        pulseCounter++;
                        if (pulseCounter < block.p0) {
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
                        if (pulseCounter < block.p1) {
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
                else if (currentBlock is PZXFile.PAUS_Block) {
                    isPauseBlockPreproccess = false;
                    NextPZXBlock();
                    return;
                }
/*
 else if (currentBlock is PZXFile.PAUS_Block) {
                    if (isPauseBlockPreproccess) {
                        PZXFile.PAUS_Block block = (PZXFile.PAUS_Block)currentBlock;
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
            } else if (e.EventType == TapeEventType.STOP_TAPE) //stop
            {
                StopTape();
                blockCounter--;

            } else if (e.EventType == TapeEventType.START_TAPE) {
                if (TapeEvent != null)
                    OnTapeEvent(new TapeEventArgs(TapeEventType.START_TAPE));

                NextPZXBlock();
               
            } else if (e.EventType == TapeEventType.FLASH_LOAD) {
                FlashLoad();
            }
        }
    }

}