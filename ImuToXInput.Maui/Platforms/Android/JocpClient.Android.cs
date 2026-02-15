using System.Net;
using System.Net.Sockets;
using Microsoft.Maui.Dispatching;
using ImuToXInput.Core.Output;

namespace ImuToXInput.Maui.Platforms.Android;

/// <summary>
/// Sends JOCP INPUT packets over UDP to Joypad OS (e.g. 192.168.4.1:30100).
/// Uses the same report source as BLE (BleGamepadOutput); call SetReportProvider and Start.
/// </summary>
public sealed class JocpClient
{
    public static JocpClient? Current { get; set; }

    private const int SendIntervalMs = 8; // ~125 Hz

    private UdpClient? _udp;
    private IPEndPoint? _remote;
    private IDispatcherTimer? _timer;
    private Func<byte[]>? _getReport;
    private ushort _sequence;
    private bool _disposed;

    public bool IsActive => _udp != null && _timer != null;
    public string? Host { get; private set; }
    public int Port { get; private set; }

    /// <summary>Fired when send loop stops (e.g. after Stop or error).</summary>
    public event EventHandler<bool>? ConnectionStateChanged;

    /// <summary>Provide the 12-byte report to send each tick (e.g. BleGamepadOutput.Instance.GetReportBytes).</summary>
    public void SetReportProvider(Func<byte[]> getReport)
    {
        _getReport = getReport;
    }

    /// <summary>Start sending JOCP packets to host:port. Default port 30100.</summary>
    public void Start(string host, int port = 30100)
    {
        Stop();
        if (_getReport == null)
            return;

        try
        {
            IPAddress? addr = null;
            if (IPAddress.TryParse(host, out var parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
                addr = parsed;
            if (addr == null)
                addr = Dns.GetHostAddresses(host).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (addr == null)
                throw new InvalidOperationException("Could not resolve host.");
            _remote = new IPEndPoint(addr, port);
            _udp = new UdpClient(AddressFamily.InterNetwork);
            _udp.Connect(_remote);
            Host = host;
            Port = port;
            _sequence = 0;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                _timer = dispatcher.CreateTimer();
                _timer.Interval = TimeSpan.FromMilliseconds(SendIntervalMs);
                _timer.Tick += OnSendTick;
                _timer.Start();
            }

            Current = this;
            if (!_disposed)
                ConnectionStateChanged?.Invoke(this, true);
        }
        catch
        {
            Stop();
        }
    }

    /// <summary>Stop sending and close the socket.</summary>
    public void Stop()
    {
        StopTimer();
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
        if (Current == this)
            Current = null;
        if (!_disposed)
            ConnectionStateChanged?.Invoke(this, false);
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
            MainThread.BeginInvokeOnMainThread(Stop);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
