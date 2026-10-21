using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;

namespace XisfExplorerPreview
{
    public static class StfEngine
    {
        // Always computes the linked (pooled-channel) STF, since it's used as the fallback
        // for mono frames and for FastViewerForm's manual/"reset to auto" editing. RGB frames
        // additionally get an unlinked per-channel STF, used only for the automatic initial
        // render (thumbnail/preview/first FastViewer load) — see RenderAutoStretch.
        public static void ComputeAutoStf(XisfRawFrame frame, bool isThumbnail)
        {
            ComputeLinkedAutoStf(frame, isThumbnail);

            int activeChannels = Math.Min(frame.Channels, 3);
            if (!isThumbnail && activeChannels >= 3 && !frame.AutoStfSkipped)
            {
                ComputeUnlinkedAutoStf(frame, activeChannels);
            }
        }

        // Independent per-channel median/MAD/MTF-solve — same math as the linked version,
        // just scoped to one channel's own samples instead of pooling across channels.
        private static void ComputeUnlinkedAutoStf(XisfRawFrame frame, int activeChannels)
        {
            int totalPixels = frame.Width * frame.Height;
            const int maxSamples = 65536;
            int step = Math.Max(1, totalPixels / maxSamples);
            int count = (totalPixels + step - 1) / step;

            float[] shadows = new float[activeChannels];
            float[] midtones = new float[activeChannels];
            float[] highlights = new float[activeChannels];

            float[] samples = new float[count];

            for (int c = 0; c < activeChannels; c++)
            {
                int idx = 0;
                for (int i = 0; i < totalPixels && idx < samples.Length; i += step)
                {
                    samples[idx++] = frame.NormalizedData[(i * frame.Channels) + c];
                }

                Array.Sort(samples, 0, idx);
                float median = idx > 0 ? samples[idx / 2] : 0.0f;

                if (median >= 0.1f) // Already bright enough; skip stretch
                {
                    shadows[c] = 0.0f;
                    midtones[c] = 0.5f;
                    highlights[c] = 1.0f;
                    continue;
                }

                float[] absDev = new float[idx];
                for (int i = 0; i < idx; i++)
                    absDev[i] = Math.Abs(samples[i] - median);
                Array.Sort(absDev);
                float mad = idx > 0 ? absDev[idx / 2] : 0.0f;

                float nmad = 1.4826f * mad;
                const float B = 0.25f;
                const float C = -2.8f;

                float c0 = 0.0f;
                if (nmad > 0.000001f)
                {
                    c0 = median + (C * nmad);
                    if (c0 < 0.0f) c0 = 0.0f;
                }

                float x = median - c0;
                float m = 0.5f;
                if (x > 0.000001f && x < 0.999999f)
                {
                    float num = x * (1.0f - B);
                    float den = (x * (1.0f - 2.0f * B)) + B;
                    if (Math.Abs(den) > 1e-7f) m = num / den;
                    m = Math.Max(0.0001f, Math.Min(0.9999f, m));
                }

                shadows[c] = c0;
                midtones[c] = m;
                highlights[c] = 1.0f;
            }

            frame.AutoShadowsPerChannel = shadows;
            frame.AutoMidtonesPerChannel = midtones;
            frame.AutoHighlightsPerChannel = highlights;
        }

        public static void ComputeLinkedAutoStf(XisfRawFrame frame, bool isThumbnail)
        {
            if (isThumbnail)
            {
                frame.AutoShadows = 0.0f;
                frame.AutoMidtones = 0.5f;
                frame.AutoHighlights = 1.0f;
                return;
            }

            int totalPixels = frame.Width * frame.Height;
            const int maxSamples = 65536;
            int step = Math.Max(1, totalPixels / maxSamples);
            int count = (totalPixels + step - 1) / step;
            int activeChannels = Math.Min(frame.Channels, 3);

            // Pool samples across all channels for linked statistics
            float[] pooledSamples = new float[count * activeChannels];
            int idx = 0;

            for (int i = 0; i < totalPixels && idx < pooledSamples.Length; i += step)
            {
                for (int c = 0; c < activeChannels; c++)
                {
                    pooledSamples[idx++] = frame.NormalizedData[(i * frame.Channels) + c];
                }
            }

            Array.Sort(pooledSamples, 0, idx);
            float median = pooledSamples[idx / 2];
            frame.Median = median;

            // Already finished: either bright overall, or a large share of pixels is bright
            // (e.g. a planetary disc on a black sky, where the median alone is ~0).
            float p99 = pooledSamples[Math.Min(idx - 1, (int)(idx * 0.99f))];
            if (median >= 0.1f || p99 >= 0.5f)
            {
                frame.AutoStfSkipped = true;
                frame.AutoShadows = 0.0f;
                frame.AutoMidtones = 0.5f;
                frame.AutoHighlights = 1.0f;
                return;
            }

            float[] absDev = new float[idx];
            for (int i = 0; i < idx; i++)
                absDev[i] = Math.Abs(pooledSamples[i] - median);

            Array.Sort(absDev);
            float mad = absDev[idx / 2];
            frame.MAD = mad;

            float nmad = 1.4826f * mad;

            const float B = 0.25f; // PixInsight Target Background
            const float C = -2.8f; // PixInsight Shadows Clipping

            float c0 = 0.0f;
            if (nmad > 0.000001f)
            {
                c0 = median + (C * nmad);
                if (c0 < 0.0f) c0 = 0.0f;
            }

            float c1 = 1.0f;

            // Solve MTF(m, median - c0) = B
            float x = median - c0;
            float m = 0.5f;

            if (x > 0.000001f && x < 0.999999f)
            {
                float num = x * (1.0f - B);
                float den = (x * (1.0f - 2.0f * B)) + B;
                if (Math.Abs(den) > 1e-7f)
                {
                    m = num / den;
                }
                m = Math.Max(0.0001f, Math.Min(0.9999f, m));
            }

            frame.AutoShadows = c0;
            frame.AutoMidtones = m;
            frame.AutoHighlights = c1;
        }

        public static float MTF(float m, float x)
        {
            if (x <= 0.0f) return 0.0f;
            if (x >= 1.0f) return 1.0f;
            if (Math.Abs(x - 0.5f) < 1e-6f) return m;

            float den = ((2.0f * m - 1.0f) * x) - m;
            if (Math.Abs(den) < 1e-8f) return 0.0f;

            float val = ((m - 1.0f) * x) / den;
            return Math.Max(0.0f, Math.Min(1.0f, val));
        }

        // Automatic-stretch entry point: uses the unlinked per-channel STF for RGB frames
        // when available, otherwise falls back to the linked (or mono) triple. Manual
        // editing (histogram dragging, reset) always goes through RenderBitmapFromRaw
        // directly with an explicit linked triple, unaffected by this.
        public static unsafe Bitmap RenderAutoStretch(XisfRawFrame frame, int renderStep = 1)
        {
            int activeChannels = Math.Min(frame.Channels, 3);
            if (activeChannels >= 3 &&
                frame.AutoShadowsPerChannel != null && frame.AutoShadowsPerChannel.Length >= activeChannels &&
                frame.AutoMidtonesPerChannel != null && frame.AutoHighlightsPerChannel != null)
            {
                return RenderBitmapFromRawUnlinked(frame, frame.AutoShadowsPerChannel, frame.AutoMidtonesPerChannel, frame.AutoHighlightsPerChannel, renderStep);
            }

            return RenderBitmapFromRaw(frame, frame.AutoShadows, frame.AutoMidtones, frame.AutoHighlights, renderStep);
        }

        private static unsafe Bitmap RenderBitmapFromRawUnlinked(XisfRawFrame frame, float[] shadows, float[] midtones, float[] highlights, int renderStep)
        {
            StfParams stfR = new StfParams(shadows[0], midtones[0], highlights[0]);
            StfParams stfG = new StfParams(shadows[1], midtones[1], highlights[1]);
            StfParams stfB = new StfParams(shadows[2], midtones[2], highlights[2]);

            int outWidth = frame.Width / renderStep;
            int outHeight = frame.Height / renderStep;

            Bitmap bmp = new Bitmap(outWidth, outHeight, PixelFormat.Format24bppRgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, outWidth, outHeight), ImageLockMode.WriteOnly, bmp.PixelFormat);

            IntPtr scan0Ptr = bmpData.Scan0;
            int stride = bmpData.Stride;
            int channels = frame.Channels;
            int srcWidth = frame.Width;
            float[] normData = frame.NormalizedData;

            fixed (float* pNorm = normData)
            {
                IntPtr pNormPtr = (IntPtr)pNorm;

                Parallel.For(0, outHeight, y =>
                {
                    float* localNorm = (float*)pNormPtr.ToPointer();
                    byte* row = (byte*)scan0Ptr.ToPointer() + (y * stride);
                    int srcY = y * renderStep;
                    int srcRowOffset = srcY * srcWidth;

                    for (int x = 0; x < outWidth; x++)
                    {
                        int srcX = x * renderStep;
                        int srcIdx = (srcRowOffset + srcX) * channels;

                        byte r = ApplyStf(localNorm[srcIdx], stfR);
                        byte g = ApplyStf(localNorm[srcIdx + 1], stfG);
                        byte b = ApplyStf(localNorm[srcIdx + 2], stfB);

                        int bIdx = x * 3;
                        row[bIdx] = b;
                        row[bIdx + 1] = g;
                        row[bIdx + 2] = r;
                    }
                });
            }

            bmp.UnlockBits(bmpData);
            return bmp;
        }

        public static unsafe Bitmap RenderBitmapFromRaw(XisfRawFrame frame, float shadows, float midtones, float highlights, int renderStep = 1)
        {
            StfParams stf = new StfParams(shadows, midtones, highlights);

            int outWidth = frame.Width / renderStep;
            int outHeight = frame.Height / renderStep;

            Bitmap bmp = new Bitmap(outWidth, outHeight, PixelFormat.Format24bppRgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, outWidth, outHeight), ImageLockMode.WriteOnly, bmp.PixelFormat);

            IntPtr scan0Ptr = bmpData.Scan0;
            int stride = bmpData.Stride;
            int channels = frame.Channels;
            int srcWidth = frame.Width;
            float[] normData = frame.NormalizedData;

            fixed (float* pNorm = normData)
            {
                IntPtr pNormPtr = (IntPtr)pNorm;

                Parallel.For(0, outHeight, y =>
                {
                    float* localNorm = (float*)pNormPtr.ToPointer();
                    byte* row = (byte*)scan0Ptr.ToPointer() + (y * stride);
                    int srcY = y * renderStep;
                    int srcRowOffset = srcY * srcWidth;

                    for (int x = 0; x < outWidth; x++)
                    {
                        int srcX = x * renderStep;
                        int srcIdx = (srcRowOffset + srcX) * channels;

                        float rNorm = localNorm[srcIdx];
                        float gNorm = channels > 1 ? localNorm[srcIdx + 1] : rNorm;
                        float bNorm = channels > 2 ? localNorm[srcIdx + 2] : (channels > 1 ? gNorm : rNorm);

                        byte r = ApplyStf(rNorm, stf);
                        byte g = ApplyStf(gNorm, stf);
                        byte b = ApplyStf(bNorm, stf);

                        int bIdx = x * 3;
                        row[bIdx] = b;
                        row[bIdx + 1] = g;
                        row[bIdx + 2] = r;
                    }
                });
            }

            bmp.UnlockBits(bmpData);
            return bmp;
        }

        private struct StfParams
        {
            public readonly float C0, Range, C1, M;

            public StfParams(float c0, float m, float c1)
            {
                C0 = c0;
                C1 = c1;
                M = m;
                Range = Math.Max(0.00001f, c1 - c0);
            }
        }

        // Evaluated per pixel instead of through a lookup table: linear astro data keeps
        // nearly all its signal in the lowest percent of the 0..1 range, where even a
        // 4096-entry table leaves only a handful of distinct inputs (visible as banding).
        private static byte ApplyStf(float x, StfParams p)
        {
            if (!(x > p.C0)) return 0; // also catches NaN
            if (x >= p.C1) return 255;

            float stretched = MTF(p.M, (x - p.C0) / p.Range);
            return (byte)(stretched * 255.0f + 0.5f);
        }
    }
}