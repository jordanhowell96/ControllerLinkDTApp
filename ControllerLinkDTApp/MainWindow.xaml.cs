using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.IO.Ports;
using Microsoft.Win32;
using System.Threading;


// detect controller and launch steam when computer is unlocked

namespace ControllerLinkDTApp
{
    public partial class MainWindow : Window
    {
        private const int HOTKEY_ID = 9000; // Arbitrary ID for the hotkey
        private const int WM_HOTKEY = 0x0312;
        private const string PASSKEY = "6890";
        private SerialPort? _serialPort;

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool UnregisterHotKey(IntPtr hWnd, int id);

        public MainWindow()
        {
            InitializeComponent();
            InitializeSerial();
        }   

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            // Register the F9 key as a hotkey
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(WndProc);

            RegisterHotKey(hwnd, HOTKEY_ID, 0, (uint)KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.F9));
        }

        private void InitializeSerial()
        {
            // Get all available COM ports on the system
            string[] availablePorts = SerialPort.GetPortNames();

            // Optionally, you can filter or prioritize the ports
            string detectedPort = availablePorts.FirstOrDefault(); // Use the first available port

            if (detectedPort != null) 
            {
                _serialPort = new SerialPort(detectedPort, 9600) 
                {
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None
                };
                try 
                {
                    _serialPort.Open();
                    MessageBox.Show($"Connected to {detectedPort}");
                }
                catch (Exception ex) 
                {
                    MessageBox.Show($"Error opening serial port {detectedPort}: {ex.Message}");
                }
            }
            else 
            {
                MessageBox.Show("No available COM ports detected.");
                _serialPort = null;
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume) 
            {
                SendSerial("AWAKE_ACK");
            }
        }

        async private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock) 
            {
                SendSerial("UNLOCK_ACK");
            }
            if (await ReceiveSerialAsync("CONTROLLER_DETECTED", 10000, 20))
            {
                SendSerial("INIT_ACK");
                OpenSteam();
            }
        }

        private async Task<bool> ReceiveSerialAsync(string signal, int timeout, int serialDelay)
        {
            int elapsed = 0;

            while (elapsed < timeout)
            {
                if (_serialPort != null && _serialPort.IsOpen && _serialPort.BytesToRead > 0)
                {
                    string receivedData = await Task.Run(() => _serialPort.ReadLine().Trim());

                    if (receivedData == signal)
                    {
                        return true;
                    }
                }

                await Task.Delay(serialDelay);
                elapsed += serialDelay;
            }

            return false;
        }

        private void SendSerial(string message)
        {
            try 
            {
                if (_serialPort != null && _serialPort.IsOpen) 
                {
                    _serialPort.WriteLine(message);
                }
            }
            catch (Exception) {
                MessageBox.Show($"Error sending serial: {message}");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID) 
            {
                OpenSteam();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OpenSteam() 
        {
            try {
                ProcessStartInfo psi = new ProcessStartInfo 
                {
                    FileName = "steam://open/bigpicture",
                    UseShellExecute = true
                };

                Process.Start(psi);
                MessageBox.Show("Controller detected, launching Steam in Big Picture mode via URI...");
            }
            catch (Exception ex) {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hwnd, HOTKEY_ID);

            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;

            if (_serialPort != null && _serialPort.IsOpen) _serialPort.Close();

            base.OnClosed(e);
        }

    }
}