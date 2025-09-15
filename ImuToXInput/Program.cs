using Everything_To_IMU_SlimeVR.Osc;
using Everything_To_IMU_SlimeVR.Tracking;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Newtonsoft.Json.Linq;
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
        private static ViGEmClient client;
        private static IXbox360Controller xbox;

        // Body-part → TrackerState
        private static Dictionary<string, TrackerState> trackers = new();
        private static bool _chestCalibrated;
        private static Vector3 _chestCalibration;
        private static Vector3 _hipsCalibration;
        private static bool _hipsCalibrated;

        static void Main()
        {
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
            HandleVmcMessage(e.Item1, e.Item3);
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

        static void HandleVmcMessage(string bodyPart, Quaternion rotation)
        {
            try
            {
                var eulerCalibration = new Vector3();
                if (!trackers.ContainsKey(bodyPart))
                {
                    eulerCalibration = rotation.QuaternionToEuler();
                } else
                {
                    eulerCalibration = trackers[bodyPart].EulerCalibration;
                }
                trackers[bodyPart] = new TrackerState
                {
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
            if (trackers.TryGetValue("Chest", out var chest))
            {
                xbox.SetAxisValue(Xbox360Axis.RightThumbY, ApplyDeadzone(chest.Euler.X * 2));
                xbox.SetAxisValue(Xbox360Axis.RightThumbX, ApplyDeadzone(-chest.Euler.Z * 2f));
            }
            if (trackers.TryGetValue("Hips", out var hips))
            {
                xbox.SetAxisValue(Xbox360Axis.LeftThumbX, ApplyDeadzone(hips.Euler.Y));
                xbox.SetAxisValue(Xbox360Axis.LeftThumbY, ApplyDeadzone(-hips.Euler.X * 2));
            }
            if (trackers.TryGetValue("RightUpperArm", out var rightHand))
            {
                xbox.SetButtonState(Xbox360Button.B, rightHand.Euler.Z > 30f);

                xbox.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(rightHand.Euler.Z < -30f ? 255 : 0));
            }
            if (trackers.TryGetValue("LeftUpperArm", out var leftHand))
            {
                xbox.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)(leftHand.Euler.Z < -30f ? 255 : 0));
                //xbox.SetButtonState(Xbox360Button.Y, leftHand.Euler.Z < -30f);
            }
            if (trackers.TryGetValue("LeftFoot", out var leftFoot))
            {
                xbox.SetButtonState(Xbox360Button.A, leftFoot.Euler.X < -20);

                xbox.SetButtonState(Xbox360Button.Y, leftFoot.Euler.Z > 10);
                xbox.SetButtonState(Xbox360Button.LeftShoulder, leftFoot.Euler.Z > 10);
            }
            if (trackers.TryGetValue("RightFoot", out var rightFoot))
            {
                Console.WriteLine(rightFoot.Euler.Z);
                xbox.SetButtonState(Xbox360Button.A, rightFoot.Euler.X < -20);

                xbox.SetButtonState(Xbox360Button.X, rightFoot.Euler.Z < -5);
                xbox.SetButtonState(Xbox360Button.RightShoulder, rightFoot.Euler.Z < -5);
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
