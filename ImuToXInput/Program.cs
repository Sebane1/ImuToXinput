using Everything_To_IMU_SlimeVR.Osc;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Sockets;
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

        static void Main()
        {
            _oscHandler = new OscHandler();
            _oscHandler.BoneUpdate += _oscHandler_BoneUpdate;
            // ---- 1. Setup ViGEm ----
            client = new ViGEmClient();
            xbox = client.CreateXbox360Controller();
            xbox.Connect();
            ConnectWebSocket();

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
            Console.WriteLine("Connected to SolarXR WebSocket.");

            // ---- 3. Main loop ----
            while (true)
            {
                UpdateController();
                Thread.Sleep(1000); // ~100Hz
            }
        }

        private static void _oscHandler_BoneUpdate(object? sender, Tuple<string, System.Numerics.Vector3, System.Numerics.Quaternion> e)
        {
            HandleSolarVMCMessage
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

        static void HandleVmcMessage(string json)
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
                    X = x,
                    Y = y,
                    Z = z,
                    W = w
                };

                Console.WriteLine($"Tracker {trackerId} mapped to {bodyPart} IP={ip}");
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
                    X = x,
                    Y = y,
                    Z = z,
                    W = w
                };

                Console.WriteLine($"Tracker {trackerId} mapped to {bodyPart} IP={ip}");
            } catch (Exception ex)
            {
                Console.WriteLine($"Parse error: {ex.Message}");
            }
        }

        static void UpdateController()
        {
            // Apply smoothing to all trackers
            foreach (var t in trackers.Values)
                t.ApplySmoothing();

            // --- LEFT_FOOT → left stick + AB ---
            if (trackers.TryGetValue("LEFT_FOOT", out var leftFoot))
            {
                xbox.SetAxisValue(Xbox360Axis.LeftThumbX, ApplyDeadzone(leftFoot.SmoothX));
                xbox.SetAxisValue(Xbox360Axis.LeftThumbY, ApplyDeadzone(leftFoot.SmoothZ));

                xbox.SetButtonState(Xbox360Button.A, leftFoot.SmoothY > 0.65f);
                xbox.SetButtonState(Xbox360Button.B, leftFoot.SmoothY < -0.65f);
            }

            // --- RIGHT_FOOT → right stick + XY ---
            if (trackers.TryGetValue("RIGHT_FOOT", out var rightFoot))
            {
                xbox.SetAxisValue(Xbox360Axis.RightThumbX, ApplyDeadzone(rightFoot.SmoothX));
                xbox.SetAxisValue(Xbox360Axis.RightThumbY, ApplyDeadzone(rightFoot.SmoothZ));

                xbox.SetButtonState(Xbox360Button.X, rightFoot.SmoothY > 0.65f);
                xbox.SetButtonState(Xbox360Button.Y, rightFoot.SmoothY < -0.65f);
            }

            // --- LEFT_HAND → left trigger ---
            if (trackers.TryGetValue("LEFT_HAND", out var leftHand))
                xbox.SetSliderValue(Xbox360Slider.LeftTrigger, leftHand.GetTriggerValue());

            // --- RIGHT_HAND → right trigger ---
            if (trackers.TryGetValue("RIGHT_HAND", out var rightHand))
                xbox.SetSliderValue(Xbox360Slider.RightTrigger, rightHand.GetTriggerValue());
        }


        static short ApplyDeadzone(float value, float deadzone = 0.15f)
        {
            value = Math.Clamp(value, -1f, 1f);
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
