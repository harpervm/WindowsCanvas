using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ScrollableDesktop
{
    public class MouseHook
    {
        private readonly Camera _camera;
        private readonly WindowManager _windowManager;

        private bool _draggingWithMiddleButton = false;
        private bool _draggingWithAltLeftButton = false;
        private bool _altLeftButtonInitialized = false;
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
                bool isAltPressed = Win32.IsLeftAltPressed();
                bool isLeftMousePressed = Win32.IsLeftMouseButtonPressed();

                // Block default mouse behaviors when Alt is held (prevent focusing, text selection, etc.)
                if (isAltPressed)
                {
                    // Block left mouse button down/up when Alt is held to prevent default behaviors
                    if ((Win32.MouseMessages)wParam == Win32.MouseMessages.WM_LBUTTONDOWN)
                    {
                        // Start panning with Alt + Left Mouse Button
                        _windowManager.SyncWindowPositionsFromScreen();
                        _draggingWithAltLeftButton = true;
                        _altLeftButtonInitialized = false; // Will be initialized on first mouse move
                        // Block the message to prevent default behavior
                        return new IntPtr(1);
                    }

                    if ((Win32.MouseMessages)wParam == Win32.MouseMessages.WM_LBUTTONUP)
                    {
                        // Stop panning when left mouse button is released
                        _draggingWithAltLeftButton = false;
                        _altLeftButtonInitialized = false;
                        // Block the message to prevent default behavior
                        return new IntPtr(1);
                    }

                    // Block middle mouse button when Alt is held to prevent default scrollwheel behavior
                    if ((Win32.MouseMessages)wParam == Win32.MouseMessages.WM_MBUTTONDOWN)
                    {
                        // Start panning with Alt + Middle Mouse Button (Alt is optional)
                        _windowManager.SyncWindowPositionsFromScreen();
                        _draggingWithMiddleButton = true;
                        _lastX = info.pt.x;
                        _lastY = info.pt.y;
                        // Block the message to prevent default scrollwheel behavior
                        return new IntPtr(1);
                    }

                    if ((Win32.MouseMessages)wParam == Win32.MouseMessages.WM_MBUTTONUP)
                    {
                        // Stop panning when middle mouse button is released
                        _draggingWithMiddleButton = false;
                        // Block the message to prevent default scrollwheel behavior
                        return new IntPtr(1);
                    }
                }
                else
                {
                    // If Alt is not pressed, stop Alt+LeftButton dragging if it was active
                    if (_draggingWithAltLeftButton)
                    {
                        _draggingWithAltLeftButton = false;
                    }

                    // Start panning with middle mouse button (without Alt)
                    if ((Win32.MouseMessages)wParam == Win32.MouseMessages.WM_MBUTTONDOWN)
                    {
                        // Sync window positions and sizes from screen before panning starts
                        _windowManager.SyncWindowPositionsFromScreen();
                        
                        _draggingWithMiddleButton = true;
                        _lastX = info.pt.x;
                        _lastY = info.pt.y;
                    }

                    // Stop panning when middle mouse button is released
                    if ((Win32.MouseMessages)wParam == Win32.MouseMessages.WM_MBUTTONUP)
                    {
                        _draggingWithMiddleButton = false;
                    }
                }

                // Handle mouse movement for panning
                if ((Win32.MouseMessages)wParam == Win32.MouseMessages.WM_MOUSEMOVE)
                {
                    // Pan if middle mouse button is held OR (Alt + Left Mouse Button) is held
                    // For Alt+LeftButton, we just need the flag set and Alt still pressed
                    bool shouldPan = _draggingWithMiddleButton || (_draggingWithAltLeftButton && isAltPressed);

                    if (shouldPan)
                    {
                        // If starting pan with Alt+LeftButton on first move, initialize position
                        if (_draggingWithAltLeftButton && !_altLeftButtonInitialized)
                        {
                            _lastX = info.pt.x;
                            _lastY = info.pt.y;
                            _altLeftButtonInitialized = true;
                            return Win32.CallNextHookEx(_hookId, nCode, wParam, lParam);
                        }

                        int dx = info.pt.x - _lastX;
                        int dy = info.pt.y - _lastY;

                        // Only move if there's actual movement
                        if (dx != 0 || dy != 0)
                        {
                            _camera.Move(-dx, -dy);
                            _windowManager.UpdateWindowPositions();
                        }

                        _lastX = info.pt.x;
                        _lastY = info.pt.y;
                    }

                    // Stop Alt+LeftButton dragging if Alt is released
                    if (_draggingWithAltLeftButton && !isAltPressed)
                    {
                        _draggingWithAltLeftButton = false;
                        _altLeftButtonInitialized = false;
                    }
                }
            }

            return Win32.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }
    }
}
