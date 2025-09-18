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
using System.Reflection.Metadata;
using WebSocketSharp;

namespace ImuToXInput
{
    class Program
    {
        private static WebSocket ws;
        private static ViGEmClient client;
        private static SlimeVRClient slimeVRClient;
        private static IXbox360Controller xbox;

        // Body-part → TrackerState
        private static Dictionary<string, TrackerState> trackers = new();

        static void Main()
        {
            // ---- 1. Setup ViGEm ----
            client = new ViGEmClient();
            slimeVRClient = new SlimeVRClient();
            slimeVRClient.Start();
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
            trackers = slimeVRClient.Trackers;
            while (true)
            {
                UpdateController();
                Thread.Sleep(10);
            }
        }

        private static void _oscHandler_BoneUpdate(object? sender, Tuple<string, System.Numerics.Vector3, System.Numerics.Quaternion> e)
        {
            // HandleVmcMessage(e.Item1, e.Item2, e.Item3);
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
            if ((trackers.TryGetValue("LEFT_FOOT", out var leftAnkle)
                && trackers.TryGetValue("RIGHT_FOOT", out var rightAnkle)) ||
                (trackers.TryGetValue("LEFT_LOWER_LEG", out leftAnkle)
                && trackers.TryGetValue("RIGHT_LOWER_LEG", out rightAnkle)))
            {

                float deadZoneVertical = 0.200f;
                float deadZoneHorizontal = 0.200f;
                (bool up, bool down, bool left, bool right) GetFootDirection(TrackerState ankle)
                {
                    if (Math.Abs(ankle.CalibratedPosition.X) < deadZoneVertical && Math.Abs(ankle.CalibratedPosition.Z) < deadZoneHorizontal)
                        return (false, false, false, false);
                    if (Math.Abs(ankle.CalibratedPosition.Z) > Math.Abs(ankle.CalibratedPosition.X))
                    {
                        return ankle.CalibratedPosition.Z < 0 ? (true, false, false, false) : (false, true, false, false);
                    } else
                    {
                        return ankle.CalibratedPosition.X < 0 ? (false, false, true, false) : (false, false, false, true);
                    }
                }

                var leftDir = GetFootDirection(leftAnkle);
                var rightDir = GetFootDirection(rightAnkle);

                // Combine foot inputs: trigger if either foot has the direction
                bool upState = (leftDir.up && leftAnkle.CloseToCalibratedY) || (rightDir.up && rightAnkle.CloseToCalibratedY);
                bool downState = (leftDir.down && leftAnkle.CloseToCalibratedY) || (rightDir.down && rightAnkle.CloseToCalibratedY);
                bool leftState = (leftDir.left && leftAnkle.CloseToCalibratedY) || (rightDir.left && rightAnkle.CloseToCalibratedY);
                bool rightState = (leftDir.right && leftAnkle.CloseToCalibratedY) || (rightDir.right && rightAnkle.CloseToCalibratedY);

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

                Console.WriteLine($"Left Pos:  {leftAnkle.CalibratedPosition:F3}");
                Console.WriteLine($"Left Pos:  {leftAnkle.Position:F3}");
                Console.WriteLine($"Left Pos:  {leftAnkle.PositionCalibration:F3}");

                Console.WriteLine($"Right Pos:  {rightAnkle.CalibratedPosition:F3}");
                Console.WriteLine($"Right Pos:  {rightAnkle.Position:F3}");
                Console.WriteLine($"Right Pos:  {rightAnkle.PositionCalibration:F3}");


                Console.WriteLine($"Left X:  {leftAnkle.Euler.X}");
                Console.WriteLine($"Left Y:  {leftAnkle.Euler.Z}");
                Console.WriteLine($"Right X: {rightAnkle.Euler.X}");
                Console.WriteLine($"Right Y: {rightAnkle.Euler.Z}");
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
            if (trackers.TryGetValue("HEAD", out var head))
            {
                xbox.SetAxisValue(Xbox360Axis.RightThumbY, ApplyDeadzone(head.Euler.X * 3));
                xbox.SetAxisValue(Xbox360Axis.RightThumbX, ApplyDeadzone(-head.Euler.Z * 3f));
            }
            if (trackers.TryGetValue("CHEST", out var chest))
            {
            }
            if (trackers.TryGetValue("HIP", out var hips))
            {
                xbox.SetAxisValue(Xbox360Axis.LeftThumbX, ApplyDeadzone(hips.Euler.Y * 2));
                xbox.SetAxisValue(Xbox360Axis.LeftThumbY, ApplyDeadzone(-hips.Euler.X * 2));
            }
            if (trackers.TryGetValue("RIGHT_UPPER_ARM", out var rightHand))
            {
                xbox.SetButtonState(Xbox360Button.Y, rightHand.Euler.Z + chest.Euler.Z < -30f);

                xbox.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(rightHand.Euler.Z - chest.Euler.Z > 30f ? 255 : 0));
            }
            if (trackers.TryGetValue("LEFT_UPPER_ARM", out var leftHand))
            {
                xbox.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)(leftHand.Euler.Z - chest.Euler.Z < -30f ? 255 : 0));
            }

            if (trackers.TryGetValue("LEFT_FOOT", out var leftFoot))
            {
                xbox.SetButtonState(Xbox360Button.LeftShoulder, leftFoot.Euler.X < -20);

                //xbox.SetButtonState(Xbox360Button.Y, leftFoot.Euler.Y > -20);

                //xbox.SetButtonState(Xbox360Button.LeftShoulder, leftFoot.Euler.Z > 5);
            }
            if (trackers.TryGetValue("RIGHT_FOOT", out var rightFoot))
            {
                xbox.SetButtonState(Xbox360Button.RightShoulder, rightFoot.Euler.X < -20);

                //xbox.SetButtonState(Xbox360Button.X, rightFoot.Euler.Y > 20);

                //xbox.SetButtonState(Xbox360Button.RightShoulder, rightFoot.Euler.Z < -5);
            }

            if (trackers.TryGetValue("LEFT_LOWER_LEG", out var leftAnkle))
            {
                //   xbox.SetButtonState(Xbox360Button.Back, leftAnkle.Euler.X < 1);
            }

            if (trackers.TryGetValue("RIGHT_LOWER_LEG", out var rightAnkle))
            {
                //var value = rightAnkle.Euler.X > 1;
                //Console.WriteLine(value);
                //xbox.SetButtonState(Xbox360Button.Start, value);
            }
        }

        private static void FPS()
        {
            if (trackers.TryGetValue("HEAD", out var head))
            {
                xbox.SetAxisValue(Xbox360Axis.RightThumbY, ApplyDeadzone(head.Euler.X * 3));
                xbox.SetAxisValue(Xbox360Axis.RightThumbX, ApplyDeadzone(-head.Euler.Z * 3f));
            }
            if (trackers.TryGetValue("CHEST", out var chest))
            {
                xbox.SetAxisValue(Xbox360Axis.RightThumbY, ApplyDeadzone(chest.Euler.X * 1));
                xbox.SetAxisValue(Xbox360Axis.RightThumbX, ApplyDeadzone(-chest.Euler.Y * 1f));
            }
            if (trackers.TryGetValue("HIP", out var hips))
            {
                xbox.SetAxisValue(Xbox360Axis.LeftThumbX, ApplyDeadzone(hips.Euler.Z * 2));
                xbox.SetAxisValue(Xbox360Axis.LeftThumbY, ApplyDeadzone(-hips.Euler.X * 2));
            }
            if (trackers.TryGetValue("RIGHT_UPPER_ARM", out var rightHand))
            {
                xbox.SetButtonState(Xbox360Button.B, rightHand.Euler.Y - chest.Euler.Y > 30f);

                xbox.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(rightHand.Euler.Y + chest.Euler.Y < -15f ? 255 : 0));
            }
            if (trackers.TryGetValue("LEFT_UPPER_ARM", out var leftHand))
            {
                xbox.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)(leftHand.Euler.Y - chest.Euler.Y < -20f ? 255 : 0));
            }

            if (trackers.TryGetValue("LEFT_FOOT", out var leftFoot))
            {
                xbox.SetButtonState(Xbox360Button.A, leftFoot.Euler.X < -20);

                xbox.SetButtonState(Xbox360Button.Y, leftFoot.Euler.Z > -20);

                xbox.SetButtonState(Xbox360Button.LeftShoulder, leftFoot.Euler.Y > 5);
            }
            if (trackers.TryGetValue("RIGHT_FOOT", out var rightFoot))
            {
                xbox.SetButtonState(Xbox360Button.A, rightFoot.Euler.X < -20);

                xbox.SetButtonState(Xbox360Button.X, rightFoot.Euler.Z > 20);

                xbox.SetButtonState(Xbox360Button.RightShoulder, rightFoot.Euler.Y < -5);
            }

            if (trackers.TryGetValue("LEFT_LOWER_LEG", out var leftAnkle))
            {
                //   xbox.SetButtonState(Xbox360Button.Back, leftAnkle.Euler.X < 1);
            }

            if (trackers.TryGetValue("RIGHT_LOWER_LEG", out var rightAnkle))
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
