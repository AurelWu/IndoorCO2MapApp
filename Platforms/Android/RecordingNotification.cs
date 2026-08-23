using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.Recording;
using IndoorCO2MapAppV2.Resources.Strings;

namespace IndoorCO2MapAppV2;

/// <summary>
/// Owns the notification channel and the ongoing recording notification.
/// Kept separate from <see cref="MeasurementForegroundService"/> so the app
/// process can refresh the notification content without binding to the service:
/// notifying the same NotificationId that StartForeground used updates the
/// foreground notification in place.
/// </summary>
public static class RecordingNotification
{
    public const int NotificationId = 2001;
    public const string ChannelId = "co2_recording";

    public static void EnsureChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;

        var ctx = Android.App.Application.Context;
        var nm = (NotificationManager?)ctx.GetSystemService(Context.NotificationService);
        if (nm == null || nm.GetNotificationChannel(ChannelId) != null) return;

        var channel = new NotificationChannel(ChannelId, "CO2 Recording", NotificationImportance.Low)
        {
            Description = Localisation.NotificationChannelDescription
        };
        channel.SetShowBadge(false);
        nm.CreateNotificationChannel(channel);
    }

    public static Notification Build()
    {
        var ctx = Android.App.Application.Context;

        var tapIntent = new Intent(ctx, typeof(MainActivity));
        tapIntent.SetFlags(ActivityFlags.SingleTop);
        var pending = PendingIntent.GetActivity(ctx, 0, tapIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)!;

        return new NotificationCompat.Builder(ctx, ChannelId)
            .SetContentTitle(Localisation.NotificationRecordingTitle)
            .SetContentText(BuildContentText())
            .SetSmallIcon(Resource.Drawable.ic_stat_recording)
            .SetOngoing(true)
            .SetShowWhen(false)
            .SetPriority(NotificationCompat.PriorityLow)
            .SetContentIntent(pending)
            .Build()!;
    }

    /// <summary>
    /// Refreshes the notification content. Safe to call at any time — a missing
    /// POST_NOTIFICATIONS grant simply makes this a no-op, and nothing here may
    /// ever throw into the recording loop.
    /// </summary>
    public static void Update()
    {
        try
        {
            if (!RecordingManager.Instance.IsRecording) return;
            NotificationManagerCompat.From(Android.App.Application.Context)
                .Notify(NotificationId, Build());
        }
        catch (Exception ex)
        {
            Logger.WriteToLog("RecordingNotification|Update failed: " + ex.Message, LogMode.Verbose);
        }
    }

    private static string BuildContentText()
    {
        var recording = RecordingManager.Instance.ActiveRecording;
        if (recording == null)
            return Localisation.NotificationRecordingTapToReturn;

        long elapsedMinutes = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - recording.RecordingStart) / 60000;
        if (elapsedMinutes < 0) elapsedMinutes = 0;

        int ppm = CO2MonitorManager.Instance.CurrentCO2;
        string ppmPart = ppm > 0 ? $"{ppm} ppm · " : string.Empty;

        string name = recording.LocationName;
        if (string.IsNullOrWhiteSpace(name))
            return $"{ppmPart}{elapsedMinutes} min";

        return $"{ppmPart}{elapsedMinutes} min · {name}";
    }
}
