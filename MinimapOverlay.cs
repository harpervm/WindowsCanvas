using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScrollableDesktop
{
    public class MinimapOverlay : Form
    {
        private readonly Camera _camera;
        private readonly WindowManager _windowManager;

        public int WorldWidth { get; private set; }
        public int WorldHeight { get; private set; }
        public int ScreenWidth { get; private set; }
        public int ScreenHeight { get; private set; }

        public MinimapOverlay(Camera camera, WindowManager windowManager)
        {
            _camera = camera;
            _windowManager = windowManager;

            // Get current screen size
            var screen = Screen.PrimaryScreen?.Bounds;
            if (screen == null)
            {
                ScreenWidth = 2560;
                ScreenHeight = 1440;
            }
            else
            {
                ScreenWidth = screen.Value.Width;
                ScreenHeight = screen.Value.Height;
            }

            // World is 3x3 of the screen size
            WorldWidth = ScreenWidth * 3;
            WorldHeight = ScreenHeight * 3;

            // Calculate minimap size maintaining aspect ratio
            // Base size for minimap, but maintain screen aspect ratio
            float aspectRatio = (float)ScreenWidth / ScreenHeight;
            int baseMinimapHeight = 140;
            int minimapHeight = baseMinimapHeight;
            int minimapWidth = (int)(minimapHeight * aspectRatio);

            Width = minimapWidth;
            Height = minimapHeight;
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = Color.Black;
            Opacity = 0.75;

            StartPosition = FormStartPosition.Manual;
            PositionBottomRight();

            DoubleBuffered = true;

            // Keep minimap fixed in bottom-right corner
            this.Resize += (s, e) => PositionBottomRight();
            this.LocationChanged += (s, e) => PositionBottomRight();

            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 33; // ~30fps
            timer.Tick += (s, e) => Invalidate();
            timer.Start();
        }

        private void PositionBottomRight()
        {
            var screen = Screen.PrimaryScreen?.WorkingArea;
            if (screen == null) return;

            int newLeft = screen.Value.Right - Width - 20;
            int newTop = screen.Value.Bottom - Height - 20;

            // Only update if position actually changed to avoid infinite loop
            if (Left != newLeft || Top != newTop)
            {
                Left = newLeft;
                Top = newTop;
            }
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

            float viewW = ScreenWidth * scaleX;
            float viewH = ScreenHeight * scaleY;

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
