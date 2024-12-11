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
        internal static readonly string[] mimeValue = ["application/zip"];

        public static async void ShowRestoreDialogAsync()
        {
            string? sourceFile = await PickFileToRestore();
            if (sourceFile == null || sourceFile == string.Empty)
            {
                Serilog.Log.Information("No filename to restore from");
                return;
            }

            Serilog.Log.Warning("SourceFile:" + sourceFile);

            if (await UnPackArchive(sourceFile, Fragment_Preferences.rootPath + "/Temp") == false)
            {
                Serilog.Log.Error("Failed to unpack archive file");
                return;
            }

            //GUI options for restore
            FragmentActivity? activity = (FragmentActivity?)Platform.CurrentActivity;
            AndroidX.Fragment.App.FragmentTransaction? fragmentTransaction = activity?.SupportFragmentManager.BeginTransaction();
            AndroidX.Fragment.App.Fragment? fragmentPrev = activity?.SupportFragmentManager.FindFragmentByTag("dialog");
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

            Serilog.Log.Fatal($"Waiting here now");
        }

        private static async Task<string?> PickFileToRestore()
        {
            try
            {
                var options = new PickOptions
                {
                    PickerTitle = "Please select an activities file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, mimeValue},
                    })
                };

                var sourceFile = await FilePicker.PickAsync(options);
                if (sourceFile == null)
                {
                    Serilog.Log.Fatal($"Failed to select file to restore");
                    return null;
                }

                Serilog.Log.Warning("SourceFile:" + sourceFile.FullPath);
                return sourceFile.FullPath;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Failed to select file to restore");
                return null;
            }
        }

        private static async Task<bool> UnPackArchive(string fileName, string tmpFolder)
        {
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
                    }
                }

                await Task.Run(() =>
                {
                    using (Stream stream = File.OpenRead(fileName))
                    using (var reader = ReaderFactory.Open(stream))
                    {
                        while (reader.MoveToNextEntry())
                        {
                            if (!reader.Entry.IsDirectory)
                            {
                                if (reader.Entry.Key != null)
                                {
                                    Serilog.Log.Information(reader.Entry.Key);
                                }

                                reader.WriteEntryToDirectory(tmpFolder, new ExtractionOptions()
                                {
                                    ExtractFullPath = true,
                                    Overwrite = true
                                });
                            }
                        }
                    }
                });

                //Files from backup archive
                foreach (string files in Directory.GetFiles(tmpFolder))
                    Serilog.Log.Debug(files);

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
