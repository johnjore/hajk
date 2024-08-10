using Android;
using Android.Content.PM;
using Microsoft.Maui.ApplicationModel;

namespace hajk.Utilities
{
    internal partial class AppPermissions
    {
        /// <summary>
        /// Requests all permissions needed for application
        /// </summary>
        public static async Task<bool> RequestAppPermissions(Activity activity)
        {
            //First round of location permissions
            Serilog.Log.Debug($"Check 'LocationWhenInUse' Permission");
            if (await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() != PermissionStatus.Granted)
            {
                Serilog.Log.Debug($"Request'LocationWhenInUse' Permission");
                await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            //Request AlwaysOn Permission
            Serilog.Log.Debug($"Check 'LocationAlways' Permission");
            if (await Permissions.CheckStatusAsync<Permissions.LocationAlways>() != PermissionStatus.Granted)
            {
                Serilog.Log.Debug($"Request 'LocationAlways' Permission");
                await Permissions.RequestAsync<Permissions.LocationAlways>();
            }

            //Notifications
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                Serilog.Log.Debug($"Check 'PostNotifications' Permission");
                if (activity.CheckSelfPermission(Manifest.Permission.PostNotifications) != Permission.Granted)
                {
                    Serilog.Log.Debug($"Request 'PostNotifications' Permission");
                    activity.RequestPermissions([Manifest.Permission.PostNotifications], 0);
                }
            }

            return true;
        }

        /// <summary>
        /// Notify user if location permission does not allow background collection
        /// </summary>
        public static async Task<bool> LocationPermissionNotification(Activity activity)
        {
            if (await Permissions.CheckStatusAsync<Permissions.LocationAlways>() != PermissionStatus.Granted)
            {
                Serilog.Log.Debug($"We dont have 'LocationAlways' permissions. Notify user");
                using var alert = new AlertDialog.Builder(activity);
                alert.SetTitle(activity.Resources?.GetString(Resource.String.LocationPermissionTitle));
                alert.SetMessage(activity.Resources?.GetString(Resource.String.LocationPermissionDescription));
                alert.SetNeutralButton(Resource.String.Ok, (sender, args) => { });
                var dialog = alert.Create();
                dialog?.SetCancelable(false);
                dialog?.Show();
            }

            return true;
        }
    }
}
