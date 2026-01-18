using System;

namespace ScrollableDesktop
{
    public class WindowInfo
    {
        public IntPtr Handle { get; }
        public int WorldX { get; set; }
        public int WorldY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public WindowInfo(IntPtr handle, int x, int y, int w, int h)
        {
            Handle = handle;
            WorldX = x;
            WorldY = y;
            Width = w;
            Height = h;
        }
    }
}