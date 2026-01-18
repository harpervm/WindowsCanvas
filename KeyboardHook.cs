using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScrollableDesktop
{
    public class KeyboardHook
    {
        private readonly Camera _camera;
        private readonly WindowManager _windowManager;

        private Win32.LowLevelKeyboardProc _proc;
        private IntPtr _hookId = IntPtr.Zero;
        private System.Windows.Forms.Timer _activationCheckTimer;
        private IntPtr _lastForegroundWindow = IntPtr.Zero;
        private bool _altTabDetected = false;

        public KeyboardHook(Camera camera, WindowManager windowManager)
        {
            _camera = camera;
            _windowManager = windowManager;
            _proc = HookCallback;

            // Timer to check for window activation after Alt+Tab
            _activationCheckTimer = new System.Windows.Forms.Timer();
            _activationCheckTimer.Interval = 100; // Check every 100ms
            _activationCheckTimer.Tick += CheckWindowActivation;
        }

        public void Start()
        {
            _hookId = Win32.SetKeyboardHook(_proc);
            _activationCheckTimer.Start();
        }

        public void Stop()
        {
            if (_hookId != IntPtr.Zero)
            {
                Win32.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            _activationCheckTimer?.Stop();
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var info = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);

                // Detect Alt+Tab (system key down for Tab when Alt is pressed)
                if (wParam == (IntPtr)Win32.WM_SYSKEYDOWN && info.vkCode == Win32.VK_TAB)
                {
                    _altTabDetected = true;
                }
            }

            return Win32.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private void CheckWindowActivation(object? sender, EventArgs e)
        {
            IntPtr foregroundWindow = Win32.GetForegroundWindow();

            // Check if foreground window changed and Alt+Tab was detected
            if (foregroundWindow != _lastForegroundWindow && _altTabDetected && foregroundWindow != IntPtr.Zero)
            {
                _lastForegroundWindow = foregroundWindow;

                // Find the window in our tracked windows
                var window = _windowManager.FindWindowByHandle(foregroundWindow);
                if (window != null)
                {
                    // Check if window is fully visible
                    if (!_windowManager.IsWindowFullyVisible(window))
                    {
                        // Scroll camera to make window visible
                        _camera.ScrollToMakeVisible(window.WorldX, window.WorldY, window.Width, window.Height);
                        _windowManager.UpdateWindowPositions();
                    }
                    
                    // Reset Alt+Tab detection after handling
                    _altTabDetected = false;
                }
            }
        }
    }
}
