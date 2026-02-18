using AutoUpdaterDotNET;
using ImuToXInput.Config;
using ImuToXInput.Core.Output;
using ImuToXInput.Platform;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using SlimeImuProtocol.SlimeProtocol;
using SlimeImuProtocol.SlimeVR;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Numerics;

namespace ImuToXInput
{
    class Program
    {
        private static ViGEmClient client;
        private static SlimeVRClient slimeVRClient;
        private static IXbox360Controller xbox;
        private static IGamepadOutput _gamepadOutput = null!;
        private static ConcurrentDictionary<string, bool> _priorHapticsStates = new ConcurrentDictionary<string, bool>();
        private static ConcurrentDictionary<string, UdpClient> _hapticClients = new ConcurrentDictionary<string, UdpClient>();
        // Body-part → TrackerState
        private static Dictionary<string, TrackerState> trackers = new();
        private static LoadedConfig? _loadedConfig;

        static void Main()
        {
            bool launchForm = true;
            try
            {
                AutoUpdater.DownloadPath = AppDomain.CurrentDomain.BaseDirectory;
                AutoUpdater.Synchronous = true;
                AutoUpdater.Mandatory = true;
                AutoUpdater.UpdateMode = Mode.ForcedDownload;
                AutoUpdater.Start("https://raw.githubusercontent.com/Sebane1/ImuToXinput/main/update.xml");
                AutoUpdater.ApplicationExitEvent += delegate ()
                {
                    launchForm = false;
                };

            } catch
            {

            }

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                if (_gamepadOutput != null)
                {
                    ControllerMappingRunner.ClearInputs(_gamepadOutput);
                }
            };

            if (launchForm)
            {
                client = new ViGEmClient();
                slimeVRClient = new SlimeVRClient();
                slimeVRClient.Start();
                xbox = client.CreateXbox360Controller();
                xbox.Connect();
                _gamepadOutput = new ViGEmGamepadOutput(xbox);
                xbox.FeedbackReceived += (s, e) =>
                {
                    var intensityLeft = e.LargeMotor / 255f;
                    var intensityRight = e.SmallMotor / 255f;
                    // LEFT_HAND haptic
                    if (trackers.TryGetValue("LEFT_HAND", out var leftHand) && !string.IsNullOrEmpty(leftHand.Ip))
                    {
                        if (intensityLeft > 0) SendHapticToTracker(leftHand.Ip, intensityLeft, 150);
                    }
                    if (trackers.TryGetValue("LEFT_LOWER_ARM", out var leftLowerArm) && !string.IsNullOrEmpty(leftLowerArm.Ip))
                    {
                        if (intensityLeft > 0) SendHapticToTracker(leftLowerArm.Ip, intensityLeft, 150);
                    }
                    if (trackers.TryGetValue("LEFT_UPPER_ARM", out var leftUpperArm) && !string.IsNullOrEmpty(leftUpperArm.Ip))
                    {

                        if (intensityLeft > 0)
                        {
                            SendHapticToTracker(leftLowerArm.Ip, intensityLeft, 150);
                        }
                    }

                    // RIGHT_HAND haptic
                    if (trackers.TryGetValue("RIGHT_HAND", out var rightHand) && !string.IsNullOrEmpty(rightHand.Ip))
                    {
                        if (intensityRight > 0) SendHapticToTracker(rightHand.Ip, intensityRight, 150);
                    }
                    if (trackers.TryGetValue("RIGHT_LOWER_ARM", out var rightLowerArm) && !string.IsNullOrEmpty(rightLowerArm.Ip))
                    {
                        if (intensityRight > 0) SendHapticToTracker(rightLowerArm.Ip, intensityRight, 150);
                    }
                    if (trackers.TryGetValue("RIGHT_UPPER_ARM", out var rightUpperArm) && !string.IsNullOrEmpty(rightUpperArm.Ip))
                    {
                        if (intensityRight > 0)
                        {
                            SendHapticToTracker(rightUpperArm.Ip, intensityRight, 150);
                        }
                    }
                };
                trackers = slimeVRClient.Trackers;
                _loadedConfig = ConfigLoader.Load();
                if (_loadedConfig == null)
                {
                    Console.WriteLine("No configs folder found; using built-in game mappings.");
                }
                while (true)
                {
                    UpdateController();
                    Thread.Sleep(1);
                }
            }
        }

        static void UpdateController()
        {
            ControllerMappingRunner.Update(trackers, _gamepadOutput, _loadedConfig, DetectGameProcess, activeProfileOverride: null);
        }

        static string? DetectGameProcess()
        {
            // Executable names (without .exe) of games to detect.
            string[] supportedGames = { "MirrorsEdge", "stepmania", "ffxiv_dx11", "portal", "portal2" };

            foreach (var game in supportedGames)
            {
                var process = Process.GetProcessesByName(game).FirstOrDefault();
                if (process != null)
                {
                    return game;
                }
            }

            return "";
        }

        static async void SendHapticToTracker(string trackerIp, float intensity, ushort duration = 100)
        {
            try
            {
                Task.Run(() =>
                {
                    if (!string.IsNullOrEmpty(trackerIp))
                    {
                        var packetBuilder = new PacketBuilder("Test");
                        var data = packetBuilder.BuildHapticPacket(intensity, duration);
                        if (!_hapticClients.ContainsKey(trackerIp))
                        {
                            _hapticClients[trackerIp] = new UdpClient();
                            _hapticClients[trackerIp].Connect(trackerIp, 6969);
                        }
                        _hapticClients[trackerIp].SendAsync(data.ToArray(), data.Length);

                        Thread.Sleep(duration);
                        var endData = packetBuilder.BuildHapticPacket(0, 0);
                        _hapticClients[trackerIp].Send(endData.ToArray(), data.Length);
                    }
                });
            } catch (Exception ex)
            {
                Console.WriteLine($"Failed to send haptic: {ex.Message}");
            }
        }
    }
}
