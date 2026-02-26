using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.ApplicationModel;

namespace ImuToXInput.Maui;

/// <summary>Display row for Joypad dongle (BLE) device list (shared, not Android-specific).</summary>
public record JocpBleDeviceRow(string Name, string Address)
{
    public string Display => string.IsNullOrEmpty(Name) ? Address : $"{Name}  ({Address})";
}

/// <summary>Display row for mDNS-discovered Joypad dongle (name, IP, port).</summary>
public record JocpMdnsDeviceRow(string DisplayName, string IpAddress, int Port)
{
    public string Display => string.IsNullOrEmpty(DisplayName) ? $"{IpAddress}:{Port}" : $"{DisplayName}  ({IpAddress}:{Port})";
}

public partial class DongleConnectPage : ContentPage
{
    private const string JocpLastHostKey = "JocpLastConnectedHost";

#if ANDROID
    private Platforms.Android.JocpClient? _jocpClient;
    private Platforms.Android.JocpBleClient? _bleClient;
#endif
    private bool _modeDirect = true;
    private readonly ObservableCollection<JocpBleDeviceRow> _bleDevices = new();
    private readonly ObservableCollection<JocpMdnsDeviceRow> _mdnsDevices = new();
    private string? _selectedBleAddress;
    private (string Ip, int Port)? _selectedMdns;

    public DongleConnectPage()
    {
        InitializeComponent();
        ListBleDevices.ItemsSource = _bleDevices;
        ListMdnsDevices.ItemsSource = _mdnsDevices;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        // Use app-level singleton so connection survives back navigation; bind to live client for status.
        _jocpClient = Platforms.Android.JocpClient.GetOrCreate();
        var liveBle = Platforms.Android.JocpBleClient.Current;
        if (liveBle != null) _bleClient = liveBle;
        if (_jocpClient != null)
            _jocpClient.ConnectionStateChanged += OnJocpConnectionStateChanged;
        if (_bleClient != null)
            _bleClient.ConnectionStateChanged += OnBleConnectionStateChanged;
        UpdateJocpStatus();
        if (_modeDirect)
        {
            var lastHost = Preferences.Default.Get(JocpLastHostKey, (string?)null);
            if (!string.IsNullOrWhiteSpace(lastHost))
                EntryJocpHost.Text = lastHost.Trim();
            _ = RunDiscoveryAsync();
        }
#endif
    }

    protected override void OnDisappearing()
    {
#if ANDROID
        if (_jocpClient != null)
            _jocpClient.ConnectionStateChanged -= OnJocpConnectionStateChanged;
        if (_bleClient != null)
            _bleClient.ConnectionStateChanged -= OnBleConnectionStateChanged;
#endif
        base.OnDisappearing();
    }

    private void OnJocpConnectionStateChanged(object? sender, bool connected) => MainThread.BeginInvokeOnMainThread(UpdateJocpStatus);
    private void OnBleConnectionStateChanged(object? sender, bool connected) => MainThread.BeginInvokeOnMainThread(UpdateJocpStatus);

    private void OnModeDirectClicked(object? sender, EventArgs e)
    {
        _modeDirect = true;
        PanelDirect.IsVisible = true;
        PanelBle.IsVisible = false;
        UpdateJocpStatus();
#if ANDROID
        _ = RunDiscoveryAsync();
#endif
        ScrollViewMain.ScrollToAsync(0, 0, false);
        MainStack?.InvalidateMeasure();
    }

    private void OnModeBleClicked(object? sender, EventArgs e)
    {
        _modeDirect = false;
        PanelDirect.IsVisible = false;
        PanelBle.IsVisible = true;
        UpdateJocpStatus();
        ScrollViewMain.ScrollToAsync(0, 0, false);
        MainStack?.InvalidateMeasure();
    }

    private void UpdateJocpStatus()
    {
#if ANDROID
        if (_modeDirect)
        {
            var client = _jocpClient ?? Platforms.Android.JocpClient.Current;
            bool active = client?.IsActive ?? false;
            bool reconnecting = client?.IsReconnecting ?? false;
            LblJocpStatus.Text = active ? $"Connected: {client!.Host}" : (reconnecting ? "Reconnecting…" : "Not connected");
            var err = client?.LastError;
            if (!string.IsNullOrEmpty(err) && err.IndexOf("refused", StringComparison.OrdinalIgnoreCase) >= 0)
                err += " Wait until the dongle is ready (e.g. WiFi AP ready / STA connected), and ensure the phone is on the dongle's WiFi.";
            LblJocpError.Text = err ?? "";
            LblJocpError.IsVisible = !string.IsNullOrEmpty(err);
            BtnJocpConnect.IsEnabled = !active;
            BtnJocpDisconnect.IsVisible = active;
            EntryJocpHost.IsEnabled = !active;
            bool ctrl = active && client?.ControlChannelConnected == true;
            LblRumbleHint.Text = active ? (ctrl ? "Rumble goes to phone and to SlimeVR trackers (left/right hand and arm). If game rumble doesn't trigger, the PC/dongle may not be sending it." : "Rumble: control channel (TCP 30101) not connected; input works but rumble from dongle won't reach phone or trackers.") : "";
            LblRumbleHint.IsVisible = active;
        }
        else
        {
            var ble = _bleClient ?? Platforms.Android.JocpBleClient.Current;
            bool active = ble?.IsActive ?? false;
            bool reconnecting = ble?.IsReconnecting ?? false;
            LblJocpStatus.Text = active ? $"Connected to dongle: {ble!.DeviceName ?? ble.DeviceAddress}" : (reconnecting ? "Reconnecting…" : "Not connected");
            LblJocpError.Text = "";
            LblJocpError.IsVisible = false;
            BtnBleScan.IsEnabled = !active;
            BtnBleConnect.IsEnabled = !active && _selectedBleAddress != null;
            BtnBleDisconnect.IsVisible = active;
            LblRumbleHint.Text = active ? "Rumble goes to phone and to SlimeVR trackers (left/right hand and arm). Use Test buttons to check." : "";
            LblRumbleHint.IsVisible = active;
        }
        BtnModeDirect.IsEnabled = !_modeDirect;
        BtnModeBle.IsEnabled = _modeDirect;
        BtnTestVibration.IsVisible = true;
        BtnTestTrackerRumble.IsVisible = true;
#else
        LblJocpStatus.Text = "Not available on this device.";
        BtnJocpConnect.IsEnabled = false;
        BtnJocpDisconnect.IsVisible = false;
        BtnTestVibration.IsVisible = false;
        BtnTestTrackerRumble.IsVisible = false;
        LblRumbleHint.IsVisible = false;
#endif
    }

    private void OnTestVibrationClicked(object? sender, EventArgs e)
    {
#if ANDROID
        Platforms.Android.JocpRumbleHandler.TestVibration();
#endif
    }

    private void OnTestTrackerRumbleClicked(object? sender, EventArgs e)
    {
        Services.TrackerHapticSender.SendRumbleToTrackers(200, 200, 300);
    }

    private async void OnJocpConnectClicked(object? sender, EventArgs e)
    {
#if ANDROID
        Platforms.Android.JocpBleClient.Current?.Stop();
        var hostInput = (EntryJocpHost.Text ?? "192.168.4.1").Trim();
        if (string.IsNullOrEmpty(hostInput)) hostInput = "192.168.4.1";

        var host = hostInput;
        var resolved = await Services.MdnsHelper.ResolveHostToIpAsync(host).ConfigureAwait(true);
        if (resolved != null)
            host = resolved;
        else if (hostInput.Contains(".local", StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlert("Joypad WiFi", "Could not resolve mDNS name. Enter the dongle IP directly or pick one from the list above.", "OK");
            return;
        }

        _jocpClient = Platforms.Android.JocpClient.GetOrCreate();
        _jocpClient.SetReportProvider(() => Platforms.Android.BleGamepadOutput.Instance.GetReportBytes());
        try
        {
            _jocpClient.Start(host, port: 30100);
            Preferences.Default.Set(JocpLastHostKey, host);
            UpdateJocpStatus();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Joypad WiFi", "Could not connect: " + ex.Message, "OK");
        }
#endif
    }

    private async Task RunDiscoveryAsync()
    {
#if ANDROID
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _mdnsDevices.Clear();
            _selectedMdns = null;
            LblJocpStatus.Text = "Discovering dongles (mDNS)…";
            LblMdnsEmpty.Text = "Discovering…";
        });
        try
        {
            var list = await Services.MdnsHelper.DiscoverJoypadDonglesAsync(5000).ConfigureAwait(true);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _mdnsDevices.Clear();
                foreach (var d in list)
                    _mdnsDevices.Add(new JocpMdnsDeviceRow(d.DisplayName, d.IpAddress, d.Port));
                if (list.Count == 0)
                {
                    LblJocpStatus.Text = "No dongles found. Enter IP or ensure the dongle advertises _joypad._tcp (mDNS).";
                    LblMdnsEmpty.Text = "No dongles found. Enter host above.";
                }
                else
                {
                    LblJocpStatus.Text = $"Found {list.Count} dongle(s). Select one and tap Connect.";
                    LblMdnsEmpty.Text = "No dongles found. Enter host above.";
                    if (list.Count == 1)
                    {
                        var single = list[0];
                        EntryJocpHost.Text = single.IpAddress;
                        Preferences.Default.Set(JocpLastHostKey, single.IpAddress);
                    }
                }
                UpdateJocpStatus();
                TryAutoConnectAsync();
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LblJocpStatus.Text = "Discover failed: " + ex.Message;
                LblMdnsEmpty.Text = "Discover failed. Enter host above.";
                UpdateJocpStatus();
                TryAutoConnectAsync();
            });
        }
#endif
    }

    private async void TryAutoConnectAsync()
    {
#if ANDROID
        var lastHost = Preferences.Default.Get(JocpLastHostKey, (string?)null);
        if (string.IsNullOrWhiteSpace(lastHost)) return;
        lastHost = lastHost.Trim();
        var client = _jocpClient ?? Platforms.Android.JocpClient.GetOrCreate();
        if (client.IsActive) return;

        var host = lastHost;
        var resolved = await Services.MdnsHelper.ResolveHostToIpAsync(host).ConfigureAwait(true);
        if (resolved != null) host = resolved;

        client.SetReportProvider(() => Platforms.Android.BleGamepadOutput.Instance.GetReportBytes());
        client.Start(host, port: 30100);
        MainThread.BeginInvokeOnMainThread(UpdateJocpStatus);
#endif
    }

    private void OnMdnsDeviceSelected(object? sender, SelectionChangedEventArgs e)
    {
#if ANDROID
        if (e.CurrentSelection?.FirstOrDefault() is JocpMdnsDeviceRow row)
        {
            _selectedMdns = (row.IpAddress, row.Port);
            EntryJocpHost.Text = row.IpAddress;
            BtnJocpConnect.IsEnabled = _jocpClient?.IsActive != true;
        }
        else
        {
            _selectedMdns = null;
            BtnJocpConnect.IsEnabled = _jocpClient?.IsActive != true;
        }
#endif
    }

    private void OnJocpDisconnectClicked(object? sender, EventArgs e)
    {
#if ANDROID
        _jocpClient?.Stop();
        UpdateJocpStatus();
#endif
    }

    private async void OnBleScanClicked(object? sender, EventArgs e)
    {
#if ANDROID
        try
        {
            var status = await Permissions.RequestAsync<Permissions.Bluetooth>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }
        }
        catch { /* older SDK */ }

        _bleDevices.Clear();
        _selectedBleAddress = null;
        BtnBleConnect.IsEnabled = false;
        if (_bleClient == null)
        {
            _bleClient = new Platforms.Android.JocpBleClient();
            _bleClient.SetReportProvider(() => Platforms.Android.BleGamepadOutput.Instance.GetReportBytes());
            _bleClient.ConnectionStateChanged += OnBleConnectionStateChanged;
            _bleClient.RumbleReceived += Platforms.Android.JocpRumbleHandler.OnRumble;
        }
        _bleClient.DevicesDiscovered += OnBleDevicesDiscovered;
        _bleClient.StartScan(10000);
        LblJocpStatus.Text = "Scanning for Joypad dongle…";
        await Task.Delay(10500);
        _bleClient.DevicesDiscovered -= OnBleDevicesDiscovered;
        _bleClient.StopScan();
        if (_bleClient?.IsActive != true)
            UpdateJocpStatus();
        else
            LblJocpStatus.Text = "Connected to dongle: " + (_bleClient.DeviceName ?? _bleClient.DeviceAddress);
#endif
    }

    private void OnBleDeviceSelected(object? sender, SelectionChangedEventArgs e)
    {
#if ANDROID
        if (e.CurrentSelection?.FirstOrDefault() is JocpBleDeviceRow row)
        {
            _selectedBleAddress = row.Address;
            BtnBleConnect.IsEnabled = _bleClient?.IsActive != true;
        }
        else
        {
            _selectedBleAddress = null;
            BtnBleConnect.IsEnabled = false;
        }
#endif
    }

    private void OnBleConnectClicked(object? sender, EventArgs e)
    {
#if ANDROID
        if (string.IsNullOrEmpty(_selectedBleAddress)) return;
        Platforms.Android.JocpClient.Current?.Stop();
        if (_bleClient == null)
        {
            _bleClient = new Platforms.Android.JocpBleClient();
            _bleClient.SetReportProvider(() => Platforms.Android.BleGamepadOutput.Instance.GetReportBytes());
            _bleClient.ConnectionStateChanged += OnBleConnectionStateChanged;
            _bleClient.RumbleReceived += Platforms.Android.JocpRumbleHandler.OnRumble;
        }
        _bleClient.Connect(_selectedBleAddress);
        Platforms.Android.JocpBleClient.Current = _bleClient;
        UpdateJocpStatus();
#endif
    }

    private void OnBleDisconnectClicked(object? sender, EventArgs e)
    {
#if ANDROID
        _bleClient?.Stop();
        UpdateJocpStatus();
#endif
    }
}
