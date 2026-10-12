namespace Peripherals
{
    /// <summary>
    /// µPD765 floppy controller for the +3: the surface zx_plus3 talks to, over the managed port of
    /// fdc765 (<see cref="Fdc765"/>). Before the cross-platform port this wrapped the native x86
    /// fdc765.dll through P/Invoke, with the same calls.
    /// </summary>
    public class UDP765
    {
        private Fdc765 fdc;

        public bool DiskWriteProtect {
            get;
            set;
        }

        /// <summary>The controller, once initialised; null before that and after shutdown.</summary>
        public Fdc765 Controller => fdc;

        public byte DiskReadByte() {
            if (fdc != null)
                return fdc.DataPortRead();

            return 0;
        }

        public void DiskWriteByte(byte _data) {
            if ((fdc != null) && (!DiskWriteProtect))
                fdc.DataPortWrite(_data);
        }

        public byte DiskStatusRead() {
            if (fdc != null)
                return fdc.StatusPortRead();

            return 0;
        }

        public void DiskMotorState(byte _state) {
            fdc?.SetMotorState(_state);
        }

        public void DiskInsert(string filename, byte _unit) {
            fdc?.InsertDisk(filename, _unit);
        }

        public void DiskEject(byte _unit) {
            fdc?.EjectDisk(_unit);
        }

        public void DiskReset() {
            fdc?.ResetDevice();
        }

        public void DiskInitialise() {
            fdc?.Dispose();
            fdc = new Fdc765();
        }

        /// <summary>Eject both drives, writing back any disk the Spectrum changed, and release the controller.</summary>
        public void DiskShutdown() {
            fdc?.Dispose();
            fdc = null;
        }
    }
}
