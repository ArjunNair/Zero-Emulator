using System;
using System.IO;
using System.Threading.Tasks;
using Peripherals;
using Speccy;
using SpeccyCommon;

namespace Zero.Emulation
{
    /// <summary>
    /// The +3's two disk drives. The session remembers what is in each, so a disk survives a change
    /// of machine: switching away from the +3 writes it back and takes it out with the old machine,
    /// and coming back puts it in again.
    /// </summary>
    public sealed partial class EmulatorSession
    {
        private readonly string[] _diskPaths = new string[2];
        private volatile bool _diskMotorOn;

        /// <summary>Raised (emulation thread) when a disk goes in or out or the drive motor starts or stops.</summary>
        public event Action DisksChanged;

        /// <summary>The image in drive A (0) or B (1), or null.</summary>
        public string DiskInDrive(int unit) => _diskPaths[unit & 1];

        /// <summary>The +3's drive motor is running: the disk is being read or written.</summary>
        public bool DiskMotorOn => _diskMotorOn;

        /// <summary>The current machine has disk drives Zero can emulate (the +3).</summary>
        public bool HasDiskDrives => Model == MachineModel._plus3;

        /// <summary>
        /// Put a DSK image in a drive, switching to the +3 first if need be. An image that cannot be
        /// written (read-only file, or extended DSK) goes in write protected.
        /// </summary>
        public void InsertDisk(int unit, string path) => Post(() => InsertDiskCore(unit, path));

        public Task<bool> InsertDiskAsync(int unit, string path) => InvokeAsync(() => InsertDiskCore(unit, path));

        /// <summary>Take the disk out of a drive, writing back anything the Spectrum changed.</summary>
        public void EjectDisk(int unit) => Post(() => EjectDiskCore(unit));

        public Task EjectDiskAsync(int unit) => InvokeAsync(() => EjectDiskCore(unit));

        private bool InsertDiskCore(int unit, string path)
        {
            unit &= 1;
            if (!File.Exists(path) || !DskImage.IsDsk(path))
            {
                Error?.Invoke("This doesn't seem to be a +3 disk image (DSK): " + Path.GetFileName(path));
                return false;
            }
            _diskPaths[unit] = Path.GetFullPath(path);
            if (Model != MachineModel._plus3)
                CreateMachine(MachineModel._plus3);   // puts the remembered disks in, this one included
            else
                _zx.DiskInsert(_diskPaths[unit], (byte)unit);
            DisksChanged?.Invoke();
            return true;
        }

        private void EjectDiskCore(int unit)
        {
            unit &= 1;
            if (_diskPaths[unit] == null) return;
            if (_zx != null && Model == MachineModel._plus3)
                _zx.DiskEject((byte)unit);
            _diskPaths[unit] = null;
            DisksChanged?.Invoke();
        }

        /// <summary>A new machine has been made: give a +3 its disks back and listen for the motor (emulation thread).</summary>
        private void AttachDisks(zx_spectrum zx)
        {
            _diskMotorOn = false;
            zx.DiskEvent += OnDiskEvent;
            if (zx.model != MachineModel._plus3) return;
            for (int unit = 0; unit < 2; unit++)
            {
                if (_diskPaths[unit] == null) continue;
                if (File.Exists(_diskPaths[unit])) zx.DiskInsert(_diskPaths[unit], (byte)unit);
                else _diskPaths[unit] = null;  // gone from disk since it was inserted
            }
        }

        private void OnDiskEvent(object sender, DiskEventArgs e)
        {
            bool motor = (e.EventType & (1 << 4)) != 0;   // bit 4: motor; bits 0-3: drives with disks
            if (motor == _diskMotorOn) return;
            _diskMotorOn = motor;
            DisksChanged?.Invoke();
        }

        /// <summary>
        /// After inserting a disk: hard reset and choose Loader from the +3 menu, which boots the
        /// disk in drive A.
        /// </summary>
        private static readonly byte[] AutoLoadPlus3DiskKeys = { 13 };
    }
}
