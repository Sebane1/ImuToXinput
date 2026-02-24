using System.Collections.Concurrent;
using System.Net.Sockets;
using SlimeImuProtocol.SlimeProtocol;
using SlimeImuProtocol.SlimeVR;

namespace ImuToXInput.Maui.Services;

/// <summary>
/// Sends haptic (rumble) packets to SlimeVR trackers over UDP (port 6969), following the same pattern as
/// <see href="https://github.com/Sebane1/Everything_To_IMU_SlimeVR/blob/main/PS5-Dualsense-To-IMU-SlimeVR/Tracking/UDPHapticDevice.cs">UDPHapticDevice</see>:
/// per-tracker end time and vibrating state, intensity updates mid-rumble, and a single scheduled stop packet.
/// </summary>
public static class TrackerHapticSender
{
    private const int HapticPort = 6969;
    private const ushort DefaultDurationMs = 150;

    private static readonly ConcurrentDictionary<string, PerTrackerHapticState> _stateByIp = new();

    /// <summary>
    /// Sends rumble to SlimeVR trackers: left motor to left hand/arm, right motor to right hand/arm.
    /// Uses <see cref="ControllerLoopService.GetCurrentTrackers"/>; no-op if the controller loop is not running.
    /// </summary>
    public static void SendRumbleToTrackers(byte leftAmplitude, byte rightAmplitude, ushort durationMs)
    {
        var trackers = ControllerLoopService.GetCurrentTrackers();
        if (trackers == null) return;

        ushort duration = durationMs > 0 ? durationMs : DefaultDurationMs;
        float leftIntensity = leftAmplitude / 255f;
        float rightIntensity = rightAmplitude / 255f;

        string[] leftParts = { "LEFT_HAND", "LEFT_LOWER_ARM", "LEFT_UPPER_ARM" };
        string[] rightParts = { "RIGHT_HAND", "RIGHT_LOWER_ARM", "RIGHT_UPPER_ARM" };

        foreach (var part in leftParts)
        {
            if (trackers.TryGetValue(part, out var t) && !string.IsNullOrEmpty(t.Ip) && leftIntensity > 0)
                EngageHaptics(t.Ip, leftIntensity, duration);
        }
        foreach (var part in rightParts)
        {
            if (trackers.TryGetValue(part, out var t) && !string.IsNullOrEmpty(t.Ip) && rightIntensity > 0)
                EngageHaptics(t.Ip, rightIntensity, duration);
        }
    }

    /// <summary>
    /// Engages haptics on one tracker: updates end time; sends start (or intensity update) if needed;
    /// ensures a single background task will send the stop packet at end time. Matches UDPHapticDevice.EngageHaptics.
    /// </summary>
    public static void EngageHaptics(string trackerIp, float intensity, int durationMs)
    {
        if (string.IsNullOrEmpty(trackerIp)) return;

        var state = _stateByIp.GetOrAdd(trackerIp, ip =>
        {
            try
            {
                var c = new UdpClient();
                c.Connect(ip, HapticPort);
                return new PerTrackerHapticState(c);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tracker haptic setup failed ({ip}): {ex.Message}");
                return null;
            }
        });
        if (state == null) return;

        var endTime = DateTime.UtcNow.AddMilliseconds(durationMs);
        lock (state.Lock)
        {
            state.HapticEndTime = endTime;
            bool sendStart = !state.IsVibrating || intensity != state.LastIntensity;
            state.LastIntensity = intensity;

            if (sendStart)
            {
                var intensityCapture = intensity;
                var durationCapture = durationMs;
                Task.Run(() =>
                {
                    try
                    {
                        var data = state.PacketBuilder.BuildHapticPacket(intensityCapture, durationCapture);
                        state.Client.Send(data.ToArray(), data.Length);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Tracker haptic send failed ({trackerIp}): {ex.Message}");
                    }
                });
            }

            if (!state.IsVibrating)
            {
                state.IsVibrating = true;
                var endTimeCapture = endTime;
                Task.Run(() =>
                {
                    while (DateTime.UtcNow < endTimeCapture)
                        Thread.Sleep(10);

                    lock (state.Lock)
                    {
                        try
                        {
                            var stopData = state.PacketBuilder.BuildHapticPacket(0, 0);
                            state.Client.Send(stopData.ToArray(), stopData.Length);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Tracker haptic stop failed ({trackerIp}): {ex.Message}");
                        }
                        state.IsVibrating = false;
                    }
                });
            }
        }
    }

    /// <summary>
    /// Sends stop (0,0) to all trackers we have state for and clears vibrating state. Matches UDPHapticDevice.DisableHaptics.
    /// </summary>
    public static void DisableHaptics()
    {
        foreach (var kv in _stateByIp)
        {
            var state = kv.Value;
            lock (state.Lock)
            {
                try
                {
                    var data = state.PacketBuilder.BuildHapticPacket(0, 0);
                    state.Client.Send(data.ToArray(), data.Length);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Tracker haptic disable failed ({kv.Key}): {ex.Message}");
                }
                state.IsVibrating = false;
            }
        }
    }

    private sealed class PerTrackerHapticState
    {
        public readonly object Lock = new();
        public readonly UdpClient Client;
        public readonly PacketBuilder PacketBuilder = new("");
        public DateTime HapticEndTime;
        public bool IsVibrating;
        public float LastIntensity;

        public PerTrackerHapticState(UdpClient client)
        {
            Client = client;
        }
    }
}
