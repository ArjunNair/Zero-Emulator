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
    /// The +3 with real disks, end to end: its own ROM and +3DOS, driving the ported controller
    /// through the session, typing into +3 BASIC the way a person would.
    /// </summary>
    [Collection("PZXFile static state")]
    public class Plus3DiskTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly EmulatorSession _session;
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "zero-plus3-" + Guid.NewGuid().ToString("N"));

        public Plus3DiskTests(ITestOutputHelper output)
        {
            _output = output;
            Directory.CreateDirectory(_dir);
            var settings = new EmulatorSettings();
            settings.Emulation.Model = MachineModel._plus3;
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

        private async Task<string> BootToPlus3Basic(string disk)
        {
            _session.Start();
            await RunFrames(150);
            Assert.True(await _session.InsertDiskAsync(0, disk));
            await WaitForScreen("+3 BASIC");
            await Type($"{Down}{Enter}");          // the menu: Loader, +3 BASIC, ...
            await RunFrames(50);
            return disk;
        }

        private string BlankDisk()
        {
            string path = Path.Combine(_dir, "blank.dsk");
            DskImage.WriteBlankPlus3(path);
            return path;
        }

        [Fact]
        public async Task Cat_lists_a_blank_disk()
        {
            await BootToPlus3Basic(BlankDisk());
            await Type($"cat{Enter}");
            await WaitForScreen("173K free");
        }

        [Fact]
        public async Task Save_writes_a_file_to_the_disk_image()
        {
            string disk = await BootToPlus3Basic(BlankDisk());
            await Type($"10 print \"zero\"{Enter}");
            await Type($"save \"a:hello\"{Enter}");
            await RunFrames(200);
            await Type($"cat{Enter}");
            await WaitForScreen("HELLO");
            await WaitForScreen("K free");
            Assert.DoesNotContain("173K free", await Screen()); // a block went to the file

            await _session.EjectDiskAsync(0);
            byte[] image = File.ReadAllBytes(disk);
            Assert.Equal(DskImage.CreateBlankPlus3().Length, image.Length);
            // The directory is the first sectors of track 1 (track 0 is reserved for booting).
            string directory = Encoding.ASCII.GetString(image, 0x100 + 1 * 0x1300 + 0x100, 512);
            Assert.Contains("HELLO", directory);
        }

        [Fact]
        public async Task A_saved_program_loads_back_from_the_disk()
        {
            string disk = await BootToPlus3Basic(BlankDisk());
            await Type($"10 print \"zero x disk\"{Enter}");
            await Type($"save \"a:prog\"{Enter}");
            await RunFrames(200);
            // Change the program in memory (NEW would go back to the start-up menu), then load the saved one over it.
            await Type($"10 print \"changed\"{Enter}");
            await Type($"load \"a:prog\"{Enter}");
            await RunFrames(200);
            await Type($"run{Enter}");
            await WaitForScreen("zero x disk");
        }

        private async Task WaitForLights(DiskLight a, DiskLight b, int maxFrames = 1500)
        {
            for (int frame = 0; _session.DiskLightOf(0) != a || _session.DiskLightOf(1) != b; frame++)
            {
                if (frame > maxFrames)
                    throw new TimeoutException($"lights never went {a}/{b}; they are {_session.DiskLightOf(0)}/{_session.DiskLightOf(1)}");
                await RunFrames(1);
            }
        }

        [Fact]
        public async Task Drive_lights_glow_dim_at_rest_and_go_bright_while_a_command_runs()
        {
            await BootToPlus3Basic(BlankDisk());
            await WaitForLights(DiskLight.Dim, DiskLight.Off);   // at rest: A polled, so dim; no drive B fitted

            // With a disk in B it is a drive like A: polled by turns, so both dim.
            string second = Path.Combine(_dir, "second.dsk");
            DskImage.WriteBlankPlus3(second);
            Assert.True(await _session.InsertDiskAsync(1, second));
            await WaitForLights(DiskLight.Dim, DiskLight.Dim);

            // CAT holds drive A selected: A bright, B dark. Then back to rest.
            await Type($"cat{Enter}");
            await WaitForLights(DiskLight.Bright, DiskLight.Off);
            Assert.Equal(0, _session.BusyDiskUnit);
            await WaitForScreen("173K free");
            await WaitForLights(DiskLight.Dim, DiskLight.Dim);
            Assert.Equal(-1, _session.BusyDiskUnit);

            // Out with B's disk, and its light goes out with it.
            await _session.EjectDiskAsync(1);
            Assert.Equal(DiskLight.Off, _session.DiskLightOf(1));
        }

        [Fact]
        public async Task A_disk_stays_in_the_drive_across_a_change_of_machine()
        {
            string disk = BlankDisk();
            _session.Start();
            await RunFrames(50);
            Assert.True(await _session.InsertDiskAsync(1, disk));
            Assert.Equal(Path.GetFullPath(disk), _session.DiskInDrive(1));

            _session.SwitchMachine(MachineModel._48k);
            await RunFrames(20);
            Assert.False(_session.HasDiskDrives);
            _session.SwitchMachine(MachineModel._plus3);
            await RunFrames(20);
            Assert.True(_session.HasDiskDrives);
            Assert.Equal(Path.GetFullPath(disk), _session.DiskInDrive(1));
        }

        [Fact]
        public async Task A_disk_from_a_zip_keeps_its_own_name()
        {
            string zip = Path.Combine(_dir, "games.zip");
            using (var archive = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
            using (Stream entry = archive.CreateEntry("Infocom/Bored Of The Rings.dsk").Open())
                entry.Write(DskImage.CreateBlankPlus3());

            _session.Start();
            await RunFrames(50);
            Assert.True(await _session.LoadFileAsync(zip));
            Assert.Equal("Bored Of The Rings.dsk", Path.GetFileName(_session.DiskInDrive(0)));
        }

        [Fact]
        public async Task A_drive_takes_a_disk_straight_from_a_zip()
        {
            string zip = Path.Combine(_dir, "adventure.zip");
            using (var archive = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
            {
                using (var readme = new StreamWriter(archive.CreateEntry("readme.txt").Open())) readme.Write("not a disk");
                using (Stream entry = archive.CreateEntry("Bored Of The Rings.dsk").Open()) entry.Write(DskImage.CreateBlankPlus3());
            }

            _session.Start();
            await RunFrames(50);
            Assert.True(await _session.InsertDiskAsync(1, zip));
            Assert.Equal("Bored Of The Rings.dsk", Path.GetFileName(_session.DiskInDrive(1)));
            Assert.Null(_session.DiskInDrive(0));
        }

        [Fact]
        public async Task Opening_a_dsk_switches_to_the_plus3_and_puts_it_in_drive_a()
        {
            var settings = _session.Settings;
            settings.Emulation.Model = MachineModel._48k;
            _session.SwitchMachine(MachineModel._48k);
            _session.Start();
            await RunFrames(50);
            string disk = BlankDisk();
            Assert.True(await _session.LoadFileAsync(disk));
            Assert.Equal(MachineModel._plus3, _session.Model);
            Assert.Equal(Path.GetFullPath(disk), _session.DiskInDrive(0));
        }
    }
}
