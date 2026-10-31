using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace XisfPreview.Tests
{
    // Builds minimal, spec-shaped FITS files in memory.
    internal static class Fits
    {
        public static string Card(string key, string value) =>
            (key.PadRight(8) + "= " + value).PadRight(80).Substring(0, 80);

        public static byte[] Header(IEnumerable<string> cards)
        {
            var text = new StringBuilder();
            foreach (string c in cards) text.Append(c.PadRight(80));
            text.Append("END".PadRight(80));
            while (text.Length % 2880 != 0) text.Append(' ');
            return Encoding.ASCII.GetBytes(text.ToString());
        }

        public static byte[] File(int bitpix, int w, int h, int ch, byte[] data, params string[] extraCards)
        {
            var cards = new List<string>
            {
                Card("SIMPLE", "T"),
                Card("BITPIX", bitpix.ToString()),
                Card("NAXIS", ch > 1 ? "3" : "2"),
                Card("NAXIS1", w.ToString()),
                Card("NAXIS2", h.ToString())
            };
            if (ch > 1) cards.Add(Card("NAXIS3", ch.ToString()));
            cards.AddRange(extraCards);
            return Header(cards).Concat(data).ToArray();
        }

        public static byte[] Be16(IEnumerable<short> v) =>
            v.SelectMany(s => new[] { (byte)(s >> 8), (byte)s }).ToArray();

        public static byte[] Be32(IEnumerable<int> v) =>
            v.SelectMany(i => new[] { (byte)(i >> 24), (byte)(i >> 16), (byte)(i >> 8), (byte)i }).ToArray();

        public static byte[] BeFloat(IEnumerable<float> v) =>
            v.SelectMany(f => BitConverter.GetBytes(f).Reverse()).ToArray();

        // Unsigned 16-bit samples in the standard BZERO=32768 encoding.
        public static byte[] U16(int w, int h, IEnumerable<ushort> samples, params string[] extra)
        {
            var cards = new List<string> { Card("BZERO", "32768") };
            cards.AddRange(extra);
            return File(16, w, h, 1, Be16(samples.Select(s => (short)(s - 32768))), cards.ToArray());
        }
    }

    // Builds monolithic XISF files in memory.
    internal static class Xisf
    {
        public const string Ns = "http://www.pixinsight.com/xisf";

        public static byte[] Wrap(string xml, byte[] attachment = null, int attachmentOffsetPadding = 0)
        {
            byte[] xmlBytes = Encoding.UTF8.GetBytes(xml);
            using (var ms = new MemoryStream())
            {
                ms.Write(Encoding.ASCII.GetBytes("XISF0100"), 0, 8);
                ms.Write(BitConverter.GetBytes((uint)xmlBytes.Length), 0, 4);
                ms.Write(new byte[4], 0, 4);
                ms.Write(xmlBytes, 0, xmlBytes.Length);
                if (attachment != null) ms.Write(attachment, 0, attachment.Length);
                return ms.ToArray();
            }
        }

        // The XML length depends on the attachment offset it declares, so the offset is
        // written with a fixed width.
        public static byte[] Attached(string imageAttrs, byte[] data, string inner = "", long? declaredSize = null)
        {
            string Xml(long pos) =>
                $"<xisf version=\"1.0\" xmlns=\"{Ns}\"><Image {imageAttrs} " +
                $"location=\"attachment:{pos:D12}:{declaredSize ?? data.Length}\">{inner}</Image></xisf>";

            int headerLen = Encoding.UTF8.GetByteCount(Xml(0));
            return Wrap(Xml(16 + headerLen), data);
        }

        public static byte[] U16(int w, int h, int ch, IEnumerable<ushort> samples, string storage = "Planar") =>
            Attached($"geometry=\"{w}:{h}:{ch}\" sampleFormat=\"UInt16\" pixelStorage=\"{storage}\"",
                samples.SelectMany(BitConverter.GetBytes).ToArray());

        public static byte[] F32(int w, int h, int ch, IEnumerable<float> samples, string attrs = "") =>
            Attached($"geometry=\"{w}:{h}:{ch}\" sampleFormat=\"Float32\" {attrs}",
                samples.SelectMany(BitConverter.GetBytes).ToArray());
    }

    // Seekable stream of a fixed prefix followed by zeros, so multi-GB files can be tested
    // without touching the disk or allocating the memory.
    internal sealed class VirtualFileStream : Stream
    {
        private readonly byte[] _prefix;
        private readonly long _length;
        public long BytesRead { get; private set; }

        public VirtualFileStream(byte[] prefix, long length) { _prefix = prefix; _length = length; }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position { get; set; }
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin)
        {
            Position = origin == SeekOrigin.Begin ? offset : origin == SeekOrigin.Current ? Position + offset : _length + offset;
            return Position;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = (int)Math.Min(count, Math.Max(0, _length - Position));
            for (int i = 0; i < n; i++)
            {
                long p = Position + i;
                buffer[offset + i] = p < _prefix.Length ? _prefix[p] : (byte)0;
            }
            Position += n;
            BytesRead += n;
            return n;
        }
    }

    // Wraps a stream as forward-only, as a shell IStream may present it.
    internal sealed class ForwardOnlyStream : Stream
    {
        private readonly Stream _inner;
        public ForwardOnlyStream(byte[] data) { _inner = new MemoryStream(data); }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
        public override int Read(byte[] b, int o, int c) => _inner.Read(b, o, c);
    }

    internal static class Pixels
    {
        public static byte[] Rgb(Bitmap bmp)
        {
            var result = new byte[bmp.Width * bmp.Height * 3];
            int i = 0;
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    result[i++] = c.R; result[i++] = c.G; result[i++] = c.B;
                }
            return result;
        }

        // Dark sky with noise around 'level' plus a linear ramp, as a linear astro frame looks.
        public static ushort[] LinearSky(int w, int h, ushort level, int noise, int seed = 1)
        {
            var rnd = new Random(seed);
            var s = new ushort[w * h];
            for (int i = 0; i < s.Length; i++) s[i] = (ushort)(level + rnd.Next(-noise, noise + 1));
            return s;
        }
    }
}
