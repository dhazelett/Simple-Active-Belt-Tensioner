using SimHub;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace User.ActiveBeltTensioner
{
    /// <summary>Owns the RS485 serial port and all frame-level protocol operations</summary>
    public class MotorBus : IDisposable
    {
        private SerialPort _port;
        private readonly object _lock = new object();

        public bool IsOpen => _port?.IsOpen == true;

        // -----------------------------------------------------------------------------------------
        // Connection lifecycle
        // -----------------------------------------------------------------------------------------

        /// <summary>Opens the given serial port at the given baud rate, retrying up to ten times on access failures</summary>
        /// <returns>Whether the port was successfully opened</returns>
        public bool Connect(string portName, int baudRate = 115200)
        {
            lock (_lock)
            {
                if (_port?.IsOpen == true) return true;

                try
                {
                    _port?.Dispose();
                    _port = new SerialPort(portName, baudRate)
                    {
                        Parity      = Parity.None,
                        StopBits    = StopBits.One,
                        ReadTimeout = 10,
                        WriteTimeout = 100,
                        DtrEnable   = false,
                        RtsEnable   = false,
                        NewLine     = "\n"
                    };

                    const int retries = 10;

                    for (int i = 0; i < retries; i++)
                    {
                        try
                        {
                            _port.Open();
                            return true;
                        }
                        catch (UnauthorizedAccessException)
                        {
                            _port.Close();
                            Logging.Current.Warn($"SABT: Serial port opening failure ({i + 1}/{retries} retries)");
                            Thread.Sleep(100);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.Current.Warn($"SABT: Unexpected serial error: {ex.Message}");
                }

                _port?.Dispose();
                _port = null;
                return false;
            }
        }

        /// <summary>Closes and releases the serial port</summary>
        public void Disconnect()
        {
            lock (_lock)
            {
                try   { _port?.Close(); _port?.Dispose(); }
                catch { Logging.Current.Warn("SABT: Serial port release failure"); }
                finally { _port = null; }
            }
        }

        /// <summary>An alias of <see cref="Disconnect()" /> for the purposes of fulfilling the <see cref="IDisposable" /> interface</summary>
        public void Dispose() => Disconnect();

        // -----------------------------------------------------------------------------------------
        // Bus scanning
        // -----------------------------------------------------------------------------------------

        /// <summary>Queries IDs 1–<paramref name="maxId"/> and returns every ID that replies with a valid frame</summary>
        public List<byte> ScanBus(byte maxId = 4)
        {
            var responders = new List<byte>();

            for (byte id = 0; id <= maxId; id++)
            {
                Flush();
                byte[] tx = BuildFrame(id, 0x74);
                byte[] rx = new byte[10];

                if (Send(tx, rx, timeoutMs: 300, validate: true, log: true) && rx[0] == id)
                    responders.Add(id);
            }

            return responders;
        }

        // -----------------------------------------------------------------------------------------
        // Low-level I/O
        // -----------------------------------------------------------------------------------------

        /// <summary>Discards any bytes currently waiting in the serial receive buffer</summary>
        internal void Flush()
        {
            if (_port == null) return;

            lock (_lock)
            {
                try { while (_port.BytesToRead > 0) _port.ReadByte(); }
                catch { }
            }
        }

        /// <summary>Writes <paramref name="tx"/> and blocks until a 10-byte reply is received or the timeout elapses</summary>
        internal bool Send(byte[] tx, byte[] rx, int timeoutMs = 10, bool validate = true, bool log = false)
        {
            if (!IsOpen)
            {
                Logging.Current.Warn("SABT: Serial port is not available or not open");
                return false;
            }

            lock (_lock)
            {
                try
                {
                    while (_port.BytesToRead > 0) _port.ReadByte();
                    _port.Write(tx, 0, tx.Length);
                }
                catch { return false; }

                if (log) Logging.Current.Info("SABT: TX (" + BitConverter.ToString(tx) + ")");

                long start       = System.Diagnostics.Stopwatch.GetTimestamp();
                long limitTicks  = (long)(timeoutMs * System.Diagnostics.Stopwatch.Frequency / 1000.0);
                int  received    = 0;

                while (received < 10)
                {
                    try
                    {
                        int b = _port.ReadByte();
                        if (b >= 0) rx[received++] = (byte)b;
                    }
                    catch (TimeoutException) { }

                    if (System.Diagnostics.Stopwatch.GetTimestamp() - start > limitTicks)
                    {
                        Array.Clear(rx, 0, rx.Length);
                        return false;
                    }
                }
            }

            if (log) Logging.Current.Info("SABT: RX (" + BitConverter.ToString(rx) + ")");

            if (validate)
            {
                byte expected = Checksum(rx, 9);

                if (rx[9] != expected)
                {
                    Logging.Current.Warn($"SABT: Bad checksum ({rx[9]:X2} != {expected:X2})");
                    return false;
                }
            }

            return true;
        }

        // -----------------------------------------------------------------------------------------
        // Frame construction
        // -----------------------------------------------------------------------------------------

        /// <summary>Constructs a 10-byte frame that can be understood by the motor controller, computing the checksum automatically unless overridden</summary>
        /// <returns>The byte array of the constructed frame</returns>
        internal static byte[] BuildFrame(
            byte id, byte command,
            byte b0 = 0, byte b1 = 0, byte b2 = 0, byte b3 = 0,
            byte b4 = 0, byte b5 = 0, byte b6 = 0, byte? b7 = null)
        {
            byte[] frame = new byte[10];
            frame[0] = id;      frame[1] = command;
            frame[2] = b0;      frame[3] = b1;      frame[4] = b2;  frame[5] = b3;
            frame[6] = b4;      frame[7] = b5;      frame[8] = b6;
            frame[9] = b7 ?? Checksum(frame, 9);
            return frame;
        }

        /// <summary>Determines the CRC-8 checksum byte for the given frame data</summary>
        /// <returns>The computed checksum byte</returns>
        private static byte Checksum(byte[] data, int length)
        {
            byte cs = 0;

            for (int i = 0; i < length; i++)
            {
                cs ^= data[i];

                for (int b = 0; b < 8; b++)
                    cs = (cs & 1) != 0 ? (byte)((cs >> 1) ^ 0x8C) : (byte)(cs >> 1);
            }

            return cs;
        }
    }
}
