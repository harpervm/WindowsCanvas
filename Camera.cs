using System.Windows.Forms;

namespace ScrollableDesktop
{
    public class Camera
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int ScreenWidth { get; private set; }
        public int ScreenHeight { get; private set; }
        private System.Action _onPositionChanged;

        public Camera()
        {
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

            // Start in middle screen (screen 1,1 of 3x3)
            X = ScreenWidth;
            Y = ScreenHeight;
        }

        public void SetPositionChangedCallback(System.Action callback)
        {
            _onPositionChanged = callback;
        }

        public void Move(int dx, int dy)
        {
            int newX = X + dx;
            int newY = Y + dy;

            // Clamp to world boundaries (3x3 grid)
            // Camera position is top-left of viewport, so max position allows viewport to reach world edge
            int maxX = ScreenWidth * 2; // Viewport width is ScreenWidth, so max X is ScreenWidth * 2
            int maxY = ScreenHeight * 2; // Viewport height is ScreenHeight, so max Y is ScreenHeight * 2

            X = Math.Max(0, Math.Min(maxX, newX));
            Y = Math.Max(0, Math.Min(maxY, newY));
            
            _onPositionChanged?.Invoke();
        }

        public void ScrollToMakeVisible(int worldX, int worldY, int worldWidth, int worldHeight)
        {
            const int offset = 10; // 10px offset on all sides
            const int taskbarHeight = 50; // Extra spacing at bottom for Windows taskbar
            
            // Calculate window's screen position with current camera
            int screenX = worldX - X;
            int screenY = worldY - Y;
            int screenRight = screenX + worldWidth;
            int screenBottom = screenY + worldHeight;

            // Effective viewport boundaries with offsets
            int effectiveLeft = offset;
            int effectiveRight = ScreenWidth - offset;
            int effectiveTop = offset;
            int effectiveBottom = ScreenHeight - offset - taskbarHeight;

            int newCameraX = X;
            int newCameraY = Y;

            // Calculate how much we need to scroll in each direction
            int deltaX = 0;
            int deltaY = 0;

            // Check if window extends beyond left edge
            if (screenX < effectiveLeft)
            {
                deltaX = screenX - effectiveLeft; // Negative value, move camera left
            }
            // Check if window extends beyond right edge
            else if (screenRight > effectiveRight)
            {
                deltaX = screenRight - effectiveRight; // Positive value, move camera right
            }

            // Check if window extends beyond top edge
            if (screenY < effectiveTop)
            {
                deltaY = screenY - effectiveTop; // Negative value, move camera up
            }
            // Check if window extends beyond bottom edge
            else if (screenBottom > effectiveBottom)
            {
                deltaY = screenBottom - effectiveBottom; // Positive value, move camera down
            }

            // Apply the minimum scroll needed
            newCameraX = X + deltaX;
            newCameraY = Y + deltaY;

            // Clamp to world boundaries
            int maxX = ScreenWidth * 2;
            int maxY = ScreenHeight * 2;
            newCameraX = Math.Max(0, Math.Min(maxX, newCameraX));
            newCameraY = Math.Max(0, Math.Min(maxY, newCameraY));

            // Smoothly animate to new position
            AnimateToPosition(newCameraX, newCameraY);
        }

        private System.Windows.Forms.Timer _animationTimer;
        private int _targetX;
        private int _targetY;
        private int _startX;
        private int _startY;
        private double _animationProgress = 0.0;
        private bool _isAnimating = false;
        private const double AnimationDuration = 0.4; // Duration in seconds
        private DateTime _animationStartTime;

        // Ease-in-out cubic function: starts slow, accelerates in middle, decelerates at end
        private static double EaseInOutCubic(double t)
        {
            return t < 0.5
                ? 4 * t * t * t
                : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        }

        private void AnimateToPosition(int targetX, int targetY)
        {
            _targetX = targetX;
            _targetY = targetY;
            _startX = X;
            _startY = Y;
            _animationProgress = 0.0;
            _animationStartTime = DateTime.Now;

            if (_isAnimating)
                return; // Already animating

            _isAnimating = true;
            _animationTimer = new System.Windows.Forms.Timer();
            _animationTimer.Interval = 16; // ~60fps
            _animationTimer.Tick += (s, e) =>
            {
                double elapsed = (DateTime.Now - _animationStartTime).TotalSeconds;
                _animationProgress = Math.Min(elapsed / AnimationDuration, 1.0);

                // Apply easing function
                double eased = EaseInOutCubic(_animationProgress);

                // Calculate new position based on eased progress
                int newX = _startX + (int)((_targetX - _startX) * eased);
                int newY = _startY + (int)((_targetY - _startY) * eased);

                bool moved = (X != newX || Y != newY);

                X = newX;
                Y = newY;

                // Notify position changed during animation
                if (moved)
                {
                    _onPositionChanged?.Invoke();
                }

                // Stop animation when reached target
                if (_animationProgress >= 1.0)
                {
                    X = _targetX;
                    Y = _targetY;
                    _onPositionChanged?.Invoke();
                    _isAnimating = false;
                    _animationTimer.Stop();
                    _animationTimer.Dispose();
                }
            };
            _animationTimer.Start();
        }
    }
}