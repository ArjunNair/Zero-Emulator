//Z80Core.cs
//(c) Arjun Nair 2009-2011

namespace Speccy
{
    public class Z80Core
    {
        // private zxmachine machine;

        /* public zxmachine Machine
         {
             get
             {
                 return machine;
             }
             set
             {
                 machine = value;
             }
         }
         */

        public bool loggingEnabled = false;

        public bool runningInterrupt = false;   //true if interrupts are active

        public bool and_32_Or_64 = false;         //used for edge loading

        public bool resetOver = false;
        public bool HaltOn = false;            //true if HALT instruction is being processed
        protected int frameCount;
        public byte lastOpcodeWasEI = 0;   //used for re-triggered interrupts

        protected int disp = 0; //used later on to calculate relative jumps in Execute()
        protected int deltaTStates = 0;
        public int tstates = 0;                 //opcode t-states
        public int totalTStates = 0;
        public int oldTStates = 0;
        public int interruptMode;               //0 = IM0, 1 = IM1, 2 = IM2
        protected int timeToOutSound = 0;
        //private int contendTStates = 0;         //number of t-states in contention period

        //All registers
        protected int a = 0, f = 0, bc = 0, hl = 0, de = 0, sp = 0, pc = 0, ix = 0, iy = 0;

        protected int i = 0, r = 0;

        //All alternate registers
        protected int _af = 0, _bc = 0, _de = 0, _hl = 0;

        protected int _r = 0; //not really a real z80 alternate reg,
        //but used here to store the value for R temporarily

        //MEMPTR register - internal cpu register
        //Bits 3 and 5 of Flag for Bit n, (HL) instruction, are copied from bits 11 & 13 of MemPtr.

        protected int memPtr = 0;

        public int MemPtr {
            get {
                return memPtr;
            }
            set {
                memPtr = value & 0xffff;
            }
        }

        protected const int MEMPTR_11 = 0x800;
        protected const int MEMPTR_13 = 0x2000;

        protected const int F_CARRY = 0x01;
        protected const int F_NEG = 0x02;
        protected const int F_PARITY = 0x04;
        protected const int F_3 = 0x08;
        protected const int F_HALF = 0x010;
        protected const int F_5 = 0x020;
        protected const int F_ZERO = 0x040;
        protected const int F_SIGN = 0x080;
        public bool IFF1, IFF2;

        //Misc variables used in the switch-case statement
        protected int opcode = 0;

        protected int val, addr;

        protected byte[] parity = new byte[256];
        protected byte[] IOIncParityTable = new byte[16] { 0, 0, 1, 0, 0, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 0 };
        protected byte[] IODecParityTable = new byte[16] { 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 1 };
        protected byte[] halfcarry_add = new byte[] { 0, F_HALF, F_HALF, F_HALF, 0, 0, 0, F_HALF };
        protected byte[] halfcarry_sub = new byte[] { 0, 0, F_HALF, 0, F_HALF, 0, F_HALF, F_HALF };
        protected byte[] overflow_add = new byte[] { 0, 0, 0, F_PARITY, F_PARITY, 0, 0, 0 };
        protected byte[] overflow_sub = new byte[] { 0, F_PARITY, 0, 0, 0, 0, F_PARITY, 0 };
        protected byte[] sz53 = new byte[256];
        protected byte[] sz53p = new byte[256];

        #region 8 bit register access

        public int A {
            get {
                return a & 0xff;
            }

            set {
                a = value;
            }
        }

        public int B {
            get {
                return (bc >> 8) & 0xff; //Since b and c are stored left to right in memory here
            }

            set {
                bc = (bc & 0x00ff) | (value << 8); // mask out b, then or in the new value
            }
        }

        public int C {
            get {
                return (bc & 0xff);
            }

            set {
                bc = (bc & 0xff00) | value;
            }
        }

        public int H {
            get {
                return (hl >> 8) & 0xff;
            }

            set {
                hl = (hl & 0x00ff) | (value << 8);
            }
        }

        public int L {
            get {
                return (hl & 0xff);
            }

            set {
                hl = (hl & 0xff00) | value;
            }
        }

        public int D {
            get {
                return (de >> 8) & 0xff;
            }

            set {
                de = (de & 0x00ff) | (value << 8);
            }
        }

        public int E {
            get {
                return (de & 0xff);
            }

            set {
                de = (de & 0xff00) | value;
            }
        }

        public int F {
            get {
                return f & 0xff;
            }

            set {
                f = value;
            }
        }

        public int I {
            get {
                return i & 0xff;
            }
            set {
                i = value;
            }
        }

        public int R {
            get {
                return (_r | (r & 0x7f));
            }
            set {
                r = value & 0x7f; //only the lower 7 bits are affected
            }
        }

        public int _R {
            set {
                _r = value & 0x80;  //store Bit 7
                R = value;
            }
        }

        public int IXH {
            get {
                return (ix >> 8) & 0xff;
            }
            set {
                ix = (ix & 0x00ff) | (value << 8);
            }
        }

        public int IXL {
            get {
                return (ix & 0xff);
            }
            set {
                ix = (ix & 0xff00) | value;
            }
        }

        public int IYH {
            get {
                return (iy >> 8) & 0xff;
            }
            set {
                iy = (iy & 0x00ff) | (value << 8);
            }
        }

        public int IYL {
            get {
                return (iy & 0xff);
            }
            set {
                iy = (iy & 0xff00) | value;
            }
        }

        #endregion 8 bit register access

        #region 16 bit register access

        public int IR {
            get {
                return ((I << 8) | (R));
            }
        }

        public int AF {
            get {
                return ((a << 8) | f);
            }

            set {
                a = (value & 0xff00) >> 8;
                f = value & 0x00ff;
            }
        }

        public int _AF {
            get {
                return _af;
            }

            set {
                _af = value;
            }
        }

        public int _HL {
            get {
                return _hl;
            }

            set {
                _hl = value;
            }
        }

        public int _BC {
            get {
                return _bc;
            }

            set {
                _bc = value;
            }
        }

        public int _DE {
            get {
                return _de;
            }

            set {
                _de = value;
            }
        }

        public int BC {
            get {
                return bc & 0xffff;
            }

            set {
                bc = value & 0xffff;
            }
        }

        public int DE {
            get {
                return de & 0xffff;
            }

            set {
                de = value & 0xffff;
            }
        }

        public int HL {
            get {
                return hl & 0xffff;
            }

            set {
                hl = value & 0xffff;
            }
        }

        public int IX {
            get {
                return ix & 0xffff;
            }

            set {
                ix = value & 0xffff;
            }
        }

        public int IY {
            get {
                return iy & 0xffff;
            }

            set {
                iy = value & 0xffff;
            }
        }

        public int SP {
            get {
                return sp & 0xffff;
            }

            set {
                sp = value & 0xffff;
            }
        }

        public int PC {
            get {
                return pc & 0xffff;
            }

            set {
                pc = value & 0xffff;
            }
        }

        #endregion 16 bit register access

        #region Flag manipulation

        public void SetCarry(bool val) {
            if (val) {
                f |= F_CARRY;
            } else {
                f &= ~(F_CARRY);
            }
        }

        public void SetNeg(bool val) {
            if (val) {
                f |=  F_NEG;
            } else {
                f &=  ~(F_NEG);
            }
        }

        public void SetParity(byte val) {
            if (val > 0) {
                f |= F_PARITY;
            } else {
                f &= ~(F_PARITY);
            }
        }

        public void SetParity(bool val) {
            if (val) {
                f |= F_PARITY;
            } else {
                f &= ~(F_PARITY);
            }
        }

        public void SetHalf(bool val) {
            if (val) {
                f |= F_HALF;
            } else {
                f &= ~(F_HALF);
            }
        }

        public void SetZero(bool val) {
            if (val) {
                f |= F_ZERO;
            } else {
                f &= ~(F_ZERO);
            }
        }

        public void SetSign(bool val) {
            if (val) {
                f |= F_SIGN;
            } else {
                f &= ~(F_SIGN);
            }
        }

        public void SetF3(bool val) {
            if (val) {
                f |= F_3;
            } else {
                f &= ~(F_3);
            }
        }

        public void SetF5(bool val) {
            if (val) {
                f |= F_5;
            } else {
                f &= ~(F_5);
            }
        }

        #endregion Flag manipulation

        public Z80Core() {
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
                sz53[i] = (byte)(i & (F_3 | F_5 | F_SIGN));
                j = i; p = 0;
                for (k = 0; k < 8; k++) { p ^= (byte)(j & 1); j >>= 1; }
                parity[i] = (byte)(p > 0 ? 0 : F_PARITY);
                sz53p[i] = (byte)(sz53[i] | parity[i]);
            }

            sz53[0] |= (byte)F_ZERO;
            sz53p[0] |= (byte)F_ZERO;
        }

        /*
                public virtual int PeekByte(int addr)
                {
                    //return  PeekByte(addr);
                    return 0;
                }

                public virtual int PeekByteNoContend(int addr)
                {
                    //return  PeekByteNoContend(addr);
                    return 0;
                }

                public virtual void PokeByte(int addr, int b)
                {
                    // PokeByte(addr, b);
                }

                public virtual int PeekWord(int addr)
                {
                    //int w =  PeekByte(addr) + ( PeekByte(addr + 1) << 8);
                    //return w;
                    return 0;
                }

                public virtual void PokeWord(int addr, int w)
                {
                    // PokeByte(addr, w);
                    // PokeByte((addr + 1), w >> 8);
                }

                //In A, (n)
                public virtual int In(int port)
                {
                   // return  In(port);
                    return 0;
                }

                //Out
                public virtual void Out(int port, int val)
                {
                   //  Out(port, val);
                }
        */

        public void exx() {
            int temp;
            temp = _hl;
            _hl = HL;
            HL = temp;

            temp = _de;
            _de = DE;
            DE = temp;

            temp = _bc;
            _bc = BC;
            BC = temp;
        }

        public void ex_af_af() {
            int temp = _af;
            _af = AF;
            AF = temp;
        }

        //Reads next instruction from address pointed to by PC
        /*   public int FetchInstruction()
           {
               R++;
               int b = PeekByte(PC);
               PC = (PC + 1) & 0xffff;
               totalTStates++; //effectively, totalTStates + 4 because PeekByte does the other 3
               return b;
           }
   */

        private void LogRegisters() {
            //logWriter.Write("#{0,-5:X} #{1,-5:X} #{2,-5:X} #{3,-5:X} #{4,-5:X} #{5,-5}", SP, HL, BC, DE, A, PC);
        }

        protected void Log(System.String op) {
            // logWriter.Write("{0, -17} ", op);
        }

        private void LogTStates() {
            //logWriter.Write("{0,-18}", totalTStates);
            //logWriter.Write((F & F_SIGN) != 0 ? 1 : 0);
            //logWriter.Write((F & F_ZERO) != 0 ? 1 : 0);
            //logWriter.Write((F & F_5) != 0 ? 1 : 0);
            //logWriter.Write((F & F_HALF) != 0 ? 1 : 0);
            //logWriter.Write((F & F_3) != 0 ? 1 : 0);
            //logWriter.Write((F & F_PARITY) != 0 ? 1 : 0);
            //logWriter.Write((F & F_NEG) != 0 ? 1 : 0);
            //logWriter.Write((F & F_CARRY) != 0 ? 1 : 0);
            //logWriter.WriteLine();
        }

        public int Inc(int reg) {
            /*
            SetParity((reg == 0x7f));   //reg = 127? We're gonna overflow on inc!
            SetNeg(false);               //Negative is always reset (0)
            SetHalf((((reg & 0x0f) + 1) & F_HALF) != 0);

            reg = (reg + 1) & 0xff;
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
            SetZero(reg == 0);
            SetSign((reg & F_SIGN) != 0);
            return reg;*/

            reg = (reg + 1);
            F = ( F & F_CARRY ) | ( (reg == 0x80) ? F_PARITY : 0 ) | ((reg & 0x0f) > 0 ? 0 : F_HALF );
            reg &= 0xff;
            F |= sz53[reg];
            return reg;
        }

        public int Dec(int reg) {
            /*
            SetNeg(true);                //Negative is always set (1)
            SetParity((reg == 0x80));   //reg = -128? We're gonna overflow on dec!
            SetHalf((((reg & 0x0f) - 1) & F_HALF) != 0);

            reg = (reg - 1) & 0xff;
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
            SetZero(reg == 0);
            SetSign((reg & F_SIGN) != 0);

            return reg;*/
             F = ( F & F_CARRY ) | ( (reg & 0x0f) > 0 ? 0 : F_HALF ) | F_NEG;
             reg = (reg - 1);
             F |= ((reg) == 0x7f ? F_PARITY : 0);
             reg &= 0xff;
             F |= sz53[reg];
             return reg;
        }

        //16 bit addition (no carry)
        public int Add_RR(int rr1, int rr2) {
            /*
            SetNeg(false);
            //SetHalf(((((rr1 >> 8) & 0x0f) + ((rr2 >> 8) & 0x0f)) & F_HALF) != 0); //Set from high byte of operands
            SetHalf((((rr1 & 0xfff) + (rr2 & 0xfff)) & 0x1000) != 0); //Set from high byte of operands
            rr1 += rr2;

            SetCarry((rr1 & 0x10000) != 0);
            SetF3(((rr1 >> 8) & F_3) != 0);
            SetF5(((rr1 >> 8) & F_5) != 0);
            return (rr1 & 0xffff);*/
             int add16temp = (rr1) + (rr2);
              byte lookup = (byte)((((rr1) & 0x0800 ) >> 11 ) | ( (  (rr2) & 0x0800 ) >> 10 ) | ( ( add16temp & 0x0800 ) >>  9));
              rr1 = add16temp;
              F = ( F & ( F_PARITY | F_ZERO | F_SIGN ) ) | ((add16temp & 0x10000) > 0 ? F_CARRY : 0 )|( ( add16temp >> 8 ) & ( F_3 | F_5 ) ) | halfcarry_add[lookup];
              return rr1 & 0xffff; ;
        }

        //8 bit add to accumulator (no carry)
        public void Add_R(int reg) {
            /*
            SetNeg(false);
            SetHalf((((A & 0x0f) + (reg & 0x0f)) & F_HALF) != 0);

            int ans = (A + reg) & 0xff;
            SetCarry(((A + reg) & 0x100) != 0);
            SetParity(((A ^ ~reg) & (A ^ ans) & 0x80) != 0);
            SetSign((ans & F_SIGN) != 0);
            SetZero(ans == 0);
            SetHalf((((A & 0x0f) + (reg & 0x0f)) & F_HALF) != 0);
            SetF3((ans & F_3) != 0);
            SetF5((ans & F_5) != 0);
            A = ans;
             * */
             int addtemp = A + (reg);
             byte lookup = (byte)(((A & 0x88 ) >> 3 ) | (((reg) & 0x88 ) >> 2 ) | ( ( addtemp & 0x88 ) >> 1 ));
             A=addtemp & 0xff;
             F = ((addtemp & 0x100) > 0 ? F_CARRY : 0 ) | halfcarry_add[lookup & 0x07] | overflow_add[lookup >> 4] | sz53[A];
        }

        //Add with carry into accumulator
        public void Adc_R(int reg) {
            /*
            SetNeg(false);
            int fc = ((F & F_CARRY) != 0 ? 1 : 0);
            SetHalf((((A & 0x0f) + (reg & 0x0f) + fc) & F_HALF) != 0);
            int ans = (A + reg + fc) & 0xff;

            SetCarry(((A + reg + fc) & 0x100) != 0);

            SetParity(((A ^ ~reg) & (A ^ ans) & 0x80) != 0);
            SetSign((ans & F_SIGN) != 0);
            SetZero(ans == 0);
            SetF3((ans & F_3) != 0);
            SetF5((ans & F_5) != 0);
            A = ans;*/
            int adctemp = A + (reg) + ( F & F_CARRY ); 
            byte lookup = (byte)(((A & 0x88) >> 3) | (((reg) & 0x88)>>2) | ((adctemp & 0x88)>> 1)); 
            A=adctemp & 0xff;
            F = ((adctemp & 0x100) > 0 ? F_CARRY : 0 ) | halfcarry_add[lookup & 0x07] | overflow_add[lookup >> 4] | sz53[A];
        }

        //Add with carry into HL
        public void Adc_RR(int reg) {
            /*
            SetNeg(false);
            int fc = ((F & F_CARRY) != 0 ? 1 : 0);
            int ans = (HL + reg + fc) & 0xffff;
            SetCarry(((HL + reg + fc) & 0x10000) != 0);
            SetHalf((((HL & 0xfff) + (reg & 0xfff) + fc) & 0x1000) != 0); //Set from high byte of operands
            //SetHalf(((((HL >> 8) & 0x0f + (reg >> 8) & 0x0f) + fc) & F_HALF) != 0); //Set from high byte of operands
            SetParity(((HL ^ ~reg) & (HL ^ ans) & 0x8000) != 0);
            SetSign((ans & (F_SIGN << 8)) != 0);
            SetZero(ans == 0);
            SetF3(((ans >> 8) & F_3) != 0);
            SetF5(((ans >> 8) & F_5) != 0);
            HL = ans;*/
            int add16temp = HL + (reg) + ( F & F_CARRY );
              byte lookup = (byte)(((HL & 0x8800 ) >> 11 ) | (((reg) & 0x8800 ) >> 10 ) | ( ( add16temp & 0x8800 ) >>  9 ));
              HL = add16temp & 0xffff;
              F = ( (add16temp & 0x10000) > 0? F_CARRY : 0 ) | overflow_add[lookup >> 4] | ( H & ( F_3 | F_5 | F_SIGN ) ) |                         halfcarry_add[lookup&0x07] | ( HL > 0? 0 : F_ZERO );
        }

        //8 bit subtract to accumulator (no carry)
        public void Sub_R(int reg) {
            /*
            SetNeg(true);

            int ans = (A - reg) & 0xff;
            SetCarry(((A - reg) & 0x100) != 0);
            SetF3((ans & F_3) != 0);
            SetF5((ans & F_5) != 0);
            SetParity(((A ^ reg) & (A ^ ans) & 0x80) != 0);
            SetSign((ans & F_SIGN) != 0);
            SetHalf((((A & 0x0f) - (reg & 0x0f)) & F_HALF) != 0);
            SetZero(ans == 0);
            SetNeg(true);

            A = ans;*/
            int subtemp = A - (reg);
              byte lookup = (byte)(((A & 0x88 ) >> 3 ) | ( (reg & 0x88 ) >> 2 ) | ((subtemp & 0x88 ) >> 1 )); 
              A=subtemp & 0xff;
              F = ((subtemp & 0x100) > 0 ? F_CARRY : 0 ) | F_NEG | halfcarry_sub[lookup & 0x07] | overflow_sub[lookup >> 4] | sz53[A];
        }

        //8 bit subtract from accumulator with carry (SBC A, r)
        public void Sbc_R(int reg) {
           /* SetNeg(true);
            int fc = ((F & F_CARRY) != 0 ? 1 : 0);

            int ans = (A - reg - fc) & 0xff;
            SetCarry(((A - reg - fc) & 0x100) != 0);
            SetParity(((A ^ reg) & (A ^ ans) & 0x80) != 0);
            SetSign((ans & F_SIGN) != 0);
            SetHalf((((A & 0x0f) - (reg & 0x0f) - fc) & F_HALF) != 0);
            SetZero(ans == 0);
            SetF3((ans & F_3) != 0);
            SetF5((ans & F_5) != 0);
            A = ans;*/
             int sbctemp = A - (reg) - ( F & F_CARRY );
              byte lookup = (byte)(((A & 0x88 ) >> 3 ) |( ( (reg) & 0x88 ) >> 2 ) |( ( sbctemp & 0x88 ) >> 1 ));
              A=sbctemp & 0xff;
              F = ((sbctemp & 0x100)>0 ? F_CARRY : 0 ) | F_NEG |halfcarry_sub[lookup & 0x07] | overflow_sub[lookup >> 4] | sz53[A];
        }

        //16 bit subtract from HL with carry
        public void Sbc_RR(int reg) {
            /*
            SetNeg(true);
            int fc = ((F & F_CARRY) != 0 ? 1 : 0);

            SetHalf((((HL & 0xfff) - (reg & 0xfff) - fc) & 0x1000) != 0); //Set from high byte of operands
            // SetHalf((((((HL >> 8) & 0x0f) - ((reg >> 8) & 0x0f)) - fc) & F_HALF) != 0); //Set from high byte of operands

            int ans = (HL - reg - fc) & 0xffff;
            SetCarry(((HL - reg - fc) & 0x10000) != 0);
            SetParity(((HL ^ reg) & (HL ^ ans) & 0x8000) != 0);
            SetSign((ans & (F_SIGN << 8)) != 0);
            SetZero(ans == 0);
            SetF3(((ans >> 8) & F_3) != 0);
            SetF5(((ans >> 8) & F_5) != 0);

            HL = ans;*/
            int sub16temp = HL - (reg) - (F & F_CARRY);
              byte lookup = (byte)(((HL & 0x8800 ) >> 11 ) | ( ((reg) & 0x8800 ) >> 10 ) | (( sub16temp & 0x8800 ) >>  9 ));
              HL = sub16temp & 0xffff;
              F = ((sub16temp & 0x10000) > 0 ? F_CARRY : 0 ) | F_NEG | overflow_sub[lookup >> 4] | (H & ( F_3 | F_5 | F_SIGN ) ) |halfcarry_sub[lookup&0x07] |( HL > 0 ? 0 : F_ZERO);
        }

        //Comparison with accumulator
        public void Cp_R(int reg) {
            /*
            SetNeg(true);

            int result = A - reg;
            int ans = result & 0xff;
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
            SetHalf((((A & 0x0f) - (reg & 0x0f)) & F_HALF) != 0);
            SetParity(((A ^ reg) & (A ^ ans) & 0x80) != 0);
            SetSign((ans & F_SIGN) != 0);
            SetZero(ans == 0);
            SetCarry((result & 0x100) != 0);*/
            int cptemp = A - reg;
              byte lookup = (byte)(((A & 0x88 ) >> 3 ) | ( ( (reg) & 0x88 ) >> 2 ) | ( (cptemp & 0x88 ) >> 1 ));
               F = ( (cptemp & 0x100) > 0 ? F_CARRY : ( cptemp > 0 ? 0 : F_ZERO ) ) | F_NEG | halfcarry_sub[lookup & 0x07] | overflow_sub[lookup >> 4] |( reg & ( F_3 | F_5 ) ) | ( cptemp & F_SIGN );
        }

        //AND with accumulator
        public void And_R(int reg) {
            /*
            SetCarry(false);
            SetNeg(false);

            int ans = A & reg;
            SetSign((ans & F_SIGN) != 0);
            SetHalf(true);
            SetZero(ans == 0);
            //SetParity(GetParity(ans));
            SetParity(parity[ans]);
            SetF3((ans & F_3) != 0);
            SetF5((ans & F_5) != 0);
            A = ans;*/
            A &= reg;
            F = F_HALF | sz53p[A];
            if (((reg & (~96)) == 0) &&  (reg != 96))
                and_32_Or_64 = true;
        }

        //XOR with accumulator
        public void Xor_R(int reg) {
            /*
            SetCarry(false);
            SetNeg(false);

            int ans = (A ^ reg) & 0xff;
            SetSign((ans & F_SIGN) != 0);
            SetHalf(false);
            SetZero(ans == 0);
            // SetParity(GetParity(ans));
            SetParity(parity[ans]);
            SetF3((ans & F_3) != 0);
            SetF5((ans & F_5) != 0);
            A = ans;*/
            A = ( A ^ reg) & 0xff;
            F = sz53p[A];
        }

        //OR with accumulator
        public void Or_R(int reg) {
            /*
            SetCarry(false);
            SetNeg(false);

            int ans = A | reg;
            SetSign((ans & F_SIGN) != 0);
            SetHalf(false);
            SetZero(ans == 0);
            //SetParity(GetParity(ans));
            SetParity(parity[ans]);
            SetF3((ans & F_3) != 0);
            SetF5((ans & F_5) != 0);
            A = ans;*/
             A |= reg;
             F = sz53p[A];
        }

        //Rotate left with carry register (RLC r)
        public int Rlc_R(int reg) {
            /*
            int msb = reg & F_SIGN;

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
            SetSign((reg & F_SIGN) != 0);
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
            return reg;*/
            reg = ((reg << 1 ) | (reg >>7 )) & 0xff;
            F = (reg & F_CARRY) | sz53p[reg];
            return reg;
        }

        //Rotate right with carry register (RLC r)
        public int Rrc_R(int reg) {
            /*
            int lsb = reg & F_CARRY; //save the lsb bit

            if (lsb != 0) {
                reg = (reg >> 1) | 0x80;
            } else
                reg = reg >> 1;

            SetCarry(lsb != 0);
            SetHalf(false);
            SetNeg(false);
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
            //SetParity(GetParity(reg));
            SetParity(parity[reg]);
            SetZero(reg == 0);
            SetSign((reg & F_SIGN) != 0);

            return reg;*/
            F = reg & F_CARRY;
            reg = ((reg >>1 ) | (reg << 7)) & 0xff;
            F |= sz53p[reg];
            return reg;
        }

        //Rotate left register (RL r)
        public int Rl_R(int reg) {
            /*
            bool rc = (reg & F_SIGN) != 0;
            int msb = F & F_CARRY; //save the msb bit

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
            SetSign((reg & F_SIGN) != 0);
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
            return reg;*/
            byte rltemp = (byte)(reg & 0xff);
            reg = ((reg << 1) | (F & F_CARRY)) & 0xff;
            F = ( rltemp >> 7 ) | sz53p[reg];
            return reg;
        }

        //Rotate right register (RL r)
        public int Rr_R(int reg) {
            /*
            bool rc = (reg & F_CARRY) != 0;
            int lsb = F & F_CARRY; //save the lsb bit

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
            SetSign((reg & F_SIGN) != 0);
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
            return reg;*/
            byte rrtemp = (byte)(reg & 0xff);
            reg = ((reg >> 1 ) | ( F << 7 )) & 0xff;
            F = ( rrtemp & F_CARRY ) | sz53p[reg];
            return reg;
        }

        //Shift left arithmetic register (SLA r)
        public int Sla_R(int reg) {
            /*
            int msb = reg & F_SIGN; //save the msb bit

            reg = (reg << 1) & 0xff;

            SetCarry(msb != 0);
            SetHalf(false);
            SetNeg(false);
            //SetParity(GetParity(reg));
            SetParity(parity[reg]);
            SetZero(reg == 0);
            SetSign((reg & F_SIGN) != 0);
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
            return reg;*/
            F = reg >> 7;
            reg = (reg <<  1) & 0xff;
            F |= sz53p[reg];
            return reg;
        }

        //Shift right arithmetic register (SRA r)
        public int Sra_R(int reg) {
            /*
            int lsb = reg & F_CARRY; //save the lsb bit
            reg = (reg >> 1) | (reg & F_SIGN);

            SetCarry(lsb != 0);
            SetHalf(false);
            SetNeg(false);
            // SetParity(GetParity(reg));
            SetParity(parity[reg]);
            SetZero(reg == 0);
            SetSign((reg & 0x80) != 0);
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
            return reg;*/
            F = reg & F_CARRY;
            reg =( (reg & 0x80 ) | (reg >> 1 )) & 0xff;
            F |= sz53p[reg];
            return reg;
        }

        //Shift left logical register (SLL r)
        public int Sll_R(int reg) {
            /*
            int msb = reg & F_SIGN; //save the msb bit
            reg = reg << 1;
            reg = (reg | 0x01) & 0xff;

            SetCarry(msb != 0);
            SetHalf(false);
            SetNeg(false);
            // SetParity(GetParity(reg));
            SetParity(parity[reg]);
            SetZero(reg == 0);
            SetSign((reg & F_SIGN) != 0);
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
            return reg;*/
            F = reg >> 7;
            reg = (( reg << 1 ) | 0x01) & 0xff;
            F |= sz53p[reg];
            return reg;
        }

        //Shift right logical register (SRL r)
        public int Srl_R(int reg) {
            /*
            int lsb = reg & F_CARRY; //save the lsb bit
            reg = reg >> 1;

            SetCarry(lsb != 0);
            SetHalf(false);
            SetNeg(false);
            //SetParity(GetParity(reg));
            SetParity(parity[reg]);
            SetZero(reg == 0);
            SetSign((reg & F_SIGN) != 0);
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
            return reg;*/
            F = reg & F_CARRY;
            reg = (reg >> 1) & 0xff;
            F |= sz53p[reg];
            return reg;
        }

        //Bit test operation (BIT b, r)
        public void Bit_R(int b, int reg) {
            /*
            bool bitset = ((reg & (1 << b)) != 0);  //true if bit is set
            SetZero(!bitset);                       //true if bit is not set, false if bit is set
            SetParity(!bitset);                     //copy of Z
            SetNeg(false);
            SetHalf(true);
            SetSign((b == 7) ? bitset : false);
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);*/
            F = ( F & F_CARRY ) | F_HALF | ( reg & ( F_3 | F_5 ) );
            if( !((reg & ( 0x01 << (b))) > 0)) F |= F_PARITY | F_ZERO;
            if( (b == 7) && ((reg & 0x80) > 0)) F |= F_SIGN; 
        }

        //Reset bit operation (RES b, r)
        public int Res_R(int b, int reg) {
            reg = reg & ~(1 << b);
            return reg;
        }

        //Set bit operation (SET b, r)
        public int Set_R(int b, int reg) {
            reg = reg | (1 << b);
            return reg;
        }

        //Decimal Adjust Accumulator (DAA)
        public void DAA() {
            int ans = A;
            int incr = 0;
            bool carry = (F & F_CARRY) != 0;

            if (((F & F_HALF) != 0) || ((ans & 0x0f) > 0x09)) {
                incr |= 0x06;
            }

            if (carry || (ans > 0x9f) || ((ans > 0x8f) && ((ans & 0x0f) > 0x09))) {
                incr |= 0x60;
            }

            if (ans > 0x99) {
                carry = true;
            }

            if ((F & F_NEG) != 0) {
                Sub_R(incr);
            } else {
                Add_R(incr);
            }

            ans = A;

            SetCarry(carry);
            SetParity(parity[ans]);
        }

        //Returns parity of a number (true if there are even numbers of 1, false otherwise)
        public bool GetParity(int val) {
            bool parity = false;
            int runningCounter = 0;
            for (int count = 0; count < 8; count++) {
                if ((val & 0x80) != 0)
                    runningCounter++;
                val = val << 1;
            }

            if (runningCounter % 2 == 0)
                parity = true;

            return parity;
        }

        public int GetDisplacement(int val) {
            int res = (128 ^ val) - 128;
            return res;
        }
    }
}