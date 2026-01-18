using System;
using System.Drawing;
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
            BackColor = Color.Black;
            _currentOpacity = FullOpacity;
            _targetOpacity = FullOpacity;
            Opacity = FullOpacity;

            StartPosition = FormStartPosition.Manual;
            PositionBottomRight();

            DoubleBuffered = true;

            // Keep minimap fixed in bottom-right corner
            this.Resize += (s, e) => PositionBottomRight();
            this.LocationChanged += (s, e) => PositionBottomRight();

            // Timer for rendering
            var renderTimer = new System.Windows.Forms.Timer();
            renderTimer.Interval = 33; // ~30fps
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
