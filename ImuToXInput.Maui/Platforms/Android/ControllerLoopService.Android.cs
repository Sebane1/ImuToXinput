using ImuToXInput.Core.Output;

namespace ImuToXInput.Maui.Services;

public static partial class ControllerLoopService
{
    static ControllerLoopService()
    {
        GetOutput = () => Platforms.Android.BleGamepadOutput.Instance;
    }
}
