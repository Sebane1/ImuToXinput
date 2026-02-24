using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.ApplicationModel;

namespace ImuToXInput.Maui;

/// <summary>Display row for ESP32 proxy device list (shared, not Android-specific).</summary>
public record Esp32DeviceRow(string Name, string Address)
{
    public string Display => string.IsNullOrEmpty(Name) ? Address : $"{Name}  ({Address})";
}

public partial class DongleConnectPage : ContentPage
{
#if ANDROID
    private Platforms.Android.JocpClient? _jocpClient;
    private Platforms.Android.JocpBleProxyClient? _bleProxyClient;
#endif
    private bool _modeDirect = true;
    private readonly ObservableCollection<Esp32DeviceRow> _esp32Devices = new();
    private string? _selectedEsp32Address;

    public DongleConnectPage()
    {
        InitializeComponent();
        ListEsp32Devices.ItemsSource = _esp32Devices;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        UpdateJocpStatus();
        if (_jocpClient != null)
            _jocpClient.ConnectionStateChanged += OnJocpConnectionStateChanged;
        if (_bleProxyClient != null)
            _bleProxyClient.ConnectionStateChanged += OnBleProxyConnectionStateChanged;
#endif
    }

    protected override void OnDisappearing()
    {
#if ANDROID
        if (_jocpClient != null)
            _jocpClient.ConnectionStateChanged -= OnJocpConnectionStateChanged;
        if (_bleProxyClient != null)
            _bleProxyClient.ConnectionStateChanged -= OnBleProxyConnectionStateChanged;
#endif
        base.OnDisappearing();
    }

    private void OnJocpConnectionStateChanged(object? sender, bool connected) => MainThread.BeginInvokeOnMainThread(UpdateJocpStatus);
    private void OnBleProxyConnectionStateChanged(object? sender, bool connected) => MainThread.BeginInvokeOnMainThread(UpdateJocpStatus);

    private void OnModeDirectClicked(object? sender, EventArgs e)
    {
        _modeDirect = true;
        PanelDirect.IsVisible = true;
        PanelEsp32.IsVisible = false;
        UpdateJocpStatus();
    }

    private void OnModeEsp32Clicked(object? sender, EventArgs e)
    {
        _modeDirect = false;
        PanelDirect.IsVisible = false;
        PanelEsp32.IsVisible = true;
        UpdateJocpStatus();
    }

    private void UpdateJocpStatus()
    {
#if ANDROID
        if (_modeDirect)
        {
            var client = _jocpClient ?? Platforms.Android.JocpClient.Current;
            bool active = client?.IsActive ?? false;
            bool reconnecting = client?.IsReconnecting ?? false;
            LblJocpStatus.Text = active ? $"Connected: {client!.Host}:{client.Port}" : (reconnecting ? "Reconnecting…" : "Not connected");
            BtnJocpConnect.IsEnabled = !active;
            BtnJocpDisconnect.IsVisible = active;
            EntryJocpHost.IsEnabled = !active;
            EntryJocpPort.IsEnabled = !active;
        }
        else
        {
            var proxy = _bleProxyClient ?? Platforms.Android.JocpBleProxyClient.Current;
            bool active = proxy?.IsActive ?? false;
            bool reconnecting = proxy?.IsReconnecting ?? false;
            LblJocpStatus.Text = active ? $"Connected to ESP32: {proxy!.DeviceName ?? proxy.DeviceAddress}" : (reconnecting ? "Reconnecting…" : "Not connected");
            BtnEsp32Scan.IsEnabled = !active;
            BtnEsp32Connect.IsEnabled = !active && _selectedEsp32Address != null;
            BtnEsp32Disconnect.IsVisible = active;
        }
#else
        LblJocpStatus.Text = "Not available on this device.";
        BtnJocpConnect.IsEnabled = false;
        BtnJocpDisconnect.IsVisible = false;
#endif
    }

    private async void OnJocpConnectClicked(object? sender, EventArgs e)
    {
#if ANDROID
        Platforms.Android.JocpBleProxyClient.Current?.Stop();
        var host = (EntryJocpHost.Text ?? "192.168.4.1").Trim();
        if (string.IsNullOrEmpty(host)) host = "192.168.4.1";
        if (!int.TryParse(EntryJocpPort.Text?.Trim(), out int port) || port <= 0 || port > 65535)
            port = 30100;

        if (_jocpClient == null)
        {
            _jocpClient = new Platforms.Android.JocpClient();
            _jocpClient.RumbleReceived += Platforms.Android.JocpRumbleHandler.OnRumble;
        }
        _jocpClient.SetReportProvider(() => Platforms.Android.BleGamepadOutput.Instance.GetReportBytes());
        try
        {
            _jocpClient.Start(host, port);
            Platforms.Android.JocpClient.Current = _jocpClient;
            UpdateJocpStatus();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Joypad WiFi", "Could not connect: " + ex.Message, "OK");
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

    private async void OnEsp32ScanClicked(object? sender, EventArgs e)
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

        _esp32Devices.Clear();
        _selectedEsp32Address = null;
        BtnEsp32Connect.IsEnabled = false;
        if (_bleProxyClient == null)
        {
            _bleProxyClient = new Platforms.Android.JocpBleProxyClient();
            _bleProxyClient.SetReportProvider(() => Platforms.Android.BleGamepadOutput.Instance.GetReportBytes());
            _bleProxyClient.ConnectionStateChanged += OnBleProxyConnectionStateChanged;
            _bleProxyClient.RumbleReceived += Platforms.Android.JocpRumbleHandler.OnRumble;
        }
        _bleProxyClient.DevicesDiscovered += OnEsp32DevicesDiscovered;
        _bleProxyClient.StartScan(10000);
        LblJocpStatus.Text = "Scanning for ESP32…";
        await Task.Delay(10500);
        _bleProxyClient.DevicesDiscovered -= OnEsp32DevicesDiscovered;
        _bleProxyClient.StopScan();
        if (_bleProxyClient?.IsActive != true)
            UpdateJocpStatus();
        else
            LblJocpStatus.Text = "Connected to ESP32: " + (_bleProxyClient.DeviceName ?? _bleProxyClient.DeviceAddress);
#endif
    }

    private void OnEsp32DevicesDiscovered(object? sender, IReadOnlyList<Platforms.Android.JocpBleProxyClient.BleProxyDevice> devices)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _esp32Devices.Clear();
            foreach (var d in devices)
                _esp32Devices.Add(new Esp32DeviceRow(d.Name, d.Address));
        });
    }

    private void OnEsp32DeviceSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is Esp32DeviceRow row)
        {
            _selectedEsp32Address = row.Address;
            BtnEsp32Connect.IsEnabled = _bleProxyClient?.IsActive != true;
        }
        else
        {
            _selectedEsp32Address = null;
            BtnEsp32Connect.IsEnabled = false;
        }
    }

    private void OnEsp32ConnectClicked(object? sender, EventArgs e)
    {
#if ANDROID
        if (string.IsNullOrEmpty(_selectedEsp32Address)) return;
        Platforms.Android.JocpClient.Current?.Stop();
        if (_bleProxyClient == null)
        {
            _bleProxyClient = new Platforms.Android.JocpBleProxyClient();
            _bleProxyClient.SetReportProvider(() => Platforms.Android.BleGamepadOutput.Instance.GetReportBytes());
            _bleProxyClient.ConnectionStateChanged += OnBleProxyConnectionStateChanged;
            _bleProxyClient.RumbleReceived += Platforms.Android.JocpRumbleHandler.OnRumble;
        }
        _bleProxyClient.Connect(_selectedEsp32Address);
        Platforms.Android.JocpBleProxyClient.Current = _bleProxyClient;
        UpdateJocpStatus();
#endif
    }

    private void OnEsp32DisconnectClicked(object? sender, EventArgs e)
    {
#if ANDROID
        _bleProxyClient?.Stop();
        UpdateJocpStatus();
#endif
    }
}
