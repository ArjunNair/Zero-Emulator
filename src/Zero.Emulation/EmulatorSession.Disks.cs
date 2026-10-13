using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Peripherals;
using Speccy;
using SpeccyCommon;

namespace Zero.Emulation
{
    /// <summary>
    /// The disk drives: the +3's two, and the four on the Pentagon's Beta 128 interface (TR-DOS).
    /// The session remembers what is in each, so a disk survives a change of machine: switching
    /// away writes it back and takes it out with the old machine, and coming back puts it in again.
    /// The +3's disks and the Pentagon's are remembered separately, since neither takes the other's.
    /// </summary>
    /// <summary>What a drive's busy light is doing.</summary>
    public enum DiskLight { Off, Dim, Bright }

    public sealed partial class EmulatorSession
    {
        private enum DiskSystem { Plus3, Beta }

        private readonly string[] _plus3Disks = new string[2];
        private readonly string[] _betaDisks = new string[Beta128.BetaDriveCount];

        // The drives the deck shows: the current machine's, or on a machine without any, the ones
        // last used (their disks are still remembered).
        private DiskSystem _diskSystem = DiskSystem.Plus3;
        private volatile bool _diskMotorOn;

        /// <summary>Raised (emulation thread) when a disk goes in or out or the drive motor starts or stops.</summary>
        public event Action DisksChanged;

        private static DiskSystem? DiskSystemOf(MachineModel model) => model switch
        {
            MachineModel._plus3 => DiskSystem.Plus3,
            MachineModel._pentagon => DiskSystem.Beta,
            _ => null
        };

        private static MachineModel MachineFor(DiskSystem system) =>
            system == DiskSystem.Beta ? MachineModel._pentagon : MachineModel._plus3;

        private string[] DisksOf(DiskSystem system) => system == DiskSystem.Beta ? _betaDisks : _plus3Disks;

        private string[] Disks => DisksOf(_diskSystem);

        /// <summary>
        /// How many drives there are: the +3 has two (A and B), the Pentagon four (A to D). A machine
        /// without drives answers for the last one that had them, whose disks are still remembered.
        /// </summary>
        public int DiskDriveCount => Disks.Length;

        /// <summary>The image in drive A (0) to D (3), or null.</summary>
        public string DiskInDrive(int unit) => unit >= 0 && unit < Disks.Length ? Disks[unit] : null;

        /// <summary>A drive is in use: the +3's motor runs, or the Pentagon's head is loaded.</summary>
        public bool DiskMotorOn => _diskMotorOn || _betaLitDrive >= 0;

        /// <summary>
        /// The drive whose busy light is bright, or -1. Read from the UI thread, so it may be a
        /// moment behind the machine.
        ///  - Pentagon: the drive selected while the controller has the head loaded. The Beta 128
        ///    only selects a drive (spinning and lighting it) while HLD is active, which lasts from
        ///    the first command to three seconds of idle (WD1793 data sheet; Beta Disc book).
        ///  - +3: the drive a command has just selected, for <see cref="Plus3BrightFrames"/> after.
        ///    The µPD765 holds a drive selected while it runs a command on it.
        /// </summary>
        public int BusyDiskUnit => _zx switch
        {
            zx_plus3 => _plus3LitDrive,
            Pentagon_128k => _betaLitDrive,
            _ => -1
        };

        /// <summary>
        /// What a drive's busy light is doing. Bright for <see cref="BusyDiskUnit"/>. On the +3 both
        /// drives glow dim otherwise: between commands the µPD765 polls the drives by cycling its
        /// unit select lines, and with only US0 wired that flips the select between A and B, each
        /// selected half the time. While a command holds one drive selected the other is dark.
        /// The glow does not need a disk in the drive: polling selects it all the same. (From the
        /// µPD765 data sheet and owners' reports of the drive LED glowing at rest and going bright
        /// on access, one of whom confirmed it glows with the drive empty; not from a schematic.)
        /// The Pentagon's lights are off at rest.
        /// A stock +3 has no drive B, so an empty B stands for none fitted and stays dark, even
        /// when +3DOS looks for one.
        /// </summary>
        public DiskLight DiskLightOf(int unit)
        {
            if (_zx is zx_plus3 && unit == 1 && DiskInDrive(1) == null) return DiskLight.Off;
            int busy = BusyDiskUnit;
            if (busy == unit) return DiskLight.Bright;
            return _zx is zx_plus3 && busy < 0 && unit < _plus3Disks.Length ? DiskLight.Dim : DiskLight.Off;
        }

        /// <summary>The current machine has disk drives Zero can emulate (the +3 and the Pentagon).</summary>
        public bool HasDiskDrives => DiskSystemOf(Model) != null;

        /// <summary>
        /// Put a disk image in a drive, switching machine first if need be: a DSK goes in the +3, a
        /// TRD or SCL in the Pentagon. A DSK that cannot be written (read-only file, or extended DSK)
        /// goes in write protected; an SCL is written back as a .trd beside it. A ZIP is opened for
        /// the disk image inside it.
        /// </summary>
        public void InsertDisk(int unit, string path) => Post(() => InsertDiskCore(unit, path));

        public Task<bool> InsertDiskAsync(int unit, string path) => InvokeAsync(() => InsertDiskCore(unit, path));

        /// <summary>Take the disk out of a drive, writing back anything the Spectrum changed.</summary>
        public void EjectDisk(int unit) => Post(() => EjectDiskCore(unit));

        public Task EjectDiskAsync(int unit) => InvokeAsync(() => EjectDiskCore(unit));

        private bool InsertDiskCore(int unit, string path)
        {
            if (unit < 0 || unit >= Beta128.BetaDriveCount) return false;
            if (File.Exists(path) && Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                path = ExtractDiskFromZip(path);
                if (path == null) return false;
            }
            DiskSystem system;
            if (File.Exists(path) && DskImage.IsDsk(path)) system = DiskSystem.Plus3;
            else if (File.Exists(path) && TrdImage.IsTrDosImage(path)) system = DiskSystem.Beta;
            else
            {
                Error?.Invoke("This doesn't seem to be a disk image Zero can use (+3 DSK, or TR-DOS TRD or SCL): " + Path.GetFileName(path));
                return false;
            }
            string[] disks = DisksOf(system);
            if (unit >= disks.Length)
            {
                Error?.Invoke($"The +3 has only drives A and B, so the disk can't go in drive {(char)('A' + unit)}.");
                return false;
            }
            disks[unit] = Path.GetFullPath(path);
            _diskSystem = system;
            if (Model != MachineFor(system))
                CreateMachine(MachineFor(system));   // puts the remembered disks in, this one included
            else
                _zx.DiskInsert(disks[unit], (byte)unit);
            DisksChanged?.Invoke();
            return true;
        }

        private static bool IsDiskImageName(string name) =>
            DiskExtensions.Contains(Path.GetExtension(name).ToLowerInvariant());

        /// <summary>
        /// The disk image inside a ZIP, written out under its own name for the drive to use (it
        /// writes changes back to a file). Asks which one when there are several; null if none.
        /// </summary>
        private string ExtractDiskFromZip(string zipPath)
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
            var disks = archive.Entries
                .Where(e => IsDiskImageName(e.Name))
                .Select(e => e.FullName)
                .ToList();
            if (disks.Count == 0)
            {
                Error?.Invoke("There's no disk image (DSK, TRD or SCL) in " + Path.GetFileName(zipPath) + ".");
                return null;
            }
            string chosen = disks.Count == 1 ? disks[0] : (ChooseArchiveEntry?.Invoke(disks) ?? disks[0]);
            if (chosen == null) return null;
            System.IO.Compression.ZipArchiveEntry entry = archive.GetEntry(chosen);
            using var data = new MemoryStream();
            using (Stream s = entry.Open()) s.CopyTo(data);
            return WriteTemp(entry.Name, data.ToArray());
        }

        private void EjectDiskCore(int unit)
        {
            string[] disks = Disks;
            if (unit < 0 || unit >= disks.Length || disks[unit] == null) return;
            if (_zx != null && Model == MachineFor(_diskSystem))
                _zx.DiskEject((byte)unit);
            disks[unit] = null;
            DisksChanged?.Invoke();
        }

        /// <summary>A new machine has been made: give it its disks back and listen for the motor (emulation thread).</summary>
        private void AttachDisks(zx_spectrum zx)
        {
            _diskMotorOn = false;
            _betaLitDrive = -1;
            _plus3LitDrive = -1;
            _plus3Selections[0] = _plus3Selections[1] = 0;   // a new controller counts from nothing
            zx.DiskEvent += OnDiskEvent;
            if (DiskSystemOf(zx.model) is not DiskSystem system) return;
            _diskSystem = system;
            string[] disks = DisksOf(system);
            for (int unit = 0; unit < disks.Length; unit++)
            {
                if (disks[unit] == null) continue;
                if (File.Exists(disks[unit])) zx.DiskInsert(disks[unit], (byte)unit);
                else disks[unit] = null;  // gone from disk since it was inserted
            }
        }

        /// <summary>
        /// The machine is about to go (emulation thread). An SCL the Spectrum wrote to is saved as
        /// a .trd beside it; remember that instead, or coming back would lose the changes.
        /// </summary>
        private void DetachDisks(zx_spectrum zx)
        {
            if (zx is not Pentagon_128k pentagon || pentagon.FloppyController.Controller is not Beta128 beta) return;
            for (byte unit = 0; unit < _betaDisks.Length; unit++)
            {
                if (_betaDisks[unit] == null || !beta.DiskModified(unit)) continue;
                beta.FlushDisk(unit);
                string written = beta.DiskFileName(unit);
                if (File.Exists(written)) _betaDisks[unit] = Path.GetFullPath(written);
            }
        }

        private void OnDiskEvent(object sender, DiskEventArgs e)
        {
            // The Pentagon's bit 4 follows the command type, not a motor. (Neither machine's busy
            // light comes from here now: see TrackDiskActivity.)
            if (sender is Pentagon_128k) return;
            bool motor = (e.EventType & (1 << 4)) != 0;   // bit 4: motor; bits 0-3: drives with disks
            if (motor == _diskMotorOn) return;
            _diskMotorOn = motor;
            DisksChanged?.Invoke();
        }

        // The drive lit bright at the end of the last frame (emulation thread writes, UI reads).
        private volatile int _betaLitDrive = -1;
        private volatile int _plus3LitDrive = -1;

        /// <summary>
        /// How long a +3 drive stays bright after a command selects it. The controller has no
        /// timing, so a command is over within the frame; a real one takes up to a revolution
        /// (a fifth of a second) and longer to seek. This hold is invented, to make it visible.
        /// </summary>
        public const int Plus3BrightFrames = 13;

        private readonly int[] _plus3Selections = new int[2];
        private int _plus3BrightLeft;

        /// <summary>
        /// Once a frame (emulation thread): let the controllers see the frames go by, and tell the
        /// UI when the bright drive changes. Frames that did not run (paused) do not count.
        /// </summary>
        private void TrackDiskActivity(zx_spectrum zx, int frames)
        {
            int lit;
            if (zx is Pentagon_128k pentagon && pentagon.FloppyController.Controller is Beta128 beta)
            {
                beta.ElapseFrames(frames);   // its head unloads after three idle seconds of them
                lit = beta.HeadLoaded ? beta.SelectedDrive : -1;
                if (lit == _betaLitDrive) return;
                _betaLitDrive = lit;
            }
            else if (zx is zx_plus3 plus3 && plus3.FloppyController.Controller is Fdc765 fdc)
            {
                lit = _plus3LitDrive;
                bool selected = false;
                for (int unit = 0; unit < _plus3Selections.Length; unit++)
                {
                    int count = fdc.Selections(unit);
                    if (count == _plus3Selections[unit]) continue;
                    _plus3Selections[unit] = count;
                    lit = unit;
                    selected = true;
                }
                if (selected) _plus3BrightLeft = Plus3BrightFrames;
                else if (lit >= 0 && (_plus3BrightLeft -= frames) <= 0) lit = -1;
                if (lit == _plus3LitDrive) return;
                _plus3LitDrive = lit;
            }
            else return;
            DisksChanged?.Invoke();
        }

        /// <summary>
        /// After inserting a disk: hard reset and choose Loader from the +3 menu, which boots the
        /// disk in drive A.
        /// </summary>
        private static readonly byte[] AutoLoadPlus3DiskKeys = { 13 };

        /// <summary>
        /// After inserting a TR-DOS disk: hard reset, choose TR-DOS from the Pentagon's menu (the
        /// fifth entry), then RUN, which loads and runs the disk's "boot" program. TR-DOS drops keys
        /// typed while it starts up, so RUN waits a moment.
        /// </summary>
        private static readonly byte[] AutoLoadTrDosKeys =                                     // (down x4) ENTER (pause) RUN ENTER
            { 10, 10, 10, 10, 13, AutoLoadPause, 0xF7, 13 };
    }
}
