using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScrollableDesktop
{
    public class MinimapOverlay : Form
    {
        private readonly Camera _camera;
        private readonly WindowManager _windowManager;

        public int WorldWidth = 2560 * 3;
        public int WorldHeight = 1440 * 3;

        public MinimapOverlay(Camera camera, WindowManager windowManager)
        {
            _camera = camera;
            _windowManager = windowManager;

            Width = 220;
            Height = 140;
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            Opacity = 0.75;

            StartPosition = FormStartPosition.Manual;
            PositionBottomRight();

            DoubleBuffered = true;

            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 33; // ~30fps
            timer.Tick += (s, e) => Invalidate();
            timer.Start();
        }

        private void PositionBottomRight()
        {
            var screen = Screen.PrimaryScreen?.WorkingArea;
            if (screen == null) return;

            Left = screen.Value.Right - Width - 20;
            Top = screen.Value.Bottom - Height - 20;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;

            g.FillRectangle(Brushes.Black, 0, 0, Width, Height);

            float scaleX = (float)Width / WorldWidth;
            float scaleY = (float)Height / WorldHeight;

            // Camera viewport
            float camX = _camera.X * scaleX;
            float camY = _camera.Y * scaleY;

            float viewW = 2560 * scaleX;
            float viewH = 1440 * scaleY;

            g.DrawRectangle(Pens.Lime, camX, camY, viewW, viewH);

            // Windows
            foreach (var win in _windowManager.Windows)
            {
                float x = win.WorldX * scaleX;
                float y = win.WorldY * scaleY;
                float w = win.Width * scaleX;
                float h = win.Height * scaleY;

                g.DrawRectangle(Pens.White, x, y, w, h);
            }
        }
    }
}
