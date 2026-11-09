using Android.App;
using Android.Content;
using Android.OS;
using Serilog;
using AndroidLocation = Android.Locations.Location;

namespace hajk.Location.WakeLock;
/// <summary>
/// Periodic alarm that keeps recording the track while the device is idle in battery-save mode and
/// the normal location updates stop arriving. Each alarm turns the screen on (dimmed), waits until
/// the recording has grown by one location, and then lets the device go back to sleep.
/// <para>
/// Call <see cref="Schedule"/> when recording starts and <see cref="Cancel"/> when it stops.
/// The alarm does nothing, and does not re-arm itself, while no track is being recorded.
/// </para>
/// </summary>
internal static class Alarms
{
    private const int AlarmRequestCode = 0;
    private const string WakeLockTag = "hajk:AlarmLocation";

    private const string EnableWakeLockKey = "EnableWakeLock";
    private const string RecordingTrackKey = "RecordingTrack";

    // Longest the screen stays on for one alarm. A phone that has been asleep can need a while
    // to get its first fix.
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(60);

    // Safety net only. The wake lock is released as soon as the work finishes.
    // Must be longer than WaitTimeout.
    private static readonly TimeSpan WakeLockTimeout = TimeSpan.FromSeconds(75);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private static volatile bool _waitingForLocation;

    /// <summary>
    /// True while an alarm has woken the device and is waiting for a location to be added to the
    /// recording. The recorder uses it to store the next location straight away instead of waiting
    /// for its next 5 second mark.
    /// </summary>
    internal static bool WaitingForLocation => _waitingForLocation;

    /// <summary>Arms the alarm for the next interval, replacing any pending one. Never throws.</summary>
    public static void Schedule()
    {
        try
        {
            ScheduleAlarm();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not schedule the alarm");
        }
    }

    /// <summary>Cancels the pending alarm (if any) and closes the wake-up screen. Never throws.</summary>
    public static void Cancel()
    {
        try
        {
            CancelAlarm();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not cancel the alarm");
        }

        ScreenWake.Hide(Android.App.Application.Context);
    }

    /// <summary>
    /// Called by <see cref="AlarmReceiver"/> when the alarm fires. Returns quickly. If no track is
    /// being recorded nothing happens and the alarm is not re-armed. Otherwise the alarm is re-armed
    /// and, if the stored fix is stale, the screen is turned on until one location has been added to
    /// the recording.
    /// </summary>
    internal static void OnAlarm()
    {
        if (!IsRecordingTrack())
        {
            Log.Debug("Not recording a track, alarm not re-armed");
            return;
        }

        // Re-arm first so the cycle continues even if the work below fails.
        Schedule();

        AndroidLocation? lastFix = hajk.LocationForegroundService.GetLocation();
        if (!IsNewFixNeeded(lastFix))
        {
            Log.Debug("Recent GPS fix available, nothing to do");
            return;
        }

        // The number of locations in the recording before anything is done. The work below is
        // finished as soon as this has grown, whoever supplied the new location.
        int baselineCount = RecordedCount();

        if (!Preferences.Get(EnableWakeLockKey, Fragment_Preferences.EnableWakeLock))
        {
            Log.Debug("Wake lock is disabled in the settings, nothing to do");
            return;
        }

        Context context = Android.App.Application.Context;

        // Keeps the CPU running until the screen is on. Taken before the receiver returns.
        PowerManager.WakeLock? wakeLock = AcquireWakeLock(context);

        // Doze and battery saver give no GPS fixes while the screen is off. Turning it on lets the
        // foreground service receive fixes and record them again. Set before the screen comes on, so
        // the recorder stores the first fix without waiting for its 5 second mark.
        _waitingForLocation = true;
        ScreenWake.Show(context);

        _ = Task.Run(async () =>
        {
            try
            {
                await WaitForRecordingAsync(baselineCount).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Alarm handling failed");
            }
            finally
            {
                _waitingForLocation = false;

                if (wakeLock?.IsHeld == true)
                {
                    wakeLock.Release();
                }

                ScreenWake.Hide(context);
            }
        });
    }

    private static void ScheduleAlarm()
    {
        Context context = Android.App.Application.Context;

        if (context.GetSystemService(Context.AlarmService) is not AlarmManager alarmManager)
        {
            Log.Warning("AlarmManager not available, alarm not set");
            return;
        }

        PendingIntent? pendingIntent = CreatePendingIntent(context, PendingIntentFlags.Immutable);
        if (pendingIntent is null)
        {
            Log.Warning("Could not create the alarm PendingIntent, alarm not set");
            return;
        }

        long intervalMs = (long)(Fragment_Preferences.WakeLockInterval * 1000d);
        long triggerAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + intervalMs;
        DateTimeOffset triggerTime = DateTimeOffset.FromUnixTimeMilliseconds(triggerAtMs).ToLocalTime();

        // Inexact alarms can be deferred, which shows up as gaps in the recorded track.
        alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMs, pendingIntent);
        Log.Information("Inexact alarm set for {TriggerTime:HH:mm:ss}", triggerTime);
    }

    private static void CancelAlarm()
    {
        Context context = Android.App.Application.Context;

        if (context.GetSystemService(Context.AlarmService) is not AlarmManager alarmManager)
        {
            return;
        }

        PendingIntent? pendingIntent = CreatePendingIntent(context, PendingIntentFlags.Immutable | PendingIntentFlags.NoCreate);
        if (pendingIntent is not null)
        {
            alarmManager.Cancel(pendingIntent);
            pendingIntent.Cancel();
            Log.Debug("Alarm cancelled");
        }
    }

    /// <summary>Returns when the recording has grown, recording has stopped, or <see cref="WaitTimeout"/> is up.</summary>
    private static async Task WaitForRecordingAsync(int baselineCount)
    {
        using CancellationTokenSource timeout = new(WaitTimeout);

        try
        {
            while (IsRecordingTrack())
            {
                if (RecordedCount() > baselineCount)
                {
                    Log.Information("A location was added to the recording, letting the device sleep");
                    return;
                }

                await Task.Delay(PollInterval, timeout.Token).ConfigureAwait(false);
            }
        }
        catch (System.OperationCanceledException)
        {
            Log.Information("Nothing was added to the recording in time. Power save?");
        }
    }

    private static PowerManager.WakeLock? AcquireWakeLock(Context context)
    {
        if (context.GetSystemService(Context.PowerService) is not PowerManager powerManager)
        {
            Log.Warning("PowerManager not available, running without wake lock");
            return null;
        }

        PowerManager.WakeLock? wakeLock = powerManager.NewWakeLock(WakeLockFlags.Partial, WakeLockTag);
        wakeLock?.Acquire((long)WakeLockTimeout.TotalMilliseconds);
        return wakeLock;
    }

    private static PendingIntent? CreatePendingIntent(Context context, PendingIntentFlags flags)
    {
        Intent intent = new(context, typeof(AlarmReceiver));
        return PendingIntent.GetBroadcast(context, AlarmRequestCode, intent, flags);
    }

    private static bool IsRecordingTrack()
    {
        return Preferences.Get(RecordingTrackKey, false);
    }

    /// <summary>The number of locations in the recording. Grows each time the recorder adds one.</summary>
    private static int RecordedCount()
    {
        return hajk.RecordTrack.trackGpx?.Waypoints?.Count ?? 0;
    }

    /// <summary>Only called while recording. True if there is no stored fix or it is older than two update intervals.</summary>
    private static bool IsNewFixNeeded(AndroidLocation? lastFix)
    {
        if (lastFix is null)
        {
            return true;
        }

        long ageMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - lastFix.Time;
        return ageMs > Fragment_Preferences.freq_s * 2 * 1000d;
    }
}