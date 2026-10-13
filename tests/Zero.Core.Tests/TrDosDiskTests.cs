using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Peripherals;
using SpeccyCommon;
using Xunit;
using Xunit.Abstractions;
using Zero.Emulation;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.Core.Tests
{
    /// <summary>
    /// The Pentagon with real TR-DOS disks, end to end: its own ROM and TR-DOS 5.03, driving the
    /// ported Beta 128 through the session, typing the way a person would.
    /// </summary>
    [Collection("PZXFile static state")]
    public class TrDosDiskTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly EmulatorSession _session;
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "zero-trdos-" + Guid.NewGuid().ToString("N"));

        public TrDosDiskTests(ITestOutputHelper output)
        {
            _output = output;
            Directory.CreateDirectory(_dir);
            var settings = new EmulatorSettings();
            settings.Emulation.Model = MachineModel._pentagon;
            settings.Tape.AutoLoad = false;
            _session = new EmulatorSession(settings)
            {
                RomDirectory = TestPaths.RomDir,
                AudioFactory = () => new NullAudioOutput()
            };
            _session.Error += e => _output.WriteLine("ERROR: " + e);
        }

        public void Dispose()
        {
            _session.Dispose();
            try { Directory.Delete(_dir, true); } catch { }
        }

        private const int LAST_K = 23560, FLAGS = 23611;

        private async Task RunFrames(int frames)
        {
            long target = _session.FrameCount + frames;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (_session.FrameCount < target)
            {
                if (sw.ElapsedMilliseconds > 60000) throw new TimeoutException("frames stopped");
                await Task.Delay(2);
            }
        }

        private Task<string> Screen() => _session.InvokeAsync(() => ScreenText.Dump(_session.Machine));

        private async Task WaitForScreen(string text, int maxFrames = 3000)
        {
            for (int waited = 0; waited < maxFrames; waited += 25)
            {
                if ((await Screen()).Contains(text)) return;
                await RunFrames(25);
            }
            string screen = await Screen();
            _output.WriteLine(screen);
            throw new TimeoutException($"'{text}' never appeared on screen");
        }

        /// <summary>Type through the ROM's own key buffer: LAST_K, flagged in FLAGS bit 5.</summary>
        private async Task Type(string keys)
        {
            foreach (char c in keys)
            {
                for (int i = 0; ; i++)
                {
                    bool consumed = await _session.InvokeAsync(() => (_session.Machine.PeekByteNoContend(FLAGS) & 0x20) == 0);
                    if (consumed) break;
                    if (i > 500) throw new TimeoutException("the ROM stopped taking keys");
                    await RunFrames(1);
                }
                byte code = (byte)c;
                await _session.InvokeAsync(() =>
                {
                    _session.Machine.PokeByteNoContend(LAST_K, code);
                    _session.Machine.PokeByteNoContend(FLAGS, _session.Machine.PeekByteNoContend(FLAGS) | 0x20);
                });
                await RunFrames(2);
            }
        }

        private const char Down = (char)10, Enter = (char)13;

        // Keywords go in as their tokens, as the 48K editor's K mode would put them.
        private const char Cat = (char)0xCF, Line = (char)0xCA, Usr = (char)0xC0, Rem = (char)0xEA,
            Print = (char)0xF5, Load = (char)0xEF, Run = (char)0xF7, Save = (char)0xF8, Randomize = (char)0xF9;

        /// <summary>From 48 BASIC, a TR-DOS command: RANDOMIZE USR 15619: REM: command.</summary>
        private static string TrDos(string command) => $"{Randomize}{Usr}15619:{Rem}:{command}{Enter}";

        private async Task Boot(string disk)
        {
            _session.Start();
            await RunFrames(150);
            Assert.True(await _session.InsertDiskAsync(0, disk));
            await WaitForScreen("TR-DOS");                  // the Pentagon's menu
        }

        /// <summary>The menu's fifth entry: TR-DOS, at its A&gt; prompt.</summary>
        private async Task BootToTrDos(string disk)
        {
            await Boot(disk);
            await Type($"{Down}{Down}{Down}{Down}{Enter}");
            await WaitForScreen("TR-DOS Ver 5.03");
            await WaitForScreen("A>");                      // keys typed before the prompt are thrown away
        }

        /// <summary>The menu's fourth entry: 48 BASIC, where programs are typed and TR-DOS is called by USR.</summary>
        private async Task BootTo48Basic(string disk)
        {
            await Boot(disk);
            await Type($"{Down}{Down}{Down}{Enter}");
            await WaitForScreen("1982 Sinclair Research");
        }

        private string BlankDisk(string name = "blank.trd")
        {
            string path = Path.Combine(_dir, name);
            TrdImage.WriteBlank(path);
            return path;
        }

        /// <summary>The 9-character name and type of the first catalog entry, e.g. "hello   B".</summary>
        private static string FirstCatalogEntry(byte[] image) => Encoding.ASCII.GetString(image, 0, 9);

        /// <summary>The first file on a TRD, packed as an SCL archive.</summary>
        private static byte[] SclOfFirstFile(byte[] trd)
        {
            int sectors = trd[13], sector = trd[14], track = trd[15];
            var scl = new MemoryStream();
            scl.Write("SINCLAIR"u8);
            scl.WriteByte(1);
            scl.Write(trd, 0, 14);
            scl.Write(trd, track * TrdImage.TrackSize + sector * TrdImage.SectorSize, sectors * TrdImage.SectorSize);
            return scl.ToArray();
        }

        /// <summary>Save a "boot" program that prints a line and starts itself, and take the disk out.</summary>
        private async Task<byte[]> SaveBootProgram(string disk)
        {
            await BootTo48Basic(disk);
            await Type($"10 {Print}\"zero x disk\"{Enter}");
            await Type(TrDos($"{Save}\"boot\"{Line}10"));
            await RunFrames(150);
            await _session.EjectDiskAsync(0);
            return File.ReadAllBytes(disk);
        }

        [Fact]
        public async Task Cat_lists_a_blank_disk()
        {
            await BootToTrDos(BlankDisk());
            await Type($"{Cat}{Enter}");
            await WaitForScreen("2544 Free");               // 80 tracks x 2 sides x 16 sectors, less track 0
            Assert.Contains("0 File(s)", await Screen());
        }

        [Fact]
        public async Task Save_writes_a_file_to_the_disk_image()
        {
            string disk = BlankDisk();
            await BootTo48Basic(disk);
            await Type($"10 {Print}\"zero\"{Enter}");
            await Type(TrDos($"{Save}\"hello\""));
            await RunFrames(150);
            await Type(TrDos($"{Cat}"));
            await WaitForScreen("hello");
            await WaitForScreen("2543 Free");

            await _session.EjectDiskAsync(0);
            byte[] image = File.ReadAllBytes(disk);
            Assert.Equal(TrdImage.CreateBlank().Length, image.Length);
            Assert.Equal("hello   B", FirstCatalogEntry(image));   // the catalog is the first sectors of track 0
            Assert.Equal(1, image[8 * 256 + TrdImage.TRDSYS_NumFiles]);
        }

        [Fact]
        public async Task A_saved_program_loads_back_from_the_disk()
        {
            await BootTo48Basic(BlankDisk());
            await Type($"10 {Print}\"zero x disk\"{Enter}");
            await Type(TrDos($"{Save}\"prog\""));
            await RunFrames(150);
            await Type($"10 {Print}\"changed\"{Enter}");
            await Type(TrDos($"{Load}\"prog\""));
            await RunFrames(150);
            await Type($"{Run}{Enter}");
            await WaitForScreen("zero x disk");
        }

        [Fact]
        public async Task Opening_an_scl_with_auto_load_boots_it_from_trdos()
        {
            byte[] trd = await SaveBootProgram(BlankDisk());
            string scl = Path.Combine(_dir, "game.scl");
            File.WriteAllBytes(scl, SclOfFirstFile(trd));

            _session.Tape.AutoLoad = true;
            Assert.True(await _session.LoadFileAsync(scl));
            Assert.Equal(Path.GetFullPath(scl), _session.DiskInDrive(0));
            await WaitForScreen("zero x disk");
        }

        [Fact]
        public async Task An_scl_is_written_back_as_a_trd_beside_it()
        {
            byte[] trd = await SaveBootProgram(BlankDisk());
            string scl = Path.Combine(_dir, "game.scl");
            byte[] sclBytes = SclOfFirstFile(trd);
            File.WriteAllBytes(scl, sclBytes);

            Assert.True(await _session.InsertDiskAsync(0, scl));
            _session.Reset(true);
            await WaitForScreen("TR-DOS");
            await Type($"{Down}{Down}{Down}{Enter}");
            await WaitForScreen("1982 Sinclair Research");
            await Type($"10 {Print}\"more\"{Enter}");
            await Type(TrDos($"{Save}\"more\""));
            await RunFrames(150);
            await _session.EjectDiskAsync(0);

            Assert.Equal(sclBytes, File.ReadAllBytes(scl));  // the archive itself is left alone
            byte[] written = File.ReadAllBytes(Path.Combine(_dir, "game.trd"));
            Assert.Equal("boot    B", FirstCatalogEntry(written));
            Assert.Equal("more    B", Encoding.ASCII.GetString(written, 16, 9));
        }

        [Fact]
        public async Task A_changed_scl_comes_back_as_its_trd_after_a_change_of_machine()
        {
            byte[] trd = await SaveBootProgram(BlankDisk());
            string scl = Path.Combine(_dir, "game.scl");
            File.WriteAllBytes(scl, SclOfFirstFile(trd));

            Assert.True(await _session.InsertDiskAsync(0, scl));
            _session.Reset(true);
            await WaitForScreen("TR-DOS");
            await Type($"{Down}{Down}{Down}{Enter}");
            await WaitForScreen("1982 Sinclair Research");
            await Type($"10 {Print}\"more\"{Enter}");
            await Type(TrDos($"{Save}\"more\""));
            await RunFrames(150);

            _session.SwitchMachine(MachineModel._48k);
            await RunFrames(20);
            _session.SwitchMachine(MachineModel._pentagon);
            await RunFrames(20);
            Assert.Equal(Path.GetFullPath(Path.Combine(_dir, "game.trd")), _session.DiskInDrive(0));
        }

        [Fact]
        public async Task Disks_stay_in_the_drives_across_a_change_of_machine()
        {
            string trd = BlankDisk();
            string dsk = Path.Combine(_dir, "plus3.dsk");
            DskImage.WriteBlankPlus3(dsk);
            _session.Start();
            await RunFrames(50);
            Assert.True(await _session.InsertDiskAsync(3, trd));
            Assert.Equal(4, _session.DiskDriveCount);
            Assert.Equal(Path.GetFullPath(trd), _session.DiskInDrive(3));

            // A DSK takes the machine to the +3, which has its own two drives...
            Assert.True(await _session.InsertDiskAsync(1, dsk));
            Assert.Equal(MachineModel._plus3, _session.Model);
            Assert.Equal(2, _session.DiskDriveCount);
            Assert.Equal(Path.GetFullPath(dsk), _session.DiskInDrive(1));

            // ...and the Pentagon still has its disk when it comes back.
            _session.SwitchMachine(MachineModel._48k);
            await RunFrames(20);
            Assert.False(_session.HasDiskDrives);
            _session.SwitchMachine(MachineModel._pentagon);
            await RunFrames(20);
            Assert.True(_session.HasDiskDrives);
            Assert.Equal(4, _session.DiskDriveCount);
            Assert.Equal(Path.GetFullPath(trd), _session.DiskInDrive(3));
            Assert.Null(_session.DiskInDrive(1));
        }

        [Fact]
        public async Task The_plus3_has_no_drive_c()
        {
            string dsk = Path.Combine(_dir, "plus3.dsk");
            DskImage.WriteBlankPlus3(dsk);
            _session.Start();
            await RunFrames(50);
            Assert.False(await _session.InsertDiskAsync(2, dsk));
            Assert.Equal(MachineModel._pentagon, _session.Model);
        }

        [Fact]
        public async Task A_trd_from_a_zip_keeps_its_own_name()
        {
            string zip = Path.Combine(_dir, "games.zip");
            using (var archive = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
            using (Stream entry = archive.CreateEntry("Demos/Inferno.trd").Open())
                entry.Write(TrdImage.CreateBlank());

            _session.Settings.Emulation.Model = MachineModel._48k;
            _session.SwitchMachine(MachineModel._48k);
            _session.Start();
            await RunFrames(50);
            Assert.True(await _session.LoadFileAsync(zip));
            Assert.Equal(MachineModel._pentagon, _session.Model);
            Assert.Equal("Inferno.trd", Path.GetFileName(_session.DiskInDrive(0)));
        }

        [Fact]
        public async Task A_drive_takes_a_disk_straight_from_a_zip()
        {
            string zip = Path.Combine(_dir, "demo.zip");
            using (var archive = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
            {
                using (var readme = new StreamWriter(archive.CreateEntry("readme.txt").Open())) readme.Write("not a disk");
                using (Stream entry = archive.CreateEntry("Inferno.trd").Open()) entry.Write(TrdImage.CreateBlank());
            }

            _session.Start();
            await RunFrames(50);
            Assert.True(await _session.InsertDiskAsync(3, zip));
            Assert.Equal("Inferno.trd", Path.GetFileName(_session.DiskInDrive(3)));
            Assert.Null(_session.DiskInDrive(0));
        }

        [Fact]
        public async Task Opening_a_trd_switches_to_the_pentagon_and_puts_it_in_drive_a()
        {
            _session.Settings.Emulation.Model = MachineModel._plus3;
            _session.SwitchMachine(MachineModel._plus3);
            _session.Start();
            await RunFrames(50);
            string disk = BlankDisk();
            Assert.True(await _session.LoadFileAsync(disk));
            Assert.Equal(MachineModel._pentagon, _session.Model);
            Assert.Equal(Path.GetFullPath(disk), _session.DiskInDrive(0));
        }

        private async Task WaitForBusyDrive(int unit, int maxFrames = 60)
        {
            for (int frame = 0; _session.BusyDiskUnit != unit; frame++)
            {
                if (frame > maxFrames) throw new TimeoutException($"drive {unit} never lit; drive {_session.BusyDiskUnit} is");
                await RunFrames(1);
            }
        }

        [Fact]
        public async Task The_busy_light_is_on_while_the_head_is_loaded()
        {
            string disk = BlankDisk();
            await BootTo48Basic(disk);
            await RunFrames(200);                            // the head unloads after booting
            Assert.Equal(-1, _session.BusyDiskUnit);
            Assert.False(_session.DiskMotorOn);

            // CAT loads the head on drive A, and it stays loaded (and lit) after the catalog is
            // shown. (TR-DOS's delay loop selects drive D for a moment while the head is loaded,
            // lighting D's light briefly as the real interface would, so wait for A's.)
            await Type(TrDos($"{Cat}"));
            await WaitForScreen("2544 Free");
            await WaitForBusyDrive(0);
            Assert.True(_session.DiskMotorOn);

            // ...until 15 idle revolutions (three seconds of frames) have passed.
            await RunFrames(Beta128.HeadUnloadRevolutions * Beta128.FramesPerRevolution + 10);
            Assert.Equal(-1, _session.BusyDiskUnit);
        }
    }
}
