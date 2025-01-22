using Android.Content;
using Android.OS;
using System;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using AndroidX.AppCompat.App;
using Android.App;

namespace hajk.Utilities
{
    internal class BatteryOptimization
    {
        /// <summary>
        /// Opt out of BatteryOptimization
        /// </summary>
        public static bool SetDozeOptimization(Activity? activity)
        {
            if (activity == null)
            {
                return false;
            }
            
            if (Build.VERSION.SdkInt < BuildVersionCodes.M)
            {
                Serilog.Log.Debug($"BatteryOptimizations Not Supported (Requires M or above)");
                return false;
            }

            Serilog.Log.Debug($"Request disabling BatteryOptimizations");
            var intent = new Intent();
            intent.AddFlags(ActivityFlags.NewTask);

            PowerManager? pm = (PowerManager?)activity.GetSystemService(Context.PowerService);
            if (pm != null && pm.IsIgnoringBatteryOptimizations(activity.PackageName) == false)
            {
                intent.SetAction(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
                intent.SetData(Android.Net.Uri.Parse("package:" + activity.PackageName));
                try
                {
                    activity.StartActivity(intent);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, "Failed to set BatteryOptimization");
                }
            }

            return true;
        }

        /// <summary>
        /// Subscribe to PowerSaving Events
        /// </summary>
        public static void BatterySaveModeNotification()
        {
            Serilog.Log.Debug($"Subscribe to PowerSaving Events");
            Battery.EnergySaverStatusChanged += OnEnergySaverStatusChanged;

            Serilog.Log.Debug($"Check if PowerSaving is Enabled or Not");
            OnEnergySaverStatusChanged(null, null);
        }

        /// <summary>
        /// Warn user if PowerSaving is Enabled
        /// </summary>
        private static void OnEnergySaverStatusChanged(object? sender, EnergySaverStatusChangedEventArgs? e)
        {
            if (Battery.EnergySaverStatus == EnergySaverStatus.Off)
            {
                return;
            }

            using var alert = new AndroidX.AppCompat.App.AlertDialog.Builder(Platform.CurrentActivity);
            alert.SetTitle(Platform.CurrentActivity.Resources?.GetString(Resource.String.BatterySaveModeEnabledTitle));
            alert.SetMessage(Platform.CurrentActivity.Resources?.GetString(Resource.String.BatterySaveModeEnabledDescription));
            alert.SetNeutralButton(Resource.String.Ok, (sender, args) => { });
            var dialog = alert.Create();
            dialog?.Show();
        }
    }
}
