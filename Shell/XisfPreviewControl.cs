using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using SharpShell.SharpPreviewHandler;

namespace XisfExplorerPreview
{
    public class XisfPreviewControl : PreviewHandlerControl
    {
        private Bitmap _previewBitmap;
        private string _statusMessage;
        private Color _statusTextColor;

        private static readonly Color LightBackColor = Color.White;
        private static readonly Color DarkBackColor = Color.FromArgb(16, 16, 18);

        private const int WM_SETTINGCHANGE = 0x001A;

        public XisfPreviewControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            bool light = IsLightTheme();
            BackColor = light ? LightBackColor : DarkBackColor;
            _statusTextColor = light ? Color.FromArgb(64, 64, 64) : Color.LightGray;
        }

        // Explorer's preview pane follows the per-user "apps" dark/light setting, not the
        // classic Win32 SystemColors (those never change - dark mode is an opt-in per-app
        // immersive style, not a system palette swap), so it has to be read from the
        // registry directly to match what the native image preview handler shows.
        private static bool IsLightTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key?.GetValue("AppsUseLightTheme") is int value) return value != 0;
                }
            }
            catch
            {
                // Fall back to light, which is also Explorer's own default.
            }
            return true;
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
                const int margin = 16;
                float availWidth = Math.Max(1, Width - (margin * 2));
                float availHeight = Math.Max(1, Height - (margin * 2));

                float zx = availWidth / _previewBitmap.Width;
                float zy = availHeight / _previewBitmap.Height;
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
                    TextRenderer.DrawText(g, _statusMessage, f, ClientRectangle, _statusTextColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                }
            }
        }

        // WM_SETTINGCHANGE with "ImmersiveColorSet" fires when the user flips the
        // light/dark toggle while a preview is already open, so it re-themes live
        // instead of only picking up the setting on the next file selection.
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_SETTINGCHANGE && m.LParam != IntPtr.Zero &&
                Marshal.PtrToStringUni(m.LParam) == "ImmersiveColorSet")
            {
                ApplyTheme();
                Invalidate();
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
