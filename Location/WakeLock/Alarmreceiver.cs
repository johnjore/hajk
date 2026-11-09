using Android.App;
using Android.Content;
using Serilog;

namespace hajk.Location.WakeLock;

/// <summary>Receives the periodic alarm set by <see cref="Alarms.Schedule"/>.</summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class AlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        Log.Debug("Alarm going off at {Time:HH:mm:ss}", DateTime.Now);

        // Returns quickly. The location request runs in the background, the process is kept alive
        // by LocationForegroundService and the CPU by the wake lock taken inside OnAlarm.
        try
        {
            Alarms.OnAlarm();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Alarm handling failed");
        }
    }
}