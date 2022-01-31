//Z80Core.cs
//(c) Arjun Nair 2009

namespace Cpu
{
    using System.Runtime.CompilerServices;
    public delegate byte ReadByteCallback(ushort addr);
    public delegate ushort ReadWordCallback(ushort addr);
    public delegate void WriteByteCallback(ushort addr, byte val);
    public delegate void WriteWordCallback(ushort addr, ushort val);
    public delegate void ContendCallback(int reg, int times, int count);
    public delegate byte InCallback(ushort addr);
    public delegate void OutCallback(ushort addr, byte val);
    public delegate void InstructionFetchCallback();
    public delegate void TapeEdgeDetectionCallback();
    public delegate void TapeEdgeDecACallback();
    public delegate void TapeEdgeCpACallback();
    public struct Z80_Registers {
        public ushort SP, PC, IX, IY;
        public ushort AF_, BC_, DE_, HL_;
        public ushort MemPtr;
        public byte A;
        public byte B, C;
        public byte H, L;
        public byte D, E;
        public byte I, R;
        public byte R_;
        public byte Q;
        public bool modified_F;
        private byte f;

        public byte F
        {
            get { return f; }
            set { 
                f = value;
                modified_F = true;
            }
        }
        public byte IXH
        {
            get { return (byte)(IX >> 8); }
            set { IX = (ushort)((IX & 0x00ff) | (value << 8)); }
        }

        public byte IXL
        {
            get { return (byte)IX; }
            set { IX = (ushort)((IX & 0xff00) | value); }
        }

        public byte IYH
        {
            get { return (byte)(IY >> 8); }
            set { IY = (ushort)((IY & 0x00ff) | (value << 8)); }
        }

        public byte IYL
        {
            get { return (byte)IY; }
            set { IY = (ushort)((IY & 0xff00) | value); }
        }

        public ushort IR
        {
            get { return (ushort)((I << 8) | (R & 0x7f) | (R_ & 0x80)); }
        }

        public ushort AF
        {
            get { return (ushort)((A << 8) | F); }
            set
            {
                A = (byte)((value & 0xff00) >> 8);
                F = (byte)(value & 0x00ff);
            }
        }

        public ushort BC
        {
            get { return (ushort)((B << 8) | C); }
            set
            {
                B = (byte)((value & 0xff00) >> 8);
                C = (byte)(value & 0x00ff);
            }
        }

        public ushort DE
        {
            get { return (ushort)((D << 8) | E); }
            set
            {
                D = (byte)((value & 0xff00) >> 8);
                E = (byte)(value & 0x00ff);
            }
        }

        public ushort HL
        {
            get { return (ushort)((H << 8) | L); }
            set
            {
                H = (byte)((value & 0xff00) >> 8);
                L = (byte)(value & 0x00ff);
            }
        }
    };

    public class Z80
    {
        //Clock state
        public int t_states = 0;                 //opcode t-states

        //Interrupts
        //ref: http://z80.info/zip/z80-interrupts_rewritten.pdf
        public byte interrupt_mode;         //0 = IM0, 1 = IM1, 2 = IM2
        public byte interrupt_count = 0;    //used for re-triggered interrupts

        public bool iff_1, iff_2;           //iff_1 = internal flip flop for interrupts. iff_2 = Temp flip-flop
        public bool is_halted = false;      //true if HALT instruction is being processed  
     
        private int disp = 0;               //used later on to calculate relative jumps and read multiple opcodes 
        private ushort addr = 0;
        private byte val = 0;               //temp storage

        public bool and_32_Or_64 = false;   //tape trap acceleration
        public Z80_Registers regs;

        // -----------------------------------------------------
        // Bit |  7  |  6  |  5  |  4  |  3  |  2  |  1  |  0  |
        // -----------------------------------------------------
        // Flag|  S  |  Z  | F5  |  H  | F3  | P/V |  N  |  C  |
        // -----------------------------------------------------
        private const byte BIT_F_CARRY = 0x01;
        private const byte BIT_F_NEG = 0x02;
        private const byte BIT_F_PARITY = 0x04;
        private const byte BIT_F_3 = 0x08;
        private const byte BIT_F_HALF = 0x10;
        private const byte BIT_F_5 = 0x20;
        private const byte BIT_F_ZERO = 0x40;
        private const byte BIT_F_SIGN = 0x80;

        private const int BIT_11 = 0x800;
        private const int BIT_13 = 0x2000;


        //Tables for parity and flags. Pretty much taken from Fuse.
        private byte[] parity = new byte[256];
        private byte[] halfcarry_add = new byte[] { 0, BIT_F_HALF, BIT_F_HALF, BIT_F_HALF, 0, 0, 0, BIT_F_HALF };
        private byte[] halfcarry_sub = new byte[] { 0, 0, BIT_F_HALF, 0, BIT_F_HALF, 0, BIT_F_HALF, BIT_F_HALF };
        private byte[] overflow_add = new byte[] { 0, 0, 0, BIT_F_PARITY, BIT_F_PARITY, 0, 0, 0 };
        private byte[] overflow_sub = new byte[] { 0, BIT_F_PARITY, 0, 0, 0, 0, BIT_F_PARITY, 0 };
        private byte[] sz53 = new byte[256];
        private byte[] sz53p = new byte[256];

        //Temp placeholders for flag operations.
        private int carry, neg, pv, f3, half, f5, zero, sign;

        //Parity/Overflow flag needs to be rest if there is an interrupt, right after
        //LD A, R or LD A, I
        //Reference: https://worldofspectrum.org/forums/discussion/4971/
        public bool parityBitNeedsReset = false;

        #region 8 bit register access
        /*
        public int A {
            get { return a; }
            set { a = value & 0xff; }
        }

        public int B {
            get { return b; }
            set { b = value & 0xff; }
        }

        public int C {
            get { return c; }
            set { c = value & 0xff; }
        }

        public int H {
            get { return h; }
            set { h = value & 0xff; }
        }

        public int L {
            get { return l; }
            set { l = value & 0xff; }
        }

        public int D {
            get { return d; }
            set { d = value & 0xff; }
        }

        public int E {
            get { return e; }
            set { e = value & 0xff; }
        }

        public int F {
            get { return f; }
            set { f = value & 0xff; }
        }

        public int I {
            get { return i; }
            set { i = value & 0xff; }
        }

        public int R {
            //only the lower 7 bits are affected
            get { return (r_ | (r & 0x7f)); }
            set { r = value & 0x7f; }
        }

        public int R_ {
            set {
                r_ = value & 0x80;  //store Bit 7
                R = value;
            }
        }

        public int IXH {
            get { return (ix >> 8) & 0xff; }
            set { ix = (ix & 0x00ff) | (value << 8);}
        }

        public int IXL {
            get { return (ix & 0xff); }
            set { ix = (ix & 0xff00) | value; }
        }

        public int IYH {
            get { return (iy >> 8) & 0xff; }
            set { iy = (iy & 0x00ff) | (value << 8); }
        }

        public int IYL {
            get { return (iy & 0xff); }
            set { iy = (iy & 0xff00) | value; }
        }

        #endregion 8 bit register access

        #region 16 bit register access

        public int regs.IR {
            get { return (I << 8) | R; }
        }

        public int regs.AF {
            get { return (a << 8) | f; }
            set {
                a = (value & 0xff00) >> 8;
                f = value & 0x00ff;
            }
        }

        public int AF_ {
            get { return regs.AF_; }
            set { regs.AF_ = value; }
        }

        public int HL_ {
            get { return hl_; }
            set { hl_ = value; }
        }

        public int BC_ {
            get { return regs.BC_;}
            set { regs.BC_ = value; }
        }

        public int DE_ {
            get { return regs.DE_; }
            set { regs.DE_ = value; }
        }

        public int regs.BC {
            get { return (b << 8) | c; }
            set {
                a = (value & 0xff00) >> 8;
                f = value & 0x00ff;
            }
        }

        public int regs.DE {
            get { return (d << 8) | e; }
            set
            {
                d = (value & 0xff00) >> 8;
                e = value & 0x00ff;
            }
        }

        public int regs.HL {
            get { return (h << 8) | l; }
            set
            {
                h = (value & 0xff00) >> 8;
                l = value & 0x00ff;
            }
        }

        public int regs.IX {
            get { return ix; }
            set { ix = value & 0xffff; }
        }

        public int regs.IY {
            get { return iy; }
            set { iy = value & 0xffff; }
        }

        public int regs.SP {
            get { return sp; }
            set { sp = value & 0xffff; }
        }

        public int regs.PC {
            get { return pc; }
            set { pc = value & 0xffff; }
        }
        */
#endregion 16 bit register access

        #region Flag manipulation

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCarry(bool val) {
            if (val) {
                regs.F |= BIT_F_CARRY;
            } else {
                regs.F &= (~BIT_F_CARRY) & 0xff;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetNeg(bool val) {
            if (val) {
                regs.F |=  BIT_F_NEG;
            } else {
                regs.F &= (~BIT_F_NEG) & 0xff;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetParity(byte val) {
            if (val > 0) {
                regs.F |= BIT_F_PARITY;
            } else {
                regs.F &= (~BIT_F_PARITY) & 0xff;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetParity(bool val) {
            if (val) {
                regs.F |= BIT_F_PARITY;
            } else {
                regs.F &= (~BIT_F_PARITY) & 0xff;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetHalf(bool val) {
            if (val) {
                regs.F |= BIT_F_HALF;
            } else {
                regs.F &= (~BIT_F_HALF) & 0xff;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetZero(bool val) {
            if(val) {
                regs.F |= BIT_F_ZERO;
            }
            else {
                regs.F &= (~BIT_F_ZERO) & 0xff;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSign(bool val) {
            if(val) {
                regs.F |= BIT_F_SIGN;
            }
            else {
                regs.F &= (~BIT_F_SIGN) & 0xff;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetF3(bool val) {
            if(val) {
                regs.F |= BIT_F_3;
            }
            else {
                regs.F &= (~BIT_F_3) & 0xff;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetF5(bool val) {
            if(val) {
                regs.F |= BIT_F_5;
            }
            else {
                regs.F &= (~BIT_F_5) & 0xff;
            }
        }

        #endregion Flag manipulation

        public ReadByteCallback PeekByte;
        public ReadWordCallback PeekWord;
        public WriteByteCallback PokeByte;
        public WriteWordCallback PokeWord;
        public ContendCallback Contend;
        public InCallback In;
        public OutCallback Out;
        public InstructionFetchCallback InstructionFetchSignal;
        public TapeEdgeDetectionCallback TapeEdgeDetection;
        public TapeEdgeDecACallback TapeEdgeDecA;
        public TapeEdgeCpACallback TapeEdgeCpA;
        public Z80() {
            //Build Parity Table
            /*
            for (int f = 0; f < 256; f++) {
                int val = f;
                bool _parity = false;
                int runningCounter = 0;
                for (int count = 0; count < 8; count++) {
                    if ((val & 0x80) != 0)
                        runningCounter++;
                    val = val << 1;
                }

                if (runningCounter % 2 == 0)
                    _parity = true;

                parity[f] = _parity;
            }
            */
            int i, j, k;
            byte p;

            for (i = 0; i < 256; i++) {
                sz53[i] = (byte)(i & (BIT_F_3 | BIT_F_5 | BIT_F_SIGN));
                j = i; p = 0;
                for (k = 0; k < 8; k++) { p ^= (byte)(j & 1); j >>= 1; }
                parity[i] = (byte)(p > 0 ? 0 : BIT_F_PARITY);
                sz53p[i] = (byte)(sz53[i] | parity[i]);
            }

            sz53[0] |= (byte)BIT_F_ZERO;
            sz53p[0] |= (byte)BIT_F_ZERO;

            //All registers are set to 0xffff during a cold boot
            //http://worldofspectrum.org/forums/showthread.php?t=34574&page=3
            regs.SP = 0xffff;
            regs.IY = 0xffff;
            regs.IX = 0xffff;

            regs.AF = 0xffff;
            regs.BC = 0xffff;
            regs.DE = 0xffff;
            regs.HL = 0xffff;

            exx();
            ex_af_af();

            regs.AF = 0xffff;
            regs.BC = 0xffff;
            regs.DE = 0xffff;
            regs.HL = 0xffff;

            regs.PC = 0;
            interrupt_mode = 0;
            regs.I = 0;
            regs.R = 0;
            regs.R_ = 0;
            regs.MemPtr = 0;
            iff_1 = false;
            iff_2 = false;
        }

        public void HardReset() {
            //RESET behaviour
            //http://worldofspectrum.org/forums/showthread.php?t=34574&page=3
            regs.SP = 0xffff;
            regs.IY = 0xffff;
            regs.IX = 0xffff;

            regs.AF = 0xffff;
            regs.BC = 0xffff;
            regs.DE = 0xffff;
            regs.HL = 0xffff;

            exx();
            ex_af_af();

            regs.AF = 0xffff;
            regs.BC = 0xffff;
            regs.DE = 0xffff;
            regs.HL = 0xffff;
            regs.modified_F = false;
            regs.Q = 0;

            regs.PC = 0;
            interrupt_mode = 0;
            regs.I = 0;
            regs.R = 0;
            regs.R_ = 0;
            regs.MemPtr = 0;
            iff_1 = false;
            iff_2 = false;
        }

        public void UserReset() {
            //Special/User reset
            //http://worldofspectrum.org/forums/showthread.php?t=34574&page=3
            regs.PC = 0;
            interrupt_mode = 0;
            interrupt_count = 0;
            regs.I = 0;
            regs.R = 0;
            regs.R_ = 0;
            regs.MemPtr = 0;
            iff_1 = false;
            iff_2 = false;
            regs.modified_F = false;
            regs.Q = 0;
        }

        public void Interrupt() {
            regs.R++;
            regs.modified_F = false;
            regs.Q = 0;

            //Disable interrupts
            iff_1 = false;
            iff_2 = false;

            if (is_halted) {
                is_halted = false;
            }
            
            if (interrupt_mode < 2) //IM0 = IM1 for our purpose
            {
                Trigger_IM_1();
            }
            else    //IM 2
            {
                Trigger_IM_2();
            }
        }

        public void exx() {
            ushort temp;
            temp = regs.HL_;
            regs.HL_ = regs.HL;
            regs.HL = temp;

            temp = regs.DE_;
            regs.DE_ = regs.DE;
            regs.DE = temp;

            temp = regs.BC_;
            regs.BC_ = regs.BC;
            regs.BC = temp;
        }

        public void ex_af_af() {
            ushort temp = regs.AF_;
            regs.AF_ = regs.AF;
            regs.AF = temp;
        }

        //Reads next instruction from address pointed to by regs.PC
        public int FetchInstruction()
        {
            regs.R++;
            InstructionFetchSignal(); //Raise the instruction fetch signal for debugging, rzx etc...
            byte b = PeekByte(regs.PC);

            if (!is_halted)
                regs.PC = (ushort)(regs.PC + 1);

            t_states++; //effectively, totalTStates + 4 because PeekByte does the other 3
            return b;
        }

        //IM 0, IM 1, IM 2 and  NMI described here:
        //https://www.worldofspectrum.org/faq/reference/z80reference.htm
        public void Trigger_IM_1() {
            //Perform a RST 0x038
            PushStack(regs.PC);
            t_states += 7;
            regs.PC = 0x38;
            regs.MemPtr = regs.PC;
        }

        public void Trigger_IM_2() {
            //In reality the I is paired with whatever's on the databus to produce
            //the 16 bit pointer, but 0xff works just fine because that's the speccy's
            //default state when no peripherals are attached (see ref above).
            ushort ptr = (ushort)((regs.I << 8) | 0xff);
            PushStack(regs.PC);
            regs.PC = PeekWord(ptr);
            t_states += 7;
            regs.MemPtr = regs.PC;
        }

        public void NMI() {
            iff_1 = false;
            is_halted = false;
            PushStack(regs.PC);
            t_states += 5;
            regs.PC = 0x66;
            regs.MemPtr = regs.PC;
        }

        public byte Inc(byte reg) {
            /*
            SetParity((reg == 0x7f));   //reg = 127? We're gonna overflow on inc!
            SetNeg(false);               //Negative is always reset (0)
            SetHalf((((reg & 0x0f) + 1) & BIT_F_HALF) != 0);

            reg = (reg + 1) & 0xff;
            SetF3((reg & BIT_F_3) != 0);
            SetF5((reg & BIT_F_5) != 0);
            SetZero(reg == 0);
            SetSign((reg & BIT_F_SIGN) != 0);
            return reg;*/

            reg++;
            regs.F = (byte)(( regs.F & BIT_F_CARRY ) | ( (reg == 0x80) ? BIT_F_PARITY : 0 ) | 
                ((reg & 0x0f) > 0 ? 0 : BIT_F_HALF ));
            //reg &= 0xff;
            regs.F |= sz53[reg];
            return reg;
        }

        public byte Dec(byte reg) {
            /*
            SetNeg(true);                //Negative is always set (1)
            SetParity((reg == 0x80));   //reg = -128? We're gonna overflow on dec!
            SetHalf((((reg & 0x0f) - 1) & BIT_F_HALF) != 0);

            reg = (reg - 1) & 0xff;
            SetF3((reg & BIT_F_3) != 0);
            SetF5((reg & BIT_F_5) != 0);
            SetZero(reg == 0);
            SetSign((reg & BIT_F_SIGN) != 0);

            return reg;*/
             regs.F = (byte)(( regs.F & BIT_F_CARRY ) | ( (reg & 0x0f) > 0 ? 0 : BIT_F_HALF ) | BIT_F_NEG);
             reg--;
             regs.F |= (byte)(((reg) == 0x7f ? BIT_F_PARITY : 0));
             //reg &= 0xff;
             regs.F |= sz53[reg];
             return reg;
        }

        //16 bit addition (no carry)
        public ushort Add_RR(ushort rr1, ushort rr2) {
            /*
            SetNeg(false);
            //SetHalf(((((rr1 >> 8) & 0x0f) + ((rr2 >> 8) & 0x0f)) & BIT_F_HALF) != 0); //Set from high byte of operands
            SetHalf((((rr1 & 0xfff) + (rr2 & 0xfff)) & 0x1000) != 0); //Set from high byte of operands
            rr1 += rr2;

            SetCarry((rr1 & 0x10000) != 0);
            SetF3(((rr1 >> 8) & BIT_F_3) != 0);
            SetF5(((rr1 >> 8) & BIT_F_5) != 0);
            return (rr1 & 0xffff);*/
            int add16temp = (rr1) + (rr2);
            byte lookup = (byte)((((rr1) & 0x0800 ) >> 11 ) | ( (  (rr2) & 0x0800 ) >> 10 ) | ( ( add16temp & 0x0800 ) >>  9));
            rr1 = (ushort)add16temp;
            regs.F = (byte)(( regs.F & ( BIT_F_PARITY | BIT_F_ZERO | BIT_F_SIGN ) ) | ((add16temp & 0x10000) > 0 ? BIT_F_CARRY : 0 )|
                ( ( add16temp >> 8 ) & ( BIT_F_3 | BIT_F_5 ) ) | halfcarry_add[lookup]);
            return rr1;
        }

        //8 bit add to accumulator (no carry)
        public void Add_R(byte reg) {
            /*
            SetNeg(false);
            SetHalf((((A & 0x0f) + (reg & 0x0f)) & BIT_F_HALF) != 0);

            int ans = (A + reg) & 0xff;
            SetCarry(((A + reg) & 0x100) != 0);
            SetParity(((A ^ ~reg) & (A ^ ans) & 0x80) != 0);
            SetSign((ans & BIT_F_SIGN) != 0);
            SetZero(ans == 0);
            SetHalf((((A & 0x0f) + (reg & 0x0f)) & BIT_F_HALF) != 0);
            SetF3((ans & BIT_F_3) != 0);
            SetF5((ans & BIT_F_5) != 0);
            A = ans;
             * */
             int addtemp = regs.A + reg;
             byte lookup = (byte)(((regs.A & 0x88 ) >> 3 ) | (((reg) & 0x88 ) >> 2 ) | ( ( addtemp & 0x88 ) >> 1 ));
             regs.A= (byte)(addtemp & 0xff);
             regs.F = (byte)(((addtemp & 0x100) > 0 ? BIT_F_CARRY : 0 ) | halfcarry_add[lookup & 0x07] | 
                overflow_add[lookup >> 4] | sz53[regs.A]);
        }

        //Add with carry into accumulator
        public void Adc_R(byte reg) {
            /*
            SetNeg(false);
            int fc = ((F & BIT_F_CARRY) != 0 ? 1 : 0);
            SetHalf((((A & 0x0f) + (reg & 0x0f) + fc) & BIT_F_HALF) != 0);
            int ans = (A + reg + fc) & 0xff;

            SetCarry(((A + reg + fc) & 0x100) != 0);

            SetParity(((A ^ ~reg) & (A ^ ans) & 0x80) != 0);
            SetSign((ans & BIT_F_SIGN) != 0);
            SetZero(ans == 0);
            SetF3((ans & BIT_F_3) != 0);
            SetF5((ans & BIT_F_5) != 0);
            A = ans;*/
            int adctemp = regs.A + (reg) + ( regs.F & BIT_F_CARRY ); 
            byte lookup = (byte)(((regs.A & 0x88) >> 3) | (((reg) & 0x88)>>2) | ((adctemp & 0x88)>> 1)); 
            regs.A= (byte)(adctemp & 0xff);
            regs.F = (byte)(((adctemp & 0x100) > 0 ? BIT_F_CARRY : 0 ) | halfcarry_add[lookup & 0x07] | 
                overflow_add[lookup >> 4] | sz53[regs.A]);
        }

        //Add with carry into regs.HL
        public void Adc_RR(ushort reg) {
            /*
            SetNeg(false);
            int fc = ((F & BIT_F_CARRY) != 0 ? 1 : 0);
            int ans = (regs.HL + reg + fc) & 0xffff;
            SetCarry(((regs.HL + reg + fc) & 0x10000) != 0);
            SetHalf((((regs.HL & 0xfff) + (reg & 0xfff) + fc) & 0x1000) != 0); //Set from high byte of operands
            //SetHalf(((((regs.HL >> 8) & 0x0f + (reg >> 8) & 0x0f) + fc) & BIT_F_HALF) != 0); //Set from high byte of operands
            SetParity(((regs.HL ^ ~reg) & (regs.HL ^ ans) & 0x8000) != 0);
            SetSign((ans & (BIT_F_SIGN << 8)) != 0);
            SetZero(ans == 0);
            SetF3(((ans >> 8) & BIT_F_3) != 0);
            SetF5(((ans >> 8) & BIT_F_5) != 0);
            regs.HL = ans;*/
            int add16temp = regs.HL + (reg) + ( regs.F & BIT_F_CARRY );
            byte lookup = (byte)(((regs.HL & 0x8800 ) >> 11 ) | (((reg) & 0x8800 ) >> 10 ) | ( ( add16temp & 0x8800 ) >>  9 ));
            regs.HL = (ushort)(add16temp);
            regs.F = (byte)(((add16temp & 0x10000) > 0? BIT_F_CARRY : 0 ) | overflow_add[lookup >> 4] | 
                ( regs.H & ( BIT_F_3 | BIT_F_5 | BIT_F_SIGN ) ) |
                halfcarry_add[lookup&0x07] | ( regs.HL > 0? 0 : BIT_F_ZERO ));
        }

        //8 bit subtract to accumulator (no carry)
        public void Sub_R(byte reg) {
            /*
            SetNeg(true);

            int ans = (A - reg) & 0xff;
            SetCarry(((A - reg) & 0x100) != 0);
            SetF3((ans & BIT_F_3) != 0);
            SetF5((ans & BIT_F_5) != 0);
            SetParity(((A ^ reg) & (A ^ ans) & 0x80) != 0);
            SetSign((ans & BIT_F_SIGN) != 0);
            SetHalf((((A & 0x0f) - (reg & 0x0f)) & BIT_F_HALF) != 0);
            SetZero(ans == 0);
            SetNeg(true);

            A = ans;*/
            int subtemp = regs.A - (reg);
            byte lookup = (byte)(((regs.A & 0x88 ) >> 3 ) | ( (reg & 0x88 ) >> 2 ) | ((subtemp & 0x88 ) >> 1 )); 
            regs.A= (byte)subtemp;
            regs.F = (byte)(((subtemp & 0x100) > 0 ? BIT_F_CARRY : 0 ) | BIT_F_NEG | halfcarry_sub[lookup & 0x07] | 
                overflow_sub[lookup >> 4] | sz53[regs.A]);
        }

        //8 bit subtract from accumulator with carry (SBC A, r)
        public void Sbc_R(byte reg) {
           /* SetNeg(true);
            int fc = ((F & BIT_F_CARRY) != 0 ? 1 : 0);

            int ans = (A - reg - fc) & 0xff;
            SetCarry(((A - reg - fc) & 0x100) != 0);
            SetParity(((A ^ reg) & (A ^ ans) & 0x80) != 0);
            SetSign((ans & BIT_F_SIGN) != 0);
            SetHalf((((A & 0x0f) - (reg & 0x0f) - fc) & BIT_F_HALF) != 0);
            SetZero(ans == 0);
            SetF3((ans & BIT_F_3) != 0);
            SetF5((ans & BIT_F_5) != 0);
            A = ans;*/
            int sbctemp = regs.A - (reg) - ( regs.F & BIT_F_CARRY );
            byte lookup = (byte)(((regs.A & 0x88 ) >> 3 ) |( ( (reg) & 0x88 ) >> 2 ) |( ( sbctemp & 0x88 ) >> 1 ));
            regs.A= (byte)sbctemp;
            regs.F = (byte)(((sbctemp & 0x100)>0 ? BIT_F_CARRY : 0 ) | BIT_F_NEG |halfcarry_sub[lookup & 0x07] | 
                overflow_sub[lookup >> 4] | sz53[regs.A]);
        }

        //16 bit subtract from regs.HL with carry
        public void Sbc_RR(ushort reg) {
            /*
            SetNeg(true);
            int fc = ((F & BIT_F_CARRY) != 0 ? 1 : 0);

            SetHalf((((regs.HL & 0xfff) - (reg & 0xfff) - fc) & 0x1000) != 0); //Set from high byte of operands
            // SetHalf((((((regs.HL >> 8) & 0x0f) - ((reg >> 8) & 0x0f)) - fc) & BIT_F_HALF) != 0); //Set from high byte of operands

            int ans = (regs.HL - reg - fc) & 0xffff;
            SetCarry(((regs.HL - reg - fc) & 0x10000) != 0);
            SetParity(((regs.HL ^ reg) & (regs.HL ^ ans) & 0x8000) != 0);
            SetSign((ans & (BIT_F_SIGN << 8)) != 0);
            SetZero(ans == 0);
            SetF3(((ans >> 8) & BIT_F_3) != 0);
            SetF5(((ans >> 8) & BIT_F_5) != 0);

            regs.HL = ans;*/
            int sub16temp = regs.HL - (reg) - (regs.F & BIT_F_CARRY);
            byte lookup = (byte)(((regs.HL & 0x8800 ) >> 11 ) | ( ((reg) & 0x8800 ) >> 10 ) | (( sub16temp & 0x8800 ) >>  9 ));
            regs.HL = (ushort)sub16temp;
            regs.F = (byte)(((sub16temp & 0x10000) > 0 ? BIT_F_CARRY : 0 ) | BIT_F_NEG | overflow_sub[lookup >> 4] | 
                (regs.H & ( BIT_F_3 | BIT_F_5 | BIT_F_SIGN ) ) |halfcarry_sub[lookup&0x07] |( regs.HL > 0 ? 0 : BIT_F_ZERO));
        }

        //Comparison with accumulator
        public void Cp_R(byte reg) {
            /*
            SetNeg(true);

            int result = A - reg;
            int ans = result & 0xff;
            SetF3((reg & BIT_F_3) != 0);
            SetF5((reg & BIT_F_5) != 0);
            SetHalf((((A & 0x0f) - (reg & 0x0f)) & BIT_F_HALF) != 0);
            SetParity(((A ^ reg) & (A ^ ans) & 0x80) != 0);
            SetSign((ans & BIT_F_SIGN) != 0);
            SetZero(ans == 0);
            SetCarry((result & 0x100) != 0);*/
            int cptemp = regs.A - reg;
            byte lookup = (byte)(((regs.A & 0x88 ) >> 3 ) | ( ( (reg) & 0x88 ) >> 2 ) | ( (cptemp & 0x88 ) >> 1 ));
            regs.F = (byte)(((cptemp & 0x100) > 0 ? BIT_F_CARRY : ( cptemp > 0 ? 0 : BIT_F_ZERO ) ) | BIT_F_NEG |
                halfcarry_sub[lookup & 0x07] | overflow_sub[lookup >> 4] |( reg & ( BIT_F_3 | BIT_F_5 ) ) | ( cptemp & BIT_F_SIGN ));
        }

        //AND with accumulator
        public void And_R(byte reg) {
            /*
            SetCarry(false);
            SetNeg(false);

            int ans = A & reg;
            SetSign((ans & BIT_F_SIGN) != 0);
            SetHalf(true);
            SetZero(ans == 0);
            //SetParity(GetParity(ans));
            SetParity(parity[ans]);
            SetF3((ans & BIT_F_3) != 0);
            SetF5((ans & BIT_F_5) != 0);
            A = ans;*/
            regs.A &= reg;
            regs.F = (byte)(BIT_F_HALF | sz53p[regs.A]);
            
            //For ROM TRAP
            if (((reg & (~96)) == 0) &&  (reg != 96))
                and_32_Or_64 = true;
        }

        //XOR with accumulator
        public void Xor_R(byte reg) {
            /*
            SetCarry(false);
            SetNeg(false);

            int ans = (A ^ reg) & 0xff;
            SetSign((ans & BIT_F_SIGN) != 0);
            SetHalf(false);
            SetZero(ans == 0);
            // SetParity(GetParity(ans));
            SetParity(parity[ans]);
            SetF3((ans & BIT_F_3) != 0);
            SetF5((ans & BIT_F_5) != 0);
            A = ans;*/
            regs.A = (byte)( regs.A ^ reg);
            regs.F = sz53p[regs.A];
        }

        //OR with accumulator
        public void Or_R(byte reg) {
            /*
            SetCarry(false);
            SetNeg(false);

            int ans = A | reg;
            SetSign((ans & BIT_F_SIGN) != 0);
            SetHalf(false);
            SetZero(ans == 0);
            //SetParity(GetParity(ans));
            SetParity(parity[ans]);
            SetF3((ans & BIT_F_3) != 0);
            SetF5((ans & BIT_F_5) != 0);
            A = ans;*/
            regs.A |= reg;
            regs.F = sz53p[regs.A];
        }

        //Rotate left with carry register (RLC r)
        public byte Rlc_R(byte reg) {
            /*
            int msb = reg & BIT_F_SIGN;

            if (msb != 0) {
                reg = ((reg << 1) | 0x01) & 0xff;
            } else
                reg = (reg << 1) & 0xff;

            SetCarry(msb != 0);
            SetHalf(false);
            SetNeg(false);
            //SetParity(GetParity(reg));
            SetParity(parity[reg]);
            SetZero(reg == 0);
            SetSign((reg & BIT_F_SIGN) != 0);
            SetF3((reg & BIT_F_3) != 0);
            SetF5((reg & BIT_F_5) != 0);
            return reg;*/
            reg = (byte)((reg << 1 ) | (reg >>7 ));
            regs.F = (byte)((reg & BIT_F_CARRY) | sz53p[reg]);
            return reg;
        }

        //Rotate right with carry register (RLC r)
        public byte Rrc_R(byte reg) {
            /*
            int lsb = reg & BIT_F_CARRY; //save the lsb bit

            if (lsb != 0) {
                reg = (reg >> 1) | 0x80;
            } else
                reg = reg >> 1;

            SetCarry(lsb != 0);
            SetHalf(false);
            SetNeg(false);
            SetF3((reg & BIT_F_3) != 0);
            SetF5((reg & BIT_F_5) != 0);
            //SetParity(GetParity(reg));
            SetParity(parity[reg]);
            SetZero(reg == 0);
            SetSign((reg & BIT_F_SIGN) != 0);

            return reg;*/
            regs.F = (byte)(reg & BIT_F_CARRY);
            reg = (byte)((reg >>1 ) | (reg << 7));
            regs.F |= sz53p[reg];
            return reg;
        }

        //Rotate left register (RL r)
        public byte Rl_R(byte reg) {
            /*
            bool rc = (reg & BIT_F_SIGN) != 0;
            int msb = F & BIT_F_CARRY; //save the msb bit

            if (msb != 0) {
                reg = ((reg << 1) | 0x01) & 0xff;
            } else {
                reg = (reg << 1) & 0xff;
            }

            SetCarry(rc);
            SetHalf(false);
            SetNeg(false);
            //SetParity(GetParity(reg));
            SetParity(parity[reg]);
            SetZero(reg == 0);
            SetSign((reg & BIT_F_SIGN) != 0);
            SetF3((reg & BIT_F_3) != 0);
            SetF5((reg & BIT_F_5) != 0);
            return reg;*/
            byte rltemp = reg;
            reg = (byte)((reg << 1) | (regs.F & BIT_F_CARRY));
            regs.F = (byte)(( rltemp >> 7 ) | sz53p[reg]);
            return reg;
        }

        //Rotate right register (RL r)
        public byte Rr_R(byte reg) {
            /*
            bool rc = (reg & BIT_F_CARRY) != 0;
            int lsb = F & BIT_F_CARRY; //save the lsb bit

            if (lsb != 0) {
                reg = (reg >> 1) | 0x80;
            } else
                reg = reg >> 1;

            SetCarry(rc);
            SetHalf(false);
            SetNeg(false);
            // SetParity(GetParity(reg));
            SetParity(parity[reg]);
            SetZero(reg == 0);
            SetSign((reg & BIT_F_SIGN) != 0);
            SetF3((reg & BIT_F_3) != 0);
            SetF5((reg & BIT_F_5) != 0);
            return reg;*/
            byte rrtemp = reg;
            reg = (byte)((reg >> 1 ) | ( regs.F << 7 ));
            regs.F = (byte)(( rrtemp & BIT_F_CARRY ) | sz53p[reg]);
            return reg;
        }

        //Shift left arithmetic register (SLA r)
        public byte Sla_R(byte reg) {
            /*
            int msb = reg & BIT_F_SIGN; //save the msb bit

            reg = (reg << 1) & 0xff;

            SetCarry(msb != 0);
            SetHalf(false);
            SetNeg(false);
            //SetParity(GetParity(reg));
            SetParity(parity[reg]);
            SetZero(reg == 0);
            SetSign((reg & BIT_F_SIGN) != 0);
            SetF3((reg & BIT_F_3) != 0);
            SetF5((reg & BIT_F_5) != 0);
            return reg;*/
            regs.F = (byte)(reg >> 7);
            reg = (byte)(reg <<  1);
            regs.F |= sz53p[reg];
            return reg;
        }

        //Shift right arithmetic register (SRA r)
        public byte Sra_R(byte reg) {
            /*
            int lsb = reg & BIT_F_CARRY; //save the lsb bit
            reg = (reg >> 1) | (reg & BIT_F_SIGN);

            SetCarry(lsb != 0);
            SetHalf(false);
            SetNeg(false);
            // SetParity(GetParity(reg));
            SetParity(parity[reg]);
            SetZero(reg == 0);
            SetSign((reg & 0x80) != 0);
            SetF3((reg & BIT_F_3) != 0);
            SetF5((reg & BIT_F_5) != 0);
            return reg;*/
            regs.F = (byte)(reg & BIT_F_CARRY);
            reg = (byte)((reg & 0x80 ) | (reg >> 1 ));
            regs.F |= sz53p[reg];
            return reg;
        }

        //Shift left logical register (SLL r)
        public byte Sll_R(byte reg) {
            /*
            int msb = reg & BIT_F_SIGN; //save the msb bit
            reg = reg << 1;
            reg = (reg | 0x01) & 0xff;

            SetCarry(msb != 0);
            SetHalf(false);
            SetNeg(false);
            // SetParity(GetParity(reg));
            SetParity(parity[reg]);
            SetZero(reg == 0);
            SetSign((reg & BIT_F_SIGN) != 0);
            SetF3((reg & BIT_F_3) != 0);
            SetF5((reg & BIT_F_5) != 0);
            return reg;*/
            regs.F = (byte)(reg >> 7);
            reg = (byte)(( reg << 1 ) | 0x01);
            regs.F |= sz53p[reg];
            return reg;
        }

        //Shift right logical register (SRL r)
        public byte Srl_R(byte reg) {
            /*
            int lsb = reg & BIT_F_CARRY; //save the lsb bit
            reg = reg >> 1;

            SetCarry(lsb != 0);
            SetHalf(false);
            SetNeg(false);
            //SetParity(GetParity(reg));
            SetParity(parity[reg]);
            SetZero(reg == 0);
            SetSign((reg & BIT_F_SIGN) != 0);
            SetF3((reg & BIT_F_3) != 0);
            SetF5((reg & BIT_F_5) != 0);
            return reg;*/
            regs.F = (byte)(reg & BIT_F_CARRY);
            reg = (byte)(reg >> 1);
            regs.F |= sz53p[reg];
            return reg;
        }

        //Bit test operation (BIT b, r)
        public void Bit_R(int b, byte val) {
            /*
            bool bitset = ((reg & (1 << b)) != 0);  //true if bit is set
            SetZero(!bitset);                       //true if bit is not set, false if bit is set
            SetParity(!bitset);                     //copy of Z
            SetNeg(false);
            SetHalf(true);
            SetSign((b == 7) ? bitset : false);
            SetF3((reg & BIT_F_3) != 0);
            SetF5((reg & BIT_F_5) != 0);*/
            regs.F = (byte)(( regs.F & BIT_F_CARRY ) | BIT_F_HALF | ( val & ( BIT_F_3 | BIT_F_5 ) ));
            if( !((val & ( 0x01 << (b))) > 0)) regs.F |= BIT_F_PARITY | BIT_F_ZERO;
            if( (b == 7) && ((val & 0x80) > 0)) regs.F |= BIT_F_SIGN; 
        }

        public void Bit_MemPtr(int b, byte val) {
            regs.F = (byte)((regs.F & BIT_F_CARRY) | BIT_F_HALF | ((regs.MemPtr >> 8) & (BIT_F_3 | BIT_F_5)));
            if (!((val & (0x01 << (b))) > 0)) regs.F |= BIT_F_PARITY | BIT_F_ZERO;
            if ((b == 7) && ((val & 0x80) > 0)) regs.F |= BIT_F_SIGN;
        }

        //Reset bit operation (RES b, r)
        public byte Res_R(int b, byte reg) {
            reg = (byte)(reg & ~(1 << b));
            return reg;
        }

        //Set bit operation (SET b, r)
        public byte Set_R(int b, byte reg) {
            reg = (byte)(reg | (1 << b));
            return reg;
        }

        //Decimal Adjust Accumulator (DAA)
        public void DAA() {
            byte ans = regs.A;
            byte incr = 0;
            bool carry = (regs.F & BIT_F_CARRY) != 0;

            if (((regs.F & BIT_F_HALF) != 0) || ((ans & 0x0f) > 0x09)) {
                incr |= 0x06;
            }

            if (carry || (ans > 0x9f) || ((ans > 0x8f) && ((ans & 0x0f) > 0x09))) {
                incr |= 0x60;
            }

            if (ans > 0x99) {
                carry = true;
            }

            if ((regs.F & BIT_F_NEG) != 0) {
                Sub_R(incr);
            } else {
                Add_R(incr);
            }

            ans = regs.A;

            SetCarry(carry);
            SetParity(parity[ans]);
        }

        //Returns parity of a number (true if there are even numbers of 1, false otherwise)
        //Superseded by the table method.
        public bool GetParity(byte val) {
            bool parity = false;
            int runningCounter = 0;
            for (int count = 0; count < 8; count++) {
                if ((val & 0x80) != 0)
                    runningCounter++;
                val <<= 1;
            }

            if (runningCounter % 2 == 0)
                parity = true;

            return parity;
        }

        public int GetDisplacement(byte val) {
            int res = ((128 ^ val) - 128);
            return res;
        }

        public byte In_BC(){
            byte result = In(regs.BC);
            /*
            SetNeg(false);
            SetParity(parity[result]);
            SetSign((result & F_SIGN) != 0);
            SetZero(result == 0);
            SetHalf(false);
            SetF3((result & F_3) != 0);
            SetF5((result & F_5) != 0);

            return result;*/
            regs.F = (byte)((regs.F & BIT_F_CARRY) | sz53p[result]);
            return result;
        }

        //The stack is pushed in low byte, high byte form
        public void PushStack(ushort val) {
            regs.SP -= 2;
            PokeByte((ushort)(regs.SP + 1), (byte)(val >> 8));
            PokeByte(regs.SP, (byte)(val & 0xff));
            //if (PushStackEvent != null)
            //    OnPushStackEvent(regs.SP, val);
        }

        public ushort PopStack() {
            ushort val = (ushort)((PeekByte(regs.SP)) | (PeekByte((ushort)(regs.SP + 1)) << 8));
            regs.SP += 2;
            //if (PopStackEvent != null)
            //    OnPopStackEvent(val);
            return val;
        }

        public void Step() {
            int opcode = FetchInstruction();
            regs.modified_F = false;
            Execute(opcode);
            regs.Q = (byte)((regs.modified_F ? 0 : regs.F) & (BIT_F_3 | BIT_F_5));
        }

        //Executes a single opcode
        public void Execute(int opcode) {
            if (is_halted)
                return;
            //disp = 0;
            //Massive switch-case to decode the instructions!
            switch(opcode) {

                #region NOP

                case 0x00: //NOP
                           // Log("NOP");
                break;

                #endregion NOP

                #region 16 bit load operations (LD rr, nn)
                /** LD rr, nn (excluding DD prefix) **/
                case 0x01: //LD regs.BC, nn
                regs.BC = PeekWord(regs.PC);
                // Log(String.Format("LD regs.BC, {0,-6:X}", regs.BC));
                regs.PC += 2;
                break;

                case 0x11:  //LD regs.DE, nn
                regs.DE = PeekWord(regs.PC);
                // Log(String.Format("LD regs.DE, {0,-6:X}", regs.DE));
                regs.PC += 2;
                break;

                case 0x21:  //LD regs.HL, nn
                regs.HL = PeekWord(regs.PC);
                // Log(String.Format("LD regs.HL, {0,-6:X}", regs.HL));
                regs.PC += 2;
                break;

                case 0x2A:  //LD regs.HL, (nn)
                {
                    ushort w = PeekWord(regs.PC);
                    regs.HL = PeekWord(w);
                    // Log(String.Format("LD regs.HL, ({0,-6:X})", disp));
                    regs.PC += 2;
                    regs.MemPtr = (ushort)(w + 1);
                    break;
                }
                case 0x31:  //LD regs.SP, nn
                regs.SP = PeekWord(regs.PC);
                // Log(String.Format("LD regs.SP, {0,-6:X}", regs.SP));
                regs.PC += 2;
                break;

                case 0xF9:  //LD regs.SP, regs.HL
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 2;
                            //else
                Contend(regs.IR, 1, 2);
                // Log("LD regs.SP, regs.HL");
                regs.SP = regs.HL;
                break;
                #endregion

                #region 16 bit increments (INC rr)
                /** INC rr **/
                case 0x03:  //INC regs.BC
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 2;
                            //else
                Contend(regs.IR, 1, 2);
                // Log("INC regs.BC");
                regs.BC++;
                break;

                case 0x13:  //INC regs.DE
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 2;
                            //else
                Contend(regs.IR, 1, 2);
                // Log("INC regs.DE");
                regs.DE++;
                break;

                case 0x23:  //INC regs.HL
                            // if (model == MachineModel._plus3)
                            //     totalTStates += 2;
                            // else
                Contend(regs.IR, 1, 2);
                // Log("INC regs.HL");
                regs.HL++;
                break;

                case 0x33:  //INC regs.SP
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 2;
                            //else
                Contend(regs.IR, 1, 2);
                // Log("INC regs.SP");
                regs.SP++;
                break;
                #endregion INC rr

                #region 8 bit increments (INC r)
                /** INC r + INC (regs.HL) **/
                case 0x04:  //INC B
                regs.B = Inc(regs.B);
                // Log("INC B");
                break;

                case 0x0C:  //INC C
                regs.C = Inc(regs.C);
                // Log("INC C");
                break;

                case 0x14:  //INC D
                regs.D = Inc(regs.D);
                // Log("INC D");
                break;

                case 0x1C:  //INC E
                regs.E = Inc(regs.E);
                // Log("INC E");
                break;

                case 0x24:  //INC H
                regs.H = Inc(regs.H);
                // Log("INC H");
                break;

                case 0x2C:  //INC L
                regs.L = Inc(regs.L);
                // Log("INC L");
                break;

                case 0x34:  //INC (regs.HL)
                val = PeekByte(regs.HL);
                val = Inc(val);
                Contend(regs.HL, 1, 1);
                PokeByte(regs.HL, val);
                // Log("INC (regs.HL)");
                break;

                case 0x3C:  //INC A
                            // Log("INC A");
                regs.A = Inc(regs.A);
                break;
                #endregion

                #region 8 bit decrement (DEC r)
                /** DEC r + DEC (regs.HL)**/
                case 0x05: //DEC B
                           // Log("DEC B");
                regs.B = Dec(regs.B);
                break;

                case 0x0D:    //DEC C
                              // Log("DEC C");
                regs.C = Dec(regs.C);
                break;

                case 0x15:  //DEC D
                            // Log("DEC D");
                regs.D = Dec(regs.D);
                break;

                case 0x1D:  //DEC E
                            // Log("DEC E");
                regs.E = Dec(regs.E);
                break;

                case 0x25:  //DEC H
                            // Log("DEC H");
                regs.H = Dec(regs.H);
                break;

                case 0x2D:  //DEC L
                            // Log("DEC L");
                regs.L = Dec(regs.L);
                break;

                case 0x35:  //DEC (regs.HL)
                            // Log("DEC (regs.HL)");
                            //val = PeekByte(regs.HL);
                val = Dec(PeekByte(regs.HL));
                Contend(regs.HL, 1, 1);
                PokeByte(regs.HL, val);
                break;

                case 0x3D:  //DEC A
                            // Log("DEC A");
                regs.A = Dec(regs.A);
             
                TapeEdgeDecA?.Invoke();
                break;
                #endregion

                #region 16 bit decrements
                /** DEC rr **/
                case 0x0B:  //DEC regs.BC
                            // Log("DEC regs.BC");
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 2;
                            //else
                Contend(regs.IR, 1, 2);
                regs.BC--;
                break;

                case 0x1B:  //DEC regs.DE
                            // Log("DEC regs.DE");
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 2;
                            //else
                Contend(regs.IR, 1, 2);
                regs.DE--;
                break;

                case 0x2B:  //DEC regs.HL
                            // Log("DEC regs.HL");
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 2;
                            //else
                Contend(regs.IR, 1, 2);
                regs.HL--;
                break;

                case 0x3B:  //DEC regs.SP
                            // Log("DEC regs.SP");
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 2;
                            //else
                Contend(regs.IR, 1, 2);
                regs.SP--;
                break;
                #endregion

                #region Immediate load operations (LD (nn), r)
                /** LD (rr), r + LD (nn), regs.HL  + LD (nn), A **/
                case 0x02: //LD (regs.BC), A
                           // Log("LD (regs.BC), A");
                PokeByte(regs.BC, regs.A);
                regs.MemPtr = (ushort)(((regs.BC + 1) & 0xff) | (regs.A << 8));
                break;

                case 0x12:  //LD (regs.DE), A
                            // Log("LD (regs.DE), A");
                PokeByte(regs.DE, regs.A);
                regs.MemPtr = (ushort)(((regs.DE + 1) & 0xff) | (regs.A << 8));
                break;

                case 0x22:  //LD (nn), regs.HL
                addr = PeekWord(regs.PC);
                // Log(String.Format("LD ({0,-6:X}), regs.HL", addr));

                PokeWord(addr, regs.HL);
                regs.MemPtr = (ushort)(addr + 1);
                regs.PC += 2;
                break;

                case 0x32:  //LD (nn), A
                addr = PeekWord(regs.PC);
                // Log(String.Format("LD ({0,-6:X}), A", addr));

                PokeByte(addr, regs.A);
                regs.MemPtr = (ushort)(((addr + 1) & 0xff) | (regs.A << 8));
                regs.PC += 2;
                break;

                case 0x36:  //LD (regs.HL), n
                val = PeekByte(regs.PC);
                // Log(String.Format("LD (regs.HL), {0,-6:X}", val));

                PokeByte(regs.HL, val);
                regs.PC += 1;
                break;
                #endregion

                #region Indirect load operations (LD r, r)
                /** LD r, r **/
                case 0x06: //LD B, n
                regs.B = PeekByte(regs.PC);
                // Log(String.Format("LD B, {0,-6:X}", B));

                regs.PC += 1;
                break;

                case 0x0A:  //LD A, (regs.BC)
                regs.A = PeekByte(regs.BC);
                regs.MemPtr = (ushort)(regs.BC + 1);
                // Log("LD A, (regs.BC)");

                break;

                case 0x0E:  //LD C, n
                regs.C = PeekByte(regs.PC);
                // Log(String.Format("LD C, {0,-6:X}", C));

                regs.PC += 1;
                break;

                case 0x16:  //LD D,n
                regs.D = PeekByte(regs.PC);
                // Log(String.Format("LD D, {0,-6:X}", D));

                regs.PC += 1;
                break;

                case 0x1A:  //LD A,(regs.DE)
                            // Log("LD A, (regs.DE)");
                regs.A = PeekByte(regs.DE);
                regs.MemPtr = (ushort)(regs.DE + 1);
                break;

                case 0x1E:  //LD E,n
                regs.E = PeekByte(regs.PC);
                // Log(String.Format("LD E, {0,-6:X}", E));

                regs.PC += 1;
                break;

                case 0x26:  //LD H,n
                regs.H = PeekByte(regs.PC);
                // Log(String.Format("LD H, {0,-6:X}", H));

                regs.PC += 1;
                break;

                case 0x2E:  //LD L,n
                regs.L = PeekByte(regs.PC);
                // Log(String.Format("LD L, {0,-6:X}", L));

                regs.PC += 1;
                break;

                case 0x3A:  //LD A,(nn)
                addr = PeekWord(regs.PC);
                // Log(String.Format("LD A, ({0,-6:X})", addr));
                regs.MemPtr = (ushort)(addr + 1);
                regs.A = PeekByte(addr);
                regs.PC += 2;
                break;

                case 0x3E:  //LD A,n
                regs.A = PeekByte(regs.PC);
                // Log(String.Format("LD A, {0,-6:X}", A));

                regs.PC += 1;
                break;

                case 0x40:  //LD B,B
                            // Log("LD B, B");
                //regs.B = regs.B;
                break;

                case 0x41:  //LD B,C
                            // Log("LD B, C");
                regs.B = regs.C;
                break;

                case 0x42:  //LD B,D
                            // Log("LD B, D");
                regs.B = regs.D;
                break;

                case 0x43:  //LD B,E
                            // Log("LD B, E");
                regs.B = regs.E;
                break;

                case 0x44:  //LD B,H
                            // Log("LD B, H");
                regs.B = regs.H;
                break;

                case 0x45:  //LD B,L
                            // Log("LD B, L");
                regs.B = regs.L;
                break;

                case 0x46:  //LD B,(regs.HL)
                            // Log("LD B, (regs.HL)");
                regs.B = PeekByte(regs.HL);
                break;

                case 0x47:  //LD B,A
                            // Log("LD B, A");
                regs.B = regs.A;
                break;

                case 0x48:  //LD C,B
                            // Log("LD C, B");
                regs.C = regs.B;
                break;

                case 0x49:  //LD C,C
                            // Log("LD C, C");
                //C = C;
                break;

                case 0x4A:  //LD C,D
                            // Log("LD C, D");
                regs.C = regs.D;
                break;

                case 0x4B:  //LD C,E
                            // Log("LD C, E");
                regs.C = regs.E;
                break;

                case 0x4C:  //LD C,H
                            // Log("LD C, H");
                regs.C = regs.H;
                break;

                case 0x4D:  //LD C,L
                            // Log("LD C, L");
                regs.C = regs.L;
                break;

                case 0x4E:  //LD C, (regs.HL)
                            // Log("LD C, (regs.HL)");
                regs.C = PeekByte(regs.HL);
                break;

                case 0x4F:  //LD C,A
                            // Log("LD C, A");
                regs.C = regs.A;
                break;

                case 0x50:  //LD D,B
                            // Log("LD D, B");
                regs.D = regs.B;
                break;

                case 0x51:  //LD D,C
                            // Log("LD D, C");
                regs.D = regs.C;
                break;

                case 0x52:  //LD D,D
                            // Log("LD D, D");
                //regs.D = D;
                break;

                case 0x53:  //LD D,E
                            // Log("LD D, E");
                regs.D = regs.E;
                break;

                case 0x54:  //LD D,H
                            // Log("LD D, H");
                regs.D = regs.H;
                break;

                case 0x55:  //LD D,L
                            // Log("LD D, L");
                regs.D = regs.L;
                break;

                case 0x56:  //LD D,(regs.HL)
                            // Log("LD D, (regs.HL)");
                regs.D = PeekByte(regs.HL);
                break;

                case 0x57:  //LD D,A
                            // Log("LD D, A");
                regs.D = regs.A;
                break;

                case 0x58:  //LD E,B
                            // Log("LD E, B");
                regs.E = regs.B;
                break;

                case 0x59:  //LD E,C
                            // Log("LD E, C");
                regs.E = regs.C;
                break;

                case 0x5A:  //LD E,D
                            // Log("LD E, D");
                regs.E = regs.D;
                break;

                case 0x5B:  //LD E,E
                            // Log("LD E, E");
                //regs.E = regs.E;
                break;

                case 0x5C:  //LD E,H
                            // Log("LD E, H");
                regs.E = regs.H;
                break;

                case 0x5D:  //LD E,L
                            // Log("LD E, L");
                regs.E = regs.L;
                break;

                case 0x5E:  //LD E,(regs.HL)
                            // Log("LD E, (regs.HL)");
                regs.E = PeekByte(regs.HL);
                break;

                case 0x5F:  //LD E,A
                            // Log("LD E, A");
                regs.E = regs.A;
                break;

                case 0x60:  //LD H,B
                            // Log("LD H, B");
                regs.H = regs.B;
                break;

                case 0x61:  //LD H,C
                            // Log("LD H, C");
                regs.H = regs.C;
                break;

                case 0x62:  //LD H,D
                            // Log("LD H, D");
                regs.H = regs.D;
                break;

                case 0x63:  //LD H,E
                            // Log("LD H, E");
                regs.H = regs.E;
                break;

                case 0x64:  //LD H,H
                            // Log("LD H, H");
                //regs.H = H;
                break;

                case 0x65:  //LD H,L
                            // Log("LD H, L");
                regs.H = regs.L;
                break;

                case 0x66:  //LD H,(regs.HL)
                            // Log("LD H, (regs.HL)");
                regs.H = PeekByte(regs.HL);
                break;

                case 0x67:  //LD H,A
                            // Log("LD H, A");
                regs.H = regs.A;
                break;

                case 0x68:  //LD L,B
                            // Log("LD L, B");
                regs.L = regs.B;
                break;

                case 0x69:  //LD L,C
                            // Log("LD L, C");
                regs.L = regs.C;
                break;

                case 0x6A:  //LD L,D
                            // Log("LD L, D");
                regs.L = regs.D;
                break;

                case 0x6B:  //LD L,E
                            // Log("LD L, E");
                regs.L = regs.E;
                break;

                case 0x6C:  //LD L,H
                            // Log("LD L, H");
                regs.L = regs.H;
                break;

                case 0x6D:  //LD L,L
                            // Log("LD L, L");
                //regs.L = regs.L;
                break;

                case 0x6E:  //LD L,(regs.HL)
                            // Log("LD L, (regs.HL)");
                regs.L = PeekByte(regs.HL);
                break;

                case 0x6F:  //LD L,A
                            // Log("LD L, A");
                regs.L = regs.A;
                break;

                case 0x70:  //LD (regs.HL),B
                            // Log("LD (regs.HL), B");
                PokeByte(regs.HL, regs.B);
                break;

                case 0x71:  //LD (regs.HL),C
                            // Log("LD (regs.HL), C");
                PokeByte(regs.HL, regs.C);
                break;

                case 0x72:  //LD (regs.HL),D
                            // Log("LD (regs.HL), D");
                PokeByte(regs.HL, regs.D);
                break;

                case 0x73:  //LD (regs.HL),E
                            // Log("LD (regs.HL), E");
                PokeByte(regs.HL, regs.E);
                break;

                case 0x74:  //LD (regs.HL),H
                            // Log("LD (regs.HL), H");
                PokeByte(regs.HL, regs.H);
                break;

                case 0x75:  //LD (regs.HL),L
                            // Log("LD (regs.HL), L");
                PokeByte(regs.HL, regs.L);
                break;

                case 0x77:  //LD (regs.HL),A
                            // Log("LD (regs.HL), A");
                PokeByte(regs.HL, regs.A);
                break;

                case 0x78:  //LD A,B
                            // Log("LD A, B");
                regs.A = regs.B;
                break;

                case 0x79:  //LD A,C
                            // Log("LD A, C");
                regs.A = regs.C;
                break;

                case 0x7A:  //LD A,D
                            // Log("LD A, D");
                regs.A = regs.D;
                break;

                case 0x7B:  //LD A,E
                            // Log("LD A, E");
                regs.A = regs.E;
                break;

                case 0x7C:  //LD A,H
                            // Log("LD A, H");
                regs.A = regs.H;
                break;

                case 0x7D:  //LD A,L
                            // Log("LD A, L");
                regs.A = regs.L;
                break;

                case 0x7E:  //LD A,(regs.HL)
                            // Log("LD A, (regs.HL)");
                regs.A = PeekByte(regs.HL);
                break;

                case 0x7F:  //LD A,A
                            // Log("LD A, A");
                //regs.A = regs.A;
                break;
                #endregion

                #region Rotates on Accumulator
                /** Accumulator Rotates **/
                case 0x07: //RLCA
                           // Log("RLCA");
                bool ac = (regs.A & BIT_F_SIGN) != 0; //save the msb bit

                if(ac) {
                    regs.A = (byte)((regs.A << 1) | BIT_F_CARRY);
                }
                else {
                    regs.A <<= 1;
                }
                SetF3((regs.A & BIT_F_3) != 0);
                SetF5((regs.A & BIT_F_5) != 0);
                SetCarry(ac);
                SetHalf(false);
                SetNeg(false);
                break;

                case 0x0F:  //RRCA
                            // Log("RRCA");

                ac = (regs.A & BIT_F_CARRY) != 0; //save the lsb bit

                if(ac) {
                    regs.A = (byte)((regs.A >> 1) | BIT_F_SIGN);
                }
                else {
                    regs.A >>= 1;
                }

                SetF3((regs.A & BIT_F_3) != 0);
                SetF5((regs.A & BIT_F_5) != 0);
                SetCarry(ac);
                SetHalf(false);
                SetNeg(false);
                break;

                case 0x17:  //RLA
                            // Log("RLA");
                ac = ((regs.A & BIT_F_SIGN) != 0);

                int msb = regs.F & BIT_F_CARRY;

                if(msb != 0) {
                    regs.A = (byte)((regs.A << 1) | BIT_F_CARRY);
                }
                else {
                    regs.A <<= 1;
                }
                SetF3((regs.A & BIT_F_3) != 0);
                SetF5((regs.A & BIT_F_5) != 0);
                SetCarry(ac);
                SetHalf(false);
                SetNeg(false);
                break;

                case 0x1F:  //RRA
                            // Log("RRA");
                ac = (regs.A & BIT_F_CARRY) != 0; //save the lsb bit
                int lsb = regs.F & BIT_F_CARRY;

                if(lsb != 0) {
                    regs.A = (byte)((regs.A >> 1) | BIT_F_SIGN);
                }
                else {
                    regs.A >>= 1;
                }
                SetF3((regs.A & BIT_F_3) != 0);
                SetF5((regs.A & BIT_F_5) != 0);
                SetCarry(ac);
                SetHalf(false);
                SetNeg(false);
                break;
                #endregion

                #region Exchange operations (EX)
                /** Exchange operations **/
                case 0x08:     //EX regs.AF, regs.AF'
                               // Log("EX regs.AF, regs.AF'");
                ex_af_af();
                regs.modified_F = false;
                break;

                case 0xD9:   //EXX
                             // Log("EXX");
                exx();
                break;

                case 0xE3:  //EX (regs.SP), regs.HL
                            // Log("EX (regs.SP), regs.HL");
                            //int temp = regs.HL;
                addr = PeekWord(regs.SP);
                Contend(regs.SP + 1, 1, 1);
                PokeByte((ushort)(regs.SP + 1), regs.H);
                PokeByte(regs.SP, regs.L);
                Contend(regs.SP, 1, 2);
                regs.HL = addr;
                regs.MemPtr = regs.HL;
                break;

                case 0xEB:  //EX regs.DE, regs.HL
                            // Log("EX regs.DE, regs.HL");
                ushort temp = regs.DE;
                regs.DE = regs.HL;
                regs.HL = temp;
                break;
                #endregion

                #region 16 bit addition to regs.HL (Add regs.HL, rr)
                /** Add regs.HL, rr **/
                case 0x09:     //ADD regs.HL, regs.BC
                               // Log("ADD regs.HL, regs.BC");
                               //if (model == MachineModel._plus3)
                               //    totalTStates += 7;
                               //else
                Contend(regs.IR, 1, 7);

                regs.MemPtr = (ushort)(regs.HL + 1);
                regs.HL = Add_RR(regs.HL, regs.BC);

                break;

                case 0x19:    //ADD regs.HL, regs.DE
                              // Log("ADD regs.HL, regs.DE");
                              //if (model == MachineModel._plus3)
                              //    totalTStates += 7;
                              //else
                Contend(regs.IR, 1, 7);
                regs.MemPtr = (ushort)(regs.HL + 1);
                regs.HL = Add_RR(regs.HL, regs.DE);

                break;

                case 0x29:  //ADD regs.HL, regs.HL
                            // Log("ADD regs.HL, regs.HL");
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 7;
                            //else
                Contend(regs.IR, 1, 7);
                regs.MemPtr = (ushort)(regs.HL + 1);
                regs.HL = Add_RR(regs.HL, regs.HL);
                break;

                case 0x39:  //ADD regs.HL, regs.SP
                            // Log("ADD regs.HL, regs.SP");
                            //if (model == MachineModel._plus3)
                            //    totalTStates += 7;
                            //else
                Contend(regs.IR, 1, 7);
                regs.MemPtr = (ushort)(regs.HL + 1);
                regs.HL = Add_RR(regs.HL, regs.SP);
                break;
                #endregion

                #region 8 bit addition to accumulator (Add r, r)
                /*** ADD r, r ***/
                case 0x80:  //ADD A,B
                            // Log("ADD A, B");
                Add_R(regs.B);
                break;

                case 0x81:  //ADD A,C
                            // Log("ADD A, C");
                Add_R(regs.C);
                break;

                case 0x82:  //ADD A,D
                            // Log("ADD A, D");
                Add_R(regs.D);
                break;

                case 0x83:  //ADD A,E
                            // Log("ADD A, E");
                Add_R(regs.E);
                break;

                case 0x84:  //ADD A,H
                            // Log("ADD A, H");
                Add_R(regs.H);
                break;

                case 0x85:  //ADD A,L
                            // Log("ADD A, L");
                Add_R(regs.L);
                break;

                case 0x86:  //ADD A, (regs.HL)
                            // Log("ADD A, (regs.HL)");
                Add_R(PeekByte(regs.HL));
                break;

                case 0x87:  //ADD A, A
                            // Log("ADD A, A");
                Add_R(regs.A);
                break;

                case 0xC6:  //ADD A, n
                {
                    byte b = PeekByte(regs.PC);
                    // Log(String.Format("ADD A, {0,-6:X}", disp));
                    Add_R(b);
                    regs.PC++;
                    break;
                }
                #endregion

                #region Add to accumulator with carry (Adc A, r)
                /** Adc a, r **/
                case 0x88:  //ADC A,B
                            // Log("ADC A, B");
                Adc_R(regs.B);
                break;

                case 0x89:  //ADC A,C
                            // Log("ADC A, C");
                Adc_R(regs.C);
                break;

                case 0x8A:  //ADC A,D
                            // Log("ADC A, D");
                Adc_R(regs.D);
                break;

                case 0x8B:  //ADC A,E
                            // Log("ADC A, E");
                Adc_R(regs.E);
                break;

                case 0x8C:  //ADC A,H
                            // Log("ADC A, H");
                Adc_R(regs.H);
                break;

                case 0x8D:  //ADC A,L
                            // Log("ADC A, L");
                Adc_R(regs.L);
                break;

                case 0x8E:  //ADC A,(regs.HL)
                            // Log("ADC A, (regs.HL)");
                Adc_R(PeekByte(regs.HL));
                break;

                case 0x8F:  //ADC A,A
                            // Log("ADC A, A");
                Adc_R(regs.A);
                break;

                case 0xCE:  //ADC A, n
                {
                    byte b = PeekByte(regs.PC);
                    // Log(String.Format("ADC A, {0,-6:X}", disp));
                    Adc_R(b);
                    regs.PC += 1;
                    break;
                }
                #endregion

                #region 8 bit subtraction from accumulator(SUB r)
                case 0x90:  //SUB B
                            // Log("SUB B");
                Sub_R(regs.B);
                break;

                case 0x91:  //SUB C
                            // Log("SUB C");
                Sub_R(regs.C);
                break;

                case 0x92:  //SUB D
                            // Log("SUB D");
                Sub_R(regs.D);
                break;

                case 0x93:  //SUB E
                            // Log("SUB E");
                Sub_R(regs.E);
                break;

                case 0x94:  //SUB H
                            // Log("SUB H");
                Sub_R(regs.H);
                break;

                case 0x95:  //SUB L
                            // Log("SUB L");
                Sub_R(regs.L);
                break;

                case 0x96:  //SUB (regs.HL)
                            // Log("SUB (regs.HL)");
                Sub_R(PeekByte(regs.HL));
                break;

                case 0x97:  //SUB A
                            // Log("SUB A");
                Sub_R(regs.A);
                break;

                case 0xD6:  //SUB n
                {
                    byte b = PeekByte(regs.PC);
                    // Log(String.Format("SUB {0,-6:X}", disp));
                    Sub_R(b);
                    regs.PC += 1;
                    break;
                }
                #endregion

                #region 8 bit subtraction from accumulator with carry(SBC A, r)
                case 0x98:  //SBC A, B
                            // Log("SBC A, B");
                Sbc_R(regs.B);
                break;

                case 0x99:  //SBC A, C
                            // Log("SBC A, C");
                Sbc_R(regs.C);
                break;

                case 0x9A:  //SBC A, D
                            // Log("SBC A, D");
                Sbc_R(regs.D);
                break;

                case 0x9B:  //SBC A, E
                            // Log("SBC A, E");
                Sbc_R(regs.E);
                break;

                case 0x9C:  //SBC A, H
                            // Log("SBC A, H");
                Sbc_R(regs.H);
                break;

                case 0x9D:  //SBC A, L
                            // Log("SBC A, L");
                Sbc_R(regs.L);
                break;

                case 0x9E:  //SBC A, (regs.HL)
                            // Log("SBC A, (regs.HL)");
                Sbc_R(PeekByte(regs.HL));
                break;

                case 0x9F:  //SBC A, A
                            // Log("SBC A, A");
                Sbc_R(regs.A);
                break;

                case 0xDE:  //SBC A, n
                {
                    byte b = PeekByte(regs.PC);
                    // Log(String.Format("SBC A, {0,-6:X}", disp));
                    Sbc_R(b);
                    regs.PC += 1;
                    break;
                }
                #endregion

                #region Relative Jumps (JR / DJNZ)
                /*** Relative Jumps ***/
                case 0x10:  //DJNZ n
                Contend(regs.IR, 1, 1);
                disp = GetDisplacement(PeekByte(regs.PC));
                // Log(String.Format("DJNZ {0,-6:X}", regs.PC + disp + 1));
                regs.B--;
                if(regs.B != 0) {
                    Contend(regs.PC, 1, 5);
                    regs.PC = (ushort)(regs.PC + disp);
                    regs.MemPtr = (ushort)(regs.PC + 1);
                }
                regs.PC++;

                break;

                case 0x18:  //JR n
                disp = GetDisplacement(PeekByte(regs.PC));
                // Log(String.Format("JR {0,-6:X}", regs.PC + disp + 1));
                Contend(regs.PC, 1, 5);
                regs.PC = (ushort)(regs.PC + disp);
                regs.PC++;
                regs.MemPtr = regs.PC;
                break;

                case 0x20:  //JRNZ n
                disp = GetDisplacement(PeekByte(regs.PC));
                // Log(String.Format("JR NZ, {0,-6:X}", regs.PC + disp + 1));
                if((regs.F & BIT_F_ZERO) == 0) {
                    Contend(regs.PC, 1, 5);
                    regs.PC = (ushort)(regs.PC + disp);
                    regs.MemPtr = (ushort)(regs.PC + 1);
                }
                regs.PC++;
                break;

                case 0x28:  //JRZ n
                disp = GetDisplacement(PeekByte(regs.PC));
                // Log(String.Format("JR Z, {0,-6:X}", regs.PC + disp + 1));

                if((regs.F & BIT_F_ZERO) != 0) {
                    Contend(regs.PC, 1, 5);
                    regs.PC = (ushort)(regs.PC + disp);
                    regs.MemPtr = (ushort)(regs.PC + 1);
                }
                regs.PC++;
                break;

                case 0x30:  //JRNC n
                disp = GetDisplacement(PeekByte(regs.PC));
                // Log(String.Format("JR NC, {0,-6:X}", regs.PC + disp + 1));

                if((regs.F & BIT_F_CARRY) == 0) {
                    Contend(regs.PC, 1, 5);
                    regs.PC = (ushort)(regs.PC + disp);
                    regs.MemPtr = (ushort)(regs.PC + 1);
                }
                regs.PC++;
                break;

                case 0x38:  //JRC n
                disp = GetDisplacement(PeekByte(regs.PC));
                // Log(String.Format("JR C, {0,-6:X}", regs.PC + disp + 1));

                if((regs.F & BIT_F_CARRY) != 0) {
                    Contend(regs.PC, 1, 5);
                    regs.PC = (ushort)(regs.PC + disp);
                    regs.MemPtr = (ushort)(regs.PC + 1);
                }
                regs.PC++;
                break;
                #endregion

                #region Direct jumps (JP)
                /*** Direct jumps ***/
                case 0xC2:  //JPNZ nn
                disp = PeekWord(regs.PC);
                // Log(String.Format("JP NZ, {0,-6:X}", disp));
                if((regs.F & BIT_F_ZERO) == 0) {
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                regs.MemPtr = (ushort)disp;
                break;

                case 0xC3:  //JP nn
                disp = PeekWord(regs.PC);
                // Log(String.Format("JP {0,-6:X}", disp));
                regs.PC = (ushort)disp;
                regs.MemPtr = (ushort)disp;
                break;

                case 0xCA:  //JPZ nn
                disp = PeekWord(regs.PC);
                // Log(String.Format("JP Z, {0,-6:X}", disp));
                if((regs.F & BIT_F_ZERO) != 0) {
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                regs.MemPtr = (ushort)disp;
                break;

                case 0xD2:  //JPNC nn
                disp = PeekWord(regs.PC);
                // Log(String.Format("JP NC, {0,-6:X}", disp));
                if((regs.F & BIT_F_CARRY) == 0) {
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                regs.MemPtr = (ushort)disp;
                break;

                case 0xDA:  //JPC nn
                disp = PeekWord(regs.PC);
                // Log(String.Format("JP C, {0,-6:X}", disp));
                if((regs.F & BIT_F_CARRY) != 0) {
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                regs.MemPtr = (ushort)disp;
                break;

                case 0xE2:  //JP PO nn
                disp = PeekWord(regs.PC);
                // Log(String.Format("JP PO, {0,-6:X}", disp));
                if((regs.F & BIT_F_PARITY) == 0) {
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                regs.MemPtr = (ushort)disp;
                break;

                case 0xE9:  //JP (regs.HL)
                            // Log("JP (regs.HL)");
                            //regs.PC = PeekWord(regs.HL);
                regs.PC = regs.HL;
                //  regs.MemPtr = regs.PC;
                break;

                case 0xEA:  //JP PE nn
                disp = PeekWord(regs.PC);
                // Log(String.Format("JP PE, {0,-6:X}", disp));
                if((regs.F & BIT_F_PARITY) != 0) {
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                regs.MemPtr = (ushort)disp;
                break;

                case 0xF2:  //JP P nn
                disp = PeekWord(regs.PC);
                // Log(String.Format("JP P, {0,-6:X}", disp));
                if((regs.F & BIT_F_SIGN) == 0) {
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                regs.MemPtr = (ushort)disp;
                break;

                case 0xFA:  //JP M nn
                disp = PeekWord(regs.PC);
                // Log(String.Format("JP M, {0,-6:X}", disp));
                if((regs.F & BIT_F_SIGN) != 0) {
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                regs.MemPtr = (ushort)disp;
                break;
                #endregion

                #region Compare instructions (CP)
                /*** Compare instructions **/
                case 0xB8:  //CP B
                            // Log("CP B");
                Cp_R(regs.B);
                break;

                case 0xB9:  //CP C
                            // Log("CP C");
                Cp_R(regs.C);
                break;

                case 0xBA:  //CP D
                            // Log("CP D");
                Cp_R(regs.D);
                break;

                case 0xBB:  //CP E
                            // Log("CP E");
                Cp_R(regs.E);
                break;

                case 0xBC:  //CP H
                            // Log("CP H");
                Cp_R(regs.H);
                break;

                case 0xBD:  //CP L
                            // Log("CP L");
                Cp_R(regs.L);
                break;

                case 0xBE:  //CP (regs.HL)
                            // Log("CP (regs.HL)");
                val = PeekByte(regs.HL);
                Cp_R(val);
                break;

                case 0xBF:  //CP A
                            // Log("CP A");
                Cp_R(regs.A);
                TapeEdgeCpA?.Invoke();
                //Tape trap
                //if(tape_readToPlay && !tapeTrapsDisabled)
                //    if(regs.PC == 0x56b)
                //        FlashLoadTape();
                break;

                case 0xFE:  //CP n
                {
                    byte b = PeekByte(regs.PC);
                    // Log(String.Format(String.Format("CP {0,-6:X}", disp)));
                    Cp_R(b);
                    regs.PC += 1;
                    break;
                }
                #endregion

                #region Carry Flag operations
                /*** Carry Flag operations ***/
                case 0x37:  //SCF
                {
                    // Log("SCF");
                    //Undocumented SCF behaviour:
                    //https://worldofspectrum.org/forums/discussion/41704/scf-ccf-flags-new-discovery/p1

                    //regs.F = (byte)((regs.F & (BIT_F_PARITY | BIT_F_ZERO | BIT_F_SIGN)) 
                    //            | (regs.A & (BIT_F_3 | BIT_F_5)) | BIT_F_CARRY);

                    regs.F = (byte)((regs.F & (BIT_F_PARITY | BIT_F_ZERO | BIT_F_SIGN))
                                | ((regs.Q | (regs.A & (BIT_F_3 | BIT_F_5))) | BIT_F_CARRY));
                }
                break;

                case 0x3F:  //CCF
                {
                        // Log("CCF");
                        //Undocumented SCF behaviour:

                        //regs.F = (byte)((regs.F & (BIT_F_PARITY | BIT_F_ZERO | BIT_F_SIGN)) 
                        //            | ((regs.F & BIT_F_CARRY) != 0 ? BIT_F_HALF : BIT_F_CARRY) 
                        //            | (regs.A & (BIT_F_3 | BIT_F_5)));

                        regs.F = (byte)((regs.F & (BIT_F_PARITY | BIT_F_ZERO | BIT_F_SIGN))
                                    | ((regs.F & BIT_F_CARRY) != 0 ? BIT_F_HALF : BIT_F_CARRY)
                                    | (regs.Q | (regs.A & (BIT_F_3 | BIT_F_5))));
                    }
                break;
                #endregion

                #region Bitwise AND (AND r)
                case 0xA0:  //AND B
                            // Log("AND B");
                And_R(regs.B);
                break;

                case 0xA1:  //AND C
                            // Log("AND C");
                And_R(regs.C);
                break;

                case 0xA2:  //AND D
                            // Log("AND D");
                And_R(regs.D);
                break;

                case 0xA3:  //AND E
                            // Log("AND E");
                And_R(regs.E);
                break;

                case 0xA4:  //AND H
                            // Log("AND H");
                And_R(regs.H);
                break;

                case 0xA5:  //AND L
                            // Log("AND L");
                And_R(regs.L);
                break;

                case 0xA6:  //AND (regs.HL)
                            // Log("AND (regs.HL)");
                And_R(PeekByte(regs.HL));
                break;

                case 0xA7:  //AND A
                            // Log("AND A");
                And_R(regs.A);
                break;

                case 0xE6:  //AND n
                {
                    byte b = PeekByte(regs.PC);
                    // Log(String.Format("AND {0,-6:X}", disp));
                    And_R(b);

                    regs.PC++;
                    break;
                }
                
                #endregion

                #region Bitwise XOR (XOR r)
                case 0xA8: //XOR B
                           // Log("XOR B");
                Xor_R(regs.B);
                break;

                case 0xA9: //XOR C
                           // Log("XOR C");
                Xor_R(regs.C);
                break;

                case 0xAA: //XOR D
                           // Log("XOR D");
                Xor_R(regs.D);
                break;

                case 0xAB: //XOR E
                           // Log("XOR E");
                Xor_R(regs.E);
                break;

                case 0xAC: //XOR H
                           // Log("XOR H");
                Xor_R(regs.H);
                break;

                case 0xAD: //XOR L
                           // Log("XOR L");
                Xor_R(regs.L);
                break;

                case 0xAE: //XOR (regs.HL)
                           // Log("XOR (regs.HL)");
                Xor_R(PeekByte(regs.HL));
                break;

                case 0xAF: //XOR A
                           // Log("XOR A");
                Xor_R(regs.A);
                break;

                case 0xEE:  //XOR n
                {
                    byte b = PeekByte(regs.PC);
                    // Log(String.Format("XOR {0,-6:X}", disp));
                    Xor_R(b);
                    regs.PC++;
                    break;
                }
                #endregion

                #region Bitwise OR (OR r)
                case 0xB0:  //OR B
                            // Log("OR B");
                Or_R(regs.B);
                break;

                case 0xB1:  //OR C
                            // Log("OR C");
                Or_R(regs.C);
                break;

                case 0xB2:  //OR D
                            // Log("OR D");
                Or_R(regs.D);
                break;

                case 0xB3:  //OR E
                            // Log("OR E");
                Or_R(regs.E);
                break;

                case 0xB4:  //OR H
                            // Log("OR H");
                Or_R(regs.H);
                break;

                case 0xB5:  //OR L
                            // Log("OR L");
                Or_R(regs.L);
                break;

                case 0xB6:  //OR (regs.HL)
                            // Log("OR (regs.HL)");
                Or_R(PeekByte(regs.HL));
                break;

                case 0xB7:  //OR A
                            // Log("OR A");
                Or_R(regs.A);
                break;

                case 0xF6:  //OR n
                {
                    byte b = PeekByte(regs.PC);
                    // Log(String.Format("OR {0,-6:X}", disp));
                    Or_R(b);
                    regs.PC++;
                    break;
                }
                #endregion

                #region Return instructions
                case 0xC0:  //RET NZ
                            // Log("RET NZ");
                Contend(regs.IR, 1, 1);
                if((regs.F & BIT_F_ZERO) == 0) {
                    regs.PC = PopStack();
                    regs.MemPtr = regs.PC;
                }
                break;

                case 0xC8:  //RET Z
                            // Log("RET Z");
                Contend(regs.IR, 1, 1);
                if((regs.F & BIT_F_ZERO) != 0) {
                    regs.PC = PopStack();
                    regs.MemPtr = regs.PC;
                }
                break;

                case 0xC9:  //RET
                            // Log("RET");
                regs.PC = PopStack();
                regs.MemPtr = regs.PC;
                break;

                case 0xD0:  //RET NC
                            // Log("RET NC");
                Contend(regs.IR, 1, 1);
                if((regs.F & BIT_F_CARRY) == 0) {
                    regs.PC = PopStack();
                    regs.MemPtr = regs.PC;
                }
                break;

                case 0xD8:  //RET C
                            // Log("RET C");
                Contend(regs.IR, 1, 1);
                if((regs.F & BIT_F_CARRY) != 0) {
                    regs.PC = PopStack();
                    regs.MemPtr = regs.PC;
                }
                break;

                case 0xE0:  //RET PO
                            // Log("RET PO");
                Contend(regs.IR, 1, 1);
                if((regs.F & BIT_F_PARITY) == 0) {
                    regs.PC = PopStack();
                    regs.MemPtr = regs.PC;
                }
                break;

                case 0xE8:  //RET PE
                            // Log("RET PE");
                Contend(regs.IR, 1, 1);
                if((regs.F & BIT_F_PARITY) != 0) {
                    regs.PC = PopStack();
                    regs.MemPtr = regs.PC;
                }
                break;

                case 0xF0:  //RET P
                            // Log("RET P");
                Contend(regs.IR, 1, 1);
                if((regs.F & BIT_F_SIGN) == 0) {
                    regs.PC = PopStack();
                    regs.MemPtr = regs.PC;
                }
                break;

                case 0xF8:  //RET M
                            // Log("RET M");
                Contend(regs.IR, 1, 1);
                if((regs.F & BIT_F_SIGN) != 0) {
                    regs.PC = PopStack();
                    regs.MemPtr = regs.PC;
                }
                break;
                #endregion

                #region POP/PUSH instructions
                case 0xC1:  //POP regs.BC
                            // Log("POP regs.BC");
                regs.BC = PopStack();
                break;

                case 0xC5:  //PUSH regs.BC
                            // Log("PUSH regs.BC");
                Contend(regs.IR, 1, 1);
                PushStack(regs.BC);
                break;

                case 0xD1:  //POP regs.DE
                            // Log("POP regs.DE");

                regs.DE = PopStack();
                break;

                case 0xD5:  //PUSH regs.DE
                            // Log("PUSH regs.DE");
                Contend(regs.IR, 1, 1);
                PushStack(regs.DE);
                break;

                case 0xE1:  //POP regs.HL
                            // Log("POP regs.HL");
                regs.HL = PopStack();
                break;

                case 0xE5:  //PUSH regs.HL
                            // Log("PUSH regs.HL");
                Contend(regs.IR, 1, 1);
                PushStack(regs.HL);
                break;

                case 0xF1:  //POP regs.AF
                            // Log("POP regs.AF");
                regs.AF = PopStack();
                regs.modified_F = false;
                break;

                case 0xF5:  //PUSH regs.AF
                            // Log("PUSH regs.AF");
                Contend(regs.IR, 1, 1);
                PushStack(regs.AF);
                break;
                #endregion

                #region CALL instructions
                case 0xC4:  //CALL NZ, nn
                disp = PeekWord(regs.PC);
                regs.MemPtr = (ushort)disp;
                // Log(String.Format("CALL NZ, {0,-6:X}", disp));
                if((regs.F & BIT_F_ZERO) == 0) {
                    Contend(regs.PC + 1, 1, 1);
                    PushStack((ushort)(regs.PC + 2));
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                break;

                case 0xCC:  //CALL Z, nn
                disp = PeekWord(regs.PC);
                regs.MemPtr = (ushort)disp;
                // Log(String.Format("CALL Z, {0,-6:X}", disp));
                if((regs.F & BIT_F_ZERO) != 0) {
                    Contend(regs.PC + 1, 1, 1);
                    PushStack((ushort)(regs.PC + 2));
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                break;

                case 0xCD:  //CALL nn
                disp = PeekWord(regs.PC);
                regs.MemPtr = (ushort)disp;
                // Log(String.Format("CALL {0,-6:X}", disp));
                Contend(regs.PC + 1, 1, 1);
                PushStack((ushort)(regs.PC + 2));
                regs.PC = (ushort)disp;
                break;

                case 0xD4:  //CALL NC, nn
                disp = PeekWord(regs.PC);
                regs.MemPtr = (ushort)disp;
                // Log(String.Format("CALL NC, {0,-6:X}", disp));
                if((regs.F & BIT_F_CARRY) == 0) {
                    Contend(regs.PC + 1, 1, 1);
                    PushStack((ushort)(regs.PC + 2));
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                break;

                case 0xDC:  //CALL C, nn
                disp = PeekWord(regs.PC);
                regs.MemPtr = (ushort)disp;
                // Log(String.Format("CALL C, {0,-6:X}", disp));
                if((regs.F & BIT_F_CARRY) != 0) {
                    Contend(regs.PC + 1, 1, 1);
                    PushStack((ushort)(regs.PC + 2));
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                break;

                case 0xE4:  //CALL PO, nn
                disp = PeekWord(regs.PC);
                regs.MemPtr = (ushort)disp;
                // Log(String.Format("CALL PO, {0,-6:X}", disp));
                if((regs.F & BIT_F_PARITY) == 0) {
                    Contend(regs.PC + 1, 1, 1);
                    PushStack((ushort)(regs.PC + 2));
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                break;

                case 0xEC:  //CALL PE, nn
                disp = PeekWord(regs.PC);
                regs.MemPtr = (ushort)disp;
                // Log(String.Format("CALL PE, {0,-6:X}", disp));
                if((regs.F & BIT_F_PARITY) != 0) {
                    Contend(regs.PC + 1, 1, 1);
                    PushStack((ushort)(regs.PC + 2));
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                break;

                case 0xF4:  //CALL P, nn
                disp = PeekWord(regs.PC);
                regs.MemPtr = (ushort)disp;
                // Log(String.Format("CALL P, {0,-6:X}", disp));
                if((regs.F & BIT_F_SIGN) == 0) {
                    Contend(regs.PC + 1, 1, 1);
                    PushStack((ushort)(regs.PC + 2));
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                break;

                case 0xFC:  //CALL M, nn
                disp = PeekWord(regs.PC);
                regs.MemPtr = (ushort)disp;
                // Log(String.Format("CALL M, {0,-6:X}", disp));
                if((regs.F & BIT_F_SIGN) != 0) {
                    Contend(regs.PC + 1, 1, 1);
                    PushStack((ushort)(regs.PC + 2));
                    regs.PC = (ushort)disp;
                }
                else {
                    regs.PC += 2;
                }
                break;
                #endregion

                #region Restart instructions (RST n)
                case 0xC7:  //RST 0x00
                            // Log("RST 00");
                Contend(regs.IR, 1, 1);
                PushStack(regs.PC);
                regs.PC = 0x00;
                regs.MemPtr = regs.PC;
                break;

                case 0xCF:  //RST 0x08
                            // Log("RST 08");
                Contend(regs.IR, 1, 1);
                PushStack(regs.PC);
                regs.PC = 0x08;
                regs.MemPtr = regs.PC;
                break;

                case 0xD7:  //RST 0x10
                            // Log("RST 10");
                Contend(regs.IR, 1, 1);
                PushStack(regs.PC);
                regs.PC = 0x10;
                regs.MemPtr = regs.PC;
                break;

                case 0xDF:  //RST 0x18
                            // Log("RST 18");
                Contend(regs.IR, 1, 1);
                PushStack(regs.PC);
                regs.PC = 0x18;
                regs.MemPtr = regs.PC;
                break;

                case 0xE7:  //RST 0x20
                            // Log("RST 20");
                Contend(regs.IR, 1, 1);
                PushStack(regs.PC);
                regs.PC = 0x20;
                regs.MemPtr = regs.PC;
                break;

                case 0xEF:  //RST 0x28
                            // Log("RST 28");
                Contend(regs.IR, 1, 1);
                PushStack(regs.PC);
                regs.PC = 0x28;
                regs.MemPtr = regs.PC;
                break;

                case 0xF7:  //RST 0x30
                            // Log("RST 30");
                Contend(regs.IR, 1, 1);
                PushStack(regs.PC);
                regs.PC = 0x30;
                regs.MemPtr = regs.PC;
                break;

                case 0xFF:  //RST 0x38
                            // Log("RST 38");
                Contend(regs.IR, 1, 1);
                PushStack(regs.PC);
                regs.PC = 0x38;
                regs.MemPtr = regs.PC;
                break;
                #endregion

                #region IN A, (n)
                case 0xDB:  //IN A, (n)

                disp = PeekByte(regs.PC);
                ushort port = (ushort)((regs.A << 8) | disp);

                //Tape Trap
                if (((disp & 0x1) == 0) && and_32_Or_64)
                    TapeEdgeDetection?.Invoke();

                // Log(String.Format("IN A, ({0:X})", disp));
                regs.MemPtr = (ushort)((regs.A << 8) + disp + 1);
                regs.A = In(port);


                and_32_Or_64 = false;
                regs.PC++;
                break;
                #endregion

                #region OUT (n), A
                case 0xD3:  //OUT (n), A
                disp = PeekByte(regs.PC);
                // Log(String.Format("OUT ({0:X}), A", disp));
                Out((ushort)(disp | (regs.A << 8)), regs.A);
                regs.MemPtr = (ushort)(((disp + 1) & 0xff) | (regs.A << 8));
                regs.PC++;
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
                regs.A = (byte)(regs.A ^ 0xff);
                SetF3((regs.A & BIT_F_3) != 0);
                SetF5((regs.A & BIT_F_5) != 0);
                SetNeg(true);
                SetHalf(true);
                break;
                #endregion

                #region Halt (HALT) - TO BE CHECKED!
                case 0x76:  //HALT
                            // Log("HALT");
                is_halted = true;
                //Refer to the section HALT and Special reset in:
                //http://www.primrosebank.net/computers/z80/z80_special_reset.htm
                // "When HALT is low PC has already been incremented and the opcode fetched is for the instruction after HALT.
                // The halt state stops this instruction from being executed and PC from incrementing so this opcode is read
                // again and again until an exit condition occurs. When an interrupt occurs during the halt state PC is pushed
                // unchanged onto the stack as it is already the correct return address."
                //regs.PC--;
                break;
                #endregion

                #region Interrupts
                case 0xF3:  //DI
                            // Log("DI");
                iff_1 = false;
                iff_2 = false;
                break;

                case 0xFB:  //EI
                            // Log("EI");
                iff_1 = true;
                iff_2 = true;
                interrupt_count = 1;

                break;
                #endregion

                #region Opcodes with CB prefix
                case 0xCB:
                switch(opcode = FetchInstruction()) {
                    #region Rotate instructions
                    case 0x00: //RLC B
                               // Log("RLC B");
                    regs.B = Rlc_R(regs.B);
                    break;

                    case 0x01: //RLC C
                               // Log("RLC C");
                    regs.C = Rlc_R(regs.C);
                    break;

                    case 0x02: //RLC D
                               // Log("RLC D");
                    regs.D = Rlc_R(regs.D);
                    break;

                    case 0x03: //RLC E
                               // Log("RLC E");
                    regs.E = Rlc_R(regs.E);
                    break;

                    case 0x04: //RLC H
                               // Log("RLC H");
                    regs.H = Rlc_R(regs.H);
                    break;

                    case 0x05: //RLC L
                               // Log("RLC L");
                    regs.L = Rlc_R(regs.L);
                    break;

                    case 0x06: //RLC (regs.HL)
                               // Log("RLC (regs.HL)");
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Rlc_R(b));
                        break;
                    }

                    case 0x07: //RLC A
                               // Log("RLC A");
                    regs.A = Rlc_R(regs.A);
                    break;

                    case 0x08: //RRC B
                               // Log("RRC B");
                    regs.B = Rrc_R(regs.B);
                    break;

                    case 0x09: //RRC C
                               // Log("RRC C");
                    regs.C = Rrc_R(regs.C);
                    break;

                    case 0x0A: //RRC D
                               // Log("RRC D");
                    regs.D = Rrc_R(regs.D);
                    break;

                    case 0x0B: //RRC E
                               // Log("RRC E");
                    regs.E = Rrc_R(regs.E);
                    break;

                    case 0x0C: //RRC H
                               // Log("RRC H");
                    regs.H = Rrc_R(regs.H);
                    break;

                    case 0x0D: //RRC L
                               // Log("RRC L");
                    regs.L = Rrc_R(regs.L);
                    break;

                    case 0x0E: //RRC (regs.HL)
                               // Log("RRC (regs.HL)");
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Rrc_R(b));
                        break;
                    }

                    case 0x0F: //RRC A
                               // Log("RRC A");
                    regs.A = Rrc_R(regs.A);
                    break;

                    case 0x10: //RL B
                               // Log("RL B");
                    regs.B = Rl_R(regs.B);
                    break;

                    case 0x11: //RL C
                               // Log("RL C");
                    regs.C = Rl_R(regs.C);
                    break;

                    case 0x12: //RL D
                               // Log("RL D");
                    regs.D = Rl_R(regs.D);
                    break;

                    case 0x13: //RL E
                               // Log("RL E");
                    regs.E = Rl_R(regs.E);
                    break;

                    case 0x14: //RL H
                               // Log("RL H");
                    regs.H = Rl_R(regs.H);
                    break;

                    case 0x15: //RL L
                               // Log("RL L");
                    regs.L = Rl_R(regs.L);
                    break;

                    case 0x16: //RL (regs.HL)
                               // Log("RL (regs.HL)");
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Rl_R(b));
                        break;
                    }

                    case 0x17: //RL A
                               // Log("RL A");
                    regs.A = Rl_R(regs.A);
                    break;

                    case 0x18: //RR B
                               // Log("RR B");
                    regs.B = Rr_R(regs.B);
                    break;

                    case 0x19: //RR C
                               // Log("RR C");
                    regs.C = Rr_R(regs.C);
                    break;

                    case 0x1A: //RR D
                               // Log("RR D");
                    regs.D = Rr_R(regs.D);
                    break;

                    case 0x1B: //RR E
                               // Log("RR E");
                    regs.E = Rr_R(regs.E);
                    break;

                    case 0x1C: //RR H
                               // Log("RR H");
                    regs.H = Rr_R(regs.H);
                    break;

                    case 0x1D: //RR L
                               // Log("RR L");
                    regs.L = Rr_R(regs.L);
                    break;

                    case 0x1E: //RR (regs.HL)
                               // Log("RR (regs.HL)");
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Rr_R(b));
                        break;
                    }

                    case 0x1F: //RR A
                               // Log("RR A");
                    regs.A = Rr_R(regs.A);
                    break;
                    #endregion

                    #region Register shifts
                    case 0x20:  //SLA B
                                // Log("SLA B");
                    regs.B = Sla_R(regs.B);
                    break;

                    case 0x21:  //SLA C
                                // Log("SLA C");
                    regs.C = Sla_R(regs.C);
                    break;

                    case 0x22:  //SLA D
                                // Log("SLA D");
                    regs.D = Sla_R(regs.D);
                    break;

                    case 0x23:  //SLA E
                                // Log("SLA E");
                    regs.E = Sla_R(regs.E);
                    break;

                    case 0x24:  //SLA H
                                // Log("SLA H");
                    regs.H = Sla_R(regs.H);
                    break;

                    case 0x25:  //SLA L
                                // Log("SLA L");
                    regs.L = Sla_R(regs.L);
                    break;

                    case 0x26:  //SLA (regs.HL)
                                // Log("SLA (regs.HL)");
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Sla_R(b));
                        break;
                    }

                    case 0x27:  //SLA A
                                // Log("SLA A");
                    regs.A = Sla_R(regs.A);
                    break;

                    case 0x28:  //SRA B
                                // Log("SRA B");
                    regs.B = Sra_R(regs.B);
                    break;

                    case 0x29:  //SRA C
                                // Log("SRA C");
                    regs.C = Sra_R(regs.C);
                    break;

                    case 0x2A:  //SRA D
                                // Log("SRA D");
                    regs.D = Sra_R(regs.D);
                    break;

                    case 0x2B:  //SRA E
                                // Log("SRA E");
                    regs.E = Sra_R(regs.E);
                    break;

                    case 0x2C:  //SRA H
                                // Log("SRA H");
                    regs.H = Sra_R(regs.H);
                    break;

                    case 0x2D:  //SRA L
                                // Log("SRA L");
                    regs.L = Sra_R(regs.L);
                    break;

                    case 0x2E:  //SRA (regs.HL)
                                // Log("SRA (regs.HL)");
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Sra_R(b));
                        break;
                    }

                    case 0x2F:  //SRA A
                                // Log("SRA A");
                    regs.A = Sra_R(regs.A);
                    break;

                    case 0x30:  //SLL B
                                // Log("SLL B");
                    regs.B = Sll_R(regs.B);
                    break;

                    case 0x31:  //SLL C
                                // Log("SLL C");
                    regs.C = Sll_R(regs.C);
                    break;

                    case 0x32:  //SLL D
                                // Log("SLL D");
                    regs.D = Sll_R(regs.D);
                    break;

                    case 0x33:  //SLL E
                                // Log("SLL E");
                    regs.E = Sll_R(regs.E);
                    break;

                    case 0x34:  //SLL H
                                // Log("SLL H");
                    regs.H = Sll_R(regs.H);
                    break;

                    case 0x35:  //SLL L
                                // Log("SLL L");
                    regs.L = Sll_R(regs.L);
                    break;

                    case 0x36:  //SLL (regs.HL)
                                // Log("SLL (regs.HL)");
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Sll_R(b));
                        break;
                    }
                    case 0x37:  //SLL A
                                // Log("SLL A");
                                //tstates += 8;
                    regs.A = Sll_R(regs.A);
                    break;

                    case 0x38:  //SRL B
                                // Log("SRL B");
                                //tstates += 8;
                    regs.B = Srl_R(regs.B);
                    break;

                    case 0x39:  //SRL C
                                // Log("SRL C");
                                //tstates += 8;
                    regs.C = Srl_R(regs.C);
                    break;

                    case 0x3A:  //SRL D
                                // Log("SRL D");
                                //tstates += 8;
                    regs.D = Srl_R(regs.D);
                    break;

                    case 0x3B:  //SRL E
                                // Log("SRL E");
                                //tstates += 8;
                    regs.E = Srl_R(regs.E);
                    break;

                    case 0x3C:  //SRL H
                                // Log("SRL H");
                                //tstates += 8;
                    regs.H = Srl_R(regs.H);
                    break;

                    case 0x3D:  //SRL L
                                // Log("SRL L");
                                //tstates += 8;
                    regs.L = Srl_R(regs.L);
                    break;

                    case 0x3E:  //SRL (regs.HL)
                                // Log("SRL (regs.HL)");
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Srl_R(b));
                        break;
                    }

                    case 0x3F:  //SRL A
                                // Log("SRL A");
                    regs.A = Srl_R(regs.A);
                    break;
                    #endregion

                    #region Bit test operation (BIT b, r)
                    case 0x40:  //BIT 0, B
                                // Log("BIT 0, B");
                    Bit_R(0, regs.B);
                    break;

                    case 0x41:  //BIT 0, C
                                // Log("BIT 0, C");
                    Bit_R(0, regs.C);
                    break;

                    case 0x42:  //BIT 0, D
                                // Log("BIT 0, D");
                    Bit_R(0, regs.D);
                    break;

                    case 0x43:  //BIT 0, E
                                // Log("BIT 0, E");
                    Bit_R(0, regs.E);
                    break;

                    case 0x44:  //BIT 0, H
                                // Log("BIT 0, H");
                    Bit_R(0, regs.H);
                    break;

                    case 0x45:  //BIT 0, L
                                // Log("BIT 0, L");
                    Bit_R(0, regs.L);
                    break;

                    case 0x46:  //BIT 0, (regs.HL)
                                // Log("BIT 0, (regs.HL)");
                    Bit_MemPtr(0, PeekByte(regs.HL));
                    Contend(regs.HL, 1, 1);
                    break;

                    case 0x47:  //BIT 0, A
                                // Log("BIT 0, A");
                    Bit_R(0, regs.A);
                    break;

                    case 0x48:  //BIT 1, B
                                // Log("BIT 1, B");
                    Bit_R(1, regs.B);
                    break;

                    case 0x49:  //BIT 1, C
                                // Log("BIT 1, C");
                    Bit_R(1, regs.C);
                    break;

                    case 0x4A:  //BIT 1, D
                                // Log("BIT 1, D");
                    Bit_R(1, regs.D);
                    break;

                    case 0x4B:  //BIT 1, E
                                // Log("BIT 1, E");
                    Bit_R(1, regs.E);
                    break;

                    case 0x4C:  //BIT 1, H
                                // Log("BIT 1, H");
                    Bit_R(1, regs.H);
                    break;

                    case 0x4D:  //BIT 1, L
                                // Log("BIT 1, L");
                    Bit_R(1, regs.L);
                    break;

                    case 0x4E:  //BIT 1, (regs.HL)
                                // Log("BIT 1, (regs.HL)");
                    Bit_MemPtr(1, PeekByte(regs.HL));
                    Contend(regs.HL, 1, 1);
                    break;

                    case 0x4F:  //BIT 1, A
                                // Log("BIT 1, A");
                    Bit_R(1, regs.A);
                    break;

                    case 0x50:  //BIT 2, B
                                // Log("BIT 2, B");
                    Bit_R(2, regs.B);
                    break;

                    case 0x51:  //BIT 2, C
                                // Log("BIT 2, C");
                    Bit_R(2, regs.C);
                    break;

                    case 0x52:  //BIT 2, D
                                // Log("BIT 2, D");
                    Bit_R(2, regs.D);
                    break;

                    case 0x53:  //BIT 2, E
                                // Log("BIT 2, E");
                    Bit_R(2, regs.E);
                    break;

                    case 0x54:  //BIT 2, H
                                // Log("BIT 2, H");
                    Bit_R(2, regs.H);
                    break;

                    case 0x55:  //BIT 2, L
                                // Log("BIT 2, L");
                    Bit_R(2, regs.L);
                    break;

                    case 0x56:  //BIT 2, (regs.HL)
                                // Log("BIT 2, (regs.HL)");
                    Bit_MemPtr(2, PeekByte(regs.HL));
                    Contend(regs.HL, 1, 1);

                    break;

                    case 0x57:  //BIT 2, A
                                // Log("BIT 2, A");
                    Bit_R(2, regs.A);
                    break;

                    case 0x58:  //BIT 3, B
                                // Log("BIT 3, B");
                    Bit_R(3, regs.B);
                    break;

                    case 0x59:  //BIT 3, C
                                // Log("BIT 3, C");
                    Bit_R(3, regs.C);
                    break;

                    case 0x5A:  //BIT 3, D
                                // Log("BIT 3, D");
                    Bit_R(3, regs.D);
                    break;

                    case 0x5B:  //BIT 3, E
                                // Log("BIT 3, E");
                    Bit_R(3, regs.E);
                    break;

                    case 0x5C:  //BIT 3, H
                                // Log("BIT 3, H");
                    Bit_R(3, regs.H);
                    break;

                    case 0x5D:  //BIT 3, L
                                // Log("BIT 3, L");
                    Bit_R(3, regs.L);
                    break;

                    case 0x5E:  //BIT 3, (regs.HL)
                                // Log("BIT 3, (regs.HL)");
                    Bit_MemPtr(3, PeekByte(regs.HL));
                    Contend(regs.HL, 1, 1);

                    break;

                    case 0x5F:  //BIT 3, A
                                // Log("BIT 3, A");
                    Bit_R(3, regs.A);
                    break;

                    case 0x60:  //BIT 4, B
                                // Log("BIT 4, B");
                    Bit_R(4, regs.B);
                    break;

                    case 0x61:  //BIT 4, C
                                // Log("BIT 4, C");
                    Bit_R(4, regs.C);
                    break;

                    case 0x62:  //BIT 4, D
                                // Log("BIT 4, D");
                    Bit_R(4, regs.D);
                    break;

                    case 0x63:  //BIT 4, E
                                // Log("BIT 4, E");
                    Bit_R(4, regs.E);
                    break;

                    case 0x64:  //BIT 4, H
                                // Log("BIT 4, H");
                    Bit_R(4, regs.H);
                    break;

                    case 0x65:  //BIT 4, L
                                // Log("BIT 4, L");
                    Bit_R(4, regs.L);
                    break;

                    case 0x66:  //BIT 4, (regs.HL)
                                // Log("BIT 4, (regs.HL)");
                    Bit_MemPtr(4, PeekByte(regs.HL));
                    Contend(regs.HL, 1, 1);

                    break;

                    case 0x67:  //BIT 4, A
                                // Log("BIT 4, A");
                    Bit_R(4, regs.A);
                    break;

                    case 0x68:  //BIT 5, B
                                // Log("BIT 5, B");
                    Bit_R(5, regs.B);
                    break;

                    case 0x69:  //BIT 5, C
                                // Log("BIT 5, C");
                    Bit_R(5, regs.C);
                    break;

                    case 0x6A:  //BIT 5, D
                                // Log("BIT 5, D");
                    Bit_R(5, regs.D);
                    break;

                    case 0x6B:  //BIT 5, E
                                // Log("BIT 5, E");
                    Bit_R(5, regs.E);
                    break;

                    case 0x6C:  //BIT 5, H
                                // Log("BIT 5, H");
                    Bit_R(5, regs.H);
                    break;

                    case 0x6D:  //BIT 5, L
                                // Log("BIT 5, L");
                    Bit_R(5, regs.L);
                    break;

                    case 0x6E:  //BIT 5, (regs.HL)
                                // Log("BIT 5, (regs.HL)");
                    Bit_MemPtr(5, PeekByte(regs.HL));
                    Contend(regs.HL, 1, 1);

                    break;

                    case 0x6F:  //BIT 5, A
                                // Log("BIT 5, A");
                    Bit_R(5, regs.A);
                    break;

                    case 0x70:  //BIT 6, B
                                // Log("BIT 6, B");
                    Bit_R(6, regs.B);
                    break;

                    case 0x71:  //BIT 6, C
                                // Log("BIT 6, C");
                    Bit_R(6, regs.C);
                    break;

                    case 0x72:  //BIT 6, D
                                // Log("BIT 6, D");
                    Bit_R(6, regs.D);
                    break;

                    case 0x73:  //BIT 6, E
                                // Log("BIT 6, E");
                    Bit_R(6, regs.E);
                    break;

                    case 0x74:  //BIT 6, H
                                // Log("BIT 6, H");
                    Bit_R(6, regs.H);
                    break;

                    case 0x75:  //BIT 6, L
                                // Log("BIT 6, L");
                    Bit_R(6, regs.L);
                    break;

                    case 0x76:  //BIT 6, (regs.HL)
                                // Log("BIT 6, (regs.HL)");
                    Bit_MemPtr(6, PeekByte(regs.HL));
                    Contend(regs.HL, 1, 1);

                    break;

                    case 0x77:  //BIT 6, A
                                // Log("BIT 6, A");
                    Bit_R(6, regs.A);
                    break;

                    case 0x78:  //BIT 7, B
                                // Log("BIT 7, B");
                    Bit_R(7, regs.B);
                    break;

                    case 0x79:  //BIT 7, C
                                // Log("BIT 7, C");
                    Bit_R(7, regs.C);
                    break;

                    case 0x7A:  //BIT 7, D
                                // Log("BIT 7, D");
                    Bit_R(7, regs.D);
                    break;

                    case 0x7B:  //BIT 7, E
                                // Log("BIT 7, E");
                    Bit_R(7, regs.E);
                    break;

                    case 0x7C:  //BIT 7, H
                                // Log("BIT 7, H");
                    Bit_R(7, regs.H);
                    break;

                    case 0x7D:  //BIT 7, L
                                // Log("BIT 7, L");
                    Bit_R(7, regs.L);
                    break;

                    case 0x7E:  //BIT 7, (regs.HL)
                                // Log("BIT 7, (regs.HL)");
                    Bit_MemPtr(7, PeekByte(regs.HL));
                    Contend(regs.HL, 1, 1);

                    break;

                    case 0x7F:  //BIT 7, A
                                // Log("BIT 7, A");
                    Bit_R(7, regs.A);
                    break;
                    #endregion

                    #region Reset bit operation (RES b, r)
                    case 0x80:  //RES 0, B
                                // Log("RES 0, B");
                    regs.B = Res_R(0, regs.B);
                    break;

                    case 0x81:  //RES 0, C
                                // Log("RES 0, C");
                    regs.C = Res_R(0, regs.C);
                    break;

                    case 0x82:  //RES 0, D
                                // Log("RES 0, D");
                    regs.D = Res_R(0, regs.D);
                    break;

                    case 0x83:  //RES 0, E
                                // Log("RES 0, E");
                    regs.E = Res_R(0, regs.E);
                    break;

                    case 0x84:  //RES 0, H
                                // Log("RES 0, H");
                    regs.H = Res_R(0, regs.H);
                    break;

                    case 0x85:  //RES 0, L
                                // Log("RES 0, L");
                    regs.L = Res_R(0, regs.L);
                    break;

                    case 0x86:  //RES 0, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Res_R(0, b));
                        break;
                    }

                    case 0x87:  //RES 0, A
                                // Log("RES 0, A");
                    regs.A = Res_R(0, regs.A);
                    break;

                    case 0x88:  //RES 1, B
                                // Log("RES 1, B");
                    regs.B = Res_R(1, regs.B);
                    break;

                    case 0x89:  //RES 1, C
                                // Log("RES 1, C");
                    regs.C = Res_R(1, regs.C);
                    break;

                    case 0x8A:  //RES 1, D
                                // Log("RES 1, D");
                    regs.D = Res_R(1, regs.D);
                    break;

                    case 0x8B:  //RES 1, E
                                // Log("RES 1, E");
                    regs.E = Res_R(1, regs.E);
                    break;

                    case 0x8C:  //RES 1, H
                                // Log("RES 1, H");
                                //tstates += 8;
                    regs.H = Res_R(1, regs.H);
                    break;

                    case 0x8D:  //RES 1, L
                                // Log("RES 1, L");
                    regs.L = Res_R(1, regs.L);
                    break;

                    case 0x8E:  //RES 1, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Res_R(1, b));
                        break;
                    }

                    case 0x8F:  //RES 1, A
                                // Log("RES 1, A");
                    regs.A = Res_R(1, regs.A);
                    break;

                    case 0x90:  //RES 2, B
                                // Log("RES 2, B");
                    regs.B = Res_R(2, regs.B);
                    break;

                    case 0x91:  //RES 2, C
                                // Log("RES 2, C");
                    regs.C = Res_R(2, regs.C);
                    break;

                    case 0x92:  //RES 2, D
                                // Log("RES 2, D");
                    regs.D = Res_R(2, regs.D);
                    break;

                    case 0x93:  //RES 2, E
                                // Log("RES 2, E");
                    regs.E = Res_R(2, regs.E);
                    break;

                    case 0x94:  //RES 2, H
                                // Log("RES 2, H");
                    regs.H = Res_R(2, regs.H);
                    break;

                    case 0x95:  //RES 2, L
                                // Log("RES 2, L");
                    regs.L = Res_R(2, regs.L);
                    break;

                    case 0x96:  //RES 2, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Res_R(2, b));
                        break;
                    }

                    case 0x97:  //RES 2, A
                                // Log("RES 2, A");
                    regs.A = Res_R(2, regs.A);
                    break;

                    case 0x98:  //RES 3, B
                                // Log("RES 3, B");
                    regs.B = Res_R(3, regs.B);
                    break;

                    case 0x99:  //RES 3, C
                                // Log("RES 3, C");
                    regs.C = Res_R(3, regs.C);
                    break;

                    case 0x9A:  //RES 3, D
                                // Log("RES 3, D");
                    regs.D = Res_R(3, regs.D);
                    break;

                    case 0x9B:  //RES 3, E
                                // Log("RES 3, E");
                    regs.E = Res_R(3, regs.E);
                    break;

                    case 0x9C:  //RES 3, H
                                // Log("RES 3, H");
                    regs.H = Res_R(3, regs.H);
                    break;

                    case 0x9D:  //RES 3, L
                                // Log("RES 3, L");
                    regs.L = Res_R(3, regs.L);
                    break;

                    case 0x9E:  //RES 3, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Res_R(3, b));
                        break;
                    }

                    case 0x9F:  //RES 3, A
                                // Log("RES 3, A");
                    regs.A = Res_R(3, regs.A);
                    break;

                    case 0xA0:  //RES 4, B
                                // Log("RES 4, B");
                    regs.B = Res_R(4, regs.B);
                    break;

                    case 0xA1:  //RES 4, C
                                // Log("RES 4, C");
                    regs.C = Res_R(4, regs.C);
                    break;

                    case 0xA2:  //RES 4, D
                                // Log("RES 4, D");
                    regs.D = Res_R(4, regs.D);
                    break;

                    case 0xA3:  //RES 4, E
                                // Log("RES 4, E");
                    regs.E = Res_R(4, regs.E);
                    break;

                    case 0xA4:  //RES 4, H
                                // Log("RES 4, H");
                    regs.H = Res_R(4, regs.H);
                    break;

                    case 0xA5:  //RES 4, L
                                // Log("RES 4, L");
                    regs.L = Res_R(4, regs.L);
                    break;

                    case 0xA6:  //RES 4, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Res_R(4, b));
                        break;
                    }


                    case 0xA7:  //RES 4, A
                                // Log("RES 4, A");
                    regs.A = Res_R(4, regs.A);
                    break;

                    case 0xA8:  //RES 5, B
                                // Log("RES 5, B");
                    regs.B = Res_R(5, regs.B);
                    break;

                    case 0xA9:  //RES 5, C
                                // Log("RES 5, C");
                    regs.C = Res_R(5, regs.C);
                    break;

                    case 0xAA:  //RES 5, D
                                // Log("RES 5, D");
                    regs.D = Res_R(5, regs.D);
                    break;

                    case 0xAB:  //RES 5, E
                                // Log("RES 5, E");
                    regs.E = Res_R(5, regs.E);
                    break;

                    case 0xAC:  //RES 5, H
                                // Log("RES 5, H");
                    regs.H = Res_R(5, regs.H);
                    break;

                    case 0xAD:  //RES 5, L
                                // Log("RES 5, L");
                    regs.L = Res_R(5, regs.L);
                    break;

                    case 0xAE:  //RES 5, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Res_R(5, b));
                        break;
                    }


                    case 0xAF:  //RES 5, A
                                // Log("RES 5, A");
                    regs.A = Res_R(5, regs.A);
                    break;

                    case 0xB0:  //RES 6, B
                                // Log("RES 6, B");
                    regs.B = Res_R(6, regs.B);
                    break;

                    case 0xB1:  //RES 6, C
                                // Log("RES 6, C");
                    regs.C = Res_R(6, regs.C);
                    break;

                    case 0xB2:  //RES 6, D
                                // Log("RES 6, D");
                    regs.D = Res_R(6, regs.D);
                    break;

                    case 0xB3:  //RES 6, E
                                // Log("RES 6, E");
                    regs.E = Res_R(6, regs.E);
                    break;

                    case 0xB4:  //RES 6, H
                                // Log("RES 6, H");
                    regs.H = Res_R(6, regs.H);
                    break;

                    case 0xB5:  //RES 6, L
                                // Log("RES 6, L");
                    regs.L = Res_R(6, regs.L);
                    break;

                    case 0xB6:  //RES 6, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Res_R(6, b));
                        break;
                    }

                    case 0xB7:  //RES 6, A
                                // Log("RES 6, A");
                    regs.A = Res_R(6, regs.A);
                    break;

                    case 0xB8:  //RES 7, B
                                // Log("RES 7, B");
                    regs.B = Res_R(7, regs.B);
                    break;

                    case 0xB9:  //RES 7, C
                                // Log("RES 7, C");
                    regs.C = Res_R(7, regs.C);
                    break;

                    case 0xBA:  //RES 7, D
                                // Log("RES 7, D");
                    regs.D = Res_R(7, regs.D);
                    break;

                    case 0xBB:  //RES 7, E
                                // Log("RES 7, E");
                    regs.E = Res_R(7, regs.E);
                    break;

                    case 0xBC:  //RES 7, H
                                // Log("RES 7, H");
                    regs.H = Res_R(7, regs.H);
                    break;

                    case 0xBD:  //RES 7, L
                                // Log("RES 7, L");
                    regs.L = Res_R(7, regs.L);
                    break;

                    case 0xBE:  //RES 7, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Res_R(7, b));
                        break;
                    }
                    case 0xBF:  //RES 7, A
                                // Log("RES 7, A");
                    regs.A = Res_R(7, regs.A);
                    break;
                    #endregion

                    #region Set bit operation (SET b, r)
                    case 0xC0:  //SET 0, B
                                // Log("SET 0, B");
                    regs.B = Set_R(0, regs.B);
                    break;

                    case 0xC1:  //SET 0, C
                                // Log("SET 0, C");
                    regs.C = Set_R(0, regs.C);
                    break;

                    case 0xC2:  //SET 0, D
                                // Log("SET 0, D");
                    regs.D = Set_R(0, regs.D);
                    break;

                    case 0xC3:  //SET 0, E
                                // Log("SET 0, E");
                    regs.E = Set_R(0, regs.E);
                    break;

                    case 0xC4:  //SET 0, H
                                // Log("SET 0, H");
                    regs.H = Set_R(0, regs.H);
                    break;

                    case 0xC5:  //SET 0, L
                                // Log("SET 0, L");
                    regs.L = Set_R(0, regs.L);
                    break;

                    case 0xC6:  //SET 0, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Set_R(0, b));
                        break;
                    }

                    case 0xC7:  //SET 0, A
                                // Log("SET 0, A");
                    regs.A = Set_R(0, regs.A);
                    break;

                    case 0xC8:  //SET 1, B
                                // Log("SET 1, B");
                    regs.B = Set_R(1, regs.B);
                    break;

                    case 0xC9:  //SET 1, C
                                // Log("SET 1, C");
                    regs.C = Set_R(1, regs.C);
                    break;

                    case 0xCA:  //SET 1, D
                                // Log("SET 1, D");
                    regs.D = Set_R(1, regs.D);
                    break;

                    case 0xCB:  //SET 1, E
                                // Log("SET 1, E");
                    regs.E = Set_R(1, regs.E);
                    break;

                    case 0xCC:  //SET 1, H
                                // Log("SET 1, H");
                    regs.H = Set_R(1, regs.H);
                    break;

                    case 0xCD:  //SET 1, L
                                // Log("SET 1, L");
                    regs.L = Set_R(1, regs.L);
                    break;

                    case 0xCE:  //SET 1, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Set_R(1, b));
                        break;
                    }
                    case 0xCF:  //SET 1, A
                                // Log("SET 1, A");
                    regs.A = Set_R(1, regs.A);
                    break;

                    case 0xD0:  //SET 2, B
                                // Log("SET 2, B");
                    regs.B = Set_R(2, regs.B);
                    break;

                    case 0xD1:  //SET 2, C
                                // Log("SET 2, C");
                    regs.C = Set_R(2, regs.C);
                    break;

                    case 0xD2:  //SET 2, D
                                // Log("SET 2, D");
                    regs.D = Set_R(2, regs.D);
                    break;

                    case 0xD3:  //SET 2, E
                                // Log("SET 2, E");
                    regs.E = Set_R(2, regs.E);
                    break;

                    case 0xD4:  //SET 2, H
                                // Log("SET 2, H");
                    regs.H = Set_R(2, regs.H);
                    break;

                    case 0xD5:  //SET 2, L
                                // Log("SET 2, L");
                    regs.L = Set_R(2, regs.L);
                    break;

                    case 0xD6:  //SET 2, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Set_R(2, b));
                        break;
                    }

                    case 0xD7:  //SET 2, A
                                // Log("SET 2, A");
                    regs.A = Set_R(2, regs.A);
                    break;

                    case 0xD8:  //SET 3, B
                                // Log("SET 3, B");
                    regs.B = Set_R(3, regs.B);
                    break;

                    case 0xD9:  //SET 3, C
                                // Log("SET 3, C");
                    regs.C = Set_R(3, regs.C);
                    break;

                    case 0xDA:  //SET 3, D
                                // Log("SET 3, D");
                    regs.D = Set_R(3, regs.D);
                    break;

                    case 0xDB:  //SET 3, E
                                // Log("SET 3, E");
                    regs.E = Set_R(3, regs.E);
                    break;

                    case 0xDC:  //SET 3, H
                                // Log("SET 3, H");
                    regs.H = Set_R(3, regs.H);
                    break;

                    case 0xDD:  //SET 3, L
                                // Log("SET 3, L");
                    regs.L = Set_R(3, regs.L);
                    break;

                    case 0xDE:  //SET 3, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Set_R(3, b));
                        break;
                    }

                    case 0xDF:  //SET 3, A
                                // Log("SET 3, A");
                    regs.A = Set_R(3, regs.A);
                    break;

                    case 0xE0:  //SET 4, B
                                // Log("SET 4, B");
                    regs.B = Set_R(4, regs.B);
                    break;

                    case 0xE1:  //SET 4, C
                                // Log("SET 4, C");
                    regs.C = Set_R(4, regs.C);
                    break;

                    case 0xE2:  //SET 4, D
                                // Log("SET 4, D");
                    regs.D = Set_R(4, regs.D);
                    break;

                    case 0xE3:  //SET 4, E
                                // Log("SET 4, E");
                    regs.E = Set_R(4, regs.E);
                    break;

                    case 0xE4:  //SET 4, H
                                // Log("SET 4, H");
                    regs.H = Set_R(4, regs.H);
                    break;

                    case 0xE5:  //SET 4, L
                                // Log("SET 4, L");
                    regs.L = Set_R(4, regs.L);
                    break;

                    case 0xE6:  //SET 4, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Set_R(4, b));
                        break;
                    }

                    case 0xE7:  //SET 4, A
                                // Log("SET 4, A");
                    regs.A = Set_R(4, regs.A);
                    break;

                    case 0xE8:  //SET 5, B
                                // Log("SET 5, B");
                    regs.B = Set_R(5, regs.B);
                    break;

                    case 0xE9:  //SET 5, C
                                // Log("SET 5, C");
                    regs.C = Set_R(5, regs.C);
                    break;

                    case 0xEA:  //SET 5, D
                                // Log("SET 5, D");
                    regs.D = Set_R(5, regs.D);
                    break;

                    case 0xEB:  //SET 5, E
                                // Log("SET 5, E");
                    regs.E = Set_R(5, regs.E);
                    break;

                    case 0xEC:  //SET 5, H
                                // Log("SET 5, H");
                    regs.H = Set_R(5, regs.H);
                    break;

                    case 0xED:  //SET 5, L
                                // Log("SET 5, L");
                    regs.L = Set_R(5, regs.L);
                    break;

                    case 0xEE:  //SET 5, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Set_R(5, b));
                        break;
                    }
                    case 0xEF:  //SET 5, A
                                // Log("SET 5, A");
                    regs.A = Set_R(5, regs.A);
                    break;

                    case 0xF0:  //SET 6, B
                                // Log("SET 6, B");
                    regs.B = Set_R(6, regs.B);
                    break;

                    case 0xF1:  //SET 6, C
                                // Log("SET 6, C");
                    regs.C = Set_R(6, regs.C);
                    break;

                    case 0xF2:  //SET 6, D
                                // Log("SET 6, D");
                    regs.D = Set_R(6, regs.D);
                    break;

                    case 0xF3:  //SET 6, E
                                // Log("SET 6, E");
                    regs.E = Set_R(6, regs.E);
                    break;

                    case 0xF4:  //SET 6, H
                                // Log("SET 6, H");
                    regs.H = Set_R(6, regs.H);
                    break;

                    case 0xF5:  //SET 6, L
                                // Log("SET 6, L");
                    regs.L = Set_R(6, regs.L);
                    break;

                    case 0xF6:  //SET 6, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Set_R(6, b));
                        break;
                    }

                    case 0xF7:  //SET 6, A
                                // Log("SET 6, A");
                    regs.A = Set_R(6, regs.A);
                    break;

                    case 0xF8:  //SET 7, B
                                // Log("SET 7, B");
                    regs.B = Set_R(7, regs.B);
                    break;

                    case 0xF9:  //SET 7, C
                                // Log("SET 7, C");
                    regs.C = Set_R(7, regs.C);
                    break;

                    case 0xFA:  //SET 7, D
                                // Log("SET 7, D");
                    regs.D = Set_R(7, regs.D);
                    break;

                    case 0xFB:  //SET 7, E
                                // Log("SET 7, E");
                    regs.E = Set_R(7, regs.E);
                    break;

                    case 0xFC:  //SET 7, H
                                // Log("SET 7, H");
                    regs.H = Set_R(7, regs.H);
                    break;

                    case 0xFD:  //SET 7, L
                                // Log("SET 7, L");
                    regs.L = Set_R(7, regs.L);
                    break;

                    case 0xFE:  //SET 7, (regs.HL)
                    {
                        byte b = PeekByte(regs.HL);
                        Contend(regs.HL, 1, 1);
                        PokeByte(regs.HL, Set_R(7, b));
                        break;
                    }

                    case 0xFF:  //SET 7, A
                                // Log("SET 7, A");
                    regs.A = Set_R(7, regs.A);
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
                switch(opcode = FetchInstruction()) {
                    #region Addition instructions
                    case 0x09:  //ADD regs.IX, regs.BC
                                // Log("ADD regs.IX, regs.BC");
                    Contend(regs.IR, 1, 7);
                    regs.MemPtr = (ushort)(regs.IX + 1);
                    regs.IX = Add_RR(regs.IX, regs.BC);
                    break;

                    case 0x19:  //ADD regs.IX, regs.DE
                                // Log("ADD regs.IX, regs.DE");
                    Contend(regs.IR, 1, 7);
                    regs.MemPtr = (ushort)(regs.IX + 1);
                    regs.IX = Add_RR(regs.IX, regs.DE);
                    break;

                    case 0x29:  //ADD regs.IX, regs.IX
                                // Log("ADD regs.IX, regs.IX");
                    Contend(regs.IR, 1, 7);
                    regs.MemPtr = (ushort)(regs.IX + 1);
                    regs.IX = Add_RR(regs.IX, regs.IX);
                    break;

                    case 0x39:  //ADD regs.IX, regs.SP
                                // Log("ADD regs.IX, regs.SP");
                    Contend(regs.IR, 1, 7);
                    regs.MemPtr = (ushort)(regs.IX + 1);
                    regs.IX = Add_RR(regs.IX, regs.SP);
                    break;

                    case 0x84:  //ADD A, IXH
                                // Log("ADD A, IXH");
                    Add_R(regs.IXH);
                    break;

                    case 0x85:  //ADD A, IXL
                                // Log("ADD A, IXL");
                    Add_R(regs.IXL);
                    break;

                    case 0x86:  //Add A, (regs.IX+d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        Add_R(PeekByte(offset));
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }

                    case 0x8C:  //ADC A, IXH
                                // Log("ADC A, IXH");
                    Adc_R(regs.IXH);
                    break;

                    case 0x8D:  //ADC A, IXL
                                // Log("ADC A, IXL");
                    Adc_R(regs.IXL);
                    break;

                    case 0x8E: //ADC A, (regs.IX+d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required
                        // Log(string.Format("ADC A, (regs.IX + {0:X})", disp));
                        //if (model == MachineModel._plus3)
                        //    totalTStates += 5;
                        //else
                        Contend(regs.PC, 1, 5);
                        Adc_R(PeekByte(offset));
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }
                    #endregion

                    #region Subtraction instructions
                    case 0x94:  //SUB A, IXH
                                // Log("SUB A, IXH");
                    Sub_R(regs.IXH);
                    break;

                    case 0x95:  //SUB A, IXL
                                // Log("SUB A, IXL");
                    Sub_R(regs.IXL);
                    break;

                    case 0x96:  //SUB (regs.IX + d)
                    {
                        int b= GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required
                        // Log(string.Format("SUB (regs.IX + {0:X})", disp));
                        //if (model == MachineModel._plus3)
                        //    totalTStates += 5;
                        //else
                        Contend(regs.PC, 1, 5);
                        Sub_R(PeekByte(offset));
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }

                    case 0x9C:  //SBC A, IXH
                                // Log("SBC A, IXH");
                    Sbc_R(regs.IXH);
                    break;

                    case 0x9D:  //SBC A, IXL
                                // Log("SBC A, IXL");
                    Sbc_R(regs.IXL);
                    break;

                    case 0x9E:  //SBC A, (regs.IX + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required
                        // Log(string.Format("SBC A, (regs.IX + {0:X})", disp));
                        //if (model == MachineModel._plus3)
                        //    totalTStates += 5;
                        //else
                        Contend(regs.PC, 1, 5);
                        regs.MemPtr = offset;
                        Sbc_R(PeekByte(offset));
                        regs.PC++;
                        break;
                    }
                    #endregion

                    #region Increment/Decrements
                    case 0x23:  //INC regs.IX
                                // Log("INC regs.IX");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 2;
                                //else
                    Contend(regs.IR, 1, 2);
                    regs.IX++;
                    break;

                    case 0x24:  //INC IXH
                                // Log("INC IXH");
                    regs.IXH = Inc(regs.IXH);
                    break;

                    case 0x25:  //DEC IXH
                                // Log("DEC IXH");
                    regs.IXH = Dec(regs.IXH);
                    break;

                    case 0x2B:  //DEC regs.IX
                                // Log("DEC regs.IX");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 2;
                                //else
                    Contend(regs.IR, 1, 2);
                    regs.IX--;
                    break;

                    case 0x2C:  //INC IXL
                                // Log("INC IXL");
                    regs.IXL = Inc(regs.IXL);
                    break;

                    case 0x2D:  //DEC IXL
                                // Log("DEC IXL");
                    regs.IXL = Dec(regs.IXL);
                    break;

                    case 0x34:  //INC (regs.IX + d)
                    {
                        int d = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + d); //The displacement required
                                                    // Log(string.Format("INC (regs.IX + {0:X})", disp));
                        Contend(regs.PC, 1, 5);
                        byte b = Inc(PeekByte(offset));
                        Contend(offset, 1, 1);
                        PokeByte(offset, b);
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }

                    case 0x35:  //DEC (regs.IX + d)
                    {
                        int d = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + d); //The displacement required
                                                                // Log(string.Format("INC (regs.IX + {0:X})", disp));
                        Contend(regs.PC, 1, 5);
                        byte b = Dec(PeekByte(offset));
                        Contend(offset, 1, 1);
                        PokeByte(offset, b);
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }
                    #endregion

                    #region Bitwise operators

                    case 0xA4:  //AND IXH
                                // Log("AND IXH");
                    And_R(regs.IXH);
                    break;

                    case 0xA5:  //AND IXL
                                // Log("AND IXL");
                    And_R(regs.IXL);
                    break;

                    case 0xA6:  //AND (regs.IX + d)
                    {
                        int d = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + d); //The displacement required
                                                    // Log(string.Format("AND (regs.IX + {0:X})", disp));
                                                    //if (model == MachineModel._plus3)
                                                    //    totalTStates += 5;
                                                    //else
                        Contend(regs.PC, 1, 5);
                        And_R(PeekByte(offset));
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }

                    case 0xAC:  //XOR IXH
                                // Log("XOR IXH");
                    Xor_R(regs.IXH);
                    break;

                    case 0xAD:  //XOR IXL
                                // Log("XOR IXL");
                    Xor_R(regs.IXL);
                    break;

                    case 0xAE:  //XOR (regs.IX + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required
                                                                    // Log(string.Format("AND (regs.IX + {0:X})", disp));
                                                                    //if (model == MachineModel._plus3)
                                                                    //    totalTStates += 5;
                                                                    //else
                        Contend(regs.PC, 1, 5);
                        Xor_R(PeekByte(offset));
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }

                    case 0xB4:  //OR IXH
                                // Log("OR IXH");
                    Or_R(regs.IXH);
                    break;

                    case 0xB5:  //OR IXL
                                // Log("OR IXL");
                    Or_R(regs.IXL);
                    break;

                    case 0xB6:  //OR (regs.IX + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required
                                                                    // Log(string.Format("AND (regs.IX + {0:X})", disp));
                                                                    //if (model == MachineModel._plus3)
                                                                    //    totalTStates += 5;
                                                                    //else
                        Contend(regs.PC, 1, 5);
                        Or_R(PeekByte(offset));
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }
                    #endregion

                    #region Compare operator
                    case 0xBC:  //CP IXH
                                // Log("CP IXH");
                    Cp_R(regs.IXH);
                    break;

                    case 0xBD:  //CP IXL
                                // Log("CP IXL");
                    Cp_R(regs.IXL);
                    break;

                    case 0xBE:  //CP (regs.IX + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required
                                                                    // Log(string.Format("AND (regs.IX + {0:X})", disp));
                                                                    //if (model == MachineModel._plus3)
                                                                    //    totalTStates += 5;
                                                                    //else
                        Contend(regs.PC, 1, 5);
                        Cp_R(PeekByte(offset));
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }
                    #endregion

                    #region Load instructions
                    case 0x21:  //LD regs.IX, nn
                                // Log(string.Format("LD regs.IX, {0,-6:X}", PeekWord(regs.PC)));
                    regs.IX = PeekWord(regs.PC);
                    regs.PC += 2;
                    break;

                    case 0x22:  //LD (nn), regs.IX
                                // Log(string.Format("LD ({0:X}), regs.IX", PeekWord(regs.PC)));
                    addr = PeekWord(regs.PC);
                    PokeWord(addr, regs.IX);
                    regs.PC += 2;
                    regs.MemPtr = (ushort)(addr + 1);
                    break;

                    case 0x26:  //LD IXH, n
                                // Log(string.Format("LD IXH, {0:X}", PeekByte(regs.PC)));
                    regs.IXH = PeekByte(regs.PC);
                    regs.PC++;
                    break;

                    case 0x2A:  //LD regs.IX, (nn)
                                // Log(string.Format("LD regs.IX, ({0:X})", PeekWord(regs.PC)));
                    addr = PeekWord(regs.PC);
                    regs.IX = PeekWord(addr);
                    regs.MemPtr = (ushort)(addr + 1);
                    regs.PC += 2;
                    break;

                    case 0x2E:  //LD IXL, n
                                // Log(string.Format("LD IXL, {0:X}", PeekByte(regs.PC)));
                    regs.IXL = PeekByte(regs.PC);
                    regs.PC++;
                    break;

                    case 0x36:  //LD (regs.IX + d), n
                    {
                        int d = GetDisplacement(PeekByte(regs.PC));

                        ushort offset = (ushort)(regs.IX + d); //The displacement required
                        byte b = PeekByte((ushort)(regs.PC + 1));
                        Contend(regs.PC + 1, 1, 2);

                        PokeByte(offset, b);
                        regs.MemPtr = offset;
                        regs.PC += 2;
                        break;
                    }

                    case 0x44:  //LD B, IXH
                                // Log("LD B, IXH");
                    regs.B = regs.IXH;
                    break;

                    case 0x45:  //LD B, IXL
                                // Log("LD B, IXL");
                    regs.B = regs.IXL;
                    break;

                    case 0x46:  //LD B, (regs.IX + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        regs.B = PeekByte(offset);
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }

                    case 0x4C:  //LD C, IXH
                                // Log("LD C, IXH");
                    regs.C = regs.IXH;
                    break;

                    case 0x4D:  //LD C, IXL
                                // Log("LD C, IXL");
                    regs.C = regs.IXL;
                    break;

                    case 0x4E:  //LD C, (regs.IX + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        regs.C = PeekByte(offset);
                        regs.MemPtr = offset;
                        regs.PC++;
                    }
                    break;

                    case 0x54:  //LD D, IXH
                                // Log("LD D, IXH");
                                //tstates += 4;
                    regs.D = regs.IXH;
                    break;

                    case 0x55:  //LD D, IXL
                                // Log("LD D, IXL");
                                //tstates += 4;
                    regs.D = regs.IXL;
                    break;

                    case 0x56:  //LD D, (regs.IX + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required
                                                                
                        Contend(regs.PC, 1, 5);
                        regs.D = PeekByte(offset);
                        regs.MemPtr = offset;
                        regs.PC++;
                    }
                        break;

                    case 0x5C:  //LD E, IXH
                                // Log("LD E, IXH");
                                //tstates += 4;
                    regs.E = regs.IXH;
                    break;

                    case 0x5D:  //LD E, IXL
                                // Log("LD E, IXL");
                                //tstates += 4;
                    regs.E = regs.IXL;
                    break;

                    case 0x5E:  //LD E, (regs.IX + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        regs.E = PeekByte(offset);
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }
                        

                    case 0x60:  //LD IXH, B
                                // Log("LD IXH, B");
                                //tstates += 4;
                    regs.IXH = regs.B;
                    break;

                    case 0x61:  //LD IXH, C
                                // Log("LD IXH, C");
                                //tstates += 4;
                    regs.IXH = regs.C;
                    break;

                    case 0x62:  //LD IXH, D
                                // Log("LD IXH, D");
                                //tstates += 4;
                    regs.IXH = regs.D;
                    break;

                    case 0x63:  //LD IXH, E
                                // Log("LD IXH, E");
                                //tstates += 4;
                    regs.IXH = regs.E;
                    break;

                    case 0x64:  //LD IXH, IXH
                                // Log("LD IXH, IXH");
                                //tstates += 4;
                    regs.IXH = regs.IXH;
                    break;

                    case 0x65:  //LD IXH, IXL
                                // Log("LD IXH, IXL");
                                //tstates += 4;
                    regs.IXH = regs.IXL;
                    break;

                    case 0x66:  //LD H, (regs.IX + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        regs.H = PeekByte(offset);
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }
                    case 0x67:  //LD IXH, A
                                // Log("LD IXH, A");
                                //tstates += 4;
                    regs.IXH = regs.A;
                    break;

                    case 0x68:  //LD IXL, B
                                // Log("LD IXL, B");
                                //tstates += 4;
                    regs.IXL = regs.B;
                    break;

                    case 0x69:  //LD IXL, C
                                // Log("LD IXL, C");
                                //tstates += 4;
                    regs.IXL = regs.C;
                    break;

                    case 0x6A:  //LD IXL, D
                                // Log("LD IXL, D");
                                //tstates += 4;
                    regs.IXL = regs.D;
                    break;

                    case 0x6B:  //LD IXL, E
                                // Log("LD IXL, E");
                                //tstates += 4;
                    regs.IXL = regs.E;
                    break;

                    case 0x6C:  //LD IXL, IXH
                                // Log("LD IXL, IXH");
                                //tstates += 4;
                    regs.IXL = regs.IXH;
                    break;

                    case 0x6D:  //LD IXL, IXL
                                // Log("LD IXL, IXL");
                                //tstates += 4;
                    regs.IXL = regs.IXL;
                    break;

                    case 0x6E:  //LD L, (regs.IX + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        regs.L = PeekByte(offset);
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }

                    case 0x6F:  //LD IXL, A
                                // Log("LD IXL, A");
                                //tstates += 4;
                    regs.IXL = regs.A;
                    break;

                    case 0x70:  //LD (regs.IX + d), B
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required
                            
                        Contend(regs.PC, 1, 5);
                        PokeByte(offset, regs.B);
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }

                    case 0x71:  //LD (regs.IX + d), C
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        PokeByte(offset, regs.C);
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }

                    case 0x72:  //LD (regs.IX + d), D
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        PokeByte(offset, regs.D);
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }

                    case 0x73:  //LD (regs.IX + d), E
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        PokeByte(offset, regs.E);
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }

                    case 0x74:  //LD (regs.IX + d), H
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        PokeByte(offset, regs.H);
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }

                    case 0x75:  //LD (regs.IX + d), L
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        PokeByte(offset, regs.L);
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }

                    case 0x77:  //LD (regs.IX + d), A
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        PokeByte(offset, regs.A);
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }

                    case 0x7C:  //LD A, IXH
                                // Log("LD A, IXH");
                    regs.A = regs.IXH;
                    break;

                    case 0x7D:  //LD A, IXL
                                // Log("LD A, IXL");
                    regs.A = regs.IXL;
                    break;

                    case 0x7E:  //LD A, (regs.IX + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IX + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        regs.A = PeekByte(offset);
                        regs.MemPtr = offset;
                        regs.PC++;
                        break;
                    }

                    case 0xF9:  //LD regs.SP, regs.IX
                                // Log("LD regs.SP, regs.IX");
                    Contend(regs.IR, 1, 2);
                    regs.SP = regs.IX;
                    break;
                    #endregion

                    #region All DDCB instructions
                    case 0xCB: 
                     {
                            disp = GetDisplacement(PeekByte(regs.PC));
                            ushort offset = (ushort)(regs.IX + disp); //The displacement required
                            regs.PC++;
                            //GetOpcode triggers memory execute event
                            //replacing with PeekByte for now
                            //opcode = GetOpcode(regs.PC);      //The opcode comes after the offset byte!
                            opcode = PeekByte(regs.PC);
                            Contend(regs.PC, 1, 2);
                            regs.PC++;
                            byte b = PeekByte(offset);
                            Contend(offset, 1, 1);
                            // if ((opcode >= 0x40) && (opcode <= 0x7f))
                            regs.MemPtr = offset;

                            switch(opcode) {
                                case 0x00: //LD B, RLC (regs.IX+d)
                                           // Log(string.Format("LD B, RLC (regs.IX + {0:X})", disp));
                                regs.B = Rlc_R(b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0x01: //LD C, RLC (regs.IX+d)
                                           // Log(string.Format("LD C, RLC (regs.IX + {0:X})", disp));
                                regs.C = Rlc_R(b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0x02: //LD D, RLC (regs.IX+d)
                                           // Log(string.Format("LD D, RLC (regs.IX + {0:X})", disp));
                                regs.D = Rlc_R(b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0x03: //LD E, RLC (regs.IX+d)
                                           // Log(string.Format("LD E, RLC (regs.IX + {0:X})", disp));
                                regs.E = Rlc_R(b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0x04: //LD H, RLC (regs.IX+d)
                                           // Log(string.Format("LD H, RLC (regs.IX + {0:X})", disp));
                                regs.H = Rlc_R(b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0x05: //LD L, RLC (regs.IX+d)
                                           // Log(string.Format("LD L, RLC (regs.IX + {0:X})", disp));
                                regs.L = Rlc_R(b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0x06:  //RLC (regs.IX + d)
                                            // Log(string.Format("RLC (regs.IX + {0:X})", disp));
                                PokeByte(offset, Rlc_R(b));
                                break;

                                case 0x07: //LD A, RLC (regs.IX+d)
                                           // Log(string.Format("LD A, RLC (regs.IX + {0:X})", disp));
                                regs.A = Rlc_R(b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0x08: //LD B, RRC (regs.IX+d)
                                           // Log(string.Format("LD B, RRC (regs.IX + {0:X})", disp));
                                regs.B = Rrc_R(b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0x09: //LD C, RRC (regs.IX+d)
                                           // Log(string.Format("LD C, RRC (regs.IX + {0:X})", disp));
                                regs.C = Rrc_R(b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0x0A: //LD D, RRC (regs.IX+d)
                                           // Log(string.Format("LD D, RRC (regs.IX + {0:X})", disp));
                                regs.D = Rrc_R(b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0x0B: //LD E, RRC (regs.IX+d)
                                           // Log(string.Format("LD E, RRC (regs.IX + {0:X})", disp));
                                regs.E = Rrc_R(b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0x0C: //LD H, RRC (regs.IX+d)
                                           // Log(string.Format("LD H, RRC (regs.IX + {0:X})", disp));
                                regs.H = Rrc_R(b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0x0D: //LD L, RRC (regs.IX+d)
                                           // Log(string.Format("LD L, RRC (regs.IX + {0:X})", disp));
                                regs.L = Rrc_R(b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0x0E:  //RRC (regs.IX + d)
                                            // Log(string.Format("RRC (regs.IX + {0:X})", disp));
                                PokeByte(offset, Rrc_R(b));
                                break;

                                case 0x0F: //LD A, RRC (regs.IX+d)
                                           // Log(string.Format("LD A, RRC (regs.IX + {0:X})", disp));
                                regs.A = Rrc_R(b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0x10: //LD B, RL (regs.IX+d)
                                           // Log(string.Format("LD B, RL (regs.IX + {0:X})", disp));
                                regs.B = Rl_R(b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0x11: //LD C, RL (regs.IX+d)
                                           // Log(string.Format("LD C, RL (regs.IX + {0:X})", disp));
                                regs.C = Rl_R(b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0x12: //LD D, RL (regs.IX+d)
                                           // Log(string.Format("LD D, RL (regs.IX + {0:X})", disp));
                                regs.D = Rl_R(b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0x13: //LD E, RL (regs.IX+d)
                                           // Log(string.Format("LD E, RL (regs.IX + {0:X})", disp));
                                regs.E = Rl_R(b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0x14: //LD H, RL (regs.IX+d)
                                           // Log(string.Format("LD H, RL (regs.IX + {0:X})", disp));
                                regs.H = Rl_R(b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0x15: //LD L, RL (regs.IX+d)
                                           // Log(string.Format("LD L, RL (regs.IX + {0:X})", disp));
                                regs.L = Rl_R(b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0x16:  //RL (regs.IX + d)
                                            // Log(string.Format("RL (regs.IX + {0:X})", disp));
                                PokeByte(offset, Rl_R(b));

                                break;

                                case 0x17: //LD A, RL (regs.IX+d)
                                           // Log(string.Format("LD A, RL (regs.IX + {0:X})", disp));
                                regs.A = Rl_R(b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0x18: //LD B, RR (regs.IX+d)
                                           // Log(string.Format("LD B, RR (regs.IX + {0:X})", disp));
                                regs.B = Rr_R(b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0x19: //LD C, RR (regs.IX+d)
                                           // Log(string.Format("LD C, RR (regs.IX + {0:X})", disp));
                                regs.C = Rr_R(b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0x1A: //LD D, RR (regs.IX+d)
                                           // Log(string.Format("LD D, RR (regs.IX + {0:X})", disp));
                                regs.D = Rr_R(b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0x1B: //LD E, RR (regs.IX+d)
                                           // Log(string.Format("LD E, RR (regs.IX + {0:X})", disp));
                                regs.E = Rr_R(b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0x1C: //LD H, RR (regs.IX+d)
                                           // Log(string.Format("LD H, RR (regs.IX + {0:X})", disp));
                                regs.H = Rr_R(b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0x1D: //LD L, RR (regs.IX+d)
                                           // Log(string.Format("LD L, RR (regs.IX + {0:X})", disp));
                                regs.L = Rr_R(b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0x1E:  //RR (regs.IX + d)
                                            // Log(string.Format("RR (regs.IX + {0:X})", disp));
                                PokeByte(offset, Rr_R(b));
                                break;

                                case 0x1F: //LD A, RR (regs.IX+d)
                                           // Log(string.Format("LD A, RR (regs.IX + {0:X})", disp));
                                regs.A = Rr_R(b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0x20: //LD B, SLA (regs.IX+d)
                                           // Log(string.Format("LD B, SLA (regs.IX + {0:X})", disp));
                                regs.B = Sla_R(b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0x21: //LD C, SLA (regs.IX+d)
                                           // Log(string.Format("LD C, SLA (regs.IX + {0:X})", disp));
                                regs.C = Sla_R(b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0x22: //LD D, SLA (regs.IX+d)
                                           // Log(string.Format("LD D, SLA (regs.IX + {0:X})", disp));
                                regs.D = Sla_R(b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0x23: //LD E, SLA (regs.IX+d)
                                           // Log(string.Format("LD E, SLA (regs.IX + {0:X})", b));
                                regs.E = Sla_R(b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0x24: //LD H, SLA (regs.IX+d)
                                           // Log(string.Format("LD H, SLA (regs.IX + {0:X})", b));
                                regs.H = Sla_R(b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0x25: //LD L, SLA (regs.IX+d)
                                           // Log(string.Format("LD L, SLA (regs.IX + {0:X})", b));
                                regs.L = Sla_R(b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0x26:  //SLA (regs.IX + d)
                                            // Log(string.Format("SLA (regs.IX + {0:X})", b));
                                PokeByte(offset, Sla_R(b));
                                break;

                                case 0x27: //LD A, SLA (regs.IX+d)
                                           // Log(string.Format("LD A, SLA (regs.IX + {0:X})", b));
                                regs.A = Sla_R(b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0x28: //LD B, SRA (regs.IX+d)
                                           // Log(string.Format("LD B, SRA (regs.IX + {0:X})", b));
                                regs.B = Sra_R(b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0x29: //LD C, SRA (regs.IX+d)
                                           // Log(string.Format("LD C, SRA (regs.IX + {0:X})", b));
                                regs.C = Sra_R(b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0x2A: //LD D, SRA (regs.IX+d)
                                           // Log(string.Format("LD D, SRA (regs.IX + {0:X})", b));
                                regs.D = Sra_R(b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0x2B: //LD E, SRA (regs.IX+d)
                                           // Log(string.Format("LD E, SRA (regs.IX + {0:X})", b));
                                regs.E = Sra_R(b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0x2C: //LD H, SRA (regs.IX+d)
                                           // Log(string.Format("LD H, SRA (regs.IX + {0:X})", b));
                                regs.H = Sra_R(b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0x2D: //LD L, SRA (regs.IX+d)
                                           // Log(string.Format("LD L, SRA (regs.IX + {0:X})", b));
                                regs.L = Sra_R(b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0x2E:  //SRA (regs.IX + d)
                                            // Log(string.Format("SRA (regs.IX + {0:X})", b));
                                PokeByte(offset, Sra_R(b));
                                break;

                                case 0x2F: //LD A, SRA (regs.IX+d)
                                           // Log(string.Format("LD A, SRA (regs.IX + {0:X})", b));
                                regs.A = Sra_R(b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0x30: //LD B, SLL (regs.IX+d)
                                           // Log(string.Format("LD B, SLL (regs.IX + {0:X})", b));
                                regs.B = Sll_R(b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0x31: //LD C, SLL (regs.IX+d)
                                           // Log(string.Format("LD C, SLL (regs.IX + {0:X})", b));
                                regs.C = Sll_R(b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0x32: //LD D, SLL (regs.IX+d)
                                           // Log(string.Format("LD D, SLL (regs.IX + {0:X})", b));
                                regs.D = Sll_R(b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0x33: //LD E, SLL (regs.IX+d)
                                           // Log(string.Format("LD E, SLL (regs.IX + {0:X})", b));
                                regs.E = Sll_R(b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0x34: //LD H, SLL (regs.IX+d)
                                           // Log(string.Format("LD H, SLL (regs.IX + {0:X})", b));
                                regs.H = Sll_R(b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0x35: //LD L, SLL (regs.IX+d)
                                           // Log(string.Format("LD L, SLL (regs.IX + {0:X})", b));
                                regs.L = Sll_R(b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0x36:  //SLL (regs.IX + d)
                                            // Log(string.Format("SLL (regs.IX + {0:X})", b));
                                PokeByte(offset, Sll_R(b));
                                break;

                                case 0x37: //LD A, SLL (regs.IX+d)
                                           // Log(string.Format("LD A, SLL (regs.IX + {0:X})", b));
                                regs.A = Sll_R(b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0x38: //LD B, SRL (regs.IX+d)
                                           // Log(string.Format("LD B, SRL (regs.IX + {0:X})", b));
                                regs.B = Srl_R(b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0x39: //LD C, SRL (regs.IX+d)
                                           // Log(string.Format("LD C, SRL (regs.IX + {0:X})", b));
                                regs.C = Srl_R(b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0x3A: //LD D, SRL (regs.IX+d)
                                           // Log(string.Format("LD D, SRL (regs.IX + {0:X})", b));
                                regs.D = Srl_R(b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0x3B: //LD E, SRL (regs.IX+d)
                                           // Log(string.Format("LD E, SRL (regs.IX + {0:X})", b));
                                regs.E = Srl_R(b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0x3C: //LD H, SRL (regs.IX+d)
                                           // Log(string.Format("LD H, SRL (regs.IX + {0:X})", b));
                                regs.H = Srl_R(b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0x3D: //LD L, SRL (regs.IX+d)
                                           // Log(string.Format("LD L, SRL (regs.IX + {0:X})", b));
                                regs.L = Srl_R(b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0x3E:  //SRL (regs.IX + d)
                                            // Log(string.Format("SRL (regs.IX + {0:X})", b));
                                PokeByte(offset, Srl_R(b));
                                break;

                                case 0x3F: //LD A, SRL (regs.IX+d)
                                           // Log(string.Format("LD A, SRL (regs.IX + {0:X})", b));
                                regs.A = Srl_R(b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0x40:  //BIT 0, (regs.IX + d)
                                case 0x41:  //BIT 0, (regs.IX + d)
                                case 0x42:  //BIT 0, (regs.IX + d)
                                case 0x43:  //BIT 0, (regs.IX + d)
                                case 0x44:  //BIT 0, (regs.IX + d)
                                case 0x45:  //BIT 0, (regs.IX + d)
                                case 0x46:  //BIT 0, (regs.IX + d)
                                case 0x47:  //BIT 0, (regs.IX + d)
                                            // Log(string.Format("BIT 0, (regs.IX + {0:X})", disp));
                                Bit_MemPtr(0, b);
                                
                                break;

                                case 0x48:  //BIT 1, (regs.IX + d)
                                case 0x49:  //BIT 1, (regs.IX + d)
                                case 0x4A:  //BIT 1, (regs.IX + d)
                                case 0x4B:  //BIT 1, (regs.IX + d)
                                case 0x4C:  //BIT 1, (regs.IX + d)
                                case 0x4D:  //BIT 1, (regs.IX + d)
                                case 0x4E:  //BIT 1, (regs.IX + d)
                                case 0x4F:  //BIT 1, (regs.IX + d)
                                            // Log(string.Format("BIT 1, (regs.IX + {0:X})", b));
                                Bit_MemPtr(1, b);

                                break;

                                case 0x50:  //BIT 2, (regs.IX + d)
                                case 0x51:  //BIT 2, (regs.IX + d)
                                case 0x52:  //BIT 2, (regs.IX + d)
                                case 0x53:  //BIT 2, (regs.IX + d)
                                case 0x54:  //BIT 2, (regs.IX + d)
                                case 0x55:  //BIT 2, (regs.IX + d)
                                case 0x56:  //BIT 2, (regs.IX + d)
                                case 0x57:  //BIT 2, (regs.IX + d)
                                            // Log(string.Format("BIT 2, (regs.IX + {0:X})", b));
                                Bit_MemPtr(2, b);

                                break;

                                case 0x58:  //BIT 3, (regs.IX + d)
                                case 0x59:  //BIT 3, (regs.IX + d)
                                case 0x5A:  //BIT 3, (regs.IX + d)
                                case 0x5B:  //BIT 3, (regs.IX + d)
                                case 0x5C:  //BIT 3, (regs.IX + d)
                                case 0x5D:  //BIT 3, (regs.IX + d)
                                case 0x5E:  //BIT 3, (regs.IX + d)
                                case 0x5F:  //BIT 3, (regs.IX + d)
                                            // Log(string.Format("BIT 3, (regs.IX + {0:X})", b));
                                Bit_MemPtr(3, b);

                                break;

                                case 0x60:  //BIT 4, (regs.IX + d)
                                case 0x61:  //BIT 4, (regs.IX + d)
                                case 0x62:  //BIT 4, (regs.IX + d)
                                case 0x63:  //BIT 4, (regs.IX + d)
                                case 0x64:  //BIT 4, (regs.IX + d)
                                case 0x65:  //BIT 4, (regs.IX + d)
                                case 0x66:  //BIT 4, (regs.IX + d)
                                case 0x67:  //BIT 4, (regs.IX + d)
                                            // Log(string.Format("BIT 4, (regs.IX + {0:X})", b));
                                Bit_MemPtr(4, b);

                                break;

                                case 0x68:  //BIT 5, (regs.IX + d)
                                case 0x69:  //BIT 5, (regs.IX + d)
                                case 0x6A:  //BIT 5, (regs.IX + d)
                                case 0x6B:  //BIT 5, (regs.IX + d)
                                case 0x6C:  //BIT 5, (regs.IX + d)
                                case 0x6D:  //BIT 5, (regs.IX + d)
                                case 0x6E:  //BIT 5, (regs.IX + d)
                                case 0x6F:  //BIT 5, (regs.IX + d)
                                            // Log(string.Format("BIT 5, (regs.IX + {0:X})", b));
                                Bit_MemPtr(5, b); ;

                                break;

                                case 0x70://BIT 6, (regs.IX + d)
                                case 0x71://BIT 6, (regs.IX + d)
                                case 0x72://BIT 6, (regs.IX + d)
                                case 0x73://BIT 6, (regs.IX + d)
                                case 0x74://BIT 6, (regs.IX + d)
                                case 0x75://BIT 6, (regs.IX + d)
                                case 0x76://BIT 6, (regs.IX + d)
                                case 0x77:  //BIT 6, (regs.IX + d)
                                            // Log(string.Format("BIT 6, (regs.IX + {0:X})", b));
                                Bit_MemPtr(6, b);

                                break;

                                case 0x78:  //BIT 7, (regs.IX + d)
                                case 0x79:  //BIT 7, (regs.IX + d)
                                case 0x7A:  //BIT 7, (regs.IX + d)
                                case 0x7B:  //BIT 7, (regs.IX + d)
                                case 0x7C:  //BIT 7, (regs.IX + d)
                                case 0x7D:  //BIT 7, (regs.IX + d)
                                case 0x7E:  //BIT 7, (regs.IX + d)
                                case 0x7F:  //BIT 7, (regs.IX + d)
                                            // Log(string.Format("BIT 7, (regs.IX + {0:X})", b));
                                Bit_MemPtr(7, b);

                                break;

                                case 0x80: //LD B, RES 0, (regs.IX+d)
                                           // Log(string.Format("LD B, RES 0, (regs.IX + {0:X})", b));
                                regs.B = Res_R(0, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0x81: //LD C, RES 0, (regs.IX+d)
                                           // Log(string.Format("LD C, RES 0, (regs.IX + {0:X})", b));
                                regs.C = Res_R(0, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0x82: //LD D, RES 0, (regs.IX+d)
                                           // Log(string.Format("LD D, RES 0, (regs.IX + {0:X})", b));
                                regs.D = Res_R(0, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0x83: //LD E, RES 0, (regs.IX+d)
                                           // Log(string.Format("LD E, RES 0, (regs.IX + {0:X})", b));
                                regs.E = Res_R(0, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0x84: //LD H, RES 0, (regs.IX+d)
                                           // Log(string.Format("LD H, RES 0, (regs.IX + {0:X})", b));
                                regs.H = Res_R(0, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0x85: //LD L, RES 0, (regs.IX+d)
                                           // Log(string.Format("LD L, RES 0, (regs.IX + {0:X})", b));
                                regs.L = Res_R(0, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0x86:  //RES 0, (regs.IX + d)
                                            // Log(string.Format("RES 0, (regs.IX + {0:X})", b));
                                PokeByte(offset, Res_R(0, b));
                                break;

                                case 0x87: //LD A, RES 0, (regs.IX+d)
                                           // Log(string.Format("LD A, RES 0, (regs.IX + {0:X})", b));
                                regs.A = Res_R(0, b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0x88: //LD B, RES 1, (regs.IX+d)
                                           // Log(string.Format("LD B, RES 1, (regs.IX + {0:X})", b));
                                regs.B = Res_R(1, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0x89: //LD C, RES 1, (regs.IX+d)
                                           // Log(string.Format("LD C, RES 1, (regs.IX + {0:X})", b));
                                regs.C = Res_R(1, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0x8A: //LD D, RES 1, (regs.IX+d)
                                           // Log(string.Format("LD D, RES 1, (regs.IX + {0:X})", b));
                                regs.D = Res_R(1, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0x8B: //LD E, RES 1, (regs.IX+d)
                                           // Log(string.Format("LD E, RES 1, (regs.IX + {0:X})", b));
                                regs.E = Res_R(1, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0x8C: //LD H, RES 1, (regs.IX+d)
                                           // Log(string.Format("LD H, RES 1, (regs.IX + {0:X})", b));
                                regs.H = Res_R(1, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0x8D: //LD L, RES 1, (regs.IX+d)
                                           // Log(string.Format("LD L, RES 1, (regs.IX + {0:X})", b));
                                regs.L = Res_R(1, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0x8E:  //RES 1, (regs.IX + d)
                                            // Log(string.Format("RES 1, (regs.IX + {0:X})", b));
                                PokeByte(offset, Res_R(1, b));
                                break;

                                case 0x8F: //LD A, RES 1, (regs.IX+d)
                                           // Log(string.Format("LD A, RES 1, (regs.IX + {0:X})", b));
                                regs.A = Res_R(1, b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0x90: //LD B, RES 2, (regs.IX+d)
                                           // Log(string.Format("LD B, RES 2, (regs.IX + {0:X})", b));
                                regs.B = Res_R(2, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0x91: //LD C, RES 2, (regs.IX+d)
                                           // Log(string.Format("LD C, RES 2, (regs.IX + {0:X})", b));
                                regs.C = Res_R(2, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0x92: //LD D, RES 2, (regs.IX+d)
                                           // Log(string.Format("LD D, RES 2, (regs.IX + {0:X})", b));
                                regs.D = Res_R(2, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0x93: //LD E, RES 2, (regs.IX+d)
                                           // Log(string.Format("LD E, RES 2, (regs.IX + {0:X})", b));
                                regs.E = Res_R(2, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0x94: //LD H, RES 2, (regs.IX+d)
                                           // Log(string.Format("LD H, RES 2, (regs.IX + {0:X})", b));
                                regs.H = Res_R(2, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0x95: //LD L, RES 2, (regs.IX+d)
                                           // Log(string.Format("LD L, RES 2, (regs.IX + {0:X})", b));
                                regs.L = Res_R(2, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0x96:  //RES 2, (regs.IX + d)
                                            // Log(string.Format("RES 2, (regs.IX + {0:X})", b));
                                PokeByte(offset, Res_R(2, b));
                                break;

                                case 0x97: //LD A, RES 2, (regs.IX+d)
                                           // Log(string.Format("LD A, RES 2, (regs.IX + {0:X})", b));
                                regs.A = Res_R(2, b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0x98: //LD B, RES 3, (regs.IX+d)
                                           // Log(string.Format("LD B, RES 3, (regs.IX + {0:X})", b));
                                regs.B = Res_R(3, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0x99: //LD C, RES 3, (regs.IX+d)
                                           // Log(string.Format("LD C, RES 3, (regs.IX + {0:X})", b));
                                regs.C = Res_R(3, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0x9A: //LD D, RES 3, (regs.IX+d)
                                           // Log(string.Format("LD D, RES 3, (regs.IX + {0:X})", b));
                                regs.D = Res_R(3, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0x9B: //LD E, RES 3, (regs.IX+d)
                                           // Log(string.Format("LD E, RES 3, (regs.IX + {0:X})", b));
                                regs.E = Res_R(3, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0x9C: //LD H, RES 3, (regs.IX+d)
                                           // Log(string.Format("LD H, RES 3, (regs.IX + {0:X})", b));
                                regs.H = Res_R(3, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0x9D: //LD L, RES 3, (regs.IX+d)
                                           // Log(string.Format("LD L, RES 3, (regs.IX + {0:X})", b));
                                regs.L = Res_R(3, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0x9E:  //RES 3, (regs.IX + d)
                                            // Log(string.Format("RES 3, (regs.IX + {0:X})", b));
                                PokeByte(offset, Res_R(3, b));
                                break;

                                case 0x9F: //LD A, RES 3, (regs.IX+d)
                                           // Log(string.Format("LD A, RES 3, (regs.IX + {0:X})", b));
                                regs.A = Res_R(3, b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0xA0: //LD B, RES 4, (regs.IX+d)
                                           // Log(string.Format("LD B, RES 4, (regs.IX + {0:X})", b));
                                regs.B = Res_R(4, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0xA1: //LD C, RES 4, (regs.IX+d)
                                           // Log(string.Format("LD C, RES 4, (regs.IX + {0:X})", b));
                                regs.C = Res_R(4, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0xA2: //LD D, RES 4, (regs.IX+d)
                                           // Log(string.Format("LD D, RES 4, (regs.IX + {0:X})", b));
                                regs.D = Res_R(4, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0xA3: //LD E, RES 4, (regs.IX+d)
                                           // Log(string.Format("LD E, RES 4, (regs.IX + {0:X})", b));
                                regs.E = Res_R(4, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0xA4: //LD H, RES 4, (regs.IX+d)
                                           // Log(string.Format("LD H, RES 4, (regs.IX + {0:X})", b));
                                regs.H = Res_R(4, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0xA5: //LD L, RES 4, (regs.IX+d)
                                           // Log(string.Format("LD L, RES 4, (regs.IX + {0:X})", b));
                                regs.L = Res_R(4, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0xA6:  //RES 4, (regs.IX + d)
                                            // Log(string.Format("RES 4, (regs.IX + {0:X})", b));
                                PokeByte(offset, Res_R(4, b));
                                break;

                                case 0xA7: //LD A, RES 4, (regs.IX+d)
                                           // Log(string.Format("LD A, RES 4, (regs.IX + {0:X})", b));
                                regs.A = Res_R(4, b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0xA8: //LD B, RES 5, (regs.IX+d)
                                           // Log(string.Format("LD B, RES 5, (regs.IX + {0:X})", b));
                                regs.B = Res_R(5, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0xA9: //LD C, RES 5, (regs.IX+d)
                                           // Log(string.Format("LD C, RES 5, (regs.IX + {0:X})", b));
                                regs.C = Res_R(5, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0xAA: //LD D, RES 5, (regs.IX+d)
                                           // Log(string.Format("LD D, RES 5, (regs.IX + {0:X})", b));
                                regs.D = Res_R(5, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0xAB: //LD E, RES 5, (regs.IX+d)
                                           // Log(string.Format("LD E, RES 5, (regs.IX + {0:X})", b));
                                regs.E = Res_R(5, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0xAC: //LD H, RES 5, (regs.IX+d)
                                           // Log(string.Format("LD H, RES 5, (regs.IX + {0:X})", b));
                                regs.H = Res_R(5, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0xAD: //LD L, RES 5, (regs.IX+d)
                                           // Log(string.Format("LD L, RES 5, (regs.IX + {0:X})", b));
                                regs.L = Res_R(5, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0xAE:  //RES 5, (regs.IX + d)
                                            // Log(string.Format("RES 5, (regs.IX + {0:X})", b));
                                PokeByte(offset, Res_R(5, b));
                                break;

                                case 0xAF: //LD A, RES 5, (regs.IX+d)
                                           // Log(string.Format("LD A, RES 5, (regs.IX + {0:X})", b));
                                regs.A = Res_R(5, b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0xB0: //LD B, RES 6, (regs.IX+d)
                                           // Log(string.Format("LD B, RES 6, (regs.IX + {0:X})", b));
                                regs.B = Res_R(6, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0xB1: //LD C, RES 6, (regs.IX+d)
                                           // Log(string.Format("LD C, RES 6, (regs.IX + {0:X})", b));
                                regs.C = Res_R(6, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0xB2: //LD D, RES 6, (regs.IX+d)
                                           // Log(string.Format("LD D, RES 6, (regs.IX + {0:X})", b));
                                regs.D = Res_R(6, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0xB3: //LD E, RES 6, (regs.IX+d)
                                           // Log(string.Format("LD E, RES 6, (regs.IX + {0:X})", b));
                                regs.E = Res_R(6, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0xB4: //LD H, RES 5, (regs.IX+d)
                                           // Log(string.Format("LD H, RES 6, (regs.IX + {0:X})", b));
                                regs.H = Res_R(6, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0xB5: //LD L, RES 5, (regs.IX+d)
                                           // Log(string.Format("LD L, RES 6, (regs.IX + {0:X})", b));
                                regs.L = Res_R(6, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0xB6:  //RES 6, (regs.IX + d)
                                            // Log(string.Format("RES 6, (regs.IX + {0:X})", b));
                                PokeByte(offset, Res_R(6, b));
                                break;

                                case 0xB7: //LD A, RES 5, (regs.IX+d)
                                           // Log(string.Format("LD A, RES 6, (regs.IX + {0:X})", b));
                                regs.A = Res_R(6, b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0xB8: //LD B, RES 7, (regs.IX+d)
                                           // Log(string.Format("LD B, RES 7, (regs.IX + {0:X})", b));
                                regs.B = Res_R(7, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0xB9: //LD C, RES 7, (regs.IX+d)
                                           // Log(string.Format("LD C, RES 7, (regs.IX + {0:X})", b));
                                regs.C = Res_R(7, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0xBA: //LD D, RES 7, (regs.IX+d)
                                           // Log(string.Format("LD D, RES 7, (regs.IX + {0:X})", b));
                                regs.D = Res_R(7, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0xBB: //LD E, RES 7, (regs.IX+d)
                                           // Log(string.Format("LD E, RES 7, (regs.IX + {0:X})", b));
                                regs.E = Res_R(7, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0xBC: //LD H, RES 7, (regs.IX+d)
                                           // Log(string.Format("LD H, RES 7, (regs.IX + {0:X})", b));
                                regs.H = Res_R(7, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0xBD: //LD L, RES 7, (regs.IX+d)
                                           // Log(string.Format("LD L, RES 7, (regs.IX + {0:X})", b));
                                regs.L = Res_R(7, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0xBE:  //RES 7, (regs.IX + d)
                                            // Log(string.Format("RES 7, (regs.IX + {0:X})", b));
                                PokeByte(offset, Res_R(7, b));
                                break;

                                case 0xBF: //LD A, RES 7, (regs.IX+d)
                                           // Log(string.Format("LD A, RES 7, (regs.IX + {0:X})", b));
                                regs.A = Res_R(7, b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0xC0: //LD B, SET 0, (regs.IX+d)
                                           // Log(string.Format("LD B, SET 0, (regs.IX + {0:X})", b));
                                regs.B = Set_R(0, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0xC1: //LD C, SET 0, (regs.IX+d)
                                           // Log(string.Format("LD C, SET 0, (regs.IX + {0:X})", b));
                                regs.C = Set_R(0, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0xC2: //LD D, SET 0, (regs.IX+d)
                                           // Log(string.Format("LD D, SET 0, (regs.IX + {0:X})", b));
                                regs.D = Set_R(0, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0xC3: //LD E, SET 0, (regs.IX+d)
                                           // Log(string.Format("LD E, SET 0, (regs.IX + {0:X})", b));
                                regs.E = Set_R(0, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0xC4: //LD H, SET 0, (regs.IX+d)
                                           // Log(string.Format("LD H, SET 0, (regs.IX + {0:X})", b));
                                regs.H = Set_R(0, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0xC5: //LD L, SET 0, (regs.IX+d)
                                           // Log(string.Format("LD L, SET 0, (regs.IX + {0:X})", b));
                                regs.L = Set_R(0, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0xC6:  //SET 0, (regs.IX + d)
                                            // Log(string.Format("SET 0, (regs.IX + {0:X})", b));
                                PokeByte(offset, Set_R(0, b));
                                break;

                                case 0xC7: //LD A, SET 0, (regs.IX+d)
                                           // Log(string.Format("LD A, SET 0, (regs.IX + {0:X})", b));
                                regs.A = Set_R(0, b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0xC8: //LD B, SET 1, (regs.IX+d)
                                           // Log(string.Format("LD B, SET 1, (regs.IX + {0:X})", b));
                                regs.B = Set_R(1, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0xC9: //LD C, SET 0, (regs.IX+d)
                                           // Log(string.Format("LD C, SET 1, (regs.IX + {0:X})", b));
                                regs.C = Set_R(1, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0xCA: //LD D, SET 1, (regs.IX+d)
                                           // Log(string.Format("LD D, SET 1, (regs.IX + {0:X})", b));
                                regs.D = Set_R(1, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0xCB: //LD E, SET 1, (regs.IX+d)
                                           // Log(string.Format("LD E, SET 1, (regs.IX + {0:X})", b));
                                regs.E = Set_R(1, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0xCC: //LD H, SET 1, (regs.IX+d)
                                           // Log(string.Format("LD H, SET 1, (regs.IX + {0:X})", b));
                                regs.H = Set_R(1, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0xCD: //LD L, SET 1, (regs.IX+d)
                                           // Log(string.Format("LD L, SET 1, (regs.IX + {0:X})", b));
                                regs.L = Set_R(1, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0xCE:  //SET 1, (regs.IX + d)
                                            // Log(string.Format("SET 1, (regs.IX + {0:X})", b));
                                PokeByte(offset, Set_R(1, b));
                                break;

                                case 0xCF: //LD A, SET 1, (regs.IX+d)
                                           // Log(string.Format("LD A, SET 1, (regs.IX + {0:X})", b));
                                regs.A = Set_R(1, b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0xD0: //LD B, SET 2, (regs.IX+d)
                                           // Log(string.Format("LD B, SET 2, (regs.IX + {0:X})", b));
                                regs.B = Set_R(2, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0xD1: //LD C, SET 2, (regs.IX+d)
                                           // Log(string.Format("LD C, SET 2, (regs.IX + {0:X})", b));
                                regs.C = Set_R(2, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0xD2: //LD D, SET 2, (regs.IX+d)
                                           // Log(string.Format("LD D, SET 2, (regs.IX + {0:X})", b));
                                regs.D = Set_R(2, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0xD3: //LD E, SET 2, (regs.IX+d)
                                           // Log(string.Format("LD E, SET 2, (regs.IX + {0:X})", b));
                                regs.E = Set_R(2, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0xD4: //LD H, SET 21, (regs.IX+d)
                                           // Log(string.Format("LD H, SET 2, (regs.IX + {0:X})", b));
                                regs.H = Set_R(2, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0xD5: //LD L, SET 2, (regs.IX+d)
                                           // Log(string.Format("LD L, SET 2, (regs.IX + {0:X})", b));
                                regs.L = Set_R(2, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0xD6:  //SET 2, (regs.IX + d)
                                            // Log(string.Format("SET 2, (regs.IX + {0:X})", b));
                                PokeByte(offset, Set_R(2, b));
                                break;

                                case 0xD7: //LD A, SET 2, (regs.IX+d)
                                           // Log(string.Format("LD A, SET 2, (regs.IX + {0:X})", b));
                                regs.A = Set_R(2, b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0xD8: //LD B, SET 3, (regs.IX+d)
                                           // Log(string.Format("LD B, SET 3, (regs.IX + {0:X})", b));
                                regs.B = Set_R(3, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0xD9: //LD C, SET 3, (regs.IX+d)
                                           // Log(string.Format("LD C, SET 3, (regs.IX + {0:X})", b));
                                regs.C = Set_R(3, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0xDA: //LD D, SET 3, (regs.IX+d)
                                           // Log(string.Format("LD D, SET 3, (regs.IX + {0:X})", b));
                                regs.D = Set_R(3, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0xDB: //LD E, SET 3, (regs.IX+d)
                                           // Log(string.Format("LD E, SET 3, (regs.IX + {0:X})", b));
                                regs.E = Set_R(3, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0xDC: //LD H, SET 21, (regs.IX+d)
                                           // Log(string.Format("LD H, SET 3, (regs.IX + {0:X})", b));
                                regs.H = Set_R(3, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0xDD: //LD L, SET 3, (regs.IX+d)
                                           // Log(string.Format("LD L, SET 3, (regs.IX + {0:X})", b));
                                regs.L = Set_R(3, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0xDE:  //SET 3, (regs.IX + d)
                                            // Log(string.Format("SET 3, (regs.IX + {0:X})", b));
                                PokeByte(offset, Set_R(3, b));
                                break;

                                case 0xDF: //LD A, SET 3, (regs.IX+d)
                                           // Log(string.Format("LD A, SET 3, (regs.IX + {0:X})", b));
                                regs.A = Set_R(3, b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0xE0: //LD B, SET 4, (regs.IX+d)
                                           // Log(string.Format("LD B, SET 4, (regs.IX + {0:X})", b));
                                regs.B = Set_R(4, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0xE1: //LD C, SET 4, (regs.IX+d)
                                           // Log(string.Format("LD C, SET 4, (regs.IX + {0:X})", b));
                                regs.C = Set_R(4, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0xE2: //LD D, SET 4, (regs.IX+d)
                                           // Log(string.Format("LD D, SET 4, (regs.IX + {0:X})", b));
                                regs.D = Set_R(4, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0xE3: //LD E, SET 4, (regs.IX+d)
                                           // Log(string.Format("LD E, SET 4, (regs.IX + {0:X})", b));
                                regs.E = Set_R(4, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0xE4: //LD H, SET 4, (regs.IX+d)
                                           // Log(string.Format("LD H, SET 4, (regs.IX + {0:X})", b));
                                regs.H = Set_R(4, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0xE5: //LD L, SET 3, (regs.IX+d)
                                           // Log(string.Format("LD L, SET 4, (regs.IX + {0:X})", b));
                                regs.L = Set_R(4, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0xE6:  //SET 4, (regs.IX + d)
                                            // Log(string.Format("SET 4, (regs.IX + {0:X})", b));
                                PokeByte(offset, Set_R(4, b));
                                break;

                                case 0xE7: //LD A, SET 4, (regs.IX+d)
                                           // Log(string.Format("LD A, SET 4, (regs.IX + {0:X})", b));
                                regs.A = Set_R(4, b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0xE8: //LD B, SET 5, (regs.IX+d)
                                           // Log(string.Format("LD B, SET 5, (regs.IX + {0:X})", b));
                                regs.B = Set_R(5, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0xE9: //LD C, SET 5, (regs.IX+d)
                                           // Log(string.Format("LD C, SET 5, (regs.IX + {0:X})", b));
                                regs.C = Set_R(5, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0xEA: //LD D, SET 5, (regs.IX+d)
                                           // Log(string.Format("LD D, SET 5, (regs.IX + {0:X})", b));
                                regs.D = Set_R(5, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0xEB: //LD E, SET 5, (regs.IX+d)
                                           // Log(string.Format("LD E, SET 5, (regs.IX + {0:X})", b));
                                regs.E = Set_R(5, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0xEC: //LD H, SET 5, (regs.IX+d)
                                           // Log(string.Format("LD H, SET 5, (regs.IX + {0:X})", b));
                                regs.H = Set_R(5, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0xED: //LD L, SET 5, (regs.IX+d)
                                           // Log(string.Format("LD L, SET 5, (regs.IX + {0:X})", b));
                                regs.L = Set_R(5, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0xEE:  //SET 5, (regs.IX + d)
                                            // Log(string.Format("SET 5, (regs.IX + {0:X})", b));
                                PokeByte(offset, Set_R(5, b));
                                break;

                                case 0xEF: //LD A, SET 5, (regs.IX+d)
                                           // Log(string.Format("LD A, SET 5, (regs.IX + {0:X})", b));
                                regs.A = Set_R(5, b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0xF0: //LD B, SET 6, (regs.IX+d)
                                           // Log(string.Format("LD B, SET 6, (regs.IX + {0:X})", b));
                                regs.B = Set_R(6, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0xF1: //LD C, SET 6, (regs.IX+d)
                                           // Log(string.Format("LD C, SET 6, (regs.IX + {0:X})", b));
                                regs.C = Set_R(6, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0xF2: //LD D, SET 6, (regs.IX+d)
                                           // Log(string.Format("LD D, SET 6, (regs.IX + {0:X})", b));
                                regs.D = Set_R(6, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0xF3: //LD E, SET 6, (regs.IX+d)
                                           // Log(string.Format("LD E, SET 6, (regs.IX + {0:X})", b));
                                regs.E = Set_R(6, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0xF4: //LD H, SET 6, (regs.IX+d)
                                           // Log(string.Format("LD H, SET 6, (regs.IX + {0:X})", b));
                                regs.H = Set_R(6, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0xF5: //LD L, SET 6, (regs.IX+d)
                                           // Log(string.Format("LD L, SET 6, (regs.IX + {0:X})", b));
                                regs.L = Set_R(6, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0xF6:  //SET 6, (regs.IX + d)
                                            // Log(string.Format("SET 6, (regs.IX + {0:X})", b));
                                PokeByte(offset, Set_R(6, b));
                                break;

                                case 0xF7: //LD A, SET 6, (regs.IX+d)
                                           // Log(string.Format("LD A, SET 6, (regs.IX + {0:X})", b));
                                regs.A = Set_R(6, b);
                                PokeByte(offset, regs.A);
                                break;

                                case 0xF8: //LD B, SET 7, (regs.IX+d)
                                           // Log(string.Format("LD B, SET 7, (regs.IX + {0:X})", b));
                                regs.B = Set_R(7, b);
                                PokeByte(offset, regs.B);
                                break;

                                case 0xF9: //LD C, SET 7, (regs.IX+d)
                                           // Log(string.Format("LD C, SET 7, (regs.IX + {0:X})", b));
                                regs.C = Set_R(7, b);
                                PokeByte(offset, regs.C);
                                break;

                                case 0xFA: //LD D, SET 7, (regs.IX+d)
                                           // Log(string.Format("LD D, SET 7, (regs.IX + {0:X})", b));
                                regs.D = Set_R(7, b);
                                PokeByte(offset, regs.D);
                                break;

                                case 0xFB: //LD E, SET 7, (regs.IX+d)
                                           // Log(string.Format("LD E, SET 7, (regs.IX + {0:X})", b));
                                regs.E = Set_R(7, b);
                                PokeByte(offset, regs.E);
                                break;

                                case 0xFC: //LD H, SET 7, (regs.IX+d)
                                           // Log(string.Format("LD H, SET 7, (regs.IX + {0:X})", b));
                                regs.H = Set_R(7, b);
                                PokeByte(offset, regs.H);
                                break;

                                case 0xFD: //LD L, SET 7, (regs.IX+d)
                                           // Log(string.Format("LD L, SET 7, (regs.IX + {0:X})", b));
                                regs.L = Set_R(7, b);
                                PokeByte(offset, regs.L);
                                break;

                                case 0xFE:  //SET 7, (regs.IX + d)
                                            // Log(string.Format("SET 7, (regs.IX + {0:X})", b));
                                PokeByte(offset, Set_R(7, b));
                                break;

                                case 0xFF: //LD A, SET 7, (regs.IX + D)
                                regs.A = Set_R(7, b);
                                PokeByte(offset, regs.A);
                                break;

                                default:
                                System.String msg = "ERROR: Could not handle DDCB " + opcode.ToString();
                                System.Windows.Forms.MessageBox.Show(msg, "Opcode handler",
                                            System.Windows.Forms.MessageBoxButtons.OKCancel, System.Windows.Forms.MessageBoxIcon.Error);
                                break;
                            }
                            break;
                     }
                    #endregion

                    #region Pop/Push instructions
                    case 0xE1:  //POP regs.IX
                                // Log("POP regs.IX");
                    regs.IX = PopStack();
                    break;

                    case 0xE5:  //PUSH regs.IX
                                // Log("PUSH regs.IX");
                    Contend(regs.IR, 1, 1);
                    PushStack(regs.IX);
                    break;
                    #endregion

                    #region Exchange instruction
                    case 0xE3:  //EX (regs.SP), regs.IX
                                // Log("EX (regs.SP), regs.IX");
                                //disp = regs.IX;
                    addr = PeekWord(regs.SP);
                    Contend(regs.SP + 1, 1, 1);
                    PokeByte((ushort)(regs.SP + 1), (byte)(regs.IX >> 8));
                    PokeByte(regs.SP, (byte)(regs.IX & 0xff));
                    Contend(regs.SP, 1, 2);
                    regs.IX = addr;
                    regs.MemPtr = regs.IX;
                    break;
                    #endregion

                    #region Jump instruction
                    case 0xE9:  //JP (regs.IX)
                                // Log("JP (regs.IX)");
                    regs.PC = regs.IX;
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
                    Execute(opcode);  //Try to excute it as a normal instruction then
                    break;
                }
                break;
                #endregion

                #region Opcodes with ED prefix
                case 0xED:
                opcode = FetchInstruction();
                if(opcode < 0x40) {
                    break;
                }
                else
                    switch(opcode) {
                        case 0x40: //IN B, (C)
                                   // Log("IN B, (C)");
                        regs.B = In_BC();
                        break;

                        case 0x41: //Out (C), B
                                   // Log("OUT (C), B");
                        Out(regs.BC, regs.B);
                        break;

                        case 0x42:  //SBC regs.HL, regs.BC
                                    // Log("SBC regs.HL, regs.BC");
                                    //if (model == MachineModel._plus3)
                                    //    totalTStates += 7;
                                    //else
                        Contend(regs.IR, 1, 7);
                        regs.MemPtr = (ushort)(regs.HL + 1);
                        Sbc_RR(regs.BC);
                        break;

                        case 0x43:  //LD (nn), regs.BC
                        {
                            ushort disp = PeekWord(regs.PC);
                            // Log(String.Format("LD ({0:X}), regs.BC", disp));
                            PokeWord(disp, regs.BC);
                            regs.MemPtr = (ushort)(disp + 1);
                            regs.PC += 2;
                            break;
                        }
                        case 0x44:  //NEG
                        {
                            byte b = regs.A;
                            regs.A = 0;
                            Sub_R(b); //Sets flags correctly for NEG as well!
                            break;
                        }

                        case 0x45:  //RETN
                                    // Log("RET N");
                        regs.PC = PopStack();
                        iff_1 = iff_2;
                        regs.MemPtr = regs.PC;
                        break;

                        case 0x46:  //IM0
                                    // Log("IM 0");
                        interrupt_mode = 0;
                        break;

                        case 0x47:  //LD I, A
                                    // Log("LD I, A");
                                    //if (model == MachineModel._plus3)
                                    //    totalTStates += 1;
                                    //else
                        Contend(regs.IR, 1, 1);
                        regs.I = regs.A;
                        break;

                        case 0x48: //IN C, (C)
                                   // Log("IN C, (C)");
                        regs.C = In_BC();
                        //tstates = 0;
                        break;

                        case 0x49: //Out (C), C
                                   // Log("OUT (C), C");
                        Out(regs.BC, regs.C);
                        //tstates = 0;
                        break;

                        case 0x4A:  //ADC regs.HL, regs.BC
                                    // Log("ADC regs.HL, regs.BC");
                                    //if (model == MachineModel._plus3)
                                    //    totalTStates += 7;
                                    //else
                        Contend(regs.IR, 1, 7);
                        regs.MemPtr = (ushort)(regs.HL + 1);
                        Adc_RR(regs.BC);
                        break;

                        case 0x4B:  //LD regs.BC, (nn)
                        {
                            ushort disp = PeekWord(regs.PC);
                            // Log(String.Format("LD regs.BC, ({0:X})", disp));
                            regs.BC = PeekWord(disp);
                            regs.MemPtr = (ushort)(disp + 1);
                            regs.PC += 2;
                            break;
                        }

                        case 0x4C:  //NEG
                        {
                            byte b = regs.A;
                            regs.A = 0;
                            Sub_R(b); //Sets flags correctly for NEG as well!
                            break;
                        }
                        case 0x4D:  //RETI
                                    // Log("RETI");
                        regs.PC = PopStack();
                        iff_1 = iff_2;
                        regs.MemPtr = regs.PC;
                        break;

                        case 0x4F:  //LD R, A
                                    // Log("LD R, A");
                                    //if (model == MachineModel._plus3)
                                    //    totalTStates += 1;
                                    //else
                        Contend(regs.IR, 1, 1);
                        regs.R = regs.R_ = regs.A;
                        break;

                        case 0x50: //IN D, (C)
                                   // Log("IN D, (C)");
                        regs.D = In_BC();
                        //tstates = 0;
                        break;

                        case 0x51: //Out (C), D
                                   // Log("OUT (C), D");
                        Out(regs.BC, regs.D);
                        break;

                        case 0x52:  //SBC regs.HL, regs.DE
                                    // Log("SBC regs.HL, regs.DE");
                                    //if (model == MachineModel._plus3)
                                    //    totalTStates += 7;
                                    //else
                        Contend(regs.IR, 1, 7);
                        regs.MemPtr = (ushort)(regs.HL + 1);
                        Sbc_RR(regs.DE);
                        break;

                        case 0x53:  //LD (nn), regs.DE
                        {
                            ushort disp = PeekWord(regs.PC);
                            // Log(String.Format("LD ({0:X}), regs.DE", disp));
                            PokeWord(disp, regs.DE);
                            regs.MemPtr = (ushort)(disp + 1);
                            regs.PC += 2;
                            break;
                        }
                        case 0x54:  //NEG
                        {
                            byte b = regs.A;
                            regs.A = 0;
                            Sub_R(b); //Sets flags correctly for NEG as well!
                            break;
                        }
                        case 0x55:  //RETN
                                    // Log("RETN");
                        regs.PC = PopStack();
                        iff_1 = iff_2;
                        regs.MemPtr = regs.PC;
                        break;

                        case 0x56:  //IM1
                                    // Log("IM 1");
                        interrupt_mode = 1;
                        break;

                        case 0x57:  //LD A, I
                                    // Log("LD A, I");
                                    //if (model == MachineModel._plus3)
                                    //    totalTStates += 1;
                                    //else
                        Contend(regs.IR, 1, 1);
                        regs.A = regs.I;
                        /*
                        SetNeg(false);
                        SetHalf(false);
                        SetParity(iff_2);
                        SetSign((regs.A & BIT_F_SIGN) != 0);
                        SetZero(regs.A == 0);
                        SetF3((regs.A & BIT_F_3) != 0);
                        SetF5((regs.A & BIT_F_5) != 0);
                        */
                        regs.F = (byte)((regs.F & BIT_F_CARRY) | sz53[regs.A]);
                        regs.F |= (byte)(iff_2 ? BIT_F_PARITY : 0);
                        parityBitNeedsReset = true;
                        break;

                        case 0x58: //IN E, (C)
                                   // Log("IN E, (C)");
                        regs.E = In_BC();
                        //tstates = 0;
                        break;

                        case 0x59: //Out (C), E
                                   // Log("OUT (C), E");
                        Out(regs.BC, regs.E);
                        //t_states = 0;
                        break;

                        case 0x5A:  //ADC regs.HL, regs.DE
                                    // Log("ADC regs.HL, regs.DE");
                                    //if (model == MachineModel._plus3)
                                    //    totalTStates += 7;
                                    //else
                        Contend(regs.IR, 1, 7);
                        regs.MemPtr = (ushort)(regs.HL + 1);
                        Adc_RR(regs.DE);
                        break;

                        case 0x5B:  //LD regs.DE, (nn)
                        {
                            ushort disp = PeekWord(regs.PC);
                            // Log(String.Format("LD regs.DE, ({0:X})", disp));
                            regs.DE = PeekWord(disp);
                            regs.MemPtr = (ushort)(disp + 1);
                            regs.PC += 2;
                            break;
                        }
                        case 0x5C:  //NEG
                        {
                            byte b = regs.A;
                            regs.A = 0;
                            Sub_R(b); //Sets flags correctly for NEG as well!
                            break;
                        }
                        case 0x5D:  //RETN
                                    // Log("RETN");
                        regs.PC = PopStack();
                        iff_1 = iff_2;
                        regs.MemPtr = regs.PC;
                        break;

                        case 0x5E:  //IM2
                                    // Log("IM 2");
                        interrupt_mode = 2;
                        break;

                        case 0x5F:  //LD A, R
                                    // Log("LD A, R");
                                    //if (model == MachineModel._plus3)
                                    //    totalTStates += 1;
                                    //else
                        Contend(regs.IR, 1, 1);
                        regs.A = (byte)((regs.R & 0x7f) | (regs.R_ & 0x80));
                        regs.F = (byte)(((regs.F & BIT_F_CARRY) | sz53[regs.A]) | (iff_2 ? BIT_F_PARITY : 0));
                        parityBitNeedsReset = true;
                        break;

                        case 0x60: //IN H, (C)
                                   // Log("IN H, (C)");
                        regs.H = In_BC();
                        break;

                        case 0x61: //Out (C), H
                                   // Log("OUT (C), H");
                        Out(regs.BC, regs.H);
                        break;

                        case 0x62:  //SBC regs.HL, regs.HL
                                    // Log("SBC regs.HL, regs.HL");
                                    //if (model == MachineModel._plus3)
                                    //    totalTStates += 7;
                                    //else
                        Contend(regs.IR, 1, 7);
                        regs.MemPtr = (ushort)(regs.HL + 1);
                        Sbc_RR(regs.HL);
                        break;

                        case 0x63:  //LD (nn), regs.HL
                        {
                            ushort disp = PeekWord(regs.PC);
                            // Log(String.Format("LD ({0:X}), regs.HL", disp));
                            PokeWord(disp, regs.HL);
                            regs.MemPtr = (ushort)(disp + 1);
                            regs.PC += 2;
                            break;
                        }
                        case 0x64:  //NEG
                        {
                            byte b = regs.A;
                            regs.A = 0;
                            Sub_R(b); //Sets flags correctly for NEG as well!
                            break;
                        }
                        case 0x65:  //RETN
                                    // Log("RETN");
                        regs.PC = PopStack();
                        iff_1 = iff_2;
                        regs.MemPtr = regs.PC;
                        break;

                        case 0x67:  //RRD
                                    // Log("RRD");
                        temp = regs.A;
                        byte data = PeekByte(regs.HL);
                        regs.A = (byte)((regs.A & 0xf0) | (data & 0x0f));
                        data = (byte)((data >> 4) | (temp << 4));
                        Contend(regs.HL, 1, 4);
                        PokeByte(regs.HL, data);
                        regs.MemPtr = (ushort)(regs.HL + 1);
                        SetSign((regs.A & BIT_F_SIGN) != 0);
                        SetF3((regs.A & BIT_F_3) != 0);
                        SetF5((regs.A & BIT_F_5) != 0);
                        SetZero(regs.A == 0);
                        // SetParity(GetParity(A));
                        SetParity(parity[regs.A]);
                        SetHalf(false);
                        SetNeg(false);
                        break;

                        case 0x68: //IN L, (C)
                                   // Log("IN L, (C)");
                        regs.L = In_BC();
                        break;

                        case 0x69: //Out (C), L
                                   // Log("OUT (C), L");
                        Out(regs.BC, regs.L);
                        break;

                        case 0x6A:  //ADC regs.HL, regs.HL
                                    // Log("ADC regs.HL, regs.HL");
                                    //if (model == MachineModel._plus3)
                                    //    totalTStates += 7;
                                    //else
                        Contend(regs.IR, 1, 7);
                        regs.MemPtr = (ushort)(regs.HL + 1);
                        Adc_RR(regs.HL);
                        break;

                        case 0x6B:  //LD regs.HL, (nn)
                        {
                            ushort disp = PeekWord(regs.PC);
                            // Log(String.Format("LD regs.HL, ({0:X})", disp));
                            regs.HL = PeekWord(disp);
                            regs.MemPtr = (ushort)(disp + 1);
                            regs.PC += 2;
                            break;
                        }
                        case 0x6C:  //NEG
                        {
                            byte b = regs.A;
                            regs.A = 0;
                            Sub_R(b); //Sets flags correctly for NEG as well!
                            break;
                        }
                        case 0x6D:  //RETN
                                    // Log("RETN");
                        regs.PC = PopStack();
                        iff_1 = iff_2;
                        regs.MemPtr = regs.PC;
                        break;

                        case 0x6F:  //RLD
                                    // Log("RLD");
                        temp = regs.A;
                        data = PeekByte(regs.HL);
                        regs.A = (byte)((regs.A & 0xf0) | (data >> 4));
                        data = (byte)((data << 4) | (temp & 0x0f));
                        Contend(regs.HL, 1, 4);
                        PokeByte(regs.HL, data);
                        regs.MemPtr = (ushort)(regs.HL + 1);
                        SetSign((regs.A & BIT_F_SIGN) != 0);
                        SetF3((regs.A & BIT_F_3) != 0);
                        SetF5((regs.A & BIT_F_5) != 0);
                        SetZero(regs.A == 0);
                        // SetParity(GetParity(A)); // Not sure what to do here!
                        SetParity(parity[regs.A]);
                        SetHalf(false);
                        SetNeg(false);
                        break;

                        case 0x70:  //IN (C)
                                    // Log("IN (C)");
                        In_BC();
                        //tstates = 0;
                        break;

                        case 0x71:
                        // Log("OUT (C), 0");
                        Out(regs.BC, 0);
                        break;

                        case 0x72:  //SBC regs.HL, regs.SP
                                    // Log("SBC regs.HL, regs.SP");
                                    //if (model == MachineModel._plus3)
                                    //    totalTStates += 7;
                                    //else
                        Contend(regs.IR, 1, 7);
                        regs.MemPtr = (ushort)(regs.HL + 1);
                        Sbc_RR(regs.SP);
                        break;

                        case 0x73:  //LD (nn), regs.SP
                        {
                            ushort disp = PeekWord(regs.PC);
                            // Log(String.Format("LD ({0:X}), regs.SP", disp));
                            PokeWord(disp, regs.SP);
                            regs.MemPtr = (ushort)(disp + 1);
                            regs.PC += 2;
                            break;
                        }
                        case 0x74:  //NEG
                        {
                            byte b = regs.A;
                            regs.A = 0;
                            Sub_R(b); //Sets flags correctly for NEG as well!
                            break;
                        }
                        case 0x75:  //RETN
                                    // Log("RETN");
                        regs.PC = PopStack();
                        iff_1 = iff_2;
                        regs.MemPtr = regs.PC;
                        break;

                        case 0x76:  //IM 1
                                    // Log("IM 1");
                        interrupt_mode = 1;
                        break;

                        case 0x78:  //IN A, (C)
                                    // Log("IN A, (C)");
                        regs.MemPtr = (ushort)(regs.BC + 1);
                        regs.A = In_BC();
                        break;

                        case 0x79: //Out (C), A
                                   // Log("OUT (C), A");
                        regs.MemPtr = (ushort)(regs.BC + 1);
                        Out(regs.BC, regs.A);
                        break;

                        case 0x7A:  //ADC regs.HL, regs.SP
                                    // Log("ADC regs.HL, regs.SP");
                                    //if (model == MachineModel._plus3)
                                    //    totalTStates += 7;
                                    //else
                        Contend(regs.IR, 1, 7);
                        regs.MemPtr = (ushort)(regs.HL + 1);
                        Adc_RR(regs.SP);
                        break;

                        case 0x7B:  //LD regs.SP, (nn)
                        {
                            ushort disp = PeekWord(regs.PC);
                            // Log(String.Format("LD regs.SP, ({0:X})", disp));
                            regs.SP = PeekWord(disp);
                            regs.MemPtr = (ushort)(disp + 1);
                            regs.PC += 2;
                            break;
                        }
                        case 0x7C:  //NEG
                        {
                            byte b = regs.A;
                            regs.A = 0;
                            Sub_R(b); //Sets flags correctly for NEG as well!
                            break;
                        }
                        case 0x7D:  //RETN
                                    // Log("RETN");
                        regs.PC = PopStack();
                        iff_1 = iff_2;
                        regs.MemPtr = regs.PC;
                        break;

                        case 0x7E:  //IM 2
                                    // Log("IM 2");
                        interrupt_mode = 2;
                        break;

                        case 0xA0:  //LDI
                        {
                            byte b = PeekByte(regs.HL);
                            PokeByte(regs.DE, b);
                            Contend(regs.DE, 1, 2);
                            regs.HL++;
                            regs.DE++;
                            regs.BC--;
                            b += regs.A;
                            regs.F = (byte)((regs.F & (BIT_F_CARRY | BIT_F_ZERO | BIT_F_SIGN))
                                | (regs.BC != 0 ? BIT_F_PARITY : 0)
                                | (b & BIT_F_3) | ((b & 0x02) != 0 ? BIT_F_5 : 0));
                            break;
                        }
                        case 0xA1:  //CPI
                        {
                            byte b = PeekByte(regs.HL);
                            bool lastCarry = ((regs.F & BIT_F_CARRY) != 0);
                            Cp_R(b);
                            Contend(regs.HL, 1, 5);
                            regs.HL++;
                            regs.BC--;

                            regs.MemPtr++;
                            SetCarry(lastCarry);
                            SetParity(regs.BC != 0);
                            SetF3((((regs.A - b - ((regs.F & BIT_F_HALF) >> 4)) & 0xff) & BIT_F_3) != 0);
                            SetF5((((regs.A - b - ((regs.F & BIT_F_HALF) >> 4)) & 0xff) & BIT_F_NEG) != 0);
                            break;
                        }
                        case 0xA2:  //INI
                                    // Log("INI");
                        Contend(regs.IR, 1, 1);
                        byte result = In_BC();
                        PokeByte(regs.HL, result);
                        regs.MemPtr = (ushort)(regs.BC + 1);
                        regs.B = Dec(regs.B);
                        regs.HL++;
                        SetNeg((result & BIT_F_SIGN) != 0);
                        carry = ((regs.C + 1) & 0xff) + result;
                        SetCarry(carry > 0xff);
                        SetHalf((regs.F & BIT_F_CARRY) != 0);
                        SetParity(parity[((carry & 0x7) ^ regs.B)]);
                        break;

                        case 0xA3:  //OUTI
                        {
                            Contend(regs.IR, 1, 1);
                            // Log("OUTI");

                            regs.B = Dec(regs.B);
                            regs.MemPtr = (ushort)(regs.BC + 1);
                            byte b = PeekByte(regs.HL);
                            Out(regs.BC, b);

                            regs.HL++;
                            SetNeg((b & BIT_F_SIGN) != 0);
                            SetCarry((b + regs.L) > 0xff);
                            SetHalf((regs.F & BIT_F_CARRY) != 0);

                            SetParity(parity[(((b + regs.L) & 0x7) ^ regs.B)]);
                            //SetZero(B == 0);
                            break;
                        }
                        case 0xA8:  //LDD
                        {
                            byte b = PeekByte(regs.HL);
                            PokeByte(regs.DE, b);
                            Contend(regs.DE, 1, 2);
                           
                            regs.HL--;
                            regs.DE--;
                            regs.BC--;
                            b += regs.A;
                            regs.F = (byte)((regs.F & (BIT_F_CARRY | BIT_F_ZERO | BIT_F_SIGN)) 
                                    | (regs.BC != 0 ? BIT_F_PARITY : 0) 
                                    | ((b & BIT_F_3) | ((b & 0x02) != 0 ? BIT_F_5 : 0)));
                            break;
                        }
                        case 0xA9:  //CPD
                        {
                            bool lastCarry = ((regs.F & BIT_F_CARRY) != 0);
                            byte b = PeekByte(regs.HL);
                            Cp_R(b);
                            Contend(regs.HL, 1, 5);
                            regs.HL--;
                            regs.BC--;
                            SetParity(regs.BC != 0);
                            SetF3((((regs.A - b - ((regs.F & BIT_F_HALF) >> 4)) & 0xff) & BIT_F_3) != 0);
                            SetF5((((regs.A - b - ((regs.F & BIT_F_HALF) >> 4)) & 0xff) & BIT_F_NEG) != 0);
                            SetCarry(lastCarry);
                            regs.MemPtr--;
                            break;
                        }
                        case 0xAA:  //IND
                                    // Log("IND");
                        Contend(regs.IR, 1, 1);
                        result = In_BC();
                        PokeByte(regs.HL, result);
                        regs.MemPtr = (ushort)(regs.BC - 1);
                        regs.B = Dec(regs.B); ;
                        regs.HL--;
                        SetNeg((result & BIT_F_SIGN) != 0);
                        carry = ((regs.C - 1) & 0xff) + result;
                        SetCarry(carry > 0xff);
                        SetHalf((regs.F & BIT_F_CARRY) != 0);
                        SetParity(parity[((carry & 0x7) ^ regs.B)]);
                        break;

                        case 0xAB:  //OUTD
                        {
                            Contend(regs.IR, 1, 1);

                            regs.B = Dec(regs.B);
                            regs.MemPtr = (ushort)(regs.BC - 1);

                            byte b = PeekByte(regs.HL);
                            Out(regs.BC, b);

                            regs.HL--;

                            SetNeg((b & BIT_F_SIGN) != 0);
                            SetCarry((b + regs.L) > 0xff);
                            SetHalf((regs.F & BIT_F_CARRY) != 0);

                            SetParity(parity[(((b + regs.L) & 0x7) ^ regs.B)]);
                            break;
                        }
                        case 0xB0:  //LDIR
                        {
                            byte b = PeekByte(regs.HL);
                            PokeByte(regs.DE, b);
                            Contend(regs.DE, 1, 2);
                           
                            if(regs.BC != 1) {
                                regs.MemPtr = (ushort)(regs.PC - 1); //points to B0 byte
                            }

                            regs.BC--;
                            if(regs.BC != 0) {
                                Contend(regs.DE, 1, 5);
                                regs.PC -= 2;
                                SetF3((regs.PC & BIT_11) != 0);
                                SetF5((regs.PC & BIT_13) != 0);
                            }
                            else {
                                SetF3(((b + regs.A) & BIT_F_3) != 0);
                                SetF5(((b + regs.A) & BIT_F_NEG) != 0);
                            }

                            SetNeg(false);
                            SetHalf(false);
                            SetParity(regs.BC != 0);
                            regs.HL++;
                            regs.DE++;

                            break;
                        }
                        case 0xB1:  //CPIR
                        {
                            bool lastCarry = ((regs.F & BIT_F_CARRY) != 0);
                            byte b = PeekByte(regs.HL);
                            Cp_R(b);
                            Contend(regs.HL, 1, 5);

                            if((regs.BC == 1) || (regs.A == disp)) {
                                regs.MemPtr++;
                            }
                            else {
                                regs.MemPtr = (ushort)(regs.PC - 1);
                            }
                            regs.BC--;
                            SetCarry(lastCarry);
                            SetParity(regs.BC != 0);                        

                            if((regs.BC != 0) && ((regs.F & BIT_F_ZERO) == 0)) {
                                Contend(regs.HL, 1, 5);
                                regs.PC -= 2;
                                SetF3((regs.PC & BIT_11) != 0);
                                SetF5((regs.PC & BIT_13) != 0);
                            }
                            else {
                                SetF3((((regs.A - b - ((regs.F & BIT_F_HALF) >> 4)) & 0xff) & BIT_F_3) != 0);
                                SetF5((((regs.A - b - ((regs.F & BIT_F_HALF) >> 4)) & 0xff) & BIT_F_NEG) != 0);
                            }
                            regs.HL++;

                            break;
                        }
                        
                        case 0xB2:  //INIR
                                    // Log("INIR");
                        Contend(regs.IR, 1, 1);
                        result = In_BC();
                        PokeByte(regs.HL, result);
                        regs.MemPtr = (ushort)(regs.BC + 1);
                        regs.B = Dec(regs.B); ;
                        regs.HL++;

                        SetNeg((result & BIT_F_SIGN) != 0);
                        carry = ((regs.C + 1) & 0xff) + result;
                        SetCarry(carry > 0xff);
                        SetHalf((regs.F & BIT_F_CARRY) != 0);
                        SetParity(parity[((carry & 0x7) ^ regs.B)]);

                        if (regs.B != 0) {
                            Contend(regs.HL, 1, 5);
                            regs.PC -= 2;
                            SetF3((regs.PC & BIT_11) != 0);
                            SetF5((regs.PC & BIT_13) != 0);
                            int _b = regs.B;

                            if ((regs.F & BIT_F_CARRY) != 0) {
                                _b += ((regs.F & BIT_F_NEG) != 0 ? -1 : 1);
                                half = _b ^ regs.B;
                                SetHalf((half & BIT_F_HALF) != 0);
                            }
                            SetParity((parity[regs.F & BIT_F_PARITY] ^ parity[_b & 0x7]) != 0);
                        }
                        break;

                        case 0xB3:  //OTIR       
                        {
                            Contend(regs.IR, 1, 1);
                            regs.B = Dec(regs.B);
                            regs.MemPtr = (ushort)(regs.BC + 1);

                            byte b = PeekByte(regs.HL);
                           
                            Out(regs.BC, b);
                            regs.HL++;
                            int blockIOResult = b + regs.L;

                            SetNeg((b & BIT_F_SIGN) != 0);
                            SetCarry(blockIOResult > 0xff);
                            SetHalf((regs.F & BIT_F_CARRY) != 0);
                            SetParity(parity[((blockIOResult & 0x7) ^ regs.B)]);

                            if (regs.B != 0) {
                                Contend(regs.HL, 1, 5);
                                regs.PC -= 2;
                                SetF3((regs.PC & BIT_11) != 0);
                                SetF5((regs.PC & BIT_13) != 0);
                                int _b = regs.B;

                                if ((regs.F & BIT_F_CARRY) != 0) {
                                    _b += ((regs.F & BIT_F_NEG) != 0 ? -1 : 1);
                                    half = _b ^ regs.B;
                                    SetHalf((half & BIT_F_HALF) != 0);
                                }
                                SetParity((parity[regs.F & BIT_F_PARITY] ^ parity[_b & 0x7]) != 0);
                            }

                            break;
                        }
                        case 0xB8:  //LDDR
                        {
                            byte b = PeekByte(regs.HL);
                            PokeByte(regs.DE, b);
                            Contend(regs.DE, 1, 2);

                           
                            SetNeg(false);
                            SetHalf(false);

                            if(regs.BC != 1) {
                                regs.MemPtr = (ushort)(regs.PC - 1);
                            }

                            regs.BC--;

                            if (regs.BC != 0) {
                                Contend(regs.DE, 1, 5);
                                regs.PC -= 2;
                                SetF3((regs.PC & BIT_11) != 0);
                                SetF5((regs.PC & BIT_13) != 0);
                            }
                            else {
                                SetF3(((b + regs.A) & BIT_F_3) != 0);
                                SetF5(((b + regs.A) & BIT_F_NEG) != 0);
                            }
                            SetParity(regs.BC != 0);
                            regs.HL--;
                            regs.DE--;

                            break;
                        }
                        case 0xB9:  //CPDR
                        {
                            bool lastCarry = ((regs.F & BIT_F_CARRY) != 0);
                            byte b = PeekByte(regs.HL);
                            Cp_R(b);
                            Contend(regs.HL, 1, 5);

                            if((regs.BC == 1) || (regs.A == b)) {
                                regs.MemPtr--;
                            }
                            else {
                                regs.MemPtr = (ushort)(regs.PC - 1);
                            }

                            regs.BC--;
                            SetCarry(lastCarry);
                            SetParity(regs.BC != 0);
                            

                            if((regs.BC != 0) && ((regs.F & BIT_F_ZERO) == 0)) {
                                Contend(regs.HL, 1, 5);
                                regs.PC -= 2;
                                SetF3((regs.PC & BIT_11) != 0);
                                SetF5((regs.PC & BIT_13) != 0);
                            }
                            else {
                                SetF3((((regs.A - b - ((regs.F & BIT_F_HALF) >> 4)) & 0xff) & BIT_F_3) != 0);
                                SetF5((((regs.A - b - ((regs.F & BIT_F_HALF) >> 4)) & 0xff) & BIT_F_NEG) != 0);
                            }
                            regs.HL--;
                            break;
                        }
                        case 0xBA:  //INDR
                                    // Log("INDR");
                        Contend(regs.IR, 1, 1);
                        result = In_BC();
                        PokeByte(regs.HL, result);
                        regs.MemPtr = (ushort)(regs.BC - 1);
                        regs.B = Dec(regs.B);
                        int blockIOData = result;

                        SetNeg((result & BIT_F_SIGN) != 0);
                        carry = ((regs.C - 1) & 0xff) + result;
                        SetCarry(carry > 0xff);
                        SetHalf((regs.F & BIT_F_CARRY) != 0);
                        SetParity(parity[((carry & 0x7) ^ regs.B)]);            

                        if (regs.B != 0) {
                            Contend(regs.HL, 1, 5);
                            regs.PC -= 2;
                            SetF3((regs.PC & BIT_11) != 0);
                            SetF5((regs.PC & BIT_13) != 0);
                            int _b = regs.B;                         
                            if ((regs.F & BIT_F_CARRY) != 0) {
                                _b += ((result & BIT_F_NEG) != 0 ? -1 : 1);
                                half = _b ^ regs.B;
                                SetHalf((half & BIT_F_HALF) != 0);
                            }
                            SetParity((parity[regs.F & BIT_F_PARITY] ^ parity[_b & 0x7]) != 0);
                        }
                        regs.HL--;    
                        break;

                        case 0xBB:  //OTDR
                        {
                            Contend(regs.IR, 1, 1);
                            regs.B = Dec(regs.B);
                            regs.MemPtr = (ushort)(regs.BC - 1);

                            byte b = PeekByte(regs.HL);
                            Out(regs.BC, b);
                            regs.HL--;

                            blockIOData = b;
                            int blockIOResult = b + regs.L;
                            SetCarry(blockIOResult > 0xff);
                            SetParity(parity[((blockIOResult & 0x7) ^ regs.B)]);
                            SetHalf((regs.F & BIT_F_CARRY) != 0);
                            SetNeg((b & BIT_F_SIGN) != 0);

                            if (regs.B != 0) {
                                Contend(regs.HL, 1, 5);
                                regs.PC -= 2;
                                SetF3((regs.PC & BIT_11) != 0);
                                SetF5((regs.PC & BIT_13) != 0);
                                int _b = regs.B;

                                if ((regs.F & BIT_F_CARRY) != 0) {
                                    _b += ((regs.F & BIT_F_NEG) != 0 ? -1 : 1);
                                    half = _b ^ regs.B;
                                    SetHalf((half & BIT_F_HALF) != 0);
                                }
                                SetParity((parity[regs.F & BIT_F_PARITY] ^ parity[_b & 0x7]) != 0);
                            }

                            break;
                        }
                        default:
                        //According to Sean's doc: http://z80.info/z80sean.txt
                        //If an EDxx instruction is not listed, it should operate as two NOPs.
                        break;  //Carry on to next instruction then
                    }
                break;
                #endregion

                #region Opcodes with FD prefix (includes FDCB)
                case 0xFD:
                switch(opcode = FetchInstruction()) {
                    #region Addition instructions
                    case 0x09:  //ADD regs.IY, regs.BC
                                // Log("ADD regs.IY, regs.BC");
                    Contend(regs.IR, 1, 7);
                    regs.MemPtr = (ushort)(regs.IY + 1);
                    regs.IY = Add_RR(regs.IY, regs.BC);
                    break;

                    case 0x19:  //ADD regs.IY, regs.DE
                                // Log("ADD regs.IY, regs.DE");
                    Contend(regs.IR, 1, 7);
                    regs.MemPtr = (ushort)(regs.IY + 1);
                    regs.IY = Add_RR(regs.IY, regs.DE);
                    break;

                    case 0x29:  //ADD regs.IY, regs.IY
                                // Log("ADD regs.IY, regs.IY");
                    Contend(regs.IR, 1, 7);
                    regs.MemPtr = (ushort)(regs.IY + 1);
                    regs.IY = Add_RR(regs.IY, regs.IY);
                    break;

                    case 0x39:  //ADD regs.IY, regs.SP
                                // Log("ADD regs.IY, regs.SP");
                    Contend(regs.IR, 1, 7);
                    regs.MemPtr = (ushort)(regs.IY + 1);
                    regs.IY = Add_RR(regs.IY, regs.SP);
                    break;

                    case 0x84:  //ADD A, IYH
                                // Log("ADD A, IYH");
                    Add_R(regs.IYH);
                    break;

                    case 0x85:  //ADD A, IYL
                                // Log("ADD A, IYL");
                    Add_R(regs.IYL);
                    break;

                    case 0x86:  //Add A, (regs.IY+d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required
                                                        
                        Contend(regs.PC, 1, 5);
                        Add_R(PeekByte(offset));
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }
                    case 0x8C:  //ADC A, IYH
                                // Log("ADC A, IYH");
                    Adc_R(regs.IYH);
                    break;

                    case 0x8D:  //ADC A, IYL
                                // Log("ADC A, IYL");
                    Adc_R(regs.IYL);
                    break;

                    case 0x8E: //ADC A, (regs.IY+d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        Adc_R(PeekByte(offset));
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }
                    #endregion

                    #region Subtraction instructions
                    case 0x94:  //SUB A, IYH
                                // Log("SUB A, IYH");
                    Sub_R(regs.IYH);
                    break;

                    case 0x95:  //SUB A, IYL
                                // Log("SUB A, IYL");
                    Sub_R(regs.IYL);
                    break;

                    case 0x96:  //SUB (regs.IY + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        Sub_R(PeekByte(offset));
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }

                    case 0x9C:  //SBC A, IYH
                                // Log("SBC A, IYH");
                    Sbc_R(regs.IYH);
                    break;

                    case 0x9D:  //SBC A, IYL
                                // Log("SBC A, IYL");
                    Sbc_R(regs.IYL);
                    break;

                    case 0x9E:  //SBC A, (regs.IY + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        Sbc_R(PeekByte(offset));
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }
                    #endregion

                    #region Increment/Decrements
                    case 0x23:  //INC regs.IY
                                // Log("INC regs.IY");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 2;
                                //else
                    Contend(regs.IR, 1, 2);
                    regs.IY++;
                    break;

                    case 0x24:  //INC IYH
                                // Log("INC IYH");
                    regs.IYH = Inc(regs.IYH);
                    break;

                    case 0x25:  //DEC IYH
                                // Log("DEC IYH");
                    regs.IYH = Dec(regs.IYH);
                    break;

                    case 0x2B:  //DEC regs.IY
                                // Log("DEC regs.IY");
                                //if (model == MachineModel._plus3)
                                //    totalTStates += 2;
                                //else
                    Contend(regs.IR, 1, 2);
                    regs.IY--;
                    break;

                    case 0x2C:  //INC IYL
                                // Log("INC IYL");
                    regs.IYL = Inc(regs.IYL);
                    break;

                    case 0x2D:  //DEC IYL
                                // Log("DEC IYL");
                    regs.IYL = Dec(regs.IYL);
                    break;

                    case 0x34:  //INC (regs.IY + d)
                    {
                        int d = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + d); //The displacement required
                        Contend(regs.PC, 1, 5);
                        byte b = Inc(PeekByte(offset));
                        Contend(offset, 1, 1);
                        PokeByte(offset, b);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }
                    case 0x35:  //DEC (regs.IY + d)
                    {
                        int d = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + d); //The displacement required
                        Contend(regs.PC, 1, 5);
                        byte b = Dec(PeekByte(offset));
                        Contend(offset, 1, 1);
                        PokeByte(offset, b);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }
                    #endregion

                    #region Bitwise operators

                    case 0xA4:  //AND IYH
                                // Log("AND IYH");
                    And_R(regs.IYH);
                    break;

                    case 0xA5:  //AND IYL
                                // Log("AND IYL");
                    And_R(regs.IYL);
                    break;

                    case 0xA6:  //AND (regs.IY + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required
                                                    
                        Contend(regs.PC, 1, 5);
                        And_R(PeekByte(offset));
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }
                    case 0xAC:  //XOR IYH
                                // Log("XOR IYH");
                    Xor_R(regs.IYH);
                    break;

                    case 0xAD:  //XOR IYL
                                // Log("XOR IYL");
                    Xor_R(regs.IYL);
                    break;

                    case 0xAE:  //XOR (regs.IY + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        Xor_R(PeekByte(offset));
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }

                    case 0xB4:  //OR IYH
                                // Log("OR IYH");
                    Or_R(regs.IYH);
                    break;

                    case 0xB5:  //OR IYL
                                // Log("OR IYL");
                    Or_R(regs.IYL);
                    break;

                    case 0xB6:  //OR (regs.IY + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        Or_R(PeekByte(offset));
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }
                    #endregion

                    #region Compare operator
                    case 0xBC:  //CP IYH
                                // Log("CP IYH");
                    Cp_R(regs.IYH);
                    break;

                    case 0xBD:  //CP IYL
                                // Log("CP IYL");
                    Cp_R(regs.IYL);
                    break;

                    case 0xBE:  //CP (regs.IY + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        Cp_R(PeekByte(offset));
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }
                    #endregion

                    #region Load instructions
                    case 0x21:  //LD regs.IY, nn
                                // Log(string.Format("LD regs.IY, {0,-6:X}", PeekWord(regs.PC)));
                    regs.IY = PeekWord(regs.PC);
                    regs.PC += 2;
                    break;

                    case 0x22:  //LD (nn), regs.IY
                                // Log(string.Format("LD ({0:X}), regs.IY", PeekWord(regs.PC)));
                    addr = PeekWord(regs.PC);
                    PokeWord(addr, regs.IY);
                    regs.PC += 2;
                    regs.MemPtr = (ushort)(addr + 1);
                    break;

                    case 0x26:  //LD IYH, n
                                // Log(string.Format("LD IYH, {0:X}", PeekByte(regs.PC)));
                    regs.IYH = PeekByte(regs.PC);
                    regs.PC++;
                    break;

                    case 0x2A:  //LD regs.IY, (nn)
                                // Log(string.Format("LD regs.IY, ({0:X})", PeekWord(regs.PC)));
                    addr = PeekWord(regs.PC);
                    regs.IY = PeekWord(addr);
                    regs.PC += 2;
                    regs.MemPtr = (ushort)(addr + 1);
                    break;

                    case 0x2E:  //LD IYL, n
                                // Log(string.Format("LD IYL, {0:X}", PeekByte(regs.PC)));
                    regs.IYL = PeekByte(regs.PC);
                    regs.PC++;
                    break;

                    case 0x36:  //LD (regs.IY + d), n
                    {
                        int d = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + d); //The displacement required

                        byte b = PeekByte((ushort)(regs.PC + 1));
                        Contend(regs.PC + 1, 1, 2);
                        PokeByte(offset, b);
                        regs.PC += 2;
                        regs.MemPtr = offset;
                        break;
                    }
                    case 0x44:  //LD B, IYH
                                // Log("LD B, IYH");
                    regs.B = regs.IYH;
                    break;

                    case 0x45:  //LD B, IYL
                                // Log("LD B, IYL");
                    regs.B = regs.IYL;
                    break;

                    case 0x46:  //LD B, (regs.IY + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required
                        Contend(regs.PC, 1, 5);

                        regs.B = PeekByte(offset);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }
                    case 0x4C:  //LD C, IYH
                                // Log("LD C, IYH");
                    regs.C = regs.IYH;
                    break;

                    case 0x4D:  //LD C, IYL
                                // Log("LD C, IYL");
                    regs.C = regs.IYL;
                    break;

                    case 0x4E:  //LD C, (regs.IY + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required
                        Contend(regs.PC, 1, 5);

                        regs.C = PeekByte(offset);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }

                    case 0x54:  //LD D, IYH
                                // Log("LD D, IYH");
                                //tstates += 4;
                    regs.D = regs.IYH;
                    break;

                    case 0x55:  //LD D, IYL
                                // Log("LD D, IYL");
                                //tstates += 4;
                    regs.D = regs.IYL;
                    break;

                    case 0x56:  //LD D, (regs.IY + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required
                        Contend(regs.PC, 1, 5);

                        regs.D = PeekByte(offset);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }

                    case 0x5C:  //LD E, IYH
                                // Log("LD E, IYH");
                                //tstates += 4;
                    regs.E = regs.IYH;
                    break;

                    case 0x5D:  //LD E, IYL
                                // Log("LD E, IYL");
                                //tstates += 4;
                    regs.E = regs.IYL;
                    break;

                    case 0x5E:  //LD E, (regs.IY + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required
                        Contend(regs.PC, 1, 5);

                        regs.E = PeekByte(offset);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }

                    case 0x60:  //LD IYH, B
                                // Log("LD IYH, B");
                                //tstates += 4;
                    regs.IYH = regs.B;
                    break;

                    case 0x61:  //LD IYH, C
                                // Log("LD IYH, C");
                                //tstates += 4;
                    regs.IYH = regs.C;
                    break;

                    case 0x62:  //LD IYH, D
                                // Log("LD IYH, D");
                                //tstates += 4;
                    regs.IYH = regs.D;
                    break;

                    case 0x63:  //LD IYH, E
                                // Log("LD IYH, E");
                                //tstates += 4;
                    regs.IYH = regs.E;
                    break;

                    case 0x64:  //LD IYH, IYH
                                // Log("LD IYH, IYH");
                                //tstates += 4;
                    regs.IYH = regs.IYH;
                    break;

                    case 0x65:  //LD IYH, IYL
                                // Log("LD IYH, IYL");
                                //tstates += 4;
                    regs.IYH = regs.IYL;
                    break;

                    case 0x66:  //LD H, (regs.IY + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required
                        Contend(regs.PC, 1, 5);

                        regs.H = PeekByte(offset);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }

                    case 0x67:  //LD IYH, A
                                // Log("LD IYH, A");
                                //tstates += 4;
                    regs.IYH = regs.A;
                    break;

                    case 0x68:  //LD IYL, B
                                // Log("LD IYL, B");
                                //tstates += 4;
                    regs.IYL = regs.B;
                    break;

                    case 0x69:  //LD IYL, C
                                // Log("LD IYL, C");
                                //tstates += 4;
                    regs.IYL = regs.C;
                    break;

                    case 0x6A:  //LD IYL, D
                                // Log("LD IYL, D");
                                //tstates += 4;
                    regs.IYL = regs.D;
                    break;

                    case 0x6B:  //LD IYL, E
                                // Log("LD IYL, E");
                                //tstates += 4;
                    regs.IYL = regs.E;
                    break;

                    case 0x6C:  //LD IYL, IYH
                                // Log("LD IYL, IYH");
                                //tstates += 4;
                    regs.IYL = regs.IYH;
                    break;

                    case 0x6D:  //LD IYL, IYL
                                // Log("LD IYL, IYL");
                                //tstates += 4;
                    regs.IYL = regs.IYL;
                    break;

                    case 0x6E:  //LD L, (regs.IY + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required
                        Contend(regs.PC, 1, 5);

                        regs.L = PeekByte(offset);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }

                    case 0x6F:  //LD IYL, A
                                // Log("LD IYL, A");
                                //tstates += 4;
                    regs.IYL = regs.A;
                    break;

                    case 0x70:  //LD (regs.IY + d), B
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required
                                                   
                        Contend(regs.PC, 1, 5);
                        PokeByte(offset, regs.B);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }

                    case 0x71:  //LD (regs.IY + d), C
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        PokeByte(offset, regs.C);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }

                    case 0x72:  //LD (regs.IY + d), D
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        PokeByte(offset, regs.D);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }

                    case 0x73:  //LD (regs.IY + d), E
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        PokeByte(offset, regs.E);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }

                    case 0x74:  //LD (regs.IY + d), H
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        PokeByte(offset, regs.H);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }

                    case 0x75:  //LD (regs.IY + d), L
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        PokeByte(offset, regs.L);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }

                    case 0x77:  //LD (regs.IY + d), A
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        PokeByte(offset, regs.A);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }

                    case 0x7C:  //LD A, IYH
                                // Log("LD A, IYH");
                    regs.A = regs.IYH;
                    break;

                    case 0x7D:  //LD A, IYL
                                // Log("LD A, IYL");
                    regs.A = regs.IYL;
                    break;

                    case 0x7E:  //LD A, (regs.IY + d)
                    {
                        int b = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + b); //The displacement required

                        Contend(regs.PC, 1, 5);
                        regs.A = PeekByte(offset);
                        regs.PC++;
                        regs.MemPtr = offset;
                        break;
                    }
                    case 0xF9:  //LD regs.SP, regs.IY
                                // Log("LD regs.SP, regs.IY");
                    Contend(regs.IR, 1, 2);
                    regs.SP = regs.IY;
                    break;
                    #endregion

                    #region All FDCB instructions
                    case 0xCB: 
                    {
                        int d = GetDisplacement(PeekByte(regs.PC));
                        ushort offset = (ushort)(regs.IY + d); //The displacement required
                        regs.PC++;

                        //TEMP
                        //opcode = GetOpcode(regs.PC);      //The opcode comes after the offset byte!
                        opcode = PeekByte(regs.PC);

                        Contend(regs.PC, 1, 2);
                        regs.PC++;
                        byte disp = PeekByte(offset);
                        Contend(offset, 1, 1);
                        // if ((opcode >= 0x40) && (opcode <= 0x7f))
                        regs.MemPtr = offset;

                        switch(opcode) {
                            case 0x00: //LD B, RLC (regs.IY+d)
                                        // Log(string.Format("LD B, RLC (regs.IY + {0:X})", disp));
                            regs.B = Rlc_R(disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0x01: //LD C, RLC (regs.IY+d)
                                        // Log(string.Format("LD C, RLC (regs.IY + {0:X})", disp));
                            regs.C = Rlc_R(disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0x02: //LD D, RLC (regs.IY+d)
                                        // Log(string.Format("LD D, RLC (regs.IY + {0:X})", disp));
                            regs.D = Rlc_R(disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0x03: //LD E, RLC (regs.IY+d)
                                        // Log(string.Format("LD E, RLC (regs.IY + {0:X})", disp));
                            regs.E = Rlc_R(disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0x04: //LD H, RLC (regs.IY+d)
                                        // Log(string.Format("LD H, RLC (regs.IY + {0:X})", disp));
                            regs.H = Rlc_R(disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0x05: //LD L, RLC (regs.IY+d)
                                        // Log(string.Format("LD L, RLC (regs.IY + {0:X})", disp));
                            regs.L = Rlc_R(disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0x06:  //RLC (regs.IY + d)
                                        // Log(string.Format("RLC (regs.IY + {0:X})", disp));
                            PokeByte(offset, Rlc_R(disp));
                            break;

                            case 0x07: //LD A, RLC (regs.IY+d)
                                        // Log(string.Format("LD A, RLC (regs.IY + {0:X})", disp));
                            regs.A = Rlc_R(disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0x08: //LD B, RRC (regs.IY+d)
                                        // Log(string.Format("LD B, RRC (regs.IY + {0:X})", disp));
                            regs.B = Rrc_R(disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0x09: //LD C, RRC (regs.IY+d)
                                        // Log(string.Format("LD C, RRC (regs.IY + {0:X})", disp));
                            regs.C = Rrc_R(disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0x0A: //LD D, RRC (regs.IY+d)
                                        // Log(string.Format("LD D, RRC (regs.IY + {0:X})", disp));
                            regs.D = Rrc_R(disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0x0B: //LD E, RRC (regs.IY+d)
                                        // Log(string.Format("LD E, RRC (regs.IY + {0:X})", disp));
                            regs.E = Rrc_R(disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0x0C: //LD H, RRC (regs.IY+d)
                                        // Log(string.Format("LD H, RRC (regs.IY + {0:X})", disp));
                            regs.H = Rrc_R(disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0x0D: //LD L, RRC (regs.IY+d)
                                        // Log(string.Format("LD L, RRC (regs.IY + {0:X})", disp));
                            regs.L = Rrc_R(disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0x0E:  //RRC (regs.IY + d)
                                        // Log(string.Format("RRC (regs.IY + {0:X})", disp));
                            PokeByte(offset, Rrc_R(disp));
                            break;

                            case 0x0F: //LD A, RRC (regs.IY+d)
                                        // Log(string.Format("LD A, RRC (regs.IY + {0:X})", disp));
                            regs.A = Rrc_R(disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0x10: //LD B, RL (regs.IY+d)
                                        // Log(string.Format("LD B, RL (regs.IY + {0:X})", disp));
                            regs.B = Rl_R(disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0x11: //LD C, RL (regs.IY+d)
                                        // Log(string.Format("LD C, RL (regs.IY + {0:X})", disp));
                            regs.C = Rl_R(disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0x12: //LD D, RL (regs.IY+d)
                                        // Log(string.Format("LD D, RL (regs.IY + {0:X})", disp));
                            regs.D = Rl_R(disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0x13: //LD E, RL (regs.IY+d)
                                        // Log(string.Format("LD E, RL (regs.IY + {0:X})", disp));
                            regs.E = Rl_R(disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0x14: //LD H, RL (regs.IY+d)
                                        // Log(string.Format("LD H, RL (regs.IY + {0:X})", disp));
                            regs.H = Rl_R(disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0x15: //LD L, RL (regs.IY+d)
                                        // Log(string.Format("LD L, RL (regs.IY + {0:X})", disp));
                            regs.L = Rl_R(disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0x16:  //RL (regs.IY + d)
                                        // Log(string.Format("RL (regs.IY + {0:X})", disp));
                            PokeByte(offset, Rl_R(disp));

                            break;

                            case 0x17: //LD A, RL (regs.IY+d)
                                        // Log(string.Format("LD A, RL (regs.IY + {0:X})", disp));
                            regs.A = Rl_R(disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0x18: //LD B, RR (regs.IY+d)
                                        // Log(string.Format("LD B, RR (regs.IY + {0:X})", disp));
                            regs.B = Rr_R(disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0x19: //LD C, RR (regs.IY+d)
                                        // Log(string.Format("LD C, RR (regs.IY + {0:X})", disp));
                            regs.C = Rr_R(disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0x1A: //LD D, RR (regs.IY+d)
                                        // Log(string.Format("LD D, RR (regs.IY + {0:X})", disp));
                            regs.D = Rr_R(disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0x1B: //LD E, RR (regs.IY+d)
                                        // Log(string.Format("LD E, RR (regs.IY + {0:X})", disp));
                            regs.E = Rr_R(disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0x1C: //LD H, RR (regs.IY+d)
                                        // Log(string.Format("LD H, RR (regs.IY + {0:X})", disp));
                            regs.H = Rr_R(disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0x1D: //LD L, RRC (regs.IY+d)
                                        // Log(string.Format("LD L, RR (regs.IY + {0:X})", disp));
                            regs.L = Rr_R(disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0x1E:  //RR (regs.IY + d)
                                        // Log(string.Format("RR (regs.IY + {0:X})", disp));
                            PokeByte(offset, Rr_R(disp));
                            break;

                            case 0x1F: //LD A, RRC (regs.IY+d)
                                        // Log(string.Format("LD A, RR (regs.IY + {0:X})", disp));
                            regs.A = Rr_R(disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0x20: //LD B, SLA (regs.IY+d)
                                        // Log(string.Format("LD B, SLA (regs.IY + {0:X})", disp));
                            regs.B = Sla_R(disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0x21: //LD C, SLA (regs.IY+d)
                                        // Log(string.Format("LD C, SLA (regs.IY + {0:X})", disp));
                            regs.C = Sla_R(disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0x22: //LD D, SLA (regs.IY+d)
                                        // Log(string.Format("LD D, SLA (regs.IY + {0:X})", disp));
                            regs.D = Sla_R(disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0x23: //LD E, SLA (regs.IY+d)
                                        // Log(string.Format("LD E, SLA (regs.IY + {0:X})", disp));
                            regs.E = Sla_R(disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0x24: //LD H, SLA (regs.IY+d)
                                        // Log(string.Format("LD H, SLA (regs.IY + {0:X})", disp));
                            regs.H = Sla_R(disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0x25: //LD L, SLA (regs.IY+d)
                                        // Log(string.Format("LD L, SLA (regs.IY + {0:X})", disp));
                            regs.L = Sla_R(disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0x26:  //SLA (regs.IY + d)
                                        // Log(string.Format("SLA (regs.IY + {0:X})", disp));
                            PokeByte(offset, Sla_R(disp));
                            break;

                            case 0x27: //LD A, SLA (regs.IY+d)
                                        // Log(string.Format("LD A, SLA (regs.IY + {0:X})", disp));
                            regs.A = Sla_R(disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0x28: //LD B, SRA (regs.IY+d)
                                        // Log(string.Format("LD B, SRA (regs.IY + {0:X})", disp));
                            regs.B = Sra_R(disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0x29: //LD C, SRA (regs.IY+d)
                                        // Log(string.Format("LD C, SRA (regs.IY + {0:X})", disp));
                            regs.C = Sra_R(disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0x2A: //LD D, SRA (regs.IY+d)
                                        // Log(string.Format("LD D, SRA (regs.IY + {0:X})", disp));
                            regs.D = Sra_R(disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0x2B: //LD E, SRA (regs.IY+d)
                                        // Log(string.Format("LD E, SRA (regs.IY + {0:X})", disp));
                            regs.E = Sra_R(disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0x2C: //LD H, SRA (regs.IY+d)
                                        // Log(string.Format("LD H, SRA (regs.IY + {0:X})", disp));
                            regs.H = Sra_R(disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0x2D: //LD L, SRA (regs.IY+d)
                                        // Log(string.Format("LD L, SRA (regs.IY + {0:X})", disp));
                            regs.L = Sra_R(disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0x2E:  //SRA (regs.IY + d)
                                        // Log(string.Format("SRA (regs.IY + {0:X})", disp));
                            PokeByte(offset, Sra_R(disp));
                            break;

                            case 0x2F: //LD A, SRA (regs.IY+d)
                                        // Log(string.Format("LD A, SRA (regs.IY + {0:X})", disp));
                            regs.A = Sra_R(disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0x30: //LD B, SLL (regs.IY+d)
                                        // Log(string.Format("LD B, SLL (regs.IY + {0:X})", disp));
                            regs.B = Sll_R(disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0x31: //LD C, SLL (regs.IY+d)
                                        // Log(string.Format("LD C, SLL (regs.IY + {0:X})", disp));
                            regs.C = Sll_R(disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0x32: //LD D, SLL (regs.IY+d)
                                        // Log(string.Format("LD D, SLL (regs.IY + {0:X})", disp));
                            regs.D = Sll_R(disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0x33: //LD E, SLL (regs.IY+d)
                                        // Log(string.Format("LD E, SLL (regs.IY + {0:X})", disp));
                            regs.E = Sll_R(disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0x34: //LD H, SLL (regs.IY+d)
                                        // Log(string.Format("LD H, SLL (regs.IY + {0:X})", disp));
                            regs.H = Sll_R(disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0x35: //LD L, SLL (regs.IY+d)
                                        // Log(string.Format("LD L, SLL (regs.IY + {0:X})", disp));
                            regs.L = Sll_R(disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0x36:  //SLL (regs.IY + d)
                                        // Log(string.Format("SLL (regs.IY + {0:X})", disp));
                            PokeByte(offset, Sll_R(disp));
                            break;

                            case 0x37: //LD A, SLL (regs.IY+d)
                                        // Log(string.Format("LD A, SLL (regs.IY + {0:X})", disp));
                            regs.A = Sll_R(disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0x38: //LD B, SRL (regs.IY+d)
                                        // Log(string.Format("LD B, SRL (regs.IY + {0:X})", disp));
                            regs.B = Srl_R(disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0x39: //LD C, SRL (regs.IY+d)
                                        // Log(string.Format("LD C, SRL (regs.IY + {0:X})", disp));
                            regs.C = Srl_R(disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0x3A: //LD D, SRL (regs.IY+d)
                                        // Log(string.Format("LD D, SRL (regs.IY + {0:X})", disp));
                            regs.D = Srl_R(disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0x3B: //LD E, SRL (regs.IY+d)
                                        // Log(string.Format("LD E, SRL (regs.IY + {0:X})", disp));
                            regs.E = Srl_R(disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0x3C: //LD H, SRL (regs.IY+d)
                                        // Log(string.Format("LD H, SRL (regs.IY + {0:X})", disp));
                            regs.H = Srl_R(disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0x3D: //LD L, SRL (regs.IY+d)
                                        // Log(string.Format("LD L, SRL (regs.IY + {0:X})", disp));
                            regs.L = Srl_R(disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0x3E:  //SRL (regs.IY + d)
                                        // Log(string.Format("SRL (regs.IY + {0:X})", disp));
                            PokeByte(offset, Srl_R(disp));
                            break;

                            case 0x3F: //LD A, SRL (regs.IY+d)
                                        // Log(string.Format("LD A, SRL (regs.IY + {0:X})", disp));
                            regs.A = Srl_R(disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0x40:  //BIT 0, (regs.IY + d)
                            case 0x41:  //BIT 0, (regs.IY + d)
                            case 0x42:  //BIT 0, (regs.IY + d)
                            case 0x43:  //BIT 0, (regs.IY + d)
                            case 0x44:  //BIT 0, (regs.IY + d)
                            case 0x45:  //BIT 0, (regs.IY + d)
                            case 0x46:  //BIT 0, (regs.IY + d)
                            case 0x47:  //BIT 0, (regs.IY + d)
                                        // Log(string.Format("BIT 0, (regs.IY + {0:X})", disp));
                                Bit_MemPtr(0, disp);
                                break;

                            case 0x48:  //BIT 1, (regs.IY + d)
                            case 0x49:  //BIT 1, (regs.IY + d)
                            case 0x4A:  //BIT 1, (regs.IY + d)
                            case 0x4B:  //BIT 1, (regs.IY + d)
                            case 0x4C:  //BIT 1, (regs.IY + d)
                            case 0x4D:  //BIT 1, (regs.IY + d)
                            case 0x4E:  //BIT 1, (regs.IY + d)
                            case 0x4F:  //BIT 1, (regs.IY + d)
                                        // Log(string.Format("BIT 1, (regs.IY + {0:X})", disp));
                                Bit_MemPtr(1, disp);
                                break;

                            case 0x50:  //BIT 2, (regs.IY + d)
                            case 0x51:  //BIT 2, (regs.IY + d)
                            case 0x52:  //BIT 2, (regs.IY + d)
                            case 0x53:  //BIT 2, (regs.IY + d)
                            case 0x54:  //BIT 2, (regs.IY + d)
                            case 0x55:  //BIT 2, (regs.IY + d)
                            case 0x56:  //BIT 2, (regs.IY + d)
                            case 0x57:  //BIT 2, (regs.IY + d)
                                        // Log(string.Format("BIT 2, (regs.IY + {0:X})", disp));
                                Bit_MemPtr(2, disp);
                                break;

                            case 0x58:  //BIT 3, (regs.IY + d)
                            case 0x59:  //BIT 3, (regs.IY + d)
                            case 0x5A:  //BIT 3, (regs.IY + d)
                            case 0x5B:  //BIT 3, (regs.IY + d)
                            case 0x5C:  //BIT 3, (regs.IY + d)
                            case 0x5D:  //BIT 3, (regs.IY + d)
                            case 0x5E:  //BIT 3, (regs.IY + d)
                            case 0x5F:  //BIT 3, (regs.IY + d)
                                        // Log(string.Format("BIT 3, (regs.IY + {0:X})", disp));
                                Bit_MemPtr(3, disp);
                                break;

                            case 0x60:  //BIT 4, (regs.IY + d)
                            case 0x61:  //BIT 4, (regs.IY + d)
                            case 0x62:  //BIT 4, (regs.IY + d)
                            case 0x63:  //BIT 4, (regs.IY + d)
                            case 0x64:  //BIT 4, (regs.IY + d)
                            case 0x65:  //BIT 4, (regs.IY + d)
                            case 0x66:  //BIT 4, (regs.IY + d)
                            case 0x67:  //BIT 4, (regs.IY + d)
                                        // Log(string.Format("BIT 4, (regs.IY + {0:X})", disp));
                                Bit_MemPtr(4, disp);
                                break;

                            case 0x68:  //BIT 5, (regs.IY + d)
                            case 0x69:  //BIT 5, (regs.IY + d)
                            case 0x6A:  //BIT 5, (regs.IY + d)
                            case 0x6B:  //BIT 5, (regs.IY + d)
                            case 0x6C:  //BIT 5, (regs.IY + d)
                            case 0x6D:  //BIT 5, (regs.IY + d)
                            case 0x6E:  //BIT 5, (regs.IY + d)
                            case 0x6F:  //BIT 5, (regs.IY + d)
                                        // Log(string.Format("BIT 5, (regs.IY + {0:X})", disp));
                                Bit_MemPtr(5, disp);
                                break;

                            case 0x70://BIT 6, (regs.IY + d)
                            case 0x71://BIT 6, (regs.IY + d)
                            case 0x72://BIT 6, (regs.IY + d)
                            case 0x73://BIT 6, (regs.IY + d)
                            case 0x74://BIT 6, (regs.IY + d)
                            case 0x75://BIT 6, (regs.IY + d)
                            case 0x76://BIT 6, (regs.IY + d)
                            case 0x77:  //BIT 6, (regs.IY + d)
                                        // Log(string.Format("BIT 6, (regs.IY + {0:X})", disp));
                                Bit_MemPtr(6, disp);
                                break;

                            case 0x78:  //BIT 7, (regs.IY + d)
                            case 0x79:  //BIT 7, (regs.IY + d)
                            case 0x7A:  //BIT 7, (regs.IY + d)
                            case 0x7B:  //BIT 7, (regs.IY + d)
                            case 0x7C:  //BIT 7, (regs.IY + d)
                            case 0x7D:  //BIT 7, (regs.IY + d)
                            case 0x7E:  //BIT 7, (regs.IY + d)
                            case 0x7F:  //BIT 7, (regs.IY + d)
                                        // Log(string.Format("BIT 7, (regs.IY + {0:X})", disp));
                            Bit_MemPtr(7, disp);
                            break;

                            case 0x80: //LD B, RES 0, (regs.IY+d)
                                        // Log(string.Format("LD B, RES 0, (regs.IY + {0:X})", disp));
                            regs.B = Res_R(0, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0x81: //LD C, RES 0, (regs.IY+d)
                                        // Log(string.Format("LD C, RES 0, (regs.IY + {0:X})", disp));
                            regs.C = Res_R(0, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0x82: //LD D, RES 0, (regs.IY+d)
                                        // Log(string.Format("LD D, RES 0, (regs.IY + {0:X})", disp));
                            regs.D = Res_R(0, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0x83: //LD E, RES 0, (regs.IY+d)
                                        // Log(string.Format("LD E, RES 0, (regs.IY + {0:X})", disp));
                            regs.E = Res_R(0, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0x84: //LD H, RES 0, (regs.IY+d)
                                        // Log(string.Format("LD H, RES 0, (regs.IY + {0:X})", disp));
                            regs.H = Res_R(0, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0x85: //LD L, RES 0, (regs.IY+d)
                                        // Log(string.Format("LD L, RES 0, (regs.IY + {0:X})", disp));
                            regs.L = Res_R(0, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0x86:  //RES 0, (regs.IY + d)
                                        // Log(string.Format("RES 0, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Res_R(0, disp));
                            break;

                            case 0x87: //LD A, RES 0, (regs.IY+d)
                                        // Log(string.Format("LD A, RES 0, (regs.IY + {0:X})", disp));
                            regs.A = Res_R(0, disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0x88: //LD B, RES 1, (regs.IY+d)
                                        // Log(string.Format("LD B, RES 1, (regs.IY + {0:X})", disp));
                            regs.B = Res_R(1, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0x89: //LD C, RES 1, (regs.IY+d)
                                        // Log(string.Format("LD C, RES 1, (regs.IY + {0:X})", disp));
                            regs.C = Res_R(1, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0x8A: //LD D, RES 1, (regs.IY+d)
                                        // Log(string.Format("LD D, RES 1, (regs.IY + {0:X})", disp));
                            regs.D = Res_R(1, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0x8B: //LD E, RES 1, (regs.IY+d)
                                        // Log(string.Format("LD E, RES 1, (regs.IY + {0:X})", disp));
                            regs.E = Res_R(1, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0x8C: //LD H, RES 1, (regs.IY+d)
                                        // Log(string.Format("LD H, RES 1, (regs.IY + {0:X})", disp));
                            regs.H = Res_R(1, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0x8D: //LD L, RES 1, (regs.IY+d)
                                        // Log(string.Format("LD L, RES 1, (regs.IY + {0:X})", disp));
                            regs.L = Res_R(1, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0x8E:  //RES 1, (regs.IY + d)
                                        // Log(string.Format("RES 1, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Res_R(1, disp));
                            break;

                            case 0x8F: //LD A, RES 1, (regs.IY+d)
                                        // Log(string.Format("LD A, RES 1, (regs.IY + {0:X})", disp));
                            regs.A = Res_R(1, disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0x90: //LD B, RES 2, (regs.IY+d)
                                        // Log(string.Format("LD B, RES 2, (regs.IY + {0:X})", disp));
                            regs.B = Res_R(2, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0x91: //LD C, RES 2, (regs.IY+d)
                                        // Log(string.Format("LD C, RES 2, (regs.IY + {0:X})", disp));
                            regs.C = Res_R(2, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0x92: //LD D, RES 2, (regs.IY+d)
                                        // Log(string.Format("LD D, RES 2, (regs.IY + {0:X})", disp));
                            regs.D = Res_R(2, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0x93: //LD E, RES 2, (regs.IY+d)
                                        // Log(string.Format("LD E, RES 2, (regs.IY + {0:X})", disp));
                            regs.E = Res_R(2, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0x94: //LD H, RES 2, (regs.IY+d)
                                        // Log(string.Format("LD H, RES 2, (regs.IY + {0:X})", disp));
                            regs.H = Res_R(2, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0x95: //LD L, RES 2, (regs.IY+d)
                                        // Log(string.Format("LD L, RES 2, (regs.IY + {0:X})", disp));
                            regs.L = Res_R(2, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0x96:  //RES 2, (regs.IY + d)
                                        // Log(string.Format("RES 2, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Res_R(2, disp));
                            break;

                            case 0x97: //LD A, RES 2, (regs.IY+d)
                                        // Log(string.Format("LD A, RES 2, (regs.IY + {0:X})", disp));
                            regs.A = Res_R(2, disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0x98: //LD B, RES 3, (regs.IY+d)
                                        // Log(string.Format("LD B, RES 3, (regs.IY + {0:X})", disp));
                            regs.B = Res_R(3, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0x99: //LD C, RES 3, (regs.IY+d)
                                        // Log(string.Format("LD C, RES 3, (regs.IY + {0:X})", disp));
                            regs.C = Res_R(3, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0x9A: //LD D, RES 3, (regs.IY+d)
                                        // Log(string.Format("LD D, RES 3, (regs.IY + {0:X})", disp));
                            regs.D = Res_R(3, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0x9B: //LD E, RES 3, (regs.IY+d)
                                        // Log(string.Format("LD E, RES 3, (regs.IY + {0:X})", disp));
                            regs.E = Res_R(3, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0x9C: //LD H, RES 3, (regs.IY+d)
                                        // Log(string.Format("LD H, RES 3, (regs.IY + {0:X})", disp));
                            regs.H = Res_R(3, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0x9D: //LD L, RES 3, (regs.IY+d)
                                        // Log(string.Format("LD L, RES 3, (regs.IY + {0:X})", disp));
                            regs.L = Res_R(3, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0x9E:  //RES 3, (regs.IY + d)
                                        // Log(string.Format("RES 3, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Res_R(3, disp));
                            break;

                            case 0x9F: //LD A, RES 3, (regs.IY+d)
                                        // Log(string.Format("LD A, RES 3, (regs.IY + {0:X})", disp));
                            regs.A = Res_R(3, disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0xA0: //LD B, RES 4, (regs.IY+d)
                                        // Log(string.Format("LD B, RES 4, (regs.IY + {0:X})", disp));
                            regs.B = Res_R(4, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0xA1: //LD C, RES 4, (regs.IY+d)
                                        // Log(string.Format("LD C, RES 4, (regs.IY + {0:X})", disp));
                            regs.C = Res_R(4, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0xA2: //LD D, RES 4, (regs.IY+d)
                                        // Log(string.Format("LD D, RES 4, (regs.IY + {0:X})", disp));
                            regs.D = Res_R(4, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0xA3: //LD E, RES 4, (regs.IY+d)
                                        // Log(string.Format("LD E, RES 4, (regs.IY + {0:X})", disp));
                            regs.E = Res_R(4, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0xA4: //LD H, RES 4, (regs.IY+d)
                                        // Log(string.Format("LD H, RES 4, (regs.IY + {0:X})", disp));
                            regs.H = Res_R(4, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0xA5: //LD L, RES 4, (regs.IY+d)
                                        // Log(string.Format("LD L, RES 4, (regs.IY + {0:X})", disp));
                            regs.L = Res_R(4, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0xA6:  //RES 4, (regs.IY + d)
                                        // Log(string.Format("RES 4, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Res_R(4, disp));
                            break;

                            case 0xA7: //LD A, RES 4, (regs.IY+d)
                                        // Log(string.Format("LD A, RES 4, (regs.IY + {0:X})", disp));
                            regs.A = Res_R(4, disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0xA8: //LD B, RES 5, (regs.IY+d)
                                        // Log(string.Format("LD B, RES 5, (regs.IY + {0:X})", disp));
                            regs.B = Res_R(5, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0xA9: //LD C, RES 5, (regs.IY+d)
                                        // Log(string.Format("LD C, RES 5, (regs.IY + {0:X})", disp));
                            regs.C = Res_R(5, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0xAA: //LD D, RES 5, (regs.IY+d)
                                        // Log(string.Format("LD D, RES 5, (regs.IY + {0:X})", disp));
                            regs.D = Res_R(5, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0xAB: //LD E, RES 5, (regs.IY+d)
                                        // Log(string.Format("LD E, RES 5, (regs.IY + {0:X})", disp));
                            regs.E = Res_R(5, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0xAC: //LD H, RES 5, (regs.IY+d)
                                        // Log(string.Format("LD H, RES 5, (regs.IY + {0:X})", disp));
                            regs.H = Res_R(5, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0xAD: //LD L, RES 5, (regs.IY+d)
                                        // Log(string.Format("LD L, RES 5, (regs.IY + {0:X})", disp));
                            regs.L = Res_R(5, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0xAE:  //RES 5, (regs.IY + d)
                                        // Log(string.Format("RES 5, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Res_R(5, disp));
                            break;

                            case 0xAF: //LD A, RES 5, (regs.IY+d)
                                        // Log(string.Format("LD A, RES 5, (regs.IY + {0:X})", disp));
                            regs.A = Res_R(5, disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0xB0: //LD B, RES 6, (regs.IY+d)
                                        // Log(string.Format("LD B, RES 6, (regs.IY + {0:X})", disp));
                            regs.B = Res_R(6, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0xB1: //LD C, RES 6, (regs.IY+d)
                                        // Log(string.Format("LD C, RES 6, (regs.IY + {0:X})", disp));
                            regs.C = Res_R(6, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0xB2: //LD D, RES 6, (regs.IY+d)
                                        // Log(string.Format("LD D, RES 6, (regs.IY + {0:X})", disp));
                            regs.D = Res_R(6, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0xB3: //LD E, RES 6, (regs.IY+d)
                                        // Log(string.Format("LD E, RES 6, (regs.IY + {0:X})", disp));
                            regs.E = Res_R(6, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0xB4: //LD H, RES 5, (regs.IY+d)
                                        // Log(string.Format("LD H, RES 6, (regs.IY + {0:X})", disp));
                            regs.H = Res_R(6, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0xB5: //LD L, RES 5, (regs.IY+d)
                                        // Log(string.Format("LD L, RES 6, (regs.IY + {0:X})", disp));
                            regs.L = Res_R(6, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0xB6:  //RES 6, (regs.IY + d)
                                        // Log(string.Format("RES 6, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Res_R(6, disp));
                            break;

                            case 0xB7: //LD A, RES 5, (regs.IY+d)
                                        // Log(string.Format("LD A, RES 6, (regs.IY + {0:X})", disp));
                            regs.A = Res_R(6, disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0xB8: //LD B, RES 7, (regs.IY+d)
                                        // Log(string.Format("LD B, RES 7, (regs.IY + {0:X})", disp));
                            regs.B = Res_R(7, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0xB9: //LD C, RES 7, (regs.IY+d)
                                        // Log(string.Format("LD C, RES 7, (regs.IY + {0:X})", disp));
                            regs.C = Res_R(7, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0xBA: //LD D, RES 7, (regs.IY+d)
                                        // Log(string.Format("LD D, RES 7, (regs.IY + {0:X})", disp));
                            regs.D = Res_R(7, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0xBB: //LD E, RES 7, (regs.IY+d)
                                        // Log(string.Format("LD E, RES 7, (regs.IY + {0:X})", disp));
                            regs.E = Res_R(7, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0xBC: //LD H, RES 7, (regs.IY+d)
                                        // Log(string.Format("LD H, RES 7, (regs.IY + {0:X})", disp));
                            regs.H = Res_R(7, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0xBD: //LD L, RES 7, (regs.IY+d)
                                        // Log(string.Format("LD L, RES 7, (regs.IY + {0:X})", disp));
                            regs.L = Res_R(7, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0xBE:  //RES 7, (regs.IY + d)
                                        // Log(string.Format("RES 7, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Res_R(7, disp));
                            break;

                            case 0xBF: //LD A, RES 7, (regs.IY+d)
                                        // Log(string.Format("LD A, RES 7, (regs.IY + {0:X})", disp));
                            regs.A = Res_R(7, disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0xC0: //LD B, SET 0, (regs.IY+d)
                                        // Log(string.Format("LD B, SET 0, (regs.IY + {0:X})", disp));
                            regs.B = Set_R(0, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0xC1: //LD C, SET 0, (regs.IY+d)
                                        // Log(string.Format("LD C, SET 0, (regs.IY + {0:X})", disp));
                            regs.C = Set_R(0, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0xC2: //LD D, SET 0, (regs.IY+d)
                                        // Log(string.Format("LD D, SET 0, (regs.IY + {0:X})", disp));
                            regs.D = Set_R(0, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0xC3: //LD E, SET 0, (regs.IY+d)
                                        // Log(string.Format("LD E, SET 0, (regs.IY + {0:X})", disp));
                            regs.E = Set_R(0, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0xC4: //LD H, SET 0, (regs.IY+d)
                                        // Log(string.Format("LD H, SET 0, (regs.IY + {0:X})", disp));
                            regs.H = Set_R(0, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0xC5: //LD L, SET 0, (regs.IY+d)
                                        // Log(string.Format("LD L, SET 0, (regs.IY + {0:X})", disp));
                            regs.L = Set_R(0, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0xC6:  //SET 0, (regs.IY + d)
                                        // Log(string.Format("SET 0, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Set_R(0, disp));
                            break;

                            case 0xC7: //LD A, SET 0, (regs.IY+d)
                                        // Log(string.Format("LD A, SET 0, (regs.IY + {0:X})", disp));
                            regs.A = Set_R(0, disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0xC8: //LD B, SET 1, (regs.IY+d)
                                        // Log(string.Format("LD B, SET 1, (regs.IY + {0:X})", disp));
                            regs.B = Set_R(1, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0xC9: //LD C, SET 0, (regs.IY+d)
                                        // Log(string.Format("LD C, SET 1, (regs.IY + {0:X})", disp));
                            regs.C = Set_R(1, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0xCA: //LD D, SET 1, (regs.IY+d)
                                        // Log(string.Format("LD D, SET 1, (regs.IY + {0:X})", disp));
                            regs.D = Set_R(1, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0xCB: //LD E, SET 1, (regs.IY+d)
                                        // Log(string.Format("LD E, SET 1, (regs.IY + {0:X})", disp));
                            regs.E = Set_R(1, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0xCC: //LD H, SET 1, (regs.IY+d)
                                        // Log(string.Format("LD H, SET 1, (regs.IY + {0:X})", disp));
                            regs.H = Set_R(1, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0xCD: //LD L, SET 1, (regs.IY+d)
                                        // Log(string.Format("LD L, SET 1, (regs.IY + {0:X})", disp));
                            regs.L = Set_R(1, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0xCE:  //SET 1, (regs.IY + d)
                                        // Log(string.Format("SET 1, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Set_R(1, disp));
                            break;

                            case 0xCF: //LD A, SET 1, (regs.IY+d)
                                        // Log(string.Format("LD A, SET 1, (regs.IY + {0:X})", disp));
                            regs.A = Set_R(1, disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0xD0: //LD B, SET 2, (regs.IY+d)
                                        // Log(string.Format("LD B, SET 2, (regs.IY + {0:X})", disp));
                            regs.B = Set_R(2, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0xD1: //LD C, SET 2, (regs.IY+d)
                                        // Log(string.Format("LD C, SET 2, (regs.IY + {0:X})", disp));
                            regs.C = Set_R(2, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0xD2: //LD D, SET 2, (regs.IY+d)
                                        // Log(string.Format("LD D, SET 2, (regs.IY + {0:X})", disp));
                            regs.D = Set_R(2, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0xD3: //LD E, SET 2, (regs.IY+d)
                                        // Log(string.Format("LD E, SET 2, (regs.IY + {0:X})", disp));
                            regs.E = Set_R(2, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0xD4: //LD H, SET 21, (regs.IY+d)
                                        // Log(string.Format("LD H, SET 2, (regs.IY + {0:X})", disp));
                            regs.H = Set_R(2, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0xD5: //LD L, SET 2, (regs.IY+d)
                                        // Log(string.Format("LD L, SET 2, (regs.IY + {0:X})", disp));
                            regs.L = Set_R(2, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0xD6:  //SET 2, (regs.IY + d)
                                        // Log(string.Format("SET 2, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Set_R(2, disp));
                            break;

                            case 0xD7: //LD A, SET 2, (regs.IY+d)
                                        // Log(string.Format("LD A, SET 2, (regs.IY + {0:X})", disp));
                            regs.A = Set_R(2, disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0xD8: //LD B, SET 3, (regs.IY+d)
                                        // Log(string.Format("LD B, SET 3, (regs.IY + {0:X})", disp));
                            regs.B = Set_R(3, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0xD9: //LD C, SET 3, (regs.IY+d)
                                        // Log(string.Format("LD C, SET 3, (regs.IY + {0:X})", disp));
                            regs.C = Set_R(3, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0xDA: //LD D, SET 3, (regs.IY+d)
                                        // Log(string.Format("LD D, SET 3, (regs.IY + {0:X})", disp));
                            regs.D = Set_R(3, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0xDB: //LD E, SET 3, (regs.IY+d)
                                        // Log(string.Format("LD E, SET 3, (regs.IY + {0:X})", disp));
                            regs.E = Set_R(3, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0xDC: //LD H, SET 21, (regs.IY+d)
                                        // Log(string.Format("LD H, SET 3, (regs.IY + {0:X})", disp));
                            regs.H = Set_R(3, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0xDD: //LD L, SET 3, (regs.IY+d)
                                        // Log(string.Format("LD L, SET 3, (regs.IY + {0:X})", disp));
                            regs.L = Set_R(3, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0xDE:  //SET 3, (regs.IY + d)
                                        // Log(string.Format("SET 3, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Set_R(3, disp));
                            break;

                            case 0xDF: //LD A, SET 3, (regs.IY+d)
                                        // Log(string.Format("LD A, SET 3, (regs.IY + {0:X})", disp));
                            regs.A = Set_R(3, disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0xE0: //LD B, SET 4, (regs.IY+d)
                                        // Log(string.Format("LD B, SET 4, (regs.IY + {0:X})", disp));
                            regs.B = Set_R(4, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0xE1: //LD C, SET 4, (regs.IY+d)
                                        // Log(string.Format("LD C, SET 4, (regs.IY + {0:X})", disp));
                            regs.C = Set_R(4, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0xE2: //LD D, SET 4, (regs.IY+d)
                                        // Log(string.Format("LD D, SET 4, (regs.IY + {0:X})", disp));
                            regs.D = Set_R(4, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0xE3: //LD E, SET 4, (regs.IY+d)
                                        // Log(string.Format("LD E, SET 4, (regs.IY + {0:X})", disp));
                            regs.E = Set_R(4, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0xE4: //LD H, SET 4, (regs.IY+d)
                                        // Log(string.Format("LD H, SET 4, (regs.IY + {0:X})", disp));
                            regs.H = Set_R(4, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0xE5: //LD L, SET 3, (regs.IY+d)
                                        // Log(string.Format("LD L, SET 4, (regs.IY + {0:X})", disp));
                            regs.L = Set_R(4, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0xE6:  //SET 4, (regs.IY + d)
                                        // Log(string.Format("SET 4, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Set_R(4, disp));
                            break;

                            case 0xE7: //LD A, SET 4, (regs.IY+d)
                                        // Log(string.Format("LD A, SET 4, (regs.IY + {0:X})", disp));
                            regs.A = Set_R(4, disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0xE8: //LD B, SET 5, (regs.IY+d)
                                        // Log(string.Format("LD B, SET 5, (regs.IY + {0:X})", disp));
                            regs.B = Set_R(5, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0xE9: //LD C, SET 5, (regs.IY+d)
                                        // Log(string.Format("LD C, SET 5, (regs.IY + {0:X})", disp));
                            regs.C = Set_R(5, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0xEA: //LD D, SET 5, (regs.IY+d)
                                        // Log(string.Format("LD D, SET 5, (regs.IY + {0:X})", disp));
                            regs.D = Set_R(5, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0xEB: //LD E, SET 5, (regs.IY+d)
                                        // Log(string.Format("LD E, SET 5, (regs.IY + {0:X})", disp));
                            regs.E = Set_R(5, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0xEC: //LD H, SET 5, (regs.IY+d)
                                        // Log(string.Format("LD H, SET 5, (regs.IY + {0:X})", disp));
                            regs.H = Set_R(5, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0xED: //LD L, SET 5, (regs.IY+d)
                                        // Log(string.Format("LD L, SET 5, (regs.IY + {0:X})", disp));
                            regs.L = Set_R(5, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0xEE:  //SET 5, (regs.IY + d)
                                        // Log(string.Format("SET 5, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Set_R(5, disp));
                            break;

                            case 0xEF: //LD A, SET 5, (regs.IY+d)
                                        // Log(string.Format("LD A, SET 5, (regs.IY + {0:X})", disp));
                            regs.A = Set_R(5, disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0xF0: //LD B, SET 6, (regs.IY+d)
                                        // Log(string.Format("LD B, SET 6, (regs.IY + {0:X})", disp));
                            regs.B = Set_R(6, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0xF1: //LD C, SET 6, (regs.IY+d)
                                        // Log(string.Format("LD C, SET 6, (regs.IY + {0:X})", disp));
                            regs.C = Set_R(6, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0xF2: //LD D, SET 6, (regs.IY+d)
                                        // Log(string.Format("LD D, SET 6, (regs.IY + {0:X})", disp));
                            regs.D = Set_R(6, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0xF3: //LD E, SET 6, (regs.IY+d)
                                        // Log(string.Format("LD E, SET 6, (regs.IY + {0:X})", disp));
                            regs.E = Set_R(6, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0xF4: //LD H, SET 6, (regs.IY+d)
                                        // Log(string.Format("LD H, SET 6, (regs.IY + {0:X})", disp));
                            regs.H = Set_R(6, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0xF5: //LD L, SET 6, (regs.IY+d)
                                        // Log(string.Format("LD L, SET 6, (regs.IY + {0:X})", disp));
                            regs.L = Set_R(6, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0xF6:  //SET 6, (regs.IY + d)
                                        // Log(string.Format("SET 6, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Set_R(6, disp));
                            break;

                            case 0xF7: //LD A, SET 6, (regs.IY+d)
                                        // Log(string.Format("LD A, SET 6, (regs.IY + {0:X})", disp));
                            regs.A = Set_R(6, disp);
                            PokeByte(offset, regs.A);
                            break;

                            case 0xF8: //LD B, SET 7, (regs.IY+d)
                                        // Log(string.Format("LD B, SET 7, (regs.IY + {0:X})", disp));
                            regs.B = Set_R(7, disp);
                            PokeByte(offset, regs.B);
                            break;

                            case 0xF9: //LD C, SET 7, (regs.IY+d)
                                        // Log(string.Format("LD C, SET 7, (regs.IY + {0:X})", disp));
                            regs.C = Set_R(7, disp);
                            PokeByte(offset, regs.C);
                            break;

                            case 0xFA: //LD D, SET 7, (regs.IY+d)
                                        // Log(string.Format("LD D, SET 7, (regs.IY + {0:X})", disp));
                            regs.D = Set_R(7, disp);
                            PokeByte(offset, regs.D);
                            break;

                            case 0xFB: //LD E, SET 7, (regs.IY+d)
                                        // Log(string.Format("LD E, SET 7, (regs.IY + {0:X})", disp));
                            regs.E = Set_R(7, disp);
                            PokeByte(offset, regs.E);
                            break;

                            case 0xFC: //LD H, SET 7, (regs.IY+d)
                                        // Log(string.Format("LD H, SET 7, (regs.IY + {0:X})", disp));
                            regs.H = Set_R(7, disp);
                            PokeByte(offset, regs.H);
                            break;

                            case 0xFD: //LD L, SET 7, (regs.IY+d)
                                        // Log(string.Format("LD L, SET 7, (regs.IY + {0:X})", disp));
                            regs.L = Set_R(7, disp);
                            PokeByte(offset, regs.L);
                            break;

                            case 0xFE:  //SET 7, (regs.IY + d)
                                        // Log(string.Format("SET 7, (regs.IY + {0:X})", disp));
                            PokeByte(offset, Set_R(7, disp));
                            break;

                            case 0xFF: //LD A, SET 7, (regs.IY + D)
                            regs.A = Set_R(7, disp);
                            PokeByte(offset, regs.A);
                            break;

                            default:
                            System.String msg = "ERROR: Could not handle FDCB " + opcode.ToString();
                            System.Windows.Forms.MessageBox.Show(msg, "Opcode handler",
                                        System.Windows.Forms.MessageBoxButtons.OKCancel, System.Windows.Forms.MessageBoxIcon.Error);
                            break;
                        }
                        break;
                    }
                    #endregion

                    #region Pop/Push instructions
                    case 0xE1:  //POP regs.IY
                                // Log("POP regs.IY");
                    regs.IY = PopStack();
                    break;

                    case 0xE5:  //PUSH regs.IY
                                // Log("PUSH regs.IY");
                    Contend(regs.IR, 1, 1);
                    PushStack(regs.IY);
                    break;
                    #endregion

                    #region Exchange instruction
                    case 0xE3:  //EX (regs.SP), regs.IY
                    {
                        // Log("EX (regs.SP), regs.IY");
                        addr = PeekWord(regs.SP);
                        Contend(regs.SP + 1, 1, 1);
                        PokeByte((ushort)(regs.SP + 1), (byte)(regs.IY >> 8));
                        PokeByte(regs.SP, (byte)(regs.IY & 0xff));
                        Contend(regs.SP, 1, 2);
                        regs.IY = addr;
                        regs.MemPtr = regs.IY;
                        break;
                    }
                    #endregion

                    #region Jump instruction
                    case 0xE9:  //JP (regs.IY)
                                // Log("JP (regs.IY)");
                    regs.PC = regs.IY;
                    break;
                    #endregion

                    default:
                    //According to Sean's doc: http://z80.info/z80sean.txt
                    //If a DDxx or FDxx instruction is not listed, it should operate as
                    //without the DD or FD prefix, and the DD or FD prefix itself should
                    //operate as a NOP.
                    Execute(opcode);      //Try and execute it as a normal instruction then
                    break;
                }
                break;
                #endregion
            }
        }
    }
}