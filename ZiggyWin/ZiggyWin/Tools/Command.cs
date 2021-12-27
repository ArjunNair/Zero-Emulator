using System;
using System.Text;

namespace ZeroWin.Tools
{
    public class Command
    {
        public virtual string Execute(string[] input) { return "Not implemented for " + this.GetType(); }
        public virtual string Error()
        {
            return "Unrecognized Command. Type 'help' for valid commands and syntax.";
        }
        public virtual string Help() { return "Not implemented for " + this.GetType(); }
    }

    public class TapeCommands: Command
    {
        private TapeDeck tapeDeck = null;
        public TapeCommands(TapeDeck td)
        {
            tapeDeck = td;
        }

        public override string Help()
        {
            string s = "Tape Commands:\n";
            return s + "tape stop|start|play\n";
        }

        public override string Execute(string[] input)
        {
            if(input[0] == "help")
                return Help();

            if (input[0] == "tape")
            {
                switch(input[1]) {
                    case "stop":
                        return tapeDeck.StopTape();
                    case "start":
                    case "play":
                        return tapeDeck.StartTape();
                    default:
                        break;
                }

            }

            return null;
        }
    }

    public class MemoryCommands: Command
    {
        Speccy.zx_spectrum zx = null;

        public MemoryCommands(Speccy.zx_spectrum _zx)
        {
            zx = _zx;
        }

        public override string Help()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Memory commands:\n");
            sb.Append("poke <addr> <val>[ <val2> <val3> ...]\n");
            sb.Append("poke <addr1> to <addr2> with <val>\n");
            sb.Append("peek <addr>[ <addr2> <addr3> ...]\n");
            sb.Append("peek <addr1> to <addr2>\n");
            return sb.ToString();
        }
        public override string Execute(string[] input)
        {
            if(input[0] == "help")
                return Help();

            if(input[0] == "poke")
            {
                try
                {
                    if(input.Length > 2 && input[2] == "to" && input[4] == "with")
                    {
                        int addr1 = Convert.ToInt32(input[1]);
                        int addr2 = Convert.ToInt32(input[3]);
                        int v = Convert.ToInt32(input[5]);

                        for(int i = 0; i < addr2 - addr1; i++)
                        {
                            zx.PokeByteNoContend(addr1 + i, v);
                        }
                        return "Done.";
                    }
                    int addr = Convert.ToInt32(input[1]);
                    for(int i = 2; i < input.Length; i++)
                    {
                        int v = Convert.ToInt32(input[i]);
                        zx.PokeByteNoContend(addr, v);
                        addr++;
                    }
                }
                catch (Exception e)
                {
                    return Error();
                }
                return "Done.";
            }

            if(input[0] == "peek")
            {
                try
                {
                    if(input.Length > 2 && input[2] == "to")
                    {
                        int addr1 = Convert.ToInt32(input[1]);
                        int addr2 = Convert.ToInt32(input[3]);
                        StringBuilder sb2 = new StringBuilder();

                        for(ushort i = 0; i < addr2 - addr1; i++)
                        {
                            byte b = zx.PeekByteNoContend((ushort)(addr1 + i));
                            sb2.Append((addr1 + i).ToString() + "\t" + b.ToString() + "\n");
                        }
                        return sb2.ToString();
                    }
                    StringBuilder sb3 = new StringBuilder();
                    for(int i = 1; i < input.Length; i++)
                    {
                        int addr = Convert.ToInt32(input[i]);
                        byte b = zx.PeekByteNoContend((ushort)addr);
                        sb3.Append(addr.ToString() + "\t" + b.ToString() + "\n");
                    }
                    return sb3.ToString();
                }
                catch (Exception e)
                {
                    return Error();
                }
            }
            return null;
        }
    }
}
