namespace Peripherals
{
    /// <summary>
    /// WD1793 floppy controller and Beta 128 interface (TR-DOS) for the Pentagon: the surface
    /// Pentagon_128k talks to, over the managed port of Spin's Beta 128 (<see cref="Beta128"/>).
    /// Before the cross-platform port this wrapped the native x86 wd1793.dll, with the same calls.
    /// </summary>
    public class WD1793
    {
        private Beta128 beta;

        /// <summary>The controller, once initialised; null before that and after shutdown.</summary>
        public Beta128 Controller => beta;

        public void DiskInsert(string filename, byte _unit) {
            beta?.InsertDisk(_unit, filename);
        }

        public void DiskEject(byte _unit) {
            beta?.EjectDisk(_unit);
        }

        public byte ReadStatusReg() => beta?.ReadStatus() ?? 0xFF;

        public byte ReadSectorReg() => beta?.ReadSectorReg() ?? 0xFF;

        public byte ReadDataReg() => beta?.ReadData() ?? 0xFF;

        public byte ReadTrackReg() => beta?.ReadTrack() ?? 0xFF;

        public byte ReadSystemReg() => beta?.ReadSystemRegister() ?? 0xFF;

        // The program counter was for the DLL's debugging; the controller does not need it.
        public void WriteCommandReg(byte _data, ushort _pc) {
            beta?.WriteCommand(_data);
        }

        public void WriteSectorReg(byte _data) {
            beta?.WriteSectorReg(_data);
        }

        public void WriteTrackReg(byte _data) {
            beta?.WriteTrack(_data);
        }

        public void WriteDataReg(byte _data) {
            beta?.WriteData(_data);
        }

        public void WriteSystemReg(byte _data) {
            beta?.WriteSystemRegister(_data);
        }

        public void DiskReset() {
            beta?.Reset();
        }

        public void DiskInitialise() {
            beta?.FlushAll();
            beta = new Beta128();
        }

        /// <summary>Write back every disk the Spectrum changed, and release the controller.</summary>
        public void DiskShutdown() {
            beta?.FlushAll();
            beta = null;
        }
    }
}
