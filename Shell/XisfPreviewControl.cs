using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SharpShell.SharpPreviewHandler;

namespace XisfExplorerPreview
{
    public class XisfPreviewControl : PreviewHandlerControl
    {
        private Bitmap _previewBitmap;
        private string _statusMessage;

        public XisfPreviewControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(16, 16, 18);
        }

        public void SetImage(Bitmap bmp)
        {
            var old = _previewBitmap;
            _previewBitmap = bmp;
            _statusMessage = null;
            old?.Dispose();
            Invalidate();
        }

        public void ShowMessage(string message)
        {
            var old = _previewBitmap;
            _previewBitmap = null;
            _statusMessage = message;
            old?.Dispose();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            if (_previewBitmap != null)
            {
                float zx = (float)Width / _previewBitmap.Width;
                float zy = (float)Height / _previewBitmap.Height;
                float zoom = Math.Min(zx, zy);

                float dw = _previewBitmap.Width * zoom;
                float dh = _previewBitmap.Height * zoom;
                float dx = (Width - dw) / 2f;
                float dy = (Height - dh) / 2f;

                g.InterpolationMode = zoom < 1.0f ? InterpolationMode.Bilinear : InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(_previewBitmap, dx, dy, dw, dh);
            }
            else if (!string.IsNullOrEmpty(_statusMessage))
            {
                using (Font f = new Font("Segoe UI", 10f))
                {
                    TextRenderer.DrawText(g, _statusMessage, f, ClientRectangle, Color.LightGray,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _previewBitmap?.Dispose();
                _previewBitmap = null;
            }
            base.Dispose(disposing);
        }
    }
}