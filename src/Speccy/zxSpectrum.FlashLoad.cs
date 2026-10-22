using Peripherals;

namespace Speccy
{
    // The tape as flash loading's handlers see it: Spin's live tape state, over the TapePlayer.
    public abstract partial class zx_spectrum : IFlashTape
    {
        int IFlashTape.State {
            get {
                switch (tape.Phase) {
                    case TapePhase.Pilot: return 0;
                    case TapePhase.Sync: return SyncNumber();
                    case TapePhase.Data:
                    case TapePhase.Tail: return 3;
                    case TapePhase.Pause: return 4;
                    case TapePhase.Samples: return 6;
                    default: return 5;
                }
            }
        }

        // The first sync after a pilot is Spin's state 1, any after it 2.
        private int SyncNumber() {
            PulseSegment p = tape.CurrentPulses;
            if (p == null) return 1;
            int before = 0;
            for (int r = tape.RunIndex - 1; r >= 0 && p.Runs[r].Phase == TapePhase.Sync; r--)
                before++;
            return before == 0 ? 1 : 2;
        }

        byte IFlashTape.BlockId {
            get {
                TapeBlock b = tape.CurrentBlock;
                if (b != null) return b.Id;
                var blocks = tape.Image.Blocks;
                return tape.BlockIndex < blocks.Count ? blocks[tape.BlockIndex].Id : (byte)0;
            }
        }

        int IFlashTape.PulseToneLen {
            get {
                PulseSegment p = tape.CurrentPulses;
                if (p == null || tape.RunIndex >= p.Runs.Length) return 0;
                PulseRun run = p.Runs[tape.RunIndex];
                if (run.Count > 1)
                    return (int)(run.Count - tape.PulseInRun + 1);
                long left = 0;
                for (int r = tape.RunIndex; r < p.Runs.Length; r++)
                    left += p.Runs[r].Count;
                return (int)(left - (tape.PulseInRun - 1));
            }
        }

        uint IFlashTape.PulseLen {
            get {
                PulseSegment p = tape.CurrentPulses;
                return p != null && tape.RunIndex < p.Runs.Length ? p.Runs[tape.RunIndex].Duration : 0;
            }
        }

        uint IFlashTape.AimTStates => tape.CurrentPulseLength;

        long IFlashTape.ToNextEdge => tape.TStatesToNextEdge;

        // The data playing, or the block's data still to come after its pilot.
        private DataSegment FlashData(out long bit) {
            DataSegment d = tape.CurrentData;
            if (d != null) {
                bit = tape.SymbolIndex * d.BitsPerSymbol;
                return d;
            }
            bit = 0;
            TapeBlock b = tape.CurrentBlock;
            if (b == null) return null;
            for (int s = tape.SegmentIndex + 1; s < b.Segments.Length; s++)
                if (b.Segments[s] is DataSegment next)
                    return next;
            return null;
        }

        int IFlashTape.DataPos {
            get {
                return FlashData(out long bit) != null ? (int)(bit >> 3) : 0;
            }
        }

        int IFlashTape.DataLen => FlashData(out _)?.ByteCount ?? 0;

        byte IFlashTape.CurDataByte {
            get {
                DataSegment d = FlashData(out long bit);
                long at = bit >> 3;
                return d != null && at < d.Data.Length ? d.Data[at] : (byte)0;
            }
        }

        byte IFlashTape.BitCounter {
            get {
                FlashData(out long bit);
                return (byte)(0x80 >> (int)(bit & 7));
            }
        }

        bool IFlashTape.EdgeGone => tape.CurrentData != null && tape.PulseInSymbol >= 1;

        bool IFlashTape.EdgeTrigger {
            get => tapeBitWasFlipped;
            set => tapeBitWasFlipped = value;
        }

        bool IFlashTape.EdgeTriggerAck {
            get => tapeBitFlipAck;
            set => tapeBitFlipAck = value;
        }

        bool IFlashTape.EarLevel => pulseLevel != 0;

        bool IFlashTape.EdgeUnseen => tapeEdgeUnseen;

        void IFlashTape.TakeBackUnseenEdge() {
            if (!tapeEdgeUnseen) return;
            FlashInvert();
            tapeBitWasFlipped = false;
            tapeBitFlipAck = true;
            tapeEdgeUnseen = false;
        }

        bool IFlashTape.Playing => tapeIsPlaying;

        void IFlashTape.Update(long tstates) {
            if (tstates != 0)
                tape.AddTime(tstates);
            UpdateTapePlayback();
        }

        void IFlashTape.EndToneAndFinishPulse() {
            tape.EndRun();
            FlashFinishPulse();
        }

        void IFlashTape.FinishPulse() => FlashFinishPulse();

        private void FlashFinishPulse() {
            if (!tape.IsActive) return;
            OnTapeEdge(tape.FinishPulse());
            UpdateTapePlayback();
        }

        int IFlashTape.ConsumeTapeBit() {
            DataSegment d = tape.CurrentData;
            if (d == null) {
                FlashFinishPulse();
                return 0;
            }
            IFlashTape t = this;
            int bit = (t.CurDataByte & t.BitCounter) != 0 ? 1 : 0;
            OnTapeEdge(tape.FinishSymbol(out int edges));
            // Spin's tape plays a bit out with one edge, so the level after a byte is where
            // the loader's own code left it: two pulses' edges make no flip, so make one.
            if (edges % 2 == 0)
                FlashInvert();
            UpdateTapePlayback();
            return bit;
        }

        private void FlashInvert() {
            tape.InvertLevel();
            ApplyTapeLevel();
        }

        void IFlashTape.MarkEdge() => FlashInvert();

        void IFlashTape.ConsumeEdge() {
            FlashInvert();
            tapeBitWasFlipped = false;
            tapeBitFlipAck = true;
        }

        void IFlashTape.ForwardOneBlock() {
            if (!tapeIsPlaying) return;
            OnTapeEdge(tape.ForwardOneBlock());
        }

        bool IFlashTape.EnsurePlaying() {
            if (!tapeIsPlaying && tape_AutoPlay && !tape_heldStopped && !tape.AtEnd)
                AutoStartTape();
            return tapeIsPlaying;
        }
    }
}
