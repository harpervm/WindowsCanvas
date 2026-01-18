using System.Windows.Forms;

namespace ScrollableDesktop
{
    public class Camera
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int ScreenWidth { get; private set; }
        public int ScreenHeight { get; private set; }

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
        }
    }
}