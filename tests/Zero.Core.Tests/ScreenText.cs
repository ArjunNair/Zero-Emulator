using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Speccy;

namespace Zero.Core.Tests
{
    /// <summary>
    /// Reads the Spectrum display file back as text by matching each 8x8 character cell against
    /// the 48K ROM font (0x3D00..0x3FFF, chars 32..127). Cells that match nothing become '?'.
    /// Good enough to assert on boot messages and menus without a renderer.
    /// </summary>
    internal static class ScreenText
    {
        private static Dictionary<ulong, char> _font;

        private static Dictionary<ulong, char> Font
        {
            get
            {
                if (_font == null)
                {
                    byte[] rom = File.ReadAllBytes(Path.Combine(TestPaths.RomDir, "48k.rom"));
                    var font = new Dictionary<ulong, char>();
                    for (int ch = 32; ch < 128; ch++)
                    {
                        ulong key = 0;
                        for (int row = 0; row < 8; row++)
                            key = (key << 8) | rom[0x3D00 + (ch - 32) * 8 + row];
                        font[key] = ch == 127 ? '©' : (char)ch;
                    }
                    _font = font;
                }
                return _font;
            }
        }

        public static string[] Read(zx_spectrum zx)
        {
            var lines = new string[24];
            for (int row = 0; row < 24; row++)
            {
                var sb = new StringBuilder(32);
                for (int col = 0; col < 32; col++)
                {
                    ulong key = 0;
                    for (int y = 0; y < 8; y++)
                    {
                        int line = row * 8 + y;
                        int addr = 0x4000 | ((line & 0xC0) << 5) | ((line & 0x07) << 8) | ((line & 0x38) << 2) | col;
                        key = (key << 8) | zx.PeekByteNoContend((ushort)addr);
                    }
                    if (key == 0)
                        sb.Append(' ');
                    else if (Font.TryGetValue(key, out char c))
                        sb.Append(c);
                    else if (Font.TryGetValue(~key, out char inv))
                        sb.Append(inv); // inverse video (pixel-inverted) cell
                    else
                        sb.Append('?');
                }
                lines[row] = sb.ToString().TrimEnd();
            }
            return lines;
        }

        public static string Dump(zx_spectrum zx) => string.Join(Environment.NewLine, Read(zx));
    }
}
