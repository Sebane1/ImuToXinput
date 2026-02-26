using System.Net;
using System.Net.Sockets;
using Android.Content;
using Android.OS;
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
    private static JocpClient? _instance;

    /// <summary>Single app-level instance so the connection survives navigation. Use this or Current.</summary>
    public static JocpClient GetOrCreate()
    {
        if (_instance == null)
        {
            _instance = new JocpClient();
            _instance.RumbleReceived += JocpRumbleHandler.OnRumble;
        }
        return _instance;
    }

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
    private Thread? _sendThread;
    private volatile bool _sendStop;
    private PowerManager.WakeLock? _sendWakeLock;
    private IDispatcherTimer? _reconnectTimer;
    private Func<byte[]>? _getReport;
    private ushort _sequence;
    private bool _disposed;
    private string? _desiredHost;
    private int _desiredPort;
    private bool _autoReconnect;

    public bool IsActive => _udp != null && _sendThread != null && !_sendStop;
    /// <summary>True when we are attempting to reconnect after a drop.</summary>
    public bool IsReconnecting => _reconnectTimer != null;
    /// <summary>True when TCP control channel (port 30101) is connected. Rumble and LED from dongle only work when this is true.</summary>
    public bool ControlChannelConnected => _tcpControl != null && _tcpControl.Connected;
    public string? Host { get; private set; }
    public int Port { get; private set; }

    /// <summary>Last connection or send error message for UI display. Cleared when connection succeeds.</summary>
    public string? LastError { get; private set; }

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
        Stop(notify: false); // avoid spurious "disconnected" before we try connecting
        if (_getReport == null)
            return;

        _desiredHost = host.Trim();
        _desiredPort = port;
        _autoReconnect = true;

        if (TryConnect())
        {
            LastError = null;
            Current = this;
            if (!_disposed)
                ConnectionStateChanged?.Invoke(this, true);
        }
        else
        {
            if (!_disposed)
                ConnectionStateChanged?.Invoke(this, false);
            StartReconnectTimer();
        }
    }

    /// <summary>Stop sending and stop auto-reconnect.</summary>
    /// <param name="notify">If true, fire ConnectionStateChanged(false). Use false when stopping only to restart (e.g. from Start()).</param>
    public void Stop(bool notify = true)
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
        if (notify)
        {
            LastError = null;
            if (!_disposed)
                ConnectionStateChanged?.Invoke(this, false);
        }
    }

    /// <summary>Called when send loop catches; store for UI and log.</summary>
    private void OnSendLoopException(Exception ex)
    {
        LastError = ex.Message;
        System.Diagnostics.Debug.WriteLine($"[JocpClient] Send loop error: {ex.GetType().Name}: {ex.Message}");
    }

    private bool TryConnect()
    {
        if (_desiredHost == null || _getReport == null)
        {
            LastError = "No host or report provider.";
            return false;
        }
        try
        {
            IPAddress? addr = null;
            if (IPAddress.TryParse(_desiredHost, out var parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
                addr = parsed;
            if (addr == null)
            {
                try
                {
                    addr = Dns.GetHostAddresses(_desiredHost).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                }
                catch (Exception ex)
                {
                    LastError = $"Could not resolve host: {ex.Message}";
                    return false;
                }
            }
            if (addr == null)
            {
                LastError = "Could not resolve host to an IPv4 address.";
                return false;
            }

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

            _sendStop = false;
            AcquireSendWakeLock();
            _sendThread = new Thread(SendLoop) { IsBackground = true, Name = "JocpSend" };
            _sendThread.Start();
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
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
#if DEBUG
                        System.Diagnostics.Debug.WriteLine($"[JocpClient] Rumble received: L={payload[0]} R={payload[2]} duration={durationMs}ms");
#endif
                        try { RumbleReceived?.Invoke(this, args); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[JocpClient] RumbleReceived handler error: {ex.Message}"); }
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
        _sendStop = true;
        try
        {
            _sendThread?.Join(500);
        }
        catch { }
        _sendThread = null;
        ReleaseSendWakeLock();
    }

    private void AcquireSendWakeLock()
    {
        try
        {
            var ctx = global::Android.App.Application.Context;
            if (ctx == null) return;
            var pm = (PowerManager?)ctx.GetSystemService(Context.PowerService);
            if (pm == null) return;
            ReleaseSendWakeLock();
            _sendWakeLock = pm.NewWakeLock(WakeLockFlags.Partial, "ImuToXInput::JocpSend");
            _sendWakeLock.SetReferenceCounted(false);
            _sendWakeLock.Acquire();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JocpClient] WakeLock acquire failed: {ex.Message}");
        }
    }

    private void ReleaseSendWakeLock()
    {
        try
        {
            if (_sendWakeLock?.IsHeld == true)
                _sendWakeLock.Release();
            _sendWakeLock = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JocpClient] WakeLock release failed: {ex.Message}");
        }
    }

    private long _lastDebugLogTick;
    private int _sendCountSinceLog;
    private bool _lastReportHadInput;

    private void SendLoop()
    {
        while (!_sendStop && _udp != null && _remote != null && _getReport != null)
        {
            try
            {
                var report = _getReport();
                if (report.Length >= 12)
                {
                    bool hasInput = report.AsSpan(0, 12).IndexOfAnyExcept((byte)0) >= 0;
                    var packet = JocpPacket.BuildFromXbox360Report(report.AsSpan(0, 12), _sequence, (uint)System.Environment.TickCount64);
                    _udp.Send(packet, packet.Length);
                    _sequence++;
#if DEBUG
                    _sendCountSinceLog++;
                    _lastReportHadInput |= hasInput;
                    var now = System.Environment.TickCount64;
                    if (now - _lastDebugLogTick >= 2000)
                    {
                        System.Diagnostics.Debug.WriteLine($"[JocpClient] Sending: {_sendCountSinceLog} packets/2s, report had input: {_lastReportHadInput}");
                        _lastDebugLogTick = now;
                        _sendCountSinceLog = 0;
                        _lastReportHadInput = false;
                    }
#endif
                }
            }
            catch (Exception ex)
            {
                OnSendLoopException(ex);
                MainThread.BeginInvokeOnMainThread(DropConnection);
                return;
            }
            Thread.Sleep(SendIntervalMs);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
