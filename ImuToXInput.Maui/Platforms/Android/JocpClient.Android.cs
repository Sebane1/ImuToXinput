using System.Net;
using System.Net.Sockets;
using Microsoft.Maui.Dispatching;
using ImuToXInput.Core.Output;

namespace ImuToXInput.Maui.Platforms.Android;

/// <summary>
/// Sends JOCP INPUT packets over UDP to Joypad OS (e.g. 192.168.4.1:30100)
/// and receives JOCP output (rumble, LED) over TCP control channel (port 30101).
/// Uses the same report source as BLE (BleGamepadOutput); call SetReportProvider and Start.
/// Auto-reconnects when the connection drops (e.g. WiFi) until the user disconnects.
/// </summary>
public sealed class JocpClient
{
    public static JocpClient? Current { get; set; }

    /// <summary>TCP port for JOCP control channel (output: rumble, LED).</summary>
    public const int ControlPortDefault = 30101;

    private const int SendIntervalMs = 8; // ~125 Hz
    private const int ReconnectIntervalMs = 2000; // try every 2s when dropped

    private UdpClient? _udp;
    private TcpClient? _tcpControl;
    private NetworkStream? _tcpStream;
    private Thread? _tcpReadThread;
    private volatile bool _tcpReadStop;
    private IPEndPoint? _remote;
    private IDispatcherTimer? _timer;
    private IDispatcherTimer? _reconnectTimer;
    private Func<byte[]>? _getReport;
    private ushort _sequence;
    private bool _disposed;
    private string? _desiredHost;
    private int _desiredPort;
    private bool _autoReconnect;

    public bool IsActive => _udp != null && _timer != null;
    /// <summary>True when we are attempting to reconnect after a drop.</summary>
    public bool IsReconnecting => _reconnectTimer != null;
    public string? Host { get; private set; }
    public int Port { get; private set; }

    /// <summary>Fired when send loop stops (e.g. after Stop or error) or when reconnected.</summary>
    public event EventHandler<bool>? ConnectionStateChanged;

    /// <summary>Fired when the dongle sends a rumble command over the TCP control channel.</summary>
    public event EventHandler<JocpRumbleEventArgs>? RumbleReceived;

    /// <summary>Provide the 12-byte report to send each tick (e.g. BleGamepadOutput.Instance.GetReportBytes).</summary>
    public void SetReportProvider(Func<byte[]> getReport)
    {
        _getReport = getReport;
    }

    /// <summary>Start sending JOCP packets to host:port. Default port 30100. Auto-reconnects on drop until Stop().</summary>
    public void Start(string host, int port = 30100)
    {
        Stop();
        if (_getReport == null)
            return;

        _desiredHost = host.Trim();
        _desiredPort = port;
        _autoReconnect = true;

        if (TryConnect())
        {
            Current = this;
            if (!_disposed)
                ConnectionStateChanged?.Invoke(this, true);
        }
        else
            StartReconnectTimer();
    }

    /// <summary>Stop sending and stop auto-reconnect.</summary>
    public void Stop()
    {
        _autoReconnect = false;
        StopReconnectTimer();
        StopTimer();
        _tcpReadStop = true;
        try { _tcpStream?.Close(); } catch { }
        try { _tcpControl?.Close(); _tcpControl?.Dispose(); } catch { }
        _tcpStream = null;
        _tcpControl = null;
        try { _tcpReadThread?.Join(500); } catch { }
        _tcpReadThread = null;
        try
        {
            _udp?.Close();
            _udp?.Dispose();
        }
        catch { }
        _udp = null;
        _remote = null;
        Host = null;
        Port = 0;
        _desiredHost = null;
        _desiredPort = 0;
        if (Current == this)
            Current = null;
        if (!_disposed)
            ConnectionStateChanged?.Invoke(this, false);
    }

    private bool TryConnect()
    {
        if (_desiredHost == null || _getReport == null)
            return false;
        try
        {
            IPAddress? addr = null;
            if (IPAddress.TryParse(_desiredHost, out var parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
                addr = parsed;
            if (addr == null)
                addr = Dns.GetHostAddresses(_desiredHost).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (addr == null)
                return false;

            _remote = new IPEndPoint(addr, _desiredPort);
            _udp = new UdpClient(AddressFamily.InterNetwork);
            _udp.Connect(_remote);
            Host = _desiredHost;
            Port = _desiredPort;
            _sequence = 0;

            // TCP control channel (port 30101) for output: rumble, LED
            try
            {
                _tcpControl = new TcpClient(AddressFamily.InterNetwork);
                _tcpControl.Connect(addr, ControlPortDefault);
                _tcpControl.ReceiveTimeout = 5000;
                _tcpStream = _tcpControl.GetStream();
                _tcpReadStop = false;
                _tcpReadThread = new Thread(TcpOutputReadLoop) { IsBackground = true, Name = "JocpTcpOutput" };
                _tcpReadThread.Start();
            }
            catch
            {
                try { _tcpStream?.Close(); _tcpControl?.Close(); } catch { }
                _tcpStream = null;
                _tcpControl = null;
                // Continue without control channel (input still works)
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                _timer = dispatcher.CreateTimer();
                _timer.Interval = TimeSpan.FromMilliseconds(SendIntervalMs);
                _timer.Tick += OnSendTick;
                _timer.Start();
            }
            return true;
        }
        catch
        {
            try { _udp?.Dispose(); } catch { }
            try { _tcpStream?.Close(); _tcpControl?.Close(); } catch { }
            _udp = null;
            _remote = null;
            _tcpStream = null;
            _tcpControl = null;
            return false;
        }
    }

    private void TcpOutputReadLoop()
    {
        var header = new byte[JocpOutputConstants.HeaderSize];
        var payload = new byte[8]; // max payload 6 for rumble
        if (_tcpStream == null) return;
        try
        {
            while (!_tcpReadStop && _tcpStream.CanRead)
            {
                if (!ReadExactly(_tcpStream, header, 0, header.Length)) break;
                ushort magic = (ushort)(header[0] | (header[1] << 8));
                byte msgType = header[3];
                if (magic != JocpOutputConstants.Magic || msgType != JocpOutputConstants.MsgTypeOutputCmd)
                    continue;
                int cmdByte = _tcpStream.ReadByte();
                if (cmdByte < 0) break;
                byte cmd = (byte)cmdByte;
                int payloadLen = cmd switch
                {
                    JocpOutputConstants.CmdRumble => JocpOutputConstants.RumblePayloadSize,
                    JocpOutputConstants.CmdPlayerLed => JocpOutputConstants.PlayerLedPayloadSize,
                    JocpOutputConstants.CmdRgbLed => JocpOutputConstants.RgbLedPayloadSize,
                    _ => 0
                };
                if (payloadLen > 0 && payloadLen <= payload.Length && ReadExactly(_tcpStream, payload, 0, payloadLen))
                {
                    if (cmd == JocpOutputConstants.CmdRumble && payloadLen >= 6)
                    {
                        ushort durationMs = (ushort)(payload[4] | (payload[5] << 8));
                        var args = new JocpRumbleEventArgs(payload[0], payload[1], payload[2], payload[3], durationMs);
                        try { RumbleReceived?.Invoke(this, args); } catch { }
                    }
                }
            }
        }
        catch { }
        _tcpReadStop = true;
    }

    private static bool ReadExactly(Stream stream, byte[] buffer, int offset, int count)
    {
        int read = 0;
        while (read < count)
        {
            int n = stream.Read(buffer, offset + read, count - read);
            if (n <= 0) return false;
            read += n;
        }
        return true;
    }

    private void DropConnection()
    {
        StopTimer();
        _tcpReadStop = true;
        try { _tcpStream?.Close(); } catch { }
        try { _tcpControl?.Close(); _tcpControl?.Dispose(); } catch { }
        _tcpStream = null;
        _tcpControl = null;
        try { _tcpReadThread?.Join(300); } catch { }
        _tcpReadThread = null;
        try
        {
            _udp?.Close();
            _udp?.Dispose();
        }
        catch { }
        _udp = null;
        _remote = null;
        if (Current == this)
            Current = null;
        if (!_disposed)
            ConnectionStateChanged?.Invoke(this, false);
        if (_autoReconnect && !_disposed && _desiredHost != null)
            StartReconnectTimer();
    }

    private void StartReconnectTimer()
    {
        StopReconnectTimer();
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;
        _reconnectTimer = dispatcher.CreateTimer();
        _reconnectTimer.Interval = TimeSpan.FromMilliseconds(ReconnectIntervalMs);
        _reconnectTimer.Tick += OnReconnectTick;
        _reconnectTimer.Start();
    }

    private void StopReconnectTimer()
    {
        if (_reconnectTimer == null) return;
        _reconnectTimer.Stop();
        _reconnectTimer.Tick -= OnReconnectTick;
        _reconnectTimer = null;
    }

    private void OnReconnectTick(object? sender, EventArgs e)
    {
        if (!_autoReconnect || _disposed || _desiredHost == null) return;
        if (TryConnect())
        {
            StopReconnectTimer();
            Current = this;
            if (!_disposed)
                ConnectionStateChanged?.Invoke(this, true);
        }
    }

    private void StopTimer()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Tick -= OnSendTick;
            _timer = null;
        }
    }

    private void OnSendTick(object? sender, EventArgs e)
    {
        if (_udp == null || _remote == null || _getReport == null) return;
        try
        {
            var report = _getReport();
            if (report.Length < 12) return;
            var packet = JocpPacket.BuildFromXbox360Report(report.AsSpan(0, 12), _sequence, (uint)Environment.TickCount64);
            _udp.Send(packet, packet.Length);
            _sequence++;
        }
        catch
        {
            MainThread.BeginInvokeOnMainThread(DropConnection);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
