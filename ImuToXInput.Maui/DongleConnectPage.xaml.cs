namespace ImuToXInput.Maui;

public partial class DongleConnectPage : ContentPage
{
#if ANDROID
    private Platforms.Android.JocpClient? _jocpClient;
#endif

    public DongleConnectPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        UpdateJocpStatus();
        if (_jocpClient != null)
            _jocpClient.ConnectionStateChanged += OnJocpConnectionStateChanged;
#endif
    }

    protected override void OnDisappearing()
    {
#if ANDROID
        if (_jocpClient != null)
            _jocpClient.ConnectionStateChanged -= OnJocpConnectionStateChanged;
#endif
        base.OnDisappearing();
    }

    private void OnJocpConnectionStateChanged(object? sender, bool connected)
    {
#if ANDROID
        MainThread.BeginInvokeOnMainThread(UpdateJocpStatus);
#endif
    }

    private void UpdateJocpStatus()
    {
#if ANDROID
        var client = _jocpClient ?? Platforms.Android.JocpClient.Current;
        bool active = client?.IsActive ?? false;
        LblJocpStatus.Text = active ? $"Connected: {client!.Host}:{client.Port}" : "Not connected";
        BtnJocpConnect.IsEnabled = !active;
        BtnJocpDisconnect.IsVisible = active;
        EntryJocpHost.IsEnabled = !active;
        EntryJocpPort.IsEnabled = !active;
#else
        LblJocpStatus.Text = "Not available on this device.";
        BtnJocpConnect.IsEnabled = false;
        BtnJocpDisconnect.IsVisible = false;
#endif
    }

    private async void OnJocpConnectClicked(object? sender, EventArgs e)
    {
#if ANDROID
        var host = (EntryJocpHost.Text ?? "192.168.4.1").Trim();
        if (string.IsNullOrEmpty(host)) host = "192.168.4.1";
        if (!int.TryParse(EntryJocpPort.Text?.Trim(), out int port) || port <= 0 || port > 65535)
            port = 30100;

        if (_jocpClient == null)
            _jocpClient = new Platforms.Android.JocpClient();

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
}
