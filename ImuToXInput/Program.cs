using DDRPadEmu;
using Everything_To_IMU_SlimeVR.Osc;
using Everything_To_IMU_SlimeVR.Tracking;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using WebSocketSharp;

namespace ImuToXInput
{
    class Program
    {
        private static WebSocket ws;
        private static OscHandler _oscHandler;
        private static DdrPadEmulator _ddrInputManager;
        private static ViGEmClient client;
        private static IXbox360Controller xbox;

        // Body-part → TrackerState
        private static Dictionary<string, TrackerState> trackers = new();

        static void Main()
        {
            _ddrInputManager = new DdrPadEmulator();
            // ---- 1. Setup ViGEm ----
            client = new ViGEmClient();
            xbox = client.CreateXbox360Controller();
            xbox.Connect();
            xbox.FeedbackReceived += (s, e) =>
            {
                // LEFT_HAND haptic
                if (trackers.TryGetValue("LEFT_HAND", out var leftHand) && !string.IsNullOrEmpty(leftHand.Ip))
                {
                    byte intensity = (byte)Math.Min(100, (e.LargeMotor / 65535.0) * 100);
                    if (intensity > 0) SendHapticToTracker(leftHand.Ip, intensity, 150);
                }

                // RIGHT_HAND haptic
                if (trackers.TryGetValue("RIGHT_HAND", out var rightHand) && !string.IsNullOrEmpty(rightHand.Ip))
                {
                    byte intensity = (byte)Math.Min(100, (e.SmallMotor / 65535.0) * 100);
                    if (intensity > 0) SendHapticToTracker(rightHand.Ip, intensity, 150);
                }
            };
            _oscHandler = new OscHandler();
            _oscHandler.BoneUpdate += _oscHandler_BoneUpdate;
            while (true)
            {
                UpdateController();
                Thread.Sleep(10);
            }
        }

        private static void _oscHandler_BoneUpdate(object? sender, Tuple<string, System.Numerics.Vector3, System.Numerics.Quaternion> e)
        {
            HandleVmcMessage(e.Item1, e.Item2, e.Item3);
        }

        static void ConnectWebSocket()
        {
            ws = new WebSocket("ws://localhost:21110/") { Origin = "http://localhost" };

            ws.OnMessage += (s, e) =>
            {
                Console.WriteLine("Received: " + e.Data.Substring(0, Math.Min(100, e.Data.Length)));
                HandleSolarXRMessage(e.Data);
            };
            ws.OnClose += (s, e) =>
            {
                Console.WriteLine($"WebSocket closed. Reason: {e.Reason}. Reconnecting...");
                ReconnectWebSocket();
            };
            ws.OnError += (s, e) =>
            {
                Console.WriteLine($"WebSocket error: {e.Message}");
            };

            ReconnectWebSocket();
        }

        static void ReconnectWebSocket()
        {
            while (!ws.IsAlive)
            {
                try
                {
                    ws.Connect();
                    if (ws.IsAlive)
                        Console.WriteLine("Connected to SolarXR WebSocket.");
                } catch (Exception ex)
                {
                    Console.WriteLine($"Failed to connect: {ex.Message}. Retrying in 1s...");
                    Thread.Sleep(1000);
                }
            }
        }

        static void HandleVmcMessage(string bodyPart, Vector3 position, Quaternion rotation)
        {
            try
            {
                var eulerCalibration = new Vector3();
                var positionCalibration = new Vector3();
                if (!trackers.ContainsKey(bodyPart))
                {
                    eulerCalibration = rotation.QuaternionToEuler();
                    positionCalibration = position;

                } else
                {
                    eulerCalibration = trackers[bodyPart].EulerCalibration;
                    positionCalibration = trackers[bodyPart].PositionCalibration;
                }
                trackers[bodyPart] = new TrackerState
                {
                    PositionCalibration = positionCalibration,
                    Position = position,
                    EulerCalibration = eulerCalibration,
                    BodyPart = bodyPart,
                    Ip = "",
                    Rotation = rotation
                };
            } catch (Exception ex)
            {
                Console.WriteLine($"Parse error: {ex.Message}");
            }
        }
        static void HandleSolarXRMessage(string json)
        {
            try
            {
                var obj = JObject.Parse(json);
                if (obj["type"]?.ToString() != "tracker") return;

                var data = obj["data"];
                int trackerId = data["id"].Value<int>();
                string bodyPart = data["info"]?["bodyPart"]?.ToString().ToUpper() ?? $"TRACKER_{trackerId}";
                string ip = data["info"]?["ip"]?.ToString();

                var rot = data["rotation"];
                float x = rot["x"].Value<float>();
                float y = rot["y"].Value<float>();
                float z = rot["z"].Value<float>();
                float w = rot["w"].Value<float>();

                trackers[bodyPart] = new TrackerState
                {
                    TrackerId = trackerId,
                    BodyPart = bodyPart,
                    Ip = ip,
                    Rotation = new Quaternion(x, y, z, w),
                };

                Console.WriteLine($"Tracker {trackerId} mapped to {bodyPart} IP={ip}");
            } catch (Exception ex)
            {
                Console.WriteLine($"Parse error: {ex.Message}");
            }
        }

        static void UpdateController()
        {
            string? runningGame = DetectGameProcess();
            switch (runningGame)
            {
                case "mirrorsedge":
                    MirrorsEdge();
                    break;
                case "stepmania":
                    StepMania();
                    break;
                default:
                    FPS();
                    break;
            }
        }

        private static void StepMania()
        {
            if (trackers.TryGetValue("LeftLowerLeg", out var leftFoot) && trackers.TryGetValue("RightLowerLeg", out var rightFoot))
            {
                float deadZoneVertical = 0.9f;
                float deadZoneHorizontal = 0.6f;

                // Determine dominant direction per foot
                (bool up, bool down, bool left, bool right) GetFootDirection(float x, float y)
                {
                    // Scale to -1..1
                    x = Math.Clamp(x / 15f, -1f, 1f);
                    y = Math.Clamp(y / 15f, -1f, 1f);

                    // Ignore small movements
                    if (Math.Abs(x) < deadZoneVertical && Math.Abs(y) < deadZoneHorizontal)
                        return (false, false, false, false);

                    // Pick dominant axis
                    if (Math.Abs(x) > Math.Abs(y))
                    {
                        return x > 0 ? (true, false, false, false) : (false, true, false, false);
                    } else
                    {
                        return y > 0 ? (false, false, true, false) : (false, false, false, true);
                    }
                }

                var leftDir = GetFootDirection(leftFoot.Euler.X, leftFoot.Euler.Y);
                var rightDir = GetFootDirection(rightFoot.Euler.X, rightFoot.Euler.Y);

                // Combine foot inputs: trigger if either foot has the direction
                bool upState = (leftDir.up && leftFoot.CloseToCalibratedY) || (rightDir.up && rightFoot.CloseToCalibratedY);
                bool downState = (leftDir.down && leftFoot.CloseToCalibratedY) || (rightDir.down && rightFoot.CloseToCalibratedY);
                bool leftState = (leftDir.left && leftFoot.CloseToCalibratedY) || (rightDir.left && rightFoot.CloseToCalibratedY);
                bool rightState = (leftDir.right && leftFoot.CloseToCalibratedY) || (rightDir.right && rightFoot.CloseToCalibratedY);

                // Send to virtual controller
                xbox.SetButtonState(Xbox360Button.Up, upState);
                xbox.SetButtonState(Xbox360Button.Down, downState);
                xbox.SetButtonState(Xbox360Button.Left, leftState);
                xbox.SetButtonState(Xbox360Button.Right, rightState);

                // Debug
                Console.SetCursorPosition(0, 0);
                Console.WriteLine($"Up:    {upState}");
                Console.WriteLine($"Down:  {downState}");
                Console.WriteLine($"Left:  {leftState}");
                Console.WriteLine($"Right: {rightState}");
                Console.WriteLine();

                Console.WriteLine($"Left Pos:  {leftFoot.CalibratedPosition:F3}");
                Console.WriteLine($"Left Pos:  {leftFoot.Position:F3}");
                Console.WriteLine($"Left Pos:  {leftFoot.PositionCalibration:F3}");

                Console.WriteLine($"Right Pos:  {rightFoot.CalibratedPosition:F3}");
                Console.WriteLine($"Right Pos:  {rightFoot.Position:F3}");
                Console.WriteLine($"Right Pos:  {rightFoot.PositionCalibration:F3}");


                Console.WriteLine($"Left X:  {leftFoot.Euler.X:F3}");
                Console.WriteLine($"Left Y:  {leftFoot.Euler.Y:F3}");
                Console.WriteLine($"Right X: {rightFoot.Euler.X:F3}");
                Console.WriteLine($"Right Y: {rightFoot.Euler.Y:F3}");
            }
        }

        static string? DetectGameProcess()
        {
            // Add the executable names (without .exe) of games you want to detect
            string[] supportedGames = { "MirrorsEdge", "stepmania" };

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
            if (trackers.TryGetValue("Chest", out var chest))
            {
                xbox.SetAxisValue(Xbox360Axis.RightThumbY, ApplyDeadzone(chest.Euler.X * 3));
                xbox.SetAxisValue(Xbox360Axis.RightThumbX, ApplyDeadzone(-chest.Euler.Z * 3f));
            }
            if (trackers.TryGetValue("Hips", out var hips))
            {
                xbox.SetAxisValue(Xbox360Axis.LeftThumbX, ApplyDeadzone(hips.Euler.Y * 2));
                xbox.SetAxisValue(Xbox360Axis.LeftThumbY, ApplyDeadzone(-hips.Euler.X * 2));
            }
            if (trackers.TryGetValue("RightUpperArm", out var rightHand))
            {
                xbox.SetButtonState(Xbox360Button.Y, rightHand.Euler.Z + chest.Euler.Z < -30f);

                xbox.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(rightHand.Euler.Z - chest.Euler.Z > 30f ? 255 : 0));
            }
            if (trackers.TryGetValue("LeftUpperArm", out var leftHand))
            {
                xbox.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)(leftHand.Euler.Z - chest.Euler.Z < -30f ? 255 : 0));
            }

            if (trackers.TryGetValue("LeftFoot", out var leftFoot))
            {
                xbox.SetButtonState(Xbox360Button.LeftShoulder, leftFoot.Euler.X < -20);

                //xbox.SetButtonState(Xbox360Button.Y, leftFoot.Euler.Y > -20);

                //xbox.SetButtonState(Xbox360Button.LeftShoulder, leftFoot.Euler.Z > 5);
            }
            if (trackers.TryGetValue("RightFoot", out var rightFoot))
            {
                xbox.SetButtonState(Xbox360Button.RightShoulder, rightFoot.Euler.X < -20);

                //xbox.SetButtonState(Xbox360Button.X, rightFoot.Euler.Y > 20);

                //xbox.SetButtonState(Xbox360Button.RightShoulder, rightFoot.Euler.Z < -5);
            }

            if (trackers.TryGetValue("LeftLowerLeg", out var leftAnkle))
            {
                //   xbox.SetButtonState(Xbox360Button.Back, leftAnkle.Euler.X < 1);
            }

            if (trackers.TryGetValue("RightLowerLeg", out var rightAnkle))
            {
                //var value = rightAnkle.Euler.X > 1;
                //Console.WriteLine(value);
                //xbox.SetButtonState(Xbox360Button.Start, value);
            }
        }

        private static void FPS()
        {
            if (trackers.TryGetValue("Chest", out var chest))
            {
                xbox.SetAxisValue(Xbox360Axis.RightThumbY, ApplyDeadzone(chest.Euler.X * 3));
                xbox.SetAxisValue(Xbox360Axis.RightThumbX, ApplyDeadzone(-chest.Euler.Z * 3f));
            }
            if (trackers.TryGetValue("Hips", out var hips))
            {
                xbox.SetAxisValue(Xbox360Axis.LeftThumbX, ApplyDeadzone(hips.Euler.Y * 2));
                xbox.SetAxisValue(Xbox360Axis.LeftThumbY, ApplyDeadzone(-hips.Euler.X * 2));
            }
            if (trackers.TryGetValue("RightUpperArm", out var rightHand))
            {
                xbox.SetButtonState(Xbox360Button.B, rightHand.Euler.Z - chest.Euler.Z > 30f);

                xbox.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(rightHand.Euler.Z + chest.Euler.Z < -30f ? 255 : 0));
            }
            if (trackers.TryGetValue("LeftUpperArm", out var leftHand))
            {
                xbox.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)(leftHand.Euler.Z - chest.Euler.Z < -30f ? 255 : 0));
            }

            if (trackers.TryGetValue("LeftFoot", out var leftFoot))
            {
                xbox.SetButtonState(Xbox360Button.A, leftFoot.Euler.X < -20);

                xbox.SetButtonState(Xbox360Button.Y, leftFoot.Euler.Y > -20);

                xbox.SetButtonState(Xbox360Button.LeftShoulder, leftFoot.Euler.Z > 5);
            }
            if (trackers.TryGetValue("RightFoot", out var rightFoot))
            {
                xbox.SetButtonState(Xbox360Button.A, rightFoot.Euler.X < -20);

                xbox.SetButtonState(Xbox360Button.X, rightFoot.Euler.Y > 20);

                xbox.SetButtonState(Xbox360Button.RightShoulder, rightFoot.Euler.Z < -5);
            }

            if (trackers.TryGetValue("LeftLowerLeg", out var leftAnkle))
            {
                //   xbox.SetButtonState(Xbox360Button.Back, leftAnkle.Euler.X < 1);
            }

            if (trackers.TryGetValue("RightLowerLeg", out var rightAnkle))
            {
                //var value = rightAnkle.Euler.X > 1;
                //Console.WriteLine(value);
                //xbox.SetButtonState(Xbox360Button.Start, value);
            }
        }

        static short ApplyDeadzone(float value, float deadzone = 0.15f)
        {
            // Console.WriteLine(value);
            value = Math.Clamp(value / 15, -1f, 1f);
            //  Console.WriteLine(value);
            if (Math.Abs(value) < deadzone) return 0;
            float sign = Math.Sign(value);
            float scaled = (Math.Abs(value) - deadzone) / (1f - deadzone);
            return (short)(sign * scaled * 32767);
        }

        static void SendHapticToTracker(string trackerIp, byte intensity, ushort duration = 100)
        {
            try
            {
                using var udp = new UdpClient();
                byte[] packet = new byte[5];
                packet[0] = 0x06; // haptic packet
                packet[1] = 0x00; // motor index (single motor)
                packet[2] = intensity;
                packet[3] = (byte)(duration & 0xFF);
                packet[4] = (byte)((duration >> 8) & 0xFF);

                udp.Send(packet, packet.Length, new IPEndPoint(IPAddress.Parse(trackerIp), 6969));
            } catch (Exception ex)
            {
                Console.WriteLine($"Failed to send haptic: {ex.Message}");
            }
        }
    }
}
