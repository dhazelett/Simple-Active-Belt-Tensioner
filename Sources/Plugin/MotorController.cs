using SimHub;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using WoteverLocalization;

namespace User.ActiveBeltTensioner
{
    /// <summary>A representation of the motor control system, which is technically one serial port shared by multiple <see cref="Motor" /> objects</summary>
    public class MotorController : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void Notify([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // -----------------------------------------------------------------------------------------
        // Public state
        // -----------------------------------------------------------------------------------------

        private Motor[] Motors { get; }

        public bool IsBusy => _activeActions > 0;

        public bool HasSerial => _bus.IsOpen;

        public bool BothMotorsAreConnected  => GetLeftMotor().IsConnected && GetRightMotor().IsConnected;
        public bool OneMotorIsConnected     => GetLeftMotor().IsConnected != GetRightMotor().IsConnected;
        public bool LeftMotorIsConnected    => GetLeftMotor()?.IsConnected ?? false;
        public bool RightMotorIsConnected   => GetRightMotor()?.IsConnected ?? false;

        public string LeftMotorStatus   => GetLeftMotor()?.Status  ?? SLoc.GetValue("SABT_Status_Disconnected");
        public string RightMotorStatus  => GetRightMotor()?.Status ?? SLoc.GetValue("SABT_Status_Disconnected");
        public string LeftMotorGraphic  => GetLeftMotor()?.Graphic  ?? MotorGraphic.Disconnected;
        public string RightMotorGraphic => GetRightMotor()?.Graphic ?? MotorGraphic.Disconnected;

        private string[] _serialPorts = Array.Empty<string>();

        public string[] SerialPorts
        {
            get => _serialPorts;
            set
            {
                if (!ReferenceEquals(_serialPorts, value))
                {
                    _serialPorts = value ?? Array.Empty<string>();
                    Notify();
                }
            }
        }

        // -----------------------------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------------------------

        private readonly DevicePlugin _plugin;
        private readonly MotorBus     _bus;

        private int  _activeActions;
        private bool _hasNotifiedOfLicense;
        private bool _motorCommandSwitch = true;

        private readonly long _motorCommandTicks;
        private long          _lastCommandTicks;

        // -----------------------------------------------------------------------------------------
        // Construction
        // -----------------------------------------------------------------------------------------

        public MotorController(DevicePlugin plugin)
        {
            _plugin = plugin;
            _bus    = new MotorBus();

            Motors = new Motor[]
            {
                new Motor(_bus, 0x01, "Left"),
                new Motor(_bus, 0x02, "Right")
            };

            foreach (Motor m in Motors)
                m.PropertyChanged += OnMotorPropertyChanged;

            _motorCommandTicks = (long)(16.67 * System.Diagnostics.Stopwatch.Frequency / 1000.0); // 60 Hz
        }

        private void OnMotorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!(sender is Motor motor)) return;

            Notify($"{motor.Label}Motor{e.PropertyName}");
            Notify(nameof(BothMotorsAreConnected));
            Notify(nameof(OneMotorIsConnected));
        }

        // -----------------------------------------------------------------------------------------
        // Setup wizard
        // -----------------------------------------------------------------------------------------

        /// <summary>Guides the user through one-motor-at-a-time ID assignment</summary>
        public bool Setup()
        {
            BeginAction(out int token);

            try   { return DoSetup(); }
            finally { EndAction(token); }
        }

        /// <summary>Performs the motor configuration process via a series of guided prompts</summary>
        /// <returns>Whether the process succeeded</returns>
        private bool DoSetup()
        {
            // Disable motors and disconnect cleanly — prevents the control loop
            // from interleaving commands with setup frames on the serial bus
            _plugin.IsEnabled = false;
            _bus.Disconnect();

            if (!_plugin.Settings.IsSerialPortValid || !_bus.Connect(_plugin.Settings.SerialPort))
            {
                MessageBox.Show(
                    SLoc.GetValue("SABT_Message_NoDeviceDetected"),
                    SLoc.GetValue("SABT_Plugin"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                );
                return false;
            }

            if (Motors.All(m => m.IsConnected))
            {
                MessageBox.Show(
                    SLoc.GetValue("SABT_Message_Setup_AlreadySetUp"),
                    SLoc.GetValue("SABT_Plugin"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation
                );
                return false;
            }

            if (Prompt("SABT_Message_Setup_TurnOffPower") != MessageBoxResult.Yes) return false;
            
            Motor left  = ConfigureMotor("Left");
            if (left == null) return false;

            Motor right = ConfigureMotor("Right");
            if (right == null) return false;

            // Step h: power off, plug in both motors, power on
            if (Prompt("SABT_Message_Setup_PowerCycleToSwap") != MessageBoxResult.Yes) return false;

            // Step i: final scan — both IDs must be present
            List<byte> final = _bus.ScanBus();
            bool verified    = final.Count == 2
                && final.Contains(left.Id)
                && final.Contains(right.Id);

            if (verified)
            {
                MessageBox.Show(
                    SLoc.GetValue("SABT_Message_Setup_Complete"),
                    SLoc.GetValue("SABT_Plugin"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return true;
            }

            Error("SABT_Message_Setup_VerifyFailed");
            return false;
        }
        
        /// <summary>Guides the user through the plug-in and ID-assignment steps for one motor; returns the configured motor or <see langword="null" /> if the user cancels</summary>
        /// <returns>The configured motor instance, or <see langword="null" /> if the process was cancelled or failed</returns>
        private Motor ConfigureMotor(string position)
        {
            Motor motor = MotorByLabel(position);

            motor.Status  = SLoc.GetValue("SABT_Status_AwaitingConnection");
            motor.Graphic = MotorGraphic.Connect;
            
            if (Prompt($"SABT_Message_Setup_PlugIn{position}Motor") != MessageBoxResult.Yes) return null;

            if (!WaitForSingleMotor()) return null;

            if (!motor.SetIdentifier() || !VerifySingleId(motor.Id))
            {
                Error($"SABT_Message_Setup_FailedToSet{position}Motor");
                return null;
            }

            return motor;
        }

        /// <summary>Scans until exactly one motor responds, prompting the user to fix the connection on each mismatch</summary>
        private bool WaitForSingleMotor()
        {
            while (true)
            {
                List<byte> found = _bus.ScanBus();

                if (found.Count == 1) return true;

                string key = found.Count == 0
                    ? "SABT_Message_Setup_NoMotorOnBus"
                    : "SABT_Message_Setup_MultipleMotorsOnBus";

                if (MessageBox.Show(
                    SLoc.GetValue(key),
                    SLoc.GetValue("SABT_Plugin"),
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning
                ) != MessageBoxResult.OK) return false;
            }
        }

        /// <summary>Re-scans and returns true only if <paramref name="id"/> is the sole responder</summary>
        private bool VerifySingleId(byte id)
        {
            List<byte> found = _bus.ScanBus();
            return found.Count == 1 && found[0] == id;
        }

        // -----------------------------------------------------------------------------------------
        // Connection lifecycle
        // -----------------------------------------------------------------------------------------

        /// <summary>Opens the selected serial port; checking motor communication automatically if enabled</summary>
        /// <returns>Whether the serial port was successfully opened</returns>
        public bool Connect()
        {
            BeginAction(out int token);

            bool connected = false;

            if (_bus.IsOpen)
            {
                if (_plugin.IsEnabled) Check();
                EndAction(token);
                return true;
            }

            if (_plugin.Settings.IsSerialPortValid)
            {
                connected = _bus.Connect(_plugin.Settings.SerialPort);

                if (!connected)
                    Logging.Current.Warn("SABT: Failed to open serial port");
            }
            else
            {
                Logging.Current.Warn("SABT: Invalid serial port selection");
            }

            EndAction(token);

            if (connected && _plugin.IsEnabled) Check();

            return connected;
        }

        /// <summary>Invokes the <see cref="Motor.Check()" /> method on each motor</summary>
        /// <returns>Whether all motors were successfully connected</returns>
        private bool Check()
        {
            if (!_plugin.PluginManager.IsSimHubLicenceValid && !_hasNotifiedOfLicense)
            {
                _hasNotifiedOfLicense = true;
                MessageBox.Show(
                    SLoc.GetValue("SABT_Message_SimHubLicenseRequired"),
                    SLoc.GetValue("SABT_Plugin"),
                    MessageBoxButton.OK
                );
            }

            BeginAction(out int token);

            bool allConnected = Motors.Aggregate(true, (current, m) => m.Check() && current);

            EndAction(token);
            return allConnected;
        }

        /// <summary>Invokes the <see cref="Motor.Stop()" /> method on each motor then closes the serial port</summary>
        public void Disconnect()
        {
            BeginAction(out int token);

            foreach (Motor m in Motors) m.Stop();

            _bus.Disconnect();

            EndAction(token);
        }

        /// <summary>An alias of <see cref="Disconnect()" /> for the purposes of fulfilling the <see cref="IDisposable" /> interface</summary>
        public void Dispose() => Disconnect();

        // -----------------------------------------------------------------------------------------
        // Runtime torque output (60 Hz, alternating left/right at 30 Hz each)
        // -----------------------------------------------------------------------------------------

        /// <summary>Sends the given torque values (as fractions of maximum torque) to the two motors, alternating between motors at 30Hz per motor (60Hz overall)</summary>
        /// <returns>Whether the motor commands were sent successfully (if applicable)</returns>
        public bool SetTorques(double left, double right, double smoothing = 0.0)
        {
            BeginAction(out int token);

            if (!_bus.IsOpen) { EndAction(token); return false; }

            bool ok = true;
            long now = System.Diagnostics.Stopwatch.GetTimestamp();

            if (now - _lastCommandTicks >= _motorCommandTicks)
            {
                ok = _motorCommandSwitch
                    ? GetLeftMotor().SetTorque(left, smoothing)
                    : GetRightMotor().SetTorque(right * -1, smoothing);

                _lastCommandTicks    = now;
                _motorCommandSwitch  = !_motorCommandSwitch;
            }

            EndAction(token);
            return ok;
        }

        // -----------------------------------------------------------------------------------------
        // Motor accessors (respect Flip Channels setting)
        // -----------------------------------------------------------------------------------------

        /// <summary>Provides the motor instance currently mapped to the left channel, accounting for the flip setting</summary>
        public Motor GetLeftMotor()  => MotorByLabel(_plugin.Settings.IsFlipped ? "Right" : "Left");

        /// <summary>Provides the motor instance currently mapped to the right channel, accounting for the flip setting</summary>
        public Motor GetRightMotor() => MotorByLabel(_plugin.Settings.IsFlipped ? "Left"  : "Right");

        /// <summary>Returns the motor whose <see cref="Motor.Label" /> matches the given value, or <see langword="null" /> if none is found</summary>
        private Motor MotorByLabel(string label)
        {
            return Motors.FirstOrDefault(m => m.Label == label);
        }

        // -----------------------------------------------------------------------------------------
        // Serial port discovery
        // -----------------------------------------------------------------------------------------

        /// <summary>Identifies devices that match the expected VID/PID for the controller board and returns a sorted list of their COM port names</summary>
        /// <returns>A sorted array of COM port name strings that appear to match</returns>
        public string[] UpdateSerialPorts()
        {
            if (_bus.IsOpen && _plugin.IsEnabled && SerialPorts?.Length > 0)
                return SerialPorts;

            Logging.Current.Info("SABT: Detecting serial ports...");

            const string vidPid      = "VID_1A86&PID_55D3";
            Regex        portPattern = new Regex(@"\((COM\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
            List<string> ports       = new List<string>();

            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, Caption, PNPDeviceID FROM Win32_PnPEntity"))
            {
                foreach (ManagementObject mo in searcher.Get())
                {
                    string pnpId   = mo["PNPDeviceID"] as string ?? string.Empty;
                    string display = (mo["Name"] as string ?? mo["Caption"] as string) ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(pnpId)) continue;
                    if (pnpId.IndexOf(vidPid, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    Match m = portPattern.Match(display);
                    if (m.Success) ports.Add(m.Groups[1].Value.ToUpperInvariant());
                }
            }

            string[] sorted = ports
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            SerialPorts = sorted;

            if (sorted.Length == 0)
            {
                Disconnect();
                _plugin.Settings.SerialPort = null;
                return SerialPorts;
            }

            if (string.IsNullOrWhiteSpace(_plugin.Settings.SerialPort) ||
                !sorted.Contains(_plugin.Settings.SerialPort, StringComparer.OrdinalIgnoreCase))
            {
                _plugin.Settings.SerialPort = sorted[0];
            }

            return SerialPorts;
        }

        // -----------------------------------------------------------------------------------------
        // Action tracking (exposes IsBusy for UI binding)
        // -----------------------------------------------------------------------------------------

        /// <summary>Increments the active action counter and notifies bindings of the <see cref="IsBusy" /> state change</summary>
        /// <remarks>Consult <see cref="IsBusy" /> to check if any actions are in-progress, and <see cref="EndAction" /> to mark an action as complete</remarks>
        private void BeginAction(out int token)
        {
            token = Interlocked.Increment(ref _activeActions);
            if (token == 1) Notify(nameof(IsBusy));
        }

        /// <summary>Decrements the active action counter and notifies bindings of the <see cref="IsBusy" /> state change</summary>
        /// <remarks>Consult <see cref="IsBusy" /> to check if any actions are in-progress, and <see cref="BeginAction" /> to mark an action as started</remarks>
        private void EndAction(int token)
        {
            int remaining = Interlocked.Decrement(ref _activeActions);
            if (remaining == 0) Notify(nameof(IsBusy));
        }

        // -----------------------------------------------------------------------------------------
        // MessageBox helpers (keep Setup() readable)
        // -----------------------------------------------------------------------------------------

        /// <summary>Shows a localised <see cref="MessageBoxButton.YesNoCancel" /> information dialog and returns the user's choice</summary>
        private static MessageBoxResult Prompt(string key) => MessageBox.Show(
            SLoc.GetValue(key), SLoc.GetValue("SABT_Plugin"),
            MessageBoxButton.YesNoCancel, MessageBoxImage.Information);

        /// <summary>Shows a localised <see cref="MessageBoxButton.OK" /> error dialog</summary>
        private static void Error(string key) => MessageBox.Show(
            SLoc.GetValue(key), SLoc.GetValue("SABT_Plugin"),
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
