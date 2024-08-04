using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using System.Windows.Interop;

namespace ControllerLinkDTApp
{
    public class Worker
    {
        private volatile bool _shouldStop = false;

        // Event to update the UI (optional)
        public event Action<string> StatusUpdated;

        private const string STEAM_PATH = @"C:\Program Files (x86)\Steam\steam.exe";
        private const int HOTKEY_ID = 9000; // Arbitrary ID for the hotkey

        // Windows API calls to register and unregister hotkeys
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public void DoWork()
        {
            // Register the F20 key (virtual key code 0x7B is for F12; F20 would require a custom registration)
            RegisterHotKey(IntPtr.Zero, HOTKEY_ID, 0, (uint)KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.F20));

            // Main loop for the worker
            while (!_shouldStop)
            {
                // Wait for a key press event
                // This would be in a message loop in a real application, here simulated by Sleep
                Thread.Sleep(100);

                // Optional: Update status
                StatusUpdated?.Invoke("Listening for F20 key press...");
            }

            // Unregister the hotkey when stopping
            UnregisterHotKey(IntPtr.Zero, HOTKEY_ID);
        }

        public void Stop()
        {
            _shouldStop = true;
        }

        // This method would be called when a key press is detected (in a real application)
        private void OnF20KeyPressed()
        {
            try
            {
                Process.Start(STEAM_PATH, "-bigpicture");
                StatusUpdated?.Invoke("F20 key detected, launching Steam in Big Picture mode...");
                Thread.Sleep(30000); // Wait for 30 seconds (optional)
                StatusUpdated?.Invoke("Steam launched.");
            }
            catch (Exception ex)
            {
                StatusUpdated?.Invoke($"Error: {ex.Message}");
            }
        }

        // You'll need to override the WndProc method to handle the hotkey messages
        public void ProcessHotKeyMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                OnF20KeyPressed();
                handled = true;
            }
        }
    }
}
