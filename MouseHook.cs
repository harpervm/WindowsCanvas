using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScrollableDesktop
{
    public class MouseHook
    {
        private readonly Camera _camera;
        private readonly WindowManager _windowManager;

        private bool _dragging = false;
        private int _lastX, _lastY;

        private Win32.LowLevelMouseProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        public MouseHook(Camera camera, WindowManager windowManager)
        {
            _camera = camera;
            _windowManager = windowManager;
            _proc = HookCallback;
        }

        public void Start()
        {
            _hookId = Win32.SetHook(_proc);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var info = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);

                if ((Win32.MouseMessages)wParam == Win32.MouseMessages.WM_MBUTTONDOWN)
                {
                    // Sync window positions and sizes from screen before panning starts
                    // This captures any manual window moves/resizes that happened
                    _windowManager.SyncWindowPositionsFromScreen();
                    
                    _dragging = true;
                    _lastX = info.pt.x;
                    _lastY = info.pt.y;
                }

                if ((Win32.MouseMessages)wParam == Win32.MouseMessages.WM_MBUTTONUP)
                {
                    _dragging = false;
                }

                if ((Win32.MouseMessages)wParam == Win32.MouseMessages.WM_MOUSEMOVE && _dragging)
                {
                    int dx = info.pt.x - _lastX;
                    int dy = info.pt.y - _lastY;

                    _camera.Move(-dx, -dy);
                    _windowManager.UpdateWindowPositions();

                    _lastX = info.pt.x;
                    _lastY = info.pt.y;
                }
            }

            return Win32.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
    }
}
