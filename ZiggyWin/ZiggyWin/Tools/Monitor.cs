using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using SpeccyCommon;
using Cpu;

namespace ZeroWin
{
    public partial class Monitor : Form
    {
        public Form1 ziggyWin;
        public Z80 cpu;
        private MemoryViewer memoryViewer = null;
        private Profiler profiler = null;
        private Breakpoints breakpointViewer = null;
        private Registers registerViewer = null;
        private Machine_State machineState = null;
        private CallStackViewer callStackViewer = null;
        private WatchWindow watchWindow = null;

        //Internal set of registers that mimic the z80 reg state
        private ushort pc, bc, de, hl, ir, mp, sp, _bc, _de, _hl, ix, iy, im, af, _af;

        private int tstates;
        private String breakPointStatus = "";
        private bool pauseEmulation = false;
        private int runToCursorAddress = -1;
        private ushort previousPC = 0;
        private int previousTState = 0;
        private bool lastOpcodeWasRET = false; //used for Step Out operation
        private bool isBreakpointWindowOpen = false;
        private bool isProfilerOpen = false;
        private bool isRegistersOpen = false;
        private bool isMemoryViewerOpen = false;
        private bool isMachineStateViewerOpen = false;
        private bool isCallStackViewerOpen = false;
        private bool isWatchWindowOpen = false;
        public bool useHexNumbers = false;
        public bool isTraceOn = false;
        public bool showSysVars = true;

        public Dictionary<int, String> systemVariables = new Dictionary<int, string>() {
            {23552, "KSTATE"}, {23560, "LAST K"}, {23561, "REPDEL"}, {23562, "REPPER"},
            {23563, "DEFADD"}, {23565, "K DATA"}, {23566, "TVDATA"}, {23568, "STRMS"},
            {23606, "CHARS"}, {23608, "RASP"}, {23609, "PIP"}, {23610, "ERR NR"},
            {23611, "FLAGS"}, {23612, "TV FLAG"}, {23613, "ERR SP"}, {23615, "LIST SP"},
            {23617, "MODE"}, {23618, "NEWPPC"}, {23620, "NSPPC"}, {23621, "PPC"},
            {23623, "SUBPPC"}, {23624, "BORDERCR"}, {23625, "E PPC"}, {23627, "VARS"},
            {23629, "DEST"}, {23631, "CHANS"}, {23633, "CURCHL"}, {23635, "PROG"},
            {23637, "NXTLIN"}, {23639, "DATAADD"}, {23641, "E LINE"}, {23643, "K CUR"},
            {23645, "CH ADD"}, {23647, "X PTR"}, {23649, "WORKSP"}, {23651, "STKBOT"},
            {23653, "STKEND"}, {23655, "BREG"}, {23656, "MEM"}, {23658, "FLAGS2"},
            {23659, "DF SZ"}, {23660, "S TOP"}, {23662, "OLDPPC"}, {23664, "OSPPC"},
            {23665, "FLAGX"}, {23666, "STRLEN"}, {23668, "T ADDR"}, {23670, "SEED"},
            {23672, "FRAMES"}, {23675, "UDG"}, {23677, "COORDS"}, {23679, "P POSN"},
            {23680, "PR CC"}, {23682, "ECHO E"}, {23684, "DF CC"}, {23686, "DFCCL"},
            {23688, "S POSN"}, {23690, "SPOSNL"}, {23692, "SCR CT"}, {23693, "ATTR P"},
            {23694, "MASK P"}, {23695, "ATTR T"}, {23696, "MASK T"}, {23697, "P FLAG"},
            {23698, "MEMBOT"}, {23728, "NMIADD"}, {23730, "RAMTOP"}, {23732, "P RAMT"}
        };

        #region Accessors

        public int TStates {
            get {
                return tstates;
            }
            set {
                tstates = value;
                //HexAnd8bitRegUpdate();
            }
        }

        public ushort ValuePC {
            get {
                return pc;
            }
            set {
                pc = value;
                //HexAnd8bitRegUpdate();
            }
        }

        public ushort ValueSP {
            get {
                return sp;
            }
            set {
                sp = value;
                //HexAnd8bitRegUpdate();
            }
        }

        public ushort ValueMP {
            get {
                return mp;
            }
            set {
                mp = value;
                //HexAnd8bitRegUpdate();
            }
        }

        public ushort ValueIM {
            get {
                return im;
            }
            set {
                im = value;
            }
        }

        public ushort ValueAF {
            get {
                return af;
            }
            set {
                af = value;
            }
        }

        public ushort ValueIR {
            get {
                return ir;
            }
            set {
                ir = value;
            }
        }

        public ushort ValueAF_ {
            get {
                return _af;
            }
            set {
                _af = value;
            }
        }

        public ushort ValueHL {
            get {
                return hl;
            }
            set {
                hl = value;
            }
        }

        public ushort ValueBC {
            get {
                return bc;
            }
            set {
                bc = value;
            }
        }

        public ushort ValueDE {
            get {
                return de;
            }
            set {
                de = value;
            }
        }

        public ushort ValueHL_ {
            get {
                return _hl;
            }
            set {
                _hl = value;
            }
        }

        public ushort ValueBC_ {
            get {
                return _bc;
            }
            set {
                _bc = value;
            }
        }

        public ushort ValueDE_ {
            get {
                return _de;
            }
            set {
                _de = value;
            }
        }

        public ushort ValueIX {
            get {
                return ix;
            }
            set {
                ix = value;
            }
        }

        public ushort ValueIY {
            get {
                return iy;
            }
            set {
                iy = value;
            }
        }

        #endregion Accessors

        //The current state of the debugger
        //0 = run, 1 = pause, 2 = single step, 3 = step over, 4 = run to cursor
        public enum MonitorState
        {
            RUN,
            PAUSE,
            STEPIN,
            STEPOVER,
            STEPOUT,
            RUNTOCURSOR,
        }

        public class WatchVariable
        {
            private int address;
            private int data;
            private string label;

            public int Address
            {
                get { return address; }
                set { address = value; }
            }

            public int Data
            {
                get { return data; }
                set { data = value; }
            }

            public string Label
            {
                get { return label; }
                set { label = value;}
            }

            public string AddressAsString
            {
                get
                {
                    if(address < 0)
                        return "-";
                    else
                        return address.ToString();
                }

            }

            public string DataAsString
            {
                get
                {
                    if(data < 0)
                        return "-";
                    else
                        return data.ToString();
                }
            }

            public WatchVariable()
            {
                address = 0;
                data = 0;
                label = "";
            }
        }

        public class BreakPointCondition : Object
        {
            private SPECCY_EVENT condition;
            private int address;
            private int data;

            public string Condition
            {
                get { return Utilities.GetStringFromEnum(condition); }
            }

            public int Address {
                get { return address; }
            }

            public string AddressAsString {
                get {
                    if (address < 0)
                        return "-";
                    else
                        return address.ToString();
                }
            }

            public int Data {
                get { return data; }
            }

            public string DataAsString {
                get {
                    if (data < 0)
                        return "-";
                    else
                        return data.ToString();
                }
            }

            public BreakPointCondition() {
                address = 0;
                data = 0;
            }

            public BreakPointCondition(SPECCY_EVENT cond, int addr, int val)
            {
                condition = cond;
                address = addr;
                data = val;
            }

            public override bool Equals(Object obj) {
                //Check for null and compare run-time types.
                if (obj == null || GetType() != obj.GetType()) return false;
                BreakPointCondition p = (BreakPointCondition)obj;
                return (address == p.address) && (data == p.data) && (condition == p.condition);
            }

            public override int GetHashCode() {
                return address ^ data;
            }
        };

        public MonitorState dbState = MonitorState.PAUSE;

        private PokeMemory pokeMemoryDialog;

        private delegate void AssignToDGVDisassemblyDelegate(DataGridView lst1, DisassemblyList lst2);

        private void AssignToDGVDisassembly(DataGridView lst1, DisassemblyList lst2) {
            if (lst1.InvokeRequired) {
                AssignToDGVDisassemblyDelegate d = new AssignToDGVDisassemblyDelegate(AssignToDGVDisassembly);
                lst1.Invoke(d, new object[] { lst1, lst2 });
            } else {
                lst1.DataSource = lst2;
            }
        }

        private delegate void AssignToDGVMemoryDelegate(DataGridView lst1, BindingList<MemoryUnit> lst2);

        private void AssignToDGVMemory(DataGridView lst1, BindingList<MemoryUnit> lst2) {
            if (lst1.InvokeRequired) {
                AssignToDGVMemoryDelegate d = new AssignToDGVMemoryDelegate(AssignToDGVMemory);
                lst1.Invoke(d, new object[] { lst1, lst2 });
            } else {
                lst1.DataSource = lst2;
            }
        }

        private delegate void AssignToDGVLogDelegate(DataGridView lst1, BindingList<LogMessage> lst2);

        private void AssignToDGVLog(DataGridView lst1, BindingList<LogMessage> lst2) {
            if (lst1.InvokeRequired) {
                AssignToDGVLogDelegate d = new AssignToDGVLogDelegate(AssignToDGVLog);
                lst1.Invoke(d, new object[] { lst1, lst2 });
            } else {
                lst1.DataSource = lst2;
            }
        }

        private void UpdateToolsWindows()
        {
            if(profiler != null && !profiler.IsDisposed)
                profiler.RefreshData();

            if(memoryViewer != null && !memoryViewer.IsDisposed)
                memoryViewer.RefreshData(useHexNumbers);

            if(registerViewer != null && !registerViewer.IsDisposed)
                registerViewer.RefreshView(useHexNumbers);

            if(machineState != null && !machineState.IsDisposed)
                machineState.RefreshView(this.ziggyWin);

            if(watchWindow != null && !watchWindow.IsDisposed)
                watchWindow.RefreshData(useHexNumbers);

            if(callStackViewer != null && !callStackViewer.IsDisposed)
                callStackViewer.RefreshView();

        }

        private void ProcessMemoryBreakpoint(int addr, int val) {
            pauseEmulation = true;
            pc = cpu.regs.PC;
            DoPauseEmulation();
            SetState(MonitorState.PAUSE);
            Disassemble(cpu.regs.PC, cpu.regs.PC, false, false);
            ziggyWin.zx.needsPaint = true;

            UpdateToolsWindows();

            if (!(dbState == MonitorState.RUN || dbState == MonitorState.STEPOVER || dbState == MonitorState.STEPOUT) && !pauseEmulation) {
                Disassemble(cpu.regs.PC, cpu.regs.PC, false, false);
                memoryViewList[addr / 10].Bytes[addr % 10] = val;

                if (memoryViewer != null && !memoryViewer.IsDisposed)
                    memoryViewer.RefreshData(useHexNumbers);

                if (registerViewer != null && !registerViewer.IsDisposed)
                    registerViewer.RefreshView(useHexNumbers);

                if (machineState != null && !machineState.IsDisposed)
                    machineState.RefreshView(this.ziggyWin);
            }
        }

        public void DoPauseEmulation() {
            ziggyWin.ShouldExitFullscreen();
            ziggyWin.zx.Pause();
            ziggyWin.ForceScreenUpdate();

            dataGridView1.DataSource = null;
            disassemblyList = new DisassemblyList();
            Disassemble(0, 65535, true, false);
            AssignToDGVDisassembly(dataGridView1, disassemblyList);
            SetState(MonitorState.PAUSE);
            monitorStatusLabel.Text = "Breakpoint hit: " + breakPointStatus;
            memoryViewList = new BindingList<MemoryUnit>();
            for (int i = 0; i < 65535; i += 10) {
                MemoryUnit mu = new MemoryUnit(this);
                mu.Address = i;
                mu.Bytes = new List<int>();
                for (int g = 0; g < 10; g++) {
                    if (i + g > 65535)
                        break;
                    mu.Bytes.Add(ziggyWin.zx.PeekByteNoContend((ushort)(i + g)));
                }
                memoryViewList.Add(mu);
            }
            UpdateToolsWindows();
            this.Show();
            this.Focus();
        }

        //Event: Raised when the z80 memory contents have changed (via POKE)
        public void Monitor_MemoryWrite(Object sender, MemoryEventArgs e) {
            //Check if any breakpoints have been hit
            foreach (KeyValuePair<SPECCY_EVENT, BreakPointCondition> kv in breakPointList)
            {
                BreakPointCondition val = kv.Value;
                if (kv.Key == SPECCY_EVENT.MEMORY_WRITE)
                {
                    if (e.Address == val.Address) {
                        if (val.Data > -1) {
                            if (e.Byte == val.Data) {
                                breakPointStatus = String.Format("Memory write @ {0} (${0:x}) with value {1} (${1:x})", val.Address, val.Data);
                                ProcessMemoryBreakpoint(e.Address, e.Byte);
                                break;
                            }
                        } else {
                            breakPointStatus = String.Format("Memory write @ {0} (${0:x}) with value {1} (${1:x})", e.Address, e.Byte);
                            ProcessMemoryBreakpoint(e.Address, e.Byte);
                            break;
                        }
                    }
                }
            }

            if (watchWindow != null && !watchWindow.IsDisposed && watchWindow.Visible) {
                for (int i = 0; i < watchVariableList.Count; i++)
                    watchVariableList[i].Data = ziggyWin.zx.PeekByteNoContend((ushort)(watchVariableList[i].Address));

                watchWindow.RefreshData(useHexNumbers);
            }
        }

        //Event: Raised when the z80 memory contents are read (via PEEK)
        public void Monitor_MemoryRead(Object sender, MemoryEventArgs e) {
            //Check if any breakpoints have been hit
            foreach (KeyValuePair<SPECCY_EVENT, BreakPointCondition> kv in breakPointList)
            {
                BreakPointCondition val = kv.Value;
                if (kv.Key == SPECCY_EVENT.MEMORY_READ) {
                    if (e.Address == val.Address) {
                        if (val.Data > -1) {
                            if (e.Byte == val.Data) {
                                breakPointStatus = String.Format("Memory read @ {0} (${0:x}) with value {1} (${1:x})", val.Address, val.Data);
                                ProcessMemoryBreakpoint(e.Address, e.Byte);
                                break;
                            }
                        } else {
                            breakPointStatus = String.Format("Memory read @ {0} (${0:x}) with value {1} (${1:x})", e.Address, e.Byte);
                            ProcessMemoryBreakpoint(e.Address, e.Byte);
                            break;
                        }
                    }
                }
            }
        }

        //Event: Raised when the z80 memory contents are executed (opcode fetch)
        public void Monitor_MemoryExecute(Object sender, MemoryEventArgs e) {
            //Check if any breakpoints have been hit
            foreach (KeyValuePair<SPECCY_EVENT, BreakPointCondition> kv in breakPointList)
            {
                BreakPointCondition val = kv.Value;
                if (kv.Key == SPECCY_EVENT.MEMORY_EXECUTE)
                {
                    if (e.Address == val.Address) {
                        if (val.Data > -1) {
                            if (e.Byte == val.Data) {
                                breakPointStatus = String.Format("Memory execute @ {0} (${0:x}) with value {1} (${1:x})", val.Address, val.Data);
                                ProcessMemoryBreakpoint(e.Address, e.Byte);
                                break;
                            }
                        } else {
                            breakPointStatus = String.Format("Memory execute @ {0} (${0:x}) with value {1} (${1:x})", e.Address, e.Byte);
                            ProcessMemoryBreakpoint(e.Address, e.Byte);
                            break;
                        }
                    }
                }
            }
        }

        //Event: Raised before a opcode has been executed by the z80
        // public void Monitor_OpcodeExecuted(Object sender, OpcodeExecutedEventArgs e)
        public void Monitor_OpcodeExecuted(Object sender) {
            if (pauseEmulation && pc == cpu.regs.PC) {
                pauseEmulation = false;
                return;
            }
            cpu = ziggyWin.zx.cpu;
            //ziggyWin.zx.Pause();
            //Can't do ValuePC = etc because ValuePC calls HexAnd8bitRegUpdate internally
            //leading to a severe hit on framerate.
            pc = cpu.regs.PC;

            //Check if any breakpoints have been hit
            foreach (KeyValuePair<SPECCY_EVENT, BreakPointCondition> kv in breakPointList)
            {
                // int val = Convert.ToInt32(kv.Value);
                BreakPointCondition val = kv.Value;

                switch (kv.Key) {
                    case SPECCY_EVENT.OPCODE_A:
                        if (cpu.regs.A == val.Address) {
                            pauseEmulation = true;
                            breakPointStatus = String.Format("A = {0} (${0:x})", val.Address);
                        }
                        break;

                    case SPECCY_EVENT.OPCODE_PC:
                        if (pc == val.Address) {
                            pauseEmulation = true;
                            breakPointStatus = String.Format("PC = {0} (${0:x})", val.Address);
                        }
                        break;

                    case SPECCY_EVENT.OPCODE_HL:
                        if (cpu.regs.HL == val.Address) {
                            pauseEmulation = true;
                            breakPointStatus = String.Format("HL = {0} (${0:x})", val.Address);
                        }
                        break;

                    case SPECCY_EVENT.OPCODE_BC:
                        if (cpu.regs.BC == val.Address) {
                            pauseEmulation = true;
                            breakPointStatus = String.Format("BC = {0} (${0:x})", val.Address);
                        }
                        break;

                    case SPECCY_EVENT.OPCODE_DE:
                        if (cpu.regs.DE == val.Address) {
                            pauseEmulation = true;
                            breakPointStatus = String.Format("DE = {0} (${0:x})", val.Address);
                        }
                        break;

                    case SPECCY_EVENT.OPCODE_IX:
                        if (cpu.regs.IX == val.Address) {
                            pauseEmulation = true;
                            breakPointStatus = String.Format("IX = {0} (${0:x})", val.Address);
                        }
                        break;

                    case SPECCY_EVENT.OPCODE_IY:
                        if (cpu.regs.IY == val.Address) {
                            pauseEmulation = true;
                            breakPointStatus = String.Format("IY = {0} (${0:x})", val.Address);
                        }
                        break;

                    case SPECCY_EVENT.OPCODE_SP:
                        if (cpu.regs.SP == val.Address) {
                            pauseEmulation = true;
                            breakPointStatus = String.Format("SP = {0} (${0:x})", val.Address);
                        }
                        break;
                }
            }

            if (pc == runToCursorAddress) {
                pauseEmulation = true;
                breakPointStatus = String.Format("PC reached cursor position @ {0} (${0:x})", runToCursorAddress);
                runToCursorAddress = -1;
            }

            if (dbState == MonitorState.STEPOUT) {
                if (lastOpcodeWasRET) {
                    pauseEmulation = true;
                    lastOpcodeWasRET = false;
                } else {
                    switch (PeekByte(pc)) {
                        case 0xC9:  //RET
                        case 0xD8:  //RET C
                        case 0xF8:  //RET M
                        case 0xC0:  //RET NC
                        case 0xD0:  //RET NZ
                        case 0xF0:  //RET P
                        case 0xE8:  //RET PE
                        case 0xE0:  //RET PO
                        case 0xC8:  //RET Z
                            lastOpcodeWasRET = true;
                            break;

                        case 0xED:
                            int nxtopc = PeekByte((ushort)(pc + 1));
                            if (nxtopc == 0x45 || nxtopc == 0x4D)   //RETI or RETN
                                lastOpcodeWasRET = true;
                            break;

                        default:
                            lastOpcodeWasRET = false;
                            break;
                    }
                }
            }

            if (pauseEmulation) {
                pauseEmulation = false;
                DoPauseEmulation();
                //ziggyWin.zx.breakForMonitor = true;
                //ziggyWin.invokeMonitor = true;
            }

            if (dbState == MonitorState.STEPIN) {
                SetState(MonitorState.PAUSE);
                Disassemble(cpu.regs.PC, (ushort)(cpu.regs.PC + 1), false, false);
                ziggyWin.zx.needsPaint = true;
                UpdateToolsWindows();
            } else {
                breakPointStatus = "";
            }

            if (isTraceOn && (dbState != MonitorState.STEPOVER)) {
                if (previousPC != pc) {
                    Disassemble(previousPC, (ushort)(previousPC + 1), false, true);

                    LogMessage log = new LogMessage();            
                    log.Address = TraceMessage.address;
                    log.Opcodes = TraceMessage.opcodes;
                    log.Tstates = previousTState;//ziggyWin.zx. totalTStates;
                    logList.Add(log);
                }
            }

            if (dbState != MonitorState.STEPOVER) {
                previousPC = pc;
                previousTState = cpu.t_states;
            }
        }

        public void Process() {
            if (pauseEmulation) {
                DoPauseEmulation();
            }
            if (dbState == MonitorState.STEPIN) {
                SetState(MonitorState.PAUSE);
                Disassemble(cpu.regs.PC, cpu.regs.PC, false, false);
                ziggyWin.zx.needsPaint = true;
                UpdateToolsWindows();
            }
        }

        private void Monitor_StateChangeEvent(object sender, StateChangeEventArgs e) {
            pc = cpu.regs.PC;

            pauseEmulation = false;

            foreach (KeyValuePair<SPECCY_EVENT, BreakPointCondition> kv in breakPointList)
            {
                // int val = Convert.ToInt32(kv.Value);
                BreakPointCondition val = kv.Value;

                if (e.EventType == kv.Key) {
                    breakPointStatus = String.Format("{0}", e.EventType);
                    DoPauseEmulation();
                    SetState(MonitorState.PAUSE);
                    Disassemble(cpu.regs.PC, cpu.regs.PC, false, false);
                    ziggyWin.zx.needsPaint = true;
                    UpdateToolsWindows();
                    break;
                }
            }
        }

        public void Monitor_PushStackEvent(object sender, int val) {
        }

        public void Monitor_PopStackEvent(object sender, int val) {
        }

        public void Monitor_PortIO(Object sender, PortIOEventArgs e) {
            pc = cpu.regs.PC;

            pauseEmulation = false;
            //Check if any breakpoints have been hit
            foreach (KeyValuePair<SPECCY_EVENT, BreakPointCondition> kv in breakPointList)
            {
                // int val = Convert.ToInt32(kv.Value);
                BreakPointCondition val = kv.Value;

                switch (kv.Key) {
                    case SPECCY_EVENT.PORT_WRITE:
                        if (e.IsWrite && (e.Port == val.Address)) {
                            //Break on specific data value only for this address?
                            if (val.Data > -1) {
                                if (val.Data == e.Value) {
                                    breakPointStatus = String.Format("Port write @ {0} (${0:x}) with value {1} (${1:x}))", e.Port, e.Value);
                                    pauseEmulation = true;
                                }
                            } else //we don't care about the data so break anyway
                            {
                                breakPointStatus = String.Format("Port write @ {0} (${0:x})", val);
                                pauseEmulation = true;
                            }
                        }
                        break;

                    case SPECCY_EVENT.PORT_READ:
                        if (!e.IsWrite) {
                            if (ziggyWin.zx.isPlayingRZX) {
                                breakPointStatus = String.Format("RZX Port read @ {0} (${0:x})", val);
                                pauseEmulation = true;
                            }
                            else
                            if ((e.Port & val.Address) == val.Address) {
                                breakPointStatus = String.Format("Port read @ {0} (${0:x})", val);
                                pauseEmulation = true;
                            }
                        }
                        break;

                    case SPECCY_EVENT.ULA_WRITE:
                        if (e.IsWrite && ((e.Port & 0x1) == 0)) {
                            if (val.Data > -1) {
                                if (val.Data == e.Value) {
                                    breakPointStatus = String.Format("ULA write @ {0} (${0:x}) with value {1} (${1:x}))", e.Port, e.Value);
                                    pauseEmulation = true;
                                }
                            } else {
                                breakPointStatus = String.Format("ULA write @ {0} (${0:x})", e.Port);
                                pauseEmulation = true;
                            }
                        }
                        break;

                    case SPECCY_EVENT.ULA_READ:
                        if (!e.IsWrite && ((e.Port & 0x1) == 0)) {
                            breakPointStatus = String.Format("ULA read @ {0} (${0:x})", e.Port);
                            pauseEmulation = true;
                        }
                        break;
                }

                if (pauseEmulation) {
                    DoPauseEmulation();
                    SetState(MonitorState.PAUSE);
                    Disassemble(cpu.regs.PC, cpu.regs.PC, false, false);
                    ziggyWin.zx.needsPaint = true;
                    UpdateToolsWindows();
                    break;
                }
            }
        }

        private void Monitor_RZXPlaybackStartEvent(object sender) {
            breakPointStatus = String.Format("RZX Playback start." );
            DoPauseEmulation();
            SetState(MonitorState.PAUSE);
            Disassemble(cpu.regs.PC, cpu.regs.PC, false, false);
            ziggyWin.zx.needsPaint = true;
            UpdateToolsWindows();
        }

        private void Monitor_RZXFrameEndEvent(object sender, RZXFrameEventArgs e) {
            foreach (KeyValuePair<SPECCY_EVENT, BreakPointCondition> kv in breakPointList) {
                // int val = Convert.ToInt32(kv.Value);
                BreakPointCondition val = kv.Value;

                if (kv.Key == SPECCY_EVENT.RZX_FRAME_END) {
                    if (val.Data == -1 || val.Data == e.FrameNumber) {
                        breakPointStatus = String.Format("RZX frame {0} end: #ExpectedFetch {1} #ActualFetch {2} #ExpectedINs {3} #ActualINs {4}", e.FrameNumber, e.ExpectedFetchCount, e.ActualFetchCount, e.ExpectedINs, e.ExecutedINs);
                        DoPauseEmulation();
                        SetState(MonitorState.PAUSE);
                        Disassemble(cpu.regs.PC, cpu.regs.PC, false, false);
                        ziggyWin.zx.needsPaint = true;
                        UpdateToolsWindows();
                    }
                }
            }
        }

        //OpcodeDisassembly acts as a DataSource for the DataGridView.
        //Implements INotifyPropertyChanged to tell the DGV when contents have changed
        public class OpcodeDisassembly : INotifyPropertyChanged
        {
            private Monitor monitorRef;
            private ushort address;
            private List<int> bytesAtAddress = new List<int>();
            private String opcodes;
            private int param1 = int.MaxValue;
            private int param2 = int.MaxValue;
            public string toolTipText = "";

            public OpcodeDisassembly(Monitor mref) {
                monitorRef = mref;
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void NotifyPropertyChanged(string name) {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(name));
            }

            public ushort Address {
                get {
                    return address;
                }

                set {
                    address = value;
                    this.NotifyPropertyChanged("Address");
                }
            }

            public String AddressAsString {
                get {
                    if (monitorRef.useHexNumbers) {
                        return address.ToString("x");
                    }

                    return address.ToString();
                }
            }

            public List<int> BytesAtAddress {
                get {
                    return bytesAtAddress;
                }
                set {
                    bytesAtAddress = value;
                    this.NotifyPropertyChanged("GetBytesAtAddress");
                }
            }

            public String BytesAtAddressAsString {
                get {
                    System.Text.StringBuilder byteString = new System.Text.StringBuilder();
                    for (int i = 0; i < bytesAtAddress.Count; i++) {
                        if (monitorRef.aSCIICharactersToolStripMenuItem.Checked) {
                            if ((bytesAtAddress[i] > 31) && (bytesAtAddress[i] < 128)) {
                                byteString.Append((char)bytesAtAddress[i]);
                            } else
                                byteString.Append('.');
                        } else if (monitorRef.useHexNumbers) {
                            byteString.Append(bytesAtAddress[i].ToString("x2"));
                        } else {
                            byteString.Append(bytesAtAddress[i].ToString("D2"));
                        }

                        byteString.Append(" ");
                    }
                    return byteString.ToString();
                }
            }

            public String Opcodes {
                get {
                    String opString = "Error";
                    bool foundSysVars = false;
                    if (monitorRef.showSysVars) {
                        if (param2 == int.MaxValue) {
                            String svar;
                            if (monitorRef.systemVariables.TryGetValue(Param1, out svar)) {
                                foundSysVars = true;
                                toolTipText = Param1.ToString(monitorRef.useHexNumbers ? "x2": "D2");
                                opString = String.Format(opcodes, svar);
                            }
                        }
                    }

                    if (!foundSysVars) {
                        if (monitorRef.useHexNumbers) {
                            opcodes = opcodes.Replace(":D", ":x");
                        } else {
                            opcodes = opcodes.Replace(":x", ":D");
                        }
                        
                        if (param2 != int.MaxValue) {
                            opString = String.Format(opcodes, Param1, Param2);
                        } else if (param1 != int.MaxValue) {
                            opString = String.Format(opcodes, Param1);
                        } else
                            opString = opcodes;
                    }

                    return opString;
                }

                set {
                    opcodes = value;

                    this.NotifyPropertyChanged("Opcodes");
                }
            }

            public int Param1 {
                get { return (param1 % 65536); }
                set { param1 = value; }
            }

            public int Param2 {
                get { return (param2 % 65536); }
                set { param2 = value; }
            }
        }

        public class TraceMessage
        {
            public static String address;
            public static String opcodes;
        }

        //Extend the BindingList so as to implement a Find functionality
        public class DisassemblyList : BindingList<OpcodeDisassembly>
        {
            protected override bool SupportsSearchingCore {
                get {
                    return true;
                }
            }

            protected override int FindCore(PropertyDescriptor prop, object key) {
                // Get the property info for the specified property.
                // Ignore the prop value and search by address.
                ushort val = Convert.ToUInt16(key);
                for (int i = 0; i < Count; ++i) {
                    if (Items[i].Address == val)
                        return i;
                    else if (Items[i].Address > val)
                        return i - 1;
                }
                return -1;
            }

            public int Find(string property, object key) {
                // Check the properties for a property with the specified name.
                PropertyDescriptorCollection properties =
                    TypeDescriptor.GetProperties(typeof(OpcodeDisassembly));
                PropertyDescriptor prop = properties.Find(property, true);

                // If there is not a match, return -1 otherwise pass search to
                // FindCore method.
                if (prop == null)
                    return -1;
                else
                    return FindCore(prop, key);
            }
        }

        //The actual list of all disassembled addresses, associated bytes and opcodes
        public DisassemblyList disassemblyList = new DisassemblyList();

        //A dictionary lookup for PC to disassemblyList index
        public Dictionary<int, int> disassemblyLookup = new Dictionary<int, int>(6000);

        public class LogMessage
        {
            private String address;
            private String opcodes;
            private int tstates;

            public String Address {
                get { return address; }
                set { address = value; }
            }

            public int Tstates {
                get { return tstates; }
                set { tstates = value; }
            }

            public String Opcodes {
                get { return opcodes; }
                set { opcodes = value; }
            }
        }

        public BindingList<LogMessage> logList = new BindingList<LogMessage>();

        //Breakpoint lists consist of key (register) value pairs.
        public BindingList<KeyValuePair<SPECCY_EVENT, BreakPointCondition>> breakPointList = new BindingList<KeyValuePair<SPECCY_EVENT, BreakPointCondition>>();

        //Stores the row numbers for active breakpoints
        private List<int> breakpointRowList = new List<int>();

        public BindingList<BreakPointCondition> breakPointConditions = new BindingList<BreakPointCondition>();

        public class MemoryUnit
        {
            private Monitor monitorRef;
            private int address;
            private List<int> bytes;

            public MemoryUnit(Monitor m) {
                monitorRef = m;
            }

            public int Address {
                get { return address; }
                set { address = value; }
            }

            public List<int> Bytes {
                get { return bytes; }
                set { bytes = value; }
            }

            public String GetBytes {
                get {
                    System.Text.StringBuilder byteString = new System.Text.StringBuilder();
                    for (int i = 0; i < bytes.Count; i++) {
                        if (monitorRef.useHexNumbers) {
                            byteString.Append(bytes[i].ToString("x2"));
                        } else {
                            byteString.Append(bytes[i].ToString("D2"));
                        }

                        byteString.Append(" ");
                    }
                    return byteString.ToString();
                }
            }

            public String GetCharacters {
                get {
                    System.Text.StringBuilder byteString = new System.Text.StringBuilder();
                    for (int i = 0; i < bytes.Count; i++) {
                        // char c = (char)(bytes[i]);
                        if ((bytes[i] > 31) && (bytes[i] < 128)) {
                            byteString.Append((char)(bytes[i]));
                        } else
                            byteString.Append('.');
                    }
                    return byteString.ToString();
                }
            }
        }

        public BindingList<MemoryUnit> memoryViewList = new BindingList<MemoryUnit>();
        public BindingList<WatchVariable> watchVariableList = new BindingList<WatchVariable>();

        /// <summary>
        /// Remove the column header border in the Aero theme in Vista,
        /// but keep it for other themes such as standard and classic.
        /// </summary>
        public static DataGridViewHeaderBorderStyle ProperColumnHeadersBorderStyle {
            get {
                return (System.Drawing.SystemFonts.MessageBoxFont.Name == "Segoe UI") ?
                    DataGridViewHeaderBorderStyle.None :
                    DataGridViewHeaderBorderStyle.Raised;
            }
        }

        public void DeRegisterAllEvents() {
            ziggyWin.zx.MemoryReadEvent -= new MemoryReadEventHandler(Monitor_MemoryRead);
            ziggyWin.zx.MemoryExecuteEvent -= new MemoryExecuteEventHandler(Monitor_MemoryExecute);
            ziggyWin.zx.MemoryWriteEvent -= new MemoryWriteEventHandler(Monitor_MemoryWrite);
            ziggyWin.zx.OpcodeExecutedEvent -= new OpcodeExecutedEventHandler(Monitor_OpcodeExecuted);
            ziggyWin.zx.PortEvent -= new PortIOEventHandler(Monitor_PortIO);
            ziggyWin.zx.RZXPlaybackStartEvent -= new RZXPlaybackStartEventHandler(Monitor_RZXPlaybackStartEvent);
            ziggyWin.zx.RZXFrameEndEvent -= new RZXFrameEndEventHandler(Monitor_RZXFrameEndEvent);
            ziggyWin.zx.StateChangeEvent -= new StateChangeEventHandler(Monitor_StateChangeEvent);
        }

        public void ReRegisterAllEvents() {
            ziggyWin.zx.MemoryReadEvent += new MemoryReadEventHandler(Monitor_MemoryRead);
            ziggyWin.zx.MemoryExecuteEvent += new MemoryExecuteEventHandler(Monitor_MemoryExecute);
            ziggyWin.zx.MemoryWriteEvent += new MemoryWriteEventHandler(Monitor_MemoryWrite);
            ziggyWin.zx.OpcodeExecutedEvent += new OpcodeExecutedEventHandler(Monitor_OpcodeExecuted);
            ziggyWin.zx.PortEvent += new PortIOEventHandler(Monitor_PortIO);
            ziggyWin.zx.RZXPlaybackStartEvent += new RZXPlaybackStartEventHandler(Monitor_RZXPlaybackStartEvent);
            ziggyWin.zx.RZXFrameEndEvent += new RZXFrameEndEventHandler(Monitor_RZXFrameEndEvent);
            ziggyWin.zx.StateChangeEvent += new StateChangeEventHandler(Monitor_StateChangeEvent);
        }

        public void ReSyncWithZX() {
            //ziggyWin.zx.MemoryChangeEvent += new MemoryChangeEventHandler(Monitor_MemoryChanged);
            //ziggyWin.zx.OpcodeExecutedEvent += new OpcodeExecutedEventHandler(Monitor_OpcodeExecuted);
            disassemblyList = new DisassemblyList();
            Disassemble(0, 65535, true, false);
            dataGridView1.DataSource = disassemblyList;
            dataGridView1.Enabled = true;

            memoryViewList = new BindingList<MemoryUnit>();
            for (int i = 0; i < 65535; i += 10) {
                MemoryUnit mu = new MemoryUnit(this);
                mu.Address = i;
                mu.Bytes = new List<int>();
                for (int g = 0; g < 10; g++) {
                    if (i + g > 65535)
                        break;
                    mu.Bytes.Add(ziggyWin.zx.PeekByteNoContend((ushort)(i + g)));
                }
                memoryViewList.Add(mu);
            }

            for(int i = 0; i < watchVariableList.Count; i++)
                watchVariableList[i].Data = ziggyWin.zx.PeekByteNoContend((ushort)(watchVariableList[i].Address));

            breakpointRowList.Clear();

            foreach (var br in breakPointList)
            {
               if (br.Key == SPECCY_EVENT.OPCODE_PC)
               {
                    int index = disassemblyList.Find("Address", br.Value.Address);
                    if (index >= 0)
                    {
                        breakpointRowList.Add(index);
                    }
               }
            }
            UpdateToolsWindows();
        }

        public void DeSyncWithZX() {
            //ziggyWin.zx.MemoryChangeEvent -= new MemoryChangeEventHandler(Monitor_MemoryChanged);
            //ziggyWin.zx.OpcodeExecutedEvent -= new OpcodeExecutedEventHandler(Monitor_OpcodeExecuted);
            dataGridView1.DataSource = null;
            disassemblyList.Clear();
            dataGridView1.Enabled = false;

            SetState(0);
        }

        public void SetState(MonitorState state) {
            switch (state) {
                case MonitorState.RUN:
                    dbState = MonitorState.RUN;
                    //pauseEmulation = false;
                    ziggyWin.zx.Resume();
                    ziggyWin.zx.doRun = true;
                    /** Thread code
                    if (breakPointList.Count == 0)
                        DeRegisterAllEvents();

                    if (ziggyWin.zx.isSuspended)
                        ziggyWin.zx.Resume();
                    else
                    {
                        lock (ziggyWin.zx.lockThis2)
                        {
                            ziggyWin.zx.monitorIsRunning = false;
                            System.Threading.Monitor.Pulse(ziggyWin.zx.lockThis2);
                        }
                    }*/
                    break;

                case MonitorState.PAUSE:
                    ziggyWin.zx.Pause();
                    dbState = MonitorState.PAUSE;
                    UpdateZXState();
                    ziggyWin.zx.doRun = false;
                    pauseEmulation = true;
                    break;

                case MonitorState.STEPIN:
                    dbState = MonitorState.STEPIN;
                    ziggyWin.zx.Resume();
                    //pauseEmulation = false;
                    ziggyWin.zx.doRun = true;
                    //ziggyWin.zx.Resume();
                    break;

                case MonitorState.STEPOUT:
                    dbState = MonitorState.STEPOUT;
                    ziggyWin.zx.Resume();
                    //pauseEmulation = false;
                    ziggyWin.zx.doRun = true;
                    //ziggyWin.zx.Resume();
                    break;
            }
        }

        public void RefreshMemory(int from) {
            this.SuspendLayout();
            dataGridView1.DataSource = null;
            //int line = disassemblyList.Find("Address", from);
            //Disassemble(disassemblyList[line].Address, 65535, line, false);
            Disassemble(0, 65535, true, false);
            dataGridView1.DataSource = disassemblyList;
            this.ResumeLayout();
        }

        public Monitor(Form1 zw) {
            InitializeComponent();
           
            ziggyWin = zw;


            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = System.Drawing.SystemFonts.MessageBoxFont;
            }
            //dataGridView1.DoubleBuffered(true);

            this.SuspendLayout();

            dataGridView1.ColumnHeadersBorderStyle = ProperColumnHeadersBorderStyle;
            dataGridView1.CellValidating += new DataGridViewCellValidatingEventHandler(dataGridView1_CellValidating);
            //dataGridView1.CellDoubleClick += new DataGridViewCellEventHandler(dataGridView1_CellDoubleClick);
            dataGridView1.CellEndEdit += new DataGridViewCellEventHandler(dataGridView1_CellEndEdit);
            dataGridView1.CellToolTipTextNeeded += new DataGridViewCellToolTipTextNeededEventHandler(dataGridView1_CellToolTipTextNeeded);
            
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 =
                 new System.Windows.Forms.DataGridViewCellStyle();

            //Define Header Style
            dataGridViewCellStyle2.Alignment =
                System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle2.BackColor = Control.DefaultBackColor;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Consolas",
                10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle2.SelectionBackColor = Control.DefaultBackColor;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle2.WrapMode =
                System.Windows.Forms.DataGridViewTriState.False;

            //Apply Header Style
            dataGridView1.RowHeadersDefaultCellStyle = dataGridViewCellStyle2;

            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.ControlLightLight;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Consolas",
                10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;

            this.dataGridView1.ColumnHeadersBorderStyle =
             DataGridViewHeaderBorderStyle.Raised;

            
            //clear any previously set columns
            //dataGridView1.Columns.Clear();
            dataGridView1.AutoGenerateColumns = false;
            DataGridViewTextBoxColumn dgridColAddress = new DataGridViewTextBoxColumn();
            dgridColAddress.HeaderText = "Address";
            dgridColAddress.Name = "Address";
            dgridColAddress.Width = 60;
            dgridColAddress.DataPropertyName = "Address";
            dgridColAddress.ReadOnly = true;
            dataGridView1.Columns.Add(dgridColAddress);

            DataGridViewTextBoxColumn dgridColBytes = new DataGridViewTextBoxColumn();
            dgridColBytes.HeaderText = "Bytes";
            dgridColBytes.Name = "Bytes";
            dgridColBytes.Width = 100;
            dgridColBytes.ReadOnly = false;
            dgridColBytes.DataPropertyName = "BytesAtAddressAsString";
            dataGridView1.Columns.Add(dgridColBytes);

            DataGridViewTextBoxColumn dgridColOpcodes = new DataGridViewTextBoxColumn();
            dgridColOpcodes.HeaderText = "Instruction";
            dgridColOpcodes.Name = "Instruction";
            dgridColOpcodes.Width = 220;
            dgridColOpcodes.ReadOnly = true;
            dgridColOpcodes.DataPropertyName = "Opcodes";
            dataGridView1.Columns.Add(dgridColOpcodes);

            DataGridViewTextBoxColumn dgrid2ColCondition = new DataGridViewTextBoxColumn();
            dgrid2ColCondition.HeaderText = "Condition";
            dgrid2ColCondition.Name = "Condition";
            dgrid2ColCondition.Width = 141;
            dgrid2ColCondition.DataPropertyName = "Condition";

            DataGridViewTextBoxColumn dgridColLogAddress = new DataGridViewTextBoxColumn();
            dgridColLogAddress.HeaderText = "Address";
            dgridColLogAddress.Name = "Address";
            dgridColLogAddress.Width = 120;
            dgridColLogAddress.DataPropertyName = "Address";

            DataGridViewTextBoxColumn dgridColLogTstates = new DataGridViewTextBoxColumn();
            dgridColLogTstates.HeaderText = "T-State";
            dgridColLogTstates.Name = "Tstates";
            dgridColLogTstates.Width = 150;
            dgridColLogTstates.DataPropertyName = "Tstates";

            DataGridViewTextBoxColumn dgridColLogInstructions = new DataGridViewTextBoxColumn();
            dgridColLogInstructions.HeaderText = "Instruction";
            dgridColLogInstructions.Name = "Opcodes";
            dgridColLogInstructions.Width = 195;
            dgridColLogInstructions.DataPropertyName = "Opcodes";

            this.ResumeLayout();
        }

        private void dataGridView1_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            //Console.WriteLine("dataGridView1_CellValidating");
            if(!dataGridView1.IsCurrentCellInEditMode)
                return;

            dataGridView1.Rows[e.RowIndex].ErrorText = "";
            int newInteger;

            // Do not try to validate the 'new row' until finished 
            // editing since there 
            // is not any point in validating its initial value. 
            if(dataGridView1.Rows[e.RowIndex].IsNewRow) { return; }
            var enteredString = e.FormattedValue.ToString().Split(' ');
            int[] newValues = new int[enteredString.Length];
            bool validEdit = true;
            for (int i = 0; i < enteredString.Length; i++)
            {
                var fv = enteredString[i];
                if(fv == "")
                    continue;
                newInteger = Utilities.ConvertToInt(fv);
                if (newInteger < 0 || newInteger > 255 || enteredString.Length > 5)
                {
                    //e.Cancel = true;
                    //Console.Write(e.FormattedValue);
                    //Console.WriteLine(": the value must be a valid integer");
                    dataGridView1.Rows[e.RowIndex].ErrorText = "the value must be a valid integer";
                    validEdit = false;
                    break;
                }
                newValues[i] = newInteger;
            }
            if (validEdit)
            {
                int numberOfChanges = newValues.Length - disassemblyList[e.RowIndex].BytesAtAddress.Count;
                for(int i = 0; i < newValues.Length; i++)
                {
                    Console.WriteLine(newValues[i]);
                    ziggyWin.zx.PokeByteNoContend(disassemblyList[e.RowIndex].Address + i, newValues[i]);
                }

                //The number of edits is less than the previous number of bytes at this disassembly address. So poke the remaining to zero.
                if (numberOfChanges < 0)
                {
                    for (int i = newValues.Length; i < disassemblyList[e.RowIndex].BytesAtAddress.Count; ++i)
                    {
                        ziggyWin.zx.PokeByteNoContend(disassemblyList[e.RowIndex].Address + i, 0);
                    }
                }
            }
            dataGridView1.RefreshEdit();
            //dataGridView1.EndEdit();
        }

        private void dataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            //Console.WriteLine("dataGridView1_CellEndEdit");
            dataGridView1.SuspendLayout();
            //var enteredString = dataGridView1[e.ColumnIndex, e.RowIndex].FormattedValue.ToString().Split(' ');
            Disassemble(disassemblyList[e.RowIndex].Address, (ushort)(disassemblyList[e.RowIndex].Address + 10), false, false);
            dataGridView1.ResumeLayout();
            dataGridView1.Refresh();
            DataGridViewCell cell = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex];
            cell.ReadOnly = true;
        }

        private void dataGridView1_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e) {
            if (e.RowIndex == -1 || e.ColumnIndex != 2)
                return;

            if (!string.IsNullOrEmpty(disassemblyList[e.RowIndex].toolTipText)) {
                e.ToolTipText = disassemblyList[e.RowIndex].toolTipText;
                return;
            }

            e.ToolTipText = "Double-click to toggle a breakpoint at this address.";
        }

        //Register state and other information are updated here.
        //Also the DataGridView is set to point to the current PC address
        private void UpdateZXState() {
            cpu = ziggyWin.zx.cpu;
            TStates = cpu.t_states;
            ValuePC = cpu.regs.PC;
            ValueSP = cpu.regs.SP;
            ValueHL = cpu.regs.HL;
            ValueDE = cpu.regs.DE;
            ValueBC = cpu.regs.BC;
            ValueAF = cpu.regs.AF;
            ValueIR = cpu.regs.IR;
            ValueMP = cpu.regs.MemPtr;
            ValueHL_ = cpu.regs.HL_;
            ValueDE_ = cpu.regs.DE_;
            ValueBC_ = cpu.regs.BC_;
            ValueAF_ = cpu.regs.AF_;
            ValueIX = cpu.regs.IX;
            ValueIY = cpu.regs.IY;
            ValueIM = cpu.interrupt_mode;

            if (registerViewer != null && !registerViewer.IsDisposed)
                registerViewer.RefreshView(useHexNumbers);

            if (machineState != null && !machineState.IsDisposed)
                machineState.RefreshView(ziggyWin);

            //HexAnd8bitRegUpdate();
            int index;
            if (!disassemblyLookup.TryGetValue(ValuePC, out index)) {
                index = disassemblyList.Find("Address", ValuePC);
            }
            if (index < 0)
                index = 0;

            if (index < dataGridView1.Rows.Count) {
                dataGridView1.FirstDisplayedScrollingRowIndex = index;
                dataGridView1.CurrentCell = dataGridView1.Rows[index].Cells[0];
                dataGridView1.Rows[index].Selected = true;
            }
            dataGridView1.Refresh();
        }

        //Returns the byte value at an address and adds it to the byteList
        private byte PeekByte(ushort addr) {
            byte b = ziggyWin.zx.PeekByteNoContend(addr);
            byteList.Add(b); //= byteString + " " + b.ToString();
            return b;
        }

        //Returns the word value at an address and adds it to the byteList
        private ushort PeekWord(ushort addr) {
            int a = ziggyWin.zx.PeekByteNoContend(addr);
            byteList.Add(a);// = byteString + " " + a.ToString();
            a = ziggyWin.zx.PeekByteNoContend((ushort)(addr + 1));
            byteList.Add(a);// byteString = byteString + " " + a.ToString();
            return ziggyWin.zx.PeekWordNoContend(addr);
        }

        public void PokeByte(ushort addr, byte val) {
            ziggyWin.zx.PokeByteNoContend(addr, val);
            Disassemble(addr, addr, false, false);
            memoryViewList[addr / 10].Bytes[addr % 10] = val;

            if(memoryViewer != null && !memoryViewer.IsDisposed)
                memoryViewer.RefreshData(useHexNumbers);

            for(int i = 0; i < watchVariableList.Count; i++)
                if(watchVariableList[i].Address == addr)
                    watchVariableList[i].Data = val;

            if(watchWindow != null && !watchWindow.IsDisposed)
                watchWindow.RefreshData(useHexNumbers);

            dataGridView1.Update();
        }

        //Stores opcode and one paramater
        private void Log(String logMsg1, int p1) {
            opcodeString = logMsg1;
            param1 = p1;
        }

        //Stores opcode and two paramaters
        private void Log(String logMsg1, int p1, int p2) {
            opcodeString = logMsg1;
            param1 = p1;
            param2 = p2;
        }

        //Stores opcode only
        private void Log(String logMsg1) {
            opcodeString = logMsg1;
        }

        //Stores byte at address
        private void Log(int opcodeVal) {
            byteList.Add(opcodeVal);
        }

        //Stores byte and opcode
        private void Log(int opcodeVal, String logMsg2) {
            byteList.Add(opcodeVal);// byteString = byteString + opcodeVal.ToString();
            opcodeString = logMsg2;
        }

        //Returns the displacement in 2's complement
        private int GetDisplacement(int val) {
            int res = (128 ^ val) - 128;
            return res;
        }

        //Used by the Disassemble routine
        private List<int> byteList = new List<int>();

        private String opcodeString;
        private int param1;
        private int param2;

        //Disassembles within a range of memory addresses.
        //specifying rebuild as true will generate a list from scratch.
        protected void Disassemble(ushort startAddr, ushort endAddr, bool rebuild, bool traceOn) {
            int PC = startAddr;
            int opcode;
            int disp = 0; //used later on to calculate relative jumps
            int opcodeMatches = 0;
            //int line = disassemblyList.Find("Address", startAddr);
            int line = -1;

            if (!disassemblyLookup.TryGetValue(startAddr, out line)) {
                line = 0;
            }

            for (; ; ) {
                if (PC > endAddr)
                    break;

                // OpcodeDisassembly od = new OpcodeDisassembly(this);
                //od.LineNo = line;
                int address = PC;

                param1 = int.MaxValue;
                param2 = int.MaxValue;

                if (!traceOn)
                    byteList = new List<int>();

                if (!rebuild && !traceOn) {
                    disassemblyList[line].Address = (ushort)PC;
                }

                if (PC > 65535) {
                    break;
                }

                opcode = PeekByte((ushort)PC);
               
                PC++;
                jmp4Undoc:  //We will jump here for undocumented instructions.
                bool jumpForUndoc = false;
                disp = 0;
                //Massive switch-case to decode the instructions!
                switch (opcode) {

                    #region NOP

                    case 0x00: //NOP
                        Log("NOP");
                        break;

                    #endregion NOP

                    # region 16 bit load operations (LD rr, nn)
                    /** LD rr, nn (excluding DD prefix) **/
                    case 0x01: //LD BC, nn
                        disp = PeekWord((ushort)PC);
                        Log("LD BC, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0x11:  //LD DE, nn
                        disp = PeekWord((ushort)PC);
                        Log("LD DE, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0x21:  //LD HL, nn
                        disp = PeekWord((ushort)PC);
                        Log("LD HL, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0x2A:  //LD HL, (nn)
                        disp = PeekWord((ushort)PC);
                        Log("LD HL, ({0,0:D})", disp);
                        PC += 2;
                        break;

                    case 0x31:  //LD SP, nn
                        disp = PeekWord((ushort)PC);
                        Log("LD SP, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xF9:  //LD SP, HL
                        Log("LD SP, HL");
                        break;
                    #endregion

                    #region 16 bit increments (INC rr)
                    /** INC rr **/
                    case 0x03:  //INC BC
                        Log("INC BC");
                        break;

                    case 0x13:  //INC DE
                        Log("INC DE");
                        break;

                    case 0x23:  //INC HL
                        Log("INC HL");
                        break;

                    case 0x33:  //INC SP
                        Log("INC SP");
                        break;
                    #endregion INC rr

                    #region 8 bit increments (INC r)
                    /** INC r + INC (HL) **/
                    case 0x04:  //INC B
                        Log("INC B");
                        break;

                    case 0x0C:  //INC C
                        Log("INC C");
                        break;

                    case 0x14:  //INC D
                        Log("INC D");
                        break;

                    case 0x1C:  //INC E
                        Log("INC E");
                        break;

                    case 0x24:  //INC H
                        Log("INC H");
                        break;

                    case 0x2C:  //INC L
                        Log("INC L");
                        break;

                    case 0x34:  //INC (HL)
                        Log("INC (HL)");
                        break;

                    case 0x3C:  //INC A
                        Log("INC A");
                        break;
                    #endregion

                    #region 8 bit decrement (DEC r)
                    /** DEC r + DEC (HL)**/
                    case 0x05: //DEC B
                        Log("DEC B");
                        break;

                    case 0x0D:    //DEC C
                        Log("DEC C");
                        break;

                    case 0x15:  //DEC D
                        Log("DEC D");
                        break;

                    case 0x1D:  //DEC E
                        Log("DEC E");
                        break;

                    case 0x25:  //DEC H
                        Log("DEC H");
                        break;

                    case 0x2D:  //DEC L
                        Log("DEC L");
                        break;

                    case 0x35:  //DEC (HL)
                        Log("DEC (HL)");
                        break;

                    case 0x3D:  //DEC A
                        Log("DEC A");
                        break;
                    #endregion

                    #region 16 bit decrements
                    /** DEC rr **/
                    case 0x0B:  //DEC BC
                        Log("DEC BC");
                        break;

                    case 0x1B:  //DEC DE
                        Log("DEC DE");
                        break;

                    case 0x2B:  //DEC HL
                        Log("DEC HL");
                        break;

                    case 0x3B:  //DEC SP
                        Log("DEC SP");
                        break;
                    #endregion

                    #region Immediate load operations (LD (nn), r)
                    /** LD (rr), r + LD (nn), HL  + LD (nn), A **/
                    case 0x02: //LD (BC), A
                        Log("LD (BC), A");
                        break;

                    case 0x12:  //LD (DE), A
                        Log("LD (DE), A");
                        break;

                    case 0x22:  //LD (nn), HL
                        disp = PeekWord((ushort)PC);
                        Log("LD ({0,0:D}), HL", disp);
                        PC += 2;
                        break;

                    case 0x32:  //LD (nn), A
                        disp = PeekWord((ushort)PC);
                        Log("LD ({0,0:D}), A", disp);
                        PC += 2;
                        break;

                    case 0x36:  //LD (HL), n
                        disp = PeekByte((ushort)PC);
                        Log("LD (HL), {0,0:D}", disp);
                        PC += 1;
                        break;
                    #endregion

                    #region Indirect load operations (LD r, r)
                    /** LD r, r **/
                    case 0x06: //LD B, n
                        disp = PeekByte((ushort)PC);
                        Log("LD B, {0,0:D}", disp);
                        PC += 1;
                        break;

                    case 0x0A:  //LD A, (BC)
                        Log("LD A, (BC)");
                        break;

                    case 0x0E:  //LD C, n
                        disp = PeekByte((ushort)PC);
                        Log("LD C, {0,0:D}", disp);
                        PC += 1;
                        break;

                    case 0x16:  //LD D,n
                        disp = PeekByte((ushort)PC);
                        Log("LD D, {0,0:D}", disp);
                        PC += 1;
                        break;

                    case 0x1A:  //LD A,(DE)
                        Log("LD A, (DE)");
                        break;

                    case 0x1E:  //LD E,n
                        disp = PeekByte((ushort)PC);
                        Log("LD E, {0,0:D}", disp);
                        PC += 1;
                        break;

                    case 0x26:  //LD H,n
                        disp = PeekByte((ushort)PC);
                        Log("LD H, {0,0:D}", disp);
                        PC += 1;
                        break;

                    case 0x2E:  //LD L,n
                        disp = PeekByte((ushort)PC);
                        Log("LD L, {0,0:D}", disp);
                        PC += 1;
                        break;

                    case 0x3A:  //LD A,(nn)
                        disp = PeekWord((ushort)PC);
                        Log("LD A, ({0,0:D})", disp);
                        PC += 2;
                        break;

                    case 0x3E:  //LD A,n
                        disp = PeekByte((ushort)PC);
                        Log("LD A, {0,0:D}", disp);
                        PC += 1;
                        break;

                    case 0x40:  //LD B,B
                        Log("LD B, B");
                        break;

                    case 0x41:  //LD B,C
                        Log("LD B, C");
                        break;

                    case 0x42:  //LD B,D
                        Log("LD B, D");
                        break;

                    case 0x43:  //LD B,E
                        Log("LD B, E");
                        break;

                    case 0x44:  //LD B,H
                        Log("LD B, H");
                        break;

                    case 0x45:  //LD B,L
                        Log("LD B, L");
                        break;

                    case 0x46:  //LD B,(HL)
                        Log("LD B, (HL)");
                        break;

                    case 0x47:  //LD B,A
                        Log("LD B, A");
                        break;

                    case 0x48:  //LD C,B
                        Log("LD C, B");
                        break;

                    case 0x49:  //LD C,C
                        Log("LD C, C");
                        break;

                    case 0x4A:  //LD C,D
                        Log("LD C, D");
                        break;

                    case 0x4B:  //LD C,E
                        Log("LD C, E");
                        break;

                    case 0x4C:  //LD C,H
                        Log("LD C, H");
                        break;

                    case 0x4D:  //LD C,L
                        Log("LD C, L");
                        break;

                    case 0x4E:  //LD C, (HL)
                        Log("LD C, (HL)");
                        break;

                    case 0x4F:  //LD C,A
                        Log("LD C, A");
                        break;

                    case 0x50:  //LD D,B
                        Log("LD D, B");
                        break;

                    case 0x51:  //LD D,C
                        Log("LD D, C");
                        break;

                    case 0x52:  //LD D,D
                        Log("LD D, D");
                        break;

                    case 0x53:  //LD D,E
                        Log("LD D, E");
                        break;

                    case 0x54:  //LD D,H
                        Log("LD D, H");
                        break;

                    case 0x55:  //LD D,L
                        Log("LD D, L");
                        break;

                    case 0x56:  //LD D,(HL)
                        Log("LD D, (HL)");
                        break;

                    case 0x57:  //LD D,A
                        Log("LD D, A");
                        break;

                    case 0x58:  //LD E,B
                        Log("LD E, B");
                        break;

                    case 0x59:  //LD E,C
                        Log("LD E, C");
                        break;

                    case 0x5A:  //LD E,D
                        Log("LD E, D");
                        break;

                    case 0x5B:  //LD E,E
                        Log("LD E, E");
                        break;

                    case 0x5C:  //LD E,H
                        Log("LD E, H");
                        break;

                    case 0x5D:  //LD E,L
                        Log("LD E, L");
                        break;

                    case 0x5E:  //LD E,(HL)
                        Log("LD E, (HL)");
                        break;

                    case 0x5F:  //LD E,A
                        Log("LD E, A");
                        break;

                    case 0x60:  //LD H,B
                        Log("LD H, B");
                        break;

                    case 0x61:  //LD H,C
                        Log("LD H, C");
                        break;

                    case 0x62:  //LD H,D
                        Log("LD H, D");
                        break;

                    case 0x63:  //LD H,E
                        Log("LD H, E");
                        break;

                    case 0x64:  //LD H,H
                        Log("LD H, H");
                        break;

                    case 0x65:  //LD H,L
                        Log("LD H, L");
                        break;

                    case 0x66:  //LD H,(HL)
                        Log("LD H, (HL)");
                        break;

                    case 0x67:  //LD H,A
                        Log("LD H, A");
                        break;

                    case 0x68:  //LD L,B
                        Log("LD L, B");
                        break;

                    case 0x69:  //LD L,C
                        Log("LD L, C");
                        break;

                    case 0x6A:  //LD L,D
                        Log("LD L, D");
                        break;

                    case 0x6B:  //LD L,E
                        Log("LD L, E");
                        break;

                    case 0x6C:  //LD L,H
                        Log("LD L, H");
                        break;

                    case 0x6D:  //LD L,L
                        Log("LD L, L");
                        break;

                    case 0x6E:  //LD L,(HL)
                        Log("LD L, (HL)");
                        break;

                    case 0x6F:  //LD L,A
                        Log("LD L, A");
                        break;

                    case 0x70:  //LD (HL),B
                        Log("LD (HL), B");
                        break;

                    case 0x71:  //LD (HL),C
                        Log("LD (HL), C");
                        break;

                    case 0x72:  //LD (HL),D
                        Log("LD (HL), D");
                        break;

                    case 0x73:  //LD (HL),E
                        Log("LD (HL), E");
                        break;

                    case 0x74:  //LD (HL),H
                        Log("LD (HL), H");
                        break;

                    case 0x75:  //LD (HL),L
                        Log("LD (HL), L");
                        break;

                    case 0x77:  //LD (HL),A
                        Log("LD (HL), A");
                        break;

                    case 0x78:  //LD A,B
                        Log("LD A, B");
                        break;

                    case 0x79:  //LD A,C
                        Log("LD A, C");
                        break;

                    case 0x7A:  //LD A,D
                        Log("LD A, D");
                        break;

                    case 0x7B:  //LD A,E
                        Log("LD A, E");
                        break;

                    case 0x7C:  //LD A,H
                        Log("LD A, H");
                        break;

                    case 0x7D:  //LD A,L
                        Log("LD A, L");
                        break;

                    case 0x7E:  //LD A,(HL)
                        Log("LD A, (HL)");
                        break;

                    case 0x7F:  //LD A,A
                        Log("LD A, A");
                        break;
                    #endregion

                    #region Rotates on Accumulator
                    /** Accumulator Rotates **/
                    case 0x07: //RLCA
                        Log("RLCA");
                        break;

                    case 0x0F:  //RRCA
                        Log("RRCA");
                        break;

                    case 0x17:  //RLA
                        Log("RLA");
                        break;

                    case 0x1F:  //RRA
                        Log("RRA");
                        break;
                    #endregion

                    #region Exchange operations (EX)
                    /** Exchange operations **/
                    case 0x08:     //EX AF, AF'
                        Log("EX AF, AF'");
                        break;

                    case 0xD9:   //EXX
                        Log("EXX");
                        break;

                    case 0xE3:  //EX (SP), HL
                        Log("EX (SP), HL");
                        break;

                    case 0xEB:  //EX DE, HL
                        Log("EX DE, HL");
                        break;
                    #endregion

                    #region 16 bit addition to HL (Add HL, rr)
                    /** Add HL, rr **/
                    case 0x09:     //ADD HL, BC
                        Log("ADD HL, BC");
                        break;

                    case 0x19:    //ADD HL, DE
                        Log("ADD HL, DE");
                        break;

                    case 0x29:  //ADD HL, HL
                        Log("ADD HL, HL");
                        break;

                    case 0x39:  //ADD HL, SP
                        Log("ADD HL, SP");
                        break;
                    #endregion

                    #region 8 bit addition to accumulator (Add r, r)
                    /*** ADD r, r ***/
                    case 0x80:  //ADD A,B
                        Log("ADD A, B");
                        break;

                    case 0x81:  //ADD A,C
                        Log("ADD A, C");
                        break;

                    case 0x82:  //ADD A,D
                        Log("ADD A, D");
                        break;

                    case 0x83:  //ADD A,E
                        Log("ADD A, E");
                        break;

                    case 0x84:  //ADD A,H
                        Log("ADD A, H");
                        break;

                    case 0x85:  //ADD A,L
                        Log("ADD A, L");
                        break;

                    case 0x86:  //ADD A, (HL)
                        Log("ADD A, (HL)");
                        break;

                    case 0x87:  //ADD A, A
                        Log("ADD A, A");
                        break;

                    case 0xC6:  //ADD A, n
                        disp = PeekByte((ushort)PC);
                        Log("ADD A, {0,0:D}", disp);
                        PC++;
                        break;
                    #endregion

                    #region Add to accumulator with carry (Adc A, r)
                    /** Adc a, r **/
                    case 0x88:  //ADC A,B
                        Log("ADC A, B");
                        break;

                    case 0x89:  //ADC A,C
                        Log("ADC A, C");
                        break;

                    case 0x8A:  //ADC A,D
                        Log("ADC A, D");
                        break;

                    case 0x8B:  //ADC A,E
                        Log("ADC A, E");
                        break;

                    case 0x8C:  //ADC A,H
                        Log("ADC A, H");
                        break;

                    case 0x8D:  //ADC A,L
                        Log("ADC A, L");
                        break;

                    case 0x8E:  //ADC A,(HL)
                        Log("ADC A, (HL)");
                        break;

                    case 0x8F:  //ADC A,A
                        Log("ADC A, A");
                        break;

                    case 0xCE:  //ADC A, n
                        disp = PeekByte((ushort)PC);
                        Log("ADC A, {0,0:D}", disp);
                        PC += 1;
                        break;
                    #endregion

                    #region 8 bit subtraction from accumulator(SUB r)
                    case 0x90:  //SUB B
                        Log("SUB B");
                        break;

                    case 0x91:  //SUB C
                        Log("SUB C");
                        break;

                    case 0x92:  //SUB D
                        Log("SUB D");
                        break;

                    case 0x93:  //SUB E
                        Log("SUB E");
                        break;

                    case 0x94:  //SUB H
                        Log("SUB H");
                        break;

                    case 0x95:  //SUB L
                        Log("SUB L");
                        break;

                    case 0x96:  //SUB (HL)
                        Log("SUB (HL)");
                        break;

                    case 0x97:  //SUB A
                        Log("SUB A");
                        break;

                    case 0xD6:  //SUB n
                        disp = PeekByte((ushort)PC);
                        Log("SUB {0,0:D}", disp);
                        PC += 1;
                        break;
                    #endregion

                    #region 8 bit subtraction from accumulator with carry(SBC A, r)
                    case 0x98:  //SBC A, B
                        Log("SBC A, B");
                        break;

                    case 0x99:  //SBC A, C
                        Log("SBC A, C");
                        break;

                    case 0x9A:  //SBC A, D
                        Log("SBC A, D");
                        break;

                    case 0x9B:  //SBC A, E
                        Log("SBC A, E");
                        break;

                    case 0x9C:  //SBC A, H
                        Log("SBC A, H");
                        break;

                    case 0x9D:  //SBC A, L
                        Log("SBC A, L");
                        break;

                    case 0x9E:  //SBC A, (HL)
                        Log("SBC A, (HL)");
                        break;

                    case 0x9F:  //SBC A, A
                        Log("SBC A, A");
                        break;

                    case 0xDE:  //SBC A, n
                        disp = PeekByte((ushort)PC);
                        Log("SBC A, {0,0:D}", disp);
                        PC += 1;
                        break;
                    #endregion

                    #region Relative Jumps (JR / DJNZ)
                    /*** Relative Jumps ***/
                    case 0x10:  //DJNZ n
                        disp = GetDisplacement(PeekByte((ushort)PC));
                        Log("DJNZ {0,0:D}", PC + disp + 1);
                        PC++;
                        break;

                    case 0x18:  //JR n
                        disp = GetDisplacement(PeekByte((ushort)PC));
                        Log("JR {0,0:D}", PC + disp + 1);
                        PC++;
                        break;

                    case 0x20:  //JRNZ n
                        disp = GetDisplacement(PeekByte((ushort)PC));
                        Log("JR NZ, {0,0:D}", PC + disp + 1);
                        PC++;
                        break;

                    case 0x28:  //JRZ n
                        disp = GetDisplacement(PeekByte((ushort)PC));
                        Log("JR Z, {0,0:D}", PC + disp + 1);
                        PC++;
                        break;

                    case 0x30:  //JRNC n
                        disp = GetDisplacement(PeekByte((ushort)PC));
                        Log("JR NC, {0,0:D}", PC + disp + 1);
                        PC++;
                        break;

                    case 0x38:  //JRC n
                        disp = GetDisplacement(PeekByte((ushort)PC));
                        Log("JR C, {0,0:D}", PC + disp + 1);
                        PC++;
                        break;
                    #endregion

                    #region Direct jumps (JP)
                    /*** Direct jumps ***/
                    case 0xC2:  //JPNZ nn
                        disp = PeekWord((ushort)PC);
                        Log("JP NZ, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xC3:  //JP nn
                        disp = PeekWord((ushort)PC);
                        Log("JP {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xCA:  //JPZ nn
                        disp = PeekWord((ushort)PC);
                        Log("JP Z, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xD2:  //JPNC nn
                        disp = PeekWord((ushort)PC);
                        Log("JP NC, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xDA:  //JPC nn
                        disp = PeekWord((ushort)PC);
                        Log("JP C, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xE2:  //JP PO nn
                        disp = PeekWord((ushort)PC);
                        Log("JP PO, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xE9:  //JP (HL)
                        Log("JP (HL)");
                        break;

                    case 0xEA:  //JP PE nn
                        disp = PeekWord((ushort)PC);
                        Log("JP PE, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xF2:  //JP P nn
                        disp = PeekWord((ushort)PC);
                        Log("JP P, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xFA:  //JP M nn
                        disp = PeekWord((ushort)PC);
                        Log("JP M, {0,0:D}", disp);
                        PC += 2;
                        break;
                    #endregion

                    #region Compare instructions (CP)
                    /*** Compare instructions **/
                    case 0xB8:  //CP B
                        Log("CP B");
                        break;

                    case 0xB9:  //CP C
                        Log("CP C");
                        break;

                    case 0xBA:  //CP D
                        Log("CP D");
                        break;

                    case 0xBB:  //CP E
                        Log("CP E");
                        break;

                    case 0xBC:  //CP H
                        Log("CP H");
                        break;

                    case 0xBD:  //CP L
                        Log("CP L");
                        break;

                    case 0xBE:  //CP (HL)
                        Log("CP (HL)");
                        break;

                    case 0xBF:  //CP A
                        Log("CP A");
                        break;

                    case 0xFE:  //CP n
                        disp = PeekByte((ushort)PC);
                        Log("CP {0,0:D}", disp);
                        PC += 1;
                        break;
                    #endregion

                    #region Carry Flag operations
                    /*** Carry Flag operations ***/
                    case 0x37:  //SCF
                        Log("SCF");
                        break;

                    case 0x3F:  //CCF
                        Log("CCF");
                        break;
                    #endregion

                    #region Bitwise AND (AND r)
                    case 0xA0:  //AND B
                        Log("AND B");
                        break;

                    case 0xA1:  //AND C
                        Log("AND C");
                        break;

                    case 0xA2:  //AND D
                        Log("AND D");
                        break;

                    case 0xA3:  //AND E
                        Log("AND E");
                        break;

                    case 0xA4:  //AND H
                        Log("AND H");
                        break;

                    case 0xA5:  //AND L
                        Log("AND L");
                        break;

                    case 0xA6:  //AND (HL)
                        Log("AND (HL)");
                        break;

                    case 0xA7:  //AND A
                        Log("AND A");
                        break;

                    case 0xE6:  //AND n
                        disp = PeekByte((ushort)PC);
                        Log("AND {0,0:D}", disp);
                        PC++;
                        break;
                    #endregion

                    #region Bitwise XOR (XOR r)
                    case 0xA8: //XOR B
                        Log("XOR B");
                        break;

                    case 0xA9: //XOR C
                        Log("XOR C");
                        break;

                    case 0xAA: //XOR D
                        Log("XOR D");
                        break;

                    case 0xAB: //XOR E
                        Log("XOR E");
                        break;

                    case 0xAC: //XOR H
                        Log("XOR H");
                        break;

                    case 0xAD: //XOR L
                        Log("XOR L");
                        break;

                    case 0xAE: //XOR (HL)
                        Log("XOR (HL)");
                        break;

                    case 0xAF: //XOR A
                        Log("XOR A");
                        break;

                    case 0xEE:  //XOR n
                        disp = PeekByte((ushort)PC);
                        Log("XOR {0,0:D}", disp);
                        PC++;
                        break;

                    #endregion

                    #region Bitwise OR (OR r)
                    case 0xB0:  //OR B
                        Log("OR B");
                        break;

                    case 0xB1:  //OR C
                        Log("OR C");
                        break;

                    case 0xB2:  //OR D
                        Log("OR D");
                        break;

                    case 0xB3:  //OR E
                        Log("OR E");
                        break;

                    case 0xB4:  //OR H
                        Log("OR H");
                        break;

                    case 0xB5:  //OR L
                        Log("OR L");
                        break;

                    case 0xB6:  //OR (HL)
                        Log("OR (HL)");
                        break;

                    case 0xB7:  //OR A
                        Log("OR A");
                        break;

                    case 0xF6:  //OR n
                        disp = PeekByte((ushort)PC);
                        Log("OR {0,0:D}", disp);
                        PC++;
                        break;
                    #endregion

                    #region Return instructions
                    case 0xC0:  //RET NZ
                        Log("RET NZ");
                        break;

                    case 0xC8:  //RET Z
                        Log("RET Z");
                        break;

                    case 0xC9:  //RET
                        Log("RET");
                        break;

                    case 0xD0:  //RET NC
                        Log("RET NC");
                        break;

                    case 0xD8:  //RET C
                        Log("RET C");
                        break;

                    case 0xE0:  //RET PO
                        Log("RET PO");
                        break;

                    case 0xE8:  //RET PE
                        Log("RET PE");
                        break;

                    case 0xF0:  //RET P
                        Log("RET P");
                        break;

                    case 0xF8:  //RET M
                        Log("RET M");
                        break;
                    #endregion

                    #region POP/PUSH instructions (Fix these for SP overflow later!)
                    case 0xC1:  //POP BC
                        Log("POP BC");
                        break;

                    case 0xC5:  //PUSH BC
                        Log("PUSH BC");
                        break;

                    case 0xD1:  //POP DE
                        Log("POP DE");
                        break;

                    case 0xD5:  //PUSH DE
                        Log("PUSH DE");
                        break;

                    case 0xE1:  //POP HL
                        Log("POP HL");
                        break;

                    case 0xE5:  //PUSH HL
                        Log("PUSH HL");
                        break;

                    case 0xF1:  //POP AF
                        Log("POP AF");
                        break;

                    case 0xF5:  //PUSH AF
                        Log("PUSH AF");
                        break;
                    #endregion

                    #region CALL instructions
                    case 0xC4:  //CALL NZ, nn
                        disp = PeekWord((ushort)PC);
                        Log("CALL NZ, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xCC:  //CALL Z, nn
                        disp = PeekWord((ushort)PC);
                        Log("CALL Z, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xCD:  //CALL nn
                        disp = PeekWord((ushort)PC);
                        Log("CALL {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xD4:  //CALL NC, nn
                        disp = PeekWord((ushort)PC);
                        Log("CALL NC, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xDC:  //CALL C, nn
                        disp = PeekWord((ushort)PC);
                        Log("CALL C, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xE4:  //CALL PO, nn
                        disp = PeekWord((ushort)PC);
                        Log("CALL PO, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xEC:  //CALL PE, nn
                        disp = PeekWord((ushort)PC);
                        Log("CALL PE, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xF4:  //CALL P, nn
                        disp = PeekWord((ushort)PC);
                        Log("CALL P, {0,0:D}", disp);
                        PC += 2;
                        break;

                    case 0xFC:  //CALL M, nn
                        disp = PeekWord((ushort)PC);
                        Log("CALL M, {0,0:D}", disp);
                        PC += 2;
                        break;
                    #endregion

                    #region Restart instructions (RST n)
                    case 0xC7:  //RST 0x00
                        Log("RST {0:D}", 0);
                        break;

                    case 0xCF:  //RST 0x08
                        Log("RST {0:D}", 8);
                        break;

                    case 0xD7:  //RST 0x10
                        Log("RST {0:D}", 16);
                        break;

                    case 0xDF:  //RST 0x18
                        Log("RST {0:D}", 24);
                        break;

                    case 0xE7:  //RST 0x20
                        Log("RST {0:D}", 32);
                        break;

                    case 0xEF:  //RST 0x28
                        Log("RST {0:D}", 40);
                        break;

                    case 0xF7:  //RST 0x30
                        Log("RST {0:D}", 48);
                        break;

                    case 0xFF:  //RST 0x38
                        Log("RST {0:D}", 56);
                        break;
                    #endregion

                    #region IN instructions
                    case 0xDB:  //IN A, (n)
                        disp = PeekByte((ushort)PC);
                        Log("IN A, ({0:D})", disp);
                        PC++;
                        break;
                    #endregion

                    #region OUT instructions
                    case 0xD3:  //OUT (n), A
                        disp = PeekByte((ushort)PC);
                        Log("OUT ({0:D}), A", disp);
                        PC++;
                        break;
                    #endregion

                    #region Decimal Adjust Accumulator (DAA)
                    case 0x27:  //DAA
                        Log("DAA");
                        break;
                    #endregion

                    #region Complement (CPL)
                    case 0x2f:  //CPL
                        Log("CPL");
                        break;
                    #endregion

                    #region Halt (HALT) - TO BE CHECKED!
                    case 0x76:  //HALT
                        Log("HALT");
                        break;
                    #endregion

                    #region Interrupts
                    case 0xF3:  //DI
                        Log("DI");
                        break;

                    case 0xFB:  //EI
                        Log("EI");
                        break;
                    #endregion

                    #region Opcodes with CB prefix
                    case 0xCB:
                        switch (opcode = PeekByte((ushort)PC++)) {
                            #region Rotate instructions
                            case 0x00: //RLC B
                                Log("RLC B");
                                break;

                            case 0x01: //RLC C
                                Log("RLC C");
                                break;

                            case 0x02: //RLC D
                                Log("RLC D");
                                break;

                            case 0x03: //RLC E
                                Log("RLC E");
                                break;

                            case 0x04: //RLC H
                                Log("RLC H");
                                break;

                            case 0x05: //RLC L
                                Log("RLC L");
                                break;

                            case 0x06: //RLC (HL)
                                Log("RLC (HL)");
                                break;

                            case 0x07: //RLC A
                                Log("RLC A");
                                break;

                            case 0x08: //RRC B
                                Log("RRC B");
                                break;

                            case 0x09: //RRC C
                                Log("RRC C");
                                break;

                            case 0x0A: //RRC D
                                Log("RRC D");
                                break;

                            case 0x0B: //RRC E
                                Log("RRC E");
                                break;

                            case 0x0C: //RRC H
                                Log("RRC H");
                                break;

                            case 0x0D: //RRC L
                                Log("RRC L");
                                break;

                            case 0x0E: //RRC (HL)
                                Log("RRC (HL)");
                                break;

                            case 0x0F: //RRC A
                                Log("RRC A");
                                break;

                            case 0x10: //RL B
                                Log("RL B");
                                break;

                            case 0x11: //RL C
                                Log("RL C");
                                break;

                            case 0x12: //RL D
                                Log("RL D");
                                break;

                            case 0x13: //RL E
                                Log("RL E");
                                break;

                            case 0x14: //RL H
                                Log("RL H");
                                break;

                            case 0x15: //RL L
                                Log("RL L");
                                break;

                            case 0x16: //RL (HL)
                                Log("RL (HL)");
                                break;

                            case 0x17: //RL A
                                Log("RL A");
                                break;

                            case 0x18: //RR B
                                Log("RR B");
                                break;

                            case 0x19: //RR C
                                Log("RR C");
                                break;

                            case 0x1A: //RR D
                                Log("RR D");
                                break;

                            case 0x1B: //RR E
                                Log("RR E");
                                break;

                            case 0x1C: //RR H
                                Log("RR H");
                                break;

                            case 0x1D: //RR L
                                Log("RR L");
                                break;

                            case 0x1E: //RR (HL)
                                Log("RR (HL)");
                                break;

                            case 0x1F: //RR A
                                Log("RR A");
                                break;
                            #endregion

                            #region Register shifts
                            case 0x20:  //SLA B
                                Log("SLA B");
                                break;

                            case 0x21:  //SLA C
                                Log("SLA C");
                                break;

                            case 0x22:  //SLA D
                                Log("SLA D");
                                break;

                            case 0x23:  //SLA E
                                Log("SLA E");
                                break;

                            case 0x24:  //SLA H
                                Log("SLA H");
                                break;

                            case 0x25:  //SLA L
                                Log("SLA L");
                                break;

                            case 0x26:  //SLA (HL)
                                Log("SLA (HL)");
                                break;

                            case 0x27:  //SLA A
                                Log("SLA A");
                                break;

                            case 0x28:  //SRA B
                                Log("SRA B");
                                break;

                            case 0x29:  //SRA C
                                Log("SRA C");
                                break;

                            case 0x2A:  //SRA D
                                Log("SRA D");
                                break;

                            case 0x2B:  //SRA E
                                Log("SRA E");
                                break;

                            case 0x2C:  //SRA H
                                Log("SRA H");
                                break;

                            case 0x2D:  //SRA L
                                Log("SRA L");
                                break;

                            case 0x2E:  //SRA (HL)
                                Log("SRA (HL)");
                                break;

                            case 0x2F:  //SRA A
                                Log("SRA A");
                                break;

                            case 0x30:  //SLL B
                                Log("SLL B");
                                break;

                            case 0x31:  //SLL C
                                Log("SLL C");
                                break;

                            case 0x32:  //SLL D
                                Log("SLL D");
                                break;

                            case 0x33:  //SLL E
                                Log("SLL E");
                                break;

                            case 0x34:  //SLL H
                                Log("SLL H");
                                break;

                            case 0x35:  //SLL L
                                Log("SLL L");
                                break;

                            case 0x36:  //SLL (HL)
                                Log("SLL (HL)");
                                break;

                            case 0x37:  //SLL A
                                Log("SLL A");
                                break;

                            case 0x38:  //SRL B
                                Log("SRL B");
                                break;

                            case 0x39:  //SRL C
                                Log("SRL C");
                                break;

                            case 0x3A:  //SRL D
                                Log("SRL D");
                                break;

                            case 0x3B:  //SRL E
                                Log("SRL E");
                                break;

                            case 0x3C:  //SRL H
                                Log("SRL H");
                                break;

                            case 0x3D:  //SRL L
                                Log("SRL L");
                                break;

                            case 0x3E:  //SRL (HL)
                                Log("SRL (HL)");
                                break;

                            case 0x3F:  //SRL A
                                Log("SRL A");
                                break;
                            #endregion

                            #region Bit test operation (BIT b, r)
                            case 0x40:  //BIT 0, B
                                Log("BIT 0, B");
                                //(0, B);
                                break;

                            case 0x41:  //BIT 0, C
                                Log("BIT 0, C");
                                //(0, C);
                                break;

                            case 0x42:  //BIT 0, D
                                Log("BIT 0, D");
                                //(0, D);
                                break;

                            case 0x43:  //BIT 0, E
                                Log("BIT 0, E");
                                //(0, E);
                                break;

                            case 0x44:  //BIT 0, H
                                Log("BIT 0, H");
                                //(0, H);
                                break;

                            case 0x45:  //BIT 0, L
                                Log("BIT 0, L");
                                //(0, L);
                                break;

                            case 0x46:  //BIT 0, (HL)
                                Log("BIT 0, (HL)");
                                //(0, PeekByte(HL));
                                break;

                            case 0x47:  //BIT 0, A
                                Log("BIT 0, A");
                                //(0, A);
                                break;

                            case 0x48:  //BIT 1, B
                                Log("BIT 1, B");
                                //(1, B);
                                break;

                            case 0x49:  //BIT 1, C
                                Log("BIT 1, C");
                                //(1, C);
                                break;

                            case 0x4A:  //BIT 1, D
                                Log("BIT 1, D");
                                //(1, D);
                                break;

                            case 0x4B:  //BIT 1, E
                                Log("BIT 1, E");
                                //(1, E);
                                break;

                            case 0x4C:  //BIT 1, H
                                Log("BIT 1, H");
                                //(1, H);
                                break;

                            case 0x4D:  //BIT 1, L
                                Log("BIT 1, L");
                                //(1, L);
                                break;

                            case 0x4E:  //BIT 1, (HL)
                                Log("BIT 1, (HL)");
                                //(1, PeekByte(HL));
                                break;

                            case 0x4F:  //BIT 1, A
                                Log("BIT 1, A");
                                //(1, A);
                                break;

                            case 0x50:  //BIT 2, B
                                Log("BIT 2, B");
                                //(2, B);
                                break;

                            case 0x51:  //BIT 2, C
                                Log("BIT 2, C");
                                //(2, C);
                                break;

                            case 0x52:  //BIT 2, D
                                Log("BIT 2, D");
                                //(2, D);
                                break;

                            case 0x53:  //BIT 2, E
                                Log("BIT 2, E");
                                //(2, E);
                                break;

                            case 0x54:  //BIT 2, H
                                Log("BIT 2, H");
                                //(2, H);
                                break;

                            case 0x55:  //BIT 2, L
                                Log("BIT 2, L");
                                //(2, L);
                                break;

                            case 0x56:  //BIT 2, (HL)
                                Log("BIT 2, (HL)");
                                //(2, PeekByte(HL));
                                break;

                            case 0x57:  //BIT 2, A
                                Log("BIT 2, A");
                                //(2, A);
                                break;

                            case 0x58:  //BIT 3, B
                                Log("BIT 3, B");
                                //(3, B);
                                break;

                            case 0x59:  //BIT 3, C
                                Log("BIT 3, C");
                                //(3, C);
                                break;

                            case 0x5A:  //BIT 3, D
                                Log("BIT 3, D");
                                //(3, D);
                                break;

                            case 0x5B:  //BIT 3, E
                                Log("BIT 3, E");
                                //(3, E);
                                break;

                            case 0x5C:  //BIT 3, H
                                Log("BIT 3, H");
                                //(3, H);
                                break;

                            case 0x5D:  //BIT 3, L
                                Log("BIT 3, L");
                                //(3, L);
                                break;

                            case 0x5E:  //BIT 3, (HL)
                                Log("BIT 3, (HL)");
                                //(3, PeekByte(HL));
                                break;

                            case 0x5F:  //BIT 3, A
                                Log("BIT 3, A");
                                //(3, A);
                                break;

                            case 0x60:  //BIT 4, B
                                Log("BIT 4, B");
                                //(4, B);
                                break;

                            case 0x61:  //BIT 4, C
                                Log("BIT 4, C");
                                //(4, C);
                                break;

                            case 0x62:  //BIT 4, D
                                Log("BIT 4, D");
                                //(4, D);
                                break;

                            case 0x63:  //BIT 4, E
                                Log("BIT 4, E");
                                //(4, E);
                                break;

                            case 0x64:  //BIT 4, H
                                Log("BIT 4, H");
                                //(4, H);
                                break;

                            case 0x65:  //BIT 4, L
                                Log("BIT 4, L");
                                //(4, L);
                                break;

                            case 0x66:  //BIT 4, (HL)
                                Log("BIT 4, (HL)");
                                //(4, PeekByte(HL));
                                break;

                            case 0x67:  //BIT 4, A
                                Log("BIT 4, A");
                                //(4, A);
                                break;

                            case 0x68:  //BIT 5, B
                                Log("BIT 5, B");
                                //(5, B);
                                break;

                            case 0x69:  //BIT 5, C
                                Log("BIT 5, C");
                                //(5, C);
                                break;

                            case 0x6A:  //BIT 5, D
                                Log("BIT 5, D");
                                //(5, D);
                                break;

                            case 0x6B:  //BIT 5, E
                                Log("BIT 5, E");
                                //(5, E);
                                break;

                            case 0x6C:  //BIT 5, H
                                Log("BIT 5, H");
                                //(5, H);
                                break;

                            case 0x6D:  //BIT 5, L
                                Log("BIT 5, L");
                                //(5, L);
                                break;

                            case 0x6E:  //BIT 5, (HL)
                                Log("BIT 5, (HL)");
                                //(5, PeekByte(HL));
                                break;

                            case 0x6F:  //BIT 5, A
                                Log("BIT 5, A");
                                //(5, A);
                                break;

                            case 0x70:  //BIT 6, B
                                Log("BIT 6, B");
                                //(6, B);
                                break;

                            case 0x71:  //BIT 6, C
                                Log("BIT 6, C");
                                //(6, C);
                                break;

                            case 0x72:  //BIT 6, D
                                Log("BIT 6, D");
                                //(6, D);
                                break;

                            case 0x73:  //BIT 6, E
                                Log("BIT 6, E");
                                //(6, E);
                                break;

                            case 0x74:  //BIT 6, H
                                Log("BIT 6, H");
                                //(6, H);
                                break;

                            case 0x75:  //BIT 6, L
                                Log("BIT 6, L");
                                //(6, L);
                                break;

                            case 0x76:  //BIT 6, (HL)
                                Log("BIT 6, (HL)");
                                //(6, PeekByte(HL));
                                break;

                            case 0x77:  //BIT 6, A
                                Log("BIT 6, A");
                                //(6, A);
                                break;

                            case 0x78:  //BIT 7, B
                                Log("BIT 7, B");
                                //(7, B);
                                break;

                            case 0x79:  //BIT 7, C
                                Log("BIT 7, C");
                                //(7, C);
                                break;

                            case 0x7A:  //BIT 7, D
                                Log("BIT 7, D");
                                //(7, D);
                                break;

                            case 0x7B:  //BIT 7, E
                                Log("BIT 7, E");
                                //(7, E);
                                break;

                            case 0x7C:  //BIT 7, H
                                Log("BIT 7, H");
                                //(7, H);
                                break;

                            case 0x7D:  //BIT 7, L
                                Log("BIT 7, L");
                                //(7, L);
                                break;

                            case 0x7E:  //BIT 7, (HL)
                                Log("BIT 7, (HL)");
                                //(7, PeekByte(HL));
                                break;

                            case 0x7F:  //BIT 7, A
                                Log("BIT 7, A");
                                //(7, A);
                                break;
                            #endregion

                            #region Reset bit operation (RES b, r)
                            case 0x80:  //RES 0, B
                                Log("RES 0, B");
                                break;

                            case 0x81:  //RES 0, C
                                Log("RES 0, C");
                                break;

                            case 0x82:  //RES 0, D
                                Log("RES 0, D");
                                break;

                            case 0x83:  //RES 0, E
                                Log("RES 0, E");
                                break;

                            case 0x84:  //RES 0, H
                                Log("RES 0, H");
                                break;

                            case 0x85:  //RES 0, L
                                Log("RES 0, L");
                                break;

                            case 0x86:  //RES 0, (HL)
                                Log("RES 0, (HL)");
                                break;

                            case 0x87:  //RES 0, A
                                Log("RES 0, A");
                                break;

                            case 0x88:  //RES 1, B
                                Log("RES 1, B");
                                break;

                            case 0x89:  //RES 1, C
                                Log("RES 1, C");
                                break;

                            case 0x8A:  //RES 1, D
                                Log("RES 1, D");
                                break;

                            case 0x8B:  //RES 1, E
                                Log("RES 1, E");
                                break;

                            case 0x8C:  //RES 1, H
                                Log("RES 1, H");
                                break;

                            case 0x8D:  //RES 1, L
                                Log("RES 1, L");
                                break;

                            case 0x8E:  //RES 1, (HL)
                                Log("RES 1, (HL)");
                                break;

                            case 0x8F:  //RES 1, A
                                Log("RES 1, A");
                                break;

                            case 0x90:  //RES 2, B
                                Log("RES 2, B");
                                break;

                            case 0x91:  //RES 2, C
                                Log("RES 2, C");
                                break;

                            case 0x92:  //RES 2, D
                                Log("RES 2, D");
                                break;

                            case 0x93:  //RES 2, E
                                Log("RES 2, E");
                                break;

                            case 0x94:  //RES 2, H
                                Log("RES 2, H");
                                break;

                            case 0x95:  //RES 2, L
                                Log("RES 2, L");
                                break;

                            case 0x96:  //RES 2, (HL)
                                Log("RES 2, (HL)");
                                break;

                            case 0x97:  //RES 2, A
                                Log("RES 2, A");
                                break;

                            case 0x98:  //RES 3, B
                                Log("RES 3, B");
                                break;

                            case 0x99:  //RES 3, C
                                Log("RES 3, C");
                                break;

                            case 0x9A:  //RES 3, D
                                Log("RES 3, D");
                                break;

                            case 0x9B:  //RES 3, E
                                Log("RES 3, E");
                                break;

                            case 0x9C:  //RES 3, H
                                Log("RES 3, H");
                                break;

                            case 0x9D:  //RES 3, L
                                Log("RES 3, L");
                                break;

                            case 0x9E:  //RES 3, (HL)
                                Log("RES 3, (HL)");
                                break;

                            case 0x9F:  //RES 3, A
                                Log("RES 3, A");
                                break;

                            case 0xA0:  //RES 4, B
                                Log("RES 4, B");
                                break;

                            case 0xA1:  //RES 4, C
                                Log("RES 4, C");
                                break;

                            case 0xA2:  //RES 4, D
                                Log("RES 4, D");
                                break;

                            case 0xA3:  //RES 4, E
                                Log("RES 4, E");
                                break;

                            case 0xA4:  //RES 4, H
                                Log("RES 4, H");
                                break;

                            case 0xA5:  //RES 4, L
                                Log("RES 4, L");
                                break;

                            case 0xA6:  //RES 4, (HL)
                                Log("RES 4, (HL)");
                                break;

                            case 0xA7:  //RES 4, A
                                Log("RES 4, A");
                                break;

                            case 0xA8:  //RES 5, B
                                Log("RES 5, B");
                                break;

                            case 0xA9:  //RES 5, C
                                Log("RES 5, C");
                                break;

                            case 0xAA:  //RES 5, D
                                Log("RES 5, D");
                                break;

                            case 0xAB:  //RES 5, E
                                Log("RES 5, E");
                                break;

                            case 0xAC:  //RES 5, H
                                Log("RES 5, H");
                                break;

                            case 0xAD:  //RES 5, L
                                Log("RES 5, L");
                                break;

                            case 0xAE:  //RES 5, (HL)
                                Log("RES 5, (HL)");
                                break;

                            case 0xAF:  //RES 5, A
                                Log("RES 5, A");
                                break;

                            case 0xB0:  //RES 6, B
                                Log("RES 6, B");
                                break;

                            case 0xB1:  //RES 6, C
                                Log("RES 6, C");
                                break;

                            case 0xB2:  //RES 6, D
                                Log("RES 6, D");
                                break;

                            case 0xB3:  //RES 6, E
                                Log("RES 6, E");
                                break;

                            case 0xB4:  //RES 6, H
                                Log("RES 6, H");
                                break;

                            case 0xB5:  //RES 6, L
                                Log("RES 6, L");
                                break;

                            case 0xB6:  //RES 6, (HL)
                                Log("RES 6, (HL)");
                                break;

                            case 0xB7:  //RES 6, A
                                Log("RES 6, A");
                                break;

                            case 0xB8:  //RES 7, B
                                Log("RES 7, B");
                                break;

                            case 0xB9:  //RES 7, C
                                Log("RES 7, C");
                                break;

                            case 0xBA:  //RES 7, D
                                Log("RES 7, D");
                                break;

                            case 0xBB:  //RES 7, E
                                Log("RES 7, E");
                                break;

                            case 0xBC:  //RES 7, H
                                Log("RES 7, H");
                                break;

                            case 0xBD:  //RES 7, L
                                Log("RES 7, L");
                                break;

                            case 0xBE:  //RES 7, (HL)
                                Log("RES 7, (HL)");
                                break;

                            case 0xBF:  //RES 7, A
                                Log("RES 7, A");
                                break;
                            #endregion

                            #region Set bit operation (SET b, r)
                            case 0xC0:  //SET 0, B
                                Log("SET 0, B");
                                break;

                            case 0xC1:  //SET 0, C
                                Log("SET 0, C");
                                break;

                            case 0xC2:  //SET 0, D
                                Log("SET 0, D");
                                break;

                            case 0xC3:  //SET 0, E
                                Log("SET 0, E");
                                break;

                            case 0xC4:  //SET 0, H
                                Log("SET 0, H");
                                break;

                            case 0xC5:  //SET 0, L
                                Log("SET 0, L");
                                break;

                            case 0xC6:  //SET 0, (HL)
                                Log("SET 0, (HL)");
                                break;

                            case 0xC7:  //SET 0, A
                                Log("SET 0, A");
                                break;

                            case 0xC8:  //SET 1, B
                                Log("SET 1, B");
                                break;

                            case 0xC9:  //SET 1, C
                                Log("SET 1, C");
                                break;

                            case 0xCA:  //SET 1, D
                                Log("SET 1, D");
                                break;

                            case 0xCB:  //SET 1, E
                                Log("SET 1, E");
                                break;

                            case 0xCC:  //SET 1, H
                                Log("SET 1, H");
                                break;

                            case 0xCD:  //SET 1, L
                                Log("SET 1, L");
                                break;

                            case 0xCE:  //SET 1, (HL)
                                Log("SET 1, (HL)");
                                break;

                            case 0xCF:  //SET 1, A
                                Log("SET 1, A");
                                break;

                            case 0xD0:  //SET 2, B
                                Log("SET 2, B");
                                break;

                            case 0xD1:  //SET 2, C
                                Log("SET 2, C");
                                break;

                            case 0xD2:  //SET 2, D
                                Log("SET 2, D");
                                break;

                            case 0xD3:  //SET 2, E
                                Log("SET 2, E");
                                break;

                            case 0xD4:  //SET 2, H
                                Log("SET 2, H");
                                break;

                            case 0xD5:  //SET 2, L
                                Log("SET 2, L");
                                break;

                            case 0xD6:  //SET 2, (HL)
                                Log("SET 2, (HL)");
                                break;

                            case 0xD7:  //SET 2, A
                                Log("SET 2, A");
                                break;

                            case 0xD8:  //SET 3, B
                                Log("SET 3, B");
                                break;

                            case 0xD9:  //SET 3, C
                                Log("SET 3, C");
                                break;

                            case 0xDA:  //SET 3, D
                                Log("SET 3, D");
                                break;

                            case 0xDB:  //SET 3, E
                                Log("SET 3, E");
                                break;

                            case 0xDC:  //SET 3, H
                                Log("SET 3, H");
                                break;

                            case 0xDD:  //SET 3, L
                                Log("SET 3, L");
                                break;

                            case 0xDE:  //SET 3, (HL)
                                Log("SET 3, (HL)");
                                break;

                            case 0xDF:  //SET 3, A
                                Log("SET 3, A");
                                break;

                            case 0xE0:  //SET 4, B
                                Log("SET 4, B");
                                break;

                            case 0xE1:  //SET 4, C
                                Log("SET 4, C");
                                break;

                            case 0xE2:  //SET 4, D
                                Log("SET 4, D");
                                break;

                            case 0xE3:  //SET 4, E
                                Log("SET 4, E");
                                break;

                            case 0xE4:  //SET 4, H
                                Log("SET 4, H");
                                break;

                            case 0xE5:  //SET 4, L
                                Log("SET 4, L");
                                break;

                            case 0xE6:  //SET 4, (HL)
                                Log("SET 4, (HL)");
                                break;

                            case 0xE7:  //SET 4, A
                                Log("SET 4, A");
                                break;

                            case 0xE8:  //SET 5, B
                                Log("SET 5, B");
                                break;

                            case 0xE9:  //SET 5, C
                                Log("SET 5, C");
                                break;

                            case 0xEA:  //SET 5, D
                                Log("SET 5, D");
                                break;

                            case 0xEB:  //SET 5, E
                                Log("SET 5, E");
                                break;

                            case 0xEC:  //SET 5, H
                                Log("SET 5, H");
                                break;

                            case 0xED:  //SET 5, L
                                Log("SET 5, L");
                                break;

                            case 0xEE:  //SET 5, (HL)
                                Log("SET 5, (HL)");
                                break;

                            case 0xEF:  //SET 5, A
                                Log("SET 5, A");
                                break;

                            case 0xF0:  //SET 6, B
                                Log("SET 6, B");
                                break;

                            case 0xF1:  //SET 6, C
                                Log("SET 6, C");
                                break;

                            case 0xF2:  //SET 6, D
                                Log("SET 6, D");
                                break;

                            case 0xF3:  //SET 6, E
                                Log("SET 6, E");
                                break;

                            case 0xF4:  //SET 6, H
                                Log("SET 6, H");
                                break;

                            case 0xF5:  //SET 6, L
                                Log("SET 6, L");
                                break;

                            case 0xF6:  //SET 6, (HL)
                                Log("SET 6, (HL)");
                                break;

                            case 0xF7:  //SET 6, A
                                Log("SET 6, A");
                                break;

                            case 0xF8:  //SET 7, B
                                Log("SET 7, B");
                                break;

                            case 0xF9:  //SET 7, C
                                Log("SET 7, C");
                                break;

                            case 0xFA:  //SET 7, D
                                Log("SET 7, D");
                                break;

                            case 0xFB:  //SET 7, E
                                Log("SET 7, E");
                                break;

                            case 0xFC:  //SET 7, H
                                Log("SET 7, H");
                                break;

                            case 0xFD:  //SET 7, L
                                Log("SET 7, L");
                                break;

                            case 0xFE:  //SET 7, (HL)
                                Log("SET 7, (HL)");
                                break;

                            case 0xFF:  //SET 7, A
                                Log("SET 7, A");
                                break;
                            #endregion

                            default:
                                String msg = "ERROR: Could not handle DD " + opcode.ToString();
                                MessageBox.Show(msg, "Opcode handler",
                                            MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                                break;
                        }
                        break;
                    #endregion

                    #region Opcodes with DD prefix (includes DDCB)
                    case 0xDD:
                        switch (opcode = PeekByte((ushort)PC++)) {
                            #region Addition instructions
                            case 0x09:  //ADD IX, BC
                                Log("ADD IX, BC");
                                break;

                            case 0x19:  //ADD IX, DE
                                Log("ADD IX, DE");
                                break;

                            case 0x29:  //ADD IX, IX
                                Log("ADD IX, IX");
                                break;

                            case 0x39:  //ADD IX, SP
                                Log("ADD IX, SP");
                                break;

                            case 0x84:  //ADD A, IXH
                                Log("ADD A, IXH");
                                break;

                            case 0x85:  //ADD A, IXL
                                Log("ADD A, IXL");
                                break;

                            case 0x86:  //Add A, (IX+d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("ADD A, (IX + {0:D})", disp);
                                PC++;
                                break;

                            case 0x8C:  //ADC A, IXH
                                Log("ADC A, IXH");
                                break;

                            case 0x8D:  //ADC A, IXL
                                Log("ADC A, IXL");
                                break;

                            case 0x8E: //ADC A, (IX+d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("ADC A, (IX + {0:D})", disp);
                                PC++;
                                break;
                            #endregion

                            #region Subtraction instructions
                            case 0x94:  //SUB A, IXH
                                Log("SUB A, IXH");
                                break;

                            case 0x95:  //SUB A, IXL
                                Log("SUB A, IXL");
                                break;

                            case 0x96:  //SUB (IX + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("SUB (IX + {0:D})", disp);
                                PC++;
                                break;

                            case 0x9C:  //SBC A, IXH
                                Log("SBC A, IXH");
                                break;

                            case 0x9D:  //SBC A, IXL
                                Log("SBC A, IXL");
                                break;

                            case 0x9E:  //SBC A, (IX + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("SBC A, (IX + {0:D})", disp);
                                PC++;
                                break;
                            #endregion

                            #region Increment/Decrements
                            case 0x23:  //INC IX
                                Log("INC IX");
                                break;

                            case 0x24:  //INC IXH
                                Log("INC IXH");
                                break;

                            case 0x25:  //DEC IXH
                                Log("DEC IXH");
                                break;

                            case 0x2B:  //DEC IX
                                Log("DEC IX");
                                break;

                            case 0x2C:  //INC IXL
                                Log("INC IXL");
                                break;

                            case 0x2D:  //DEC IXL
                                Log("DEC IXL");
                                break;

                            case 0x34:  //INC (IX + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("INC (IX + {0:D})", disp);
                                PC++;
                                break;

                            case 0x35:  //DEC (IX + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("DEC (IX + {0:D})", disp);
                                PC++;
                                break;
                            #endregion

                            #region Bitwise operators

                            case 0xA4:  //AND IXH
                                Log("AND IXH");
                                break;

                            case 0xA5:  //AND IXL
                                Log("AND IXL");
                                break;

                            case 0xA6:  //AND (IX + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("AND (IX + {0:D})", disp);
                                PC++;
                                break;

                            case 0xAC:  //XOR IXH
                                Log("XOR IXH");
                                break;

                            case 0xAD:  //XOR IXL
                                Log("XOR IXL");
                                break;

                            case 0xAE:  //XOR (IX + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("XOR (IX + {0:D})", disp);
                                PC++;
                                break;

                            case 0xB4:  //OR IXH
                                Log("OR IXH");
                                break;

                            case 0xB5:  //OR IXL
                                Log("OR IXL");
                                break;

                            case 0xB6:  //OR (IX + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("OR (IX + {0:D})", disp);
                                PC++;
                                break;
                            #endregion

                            #region Compare operator
                            case 0xBC:  //CP IXH
                                Log("CP IXH");
                                break;

                            case 0xBD:  //CP IXL
                                Log("CP IXL");
                                break;

                            case 0xBE:  //CP (IX + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("CP (IX + {0:D})", disp & 0xff);
                                PC++;
                                break;
                            #endregion

                            #region Load instructions
                            case 0x21:  //LD IX, nn
                                Log("LD IX, {0,0:D}", PeekWord((ushort)PC));
                                PC += 2;
                                break;

                            case 0x22:  //LD (nn), IX
                                Log("LD ({0:D}), IX", PeekWord((ushort)PC));
                                PC += 2;
                                break;

                            case 0x26:  //LD IXH, n
                                Log("LD IXH, {0:D}", PeekByte((ushort)PC));
                                PC++;
                                break;

                            case 0x2A:  //LD IX, (nn)
                                Log("LD IX, ({0:D})", PeekWord((ushort)PC));
                                PC += 2;
                                break;

                            case 0x2E:  //LD IXL, n
                                Log("LD IXL, {0:D}", PeekByte((ushort)PC));
                                PC++;
                                break;

                            case 0x36:  //LD (IX + d), n
                                disp = GetDisplacement(PeekByte((ushort)PC));

                                Log("LD (IX + {0:D}), {1,0:D}", disp, PeekByte((ushort)(PC + 1)));
                                PC += 2;
                                break;

                            case 0x44:  //LD B, IXH
                                Log("LD B, IXH");
                                break;

                            case 0x45:  //LD B, IXL
                                Log("LD B, IXL");
                                break;

                            case 0x46:  //LD B, (IX + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD B, (IX + {0:D})", disp);
                                PC++;
                                break;

                            case 0x4C:  //LD C, IXH
                                Log("LD C, IXH");
                                break;

                            case 0x4D:  //LD C, IXL
                                Log("LD C, IXL");
                                break;

                            case 0x4E:  //LD C, (IX + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD C, (IX + {0:D})", disp);
                                PC++;
                                break;

                            case 0x54:  //LD D, IXH
                                Log("LD D, IXH");
                                break;

                            case 0x55:  //LD D, IXL
                                Log("LD D, IXL");
                                break;

                            case 0x56:  //LD D, (IX + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD D, (IX + {0:D})", disp);
                                PC++;
                                break;

                            case 0x5C:  //LD E, IXH
                                Log("LD E, IXH");
                                break;

                            case 0x5D:  //LD E, IXL
                                Log("LD E, IXL");
                                break;

                            case 0x5E:  //LD E, (IX + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD E, (IX + {0:D})", disp);
                                PC++;
                                break;

                            case 0x60:  //LD IXH, B
                                Log("LD IXH, B");
                                break;

                            case 0x61:  //LD IXH, C
                                Log("LD IXH, C");
                                break;

                            case 0x62:  //LD IXH, D
                                Log("LD IXH, D");
                                break;

                            case 0x63:  //LD IXH, E
                                Log("LD IXH, E");
                                break;

                            case 0x64:  //LD IXH, IXH
                                Log("LD IXH, IXH");
                                break;

                            case 0x65:  //LD IXH, IXL
                                Log("LD IXH, IXL");
                                break;

                            case 0x66:  //LD H, (IX + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD H, (IX + {0:D})", disp);
                                PC++;
                                break;

                            case 0x67:  //LD IXH, A
                                Log("LD IXH, A");
                                break;

                            case 0x68:  //LD IXL, B
                                Log("LD IXL, B");
                                break;

                            case 0x69:  //LD IXL, C
                                Log("LD IXL, C");
                                break;

                            case 0x6A:  //LD IXL, D
                                Log("LD IXL, D");
                                break;

                            case 0x6B:  //LD IXL, E
                                Log("LD IXL, E");
                                break;

                            case 0x6C:  //LD IXL, IXH
                                Log("LD IXL, IXH");
                                break;

                            case 0x6D:  //LD IXL, IXL
                                Log("LD IXL, IXL");
                                break;

                            case 0x6E:  //LD L, (IX + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD L, (IX + {0:D})", disp);
                                PC++;
                                break;

                            case 0x6F:  //LD IXL, A
                                Log("LD IXL, A");
                                break;

                            case 0x70:  //LD (IX + d), B
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD (IX + {0:D}), B", disp);
                                PC++;
                                break;

                            case 0x71:  //LD (IX + d), C
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD (IX + {0:D}), C", disp);
                                PC++;
                                break;

                            case 0x72:  //LD (IX + d), D
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD (IX + {0:D}), D", disp);
                                PC++;
                                break;

                            case 0x73:  //LD (IX + d), E
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD (IX + {0:D}), E", disp);
                                PC++;
                                break;

                            case 0x74:  //LD (IX + d), H
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD (IX + {0:D}), H", disp);
                                PC++;
                                break;

                            case 0x75:  //LD (IX + d), L
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD (IX + {0:D}), L", disp);
                                PC++;
                                break;

                            case 0x77:  //LD (IX + d), A
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD (IX + {0:D}), A", disp);
                                PC++;
                                break;

                            case 0x7C:  //LD A, IXH
                                Log("LD A, IXH");
                                break;

                            case 0x7D:  //LD A, IXL
                                Log("LD A, IXL");
                                break;

                            case 0x7E:  //LD A, (IX + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD A, (IX + {0:D})", disp);
                                PC++;
                                break;

                            case 0xF9:  //LD SP, IX
                                Log("LD SP, IX");
                                break;
                            #endregion

                            #region All DDCB instructions
                            case 0xCB:
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                PC++;
                                opcode = PeekByte((ushort)PC);      //The opcode comes after the offset byte!
                                PC++;
                                switch (opcode) {
                                    case 0x00: //LD B, RLC (IX+d)
                                        Log("LD B, RLC (IX + {0:D})", disp & 0xff & 0xff);
                                        break;

                                    case 0x01: //LD C, RLC (IX+d)
                                        Log("LD C, RLC (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x02: //LD D, RLC (IX+d)
                                        Log("LD D, RLC (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x03: //LD E, RLC (IX+d)
                                        Log("LD E, RLC (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x04: //LD H, RLC (IX+d)
                                        Log("LD H, RLC (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x05: //LD L, RLC (IX+d)
                                        Log("LD L, RLC (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x06:  //RLC (IX + d)
                                        Log("RLC (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x07: //LD A, RLC (IX+d)
                                        Log("LD A, RLC (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x08: //LD B, RRC (IX+d)
                                        Log("LD B, RRC (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x09: //LD C, RRC (IX+d)
                                        Log("LD C, RRC (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x0A: //LD D, RRC (IX+d)
                                        Log("LD D, RRC (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x0B: //LD E, RRC (IX+d)
                                        Log("LD E, RRC (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x0C: //LD H, RRC (IX+d)
                                        Log("LD H, RRC (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x0D: //LD L, RRC (IX+d)
                                        Log("LD L, RRC (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x0E:  //RRC (IX + d)
                                        Log("RRC (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x0F: //LD A, RRC (IX+d)
                                        Log("LD A, RRC (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x10: //LD B, RL (IX+d)
                                        Log("LD B, RL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x11: //LD C, RL (IX+d)
                                        Log("LD C, RL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x12: //LD D, RL (IX+d)
                                        Log("LD D, RL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x13: //LD E, RL (IX+d)
                                        Log("LD E, RL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x14: //LD H, RL (IX+d)
                                        Log("LD H, RL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x15: //LD L, RL (IX+d)
                                        Log("LD L, RL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x16:  //RL (IX + d)
                                        Log("RL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x17: //LD A, RL (IX+d)
                                        Log("LD A, RL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x18: //LD B, RR (IX+d)
                                        Log("LD B, RR (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x19: //LD C, RR (IX+d)
                                        Log("LD C, RR (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x1A: //LD D, RR (IX+d)
                                        Log("LD D, RR (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x1B: //LD E, RR (IX+d)
                                        Log("LD E, RR (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x1C: //LD H, RR (IX+d)
                                        Log("LD H, RR (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x1D: //LD L, RRC (IX+d)
                                        Log("LD L, RR (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x1E:  //RR (IX + d)
                                        Log("RR (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x1F: //LD A, RRC (IX+d)
                                        Log("LD A, RR (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x20: //LD B, SLA (IX+d)
                                        Log("LD B, SLA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x21: //LD C, SLA (IX+d)
                                        Log("LD C, SLA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x22: //LD D, SLA (IX+d)
                                        Log("LD D, SLA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x23: //LD E, SLA (IX+d)
                                        Log("LD E, SLA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x24: //LD H, SLA (IX+d)
                                        Log("LD H, SLA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x25: //LD L, SLA (IX+d)
                                        Log("LD L, SLA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x26:  //SLA (IX + d)
                                        Log("SLA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x27: //LD A, SLA (IX+d)
                                        Log("LD A, SLA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x28: //LD B, SRA (IX+d)
                                        Log("LD B, SRA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x29: //LD C, SRA (IX+d)
                                        Log("LD C, SRA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x2A: //LD D, SRA (IX+d)
                                        Log("LD D, SRA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x2B: //LD E, SRA (IX+d)
                                        Log("LD E, SRA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x2C: //LD H, SRA (IX+d)
                                        Log("LD H, SRA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x2D: //LD L, SRA (IX+d)
                                        Log("LD L, SRA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x2E:  //SRA (IX + d)
                                        Log("SRA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x2F: //LD A, SRA (IX+d)
                                        Log("LD A, SRA (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x30: //LD B, SLL (IX+d)
                                        Log("LD B, SLL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x31: //LD C, SLL (IX+d)
                                        Log("LD C, SLL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x32: //LD D, SLL (IX+d)
                                        Log("LD D, SLL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x33: //LD E, SLL (IX+d)
                                        Log("LD E, SLL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x34: //LD H, SLL (IX+d)
                                        Log("LD H, SLL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x35: //LD L, SLL (IX+d)
                                        Log("LD L, SLL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x36:  //SLL (IX + d)
                                        Log("SLL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x37: //LD A, SLL (IX+d)
                                        Log("LD A, SLL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x38: //LD B, SRL (IX+d)
                                        Log("LD B, SRL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x39: //LD C, SRL (IX+d)
                                        Log("LD C, SRL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x3A: //LD D, SRL (IX+d)
                                        Log("LD D, SRL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x3B: //LD E, SRL (IX+d)
                                        Log("LD E, SRL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x3C: //LD H, SRL (IX+d)
                                        Log("LD H, SRL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x3D: //LD L, SRL (IX+d)
                                        Log("LD L, SRL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x3E:  //SRL (IX + d)
                                        Log("SRL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x3F: //LD A, SRL (IX+d)
                                        Log("LD A, SRL (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x40:  //BIT 0, (IX + d)
                                        Log("BIT 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x41:  //BIT 0, (IX + d)
                                        Log("BIT 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x42:  //BIT 0, (IX + d)
                                        Log("BIT 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x43:  //BIT 0, (IX + d)
                                        Log("BIT 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x44:  //BIT 0, (IX + d)
                                        Log("BIT 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x45:  //BIT 0, (IX + d)
                                        Log("BIT 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x46:  //BIT 0, (IX + d)
                                        Log("BIT 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x47:  //BIT 0, (IX + d)
                                        Log("BIT 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x48:  //BIT 1, (IX + d)
                                        Log("BIT 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x49:  //BIT 1, (IX + d)
                                        Log("BIT 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x4A:  //BIT 1, (IX + d)
                                        Log("BIT 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x4B:  //BIT 1, (IX + d)
                                        Log("BIT 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x4C:  //BIT 1, (IX + d)
                                        Log("BIT 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x4D:  //BIT 1, (IX + d)
                                        Log("BIT 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x4E:  //BIT 1, (IX + d)
                                        Log("BIT 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x4F:  //BIT 1, (IX + d)
                                        Log("BIT 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x50:  //BIT 2, (IX + d)
                                        Log("BIT 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x51:  //BIT 2, (IX + d)
                                        Log("BIT 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x52:  //BIT 2, (IX + d)
                                        Log("BIT 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x53:  //BIT 2, (IX + d)
                                        Log("BIT 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x54:  //BIT 2, (IX + d)
                                        Log("BIT 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x55:  //BIT 2, (IX + d)
                                        Log("BIT 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x56:  //BIT 2, (IX + d)
                                        Log("BIT 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x57:  //BIT 2, (IX + d)
                                        Log("BIT 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x58:  //BIT 3, (IX + d)
                                        Log("BIT 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x59:  //BIT 3, (IX + d)
                                        Log("BIT 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x5A:  //BIT 3, (IX + d)
                                        Log("BIT 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x5B:  //BIT 3, (IX + d)
                                        Log("BIT 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x5C:  //BIT 3, (IX + d)
                                        Log("BIT 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x5D:  //BIT 3, (IX + d)
                                        Log("BIT 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x5E:  //BIT 3, (IX + d)
                                        Log("BIT 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x5F:  //BIT 3, (IX + d)
                                        Log("BIT 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x60:  //BIT 4, (IX + d)
                                        Log("BIT 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x61:  //BIT 4, (IX + d)
                                        Log("BIT 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x62:  //BIT 4, (IX + d)
                                        Log("BIT 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x63:  //BIT 4, (IX + d)
                                        Log("BIT 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x64:  //BIT 4, (IX + d)
                                        Log("BIT 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x65:  //BIT 4, (IX + d)
                                        Log("BIT 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x66:  //BIT 4, (IX + d)
                                        Log("BIT 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x67:  //BIT 4, (IX + d)
                                        Log("BIT 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x68:  //BIT 5, (IX + d)
                                        Log("BIT 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x69:  //BIT 5, (IX + d)
                                        Log("BIT 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x6A:  //BIT 5, (IX + d)
                                        Log("BIT 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x6B:  //BIT 5, (IX + d)
                                        Log("BIT 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x6C:  //BIT 5, (IX + d)
                                        Log("BIT 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x6D:  //BIT 5, (IX + d)
                                        Log("BIT 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x6E:  //BIT 5, (IX + d)
                                        Log("BIT 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x6F:  //BIT 5, (IX + d)
                                        Log("BIT 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x70:  //BIT 6, (IX + d)
                                        Log("BIT 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x71:  //BIT 6, (IX + d)
                                        Log("BIT 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x72:  //BIT 6, (IX + d)
                                        Log("BIT 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x73:  //BIT 6, (IX + d)
                                        Log("BIT 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x74:  //BIT 6, (IX + d)
                                        Log("BIT 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x75:  //BIT 6, (IX + d)
                                        Log("BIT 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x76:  //BIT 6, (IX + d)
                                        Log("BIT 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x77:  //BIT 6, (IX + d)
                                        Log("BIT 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x78:  //BIT 7, (IX + d)
                                        Log("BIT 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x79:  //BIT 7, (IX + d)
                                        Log("BIT 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x7A:  //BIT 7, (IX + d)
                                        Log("BIT 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x7B:  //BIT 7, (IX + d)
                                        Log("BIT 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x7C:  //BIT 7, (IX + d)
                                        Log("BIT 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x7D:  //BIT 7, (IX + d)
                                        Log("BIT 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x7E:  //BIT 7, (IX + d)
                                        Log("BIT 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x7F:  //BIT 7, (IX + d)
                                        Log("BIT 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x80: //LD B, RES 0, (IX+d)
                                        Log("LD B, RES 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x81: //LD C, RES 0, (IX+d)
                                        Log("LD C, RES 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x82: //LD D, RES 0, (IX+d)
                                        Log("LD D, RES 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x83: //LD E, RES 0, (IX+d)
                                        Log("LD E, RES 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x84: //LD H, RES 0, (IX+d)
                                        Log("LD H, RES 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x85: //LD L, RES 0, (IX+d)
                                        Log("LD L, RES 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x86:  //RES 0, (IX + d)
                                        Log("RES 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x87: //LD A, RES 0, (IX+d)
                                        Log("LD A, RES 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x88: //LD B, RES 1, (IX+d)
                                        Log("LD B, RES 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x89: //LD C, RES 1, (IX+d)
                                        Log("LD C, RES 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x8A: //LD D, RES 1, (IX+d)
                                        Log("LD D, RES 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x8B: //LD E, RES 1, (IX+d)
                                        Log("LD E, RES 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x8C: //LD H, RES 1, (IX+d)
                                        Log("LD H, RES 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x8D: //LD L, RES 1, (IX+d)
                                        Log("LD L, RES 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x8E:  //RES 1, (IX + d)
                                        Log("RES 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x8F: //LD A, RES 1, (IX+d)
                                        Log("LD A, RES 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x90: //LD B, RES 2, (IX+d)
                                        Log("LD B, RES 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x91: //LD C, RES 2, (IX+d)
                                        Log("LD C, RES 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x92: //LD D, RES 2, (IX+d)
                                        Log("LD D, RES 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x93: //LD E, RES 2, (IX+d)
                                        Log("LD E, RES 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x94: //LD H, RES 2, (IX+d)
                                        Log("LD H, RES 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x95: //LD L, RES 2, (IX+d)
                                        Log("LD L, RES 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x96:  //RES 2, (IX + d)
                                        Log("RES 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x97: //LD A, RES 2, (IX+d)
                                        Log("LD A, RES 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x98: //LD B, RES 3, (IX+d)
                                        Log("LD B, RES 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x99: //LD C, RES 3, (IX+d)
                                        Log("LD C, RES 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x9A: //LD D, RES 3, (IX+d)
                                        Log("LD D, RES 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x9B: //LD E, RES 3, (IX+d)
                                        Log("LD E, RES 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x9C: //LD H, RES 3, (IX+d)
                                        Log("LD H, RES 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x9D: //LD L, RES 3, (IX+d)
                                        Log("LD L, RES 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x9E:  //RES 3, (IX + d)
                                        Log("RES 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0x9F: //LD A, RES 3, (IX+d)
                                        Log("LD A, RES 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA0: //LD B, RES 4, (IX+d)
                                        Log("LD B, RES 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA1: //LD C, RES 4, (IX+d)
                                        Log("LD C, RES 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA2: //LD D, RES 4, (IX+d)
                                        Log("LD D, RES 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA3: //LD E, RES 4, (IX+d)
                                        Log("LD E, RES 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA4: //LD H, RES 4, (IX+d)
                                        Log("LD H, RES 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA5: //LD L, RES 4, (IX+d)
                                        Log("LD L, RES 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA6:  //RES 4, (IX + d)
                                        Log("RES 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA7: //LD A, RES 4, (IX+d)
                                        Log("LD A, RES 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA8: //LD B, RES 5, (IX+d)
                                        Log("LD B, RES 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA9: //LD C, RES 5, (IX+d)
                                        Log("LD C, RES 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xAA: //LD D, RES 5, (IX+d)
                                        Log("LD D, RES 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xAB: //LD E, RES 5, (IX+d)
                                        Log("LD E, RES 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xAC: //LD H, RES 5, (IX+d)
                                        Log("LD H, RES 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xAD: //LD L, RES 5, (IX+d)
                                        Log("LD L, RES 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xAE:  //RES 5, (IX + d)
                                        Log("RES 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xAF: //LD A, RES 5, (IX+d)
                                        Log("LD A, RES 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB0: //LD B, RES 6, (IX+d)
                                        Log("LD B, RES 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB1: //LD C, RES 6, (IX+d)
                                        Log("LD C, RES 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB2: //LD D, RES 6, (IX+d)
                                        Log("LD D, RES 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB3: //LD E, RES 6, (IX+d)
                                        Log("LD E, RES 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB4: //LD H, RES 5, (IX+d)
                                        Log("LD H, RES 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB5: //LD L, RES 5, (IX+d)
                                        Log("LD L, RES 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB6:  //RES 6, (IX + d)
                                        Log("RES 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB7: //LD A, RES 5, (IX+d)
                                        Log("LD A, RES 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB8: //LD B, RES 7, (IX+d)
                                        Log("LD B, RES 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB9: //LD C, RES 7, (IX+d)
                                        Log("LD C, RES 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xBA: //LD D, RES 7, (IX+d)
                                        Log("LD D, RES 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xBB: //LD E, RES 7, (IX+d)
                                        Log("LD E, RES 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xBC: //LD H, RES 7, (IX+d)
                                        Log("LD H, RES 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xBD: //LD L, RES 7, (IX+d)
                                        Log("LD L, RES 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xBE:  //RES 7, (IX + d)
                                        Log("RES 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xBF: //LD A, RES 7, (IX+d)
                                        Log("LD A, RES 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC0: //LD B, SET 0, (IX+d)
                                        Log("LD B, SET 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC1: //LD C, SET 0, (IX+d)
                                        Log("LD C, SET 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC2: //LD D, SET 0, (IX+d)
                                        Log("LD D, SET 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC3: //LD E, SET 0, (IX+d)
                                        Log("LD E, SET 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC4: //LD H, SET 0, (IX+d)
                                        Log("LD H, SET 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC5: //LD L, SET 0, (IX+d)
                                        Log("LD L, SET 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC6:  //SET 0, (IX + d)
                                        Log("SET 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC7: //LD A, SET 0, (IX+d)
                                        Log("LD A, SET 0, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC8: //LD B, SET 1, (IX+d)
                                        Log("LD B, SET 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC9: //LD C, SET 0, (IX+d)
                                        Log("LD C, SET 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xCA: //LD D, SET 1, (IX+d)
                                        Log("LD D, SET 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xCB: //LD E, SET 1, (IX+d)
                                        Log("LD E, SET 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xCC: //LD H, SET 1, (IX+d)
                                        Log("LD H, SET 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xCD: //LD L, SET 1, (IX+d)
                                        Log("LD L, SET 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xCE:  //SET 1, (IX + d)
                                        Log("SET 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xCF: //LD A, SET 1, (IX+d)
                                        Log("LD A, SET 1, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD0: //LD B, SET 2, (IX+d)
                                        Log("LD B, SET 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD1: //LD C, SET 2, (IX+d)
                                        Log("LD C, SET 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD2: //LD D, SET 2, (IX+d)
                                        Log("LD D, SET 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD3: //LD E, SET 2, (IX+d)
                                        Log("LD E, SET 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD4: //LD H, SET 21, (IX+d)
                                        Log("LD H, SET 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD5: //LD L, SET 2, (IX+d)
                                        Log("LD L, SET 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD6:  //SET 2, (IX + d)
                                        Log("SET 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD7: //LD A, SET 2, (IX+d)
                                        Log("LD A, SET 2, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD8: //LD B, SET 3, (IX+d)
                                        Log("LD B, SET 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD9: //LD C, SET 3, (IX+d)
                                        Log("LD C, SET 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xDA: //LD D, SET 3, (IX+d)
                                        Log("LD D, SET 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xDB: //LD E, SET 3, (IX+d)
                                        Log("LD E, SET 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xDC: //LD H, SET 21, (IX+d)
                                        Log("LD H, SET 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xDD: //LD L, SET 3, (IX+d)
                                        Log("LD L, SET 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xDE:  //SET 3, (IX + d)
                                        Log("SET 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xDF: //LD A, SET 3, (IX+d)
                                        Log("LD A, SET 3, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE0: //LD B, SET 4, (IX+d)
                                        Log("LD B, SET 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE1: //LD C, SET 4, (IX+d)
                                        Log("LD C, SET 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE2: //LD D, SET 4, (IX+d)
                                        Log("LD D, SET 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE3: //LD E, SET 4, (IX+d)
                                        Log("LD E, SET 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE4: //LD H, SET 4, (IX+d)
                                        Log("LD H, SET 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE5: //LD L, SET 3, (IX+d)
                                        Log("LD L, SET 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE6:  //SET 4, (IX + d)
                                        Log("SET 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE7: //LD A, SET 4, (IX+d)
                                        Log("LD A, SET 4, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE8: //LD B, SET 5, (IX+d)
                                        Log("LD B, SET 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE9: //LD C, SET 5, (IX+d)
                                        Log("LD C, SET 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xEA: //LD D, SET 5, (IX+d)
                                        Log("LD D, SET 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xEB: //LD E, SET 5, (IX+d)
                                        Log("LD E, SET 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xEC: //LD H, SET 5, (IX+d)
                                        Log("LD H, SET 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xED: //LD L, SET 5, (IX+d)
                                        Log("LD L, SET 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xEE:  //SET 5, (IX + d)
                                        Log("SET 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xEF: //LD A, SET 5, (IX+d)
                                        Log("LD A, SET 5, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF0: //LD B, SET 6, (IX+d)
                                        Log("LD B, SET 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF1: //LD C, SET 6, (IX+d)
                                        Log("LD C, SET 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF2: //LD D, SET 6, (IX+d)
                                        Log("LD D, SET 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF3: //LD E, SET 6, (IX+d)
                                        Log("LD E, SET 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF4: //LD H, SET 6, (IX+d)
                                        Log("LD H, SET 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF5: //LD L, SET 6, (IX+d)
                                        Log("LD L, SET 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF6:  //SET 6, (IX + d)
                                        Log("SET 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF7: //LD A, SET 6, (IX+d)
                                        Log("LD A, SET 6, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF8: //LD B, SET 7, (IX+d)
                                        Log("LD B, SET 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF9: //LD C, SET 7, (IX+d)
                                        Log("LD C, SET 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xFA: //LD D, SET 7, (IX+d)
                                        Log("LD D, SET 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xFB: //LD E, SET 7, (IX+d)
                                        Log("LD E, SET 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xFC: //LD H, SET 7, (IX+d)
                                        Log("LD H, SET 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xFD: //LD L, SET 7, (IX+d)
                                        Log("LD L, SET 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xFE:  //SET 7, (IX + d)
                                        Log("SET 7, (IX + {0:D})", disp & 0xff);
                                        break;

                                    case 0xFF: //LD A, SET 7, (IX + D)
                                        Log("LD A, SET 7, (IX + {0:D})", disp & 0xff);
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
                                Log("POP IX");
                                break;

                            case 0xE5:  //PUSH IX
                                Log("PUSH IX");
                                break;
                            #endregion

                            #region Exchange instruction
                            case 0xE3:  //EX (SP), IX
                                Log("EX (SP), IX");
                                break;
                            #endregion

                            #region Jump instruction
                            case 0xE9:  //JP (IX)
                                Log("JP (IX)");
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
                        opcode = PeekByte((ushort)PC++);
                        if (opcode < 0x40) {
                            Log("NOP");
                            break;
                        } else
                            switch (opcode) {
                                case 0x40: //IN B, (C)
                                    Log("IN B, (C)");
                                    break;

                                case 0x41: //Out (C), B
                                    Log("OUT (C), B");
                                    break;

                                case 0x42:  //SBC HL, BC
                                    Log("SBC HL, BC");
                                    break;

                                case 0x43:  //LD (nn), BC
                                    disp = PeekWord((ushort)PC);
                                    Log("LD ({0:D}), BC", disp);
                                    PC += 2;
                                    break;

                                case 0x44:  //NEG
                                    Log("NEG");
                                    break;

                                case 0x45:  //RETN
                                    Log("RET N");
                                    break;

                                case 0x46:  //IM0
                                    Log("IM 0");
                                    break;

                                case 0x47:  //LD I, A
                                    Log("LD I, A");
                                    break;

                                case 0x48: //IN C, (C)
                                    Log("IN C, (C)");
                                    break;

                                case 0x49: //Out (C), C
                                    Log("OUT (C), C");
                                    break;

                                case 0x4A:  //ADC HL, BC
                                    Log("ADC HL, BC");
                                    break;

                                case 0x4B:  //LD BC, (nn)
                                    disp = PeekWord((ushort)PC);
                                    Log("LD BC, ({0:D})", disp);
                                    PC += 2;
                                    break;

                                case 0x4C:  //NEG
                                    Log("NEG");
                                    break;

                                case 0x4D:  //RETI
                                    Log("RETI");
                                    break;

                                case 0x4F:  //LD R, A
                                    Log("LD R, A");
                                    break;

                                case 0x50: //IN D, (C)
                                    Log("IN D, (C)");
                                    break;

                                case 0x51: //Out (C), D
                                    Log("OUT (C), D");
                                    break;

                                case 0x52:  //SBC HL, DE
                                    Log("SBC HL, DE");
                                    break;

                                case 0x53:  //LD (nn), DE
                                    disp = PeekWord((ushort)PC);
                                    Log("LD ({0:D}), DE", disp);
                                    PC += 2;
                                    break;

                                case 0x54:  //NEG
                                    Log("NEG");
                                    break;

                                case 0x55:  //RETN
                                    Log("RETN");
                                    break;

                                case 0x56:  //IM1
                                    Log("IM 1");
                                    break;

                                case 0x57:  //LD A, I
                                    Log("LD A, I");
                                    break;

                                case 0x58: //IN E, (C)
                                    Log("IN E, (C)");
                                    break;

                                case 0x59: //Out (C), E
                                    Log("OUT (C), E");
                                    break;

                                case 0x5A:  //ADC HL, DE
                                    Log("ADC HL, DE");
                                    break;

                                case 0x5B:  //LD DE, (nn)
                                    disp = PeekWord((ushort)PC);
                                    Log("LD DE, ({0:D})", disp);
                                    PC += 2;
                                    break;

                                case 0x5C:  //NEG
                                    Log("NEG");
                                    break;

                                case 0x5D:  //RETN
                                    Log("RETN");
                                    break;

                                case 0x5E:  //IM2
                                    Log("IM 2");
                                    break;

                                case 0x5F:  //LD A, R
                                    Log("LD A, R");
                                    break;

                                case 0x60: //IN H, (C)
                                    Log("IN H, (C)");
                                    break;

                                case 0x61: //Out (C), H
                                    Log("OUT (C), H");
                                    break;

                                case 0x62:  //SBC HL, HL
                                    Log("SBC HL, HL");
                                    break;

                                case 0x63:  //LD (nn), HL
                                    disp = PeekWord((ushort)PC);
                                    Log("LD ({0:D}), HL", disp);
                                    PC += 2;
                                    break;

                                case 0x64:  //NEG
                                    Log("NEG");
                                    break;

                                case 0x65:  //RETN
                                    Log("RETN");
                                    break;

                                case 0x67:  //RRD
                                    Log("RRD");
                                    break;

                                case 0x68: //IN L, (C)
                                    Log("IN L, (C)");
                                    break;

                                case 0x69: //Out (C), L
                                    Log("OUT (C), L");
                                    break;

                                case 0x6A:  //ADC HL, HL
                                    Log("ADC HL, HL");
                                    break;

                                case 0x6B:  //LD HL, (nn)
                                    disp = PeekWord((ushort)PC);
                                    Log("LD HL, ({0:D})", disp);
                                    PC += 2;
                                    break;

                                case 0x6C:  //NEG
                                    Log("NEG");
                                    break;

                                case 0x6D:  //RETN
                                    Log("RETN");
                                    break;

                                case 0x6F:  //RLD
                                    Log("RLD");
                                    break;

                                case 0x70:  //IN (C)
                                    Log("IN (C)");
                                    break;

                                case 0x71:
                                    Log("OUT (C), 0");
                                    break;

                                case 0x72:  //SBC HL, SP
                                    Log("SBC HL, SP");
                                    break;

                                case 0x73:  //LD (nn), SP
                                    disp = PeekWord((ushort)PC);
                                    Log("LD ({0:D}), SP", disp);
                                    PC += 2;
                                    break;

                                case 0x74:  //NEG
                                    Log("NEG");
                                    break;

                                case 0x75:  //RETN
                                    Log("RETN");
                                    break;

                                case 0x76:  //IM 1
                                    Log("IM 1");
                                    break;

                                case 0x78:  //IN A, (C)
                                    Log("IN A, (C)");
                                    break;

                                case 0x79: //Out (C), A
                                    Log("OUT (C), A");
                                    break;

                                case 0x7A:  //ADC HL, SP
                                    Log("ADC HL, SP");
                                    break;

                                case 0x7B:  //LD SP, (nn)
                                    disp = PeekWord((ushort)PC);
                                    Log("LD SP, ({0:D})", disp);
                                    PC += 2;
                                    break;

                                case 0x7C:  //NEG
                                    Log("NEG");
                                    break;

                                case 0x7D:  //RETN
                                    Log("RETN");
                                    break;

                                case 0x7E:  //IM 2
                                    Log("IM 2");
                                    break;

                                case 0xA0:  //LDI
                                    Log("LDI");
                                    break;

                                case 0xA1:  //CPI
                                    Log("CPI");
                                    break;

                                case 0xA2:  //INI
                                    Log("INI");
                                    break;

                                case 0xA3:  //OUTI
                                    Log("OUTI");
                                    break;

                                case 0xA8:  //LDD
                                    Log("LDD");
                                    break;

                                case 0xA9:  //CPD
                                    Log("CPD");
                                    break;

                                case 0xAA:  //IND
                                    Log("IND");
                                    break;

                                case 0xAB:  //OUTD
                                    Log("OUTD");
                                    break;

                                case 0xB0:  //LDIR
                                    Log("LDIR");
                                    break;

                                case 0xB1:  //CPIR
                                    Log("CPIR");
                                    break;

                                case 0xB2:  //INIR
                                    Log("INIR");
                                    break;

                                case 0xB3:  //OTIR
                                    Log("OTIR");
                                    break;

                                case 0xB8:  //LDDR
                                    Log("LDDR");
                                    break;

                                case 0xB9:  //CPDR
                                    Log("CPDR");
                                    break;

                                case 0xBA:  //INDR
                                    Log("INDR");
                                    break;

                                case 0xBB:  //OTDR
                                    Log("OTDR");
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
                        switch (opcode = PeekByte((ushort)PC++)) {
                            #region Addition instructions
                            case 0x09:  //ADD IY, BC
                                Log("ADD IY, BC");
                                break;

                            case 0x19:  //ADD IY, DE
                                Log("ADD IY, DE");
                                break;

                            case 0x29:  //ADD IY, IY
                                Log("ADD IY, IY");
                                break;

                            case 0x39:  //ADD IY, SP
                                Log("ADD IY, SP");
                                break;

                            case 0x84:  //ADD A, IYH
                                Log("ADD A, IYH");
                                break;

                            case 0x85:  //ADD A, IYL
                                Log("ADD A, IYL");
                                break;

                            case 0x86:  //Add A, (IY+d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("ADD A, (IY + {0:D})", disp);
                                PC++;
                                break;

                            case 0x8C:  //ADC A, IYH
                                Log("ADC A, IYH");
                                break;

                            case 0x8D:  //ADC A, IYL
                                Log("ADC A, IYL");
                                break;

                            case 0x8E: //ADC A, (IY+d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("ADC A, (IY + {0:D})", disp);
                                PC++;
                                break;
                            #endregion

                            #region Subtraction instructions
                            case 0x94:  //SUB A, IYH
                                Log("SUB A, IYH");
                                break;

                            case 0x95:  //SUB A, IYL
                                Log("SUB A, IYL");
                                break;

                            case 0x96:  //SUB (IY + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("SUB (IY + {0:D})", disp);
                                PC++;
                                break;

                            case 0x9C:  //SBC A, IYH
                                Log("SBC A, IYH");
                                break;

                            case 0x9D:  //SBC A, IYL
                                Log("SBC A, IYL");
                                break;

                            case 0x9E:  //SBC A, (IY + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("SBC A, (IY + {0:D})", disp);
                                PC++;
                                break;
                            #endregion

                            #region Increment/Decrements
                            case 0x23:  //INC IY
                                Log("INC IY");
                                break;

                            case 0x24:  //INC IYH
                                Log("INC IYH");
                                break;

                            case 0x25:  //DEC IYH
                                Log("DEC IYH");
                                break;

                            case 0x2B:  //DEC IY
                                Log("DEC IY");
                                break;

                            case 0x2C:  //INC IYL
                                Log("INC IYL");
                                break;

                            case 0x2D:  //DEC IYL
                                Log("DEC IYL");
                                break;

                            case 0x34:  //INC (IY + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("INC (IY + {0:D})", disp);
                                PC++;
                                break;

                            case 0x35:  //DEC (IY + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("DEC (IY + {0:D})", disp);
                                PC++;
                                break;
                            #endregion

                            #region Bitwise operators

                            case 0xA4:  //AND IYH
                                Log("AND IYH");
                                break;

                            case 0xA5:  //AND IYL
                                Log("AND IYL");
                                break;

                            case 0xA6:  //AND (IY + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("AND (IY + {0:D})", disp);
                                PC++;
                                break;

                            case 0xAC:  //XOR IYH
                                Log("XOR IYH");
                                break;

                            case 0xAD:  //XOR IYL
                                Log("XOR IYL");
                                break;

                            case 0xAE:  //XOR (IY + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("XOR (IY + {0:D})", disp);
                                PC++;
                                break;

                            case 0xB4:  //OR IYH
                                Log("OR IYH");
                                break;

                            case 0xB5:  //OR IYL
                                Log("OR IYL");
                                break;

                            case 0xB6:  //OR (IY + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("OR (IY + {0:D})", disp);
                                PC++;
                                break;
                            #endregion

                            #region Compare operator
                            case 0xBC:  //CP IYH
                                Log("CP IYH");
                                break;

                            case 0xBD:  //CP IYL
                                Log("CP IYL");
                                break;

                            case 0xBE:  //CP (IY + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("CP (IY + {0:D})", disp & 0xff);
                                PC++;
                                break;
                            #endregion

                            #region Load instructions
                            case 0x21:  //LD IY, nn
                                Log("LD IY, {0:D}", PeekWord((ushort)PC));
                                PC += 2;
                                break;

                            case 0x22:  //LD (nn), IY
                                Log("LD ({0:D}), IY", PeekWord((ushort)PC));
                                PC += 2;
                                break;

                            case 0x26:  //LD IYH, n
                                Log("LD IYH, {0:D}", PeekByte((ushort)PC));
                                PC++;
                                break;

                            case 0x2A:  //LD IY, (nn)
                                Log("LD IY, ({0:D})", PeekWord((ushort)PC));
                                PC += 2;
                                break;

                            case 0x2E:  //LD IYL, n
                                Log("LD IYL, {0:D}", PeekByte((ushort)PC));
                                PC++;
                                break;

                            case 0x36:  //LD (IY + d), n
                                disp = GetDisplacement(PeekByte((ushort)PC));

                                Log("LD (IY + {0:D}), {1,0:D}", disp, PeekByte((ushort)(PC + 1)));
                                PC += 2;
                                break;

                            case 0x44:  //LD B, IYH
                                Log("LD B, IYH");
                                break;

                            case 0x45:  //LD B, IYL
                                Log("LD B, IYL");
                                break;

                            case 0x46:  //LD B, (IY + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD B, (IY + {0:D})", disp);
                                PC++;
                                break;

                            case 0x4C:  //LD C, IYH
                                Log("LD C, IYH");
                                break;

                            case 0x4D:  //LD C, IYL
                                Log("LD C, IYL");
                                break;

                            case 0x4E:  //LD C, (IY + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD C, (IY + {0:D})", disp);
                                PC++;
                                break;

                            case 0x54:  //LD D, IYH
                                Log("LD D, IYH");
                                break;

                            case 0x55:  //LD D, IYL
                                Log("LD D, IYL");
                                break;

                            case 0x56:  //LD D, (IY + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD D, (IY + {0:D})", disp);
                                PC++;
                                break;

                            case 0x5C:  //LD E, IYH
                                Log("LD E, IYH");
                                break;

                            case 0x5D:  //LD E, IYL
                                Log("LD E, IYL");
                                break;

                            case 0x5E:  //LD E, (IY + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD E, (IY + {0:D})", disp);
                                PC++;
                                break;

                            case 0x60:  //LD IYH, B
                                Log("LD IYH, B");
                                break;

                            case 0x61:  //LD IYH, C
                                Log("LD IYH, C");
                                break;

                            case 0x62:  //LD IYH, D
                                Log("LD IYH, D");
                                break;

                            case 0x63:  //LD IYH, E
                                Log("LD IYH, E");
                                break;

                            case 0x64:  //LD IYH, IYH
                                Log("LD IYH, IYH");
                                break;

                            case 0x65:  //LD IYH, IYL
                                Log("LD IYH, IYL");
                                break;

                            case 0x66:  //LD H, (IY + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD H, (IY + {0:D})", disp);
                                PC++;
                                break;

                            case 0x67:  //LD IYH, A
                                Log("LD IYH, A");
                                break;

                            case 0x68:  //LD IYL, B
                                Log("LD IYL, B");
                                break;

                            case 0x69:  //LD IYL, C
                                Log("LD IYL, C");
                                break;

                            case 0x6A:  //LD IYL, D
                                Log("LD IYL, D");
                                break;

                            case 0x6B:  //LD IYL, E
                                Log("LD IYL, E");
                                break;

                            case 0x6C:  //LD IYL, IYH
                                Log("LD IYL, IYH");
                                break;

                            case 0x6D:  //LD IYL, IYL
                                Log("LD IYL, IYL");
                                break;

                            case 0x6E:  //LD L, (IY + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD L, (IY + {0:D})", disp);
                                PC++;
                                break;

                            case 0x6F:  //LD IYL, A
                                Log("LD IYL, A");
                                break;

                            case 0x70:  //LD (IY + d), B
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD (IY + {0:D}), B", disp);
                                PC++;
                                break;

                            case 0x71:  //LD (IY + d), C
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD (IY + {0:D}), C", disp);
                                PC++;
                                break;

                            case 0x72:  //LD (IY + d), D
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD (IY + {0:D}), D", disp);
                                PC++;
                                break;

                            case 0x73:  //LD (IY + d), E
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD (IY + {0:D}), E", disp);
                                PC++;
                                break;

                            case 0x74:  //LD (IY + d), H
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD (IY + {0:D}), H", disp);
                                PC++;
                                break;

                            case 0x75:  //LD (IY + d), L
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD (IY + {0:D}), L", disp);
                                PC++;
                                break;

                            case 0x77:  //LD (IY + d), A
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD (IY + {0:D}), A", disp);
                                PC++;
                                break;

                            case 0x7C:  //LD A, IYH
                                Log("LD A, IYH");
                                break;

                            case 0x7D:  //LD A, IYL
                                Log("LD A, IYL");
                                break;

                            case 0x7E:  //LD A, (IY + d)
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                Log("LD A, (IY + {0:D})", disp);
                                PC++;
                                break;

                            case 0xF9:  //LD SP, IY
                                Log("LD SP, IY");
                                break;
                            #endregion

                            #region All FDCB instructions
                            case 0xCB:
                                disp = GetDisplacement(PeekByte((ushort)PC));
                                PC++;
                                opcode = PeekByte((ushort)PC);      //The opcode comes after the offset byte!
                                PC++;
                                switch (opcode) {
                                    case 0x00: //LD B, RLC (IY+d)
                                        Log("LD B, RLC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x01: //LD C, RLC (IY+d)
                                        Log("LD C, RLC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x02: //LD D, RLC (IY+d)
                                        Log("LD D, RLC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x03: //LD E, RLC (IY+d)
                                        Log("LD E, RLC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x04: //LD H, RLC (IY+d)
                                        Log("LD H, RLC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x05: //LD L, RLC (IY+d)
                                        Log("LD L, RLC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x06:  //RLC (IY + d)
                                        Log("RLC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x07: //LD A, RLC (IY+d)
                                        Log("LD A, RLC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x08: //LD B, RRC (IY+d)
                                        Log("LD B, RRC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x09: //LD C, RRC (IY+d)
                                        Log("LD C, RRC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x0A: //LD D, RRC (IY+d)
                                        Log("LD D, RRC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x0B: //LD E, RRC (IY+d)
                                        Log("LD E, RRC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x0C: //LD H, RRC (IY+d)
                                        Log("LD H, RRC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x0D: //LD L, RRC (IY+d)
                                        Log("LD L, RRC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x0E:  //RRC (IY + d)
                                        Log("RRC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x0F: //LD A, RRC (IY+d)
                                        Log("LD A, RRC (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x10: //LD B, RL (IY+d)
                                        Log("LD B, RL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x11: //LD C, RL (IY+d)
                                        Log("LD C, RL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x12: //LD D, RL (IY+d)
                                        Log("LD D, RL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x13: //LD E, RL (IY+d)
                                        Log("LD E, RL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x14: //LD H, RL (IY+d)
                                        Log("LD H, RL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x15: //LD L, RL (IY+d)
                                        Log("LD L, RL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x16:  //RL (IY + d)
                                        Log("RL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x17: //LD A, RL (IY+d)
                                        Log("LD A, RL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x18: //LD B, RR (IY+d)
                                        Log("LD B, RR (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x19: //LD C, RR (IY+d)
                                        Log("LD C, RR (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x1A: //LD D, RR (IY+d)
                                        Log("LD D, RR (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x1B: //LD E, RR (IY+d)
                                        Log("LD E, RR (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x1C: //LD H, RR (IY+d)
                                        Log("LD H, RR (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x1D: //LD L, RRC (IY+d)
                                        Log("LD L, RR (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x1E:  //RR (IY + d)
                                        Log("RR (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x1F: //LD A, RRC (IY+d)
                                        Log("LD A, RR (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x20: //LD B, SLA (IY+d)
                                        Log("LD B, SLA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x21: //LD C, SLA (IY+d)
                                        Log("LD C, SLA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x22: //LD D, SLA (IY+d)
                                        Log("LD D, SLA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x23: //LD E, SLA (IY+d)
                                        Log("LD E, SLA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x24: //LD H, SLA (IY+d)
                                        Log("LD H, SLA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x25: //LD L, SLA (IY+d)
                                        Log("LD L, SLA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x26:  //SLA (IY + d)
                                        Log("SLA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x27: //LD A, SLA (IY+d)
                                        Log("LD A, SLA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x28: //LD B, SRA (IY+d)
                                        Log("LD B, SRA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x29: //LD C, SRA (IY+d)
                                        Log("LD C, SRA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x2A: //LD D, SRA (IY+d)
                                        Log("LD D, SRA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x2B: //LD E, SRA (IY+d)
                                        Log("LD E, SRA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x2C: //LD H, SRA (IY+d)
                                        Log("LD H, SRA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x2D: //LD L, SRA (IY+d)
                                        Log("LD L, SRA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x2E:  //SRA (IY + d)
                                        Log("SRA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x2F: //LD A, SRA (IY+d)
                                        Log("LD A, SRA (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x30: //LD B, SLL (IY+d)
                                        Log("LD B, SLL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x31: //LD C, SLL (IY+d)
                                        Log("LD C, SLL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x32: //LD D, SLL (IY+d)
                                        Log("LD D, SLL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x33: //LD E, SLL (IY+d)
                                        Log("LD E, SLL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x34: //LD H, SLL (IY+d)
                                        Log("LD H, SLL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x35: //LD L, SLL (IY+d)
                                        Log("LD L, SLL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x36:  //SLL (IY + d)
                                        Log("SLL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x37: //LD A, SLL (IY+d)
                                        Log("LD A, SLL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x38: //LD B, SRL (IY+d)
                                        Log("LD B, SRL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x39: //LD C, SRL (IY+d)
                                        Log("LD C, SRL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x3A: //LD D, SRL (IY+d)
                                        Log("LD D, SRL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x3B: //LD E, SRL (IY+d)
                                        Log("LD E, SRL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x3C: //LD H, SRL (IY+d)
                                        Log("LD H, SRL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x3D: //LD L, SRL (IY+d)
                                        Log("LD L, SRL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x3E:  //SRL (IY + d)
                                        Log("SRL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x3F: //LD A, SRL (IY+d)
                                        Log("LD A, SRL (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x40:  //BIT 0, (IY + d)
                                        Log("BIT 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x41:  //BIT 0, (IY + d)
                                        Log("BIT 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x42:  //BIT 0, (IY + d)
                                        Log("BIT 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x43:  //BIT 0, (IY + d)
                                        Log("BIT 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x44:  //BIT 0, (IY + d)
                                        Log("BIT 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x45:  //BIT 0, (IY + d)
                                        Log("BIT 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x46:  //BIT 0, (IY + d)
                                        Log("BIT 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x47:  //BIT 0, (IY + d)
                                        Log("BIT 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x48:  //BIT 1, (IY + d)
                                        Log("BIT 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x49:  //BIT 1, (IY + d)
                                        Log("BIT 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x4A:  //BIT 1, (IY + d)
                                        Log("BIT 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x4B:  //BIT 1, (IY + d)
                                        Log("BIT 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x4C:  //BIT 1, (IY + d)
                                        Log("BIT 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x4D:  //BIT 1, (IY + d)
                                        Log("BIT 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x4E:  //BIT 1, (IY + d)
                                        Log("BIT 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x4F:  //BIT 1, (IY + d)
                                        Log("BIT 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x50:  //BIT 2, (IY + d)
                                        Log("BIT 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x51:  //BIT 2, (IY + d)
                                        Log("BIT 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x52:  //BIT 2, (IY + d)
                                        Log("BIT 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x53:  //BIT 2, (IY + d)
                                        Log("BIT 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x54:  //BIT 2, (IY + d)
                                        Log("BIT 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x55:  //BIT 2, (IY + d)
                                        Log("BIT 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x56:  //BIT 2, (IY + d)
                                        Log("BIT 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x57:  //BIT 2, (IY + d)
                                        Log("BIT 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x58:  //BIT 3, (IY + d)
                                        Log("BIT 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x59:  //BIT 3, (IY + d)
                                        Log("BIT 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x5A:  //BIT 3, (IY + d)
                                        Log("BIT 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x5B:  //BIT 3, (IY + d)
                                        Log("BIT 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x5C:  //BIT 3, (IY + d)
                                        Log("BIT 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x5D:  //BIT 3, (IY + d)
                                        Log("BIT 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x5E:  //BIT 3, (IY + d)
                                        Log("BIT 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x5F:  //BIT 3, (IY + d)
                                        Log("BIT 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x60:  //BIT 4, (IY + d)
                                        Log("BIT 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x61:  //BIT 4, (IY + d)
                                        Log("BIT 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x62:  //BIT 4, (IY + d)
                                        Log("BIT 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x63:  //BIT 4, (IY + d)
                                        Log("BIT 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x64:  //BIT 4, (IY + d)
                                        Log("BIT 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x65:  //BIT 4, (IY + d)
                                        Log("BIT 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x66:  //BIT 4, (IY + d)
                                        Log("BIT 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x67:  //BIT 4, (IY + d)
                                        Log("BIT 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x68:  //BIT 5, (IY + d)
                                        Log("BIT 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x69:  //BIT 5, (IY + d)
                                        Log("BIT 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x6A:  //BIT 5, (IY + d)
                                        Log("BIT 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x6B:  //BIT 5, (IY + d)
                                        Log("BIT 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x6C:  //BIT 5, (IY + d)
                                        Log("BIT 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x6D:  //BIT 5, (IY + d)
                                        Log("BIT 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x6E:  //BIT 5, (IY + d)
                                        Log("BIT 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x6F:  //BIT 5, (IY + d)
                                        Log("BIT 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x70:  //BIT 6, (IY + d)
                                        Log("BIT 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x71:  //BIT 6, (IY + d)
                                        Log("BIT 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x72:  //BIT 6, (IY + d)
                                        Log("BIT 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x73:  //BIT 6, (IY + d)
                                        Log("BIT 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x74:  //BIT 6, (IY + d)
                                        Log("BIT 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x75:  //BIT 6, (IY + d)
                                        Log("BIT 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x76:  //BIT 6, (IY + d)
                                        Log("BIT 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x77:  //BIT 6, (IY + d)
                                        Log("BIT 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x78:  //BIT 7, (IY + d)
                                        Log("BIT 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x79:  //BIT 7, (IY + d)
                                        Log("BIT 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x7A:  //BIT 7, (IY + d)
                                        Log("BIT 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x7B:  //BIT 7, (IY + d)
                                        Log("BIT 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x7C:  //BIT 7, (IY + d)
                                        Log("BIT 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x7D:  //BIT 7, (IY + d)
                                        Log("BIT 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x7E:  //BIT 7, (IY + d)
                                        Log("BIT 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x7F:  //BIT 7, (IY + d)
                                        Log("BIT 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x80: //LD B, RES 0, (IY+d)
                                        Log("LD B, RES 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x81: //LD C, RES 0, (IY+d)
                                        Log("LD C, RES 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x82: //LD D, RES 0, (IY+d)
                                        Log("LD D, RES 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x83: //LD E, RES 0, (IY+d)
                                        Log("LD E, RES 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x84: //LD H, RES 0, (IY+d)
                                        Log("LD H, RES 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x85: //LD L, RES 0, (IY+d)
                                        Log("LD L, RES 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x86:  //RES 0, (IY + d)
                                        Log("RES 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x87: //LD A, RES 0, (IY+d)
                                        Log("LD A, RES 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x88: //LD B, RES 1, (IY+d)
                                        Log("LD B, RES 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x89: //LD C, RES 1, (IY+d)
                                        Log("LD C, RES 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x8A: //LD D, RES 1, (IY+d)
                                        Log("LD D, RES 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x8B: //LD E, RES 1, (IY+d)
                                        Log("LD E, RES 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x8C: //LD H, RES 1, (IY+d)
                                        Log("LD H, RES 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x8D: //LD L, RES 1, (IY+d)
                                        Log("LD L, RES 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x8E:  //RES 1, (IY + d)
                                        Log("RES 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x8F: //LD A, RES 1, (IY+d)
                                        Log("LD A, RES 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x90: //LD B, RES 2, (IY+d)
                                        Log("LD B, RES 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x91: //LD C, RES 2, (IY+d)
                                        Log("LD C, RES 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x92: //LD D, RES 2, (IY+d)
                                        Log("LD D, RES 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x93: //LD E, RES 2, (IY+d)
                                        Log("LD E, RES 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x94: //LD H, RES 2, (IY+d)
                                        Log("LD H, RES 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x95: //LD L, RES 2, (IY+d)
                                        Log("LD L, RES 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x96:  //RES 2, (IY + d)
                                        Log("RES 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x97: //LD A, RES 2, (IY+d)
                                        Log("LD A, RES 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x98: //LD B, RES 3, (IY+d)
                                        Log("LD B, RES 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x99: //LD C, RES 3, (IY+d)
                                        Log("LD C, RES 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x9A: //LD D, RES 3, (IY+d)
                                        Log("LD D, RES 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x9B: //LD E, RES 3, (IY+d)
                                        Log("LD E, RES 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x9C: //LD H, RES 3, (IY+d)
                                        Log("LD H, RES 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x9D: //LD L, RES 3, (IY+d)
                                        Log("LD L, RES 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x9E:  //RES 3, (IY + d)
                                        Log("RES 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0x9F: //LD A, RES 3, (IY+d)
                                        Log("LD A, RES 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA0: //LD B, RES 4, (IY+d)
                                        Log("LD B, RES 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA1: //LD C, RES 4, (IY+d)
                                        Log("LD C, RES 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA2: //LD D, RES 4, (IY+d)
                                        Log("LD D, RES 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA3: //LD E, RES 4, (IY+d)
                                        Log("LD E, RES 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA4: //LD H, RES 4, (IY+d)
                                        Log("LD H, RES 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA5: //LD L, RES 4, (IY+d)
                                        Log("LD L, RES 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA6:  //RES 4, (IY + d)
                                        Log("RES 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA7: //LD A, RES 4, (IY+d)
                                        Log("LD A, RES 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA8: //LD B, RES 5, (IY+d)
                                        Log("LD B, RES 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xA9: //LD C, RES 5, (IY+d)
                                        Log("LD C, RES 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xAA: //LD D, RES 5, (IY+d)
                                        Log("LD D, RES 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xAB: //LD E, RES 5, (IY+d)
                                        Log("LD E, RES 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xAC: //LD H, RES 5, (IY+d)
                                        Log("LD H, RES 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xAD: //LD L, RES 5, (IY+d)
                                        Log("LD L, RES 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xAE:  //RES 5, (IY + d)
                                        Log("RES 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xAF: //LD A, RES 5, (IY+d)
                                        Log("LD A, RES 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB0: //LD B, RES 6, (IY+d)
                                        Log("LD B, RES 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB1: //LD C, RES 6, (IY+d)
                                        Log("LD C, RES 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB2: //LD D, RES 6, (IY+d)
                                        Log("LD D, RES 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB3: //LD E, RES 6, (IY+d)
                                        Log("LD E, RES 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB4: //LD H, RES 5, (IY+d)
                                        Log("LD H, RES 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB5: //LD L, RES 5, (IY+d)
                                        Log("LD L, RES 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB6:  //RES 6, (IY + d)
                                        Log("RES 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB7: //LD A, RES 5, (IY+d)
                                        Log("LD A, RES 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB8: //LD B, RES 7, (IY+d)
                                        Log("LD B, RES 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xB9: //LD C, RES 7, (IY+d)
                                        Log("LD C, RES 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xBA: //LD D, RES 7, (IY+d)
                                        Log("LD D, RES 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xBB: //LD E, RES 7, (IY+d)
                                        Log("LD E, RES 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xBC: //LD H, RES 7, (IY+d)
                                        Log("LD H, RES 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xBD: //LD L, RES 7, (IY+d)
                                        Log("LD L, RES 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xBE:  //RES 7, (IY + d)
                                        Log("RES 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xBF: //LD A, RES 7, (IY+d)
                                        Log("LD A, RES 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC0: //LD B, SET 0, (IY+d)
                                        Log("LD B, SET 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC1: //LD C, SET 0, (IY+d)
                                        Log("LD C, SET 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC2: //LD D, SET 0, (IY+d)
                                        Log("LD D, SET 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC3: //LD E, SET 0, (IY+d)
                                        Log("LD E, SET 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC4: //LD H, SET 0, (IY+d)
                                        Log("LD H, SET 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC5: //LD L, SET 0, (IY+d)
                                        Log("LD L, SET 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC6:  //SET 0, (IY + d)
                                        Log("SET 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC7: //LD A, SET 0, (IY+d)
                                        Log("LD A, SET 0, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC8: //LD B, SET 1, (IY+d)
                                        Log("LD B, SET 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xC9: //LD C, SET 0, (IY+d)
                                        Log("LD C, SET 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xCA: //LD D, SET 1, (IY+d)
                                        Log("LD D, SET 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xCB: //LD E, SET 1, (IY+d)
                                        Log("LD E, SET 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xCC: //LD H, SET 1, (IY+d)
                                        Log("LD H, SET 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xCD: //LD L, SET 1, (IY+d)
                                        Log("LD L, SET 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xCE:  //SET 1, (IY + d)
                                        Log("SET 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xCF: //LD A, SET 1, (IY+d)
                                        Log("LD A, SET 1, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD0: //LD B, SET 2, (IY+d)
                                        Log("LD B, SET 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD1: //LD C, SET 2, (IY+d)
                                        Log("LD C, SET 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD2: //LD D, SET 2, (IY+d)
                                        Log("LD D, SET 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD3: //LD E, SET 2, (IY+d)
                                        Log("LD E, SET 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD4: //LD H, SET 21, (IY+d)
                                        Log("LD H, SET 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD5: //LD L, SET 2, (IY+d)
                                        Log("LD L, SET 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD6:  //SET 2, (IY + d)
                                        Log("SET 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD7: //LD A, SET 2, (IY+d)
                                        Log("LD A, SET 2, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD8: //LD B, SET 3, (IY+d)
                                        Log("LD B, SET 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xD9: //LD C, SET 3, (IY+d)
                                        Log("LD C, SET 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xDA: //LD D, SET 3, (IY+d)
                                        Log("LD D, SET 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xDB: //LD E, SET 3, (IY+d)
                                        Log("LD E, SET 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xDC: //LD H, SET 21, (IY+d)
                                        Log("LD H, SET 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xDD: //LD L, SET 3, (IY+d)
                                        Log("LD L, SET 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xDE:  //SET 3, (IY + d)
                                        Log("SET 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xDF: //LD A, SET 3, (IY+d)
                                        Log("LD A, SET 3, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE0: //LD B, SET 4, (IY+d)
                                        Log("LD B, SET 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE1: //LD C, SET 4, (IY+d)
                                        Log("LD C, SET 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE2: //LD D, SET 4, (IY+d)
                                        Log("LD D, SET 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE3: //LD E, SET 4, (IY+d)
                                        Log("LD E, SET 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE4: //LD H, SET 4, (IY+d)
                                        Log("LD H, SET 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE5: //LD L, SET 3, (IY+d)
                                        Log("LD L, SET 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE6:  //SET 4, (IY + d)
                                        Log("SET 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE7: //LD A, SET 4, (IY+d)
                                        Log("LD A, SET 4, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE8: //LD B, SET 5, (IY+d)
                                        Log("LD B, SET 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xE9: //LD C, SET 5, (IY+d)
                                        Log("LD C, SET 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xEA: //LD D, SET 5, (IY+d)
                                        Log("LD D, SET 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xEB: //LD E, SET 5, (IY+d)
                                        Log("LD E, SET 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xEC: //LD H, SET 5, (IY+d)
                                        Log("LD H, SET 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xED: //LD L, SET 5, (IY+d)
                                        Log("LD L, SET 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xEE:  //SET 5, (IY + d)
                                        Log("SET 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xEF: //LD A, SET 5, (IY+d)
                                        Log("LD A, SET 5, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF0: //LD B, SET 6, (IY+d)
                                        Log("LD B, SET 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF1: //LD C, SET 6, (IY+d)
                                        Log("LD C, SET 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF2: //LD D, SET 6, (IY+d)
                                        Log("LD D, SET 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF3: //LD E, SET 6, (IY+d)
                                        Log("LD E, SET 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF4: //LD H, SET 6, (IY+d)
                                        Log("LD H, SET 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF5: //LD L, SET 6, (IY+d)
                                        Log("LD L, SET 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF6:  //SET 6, (IY + d)
                                        Log("SET 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF7: //LD A, SET 6, (IY+d)
                                        Log("LD A, SET 6, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF8: //LD B, SET 7, (IY+d)
                                        Log("LD B, SET 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xF9: //LD C, SET 7, (IY+d)
                                        Log("LD C, SET 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xFA: //LD D, SET 7, (IY+d)
                                        Log("LD D, SET 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xFB: //LD E, SET 7, (IY+d)
                                        Log("LD E, SET 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xFC: //LD H, SET 7, (IY+d)
                                        Log("LD H, SET 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xFD: //LD L, SET 7, (IY+d)
                                        Log("LD L, SET 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xFE:  //SET 7, (IY + d)
                                        Log("SET 7, (IY + {0:D})", disp & 0xff);
                                        break;

                                    case 0xFF: //LD A, SET 7, (IY + D)
                                        Log("LD A, SET 7, (IY + {0:D})", disp & 0xff);
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
                                Log("POP IY");
                                break;

                            case 0xE5:  //PUSH IY
                                Log("PUSH IY");
                                break;
                            #endregion

                            #region Exchange instruction
                            case 0xE3:  //EX (SP), IY
                                Log("EX (SP), IY");
                                break;
                            #endregion

                            #region Jump instruction
                            case 0xE9:  //JP (IY)
                                Log("JP (IY)");
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

                if (rebuild) {
                    OpcodeDisassembly newOD = new OpcodeDisassembly(this);

                    newOD.Address = (ushort)address;
                    newOD.BytesAtAddress = byteList;
                    newOD.Opcodes = opcodeString;
                    newOD.Param1 = param1;
                    newOD.Param2 = param2;
                    disassemblyList.Add(newOD);
                } else if (!traceOn) {
                    if (opcodeString == disassemblyList[line].Opcodes) {
                        //opcodeMatches++;
                        //if (opcodeMatches > 5)
                        //    break;
                        if (param1 == disassemblyList[line].Param1 && param2 == disassemblyList[line].Param2) {
                            line++;
                            continue;
                        }
                    } else
                        opcodeMatches = 0;

                    disassemblyList[line].BytesAtAddress = byteList;
                    disassemblyList[line].Opcodes = opcodeString;
                    disassemblyList[line].Param1 = param1;
                    disassemblyList[line].Param2 = param2;
                } else {
                    if (useHexNumbers)
                        TraceMessage.address = previousPC.ToString("x");
                    else
                        TraceMessage.address = previousPC.ToString();

                    if (useHexNumbers)
                        TraceMessage.opcodes = opcodeString.Replace(":D", ":x2");
                    else
                        TraceMessage.opcodes = opcodeString.Replace(":x2", ":D");

                    if (param2 != int.MaxValue) {
                        TraceMessage.opcodes = String.Format(TraceMessage.opcodes, disassemblyList[line].Param1, disassemblyList[line].Param2);
                    } else if (param1 != int.MaxValue) {
                        TraceMessage.opcodes = String.Format(TraceMessage.opcodes, disassemblyList[line].Param1);
                    }
                }
                line++;
                if (line > 65535) {
                    break;
                }
            }

            if (!traceOn && opcodeMatches < 5) {
                disassemblyLookup.Clear();
                for (int f = 0; f < disassemblyList.Count; ++f) {
                    int index = -1;
                    if(!disassemblyLookup.TryGetValue(disassemblyList[f].Address, out index))
                        disassemblyLookup.Add(disassemblyList[f].Address, f);
                }
            }
        }

        private void Monitor_Load(object sender, EventArgs e) {
            pauseEmulation = true;
            cpu = ziggyWin.zx.cpu;
            ReRegisterAllEvents();

            Disassemble(0, 65535, true, false);
            //dataGridView1.VirtualMode = true;
            dataGridView1.DataSource = disassemblyList;
            dataGridView1.Refresh();

            for (int i = 0; i < 65535; i += 10) {
                MemoryUnit mu = new MemoryUnit(this);
                mu.Address = i;
                mu.Bytes = new List<int>();
                for (int g = 0; g < 10; g++) {
                    if (i + g > 65535)
                        break;
                    mu.Bytes.Add(ziggyWin.zx.PeekByteNoContend((ushort)(i + g)));
                }
                memoryViewList.Add(mu);
            }

            UpdateZXState();
            dbState = MonitorState.PAUSE;
        }

        private void dataGridView2_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e) {
        }

        //Double click on a DGV column sets or resets a breakpoint
        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e) {
            if (e.RowIndex == -1) {
                return;
            }
            
            if(e.ColumnIndex == 1)
            {
                DataGridViewCell cell = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex];
                cell.ReadOnly = false;
                Console.WriteLine(cell.ReadOnly);
                //dataGridView1.CurrentCell = cell;
                dataGridView1.BeginEdit(true); 
                return;

            }
            KeyValuePair<SPECCY_EVENT, BreakPointCondition> kv = new KeyValuePair<SPECCY_EVENT, BreakPointCondition>(SPECCY_EVENT.OPCODE_PC, new BreakPointCondition(SPECCY_EVENT.OPCODE_PC, disassemblyList[e.RowIndex].Address, -1));
            //int index = disassemblyList.Find("Address", disassemblyList[e.RowIndex].Address);
            bool found = false;
            foreach (KeyValuePair<SPECCY_EVENT, BreakPointCondition> breakpoint in breakPointList)
            {
                if (breakpoint.Equals(kv)) {
                    found = true;
                    break;
                }
            }

            if (found) {
                breakPointConditions.Remove(kv.Value);
                breakPointList.Remove(kv);
                //dataGridView1.Rows[e.RowIndex].HeaderCell.Style.BackColor = Control.DefaultBackColor;
                breakpointRowList.Remove(e.RowIndex);
            } else {
                breakPointList.Add(kv);
                breakPointConditions.Add(kv.Value);
                //dataGridView1.Rows[e.RowIndex].HeaderCell.Style.BackColor = System.Drawing.Color.Red;
                breakpointRowList.Add(e.RowIndex);
            }
            dataGridView1.Refresh();
        }

        public void RemoveBreakpoint(KeyValuePair<SPECCY_EVENT, Monitor.BreakPointCondition> _kv)
        {
            foreach (KeyValuePair<SPECCY_EVENT, Monitor.BreakPointCondition> breakpoint in breakPointList)
            {
                if (breakpoint.Equals(_kv)) {
                    if (_kv.Key == SPECCY_EVENT.OPCODE_PC)
                    {
                        int index = disassemblyList.Find("Address", _kv.Value.Address);
                        if (index >= 0) {
                            breakpointRowList.Remove(index);
                            dataGridView1.Refresh();
                        }
                    }

                    breakPointConditions.Remove(_kv.Value);
                    breakPointList.Remove(_kv);
                    break;
                }
            }
        }

        //Clear all breakpoints
        public void RemoveAllBreakpoints() {
            foreach (KeyValuePair<SPECCY_EVENT, BreakPointCondition> breakpoint in breakPointList)
            {
                if (breakpoint.Key == SPECCY_EVENT.OPCODE_PC)
                {
                    int index = disassemblyList.Find("Address", breakpoint.Value.Address);
                    if (index >= 0)
                        dataGridView1.Rows[index].HeaderCell.Style.BackColor = Control.DefaultBackColor;
                }
            }
            breakPointList.Clear();
            breakPointConditions.Clear();
            breakpointRowList.Clear();
            dataGridView1.Refresh();
        }

        //Remember to unregister all events and inform the parent form
        //that the debugger is no longer in "active" debugging state.
        //TODO: We don't seem to be telling the parent form that we aren't debuggin anymore, properly.
        private void Monitor_FormClosing(object sender, FormClosingEventArgs e) {
            DeRegisterAllEvents();
            disassemblyList.Clear();
            dataGridView1.DataSource = null;
            disassemblyLookup.Clear();
            breakPointList.Clear();
            breakPointConditions.Clear();

            logList.Clear();

            memoryViewList.Clear();

            if (breakpointViewer != null && !breakpointViewer.IsDisposed)
                breakpointViewer.Close();

            if (memoryViewer != null && !memoryViewer.IsDisposed)
                memoryViewer.Close();

            if (profiler != null && !profiler.IsDisposed)
                profiler.Close();

            if (registerViewer != null && !registerViewer.IsDisposed)
                registerViewer.Close();

            if (machineState != null && !machineState.IsDisposed)
                machineState.Close();

            if(watchWindow != null && !watchWindow.IsDisposed)
                watchWindow.Close();

            SetState(0);
            ziggyWin.zx.doRun = true;
            ziggyWin.zx.ResetKeyboard();
            ziggyWin.Focus();
            e.Cancel = true;
            this.Hide();
        }

        //8-bit/16-bit number view toggle
        private void byteCheck_CheckedChanged(object sender, EventArgs e) {
            //HexAnd8bitRegUpdate();
            if (registerViewer != null && !registerViewer.IsDisposed)
                registerViewer.RefreshView(useHexNumbers);
        }

        private void label29_Click(object sender, EventArgs e) {
        }

        public void AddBreakpoint(KeyValuePair<SPECCY_EVENT, Monitor.BreakPointCondition> _kv)
        {
            bool found = false;
            foreach (KeyValuePair<SPECCY_EVENT, Monitor.BreakPointCondition> breakpoint in breakPointList)
            {
                if (breakpoint.Equals(_kv)) {
                    found = true;
                    break;
                }
            }

            if (!found) {
                if (_kv.Key == SPECCY_EVENT.OPCODE_PC)
                {
                    int index = disassemblyList.Find("Address", _kv.Value.Address);
                    if (index >= 0) {
                        breakpointRowList.Add(index);
                        dataGridView1.Refresh();
                    }
                }

                breakPointList.Add(_kv);
                breakPointConditions.Add(_kv.Value);
            }
        }
        
        public void AddWatchVariable(int addr, string label)
        {
            WatchVariable wv = new WatchVariable();
            wv.Address = addr;
            wv.Data = ziggyWin.zx.PeekByteNoContend((ushort)addr);

            if(label == "")
                systemVariables.TryGetValue(addr, out label);

            wv.Label = label;
            watchVariableList.Add(wv);
        }

        public void RemoveWatchVariable(int addr)
        {
            for(int i = 0; i < watchVariableList.Count; i++)
            {
                if(watchVariableList[i].Address == addr)
                {
                    watchVariableList.RemoveAt(i);
                    break;
                }
            }
        }

        public void RemoveAllWatchVariables()
        {
            watchVariableList.Clear();
            watchVariableList = new BindingList<WatchVariable>();
        }

        private void jumpAddrButton_Click(object sender, EventArgs e) {
            if (jumpAddrTextBox4.Text.Length < 1)
                return;

            int addr = -1;

            addr = Utilities.ConvertToInt(jumpAddrTextBox4.Text);

            if (addr > 65535) {
                System.Windows.Forms.MessageBox.Show("The address is not within 0 to 65535!", "Invalid input", MessageBoxButtons.OK);
                return;
            }

            JumpToAddress(addr);
        }

        public void JumpToAddress(int addr) {
            int index = -1;
            if (!disassemblyLookup.TryGetValue(addr, out index)) {
                index = disassemblyList.Find("Address", addr);
            }
            if (index < 0)
                index = 0;
            dataGridView1.FirstDisplayedScrollingRowIndex = index;
            dataGridView1.CurrentCell = dataGridView1.Rows[index].Cells[0];
            dataGridView1.Rows[index].Selected = true;
            dataGridView1.Refresh();
        }

        private void PClink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            JumpToAddress(pc);
        }

        private void HLlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            JumpToAddress(hl);
        }

        private void BClink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            JumpToAddress(bc);
        }

        private void DElink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            JumpToAddress(de);
        }

        private void IXlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            JumpToAddress(ix);
        }

        private void SPlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            JumpToAddress(sp);
        }

        private void HL_link_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            JumpToAddress(_hl);
        }

        private void AFlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            JumpToAddress(af);
        }

        private void IRlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            JumpToAddress(ir);
        }

        private void BC_link_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            JumpToAddress(_bc);
        }

        private void DE_link_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            JumpToAddress(_de);
        }

        private void AF_lnk_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            JumpToAddress(_af);
        }

        private void IYlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            JumpToAddress(iy);
        }

        private void MPlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
        }

        private void machineStateButton_Click(object sender, EventArgs e) {
            if (machineState == null || machineState.IsDisposed)
                machineState = new Machine_State();

            machineState.RefreshView(this.ziggyWin);
            machineState.Show();
            isMachineStateViewerOpen = true;
        }

        private void PokeMemoryButton_Click(object sender, EventArgs e) {
            pokeMemoryDialog = new PokeMemory(this);
            pokeMemoryDialog.Show();
        }

        private void ProfilerButton_Click(object sender, EventArgs e) {
            if (profiler == null || profiler.IsDisposed)
                profiler = new Profiler(this);

            profiler.Show();
            isProfilerOpen = true;
        }

        private void RegistersButton_Click(object sender, EventArgs e) {
            if (registerViewer == null || registerViewer.IsDisposed)
                registerViewer = new Registers(this);

            registerViewer.RefreshView(useHexNumbers);
            registerViewer.Show();
        }

        private void MemoryButton_Click(object sender, EventArgs e) {
            if (memoryViewer == null || memoryViewer.IsDisposed) {
                memoryViewer = new MemoryViewer(this);
            }

            memoryViewer.Show();
            memoryViewer.RefreshData(useHexNumbers);
        }

        private void BreakpointsButton_Click(object sender, EventArgs e) {
            if (breakpointViewer == null || breakpointViewer.IsDisposed)
                breakpointViewer = new Breakpoints(this);

            breakpointViewer.Show();
        }

        private void StepOverButton_Click(object sender, EventArgs e) {
            //if we are on the last address of disassembly, there is nothing to step-over to
            if (dataGridView1.CurrentRow.Index == dataGridView1.RowCount - 1) {
                dbState = MonitorState.STEPIN;

                ziggyWin.zx.doRun = true;
                previousPC = pc;
                previousTState = cpu.t_states;
                ziggyWin.zx.Resume();
                //ziggyWin.Focus();
                return;
            }

            string opcode = disassemblyList[dataGridView1.CurrentRow.Index].Opcodes;

            //Step over only if the current opcode is a function call
            if (!(opcode.Contains("CALL") || opcode.Contains("RST") || opcode.Contains("LDIR") || opcode.Contains("INIR")
                || opcode.Contains("INDR") || opcode.Contains("LDDR") || opcode.Contains("OTIR") || opcode.Contains("OTDR")
                || opcode.Contains("CPIR") || opcode.Contains("CPDR"))) {
                dbState = MonitorState.STEPIN;

                ziggyWin.zx.doRun = true;
                previousPC = pc;
                previousTState = cpu.t_states;
                ziggyWin.zx.Resume();
                //ziggyWin.Focus();
                return;
            }
            ziggyWin.zx.ResetKeyboard();
            dbState = MonitorState.STEPOVER;
            runToCursorAddress = disassemblyList[dataGridView1.CurrentRow.Index + 1].Address;

            ziggyWin.zx.doRun = true;
            previousPC = pc;
            previousTState = cpu.t_states;

            this.Hide();
            //ziggyWin.zx.monitorSaysRun = true;
            ziggyWin.zx.Resume();
            ziggyWin.Focus();
        }

        private void StepInButton_Click(object sender, EventArgs e) {
            previousPC = pc;
            ziggyWin.zx.ResetKeyboard();
            previousTState = cpu.t_states;
            SetState(MonitorState.STEPIN);
        }

        private void StopDebuggerButton_Click(object sender, EventArgs e) {
            this.Close();
        }

        private void HideWindow() {
            if (profiler != null && !profiler.IsDisposed) {
                isProfilerOpen = true;
                profiler.Hide();
            }

            if (memoryViewer != null && !memoryViewer.IsDisposed) {
                isMemoryViewerOpen = true;
                memoryViewer.Hide();
            }

            if (registerViewer != null && !registerViewer.IsDisposed) {
                isRegistersOpen = true;
                registerViewer.Hide();
            }

            if (breakpointViewer != null && !breakpointViewer.IsDisposed) {
                isBreakpointWindowOpen = true;
                breakpointViewer.Hide();
            }

            if (machineState != null && !machineState.IsDisposed) {
                isMachineStateViewerOpen = true;
                machineState.Hide();
            }

            if(watchWindow != null && !watchWindow.IsDisposed)
            {
                isWatchWindowOpen = true;
                watchWindow.Hide();
            }

            if (callStackViewer != null && !callStackViewer.IsDisposed) {
                isCallStackViewerOpen = true;
                callStackViewer.Hide();
            }

            this.Hide();
        }

        private void RunToCursorButton_Click(object sender, EventArgs e) {
            dbState = MonitorState.RUN;
            runToCursorAddress = disassemblyList[dataGridView1.CurrentRow.Index].Address;

            ziggyWin.zx.doRun = true;
            ziggyWin.zx.ResetKeyboard();
            previousPC = pc;
            previousTState = cpu.t_states;
            HideWindow();
            //ziggyWin.zx.monitorSaysRun = true;
            ziggyWin.zx.Resume();
            ziggyWin.Focus();
        }

        private void ResumeEmulationButton_Click(object sender, EventArgs e) {
            ziggyWin.zx.ResetKeyboard();
            previousPC = pc;
            previousTState = cpu.t_states;

            HideWindow();
            SetState(0);
        }

        private void ToggleBreakpointButton_Click(object sender, EventArgs e) {
            int rowIndex = dataGridView1.CurrentRow.Index;

            if (rowIndex == -1) {
                return;
            }
            KeyValuePair<SPECCY_EVENT, BreakPointCondition> kv = new KeyValuePair<SPECCY_EVENT, BreakPointCondition>(SPECCY_EVENT.OPCODE_PC, new BreakPointCondition(SPECCY_EVENT.OPCODE_PC, disassemblyList[rowIndex].Address, -1));

            bool found = false;
            foreach (KeyValuePair<SPECCY_EVENT, BreakPointCondition> breakpoint in breakPointList)
            {
                if (breakpoint.Equals(kv)) {
                    found = true;
                    break;
                }
            }

            if (found) {
                breakPointConditions.Remove(kv.Value);
                breakPointList.Remove(kv);
                breakpointRowList.Remove(rowIndex);
                //dataGridView1.Rows[rowIndex].HeaderCell.Style.BackColor = Control.DefaultBackColor;
            } else {
                breakPointList.Add(kv);
                breakPointConditions.Add(kv.Value);
                breakpointRowList.Add(rowIndex);
                //dataGridView1.Rows[rowIndex].HeaderCell.Style.BackColor = System.Drawing.Color.Red;
            }
            dataGridView1.Refresh();
        }

        private void ClearAllBreakpointsButton_Click(object sender, EventArgs e) {
            RemoveAllBreakpoints();
        }
        

        // to draw breakpoint
        private void dataGridView1_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e) {
            e.PaintHeader(DataGridViewPaintParts.All & ~DataGridViewPaintParts.ContentBackground);
            // Paint Circle on the rowHeader if needed
            if (breakpointRowList.Contains(e.RowIndex)) {
                // Gets the color to draw the circle with
                System.Drawing.Color rowColor = System.Drawing.Color.Red;
                System.Drawing.Brush circleColor = new System.Drawing.SolidBrush(rowColor);
                // Draw the circle
                e.Graphics.DrawImage(Properties.Resources.BreakpointEnabled_6584_16x, new System.Drawing.Point(e.RowBounds.Location.X + 3, e.RowBounds.Location.Y + 4));
                //e.Graphics.FillEllipse(circleColor, e.RowBounds.Location.X + 5,
                //                              e.RowBounds.Location.Y + 4, 10, 10);
            }

            if (e.RowIndex == dataGridView1.CurrentRow.Index)
                e.Graphics.DrawImage(Properties.Resources.arrow_Next_16xSM, new System.Drawing.Point(e.RowBounds.Location.X + 4, e.RowBounds.Location.Y + 4));
        }

        private void ASCIIModeButton_CheckedChanged(object sender, EventArgs e) {
        }

        private void hexButton_CheckedChanged(object sender, EventArgs e) {
        }

        private void StepOutButton_Click(object sender, EventArgs e) {
            previousPC = pc;
            ziggyWin.zx.ResetKeyboard();
            previousTState = cpu.t_states;
            SetState(MonitorState.STEPOUT);
        }

        private void aSCIICharactersToolStripMenuItem_CheckedChanged(object sender, EventArgs e) {
            if (aSCIICharactersToolStripMenuItem.Checked) {
                this.dataGridView1.Columns[1].DefaultCellStyle.Format = "x2";
                numbersInHexToolStripMenuItem.Checked = false;
            }
            dataGridView1.Refresh();
        }

        private void numbersInHexToolStripMenuItem_CheckedChanged(object sender, EventArgs e) {
            useHexNumbers = numbersInHexToolStripMenuItem.Checked;

            if (useHexNumbers) {
                this.dataGridView1.Columns[0].DefaultCellStyle.Format = "x2";
                aSCIICharactersToolStripMenuItem.Checked = false;
            } else {
                this.dataGridView1.Columns[0].DefaultCellStyle.Format = "";
            }

            UpdateToolsWindows();
        }

        private void systemVariablesToolStripMenuItem_CheckedChanged(object sender, EventArgs e) {
            showSysVars = systemVariablesToolStripMenuItem.Checked;
            dataGridView1.Refresh();
        }

        private void breakpointsEditorToolStripMenuItem_Click(object sender, EventArgs e) {
            BreakpointsButton_Click(sender, e);
        }

        private void memoryViewerToolStripMenuItem_Click(object sender, EventArgs e) {
            MemoryButton_Click(sender, e);
        }

        private void registersToolStripMenuItem_Click(object sender, EventArgs e) {
            RegistersButton_Click(sender, e);
        }

        private void executionLogToolStripMenuItem_Click(object sender, EventArgs e) {
            ProfilerButton_Click(sender, e);
        }

        private void pokeMemoryToolStripMenuItem_Click(object sender, EventArgs e) {
            PokeMemoryButton_Click(sender, e);
        }

        private void loadSymbolsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.Title = "Select Symbol File";
            openFileDialog1.FileName = "";
            openFileDialog1.Filter = "All supported files|*.txt;*.csv";

            if(openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                using(StreamReader reader = new StreamReader(openFileDialog1.FileName))
                {
                    int count = 0;
                    while(!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        var values = line.Split(',');
                        int addr = Utilities.ConvertToInt(values[0]);
                        if (addr > 65535)
                            continue;

                        if (addr >= 0) {
                            systemVariables[addr] = Convert.ToString(values[1]).Trim();
                            count++;
                        }
                    }
                }
                MessageBox.Show("Symbols loaded successfully!", "Symbol file", MessageBoxButtons.OK);
            }
        }

        private void watchMemoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(watchWindow == null || watchWindow.IsDisposed)
                watchWindow = new WatchWindow(this);

            watchWindow.Show();
        }

        private void toolsToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void toggleBreakpointToolStripMenuItem_Click(object sender, EventArgs e) {
            ToggleBreakpointButton_Click(sender, e);
        }

        private void clearAllBreakpointsToolStripMenuItem_Click(object sender, EventArgs e) {
            ClearAllBreakpointsButton_Click(sender, e);
        }

        private void resumeEmulationToolStripMenuItem_Click(object sender, EventArgs e) {
            ResumeEmulationButton_Click(sender, e);
        }

        private void runToCursorToolStripMenuItem_Click(object sender, EventArgs e) {
            RunToCursorButton_Click(sender, e);
        }

        private void stopDebuggingToolStripMenuItem_Click(object sender, EventArgs e) {
            StopDebuggerButton_Click(sender, e);
        }

        private void stepInToolStripMenuItem_Click(object sender, EventArgs e) {
            StepInButton_Click(sender, e);
        }

        private void stepOutToolStripMenuItem_Click(object sender, EventArgs e) {
            StepOutButton_Click(sender, e);
        }

        private void stepOverToolStripMenuItem_Click(object sender, EventArgs e) {
            StepOverButton_Click(sender, e);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e) {
            FileStream fs;
            //Disassembly
            {
                try {
                    saveFileDialog1.Title = "Save Log";
                    saveFileDialog1.FileName = "disassembly.log";

                    if (saveFileDialog1.ShowDialog() == DialogResult.OK) {
                        fs = new FileStream(saveFileDialog1.FileName, FileMode.Create, FileAccess.Write);
                        StreamWriter sw = new StreamWriter(fs);

                        if (useHexNumbers) {
                            sw.WriteLine("All numbers in hex.");
                            sw.WriteLine("-------------------");
                        } else {
                            sw.WriteLine("All numbers in decimal.");
                            sw.WriteLine("-----------------------");
                        }

                        foreach (OpcodeDisassembly od in disassemblyList) {
                            sw.WriteLine("{0,-5}   {1,-15}   {2,-20}", od.AddressAsString, od.BytesAtAddressAsString, od.Opcodes);
                        }
                        sw.Close();
                        //System.Windows.Forms.MessageBox.Show("A log of the disassembly has been saved.",
                        //                "Log created!", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.None);
                    }
                } catch {
                    System.Windows.Forms.MessageBox.Show("Zero was unable to create a file! Either the disk is full, or there is a problem with access rights to the folder or something else entirely!",
                            "File Write Error!", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                }
            }
        }

        private void machineStateToolStripMenuItem_Click(object sender, EventArgs e) {
            machineStateButton_Click(sender, e);
        }

        private void Monitor_Shown(object sender, EventArgs e) {
        }

        private void Monitor_VisibleChanged(object sender, EventArgs e) {
            if (this.Visible) {
                if (isBreakpointWindowOpen) {
                    if (breakpointViewer.IsDisposed)
                        BreakpointsButton_Click(this, e);
                    else
                        breakpointViewer.Show();
                }

                if (isProfilerOpen) {
                    if (profiler.IsDisposed)
                        ProfilerButton_Click(this, e);
                    else
                        profiler.Show();
                }

                if (isMemoryViewerOpen) {
                    if (memoryViewer.IsDisposed)
                        MemoryButton_Click(this, e);
                    else
                        memoryViewer.Show();
                }

                if (isRegistersOpen) {
                    if (registerViewer.IsDisposed)
                        RegistersButton_Click(this, e);
                    else
                        registerViewer.Show();
                }

                if (isMachineStateViewerOpen) {
                    if (machineState.IsDisposed)
                        machineStateButton_Click(this, e);
                    else
                        machineState.Show();
                }

                if(isWatchWindowOpen)
                {
                    if(watchWindow.IsDisposed)
                        watchMemoryToolStripMenuItem_Click(this, e);
                    else
                        watchWindow.Show();
                }

                if (isCallStackViewerOpen)
                    callStackViewer.Show();

                isCallStackViewerOpen = false;
                isBreakpointWindowOpen = false;
                isProfilerOpen = false;
                isMemoryViewerOpen = false;
                isRegistersOpen = false;
                isMachineStateViewerOpen = false;
                isWatchWindowOpen = false;
            }
        }

        private void callStackButton_Click(object sender, EventArgs e) {
            if (callStackViewer == null || callStackViewer.IsDisposed)
                callStackViewer = new CallStackViewer(this);

            callStackViewer.Show();
        }

        private void heatMapToolStripMenuItem_Click(object sender, EventArgs e) {
            MemoryProfiler cp = new MemoryProfiler(ziggyWin);
            cp.Show();
        }
    }
}