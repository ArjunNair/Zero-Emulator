using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;

//TO DO: Remove all the if(loggingEnabled) checks in final build!

namespace Speccy
{
    public class Z80Core
    {
        private zxmachine machine;

        public zxmachine Machine
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

        //Machine specific stuff
        public int interruptPeriod;
        public int interruptTime;

        FileStream logFile;
        StreamWriter logWriter;
        public bool loggingEnabled = false;

        private int startTime = 0;
        private int endTime = 0;
        public bool runningInterrupt = false;
        private bool running = true;
        public int tstates = 0;
        public int interruptMode; //0 = IM0, 1 = IM1, 2 = IM2
        public bool IFF1, IFF2;
        public bool resetOver = false;
        private bool HaltOn = false;
        private int timeToOutSound = 0;
        private int lastTimeOut = 0;

        //All registers
        private int a = 0, f = 0, bc = 0, hl = 0, de = 0, sp = 0, pc = 0, ix = 0, iy = 0; 
        private int i = 0, r = 0;

        //All alternate registers
        private int _af = 0, _bc = 0, _de = 0, _hl = 0;

        private const int F_CARRY =     0x01;
        private const int F_NEG =       0x02;
        private const int F_PARITY =    0x04;
        private const int F_3 =         0x08;
        private const int F_HALF =      0x010;
        private const int F_5 =         0x020;
        private const int F_ZERO =      0x040;
        private const int F_SIGN =      0x080;

        //Misc variables used in the switch-case statement
        private int opcode = 0;
        private int val, addr;


        public Z80Core()
        {
            logFile = new FileStream("Monitor.log", FileMode.Create, FileAccess.Write);
            logWriter = new StreamWriter(logFile);
        }

        public void Shutdown()
        {
            logWriter.Close();
        }

        //8 bit register access
        public int A
        {
            get
            {
                return a & 0xff;
            }

            set
            {
                a = value;
            }
        }

        public int B
        {
            get
            {
                return (bc >> 8) & 0xff; //Since b and c are stored left to right in memory here
            }

            set
            {
                bc = (bc & 0x00ff) | (value << 8); // mask out b, then or in the new value
            }
        }

        public int C
        {
            get
            {
                return (bc & 0xff); 
            }

            set
            {
                bc = (bc & 0xff00) | value;
            }
        }

        public int H
        {
            get
            {
                return (hl >> 8) & 0xff;
            }

            set
            {
                hl = (hl & 0x00ff) | (value << 8);
            }
        }

        public int L
        {
            get
            {
                return (hl & 0xff);
            }

            set
            {
                hl = (hl & 0xff00) | value;
            }
        }

        public int D
        {
            get
            {
                return (de >> 8) & 0xff;
            }

            set
            {
                de = (de & 0x00ff) | (value << 8);
            }
        }

        public int E
        {
            get
            {
                return (de & 0xff);
            }

            set
            {
                de = (de & 0xff00) | value;
            }
        }
                
        public int F
        {
            get
            {
                return f & 0xff;
            }

            set
            {
                f = value;
            }
        }

        public int I
        {
            get
            {
                return i & 0xff;
            }
            set
            {
                i = value;
            }
        }

        public int R
        {
            get
            {
                return r & 0xff;
            }
            set
            {
                r = value;
            }
        }

        //16 bit register access
        public int AF
        {
            get
            {
                return ((a << 8) | f);
            }

            set
            {
                a = (value & 0xff00) >> 8;
                f = value & 0x00ff;
            }
        }

        public int _AF
        {
            get
            {
                return _af;
            }

            set
            {
                _af = value;
            }
        }

        public int _HL
        {
            get
            {
                return _hl;
            }

            set
            {
                _hl = value;
            }
        }

        public int _BC
        {
            get
            {
                return _bc;
            }

            set
            {
                _bc = value;
            }
        }
        public int _DE
        {
            get
            {
                return _de;
            }

            set
            {
                _de = value;
            }
        }
       
        public int BC
        {
            get
            {
                return bc & 0xffff;
            }

            set
            {
                bc = value & 0xffff;
            }
        }
       
        public int DE
        {
            get
            {
                return de & 0xffff;
            }

            set
            {
                de = value & 0xffff;
            }
        }

        public int HL
        {
            get
            {
                return hl & 0xffff;
            }

            set
            {
                hl = value & 0xffff;
            }
        }

        public int IX
        {
            get
            {
                return ix & 0xffff;
            }

            set
            {
                ix = value & 0xffff;
            }
        }

        public int IXH
        {
            get
            {
                return (ix >> 8) & 0xff;
            }
            set
            {
                ix = (ix & 0x00ff) | (value << 8);
            }
        }

        public int IXL
        {
            get
            {
                return (ix & 0xff);
            }
            set
            {
                ix = (ix & 0xff00) | value;
            }
        }

        public int IYH
        {
            get
            {
                return (iy >> 8) & 0xff;
            }
            set
            {
                iy = (iy & 0x00ff) | (value << 8);
            }
        }

        public int IYL
        {
            get
            {
                return (iy & 0xff);
            }
            set
            {
                iy = (iy & 0xff00) | value;
            }
        }

        public int IY
        {
            get
            {
                return iy & 0xffff;
            }

            set
            {
                iy = value & 0xffff;
            }
        }

        public int SP
        {
            get
            {
                return sp & 0xffff;
            }

            set
            {
                sp = value & 0xffff;
            }
        }

        public int PC
        {
            get
            {
                return pc & 0xffff; 
            }

            set
            {
                pc = value & 0xffff;
            }
        }

        //Flag set/unset
        public void SetCarry(bool val)
        {
            if (val)
            {
                f |= F_CARRY;
            }
            else
            {
                F &= ~(F_CARRY);
            }
        }

        public void SetNeg(bool val)
        {
            if (val)
            {
                F = F | F_NEG;
            }
            else
            {
                F = F & ~(F_NEG);
            }
        }

        public void SetParity(bool val)
        {
            if (val)
            {
                f |= F_PARITY;
            }
            else
            {
                F &= ~(F_PARITY);
            }
        }

        public void SetHalf(bool val)
        {
            if (val)
            {
                f |= F_HALF;
            }
            else
            {
                F &= ~(F_HALF);
            }
        }

        public void SetZero(bool val)
        {
            if (val)
            {
                f |= F_ZERO;
            }
            else
            {
                F &= ~(F_ZERO);
            }
        }

        public void SetSign(bool val)
        {
            if (val)
            {
                f |= F_SIGN;
            }
            else
            {
                F &= ~(F_SIGN);
            }
        }

        public void SetF3(bool val)
        {
            if (val)
            {
                f |= F_3;
            }
            else
            {
                F &= ~(F_3);
            }
        }

        public void SetF5(bool val)
        {
            if (val)
            {
                f |= F_5;
            }
            else
            {
                F &= ~(F_5);
            }
        }
       
        //Reset
        public virtual void Reset()
        {
            PC = 0;
            SP = 0;

            AF = 0;
            BC = 0;
            DE = 0;
            HL = 0;

            exx();
            ex_af_af();

            AF = 0;
            BC = 0;
            DE = 0;
            HL = 0;

            IY = 0;
            IY = 0;

           I = 0;
           interruptMode = 0;
           logWriter.WriteLine("SP     HL     BC     DE     A      PC     Opcode            Total T-States      SZ5H3PNC");
           logWriter.WriteLine("----------------------------------------------------------------------------------------");
           resetOver = false;
        }

        public void exx()
        {
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

        public void ex_af_af()
        {
            int temp = _af;
            _af = AF;
            AF = temp;
        }

        public int PeekByte(int addr)
        {
            return (Machine.PeekByte(addr) & 0xff);
        }

        public void PokeByte(int addr, int b)
        {
            Machine.PokeByte(addr, b & 0xff);
        }

        //Reads next instruction from address pointed to by PC
        public int FetchInstruction()
        {
            int b = PeekByte(PC);
            PC = (PC + 1) & 0xffff;
            return b;
        }

        public int PeekWord(int addr)
        {
            int w = Machine.PeekByte(addr) + (Machine.PeekByte((addr + 1) & 0xffff) << 8);
            return w;
        }

        public void PokeWord(int addr, int w)
        {
            Machine.PokeByte(addr, w & 0xff);
            Machine.PokeByte((addr + 1) & 0xffff, w >> 8);
        }

        protected void Log(String op)
        {
           // return;
            //Dumps register states and opcode info. Note PC is reduced by one before printing because it's pre-incremented!
            logWriter.Write("#{0,-5:X} #{1,-5:X} #{2,-5:X} #{3,-5:X} #{4,-5:X} #{5,-5:X} {6, -17} #{7,-18}", SP, HL, BC, DE, A, PC - 1, op, tstates);
            logWriter.Write((F & F_SIGN) != 0 ? 1:0);
            logWriter.Write((F & F_ZERO) != 0 ? 1:0);
            logWriter.Write((F & F_5) != 0 ? 1:0);
            logWriter.Write((F & F_HALF) != 0 ? 1 : 0);
            logWriter.Write((F & F_3) != 0 ? 1:0);
            logWriter.Write((F & F_PARITY) != 0 ? 1 : 0);
            logWriter.Write((F & F_NEG) != 0 ? 1:0);
            logWriter.Write((F & F_CARRY) != 0 ? 1:0);
            logWriter.Write("#{0, -6}", IY);
            logWriter.WriteLine();
        }

        public int Inc(int reg)
        {
            SetParity((reg == 0x7f));   //reg = 127? We're gonna overflow on inc! 
            SetNeg(false);               //Negative is always reset (0)
            SetHalf((((reg & 0x0f) + 1) & F_HALF) != 0); 

            reg = (reg + 1) & 0xff;
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
            SetZero(reg == 0);
            SetSign((reg & F_SIGN) != 0);
            return reg;
        }

        public int Dec(int reg)
        {
            SetNeg(true);                //Negative is always set (1)
            SetParity((reg == 0x80));   //reg = -128? We're gonna overflow on dec!
            SetHalf((((reg & 0x0f) - 1) & F_HALF) != 0); 

            reg = (reg - 1) & 0xff;
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
            SetZero(reg == 0);
            SetSign((reg & F_SIGN) != 0);
            
            return reg;
        }

        //16 bit addition (no carry)
        public int Add_RR(int rr1, int rr2)
        {
            SetNeg(false);
            rr1 += rr2;
            SetHalf(((rr1 & 0x0fff) + (rr2 & 0x0fff) & 0x1000) != 0);
            SetCarry((rr1 & 0x10000) != 0);
            return (rr1 & 0xffff);
        }

        //8 bit add to accumulator (no carry)
        public void Add_R(int reg)
        {
            SetNeg(false);
            SetHalf((((A & 0x0f) + (reg & 0x0f)) & F_HALF) != 0);
            
            int ans = (A + reg) & 0xff;
            SetCarry(((A + reg) & 0x100) != 0);
            SetParity(((A ^ ~reg) & (A ^ ans) & 0x80) != 0);
            SetSign((ans & F_SIGN) != 0);
            SetZero(ans == 0);
            SetHalf((((A & 0x0f) + (reg & 0x0f)) & F_HALF) != 0);

            A = ans;
        }

        //Add with carry into accumulator
        public void Adc_R(int reg)
        {
            SetNeg(false);
            int fc = ((F & F_CARRY) != 0 ? 1 : 0);
            int ans = (A + reg + fc) & 0xff;

            SetCarry(((A + reg + fc) & 0x100) != 0);
            SetHalf((((A & 0x0f) + (reg & 0x0f) + fc) & F_HALF) != 0);
            SetParity(((A ^ ~reg) & (A ^ ans) & 0x80) != 0);
            SetSign((ans & F_SIGN) != 0);
            SetZero(ans == 0);
            A = ans;
        }

        //Add with carry into HL
        public void Adc_RR(int reg)
        {
            SetNeg(false);
            int fc = ((F & F_CARRY) != 0 ? 1 : 0);
            int ans = (HL + reg + fc) & 0xffff;
            SetCarry(((HL + reg + fc) & 0x10000) != 0);
            SetHalf((((HL & 0x0fff) + (reg & 0x0fff) + fc) & 0x1000) != 0);
            SetParity(((HL ^ ~reg) & (HL ^ ans) & 0x8000) != 0);
            SetSign((ans & (F_SIGN << 8)) != 0);
            SetZero(ans == 0);
            HL = ans;
        }

        //8 bit subtract to accumulator (no carry)
        public void Sub_R(int reg)
        {
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

            A = ans;
        }

        //8 bit subtract from accumulator with carry (SBC A, r)
        public void Sbc_R(int reg)
        {
            SetNeg(true);
            int fc = ((F & F_CARRY) != 0 ? 1 : 0);
         
            int ans = (A - reg - fc) & 0xff;
            SetCarry(((A - reg - fc) & 0x100) != 0);
            SetParity(((A ^ reg) & (A ^ ans) & 0x80) != 0);
            SetSign((ans & F_SIGN) != 0);
            SetHalf((((A & 0x0f) - (reg & 0x0f) - fc) & F_HALF) != 0);
            SetZero(ans == 0);

            A = ans;
        }
        
        //16 bit subtract from HL with carry
        public void Sbc_RR(int reg)
        {
            SetNeg(true);
            int fc = ((F & F_CARRY) != 0 ? 1:0);
            
            SetHalf((((HL & 0x0fff) - (reg & 0x0fff) - fc) & 0x1000) != 0);

            int ans = (HL - reg - fc) & 0xffff;
            SetCarry(((HL - reg - fc) & 0x10000) != 0);
            SetParity(((HL ^ reg) & (HL ^ ans) & 0x8000) != 0);
            SetSign((ans & (F_SIGN << 8)) != 0);
           // SetHalf((((HL & 0x0f) - (reg & 0x0f) - fc) & F_HALF) != 0);
            SetZero(ans == 0);

            HL = ans;
        }

        //Comparison with accumulator
        public void Cp_R(int reg)
        {
            SetNeg(true);
           
            int result = A - reg;
            int ans = result & 0xff;
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
            SetHalf((((A & 0x0f) - (reg & 0x0f)) & F_HALF) != 0);
            SetParity(((A ^ reg) & (A ^ ans) & 0x80) != 0);
            SetSign((ans & F_SIGN) != 0);
            SetZero(ans == 0);
            SetCarry((result & 0x100) != 0);
        }

        //AND with accumulator
        public void And_R(int reg)
        {
            SetCarry(false);
            SetNeg(false);

            int ans = A & reg;
            SetSign((ans & F_SIGN) != 0);
            SetHalf(true);
            SetZero(ans == 0);
            SetParity(GetParity(ans));
            SetF3((ans & F_3) != 0);
            SetF5((ans & F_5) != 0);
            A = ans;
        }

        //XOR with accumulator
        public void Xor_R(int reg)
        {
            SetCarry(false);
            SetNeg(false);

            int ans = (A ^ reg) & 0xff;
            SetSign((ans & F_SIGN) != 0);
            SetHalf(false);
            SetZero(ans == 0);
            SetParity(GetParity(ans));
            SetF3((ans & F_3) != 0);
            SetF5((ans & F_5) != 0);
            A = ans;
        }

        //OR with accumulator
        public void Or_R(int reg)
        {
            SetCarry(false);
            SetNeg(false);

            int ans = A | reg;
            SetSign((ans & F_SIGN) != 0);
            SetHalf(false);
            SetZero(ans == 0);
            SetParity(GetParity(ans));
            SetF3((ans & F_3) != 0);
            SetF5((ans & F_5) != 0);
            A = ans;
        }

        //Rotate left with carry register (RLC r)
        public int Rlc_R(int reg)
        {
            int msb = reg & F_SIGN;

            if (msb != 0)
            {
                reg = ((reg << 1) | 0x01) & 0xff;
            }
            else
                reg = (reg << 1) & 0xff;

            SetCarry(msb != 0);
            SetHalf(false);
            SetNeg(false);
            SetParity(GetParity(reg));
            SetZero(reg == 0);
            SetSign((reg & F_SIGN) != 0);

            return reg;
        }

        //Rotate right with carry register (RLC r)
        public int Rrc_R(int reg)
        {
            int lsb = reg & F_CARRY; //save the lsb bit

            if (lsb != 0)
            {
                reg = (reg >> 1) | 0x80;
            }
            else
                reg = reg >> 1;
            

            SetCarry(lsb != 0);
            SetHalf(false);
            SetNeg(false);
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
            SetParity(GetParity(reg));
            SetZero(reg == 0);
            SetSign((reg & F_SIGN) != 0);

            return reg;
        }

        //Rotate left register (RL r)
        public int Rl_R(int reg)
        {
            bool rc = (reg & F_SIGN) != 0;
            int msb = F & F_CARRY; //save the msb bit

            if (msb != 0)
            {
                reg = ((reg << 1) | 0x01) & 0xff;
            }
            else
            {
                reg = (reg << 1) & 0xff;
            }
            
            SetCarry(rc);
            SetHalf(false);
            SetNeg(false);
            SetParity(GetParity(reg));
            SetZero(reg == 0);
            SetSign((reg & F_SIGN) != 0);

            return reg;
        }

        //Rotate right register (RL r)
        public int Rr_R(int reg)
        {
            
            bool rc =(reg & F_CARRY) != 0;
            int lsb = F & F_CARRY; //save the lsb bit

            if (lsb != 0)
            {
                reg = (reg >> 1) | 0x80;
            }
            else
                reg = reg >> 1;
            

            SetCarry(rc);
            SetHalf(false);
            SetNeg(false);
            SetParity(GetParity(reg));
            SetZero(reg == 0);
            SetSign((reg & F_SIGN) != 0);

            return reg;
        }

        //Shift left arithmetic register (SLA r)
        public int Sla_R(int reg)
        {
            int msb = reg & F_SIGN; //save the msb bit

            reg = (reg << 1) & 0xff;
            
            SetCarry(msb != 0);
            SetHalf(false);
            SetNeg(false);
            SetParity(GetParity(reg));
            SetZero(reg == 0);
            SetSign((reg & F_SIGN) != 0);

            return reg;
        }

        //Shift right arithmetic register (SRA r)
        public int Sra_R(int reg)
        {
            int lsb = reg & F_CARRY; //save the lsb bit
            reg = (reg >> 1) | (reg & F_SIGN);

            SetCarry(lsb != 0);
            SetHalf(false);
            SetNeg(false);
            SetParity(GetParity(reg));
            SetZero(reg == 0);
            SetSign((reg & 0x80) != 0);
           
            return reg;
        }

        //Shift left logical register (SLL r)
        public int Sll_R(int reg)
        {
            int msb = reg & F_SIGN; //save the msb bit
            reg = reg << 1;
            reg = (reg | 0x01) & 0xff;

            SetCarry(msb != 0);
            SetHalf(false);
            SetNeg(false);
            SetParity(GetParity(reg));
            SetZero(reg == 0);
            SetSign((reg & F_SIGN) != 0);

            return reg;
        }

        //Shift right logical register (SRL r)
        public int Srl_R(int reg)
        {
            int lsb = reg & F_CARRY; //save the lsb bit
            reg = reg >> 1;

            SetCarry(lsb != 0);
            SetHalf(false);
            SetNeg(false);
            SetParity(GetParity(reg));
            SetZero(reg == 0);
            SetSign((reg & F_SIGN) != 0);

            return reg;
        }

        //Bit test operation (BIT b, r)
        public void Bit_R(int b, int reg)
        {
            bool bitset = ((reg & (1 << b)) != 0);
            SetZero(!bitset);
            SetParity(!bitset);
            SetNeg(false);
            SetHalf(true);
            SetSign((b == F_SIGN) ? bitset : false);
            SetF3((reg & F_3) != 0);
            SetF5((reg & F_5) != 0);
        }

        //Reset bit operation (RES b, r)
        public int Res_R(int b, int reg)
        {
            reg = reg & ~(1 << b);
            return reg;
        }

        //Set bit operation (SET b, r)
        public int Set_R(int b, int reg)
        {
            reg = reg | (1 << b);
            return reg;
        }

        //Decimal Adjust Accumulator (DAA)
        public void DAA()
        {
            int ans = A;
            int incr = 0;
            bool carry = (F & F_CARRY) != 0;

            if (((F & F_HALF)!= 0) || ((ans & 0x0f) > 0x09))
            {
                incr |= 0x06;
            }

            if (carry || (ans > 0x9f) || ((ans > 0x8f) && ((ans & 0x0f) > 0x09)))
            {
                incr |= 0x60;
            }

            if (ans > 0x99)
            {
                carry = true;
            }

            if ((F & F_NEG)!= 0)
            {
                Sub_R(incr);
            }
            else
            {
                Add_R(incr);
            }

            ans = A;

            SetCarry(carry);
            SetParity(GetParity(ans));
        }

        //In A, (n)
        public int In(int port)
        {
            return Machine.In(port);
        }

        //In r, (C)
        public int In()
        {
            int result = Machine.In(BC);

            SetNeg(false);
            SetParity(GetParity(result));
            SetSign((result & F_SIGN) != 0);
            SetZero(result == 0);
            SetHalf(false);
            SetF3((result & F_3) != 0);
            SetF5((result & F_5) != 0);

            return result;
        }

        //Out
        public void Out(int port, int val)
        {
            Machine.Out(port, val);
        }

        //Returns parity of a number (true if there are even numbers of 1, false otherwise)
        bool GetParity(int val)
        {
            bool parity = false;
            int runningCounter = 0;
            for (int count = 1; count < 8; count++)
            {
                if ((val & 0x80) != 0)
                    runningCounter++;
                val = val << 1;
            }
            
            if (runningCounter % 2 == 0)
                parity = true;

            return parity;
        }

        int GetDisplacement(int val)
        {
            int res = (128 ^ val) - 128;
            return res;
        }

        public void Interrupt()
        {
            if (interruptMode < 2) //IM0 = IM1 for our purpose
            {
                tstates += 13;
                //Perform a RST 0x038  
                SP -= 2;
                PokeWord(SP, PC);
                PC = 0x38;
            }
            else    //IM 2
            {
                int ptr = (I << 8 | 0xff) & 0xffff; 
                SP -= 2;
                PokeWord(SP, PC);
                PC = PeekWord(ptr); 
                tstates += 19;
            }
        }

        public void Process()
        {
            for (; ; )
            {
                //Time to fire an interrupt?
                if (tstates >= interruptTime)
                {
                    tstates = tstates - interruptTime;
                    timeToOutSound =  timeToOutSound - interruptTime;
                    machine.UpdateInput();
                    machine.UpdateScreenBuffer();
                    
                    if (HaltOn)
                    {
                        HaltOn = false;
                        PC++;
                    }

                    if (IFF1)
                    {
                        //Disable interrupts
                        IFF1 = false;
                        IFF2 = false;
                        runningInterrupt = true;
                        Interrupt();
                    }
                    break;
                }

                //timeToOutSound = tstates;
                if ((tstates - timeToOutSound) >= 79)
                {
                    //machine.OutSound();
                   // machine.UpdateAudio();
                    timeToOutSound = tstates;
                }

                opcode = FetchInstruction();
                Execute();
               
                //Interrupt period over?
                if (runningInterrupt && tstates >= interruptPeriod)
                    runningInterrupt = false;
            }

        }

        public void Execute()
        {
                int disp = 0; //used later on to calculate relative jumps
                //Massive switch-case to decode the instructions!
                switch (opcode)
                {
                    #region NOP
                    case 0x00: //NOP
                        if (loggingEnabled) Log("NOP");
                        tstates += 4;
                        break;
                    #endregion

                    # region 16 bit load operations (LD rr, nn)
                    /** LD rr, nn (excluding DD prefix) **/
                    case 0x01: //LD BC, nn
                        if (loggingEnabled) Log(String.Format("LD BC, {0,-6:X}", PeekWord(PC)));
                        tstates += 10;
                        BC = PeekWord(PC);
                        PC += 2;
                        break;

                    case 0x11:  //LD DE, nn
                        if (loggingEnabled) Log(String.Format("LD DE, {0,-6:X}", PeekWord(PC)));
                        tstates += 10;
                        DE = PeekWord(PC);
                        PC += 2;
                        break;
                    
                    case 0x21:  //LD HL, nn
                        if (loggingEnabled) Log(String.Format("LD HL, {0,-6:X}", PeekWord(PC)));
                        tstates += 10;
                        HL = PeekWord(PC);
                        PC += 2;
                        break;

                    case 0x2A:  //LD HL, (nn)
                        if (loggingEnabled) Log(String.Format("LD HL, ({0,-6:X})", PeekWord(PC)));
                        tstates += 16;
                        HL = PeekWord(PeekWord(PC));
                        PC += 2;
                        break;

                    case 0x31:  //LD SP, nn
                        if (loggingEnabled) Log(String.Format("LD SP, {0,-6:X}", PeekWord(PC)));
                        tstates += 10;
                        SP = PeekWord(PC);
                        PC += 2;
                        break;

                    case 0xF9:  //LD SP, HL
                        if (loggingEnabled) Log("LD SP, HL");
                        tstates += 6;
                        SP = HL;
                        break;
                    #endregion

                    #region 16 bit increments (INC rr)
                    /** INC rr **/
                    case 0x03:  //INC BC
                        if (loggingEnabled) Log("INC BC");
                        tstates += 6;
                        BC++;
                        break;

                    case 0x13:  //INC DE
                        if (loggingEnabled) Log("INC DE");
                        tstates += 6;
                        DE++;
                        break;
                    
                    case 0x23:  //INC HL
                        if (loggingEnabled) Log("INC HL");
                        tstates += 6;
                        HL++;
                        break;

                    case 0x33:  //INC SP
                        if (loggingEnabled) Log("INC SP");
                        tstates += 6;
                        SP++;
                        break;
                    #endregion INC rr

                    #region 8 bit increments (INC r)
                    /** INC r + INC (HL) **/
                    case 0x04:  //INC B
                        if (loggingEnabled) Log("INC B");
                        tstates += 4;
                        B = Inc(B);
                        break;
                    
                    case 0x0C:  //INC C
                        if (loggingEnabled) Log("INC C");
                        tstates += 4;
                        C = Inc(C);
                        break;

                    case 0x14:  //INC D
                        if (loggingEnabled) Log("INC D");
                        tstates += 4;
                        D = Inc(D);
                        break;

                    case 0x1C:  //INC E
                        if (loggingEnabled) Log("INC E");
                        tstates += 4;
                        E = Inc(E);
                        break;

                    case 0x24:  //INC H
                        if (loggingEnabled) Log("INC H");
                        tstates += 4;
                        H = Inc(H);
                        break;

                    case 0x2C:  //INC L
                        if (loggingEnabled) Log("INC L");
                        tstates += 4;
                        L = Inc(L);
                        break;

                    case 0x34:  //INC (HL)
                        if (loggingEnabled) Log("INC (HL)");
                        tstates += 11;
                        val = PeekByte(HL);
                        val = Inc(val);
                        PokeByte(HL, val);
                        break;

                    case 0x3C:  //INC A
                        if (loggingEnabled) Log("INC A");
                        tstates += 4;
                        A = Inc(A);
                        break;
                    #endregion

                    #region 8 bit decrement (DEC r)
                    /** DEC r + DEC (HL)**/
                    case 0x05: //DEC B
                        if (loggingEnabled) Log("DEC B");
                        tstates += 4;
                        B = Dec(B);
                        break;

                    case 0x0D:    //DEC C
                        if (loggingEnabled) Log("DEC C");
                        tstates += 4;
                        C = Dec(C);
                        break;

                    case 0x15:  //DEC D
                        if (loggingEnabled) Log("DEC D");
                        tstates += 4;
                        D = Dec(D);
                        break;

                    case 0x1D:  //DEC E
                        if (loggingEnabled) Log("DEC E");
                        tstates += 4;
                        E = Dec(E);
                        break;

                    case 0x25:  //DEC H
                        if (loggingEnabled) Log("DEC H");
                        tstates += 4;
                        H = Dec(H);
                        break;

                    case 0x2D:  //DEC L
                        if (loggingEnabled) Log("DEC L");
                        tstates += 4;
                        L = Dec(L);
                        break;

                    case 0x35:  //DEC (HL)
                        if (loggingEnabled) Log("DEC (HL)");
                        tstates += 11;
                        //val = PeekByte(HL);
                        val = Dec(PeekByte(HL));
                        PokeByte(HL, val);
                        break;

                    case 0x3D:  //DEC A
                        if (loggingEnabled) Log("DEC A");
                        tstates += 4;
                        A = Dec(A);
                        break;
                    #endregion

                    #region 16 bit decrements
                    /** DEC rr **/
                    case 0x0B:  //DEC BC
                        if (loggingEnabled) Log("DEC BC");
                        tstates += 6;
                        BC--;
                        break;

                    case 0x1B:  //DEC DE
                        if (loggingEnabled) Log("DEC DE");
                        tstates += 6;
                        DE--;
                        break;

                    case 0x2B:  //DEC HL
                        if (loggingEnabled) Log("DEC HL");
                        tstates += 6;
                        HL--;
                        break;

                    case 0x3B:  //DEC SP
                        if (loggingEnabled) Log("DEC SP");
                        tstates += 6;
                        SP--;
                        break;
                    #endregion

                    #region Immediate load operations (LD (nn), r)
                    /** LD (rr), r + LD (nn), HL  + LD (nn), A **/
                    case 0x02: //LD (BC), A
                        if (loggingEnabled) Log("LD (BC), A");
                        tstates += 7;
                        PokeByte(BC, A);
                        break;
                             
                    case 0x12:  //LD (DE), A
                        if (loggingEnabled) Log("LD (DE), A");
                        tstates += 7;
                        PokeByte(DE, A);
                        break;
                    
                    case 0x22:  //LD (nn), HL
                        if (loggingEnabled) Log(String.Format("LD ({0,-6:X}), HL", PeekWord(PC)));
                        tstates += 16;
                        addr = PeekWord(PC);
                        //PokeByte(addr, L);
                        //PokeByte(addr + 1, H);
                        PokeWord(addr, HL);
                        PC += 2;
                        break;
                        
                    case 0x32:  //LD (nn), A
                        if (loggingEnabled) Log(String.Format("LD ({0,-6:X}), A", PeekWord(PC)));
                        tstates += 13;
                        addr = PeekWord(PC);
                        PokeByte(addr, A);
                        PC += 2;
                        break;

                    case 0x36:  //LD (HL), n
                        if (loggingEnabled) Log(String.Format("LD (HL), {0,-6:X}", PeekByte(PC)));
                        tstates += 10;
                        val = PeekByte(PC);
                        PokeByte(HL, val);
                        PC += 1;
                        break;
                    #endregion

                    #region Indirect load operations (LD r, r)
                    /** LD r, r **/
                    case 0x06: //LD B, n
                        if (loggingEnabled) Log(String.Format("LD B, {0,-6:X}", PeekByte(PC)));
                        tstates += 7;
                        B = PeekByte(PC);
                        PC += 1;
                        break;

                    case 0x0A:  //LD A, (BC)
                        if (loggingEnabled) Log("LD A, (BC)");
                        tstates += 7;
                        A = PeekByte(BC);
                        break;

                    case 0x0E:  //LD C, n
                        if (loggingEnabled) Log(String.Format("LD C, {0,-6:X}", PeekByte(PC)));
                        tstates += 7;
                        C = PeekByte(PC);
                        PC += 1;
                        break;

                    case 0x16:  //LD D,n
                        if (loggingEnabled) Log(String.Format("LD D, {0,-6:X}", PeekByte(PC)));
                        tstates += 7;
                        D = PeekByte(PC);
                        PC += 1;
                        break;

                    case 0x1A:  //LD A,(DE)
                        if (loggingEnabled) Log("LD A, (DE)");
                        tstates += 7;
                        A = PeekByte(DE);
                        break;

                    case 0x1E:  //LD E,n
                        if (loggingEnabled) Log(String.Format("LD E, {0,-6:X}", PeekByte(PC)));
                        tstates += 7;
                        E = PeekByte(PC);
                        PC += 1;
                        break;

                    case 0x26:  //LD H,n
                        if (loggingEnabled) Log(String.Format("LD H, {0,-6:X}", PeekByte(PC)));
                        tstates += 7;
                        H = PeekByte(PC);
                        PC += 1;
                        break;

                    case 0x2E:  //LD L,n
                        if (loggingEnabled) Log(String.Format("LD L, {0,-6:X}", PeekByte(PC)));
                        tstates += 7;
                        L = PeekByte(PC);
                        PC += 1;
                        break;

                    case 0x3A:  //LD A,(nn)
                        if (loggingEnabled) Log(String.Format("LD A, ({0,-6:X})", PeekWord(PC)));
                        tstates += 13;
                        addr = PeekWord(PC);
                        A = PeekByte(addr);
                        PC += 2;
                        break;

                    case 0x3E:  //LD A,n
                        if (loggingEnabled) Log(String.Format("LD A, {0,-6:X}", PeekByte(PC)));
                        tstates += 7;
                        A = PeekByte(PC);
                        PC += 1;
                        break;

                    case 0x40:  //LD B,B
                        if (loggingEnabled) Log("LD B, B");
                        tstates += 4;
                        B = B;
                        break;

                    case 0x41:  //LD B,C
                        if (loggingEnabled) Log("LD B, C");
                        tstates += 4;
                        B = C;
                        break;

                    case 0x42:  //LD B,D
                        if (loggingEnabled) Log("LD B, D");
                        tstates += 4;
                        B = D;
                        break;

                    case 0x43:  //LD B,E
                        if (loggingEnabled) Log("LD B, E");
                        tstates += 4;
                        B = E;
                        break;

                    case 0x44:  //LD B,H
                        if (loggingEnabled) Log("LD B, H");
                        tstates += 4;
                        B = H;
                        break;

                    case 0x45:  //LD B,L
                        if (loggingEnabled) Log("LD B, L");
                        tstates += 4;
                        B = L;
                        break;

                    case 0x46:  //LD B,(HL)
                        if (loggingEnabled) Log("LD B, (HL)");
                        tstates += 7;
                        B = PeekByte(HL);
                        break;

                    case 0x47:  //LD B,A
                        if (loggingEnabled) Log("LD B, A");
                        tstates += 4;
                        B = A;
                        break;

                    case 0x48:  //LD C,B
                        if (loggingEnabled) Log("LD C, B");
                        tstates += 4;
                        C = B;
                        break;

                    case 0x49:  //LD C,C
                        if (loggingEnabled) Log("LD C, C");
                        tstates += 4;
                        C = C;
                        break;

                    case 0x4A:  //LD C,D
                        if (loggingEnabled) Log("LD C, D");
                        tstates += 4;
                        C = D;
                        break;

                    case 0x4B:  //LD C,E
                        if (loggingEnabled) Log("LD C, E");
                        tstates += 4;
                        C = E;
                        break;

                    case 0x4C:  //LD C,H
                        if (loggingEnabled) Log("LD C, H");
                        tstates += 4;
                        C = H;
                        break;

                    case 0x4D:  //LD C,L
                        if (loggingEnabled) Log("LD C, L");
                        tstates += 4;
                        C = L;
                        break;

                    case 0x4E:  //LD C, (HL)
                        if (loggingEnabled) Log("LD C, (HL)");
                        tstates += 7;
                        C = PeekByte(HL);
                        break;

                    case 0x4F:  //LD C,A
                        if (loggingEnabled) Log("LD C, A");
                        tstates += 4;
                        C = A;
                        break;

                    case 0x50:  //LD D,B
                        if (loggingEnabled) Log("LD D, B");
                        tstates += 4;
                        D = B;
                        break;

                    case 0x51:  //LD D,C
                        if (loggingEnabled) Log("LD D, C");
                        tstates += 4;
                        D = C;
                        break;

                    case 0x52:  //LD D,D
                        if (loggingEnabled) Log("LD D, D");
                        tstates += 4;
                        D = D;
                        break;

                    case 0x53:  //LD D,E
                        if (loggingEnabled) Log("LD D, E");
                        tstates += 4;
                        D = E;
                        break;

                    case 0x54:  //LD D,H
                        if (loggingEnabled) Log("LD D, H");
                        tstates += 4;
                        D = H;
                        break;

                    case 0x55:  //LD D,L
                        if (loggingEnabled) Log("LD D, L");
                        tstates += 4;
                        D = L;
                        break;

                    case 0x56:  //LD D,(HL)
                        if (loggingEnabled) Log("LD D, (HL)");
                        tstates += 7;
                        D = PeekByte(HL);
                        break;

                    case 0x57:  //LD D,A
                        if (loggingEnabled) Log("LD D, A");
                        tstates += 4;
                        D = A;
                        break;

                    case 0x58:  //LD E,B
                        if (loggingEnabled) Log("LD E, B");
                        tstates += 4;
                        E = B;
                        break;

                    case 0x59:  //LD E,C
                        if (loggingEnabled) Log("LD E, C");
                        tstates += 4;
                        E = C;
                        break;

                    case 0x5A:  //LD E,D
                        if (loggingEnabled) Log("LD E, D");
                        tstates += 4;
                        E = D;
                        break;

                    case 0x5B:  //LD E,E
                        if (loggingEnabled) Log("LD E, E");
                        tstates += 4;
                        E = E;
                        break;

                    case 0x5C:  //LD E,H
                        if (loggingEnabled) Log("LD E, H");
                        tstates += 4;
                        E = H;
                        break;

                    case 0x5D:  //LD E,L
                        if (loggingEnabled) Log("LD E, L");
                        tstates += 4;
                        E = L;
                        break;

                    case 0x5E:  //LD E,(HL)
                        if (loggingEnabled) Log("LD E, (HL)");
                        tstates += 7;
                        E = PeekByte(HL);
                        break;

                    case 0x5F:  //LD E,A
                        if (loggingEnabled) Log("LD E, A");
                        tstates += 4;
                        E = A;
                        break;

                    case 0x60:  //LD H,B
                        if (loggingEnabled) Log("LD H, B");
                        tstates += 4;
                        H = B;
                        break;

                    case 0x61:  //LD H,C
                        if (loggingEnabled) Log("LD H, C");
                        tstates += 4;
                        H = C;
                        break;

                    case 0x62:  //LD H,D
                        if (loggingEnabled) Log("LD H, D");
                        tstates += 4;
                        H = D;
                        break;

                    case 0x63:  //LD H,E
                        if (loggingEnabled) Log("LD H, E");
                        tstates += 4;
                        H = E;
                        break;

                    case 0x64:  //LD H,H
                        if (loggingEnabled) Log("LD H, H");
                        tstates += 4;
                        H = H;
                        break;

                    case 0x65:  //LD H,L
                        if (loggingEnabled) Log("LD H, L");
                        tstates += 4;
                        H = L;
                        break;

                    case 0x66:  //LD H,(HL)
                        if (loggingEnabled) Log("LD H, (HL)");
                        tstates += 7;
                        H = PeekByte(HL);
                        break;

                    case 0x67:  //LD H,A
                        if (loggingEnabled) Log("LD H, A");
                        tstates += 4;
                        H = A;
                        break;

                    case 0x68:  //LD L,B
                        if (loggingEnabled) Log("LD L, B");
                        tstates += 4;
                        L = B;
                        break;

                    case 0x69:  //LD L,C
                        if (loggingEnabled) Log("LD L, C");
                        tstates += 4;
                        L = C;
                        break;

                    case 0x6A:  //LD L,D
                        if (loggingEnabled) Log("LD L, D");
                        tstates += 4;
                        L = D;
                        break;

                    case 0x6B:  //LD L,E
                        if (loggingEnabled) Log("LD L, E");
                        tstates += 4;
                        L = E;
                        break;

                    case 0x6C:  //LD L,H
                        if (loggingEnabled) Log("LD L, H");
                        tstates += 4;
                        L = H;
                        break;

                    case 0x6D:  //LD L,L
                        if (loggingEnabled) Log("LD L, L");
                        tstates += 4;
                        L = L;
                        break;

                    case 0x6E:  //LD L,(HL)
                        if (loggingEnabled) Log("LD L, (HL)");
                        tstates += 7;
                        L = PeekByte(HL);
                        break;

                    case 0x6F:  //LD L,A
                        if (loggingEnabled) Log("LD L, A");
                        tstates += 4;
                        L = A;
                        break;

                    case 0x70:  //LD (HL),B
                        if (loggingEnabled) Log("LD (HL), B");
                        tstates += 7;
                        PokeByte(HL, B);
                        break;

                    case 0x71:  //LD (HL),C
                        if (loggingEnabled) Log("LD (HL), C");
                        tstates += 7;
                        PokeByte(HL, C);
                        break;

                    case 0x72:  //LD (HL),D
                        if (loggingEnabled) Log("LD (HL), D");
                        tstates += 7;
                        PokeByte(HL, D);
                        break;

                    case 0x73:  //LD (HL),E
                        if (loggingEnabled) Log("LD (HL), E");
                        tstates += 7;
                        PokeByte(HL, E);
                        break;

                    case 0x74:  //LD (HL),H
                        if (loggingEnabled) Log("LD (HL), H");
                        tstates += 4;
                        PokeByte(HL, H);
                        break;

                    case 0x75:  //LD (HL),L
                        if (loggingEnabled) Log("LD (HL), L");
                        tstates += 4;
                        PokeByte(HL, L);
                        break;

                    case 0x77:  //LD (HL),A
                        if (loggingEnabled) Log("LD (HL), A");
                        tstates += 4;
                        PokeByte(HL, A);
                        break;

                    case 0x78:  //LD A,B
                        if (loggingEnabled) Log("LD A, B");
                        tstates += 4;
                        A = B;
                        break;

                    case 0x79:  //LD A,C
                        if (loggingEnabled) Log("LD A, C");
                        tstates += 4;
                        A = C;
                        break;

                    case 0x7A:  //LD A,D
                        if (loggingEnabled) Log("LD A, D");
                        tstates += 4;
                        A = D;
                        break;

                    case 0x7B:  //LD A,E
                        if (loggingEnabled) Log("LD A, E");
                        tstates += 4;
                        A = E;
                        break;

                    case 0x7C:  //LD A,H
                        if (loggingEnabled) Log("LD A, H");
                        tstates += 4;
                        A = H;
                        break;

                    case 0x7D:  //LD A,L
                        if (loggingEnabled) Log("LD A, L");
                        tstates += 4;
                        A = L;
                        break;

                    case 0x7E:  //LD A,(HL)
                        if (loggingEnabled) Log("LD A, (HL)");
                        tstates += 7;
                        A = PeekByte(HL);
                        break;

                    case 0x7F:  //LD A,A
                        if (loggingEnabled) Log("LD A, A");
                        tstates += 4;
                        A = A;
                        break;
                    #endregion

                    #region Rotates on Accumulator
                    /** Accumulator Rotates **/
                    case 0x07: //RLCA 
                        if (loggingEnabled) Log("RLCA");
                        tstates += 4;
                        bool ac = (A & F_SIGN) != 0; //save the msb bit

                        if (ac)
                        {
                            A = ((A << 1) | F_CARRY) & 0xff;
                        }
                        else
                        {
                            A = (A << 1) & 0xff;
                        }

                        SetCarry(ac);
                        SetHalf(false);
                        SetNeg(false);
                        break;

                    case 0x0F:  //RRCA
                        if (loggingEnabled) Log("RRCA");
                        tstates += 4;

                        ac = (A & F_CARRY) != 0; //save the lsb bit

                        if (ac)
                        {
                            A = (A >> 1) | F_SIGN;
                        }
                        else
                        {
                            A = A >> 1;
                        }

                        SetF3((A & F_3) != 0);
                        SetF5((A & F_5) != 0);
                        SetCarry(ac);
                        SetHalf(false);
                        SetNeg(false);
                        break;

                    case 0x17:  //RLA
                        if (loggingEnabled) Log("RLA");
                        tstates += 4;
                        ac = ((A & F_SIGN) != 0);

                        int msb = F & F_CARRY; 

                        if (msb != 0)
                        {
                            A = ((A << 1) | F_CARRY) ;
                        }
                        else
                        {
                            A = (A << 1) ;
                        }
                        
                        SetCarry(ac);
                        SetHalf(false);
                        SetNeg(false);
                        break;

                    case 0x1F:  //RRA
                        if (loggingEnabled) Log("RRA");
                        tstates += 4;
                        ac = (A & F_CARRY) != 0; //save the lsb bit
                        int lsb = F & F_CARRY; 

                        if (lsb != 0)
                        {
                            A = (A >> 1) | F_SIGN;
                        }
                        else
                        {
                            A = A >> 1;
                        }

                        SetCarry(ac);
                        SetHalf(false);
                        SetNeg(false);
                        break;
                    #endregion

                    #region Exchange operations (EX)
                    /** Exchange operations **/
                    case 0x08:     //EX AF, AF'
                        if (loggingEnabled) Log("EX AF, AF'");
                        tstates += 4;
                        ex_af_af();
                        break;

                    case 0xD9:   //EXX
                        if (loggingEnabled) Log("EXX");
                        tstates += 4;
                        exx();
                        break;

                    case 0xE3:  //EX (SP), HL
                        if (loggingEnabled) Log("EX (SP), HL");
                        tstates += 19;
                        int temp = HL;
                        addr = PeekWord(SP);
                        HL = addr;
                        PokeWord(SP, temp);
                        break;

                    case 0xEB:  //EX DE, HL
                        if (loggingEnabled) Log("EX DE, HL");
                        tstates += 4;
                        temp = DE;
                        DE = HL;
                        HL = temp;
                        break;
                    #endregion

                    #region 16 bit addition to HL (Add HL, rr)
                    /** Add HL, rr **/
                    case 0x09:     //ADD HL, BC
                        if (loggingEnabled) Log("ADD HL, BC");
                        tstates += 11;
                        HL = Add_RR(HL,BC);
                        break;

                    case 0x19:    //ADD HL, DE
                        if (loggingEnabled) Log("ADD HL, DE");
                        tstates += 11;
                        HL = Add_RR(HL, DE);
                        break;
                    
                    case 0x29:  //ADD HL, HL
                        if (loggingEnabled) Log("ADD HL, HL");
                        tstates += 11;
                        HL = Add_RR(HL, HL);
                        break;

                    case 0x39:  //ADD HL, SP
                        if (loggingEnabled) Log("ADD HL, SP");
                        tstates += 11;
                        HL = Add_RR(HL, SP);
                        break;
                    #endregion

                    #region 8 bit addition to accumulator (Add r, r)
                    /*** ADD r, r ***/
                    case 0x80:  //ADD A,B
                        if (loggingEnabled) Log("ADD A, B");
                        tstates += 4;
                        Add_R(B);
                        break;

                    case 0x81:  //ADD A,C
                        if (loggingEnabled) Log("ADD A, C");
                        tstates += 4;
                        Add_R(C);
                        break;
                    
                    case 0x82:  //ADD A,D
                        if (loggingEnabled) Log("ADD A, D");
                        tstates += 4;
                        Add_R(D);
                        break;

                    case 0x83:  //ADD A,E
                        if (loggingEnabled) Log("ADD A, E");
                        tstates += 4;
                        Add_R(E);
                        break;

                    case 0x84:  //ADD A,H
                        if (loggingEnabled) Log("ADD A, H");
                        tstates += 4;
                        Add_R(H);
                        break;

                    case 0x85:  //ADD A,L
                        if (loggingEnabled) Log("ADD A, L");
                        tstates += 4;
                        Add_R(L);
                        break;

                    case 0x86:  //ADD A, (HL)
                        if (loggingEnabled) Log("ADD A, (HL)");
                        tstates += 7;
                        Add_R(PeekByte(HL));
                        break;

                    case 0x87:  //ADD A, A
                        if (loggingEnabled) Log("ADD A, A");
                        tstates += 4;
                        Add_R(A);
                        break;

                    case 0xC6:  //ADD A, n
                        if (loggingEnabled) Log(String.Format("ADD A, {0,-6:X}", PeekByte(PC)));
                        tstates += 7;
                        Add_R(PeekByte(PC));
                        PC++;
                        break;
                    #endregion

                    #region Add to accumulator with carry (Adc A, r)
                    /** Adc a, r **/
                    case 0x88:  //ADC A,B
                        if (loggingEnabled) Log("ADC A, B");
                        tstates += 4;
                        Adc_R(B);
                        break;

                    case 0x89:  //ADC A,C
                        if (loggingEnabled) Log("ADC A, C");
                        tstates += 4;
                        Adc_R(C);
                        break;

                    case 0x8A:  //ADC A,D
                        if (loggingEnabled) Log("ADC A, D");
                        tstates += 4;
                        Adc_R(D);
                        break;

                    case 0x8B:  //ADC A,E
                        if (loggingEnabled) Log("ADC A, E");
                        tstates += 4;
                        Adc_R(E);
                        break;

                    case 0x8C:  //ADC A,H
                        if (loggingEnabled) Log("ADC A, H");
                        tstates += 4;
                        Adc_R(H);
                        break;

                    case 0x8D:  //ADC A,L
                        if (loggingEnabled) Log("ADC A, L");
                        tstates += 4;
                        Adc_R(L);
                        break;

                    case 0x8E:  //ADC A,(HL)
                        if (loggingEnabled) Log("ADC A, (HL)");
                        tstates += 7;
                        Adc_R(PeekByte(HL));
                        break;

                    case 0x8F:  //ADC A,A
                        if (loggingEnabled) Log("ADC A, A");
                        tstates += 4;
                        Adc_R(A);
                        break;

                    case 0xCE:  //ADC A, n
                        if (loggingEnabled) Log(String.Format("ADC A, {0,-6:X}", PeekByte(PC)));
                        tstates += 7;
                        Adc_R(PeekByte(PC));
                        PC += 1;
                        break;
                    #endregion

                    #region 8 bit subtraction from accumulator(SUB r)
                    case 0x90:  //SUB B
                        if (loggingEnabled) Log("SUB B");
                        tstates += 4;
                        Sub_R(B);
                        break;

                    case 0x91:  //SUB C
                        if (loggingEnabled) Log("SUB C");
                        tstates += 4;
                        Sub_R(C);
                        break;

                    case 0x92:  //SUB D
                        if (loggingEnabled) Log("SUB D");
                        tstates += 4;
                        Sub_R(D);
                        break;

                    case 0x93:  //SUB E
                        if (loggingEnabled) Log("SUB E");
                        tstates += 4;
                        Sub_R(E);
                        break;

                    case 0x94:  //SUB H
                        if (loggingEnabled) Log("SUB H");
                        tstates += 4;
                        Sub_R(H);
                        break;

                    case 0x95:  //SUB L
                        if (loggingEnabled) Log("SUB L");
                        tstates += 4;
                        Sub_R(L);
                        break;

                    case 0x96:  //SUB (HL)
                        if (loggingEnabled) Log("SUB (HL)");
                        tstates += 7;
                        Sub_R(PeekByte(HL));
                        break;

                    case 0x97:  //SUB A
                        if (loggingEnabled) Log("SUB A");
                        tstates += 4;
                        Sub_R(A);
                        break;

                    case 0xD6:  //SUB n
                        if (loggingEnabled) Log(String.Format("SUB {0,-6:X}", PeekByte(PC)));
                        tstates += 7;
                        Sub_R(PeekByte(PC));
                        PC += 1;
                        break;
                    #endregion

                    #region 8 bit subtraction from accumulator with carry(SBC A, r)
                    case 0x98:  //SBC A, B
                        if (loggingEnabled) Log("SBC A, B");
                        tstates += 4;
                        Sbc_R(B);
                        break;

                    case 0x99:  //SBC A, C
                        if (loggingEnabled) Log("SBC A, C");
                        tstates += 4;
                        Sbc_R(C);
                        break;

                    case 0x9A:  //SBC A, D
                        if (loggingEnabled) Log("SBC A, D");
                        tstates += 4;
                        Sbc_R(D);
                        break;

                    case 0x9B:  //SBC A, E
                        if (loggingEnabled) Log("SBC A, E");
                        tstates += 4;
                        Sbc_R(E);
                        break;

                    case 0x9C:  //SBC A, H
                        if (loggingEnabled) Log("SBC A, H");
                        tstates += 4;
                        Sbc_R(H);
                        break;

                    case 0x9D:  //SBC A, L
                        if (loggingEnabled) Log("SBC A, L");
                        tstates += 4;
                        Sbc_R(L);
                        break;

                    case 0x9E:  //SBC A, (HL)
                        if (loggingEnabled) Log("SBC A, (HL)");
                        tstates += 4;
                        Sbc_R(PeekByte(HL));
                        break;

                    case 0x9F:  //SBC A, A
                        if (loggingEnabled) Log("SBC A, A");
                        tstates += 4;
                        Sbc_R(A);
                        break;

                    case 0xDE:  //SBC A, n
                        if (loggingEnabled) Log(String.Format("SBC A, {0,-6:X}", PeekByte(PC)));
                        tstates += 7;
                        Sbc_R(PeekByte(PC));
                        PC += 1;
                        break;
                    #endregion

                    #region Relative Jumps (JR / DJNZ)
                    /*** Relative Jumps ***/
                    case 0x10:  //DJNZ n
                        disp = GetDisplacement(PeekByte(PC));
                        if (loggingEnabled) Log(String.Format("DJNZ {0,-6:X}", disp));
                        B--;
                        if (B != 0)
                        {
                            
                            PC += disp + 1; 
                            tstates += 13;
                        }
                        else
                        {
                            tstates += 8;
                            PC += 1;
                        }
                        break;

                    case 0x18:  //JR n
                        disp = GetDisplacement(PeekByte(PC));
                        if (loggingEnabled) Log(String.Format("JR {0,-6:X}", disp));
                        tstates += 12;
                        
                        PC += disp + 1;
                        break;
                    
                    case 0x20:  //JRNZ n
                        disp = GetDisplacement(PeekByte(PC));
                        if (loggingEnabled) Log(String.Format("JR NZ, {0,-6:X}", disp));
                        if ((F & F_ZERO) == 0)
                        {
                            tstates += 12;
                            
                            PC += disp + 1;
                        }
                        else
                        {
                            tstates += 7;
                            PC++;
                        }
                        break;

                    case 0x28:  //JRZ n
                        disp = GetDisplacement(PeekByte(PC));
                        if (loggingEnabled) Log(String.Format("JR Z, {0,-6:X}", disp));
                        if ((F & F_ZERO) != 0)
                        {
                            tstates += 12;
                            
                            PC += disp + 1;
                        }
                        else
                        {
                            tstates += 7;
                            PC++;
                        }
                        break;

                    case 0x30:  //JRNC n
                        disp = GetDisplacement(PeekByte(PC));
                        if (loggingEnabled) Log(String.Format("JR NC, {0,-6:X}", disp));
                        if ((F & F_CARRY) == 0)
                        {
                            tstates += 12;
                            
                            PC += disp + 1;
                        }
                        else
                        {
                            tstates += 7;
                            PC++;
                        }
                        break;

                    case 0x38:  //JRC n
                        disp = GetDisplacement(PeekByte(PC));
                        if (loggingEnabled) Log(String.Format("JR C, {0,-6:X}", disp));
                        if ((F & F_CARRY) != 0)
                        {
                            tstates += 12;
                            
                            PC += disp + 1;
                        }
                        else
                        {
                            tstates += 7;
                            PC++;
                        }
                        break;
                    #endregion

                    #region Direct jumps (JP)
                    /*** Direct jumps ***/
                    case 0xC2:  //JPNZ nn
                        if (loggingEnabled) Log(String.Format("JP NZ, {0,-6:X}", PeekWord(PC)));
                        tstates += 10;
                        if ((F & F_ZERO) == 0)
                        {
                           PC = PeekWord(PC);
                            
                        }
                        else
                        {
                            PC += 2;
                        }
                        break;

                    case 0xC3:  //JP nn
                        if (loggingEnabled) Log(String.Format("JP {0,-6:X}", PeekWord(PC)));
                        tstates += 10;
                        PC = PeekWord(PC);
                        break;

                    case 0xCA:  //JPZ nn
                        if (loggingEnabled) Log(String.Format("JP Z, {0,-6:X}", PeekWord(PC)));
                        tstates += 10;
                        if ((F & F_ZERO) != 0)
                        {
                            PC = PeekWord(PC);
                        }
                        else
                        {
                            PC += 2;
                        }
                        break;

                    case 0xD2:  //JPNC nn
                        if (loggingEnabled) Log(String.Format("JP NC, {0,-6:X}", PeekWord(PC)));
                        tstates += 10;
                        if ((F & F_CARRY) == 0)
                        {

                            PC = PeekWord(PC);
                        }
                        else
                        {
                            PC += 2;
                        }
                        break;

                    case 0xDA:  //JPC nn
                        if (loggingEnabled) Log(String.Format("JP C, {0,-6:X}", PeekWord(PC)));
                        tstates += 10;
                        if ((F & F_CARRY) != 0)
                        {

                            PC = PeekWord(PC);
                        }
                        else
                        {
                            PC += 2;
                        }
                        break;

                    case 0xE2:  //JP PO nn
                        if (loggingEnabled) Log(String.Format("JP PO, {0,-6:X}", PeekWord(PC)));
                        tstates += 10;
                        if ((F & F_PARITY) == 0)
                        {
                            PC = PeekWord(PC);
                        }
                        else
                        {
                            PC += 2;
                        }
                        break;

                    case 0xE9:  //JP (HL)
                        if (loggingEnabled) Log("JP (HL)");
                        tstates += 4;
                        //PC = PeekWord(HL);
                        PC = HL;
                        break;

                    case 0xEA:  //JP PE nn
                        if (loggingEnabled) Log(String.Format("JP PE, {0,-6:X}", PeekWord(PC)));
                        tstates += 10;
                        if ((F & F_PARITY) != 0)
                        {
                            PC = PeekWord(PC);
                        }
                        else
                        {
                            PC += 2;
                        }
                        break;

                    case 0xF2:  //JP P nn
                        if (loggingEnabled) Log(String.Format("JP P, {0,-6:X}", PeekWord(PC)));
                        tstates += 10;
                        if ((F & F_SIGN) == 0)
                        {
                            PC = PeekWord(PC);
                        }
                        else
                        {
                            PC += 2;
                        }
                        break;

                    case 0xFA:  //JP M nn
                        if (loggingEnabled) Log(String.Format("JP M, {0,-6:X}", PeekWord(PC)));
                        tstates += 10;
                        if ((F & F_SIGN) != 0)
                        {
                            PC = PeekWord(PC);
                        }
                        else
                        {
                            PC += 2;
                        }
                        break;
                    #endregion

                    #region Compare instructions (CP)
                    /*** Compare instructions **/
                    case 0xB8:  //CP B
                        if (loggingEnabled) Log("CP B");
                        tstates += 4;
                        Cp_R(B);
                        break;

                    case 0xB9:  //CP C
                        if (loggingEnabled) Log("CP C");
                        tstates += 4;
                        Cp_R(C);
                        break;

                    case 0xBA:  //CP D
                        if (loggingEnabled) Log("CP D");
                        tstates += 4;
                        Cp_R(D);
                        break;

                    case 0xBB:  //CP E
                        if (loggingEnabled) Log("CP E");
                        tstates += 4;
                        Cp_R(E);
                        break;

                    case 0xBC:  //CP H
                        if (loggingEnabled) Log("CP H");
                        tstates += 4;
                        Cp_R(H);
                        break;

                    case 0xBD:  //CP L
                        if (loggingEnabled) Log("CP L");
                        tstates += 4;
                        Cp_R(L);
                        break;

                    case 0xBE:  //CP (HL)
                        if (loggingEnabled) Log("CP (HL)");
                        tstates += 7;
                        val = PeekByte(HL);
                        Cp_R(val);
                        break;

                    case 0xBF:  //CP A
                        if (loggingEnabled) Log("CP A");
                        tstates += 4;
                        Cp_R(A);
                        break;

                    case 0xFE:  //CP n
                        if (loggingEnabled) Log(String.Format(String.Format("CP {0,-6:X}", PeekByte(PC))));
                        tstates += 7;
                        Cp_R(PeekByte(PC));
                        PC += 1;
                        break;
                    #endregion

                    #region Carry Flag operations
                    /*** Carry Flag operations ***/
                    case 0x37:  //SCF
                        if (loggingEnabled) Log("SCF");
                        tstates += 4;
                        SetCarry(true);

                        SetF3((A & F_3) != 0);
                        SetF5((A & F_5) != 0);
                        break;

                    case 0x3F:  //CCF
                        if (loggingEnabled) Log("CCF");
                        tstates += 4;

                        SetF3((A & F_3) != 0);
                        SetF5((A & F_5) != 0);
                       // SetHalf((F & F_CARRY) != 0);

                        SetCarry(((F & F_CARRY) != 0) ? false: true);

                        break;
                    #endregion

                    #region Bitwise AND (AND r)
                    case 0xA0:  //AND B
                        if (loggingEnabled) Log("AND B");
                        tstates += 4;
                        And_R(B);
                        break;

                    case 0xA1:  //AND C
                        if (loggingEnabled) Log("AND C");
                        tstates += 4;
                        And_R(C);
                        break;

                    case 0xA2:  //AND D
                        if (loggingEnabled) Log("AND D");
                        tstates += 4;
                        And_R(D);
                        break;

                    case 0xA3:  //AND E
                        if (loggingEnabled) Log("AND E");
                        tstates += 4;
                        And_R(E);
                        break;

                    case 0xA4:  //AND H
                        if (loggingEnabled) Log("AND H");
                        tstates += 4;
                        And_R(H);
                        break;

                    case 0xA5:  //AND L
                        if (loggingEnabled) Log("AND L");
                        tstates += 4;
                        And_R(L);
                        break;

                    case 0xA6:  //AND (HL)
                        if (loggingEnabled) Log("AND (HL)");
                        tstates += 7;
                        And_R(PeekByte(HL));
                        break;

                    case 0xA7:  //AND A
                        if (loggingEnabled) Log("AND A");
                        tstates += 4;
                        And_R(A);
                        break;

                    case 0xE6:  //AND n
                        if (loggingEnabled) Log(String.Format("AND {0,-6:X}", PeekByte(PC)));
                        tstates += 7;
                        And_R(PeekByte(PC));
                        PC++;
                        break;
                    #endregion

                    #region Bitwise XOR (XOR r)
                    case 0xA8: //XOR B
                        if (loggingEnabled) Log("XOR B");
                        tstates += 4;
                        Xor_R(B);
                        break;

                    case 0xA9: //XOR C
                        if (loggingEnabled) Log("XOR C");
                        tstates += 4;
                        Xor_R(C);
                        break;

                    case 0xAA: //XOR D
                        if (loggingEnabled) Log("XOR D");
                        tstates += 4;
                        Xor_R(D);
                        break;

                    case 0xAB: //XOR E
                        if (loggingEnabled) Log("XOR E");
                        tstates += 4;
                        Xor_R(E);
                        break;

                    case 0xAC: //XOR H
                        if (loggingEnabled) Log("XOR H");
                        tstates += 4;
                        Xor_R(H);
                        break;

                    case 0xAD: //XOR L
                        if (loggingEnabled) Log("XOR L");
                        tstates += 4;
                        Xor_R(L);
                        break;

                    case 0xAE: //XOR (HL)
                        if (loggingEnabled) Log("XOR (HL)");
                        tstates += 7;
                        Xor_R(PeekByte(HL));
                        break;

                    case 0xAF: //XOR A
                        if (loggingEnabled) Log("XOR A");
                        tstates += 4;
                        Xor_R(A);
                        break;

                    case 0xEE:  //XOR n
                        if (loggingEnabled) Log(String.Format("XOR {0,-6:X}", PeekByte(PC)));
                        tstates += 7;
                        Xor_R(PeekByte(PC));
                        PC++;
                        break;

                    #endregion

                    #region Bitwise OR (OR r)
                    case 0xB0:  //OR B
                        if (loggingEnabled) Log("OR B");
                        tstates += 4;
                        Or_R(B);
                        break;

                    case 0xB1:  //OR C
                        if (loggingEnabled) Log("OR C");
                        tstates += 4;
                        Or_R(C);
                        break;

                    case 0xB2:  //OR D
                        if (loggingEnabled) Log("OR D");
                        tstates += 4;
                        Or_R(D);
                        break;

                    case 0xB3:  //OR E
                        if (loggingEnabled) Log("OR E");
                        tstates += 4;
                        Or_R(E);
                        break;

                    case 0xB4:  //OR H
                        if (loggingEnabled) Log("OR H");
                        tstates += 4;
                        Or_R(H);
                        break;

                    case 0xB5:  //OR L
                        if (loggingEnabled) Log("OR L");
                        tstates += 4;
                        Or_R(L);
                        break;

                    case 0xB6:  //OR (HL)
                        if (loggingEnabled) Log("OR (HL)");
                        tstates += 7;
                        Or_R(PeekByte(HL));
                        break;

                    case 0xB7:  //OR A
                        if (loggingEnabled) Log("OR A");
                        tstates += 4;
                        Or_R(A);
                        break;

                    case 0xF6:  //OR n
                        if (loggingEnabled) Log(String.Format("OR {0,-6:X}", PeekByte(PC)));
                        tstates += 7;
                        Or_R(PeekByte(PC));
                        PC++;
                        break;
                    #endregion

                    #region Return instructions
                    case 0xC0:  //RET NZ
                        if (loggingEnabled) Log("RET NZ");
                        if ((F & F_ZERO) == 0)
                        {
                            tstates += 11;
                            PC = PeekWord(SP);
                            SP += 2;
                        }
                        else
                        {
                            tstates += 5;
                        }
                        break;

                    case 0xC8:  //RET Z
                        if (loggingEnabled) Log("RET Z");
                        if ((F & F_ZERO) != 0)
                        {
                            tstates += 11;
                            PC = PeekWord(SP);
                            SP += 2;
                        }
                        else
                        {
                            tstates += 5;
                        }
                        break;

                    case 0xC9:  //RET
                        if (loggingEnabled) Log("RET");
                        tstates += 10;
                        PC = PeekWord(SP);
                        SP += 2;
                        break;

                    case 0xD0:  //RET NC
                        if (loggingEnabled) Log("RET NC");
                        if ((F & F_CARRY) == 0)
                        {
                            tstates += 11;
                            PC = PeekWord(SP);
                            SP += 2;
                        }
                        else
                        {
                            tstates += 5;
                        }
                        break;

                    case 0xD8:  //RET C
                        if (loggingEnabled) Log("RET C");
                        if ((F & F_CARRY) != 0)
                        {
                            tstates += 11;
                            PC = PeekWord(SP);
                            SP += 2;
                        }
                        else
                        {
                            tstates += 5;
                        }
                        break;

                    case 0xE0:  //RET PO
                        if (loggingEnabled) Log("RET PO");
                        if ((F & F_PARITY) == 0)
                        {
                            tstates += 11;
                            PC = PeekWord(SP);
                            SP += 2;
                        }
                        else
                        {
                            tstates += 5;
                        }
                        break;

                    case 0xE8:  //RET PE
                        if (loggingEnabled) Log("RET PE");
                        if ((F & F_PARITY) != 0)
                        {
                            tstates += 11;
                            PC = PeekWord(SP);
                            SP += 2;
                        }
                        else
                        {
                            tstates += 5;
                        }
                        break;

                    case 0xF0:  //RET P
                        if (loggingEnabled) Log("RET P");
                        if ((F & F_SIGN) == 0)
                        {
                            tstates += 11;
                            PC = PeekWord(SP);
                            SP += 2;
                        }
                        else
                        {
                            tstates += 5;
                        }
                        break;

                    case 0xF8:  //RET M
                        if (loggingEnabled) Log("RET M");
                        if ((F & F_SIGN) != 0)
                        {
                            tstates += 11;
                            PC = PeekWord(SP);
                            SP += 2;
                        }
                        else
                        {
                            tstates += 5;
                        }
                        break;
                    #endregion

                    #region POP/PUSH instructions (Fix these for SP overflow later!)
                    case 0xC1:  //POP BC
                        if (loggingEnabled) Log("POP BC");
                        tstates += 10;
                        BC = PeekWord(SP);
                        SP = (SP + 2) &0xffff;
                        break;

                    case 0xC5:  //PUSH BC
                        if (loggingEnabled) Log("PUSH BC");
                        tstates += 11;
                        SP = (SP - 2) & 0xffff;
                        PokeWord(SP, BC);
                        break;

                    case 0xD1:  //POP DE
                        if (loggingEnabled) Log("POP DE");
                        tstates += 10;
                        DE = PeekWord(SP);
                        SP = (SP + 2) & 0xffff;
                        break;

                    case 0xD5:  //PUSH DE
                        if (loggingEnabled) Log("PUSH DE");
                        tstates += 11;
                        SP = (SP - 2) & 0xffff;
                        PokeWord(SP, DE); 
                        break;

                    case 0xE1:  //POP HL
                        if (loggingEnabled) Log("POP HL");
                        tstates += 10;
                        HL = PeekWord(SP);
                        SP = (SP + 2) & 0xffff;
                        break;

                    case 0xE5:  //PUSH HL
                        if (loggingEnabled) Log("PUSH HL");
                        tstates += 11;
                        SP = (SP - 2) & 0xffff;
                        PokeWord(SP, HL);
                        break;

                    case 0xF1:  //POP AF
                        if (loggingEnabled) Log("POP AF");
                        tstates += 10;
                        AF = PeekWord(SP);
                        SP = (SP + 2) & 0xffff;
                        break;

                    case 0xF5:  //PUSH AF
                        if (loggingEnabled) Log("PUSH AF");
                        tstates += 11;
                        SP = (SP - 2) & 0xffff;
                        PokeWord(SP, AF);
                        break;
                    #endregion

                    #region CALL instructions
                    case 0xC4:  //CALL NZ, nn
                        if (loggingEnabled) Log(String.Format("CALL NZ, {0,-6:X}", PeekWord(PC)));
                        if ((F & F_ZERO) == 0)
                        {
                            tstates += 17;
                            SP -= 2;
                            PokeWord(SP, PC+2);
                            PC = PeekWord(PC);
                        }
                        else
                        {
                            tstates += 10;
                            PC += 2;
                        }
                        break;

                    case 0xCC:  //CALL Z, nn
                        if (loggingEnabled) Log(String.Format("CALL Z, {0,-6:X}", PeekWord(PC)));
                        if ((F & F_ZERO) != 0)
                        {
                            tstates += 17;
                            SP -= 2;
                            PokeWord(SP, PC+2);
                            PC = PeekWord(PC);
                        }
                        else
                        {
                            tstates += 10;
                            PC += 2;
                        }
                        break;

                    case 0xCD:  //CALL nn
                        if (loggingEnabled) Log(String.Format("CALL {0,-6:X}", PeekWord(PC)));
                        tstates += 17;
                        SP -= 2;
                        PokeWord(SP, PC+2);
                        PC = PeekWord(PC);
                        break;

                    case 0xD4:  //CALL NC, nn
                        if (loggingEnabled) Log(String.Format("CALL NC, {0,-6:X}", PeekWord(PC)));
                        if ((F & F_CARRY) == 0)
                        {
                            tstates += 17;
                            SP -= 2;
                            PokeWord(SP, PC+2);
                            PC = PeekWord(PC);
                        }
                        else
                        {
                            tstates += 10;
                            PC += 2;
                        }
                        break;

                    case 0xDC:  //CALL C, nn
                        if (loggingEnabled) Log(String.Format("CALL C, {0,-6:X}", PeekWord(PC)));
                        if ((F & F_CARRY) != 0)
                        {
                            tstates += 17;
                            SP -= 2;
                            PokeWord(SP, PC+2);
                            PC = PeekWord(PC);
                        }
                        else
                        {
                            tstates += 10;
                            PC += 2;
                        }
                        break;


                    case 0xE4:  //CALL PO, nn
                        if (loggingEnabled) Log(String.Format("CALL PO, {0,-6:X}", PeekWord(PC)));
                        if ((F & F_PARITY) == 0)
                        {
                            tstates += 17;
                            SP -= 2;
                            PokeWord(SP, PC+2);
                            PC = PeekWord(PC);
                        }
                        else
                        {
                            tstates += 10;
                            PC += 2;
                        }
                        break;

                    case 0xEC:  //CALL PE, nn
                        if (loggingEnabled) Log(String.Format("CALL PE, {0,-6:X}", PeekWord(PC)));
                        if ((F & F_PARITY) != 0)
                        {
                            tstates += 17;
                            SP -= 2;
                            PokeWord(SP, PC+2);
                            PC = PeekWord(PC);
                        }
                        else
                        {
                            tstates += 10;
                            PC += 2;
                        }
                        break;

                    case 0xF4:  //CALL P, nn
                        if (loggingEnabled) Log(String.Format("CALL P, {0,-6:X}", PeekWord(PC)));
                        if ((F & F_SIGN) == 0)
                        {
                            tstates += 17;
                            SP -= 2;
                            PokeWord(SP, PC+2);
                            PC = PeekWord(PC);
                        }
                        else
                        {
                            tstates += 10;
                            PC += 2;
                        }
                        break;

                    case 0xFC:  //CALL M, nn
                        if (loggingEnabled) Log(String.Format("CALL M, {0,-6:X}", PeekWord(PC)));
                        if ((F & F_SIGN) != 0)
                        {
                            tstates += 17;
                            SP -= 2;
                            PokeWord(SP, PC+2);
                            PC = PeekWord(PC);
                        }
                        else
                        {
                            tstates += 10;
                            PC += 2;
                        }
                        break;
                    #endregion

                    #region Restart instructions (RST n)
                    case 0xC7:  //RST 0x00
                        if (loggingEnabled) Log("RST 00");
                        tstates += 11;
                        SP -= 2;
                        PokeWord(SP, PC);
                        PC = 0x00;
                        break;

                    case 0xCF:  //RST 0x08
                        if (loggingEnabled) Log("RST 08");
                        tstates += 11;
                        SP -= 2;
                        PokeWord(SP, PC);
                        PC = 0x08;
                        break;

                    case 0xD7:  //RST 0x10
                        if (loggingEnabled) Log("RST 10");
                        tstates += 11;
                        SP -= 2;
                        PokeWord(SP, PC);
                        PC = 0x10;
                        break;

                    case 0xDF:  //RST 0x18
                        if (loggingEnabled) Log("RST 18");
                        tstates += 11;
                        SP -= 2;
                        PokeWord(SP, PC);
                        PC = 0x18;
                        break;

                    case 0xE7:  //RST 0x20
                        if (loggingEnabled) Log("RST 20");
                        tstates += 11;
                        SP -= 2;
                        PokeWord(SP, PC);
                        PC = 0x20;
                        break;

                    case 0xEF:  //RST 0x28
                        if (loggingEnabled) Log("RST 28");
                        tstates += 11;
                        SP -= 2;
                        PokeWord(SP, PC);
                        PC = 0x28;
                        break;

                    case 0xF7:  //RST 0x30
                        if (loggingEnabled) Log("RST 30");
                        tstates += 11;
                        SP -= 2;
                        PokeWord(SP, PC);
                        PC = 0x30;
                        break;

                    case 0xFF:  //RST 0x38
                        if (loggingEnabled) Log("RST 38");
                        tstates += 11;
                        SP -= 2;
                        PokeWord(SP, PC);
                        PC = 0x38;
                        break;
                    #endregion

                    #region IN instructions
                    case 0xDB:  //IN A, (n)
                        if (loggingEnabled) Log(String.Format("IN A, ({0:X})", PeekByte(PC)));
                        tstates += 11;
                        A = In((A << 8)  | PeekByte(PC));
                        PC++;
                        break;
                    #endregion

                    #region OUT instructions
                    case 0xD3:  //OUT (n), A
                        if (loggingEnabled) Log(String.Format("OUT ({0:X}), A", PeekByte(PC)));
                        tstates += 11;
                        Out(PeekByte(PC), A);
                        PC++;
                        break;
                    #endregion

                    #region Decimal Adjust Accumulator (DAA)
                    case 0x27:  //DAA
                        if (loggingEnabled) Log("DAA");
                        tstates += 4;
                        DAA();
                        break;
                    #endregion

                    #region Complement (CPL)
                    case 0x2f:  //CPL
                        if (loggingEnabled) Log("CPL");
                        tstates += 4;
                        A = A ^ 0xff;
                        SetF3((A & F_3) != 0);
                        SetF5((A & F_5) != 0);
                        SetNeg(true);
                        SetHalf(true);
                        break;
                    #endregion

                    #region Halt (HALT) - TO BE CHECKED!
                    case 0x76:  //HALT
                        if (loggingEnabled) Log("HALT");
                            HaltOn = true;
                            tstates += 4;
                            PC--;
                        break;
                    #endregion

                    #region Interrupts
                    case 0xF3:  //DI
                        if (loggingEnabled) Log("DI");
                        tstates += 4;
                        IFF1 = false;
                        IFF2 = false;
                        break;

                    case 0xFB:  //EI
                        if (loggingEnabled) Log("EI");
                        tstates += 4;
                        IFF1 = true;
                        IFF2 = true;
                        break;
                    #endregion

                    #region Opcodes with CB prefix
                    case 0xCB:
                        switch (opcode = FetchInstruction())
                        {
                            #region Rotate instructions
                            case 0x00: //RLC B
                                if (loggingEnabled) Log("RLC B");
                                tstates += 8;
                                B = Rlc_R(B);
                                break;

                            case 0x01: //RLC C
                                if (loggingEnabled) Log("RLC C");
                                tstates += 8;
                                C = Rlc_R(C);
                                break;

                            case 0x02: //RLC D
                                if (loggingEnabled) Log("RLC D");
                                tstates += 8;
                                D = Rlc_R(D);
                                break;

                            case 0x03: //RLC E
                                if (loggingEnabled) Log("RLC E");
                                tstates += 8;
                                E = Rlc_R(E);
                                break;

                            case 0x04: //RLC H
                                if (loggingEnabled) Log("RLC H");
                                tstates += 8;
                                H = Rlc_R(H);
                                break;

                            case 0x05: //RLC L
                                if (loggingEnabled) Log("RLC L");
                                tstates += 8;
                                L = Rlc_R(L);
                                break;

                            case 0x06: //RLC (HL)
                                if (loggingEnabled) Log("RLC (HL)");
                                tstates += 15;
                                PokeByte(HL, Rlc_R(PeekByte(HL)));
                                break;

                            case 0x07: //RLC A
                                if (loggingEnabled) Log("RLC A");
                                tstates += 8;
                                A = Rlc_R(A);
                                break;

                            case 0x08: //RRC B
                                if (loggingEnabled) Log("RRC B");
                                tstates += 8;
                                B = Rrc_R(B);
                                break;

                            case 0x09: //RRC C
                                if (loggingEnabled) Log("RRC C");
                                tstates += 8;
                                C = Rrc_R(C);
                                break;

                            case 0x0A: //RRC D
                                if (loggingEnabled) Log("RRC D");
                                tstates += 8;
                                D = Rrc_R(D);
                                break;

                            case 0x0B: //RRC E
                                if (loggingEnabled) Log("RRC E");
                                tstates += 8;
                                E = Rrc_R(E);
                                break;

                            case 0x0C: //RRC H
                                if (loggingEnabled) Log("RRC H");
                                tstates += 8;
                                H = Rrc_R(H);
                                break;

                            case 0x0D: //RRC L
                                if (loggingEnabled) Log("RRC L");
                                tstates += 8;
                                L = Rrc_R(L);
                                break;

                            case 0x0E: //RRC (HL)
                                if (loggingEnabled) Log("RRC (HL)");
                                tstates += 15;
                                PokeByte(HL, Rrc_R(PeekByte(HL)));
                                break;

                            case 0x0F: //RRC A
                                if (loggingEnabled) Log("RRC A");
                                tstates += 8;
                                A = Rrc_R(A);
                                break;

                            case 0x10: //RL B
                                if (loggingEnabled) Log("RL B");
                                tstates += 8;
                                B = Rl_R(B);
                                break;

                            case 0x11: //RL C
                                if (loggingEnabled) Log("RL C");
                                tstates += 8;
                                C = Rl_R(C);
                                break;

                            case 0x12: //RL D
                                if (loggingEnabled) Log("RL D");
                                tstates += 8;
                                D = Rl_R(D);
                                break;

                            case 0x13: //RL E
                                if (loggingEnabled) Log("RL E");
                                tstates += 8;
                                E = Rl_R(E);
                                break;

                            case 0x14: //RL H
                                if (loggingEnabled) Log("RL H");
                                tstates += 8;
                                H = Rl_R(H);
                                break;

                            case 0x15: //RL L
                                if (loggingEnabled) Log("RL L");
                                tstates += 8;
                                L = Rl_R(L);
                                break;

                            case 0x16: //RL (HL)
                                if (loggingEnabled) Log("RL (HL)");
                                tstates += 15;
                                PokeByte(HL, Rl_R(PeekByte(HL)));
                                break;

                            case 0x17: //RL A
                                if (loggingEnabled) Log("RL A");
                                tstates += 8;
                                A = Rl_R(A);
                                break;

                            case 0x18: //RR B
                                if (loggingEnabled) Log("RR B");
                                tstates += 8;
                                B = Rr_R(B);
                                break;

                            case 0x19: //RR C
                                if (loggingEnabled) Log("RR C");
                                tstates += 8;
                                C = Rr_R(C);
                                break;

                            case 0x1A: //RR D
                                if (loggingEnabled) Log("RR D");
                                tstates += 8;
                                D = Rr_R(D);
                                break;

                            case 0x1B: //RR E
                                if (loggingEnabled) Log("RR E");
                                tstates += 8;
                                E = Rr_R(E);
                                break;

                            case 0x1C: //RR H
                                if (loggingEnabled) Log("RR H");
                                tstates += 8;
                                H = Rr_R(H);
                                break;

                            case 0x1D: //RR L
                                if (loggingEnabled) Log("RR L");
                                tstates += 8;
                                L = Rr_R(L);
                                break;

                            case 0x1E: //RR (HL)
                                if (loggingEnabled) Log("RR (HL)");
                                tstates += 15;
                                PokeByte(HL, Rr_R(PeekByte(HL)));
                                break;

                            case 0x1F: //RR A
                                if (loggingEnabled) Log("RR A");
                                tstates += 8;
                                A = Rr_R(A);
                                break;
                            #endregion

                            #region Register shifts
                            case 0x20:  //SLA B
                                if (loggingEnabled) Log("SLA B");
                                tstates += 8;
                                B = Sla_R(B);
                                break;

                            case 0x21:  //SLA C
                                if (loggingEnabled) Log("SLA C");
                                tstates += 8;
                                C = Sla_R(C);
                                break;

                            case 0x22:  //SLA D
                                if (loggingEnabled) Log("SLA D");
                                tstates += 8;
                                D = Sla_R(D);
                                break;

                            case 0x23:  //SLA E
                                if (loggingEnabled) Log("SLA E");
                                tstates += 8;
                                E = Sla_R(E);
                                break;

                            case 0x24:  //SLA H
                                if (loggingEnabled) Log("SLA H");
                                tstates += 8;
                                H = Sla_R(H);
                                break;

                            case 0x25:  //SLA L
                                if (loggingEnabled) Log("SLA L");
                                tstates += 8;
                                L = Sla_R(L);
                                break;

                            case 0x26:  //SLA (HL)
                                if (loggingEnabled) Log("SLA (HL)");
                                tstates += 15;
                                PokeByte(HL, Sla_R(PeekByte(HL)));
                                break;

                            case 0x27:  //SLA A
                                if (loggingEnabled) Log("SLA A");
                                tstates += 8;
                                A = Sla_R(A);
                                break;

                            case 0x28:  //SRA B
                                if (loggingEnabled) Log("SRA B");
                                tstates += 8;
                                B = Sra_R(B);
                                break;

                            case 0x29:  //SRA C
                                if (loggingEnabled) Log("SRA C");
                                tstates += 8;
                                C = Sra_R(C);
                                break;

                            case 0x2A:  //SRA D
                                if (loggingEnabled) Log("SRA D");
                                tstates += 8;
                                D = Sra_R(D);
                                break;

                            case 0x2B:  //SRA E
                                if (loggingEnabled) Log("SRA E");
                                tstates += 8;
                                E = Sra_R(E);
                                break;

                            case 0x2C:  //SRA H
                                if (loggingEnabled) Log("SRA H");
                                tstates += 8;
                                H = Sra_R(H);
                                break;

                            case 0x2D:  //SRA L
                                if (loggingEnabled) Log("SRA L");
                                tstates += 8;
                                L = Sra_R(L);
                                break;

                            case 0x2E:  //SRA (HL)
                                if (loggingEnabled) Log("SRA (HL)");
                                tstates += 15;
                                PokeByte(HL, Sra_R(PeekByte(HL)));
                                break;

                            case 0x2F:  //SRA A
                                if (loggingEnabled) Log("SRA A");
                                tstates += 8;
                                A = Sra_R(A);
                                break;

                            case 0x30:  //SLL B
                                if (loggingEnabled) Log("SLL B");
                                tstates += 8;
                                B = Sll_R(B);
                                break;

                            case 0x31:  //SLL C
                                if (loggingEnabled) Log("SLL C");
                                tstates += 8;
                                C = Sll_R(C);
                                break;

                            case 0x32:  //SLL D
                                if (loggingEnabled) Log("SLL D");
                                tstates += 8;
                                D = Sll_R(D);
                                break;

                            case 0x33:  //SLL E
                                if (loggingEnabled) Log("SLL E");
                                tstates += 8;
                                E = Sll_R(E);
                                break;

                            case 0x34:  //SLL H
                                if (loggingEnabled) Log("SLL H");
                                tstates += 8;
                                H = Sll_R(H);
                                break;

                            case 0x35:  //SLL L
                                if (loggingEnabled) Log("SLL L");
                                tstates += 8;
                                L = Sll_R(L);
                                break;

                            case 0x36:  //SLL (HL)
                                if (loggingEnabled) Log("SLL (HL)");
                                tstates += 15;
                                PokeByte(HL, Sll_R(PeekByte(HL)));
                                break;

                            case 0x37:  //SLL A
                                if (loggingEnabled) Log("SLL A");
                                tstates += 8;
                                A = Sll_R(A);
                                break;

                            case 0x38:  //SRL B
                                if (loggingEnabled) Log("SRL B");
                                tstates += 8;
                                B = Srl_R(B);
                                break;

                            case 0x39:  //SRL C
                                if (loggingEnabled) Log("SRL C");
                                tstates += 8;
                                C = Srl_R(C);
                                break;

                            case 0x3A:  //SRL D
                                if (loggingEnabled) Log("SRL D");
                                tstates += 8;
                                D = Srl_R(D);
                                break;

                            case 0x3B:  //SRL E
                                if (loggingEnabled) Log("SRL E");
                                tstates += 8;
                                E = Srl_R(E);
                                break;

                            case 0x3C:  //SRL H
                                if (loggingEnabled) Log("SRL H");
                                tstates += 8;
                                H = Srl_R(H);
                                break;

                            case 0x3D:  //SRL L
                                if (loggingEnabled) Log("SRL L");
                                tstates += 8;
                                L = Srl_R(L);
                                break;

                            case 0x3E:  //SRL (HL)
                                if (loggingEnabled) Log("SRL (HL)");
                                tstates += 15;
                                PokeByte(HL, Srl_R(PeekByte(HL)));
                                break;

                            case 0x3F:  //SRL A
                                if (loggingEnabled) Log("SRL A");
                                tstates += 8;
                                A = Srl_R(A);
                                break;
                            #endregion

                            #region Bit test operation (BIT b, r)
                            case 0x40:  //BIT 0, B
                                if (loggingEnabled) Log("BIT 0, B");
                                tstates += 8;
                                Bit_R(0, B);
                                break;

                            case 0x41:  //BIT 0, C
                                if (loggingEnabled) Log("BIT 0, C");
                                tstates += 8;
                                Bit_R(0, C);
                                break;

                            case 0x42:  //BIT 0, D
                                if (loggingEnabled) Log("BIT 0, D");
                                tstates += 8;
                                Bit_R(0, D);
                                break;

                            case 0x43:  //BIT 0, E
                                if (loggingEnabled) Log("BIT 0, E");
                                tstates += 8;
                                Bit_R(0, E);
                                break;

                            case 0x44:  //BIT 0, H
                                if (loggingEnabled) Log("BIT 0, H");
                                tstates += 8;
                                Bit_R(0, H);
                                break;

                            case 0x45:  //BIT 0, L
                                if (loggingEnabled) Log("BIT 0, L");
                                tstates += 8;
                                Bit_R(0, L);
                                break;

                            case 0x46:  //BIT 0, (HL)
                                if (loggingEnabled) Log("BIT 0, (HL)");
                                tstates += 12;
                                Bit_R(0, PeekByte(HL));
                                break;

                            case 0x47:  //BIT 0, A
                                if (loggingEnabled) Log("BIT 0, A");
                                tstates += 8;
                                Bit_R(0, A);
                                break;

                            case 0x48:  //BIT 1, B
                                if (loggingEnabled) Log("BIT 1, B");
                                tstates += 8;
                                Bit_R(1, B);
                                break;

                            case 0x49:  //BIT 1, C
                                if (loggingEnabled) Log("BIT 1, C");
                                tstates += 8;
                                Bit_R(1, C);
                                break;

                            case 0x4A:  //BIT 1, D
                                if (loggingEnabled) Log("BIT 1, D");
                                tstates += 8;
                                Bit_R(1, D);
                                break;

                            case 0x4B:  //BIT 1, E
                                if (loggingEnabled) Log("BIT 1, E");
                                tstates += 8;
                                Bit_R(1, E);
                                break;

                            case 0x4C:  //BIT 1, H
                                if (loggingEnabled) Log("BIT 1, H");
                                tstates += 8;
                                Bit_R(1, H);
                                break;

                            case 0x4D:  //BIT 1, L
                                if (loggingEnabled) Log("BIT 1, L");
                                tstates += 8;
                                Bit_R(1, L);
                                break;

                            case 0x4E:  //BIT 1, (HL)
                                if (loggingEnabled) Log("BIT 1, (HL)");
                                tstates += 12;
                                Bit_R(1, PeekByte(HL));
                                break;

                            case 0x4F:  //BIT 1, A
                                if (loggingEnabled) Log("BIT 1, A");
                                tstates += 8;
                                Bit_R(1, A);
                                break;

                            case 0x50:  //BIT 2, B
                                if (loggingEnabled) Log("BIT 2, B");
                                tstates += 8;
                                Bit_R(2, B);
                                break;

                            case 0x51:  //BIT 2, C
                                if (loggingEnabled) Log("BIT 2, C");
                                tstates += 8;
                                Bit_R(2, C);
                                break;

                            case 0x52:  //BIT 2, D
                                if (loggingEnabled) Log("BIT 2, D");
                                tstates += 8;
                                Bit_R(2, D);
                                break;

                            case 0x53:  //BIT 2, E
                                if (loggingEnabled) Log("BIT 2, E");
                                tstates += 8;
                                Bit_R(2, E);
                                break;

                            case 0x54:  //BIT 2, H
                                if (loggingEnabled) Log("BIT 2, H");
                                tstates += 8;
                                Bit_R(2, H);
                                break;

                            case 0x55:  //BIT 2, L
                                if (loggingEnabled) Log("BIT 2, L");
                                tstates += 8;
                                Bit_R(2, L);
                                break;

                            case 0x56:  //BIT 2, (HL)
                                if (loggingEnabled) Log("BIT 2, (HL)");
                                tstates += 12;
                                Bit_R(2, PeekByte(HL));
                                break;

                            case 0x57:  //BIT 2, A
                                if (loggingEnabled) Log("BIT 2, A");
                                tstates += 8;
                                Bit_R(2, A);
                                break;

                            case 0x58:  //BIT 3, B
                                if (loggingEnabled) Log("BIT 3, B");
                                tstates += 8;
                                Bit_R(3, B);
                                break;

                            case 0x59:  //BIT 3, C
                                if (loggingEnabled) Log("BIT 3, C");
                                tstates += 8;
                                Bit_R(3, C);
                                break;

                            case 0x5A:  //BIT 3, D
                                if (loggingEnabled) Log("BIT 3, D");
                                tstates += 8;
                                Bit_R(3, D);
                                break;

                            case 0x5B:  //BIT 3, E
                                if (loggingEnabled) Log("BIT 3, E");
                                tstates += 8;
                                Bit_R(3, E);
                                break;

                            case 0x5C:  //BIT 3, H
                                if (loggingEnabled) Log("BIT 3, H");
                                tstates += 8;
                                Bit_R(3, H);
                                break;

                            case 0x5D:  //BIT 3, L
                                if (loggingEnabled) Log("BIT 3, L");
                                tstates += 8;
                                Bit_R(3, L);
                                break;

                            case 0x5E:  //BIT 3, (HL)
                                if (loggingEnabled) Log("BIT 3, (HL)");
                                tstates += 12;
                                Bit_R(3, PeekByte(HL));
                                break;

                            case 0x5F:  //BIT 3, A
                                if (loggingEnabled) Log("BIT 3, A");
                                tstates += 8;
                                Bit_R(3, A);
                                break;

                            case 0x60:  //BIT 4, B
                                if (loggingEnabled) Log("BIT 4, B");
                                tstates += 8;
                                Bit_R(4, B);
                                break;

                            case 0x61:  //BIT 4, C
                                if (loggingEnabled) Log("BIT 4, C");
                                tstates += 8;
                                Bit_R(4, C);
                                break;

                            case 0x62:  //BIT 4, D
                                if (loggingEnabled) Log("BIT 4, D");
                                tstates += 8;
                                Bit_R(4, D);
                                break;

                            case 0x63:  //BIT 4, E
                                if (loggingEnabled) Log("BIT 4, E");
                                tstates += 8;
                                Bit_R(4, E);
                                break;

                            case 0x64:  //BIT 4, H
                                if (loggingEnabled) Log("BIT 4, H");
                                tstates += 8;
                                Bit_R(4, H);
                                break;

                            case 0x65:  //BIT 4, L
                                if (loggingEnabled) Log("BIT 4, L");
                                tstates += 8;
                                Bit_R(4, L);
                                break;

                            case 0x66:  //BIT 4, (HL)
                                if (loggingEnabled) Log("BIT 4, (HL)");
                                tstates += 12;
                                Bit_R(4, PeekByte(HL));
                                break;

                            case 0x67:  //BIT 4, A
                                if (loggingEnabled) Log("BIT 4, A");
                                tstates += 8;
                                Bit_R(4, A);
                                break;

                            case 0x68:  //BIT 5, B
                                if (loggingEnabled) Log("BIT 5, B");
                                tstates += 8;
                                Bit_R(5, B);
                                break;

                            case 0x69:  //BIT 5, C
                                if (loggingEnabled) Log("BIT 5, C");
                                tstates += 8;
                                Bit_R(5, C);
                                break;

                            case 0x6A:  //BIT 5, D
                                if (loggingEnabled) Log("BIT 5, D");
                                tstates += 8;
                                Bit_R(5, D);
                                break;

                            case 0x6B:  //BIT 5, E
                                if (loggingEnabled) Log("BIT 5, E");
                                tstates += 8;
                                Bit_R(5, E);
                                break;

                            case 0x6C:  //BIT 5, H
                                if (loggingEnabled) Log("BIT 5, H");
                                tstates += 8;
                                Bit_R(5, H);
                                break;

                            case 0x6D:  //BIT 5, L
                                if (loggingEnabled) Log("BIT 5, L");
                                tstates += 8;
                                Bit_R(5, L);
                                break;

                            case 0x6E:  //BIT 5, (HL)
                                if (loggingEnabled) Log("BIT 5, (HL)");
                                tstates += 12;
                                Bit_R(5, PeekByte(HL));
                                break;

                            case 0x6F:  //BIT 5, A
                                if (loggingEnabled) Log("BIT 5, A");
                                tstates += 8;
                                Bit_R(5, A);
                                break;

                            case 0x70:  //BIT 6, B
                                if (loggingEnabled) Log("BIT 6, B");
                                tstates += 8;
                                Bit_R(6, B);
                                break;

                            case 0x71:  //BIT 6, C
                                if (loggingEnabled) Log("BIT 6, C");
                                tstates += 8;
                                Bit_R(6, C);
                                break;

                            case 0x72:  //BIT 6, D
                                if (loggingEnabled) Log("BIT 6, D");
                                tstates += 8;
                                Bit_R(6, D);
                                break;

                            case 0x73:  //BIT 6, E
                                if (loggingEnabled) Log("BIT 6, E");
                                tstates += 8;
                                Bit_R(6, E);
                                break;

                            case 0x74:  //BIT 6, H
                                if (loggingEnabled) Log("BIT 6, H");
                                tstates += 8;
                                Bit_R(6, H);
                                break;

                            case 0x75:  //BIT 6, L
                                if (loggingEnabled) Log("BIT 6, L");
                                tstates += 8;
                                Bit_R(6, L);
                                break;

                            case 0x76:  //BIT 6, (HL)
                                if (loggingEnabled) Log("BIT 6, (HL)");
                                tstates += 12;
                                Bit_R(6, PeekByte(HL));
                                break;

                            case 0x77:  //BIT 6, A
                                if (loggingEnabled) Log("BIT 6, A");
                                tstates += 8;
                                Bit_R(6, A);
                                break;

                            case 0x78:  //BIT 7, B
                                if (loggingEnabled) Log("BIT 7, B");
                                tstates += 8;
                                Bit_R(7, B);
                                break;

                            case 0x79:  //BIT 7, C
                                if (loggingEnabled) Log("BIT 7, C");
                                tstates += 8;
                                Bit_R(7, C);
                                break;

                            case 0x7A:  //BIT 7, D
                                if (loggingEnabled) Log("BIT 7, D");
                                tstates += 8;
                                Bit_R(7, D);
                                break;

                            case 0x7B:  //BIT 7, E
                                if (loggingEnabled) Log("BIT 7, E");
                                tstates += 8;
                                Bit_R(7, E);
                                break;

                            case 0x7C:  //BIT 7, H
                                if (loggingEnabled) Log("BIT 7, H");
                                tstates += 8;
                                Bit_R(7, H);
                                break;

                            case 0x7D:  //BIT 7, L
                                if (loggingEnabled) Log("BIT 7, L");
                                tstates += 8;
                                Bit_R(7, L);
                                break;

                            case 0x7E:  //BIT 7, (HL)
                                if (loggingEnabled) Log("BIT 7, (HL)");
                                tstates += 12;
                                Bit_R(7, PeekByte(HL));
                                break;

                            case 0x7F:  //BIT 7, A
                                if (loggingEnabled) Log("BIT 7, A");
                                tstates += 8;
                                Bit_R(7, A);
                                break;
                            #endregion

                            #region Reset bit operation (RES b, r)
                            case 0x80:  //RES 0, B
                                if (loggingEnabled) Log("RES 0, B");
                                tstates += 8;
                                B = Res_R(0, B);
                                break;

                            case 0x81:  //RES 0, C
                                if (loggingEnabled) Log("RES 0, C");
                                tstates += 8;
                                C = Res_R(0, C);
                                break;

                            case 0x82:  //RES 0, D
                                if (loggingEnabled) Log("RES 0, D");
                                tstates += 8;
                                D = Res_R(0, D);
                                break;

                            case 0x83:  //RES 0, E
                                if (loggingEnabled) Log("RES 0, E");
                                tstates += 8;
                                E = Res_R(0, E);
                                break;

                            case 0x84:  //RES 0, H
                                if (loggingEnabled) Log("RES 0, H");
                                tstates += 8;
                                H = Res_R(0, H);
                                break;

                            case 0x85:  //RES 0, L
                                if (loggingEnabled) Log("RES 0, L");
                                tstates += 8;
                                L = Res_R(0, L);
                                break;

                            case 0x86:  //RES 0, (HL)
                                if (loggingEnabled) Log("RES 0, (HL)");
                                tstates += 15;
                                PokeByte(HL, Res_R(0, PeekByte(HL)));
                                break;

                            case 0x87:  //RES 0, A
                                if (loggingEnabled) Log("RES 0, A");
                                tstates += 8;
                                A = Res_R(0, A);
                                break;

                            case 0x88:  //RES 1, B
                                if (loggingEnabled) Log("RES 1, B");
                                tstates += 8;
                                B = Res_R(1, B);
                                break;

                            case 0x89:  //RES 1, C
                                if (loggingEnabled) Log("RES 1, C");
                                tstates += 8;
                                C = Res_R(1, C);
                                break;

                            case 0x8A:  //RES 1, D
                                if (loggingEnabled) Log("RES 1, D");
                                tstates += 8;
                                D = Res_R(1, D);
                                break;

                            case 0x8B:  //RES 1, E
                                if (loggingEnabled) Log("RES 1, E");
                                tstates += 8;
                                E = Res_R(1, E);
                                break;

                            case 0x8C:  //RES 1, H
                                if (loggingEnabled) Log("RES 1, H");
                                tstates += 8;
                                H = Res_R(1, H);
                                break;

                            case 0x8D:  //RES 1, L
                                if (loggingEnabled) Log("RES 1, L");
                                tstates += 8;
                                L = Res_R(1, L);
                                break;

                            case 0x8E:  //RES 1, (HL)
                                if (loggingEnabled) Log("RES 1, (HL)");
                                tstates += 15;
                                PokeByte(HL, Res_R(1, PeekByte(HL)));
                                break;

                            case 0x8F:  //RES 1, A
                                if (loggingEnabled) Log("RES 1, A");
                                tstates += 8;
                                A = Res_R(1, A);
                                break;

                            case 0x90:  //RES 2, B
                                if (loggingEnabled) Log("RES 2, B");
                                tstates += 8;
                                B = Res_R(2, B);
                                break;

                            case 0x91:  //RES 2, C
                                if (loggingEnabled) Log("RES 2, C");
                                tstates += 8;
                                C = Res_R(2, C);
                                break;

                            case 0x92:  //RES 2, D
                                if (loggingEnabled) Log("RES 2, D");
                                tstates += 8;
                                D = Res_R(2, D);
                                break;

                            case 0x93:  //RES 2, E
                                if (loggingEnabled) Log("RES 2, E");
                                tstates += 8;
                                E = Res_R(2, E);
                                break;

                            case 0x94:  //RES 2, H
                                if (loggingEnabled) Log("RES 2, H");
                                tstates += 8;
                                H = Res_R(2, H);
                                break;

                            case 0x95:  //RES 2, L
                                if (loggingEnabled) Log("RES 2, L");
                                tstates += 8;
                                L = Res_R(2, L);
                                break;

                            case 0x96:  //RES 2, (HL)
                                if (loggingEnabled) Log("RES 2, (HL)");
                                tstates += 15;
                                PokeByte(HL,Res_R(2, PeekByte(HL)));
                                break;

                            case 0x97:  //RES 2, A
                                if (loggingEnabled) Log("RES 2, A");
                                tstates += 8;
                                A = Res_R(2, A);
                                break;

                            case 0x98:  //RES 3, B
                                if (loggingEnabled) Log("RES 3, B");
                                tstates += 8;
                                B = Res_R(3, B);
                                break;

                            case 0x99:  //RES 3, C
                                if (loggingEnabled) Log("RES 3, C");
                                tstates += 8;
                                C = Res_R(3, C);
                                break;

                            case 0x9A:  //RES 3, D
                                if (loggingEnabled) Log("RES 3, D");
                                tstates += 8;
                                D = Res_R(3, D);
                                break;

                            case 0x9B:  //RES 3, E
                                if (loggingEnabled) Log("RES 3, E");
                                tstates += 8;
                                E = Res_R(3, E);
                                break;

                            case 0x9C:  //RES 3, H
                                if (loggingEnabled) Log("RES 3, H");
                                tstates += 8;
                                H = Res_R(3, H);
                                break;

                            case 0x9D:  //RES 3, L
                                if (loggingEnabled) Log("RES 3, L");
                                tstates += 8;
                                L = Res_R(3, L);
                                break;

                            case 0x9E:  //RES 3, (HL)
                                if (loggingEnabled) Log("RES 3, (HL)");
                                tstates += 15;
                                PokeByte(HL, Res_R(3, PeekByte(HL)));
                                break;

                            case 0x9F:  //RES 3, A
                                if (loggingEnabled) Log("RES 3, A");
                                tstates += 8;
                                A = Res_R(3, A);
                                break;

                            case 0xA0:  //RES 4, B
                                if (loggingEnabled) Log("RES 4, B");
                                tstates += 8;
                                B = Res_R(4, B);
                                break;

                            case 0xA1:  //RES 4, C
                                if (loggingEnabled) Log("RES 4, C");
                                tstates += 8;
                                C = Res_R(4, C);
                                break;

                            case 0xA2:  //RES 4, D
                                if (loggingEnabled) Log("RES 4, D");
                                tstates += 8;
                                D = Res_R(4, D);
                                break;

                            case 0xA3:  //RES 4, E
                                if (loggingEnabled) Log("RES 4, E");
                                tstates += 8;
                                E = Res_R(4, E);
                                break;

                            case 0xA4:  //RES 4, H
                                if (loggingEnabled) Log("RES 4, H");
                                tstates += 8;
                                H = Res_R(4, H);
                                break;

                            case 0xA5:  //RES 4, L
                                if (loggingEnabled) Log("RES 4, L");
                                tstates += 8;
                                L = Res_R(4, L);
                                break;

                            case 0xA6:  //RES 4, (HL)
                                if (loggingEnabled) Log("RES 4, (HL)");
                                tstates += 15;
                                PokeByte(HL,Res_R(4, PeekByte(HL)));
                                break;

                            case 0xA7:  //RES 4, A
                                if (loggingEnabled) Log("RES 4, A");
                                tstates += 8;
                                A = Res_R(4, A);
                                break;

                            case 0xA8:  //RES 5, B
                                if (loggingEnabled) Log("RES 5, B");
                                tstates += 8;
                                B = Res_R(5, B);
                                break;

                            case 0xA9:  //RES 5, C
                                if (loggingEnabled) Log("RES 5, C");
                                tstates += 8;
                                C = Res_R(5, C);
                                break;

                            case 0xAA:  //RES 5, D
                                if (loggingEnabled) Log("RES 5, D");
                                tstates += 8;
                                D = Res_R(5, D);
                                break;

                            case 0xAB:  //RES 5, E
                                if (loggingEnabled) Log("RES 5, E");
                                tstates += 8;
                                E = Res_R(5, E);
                                break;

                            case 0xAC:  //RES 5, H
                                if (loggingEnabled) Log("RES 5, H");
                                tstates += 8;
                                H = Res_R(5, H);
                                break;

                            case 0xAD:  //RES 5, L
                                if (loggingEnabled) Log("RES 5, L");
                                tstates += 8;
                                L = Res_R(5, L);
                                break;

                            case 0xAE:  //RES 5, (HL)
                                if (loggingEnabled) Log("RES 5, (HL)");
                                tstates += 15;
                                PokeByte(HL, Res_R(5, PeekByte(HL)));
                                break;

                            case 0xAF:  //RES 5, A
                                if (loggingEnabled) Log("RES 5, A");
                                tstates += 8;
                                A = Res_R(5, A);
                                break;

                            case 0xB0:  //RES 6, B
                                if (loggingEnabled) Log("RES 6, B");
                                tstates += 8;
                                B = Res_R(6, B);
                                break;

                            case 0xB1:  //RES 6, C
                                if (loggingEnabled) Log("RES 6, C");
                                tstates += 8;
                                C = Res_R(6, C);
                                break;

                            case 0xB2:  //RES 6, D
                                if (loggingEnabled) Log("RES 6, D");
                                tstates += 8;
                                D = Res_R(6, D);
                                break;

                            case 0xB3:  //RES 6, E
                                if (loggingEnabled) Log("RES 6, E");
                                tstates += 8;
                                E = Res_R(6, E);
                                break;

                            case 0xB4:  //RES 6, H
                                if (loggingEnabled) Log("RES 6, H");
                                tstates += 8;
                                H = Res_R(6, H);
                                break;

                            case 0xB5:  //RES 6, L
                                if (loggingEnabled) Log("RES 6, L");
                                tstates += 8;
                                L = Res_R(6, L);
                                break;

                            case 0xB6:  //RES 6, (HL)
                                if (loggingEnabled) Log("RES 6, (HL)");
                                tstates += 15;
                                PokeByte(HL, Res_R(6, PeekByte(HL)));
                                break;

                            case 0xB7:  //RES 6, A
                                if (loggingEnabled) Log("RES 6, A");
                                tstates += 8;
                                A = Res_R(6, A);
                                break;

                            case 0xB8:  //RES 7, B
                                if (loggingEnabled) Log("RES 7, B");
                                tstates += 8;
                                B = Res_R(7, B);
                                break;

                            case 0xB9:  //RES 7, C
                                if (loggingEnabled) Log("RES 7, C");
                                tstates += 8;
                                C = Res_R(7, C);
                                break;

                            case 0xBA:  //RES 7, D
                                if (loggingEnabled) Log("RES 7, D");
                                tstates += 8;
                                D = Res_R(7, D);
                                break;

                            case 0xBB:  //RES 7, E
                                if (loggingEnabled) Log("RES 7, E");
                                tstates += 8;
                                E = Res_R(7, E);
                                break;

                            case 0xBC:  //RES 7, H
                                if (loggingEnabled) Log("RES 7, H");
                                tstates += 8;
                                H = Res_R(7, H);
                                break;

                            case 0xBD:  //RES 7, L
                                if (loggingEnabled) Log("RES 7, L");
                                tstates += 8;
                                L = Res_R(7, L);
                                break;

                            case 0xBE:  //RES 7, (HL)
                                if (loggingEnabled) Log("RES 7, (HL)");
                                tstates += 15;
                                PokeByte(HL, Res_R(7, PeekByte(HL)));
                                break;

                            case 0xBF:  //RES 7, A
                                if (loggingEnabled) Log("RES 7, A");
                                tstates += 8;
                                A = Res_R(7, A);
                                break;
                            #endregion

                            #region Set bit operation (SET b, r)
                            case 0xC0:  //SET 0, B
                                if (loggingEnabled) Log("SET 0, B");
                                tstates += 8;
                                B = Set_R(0, B);
                                break;

                            case 0xC1:  //SET 0, C
                                if (loggingEnabled) Log("SET 0, C");
                                tstates += 8;
                                C = Set_R(0, C);
                                break;

                            case 0xC2:  //SET 0, D
                                if (loggingEnabled) Log("SET 0, D");
                                tstates += 8;
                                D = Set_R(0, D);
                                break;

                            case 0xC3:  //SET 0, E
                                if (loggingEnabled) Log("SET 0, E");
                                tstates += 8;
                                E = Set_R(0, E);
                                break;

                            case 0xC4:  //SET 0, H
                                if (loggingEnabled) Log("SET 0, H");
                                tstates += 8;
                                H = Set_R(0, H);
                                break;

                            case 0xC5:  //SET 0, L
                                if (loggingEnabled) Log("SET 0, L");
                                tstates += 8;
                                L = Set_R(0, L);
                                break;

                            case 0xC6:  //SET 0, (HL)
                                if (loggingEnabled) Log("SET 0, (HL)");
                                tstates += 15;
                                PokeByte(HL, Set_R(0, PeekByte(HL)));
                                break;

                            case 0xC7:  //SET 0, A
                                if (loggingEnabled) Log("SET 0, A");
                                tstates += 8;
                                A = Set_R(0, A);
                                break;

                            case 0xC8:  //SET 1, B
                                if (loggingEnabled) Log("SET 1, B");
                                tstates += 8;
                                B = Set_R(1, B);
                                break;

                            case 0xC9:  //SET 1, C
                                if (loggingEnabled) Log("SET 1, C");
                                tstates += 8;
                                C = Set_R(1, C);
                                break;

                            case 0xCA:  //SET 1, D
                                if (loggingEnabled) Log("SET 1, D");
                                tstates += 8;
                                D = Set_R(1, D);
                                break;

                            case 0xCB:  //SET 1, E
                                if (loggingEnabled) Log("SET 1, E");
                                tstates += 8;
                                E = Set_R(1, E);
                                break;

                            case 0xCC:  //SET 1, H
                                if (loggingEnabled) Log("SET 1, H");
                                tstates += 8;
                                H = Set_R(1, H);
                                break;

                            case 0xCD:  //SET 1, L
                                if (loggingEnabled) Log("SET 1, L");
                                tstates += 8;
                                L = Set_R(1, L);
                                break;

                            case 0xCE:  //SET 1, (HL)
                                if (loggingEnabled) Log("SET 1, (HL)");
                                tstates += 15;
                                PokeByte(HL, Set_R(1, PeekByte(HL)));
                                break;

                            case 0xCF:  //SET 1, A
                                if (loggingEnabled) Log("SET 1, A");
                                tstates += 8;
                                A = Set_R(1, A);
                                break;

                            case 0xD0:  //SET 2, B
                                if (loggingEnabled) Log("SET 2, B");
                                tstates += 8;
                                B = Set_R(2, B);
                                break;

                            case 0xD1:  //SET 2, C
                                if (loggingEnabled) Log("SET 2, C");
                                tstates += 8;
                                C = Set_R(2, C);
                                break;

                            case 0xD2:  //SET 2, D
                                if (loggingEnabled) Log("SET 2, D");
                                tstates += 8;
                                D = Set_R(2, D);
                                break;

                            case 0xD3:  //SET 2, E
                                if (loggingEnabled) Log("SET 2, E");
                                tstates += 8;
                                E = Set_R(2, E);
                                break;

                            case 0xD4:  //SET 2, H
                                if (loggingEnabled) Log("SET 2, H");
                                tstates += 8;
                                H = Set_R(2, H);
                                break;

                            case 0xD5:  //SET 2, L
                                if (loggingEnabled) Log("SET 2, L");
                                tstates += 8;
                                L = Set_R(2, L);
                                break;

                            case 0xD6:  //SET 2, (HL)
                                if (loggingEnabled) Log("SET 2, (HL)");
                                tstates += 15;
                                PokeByte(HL, Set_R(2, PeekByte(HL)));
                                break;

                            case 0xD7:  //SET 2, A
                                if (loggingEnabled) Log("SET 2, A");
                                tstates += 8;
                                A = Set_R(2, A);
                                break;

                            case 0xD8:  //SET 3, B
                                if (loggingEnabled) Log("SET 3, B");
                                tstates += 8;
                                B = Set_R(3, B);
                                break;

                            case 0xD9:  //SET 3, C
                                if (loggingEnabled) Log("SET 3, C");
                                tstates += 8;
                                C = Set_R(3, C);
                                break;

                            case 0xDA:  //SET 3, D
                                if (loggingEnabled) Log("SET 3, D");
                                tstates += 8;
                                D = Set_R(3, D);
                                break;

                            case 0xDB:  //SET 3, E
                                if (loggingEnabled) Log("SET 3, E");
                                tstates += 8;
                                E = Set_R(3, E);
                                break;

                            case 0xDC:  //SET 3, H
                                if (loggingEnabled) Log("SET 3, H");
                                tstates += 8;
                                H = Set_R(3, H);
                                break;

                            case 0xDD:  //SET 3, L
                                if (loggingEnabled) Log("SET 3, L");
                                tstates += 8;
                                L = Set_R(3, L);
                                break;

                            case 0xDE:  //SET 3, (HL)
                                if (loggingEnabled) Log("SET 3, (HL)");
                                tstates += 15;
                                PokeByte(HL, Set_R(3, PeekByte(HL)));
                                break;

                            case 0xDF:  //SET 3, A
                                if (loggingEnabled) Log("SET 3, A");
                                tstates += 8;
                                A = Set_R(3, A);
                                break;

                            case 0xE0:  //SET 4, B
                                if (loggingEnabled) Log("SET 4, B");
                                tstates += 8;
                                B = Set_R(4, B);
                                break;

                            case 0xE1:  //SET 4, C
                                if (loggingEnabled) Log("SET 4, C");
                                tstates += 8;
                                C = Set_R(4, C);
                                break;

                            case 0xE2:  //SET 4, D
                                if (loggingEnabled) Log("SET 4, D");
                                tstates += 8;
                                D = Set_R(4, D);
                                break;

                            case 0xE3:  //SET 4, E
                                if (loggingEnabled) Log("SET 4, E");
                                tstates += 8;
                                E = Set_R(4, E);
                                break;

                            case 0xE4:  //SET 4, H
                                if (loggingEnabled) Log("SET 4, H");
                                tstates += 8;
                                H = Set_R(4, H);
                                break;

                            case 0xE5:  //SET 4, L
                                if (loggingEnabled) Log("SET 4, L");
                                tstates += 8;
                                L = Set_R(4, L);
                                break;

                            case 0xE6:  //SET 4, (HL)
                                if (loggingEnabled) Log("SET 4, (HL)");
                                tstates += 15;
                                PokeByte(HL, Set_R(4, PeekByte(HL)));
                                break;

                            case 0xE7:  //SET 4, A
                                if (loggingEnabled) Log("SET 4, A");
                                tstates += 8;
                                A = Set_R(4, A);
                                break;

                            case 0xE8:  //SET 5, B
                                if (loggingEnabled) Log("SET 5, B");
                                tstates += 8;
                                B = Set_R(5, B);
                                break;

                            case 0xE9:  //SET 5, C
                                if (loggingEnabled) Log("SET 5, C");
                                tstates += 8;
                                C = Set_R(5, C);
                                break;

                            case 0xEA:  //SET 5, D
                                if (loggingEnabled) Log("SET 5, D");
                                tstates += 8;
                                D = Set_R(5, D);
                                break;

                            case 0xEB:  //SET 5, E
                                if (loggingEnabled) Log("SET 5, E");
                                tstates += 8;
                                E = Set_R(5, E);
                                break;

                            case 0xEC:  //SET 5, H
                                if (loggingEnabled) Log("SET 5, H");
                                tstates += 8;
                                H = Set_R(5, H);
                                break;

                            case 0xED:  //SET 5, L
                                if (loggingEnabled) Log("SET 5, L");
                                tstates += 8;
                                L = Set_R(5, L);
                                break;

                            case 0xEE:  //SET 5, (HL)
                                if (loggingEnabled) Log("SET 5, (HL)");
                                tstates += 15;
                                PokeByte(HL, Set_R(5, PeekByte(HL)));
                                break;

                            case 0xEF:  //SET 5, A
                                if (loggingEnabled) Log("SET 5, A");
                                tstates += 8;
                                A = Set_R(5, A);
                                break;

                            case 0xF0:  //SET 6, B
                                if (loggingEnabled) Log("SET 6, B");
                                tstates += 8;
                                B = Set_R(6, B);
                                break;

                            case 0xF1:  //SET 6, C
                                if (loggingEnabled) Log("SET 6, C");
                                tstates += 8;
                                C = Set_R(6, C);
                                break;

                            case 0xF2:  //SET 6, D
                                if (loggingEnabled) Log("SET 6, D");
                                tstates += 8;
                                D = Set_R(6, D);
                                break;

                            case 0xF3:  //SET 6, E
                                if (loggingEnabled) Log("SET 6, E");
                                tstates += 8;
                                E = Set_R(6, E);
                                break;

                            case 0xF4:  //SET 6, H
                                if (loggingEnabled) Log("SET 6, H");
                                tstates += 8;
                                H = Set_R(6, H);
                                break;

                            case 0xF5:  //SET 6, L
                                if (loggingEnabled) Log("SET 6, L");
                                tstates += 8;
                                L = Set_R(6, L);
                                break;

                            case 0xF6:  //SET 6, (HL)
                                if (loggingEnabled) Log("SET 6, (HL)");
                                tstates += 15;
                                PokeByte(HL, Set_R(6, PeekByte(HL)));
                                break;

                            case 0xF7:  //SET 6, A
                                if (loggingEnabled) Log("SET 6, A");
                                tstates += 8;
                                A = Set_R(6, A);
                                break;

                            case 0xF8:  //SET 7, B
                                if (loggingEnabled) Log("SET 7, B");
                                tstates += 8;
                                B = Set_R(7, B);
                                break;

                            case 0xF9:  //SET 7, C
                                if (loggingEnabled) Log("SET 7, C");
                                tstates += 8;
                                C = Set_R(7, C);
                                break;

                            case 0xFA:  //SET 7, D
                                if (loggingEnabled) Log("SET 7, D");
                                tstates += 8;
                                D = Set_R(7, D);
                                break;

                            case 0xFB:  //SET 7, E
                                if (loggingEnabled) Log("SET 7, E");
                                tstates += 8;
                                E = Set_R(7, E);
                                break;

                            case 0xFC:  //SET 7, H
                                if (loggingEnabled) Log("SET 7, H");
                                tstates += 8;
                                H = Set_R(7, H);
                                break;

                            case 0xFD:  //SET 7, L
                                if (loggingEnabled) Log("SET 7, L");
                                tstates += 8;
                                L = Set_R(7, L);
                                break;

                            case 0xFE:  //SET 7, (HL)
                                if (loggingEnabled) Log("SET 7, (HL)");
                                tstates += 15;
                                PokeByte(HL, Set_R(7, PeekByte(HL)));
                                break;

                            case 0xFF:  //SET 7, A
                                if (loggingEnabled) Log("SET 7, A");
                                tstates += 8;
                                A = Set_R(7, A);
                                break;
                            #endregion

                            default:
                                String msg = "ERROR: Could not handle DD" + opcode.ToString();
                                MessageBox.Show(msg, "Opcode handler",
                                            MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                                break;
                        }
                        break;
                    #endregion

                    #region Opcodes with DD prefix (includes DDCB)
                    case 0xDD:
                        switch (opcode = FetchInstruction())
                        {
                            #region Addition instructions
                            case 0x09:  //ADD IX, BC
                                if (loggingEnabled) Log("ADD IX, BC");
                                tstates += 15;
                                IX = Add_RR(IX, BC); 
                                break;

                            case 0x19:  //ADD IX, DE
                                if (loggingEnabled) Log("ADD IX, DE");
                                tstates += 15;
                                IX = Add_RR(IX, DE); 
                                break;

                            case 0x29:  //ADD IX, IX
                                if (loggingEnabled) Log("ADD IX, IX");
                                tstates += 15;
                                IX = Add_RR(IX, IX); 
                                break;

                            case 0x39:  //ADD IX, SP
                                if (loggingEnabled) Log("ADD IX, SP");
                                tstates += 15;
                                IX = Add_RR(IX, SP);  
                                break;

                            case 0x84:  //ADD A, IXH
                                if (loggingEnabled) Log("ADD A, IXH");
                                tstates += 4;
                                Add_R(IXH);
                                break;

                            case 0x85:  //ADD A, IXL
                                if (loggingEnabled) Log("ADD A, IXL");
                                tstates += 4;
                                Add_R(IXL);
                                break;

                            case 0x86:  //Add A, (IX+d)
                                disp = GetDisplacement(PeekByte(PC));
                                
                                int offset = IX + disp ; //The displacement required
                                if (loggingEnabled) Log(string.Format("ADD A, (IX + {0:X})", disp));
                                tstates += 19;
                                Add_R(PeekByte(offset));
                                PC++;
                                break;

                            case 0x8C:  //ADC A, IXH
                                if (loggingEnabled) Log("ADC A, IXH");
                                tstates += 4;
                                Adc_R(IXH);
                                break;

                            case 0x8D:  //ADC A, IXL
                                if (loggingEnabled) Log("ADC A, IXL");
                                tstates += 4;
                                Adc_R(IXL);
                                break;

                            case 0x8E: //ADC A, (IX+d) 
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("ADC A, (IX + {0:X})", disp));
                                tstates += 19;
                                Adc_R(PeekByte(offset));
                                PC++;
                                break;
                            #endregion

                            #region Subtraction instructions
                            case 0x94:  //SUB A, IXH
                                if (loggingEnabled) Log("SUB A, IXH");
                                tstates += 4;
                                Sub_R(IXH);
                                break;

                            case 0x95:  //SUB A, IXL
                                if (loggingEnabled) Log("SUB A, IXL");
                                tstates += 4;
                                Sub_R(IXL);
                                break;

                            case 0x96:  //SUB (IX + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("SUB (IX + {0:X})", disp));
                                tstates += 19;
                                Sub_R(PeekByte(offset));
                                PC++;
                                break;

                            case 0x9C:  //SBC A, IXH
                                if (loggingEnabled) Log("SBC A, IXH");
                                tstates += 4;
                                Sbc_R(IXH);
                                break;

                            case 0x9D:  //SBC A, IXL
                                if (loggingEnabled) Log("SBC A, IXL");
                                tstates += 4;
                                Sbc_R(IXL);
                                break;

                            case 0x9E:  //SBC A, (IX + d)
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("SBC A, (IX + {0:X})", disp));
                                tstates += 19;
                                Sbc_R(PeekByte(offset));
                                PC++;
                                break;
                            #endregion

                            #region Increment/Decrements
                            case 0x23:  //INC IX
                                if (loggingEnabled) Log("INC IX");
                                tstates += 10;
                                IX++;
                                break;

                            case 0x24:  //INC IXH
                                if (loggingEnabled) Log("INC IXH");
                                tstates += 4;
                                IXH = Inc(IXH);
                                break;

                            case 0x25:  //DEC IXH
                                if (loggingEnabled) Log("DEC IXH");
                                tstates += 4;
                                IXH = Dec(IXH);
                                break;

                            case 0x2B:  //DEC IX
                                if (loggingEnabled) Log("DEC IX");
                                tstates += 10;
                                IX--;
                                break;

                            case 0x2C:  //INC IXL
                                if (loggingEnabled) Log("INC IXL");
                                tstates += 4;
                                IXL = Inc(IXL);
                                break;

                            case 0x2D:  //DEC IXL
                                if (loggingEnabled) Log("DEC IXL");
                                tstates += 4;
                                IXL = Dec(IXL);
                                break;

                            case 0x34:  //INC (IX + d)
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("INC (IX + {0:X})", disp));
                                tstates += 23;
                                PokeByte(offset, Inc(PeekByte(offset)));
                                PC++;
                                break;

                            case 0x35:  //DEC (IX + d)
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("DEC (IX + {0:X})", disp));
                                tstates += 23;
                                PokeByte(offset, Dec(PeekByte(offset)));
                                PC++;
                                break;
                            #endregion

                            #region Bitwise operators

                            case 0xA4:  //AND IXH
                                if (loggingEnabled) Log("AND IXH");
                                tstates += 4;
                                And_R(IXH);
                                break;

                            case 0xA5:  //AND IXL
                                if (loggingEnabled) Log("AND IXL");
                                tstates += 4;
                                And_R(IXL);
                                break;

                            case 0xA6:  //AND (IX + d)
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("AND (IX + {0:X})", disp));
                                tstates += 19;
                                And_R(PeekByte(offset));
                                PC++;
                                break;

                            case 0xAC:  //XOR IXH
                                if (loggingEnabled) Log("XOR IXH");
                                tstates += 4;
                                Xor_R(IXH);
                                break;

                            case 0xAD:  //XOR IXL
                                if (loggingEnabled) Log("XOR IXL");
                                tstates += 4;
                                Xor_R(IXL);
                                break;

                            case 0xAE:  //XOR (IX + d)
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("XOR (IX + {0:X})", disp));
                                tstates += 19;
                                Xor_R(PeekByte(offset));
                                PC++;
                                break;

                            case 0xB4:  //OR IXH
                                if (loggingEnabled) Log("OR IXH");
                                tstates += 4;
                                Or_R(IXH);
                                break;

                            case 0xB5:  //OR IXL
                                if (loggingEnabled) Log("OR IXL");
                                tstates += 4;
                                Or_R(IXL);
                                break;

                            case 0xB6:  //OR (IX + d)
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("OR (IX + {0:X})", disp));
                                tstates += 19;
                                Or_R(PeekByte(offset));
                                PC++;
                                break;
                            #endregion

                            #region Compare operator
                            case 0xBC:  //CP IXH
                                if (loggingEnabled) Log("CP IXH");
                                tstates += 4;
                                Cp_R(IXH);
                                break;

                            case 0xBD:  //CP IXL
                                if (loggingEnabled) Log("CP IXL");
                                tstates += 4;
                                Cp_R(IXL);
                                break;

                            case 0xBE:  //CP (IX + d)
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("CP (IX + {0:X})", disp));
                                tstates += 19;
                                Cp_R(PeekByte(offset));
                                PC++;
                                break;
                            #endregion

                            #region Load instructions
                            case 0x21:  //LD IX, nn
                                if (loggingEnabled) Log(string.Format("LD IX, {0,-6:X}", PeekWord(PC)));
                                tstates += 14;
                                IX = PeekWord(PC);
                                PC += 2;
                                break;

                            case 0x22:  //LD (nn), IX
                                if (loggingEnabled) Log(string.Format("LD ({0:X}), IX", PeekWord(PC)));
                                tstates += 20;
                                PokeWord(PeekWord(PC), IX);
                                PC += 2;
                                break;

                            case 0x26:  //LD IXH, n
                                if (loggingEnabled) Log(string.Format("LD IXH, {0:X}", PeekByte(PC)));
                                tstates += 7;
                                IXH = PeekByte(PC);
                                PC++;
                                break;

                            case 0x2A:  //LD IX, (nn)
                                if (loggingEnabled) Log(string.Format("LD IX, ({0:X})", PeekWord(PC)));
                                tstates += 20;
                                IX = PeekWord(PeekWord(PC));
                                PC += 2;
                                break;

                            case 0x2E:  //LD IXL, n
                                if (loggingEnabled) Log(string.Format("LD IXL, {0:X}", PeekByte(PC)));
                                tstates += 7;
                                IXL = PeekByte(PC);
                                PC++;
                                break;

                            case 0x36:  //LD (IX + d), n
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IX + {0:X}), {1,-6:X}", disp, PeekByte(PC + 1)));
                                tstates += 19;
                                PokeByte(offset, PeekByte(PC + 1));
                                PC += 2;
                                break;

                            case 0x44:  //LD B, IXH
                                if (loggingEnabled) Log("LD B, IXH");
                                tstates += 4;
                                B = IXH;
                                break;

                            case 0x45:  //LD B, IXL
                                if (loggingEnabled) Log("LD B, IXL");
                                tstates += 4;
                                B = IXL;
                                break;

                            case 0x46:  //LD B, (IX + d)
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD B, (IX + {0:X})", disp));
                                tstates += 19;
                                B = PeekByte(offset);
                                PC++;
                                break;

                            case 0x4C:  //LD C, IXH
                                if (loggingEnabled) Log("LD C, IXH");
                                tstates += 4;
                                C = IXH;
                                break;

                            case 0x4D:  //LD C, IXL
                                if (loggingEnabled) Log("LD C, IXL");
                                tstates += 4;
                                C = IXL;
                                break;

                            case 0x4E:  //LD C, (IX + d)
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD C, (IX + {0:X})", disp));
                                tstates += 19;
                                C = PeekByte(offset);
                                PC++;
                                break;

                            case 0x54:  //LD D, IXH
                                if (loggingEnabled) Log("LD D, IXH");
                                tstates += 4;
                                D = IXH;
                                break;

                            case 0x55:  //LD D, IXL
                                if (loggingEnabled) Log("LD D, IXL");
                                tstates += 4;
                                D = IXL;
                                break;

                            case 0x56:  //LD D, (IX + d)
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD D, (IX + {0:X})", disp));
                                tstates += 19;
                                D = PeekByte(offset);
                                PC++;
                                break;

                            case 0x5C:  //LD E, IXH
                                if (loggingEnabled) Log("LD E, IXH");
                                tstates += 4;
                                E = IXH;
                                break;

                            case 0x5D:  //LD E, IXL
                                if (loggingEnabled) Log("LD E, IXL");
                                tstates += 4;
                                E = IXL;
                                break;

                            case 0x5E:  //LD E, (IX + d)
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD E, (IX + {0:X})", disp));
                                tstates += 19;
                                E = PeekByte(offset);
                                PC++;
                                break;

                            case 0x60:  //LD IXH, B
                                if (loggingEnabled) Log("LD IXH, B");
                                tstates += 4;
                                IXH = B;
                                break;

                            case 0x61:  //LD IXH, C
                                if (loggingEnabled) Log("LD IXH, C");
                                tstates += 4;
                                IXH = C;
                                break;

                            case 0x62:  //LD IXH, D
                                if (loggingEnabled) Log("LD IXH, D");
                                tstates += 4;
                                IXH = D;
                                break;

                            case 0x63:  //LD IXH, E
                                if (loggingEnabled) Log("LD IXH, E");
                                tstates += 4;
                                IXH = E;
                                break;

                            case 0x64:  //LD IXH, IXH
                                if (loggingEnabled) Log("LD IXH, IXH");
                                tstates += 4;
                                IXH = IXH;
                                break;

                            case 0x65:  //LD IXH, IXL
                                if (loggingEnabled) Log("LD IXH, IXL");
                                tstates += 4;
                                IXH = IXL;
                                break;

                            case 0x66:  //LD H, (IX + d)
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD H, (IX + {0:X})", disp));
                                tstates += 19;
                                H = PeekByte(offset);
                                PC++;
                                break;

                            case 0x67:  //LD IXH, A
                                if (loggingEnabled) Log("LD IXH, A");
                                tstates += 4;
                                IXH = A;
                                break;

                            case 0x68:  //LD IXL, B
                                if (loggingEnabled) Log("LD IXL, B");
                                tstates += 4;
                                IXL = B;
                                break;

                            case 0x69:  //LD IXL, C
                                if (loggingEnabled) Log("LD IXL, C");
                                tstates += 4;
                                IXL = C;
                                break;

                            case 0x6A:  //LD IXL, D
                                if (loggingEnabled) Log("LD IXL, D");
                                tstates += 4;
                                IXL = D;
                                break;

                            case 0x6B:  //LD IXL, E
                                if (loggingEnabled) Log("LD IXL, E");
                                tstates += 4;
                                IXL = E;
                                break;

                            case 0x6C:  //LD IXL, IXH
                                if (loggingEnabled) Log("LD IXL, IXH");
                                tstates += 4;
                                IXL = IXH;
                                break;

                            case 0x6D:  //LD IXL, IXL
                                if (loggingEnabled) Log("LD IXL, IXL");
                                tstates += 4;
                                IXL = IXL;
                                break;

                            case 0x6E:  //LD L, (IX + d)
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD L, (IX + {0:X})", disp));
                                tstates += 19;
                                L = PeekByte(offset);
                                PC++;
                                break;

                            case 0x6F:  //LD IXL, A
                                if (loggingEnabled) Log("LD IXL, A");
                                tstates += 4;
                                IXL = A;
                                break;

                            case 0x70:  //LD (IX + d), B
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IX + {0:X}), B", disp));
                                tstates += 19;
                                PokeByte(offset, B);
                                PC++;
                                break;

                            case 0x71:  //LD (IX + d), C
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IX + {0:X}), C", disp));
                                tstates += 19;
                                PokeByte(offset, C);
                                PC++;
                                break;

                            case 0x72:  //LD (IX + d), D
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IX + {0:X}), D", disp));
                                tstates += 19;
                                PokeByte(offset, D);
                                PC++;
                                break;

                            case 0x73:  //LD (IX + d), E
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IX + {0:X}), E", disp));
                                tstates += 19;
                                PokeByte(offset, E);
                                PC++;
                                break;

                            case 0x74:  //LD (IX + d), H
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IX + {0:X}), H", disp));
                                tstates += 19;
                                PokeByte(offset, H);
                                PC++;
                                break;

                            case 0x75:  //LD (IX + d), L
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IX + {0:X}), L", disp));
                                tstates += 19;
                                PokeByte(offset, L);
                                PC++;
                                break;

                            case 0x77:  //LD (IX + d), A
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IX + {0:X}), A", disp));
                                tstates += 19;
                                PokeByte(offset, A);
                                PC++;
                                break;

                            case 0x7C:  //LD A, IXH
                                if (loggingEnabled) Log("LD A, IXH");
                                tstates += 4;
                                A = IXH;
                                break;

                            case 0x7D:  //LD A, IXL
                                if (loggingEnabled) Log("LD A, IXL");
                                tstates += 4;
                                A = IXL;
                                break;

                            case 0x7E:  //LD A, (IX + d)
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD A, (IX + {0:X})", disp));
                                tstates += 19;
                                A = PeekByte(offset);
                                PC++;
                                break;

                            case 0xF9:  //LD SP, IX
                                if (loggingEnabled) Log("LD SP, IX");
                                tstates += 10;
                                SP = IX;
                                break;
                            #endregion

                            #region All DDCB instructions
                            case 0xCB:
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IX + disp; //The displacement required
                                PC++;
                                opcode = PeekByte(PC);      //The opcode comes after the offset byte!
                                PC++;
                                switch (opcode)
                                {
                                    case 0x06:  //RLC (IX + d)
                                        if (loggingEnabled) Log(string.Format("RLC (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Rlc_R(PeekByte(offset)));
                                        break;

                                    case 0x0E:  //RRC (IX + d)
                                        if (loggingEnabled) Log(string.Format("RRC (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Rrc_R(PeekByte(offset)));
                                        break;

                                    case 0x16:  //RL (IX + d)
                                        if (loggingEnabled) Log(string.Format("RL (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Rl_R(PeekByte(offset)));
                                        break;

                                    case 0x1E:  //RR (IX + d)
                                        if (loggingEnabled) Log(string.Format("RR (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Rr_R(PeekByte(offset)));
                                        break;

                                    case 0x26:  //SLA (IX + d)
                                        if (loggingEnabled) Log(string.Format("SLA (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Sla_R(PeekByte(offset)));
                                        break;

                                    case 0x2E:  //SRA (IX + d)
                                        if (loggingEnabled) Log(string.Format("SRA (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Sra_R(PeekByte(offset)));
                                        break;

                                    case 0x36:  //SLL (IX + d)
                                        if (loggingEnabled) Log(string.Format("SLL (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Sll_R(PeekByte(offset)));
                                        break;

                                    case 0x3E:  //SRL (IX + d)
                                        if (loggingEnabled) Log(string.Format("SRL (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Srl_R(PeekByte(offset)));
                                        break;

                                    case 0x46:  //BIT 0, (IX + d)
                                        if (loggingEnabled) Log(string.Format("BIT 0, (IX + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(0, PeekByte(offset));
                                        break;

                                    case 0x4E:  //BIT 1, (IX + d)
                                        if (loggingEnabled) Log(string.Format("BIT 1, (IX + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(1, PeekByte(offset));
                                        break;

                                    case 0x56:  //BIT 2, (IX + d)
                                        if (loggingEnabled) Log(string.Format("BIT 2, (IX + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(2, PeekByte(offset));
                                        break;

                                    case 0x5E:  //BIT 3, (IX + d)
                                        if (loggingEnabled) Log(string.Format("BIT 3, (IX + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(3, PeekByte(offset));
                                        break;

                                    case 0x66:  //BIT 4, (IX + d)
                                        if (loggingEnabled) Log(string.Format("BIT 4, (IX + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(4, PeekByte(offset));
                                        break;

                                    case 0x6E:  //BIT 5, (IX + d)
                                        if (loggingEnabled) Log(string.Format("BIT 5, (IX + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(5, PeekByte(offset));
                                        break;

                                    case 0x76:  //BIT 6, (IX + d)
                                        if (loggingEnabled) Log(string.Format("BIT 6, (IX + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(6, PeekByte(offset));
                                        break;

                                    case 0x7E:  //BIT 7, (IX + d)
                                        if (loggingEnabled) Log(string.Format("BIT 7, (IX + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(7, PeekByte(offset));
                                        break;

                                    case 0x86:  //RES 0, (IX + d)
                                        if (loggingEnabled) Log(string.Format("RES 0, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(0, PeekByte(offset)));
                                        break;

                                    case 0x8E:  //RES 1, (IX + d)
                                        if (loggingEnabled) Log(string.Format("RES 1, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(1, PeekByte(offset)));
                                        break;

                                    case 0x96:  //RES 2, (IX + d)
                                        if (loggingEnabled) Log(string.Format("RES 2, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(2, PeekByte(offset)));
                                        break;

                                    case 0x9E:  //RES 3, (IX + d)
                                        if (loggingEnabled) Log(string.Format("RES 3, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(3, PeekByte(offset)));
                                        break;

                                    case 0xA6:  //RES 4, (IX + d)
                                        if (loggingEnabled) Log(string.Format("RES 4, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(4, PeekByte(offset)));
                                        break;

                                    case 0xAE:  //RES 5, (IX + d)
                                        if (loggingEnabled) Log(string.Format("RES 5, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(5, PeekByte(offset)));
                                        break;

                                    case 0xB6:  //RES 6, (IX + d)
                                        if (loggingEnabled) Log(string.Format("RES 6, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(6, PeekByte(offset)));
                                        break;

                                    case 0xBE:  //RES 7, (IX + d)
                                        if (loggingEnabled) Log(string.Format("RES 7, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(7, PeekByte(offset)));
                                        break;

                                    case 0xC6:  //SET 0, (IX + d)
                                        if (loggingEnabled) Log(string.Format("SET 0, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(0, PeekByte(offset)));
                                        break;

                                    case 0xCE:  //SET 1, (IX + d)
                                        if (loggingEnabled) Log(string.Format("SET 1, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(1, PeekByte(offset)));
                                        break;

                                    case 0xD6:  //SET 2, (IX + d)
                                        if (loggingEnabled) Log(string.Format("SET 2, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(2, PeekByte(offset)));
                                        break;

                                    case 0xDE:  //SET 3, (IX + d)
                                        if (loggingEnabled) Log(string.Format("SET 3, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(3, PeekByte(offset)));
                                        break;

                                    case 0xE6:  //SET 4, (IX + d)
                                        if (loggingEnabled) Log(string.Format("SET 4, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(4, PeekByte(offset)));
                                        break;

                                    case 0xEE:  //SET 5, (IX + d)
                                        if (loggingEnabled) Log(string.Format("SET 5, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(5, PeekByte(offset)));
                                        break;

                                    case 0xF6:  //SET 6, (IX + d)
                                        if (loggingEnabled) Log(string.Format("SET 6, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(6, PeekByte(offset)));
                                        break;

                                    case 0xFE:  //SET 7, (IX + d)
                                        if (loggingEnabled) Log(string.Format("SET 7, (IX + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(7, PeekByte(offset)));
                                        break;

                                    default:
                                        String msg = "ERROR: Could not handle DDCB " + opcode.ToString();
                                        MessageBox.Show(msg, "Opcode handler",
                                                    MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                                     
                                        break;

                                }
                                break;
                            #endregion

                            #region Pop/Push instructions
                            case 0xE1:  //POP IX
                                if (loggingEnabled) Log("POP IX");
                                tstates += 14;
                                IX = PeekWord(SP);
                                SP = (SP + 2) & 0xffff;
                                break;

                            case 0xE5:  //PUSH IX
                                if (loggingEnabled) Log("PUSH IX");
                                tstates += 15;
                                SP = (SP - 2) & 0xffff;
                                PokeWord(SP, IX);
                                break;
                            #endregion

                            #region Exchange instruction
                            case 0xE3:  //EX (SP), IX
                                if (loggingEnabled) Log("EX (SP), IX");
                                tstates += 23;
                                int tempreg = IX;
                                IX = PeekWord(SP);
                                PokeWord(SP, tempreg);
                                break;
                            #endregion

                            #region Jump instruction
                            case 0xE9:  //JP (IX)
                                if (loggingEnabled) Log("JP (IX)");
                                tstates += 8;
                                PC = IX;
                                break;
                            #endregion

                            default:
                                String msg2 = "ERROR: Could not handle DD " + opcode.ToString();
                                MessageBox.Show(msg2, "Opcode handler",
                                            MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                                break;

                        }
                        break;
                    #endregion

                    #region Opcodes with ED prefix
                    case 0xED:
                        switch (opcode = FetchInstruction())
                        {
                            case 0x40: //IN B, (C)
                                if (loggingEnabled) Log("IN B, (C)");
                                tstates += 12;
                                B = In();
                                break;

                            case 0x41: //Out (C), B
                                if (loggingEnabled) Log("OUT (C), B");
                                tstates += 12;
                                Out(BC, B);
                                break;

                            case 0x42:  //SBC HL, BC
                                if (loggingEnabled) Log("SBC HL, BC");
                                tstates += 15;
                                Sbc_RR(BC);
                                break;

                            case 0x43:  //LD (nn), BC
                                if (loggingEnabled) Log(String.Format("LD ({0:X}), BC", PeekWord(PC)));
                                tstates += 20;
                                PokeWord(PeekWord(PC), BC);
                                PC += 2;
                                break;

                            case 0x44:  //NEG
                                if (loggingEnabled) Log("NEG");
                                tstates += 8;
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x45:  //RETN
                                if (loggingEnabled) Log("RET N");
                                tstates += 14;
                                PC = PeekWord(SP);
                                SP += 2;
                                break;

                            case 0x46:  //IM0
                                if (loggingEnabled) Log("IM 0");
                                tstates += 8;
                                interruptMode = 0;
                                break;
                                
                            case 0x47:  //LD I, A
                                if (loggingEnabled) Log("LD I, A");
                                tstates += 9;
                                I = A;
                                break;

                            case 0x48: //IN C, (C)
                                if (loggingEnabled) Log("IN C, (C)");
                                tstates += 12;
                                C = In();
                                break;

                            case 0x49: //Out (C), C
                                if (loggingEnabled) Log("OUT (C), C");
                                tstates += 12;
                                Out(BC, C);
                                break;

                            case 0x4A:  //ADC HL, BC
                                if (loggingEnabled) Log("ADC HL, BC");
                                tstates += 15;
                                Adc_RR(BC);
                                break;
                              
                            case 0x4B:  //LD BC, (nn)
                                if (loggingEnabled) Log(String.Format("LD BC, ({0:X})", PeekWord(PC)));
                                tstates += 20;
                                BC = PeekWord(PeekWord(PC));
                                PC += 2;
                                break;

                            case 0x4C:  //NEG
                                if (loggingEnabled) Log("NEG");
                                tstates += 8;
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x4D:  //RETI
                                if (loggingEnabled) Log("RETI");
                                tstates += 14;
                                PC = PeekWord(SP);
                                SP += 2;
                                break;

                            case 0x4F:  //LD R, A
                                if (loggingEnabled) Log("LD R, A");
                                tstates += 9;
                                R = (R & 0x80) | A; //Preserve the 7th bit!
                                break;

                            case 0x50: //IN D, (C)
                                if (loggingEnabled) Log("IN D, (C)");
                                tstates += 12;
                                D = In();
                                break;

                            case 0x51: //Out (C), D
                                if (loggingEnabled) Log("OUT (C), D");
                                tstates += 12;
                                Out(BC, D);
                                break;

                            case 0x52:  //SBC HL, DE
                                if (loggingEnabled) Log("SBC HL, DE");
                                tstates += 15;
                                Sbc_RR(DE);
                                break;

                            case 0x53:  //LD (nn), DE
                                if (loggingEnabled) Log(String.Format("LD ({0:X}), DE", PeekWord(PC)));
                                tstates += 20;
                                PokeWord(PeekWord(PC), DE);
                                PC += 2;
                                break;

                            case 0x54:  //NEG
                                if (loggingEnabled) Log("NEG");
                                tstates += 8;
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x55:  //RETN
                                if (loggingEnabled) Log("RETN");
                                tstates += 14;
                                PC = PeekWord(SP);
                                SP += 2;
                                break;

                            case 0x56:  //IM1
                                if (loggingEnabled) Log("IM 1");
                                tstates += 8;
                                interruptMode = 1;
                                break;

                            case 0x57:  //LD A, I
                                if (loggingEnabled) Log("LD A, I");
                                tstates += 9;
                                A = I;

                                SetNeg(false);
                                SetHalf(false);
                                SetParity(IFF2);
                                SetSign((A & F_SIGN) != 0);
                                SetZero((A & F_ZERO) != 0);
                                break;
                        
                            case 0x58: //IN E, (C)
                                if (loggingEnabled) Log("IN E, (C)");
                                tstates += 12;
                                E = In();
                                break;

                            case 0x59: //Out (C), E
                                if (loggingEnabled) Log("OUT (C), E");
                                tstates += 12;
                                Out(BC, E);
                                break;

                            case 0x5A:  //ADC HL, DE
                                if (loggingEnabled) Log("ADC HL, DE");
                                tstates += 15;
                                Adc_RR(DE);
                                break;

                            case 0x5B:  //LD DE, (nn)
                                if (loggingEnabled) Log(String.Format("LD DE, ({0:X})", PeekWord(PC)));
                                tstates += 20;
                                DE = PeekWord(PeekWord(PC));
                                PC += 2;
                                break;

                            case 0x5C:  //NEG
                                if (loggingEnabled) Log("NEG");
                                tstates += 8;
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x5D:  //RETN
                                if (loggingEnabled) Log("RETN");
                                tstates += 14;
                                PC = PeekWord(SP);
                                SP += 2;
                                break;

                            case 0x5E:  //IM2
                                if (loggingEnabled) Log("IM 2");
                                tstates += 8;
                                interruptMode = 2;
                                break;

                            case 0x5F:  //LD A, R
                                if (loggingEnabled) Log("LD A, R");
                                tstates += 9;
                                A = R;

                                SetNeg(false);
                                SetHalf(false);
                                SetParity(IFF2);
                                SetSign((A & F_SIGN) != 0);
                                SetZero((A & F_ZERO) != 0);
                                break;

                            case 0x60: //IN H, (C)
                                if (loggingEnabled) Log("IN H, (C)");
                                tstates += 12;
                                H = In();
                                break;

                            case 0x61: //Out (C), H
                                if (loggingEnabled) Log("OUT (C), H");
                                tstates += 12;
                                Out(BC, H);
                                break;

                            case 0x62:  //SBC HL, HL
                                if (loggingEnabled) Log("SBC HL, HL");
                                tstates += 15;
                                Sbc_RR(HL);
                                break;

                            case 0x63:  //LD (nn), HL
                                if (loggingEnabled) Log(String.Format("LD ({0:X}), HL", PeekWord(PC)));
                                tstates += 20;
                                PokeWord(PeekWord(PC), HL);
                                PC += 2;
                                break;

                            case 0x64:  //NEG
                                if (loggingEnabled) Log("NEG");
                                tstates += 8;
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x65:  //RETN
                                if (loggingEnabled) Log("RETN");
                                tstates += 14;
                                PC = PeekWord(SP);
                                SP += 2;
                                break;

                            case 0x67:  //RRD
                                if (loggingEnabled) Log("RRD");
                                tstates += 18;
                                temp = A;
                                int data = PeekByte(HL);
                                A = (A & 0xf0) | (data & 0x0f);
                                data = (data >> 4) | (temp << 4);
                                PokeByte(HL, data);

                                SetSign((A & F_SIGN) != 0);
                                SetF3((A & F_3) != 0);
                                SetF5((A & F_5) != 0);
                                SetZero(A == 0);
                                //SetParity(IFF2()); // Not sure what to do here!
                                SetHalf(false);
                                SetNeg(false);
                                break;

                            case 0x68: //IN L, (C)
                                if (loggingEnabled) Log("IN L, (C)");
                                tstates += 12;
                                L = In();
                                break;

                            case 0x69: //Out (C), L
                                if (loggingEnabled) Log("OUT (C), L");
                                tstates += 12;
                                Out(BC, L);
                                break;

                            case 0x6A:  //ADC HL, HL
                                if (loggingEnabled) Log("ADC HL, HL");
                                tstates += 15;
                                Adc_RR(HL);
                                break;

                            case 0x6B:  //LD HL, (nn)
                                if (loggingEnabled) Log(String.Format("LD HL, ({0:X})", PeekWord(PC)));
                                tstates += 20;
                                HL = PeekWord(PeekWord(PC));
                                PC += 2;
                                break;

                            case 0x6C:  //NEG
                                if (loggingEnabled) Log("NEG");
                                tstates += 8;
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x6D:  //RETN
                                if (loggingEnabled) Log("RETN");
                                tstates += 14;
                                PC = PeekWord(SP);
                                SP += 2;
                                break;

                            case 0x6F:  //RLD
                                if (loggingEnabled) Log("RLD");
                                tstates += 18;
                                temp = A;
                                data = PeekByte(HL);
                                A = (A & 0xf0) | (data >> 4);
                                data = (data << 4) | (temp & 0x0f);
                                PokeByte(HL, data & 0xff);

                                SetSign((A & F_SIGN) != 0);
                                SetF3((A & F_3) != 0);
                                SetF5((A & F_5) != 0);
                                SetZero(A == 0);
                                //SetParity(IFF2()); // Not sure what to do here!
                                SetHalf(false);
                                SetNeg(false);
                                break;
                                   
                            case 0x70:  //IN (C)
                                if (loggingEnabled) Log("IN (C)");
                                tstates += 12;
                                In();
                                break;

                            case 0x72:  //SBC HL, SP
                                if (loggingEnabled) Log("SBC HL, SP");
                                tstates += 15;
                                Sbc_RR(SP);
                                break;

                            case 0x73:  //LD (nn), SP
                                if (loggingEnabled) Log(String.Format("LD ({0:X}), SP", PeekWord(PC)));
                                tstates += 20;
                                PokeWord(PeekWord(PC), SP);
                                PC += 2;
                                break;

                            case 0x74:  //NEG
                                if (loggingEnabled) Log("NEG");
                                tstates += 8;
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x75:  //RETN
                                if (loggingEnabled) Log("RETN");
                                tstates += 14;
                                PC = PeekWord(SP);
                                SP += 2;
                                break;

                            case 0x76:  //IM 1
                                if (loggingEnabled) Log("IM 1");
                                tstates += 8;
                                interruptMode = 1;
                                break;


                            case 0x78:  //IN A, (C)
                                if (loggingEnabled) Log("IN A, (C)");
                                tstates += 12;
                                A = In();
                                break;

                            case 0x79: //Out (C), A
                                if (loggingEnabled) Log("OUT (C), A");
                                tstates += 12;
                                Out(BC, A);
                                break;

                            case 0x7A:  //ADC HL, SP
                                if (loggingEnabled) Log("ADC HL, SP");
                                tstates += 15;
                                Adc_RR(SP);
                                break;

                            case 0x7B:  //LD SP, (nn)
                                if (loggingEnabled) Log(String.Format("LD SP, ({0:X})", PeekWord(PC)));
                                tstates += 20;
                                SP = PeekWord( PeekWord(PC));
                                PC += 2;
                                break;

                            case 0x7C:  //NEG
                                if (loggingEnabled) Log("NEG");
                                tstates += 8;
                                temp = A;
                                A = 0;
                                Sub_R(temp); //Sets flags correctly for NEG as well!
                                break;

                            case 0x7D:  //RETN
                                if (loggingEnabled) Log("RETN");
                                tstates += 14;
                                PC = PeekWord(SP);
                                SP += 2;
                                break;

                            case 0x7E:  //IM 2
                                if (loggingEnabled) Log("IM 2");
                                tstates += 8;
                                interruptMode = 2;
                                break;

                            case 0xA0:  //LDI
                                if (loggingEnabled) Log("LDI");
                                tstates += 16;
                                PokeByte(DE, PeekByte(HL));
                                HL++;
                                DE++;
                                BC--;
                                SetNeg(false);
                                SetHalf(false);
                                SetParity(BC != 0);
                                break;

                            case 0xA1:  //CPI
                                if (loggingEnabled) Log("CPI");
                                tstates += 16;
                                Cp_R(PeekByte(HL));
                                HL++;
                                BC--;
                                SetParity(BC != 0);
                                break;

                            case 0xA2:  //INI
                                if (loggingEnabled) Log("INI");
                                tstates += 16;
                                int result = In();
                                PokeByte(HL, result);
                                B--;
                                HL++;
                                SetNeg(true);
                                SetZero(B == 0);
                                break;

                            case 0xA3:  //OUTI
                                if (loggingEnabled) Log("OUTI");
                                tstates += 16;
                                B--;
                                Out(BC, PeekByte(HL));
                                HL++;

                                SetNeg(true);
                                SetZero(B == 0);
                                break;

                            case 0xA8:  //LDD
                                if (loggingEnabled) Log("LDD");
                                tstates += 16;
                                PokeByte(DE, PeekByte(HL));
                                HL--;
                                DE--;
                                BC--;
                                SetNeg(false);
                                SetHalf(false);
                                SetParity(BC != 0);
                                break;

                            case 0xA9:  //CPD
                                if (loggingEnabled) Log("CPD");
                                tstates += 16;
                                Cp_R(PeekByte(HL));
                                HL--;
                                BC--;
                                SetParity(BC != 0);
                                break;

                            case 0xAA:  //IND
                                if (loggingEnabled) Log("IND");
                                tstates += 16;
                                result = In();
                                PokeByte(HL, result);
                                B--;
                                HL--;
                                SetNeg(true);
                                SetZero(B == 0);
                                break;

                            case 0xAB:  //OUTD
                                if (loggingEnabled) Log("OUTD");
                                tstates += 16;
                                B--;
                                Out(BC, PeekByte(HL));
                                HL--;

                                SetNeg(true);
                                SetZero(B == 0);
                                break;

                            case 0xB0:  //LDIR
                                if (loggingEnabled) Log("LDIR");
                                tstates += 16;
                                PokeByte(DE, PeekByte(HL));
                                HL++;
                                DE++;
                                BC--;
                                SetNeg(false);
                                SetHalf(false);
                                SetParity(false); //Needs to be checked!
                                if (BC != 0)
                                {
                                    PC -= 2;
                                    tstates += 5; //tstates = 21 for jump
                                }
                                break;

                            case 0xB1:  //CPIR
                                if (loggingEnabled) Log("CPIR");
                                tstates += 16;
                                Cp_R(PeekByte(HL));
                                HL++;
                                BC--;
                                SetParity(BC != 0);
                                if ((BC != 0) && ((F & F_ZERO) == 0))
                                {
                                    PC -= 2;
                                    tstates += 5;
                                }
                                break;

                            case 0xB2:  //INIR
                                if (loggingEnabled) Log("INIR");
                                tstates += 16;
                                result = In();
                                PokeByte(HL, result);
                                B--;
                                HL++;
                                SetNeg(true);
                                SetZero(B == 0);
                                if (B != 0)
                                {
                                    PC -= 2;
                                    tstates += 5;
                                }
                                break;

                            case 0xB3:  //OTIR
                                if (loggingEnabled) Log("OTIR");
                                tstates += 16;
                                B--;
                                Out(BC, PeekByte(HL));
                                HL++;

                                SetNeg(true);
                                SetZero(B == 0);
                                if (B != 0)
                                {
                                    PC -= 2;
                                    tstates += 5;
                                }
                                break;

                            case 0xB8:  //LDDR
                                if (loggingEnabled) Log("LDDR");
                                tstates += 16;
                                PokeByte(DE, PeekByte(HL));
                                HL--;
                                DE--;
                                BC--;
                                SetNeg(false);
                                SetHalf(false);
                                SetParity(false); //Needs to be checked!
                                if (BC != 0)
                                {
                                    PC -= 2;
                                    tstates += 5; //tstates = 21 for jump
                                }
                                break;

                            case 0xB9:  //CPDR
                                if (loggingEnabled) Log("CPDR");
                                tstates += 16;
                                tstates += 16;
                                Cp_R(PeekByte(HL));
                                HL--;
                                BC--;
                                SetParity(BC != 0);
                                if ((BC != 0) && ((F & F_ZERO) == 0))
                                {
                                    PC -= 2;
                                    tstates += 5;
                                }
                                break;

                            case 0xBA:  //INDR
                                if (loggingEnabled) Log("INDR");
                                tstates += 16;
                                result = In();
                                PokeByte(HL, result);
                                B--;
                                HL--;
                                SetNeg(true);
                                SetZero(B == 0);
                                if (B != 0)
                                {
                                    PC -= 2;
                                    tstates += 5;
                                }
                                break;

                            case 0xBB:  //OTDR
                                if (loggingEnabled) Log("OTDR");
                                tstates += 16;
                                B--;
                                Out(BC, PeekByte(HL));
                                HL--;

                                SetNeg(true);
                                SetZero(B == 0);
                                if (B != 0)
                                {
                                    PC -= 2;
                                    tstates += 5;
                                }
                                break;

                            default:
                                String msg = "ERROR: Could not handle ED" + opcode.ToString();
                                MessageBox.Show(msg, "Opcode handler",
                                            MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                                break;
                        }
                        break;
                    #endregion

                    #region Opcodes with FD prefix
                        case 0xFD:
                        switch (opcode = FetchInstruction())
                        {
                            #region Addition instructions
                            case 0x09:  //ADD IY, BC
                                if (loggingEnabled) Log("ADD IY, BC");
                                tstates += 15;
                                IY = Add_RR(IY, BC);
                                break;

                            case 0x19:  //ADD IY, DE
                                if (loggingEnabled) Log("ADD IY, DE");
                                tstates += 15;
                                IY = Add_RR(IY, DE);
                                break;

                            case 0x29:  //ADD IY, IY
                                if (loggingEnabled) Log("ADD IY, IY");
                                tstates += 15;
                                IY = Add_RR(IY, IY);
                                break;

                            case 0x39:  //ADD IY, SP
                                if (loggingEnabled) Log("ADD IY, SP");
                                tstates += 15;
                                IY = Add_RR(IY, SP);
                                break;

                            case 0x84:  //ADD A, IYH
                                if (loggingEnabled) Log("ADD A, IYH");
                                tstates += 4;
                                Add_R(IYH);
                                break;

                            case 0x85:  //ADD A, IYL
                                if (loggingEnabled) Log("ADD A, IYL");
                                tstates += 4;
                                Add_R(IYL);
                                break;

                            case 0x86:  //Add A, (IY+d)
                                disp = GetDisplacement(PeekByte(PC));

                                int offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("ADD A, (IY + {0:X})", disp));
                                tstates += 19;
                                Add_R(PeekByte(offset));
                                PC++;
                                break;

                            case 0x8C:  //ADC A, IYH
                                if (loggingEnabled) Log("ADC A, IYH");
                                tstates += 4;
                                Adc_R(IYH);
                                break;

                            case 0x8D:  //ADX A, IYL
                                if (loggingEnabled) Log("ADC A, IYL");
                                tstates += 4;
                                Adc_R(IYL);
                                break;

                            case 0x8E: //ADC A, (IY+d) 
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("ADC A, (IY + {0:X})", disp));
                                tstates += 19;
                                Adc_R(PeekByte(offset));
                                PC++;
                                break;
                            #endregion

                            #region Subtraction instructions
                            case 0x94:  //SUB A, IYH
                                if (loggingEnabled) Log("SUB A, IYH");
                                tstates += 4;
                                Sub_R(IYH);
                                break;

                            case 0x95:  //SUB A, IYL
                                if (loggingEnabled) Log("SUB A, IYL");
                                tstates += 4;
                                Sub_R(IYL);
                                break;

                            case 0x96:  //SUB (IY + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("SUB (IY + {0:X})", disp));
                                tstates += 19;
                                Sub_R(PeekByte(offset));
                                PC++;
                                break;

                            case 0x9C:  //SBC A, IYH
                                if (loggingEnabled) Log("SBC A, IYH");
                                tstates += 4;
                                Sbc_R(IYH);
                                break;

                            case 0x9D:  //SBC A, IYL
                                if (loggingEnabled) Log("SBC A, IYL");
                                tstates += 4;
                                Sbc_R(IYL);
                                break;

                            case 0x9E:  //SBC A, (IY + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("SBC A, (IY + {0:X})", disp));
                                tstates += 19;
                                Sbc_R(PeekByte(offset));
                                PC++;
                                break;
                            #endregion

                            #region Increment/Decrements
                            case 0x23:  //INC IY
                                if (loggingEnabled) Log("INC IY");
                                tstates += 10;
                                IY++;
                                break;

                            case 0x24:  //INC IYH
                                if (loggingEnabled) Log("INC IYH");
                                tstates += 4;
                                IYH = Inc(IYH);
                                break;

                            case 0x25:  //DEC IYH
                                if (loggingEnabled) Log("DEC IYH");
                                tstates += 4;
                                IYH = Dec(IYH);
                                break;

                            case 0x2B:  //DEC IY
                                if (loggingEnabled) Log("DEC IY");
                                tstates += 10;
                                IY--;
                                break;

                            case 0x2C:  //INC IYL
                                if (loggingEnabled) Log("INC IYL");
                                tstates += 4;
                                IYL = Inc(IYL);
                                break;

                            case 0x2D:  //DEC IYL
                                if (loggingEnabled) Log("DEC IYL");
                                tstates += 4;
                                IYL = Dec(IYL);
                                break;

                            case 0x34:  //INC (IY + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("INC (IY + {0:X})", disp));
                                tstates += 23;
                                PokeByte(offset, Inc(PeekByte(offset)));
                                PC++;
                                break;

                            case 0x35:  //DEC (IY + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("DEC (IY + {0:X})", disp));
                                tstates += 23;
                                PokeByte(offset, Dec(PeekByte(offset)));
                                PC++;
                                break;
                            #endregion

                            #region Bitwise operators

                            case 0xA4:  //AND IYH
                                if (loggingEnabled) Log("AND IYH");
                                tstates += 4;
                                And_R(IYH);
                                break;

                            case 0xA5:  //AND IYL
                                if (loggingEnabled) Log("AND IYL");
                                tstates += 4;
                                And_R(IYL);
                                break;

                            case 0xA6:  //AND (IY + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("AND (IY + {0:X})", disp));
                                tstates += 19;
                                And_R(PeekByte(offset));
                                PC++;
                                break;

                            case 0xAC:  //XOR IYH
                                if (loggingEnabled) Log("XOR IYH");
                                tstates += 4;
                                Xor_R(IYH);
                                break;

                            case 0xAD:  //XOR IYL
                                if (loggingEnabled) Log("XOR IYL");
                                tstates += 4;
                                Xor_R(IYL);
                                break;

                            case 0xAE:  //XOR (IY + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("XOR (IY + {0:X})", disp));
                                tstates += 19;
                                Xor_R(PeekByte(offset));
                                PC++;
                                break;

                            case 0xB4:  //OR IYH
                                if (loggingEnabled) Log("OR IYH");
                                tstates += 4;
                                Or_R(IYH);
                                break;

                            case 0xB5:  //OR IYL
                                if (loggingEnabled) Log("OR IYL");
                                tstates += 4;
                                Or_R(IYL);
                                break;

                            case 0xB6:  //OR (IY + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("OR (IY + {0:X})", disp));
                                tstates += 19;
                                Or_R(PeekByte(offset));
                                PC++;
                                break;
                            #endregion

                            #region Compare operator
                            case 0xBC:  //CP IYH
                                if (loggingEnabled) Log("CP IYH");
                                tstates += 4;
                                Cp_R(IYH);
                                break;

                            case 0xBD:  //CP IYL
                                if (loggingEnabled) Log("CP IYL");
                                tstates += 4;
                                Cp_R(IYL);
                                break;

                            case 0xBE:  //CP (IY + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("CP (IY + {0:X})", disp));
                                tstates += 19;
                                Cp_R(PeekByte(offset));
                                PC++;
                                break;
                            #endregion

                            #region Load instructions
                            case 0x21:  //LD IY, nn
                                if (loggingEnabled) Log(string.Format("LD IY, {0,-6:X}", PeekWord(PC)));
                                tstates += 14;
                                IY = PeekWord(PC);
                                PC += 2;
                                break;

                            case 0x22:  //LD (nn), IY
                                if (loggingEnabled) Log(string.Format("LD ({0:X}), IY", PeekWord(PC)));
                                tstates += 20;
                                PokeWord(PeekWord(PC), IY);
                                PC += 2;
                                break;

                            case 0x26:  //LD IYH, n
                                if (loggingEnabled) Log(string.Format("LD IYH, {0:X}", PeekByte(PC)));
                                tstates += 7;
                                IYH = PeekByte(PC);
                                PC++;
                                break;

                            case 0x2A:  //LD IY, (nn)
                                if (loggingEnabled) Log(string.Format("LD IY, ({0:X})", PeekWord(PC)));
                                tstates += 20;
                                IY = PeekWord(PeekWord(PC));
                                PC += 2;
                                break;

                            case 0x2E:  //LD IYL, n
                                if (loggingEnabled) Log(string.Format("LD IYL, {0:X}", PeekByte(PC)));
                                tstates += 7;
                                IYL = PeekByte(PC);
                                PC++;
                                break;

                            case 0x36:  //LD (IY + d), n
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IY + {0:X}), {1,-6:X}", disp, PeekByte(PC + 1)));
                                tstates += 19;
                                PokeByte(offset, PeekByte(PC + 1));
                                PC += 2;
                                break;

                            case 0x44:  //LD B, IYH
                                if (loggingEnabled) Log("LD B, IYH");
                                tstates += 4;
                                B = IYH;
                                break;

                            case 0x45:  //LD B, IYL
                                if (loggingEnabled) Log("LD B, IYL");
                                tstates += 4;
                                B = IYL;
                                break;

                            case 0x46:  //LD B, (IY + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD B, (IY + {0:X})", disp));
                                tstates += 19;
                                B = PeekByte(offset);
                                PC++;
                                break;

                            case 0x4C:  //LD C, IYH
                                if (loggingEnabled) Log("LD C, IYH");
                                tstates += 4;
                                C = IYH;
                                break;

                            case 0x4D:  //LD C, IYL
                                if (loggingEnabled) Log("LD C, IYL");
                                tstates += 4;
                                C = IYL;
                                break;

                            case 0x4E:  //LD C, (IY + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD C, (IY + {0:X})", disp));
                                tstates += 19;
                                C = PeekByte(offset);
                                PC++;
                                break;

                            case 0x54:  //LD D, IYH
                                if (loggingEnabled) Log("LD D, IYH");
                                tstates += 4;
                                D = IYH;
                                break;

                            case 0x55:  //LD D, IYL
                                if (loggingEnabled) Log("LD D, IYL");
                                tstates += 4;
                                D = IYL;
                                break;

                            case 0x56:  //LD D, (IY + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD D, (IY + {0:X})", disp));
                                tstates += 19;
                                D = PeekByte(offset);
                                PC++;
                                break;

                            case 0x5C:  //LD E, IYH
                                if (loggingEnabled) Log("LD E, IYH");
                                tstates += 4;
                                E = IYH;
                                break;

                            case 0x5D:  //LD E, IYL
                                if (loggingEnabled) Log("LD E, IYL");
                                tstates += 4;
                                E = IYL;
                                break;

                            case 0x5E:  //LD E, (IY + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD E, (IY + {0:X})", disp));
                                tstates += 19;
                                E = PeekByte(offset);
                                PC++;
                                break;

                            case 0x60:  //LD IYH, B
                                if (loggingEnabled) Log("LD IYH, B");
                                tstates += 4;
                                IYH = B;
                                break;

                            case 0x61:  //LD IYH, C
                                if (loggingEnabled) Log("LD IYH, C");
                                tstates += 4;
                                IYH = C;
                                break;

                            case 0x62:  //LD IYH, D
                                if (loggingEnabled) Log("LD IYH, D");
                                tstates += 4;
                                IYH = D;
                                break;

                            case 0x63:  //LD IYH, E
                                if (loggingEnabled) Log("LD IYH, E");
                                tstates += 4;
                                IYH = E;
                                break;

                            case 0x64:  //LD IYH, IYH
                                if (loggingEnabled) Log("LD IYH, IYH");
                                tstates += 4;
                                IYH = IYH;
                                break;

                            case 0x65:  //LD IYH, IYL
                                if (loggingEnabled) Log("LD IYH, IYL");
                                tstates += 4;
                                IYH = IYL;
                                break;

                            case 0x66:  //LD H, (IY + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD H, (IY + {0:X})", disp));
                                tstates += 19;
                                H = PeekByte(offset);
                                PC++;
                                break;

                            case 0x67:  //LD IYH, A
                                if (loggingEnabled) Log("LD IYH, A");
                                tstates += 4;
                                IYH = A;
                                break;

                            case 0x68:  //LD IYL, B
                                if (loggingEnabled) Log("LD IYL, B");
                                tstates += 4;
                                IYL = B;
                                break;

                            case 0x69:  //LD IYL, C
                                if (loggingEnabled) Log("LD IYL, C");
                                tstates += 4;
                                IYL = C;
                                break;

                            case 0x6A:  //LD IYL, D
                                if (loggingEnabled) Log("LD IYL, D");
                                tstates += 4;
                                IYL = D;
                                break;

                            case 0x6B:  //LD IYL, E
                                if (loggingEnabled) Log("LD IYL, E");
                                tstates += 4;
                                IYL = E;
                                break;

                            case 0x6C:  //LD IYL, IYH
                                if (loggingEnabled) Log("LD IYL, IYH");
                                tstates += 4;
                                IYL = IYH;
                                break;

                            case 0x6D:  //LD IYL, IYL
                                if (loggingEnabled) Log("LD IYL, IYL");
                                tstates += 4;
                                IYL = IYL;
                                break;

                            case 0x6E:  //LD L, (IY + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD L, (IY + {0:X})", disp));
                                tstates += 19;
                                L = PeekByte(offset);
                                PC++;
                                break;

                            case 0x6F:  //LD IYL, A
                                if (loggingEnabled) Log("LD IYL, A");
                                tstates += 4;
                                IYL = A;
                                break;

                            case 0x70:  //LD (IY + d), B
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IY + {0:X}), B", disp));
                                tstates += 19;
                                PokeByte(offset, B);
                                PC++;
                                break;

                            case 0x71:  //LD (IY + d), C
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IY + {0:X}), C", disp));
                                tstates += 19;
                                PokeByte(offset, C);
                                PC++;
                                break;

                            case 0x72:  //LD (IY + d), D
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IY + {0:X}), D", disp));
                                tstates += 19;
                                PokeByte(offset, D);
                                PC++;
                                break;

                            case 0x73:  //LD (IY + d), E
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IY + {0:X}), E", disp));
                                tstates += 19;
                                PokeByte(offset, E);
                                PC++;
                                break;

                            case 0x74:  //LD (IY + d), H
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IY + {0:X}), H", disp));
                                tstates += 19;
                                PokeByte(offset, H);
                                PC++;
                                break;

                            case 0x75:  //LD (IY + d), L
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IY + {0:X}), L", disp));
                                tstates += 19;
                                PokeByte(offset, L);
                                PC++;
                                break;

                            case 0x77:  //LD (IY + d), A
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD (IY + {0:X}), A", disp));
                                tstates += 19;
                                PokeByte(offset, A);
                                PC++;
                                break;

                            case 0x7C:  //LD A, IYH
                                if (loggingEnabled) Log("LD A, IYH");
                                tstates += 4;
                                A = IYH;
                                break;

                            case 0x7D:  //LD A, IYL
                                if (loggingEnabled) Log("LD A, IYL");
                                tstates += 4;
                                A = IYL;
                                break;

                            case 0x7E:  //LD A, (IY + d)
                                disp = GetDisplacement(PeekByte(PC));

                                offset = IY + disp; //The displacement required
                                if (loggingEnabled) Log(string.Format("LD A, (IY + {0:X})", disp));
                                tstates += 19;
                                A = PeekByte(offset);
                                PC++;
                                break;

                            case 0xF9:  //LD SP, IY
                                if (loggingEnabled) Log("LD SP, IY");
                                tstates += 10;
                                SP = IY;
                                break;
                            #endregion

                            #region All FDCB instructions
                            case 0xCB:
                                disp = GetDisplacement(PeekByte(PC));
                                
                                offset = IY + disp; //The displacement required
                                PC++;
                                opcode = PeekByte(PC);      //The opcode comes after the offset byte!
                                PC++;
                                switch (opcode)
                                {
                                    case 0x06:  //RLC (IY + d)
                                        if (loggingEnabled) Log(string.Format("RLC (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Rlc_R(PeekByte(offset)));
                                        break;

                                    case 0x0E:  //RRC (IY + d)
                                        if (loggingEnabled) Log(string.Format("RRC (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Rrc_R(PeekByte(offset)));
                                        break;

                                    case 0x16:  //RL (IY + d)
                                        if (loggingEnabled) Log(string.Format("RL (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Rl_R(PeekByte(offset)));
                                        break;

                                    case 0x1E:  //RR (IY + d)
                                        if (loggingEnabled) Log(string.Format("RR (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Rr_R(PeekByte(offset)));
                                        break;

                                    case 0x26:  //SLA (IY + d)
                                        if (loggingEnabled) Log(string.Format("SLA (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Sla_R(PeekByte(offset)));
                                        break;

                                    case 0x2E:  //SRA (IY + d)
                                        if (loggingEnabled) Log(string.Format("SRA (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Sra_R(PeekByte(offset)));
                                        break;

                                    case 0x36:  //SLL (IY + d)
                                        if (loggingEnabled) Log(string.Format("SLL (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Sll_R(PeekByte(offset)));
                                        break;

                                    case 0x3E:  //SRL (IY + d)
                                        if (loggingEnabled) Log(string.Format("SRL (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Srl_R(PeekByte(offset)));
                                        break;

                                    case 0x46:  //BIT 0, (IY + d)
                                        if (loggingEnabled) Log(string.Format("BIT 0, (IY + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(0, PeekByte(offset));
                                        break;

                                    case 0x4E:  //BIT 1, (IY + d)
                                        if (loggingEnabled) Log(string.Format("BIT 1, (IY + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(1, PeekByte(offset));
                                        break;

                                    case 0x56:  //BIT 2, (IY + d)
                                        if (loggingEnabled) Log(string.Format("BIT 2, (IY + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(2, PeekByte(offset));
                                        break;

                                    case 0x5E:  //BIT 3, (IY + d)
                                        if (loggingEnabled) Log(string.Format("BIT 3, (IY + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(3, PeekByte(offset));
                                        break;

                                    case 0x66:  //BIT 4, (IY + d)
                                        if (loggingEnabled) Log(string.Format("BIT 4, (IY + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(4, PeekByte(offset));
                                        break;

                                    case 0x6E:  //BIT 5, (IY + d)
                                        if (loggingEnabled) Log(string.Format("BIT 5, (IY + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(5, PeekByte(offset));
                                        break;

                                    case 0x76:  //BIT 6, (IY + d)
                                        if (loggingEnabled) Log(string.Format("BIT 6, (IY + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(6, PeekByte(offset));
                                        break;

                                    case 0x7E:  //BIT 7, (IY + d)
                                        if (loggingEnabled) Log(string.Format("BIT 7, (IY + {0:X})", disp));
                                        tstates += 20;
                                        Bit_R(7, PeekByte(offset));
                                        break;

                                    case 0x86:  //RES 0, (IY + d)
                                        if (loggingEnabled) Log(string.Format("RES 0, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(0, PeekByte(offset)));
                                        break;

                                    case 0x8E:  //RES 1, (IY + d)
                                        if (loggingEnabled) Log(string.Format("RES 1, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(1, PeekByte(offset)));
                                        break;

                                    case 0x96:  //RES 2, (IY + d)
                                        if (loggingEnabled) Log(string.Format("RES 2, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(2, PeekByte(offset)));
                                        break;

                                    case 0x9E:  //RES 3, (IY + d)
                                        if (loggingEnabled) Log(string.Format("RES 3, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(3, PeekByte(offset)));
                                        break;

                                    case 0xA6:  //RES 4, (IY + d)
                                        if (loggingEnabled) Log(string.Format("RES 4, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(4, PeekByte(offset)));
                                        break;

                                    case 0xAE:  //RES 5, (IY + d)
                                        if (loggingEnabled) Log(string.Format("RES 5, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(5, PeekByte(offset)));
                                        break;

                                    case 0xB6:  //RES 6, (IY + d)
                                        if (loggingEnabled) Log(string.Format("RES 6, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(6, PeekByte(offset)));
                                        break;

                                    case 0xBE:  //RES 7, (IY + d)
                                        if (loggingEnabled) Log(string.Format("RES 7, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Res_R(7, PeekByte(offset)));
                                        break;

                                    case 0xC6:  //SET 0, (IY + d)
                                        if (loggingEnabled) Log(string.Format("SET 0, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(0, PeekByte(offset)));
                                        break;

                                    case 0xCE:  //SET 1, (IY + d)
                                        if (loggingEnabled) Log(string.Format("SET 1, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(1, PeekByte(offset)));
                                        break;

                                    case 0xD6:  //SET 2, (IY + d)
                                        if (loggingEnabled) Log(string.Format("SET 2, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(2, PeekByte(offset)));
                                        break;

                                    case 0xDE:  //SET 3, (IY + d)
                                        if (loggingEnabled) Log(string.Format("SET 3, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(3, PeekByte(offset)));
                                        break;

                                    case 0xE6:  //SET 4, (IY + d)
                                        if (loggingEnabled) Log(string.Format("SET 4, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(4, PeekByte(offset)));
                                        break;

                                    case 0xEE:  //SET 5, (IY + d)
                                        if (loggingEnabled) Log(string.Format("SET 5, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(5, PeekByte(offset)));
                                        break;

                                    case 0xF6:  //SET 6, (IY + d)
                                        if (loggingEnabled) Log(string.Format("SET 6, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(6, PeekByte(offset)));
                                        break;

                                    case 0xFE:  //SET 7, (IY + d)
                                        if (loggingEnabled) Log(string.Format("SET 7, (IY + {0:X})", disp));
                                        tstates += 23;
                                        PokeByte(offset, Set_R(7, PeekByte(offset)));
                                        break;

                                    default:
                                        String msg = "ERROR: Could not handle DDCB " + opcode.ToString();
                                        MessageBox.Show(msg, "Opcode handler",
                                                    MessageBoxButtons.OKCancel, MessageBoxIcon.Error);

                                        break;

                                }
                                break;
                            #endregion

                            #region Pop/Push instructions
                            case 0xE1:  //POP IY
                                if (loggingEnabled) Log("POP IY");
                                tstates += 14;
                                IY = PeekWord(SP);
                                SP = (SP + 2) & 0xffff;
                                break;

                            case 0xE5:  //PUSH IY
                                if (loggingEnabled) Log("PUSH IY");
                                tstates += 15;
                                SP = (SP - 2) & 0xffff;
                                PokeWord(SP, IY);
                                break;
                            #endregion

                            #region Exchange instruction
                            case 0xE3:  //EX (SP), IY
                                if (loggingEnabled) Log("EX (SP), IY");
                                tstates += 23;
                                int tempreg = IY;
                                IY = PeekWord(SP);
                                PokeWord(SP, tempreg);
                                break;
                            #endregion

                            #region Jump instruction
                            case 0xE9:  //JP (IY)
                                if (loggingEnabled) Log("JP (IY)");
                                tstates += 8;
                                PC = IY;
                                break;
                            #endregion


                            default:
                                String msg2 = "ERROR: Could not handle FD" + opcode.ToString();
                                MessageBox.Show(msg2, "Opcode handler",
                                            MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                                break;

                        }
                        break;
                    #endregion

                    default:
                        String msg3 = "ERROR: Could not handle FD" + opcode.ToString();
                        MessageBox.Show(msg3, "Opcode handler",
                                    MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                        break;
                }
        }
    }

}
