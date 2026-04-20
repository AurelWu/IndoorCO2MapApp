using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace IndoorCO2MapAppV2;

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeConnectedDevice)]
public class MeasurementForegroundService : Service
{
    private const int NotificationId = 2001;
    private const string ChannelId = "co2_recording";

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        EnsureChannel();
        var notification = BuildNotification();
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            StartForeground(NotificationId, notification, ForegroundService.TypeConnectedDevice);
        else
            StartForeground(NotificationId, notification);
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }

    private void EnsureChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        var nm = (NotificationManager?)GetSystemService(NotificationService);
        if (nm?.GetNotificationChannel(ChannelId) != null) return;
        var ch = new NotificationChannel(ChannelId, "CO2 Recording", NotificationImportance.Low)
        {
            Description = "Keeps the sensor connection alive during recording"
        };
        nm?.CreateNotificationChannel(ch);
    }

    private Notification BuildNotification()
    {
        var tapIntent = new Intent(this, typeof(MainActivity));
        tapIntent.SetFlags(ActivityFlags.SingleTop);
        var pending = PendingIntent.GetActivity(this, 0, tapIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("CO2 Recording Active")
            .SetContentText("Tap to return to the app")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityLow)
            .SetContentIntent(pending)
            .Build()!;
    }
}
