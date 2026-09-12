using Zero.TestSupport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Speccy;
using Cpu;
using Xunit;
using Xunit.Abstractions;

namespace Zero.Core.Tests
{
    /// <summary>
    /// Runs Frank Cringle's ZEXALL instruction exerciser against the Z80 core in isolation:
    /// flat 64K RAM, CP/M BDOS console calls trapped at 0x0005, warm boot at 0x0000 ends a run.
    /// Each of the 67 sub-tests runs as its own theory case so failures are attributable.
    /// </summary>
    /// <remarks>Slow (~1 min per case, ~1 h total). Excluded from the default run: dotnet test --filter "Category!=Zexall".</remarks>
    [Trait("Category", "Zexall")]
    public class ZexallTests
    {
        private const ushort ImageBase = 0x0100;
        private const ushort TestTable = 0x013A;
        private const ushort BeginMessage = 0x1DDA;
        private const ushort EndMessage = 0x1DF6;
        private const ushort InitialSpPointer = 0x0006;
        private const ushort Bdos = 0x0005;
        private const byte RetOpcode = 0xC9;

        private readonly ITestOutputHelper _output;

        public ZexallTests(ITestOutputHelper output) { _output = output; }

        private static byte[] Image => File.ReadAllBytes(Path.Combine(TestPaths.AssetsDir, "zexall.com"));

        public static IEnumerable<object[]> TestIds()
        {
            byte[] image = Image;
            for (int id = 0; ; id++)
            {
                int p = TestTable - ImageBase + id * 2;
                int ptr = image[p] | (image[p + 1] << 8);
                if (ptr == 0) yield break;
                string name = ReadTestName(image, ptr - ImageBase);
                yield return new object[] { id, name };
            }
        }

        // Each test record ends with a '$'-terminated description string; the fixed part is 0x40 bytes (3 x 20-byte tstr + 4-byte crc).
        private static string ReadTestName(byte[] image, int record)
        {
            var sb = new StringBuilder();
            for (int i = record + 0x40; i < image.Length && image[i] != (byte)'$'; i++)
                sb.Append((char)image[i]);
            return sb.ToString().Trim().TrimStart('\x19', '\x00', '\x0a', '\x0d').Trim();
        }

        [Theory]
        [MemberData(nameof(TestIds))]
        public void Zexall(int id, string name)
        {
            byte[] mem = new byte[65536];
            byte[] image = Image;
            Array.Copy(image, 0, mem, ImageBase, image.Length);

            // Silence the banner/footer, return immediately from BDOS, and give CP/M a stack pointer.
            mem[BeginMessage] = (byte)'$';
            mem[EndMessage] = (byte)'$';
            mem[Bdos] = RetOpcode;
            mem[InitialSpPointer] = 0x00;
            mem[InitialSpPointer + 1] = 0xC0;

            // Run only the selected test by truncating the table after it.
            int src = TestTable + id * 2;
            mem[TestTable] = mem[src];
            mem[TestTable + 1] = mem[src + 1];
            mem[TestTable + 2] = 0;
            mem[TestTable + 3] = 0;

            var cpu = new Z80();
            cpu.PeekByte = a => mem[a];
            cpu.PeekWord = a => (ushort)(mem[a] | (mem[(ushort)(a + 1)] << 8));
            cpu.PokeByte = (a, v) => mem[a] = v;
            cpu.PokeWord = (a, v) => { mem[a] = (byte)v; mem[(ushort)(a + 1)] = (byte)(v >> 8); };
            cpu.Contend = (reg, times, count) => { };
            cpu.In = a => 0xFF;
            cpu.Out = (a, v) => { };
            cpu.InstructionFetchSignal = () => { };
            cpu.TapeEdgeDetection = () => { };
            cpu.TapeEdgeDecA = () => { };
            cpu.TapeEdgeCpA = () => { };
            cpu.OnError += msg => throw new InvalidOperationException("Z80 core error: " + msg);

            cpu.HardReset();
            cpu.regs.PC = ImageBase;

            var console = new StringBuilder();
            long steps = 0;
            while (true)
            {
                ushort pc = cpu.regs.PC;
                if (pc == 0) break;
                if (pc == Bdos)
                {
                    int fn = cpu.regs.C;
                    if (fn == 2)
                        console.Append((char)cpu.regs.E);
                    else if (fn == 9)
                    {
                        ushort de = (ushort)cpu.regs.DE;
                        for (int i = 0; i < 200 && mem[(ushort)(de + i)] != (byte)'$'; i++)
                            console.Append((char)mem[(ushort)(de + i)]);
                    }
                }
                cpu.Step();
                cpu.t_states = 0; // keep the counter from wrapping; timing is irrelevant here
                if (++steps > 20_000_000_000L)
                    throw new TimeoutException("zexall test '" + name + "' did not finish");
            }

            string text = console.ToString();
            _output.WriteLine(text.Trim());
            Assert.Contains("OK", text);
            Assert.DoesNotContain("ERROR", text);
        }
    }
}
