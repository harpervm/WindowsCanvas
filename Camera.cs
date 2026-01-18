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
            X += dx;
            Y += dy;
        }
    }
}