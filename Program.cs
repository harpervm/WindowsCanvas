using System;
using System.Windows.Forms;

namespace ScrollableDesktop
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("Scrollable Desktop starting...");

            Camera camera = new Camera();
            WindowManager windowManager = new WindowManager(camera);
            MouseHook mouseHook = new MouseHook(camera, windowManager);

            windowManager.Start();
            mouseHook.Start();

            MinimapOverlay minimap = new MinimapOverlay(camera, windowManager);
            minimap.Show();

            Application.Run();
        }
    }
}