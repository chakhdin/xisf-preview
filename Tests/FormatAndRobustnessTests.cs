using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using SharpShell.Attributes;
using XisfExplorerPreview;
using Xunit;

namespace XisfPreview.Tests
{
    public class AstroFormatDetectorTests
    {
        [Fact]
        public void Null_IsUnknown() => Assert.Equal(AstroFileFormat.Unknown, AstroFormatDetector.DetectFormat(null));

        [Theory]
        [InlineData("XISF0100", AstroFileFormat.Xisf)]
        [InlineData("SIMPLE  ", AstroFileFormat.Fits)]
        [InlineData("GIF89a..", AstroFileFormat.Unknown)]
        [InlineData("XI", AstroFileFormat.Unknown)]
        [InlineData("", AstroFileFormat.Unknown)]
        public void Signature(string head, AstroFileFormat expected) =>
            Assert.Equal(expected, AstroFormatDetector.DetectFormat(new MemoryStream(Encoding.ASCII.GetBytes(head))));

        [Fact]
        public void Detect_RestoresPosition()
        {
            var ms = new MemoryStream(Encoding.ASCII.GetBytes("XISF0100........"));
            ms.Position = 2;
            AstroFormatDetector.DetectFormat(ms);
            Assert.Equal(2, ms.Position);
        }

        [Fact]
        public void Render_Xisf()
        {
            byte[] file = Xisf.U16(4, 3, 1, Enumerable.Repeat((ushort)500, 12));
            using (Bitmap bmp = AstroFormatDetector.RenderImageFromStream(new MemoryStream(file), 0))
                Assert.Equal(new Size(4, 3), bmp.Size);
        }

        [Fact]
        public void Render_Fits()
        {
            byte[] file = Fits.U16(4, 3, Enumerable.Repeat((ushort)500, 12));
            using (Bitmap bmp = AstroFormatDetector.RenderImageFromStream(new MemoryStream(file), 0))
                Assert.Equal(new Size(4, 3), bmp.Size);
        }

        [Fact]
        public void Render_Unknown_IsNull() =>
            Assert.Null(AstroFormatDetector.RenderImageFromStream(new MemoryStream(new byte[32]), 0));

        // The detector eats the signature when it cannot seek back. Public API accepts such
        // streams; XisfThumbnailHandler only avoids this by buffering first.
        [Fact]
        public void Render_ForwardOnlyStream_Works()
        {
            byte[] file = Fits.U16(4, 3, Enumerable.Repeat((ushort)500, 12));
            using (Bitmap bmp = AstroFormatDetector.RenderImageFromStream(new ForwardOnlyStream(file), 0))
                Assert.NotNull(bmp);
        }

        [Fact]
        public void ThumbnailSize_StaysNearRequest()
        {
            ushort[] px = Pixels.LinearSky(1000, 600, 500, 20);
            using (Bitmap bmp = AstroFormatDetector.RenderImageFromStream(new MemoryStream(Fits.U16(1000, 600, px)), 96))
            {
                Assert.True(bmp.Width >= 96 || bmp.Height >= 96);
                Assert.True(bmp.Width <= 200);
            }
        }

        // Same picture in both formats must come out the same way up.
        [Fact]
        public void FitsAndXisf_RenderSameOrientation()
        {
            ushort[] topDown = Enumerable.Range(0, 16).Select(i => (ushort)(i < 8 ? 60000 : 1000)).ToArray();
            ushort[] bottomUp = topDown.Skip(8).Concat(topDown.Take(8)).ToArray();
            using (Bitmap x = AstroFormatDetector.RenderImageFromStream(new MemoryStream(Xisf.U16(4, 4, 1, topDown)), 0))
            using (Bitmap f = AstroFormatDetector.RenderImageFromStream(new MemoryStream(Fits.U16(4, 4, bottomUp)), 0))
            {
                Assert.True(x.GetPixel(0, 0).R > x.GetPixel(0, 3).R);
                Assert.True(f.GetPixel(0, 0).R > f.GetPixel(0, 3).R);
            }
        }
    }

    // Corrupt input must never escape as an exception or crash the host: parsers use unsafe
    // reads in-proc in Explorer and PixInsight, where an access violation kills the host.
    public class FuzzTests
    {
        private static byte[] Mutate(byte[] seed, Random rnd, int headerLimit)
        {
            byte[] m = (byte[])seed.Clone();
            int flips = rnd.Next(1, 16);
            for (int i = 0; i < flips; i++)
                m[rnd.Next(Math.Min(m.Length, headerLimit))] = (byte)rnd.Next(256);
            if (rnd.Next(4) == 0) Array.Resize(ref m, rnd.Next(m.Length));
            return m;
        }

        private static void Run(byte[] seed, int headerLimit)
        {
            var rnd = new Random(12345);
            for (int i = 0; i < 4000; i++)
            {
                byte[] file = Mutate(seed, rnd, headerLimit);
                int target = new[] { 0, 16, 256, 2048 }[i % 4];
                using (Bitmap b = AstroFormatDetector.RenderImageFromStream(new MemoryStream(file), target)) { }
            }
        }

        // Digit mutations hit the semantic fields that random bytes rarely produce valid.
        private static void RunDigits(byte[] seed, int headerLimit)
        {
            var rnd = new Random(777);
            string digits = "0123456789-";
            for (int i = 0; i < 4000; i++)
            {
                byte[] m = (byte[])seed.Clone();
                for (int k = 0; k < 4; k++)
                {
                    int p = rnd.Next(Math.Min(m.Length, headerLimit));
                    if (m[p] >= '0' && m[p] <= '9') m[p] = (byte)digits[rnd.Next(digits.Length)];
                }
                using (Bitmap b = AstroFormatDetector.RenderImageFromStream(new MemoryStream(m), new[] { 0, 16 }[i % 2])) { }
            }
        }

        private static byte[] FitsSeed() =>
            Fits.File(16, 40, 30, 3, new byte[40 * 30 * 3 * 2], Fits.Card("BZERO", "32768"), Fits.Card("ROWORDER", "'TOP-DOWN'"));

        private static byte[] XisfSeed() => Xisf.U16(40, 30, 3, new ushort[40 * 30 * 3]);

        [Fact] public void Fits_RandomBytes() => Run(FitsSeed(), 2880);
        [Fact] public void Fits_Digits() => RunDigits(FitsSeed(), 2880);
        [Fact] public void Xisf_RandomBytes() => Run(XisfSeed(), 400);
        [Fact] public void Xisf_Digits() => RunDigits(XisfSeed(), 400);
    }

    public class ProjectConsistencyTests
    {
        private static string RepoFile(params string[] parts)
        {
            string dir = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(dir, "installer.iss"))) dir = Path.GetDirectoryName(dir);
            return Path.Combine(new[] { dir }.Concat(parts).ToArray());
        }

        [Fact]
        public void InstallerEmbeddedScript_MatchesToolsHolders()
        {
            string iss = File.ReadAllText(RepoFile("installer.iss"));
            string b64 = string.Concat(Regex.Matches(iss, @"Result := Result \+ '([A-Za-z0-9+/=]+)';").Cast<Match>().Select(m => m.Groups[1].Value));
            string embedded = Encoding.Unicode.GetString(Convert.FromBase64String(b64));
            string source = File.ReadAllText(RepoFile("tools", "holders.ps1"));
            Assert.Equal(source.Replace("\r\n", "\n"), embedded.Replace("\r\n", "\n"));
        }

        [Fact]
        public void EncodedCommand_FitsWindowsCommandLineLimit()
        {
            string iss = File.ReadAllText(RepoFile("installer.iss"));
            int len = Regex.Matches(iss, @"Result := Result \+ '([A-Za-z0-9+/=]+)';").Cast<Match>().Sum(m => m.Groups[1].Length);
            Assert.True(len < 32000, $"{len} chars");
        }

        [Theory]
        [InlineData(typeof(XisfThumbnailHandler), "CurrentThumbnailClsid")]
        [InlineData(typeof(XisfPreviewHandler), "CurrentPreviewClsid")]
        public void InstallerClsids_MatchHandlers(Type handler, string constant)
        {
            string iss = File.ReadAllText(RepoFile("installer.iss"));
            string guid = handler.GetCustomAttribute<GuidAttribute>().Value;
            Assert.Matches("(?i)" + constant + @"\s*=\s*'\{" + guid + @"\}'", iss);
        }

        [Theory]
        [InlineData(typeof(XisfThumbnailHandler))]
        [InlineData(typeof(XisfPreviewHandler))]
        public void HandlerExtensions_MatchFormats(Type handler)
        {
            Assert.Equal(4, handler.GetCustomAttributes<COMServerAssociationAttribute>().Count());
        }

        [Fact]
        public void InstallerRegistersEveryExtensionForBothHandlers()
        {
            string iss = File.ReadAllText(RepoFile("installer.iss"));
            foreach (string ext in new[] { ".fits", ".fit", ".fts", ".xisf" })
                foreach (string shellEx in new[] { "e357fccd-a995-4576-b01f-234630154e96", "8895b1c6-b41f-4c1c-a562-0d564250836f" })
                    Assert.Contains($@"Software\Classes\{ext}\ShellEx\{{{{{shellEx}}}", iss);
        }

        [Theory]
        [InlineData("README.md")]
        [InlineData("README.it.md")]
        [InlineData("README.ru.md")]
        public void ReadmeCodeFences_AreClosed(string name)
        {
            int fences = File.ReadAllLines(RepoFile(name)).Count(l => l.StartsWith("```"));
            Assert.True(fences % 2 == 0, $"{name} has {fences} code fences");
        }
    }
}
