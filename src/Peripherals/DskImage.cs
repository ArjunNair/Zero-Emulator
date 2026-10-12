using System;
using System.IO;
using System.Text;

namespace Peripherals
{
    /// <summary>
    /// DSK (CPCEMU) disk images: blank +3 disks, and telling a DSK from anything else.
    ///
    /// Blank disks are made here rather than with FORMAT because the controller cannot format: like
    /// the fdc765.dll it is ported from, it takes the Format Track command and does nothing.
    /// </summary>
    public static class DskImage
    {
        public const int Plus3Tracks = 40, Plus3Sectors = 9, Plus3SectorSize = 512;
        private const int TrackSize = 0x100 + Plus3Sectors * Plus3SectorSize; // header + data = 0x1300

        /// <summary>True when the bytes start like a DSK or extended DSK image.</summary>
        public static bool IsDsk(ReadOnlySpan<byte> bytes) =>
            bytes.StartsWith("MV - CPC"u8) || bytes.StartsWith("EXTENDED"u8);

        public static bool IsDsk(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                Span<byte> head = stackalloc byte[8];
                return fs.Read(head) == 8 && IsDsk(head);
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// A freshly formatted +3 disk (single sided, 40 tracks of nine 512-byte sectors, 173K free),
        /// as +3 BASIC's FORMAT would leave it: the disk specification in the boot sector, and every
        /// other byte the format filler E5, which is also what marks directory entries unused.
        /// </summary>
        public static byte[] CreateBlankPlus3()
        {
            var image = new byte[0x100 + Plus3Tracks * TrackSize];

            Write(image, 0x00, "MV - CPCEMU Disk-File\r\nDisk-Info\r\n");
            Write(image, 0x22, "Zero X");
            image[0x30] = Plus3Tracks;
            image[0x31] = 1;                         // sides
            image[0x32] = TrackSize & 0xff;
            image[0x33] = TrackSize >> 8;

            for (int track = 0; track < Plus3Tracks; track++)
            {
                int t = 0x100 + track * TrackSize;
                Write(image, t, "Track-Info\r\n");
                image[t + 0x10] = (byte)track;
                image[t + 0x11] = 0;                 // side
                image[t + 0x14] = 2;                 // N: 128 << 2 = 512 bytes
                image[t + 0x15] = Plus3Sectors;
                image[t + 0x16] = 0x52;              // gap 3 (format)
                image[t + 0x17] = 0xe5;              // filler
                for (int s = 0; s < Plus3Sectors; s++)
                {
                    int info = t + 0x18 + s * 8;
                    image[info + 0] = (byte)track;   // C
                    image[info + 1] = 0;             // H
                    image[info + 2] = (byte)(s + 1); // R: sectors 1..9
                    image[info + 3] = 2;             // N
                }
                image.AsSpan(t + 0x100, Plus3Sectors * Plus3SectorSize).Fill(0xe5);
            }

            // Disk specification, at the start of track 0 sector 1: +3 format, single sided,
            // 40 tracks, 9 sectors, 512-byte sectors, 1 reserved track, 1K blocks, 2 directory
            // blocks, read/write gap 2A, format gap 52.
            byte[] spec = { 0x00, 0x00, 0x28, 0x09, 0x02, 0x01, 0x03, 0x02, 0x2a, 0x52, 0, 0, 0, 0, 0, 0 };
            spec.CopyTo(image, 0x100 + 0x100);
            return image;
        }

        public static void WriteBlankPlus3(string path) => File.WriteAllBytes(path, CreateBlankPlus3());

        private static void Write(byte[] image, int at, string ascii) => Encoding.ASCII.GetBytes(ascii).CopyTo(image, at);
    }
}
