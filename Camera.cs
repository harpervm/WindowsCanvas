namespace ScrollableDesktop
{
    public class Camera
    {
        public int X { get; set; }
        public int Y { get; set; }

        public Camera()
        {
            // Start in middle screen (screen 1,1 of 3x3)
            X = 2560;
            Y = 1440;
        }

        public void Move(int dx, int dy)
        {
            X += dx;
            Y += dy;
        }
    }
}