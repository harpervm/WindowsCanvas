using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScrollableDesktop
{
    public class MinimapOverlay : Form
    {
        private readonly Camera _camera;
        private readonly WindowManager _windowManager;
        private int _lastCameraX;
        private int _lastCameraY;
        private System.Windows.Forms.Timer _fadeTimer;
        private System.Windows.Forms.Timer _animationTimer;
        private double _targetOpacity;
        private double _currentOpacity;
        private bool _isAnimating;
        private const double FullOpacity = 0.75;
        private const double FadedOpacity = 0.10;
        private const int FadeDelayMs = 1000; // 1 second delay before fade
        private const int FadeOutDurationMs = 1000; // 1 second fade out
        private const int FadeInDurationMs = 25; // 25ms fade in
        private const int AnimationStepMs = 16; // ~60fps animation

        public int WorldWidth { get; private set; }
        public int WorldHeight { get; private set; }
        public int ScreenWidth { get; private set; }
        public int ScreenHeight { get; private set; }

        public MinimapOverlay(Camera camera, WindowManager windowManager)
        {
            _camera = camera;
            _windowManager = windowManager;
            _lastCameraX = camera.X;
            _lastCameraY = camera.Y;

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
            
            // Enable custom painting for transparency effect
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.Opaque, false);
            BackColor = Color.Black; // Base color (will be drawn with transparency in OnPaint)
            
            _currentOpacity = FullOpacity;
            _targetOpacity = FullOpacity;
            Opacity = FullOpacity;

            StartPosition = FormStartPosition.Manual;
            PositionBottomRight();

            // Set form region to rounded rectangle for proper rounded corners
            SetRoundedRegion();

            DoubleBuffered = true;

            // Keep minimap fixed in bottom-right corner
            this.Resize += (s, e) =>
            {
                SetRoundedRegion();
                PositionBottomRight();
            };
            this.LocationChanged += (s, e) => PositionBottomRight();

            // Timer for rendering
            var renderTimer = new System.Windows.Forms.Timer();
            renderTimer.Interval = 16; // ~60fps (1000ms / 60 = 16.67ms)
            renderTimer.Tick += (s, e) => Invalidate();
            renderTimer.Start();

            // Timer to check for camera movement and handle fade
            var cameraCheckTimer = new System.Windows.Forms.Timer();
            cameraCheckTimer.Interval = 100; // Check every 100ms
            cameraCheckTimer.Tick += (s, e) => CheckCameraMovement();
            cameraCheckTimer.Start();

            // Timer for fade delay
            _fadeTimer = new System.Windows.Forms.Timer();
            _fadeTimer.Interval = FadeDelayMs;
            _fadeTimer.Tick += (s, e) =>
            {
                _fadeTimer.Stop();
                StartFadeOut();
            };
            _fadeTimer.Start(); // Start the fade timer - will fade after 1 second if no movement

            // Timer for smooth opacity animation
            _animationTimer = new System.Windows.Forms.Timer();
            _animationTimer.Interval = AnimationStepMs;
            _animationTimer.Tick += (s, e) => UpdateAnimation();
        }

        private void CheckCameraMovement()
        {
            // Check if camera position changed
            if (_camera.X != _lastCameraX || _camera.Y != _lastCameraY)
            {
                // Camera moved - reset fade timer and fade in
                _lastCameraX = _camera.X;
                _lastCameraY = _camera.Y;
                
                StartFadeIn();
                _fadeTimer.Stop();
                _fadeTimer.Start(); // Restart the 1-second countdown
            }
        }

        private void StartFadeOut()
        {
            _targetOpacity = FadedOpacity;
            if (!_isAnimating)
            {
                _isAnimating = true;
                _animationTimer.Start();
            }
        }

        private void StartFadeIn()
        {
            _targetOpacity = FullOpacity;
            if (!_isAnimating)
            {
                _isAnimating = true;
                _animationTimer.Start();
            }
        }

        private void UpdateAnimation()
        {
            const double epsilon = 0.01; // Small threshold for opacity comparison
            
            if (Math.Abs(_currentOpacity - _targetOpacity) < epsilon)
            {
                // Animation complete
                _currentOpacity = _targetOpacity;
                Opacity = _targetOpacity;
                _isAnimating = false;
                _animationTimer.Stop();
                return;
            }

            // Determine animation duration based on direction
            int duration = _targetOpacity > _currentOpacity ? FadeInDurationMs : FadeOutDurationMs;
            double opacityRange = Math.Abs(FullOpacity - FadedOpacity);
            double step = (opacityRange / duration) * AnimationStepMs;

            // Animate towards target
            if (_currentOpacity < _targetOpacity)
            {
                _currentOpacity = Math.Min(_currentOpacity + step, _targetOpacity);
            }
            else
            {
                _currentOpacity = Math.Max(_currentOpacity - step, _targetOpacity);
            }

            Opacity = _currentOpacity;
        }

        private void SetRoundedRegion()
        {
            using (var path = CreateRoundedRectangle(new RectangleF(0, 0, Width, Height), 8f))
            {
                Region = new Region(path);
            }
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

        private GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float diameter = radius * 2;
            
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90); // Top-left
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90); // Top-right
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90); // Bottom-right
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90); // Bottom-left
            path.CloseFigure();
            
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            
            // Enable high-quality rendering for smooth rounded corners
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality;

            // Draw semi-transparent dark background with 8px border radius
            // Use a slightly smaller rectangle to account for the border
            using (var bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
            using (var bgPath = CreateRoundedRectangle(new RectangleF(0, 0, Width, Height), 8f))
            {
                g.FillPath(bgBrush, bgPath);
            }
            
            // Draw border as a separate inset path to avoid pixelation
            using (var borderBrush = new SolidBrush(Color.FromArgb(255, 60, 60, 60)))
            using (var outerPath = CreateRoundedRectangle(new RectangleF(0, 0, Width, Height), 8f))
            using (var innerPath = CreateRoundedRectangle(new RectangleF(1, 1, Width - 2, Height - 2), 7f))
            {
                // Create a region that's the difference between outer and inner
                using (var borderRegion = new Region(outerPath))
                {
                    borderRegion.Exclude(innerPath);
                    g.FillRegion(borderBrush, borderRegion);
                }
            }

            float scaleX = (float)Width / WorldWidth;
            float scaleY = (float)Height / WorldHeight;

            // Camera viewport
            float camX = _camera.X * scaleX;
            float camY = _camera.Y * scaleY;

            float viewW = ScreenWidth * scaleX;
            float viewH = ScreenHeight * scaleY;

            g.DrawRectangle(Pens.Lime, camX, camY, viewW, viewH);

            // Draw windows with 3px border radius
            foreach (var win in _windowManager.Windows)
            {
                float x = win.WorldX * scaleX;
                float y = win.WorldY * scaleY;
                float w = win.Width * scaleX;
                float h = win.Height * scaleY;

                // Only draw if window is large enough for rounded corners
                if (w > 6 && h > 6)
                {
                    using (var winPath = CreateRoundedRectangle(new RectangleF(x, y, w, h), 3f))
                    using (var winPen = new Pen(Color.White, 1f))
                    {
                        winPen.Alignment = PenAlignment.Inset;
                        g.DrawPath(winPen, winPath);
                    }
                }
                else
                {
                    // For very small windows, just draw a rectangle
                    g.DrawRectangle(Pens.White, x, y, w, h);
                }
            }
        }
    }
}
