using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;

namespace IndoorCO2MapAppV2;

/// <summary>
/// Keeps the app process alive (and the sensor's GATT connection with it) for
/// the duration of a recording. Without this Android's background execution
/// limits throttle and eventually kill the process, which drops the BLE
/// connection — unrecoverable data loss for sensors without on-device history.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeConnectedDevice)]
public class MeasurementForegroundService : Service
{
    private PowerManager.WakeLock? _wakeLock;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        RecordingNotification.EnsureChannel();

        try
        {
            var notification = RecordingNotification.Build();
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
                StartForeground(RecordingNotification.NotificationId, notification, ForegroundService.TypeConnectedDevice);
            else
                StartForeground(RecordingNotification.NotificationId, notification);
        }
        catch (Exception ex)
        {
            // Android 14+ throws SecurityException for the connectedDevice type if
            // BLUETOOTH_CONNECT isn't granted at this exact moment. A service that
            // fails to go foreground must stop itself or the OS kills it hard.
            Logger.WriteToLog("MeasurementForegroundService|StartForeground failed: " + ex.Message);
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        AcquireWakeLock();

        // NotSticky: after a process death there is no recording state to restore
        // here, so a headless restart would only produce an orphan notification.
        // RecoveryManager handles that case through the UI instead.
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        ReleaseWakeLock();
        StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }

    /// <summary>
    /// Held for the service's lifetime so the 30s recording timer still fires
    /// once the device enters Doze with the screen off. Tying it to the service
    /// rather than to RecordingManager means it can never outlive the service.
    /// </summary>
    private void AcquireWakeLock()
    {
        if (_wakeLock?.IsHeld == true) return;

        try
        {
            var pm = (PowerManager?)GetSystemService(PowerService);
            _wakeLock = pm?.NewWakeLock(WakeLockFlags.Partial, "IndoorCO2:Recording");
            _wakeLock?.Acquire();
        }
        catch (Exception ex)
        {
            Logger.WriteToLog("MeasurementForegroundService|AcquireWakeLock failed: " + ex.Message, LogMode.Verbose);
        }
    }

    private void ReleaseWakeLock()
    {
        try
        {
            if (_wakeLock?.IsHeld == true)
                _wakeLock.Release();
        }
        catch (Exception ex)
        {
            Logger.WriteToLog("MeasurementForegroundService|ReleaseWakeLock failed: " + ex.Message, LogMode.Verbose);
        }
        _wakeLock = null;
    }
}
