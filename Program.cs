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

            MinimapOverlay minimap = new MinimapOverlay(camera, windowManager);
            minimap.Show();
            
            // Register MinimapOverlay handle so it's excluded from canvas tracking
            windowManager.SetMinimapOverlayHandle(minimap.Handle);

            windowManager.Start();
            mouseHook.Start();

            Application.Run();
        }
    }
}