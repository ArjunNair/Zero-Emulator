namespace Peripherals
{
    public class WD1793
    {
        [System.Runtime.InteropServices.DllImport(@"wd1793.dll")]
        public static extern System.IntPtr wd1793_Initialise();

        [System.Runtime.InteropServices.DllImport(@"wd1793.dll")]
        private static extern void wd1793_ShutDown(System.IntPtr fdc);

        [System.Runtime.InteropServices.DllImport(@"wd1793.dll")]
        private static extern bool wd1793_InsertDisk(System.IntPtr fdc, byte unit, string filename);

        [System.Runtime.InteropServices.DllImport(@"wd1793.dll")]
        private static extern void wd1793_EjectDisks(System.IntPtr fdc);

        [System.Runtime.InteropServices.DllImport(@"wd1793.DLL")]
        private static extern void wd1793_EjectDisk(System.IntPtr fdc, byte _unit);

        [System.Runtime.InteropServices.DllImport(@"wd1793.DLL")]
        private static extern byte wd1793_ReadStatusReg(System.IntPtr fdc);

        [System.Runtime.InteropServices.DllImport(@"wd1793.DLL")]
        private static extern byte wd1793_ReadTrackReg(System.IntPtr fdc);

        [System.Runtime.InteropServices.DllImport(@"wd1793.DLL")]
        private static extern byte wd1793_ReadSectorReg(System.IntPtr fdc);

        [System.Runtime.InteropServices.DllImport(@"wd1793.DLL")]
        private static extern byte wd1793_ReadDataReg(System.IntPtr fdc);

        [System.Runtime.InteropServices.DllImport(@"wd1793.DLL")]
        private static extern byte wd1793_ReadSystemReg(System.IntPtr fdc);

        [System.Runtime.InteropServices.DllImport(@"wd1793.DLL")]
        private static extern void wd1793_WriteTrackReg(System.IntPtr fdc, byte _data);

        [System.Runtime.InteropServices.DllImport(@"wd1793.DLL")]
        private static extern void wd1793_WriteSectorReg(System.IntPtr fdc, byte _data);

        [System.Runtime.InteropServices.DllImport(@"wd1793.DLL")]
        private static extern void wd1793_WriteDataReg(System.IntPtr fdc, byte _data);

        [System.Runtime.InteropServices.DllImport(@"wd1793.DLL")]
        private static extern void wd1793_WriteSystemReg(System.IntPtr fdc, byte _data);

        [System.Runtime.InteropServices.DllImport(@"wd1793.DLL")]
        private static extern void wd1793_WriteCommandReg(System.IntPtr fdc, byte _data, ushort _pc);

        [System.Runtime.InteropServices.DllImport(@"wd1793.DLL")]
        private static extern bool wd1793_DiskInserted(System.IntPtr fdc, byte _unit);

        //[DllImport(@"wd1793.DLL")]
        //private static extern void wd1793_SCL2TRD(IntPtr fdc, byte _unit);

        protected System.IntPtr fdc = System.IntPtr.Zero;

        public void DiskInsert(string filename, byte _unit) {
            wd1793_InsertDisk(fdc, _unit, filename);
        }

        public void DiskEject(byte _unit) {
            if (fdc != System.IntPtr.Zero)
                wd1793_EjectDisk(fdc, _unit);
        }

        public byte ReadStatusReg() {
            return wd1793_ReadStatusReg(fdc);
        }

        public byte ReadSectorReg() {
            return wd1793_ReadSectorReg(fdc);
        }

        public byte ReadDataReg() {
            return wd1793_ReadDataReg(fdc);
        }

        public byte ReadTrackReg() {
            return wd1793_ReadTrackReg(fdc);
        }

        public byte ReadSystemReg() {
            return wd1793_ReadSystemReg(fdc);
        }

        public void WriteCommandReg(byte _data, ushort _pc) {
            wd1793_WriteCommandReg(fdc, _data, _pc);
        }

        public void WriteSectorReg(byte _data) {
            wd1793_WriteSectorReg(fdc, _data);
        }

        public void WriteTrackReg(byte _data) {
            wd1793_WriteTrackReg(fdc, _data);
        }

        public void WriteDataReg(byte _data) {
            wd1793_WriteDataReg(fdc, _data);
        }

        public void WriteSystemReg(byte _data) {
            wd1793_WriteSystemReg(fdc, _data);
        }

        public void DiskInitialise() {
            if (fdc != System.IntPtr.Zero)
                wd1793_ShutDown(fdc);

            fdc = wd1793_Initialise();
        }

        public void DiskShutdown() {
            if (fdc != System.IntPtr.Zero)
                wd1793_ShutDown(fdc);

            //OnDiskEvent(new DiskEventArgs(0));
        }
    }
}