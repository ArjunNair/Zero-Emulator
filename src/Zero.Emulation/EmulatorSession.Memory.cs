using System;
using System.Threading.Tasks;
using SpeccyCommon;

namespace Zero.Emulation
{
    /// <summary>Raw memory access for the Load/Save Binary tools and BASIC keyword injection.</summary>
    public sealed partial class EmulatorSession
    {
        public const int BankSize = 16384;

        /// <summary>Whether the current machine has switchable RAM banks (everything but the 48K).</summary>
        public bool HasRamBanks => Model != MachineModel._48k;

        /// <summary>Copy bytes into the address space from <paramref name="address"/> (16384..65535), truncated at 65536. Returns bytes written.</summary>
        public Task<int> LoadBinaryAsync(byte[] data, int address)
        {
            if (address < 16384 || address > 65535) throw new ArgumentOutOfRangeException(nameof(address), "Address must be 16384..65535");
            return InvokeAsync(() =>
            {
                int count = Math.Min(data.Length, 65536 - address);
                _zx.PokeBytesNoContend(address, 0, count, data);
                return count;
            });
        }

        /// <summary>Copy up to 16K into RAM bank 0..7 (128K machines). Returns bytes written.</summary>
        public Task<int> LoadBinaryToBankAsync(byte[] data, int bank)
        {
            if (bank < 0 || bank > 7) throw new ArgumentOutOfRangeException(nameof(bank));
            return InvokeAsync(() =>
            {
                int count = Math.Min(data.Length, BankSize);
                _zx.PokeRAMPage(bank, count, data); // takes the 16K bank number and spans both 8K halves
                return count;
            });
        }

        /// <summary>Read <paramref name="length"/> bytes from <paramref name="address"/> (0..65535), truncated at 65536.</summary>
        public Task<byte[]> SaveBinaryAsync(int address, int length)
        {
            if (address < 0 || address > 65535) throw new ArgumentOutOfRangeException(nameof(address));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            return InvokeAsync(() =>
            {
                int count = Math.Min(length, 65536 - address);
                var result = new byte[count];
                for (int i = 0; i < count; i++) result[i] = _zx.PeekByteNoContend((ushort)(address + i));
                return result;
            });
        }

        /// <summary>Read the first <paramref name="length"/> bytes (max 16K) of RAM bank 0..7.</summary>
        public Task<byte[]> SaveBankAsync(int bank, int length)
        {
            if (bank < 0 || bank > 7) throw new ArgumentOutOfRangeException(nameof(bank));
            return InvokeAsync(() =>
            {
                int count = Math.Clamp(length, 0, BankSize);
                var result = new byte[count];
                Array.Copy(_zx.GetRAMBank(bank), 0, result, 0, count);
                return result;
            });
        }

        /// <summary>
        /// Drop a character or keyword token into the BASIC editor as if typed (LAST_K + FLAGS bit 5), the
        /// way the original keyboard helper did. Token 165 + index into <see cref="SpeccyGlobals.Keywords"/>.
        /// </summary>
        public void TypeToken(byte token) => Post(() =>
        {
            if (_zx == null) return;
            _zx.PokeByteNoContend(SV_LAST_K, token);
            _zx.PokeByteNoContend(SV_FLAGS, _zx.PeekByteNoContend(SV_FLAGS) | 0x20);
        });

        public static byte KeywordToken(string keyword)
        {
            int index = Array.IndexOf(SpeccyGlobals.Keywords, keyword);
            if (index < 0) throw new ArgumentException("Unknown keyword: " + keyword);
            return (byte)(165 + index);
        }
    }
}
