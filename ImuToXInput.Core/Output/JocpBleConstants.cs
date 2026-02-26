namespace ImuToXInput.Core.Output;

/// <summary>
/// BLE protocol between the ImuToXInput app (client) and the Joypad dongle (BLE GATT server).
/// Client writes controller report; dongle may notify rumble (and optionally LED) back.
/// </summary>
public static class JocpBleConstants
{
    /// <summary>Recommended BLE device name for the dongle (for scan filtering).</summary>
    public const string DefaultDeviceNamePrefix = "JoypadOS-BLE";

    /// <summary>BLE service UUID the dongle advertises (16-bit style).</summary>
    public static readonly Guid ServiceUuid = new("0000fff0-0000-1000-8000-00805f9b34fb");

    /// <summary>Characteristic UUID: client writes 12-byte Xbox 360 report here at ~125 Hz.</summary>
    public static readonly Guid ReportCharacteristicUuid = new("0000fff1-0000-1000-8000-00805f9b34fb");

    /// <summary>Characteristic UUID: dongle notifies rumble (and optionally other feedback). Client enables Notify.</summary>
    public static readonly Guid FeedbackCharacteristicUuid = new("0000fff2-0000-1000-8000-00805f9b34fb");

    /// <summary>Report size in bytes (Xbox 360 format).</summary>
    public const int ReportSizeBytes = 12;

    /// <summary>Rumble notification payload: 6 bytes (left_amp, left_brake, right_amp, right_brake, duration_ms LE). Same as JOCP_CMD_RUMBLE.</summary>
    public const int RumblePayloadSize = 6;
}
