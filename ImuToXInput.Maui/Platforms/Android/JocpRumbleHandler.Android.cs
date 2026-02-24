using Android.OS;
using Microsoft.Maui.ApplicationModel;

namespace ImuToXInput.Maui.Platforms.Android;

/// <summary>
/// Handles JOCP rumble commands by driving the device Vibrator (haptic feedback).
/// Call from RumbleReceived handler; runs on main thread for API compliance.
/// </summary>
public static class JocpRumbleHandler
{
    public static void OnRumble(object? sender, ImuToXInput.Core.Output.JocpRumbleEventArgs e)
    {
        Microsoft.Maui.Controls.Application.Current?.Dispatcher?.Dispatch(() =>
        {
            ApplyRumble(e.LeftAmplitude, e.RightAmplitude, e.DurationMs);
        });
        // Forward rumble to SlimeVR trackers (UDP haptic to left/right hand and arm trackers)
        ImuToXInput.Maui.Services.TrackerHapticSender.SendRumbleToTrackers(e.LeftAmplitude, e.RightAmplitude, e.DurationMs);
    }

    private static void ApplyRumble(byte leftAmplitude, byte rightAmplitude, ushort durationMs)
    {
        var ctx = Platform.CurrentActivity?.ApplicationContext ?? global::Android.App.Application.Context;
        if (ctx == null) return;
        var vibrator = (Vibrator?)ctx.GetSystemService(global::Android.Content.Context.VibratorService);
        if (vibrator == null || !vibrator.HasVibrator) return;

        if (leftAmplitude == 0 && rightAmplitude == 0)
        {
            vibrator.Cancel();
            return;
        }

        byte amplitude = Math.Max(leftAmplitude, rightAmplitude);
        int duration = durationMs > 0 ? durationMs : 500; // 0 = indefinite in spec; use 500ms then next command can override

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var effect = VibrationEffect.CreateOneShot(duration, amplitude);
            vibrator.Vibrate(effect);
        }
        else
        {
#pragma warning disable CS0618
            vibrator.Vibrate(duration);
#pragma warning restore CS0618
        }
    }
}
