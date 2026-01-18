using System;
using System.Windows.Forms;

namespace ScrollableDesktop
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine("WindowsCanvas by HarperServices.nl is starting...");

            Camera camera = new Camera();
            WindowManager windowManager = new WindowManager(camera);
            MouseHook mouseHook = new MouseHook(camera, windowManager);
            
            // Set up camera position change callback to update window positions
            camera.SetPositionChangedCallback(() => windowManager.UpdateWindowPositions());

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