using ImuToXInput.Core.Output;

namespace ImuToXInput.Maui.Platforms.Android;

/// <summary>
/// Android: sends gamepad report over BLE to nRF dongle. Stub for now.
/// </summary>
public sealed class BleGamepadOutput : IGamepadOutput
{
    public void SetAxis(GamepadAxis axis, short value) { }

    public void SetButton(GamepadButton button, bool pressed) { }

    public void SetTrigger(GamepadTrigger trigger, byte value) { }
}
