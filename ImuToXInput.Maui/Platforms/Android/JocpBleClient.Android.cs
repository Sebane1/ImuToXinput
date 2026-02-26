using Android.Bluetooth;
using Android.Bluetooth.LE;
using Android.Content;
using Android.OS;
using Java.Util;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Dispatching;
using ImuToXInput.Core.Output;

namespace ImuToXInput.Maui.Platforms.Android;

/// <summary>
/// Connects directly to a Joypad dongle over BLE and streams the 12-byte Xbox 360 report at ~125 Hz.
/// The dongle exposes the JOCP GATT service (report write + feedback notify). No proxy device.
/// </summary>
public sealed class JocpBleClient
{
    public static JocpBleClient? Current { get; set; }

    private const int SendIntervalMs = 8;
    private const int ReconnectIntervalMs = 2000;

    private BluetoothAdapter? _adapter;
    private BluetoothGatt? _gatt;
    private BluetoothGattCharacteristic? _reportChar;
    private Func<byte[]>? _getReport;
    private Thread? _sendThread;
    private volatile bool _stopSend;
    private volatile bool _disposed;
    private bool _autoReconnect;
    private string? _desiredAddress;
    private IDispatcherTimer? _reconnectTimer;
    private readonly object _writeLock = new();
    private ScanCallback? _storedScanCallback;

    public bool IsActive => _gatt != null && _reportChar != null && _sendThread?.IsAlive == true;
    public bool IsReconnecting => _reconnectTimer != null;
    public string? DeviceName { get; private set; }
    public string? DeviceAddress { get; private set; }

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<IReadOnlyList<JocpBleDevice>>? DevicesDiscovered;
    /// <summary>Fired when the dongle sends a rumble notification (feedback characteristic, 6-byte payload).</summary>
    public event EventHandler<JocpRumbleEventArgs>? RumbleReceived;

    public void SetReportProvider(Func<byte[]> getReport) => _getReport = getReport;

    private void OnFeedbackPayload(object? value)
    {
        byte[]? arr = value is byte[] b ? b : (value as IList<byte>)?.ToArray();
        if (arr == null || arr.Length < JocpBleConstants.RumblePayloadSize) return;
        ushort durationMs = (ushort)(arr[4] | (arr[5] << 8));
        var args = new JocpRumbleEventArgs(arr[0], arr[1], arr[2], arr[3], durationMs);
        try { RumbleReceived?.Invoke(this, args); } catch { }
    }

    /// <summary>Scan for BLE devices advertising the JOCP service (Joypad dongles). Call from main thread.</summary>
    public void StartScan(int timeoutMs = 10000)
    {
        Stop();
        var ctx = Platform.CurrentActivity?.ApplicationContext ?? global::Android.App.Application.Context;
        if (ctx == null) return;
        var manager = (BluetoothManager?)ctx.GetSystemService(Context.BluetoothService);
        _adapter = manager?.Adapter;
        if (_adapter == null || !_adapter.IsEnabled) return;

        var scanner = _adapter.BluetoothLeScanner;
        if (scanner == null) return;

        var list = new List<JocpBleDevice>();
        var serviceUuid = UUID.FromString(JocpBleConstants.ServiceUuid.ToString("D"));
        var filterBuilder = new ScanFilter.Builder()?.SetServiceUuid(new ParcelUuid(serviceUuid));
        var filter = filterBuilder?.Build();
        var filters = filter != null ? new List<ScanFilter> { filter } : new List<ScanFilter>();
        var settings = new ScanSettings.Builder()?.SetScanMode(global::Android.Bluetooth.LE.ScanMode.LowLatency)?.Build() ?? new ScanSettings.Builder().Build();

        var callback = new ScanCallbackImpl(
            onResult: result =>
            {
                var device = result.Device;
                var name = device.Name ?? "Unknown";
                var addr = device.Address ?? "";
                if (string.IsNullOrEmpty(addr)) return;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (list.All(d => d.Address != addr))
                    {
                        list.Add(new JocpBleDevice(name, addr));
                        DevicesDiscovered?.Invoke(this, list.ToArray());
                    }
                });
            },
            onScanFailed: _ => { }
        );

        _storedScanCallback = callback;
        scanner.StartScan(filters, settings, callback);
        Task.Run(async () =>
        {
            await Task.Delay(timeoutMs);
            MainThread.BeginInvokeOnMainThread(StopScan);
        });
    }

    public void StopScan()
    {
        try
        {
            var cb = _storedScanCallback;
            _storedScanCallback = null;
            if (cb != null)
                _adapter?.BluetoothLeScanner?.StopScan(cb);
        }
        catch { }
    }

    /// <summary>Connect to a Joypad dongle by address and start streaming. Auto-reconnects on drop until Stop().</summary>
    public void Connect(string address)
    {
        Stop();
        if (_getReport == null || string.IsNullOrWhiteSpace(address)) return;

        _desiredAddress = address.Trim();
        _autoReconnect = true;
        TryConnect();
    }

    private void TryConnect()
    {
        if (_desiredAddress == null || _getReport == null) return;

        var ctx = Platform.CurrentActivity?.ApplicationContext ?? global::Android.App.Application.Context;
        if (ctx == null) return;
        var manager = (BluetoothManager?)ctx.GetSystemService(Context.BluetoothService);
        var adapter = manager?.Adapter;
        if (adapter == null) return;

        try
        {
            var device = adapter.GetRemoteDevice(_desiredAddress);
            if (device == null) return;

            _adapter = adapter;
            var feedbackCharUuid = UUID.FromString(JocpBleConstants.FeedbackCharacteristicUuid.ToString("D"));
            var gattCallback = new GattCallbackImpl(
                onConnectionStateChange: (gatt, status, newState) =>
                {
                    if (newState == (int)ProfileState.Disconnected)
                    {
                        MainThread.BeginInvokeOnMainThread(() => DropConnection());
                        return;
                    }
                    if (newState == (int)ProfileState.Connected && status == GattStatus.Success)
                    {
                        gatt?.DiscoverServices();
                    }
                },
                onServicesDiscovered: (gatt, status) =>
                {
                    if (status != GattStatus.Success || gatt == null) return;
                    var serviceUuid = UUID.FromString(JocpBleConstants.ServiceUuid.ToString("D"));
                    var reportCharUuid = UUID.FromString(JocpBleConstants.ReportCharacteristicUuid.ToString("D"));
                    var service = gatt.GetService(serviceUuid);
                    var reportChar = service?.GetCharacteristic(reportCharUuid);
                    if (reportChar == null) return;

                    var feedbackChar = service?.GetCharacteristic(feedbackCharUuid);
                    if (feedbackChar != null)
                    {
                        gatt.SetCharacteristicNotification(feedbackChar, true);
                        var desc = feedbackChar.GetDescriptor(UUID.FromString("00002902-0000-1000-8000-00805f9b34fb"));
                        var enableValue = BluetoothGattDescriptor.EnableNotificationValue;
                        desc?.SetValue(enableValue is byte[] arr ? arr : (enableValue as IList<byte>)?.ToArray());
                        try { gatt.WriteDescriptor(desc); } catch { }
                    }

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _gatt = gatt;
                        _reportChar = reportChar;
                        _reportChar!.WriteType = GattWriteType.NoResponse;
                        DeviceName = device.Name;
                        DeviceAddress = device.Address;
                        StartSendLoop();
                        StopReconnectTimer();
                        Current = this;
                        if (!_disposed)
                            ConnectionStateChanged?.Invoke(this, true);
                    });
                },
                onCharacteristicChanged: OnFeedbackPayload
            );

            device.ConnectGatt(ctx, false, gattCallback, BluetoothTransports.Le);
        }
        catch
        {
            MainThread.BeginInvokeOnMainThread(StartReconnectTimer);
        }
    }

    private void StartSendLoop()
    {
        _stopSend = false;
        _sendThread = new Thread(() =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!_stopSend && _gatt != null && _reportChar != null && _getReport != null)
            {
                try
                {
                    var report = _getReport();
                    if (report.Length >= JocpBleConstants.ReportSizeBytes)
                    {
                        var buf = report.Length == JocpBleConstants.ReportSizeBytes
                            ? report
                            : report.AsSpan(0, JocpBleConstants.ReportSizeBytes).ToArray();
                        lock (_writeLock)
                        {
                            _reportChar?.SetValue(buf);
                            _gatt?.WriteCharacteristic(_reportChar);
                        }
                    }
                }
                catch { /* ignore */ }
                var elapsed = (int)sw.ElapsedMilliseconds;
                var delay = SendIntervalMs - elapsed;
                if (delay > 0)
                    Thread.Sleep(delay);
                sw.Restart();
            }
        })
        { IsBackground = true, Name = "JocpBleSend" };
        _sendThread.Start();
    }

    private void DropConnection()
    {
        _stopSend = true;
        try { _sendThread?.Join(300); } catch { }
        _sendThread = null;
        try
        {
            _gatt?.Close();
            _gatt?.Disconnect();
        }
        catch { }
        _gatt = null;
        _reportChar = null;
        if (Current == this)
            Current = null;
        if (!_disposed)
            ConnectionStateChanged?.Invoke(this, false);
        if (_autoReconnect && !_disposed && _desiredAddress != null)
            StartReconnectTimer();
    }

    private void StartReconnectTimer()
    {
        StopReconnectTimer();
        var dispatcher = Microsoft.Maui.Controls.Application.Current?.Dispatcher;
        if (dispatcher == null) return;
        _reconnectTimer = dispatcher.CreateTimer();
        _reconnectTimer.Interval = TimeSpan.FromMilliseconds(ReconnectIntervalMs);
        _reconnectTimer.Tick += (_, _) =>
        {
            if (!_autoReconnect || _disposed || _desiredAddress == null) return;
            if (IsActive)
                StopReconnectTimer();
            else
                TryConnect();
        };
        _reconnectTimer.Start();
    }

    private void StopReconnectTimer()
    {
        _reconnectTimer?.Stop();
        _reconnectTimer = null;
    }

    public void Stop()
    {
        _autoReconnect = false;
        StopReconnectTimer();
        StopScan();
        _stopSend = true;
        try { _sendThread?.Join(500); } catch { }
        _sendThread = null;
        try
        {
            _gatt?.Close();
            _gatt?.Disconnect();
        }
        catch { }
        _gatt = null;
        _reportChar = null;
        DeviceName = null;
        DeviceAddress = null;
        _desiredAddress = null;
        if (Current == this)
            Current = null;
        if (!_disposed)
            ConnectionStateChanged?.Invoke(this, false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    public sealed record JocpBleDevice(string Name, string Address);

    private sealed class ScanCallbackImpl : ScanCallback
    {
        private readonly Action<ScanResult> _onResult;
        private readonly Action<ScanFailure> _onFail;

        public ScanCallbackImpl(Action<ScanResult> onResult, Action<ScanFailure> onScanFailed)
        {
            _onResult = onResult;
            _onFail = onScanFailed;
        }

        public override void OnScanResult(ScanCallbackType callbackType, ScanResult? result)
        {
            if (result != null) _onResult(result);
        }

        public override void OnScanFailed(ScanFailure errorCode)
        {
            _onFail(errorCode);
        }
    }

    private sealed class GattCallbackImpl : BluetoothGattCallback
    {
        private readonly Action<BluetoothGatt?, GattStatus, int> _onConnectionStateChange;
        private readonly Action<BluetoothGatt?, GattStatus> _onServicesDiscovered;
        private readonly Action<object?> _onCharacteristicChanged;

        public GattCallbackImpl(
            Action<BluetoothGatt?, GattStatus, int> onConnectionStateChange,
            Action<BluetoothGatt?, GattStatus> onServicesDiscovered,
            Action<object?> onCharacteristicChanged)
        {
            _onConnectionStateChange = onConnectionStateChange;
            _onServicesDiscovered = onServicesDiscovered;
            _onCharacteristicChanged = onCharacteristicChanged;
        }

        public override void OnConnectionStateChange(BluetoothGatt? gatt, GattStatus status, ProfileState newState)
        {
            _onConnectionStateChange(gatt, status, (int)newState);
        }

        public override void OnServicesDiscovered(BluetoothGatt? gatt, GattStatus status)
        {
            _onServicesDiscovered(gatt, status);
        }

        public override void OnCharacteristicChanged(BluetoothGatt? gatt, BluetoothGattCharacteristic? characteristic, byte[]? value)
        {
            _onCharacteristicChanged((object?)value);
        }
    }
}
