namespace Peripherals
{
    /// <summary>
    /// µPD765 floppy controller (+3 disk) — managed stub.
    ///
    /// The original implementation lived in the native x86 fdc765.dll, which was dropped in the
    /// cross-platform port. Public surface preserved; status/data reads return 0xFF ("drive not
    /// ready") so +3DOS fails cleanly instead of hanging. Replace the bodies with a managed FDC
    /// when +3 disk support returns.
    /// </summary>
    public class UDP765
    {
        public bool DiskWriteProtect {
            get;
            set;
        }

        public byte DiskReadByte() { return 0xFF; }

        public void DiskWriteByte(byte _data) { }

        public byte DiskStatusRead() { return 0xFF; }

        public void DiskMotorState(byte _state) { }

        public void DiskInsert(string filename, byte _unit) { }

        public void DiskEject(byte _unit) { }

        public void DiskReset() { }

        public void DiskInitialise() { }

        public void DiskShutdown() { }
    }
}
