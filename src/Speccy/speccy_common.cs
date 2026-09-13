using System;
using System.ComponentModel;

namespace SpeccyCommon
{
    public static class SpeccyGlobals {
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


        public const byte JOYSTICK_MOVE_RIGHT = 0x1;
        public const byte JOYSTICK_MOVE_LEFT = 0x2;
        public const byte JOYSTICK_MOVE_DOWN = 0x4;
        public const byte JOYSTICK_MOVE_UP = 0x8;
        public const byte JOYSTICK_BUTTON_1 = 0x10;
        public const byte JOYSTICK_BUTTON_2 = 0x20;
        public const byte JOYSTICK_BUTTON_3 = 0x40;
        public const byte JOYSTICK_BUTTON_4 = 0x80;
    };

    public enum keyCode {
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

    public enum SPECCY_DEVICE {
        ULA_48K,
        ULA_Pentagon,
        ULA_PLUS,
        KEMPSTON_JOYSTICK,
        SINCLAIR1_JOYSTICK,
        SINCLAIR2_JOYSTICK,
        CURSOR_JOYSTICK,
        KEMPSTON_MOUSE,
        AY_3_8912
    };

    //Matches SZX snapshot machine identifiers
    public enum MachineModel {
        [Description("Spectrum 16k")]
        _16k,
        [Description("Spectrum 48k")]
        _48k,
        [Description("Spectrum 128k")]
        _128k,
        [Description("Spectrum Plus 2")]
        _plus2,
        [Description("Spectrum Plus 2A")]
        _plus2A,
        [Description("Spectrum Plus 3")]
        _plus3,
        [Description("Spectrum Plus 3E")]
        _plus3E,
        [Description("Pentagon 128k")]
        _pentagon,
        [Description("Spectrum SE")]
        _SE = 11,
        [Description("Spectrum 48k (NTSC)")]
        _NTSC48k = 15,
        [Description("Spectrum 128ke")]
        _128ke = 16
    };

    //Handy enum for Monitor
    public enum SPECCY_EVENT {
        [Description("A")]
        OPCODE_A,
        [Description("PC")]
        OPCODE_PC,
        [Description("HL")]
        OPCODE_HL,
        [Description("BC")]
        OPCODE_BC,
        [Description("DE")]
        OPCODE_DE,
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
        RE_INTERRUPT,
        [Description("Interrupt")]
        INTERRUPT,
        [Description("Frame Start")]
        FRAME_START,
        [Description("Frame End")]
        FRAME_END,
        [Description("RZX Playback start")]
        RZX_PLAYBACK_START,
        [Description("RZX Frame end")]
        RZX_FRAME_END,
        [Description("RZX port read")]
        RZX_PORT_READ
    }

    //Handy enum for the tape deck
    public enum TapeEventType {
        START_TAPE,
        STOP_TAPE,
        EDGE_LOAD,
        SAVE_TAP,
        CLOSE_TAP,
        FLASH_LOAD,
        NEXT_BLOCK
    }

    //Each RAM bank consists of 2 8k halves.
    public enum RAM_BANK {
        ZERO_LOW,
        ZERO_HIGH,
        ONE_LOW,
        ONE_HIGH,
        TWO_LOW,
        TWO_HIGH,
        THREE_LOW,
        THREE_HIGH,
        FOUR_LOW,
        FOUR_HIGH,
        FIVE_LOW,
        FIVE_HIGH,
        SIX_LOW,
        SIX_HIGH,
        SEVEN_LOW,
        SEVEN_HIGH,
        EIGHT_LOW,
        EIGHT_HIGH
    }

    #region Delegates and args for speccy related events (used by monitor)
    public class MemoryEventArgs : EventArgs {
        private int addr, val;

        public MemoryEventArgs(int _addr, int _val) {
            this.addr = _addr;
            this.val = _val;
        }

        public int Address
        {
            get
            {
                return addr;
            }
        }

        public int Byte
        {
            get
            {
                return val;
            }
        }
    }

    public class RZXFrameEventArgs : EventArgs
    {
        private ushort expectedINs, executedINs;
        private int frameNumber, expectedFetchCount, actualFetchCount;

        public RZXFrameEventArgs(int _frameNumber, int _actualFetchCount, int _expectedFetchCount, ushort _expectedINs, ushort _executedINs) {
            this.frameNumber = _frameNumber;
            this.actualFetchCount = _actualFetchCount;
            this.expectedFetchCount = _expectedFetchCount;
            this.expectedINs = _expectedINs;
            this.executedINs = _executedINs;
        }

        public int FrameNumber
        {
            get
            {
                return frameNumber;
            }
        }

        public int ActualFetchCount
        {
            get
            {
                return actualFetchCount;
            }
        }

        public int ExpectedFetchCount
        {
            get
            {
                return expectedFetchCount;
            }
        }

        public ushort ExpectedINs
        {
            get
            {
                return expectedINs;
            }
        }
        public ushort ExecutedINs
        {
            get
            {
                return executedINs;
            }
        }
    }
    public class OpcodeExecutedEventArgs : EventArgs { }

    public class DiskEventArgs : EventArgs {
        //Lower 4 bits indicate whether a disk is present in drives A,B,C,D
        //bit 4 = motor state
        public int EventType { get; set; }

        public DiskEventArgs(int _type) {
            EventType = _type;
        }
    }

    public class TapeEventArgs : EventArgs {
        private TapeEventType type;

        public TapeEventArgs(TapeEventType _type) {
            type = _type;
        }

        public TapeEventType EventType
        {
            get { return type; }
        }
    }

    public class PortIOEventArgs : EventArgs {
        private int port, val;
        private bool isWrite;

        public PortIOEventArgs(int _port, int _val, bool _write) {
            port = _port;
            val = _val;
            isWrite = _write;
        }

        public int Port
        {
            get { return port; }
        }

        public int Value
        {
            get { return val; }
        }

        public bool IsWrite
        {
            get { return isWrite; }
        }
    }

    public class StateChangeEventArgs : EventArgs {
        private SPECCY_EVENT eventType;

        public StateChangeEventArgs(SPECCY_EVENT _eventType) {
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

    public delegate byte PortReadEventHandler(object sender, ushort port);

    public delegate void PortWriteEventHandler(object sender, ushort port, byte val);

    public delegate void DiskEventHandler(object sender, DiskEventArgs e);

    public delegate void StateChangeEventHandler(object sender, StateChangeEventArgs e);

    public delegate void PopStackEventHandler(object sender, int addr);

    public delegate void PushStackEventHandler(object sender, int addr);

    public delegate void FrameStartEventHandler(object sender);

    public delegate void FrameEndEventHandler(object sender);

    public delegate void RZXPlaybackStartEventHandler(object sender);

    public delegate void RZXFrameEndEventHandler(object sender, RZXFrameEventArgs e);

    public delegate void ULAOutEventHandler();
    #endregion
}
