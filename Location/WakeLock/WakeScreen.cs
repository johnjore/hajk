using System.Runtime.Versioning;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Serilog;

namespace hajk.Location.WakeLock;

/// <summary>
/// Black activity that turns the screen on and keeps it on, at minimum brightness, until
/// <see cref="ScreenWake.Hide"/> closes it. Started by the full-screen intent notification posted by
/// <see cref="ScreenWake"/>.
/// <para>
/// The screen is kept on with a window flag, which the system holds itself. A wake lock held by
/// the app is not used for this: Samsung flags it as abusive and drops it after about 3 seconds.
/// </para>
/// </summary>
[Activity(
    Exported = false,
    LaunchMode = Android.Content.PM.LaunchMode.SingleInstance,
    ExcludeFromRecents = true,
    NoHistory = true,
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
public sealed class WakeScreenActivity : Activity
{
    // Safety net only, in case the alarm never closes the activity. Must be longer than the alarm's timeout.
    internal static readonly TimeSpan MaxLifetime = TimeSpan.FromSeconds(75);

    private static readonly Lock Gate = new();
    private static WakeScreenActivity? _current;

    /// <summary>Closes the activity if it is showing.</summary>
    internal static void FinishCurrent()
    {
        WakeScreenActivity? activity;
        lock (Gate)
        {
            activity = _current;
        }

        activity?.RunOnUiThread(() => activity.Finish());
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (OperatingSystem.IsAndroidVersionAtLeast(27))
        {
            SetShowWhenLocked(true);
            SetTurnScreenOn(true);
        }
        else
        {
            Window?.AddFlags(WindowManagerFlags.ShowWhenLocked | WindowManagerFlags.TurnScreenOn);
        }

        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);

        // Nobody is looking at the screen: keep it as dim as the device allows to save battery.
        WindowManagerLayoutParams? attributes = Window?.Attributes;
        if (attributes is not null)
        {
            attributes.ScreenBrightness = 0.01f;
            Window!.Attributes = attributes;
        }

        lock (Gate)
        {
            _current = this;
        }

        new Handler(Looper.MainLooper!).PostDelayed(() => Finish(), (long)MaxLifetime.TotalMilliseconds);
    }

    protected override void OnDestroy()
    {
        lock (Gate)
        {
            if (ReferenceEquals(_current, this))
            {
                _current = null;
            }
        }

        base.OnDestroy();
    }
}

/// <summary>
/// Turns the screen on from the background by posting a full-screen intent notification that
/// starts <see cref="WakeScreenActivity"/>. Needs the USE_FULL_SCREEN_INTENT permission in the manifest.
/// </summary>
internal static class ScreenWake
{
    private const string ChannelId = "hajk_alarm_wake";
    private const int NotificationId = 4711;

    /// <summary>Turns the screen on and keeps it on until <see cref="Hide"/> is called. Never throws.</summary>
    public static void Show(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            Log.Warning("Turning the screen on needs Android 8 or newer, skipped");
            return;
        }

        try
        {
            ShowInternal(context);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not turn the screen on");
        }
    }

    /// <summary>Removes the notification and closes the wake-up screen. Never throws.</summary>
    public static void Hide(Context context)
    {
        try
        {
            (context.GetSystemService(Context.NotificationService) as NotificationManager)?.Cancel(NotificationId);
            WakeScreenActivity.FinishCurrent();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not close the wake-up screen");
        }
    }

    [SupportedOSPlatform("android26.0")]
    private static void ShowInternal(Context context)
    {
        if (context.GetSystemService(Context.NotificationService) is not NotificationManager notificationManager)
        {
            Log.Warning("NotificationManager not available, screen not turned on");
            return;
        }

        if (notificationManager.GetNotificationChannel(ChannelId) is null)
        {
            NotificationChannel channel = new(ChannelId, "Track recording wake-up", NotificationImportance.High);
            channel.SetSound(null, null);
            notificationManager.CreateNotificationChannel(channel);
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(34) && !notificationManager.CanUseFullScreenIntent())
        {
            Log.Warning("Full-screen intents are not allowed for hajk, the screen will not turn on");
        }

        Intent intent = new(context, typeof(WakeScreenActivity));
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);

        PendingIntent? pendingIntent = PendingIntent.GetActivity(
            context,
            0,
            intent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        if (pendingIntent is null)
        {
            Log.Warning("Could not create the wake-up PendingIntent, screen not turned on");
            return;
        }

        Notification.Builder builder = new(context, ChannelId);
        builder.SetSmallIcon(Android.Resource.Drawable.IcDialogInfo);
        builder.SetContentTitle("Recording track");
        builder.SetContentText("Getting a GPS fix");
        builder.SetCategory(Notification.CategoryAlarm);
        builder.SetTimeoutAfter((long)WakeScreenActivity.MaxLifetime.TotalMilliseconds);
        builder.SetFullScreenIntent(pendingIntent, true);

        Notification? notification = builder.Build();
        if (notification is null)
        {
            Log.Warning("Could not build the wake-up notification, screen not turned on");
            return;
        }

        notificationManager.Notify(NotificationId, notification);
    }
}