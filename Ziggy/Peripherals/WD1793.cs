namespace Peripherals
{
    /// <summary>
    /// WD1793 floppy controller (Beta 128 / TR-DOS) — managed stub.
    ///
    /// The original implementation lived in the native x86 wd1793.dll, which was dropped in the
    /// cross-platform port. This class keeps the public surface intact so the Pentagon machine
    /// compiles and boots; it reports "no drive ready" (0xFF on all reads) so TR-DOS fails cleanly.
    /// A managed WD1793 can replace the bodies below without touching callers.
    /// </summary>
    public class WD1793
    {
        // Fix for index mark toggle in Seek command (woody). Preserved for the future managed port.
        protected byte current_command = 0;
        protected byte status_read_count = 0;

        public void DiskInsert(string filename, byte _unit) { }

        public void DiskEject(byte _unit) { }

        public byte ReadStatusReg() {
            status_read_count += 1;
            return 0xFF;
        }

        public byte ReadSectorReg() { return 0xFF; }

        public byte ReadDataReg() { return 0xFF; }

        public byte ReadTrackReg() { return 0xFF; }

        public byte ReadSystemReg() { return 0xFF; }

        public void WriteCommandReg(byte _data, ushort _pc) {
            current_command = _data;
        }

        public void WriteSectorReg(byte _data) { }

        public void WriteTrackReg(byte _data) { }

        public void WriteDataReg(byte _data) { }

        public void WriteSystemReg(byte _data) { }

        public void DiskInitialise() { }

        public void DiskShutdown() { }
    }
}
