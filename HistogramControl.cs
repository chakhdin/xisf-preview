using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XisfExplorerPreview
{
    public class HistogramControl : Control
    {
        private int[] _histogram = new int[256];
        private int _maxVal = 1;

        public float Shadows { get; set; } = 0.0f;
        public float Midtones { get; set; } = 0.5f;
        public float Highlights { get; set; } = 1.0f;

        public float StatMedian { get; set; }
        public float StatMAD { get; set; }

        public event EventHandler ValuesChanging; // Fired on active drag (live feedback)
        public event EventHandler ValuesChanged;  // Fired on mouse release (full-quality commit)
        public event EventHandler AutoStfClicked;
        public event EventHandler ResetClicked;
        public event EventHandler CloseClicked;

        private enum DragMode { None, Window, Shadows, Midtones, Highlights }
        private DragMode _activeDrag = DragMode.None;
        private Point _dragStartMouse;
        private Point _dragStartLocation;

        private Rectangle _chartArea;
        private Rectangle _btnAutoStf;
        private Rectangle _btnReset;
        private Rectangle _btnClose;
        private Rectangle _titleBar;

        public HistogramControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(24, 26, 31);
            ForeColor = Color.White;
            Size = new Size(540, 220);
        }

        public void SetHistogram(int[] hist, float med, float mad)
        {
            if (hist != null)
            {
                Array.Copy(hist, _histogram, 256);
                _maxVal = 1;
                for (int i = 1; i < 255; i++)
                {
                    if (_histogram[i] > _maxVal) _maxVal = _histogram[i];
                }
            }
            StatMedian = med;
            StatMAD = mad;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Outer border
            using (Pen borderPen = new Pen(Color.FromArgb(60, 65, 75), 1))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }

            // Title bar
            _titleBar = new Rectangle(0, 0, Width, 30);
            g.FillRectangle(new SolidBrush(Color.FromArgb(32, 34, 40)), _titleBar);
            TextRenderer.DrawText(g, "Histogram & Stretch", new Font("Segoe UI", 9.5f, FontStyle.Bold), new Point(10, 6), Color.Gainsboro);

            _btnAutoStf = new Rectangle(Width - 190, 4, 75, 22);
            _btnReset = new Rectangle(Width - 110, 4, 65, 22);
            _btnClose = new Rectangle(Width - 32, 5, 20, 20);

            DrawButton(g, _btnAutoStf, "Auto stretch", Color.FromArgb(0, 122, 204));
            DrawButton(g, _btnReset, "Reset stretch", Color.FromArgb(70, 75, 85));

            // Close button (X)
            using (SolidBrush closeBrush = new SolidBrush(Color.FromArgb(210, 50, 45)))
            {
                g.FillEllipse(closeBrush, _btnClose);
            }
            using (Pen xPen = new Pen(Color.White, 1.8f))
            {
                g.DrawLine(xPen, _btnClose.Left + 5, _btnClose.Top + 5, _btnClose.Right - 5, _btnClose.Bottom - 5);
                g.DrawLine(xPen, _btnClose.Right - 5, _btnClose.Top + 5, _btnClose.Left + 5, _btnClose.Bottom - 5);
            }

            _chartArea = new Rectangle(20, 45, 330, 130);

            // Chart area
            g.FillRectangle(new SolidBrush(Color.FromArgb(15, 16, 18)), _chartArea);
            using (Pen gridPen = new Pen(Color.FromArgb(35, 38, 45)))
            {
                for (int i = 1; i < 4; i++)
                {
                    int x = _chartArea.Left + (_chartArea.Width * i / 4);
                    g.DrawLine(gridPen, x, _chartArea.Top, x, _chartArea.Bottom);
                }
            }

            // Histogram Curve
            if (_maxVal > 0)
            {
                PointF[] points = new PointF[256];
                double logMax = Math.Log(1 + _maxVal);

                for (int i = 0; i < 256; i++)
                {
                    float px = _chartArea.Left + (i / 255.0f * _chartArea.Width);
                    double logVal = Math.Log(1 + _histogram[i]);
                    float normHeight = (float)(logVal / logMax);
                    float py = _chartArea.Bottom - (normHeight * (_chartArea.Height - 5));
                    points[i] = new PointF(px, py);
                }

                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddLines(points);
                    path.AddLine(new PointF(_chartArea.Right, _chartArea.Bottom), new PointF(_chartArea.Left, _chartArea.Bottom));
                    path.CloseFigure();

                    using (SolidBrush fill = new SolidBrush(Color.FromArgb(80, 100, 160, 220)))
                    {
                        g.FillPath(fill, path);
                    }
                    using (Pen curvePen = new Pen(Color.FromArgb(140, 190, 255), 1.5f))
                    {
                        g.DrawLines(curvePen, points);
                    }
                }
            }

            // Slider Handles
            DrawHandle(g, _chartArea.Left + (Shadows * _chartArea.Width), _chartArea.Bottom, Color.DarkGray);
            DrawHandle(g, _chartArea.Left + (Midtones * _chartArea.Width), _chartArea.Bottom, Color.FromArgb(0, 175, 240));
            DrawHandle(g, _chartArea.Left + (Highlights * _chartArea.Width), _chartArea.Bottom, Color.White);

            // Metrics Sidebar
            int statX = 370;
            int statY = 50;
            Font statFont = new Font("Segoe UI", 8.5f);
            DrawStatRow(g, "Shadows:", $"{Shadows:F4}", statX, ref statY, statFont);
            DrawStatRow(g, "Midtones:", $"{Midtones:F4}", statX, ref statY, statFont);
            DrawStatRow(g, "Highlights:", $"{Highlights:F4}", statX, ref statY, statFont);
            statY += 10;
            DrawStatRow(g, "Median:", $"{StatMedian:F5}", statX, ref statY, statFont);
            DrawStatRow(g, "MAD:", $"{StatMAD:F5}", statX, ref statY, statFont);
        }

        private void DrawButton(Graphics g, Rectangle r, string text, Color bg)
        {
            g.FillRectangle(new SolidBrush(bg), r);
            TextRenderer.DrawText(g, text, new Font("Segoe UI", 7.5f, FontStyle.Bold), r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void DrawHandle(Graphics g, float x, float y, Color col)
        {
            PointF[] tri = {
                new PointF(x, y - 10),
                new PointF(x - 5, y),
                new PointF(x + 5, y)
            };
            g.FillPolygon(new SolidBrush(col), tri);
            g.DrawPolygon(Pens.Black, tri);
        }

        private void DrawStatRow(Graphics g, string label, string val, int x, ref int y, Font f)
        {
            TextRenderer.DrawText(g, label, f, new Point(x, y), Color.DarkGray);
            TextRenderer.DrawText(g, val, f, new Point(x + 75, y), Color.Gainsboro);
            y += 18;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_btnClose.Contains(e.Location))
            {
                CloseClicked?.Invoke(this, EventArgs.Empty);
                return;
            }
            if (_btnAutoStf.Contains(e.Location))
            {
                AutoStfClicked?.Invoke(this, EventArgs.Empty);
                return;
            }
            if (_btnReset.Contains(e.Location))
            {
                ResetClicked?.Invoke(this, EventArgs.Empty);
                return;
            }

            int sx = _chartArea.Left + (int)(Shadows * _chartArea.Width);
            int mx = _chartArea.Left + (int)(Midtones * _chartArea.Width);
            int hx = _chartArea.Left + (int)(Highlights * _chartArea.Width);

            if (Math.Abs(e.X - mx) < 8) _activeDrag = DragMode.Midtones;
            else if (Math.Abs(e.X - sx) < 8) _activeDrag = DragMode.Shadows;
            else if (Math.Abs(e.X - hx) < 8) _activeDrag = DragMode.Highlights;
            else if (_titleBar.Contains(e.Location))
            {
                _activeDrag = DragMode.Window;
                _dragStartMouse = Cursor.Position;
                _dragStartLocation = Location;
            }
            else _activeDrag = DragMode.None;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_activeDrag == DragMode.Window)
            {
                Point cur = Cursor.Position;
                Location = new Point(
                    _dragStartLocation.X + (cur.X - _dragStartMouse.X),
                    _dragStartLocation.Y + (cur.Y - _dragStartMouse.Y)
                );
                return;
            }

            if (_activeDrag == DragMode.None || _chartArea.Width <= 0) return;

            float norm = (float)(e.X - _chartArea.Left) / _chartArea.Width;
            norm = Math.Max(0.0f, Math.Min(1.0f, norm));

            if (_activeDrag == DragMode.Shadows)
            {
                Shadows = Math.Min(norm, Highlights - 0.001f);
            }
            else if (_activeDrag == DragMode.Highlights)
            {
                Highlights = Math.Max(norm, Shadows + 0.001f);
            }
            else if (_activeDrag == DragMode.Midtones)
            {
                Midtones = Math.Max(0.0001f, Math.Min(0.9999f, norm));
            }

            Invalidate();
            ValuesChanging?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_activeDrag != DragMode.None && _activeDrag != DragMode.Window)
            {
                ValuesChanged?.Invoke(this, EventArgs.Empty);
            }
            _activeDrag = DragMode.None;
        }
    }
}