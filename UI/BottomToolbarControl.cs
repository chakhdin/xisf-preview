using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XisfExplorerPreview
{
    public class BottomToolbarControl : Control
    {
        public event EventHandler ActualSizeClicked;
        public event EventHandler FitClicked;
        public event EventHandler ZoomInClicked;
        public event EventHandler ZoomOutClicked;
        public event EventHandler PrevClicked;
        public event EventHandler NextClicked;
        public event EventHandler HistogramToggleClicked;
        public event EventHandler AutoStfClicked;

        private Rectangle _btnActualSize;
        private Rectangle _btnFit;
        private Rectangle _btnZoomIn;
        private Rectangle _btnZoomOut;
        private Rectangle _btnPrev;
        private Rectangle _btnNext;
        private Rectangle _btnHist;
        private Rectangle _btnAutoStf;

        private int _hoveredIndex = -1;

        public BottomToolbarControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(24, 26, 31);
            Size = new Size(340, 38);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Toolbar rounded capsule outline
            using (GraphicsPath path = GetRoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), 6))
            {
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(28, 30, 36)))
                    g.FillPath(bg, path);
                using (Pen border = new Pen(Color.FromArgb(60, 65, 75), 1))
                    g.DrawPath(border, path);
            }

            int btnWidth = 36;
            int x = 8;
            int y = 5;

            _btnActualSize = new Rectangle(x, y, btnWidth, 28); x += btnWidth + 4;
            _btnFit = new Rectangle(x, y, btnWidth, 28); x += btnWidth + 4;
            _btnZoomIn = new Rectangle(x, y, btnWidth, 28); x += btnWidth + 4;
            _btnZoomOut = new Rectangle(x, y, btnWidth, 28); x += btnWidth + 4;
            _btnPrev = new Rectangle(x, y, btnWidth, 28); x += btnWidth + 4;
            _btnNext = new Rectangle(x, y, btnWidth, 28); x += btnWidth + 4;
            _btnHist = new Rectangle(x, y, btnWidth, 28); x += btnWidth + 4;
            _btnAutoStf = new Rectangle(x, y, btnWidth, 28);

            DrawButton(g, _btnActualSize, "1:1", 0);
            DrawButton(g, _btnFit, "[ ]", 1);
            DrawButton(g, _btnZoomIn, "+", 2);
            DrawButton(g, _btnZoomOut, "-", 3);
            DrawButton(g, _btnPrev, "<", 4);
            DrawButton(g, _btnNext, ">", 5);
            DrawButton(g, _btnHist, "HIST", 6);
            DrawButton(g, _btnAutoStf, "STF", 7);
        }

        private void DrawButton(Graphics g, Rectangle r, string text, int index)
        {
            if (_hoveredIndex == index)
            {
                using (SolidBrush hover = new SolidBrush(Color.FromArgb(50, 55, 68)))
                    g.FillRectangle(hover, r);
            }

            using (Font f = new Font("Segoe UI", index == 6 || index == 7 ? 7.5f : 9f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, text, f, r, Color.Gainsboro, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int oldHover = _hoveredIndex;
            if (_btnActualSize.Contains(e.Location)) _hoveredIndex = 0;
            else if (_btnFit.Contains(e.Location)) _hoveredIndex = 1;
            else if (_btnZoomIn.Contains(e.Location)) _hoveredIndex = 2;
            else if (_btnZoomOut.Contains(e.Location)) _hoveredIndex = 3;
            else if (_btnPrev.Contains(e.Location)) _hoveredIndex = 4;
            else if (_btnNext.Contains(e.Location)) _hoveredIndex = 5;
            else if (_btnHist.Contains(e.Location)) _hoveredIndex = 6;
            else if (_btnAutoStf.Contains(e.Location)) _hoveredIndex = 7;
            else _hoveredIndex = -1;

            if (oldHover != _hoveredIndex) Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoveredIndex = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_btnActualSize.Contains(e.Location)) ActualSizeClicked?.Invoke(this, EventArgs.Empty);
            else if (_btnFit.Contains(e.Location)) FitClicked?.Invoke(this, EventArgs.Empty);
            else if (_btnZoomIn.Contains(e.Location)) ZoomInClicked?.Invoke(this, EventArgs.Empty);
            else if (_btnZoomOut.Contains(e.Location)) ZoomOutClicked?.Invoke(this, EventArgs.Empty);
            else if (_btnPrev.Contains(e.Location)) PrevClicked?.Invoke(this, EventArgs.Empty);
            else if (_btnNext.Contains(e.Location)) NextClicked?.Invoke(this, EventArgs.Empty);
            else if (_btnHist.Contains(e.Location)) HistogramToggleClicked?.Invoke(this, EventArgs.Empty);
            else if (_btnAutoStf.Contains(e.Location)) AutoStfClicked?.Invoke(this, EventArgs.Empty);
        }

        private GraphicsPath GetRoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}