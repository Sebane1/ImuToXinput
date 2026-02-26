using Android.OS;
using Microsoft.Maui.ApplicationModel;

namespace ImuToXInput.Maui.Platforms.Android;

/// <summary>
/// Handles JOCP rumble commands: phone vibrator and SlimeVR trackers (left/right hand and arm).
/// Call from RumbleReceived handler; runs on main thread for API compliance when possible.
/// </summary>
public static class JocpRumbleHandler
{
    /// <summary>Trigger a short test vibration so the user can verify rumble works. Safe to call from UI.</summary>
    public static void TestVibration()
    {
        var dispatcher = Microsoft.Maui.Controls.Application.Current?.Dispatcher;
        if (dispatcher != null)
            dispatcher.Dispatch(() => ApplyRumble(200, 200, 150));
        else
            ApplyRumble(200, 200, 150);
    }

    public static void OnRumble(object? sender, ImuToXInput.Core.Output.JocpRumbleEventArgs e)
    {
        void DoRumble()
        {
            ApplyRumble(e.LeftAmplitude, e.RightAmplitude, e.DurationMs);
            ImuToXInput.Maui.Services.TrackerHapticSender.SendRumbleToTrackers(e.LeftAmplitude, e.RightAmplitude, e.DurationMs);
        }
        var dispatcher = Microsoft.Maui.Controls.Application.Current?.Dispatcher;
        if (dispatcher != null)
            dispatcher.Dispatch(DoRumble);
        else
            DoRumble(); // Fallback when app in background or no window (e.g. some devices delay or drop dispatch)
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
