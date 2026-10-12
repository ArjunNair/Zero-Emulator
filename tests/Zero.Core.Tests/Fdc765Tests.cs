using System;
using System.IO;
using System.Linq;
using Peripherals;
using Xunit;

namespace Zero.Core.Tests
{
    /// <summary>
    /// The µPD765 port, driven through its two ports the way +3DOS drives it: wait for the main
    /// status register to ask for a byte, then write or read the data register.
    /// </summary>
    public class Fdc765Tests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "zero-fdc-" + Guid.NewGuid().ToString("N"));

        public Fdc765Tests() => Directory.CreateDirectory(_dir);

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch { }
        }

        private const byte RQM = 0x80, DIO = 0x40;

        private static void Send(Fdc765 fdc, params byte[] bytes)
        {
            foreach (byte b in bytes)
            {
                Assert.Equal(RQM, fdc.StatusPortRead() & (RQM | DIO)); // ready, CPU to FDC
                fdc.DataPortWrite(b);
            }
        }

        private static byte Receive(Fdc765 fdc)
        {
            Assert.Equal(RQM | DIO, fdc.StatusPortRead() & (RQM | DIO)); // ready, FDC to CPU
            return fdc.DataPortRead();
        }

        private static byte[] Receive(Fdc765 fdc, int count) => Enumerable.Range(0, count).Select(_ => Receive(fdc)).ToArray();

        private static int SectorOffset(int track, int sector) => 0x100 + track * 0x1300 + 0x100 + (sector - 1) * 512;

        private static byte[] Pattern(int seed) => Enumerable.Range(0, 512).Select(i => (byte)(i * 7 + seed)).ToArray();

        private string BlankDisk(string name = "disk.dsk", Action<byte[]> edit = null)
        {
            byte[] image = DskImage.CreateBlankPlus3();
            edit?.Invoke(image);
            string path = Path.Combine(_dir, name);
            File.WriteAllBytes(path, image);
            return path;
        }

        private static Fdc765 Ready(string disk)
        {
            var fdc = new Fdc765();
            Assert.True(fdc.InsertDisk(disk, 0));
            fdc.SetMotorState(0x08);
            return fdc;
        }

        private static void SeekTo(Fdc765 fdc, byte track)
        {
            Send(fdc, 0x0f, 0x00, track);                     // SEEK unit 0
            Send(fdc, 0x08);                                  // SENSE INTERRUPT STATUS
            byte[] r = Receive(fdc, 2);
            Assert.Equal(0x20, r[0] & 0xf8);                  // seek end, normal termination
            Assert.Equal(track, r[1]);                        // present cylinder
        }

        [Fact]
        public void Reports_itself_as_a_uPD765A()
        {
            using var fdc = new Fdc765();
            Assert.Equal(RQM, fdc.StatusPortRead());
            Send(fdc, 0x10);                                  // VERSION
            Assert.Equal(0x80, Receive(fdc));
            Assert.Equal(RQM, fdc.StatusPortRead() & (RQM | DIO));
        }

        [Fact]
        public void Recalibrates_and_seeks()
        {
            using var fdc = Ready(BlankDisk());
            Send(fdc, 0x07, 0x00);                            // RECALIBRATE unit 0
            Send(fdc, 0x08);
            byte[] r = Receive(fdc, 2);
            Assert.Equal(0x20, r[0] & 0xf8);
            Assert.Equal(0, r[1]);
            SeekTo(fdc, 17);
            Assert.Equal(17, fdc.CurrentTrack(0));
        }

        [Fact]
        public void Reads_a_sector()
        {
            byte[] data = Pattern(3);
            using var fdc = Ready(BlankDisk(edit: img => data.CopyTo(img, SectorOffset(2, 3))));
            SeekTo(fdc, 2);

            Send(fdc, 0x46, 0x00, 2, 0, 3, 2, 3, 0x2a, 0xff); // READ DATA (MFM) C2 H0 R3 N2 EOT3
            byte[] read = Receive(fdc, 512);
            byte[] result = Receive(fdc, 7);

            Assert.Equal(data, read);
            Assert.Equal(0x40, result[0] & 0xc0);             // ends at EOT without a terminal count, as on the +3
            Assert.Equal(0x80, result[1]);                    // end of cylinder, nothing else
            Assert.Equal(0x00, result[2]);
            Assert.Equal(new byte[] { 2, 0, 3, 2 }, result.Skip(3).ToArray());
            Assert.Equal(RQM, fdc.StatusPortRead() & (RQM | DIO));
        }

        [Fact]
        public void Reports_a_missing_sector()
        {
            using var fdc = Ready(BlankDisk());
            SeekTo(fdc, 1);
            Send(fdc, 0x46, 0x00, 1, 0, 42, 2, 42, 0x2a, 0xff); // there is no sector 42
            byte[] result = Receive(fdc, 7);
            Assert.Equal(0x40, result[0] & 0xc0);             // abnormal termination
            Assert.Equal(0x04, result[1] & 0x04);             // no data
        }

        [Fact]
        public void Reads_sector_ids()
        {
            using var fdc = Ready(BlankDisk());
            SeekTo(fdc, 5);
            Send(fdc, 0x4a, 0x00);                            // READ ID
            byte[] result = Receive(fdc, 7);
            Assert.Equal(5, result[3]);                       // C
            Assert.Equal(0, result[4]);                       // H
            Assert.InRange(result[5], 1, 9);                  // R
            Assert.Equal(2, result[6]);                       // N
        }

        [Fact]
        public void Writes_a_sector_back_to_the_image_in_place()
        {
            string disk = BlankDisk();
            long size = new FileInfo(disk).Length;
            byte[] data = Pattern(11);
            using (var fdc = Ready(disk))
            {
                SeekTo(fdc, 2);
                Send(fdc, 0x45, 0x00, 2, 0, 4, 2, 4, 0x2a, 0xff); // WRITE DATA C2 H0 R4 N2 EOT4
                Send(fdc, data);
                byte[] result = Receive(fdc, 7);
                Assert.Equal(0, result[1] & 0x02);            // not write protected

                // And it reads back before the disk leaves the drive.
                Send(fdc, 0x46, 0x00, 2, 0, 4, 2, 4, 0x2a, 0xff);
                Assert.Equal(data, Receive(fdc, 512));
                Receive(fdc, 7);
            }

            byte[] image = File.ReadAllBytes(disk);
            Assert.Equal(size, image.Length);                  // updated, not appended to
            Assert.Equal(data, image.AsSpan(SectorOffset(2, 4), 512).ToArray());
            Assert.True(image.AsSpan(SectorOffset(2, 5), 512).ToArray().All(b => b == 0xe5)); // its neighbour untouched
        }

        [Fact]
        public void A_read_only_image_is_write_protected()
        {
            string disk = BlankDisk();
            File.SetAttributes(disk, FileAttributes.ReadOnly);
            try
            {
                using var fdc = Ready(disk);
                Assert.True(fdc.WriteProtected(0));
                Send(fdc, 0x04, 0x00);                        // SENSE DRIVE STATUS
                Assert.Equal(0x40, Receive(fdc) & 0x40);      // write protected
                SeekTo(fdc, 1);
                Send(fdc, 0x45, 0x00, 1, 0, 1, 2, 1, 0x2a, 0xff);
                byte[] result = Receive(fdc, 7);
                Assert.Equal(0x02, result[1] & 0x02);         // not writable
            }
            finally { File.SetAttributes(disk, FileAttributes.Normal); }
        }

        [Fact]
        public void No_disk_means_not_ready()
        {
            using var fdc = new Fdc765();
            fdc.SetMotorState(0x08);
            Send(fdc, 0x46, 0x00, 0, 0, 1, 2, 1, 0x2a, 0xff);
            byte[] result = Receive(fdc, 7);
            Assert.Equal(0x48, result[0] & 0xc8);             // abnormal termination, not ready
        }

        [Fact]
        public void Reads_an_extended_dsk()
        {
            byte[] data = Pattern(29);
            byte[] plain = DskImage.CreateBlankPlus3();
            data.CopyTo(plain, SectorOffset(3, 7));
            string disk = Path.Combine(_dir, "extended.dsk");
            File.WriteAllBytes(disk, ToExtended(plain));

            using var fdc = Ready(disk);
            Assert.True(fdc.WriteProtected(0));               // extended images are read-only, as in the DLL
            SeekTo(fdc, 3);
            Send(fdc, 0x46, 0x00, 3, 0, 7, 2, 7, 0x2a, 0xff);
            Assert.Equal(data, Receive(fdc, 512));
            Receive(fdc, 7);
        }

        [Fact]
        public void Rejects_a_file_that_is_not_a_disk_image()
        {
            string notDisk = Path.Combine(_dir, "notes.dsk");
            File.WriteAllBytes(notDisk, new byte[4096]);
            using var fdc = new Fdc765();
            Assert.False(fdc.InsertDisk(notDisk, 0));
            Assert.False(fdc.DiskInserted(0));
        }

        /// <summary>The same disk as an extended DSK: a track size table instead of one size, and each sector's length in its info entry.</summary>
        private static byte[] ToExtended(byte[] plain)
        {
            int tracks = plain[0x30], trackSize = plain[0x32] | plain[0x33] << 8;
            var e = (byte[])plain.Clone();
            "EXTENDED CPC DSK File\r\nDisk-Info\r\n"u8.ToArray().CopyTo(e, 0);
            e[0x32] = e[0x33] = 0;
            for (int t = 0; t < tracks; t++)
            {
                e[0x34 + t] = (byte)(trackSize >> 8);
                int info = 0x100 + t * trackSize + 0x18;
                for (int s = 0; s < 9; s++)
                {
                    e[info + s * 8 + 6] = 0x00;               // 512 bytes of data stored for this sector
                    e[info + s * 8 + 7] = 0x02;
                }
            }
            return e;
        }
    }
}
