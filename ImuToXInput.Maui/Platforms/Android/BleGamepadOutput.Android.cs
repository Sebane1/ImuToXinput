using ImuToXInput.Core.Output;

namespace ImuToXInput.Maui.Platforms.Android;

/// <summary>
/// Android: accumulates gamepad state and exposes the 12-byte Xbox 360 report.
/// Reports are sent at ~125 Hz by JocpClient when connected to Joypad WiFi.
/// </summary>
public sealed class BleGamepadOutput : IGamepadOutput
{
    private static BleGamepadOutput? _instance;
    /// <summary>Singleton used as the report source for JocpClient (Android only).</summary>
    public static BleGamepadOutput Instance => _instance ??= new BleGamepadOutput();

    private readonly GamepadReportState _state = new();

    public void SetAxis(GamepadAxis axis, short value) => _state.SetAxis(axis, value);

    public void SetButton(GamepadButton button, bool pressed) => _state.SetButton(button, pressed);

    public void SetTrigger(GamepadTrigger trigger, byte value) => _state.SetTrigger(trigger, value);

    /// <summary>Gets the current report as 12 bytes (Xbox 360 format). Used by JocpClient.</summary>
    public byte[] GetReportBytes() => _state.ToBytes();
}
