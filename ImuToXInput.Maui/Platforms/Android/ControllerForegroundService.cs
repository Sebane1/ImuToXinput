using Android.App;
using Android.Content;
using Android.OS;

namespace ImuToXInput.Maui.Platforms.Android;

/// <summary>
/// Foreground service that keeps the controller mapping loop running when the app is in the background.
/// Shows a persistent notification so Android does not throttle the process.
/// </summary>
[Service(Name = "com.imutoxinput.maui.ControllerForegroundService", Exported = false)]
public class ControllerForegroundService : Service
{
    public const int NotificationId = 9001;
    public const string ChannelId = "imutoxinput_controller";

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateNotificationChannel();
        var notification = BuildNotification();
        StartForeground(NotificationId, notification);
        return StartCommandResult.Sticky;
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;
        var channel = new NotificationChannel(ChannelId, "Controller active", NotificationImportance.Low)
        {
            Description = "Keeps gamepad input active when the app is in the background."
        };
        channel.SetShowBadge(false);
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(channel);
    }

    private Notification BuildNotification()
    {
        var intent = PackageManager?.GetLaunchIntentForPackage(PackageName ?? "com.imutoxinput.maui");
        var pending = PendingIntent.GetActivity(this, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var builder = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? new Notification.Builder(this, ChannelId)
            : new Notification.Builder(this);
        var notification = builder
            .SetContentTitle("ImuToXInput")
            .SetContentText("Controller active")
            .SetSmallIcon(global::Android.Resource.Drawable.StatNotifyMore)
            .SetContentIntent(pending)
            .SetOngoing(true)
            .Build();
        return notification;
    }
}
