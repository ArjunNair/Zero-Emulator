using Cpu;
using Peripherals;
using Speccy;
using Xunit;

namespace Zero.Core.Tests
{
    // Edge loading's loop detector and skip, driven by hand on a bare Z80.
    public class EdgeLoaderTests
    {
        private const int Loop = 0x8000, Other = 0x9000;
        private const long Period = 59; // the ROM's LD-SAMPLE loop

        private readonly Z80 cpu = new Z80();
        private readonly EdgeLoader edge;
        private long now = 1000;

        public EdgeLoaderTests()
        {
            edge = new EdgeLoader(cpu);
            cpu.regs.A = 0x7F;
            cpu.regs.B = 0x10;
        }

        // One pass of a loop counting in B: B steps, time passes, the IN comes round.
        private bool Pass(int pc = Loop, int step = 1, byte port = 0xFE, TapePhase phase = TapePhase.Pilot, long gap = Period)
        {
            cpu.regs.B = (byte)(cpu.regs.B + step);
            now += gap;
            return edge.Observe(now, pc, port, phase);
        }

        private void Verify(int pc = Loop, TapePhase phase = TapePhase.Pilot)
        {
            int i = 0;
            do Pass(pc, phase: phase); while (!edge.Verified && ++i < 12);
            Assert.True(edge.Verified);
        }

        [Fact]
        public void A_register_stepping_by_one_every_pass_is_a_loader_loop()
        {
            int passes = 0;
            while (!Pass()) passes++;
            // the first look, then the register found, eight steps in a row, and a ninth to confirm
            Assert.Equal(11, passes + 1);
            Assert.Equal(Period, edge.Period);
            Assert.True(edge.CanAccelerate);
        }

        [Fact]
        public void Counting_down_works_too()
        {
            for (int i = 0; i < 12 && !edge.Verified; i++) Pass(step: -1);
            Assert.True(edge.Verified);
        }

        [Fact]
        public void Steps_must_be_in_the_same_register_every_pass()
        {
            for (int i = 0; i < 30; i++)
            {
                if (i % 3 == 0) { cpu.regs.C++; now += Period; edge.Observe(now, Loop, 0xFE, TapePhase.Pilot); }
                else Pass();
            }
            Assert.False(edge.Verified);
        }

        [Fact]
        public void A_changed_or_another_port_is_no_tape_loop()
        {
            for (int i = 0; i < 20; i++) { cpu.regs.A++; Pass(); }
            Assert.False(edge.Verified);
            cpu.regs.A = 0x7F;
            for (int i = 0; i < 20; i++) Pass(port: 0xFD);
            Assert.False(edge.Verified);
        }

        [Fact]
        public void A_slow_pass_starts_the_count_again()
        {
            for (int i = 0; i < 6; i++) Pass();
            Pass(gap: 200);
            for (int i = 0; i < 6; i++) Pass();
            Assert.False(edge.Verified);
        }

        [Fact]
        public void Skip_steps_to_the_pass_that_sees_the_edge()
        {
            Verify();
            byte before = cpu.regs.B;
            Assert.Equal(10 * Period, edge.Skip(9 * Period + 1)); // the edge falls in the tenth pass
            Assert.Equal((byte)(before + 10), cpu.regs.B);
            Assert.Equal(3 * Period, edge.Skip(3 * Period)); // exactly on a pass: that pass sees it
        }

        [Fact]
        public void Skip_stops_short_of_the_loader_s_timeout()
        {
            Verify();
            cpu.regs.B = 250;
            Assert.Equal(5 * Period, edge.Skip(1000 * Period));
            Assert.Equal(255, cpu.regs.B); // the loader's own INC B then runs out

            var down = new EdgeLoaderTests();
            for (int i = 0; i < 12 && !down.edge.Verified; i++) down.Pass(step: -1);
            down.cpu.regs.B = 4;
            Assert.Equal(3 * Period, down.edge.Skip(1000 * Period));
            Assert.Equal(1, down.cpu.regs.B);
        }

        [Fact]
        public void A_confirmed_loop_is_remembered_when_the_loader_comes_back_to_it()
        {
            Verify(Loop);
            Verify(Other);
            Assert.True(Pass(Loop)); // straight away, not after another ten passes
            Assert.True(Pass(Other));
        }

        [Fact]
        public void A_remembered_loop_is_for_its_own_phase_and_register_set()
        {
            Verify(Loop, TapePhase.Pilot);
            Pass(Other);
            Assert.False(Pass(Loop, phase: TapePhase.Data)); // the same code counting in another register

            Pass(Other);
            cpu.exx(); // one EXX: B is now the other set's B
            Assert.False(Pass(Loop, phase: TapePhase.Pilot));
        }

        [Fact]
        public void A_loop_that_carries_on_past_an_edge_is_left_alone()
        {
            Verify();
            edge.Skip(5 * Period);
            edge.EdgeTaken();
            Pass(); // back a single pass later: it didn't leave on the edge
            Assert.False(edge.CanAccelerate);
            Pass(Other);
            Pass(Loop);
            Assert.True(edge.Verified);
            Assert.False(edge.CanAccelerate); // and remembered as such
        }

        [Fact]
        public void A_loader_is_active_for_a_frame_after_its_loop_was_last_seen()
        {
            Assert.False(edge.LoaderActive(now));
            Verify();
            Assert.True(edge.LoaderActive(now + 69888));
            Assert.False(edge.LoaderActive(now + 69889)); // it stopped polling: delays are the program's own
        }

        [Fact]
        public void A_loop_that_leaves_on_an_edge_keeps_being_skipped()
        {
            Verify();
            edge.Skip(5 * Period);
            edge.EdgeTaken();
            Pass(gap: 400); // it did something with the edge before coming back
            Assert.True(edge.CanAccelerate);
        }
    }
}
