using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using XisfExplorerPreview;
using Xunit;

namespace XisfPreview.Tests
{
    // Runs the real controls on an STA thread with a real message pump, off-screen so the
    // tests do not take over the desktop or need focus.
    internal static class Sta
    {
        public static void Run(Action body)
        {
            Exception error = null;
            var t = new Thread(() =>
            {
                try { body(); }
                catch (Exception ex) { error = ex; }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            if (!t.Join(TimeSpan.FromSeconds(90))) throw new TimeoutException("UI test hung");
            if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }

        public static void Pump(int ms = 50)
        {
            var sw = Stopwatch.StartNew();
            do { Application.DoEvents(); Thread.Sleep(5); } while (sw.ElapsedMilliseconds < ms);
        }
    }

    internal sealed class ViewerHarness : IDisposable
    {
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] state);
        [DllImport("user32.dll")] private static extern bool SetKeyboardState(byte[] state);
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        public string Dir { get; } = Path.Combine(Path.GetTempPath(), "xisf-tests-" + Guid.NewGuid().ToString("N"));
        public FastViewerForm Form { get; private set; }

        public ViewerHarness() => Directory.CreateDirectory(Dir);

        public string Add(string name, byte[] content)
        {
            string p = Path.Combine(Dir, name);
            File.WriteAllBytes(p, content);
            return p;
        }

        public void Open(string file)
        {
            Form = new FastViewerForm(file)
            {
                WindowState = FormWindowState.Normal,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-5000, -5000),
                Size = new Size(1000, 700),
                ShowInTaskbar = false
            };
            Form.Show();
            Sta.Pump();
        }

        // Keys go through the real message loop (PreProcessMessage included) as a keyboard
        // press would, addressed to whichever control has the focus.
        public void Press(Keys key)
        {
            IntPtr target = Form.ActiveControl?.Handle ?? Form.Handle;
            PostMessage(target, WM_KEYDOWN, (IntPtr)(int)key, IntPtr.Zero);
            PostMessage(target, WM_KEYUP, (IntPtr)(int)key, IntPtr.Zero);
            Sta.Pump();
        }

        // Marks the modifier as held for this thread only, so the real keyboard is not involved.
        public void PressWithModifier(Keys key, Keys modifier)
        {
            var state = new byte[256];
            GetKeyboardState(state);
            state[(int)modifier] = 0x80;
            SetKeyboardState(state);
            try { Press(key); }
            finally
            {
                state[(int)modifier] = 0;
                SetKeyboardState(state);
            }
        }

        public T Field<T>(string name) =>
            (T)typeof(FastViewerForm).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Form);

        public void Call(string method, params object[] args) =>
            typeof(FastViewerForm).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Form, args);

        public void Dispose()
        {
            if (Form != null && !Form.IsDisposed) Form.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try { Directory.Delete(Dir, true); } catch (IOException) { }
        }
    }

    public class FastViewerTests
    {
        private static byte[] Sample(ushort level) =>
            Fits.U16(64, 48, Pixels.LinearSky(64, 48, level, 40));

        [Fact]
        public void Opens_ShowsNameSizeAndPosition() => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                string a = v.Add("a.fits", Sample(500));
                v.Add("b.xisf", Xisf.U16(64, 48, 1, Pixels.LinearSky(64, 48, 500, 40)));
                v.Open(a);
                Assert.Contains("a.fits (64x48, 1 Ch)", v.Form.Text);
                Assert.Contains("[1/2]", v.Form.Text);
            }
        });

        [Theory]
        [InlineData(Keys.PageDown)]
        [InlineData(Keys.Right)]
        public void NextKey_MovesToNextFile(Keys key) => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                string a = v.Add("a.fits", Sample(500));
                v.Add("b.fit", Sample(600));
                v.Open(a);
                v.Press(key);
                Assert.Contains("b.fit", v.Form.Text);
            }
        });

        [Theory]
        [InlineData(Keys.PageUp)]
        [InlineData(Keys.Left)]
        public void PrevKey_WrapsToLastFile(Keys key) => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                string a = v.Add("a.fits", Sample(500));
                v.Add("b.fts", Sample(600));
                v.Open(a);
                v.Press(key);
                Assert.Contains("b.fts", v.Form.Text);
            }
        });

        // Same as PageUp/PageDown, which ignore modifiers too.
        [Theory]
        [InlineData(Keys.Left, Keys.ShiftKey)]
        [InlineData(Keys.Left, Keys.ControlKey)]
        [InlineData(Keys.Left, Keys.Menu)]
        [InlineData(Keys.Right, Keys.ShiftKey)]
        [InlineData(Keys.Right, Keys.ControlKey)]
        [InlineData(Keys.Right, Keys.Menu)]
        public void ArrowKey_WithModifier_StillNavigates(Keys key, Keys modifier) => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                string a = v.Add("a.fits", Sample(500));
                v.Add("b.fts", Sample(600));
                v.Open(a);
                v.PressWithModifier(key, modifier);
                Assert.Contains("b.fts", v.Form.Text);
            }
        });

        [Fact]
        public void H_TogglesHistogram() => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                v.Open(v.Add("a.fits", Sample(500)));
                var hist = v.Field<HistogramControl>("_histPanel");
                Assert.False(hist.Visible);
                v.Press(Keys.H);
                Assert.True(hist.Visible);
                v.Press(Keys.H);
                Assert.False(hist.Visible);
            }
        });

        [Fact]
        public void Escape_Closes() => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                v.Open(v.Add("a.fits", Sample(500)));
                v.Press(Keys.Escape);
                Assert.True(v.Form.IsDisposed || !v.Form.Visible);
            }
        });

        [Fact]
        public void OtherExtensions_AreNotInTheFolderList() => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                string a = v.Add("a.fits", Sample(500));
                v.Add("notes.txt", new byte[3]);
                v.Add("a.fits.bak", new byte[3]);
                v.Open(a);
                Assert.Contains("[1/1]", v.Form.Text);
            }
        });

        // "No image loaded" is a state the UI offers, and the Reset button is reachable in it.
        [Fact]
        public void ResetStretch_WithNoImage_DoesNotThrow() => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                v.Open(null);
                v.Press(Keys.H);
                try { v.Call("ResetStretch"); }
                catch (TargetInvocationException ex) { throw ex.InnerException; }
            }
        });

        [Fact]
        public void AutoStf_WithNoImage_DoesNotThrow() => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                v.Open(null);
                v.Press(Keys.Space);
                v.Press(Keys.F);
                v.Press(Keys.Right);
            }
        });

        [Fact]
        public void CorruptFile_ReportsFailure_AndDoesNotShowPreviousImage() => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                string a = v.Add("a.fits", Sample(500));
                v.Add("b.fits", new byte[100]);
                v.Open(a);
                v.Press(Keys.PageDown);
                Assert.Contains("Failed to load: b.fits", v.Form.Text);
                Assert.Null(v.Field<Bitmap>("_displayBitmap"));
            }
        });

        [Fact]
        public void CorruptFile_TitleSurvivesZoom() => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                string a = v.Add("a.fits", Sample(500));
                v.Add("b.fits", new byte[100]);
                v.Open(a);
                v.Press(Keys.PageDown);
                v.Press(Keys.F);
                Assert.DoesNotContain("b.fits (64x48", v.Form.Text);
            }
        });

        [Fact]
        public void BareFileName_KeepsFolderNavigation() => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                string a = v.Add("a.fits", Sample(500));
                v.Add("b.fits", Sample(600));
                string cwd = Environment.CurrentDirectory;
                Environment.CurrentDirectory = v.Dir;
                try
                {
                    v.Open("a.fits");
                    Assert.Contains("[1/2]", v.Form.Text);
                }
                finally { Environment.CurrentDirectory = cwd; }
            }
        });

        [Fact]
        public void LiveStretchOnTinyImage_DoesNotThrow() => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                v.Open(v.Add("a.fits", Fits.U16(1, 1, new ushort[] { 500 })));
                try { v.Call("ApplyLiveStretch"); }
                catch (TargetInvocationException ex) { throw ex.InnerException; }
            }
        });

        // The HUD reports the stretch on screen, so after "Auto stretch" the sliders must
        // describe the image actually displayed.
        [Fact]
        public void RgbAutoStretch_MatchesWhatTheSlidersShow() => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                var rnd = new Random(2);
                byte[] data = Fits.Be16(Enumerable.Range(0, 64 * 48 * 3).Select(i =>
                {
                    int plane = i / (64 * 48);
                    return (short)(new[] { 500, 1500, 4000 }[plane] + rnd.Next(-30, 30) - 32768);
                }));
                v.Open(v.Add("rgb.fits", Fits.File(16, 64, 48, 3, data, Fits.Card("BZERO", "32768"))));
                v.Press(Keys.Space);

                var hist = v.Field<HistogramControl>("_histPanel");
                var frame = v.Field<XisfRawFrame>("_currentFrame");

                // The auto stretch is unlinked: the HUD shows the R/G/B values the render uses.
                Assert.Equal("Auto (per channel)", hist.ModeLabel);
                Assert.Equal(frame.AutoShadowsPerChannel, hist.ChannelShadows);
                Assert.Equal(frame.AutoMidtonesPerChannel, hist.ChannelMidtones);
                Assert.Equal(frame.AutoHighlightsPerChannel, hist.ChannelHighlights);
                Assert.True(hist.ChannelMidtones.Distinct().Count() > 1, "channels should differ for this image");
                using (Bitmap expected = StfEngine.RenderAutoStretch(frame, 1))
                    Assert.Equal(Pixels.Rgb(expected), Pixels.Rgb(v.Field<Bitmap>("_displayBitmap")));

                // Dragging a slider switches to a linked stretch and says so.
                hist.Visible = true;
                using (var paint = new Bitmap(hist.Width, hist.Height)) // hit-test rectangles are computed while painting
                    hist.DrawToBitmap(paint, new Rectangle(0, 0, hist.Width, hist.Height));
                MethodInfo Down = typeof(HistogramControl).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo Move = typeof(HistogramControl).GetMethod("OnMouseMove", BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo Up = typeof(HistogramControl).GetMethod("OnMouseUp", BindingFlags.NonPublic | BindingFlags.Instance);
                MouseEventArgs At(int x) => new MouseEventArgs(MouseButtons.Left, 1, x, 170, 0);
                int mx = 20 + (int)(hist.Midtones * 330);

                // A click without movement changes nothing: still per channel, same bitmap.
                byte[] before = Pixels.Rgb(v.Field<Bitmap>("_displayBitmap"));
                Down.Invoke(hist, new object[] { At(mx) });
                Up.Invoke(hist, new object[] { At(mx) });
                Assert.Equal("Auto (per channel)", hist.ModeLabel);
                Assert.Equal(frame.AutoMidtonesPerChannel, hist.ChannelMidtones);
                Assert.Equal(before, Pixels.Rgb(v.Field<Bitmap>("_displayBitmap")));

                Down.Invoke(hist, new object[] { At(mx) });
                Move.Invoke(hist, new object[] { At(mx + 20) });
                Up.Invoke(hist, new object[] { At(mx + 20) });

                Assert.Equal("Linked (manual)", hist.ModeLabel);
                Assert.Null(hist.ChannelMidtones);
                using (Bitmap expected = StfEngine.RenderBitmapFromRaw(frame, hist.Shadows, hist.Midtones, hist.Highlights))
                    Assert.Equal(Pixels.Rgb(expected), Pixels.Rgb(v.Field<Bitmap>("_displayBitmap")));
            }
        });

        // The per-channel auto stretch renders each channel on its own, so the Median and MAD rows
        // must describe the channels too, and must not outlive the stretch or the image.
        [Fact]
        public void RgbAutoStretch_ShowsPerChannelMedianAndMad_UntilTheStretchOrImageChanges() => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                var rnd = new Random(2);
                byte[] data = Fits.Be16(Enumerable.Range(0, 64 * 48 * 3).Select(i =>
                {
                    int plane = i / (64 * 48);
                    return (short)(new[] { 500, 1500, 4000 }[plane] + rnd.Next(-30, 30) - 32768);
                }));
                string rgb = v.Add("a-rgb.fits", Fits.File(16, 64, 48, 3, data, Fits.Card("BZERO", "32768")));
                v.Add("b-mono.fits", Sample(500));
                v.Add("c-corrupt.fits", new byte[100]);
                v.Open(rgb);
                var hist = v.Field<HistogramControl>("_histPanel");
                var frame = v.Field<XisfRawFrame>("_currentFrame");

                // Per channel values, not the pooled ones: the red and blue levels are far from the pooled median.
                Assert.NotNull(frame.MedianPerChannel);
                Assert.Equal(frame.MedianPerChannel, hist.ChannelMedian);
                Assert.Equal(frame.MADPerChannel, hist.ChannelMAD);
                Assert.True(hist.ChannelMedian[0] < frame.Median / 2f && hist.ChannelMedian[2] > frame.Median * 2f);

                // Reset is a linked stretch: back to the pooled values.
                v.Call("ResetStretch");
                Assert.Null(hist.ChannelMedian);
                Assert.Null(hist.ChannelMAD);
                Assert.Equal(frame.Median, hist.StatMedian);

                // Auto stretch brings the per channel values back.
                v.Call("TriggerAutoStf");
                Assert.NotNull(hist.ChannelMedian);
                Assert.Equal(frame.MedianPerChannel, hist.ChannelMedian);
                Assert.Equal(frame.MADPerChannel, hist.ChannelMAD);

                // A mono image has no per channel values to keep.
                v.Press(Keys.PageDown);
                var mono = v.Field<XisfRawFrame>("_currentFrame");
                Assert.Contains("b-mono.fits", v.Form.Text);
                Assert.Null(hist.ChannelMedian);
                Assert.Null(hist.ChannelMAD);
                Assert.Equal(mono.Median, hist.StatMedian);
                Assert.Equal(mono.MAD, hist.StatMAD);

                // A failed load clears the image, so the statistics go too.
                v.Press(Keys.PageDown);
                Assert.Contains("Failed to load: c-corrupt.fits", v.Form.Text);
                Assert.Null(hist.ChannelMedian);
                Assert.Null(hist.ChannelMAD);
                Assert.Null(hist.ChannelShadows);
                Assert.Null(hist.ModeLabel);
                Assert.Equal(0f, hist.StatMedian);
                Assert.Equal(0f, hist.StatMAD);

                // Back on the RGB file the per channel values return.
                v.Press(Keys.PageDown);
                Assert.Contains("a-rgb.fits", v.Form.Text);
                Assert.NotNull(hist.ChannelMedian);
            }
        });

        // Preloader races: rapid navigation must never paint a disposed bitmap.
        [Fact]
        public void RapidNavigation_NeverPaintsDisposedBitmap() => Sta.Run(() =>
        {
            using (var v = new ViewerHarness())
            {
                string first = null;
                for (int i = 0; i < 12; i++)
                {
                    string p = v.Add($"f{i:D2}.fits", Fits.U16(512, 512, Pixels.LinearSky(512, 512, (ushort)(400 + i), 40, i)));
                    first = first ?? p;
                }
                v.Open(first);

                var rnd = new Random(1);
                for (int i = 0; i < 400; i++)
                {
                    v.Call("Navigate", rnd.Next(4) == 0 ? -1 : 1);
                    if (i % 3 == 0) Thread.Sleep(rnd.Next(0, 6));
                    // An off-screen window gets no WM_PAINT, so paint explicitly: OnPaint
                    // throws if the bitmap was disposed underneath it.
                    using (var shot = new Bitmap(v.Form.Width, v.Form.Height))
                        v.Form.DrawToBitmap(shot, new Rectangle(0, 0, shot.Width, shot.Height));
                }
            }
        });
    }

    public class HistogramControlTests
    {
        private static Bitmap Paint(Control c)
        {
            var bmp = new Bitmap(c.Width, c.Height);
            c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
            return bmp;
        }

        private static readonly Color CurveColor = Color.FromArgb(140, 190, 255);

        // A linear frame puts nearly every pixel in bin 0. The curve must stay in its chart
        // (top = 45 px) rather than draw over the title bar and buttons.
        [Fact]
        public void DarkLinearFrame_CurveStaysInsideChart() => Sta.Run(() =>
        {
            var hist = new int[256];
            hist[0] = 16000000;
            hist[1] = 3000;
            hist[2] = 40;
            using (var c = new HistogramControl())
            {
                c.SetHistogram(hist, 0.002f, 0.0003f);
                using (Bitmap bmp = Paint(c))
                {
                    int stray = 0;
                    // Stop before the title bar buttons: their text is close to the curve colour.
                    for (int y = 0; y < 44; y++)
                        for (int x = 0; x < bmp.Width - 190; x++)
                        {
                            Color p = bmp.GetPixel(x, y);
                            if (Math.Abs(p.R - CurveColor.R) < 12 && Math.Abs(p.G - CurveColor.G) < 12 && Math.Abs(p.B - CurveColor.B) < 12)
                                stray++;
                        }
                    Assert.True(stray == 0, $"{stray} curve pixels above the chart");
                }
            }
        });

        // The per channel Median and MAD rows are the widest text in the sidebar: three F5 values
        // must stay inside the panel (the area right of the text column is empty background).
        [Fact]
        public void PerChannelMedianAndMad_StayInsideThePanel() => Sta.Run(() =>
        {
            using (var c = new HistogramControl())
            {
                c.SetHistogram(new int[256], 0.5f, 0.5f);
                c.ChannelShadows = new[] { 0.05f, 0.05f, 0.05f };
                c.ChannelMidtones = new[] { 0.5f, 0.5f, 0.5f };
                c.ChannelHighlights = new[] { 1f, 1f, 1f };
                c.ChannelMedian = new[] { 0.00000f, 0.83333f, 0.88888f };
                c.ChannelMAD = new[] { 0.08888f, 0.00000f, 0.88888f };
                c.ModeLabel = "Auto (per channel)";
                using (Bitmap bmp = Paint(c))
                {
                    Color back = c.BackColor;
                    int stray = 0;
                    for (int y = 45; y < c.Height - 20; y++)
                        for (int x = c.Width - 6; x < c.Width - 2; x++)
                            if (bmp.GetPixel(x, y) != back) stray++;
                    Assert.True(stray == 0, $"{stray} text pixels at the right edge");
                }
            }
        });

        [Fact]
        public void Handles_FireEventsAndClamp() => Sta.Run(() =>
        {
            var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-5000, -5000), ShowInTaskbar = false, Size = new Size(700, 400) };
            var c = new HistogramControl();
            form.Controls.Add(c);
            form.Show();
            Paint(c).Dispose(); // hit-test rectangles are computed while painting

            int changing = 0, changed = 0;
            c.ValuesChanging += (s, e) => changing++;
            c.ValuesChanged += (s, e) => changed++;

            MethodInfo Down = typeof(HistogramControl).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo Move = typeof(HistogramControl).GetMethod("OnMouseMove", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo Up = typeof(HistogramControl).GetMethod("OnMouseUp", BindingFlags.NonPublic | BindingFlags.Instance);
            MouseEventArgs At(int x, int y) => new MouseEventArgs(MouseButtons.Left, 1, x, y, 0);

            // Midtones handle sits at 20 + 0.5 * 330 = 185; drag it far past the right edge.
            Down.Invoke(c, new object[] { At(185, 170) });
            Move.Invoke(c, new object[] { At(5000, 170) });
            Up.Invoke(c, new object[] { At(5000, 170) });

            Assert.Equal(0.9999f, c.Midtones, 4);
            Assert.Equal(1, changing);
            Assert.Equal(1, changed);
            form.Dispose();
        });

        // Grabbing the title bar above a slider must move the panel, not the slider.
        [Fact]
        public void TitleBarDrag_OverHandleColumn_DoesNotMoveSlider() => Sta.Run(() =>
        {
            var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-5000, -5000), ShowInTaskbar = false, Size = new Size(700, 400) };
            var c = new HistogramControl();
            form.Controls.Add(c);
            form.Show();
            Paint(c).Dispose(); // hit-test rectangles are computed while painting

            MethodInfo Down = typeof(HistogramControl).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo Move = typeof(HistogramControl).GetMethod("OnMouseMove", BindingFlags.NonPublic | BindingFlags.Instance);
            MouseEventArgs At(int x, int y) => new MouseEventArgs(MouseButtons.Left, 1, x, y, 0);

            Down.Invoke(c, new object[] { At(185, 10) }); // title bar, midtones column
            Move.Invoke(c, new object[] { At(300, 10) });

            Assert.Equal(0.5f, c.Midtones);
            form.Dispose();
        });

        // Drag fixture: a shown control with the midtones handle at x = 185 (0.5 of the chart).
        private sealed class DragRig : IDisposable
        {
            public readonly Form Form;
            public readonly HistogramControl C;
            public int Changing, Changed, Auto;
            private readonly MethodInfo _down = typeof(HistogramControl).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance);
            private readonly MethodInfo _move = typeof(HistogramControl).GetMethod("OnMouseMove", BindingFlags.NonPublic | BindingFlags.Instance);
            private readonly MethodInfo _up = typeof(HistogramControl).GetMethod("OnMouseUp", BindingFlags.NonPublic | BindingFlags.Instance);

            public DragRig()
            {
                Form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-5000, -5000), ShowInTaskbar = false, Size = new Size(700, 400) };
                C = new HistogramControl();
                Form.Controls.Add(C);
                Form.Show();
                Paint(C).Dispose(); // hit-test rectangles are computed while painting
                C.ValuesChanging += (s, e) => Changing++;
                C.ValuesChanged += (s, e) => Changed++;
                C.AutoStfClicked += (s, e) => Auto++;
            }

            private static MouseEventArgs At(MouseButtons b, int x, int y) => new MouseEventArgs(b, 1, x, y, 0);
            public void Down(MouseButtons b, int x, int y) => _down.Invoke(C, new object[] { At(b, x, y) });
            public void Move(MouseButtons b, int x, int y) => _move.Invoke(C, new object[] { At(b, x, y) });
            public void Up(MouseButtons b, int x, int y) => _up.Invoke(C, new object[] { At(b, x, y) });
            public void Dispose() => Form.Dispose();
        }

        // Space calls SetAutoStretch mid-drag: the next move must not edit the sliders or fire anything.
        [Fact]
        public void SetAutoStretch_MidDrag_CancelsDrag() => Sta.Run(() =>
        {
            using (var r = new DragRig())
            {
                r.Down(MouseButtons.Left, 185, 170);
                r.Move(MouseButtons.Left, 250, 170);
                int changing = r.Changing;

                r.C.SetAutoStretch(new XisfRawFrame { Channels = 1, AutoShadows = 0.1f, AutoMidtones = 0.3f, AutoHighlights = 0.9f });
                r.Move(MouseButtons.Left, 300, 170);
                r.Up(MouseButtons.Left, 300, 170);

                Assert.Equal(0.3f, r.C.Midtones);
                Assert.Equal(changing, r.Changing);
                Assert.Equal(0, r.Changed);
            }
        });

        // Right-press on the Auto button mid-drag: the drag ends, later moves do nothing.
        [Fact]
        public void RightPressOnAuto_MidDrag_CancelsDrag() => Sta.Run(() =>
        {
            using (var r = new DragRig())
            {
                r.Down(MouseButtons.Left, 185, 170);
                r.Move(MouseButtons.Left, 250, 170);
                int changing = r.Changing;
                float mid = r.C.Midtones;

                r.Down(MouseButtons.Right, 460, 10); // Auto stretch button
                r.Move(MouseButtons.Left, 300, 170);

                Assert.Equal(1, r.Auto);
                Assert.Equal(mid, r.C.Midtones);
                Assert.Equal(changing, r.Changing);
                Assert.Equal(0, r.Changed); // Auto replaces the render: no commit
            }
        });

        [Fact]
        public void Move_WithoutDragButtonHeld_DoesNotEdit() => Sta.Run(() =>
        {
            using (var r = new DragRig())
            {
                r.Down(MouseButtons.Left, 185, 170);
                r.Move(MouseButtons.None, 300, 170);
                r.Move(MouseButtons.Right, 300, 170);

                Assert.Equal(0.5f, r.C.Midtones);
                Assert.Equal(0, r.Changing);
            }
        });

        [Fact]
        public void Release_OfOtherButton_DoesNotEndDrag() => Sta.Run(() =>
        {
            using (var r = new DragRig())
            {
                r.Down(MouseButtons.Left, 185, 170);
                r.Move(MouseButtons.Left, 250, 170);
                r.Up(MouseButtons.Right, 250, 170);
                Assert.Equal(0, r.Changed);

                r.Move(MouseButtons.Left, 300, 170);
                Assert.True(r.C.Midtones > 0.7f);

                r.Up(MouseButtons.Left, 300, 170);
                Assert.Equal(1, r.Changed);
            }
        });

        // A second button pressed on empty space mid-drag must commit the moved drag once,
        // otherwise the image stays at draft quality.
        [Fact]
        public void SecondPress_OnEmptySpace_AfterMove_CommitsOnce() => Sta.Run(() =>
        {
            using (var r = new DragRig())
            {
                r.Down(MouseButtons.Left, 185, 170);
                r.Move(MouseButtons.Left, 250, 170);
                int changing = r.Changing;

                r.Down(MouseButtons.Right, 340, 120); // empty chart space
                Assert.Equal(1, r.Changed);

                r.Move(MouseButtons.Left, 300, 170);
                r.Up(MouseButtons.Left, 300, 170);
                Assert.Equal(1, r.Changed);
                Assert.Equal(changing, r.Changing);
            }
        });
    }

    public class ToolbarAndPreviewControlTests
    {
        [Fact]
        public void Toolbar_EveryButtonFiresItsEvent() => Sta.Run(() =>
        {
            var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-5000, -5000), ShowInTaskbar = false, Size = new Size(700, 200) };
            var t = new BottomToolbarControl();
            form.Controls.Add(t);
            form.Show();
            using (var shot = new Bitmap(t.Width, t.Height)) // hit-test rectangles are computed while painting
                t.DrawToBitmap(shot, new Rectangle(0, 0, t.Width, t.Height));

            var fired = new List<string>();
            t.ActualSizeClicked += (s, e) => fired.Add("1:1");
            t.FitClicked += (s, e) => fired.Add("fit");
            t.ZoomInClicked += (s, e) => fired.Add("+");
            t.ZoomOutClicked += (s, e) => fired.Add("-");
            t.PrevClicked += (s, e) => fired.Add("<");
            t.NextClicked += (s, e) => fired.Add(">");
            t.HistogramToggleClicked += (s, e) => fired.Add("hist");
            t.AutoStfClicked += (s, e) => fired.Add("stf");

            MethodInfo Down = typeof(BottomToolbarControl).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < 8; i++)
                Down.Invoke(t, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 8 + i * 40 + 18, 19, 0) });

            Assert.Equal(new[] { "1:1", "fit", "+", "-", "<", ">", "hist", "stf" }, fired);
            form.Dispose();
        });

        [Fact]
        public void Toolbar_ButtonsFitInsideControl() => Sta.Run(() =>
        {
            using (var t = new BottomToolbarControl())
                Assert.True(8 + 8 * 40 - 4 <= t.Width, "last button overflows");
        });

        [Fact]
        public void PreviewControl_PaintsImageAndMessage() => Sta.Run(() =>
        {
            using (var c = new XisfPreviewControl { Size = new Size(200, 200) })
            {
                var white = new Bitmap(10, 10);
                using (Graphics g = Graphics.FromImage(white)) g.Clear(Color.Red);
                c.SetImage(white);

                using (var shot = new Bitmap(200, 200))
                {
                    c.DrawToBitmap(shot, new Rectangle(0, 0, 200, 200));
                    Assert.Equal(Color.Red.ToArgb(), shot.GetPixel(100, 100).ToArgb());
                }

                c.ShowMessage("nope");
                using (var shot = new Bitmap(200, 200))
                {
                    c.DrawToBitmap(shot, new Rectangle(0, 0, 200, 200));
                    Assert.NotEqual(Color.Red.ToArgb(), shot.GetPixel(100, 100).ToArgb());
                }
            }
        });
    }
}
