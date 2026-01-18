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
        private System.Windows.Forms.Timer _startupTimer;
        private System.Collections.Generic.Dictionary<IntPtr, double> _windowOpacities;
        private double _textOpacity = 1.0;
        private double _cameraOpacity = 0.0;
        private DateTime _startTime;
        private bool _startupComplete = false;
        private double _targetOpacity;
        private double _currentOpacity;
        private bool _isAnimating;
        private const double FullOpacity = 0.75;
        private const double FadedOpacity = 0.10;
        private const int FadeDelayMs = 1000; // 1 second delay before fade
        private const int FadeOutDurationMs = 1000; // 1 second fade out
        private const int FadeInDurationMs = 25; // 25ms fade in
        private const int AnimationStepMs = 16; // ~60fps animation
        private const int TextVisibleDurationMs = 2000; // Text visible for 2 seconds
        private const int TextFadeOutDurationMs = 25; // Text fade out in 25ms
        private const int CameraFadeInDelayMs = 2000; // Camera starts at 1 second
        private const int CameraFadeInDurationMs = 25; // Camera fade in 25ms
        private const int WindowFadeInDurationMs = 100; // Window fade in 25ms
        private const int WindowFadeInDelayMs = 50; // 50ms delay between windows

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
            _windowOpacities = new System.Collections.Generic.Dictionary<IntPtr, double>();
            _startTime = DateTime.Now;

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

            // Timer for startup sequence
            _startupTimer = new System.Windows.Forms.Timer();
            _startupTimer.Interval = 16; // ~60fps
            _startupTimer.Tick += (s, e) => UpdateStartupSequence();
            _startupTimer.Start();
        }

        private void UpdateStartupSequence()
        {
            if (_startupComplete)
            {
                _startupTimer.Stop();
                return;
            }

            var elapsed = (DateTime.Now - _startTime).TotalMilliseconds;

            // 1. Text: visible for 2 seconds, then fade out in 25ms
            if (elapsed >= TextVisibleDurationMs)
            {
                // Start fading out text
                double fadeOutProgress = Math.Min(1.0, (elapsed - TextVisibleDurationMs) / TextFadeOutDurationMs);
                _textOpacity = 1.0 - fadeOutProgress;
            }

            // 2. Camera: starts fading in at 1 second, duration 25ms
            if (elapsed >= CameraFadeInDelayMs)
            {
                double fadeInProgress = Math.Min(1.0, (elapsed - CameraFadeInDelayMs) / CameraFadeInDurationMs);
                _cameraOpacity = fadeInProgress;
            }

            // 3. Windows: start appearing after text fades out, one by one every 50ms
            if (_textOpacity <= 0 && !_startupComplete)
            {
                StartWindowFadeInSequence();
            }

            Invalidate();
        }

        private void StartWindowFadeInSequence()
        {
            if (_startupComplete)
                return;

            // Find next window that hasn't been faded in yet
            foreach (var win in _windowManager.Windows)
            {
                if (!_windowOpacities.ContainsKey(win.Handle))
                {
                    // Start fading in this window
                    _windowOpacities[win.Handle] = 0.0;
                    FadeInWindow(win.Handle);
                    return;
                }
            }

            // All windows have been faded in
            if (_windowManager.Windows.Count > 0 && _windowOpacities.Count >= _windowManager.Windows.Count)
            {
                _startupComplete = true;
            }
            else if (_windowManager.Windows.Count == 0)
            {
                _startupComplete = true;
            }
        }

        private void FadeInWindow(IntPtr windowHandle)
        {
            var fadeTimer = new System.Windows.Forms.Timer();
            fadeTimer.Interval = 1; // Update every 1ms for smooth 25ms animation
            int steps = 0;
            const int totalSteps = WindowFadeInDurationMs; // 25 steps for 25ms
            DateTime startTime = DateTime.Now;

            fadeTimer.Tick += (s, e) =>
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                double progress = Math.Min(1.0, elapsed / WindowFadeInDurationMs);
                
                if (_windowOpacities.ContainsKey(windowHandle))
                {
                    _windowOpacities[windowHandle] = progress;
                    Invalidate();
                }

                if (progress >= 1.0)
                {
                    fadeTimer.Stop();
                    fadeTimer.Dispose();
                    
                    // After delay, start next window
                    var delayTimer = new System.Windows.Forms.Timer();
                    delayTimer.Interval = WindowFadeInDelayMs;
                    delayTimer.Tick += (s2, e2) =>
                    {
                        delayTimer.Stop();
                        delayTimer.Dispose();
                        StartWindowFadeInSequence();
                    };
                    delayTimer.Start();
                }
            };
            fadeTimer.Start();
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

            // Camera viewport (with fade-in animation)
            if (_cameraOpacity > 0)
            {
                float camX = _camera.X * scaleX;
                float camY = _camera.Y * scaleY;

                float viewW = ScreenWidth * scaleX;
                float viewH = ScreenHeight * scaleY;

                using (var cameraPen = new Pen(Color.FromArgb((int)(255 * _cameraOpacity), Color.Lime), 1f))
                {
                    g.DrawRectangle(cameraPen, camX, camY, viewW, viewH);
                }
            }

            // Draw startup text (center-aligned)
            if (_textOpacity > 0)
            {
                using (var textBrush = new SolidBrush(Color.FromArgb((int)(255 * _textOpacity), Color.White)))
                using (var centerFormat = new StringFormat())
                {
                    centerFormat.Alignment = StringAlignment.Center;
                    centerFormat.LineAlignment = StringAlignment.Center;

                    var font = new Font("Segoe UI", 10, FontStyle.Bold);
                    string text = "WindowsCanvas";
                    string subtitle = "by HarperServices.nl";

                    var textSize = g.MeasureString(text, font);
                    var subtitleSize = g.MeasureString(subtitle, font);

                    // Calculate center positions
                    float totalHeight = textSize.Height + subtitleSize.Height + 5;
                    float centerY = Height / 2f;
                    float textY = centerY - totalHeight / 2f + textSize.Height / 2f;
                    float subtitleY = centerY + totalHeight / 2f - subtitleSize.Height / 2f;

                    // Draw main text centered
                    g.DrawString(text, font, textBrush, new RectangleF(0, textY - textSize.Height / 2f, Width, textSize.Height), centerFormat);

                    // Draw subtitle centered
                    using (var subtitleFont = new Font("Segoe UI", 8, FontStyle.Regular))
                    {
                        g.DrawString(subtitle, subtitleFont, textBrush, new RectangleF(0, subtitleY - subtitleSize.Height / 2f, Width, subtitleSize.Height), centerFormat);
                    }

                    font.Dispose();
                }
            }

            // Draw windows with 3px border radius and fade-in animation
            foreach (var win in _windowManager.Windows)
            {
                // Get opacity for this window
                double windowOpacity;
                if (_startupComplete)
                {
                    // After startup, new windows fade in immediately
                    if (!_windowOpacities.ContainsKey(win.Handle))
                    {
                        _windowOpacities[win.Handle] = 0.0;
                        FadeInWindow(win.Handle);
                        windowOpacity = 0.0;
                    }
                    else
                    {
                        windowOpacity = _windowOpacities[win.Handle];
                    }
                }
                else
                {
                    // During startup, only show windows that have been faded in
                    windowOpacity = _windowOpacities.TryGetValue(win.Handle, out var opacity) ? opacity : 0.0;
                }

                // Skip drawing if window hasn't faded in yet (unless startup is complete)
                if (!_startupComplete && windowOpacity <= 0)
                    continue;

                float x = win.WorldX * scaleX;
                float y = win.WorldY * scaleY;
                float w = win.Width * scaleX;
                float h = win.Height * scaleY;

                // Create pen with opacity
                using (var winPen = new Pen(Color.FromArgb((int)(255 * windowOpacity), Color.White), 1f))
                {
                    // Only draw if window is large enough for rounded corners
                    if (w > 6 && h > 6)
                    {
                        using (var winPath = CreateRoundedRectangle(new RectangleF(x, y, w, h), 3f))
                        {
                            winPen.Alignment = PenAlignment.Inset;
                            g.DrawPath(winPen, winPath);
                        }
                    }
                    else
                    {
                        // For very small windows, just draw a rectangle
                        g.DrawRectangle(winPen, x, y, w, h);
                    }
                }
            }
        }
    }
}
