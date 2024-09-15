using Android;
using Android.App;
using Android.Content.PM;
using static Microsoft.Maui.ApplicationModel.Permissions;

namespace hajk.Utilities
{
    internal partial class AppPermissions
    {
        /// <summary>
        /// Check if TPermission is Granted, and if not, request it
        /// </summary>
        public static async Task<PermissionStatus> CheckAndRequestPermissionAsync<TPermission>() where TPermission : BasePermission, new()
        {
            return await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                TPermission permission = new TPermission();
                Serilog.Log.Debug($"Requesting Permission: '{permission.ToString()}'");
                PermissionStatus status = await permission.CheckStatusAsync();

                if (status != PermissionStatus.Granted)
                {
                    status = await permission.RequestAsync();
                    if (status != PermissionStatus.Granted)
                    {
                        Serilog.Log.Information("Failed to get requested permission '{permission.ToString()}'");
                    }
                    else
                    {
                        Serilog.Log.Debug($"Permission '{permission.ToString()}' granted");
                    }
                }

                return status;
            });
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
