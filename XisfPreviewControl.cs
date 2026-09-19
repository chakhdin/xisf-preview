using System;
using System.Drawing;
using System.Windows.Forms;
using SharpShell.SharpPreviewHandler;

namespace XisfExplorerPreview
{
    public class XisfPreviewControl : PreviewHandlerControl
    {
        private PictureBox _pictureBox;
        private Label _lblError;

        public XisfPreviewControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            _pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };

            _lblError = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                BackColor = Color.Black,
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            Controls.Add(_pictureBox);
            Controls.Add(_lblError);
        }

        public void LoadFile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return;

                Bitmap previewImage = XisfParser.GetPreviewImage(filePath);

                if (previewImage != null)
                {
                    if (_pictureBox.Image != null)
                    {
                        var oldImage = _pictureBox.Image;
                        _pictureBox.Image = null;
                        oldImage.Dispose();
                    }

                    _pictureBox.Image = previewImage;
                    _pictureBox.Visible = true;
                    _lblError.Visible = false;
                }
                else
                {
                    ShowError("Unable to render XISF image or thumbnail.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error loading XISF:\n{ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            _pictureBox.Visible = false;
            _lblError.Text = message;
            _lblError.Visible = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_pictureBox?.Image != null)
                {
                    _pictureBox.Image.Dispose();
                    _pictureBox.Image = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}