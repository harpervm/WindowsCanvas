using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScrollableDesktop
{
    public class WindowManager
    {
        private readonly Camera _camera;
        private readonly List<WindowInfo> _windows = new();
        private System.Windows.Forms.Timer _refreshTimer;
        private System.Windows.Forms.Timer _syncTimer;
        private IntPtr _minimapOverlayHandle = IntPtr.Zero;

        public WindowManager(Camera camera)
        {
            _camera = camera;
        }

        public void SetMinimapOverlayHandle(IntPtr handle)
        {
            _minimapOverlayHandle = handle;
        }

        public void Start()
        {
            RefreshWindows();

            // Timer to detect new windows (every 1.5 seconds)
            _refreshTimer = new System.Windows.Forms.Timer();
            _refreshTimer.Interval = 1500;
            _refreshTimer.Tick += (s, e) => RefreshWindowsIncremental();
            _refreshTimer.Start();

            // Timer to sync positions from screen in real-time (every 500ms)
            _syncTimer = new System.Windows.Forms.Timer();
            _syncTimer.Interval = 500;
            _syncTimer.Tick += (s, e) => SyncWindowPositionsFromScreen();
            _syncTimer.Start();
        }

        public void RefreshWindows()
        {
            _windows.Clear();

            Win32.EnumWindows((hWnd, lParam) =>
            {
                if (!IsRealAppWindow(hWnd))
                    return true;

                Win32.RECT rect;
                Win32.GetWindowRect(hWnd, out rect);

                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                var win = new WindowInfo(
                    hWnd,
                    rect.Left + _camera.X,
                    rect.Top + _camera.Y,
                    width,
                    height
                );

                _windows.Add(win);
                return true;
            }, IntPtr.Zero);
        }

        private void RefreshWindowsIncremental()
        {
            // Get all currently visible windows
            var currentWindows = new HashSet<IntPtr>();
            
            Win32.EnumWindows((hWnd, lParam) =>
            {
                if (IsRealAppWindow(hWnd))
                {
                    currentWindows.Add(hWnd);
                }
                return true;
            }, IntPtr.Zero);

            // Remove windows that no longer exist
            _windows.RemoveAll(win => !currentWindows.Contains(win.Handle) || !Win32.IsWindowVisible(win.Handle));

            // Add new windows that aren't in our list
            Win32.EnumWindows((hWnd, lParam) =>
            {
                if (!IsRealAppWindow(hWnd))
                    return true;

                // Check if we already have this window
                if (_windows.Any(w => w.Handle == hWnd))
                    return true;

                // New window - add it
                Win32.RECT rect;
                if (!Win32.GetWindowRect(hWnd, out rect))
                    return true;

                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                var win = new WindowInfo(
                    hWnd,
                    rect.Left + _camera.X,
                    rect.Top + _camera.Y,
                    width,
                    height
                );

                _windows.Add(win);
                return true;
            }, IntPtr.Zero);
        }


        public void SyncWindowPositionsFromScreen()
        {
            // Sync world coordinates from current screen positions
            // This ensures manual window moves/resizes are captured before panning
            foreach (var win in _windows)
            {
                // Skip MinimapOverlay - it should stay fixed to screen
                if (win.Handle == _minimapOverlayHandle)
                    continue;

                // Check if window still exists and is valid
                if (!Win32.IsWindowVisible(win.Handle))
                    continue;

                Win32.RECT rect;
                if (!Win32.GetWindowRect(win.Handle, out rect))
                    continue;

                // Update world coordinates from current screen position
                int screenX = rect.Left;
                int screenY = rect.Top;
                win.WorldX = screenX + _camera.X;
                win.WorldY = screenY + _camera.Y;

                // Update size from current window rect
                win.Width = rect.Right - rect.Left;
                win.Height = rect.Bottom - rect.Top;
            }
        }

        public void UpdateWindowPositions()
        {
            foreach (var win in _windows)
            {
                // Skip MinimapOverlay - it should stay fixed to screen
                if (win.Handle == _minimapOverlayHandle)
                    continue;

                int screenX = win.WorldX - _camera.X;
                int screenY = win.WorldY - _camera.Y;

                Win32.SetWindowPos(
                    win.Handle,
                    IntPtr.Zero,
                    screenX,
                    screenY,
                    win.Width,
                    win.Height,
                    Win32.SetWindowPosFlags.SWP_NOZORDER
                );
            }
        }
        private bool IsRealAppWindow(IntPtr hWnd)
        {
            if (!Win32.IsWindowVisible(hWnd))
                return false;

            // Exclude MinimapOverlay - it should be fixed to screen, not part of canvas
            if (hWnd == _minimapOverlayHandle)
                return false;

            IntPtr shellWindow = Win32.GetShellWindow();
            if (hWnd == shellWindow)
                return false;

            string className = Win32.GetClassName(hWnd);

            // Desktop & system layers
            if (className == "Progman") return false;
            if (className == "WorkerW") return false;
            if (className == "Shell_TrayWnd") return false;

            return true;
        }

        public WindowInfo GetWindowAtScreenPosition(int screenX, int screenY)
        {
            // Convert screen position to world position
            int worldX = screenX + _camera.X;
            int worldY = screenY + _camera.Y;

            // Find the topmost window at this position
            // Check windows in reverse order (topmost first)
            for (int i = _windows.Count - 1; i >= 0; i--)
            {
                var win = _windows[i];
                
                // Skip MinimapOverlay
                if (win.Handle == _minimapOverlayHandle)
                    continue;

                // Check if point is within window bounds
                if (worldX >= win.WorldX && worldX < win.WorldX + win.Width &&
                    worldY >= win.WorldY && worldY < win.WorldY + win.Height)
                {
                    return win;
                }
            }

            return null;
        }

        public bool IsWindowFullyVisible(WindowInfo win)
        {
            const int offset = 10; // 10px offset on all sides
            const int taskbarHeight = 50; // Extra spacing at bottom for Windows taskbar
            
            // Calculate window's screen position
            int screenX = win.WorldX - _camera.X;
            int screenY = win.WorldY - _camera.Y;
            int screenRight = screenX + win.Width;
            int screenBottom = screenY + win.Height;

            // Effective viewport boundaries with offsets
            int effectiveLeft = offset;
            int effectiveRight = _camera.ScreenWidth - offset;
            int effectiveTop = offset;
            int effectiveBottom = _camera.ScreenHeight - offset - taskbarHeight;

            // Check if window is fully within viewport with offsets
            return screenX >= effectiveLeft && screenY >= effectiveTop && 
                   screenRight <= effectiveRight && 
                   screenBottom <= effectiveBottom;
        }

        public IReadOnlyList<WindowInfo> Windows => _windows;
    }
}
