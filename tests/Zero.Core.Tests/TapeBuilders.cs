using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Peripherals;

namespace Zero.Core.Tests
{
    // Small synthetic tapes for the tape tests, and a way to watch one play.
    internal static class TapeBuilders
    {
        // ------------------------------------------------------------------ PZX

        public static byte[] Pzx(params byte[][] blocks)
        {
            var ms = new MemoryStream();
            Block(ms, "PZXT", new byte[] { 1, 0 });
            foreach (byte[] b in blocks) ms.Write(b);
            return ms.ToArray();
        }

        public static byte[] Puls(params (int count, uint duration)[] pulses)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            foreach (var (count, duration) in pulses)
            {
                // A long duration also starts with bit 15 set, so it needs the count word in front.
                if (count > 1 || duration >= 0x8000) w.Write((ushort)(0x8000 | count));
                if (duration >= 0x8000)
                {
                    w.Write((ushort)(0x8000 | (duration >> 16)));
                    w.Write((ushort)(duration & 0xFFFF));
                }
                else
                    w.Write((ushort)duration);
            }
            return Wrap("PULS", ms.ToArray());
        }

        public static byte[] Data(int initialLevel, uint bits, ushort tail, ushort[] s0, ushort[] s1, params byte[] data)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(bits | (initialLevel != 0 ? 0x80000000u : 0));
            w.Write(tail);
            w.Write((byte)s0.Length);
            w.Write((byte)s1.Length);
            foreach (ushort p in s0) w.Write(p);
            foreach (ushort p in s1) w.Write(p);
            w.Write(data);
            return Wrap("DATA", ms.ToArray());
        }

        public static byte[] Paus(int level, uint duration) =>
            Wrap("PAUS", BitConverter.GetBytes(duration | (level != 0 ? 0x80000000u : 0)));

        public static byte[] Stop(ushort flag) => Wrap("STOP", BitConverter.GetBytes(flag));

        private static byte[] Wrap(string tag, byte[] body)
        {
            var ms = new MemoryStream();
            Block(ms, tag, body);
            return ms.ToArray();
        }

        private static void Block(MemoryStream ms, string tag, byte[] body)
        {
            ms.Write(Encoding.ASCII.GetBytes(tag));
            ms.Write(BitConverter.GetBytes((uint)body.Length));
            ms.Write(body);
        }

        // ------------------------------------------------------------------ TZX

        public static byte[] Tzx(params byte[][] blocks)
        {
            var ms = new MemoryStream();
            ms.Write(Encoding.ASCII.GetBytes("ZXTape!\x1A"));
            ms.WriteByte(1);
            ms.WriteByte(20);
            foreach (byte[] b in blocks) ms.Write(b);
            return ms.ToArray();
        }

        // Little-endian bytes from a mix of byte, ushort, short, uint, int, byte[] and string.
        public static byte[] B(params object[] parts)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            foreach (object p in parts)
            {
                switch (p)
                {
                    case byte v: w.Write(v); break;
                    case ushort v: w.Write(v); break;
                    case short v: w.Write(v); break;
                    case uint v: w.Write(v); break;
                    case int v: w.Write(v); break;
                    case byte[] v: w.Write(v); break;
                    case string v: w.Write(Encoding.ASCII.GetBytes(v)); break;
                    default: throw new ArgumentException(p.GetType().Name);
                }
            }
            return ms.ToArray();
        }

        public static byte[] U24(uint v) => new[] { (byte)v, (byte)(v >> 8), (byte)(v >> 16) };

        public static byte[] Standard(ushort pause, params byte[] data) =>
            B((byte)0x10, pause, (ushort)data.Length, data);

        public static byte[] Turbo(ushort pilot, ushort sync1, ushort sync2, ushort zero, ushort one, ushort pilotPulses,
                                   byte usedBits, ushort pause, params byte[] data) =>
            B((byte)0x11, pilot, sync1, sync2, zero, one, pilotPulses, usedBits, pause, U24((uint)data.Length), data);

        public static byte[] PureTone(ushort length, ushort count) => B((byte)0x12, length, count);

        // ------------------------------------------------------------------ watching it play

        // Play from where the tape is parked and record when the level changes, in T-states from
        // the start: (time, level after). Stops when the tape does, or after maxEdges.
        public static List<(long time, int level)> Edges(TapePlayer player, out TapeEdge seen, int maxEdges = 100000)
        {
            var edges = new List<(long, int)>();
            seen = player.Begin();
            long t = 0;
            int level = player.Level;
            for (int n = 0; n < maxEdges && player.IsActive; n++)
            {
                long dt = player.TStatesToNextEdge;
                t += dt;
                player.AddTime(dt);
                while (player.EdgeDue)
                {
                    TapeEdge e = player.NextEdge();
                    seen |= e;
                }
                if (player.Level != level)
                {
                    level = player.Level;
                    edges.Add((t, level));
                }
            }
            return edges;
        }

        // The lengths of the pulses between level changes.
        public static List<long> Intervals(List<(long time, int level)> edges)
        {
            var list = new List<long>();
            long last = 0;
            foreach (var (time, _) in edges)
            {
                list.Add(time - last);
                last = time;
            }
            return list;
        }
    }
}
