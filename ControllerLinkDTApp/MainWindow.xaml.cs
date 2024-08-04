using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace ControllerLinkDTApp
{
    public partial class MainWindow : Window
    {
        private const int HOTKEY_ID = 9000; // Arbitrary ID for the hotkey
        private const string STEAM_PATH = @"C:\Program Files (x86)\Steam\steam.exe";
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Register the F9 key as a hotkey
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(WndProc);

            bool registered = RegisterHotKey(hwnd, HOTKEY_ID, 0, (uint)KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.F9));
        }

        protected override void OnClosed(EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hwnd, HOTKEY_ID);
            base.OnClosed(e);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                OnF9KeyPressed();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnF9KeyPressed()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "steam://open/bigpicture",
                    UseShellExecute = true
                };
                Process.Start(psi);
                MessageBox.Show("F9 key detected, launching Steam in Big Picture mode via URI...");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }
    }
}