using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Windows;
using System.IO.Ports;
using Microsoft.Win32;
using System.Threading;
using System.Threading.Tasks;

// Next steps:
// Refactoring
// Close start menu
// One button start / prevent pc from connecting to controller during unlock
// Option to sleep computer when steam is closed (only when a session is "active" through this program)

namespace ControllerLinkDTApp
{
    public partial class MainWindow : Window
    {
        const string START_COMMAND = "START";
        const string AWAKE_STATE = "AWAKE_STATE";
        const string UNLOCKED_STATE = "UNLOCKED_STATE";
        const int STATE_REFRESH = 1000;

        private SerialPort? _serialPort;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _pcLocked = false;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public MainWindow()
        {
            InitializeComponent();
            InitializeSerial();
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => StartStateMonitoringAsync(_cancellationTokenSource.Token));
            Task.Run(() => ReceiveSerial(_cancellationTokenSource.Token));
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            SystemEvents.SessionSwitch += OnSessionSwitch;
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                _pcLocked = false;
            }
            else if (e.Reason == SessionSwitchReason.SessionLock)
            {
                _pcLocked = true;
            }
        }

        private void InitializeSerial()
        {
            // TODO Automatically choose correct port
            string[] availablePorts = SerialPort.GetPortNames();
            string detectedPort = availablePorts.FirstOrDefault(port => port == "COM17");

            if (detectedPort != null)
            {
                _serialPort = new SerialPort(detectedPort, 9600)
                {
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    DtrEnable = true,
                    RtsEnable = true,
                    WriteTimeout = 2000
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

        private async Task StartStateMonitoringAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                string currentState = DetermineCurrentState();
                SendSerial(currentState);
                await Task.Delay(STATE_REFRESH, token);
            }
        }

        private string DetermineCurrentState()
        {
            return _pcLocked ? AWAKE_STATE : UNLOCKED_STATE;
        }

        private static void OpenSteam()
        {
            try
            {
                ProcessStartInfo psi = new()
                {
                    FileName = "steam://open/bigpicture",
                    UseShellExecute = true
                };

                Process steamProcess = Process.Start(psi);

                // Wait for Steam to be ready for input
                steamProcess.WaitForInputIdle();

                // Bring Steam to the foreground
                IntPtr hWnd = steamProcess.MainWindowHandle;
                if (hWnd != IntPtr.Zero)
                {
                    SetForegroundWindow(hWnd);
                }
                else
                {
                    // If MainWindowHandle is not available, try to find and bring any Steam window to the foreground
                    BringRunningSteamToForeground();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private static void BringRunningSteamToForeground()
        {
            // Try to find a running Steam process and bring its window to the foreground
            Process[] steamProcesses = Process.GetProcessesByName("steam");

            foreach (var process in steamProcesses)
            {
                IntPtr hWnd = process.MainWindowHandle;
                if (hWnd != IntPtr.Zero)
                {
                    SetForegroundWindow(hWnd);
                    break; // Bring the first found Steam window to the foreground
                }
            }
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
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending serial: {ex.Message}");
            }
        }

        private async Task ReceiveSerial(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (_serialPort != null && _serialPort.IsOpen)
                    {
                        string? incomingMessage = _serialPort.ReadLine();
                        _serialPort.DiscardInBuffer();

                        if (!string.IsNullOrEmpty(incomingMessage) && incomingMessage.Trim() == START_COMMAND)
                        {
                            OpenSteam();
                        }
                    }
                    await Task.Delay(100, token);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error receiving serial data: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen) _serialPort.Close();

            _cancellationTokenSource.Cancel();
            base.OnClosed(e);
        }
    }
}