#if ANDROID
using System.Collections.Generic;

namespace ImuToXInput.Maui;

public partial class DongleConnectPage
{
    private void OnBleDevicesDiscovered(object? sender, IReadOnlyList<Platforms.Android.JocpBleClient.JocpBleDevice> devices)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _bleDevices.Clear();
            foreach (var d in devices)
                _bleDevices.Add(new JocpBleDeviceRow(d.Name, d.Address));
        });
    }
}
#endif
