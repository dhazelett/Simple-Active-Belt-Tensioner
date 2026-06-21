using SimHub;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using WoteverLocalization;

namespace User.ActiveBeltTensioner
{
    /// <summary>A single motor on the RS485 bus; communicates exclusively via its <see cref="MotorBus"/></summary>
    public class Motor : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void Notify([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // -----------------------------------------------------------------------------------------
        // Protocol constants
        // -----------------------------------------------------------------------------------------

        private const byte  TorqueMode             = 0x01;
        private const short TorqueLimit            = 12000;
        private const short MaxConsecutiveFailures = 10;

        // -----------------------------------------------------------------------------------------
        // Identity
        // -----------------------------------------------------------------------------------------

        public byte   Id    { get; }
        public string Label { get; }

        // -----------------------------------------------------------------------------------------
        // Observable state (bound by DeviceControl.xaml)
        // -----------------------------------------------------------------------------------------

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            private set { if (_isConnected != value) { _isConnected = value; Notify(); } }
        }

        private string _status = SLoc.GetValue("SABT_Status_Disconnected");
        public string Status
        {
            get => _status;
            set { if (_status != value) { _status = value; Notify(); } }
        }

        private string _graphic = MotorGraphic.Disconnected;
        public string Graphic
        {
            get => _graphic;
            set { if (_graphic != value) { _graphic = value; Notify(); } }
        }

        // -----------------------------------------------------------------------------------------
        // Private state
        // -----------------------------------------------------------------------------------------

        private readonly MotorBus _bus;
        private int    _failures;
        private double _smoothedTorque;

        // -----------------------------------------------------------------------------------------
        // Construction
        // -----------------------------------------------------------------------------------------

        public Motor(MotorBus bus, byte id, string label)
        {
            _bus  = bus;
            Id    = id;
            Label = label;
        }

        // -----------------------------------------------------------------------------------------
        // Operations
        // -----------------------------------------------------------------------------------------

        /// <summary>Verifies the motor is reachable and in torque mode; sets torque mode if not</summary>
        public bool Check()
        {
            IsConnected      = false;
            _smoothedTorque  = 0;

            if (!_bus.IsOpen)
            {
                Status  = SLoc.GetValue("SABT_Status_NoDeviceDetected");
                Graphic = MotorGraphic.Disconnected;
                return false;
            }

            if (Query(requireTorqueMode: false))
            {
                Status  = SLoc.GetValue("SABT_Status_CheckingMode");
                Graphic = MotorGraphic.Communicating;

                if (Query(requireTorqueMode: true))
                {
                    IsConnected = true;
                    Status  = SLoc.GetValue("SABT_Status_Connected");
                    Graphic = MotorGraphic.Connected;
                    return true;
                }

                Status = SLoc.GetValue("SABT_Status_SettingMode");

                if (SetMode(TorqueMode))
                {
                    IsConnected = true;
                    Status  = SLoc.GetValue("SABT_Status_Connected");
                    Graphic = MotorGraphic.Connected;
                    return true;
                }
            }

            IsConnected = false;
            Status  = SLoc.GetValue("SABT_Status_CommunicationFailure");
            Graphic = MotorGraphic.Error;
            return false;
        }

        /// <summary>Sends zero torque until the motor acknowledges, then marks it disconnected</summary>
        public bool Stop()
        {
            IsConnected     = false;
            _smoothedTorque = 0;
            Status  = SLoc.GetValue("SABT_Status_Stopping");
            Graphic = MotorGraphic.Communicating;

            byte[] tx = MotorBus.BuildFrame(Id, 0x64);
            byte[] rx = new byte[10];

            for (int i = 0; i < 5; i++)
            {
                if (!_bus.Send(tx, rx)) continue;
                
                Status  = SLoc.GetValue("SABT_Status_Disconnected");
                Graphic = MotorGraphic.Disconnected;
                return true;
            }

            Status  = SLoc.GetValue("SABT_Status_CommunicationFailure");
            Graphic = MotorGraphic.Error;
            return false;
        }

        /// <summary>Sends a status request and validates the response</summary>
        private bool Query(bool requireTorqueMode)
        {
            byte[] tx = MotorBus.BuildFrame(Id, 0x74);
            byte[] rx = new byte[10];

            if (!_bus.Send(tx, rx, timeoutMs: 300, validate: true, log: true))
                return false;

            if (rx[0] != Id)                              return false;
            if (requireTorqueMode && rx[1] != TorqueMode) return false;
            if (rx[6] >= 60)                              return false; // temperature guard
            if (rx[8] != 0x00)                            return false; // error byte

            return true;
        }

        /// <summary>Changes the operating mode and confirms via <see cref="Query"/></summary>
        private bool SetMode(byte mode)
        {
            byte[] tx = MotorBus.BuildFrame(Id, 0xA0, b7: mode);
            byte[] rx = new byte[10];

            _bus.Send(tx, rx, timeoutMs: 200, validate: false, log: true);

            return Query(requireTorqueMode: true);
        }

        /// <summary>Broadcasts the ID-set command five times then verifies the motor responds at the new identifier</summary>
        /// <remarks>Per Waveshare docs, only ONE motor may be on the bus when this is called — the command is unaddressed and every connected motor will adopt the given ID</remarks>
        /// <returns>Whether the motor responded as expected at its new identifier</returns>
        public bool SetIdentifier()
        {
            Status  = SLoc.GetValue("SABT_Status_SettingIdentifier");
            Graphic = MotorGraphic.Communicating;

            byte[] tx = MotorBus.BuildFrame(0xAA, 0x55, 0x53, Id, b7: 0x00);
            byte[] rx = new byte[10];

            for (int i = 0; i < 5; i++)
            {
                _bus.Flush();
                _bus.Send(tx, rx, timeoutMs: 100, validate: false, log: true);
            }

            Thread.Sleep(500);

            if (Query(requireTorqueMode: false) && SetMode(TorqueMode))
            {
                Status  = SLoc.GetValue("SABT_Status_IdentifierSet");
                Graphic = MotorGraphic.Connected;
                return true;
            }

            Status  = SLoc.GetValue("SABT_Status_CommunicationFailure");
            Graphic = MotorGraphic.Error;
            return false;
        }

        /// <summary>Sends a series of torque commands to oscillate the motor, while updating its status indicators</summary>
        /// <returns>Whether the motor responded as expected</returns>
        public bool Test(int times = 10, double testTorque = 0.4)
        {
            Status  = SLoc.GetValue("SABT_Status_Testing");
            Graphic = MotorGraphic.Communicating;

            if (!Query(requireTorqueMode: true))
            {
                IsConnected = false;
                Status  = SLoc.GetValue("SABT_Status_TestFailed");
                Graphic = MotorGraphic.Error;
                return false;
            }

            int   direction = Label == "Left" ? -1 : 1;
            int   good      = 0;
            int   bad       = 0;
            short torque    = 0;

            for (int i = 0; i < times; i++)
            {
                byte[] tx = MotorBus.BuildFrame(Id, 0x64, (byte)((torque >> 8) & 0xFF), (byte)(torque & 0xFF));
                byte[] rx = new byte[10];

                if (_bus.Send(tx, rx, timeoutMs: 20, validate: true, log: true))
                    good++;
                else
                    bad++;

                Thread.Sleep(150);

                torque = (short)(torque != 0 ? 0 : testTorque * direction * TorqueLimit);
            }

            if (bad > 0 && good < 1)
            {
                IsConnected = false;
                Status  = SLoc.GetValue("SABT_Status_TestFailed");
                Graphic = MotorGraphic.Error;
                return false;
            }

            IsConnected = true;
            Status  = bad > 0
                ? SLoc.GetValue("SABT_Status_TestPartiallyFailed")
                : SLoc.GetValue("SABT_Status_TestPassed");
            Graphic = MotorGraphic.Connected;
            return true;
        }

        /// <summary>Sends a torque command as a fraction of maximum torque, with optional smoothing</summary>
        public bool SetTorque(double torque, double smoothing = 0.0)
        {
            _smoothedTorque = torque * (1.0 - smoothing) + _smoothedTorque * smoothing;

            short clamped = Clamp(
                (short)(_smoothedTorque * TorqueLimit * -1.0),
                (-TorqueLimit),
                TorqueLimit
            );

            byte[] tx = MotorBus.BuildFrame(Id, 0x64, (byte)((clamped >> 8) & 0xFF), (byte)(clamped & 0xFF));
            byte[] rx = new byte[10];

            if (!_bus.Send(tx, rx, timeoutMs: 10))
            {
                _failures++;
                Logging.Current.Warn($"SABT: {Label} motor communication failure ({_failures}/{MaxConsecutiveFailures})");
                return _failures < MaxConsecutiveFailures;
            }

            _failures = 0;
            return true;
        }

        /// <summary>Restricts the given value to the given range</summary>
        private static short Clamp(short v, short min, short max) =>
            v < min ? min : v > max ? max : v;
    }
}
