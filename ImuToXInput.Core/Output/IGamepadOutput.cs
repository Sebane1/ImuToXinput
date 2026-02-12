namespace ImuToXInput.Core.Output;

/// <summary>
/// Platform-agnostic gamepad output (Windows = ViGEm, Android = BLE to nRF).
/// </summary>
public interface IGamepadOutput
{
    void SetAxis(GamepadAxis axis, short value);
    void SetButton(GamepadButton button, bool pressed);
    void SetTrigger(GamepadTrigger trigger, byte value);
}

public enum GamepadAxis
{
    LeftThumbX,
    LeftThumbY,
    RightThumbX,
    RightThumbY
}

public enum GamepadButton
{
    A,
    B,
    X,
    Y,
    LeftShoulder,
    RightShoulder,
    Back,
    Start,
    Up,
    Down,
    Left,
    Right
}

public enum GamepadTrigger
{
    LeftTrigger,
    RightTrigger
}
