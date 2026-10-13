using System;
using System.IO;

namespace Peripherals
{
    /// <summary>
    /// TR-DOS disk images for the Pentagon's Beta 128 interface: blank TRDs, SCL archives turned
    /// into TRDs, and telling either from anything else. The disk-format half of
    /// ZXBeta128.pas from Paul Dunn's Spin (see <see cref="Beta128"/>), ported with its comments.
    /// </summary>
    public static class TrdImage
    {
        public const int SectorsPerTrack = 16;
        public const int SectorSize = 256;
        public const int TrackSize = SectorsPerTrack * SectorSize;   // 4096
        public const int CatalogSectors = SectorsPerTrack;           // all of track 0 is reserved
        public const int SystemSectorIndex = 8;                       // track 0, sector index 8 (the 9th sector)

        // TR-DOS system-sector field offsets, relative to the start of that
        // sector (track 0, sector index 8 -- the 9th sector). Used by CreateBlank
        // and ConvertSclToTrd; the FDC logic itself never interprets disk content
        // otherwise, purely track/side/sector addressing (TR-DOS's own ROM code
        // is what understands the filesystem).
        public const int TRDSYS_FirstFreeSector = 0xE1;
        public const int TRDSYS_FirstFreeTrack = 0xE2;
        public const int TRDSYS_DiskType = 0xE3;
        public const int TRDSYS_NumFiles = 0xE4;
        public const int TRDSYS_NumFreeSectors = 0xE5;  // 2 bytes, little-endian
        public const int TRDSYS_TRDosID = 0xE7;         // fixed $10
        public const int TRDSYS_NumDeletedFiles = 0xF4;
        public const int TRDSYS_Label = 0xF5;           // 8 bytes

        public const byte TRDOS_ID_BYTE = 0x10;

        public const byte DiskType_80_2 = 0x16;  // 80 tracks, 2 sides (standard, most common)
        public const byte DiskType_40_2 = 0x17;  // 40 tracks, 2 sides
        public const byte DiskType_80_1 = 0x18;  // 80 tracks, 1 side
        public const byte DiskType_40_1 = 0x19;  // 40 tracks, 1 side

        public const int StandardTracks = 80, StandardSides = 2;

        // ------------------------------------------------------------------ telling the formats apart

        /// <summary>True when the bytes start like an SCL archive ("SINCLAIR").</summary>
        public static bool IsScl(ReadOnlySpan<byte> bytes) => bytes.StartsWith("SINCLAIR"u8);

        /// <summary>
        /// True when a file of this size, starting with these bytes, can be a TRD. A TRD has no
        /// signature, only whole tracks of sectors (the controller infers its geometry from the
        /// size), so this rules out the images that do have one.
        /// </summary>
        public static bool IsTrd(long size, ReadOnlySpan<byte> head) =>
            size > 0 && size % TrackSize == 0 && !IsScl(head) && !DskImage.IsDsk(head);

        /// <summary>A TRD or SCL image: something the Beta 128 can take.</summary>
        public static bool IsTrDosImage(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                Span<byte> head = stackalloc byte[8];
                int read = fs.Read(head);
                head = head.Slice(0, read);
                return IsScl(head) || IsTrd(fs.Length, head);
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Where an SCL's changes are written: a .trd beside it of the same name, or, when that is
        /// taken, the first free "name (n).trd".
        /// </summary>
        public static string FreeTrdNameFor(string sclPath)
        {
            string dir = Path.GetDirectoryName(sclPath) ?? "";
            string name = Path.GetFileNameWithoutExtension(sclPath);
            string path = Path.Combine(dir, name + ".trd");
            for (int n = 1; File.Exists(path); n++)
                path = Path.Combine(dir, $"{name} ({n}).trd");
            return path;
        }

        // ------------------------------------------------------------------ blank disks

        // Shared by CreateBlank (empty catalog) and ConvertSclToTrd (populated
        // catalog) -- both need to fill in exactly the same system-sector fields,
        // just with different values.
        private static void WriteSystemSector(byte[] data, int sysOffset,
            byte diskType, int numFiles, byte firstFreeTrack, byte firstFreeSector, int freeSectors)
        {
            data[sysOffset + 0] = 0x00;  // catalog_end
            data[sysOffset + TRDSYS_FirstFreeSector] = firstFreeSector;
            data[sysOffset + TRDSYS_FirstFreeTrack] = firstFreeTrack;
            data[sysOffset + TRDSYS_DiskType] = diskType;
            data[sysOffset + TRDSYS_NumFiles] = (byte)numFiles;
            data[sysOffset + TRDSYS_NumFreeSectors] = (byte)(freeSectors & 0xFF);
            data[sysOffset + TRDSYS_NumFreeSectors + 1] = (byte)((freeSectors >> 8) & 0xFF);
            data[sysOffset + TRDSYS_TRDosID] = TRDOS_ID_BYTE;
            data[sysOffset + TRDSYS_NumDeletedFiles] = 0;
            for (int i = 0; i < 8; i++)
                data[sysOffset + TRDSYS_Label + i] = (byte)' ';
        }

        /// <summary>An empty, formatted TR-DOS disk: 80 tracks, double sided (640K) unless asked otherwise.</summary>
        public static byte[] CreateBlank(int tracks = StandardTracks, int sides = StandardSides)
        {
            if (tracks <= 0 || sides < 1 || sides > 2) throw new ArgumentOutOfRangeException(nameof(tracks));

            int totalSectors = tracks * sides * SectorsPerTrack;
            var data = new byte[totalSectors * SectorSize];

            // Track 0 (its first side only, 16 sectors -- 8 catalog + this system
            // sector + 7 unused/reserved) is entirely reserved; the FDC layer doesn't
            // need to understand this beyond "don't count it as free". This was
            // previously computed with the wrong constant -- CatalogSectors (16) was
            // used directly as a sector INDEX, landing the system sector at the start
            // of track 1 instead of track 0's 9th sector -- fixed by using the actual
            // sector index (SystemSectorIndex = 8) here instead.
            int sysOffset = SystemSectorIndex * SectorSize;
            int freeSectors = totalSectors - CatalogSectors;

            byte diskType = sides == 2
                ? (tracks >= 80 ? DiskType_80_2 : DiskType_40_2)
                : (tracks >= 80 ? DiskType_80_1 : DiskType_40_1);

            WriteSystemSector(data, sysOffset, diskType, 0, 1, 0, freeSectors);  // file data starts at (logical) track 1, sector 0
            return data;
        }

        public static void WriteBlank(string path) => File.WriteAllBytes(path, CreateBlank());

        // ------------------------------------------------------------------ SCL to TRD

        // SCL is a compact TR-DOS archive format: "SINCLAIR" + file count, then one
        // 14-byte catalog-entry-minus-location per file (byte-identical to a real
        // TR-DOS catalog entry, per the Kaitai tr_dos_image spec, except it omits
        // the trailing starting_sector/starting_track -- SCL doesn't record where
        // each file physically lives, since it isn't a real disk image), then all
        // the files' raw sector data concatenated in the same order. Building a
        // .trd from this just means assigning each file a physical location
        // (sequentially, starting right after the reserved system track) and
        // writing a normal populated catalog + system sector -- exactly what
        // SpecEmu's own wd1793_SCL2TRD and the real wd1793.dll's own export of the
        // same name do (this project has no source for either, so this is built
        // from the format spec directly, not ported from anything).
        public static bool ConvertSclToTrd(byte[] sclData, out byte[] trdData)
        {
            const int MaxCatalogFiles = 128;   // TR-DOS catalog is a fixed 8 sectors = 128 x 16-byte entries
            const int Tracks = StandardTracks;
            const int Sides = StandardSides;

            trdData = null;

            if (sclData.Length < 9) return false;
            if (!IsScl(sclData)) return false;

            int numFiles = sclData[8];
            if (numFiles > MaxCatalogFiles) return false;   // more files than a real TR-DOS catalog can ever hold

            int headerOffset = 9;
            int fileDataOffset = headerOffset + numFiles * 14;
            if (fileDataOffset > sclData.Length) return false;

            int totalSectors = Tracks * Sides * SectorsPerTrack;
            var trd = new byte[totalSectors * SectorSize];

            int curTrack = 1;   // logical track 1 -- right after the reserved system track
            int curSector = 0;  // logical track (physical*2+side), 0-based sector within it

            for (int i = 0; i < numFiles; i++)
            {
                // Copy the SCL entry's 14 bytes verbatim (name, type, position_and_length,
                // length_sectors -- byte-identical to the first 14 bytes of a real TR-DOS
                // catalog entry) and append the physical location SCL doesn't carry.
                int catalogOffset = i * 16;
                Array.Copy(sclData, headerOffset + i * 14, trd, catalogOffset, 14);
                byte lengthSectors = sclData[headerOffset + i * 14 + 13];
                trd[catalogOffset + 14] = (byte)curSector;
                trd[catalogOffset + 15] = (byte)curTrack;

                if (fileDataOffset + lengthSectors * SectorSize > sclData.Length) return false;   // truncated/corrupt archive

                int trdOffset = curTrack * TrackSize + curSector * SectorSize;
                if (trdOffset + lengthSectors * SectorSize > trd.Length) return false;   // ran out of room on a standard 80/2 disk
                Array.Copy(sclData, fileDataOffset, trd, trdOffset, lengthSectors * SectorSize);
                fileDataOffset += lengthSectors * SectorSize;

                curSector += lengthSectors;
                while (curSector >= SectorsPerTrack)
                {
                    curSector -= SectorsPerTrack;
                    curTrack++;
                }
            }

            int sysOffset = SystemSectorIndex * SectorSize;
            int freeSectors = (totalSectors - CatalogSectors) - ((curTrack - 1) * SectorsPerTrack + curSector);
            WriteSystemSector(trd, sysOffset, DiskType_80_2, numFiles, (byte)curTrack, (byte)curSector, freeSectors);

            trdData = trd;
            return true;
        }
    }
}
