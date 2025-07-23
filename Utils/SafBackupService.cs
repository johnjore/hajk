using Android.App;
using Android.Content;
using Android.Net;
using Android.Preferences;
using Android.Widget;
using AndroidX.DocumentFile.Provider;
using System.IO;
using System.Threading.Tasks;

namespace hajk
{
    public static class SafBackupService
    {
        private const int RequestCodeOpenFolder = 1001;
        public const string PrefKeySafFolderUri = "saf_backup_uri";

        public static void RequestFolderSelection(Activity activity)
        {
            Intent intent = new Intent(Intent.ActionOpenDocumentTree);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission |
                            ActivityFlags.GrantWriteUriPermission |
                            ActivityFlags.GrantPersistableUriPermission);
            activity.StartActivityForResult(intent, RequestCodeOpenFolder);
        }

        public static void HandleFolderSelection(Activity activity, int requestCode, Result resultCode, Intent data)
        {
            if (requestCode != RequestCodeOpenFolder || resultCode != Result.Ok || data?.Data == null)
                return;

            Android.Net.Uri folderUri = data.Data;

            activity.ContentResolver?.TakePersistableUriPermission(
                folderUri,
                data.Flags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission)
            );

            ISharedPreferences prefs = PreferenceManager.GetDefaultSharedPreferences(activity);
            prefs.Edit().PutString(PrefKeySafFolderUri, folderUri.ToString()).Apply();
        }

        // Returns true if SAF backup folder is set
        public static bool IsFolderSelected(Context context)
        {
            ISharedPreferences? prefs = PreferenceManager.GetDefaultSharedPreferences(context);
            return !string.IsNullOrEmpty(prefs?.GetString(PrefKeySafFolderUri, null));
        }

        public static async Task<bool> SaveBackupFile(Context context, string filename, string content, string mimeType = "text/plain")
        {
            string uriString = Preferences.Get(PrefKeySafFolderUri, "");
            if (string.IsNullOrEmpty(uriString)) 
                return false;

            Android.Net.Uri folderUri = Android.Net.Uri.Parse(uriString);

            var folder = DocumentFile.FromTreeUri(context, folderUri);
            if (folder == null || !folder.IsDirectory) 
                return false;

            // Delete existing if exists
            var existing = folder.FindFile(filename);
            existing?.Delete();

            // Create new file
            var file = folder.CreateFile(mimeType, filename);
            if (file == null) 
                return false;

            using var output = context.ContentResolver.OpenOutputStream(file.Uri, "w");
            using var writer = new StreamWriter(output);
            await writer.WriteAsync(content);
            await writer.FlushAsync();

            return true;
        }
    }
}
