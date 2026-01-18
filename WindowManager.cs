using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ScrollableDesktop
{
    public class WindowManager
    {
        private readonly Camera _camera;
        private readonly List<WindowInfo> _windows = new();

        public WindowManager(Camera camera)
        {
            _camera = camera;
        }

        public void Start()
        {
            RefreshWindows();
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


        public void UpdateWindowPositions()
        {
            foreach (var win in _windows)
            {
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

        public IReadOnlyList<WindowInfo> Windows => _windows;
    }
}
