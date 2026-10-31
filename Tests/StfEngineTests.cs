using System;
using System.Drawing;
using System.Linq;
using XisfExplorerPreview;
using Xunit;

namespace XisfPreview.Tests
{
    public class StfEngineTests
    {
        private static XisfRawFrame Frame(int w, int h, int ch, Func<int, int, float> sample)
        {
            var data = new float[w * h * ch];
            for (int i = 0; i < w * h; i++)
                for (int c = 0; c < ch; c++)
                    data[i * ch + c] = sample(i, c);
            return new XisfRawFrame { Width = w, Height = h, Channels = ch, NormalizedData = data };
        }

        private static XisfRawFrame Noisy(int w, int h, int ch, float[] level, float noise, int seed = 5)
        {
            var rnd = new Random(seed);
            return Frame(w, h, ch, (i, c) => Math.Max(0f, level[c] + (float)((rnd.NextDouble() - 0.5) * noise)));
        }

        [Theory]
        [InlineData(0.01f)]
        [InlineData(0.25f)]
        [InlineData(0.5f)]
        [InlineData(0.9f)]
        public void Mtf_FixedPoints(float m)
        {
            Assert.Equal(0f, StfEngine.MTF(m, 0f));
            Assert.Equal(1f, StfEngine.MTF(m, 1f));
            Assert.Equal(0.5f, StfEngine.MTF(m, m), 5);
        }

        [Theory]
        [InlineData(0.1f)]
        [InlineData(0.25f)]
        [InlineData(0.75f)]
        public void Mtf_IsIdentityAtMidtoneHalf(float x) =>
            Assert.Equal(x, StfEngine.MTF(0.5f, x), 5);

        // By the MTF formula, MTF(m, 0.5) = 1 - m (and MTF(m, m) = 0.5).
        [Theory]
        [InlineData(0.01f)]
        [InlineData(0.1f)]
        [InlineData(0.9f)]
        public void Mtf_AtExactlyHalf_FollowsTheFormula(float m) =>
            Assert.Equal(1f - m, StfEngine.MTF(m, 0.5f), 5);

        [Theory]
        [InlineData(0.002f)]
        [InlineData(0.05f)]
        [InlineData(0.5f)]
        [InlineData(0.95f)]
        public void Mtf_IsMonotonic(float m)
        {
            float prev = -1f;
            for (int i = 0; i <= 200000; i++)
            {
                float x = i / 200000f;
                float y = StfEngine.MTF(m, x);
                Assert.True(y >= prev - 1e-5f, $"MTF({m}, {x}) = {y} dropped below {prev}");
                prev = y;
            }
        }

        // The user-visible face of the MTF defect: mid-bright samples turn black.
        [Fact]
        public void Render_BrightPixel_IsNotBlack()
        {
            XisfRawFrame f = Frame(1, 1, 1, (i, c) => 0.5f);
            using (Bitmap bmp = StfEngine.RenderBitmapFromRaw(f, 0f, 0.01f, 1f))
                Assert.True(bmp.GetPixel(0, 0).R > 200, $"got {bmp.GetPixel(0, 0).R}");
        }

        [Fact]
        public void AutoStf_PutsMedianAtTargetBackground()
        {
            XisfRawFrame f = Noisy(256, 256, 1, new[] { 0.01f }, 0.002f);
            StfEngine.ComputeAutoStf(f, false);

            Assert.False(f.AutoStfSkipped);
            float x = (f.Median - f.AutoShadows) / (1f - f.AutoShadows);
            Assert.Equal(0.25f, StfEngine.MTF(f.AutoMidtones, x), 2);
            Assert.True(f.AutoShadows > 0f && f.AutoShadows < f.Median);
        }

        [Fact]
        public void AutoStf_StretchRevealsFaintLinearData()
        {
            XisfRawFrame f = Noisy(256, 256, 1, new[] { 0.002f }, 0.0005f);
            StfEngine.ComputeAutoStf(f, false);
            using (Bitmap bmp = StfEngine.RenderAutoStretch(f))
            {
                double mean = Pixels.Rgb(bmp).Average(b => (double)b);
                Assert.InRange(mean, 40, 90); // target background 0.25 = 64/255
            }
        }

        // The banding fix (v1.05): faint data must keep many distinct output levels.
        [Fact]
        public void AutoStf_DoesNotPosterizeFaintData()
        {
            var rnd = new Random(9);
            XisfRawFrame f = Frame(512, 512, 1, (i, c) => 0.001f + (float)rnd.NextDouble() * 0.001f);
            StfEngine.ComputeAutoStf(f, false);
            using (Bitmap bmp = StfEngine.RenderAutoStretch(f))
                Assert.True(Pixels.Rgb(bmp).Distinct().Count() > 20); // the old 4096-entry table gave about 5
        }

        [Fact]
        public void AutoStf_SkippedWhenAlreadyBright()
        {
            XisfRawFrame f = Noisy(64, 64, 1, new[] { 0.4f }, 0.01f);
            StfEngine.ComputeAutoStf(f, false);
            Assert.True(f.AutoStfSkipped);
            Assert.Equal(0f, f.AutoShadows);
            Assert.Equal(0.5f, f.AutoMidtones);
        }

        [Fact]
        public void AutoStf_SkippedForPlanetaryDiscOnBlack()
        {
            XisfRawFrame f = Frame(100, 100, 1, (i, c) => i < 1500 ? 0.8f : 0f);
            StfEngine.ComputeAutoStf(f, false);
            Assert.True(f.AutoStfSkipped);
        }

        // 80% of the pixels at the median and 20% at p99, so both statistics are exact.
        [Theory]
        [InlineData(0.05f, 0.30f, false)] // faint: always stretched
        [InlineData(0.15f, 0.20f, false)] // high background with a narrow spread: still linear
        [InlineData(0.20f, 0.25f, false)]
        [InlineData(0.15f, 0.30f, true)]  // wide spread above the background: already finished
        [InlineData(0.36f, 0.40f, true)]  // bright overall, whatever the spread
        public void AutoStf_SkipDependsOnMedianAndSpread(float median, float p99, bool skipped)
        {
            XisfRawFrame f = Frame(100, 100, 1, (i, c) => i < 2000 ? p99 : median);
            StfEngine.ComputeAutoStf(f, false);
            Assert.Equal(skipped, f.AutoStfSkipped);
            if (!skipped)
                Assert.True(f.AutoMidtones < 0.5f);
        }

        // The case behind the near-blank renders: unstretched data on a high background.
        [Fact]
        public void AutoStf_HighLinearBackground_RevealsFaintSignal()
        {
            XisfRawFrame f = Frame(100, 100, 1, (i, c) => i % 2 == 0 ? 0.15f : 0.152f);
            StfEngine.ComputeAutoStf(f, false);
            using (Bitmap bmp = StfEngine.RenderAutoStretch(f))
                Assert.True(Math.Abs(bmp.GetPixel(0, 0).R - bmp.GetPixel(1, 0).R) >= 10); // unstretched the two differ by 1
        }

        [Fact]
        public void Rgb_HighLinearBackground_GetsPerChannelStretch()
        {
            XisfRawFrame f = Noisy(128, 128, 3, new[] { 0.15f, 0.16f, 0.17f }, 0.01f);
            StfEngine.ComputeAutoStf(f, false);
            Assert.False(f.AutoStfSkipped);
            Assert.All(f.AutoShadowsPerChannel, s => Assert.True(s > 0f));
        }

        // A channel well above the others must not be left out of the per-channel stretch.
        [Fact]
        public void Rgb_BrightChannel_IsStretchedLikeTheOthers()
        {
            XisfRawFrame f = Noisy(128, 128, 3, new[] { 0.02f, 0.03f, 0.15f }, 0.01f);
            StfEngine.ComputeAutoStf(f, false);
            Assert.False(f.AutoStfSkipped);
            Assert.True(f.AutoShadowsPerChannel[2] > 0.1f);
        }

        // Mostly-empty frame (median 0, e.g. sparse speckle on black): the midtone is solved on
        // the non-zero samples, so the speckle is brightened instead of left near black.
        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void AutoStf_MedianZero_BrightensNonZeroSamples(int channels)
        {
            var rnd = new Random(3);
            XisfRawFrame f = Frame(128, 128, channels, (i, c) => i % 20 == 0 ? 0.002f + (float)rnd.NextDouble() * 0.002f : 0f);
            StfEngine.ComputeAutoStf(f, false);

            Assert.False(f.AutoStfSkipped);
            Assert.True(f.AutoMidtones < 0.5f);
            if (channels == 3)
                Assert.All(f.AutoMidtonesPerChannel, m => Assert.True(m < 0.5f));
            using (Bitmap bmp = StfEngine.RenderAutoStretch(f))
                Assert.True(Pixels.Rgb(bmp).Max() > 40); // without the fallback the 0.002..0.004 speckle renders near black
        }

        // A non-zero median at or above the target background never gets darker.
        [Fact]
        public void AutoStf_MedianZero_NonZeroMedianAboveTarget_KeepsDefaultMidtone()
        {
            XisfRawFrame f = Frame(100, 100, 1, (i, c) => i % 20 == 0 ? 0.3f : 0f);
            StfEngine.ComputeAutoStf(f, false);
            Assert.Equal(0.5f, f.AutoMidtones);
        }

        [Fact]
        public void AutoStf_ThumbnailIsIdentity()
        {
            XisfRawFrame f = Noisy(64, 64, 3, new[] { 0.01f, 0.01f, 0.01f }, 0.001f);
            StfEngine.ComputeAutoStf(f, true);
            Assert.Equal(0.5f, f.AutoMidtones);
            Assert.Null(f.AutoShadowsPerChannel);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(1f)]
        public void AutoStf_ConstantFrame_DoesNotThrow(float v)
        {
            XisfRawFrame f = Frame(8, 8, 1, (i, c) => v);
            StfEngine.ComputeAutoStf(f, false);
            using (StfEngine.RenderAutoStretch(f)) { }
        }

        [Fact]
        public void AutoStf_OnePixel_DoesNotThrow()
        {
            XisfRawFrame f = Frame(1, 1, 1, (i, c) => 0.01f);
            StfEngine.ComputeAutoStf(f, false);
            using (StfEngine.RenderAutoStretch(f)) { }
        }

        [Fact]
        public void Rgb_GetsPerChannelStf_MonoDoesNot()
        {
            XisfRawFrame rgb = Noisy(64, 64, 3, new[] { 0.01f, 0.02f, 0.04f }, 0.001f);
            XisfRawFrame mono = Noisy(64, 64, 1, new[] { 0.01f }, 0.001f);
            StfEngine.ComputeAutoStf(rgb, false);
            StfEngine.ComputeAutoStf(mono, false);

            Assert.Equal(3, rgb.AutoMidtonesPerChannel.Length);
            Assert.True(rgb.AutoShadowsPerChannel[0] < rgb.AutoShadowsPerChannel[2]);
            Assert.Null(mono.AutoMidtonesPerChannel);
        }

        [Fact]
        public void Rgb_UnlinkedAutoStretch_NeutralizesChannelOffset()
        {
            XisfRawFrame f = Noisy(128, 128, 3, new[] { 0.01f, 0.02f, 0.04f }, 0.002f);
            StfEngine.ComputeAutoStf(f, false);
            using (Bitmap bmp = StfEngine.RenderAutoStretch(f))
            {
                Color c = bmp.GetPixel(60, 60);
                Assert.InRange(Math.Abs(c.R - c.B), 0, 60);
            }
        }

        [Fact]
        public void Bitmap_ChannelsMapToRgbNotBgr()
        {
            XisfRawFrame f = Frame(1, 1, 3, (i, c) => c == 0 ? 1f : 0f);
            using (Bitmap bmp = StfEngine.RenderBitmapFromRaw(f, 0f, 0.5f, 1f))
            {
                Assert.Equal(255, bmp.GetPixel(0, 0).R);
                Assert.Equal(0, bmp.GetPixel(0, 0).B);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void AnyChannelCount_Renders(int ch)
        {
            XisfRawFrame f = Noisy(32, 32, ch, Enumerable.Repeat(0.02f, ch).ToArray(), 0.002f);
            StfEngine.ComputeAutoStf(f, false);
            using (Bitmap bmp = StfEngine.RenderAutoStretch(f))
                Assert.Equal(32, bmp.Width);
        }

        [Fact]
        public void RenderStep_DividesOutputSize()
        {
            XisfRawFrame f = Frame(100, 40, 1, (i, c) => 0.5f);
            using (Bitmap bmp = StfEngine.RenderBitmapFromRaw(f, 0f, 0.5f, 1f, 4))
            {
                Assert.Equal(25, bmp.Width);
                Assert.Equal(10, bmp.Height);
            }
        }

        // FastViewerForm.ApplyLiveStretch uses step 2 on any image up to 2000 px.
        [Fact]
        public void RenderStep_LargerThanImage_DoesNotThrow()
        {
            XisfRawFrame f = Frame(1, 1, 1, (i, c) => 0.5f);
            using (StfEngine.RenderBitmapFromRaw(f, 0f, 0.5f, 1f, 2)) { }
        }

        [Fact]
        public void Clipping_MapsToEndpoints()
        {
            XisfRawFrame f = Frame(3, 1, 1, (i, c) => new[] { 0.1f, 0.5f, 0.9f }[i]);
            using (Bitmap bmp = StfEngine.RenderBitmapFromRaw(f, 0.2f, 0.5f, 0.8f))
            {
                Assert.Equal(0, bmp.GetPixel(0, 0).R);
                Assert.Equal(255, bmp.GetPixel(2, 0).R);
            }
        }

        [Fact]
        public void NanSample_RendersBlack()
        {
            XisfRawFrame f = Frame(1, 1, 1, (i, c) => float.NaN);
            using (Bitmap bmp = StfEngine.RenderBitmapFromRaw(f, 0f, 0.5f, 1f))
                Assert.Equal(0, bmp.GetPixel(0, 0).R);
        }
    }
}
