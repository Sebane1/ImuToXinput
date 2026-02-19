using ImuToXInput.Core.Output;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace ImuToXInput.Platform;

/// <summary>
/// Windows-only: forwards gamepad output to a ViGEm virtual Xbox 360 controller.
/// </summary>
public sealed class ViGEmGamepadOutput : IGamepadOutput
{
    private readonly IXbox360Controller _xbox;

    public ViGEmGamepadOutput(IXbox360Controller xbox)
    {
        _xbox = xbox;
    }

    public void SetAxis(GamepadAxis axis, short value)
    {
        var vigem = axis switch
        {
            GamepadAxis.LeftThumbX => Xbox360Axis.LeftThumbX,
            GamepadAxis.LeftThumbY => Xbox360Axis.LeftThumbY,
            GamepadAxis.RightThumbX => Xbox360Axis.RightThumbX,
            GamepadAxis.RightThumbY => Xbox360Axis.RightThumbY,
            _ => Xbox360Axis.LeftThumbX
        };
        _xbox.SetAxisValue(vigem, value);
    }

    public void SetButton(GamepadButton button, bool pressed)
    {
        var vigem = button switch
        {
            GamepadButton.A => Xbox360Button.A,
            GamepadButton.B => Xbox360Button.B,
            GamepadButton.X => Xbox360Button.X,
            GamepadButton.Y => Xbox360Button.Y,
            GamepadButton.LeftShoulder => Xbox360Button.LeftShoulder,
            GamepadButton.RightShoulder => Xbox360Button.RightShoulder,
            GamepadButton.Back => Xbox360Button.Back,
            GamepadButton.Start => Xbox360Button.Start,
            GamepadButton.Up => Xbox360Button.Up,
            GamepadButton.Down => Xbox360Button.Down,
            GamepadButton.Left => Xbox360Button.Left,
            GamepadButton.Right => Xbox360Button.Right,
            _ => Xbox360Button.A
        };
        _xbox.SetButtonState(vigem, pressed);
    }

    public void SetTrigger(GamepadTrigger trigger, byte value)
    {
        var vigem = trigger switch
        {
            GamepadTrigger.LeftTrigger => Xbox360Slider.LeftTrigger,
            GamepadTrigger.RightTrigger => Xbox360Slider.RightTrigger,
            _ => Xbox360Slider.LeftTrigger
        };
        _xbox.SetSliderValue(vigem, value);
    }
}
