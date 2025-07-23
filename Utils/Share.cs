using Android.Content;
using AndroidX.Core.Content;
using Android.Net;
using System.IO;
using Java.IO;

namespace hajk
{
    internal class Share
    {
        public static void ShareFile(Context context, string filePath, string mimeType = "application/gpx+xml")
        {
            var file = new Java.IO.File(filePath);

            if (!file.Exists())
            {
                Serilog.Log.Fatal($"File does not exist '{filePath}'");
                return;
            }

            var fileUri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                context,
                context.PackageName + ".fileprovider",
                file
            );

            var shareIntent = new Intent(Intent.ActionSend);
            shareIntent.SetType(mimeType);
            shareIntent.PutExtra(Intent.ExtraStream, fileUri);
            shareIntent.AddFlags(ActivityFlags.GrantReadUriPermission);

            Intent? chooser = Intent.CreateChooser(shareIntent, "Share using...");
            chooser?.AddFlags(ActivityFlags.NewTask);

            context.StartActivity(chooser);
        }

        public static void CleanSharefolder()
        {
            //Make sure folder exists
            if (Directory.Exists(Fragment_Preferences.ShareFolder) == false)
                Directory.CreateDirectory(Fragment_Preferences.ShareFolder);

            //Delete all shared files
            foreach (string fileName in Directory.GetFiles(Fragment_Preferences.ShareFolder))
            {
                Serilog.Log.Information($"Deleting: '{fileName}'");
                System.IO.File.Delete(fileName);
            }
        }
    }
}