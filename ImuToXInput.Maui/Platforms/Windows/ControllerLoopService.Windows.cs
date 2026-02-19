using System.Diagnostics;
using ImuToXInput.Config;
using ImuToXInput.Core.Output;
using ImuToXInput.Maui.Platforms.Windows;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace ImuToXInput.Maui.Services;

public static partial class ControllerLoopService
{
    private static ViGEmClient? _vigemClient;
    private static ViGEmGamepadOutput? _windowsOutput;

    static ControllerLoopService()
    {
        GetProcessName = config =>
        {
            if (config == null) return null;
            foreach (var name in ConfigLoader.GetAllProcessNames(config))
            {
                if (Process.GetProcessesByName(name).Length > 0)
                {
                    return name;
                }
            }
            return null;
        };
        GetOutput = () =>
        {
            if (_windowsOutput != null)
            {
                return _windowsOutput;
            }
            try
            {
                _vigemClient = new ViGEmClient();
                var xbox = _vigemClient.CreateXbox360Controller();
                xbox.Connect();
                _windowsOutput = new ViGEmGamepadOutput(xbox);
                return _windowsOutput;
            }
            catch
            {
                return null;
            }
        };
    }
}
