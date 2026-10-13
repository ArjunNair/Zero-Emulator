using System;
using System.IO;
using System.Linq;
using System.Text;
using Peripherals;
using Xunit;

namespace Zero.Core.Tests
{
    /// <summary>
    /// The Beta 128 port, driven through its registers the way TR-DOS drives it: choose drive and
    /// side at the system register, issue a command, then pump bytes through the data register
    /// while the system register shows DRQ (bit 6), until it shows the controller idle (bit 7).
    /// </summary>
    public class Beta128Tests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "zero-beta-" + Guid.NewGuid().ToString("N"));

        public Beta128Tests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private const byte DRQ = 0x40, IDLE = 0x80;

        // System register: MFM, side bit (inverted: set is side 0), HLT, not reset, and the drive.
        private static byte System(int drive, int side) => (byte)(0x20 | (side == 0 ? 0x10 : 0) | 0x08 | 0x04 | drive);

        private static int Offset(int track, int side, int sector) => ((track * 2 + side) * 16 + sector - 1) * 256;

        private static byte[] Pattern(int seed) => Enumerable.Range(0, 256).Select(i => (byte)(i * 7 + seed)).ToArray();

        private string Disk(string name = "disk.trd", Action<byte[]> edit = null)
        {
            byte[] image = TrdImage.CreateBlank();
            edit?.Invoke(image);
            string path = Path.Combine(_dir, name);
            File.WriteAllBytes(path, image);
            return path;
        }

        private static Beta128 Ready(string disk, int drive = 0)
        {
            var beta = new Beta128();
            Assert.True(beta.InsertDisk((byte)drive, disk));
            beta.WriteSystemRegister(System(drive, 0));
            return beta;
        }

        private static void SeekTo(Beta128 beta, byte track)
        {
            beta.WriteData(track);
            beta.WriteCommand(0x18);                         // SEEK, head load
            Assert.Equal(IDLE, beta.ReadSystemRegister());
            Assert.Equal(track, beta.ReadTrack());
        }

        private static byte[] Receive(Beta128 beta)
        {
            var bytes = new System.Collections.Generic.List<byte>();
            for (byte sys; ((sys = beta.ReadSystemRegister()) & IDLE) == 0;)
            {
                Assert.Equal(DRQ, sys & DRQ);
                bytes.Add(beta.ReadData());
            }
            return bytes.ToArray();
        }

        private static void Send(Beta128 beta, byte[] data)
        {
            foreach (byte b in data)
            {
                Assert.Equal(DRQ, beta.ReadSystemRegister() & (DRQ | IDLE));
                beta.WriteData(b);
            }
            Assert.Equal(IDLE, beta.ReadSystemRegister() & (DRQ | IDLE));
        }

        private static byte[] ReadSector(Beta128 beta, byte sector)
        {
            beta.WriteSectorReg(sector);
            beta.WriteCommand(0x80);                         // READ SECTOR
            return Receive(beta);
        }

        [Fact]
        public void Restores_to_track_0_with_the_index_hole_going_round()
        {
            var beta = Ready(Disk());
            beta.WriteCommand(0x08);                         // RESTORE, head load
            byte first = beta.ReadStatus(), second = beta.ReadStatus();
            Assert.Equal(Beta128.ST_TRACK0 | Beta128.ST_HEADLOADED, first & ~Beta128.ST_INDEX);
            Assert.NotEqual(first & Beta128.ST_INDEX, second & Beta128.ST_INDEX);
            Assert.Equal(0, beta.ReadTrack());
        }

        [Fact]
        public void An_empty_drive_is_not_ready_but_its_index_still_turns()
        {
            var beta = new Beta128();
            beta.WriteSystemRegister(System(1, 0));
            beta.WriteCommand(0x08);
            byte first = beta.ReadStatus(), second = beta.ReadStatus();
            Assert.Equal(Beta128.ST_NOTREADY, first & Beta128.ST_NOTREADY);
            Assert.NotEqual(first & Beta128.ST_INDEX, second & Beta128.ST_INDEX); // TR-DOS waits for this at boot
            beta.WriteCommand(0x80);
            Assert.Equal(Beta128.ST_NOTREADY, beta.ReadStatus());
        }

        [Fact]
        public void Steps_in_and_out()
        {
            var beta = Ready(Disk());
            SeekTo(beta, 5);
            beta.WriteCommand(0x58);                         // STEP IN, update track
            Assert.Equal(6, beta.ReadTrack());
            beta.WriteCommand(0x78);                         // STEP OUT, update track
            beta.WriteCommand(0x38);                         // STEP (same way), update track
            Assert.Equal(4, beta.ReadTrack());
            beta.WriteCommand(0x68);                         // STEP OUT without updating the register
            Assert.Equal(4, beta.ReadTrack());
        }

        [Fact]
        public void Reads_a_sector_from_either_side()
        {
            byte[] front = Pattern(3), back = Pattern(9);
            var beta = Ready(Disk(edit: img =>
            {
                front.CopyTo(img, Offset(2, 0, 5));
                back.CopyTo(img, Offset(2, 1, 5));
            }));
            SeekTo(beta, 2);
            Assert.Equal(front, ReadSector(beta, 5));
            Assert.Equal(0, beta.ReadStatus() & (Beta128.ST_BUSY | Beta128.ST_RNF));

            beta.WriteSystemRegister(System(0, 1));          // side 1: bit 4 clear
            Assert.Equal(back, ReadSector(beta, 5));
        }

        [Fact]
        public void Reads_several_sectors_to_the_end_of_the_track()
        {
            byte[] image = TrdImage.CreateBlank();
            for (int s = 1; s <= 16; s++) Pattern(s).CopyTo(image, Offset(1, 0, s));
            string disk = Path.Combine(_dir, "multi.trd");
            File.WriteAllBytes(disk, image);

            var beta = Ready(disk);
            SeekTo(beta, 1);
            beta.WriteSectorReg(14);
            beta.WriteCommand(0x90);                         // READ SECTOR, multiple
            byte[] read = Receive(beta);
            Assert.Equal(Pattern(14).Concat(Pattern(15)).Concat(Pattern(16)).ToArray(), read);
            Assert.Equal(Beta128.ST_RNF, beta.ReadStatus() & Beta128.ST_RNF); // ran off the end of the track
        }

        [Fact]
        public void A_missing_sector_is_record_not_found()
        {
            var beta = Ready(Disk());
            beta.WriteSectorReg(17);
            beta.WriteCommand(0x80);
            Assert.Equal(IDLE, beta.ReadSystemRegister());      // over at once
            Assert.Equal(Beta128.ST_RNF, beta.ReadStatus());
        }

        [Fact]
        public void Reads_an_address_mark()
        {
            var beta = Ready(Disk());
            SeekTo(beta, 7);
            beta.WriteSystemRegister(System(0, 1));
            beta.WriteSectorReg(3);
            beta.WriteCommand(0xC0);                         // READ ADDRESS
            Assert.Equal(new byte[] { 7, 1, 3, 1, 0, 0 }, Receive(beta));
            Assert.Equal(7, beta.ReadSectorReg());           // loaded from the track byte
        }

        [Fact]
        public void Writes_a_sector_back_to_the_image_on_eject()
        {
            string disk = Disk();
            byte[] data = Pattern(11);
            var beta = Ready(disk);
            SeekTo(beta, 3);
            beta.WriteSectorReg(4);
            beta.WriteCommand(0xA0);                         // WRITE SECTOR
            Send(beta, data);
            Assert.Equal(data, ReadSector(beta, 4));         // reads back before the disk leaves
            Assert.True(beta.DiskModified(0));

            beta.EjectDisk(0);
            byte[] image = File.ReadAllBytes(disk);
            Assert.Equal(TrdImage.CreateBlank().Length, image.Length);
            Assert.Equal(data, image.AsSpan(Offset(3, 0, 4), 256).ToArray());
            Assert.True(image.AsSpan(Offset(3, 0, 5), 256).ToArray().All(b => b == 0));
        }

        [Fact]
        public void A_read_only_image_is_write_protected()
        {
            string disk = Disk();
            File.SetAttributes(disk, FileAttributes.ReadOnly);
            try
            {
                var beta = Ready(disk);
                Assert.True(beta.DiskWriteProtected(0));
                beta.WriteSectorReg(1);
                beta.WriteCommand(0xA0);
                Assert.Equal(IDLE, beta.ReadSystemRegister());  // over at once
                Assert.Equal(Beta128.ST_WRITEPROTECT, beta.ReadStatus());
            }
            finally { File.SetAttributes(disk, FileAttributes.Normal); }
        }

        [Fact]
        public void Each_drive_has_its_own_disk_and_head()
        {
            byte[] a = Pattern(1), d = Pattern(2);
            var beta = new Beta128();
            Assert.True(beta.InsertDisk(0, Disk("a.trd", img => a.CopyTo(img, Offset(1, 0, 1)))));
            Assert.True(beta.InsertDisk(3, Disk("d.trd", img => d.CopyTo(img, Offset(0, 0, 1)))));

            beta.WriteSystemRegister(System(0, 0));
            SeekTo(beta, 1);
            beta.WriteSystemRegister(System(3, 0));
            Assert.Equal(d, ReadSector(beta, 1));            // drive D's head never moved
            beta.WriteSystemRegister(System(0, 0));
            Assert.Equal(a, ReadSector(beta, 1));
            Assert.Equal(0, beta.SelectedDrive);
        }

        private const int UnloadFrames = Beta128.HeadUnloadRevolutions * Beta128.FramesPerRevolution;

        [Fact]
        public void A_read_loads_the_head_until_fifteen_idle_revolutions_have_passed()
        {
            var beta = Ready(Disk());
            Assert.False(beta.HeadLoaded);
            ReadSector(beta, 1);
            Assert.True(beta.HeadLoaded);                    // still loaded with the command over

            beta.ElapseFrames(UnloadFrames - 1);
            Assert.True(beta.HeadLoaded);
            ReadSector(beta, 2);                             // another command starts the count again
            beta.ElapseFrames(UnloadFrames - 1);
            Assert.True(beta.HeadLoaded);
            beta.ElapseFrames(1);
            Assert.False(beta.HeadLoaded);                   // 15 revolutions idle: unloaded
        }

        [Theory]
        [InlineData(0x08, true)]                             // RESTORE, h: loaded
        [InlineData(0x04, true)]                             // RESTORE, verify: loaded near the end
        [InlineData(0x18, true)]                             // SEEK, h (how TR-DOS seeks)
        [InlineData(0x10, false)]                            // SEEK, neither: unloaded
        [InlineData(0x58, true)]                             // STEP IN, h
        [InlineData(0x50, false)]                            // STEP IN, neither
        public void A_type_1_command_loads_or_unloads_the_head_by_its_flags(byte command, bool loaded)
        {
            var beta = Ready(Disk());
            ReadSector(beta, 1);                             // loaded to start with
            beta.WriteCommand(command);
            Assert.Equal(loaded, beta.HeadLoaded);
        }

        [Fact]
        public void An_empty_drive_never_loads_the_head_for_a_read()
        {
            var beta = new Beta128();
            beta.WriteSystemRegister(System(2, 0));
            beta.WriteCommand(0x80);
            Assert.False(beta.HeadLoaded);
            beta.WriteCommand(0x08);                         // a restore with h loads it all the same
            Assert.True(beta.HeadLoaded);
        }

        [Fact]
        public void The_head_stays_loaded_through_a_transfer_and_unloads_on_reset()
        {
            var beta = Ready(Disk());
            beta.WriteSectorReg(1);
            beta.WriteCommand(0x80);
            beta.ElapseFrames(UnloadFrames * 2);             // busy, so not idle revolutions
            Assert.True(beta.HeadLoaded);
            beta.WriteCommand(0xD0);                         // Force Interrupt leaves it loaded
            Assert.True(beta.HeadLoaded);
            beta.Reset();
            Assert.False(beta.HeadLoaded);
        }

        [Fact]
        public void Formats_a_track_from_the_bytes_written_to_it()
        {
            string disk = Disk();
            var beta = Ready(disk);
            SeekTo(beta, 10);

            // One revolution as TR-DOS's FORMAT writes it: gaps, then for each sector an ID field
            // (FE C H R N, F7 for the CRC) and a data field (FB, 256 bytes, F7).
            var track = new System.Collections.Generic.List<byte>();
            for (int s = 1; s <= 16; s++)
            {
                track.AddRange(Enumerable.Repeat((byte)0x4E, 10));
                track.AddRange(new byte[] { 0xFE, 10, 0, (byte)s, 1, 0xF7 });
                track.AddRange(Enumerable.Repeat((byte)0x4E, 22));
                track.Add(0xFB);
                track.AddRange(Enumerable.Repeat((byte)s, 256));
                track.Add(0xF7);
            }
            track.AddRange(Enumerable.Repeat((byte)0x4E, Beta128.WriteTrackLength - track.Count));

            beta.WriteCommand(0xF0);                         // WRITE TRACK
            Send(beta, track.ToArray());
            beta.EjectDisk(0);

            byte[] image = File.ReadAllBytes(disk);
            for (int s = 1; s <= 16; s++)
                Assert.True(image.AsSpan(Offset(10, 0, s), 256).ToArray().All(b => b == s));
        }

        [Fact]
        public void Force_interrupt_abandons_a_transfer()
        {
            var beta = Ready(Disk());
            beta.WriteSectorReg(1);
            beta.WriteCommand(0x80);
            beta.ReadData();
            beta.WriteCommand(0xD0);                         // FORCE INTERRUPT, no condition: ends quietly
            Assert.Equal(0, beta.ReadStatus() & Beta128.ST_BUSY);
            Assert.Equal(0, beta.ReadSystemRegister());      // no DRQ, and no interrupt

            beta.WriteCommand(0xD8);                         // FORCE INTERRUPT, immediately
            Assert.Equal(IDLE, beta.ReadSystemRegister());
        }

        [Fact]
        public void The_interrupt_request_is_raised_when_a_command_ends_and_lowered_by_a_status_read()
        {
            var beta = Ready(Disk());
            Assert.Equal(0, beta.ReadSystemRegister());      // nothing has run yet
            SeekTo(beta, 2);                                 // a Type I command interrupts at once
            beta.ReadStatus();
            Assert.Equal(0, beta.ReadSystemRegister());      // reading the status lowers it

            beta.WriteSectorReg(1);
            beta.WriteCommand(0x80);
            Assert.Equal(DRQ, beta.ReadSystemRegister());    // running: a byte waiting, no interrupt yet
            for (int i = 0; i < 255; i++) beta.ReadData();
            Assert.Equal(DRQ, beta.ReadSystemRegister());
            beta.ReadData();
            Assert.Equal(IDLE, beta.ReadSystemRegister());   // the last byte ends it

            beta.WriteCommand(0x80);                         // a new command lowers it
            Assert.Equal(DRQ, beta.ReadSystemRegister());
        }

        [Fact]
        public void A_multi_sector_read_interrupts_only_at_the_end()
        {
            var beta = Ready(Disk());
            beta.WriteSectorReg(15);
            beta.WriteCommand(0x90);
            for (int i = 0; i < 256; i++) beta.ReadData();
            Assert.Equal(DRQ, beta.ReadSystemRegister());    // on to sector 16, no interrupt between
            for (int i = 0; i < 256; i++) beta.ReadData();
            Assert.Equal(IDLE, beta.ReadSystemRegister());
        }

        [Fact]
        public void A_read_the_cpu_stops_taking_ends_in_lost_data()
        {
            byte[] data = Pattern(4);
            var beta = Ready(Disk(edit: img => data.CopyTo(img, Offset(0, 0, 3))));
            beta.WriteSectorReg(3);
            beta.WriteCommand(0x80);
            for (int i = 0; i < 10; i++) beta.ReadData();

            for (int i = 1; i < Beta128.LostDataPolls; i++)
                Assert.Equal(DRQ, beta.ReadSystemRegister()); // still waiting for the CPU
            Assert.Equal(IDLE, beta.ReadSystemRegister());   // one poll too many: over, with no DRQ

            byte status = beta.ReadStatus();
            Assert.Equal(Beta128.ST_LOSTDATA, status & Beta128.ST_LOSTDATA);
            Assert.Equal(0, status & (Beta128.ST_BUSY | Beta128.ST_DRQ));
            Assert.NotEqual(data[10], beta.ReadData());      // the rest of the sector is gone
        }

        [Fact]
        public void A_read_polled_between_bytes_never_times_out()
        {
            byte[] data = Pattern(8);
            var beta = Ready(Disk(edit: img => data.CopyTo(img, Offset(0, 0, 4))));
            beta.WriteSectorReg(4);
            beta.WriteCommand(0x80);
            var read = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                for (int poll = 1; poll < Beta128.LostDataPolls; poll++)
                    Assert.Equal(DRQ, beta.ReadSystemRegister());
                read[i] = beta.ReadData();                   // taking a byte restarts the count
            }
            Assert.Equal(data, read);
            Assert.Equal(0, beta.ReadStatus() & Beta128.ST_LOSTDATA);
        }

        [Fact]
        public void Writes_never_time_out()
        {
            string disk = Disk();
            var beta = Ready(disk);
            beta.WriteSectorReg(2);
            beta.WriteCommand(0xA0);
            for (int i = 0; i < Beta128.LostDataPolls * 4; i++)
                Assert.Equal(DRQ, beta.ReadSystemRegister());
            Send(beta, Pattern(9));
            Assert.Equal(0, beta.ReadStatus() & Beta128.ST_LOSTDATA);
            beta.EjectDisk(0);
            Assert.Equal(Pattern(9), File.ReadAllBytes(disk).AsSpan(Offset(0, 0, 2), 256).ToArray());
        }

        [Fact]
        public void Infers_a_40_track_disk_from_its_size()
        {
            byte[] data = Pattern(5);
            byte[] image = TrdImage.CreateBlank(40, 2);
            Assert.Equal(40 * 2 * 16 * 256, image.Length);
            Assert.Equal(TrdImage.DiskType_40_2, image[8 * 256 + TrdImage.TRDSYS_DiskType]);
            data.CopyTo(image, Offset(39, 1, 16));          // the very last sector
            string disk = Path.Combine(_dir, "forty.trd");
            File.WriteAllBytes(disk, image);

            var beta = Ready(disk);
            SeekTo(beta, 39);
            beta.WriteSystemRegister(System(0, 1));
            Assert.Equal(data, ReadSector(beta, 16));
        }

        [Theory]
        [InlineData(40, TrdImage.DiskType_40_1)]
        [InlineData(80, TrdImage.DiskType_80_1)]
        public void A_single_sided_disk_is_read_one_side_a_track(int tracks, byte diskType)
        {
            // The same size as a double-sided disk of half the tracks: the disk type tells them apart.
            byte[] data = Pattern(5);
            byte[] image = TrdImage.CreateBlank(tracks, 1);
            Assert.Equal(diskType, image[8 * 256 + TrdImage.TRDSYS_DiskType]);
            int last = tracks - 1;
            data.CopyTo(image, (last * 16 + 2 - 1) * 256);  // the last track, sector 2
            string disk = Path.Combine(_dir, "single.trd");
            File.WriteAllBytes(disk, image);

            var beta = Ready(disk);
            SeekTo(beta, (byte)last);
            Assert.Equal(data, ReadSector(beta, 2));
        }

        [Fact]
        public void A_disk_whose_type_says_nothing_is_taken_as_double_sided()
        {
            byte[] data = Pattern(6);
            byte[] image = new byte[80 * 16 * 256];           // no system sector at all
            data.CopyTo(image, Offset(39, 1, 16));
            string disk = Path.Combine(_dir, "untyped.trd");
            File.WriteAllBytes(disk, image);

            var beta = Ready(disk);
            SeekTo(beta, 39);
            beta.WriteSystemRegister(System(0, 1));
            Assert.Equal(data, ReadSector(beta, 16));
        }

        [Fact]
        public void Rejects_a_file_that_is_not_whole_tracks()
        {
            string notDisk = Path.Combine(_dir, "notes.trd");
            File.WriteAllBytes(notDisk, new byte[4096 + 256]);
            var beta = new Beta128();
            Assert.False(beta.InsertDisk(0, notDisk));
            Assert.False(beta.DiskInserted(0));
            Assert.False(TrdImage.IsTrDosImage(notDisk));
        }

        [Fact]
        public void A_blank_disk_has_an_empty_catalog_and_640K_free()
        {
            byte[] image = TrdImage.CreateBlank();
            int sys = 8 * 256;
            Assert.Equal(80 * 2 * 16 * 256, image.Length);
            Assert.Equal(0, image[0]);                       // no files
            Assert.Equal(1, image[sys + TrdImage.TRDSYS_FirstFreeTrack]);
            Assert.Equal(0, image[sys + TrdImage.TRDSYS_FirstFreeSector]);
            Assert.Equal(TrdImage.DiskType_80_2, image[sys + TrdImage.TRDSYS_DiskType]);
            Assert.Equal(2544, image[sys + TrdImage.TRDSYS_NumFreeSectors] | image[sys + TrdImage.TRDSYS_NumFreeSectors + 1] << 8);
            Assert.Equal(TrdImage.TRDOS_ID_BYTE, image[sys + TrdImage.TRDSYS_TRDosID]);
        }

        [Fact]
        public void Converts_an_scl_to_a_trd_laid_out_from_track_1()
        {
            // Two files: three sectors, then twenty (which runs on into the next track).
            var scl = new MemoryStream();
            scl.Write("SINCLAIR"u8);
            scl.WriteByte(2);
            scl.Write(Encoding.ASCII.GetBytes("first   C")); scl.Write(new byte[] { 0, 0x80, 0, 3, 3 });
            scl.Write(Encoding.ASCII.GetBytes("second  C")); scl.Write(new byte[] { 0, 0x80, 0, 20, 20 });
            for (int i = 0; i < 23; i++) scl.Write(Pattern(i));
            byte[] sclBytes = scl.ToArray();

            Assert.True(TrdImage.IsScl(sclBytes));
            Assert.True(TrdImage.ConvertSclToTrd(sclBytes, out byte[] trd));
            Assert.Equal("first   C", Encoding.ASCII.GetString(trd, 0, 9));
            Assert.Equal(new byte[] { 0, 1 }, trd.AsSpan(14, 2).ToArray());      // sector 0, track 1
            Assert.Equal("second  C", Encoding.ASCII.GetString(trd, 16, 9));
            Assert.Equal(new byte[] { 3, 1 }, trd.AsSpan(30, 2).ToArray());      // sector 3, track 1
            Assert.Equal(Pattern(3), trd.AsSpan(1 * 4096 + 3 * 256, 256).ToArray());
            Assert.Equal(Pattern(22), trd.AsSpan(2 * 4096 + 6 * 256, 256).ToArray()); // 20 sectors later

            int sys = 8 * 256;
            Assert.Equal(2, trd[sys + TrdImage.TRDSYS_NumFiles]);
            Assert.Equal(2, trd[sys + TrdImage.TRDSYS_FirstFreeTrack]);
            Assert.Equal(7, trd[sys + TrdImage.TRDSYS_FirstFreeSector]);
            Assert.Equal(2544 - 23, trd[sys + TrdImage.TRDSYS_NumFreeSectors] | trd[sys + TrdImage.TRDSYS_NumFreeSectors + 1] << 8);
        }

        [Fact]
        public void An_scl_goes_in_and_is_written_back_beside_itself_as_a_trd()
        {
            var scl = new MemoryStream();
            scl.Write("SINCLAIR"u8);
            scl.WriteByte(0);
            string path = Path.Combine(_dir, "empty.scl");
            File.WriteAllBytes(path, scl.ToArray());

            var beta = Ready(path);
            Assert.Equal(path, beta.DiskFileName(0));            // nothing written back yet
            Assert.False(beta.DiskWriteProtected(0));
            SeekTo(beta, 1);
            beta.WriteSectorReg(1);
            beta.WriteCommand(0xA0);
            Send(beta, Pattern(1));
            beta.EjectDisk(0);

            Assert.Equal(9, new FileInfo(path).Length);      // the SCL itself is untouched
            byte[] trd = File.ReadAllBytes(Path.Combine(_dir, "empty.trd"));
            Assert.Equal(Pattern(1), trd.AsSpan(Offset(1, 0, 1), 256).ToArray());
        }

        private string EmptyScl(string name)
        {
            string path = Path.Combine(_dir, name);
            File.WriteAllBytes(path, "SINCLAIR\0"u8.ToArray());
            return path;
        }

        private static void WriteFirstSector(Beta128 beta, int drive, byte[] data)
        {
            beta.WriteSystemRegister(System(drive, 0));
            SeekTo(beta, 1);
            beta.WriteSectorReg(1);
            beta.WriteCommand(0xA0);
            Send(beta, data);
        }

        [Fact]
        public void An_scl_never_writes_over_a_trd_already_beside_it()
        {
            string scl = EmptyScl("game.scl");
            string theirs = Path.Combine(_dir, "game.trd");
            File.WriteAllBytes(theirs, TrdImage.CreateBlank());
            byte[] before = File.ReadAllBytes(theirs);

            // The same SCL in two drives, both written to.
            var beta = new Beta128();
            Assert.True(beta.InsertDisk(0, scl));
            Assert.True(beta.InsertDisk(1, scl));
            WriteFirstSector(beta, 0, Pattern(1));
            WriteFirstSector(beta, 1, Pattern(2));
            beta.EjectDisk(0);
            beta.EjectDisk(1);

            Assert.Equal(before, File.ReadAllBytes(theirs));
            Assert.Equal(Pattern(1), File.ReadAllBytes(Path.Combine(_dir, "game (1).trd")).AsSpan(Offset(1, 0, 1), 256).ToArray());
            Assert.Equal(Pattern(2), File.ReadAllBytes(Path.Combine(_dir, "game (2).trd")).AsSpan(Offset(1, 0, 1), 256).ToArray());
        }

        [Fact]
        public void An_scl_keeps_writing_to_the_trd_it_first_wrote()
        {
            string scl = EmptyScl("demo.scl");
            var beta = Ready(scl);
            WriteFirstSector(beta, 0, Pattern(1));
            beta.FlushDisk(0);
            string written = Path.Combine(_dir, "demo.trd");
            Assert.Equal(written, beta.DiskFileName(0));

            WriteFirstSector(beta, 0, Pattern(2));
            beta.EjectDisk(0);
            Assert.False(File.Exists(Path.Combine(_dir, "demo (1).trd")));
            Assert.Equal(Pattern(2), File.ReadAllBytes(written).AsSpan(Offset(1, 0, 1), 256).ToArray());
        }
    }
}
