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
                    Console.WriteLine("No configs folder found; using built-in game mappings.");
                while (true)
                {
                    UpdateController();
                    Thread.Sleep(8);
                }
            }
        }

        static void UpdateController()
        {
            string? runningGame = DetectGameProcess();

            // StepMania / DDR pad mode
            if (string.Equals(runningGame, "stepmania", StringComparison.OrdinalIgnoreCase))
            {
                StepMania();
                return;
            }

            // Config-driven mappings for all other games
            if (_loadedConfig != null)
            {
                var profile = ConfigLoader.GetProfileForProcess(_loadedConfig, runningGame);
                if (profile != null)
                {
                    ConfigApplier.Apply(profile, trackers, _gamepadOutput);
                    return;
                }
            }

            // Fallback when no config or no matching profile: legacy hardcoded modes
            switch (runningGame)
            {
                case "MirrorsEdge":
                    MirrorsEdge();
                    break;
                case "ffxiv_dx11":
                    FFXIV();
                    break;
                case "portal":
                case "portal2":
                    Portal();
                    break;
                default:
                    FPS();
                    break;
            }
        }

        private static void Portal()
        {
            if (trackers.TryGetValue("HEAD", out var head))
            {
                _gamepadOutput.SetAxis(GamepadAxis.RightThumbY, ApplyDeadzone(-head.Euler.X * 2));
                _gamepadOutput.SetAxis(GamepadAxis.RightThumbX, ApplyDeadzone(-head.Euler.Y * 1f));
            }
            if (trackers.TryGetValue("CHEST", out var chest))
            {
            }
            if (trackers.TryGetValue("HIP", out var hips))
            {
                _gamepadOutput.SetAxis(GamepadAxis.LeftThumbX, ApplyDeadzone(hips.Euler.Z * 2));
                _gamepadOutput.SetAxis(GamepadAxis.LeftThumbY, ApplyDeadzone(-hips.Euler.X * 2));
            }
            if (trackers.TryGetValue("RIGHT_UPPER_ARM", out var rightHand))
            {
                _gamepadOutput.SetButton(GamepadButton.B, rightHand.Euler.Y - chest.Euler.Y > 30f);
                _gamepadOutput.SetTrigger(GamepadTrigger.RightTrigger, (byte)(rightHand.Euler.Y + chest.Euler.Y < -15f ? 255 : 0));
            }
            if (trackers.TryGetValue("LEFT_UPPER_ARM", out var leftHand))
            {
                bool triggerValue = leftHand.Euler.Y + chest.Euler.Y > 10f;
                _gamepadOutput.SetTrigger(GamepadTrigger.LeftTrigger, (byte)(triggerValue ? 255 : 0));
            }

            if (trackers.TryGetValue("LEFT_FOOT", out var leftFoot))
            {
                _gamepadOutput.SetButton(GamepadButton.A, leftFoot.Euler.X < -20);
                _gamepadOutput.SetButton(GamepadButton.LeftShoulder, leftFoot.Euler.Y > 5);
            }

            if (trackers.TryGetValue("RIGHT_FOOT", out var rightFoot))
            {
                _gamepadOutput.SetButton(GamepadButton.A, rightFoot.Euler.X < -20);
                _gamepadOutput.SetButton(GamepadButton.RightShoulder, rightFoot.Euler.Y < -5);
            }

            if (trackers.TryGetValue("LEFT_LOWER_LEG", out var leftAnkle))
            {
            }

            if (trackers.TryGetValue("RIGHT_LOWER_LEG", out var rightAnkle))
            {
            }
        }

        private static (bool up, bool down, bool left, bool right, bool upLeft, bool upRight, bool downLeft, bool downRight)
            GetFootDirection(TrackerState ankle, float deadZone, float diagThreshold, float cardThreshold)
        {
            float x = ankle.CalibratedPosition.X;
            float z = ankle.CalibratedPosition.Z;

            if (Math.Abs(x) < deadZone && Math.Abs(z) < deadZone)
                return (false, false, false, false, false, false, false, false);

            var dir = new Vector2(x, z);
            float mag = dir.Length();

            if (mag < deadZone)
                return (false, false, false, false, false, false, false, false);

            dir /= mag; // normalize

            // Only allow diagonals if the foot is pushed far enough from center
            if (ankle.CloseToCalibratedY)
            {
                if (mag > diagThreshold)
                {
                    if (dir.X < -0.5f && -dir.Y < -0.5f) return (false, false, false, false, true, false, false, false); // ↖
                    if (dir.X > 0.5f && -dir.Y < -0.5f) return (false, false, false, false, false, true, false, false); // ↗
                    if (dir.X < -0.5f && -dir.Y > 0.5f) return (false, false, false, false, false, false, true, false); // ↙
                    if (dir.X > 0.5f && -dir.Y > 0.5f) return (false, false, false, false, false, false, false, true); // ↘
                }

                // Otherwise fall back to cardinals if past threshold
                if (Math.Abs(dir.Y) > Math.Abs(dir.X))
                {
                    if (dir.Y < -cardThreshold) return (false, true, false, false, false, false, false, false); // ↑
                    if (dir.Y > cardThreshold) return (true, false, false, false, false, false, false, false); // ↓
                } else
                {
                    if (dir.X < -cardThreshold) return (false, false, true, false, false, false, false, false); // ←
                    if (dir.X > cardThreshold) return (false, false, false, true, false, false, false, false); // →
                }
            }
            return (false, false, false, false, false, false, false, false);
        }

        private static void StepMania()
        {
            if (((trackers.TryGetValue("LEFT_FOOT", out var leftAnkle)
                && trackers.TryGetValue("RIGHT_FOOT", out var rightAnkle)) ||
                (trackers.TryGetValue("LEFT_LOWER_LEG", out leftAnkle)
                && trackers.TryGetValue("RIGHT_LOWER_LEG", out rightAnkle)))
                && trackers.TryGetValue("HEAD", out var head))
            {
                TrackingEnvironment.UpdateFloor(leftAnkle, rightAnkle);

                float deadZone = 0.200f;       // ignore tiny wiggles near center
                float diagThreshold = 0.35f;  // how "far out" diagonals need to be
                float cardThreshold = 0.50f;  // cardinals kick in after this

                var leftDir = GetFootDirection(leftAnkle, deadZone, diagThreshold, cardThreshold);
                var rightDir = GetFootDirection(rightAnkle, deadZone, diagThreshold, cardThreshold);

                CheckHaptic(leftDir, leftAnkle.Ip);
                CheckHaptic(rightDir, rightAnkle.Ip);

                // Combine foot inputs: trigger if either foot has the direction
                bool upState = (leftDir.up) || (rightDir.up);
                bool downState = (leftDir.down) || (rightDir.down);
                bool leftState = (leftDir.left) || (rightDir.left);
                bool rightState = (leftDir.right) || (rightDir.right);

                bool upLeftState = leftDir.upLeft;
                bool downLeftState = leftDir.downLeft;

                bool upRightState = rightDir.upRight;
                bool downRightState = rightDir.downRight;

                // Send to virtual controller
                _gamepadOutput.SetButton(GamepadButton.Up, upState);
                _gamepadOutput.SetButton(GamepadButton.Down, downState);
                _gamepadOutput.SetButton(GamepadButton.Left, leftState);
                _gamepadOutput.SetButton(GamepadButton.Right, rightState);

                _gamepadOutput.SetButton(GamepadButton.A, upRightState);
                _gamepadOutput.SetButton(GamepadButton.B, upLeftState);
                _gamepadOutput.SetButton(GamepadButton.X, downRightState);
                _gamepadOutput.SetButton(GamepadButton.Y, downLeftState);

                // Debug
                Console.SetCursorPosition(0, 0);
                Console.WriteLine($"Stepmania Dance Pad Debug:");
                Console.WriteLine($"Up:    {upState}");
                Console.WriteLine($"Down:  {downState}");
                Console.WriteLine($"Left:  {leftState}");
                Console.WriteLine($"Right: {rightState}");
                Console.WriteLine($"A:     {upRightState}");
                Console.WriteLine($"B:     {upLeftState}");
                Console.WriteLine($"X:     {downRightState}");
                Console.WriteLine($"Y:     {downLeftState}");
                Console.WriteLine();

                Console.WriteLine($"Left Calibrated Pos:  {leftAnkle.CalibratedPosition:F3}");
                Console.WriteLine($"Left Raw Pos:  {leftAnkle.Position:F3}");
                Console.WriteLine($"Left Calibration Reference Pos:  {leftAnkle.PositionCalibration:F3}");
                Console.WriteLine($"Left Floor Relative Pos:  {leftAnkle.FloorRelativePosition:F3}");

                Console.WriteLine($"Right Calibrated Pos:  {rightAnkle.CalibratedPosition:F3}");
                Console.WriteLine($"Right Raw Pos:  {rightAnkle.Position:F3}");
                Console.WriteLine($"Right Calibration Reference Pos:  {rightAnkle.PositionCalibration:F3}");
                Console.WriteLine($"Right Floor Relative Pos:  {rightAnkle.FloorRelativePosition:F3}");

                Console.WriteLine($"Left X:  {leftAnkle.Euler.X}");
                Console.WriteLine($"Left Y:  {leftAnkle.Euler.Z}");
                Console.WriteLine($"Right X: {rightAnkle.Euler.X}");
                Console.WriteLine($"Right Y: {rightAnkle.Euler.Z}");
            }
        }

        static void CheckHaptic((bool up, bool down, bool left, bool right, bool upLeft, bool upRight, bool downLeft, bool downRight) dir, string ip)
        {
            if (FootHitButton(dir.up, dir.down, dir.left, dir.right, dir.upLeft, dir.downLeft, dir.upRight, dir.downRight))
            {
                if (!_priorHapticsStates.ContainsKey(ip) || !_priorHapticsStates[ip])
                {
                    Task.Run(() =>
                    {
                        SendHapticToTracker(ip, 0.4f, 300);
                        _priorHapticsStates[ip] = true;
                        Thread.Sleep(300);
                        SendHapticToTracker(ip, 0, 300);
                    });
                }
            } else
            {
                if (!_priorHapticsStates.ContainsKey(ip) || _priorHapticsStates[ip])
                {
                    SendHapticToTracker(ip, 0, 300);
                    _priorHapticsStates[ip] = false;
                }
            }
        }

        static bool FootHitButton(params bool[] buttonStates)
        {
            foreach (var value in buttonStates)
            {
                if (value)
                {
                    return value;
                }
            }
            return false;
        }
        static string? DetectGameProcess()
        {
            // Add the executable names (without .exe) of games you want to detect
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

        private static void MirrorsEdge()
        {
            if (trackers.TryGetValue("HEAD", out var head))
            {
                _gamepadOutput.SetAxis(GamepadAxis.RightThumbY, ApplyDeadzone(-head.Euler.X * 2));
                _gamepadOutput.SetAxis(GamepadAxis.RightThumbX, ApplyDeadzone(-head.Euler.Y * 1f));
            }
            if (trackers.TryGetValue("CHEST", out var chest))
            {
            }
            if (trackers.TryGetValue("HIP", out var hips))
            {
                _gamepadOutput.SetAxis(GamepadAxis.LeftThumbX, ApplyDeadzone(hips.Euler.Y * 2));
                _gamepadOutput.SetAxis(GamepadAxis.LeftThumbY, ApplyDeadzone(-hips.Euler.X * 2));
            }
            if (trackers.TryGetValue("RIGHT_UPPER_ARM", out var rightHand))
            {
                _gamepadOutput.SetButton(GamepadButton.Y, rightHand.Euler.Z + chest.Euler.Z < -30f);
                _gamepadOutput.SetTrigger(GamepadTrigger.RightTrigger, (byte)(rightHand.Euler.Z - chest.Euler.Z > 30f ? 255 : 0));
            }
            if (trackers.TryGetValue("LEFT_UPPER_ARM", out var leftHand))
            {
                _gamepadOutput.SetTrigger(GamepadTrigger.LeftTrigger, (byte)(leftHand.Euler.Z - chest.Euler.Z < -30f ? 255 : 0));
            }

            if (trackers.TryGetValue("LEFT_FOOT", out var leftFoot))
            {
                _gamepadOutput.SetButton(GamepadButton.LeftShoulder, leftFoot.Euler.X < -20);
            }
            if (trackers.TryGetValue("RIGHT_FOOT", out var rightFoot))
            {
                _gamepadOutput.SetButton(GamepadButton.RightShoulder, rightFoot.Euler.X < -20);
            }

            if (trackers.TryGetValue("LEFT_LOWER_LEG", out var leftAnkle)) { }
            if (trackers.TryGetValue("RIGHT_LOWER_LEG", out var rightAnkle)) { }
        }

        private static void FPS()
        {
            if (trackers.TryGetValue("HEAD", out var head))
            {
                _gamepadOutput.SetAxis(GamepadAxis.RightThumbY, ApplyDeadzone(-head.Euler.X * 2));
                _gamepadOutput.SetAxis(GamepadAxis.RightThumbX, ApplyDeadzone(-head.Euler.Y * 1f));
            }
            if (trackers.TryGetValue("CHEST", out var chest))
            {
            }
            if (trackers.TryGetValue("HIP", out var hips))
            {
                _gamepadOutput.SetAxis(GamepadAxis.LeftThumbX, ApplyDeadzone(hips.Euler.Z * 2));
                _gamepadOutput.SetAxis(GamepadAxis.LeftThumbY, ApplyDeadzone(-hips.Euler.X * 2));
            }
            if (trackers.TryGetValue("RIGHT_UPPER_ARM", out var rightHand))
            {
                _gamepadOutput.SetButton(GamepadButton.B, rightHand.Euler.Y - chest.Euler.Y > 30f);
                _gamepadOutput.SetTrigger(GamepadTrigger.RightTrigger, (byte)(rightHand.Euler.Y + chest.Euler.Y < -15f ? 255 : 0));
            }
            if (trackers.TryGetValue("LEFT_UPPER_ARM", out var leftHand))
            {
                _gamepadOutput.SetTrigger(GamepadTrigger.LeftTrigger, (byte)(leftHand.Euler.Y - chest.Euler.Y < -20f ? 255 : 0));
            }

            if (trackers.TryGetValue("LEFT_FOOT", out var leftFoot))
            {
                _gamepadOutput.SetButton(GamepadButton.A, leftFoot.Euler.X < -20);
                _gamepadOutput.SetButton(GamepadButton.Y, leftFoot.Euler.Z > -20);
                _gamepadOutput.SetButton(GamepadButton.LeftShoulder, leftFoot.Euler.Y > 5);
            }
            if (trackers.TryGetValue("RIGHT_FOOT", out var rightFoot))
            {
                _gamepadOutput.SetButton(GamepadButton.A, rightFoot.Euler.X < -20);
                _gamepadOutput.SetButton(GamepadButton.X, rightFoot.Euler.Z > 20);
                _gamepadOutput.SetButton(GamepadButton.RightShoulder, rightFoot.Euler.Y < -5);
            }

            if (trackers.TryGetValue("LEFT_LOWER_LEG", out var leftAnkle)) { }
            if (trackers.TryGetValue("RIGHT_LOWER_LEG", out var rightAnkle)) { }
        }

        private static void FFXIV()
        {
            if (trackers.TryGetValue("HEAD", out var head)) { }
            if (trackers.TryGetValue("CHEST", out var chest)) { }
            if (trackers.TryGetValue("HIP", out var hips))
            {
                _gamepadOutput.SetAxis(GamepadAxis.LeftThumbX, ApplyDeadzone(hips.Euler.Z * 1));
                _gamepadOutput.SetAxis(GamepadAxis.LeftThumbY, ApplyDeadzone(-hips.Euler.X * 1));
            }

            TrackerState? leftFoot = null;
            if (trackers.TryGetValue("LEFT_FOOT", out leftFoot))
            {
                _gamepadOutput.SetTrigger(GamepadTrigger.LeftTrigger, (byte)(leftFoot.Euler.Y > 15f ? 255 : 0));
            }

            TrackerState? rightFoot = null;
            if (trackers.TryGetValue("RIGHT_FOOT", out rightFoot))
            {
                _gamepadOutput.SetTrigger(GamepadTrigger.RightTrigger, (byte)(rightFoot.Euler.Y < -15f ? 255 : 0));
            }

            if (leftFoot != null && rightFoot != null)
            {
                TrackingEnvironment.UpdateFloor(leftFoot, rightFoot);
            }
            if (trackers.TryGetValue("RIGHT_LOWER_ARM", out var rightHand))
            {
                _gamepadOutput.SetButton(GamepadButton.Y, rightHand.FloorRelativePosition.Y > 0.1f);
                _gamepadOutput.SetButton(GamepadButton.A, rightHand.FloorRelativePosition.Y < -0.02f);
                _gamepadOutput.SetButton(GamepadButton.B, rightHand.FloorRelativePosition.Z > 0.1f);
                _gamepadOutput.SetButton(GamepadButton.X, rightHand.FloorRelativePosition.Z < -0.1f);
                Console.SetCursorPosition(0, 0);
                Console.WriteLine(rightHand.FloorRelativePosition);
            }
            if (trackers.TryGetValue("LEFT_UPPER_ARM", out var leftHand))
            {
                _gamepadOutput.SetButton(GamepadButton.Up, leftHand.FloorRelativePosition.Y > 0.01f);
                _gamepadOutput.SetButton(GamepadButton.Down, leftHand.FloorRelativePosition.Y < -0.02f);
                _gamepadOutput.SetButton(GamepadButton.Left, leftHand.FloorRelativePosition.Z > 0.01f);
                _gamepadOutput.SetButton(GamepadButton.Right, leftHand.FloorRelativePosition.Z < -0.01f);
                Console.WriteLine(leftHand.FloorRelativePosition);
            }

            if (trackers.TryGetValue("LEFT_LOWER_LEG", out var leftAnkle)) { }
            if (trackers.TryGetValue("RIGHT_LOWER_LEG", out var rightAnkle)) { }
        }

        static short ApplyDeadzone(float value, float deadzone = 0.2f)
        {
            // Console.WriteLine(value);
            value = Math.Clamp(value / 15, -1f, 1f);
            //  Console.WriteLine(value);
            if (Math.Abs(value) < deadzone) return 0;
            float sign = Math.Sign(value);
            float scaled = (Math.Abs(value) - deadzone) / (1f - deadzone);
            return (short)(sign * scaled * 32767);
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
