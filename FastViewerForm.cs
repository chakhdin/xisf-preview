using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XisfExplorerPreview
{
    public class PreparedView
    {
        public XisfRawFrame Frame { get; set; }
        public Bitmap RenderedBitmap { get; set; }
    }

    public class FastViewerForm : Form
    {
        private string _currentFilePath;
        private List<string> _folderFiles = new List<string>();
        private int _currentIndex = -1;

        private XisfRawFrame _currentFrame;
        private Bitmap _displayBitmap;

        // View Transform
        private float _zoom = 1.0f;
        private PointF _panOffset = new PointF(0, 0);
        private bool _isPanning = false;
        private Point _lastMousePos;

        // Overlays
        private HistogramControl _histPanel;
        private BottomToolbarControl _bottomToolbar;

        // High-Speed Frame Cache for Next/Previous preloading
        private readonly ConcurrentDictionary<string, PreparedView> _cache = new ConcurrentDictionary<string, PreparedView>(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource _preloadCts;

        public FastViewerForm(string initialFilePath)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(16, 16, 18);
            WindowState = FormWindowState.Maximized;
            Text = "FastViewer";
            KeyPreview = true;

            InitializeOverlays();

            if (!string.IsNullOrEmpty(initialFilePath) && File.Exists(initialFilePath))
            {
                LoadFolder(initialFilePath);
                OpenFile(initialFilePath);
            }
        }

        private void InitializeOverlays()
        {
            _histPanel = new HistogramControl
            {
                Location = new Point(30, 40),
                Visible = false
            };

            _histPanel.ValuesChanging += (s, e) => ApplyLiveStretch();
            _histPanel.ValuesChanged += (s, e) => ApplyFullStretch();
            _histPanel.AutoStfClicked += (s, e) => TriggerLinkedAutoStf();
            _histPanel.ResetClicked += (s, e) => ResetStretch();
            _histPanel.CloseClicked += (s, e) => { _histPanel.Visible = false; };

            _bottomToolbar = new BottomToolbarControl();
            _bottomToolbar.ActualSizeClicked += (s, e) => ZoomActualSize();
            _bottomToolbar.FitClicked += (s, e) => FitToScreen();
            _bottomToolbar.ZoomInClicked += (s, e) => ZoomStep(1.25f);
            _bottomToolbar.ZoomOutClicked += (s, e) => ZoomStep(1.0f / 1.25f);
            _bottomToolbar.PrevClicked += (s, e) => Navigate(-1);
            _bottomToolbar.NextClicked += (s, e) => Navigate(1);
            _bottomToolbar.HistogramToggleClicked += (s, e) => { _histPanel.Visible = !_histPanel.Visible; };
            _bottomToolbar.AutoStfClicked += (s, e) => TriggerLinkedAutoStf();

            Controls.Add(_histPanel);
            Controls.Add(_bottomToolbar);

            PositionBottomToolbar();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            FitToScreen();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PositionBottomToolbar();
            FitToScreen();
        }

        private void PositionBottomToolbar()
        {
            if (_bottomToolbar != null)
            {
                _bottomToolbar.Location = new Point((ClientSize.Width - _bottomToolbar.Width) / 2, ClientSize.Height - _bottomToolbar.Height - 15);
            }
        }

        private void LoadFolder(string filePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                var files = Directory.GetFiles(dir, "*.*")
                    .Where(f => f.EndsWith(".xisf", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".fits", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".fit", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".fts", StringComparison.OrdinalIgnoreCase));

                _folderFiles = new List<string>(files);
                _folderFiles.Sort(StringComparer.OrdinalIgnoreCase);
                _currentIndex = _folderFiles.IndexOf(filePath);
            }
            catch
            {
                _folderFiles.Clear();
            }
        }

        private void OpenFile(string filePath)
        {
            _currentFilePath = filePath;
            _currentIndex = _folderFiles.IndexOf(filePath);

            // 1. Instant cache hit
            if (_cache.TryGetValue(filePath, out PreparedView cached))
            {
                ApplyPreparedView(cached);
                SchedulePreload();
                return;
            }

            // 2. Decode synchronously on miss with fast display
            PreparedView loaded = LoadAndRender(filePath);
            if (loaded != null)
            {
                _cache[filePath] = loaded;
                ApplyPreparedView(loaded);
                SchedulePreload();
            }
            else
            {
                Text = $"Failed to load: {Path.GetFileName(filePath)}";
            }
        }

        private static PreparedView LoadAndRender(string filePath)
        {
            try
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                XisfRawFrame frame = (ext == ".xisf") ? XisfParser.LoadRawFrame(filePath) : FitsParser.LoadRawFrame(filePath);
                if (frame == null) return null;

                Bitmap bmp = XisfParser.RenderBitmapFromRaw(frame, frame.AutoShadows, frame.AutoMidtones, frame.AutoHighlights, 1);
                return new PreparedView { Frame = frame, RenderedBitmap = bmp };
            }
            catch
            {
                return null;
            }
        }

        private void ApplyPreparedView(PreparedView view)
        {
            _currentFrame = view.Frame;
            var old = _displayBitmap;
            _displayBitmap = view.RenderedBitmap;

            _histPanel.SetHistogram(_currentFrame.Histogram, _currentFrame.Median, _currentFrame.MAD);
            _histPanel.Shadows = _currentFrame.AutoShadows;
            _histPanel.Midtones = _currentFrame.AutoMidtones;
            _histPanel.Highlights = _currentFrame.AutoHighlights;
            _histPanel.Invalidate();

            FitToScreen();
            UpdateTitle();
            Invalidate();
        }

        // Asynchronously pre-decodes the next and previous image into RAM
        private void SchedulePreload()
        {
            _preloadCts?.Cancel();
            _preloadCts = new CancellationTokenSource();
            CancellationToken token = _preloadCts.Token;

            if (_folderFiles.Count <= 1) return;

            int nextIdx = (_currentIndex + 1) % _folderFiles.Count;
            int prevIdx = (_currentIndex - 1 + _folderFiles.Count) % _folderFiles.Count;

            string nextPath = _folderFiles[nextIdx];
            string prevPath = _folderFiles[prevIdx];

            Task.Run(() =>
            {
                if (token.IsCancellationRequested) return;

                if (!_cache.ContainsKey(nextPath))
                {
                    PreparedView pv = LoadAndRender(nextPath);
                    if (pv != null && !token.IsCancellationRequested) _cache[nextPath] = pv;
                }

                if (token.IsCancellationRequested) return;

                if (!_cache.ContainsKey(prevPath))
                {
                    PreparedView pv = LoadAndRender(prevPath);
                    if (pv != null && !token.IsCancellationRequested) _cache[prevPath] = pv;
                }

                // Keep cache constrained to 5 images max to manage memory usage
                if (_cache.Count > 5)
                {
                    foreach (var key in _cache.Keys.ToList())
                    {
                        if (!key.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase) &&
                            !key.Equals(nextPath, StringComparison.OrdinalIgnoreCase) &&
                            !key.Equals(prevPath, StringComparison.OrdinalIgnoreCase))
                        {
                            if (_cache.TryRemove(key, out PreparedView removed))
                            {
                                removed.RenderedBitmap?.Dispose();
                            }
                        }
                    }
                }
            }, token);
        }

        private void TriggerLinkedAutoStf()
        {
            if (_currentFrame == null) return;

            _histPanel.Shadows = _currentFrame.AutoShadows;
            _histPanel.Midtones = _currentFrame.AutoMidtones;
            _histPanel.Highlights = _currentFrame.AutoHighlights;
            _histPanel.Invalidate();

            var oldBmp = _displayBitmap;
            _displayBitmap = XisfParser.RenderBitmapFromRaw(_currentFrame, _currentFrame.AutoShadows, _currentFrame.AutoMidtones, _currentFrame.AutoHighlights, 1);
            oldBmp?.Dispose();

            UpdateTitle();
            Invalidate();
        }

        private void ResetStretch()
        {
            _histPanel.Shadows = 0.0f;
            _histPanel.Midtones = 0.5f;
            _histPanel.Highlights = 1.0f;
            _histPanel.Invalidate();

            var oldBmp = _displayBitmap;
            _displayBitmap = XisfParser.RenderBitmapFromRaw(_currentFrame, 0.0f, 0.5f, 1.0f, 1);
            oldBmp?.Dispose();

            UpdateTitle();
            Invalidate();
        }

        private void ApplyLiveStretch()
        {
            if (_currentFrame == null) return;

            int step = (_currentFrame.Width > 2000 || _currentFrame.Height > 2000) ? 4 : 2;
            var oldBmp = _displayBitmap;
            _displayBitmap = XisfParser.RenderBitmapFromRaw(_currentFrame, _histPanel.Shadows, _histPanel.Midtones, _histPanel.Highlights, step);
            oldBmp?.Dispose();

            Invalidate();
        }

        private void ApplyFullStretch()
        {
            if (_currentFrame == null) return;

            var oldBmp = _displayBitmap;
            _displayBitmap = XisfParser.RenderBitmapFromRaw(_currentFrame, _histPanel.Shadows, _histPanel.Midtones, _histPanel.Highlights, 1);
            oldBmp?.Dispose();

            UpdateTitle();
            Invalidate();
        }

        public void FitToScreen()
        {
            if (_currentFrame == null || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;

            float availableHeight = Math.Max(10f, ClientSize.Height - 70f);
            float zx = (float)ClientSize.Width / _currentFrame.Width;
            float zy = availableHeight / _currentFrame.Height;
            _zoom = Math.Min(zx, zy) * 0.98f;

            _panOffset = new PointF(
                (ClientSize.Width - (_currentFrame.Width * _zoom)) / 2f,
                ((ClientSize.Height - 50) - (_currentFrame.Height * _zoom)) / 2f
            );

            UpdateTitle();
            Invalidate();
        }

        private void ZoomActualSize()
        {
            if (_currentFrame == null) return;

            _zoom = 1.0f;
            _panOffset = new PointF(
                (ClientSize.Width - _currentFrame.Width) / 2f,
                (ClientSize.Height - _currentFrame.Height) / 2f
            );

            UpdateTitle();
            Invalidate();
        }

        private void ZoomStep(float factor)
        {
            if (_displayBitmap == null) return;

            Point center = new Point(ClientSize.Width / 2, ClientSize.Height / 2);
            float oldZoom = _zoom;
            _zoom = Math.Max(0.01f, Math.Min(50.0f, _zoom * factor));

            _panOffset.X = center.X - ((center.X - _panOffset.X) * (_zoom / oldZoom));
            _panOffset.Y = center.Y - ((center.Y - _panOffset.Y) * (_zoom / oldZoom));

            UpdateTitle();
            Invalidate();
        }

        private void UpdateTitle()
        {
            if (_currentFrame == null) return;
            string fileName = Path.GetFileName(_currentFilePath);
            Text = $"{fileName} ({_currentFrame.Width}x{_currentFrame.Height}, {_currentFrame.Channels} Ch) - {(_zoom * 100):F0}% | [{_currentIndex + 1}/{_folderFiles.Count}] - FastViewer";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            if (_displayBitmap != null && _currentFrame != null)
            {
                g.InterpolationMode = _zoom < 1.0f ? InterpolationMode.Bilinear : InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;

                float displayWidth = _currentFrame.Width * _zoom;
                float displayHeight = _currentFrame.Height * _zoom;

                g.DrawImage(_displayBitmap,
                    _panOffset.X, _panOffset.Y,
                    displayWidth,
                    displayHeight);
            }
            else
            {
                TextRenderer.DrawText(g, "No image loaded. Use PageUp/Down or toolbar to navigate.", Font, ClientRectangle, Color.Gray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_displayBitmap == null) return;

            float oldZoom = _zoom;
            float factor = e.Delta > 0 ? 1.2f : (1.0f / 1.2f);
            _zoom = Math.Max(0.01f, Math.Min(50.0f, _zoom * factor));

            _panOffset.X = e.X - ((e.X - _panOffset.X) * (_zoom / oldZoom));
            _panOffset.Y = e.Y - ((e.Y - _panOffset.Y) * (_zoom / oldZoom));

            UpdateTitle();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            bool overHist = _histPanel.Visible && _histPanel.Bounds.Contains(e.Location);
            bool overToolbar = _bottomToolbar.Bounds.Contains(e.Location);

            if (e.Button == MouseButtons.Left && !overHist && !overToolbar)
            {
                _isPanning = true;
                _lastMousePos = e.Location;
                Cursor = Cursors.Hand;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_isPanning)
            {
                _panOffset.X += (e.X - _lastMousePos.X);
                _panOffset.Y += (e.Y - _lastMousePos.Y);
                _lastMousePos = e.Location;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _isPanning = false;
            Cursor = Cursors.Default;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
            else if (e.KeyCode == Keys.PageDown || e.KeyCode == Keys.Right)
            {
                Navigate(1);
            }
            else if (e.KeyCode == Keys.PageUp || e.KeyCode == Keys.Left)
            {
                Navigate(-1);
            }
            else if (e.KeyCode == Keys.H)
            {
                _histPanel.Visible = !_histPanel.Visible;
            }
            else if (e.KeyCode == Keys.Space)
            {
                TriggerLinkedAutoStf();
            }
            else if (e.KeyCode == Keys.F)
            {
                FitToScreen();
            }
        }

        private void Navigate(int delta)
        {
            if (_folderFiles.Count == 0) return;

            _currentIndex = (_currentIndex + delta + _folderFiles.Count) % _folderFiles.Count;
            OpenFile(_folderFiles[_currentIndex]);
        }
    }
}