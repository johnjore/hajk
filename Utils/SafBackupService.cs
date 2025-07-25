using Android.App;
using Android.Content;
using Android.Net;
using Android.Preferences;
using Android.Provider;
using Android.Widget;
using AndroidX.DocumentFile.Provider;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace hajk
{
    public static class SafBackupService
    {
        private const int RequestCodeOpenFolder = 1001;
        public const int RequestCodeOpenFile = 1002;
        public const string PrefKeySafFolderUri = "saf_backup_uri";
        public static Action<string> OnFileSelected;

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

            var uri = DocumentFile.FromTreeUri(Platform.AppContext, folderUri)?.Uri;
            if (uri?.Authority?.Contains("nextcloud", StringComparison.OrdinalIgnoreCase) == true)
            {
                using var alert = new AndroidX.AppCompat.App.AlertDialog.Builder(activity);
                alert.SetTitle("Warning");
                alert.SetMessage("Nextcloud client is known to not implement SAF correctly");
                alert.SetNeutralButton(Resource.String.Ok, (sender, args) => { });
                var dialog = alert.Create();
                dialog?.Show();
            }
        }

        public static void LaunchFilePicker(Activity activity, string mimeType)
        {
            Intent intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType(mimeType);
            activity.StartActivityForResult(intent, RequestCodeOpenFile);
        }

        public static void HandleFileSelection(Context context, int requestCode, Result resultCode, Intent data)
        {
            if (requestCode != RequestCodeOpenFile || resultCode != Result.Ok || data?.Data == null)
                return;

            var uri = data.Data;
            var filename = GetDisplayNameFromUri(context, uri);
            if (string.IsNullOrEmpty(filename))
                return;

            string destinationPath = Path.Combine(Fragment_Preferences.rootPath, "Temp", filename);
            var success = CopyFileFromSafUri(context, uri, destinationPath);
            if (success)
            {
                OnFileSelected?.Invoke(filename);
            }
        }

        public static string? GetDisplayNameFromUri(Context? context, Android.Net.Uri uri)
        {
            using var cursor = context?.ContentResolver?.Query(uri, null, null, null, null);
            if (cursor != null && cursor.MoveToFirst())
            {
                int nameIndex = cursor.GetColumnIndex(OpenableColumns.DisplayName);
                if (nameIndex >= 0)
                    return cursor.GetString(nameIndex);
            }
            return null;
        }

        // Returns true if SAF backup folder is set
        public static bool IsFolderSelected(Context context)
        {
            ISharedPreferences? prefs = PreferenceManager.GetDefaultSharedPreferences(context);
            return !string.IsNullOrEmpty(prefs?.GetString(PrefKeySafFolderUri, null));
        }

        public static List<DocumentFile> ListFilesInFolder(Context context, string relativeFolderPath = null)
        {
            var prefs = PreferenceManager.GetDefaultSharedPreferences(context);
            var uriString = prefs.GetString(PrefKeySafFolderUri, null);
            if (string.IsNullOrEmpty(uriString))
                return null;

            var baseUri = Android.Net.Uri.Parse(uriString);
            var baseFolder = DocumentFile.FromTreeUri(context, baseUri);
            if (baseFolder == null || !baseFolder.IsDirectory)
                return null;

            DocumentFile targetFolder = baseFolder;

            if (!string.IsNullOrEmpty(relativeFolderPath))
            {
                var parts = relativeFolderPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    var next = targetFolder.FindFile(part);
                    if (next == null || !next.IsDirectory)
                    {
                        return null; // Subfolder not found
                    }
                    targetFolder = next;
                }
            }

            return targetFolder
                .ListFiles()
                .Where(f => f.IsFile && IsTimestampFormat(Path.GetFileNameWithoutExtension(f.Name)))
                .ToList();
        }

        public static void PruneOldFiles(Context context, string relativeFolderPath, string fileExtension, int maxToKeep)
        {
            var files = ListFilesInFolder(context, relativeFolderPath);
            if (files == null || files.Count == 0) return;

            var matchingFiles = files
                .Where(f =>
                    f.IsFile &&
                    f.Name.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase) &&
                    IsTimestampFormat(Path.GetFileNameWithoutExtension(f.Name))
                )
                .OrderByDescending(f => f.LastModified()) // Most recent first
                .ToList();

            if (matchingFiles.Count <= maxToKeep) return;

            var toDelete = matchingFiles.Skip(maxToKeep);
            foreach (var file in toDelete)
            {
                file.Delete();
            }
        }

        private static bool IsTimestampFormat(string nameWithoutExtension)
        {
            return DateTime.TryParseExact(
                nameWithoutExtension,
                "yyMMdd-HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);
        }

        public static bool MoveFileToSaf(Context context, string sourcePath, string targetFilename, string mimeType, string? relativeFolderPath = null)
        {
            if (!File.Exists(sourcePath))
                return false;

            var prefs = PreferenceManager.GetDefaultSharedPreferences(context);
            var uriString = prefs.GetString("saf_backup_uri", null);
            if (string.IsNullOrEmpty(uriString)) 
                return false;

            var baseFolder = DocumentFile.FromTreeUri(context, Android.Net.Uri.Parse(uriString));
            if (baseFolder == null || !baseFolder.IsDirectory) 
                return false;

            var persisted = context.ContentResolver?.PersistedUriPermissions;
            foreach (var perm in persisted)
            {
                Serilog.Log.Debug("SAF", $"Persisted: {perm.Uri}, Read: {perm.IsReadPermission}, Write: {perm.IsWritePermission}");
            }

            DocumentFile targetFolder = baseFolder;
            if (!string.IsNullOrEmpty(relativeFolderPath))
            {
                var parts = relativeFolderPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var next = targetFolder.FindFile(part);
                    if (next == null || !next.IsDirectory)
                        next = targetFolder.CreateDirectory(part);
                    if (next == null) 
                        return false;
                    targetFolder = next;
                }
            }

            //Make sure target is a directory and writeable
            if (!targetFolder.IsDirectory || !targetFolder.CanWrite())
            {
                Serilog.Log.Error("SAF", $"Target folder is not writable or not a directory: {targetFolder.Uri}");
                return false;
            }

            // Delete any existing file
            targetFolder.FindFile(targetFilename)?.Delete();

            var targetFile = targetFolder.CreateFile(mimeType, targetFilename);
            if (targetFile == null) 
                return false;

            using var inputStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read);
            using var outputStream = context.ContentResolver.OpenOutputStream(targetFile.Uri, "w");
            inputStream.CopyTo(outputStream);

            //Remove the source file
            File.Delete(sourcePath);

            return true;
        }

        public static bool CopyFileFromSafUri(Context? context, Android.Net.Uri sourceUri, string destinationPath)
        {
            try
            {
                if (sourceUri == null || string.IsNullOrEmpty(destinationPath))
                    return false;

                using var inputStream = context?.ContentResolver?.OpenInputStream(sourceUri);
                if (inputStream == null)
                    return false;

                using var outputStream = System.IO.File.Create(destinationPath);
                inputStream.CopyTo(outputStream);

                return true;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Failed to copy from SAF");
                return false;
            }
        }
    }
}
