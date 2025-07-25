using Android.Content;
using Android.Provider;
using AndroidX.Fragment.App;
using hajk.Fragments;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hajk
{
    internal class Restore
    {
        internal static readonly string mimeValue = "application/zip";

        public static void ShowRestoreDialogAsync()
        {
            SafServices.OnFileSelected = (uri) =>
            {
                Task.Run(async () =>
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Progressbar.UpdateProgressBar.Progress = 0.0;
                        Progressbar.UpdateProgressBar.MessageBody = $"";
                        _ = Progressbar.UpdateProgressBar.CreateGUIAsync("Unpacking archive");
                    });

                    if (UnPackArchive(Platform.AppContext, uri, Fragment_Preferences.rootPath + "/Temp") == false)
                    {
                        Serilog.Log.Error("Failed to unpack archive file");
                        return;
                    }

                    //GUI options for restore
                    FragmentActivity? activity = (FragmentActivity?)Platform.CurrentActivity;
                    FragmentTransaction? fragmentTransaction = activity?.SupportFragmentManager.BeginTransaction();
                    Fragment? fragmentPrev = activity?.SupportFragmentManager.FindFragmentByTag("dialog");
                    if (fragmentPrev != null)
                    {
                        fragmentTransaction?.Remove(fragmentPrev);
                    }

                    fragmentTransaction?.AddToBackStack(null);

                    Fragment_restore dialogFragment = Fragment_restore.NewInstace(null);
                    if (fragmentTransaction != null)
                    {
                        dialogFragment.Show(fragmentTransaction, "dialog");
                    }
                });
            };

            SafServices.LaunchFilePicker(Platform.CurrentActivity, mimeValue);
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

        private static bool UnPackArchive(Context context, Android.Net.Uri? uri, string tmpFolder)
        {
            var filename = GetDisplayNameFromUri(Platform.AppContext, uri);
            if (string.IsNullOrEmpty(filename))
                return false;

            Serilog.Log.Information("SourceFile:" + filename);

            try
            {
                //Make sure temp restore folder exists
                if (!Directory.Exists(tmpFolder))
                {
                    try
                    {
                        Directory.CreateDirectory(tmpFolder);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Fatal(ex, $"Failed to Create Backup Folder");
                        return false;
                    }
                }

                //Count files in file to unpack
                int Entries = 0;
                using (Stream? stream = context?.ContentResolver?.OpenInputStream(uri))
                using (var reader = ReaderFactory.Open(stream))
                {
                    while (reader.MoveToNextEntry())
                    {
                        Entries++;
                    }
                }

                double ProgressBarIncrement = ((double)100 / Entries);
                Serilog.Log.Information($"Entries in Stream '{Entries}'. Each Progressbar increment is '{ProgressBarIncrement}'");

                using (Stream stream = context?.ContentResolver?.OpenInputStream(uri))
                using (var reader = ReaderFactory.Open(stream))
                {
                    while (reader.MoveToNextEntry())
                    {
                        if (!reader.Entry.IsDirectory && reader.Entry.Key != null)
                        {
                            Progressbar.UpdateProgressBar.Progress += ProgressBarIncrement;
                            Progressbar.UpdateProgressBar.MessageBody = $"{Path.GetFileName(reader.Entry.Key)}";
                            Serilog.Log.Information($"Extracting filename: '{reader.Entry.Key}', Progress: {Progressbar.UpdateProgressBar.Progress}");
                        
                            reader.WriteEntryToDirectory(tmpFolder, new ExtractionOptions()
                            {
                                ExtractFullPath = true,
                                Overwrite = true
                            });
                        }
                    }
                }

                //Files from backup archive
                foreach (string files in Directory.GetFiles(tmpFolder))
                    Serilog.Log.Debug(files);

                Progressbar.UpdateProgressBar.Dismiss();
                Serilog.Log.Information("Done");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to unpack archive");
                return false;
            }

            return true;
        }
    }
}
