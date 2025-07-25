using Android.App;
using Android.Content.Res;
using Android.Content;
using Android.OS;
using Android.Widget;
using AndroidX.DocumentFile.Provider;
using SharpCompress.Common;
using SharpCompress.Writers;
using SQLite;
using System.Text.Json;
using System;

namespace hajk
{
    internal class Backup
    {
        public static void RunDailyBackup()
        {
            bool[] checkedItems = [
                Preferences.Get("BackupPreferences", true),
                Preferences.Get("BackupRoute&TrackData", true),
                Preferences.Get("BackupPOIData", true),
                Preferences.Get("BackupMapTiles", true),
                Preferences.Get("BackupElevationData", true),
            ];

            //All backup files
            var baseFiles = SafBackupService.ListFilesInFolder(Platform.AppContext);
            foreach (DocumentFile fileName in baseFiles)
            {
                Serilog.Log.Debug($"Backup files: {fileName.Name} / {fileName.Length()} bytes / Modified: {fileName.LastModified()}");
            }

            //Backup taken today?
            foreach (DocumentFile fileName in baseFiles)
            {
                Serilog.Log.Debug($"Backup files: {fileName.Name} / {fileName.Length()} bytes / Modified: {fileName.LastModified()}");

                var localDate = DateTimeOffset
                    .FromUnixTimeMilliseconds(fileName.LastModified())
                    .DateTime
                    .ToLocalTime();

                Serilog.Log.Debug($"Backup files: {fileName.Name} / {fileName.Length()} bytes / Modified: {fileName.LastModified()} / '{localDate.Date}'");

                if (localDate.Date >= DateTime.Today)
                {
                    Serilog.Log.Information($"Backup already taken today '{DateTime.Today.ToString("yyMMdd")}'");
                    return;
                }
            }

            //Create progress bar
            _ = Progressbar.UpdateProgressBar.CreateGUIAsync("Backup Progress");
            Progressbar.UpdateProgressBar.Progress = 0;
            Progressbar.UpdateProgressBar.MessageBody = $"Starting backup";
            int ProgressBarIncrement = (int)Math.Ceiling((double)(100 / (checkedItems.Count(c => c == true) + 2))); //2 = Compress and remove old backup files

            Task.Run(() =>
            {
                PerformBackups(ProgressBarIncrement);
            });
        }

        public static void ShowBackupDialog()
        {
            //Size of live data
            long RouteDBMB = (new FileInfo(Fragment_Preferences.LiveData + "/" + Fragment_Preferences.RouteDB).Length) / 1024 / 1024;
            long POIDBKB = (new FileInfo(Fragment_Preferences.LiveData + "/" + Fragment_Preferences.POIDB).Length) / 1024;
            long MapSizeMB = Utils.Misc.DirectorySizeBytes(new DirectoryInfo(Fragment_Preferences.MapFolder)) / 1024 / 1024;
            long GeoTiffMB = Utils.Misc.DirectorySizeBytes(new DirectoryInfo(Fragment_Preferences.LiveData + "/" + Fragment_Preferences.GeoTiffFolder)) / 1024 / 1024;

            //Warning: Do NOT Change order, unless updated in multiple places!
            string[] items = [
                "Preferences",
                "Route/Track Data (" + RouteDBMB.ToString() + " MB)",
                "POI Data (" + POIDBKB.ToString() + " KB)",
                "Map Tiles (" + MapSizeMB.ToString() + " MB)",
                "Elevation Data (" + GeoTiffMB.ToString() + " MB)",
            ];

            bool[] checkedItems = [
                Preferences.Get("BackupPreferences", true),
                Preferences.Get("BackupRoute&TrackData", true),
                Preferences.Get("BackupPOIData", true),
                Preferences.Get("BackupMapTiles", true), 
                Preferences.Get("BackupElevationData", true),                
            ];

            //Create dialogbox
            AlertDialog.Builder builder = new(Platform.CurrentActivity);
            builder.SetTitle(Resource.String.BackupOptions);

            //Update GUI
            builder.SetMultiChoiceItems(items, checkedItems, (senderDialog, args) =>
            {
                checkedItems[args.Which] = args.IsChecked;
            });

            //Create the backup
            builder.SetPositiveButton(Resource.String.Backup, (senderDialog, args) =>
            {
                //Update/save preferences
                Preferences.Set("BackupPreferences", checkedItems[0]);
                Preferences.Set("BackupRoute&TrackData", checkedItems[1]);
                Preferences.Set("BackupPOIData", checkedItems[2]);
                Preferences.Set("BackupMapTiles", checkedItems[3]);
                Preferences.Set("BackupElevationData", checkedItems[4]);

                if (Preferences.Get("BackupPreferences", true) == false &&
                    Preferences.Get("BackupRoute&TrackData", true) == false &&
                    Preferences.Get("BackupPOIData", true) == false &&
                    Preferences.Get("BackupMapTiles", true) == false &&
                    Preferences.Get("BackupElevationData", true) == false)
                {
                    Serilog.Log.Warning("No backup selections");
                    Toast.MakeText(Platform.AppContext, "Nothing to backup", ToastLength.Long)?.Show();
                    return;
                }

                //Create progress bar
                _ = Progressbar.UpdateProgressBar.CreateGUIAsync("Backup Progress");
                Progressbar.UpdateProgressBar.Progress = 0;
                Progressbar.UpdateProgressBar.MessageBody = $"Starting backup";
                int ProgressBarIncrement = (int)Math.Ceiling((double)(100 / (checkedItems.Count(c => c == true) + 2))); //2 = Compress and remove old backup files

                Task.Run(() =>
                {
                    PerformBackups(ProgressBarIncrement);
                });
            });

            builder.SetNegativeButton(Resource.String.Cancel, (senderDialog, args) =>
            {
            });

            builder.SetNeutralButton(Resource.String.SavePreference, (senderDialog, args) =>
            {
                //Update/save preferences
                Preferences.Set("BackupPreferences", checkedItems[0]);
                Preferences.Set("BackupRoute&TrackData", checkedItems[1]);
                Preferences.Set("BackupPOIData", checkedItems[2]);
                Preferences.Set("BackupMapTiles", checkedItems[3]);
                Preferences.Set("BackupElevationData", checkedItems[4]);
            });

            builder.Create();
            builder.Show();
        }

        private static bool PerformBackups(int ProgressBarIncrement)
        {
            try
            {
                //Backup results, default is success
                bool r1 = true;
                bool r2 = true;
                bool r3 = true;
                bool r4 = true;
                bool r5 = true;
                bool r6 = true;
                bool r7 = true;

                string? BackupFolder = CreateBackupFolder();
                if (BackupFolder == null || BackupFolder == string.Empty)
                {
                    Serilog.Log.Fatal("No backup folder to use - Very sad");
                    return false;
                }

                //Needed for Toast.MakeText() to work
                if (Looper.MyLooper() == null)
                {
                    Looper.Prepare();
                }

                //Backups
                if (Preferences.Get("BackupPreferences", true))
                {
                    Progressbar.UpdateProgressBar.MessageBody = "Preferences...";
                    r1 = BackupPreferences(BackupFolder);
                    Progressbar.UpdateProgressBar.Progress += ProgressBarIncrement;
                    Progressbar.UpdateProgressBar.MessageBody = "Done with Preferences";
                    if (r1 == false)
                    {
                        Toast.MakeText(Platform.AppContext, "Could not backup Preferences", ToastLength.Long)?.Show();
                    }
                }

                if (Preferences.Get("BackupRoute&TrackData", true))
                {
                    Progressbar.UpdateProgressBar.MessageBody = "Route & track data...";
                    r2 = BackupRouteTrackData(BackupFolder);
                    Progressbar.UpdateProgressBar.Progress += ProgressBarIncrement;
                    Progressbar.UpdateProgressBar.MessageBody = "Done with route & track data";
                    if (r2 == false)
                    {
                        Toast.MakeText(Platform.AppContext, "Could not backup Route & Track data", ToastLength.Long)?.Show();
                    }
                }

                if (Preferences.Get("BackupPOIData", true))
                {
                    Progressbar.UpdateProgressBar.MessageBody = "POI Data...";
                    r3 = BackupPOIData(BackupFolder);
                    Progressbar.UpdateProgressBar.Progress += ProgressBarIncrement;
                    Progressbar.UpdateProgressBar.MessageBody = "Done with POI Data";
                    if (r3 == false)
                    {
                        Toast.MakeText(Platform.AppContext, "Could not backup POI Data", ToastLength.Long)?.Show();
                    }
                }

                if (Preferences.Get("BackupMapTiles", true))
                {
                    Progressbar.UpdateProgressBar.MessageBody = "Map Tiles...";
                    r4 = BackupMapTiles(BackupFolder);
                    Progressbar.UpdateProgressBar.Progress += ProgressBarIncrement;
                    Progressbar.UpdateProgressBar.MessageBody = "Done with up map Tiles";
                    if (r4 == false)
                    {
                        Toast.MakeText(Platform.AppContext, "Could not backup map tiles", ToastLength.Long)?.Show();
                    }
                }

                if (Preferences.Get("BackupElevationData", true))
                {
                    Progressbar.UpdateProgressBar.MessageBody = "Elevation data...";
                    r5 = BackupElevationData(BackupFolder);
                    Progressbar.UpdateProgressBar.Progress += ProgressBarIncrement;
                    Progressbar.UpdateProgressBar.MessageBody = "Done with elevation data";
                    if (r5 == false)
                    {
                        Toast.MakeText(Platform.AppContext, "Failed to backup Elevation data", ToastLength.Long)?.Show();
                    }
                }

                Progressbar.UpdateProgressBar.MessageBody = "Creating archive";
                r6 = CompressBackupFolder(BackupFolder);
                Progressbar.UpdateProgressBar.Progress += ProgressBarIncrement;
                Progressbar.UpdateProgressBar.MessageBody = "Done creating archive";
                if (r6 == false)
                {
                    Toast.MakeText(Platform.AppContext, "Failed to create backup archive", ToastLength.Long)?.Show();
                }

                Progressbar.UpdateProgressBar.MessageBody = "Removing old backups";
                r7 = KeepOnlyNBackups();
                Progressbar.UpdateProgressBar.Progress += ProgressBarIncrement;
                Progressbar.UpdateProgressBar.MessageBody = "Done removing old backups";
                if (r7 == false)
                {
                    Toast.MakeText(Platform.AppContext, "Failed to remove oldest backup(s)", ToastLength.Long)?.Show();
                }

                //Only show dialog box if any of the backup steps failed
                if (r1 == false || r2 == false || r3 == false || r4 == false || r5 == false || r6 == false || r7 == false)
                {
                    if (Platform.CurrentActivity != null)
                    {
                        Show_Dialog? msg = new(Platform.CurrentActivity);
                        _ = msg.ShowDialog("Failed", "Could not create backup", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
                        return false;
                    }
                }
                else
                {
                    Toast.MakeText(Platform.AppContext, "Backup completed", ToastLength.Long)?.Show();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Crashed while performing backup");
            }
            finally
            {
                //Remove progress bar
                Progressbar.UpdateProgressBar.Dismiss();
            }

            Serilog.Log.Information("Backup completed");
            return true;
        }

        private static bool BackupPreferences(string BackupFolder)
        {
            try
            {
                Serilog.Log.Information("Backing up Preferences");
                var PrefSettings = new Models.DefaultPrefSettings.DefaultPrefSettings
                {
                    //No need to save these
                    //RecordingTrack = Preferences.Get("RecordingTrack", Fragment_Preferences.RecordingTrack),
                    //TrackLocation = Preferences.Get("TrackLocation", false),

                    DrawPOIOnGui = Preferences.Get("DrawPOIOnGui", Fragment_Preferences.DrawPOIonGui_b),
                    freq_s_OffRoute = int.Parse(Preferences.Get("freq_s_OffRoute", Fragment_Preferences.freq_OffRoute_s.ToString())),
                    EnableOffRouteWarning = Preferences.Get("EnableOffRouteWarning", Fragment_Preferences.EnableOffRouteWarning),
                    OffTrackDistanceWarning_m = int.Parse(Preferences.Get("OffTrackDistanceWarning_m", Fragment_Preferences.OffTrackDistanceWarning_m.ToString())),
                    OffTrackRouteSnooze_m = int.Parse(Preferences.Get("OffTrackRouteSnooze_m", Fragment_Preferences.OffRouteSnooze_m.ToString())),

                    DrawTracksOnGui = Preferences.Get("DrawTracksOnGui", Fragment_Preferences.DrawTracksOnGui_b),
                    DrawTrackOnGui = Preferences.Get("DrawTrackOnGui", Fragment_Preferences.DrawTrackOnGui_b),
                    freq = int.Parse(Preferences.Get("freq", Fragment_Preferences.freq_s.ToString())),
                    MapLockNorth = Preferences.Get("MapLockNorth", Fragment_Preferences.DisableMapRotate_b),
                    OSM_Browse_Source = Preferences.Get("OSM_Browse_Source", Fragment_Preferences.TileBrowseSource),
                    CustomServerURL = Preferences.Get("CustomServerURL ", ""),
                    CustomToken = Preferences.Get("CustomToken", ""),
                    MapboxToken = Preferences.Get("MapboxToken", ""),
                    StadiaToken = Preferences.Get("StadiaToken", ""),
                    ThunderforestToken = Preferences.Get("ThunderforestToken", ""),

                    mapScale = Preferences.Get("mapScale", Fragment_Preferences.DefaultMapScale),
                    mapUTMZone = Preferences.Get("mapUTMZone", Fragment_Preferences.DefaultUTMZone),

                    //Backup Settings
                    BackupPreferences = Preferences.Get("BackupPreferences", true),
                    BackupRouteTrackData = Preferences.Get("BackupRoute&TrackData", true),
                    BackupPOIData = Preferences.Get("BackupPOIData", true),
                    BackupMapTiles = Preferences.Get("BackupMapTiles", true),
                    BackupElevationData = Preferences.Get("BackupElevationData", true),
                    KeepNBackups = int.Parse(Preferences.Get("KeepNBackups", Fragment_Preferences.KeepNBackups.ToString())),
                    EnableBackupAtStartup = Preferences.Get("BackupElevationData", Fragment_Preferences.EnableBackupAtStartup),

                    //GPX Routes / Track Sorting Preferences
                    GPXSortingChoice = Preferences.Get("GPXSortingChoice", Fragment_Preferences.GPXSortingChoice),
                    GPXSortingOrder = Preferences.Get("GPXSortingOrder", (int)Fragment_Preferences.GPXSortingOrder),
                };

                AppContext.SetSwitch("System.Reflection.NullabilityInfoContext.IsSupported", true);
                File.WriteAllText(BackupFolder + "/" + Fragment_Preferences.SavedSettings, JsonSerializer.Serialize(PrefSettings));
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to create backup of settings/preferences");
                return false;
            }

            Serilog.Log.Information("Done backing up Preferences");
            return true;
        }

        private static bool BackupRouteTrackData(string BackupFolder)
        {
            try
            {
                Serilog.Log.Information("Backing up Route & Track Data");
                string Source_DB = Path.Combine(Fragment_Preferences.LiveData, Fragment_Preferences.RouteDB);
                string Destination_DB = Path.Combine(BackupFolder, Fragment_Preferences.RouteDB);

                SQLiteConnection DBBackupConnection = new(Source_DB, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex, true);

                DBBackupConnection.Backup(Destination_DB);
                DBBackupConnection.Close();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to create backup of Route & Track Database");
                return false;
            }

            Serilog.Log.Information("Done backing up route & Track Data");
            return true;
        }

        private static bool BackupPOIData(string BackupFolder)
        {
            try
            {
                Serilog.Log.Information("Backing up POI Data");
                string Source_DB = Path.Combine(Fragment_Preferences.LiveData,  Fragment_Preferences.POIDB);
                string Destination_DB = Path.Combine(BackupFolder, Fragment_Preferences.POIDB);

                SQLiteConnection DBBackupConnection = new(Source_DB, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex, true);

                DBBackupConnection.Backup(Destination_DB);
                DBBackupConnection.Close();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to create backup of POI Database");
                return false;
            }

            Serilog.Log.Information("Done backing up POI Data");
            return true;
        }

        private static bool BackupMapTiles(string BackupFolder)
        {
            try
            {
                Serilog.Log.Information("Backing up Map Tiles");

                string Source_Folder = Fragment_Preferences.MapFolder;
                string Destination_Folder = BackupFolder + "/" + Path.GetFileName(Fragment_Preferences.MapFolder);
                _ = Directory.CreateDirectory(Destination_Folder);

                //Current contents
                Serilog.Log.Debug("Current files in MapTiles Source Folder:");
                foreach (string fileName in Directory.GetFiles(Source_Folder))
                    Serilog.Log.Debug(fileName);

                string TileBrowseSource = Preferences.Get(Platform.CurrentActivity?.GetString(Resource.String.OSM_Browse_Source), Fragment_Preferences.TileBrowseSource);

                //Copy each file, except the active mbtiles file (its open, sharing violation, or inconcistent)
                foreach (string fileName in Directory.GetFiles(Source_Folder))
                {
                    if (fileName.Contains(TileBrowseSource) == false)
                    {
                        File.Copy(fileName, Destination_Folder + "/" + Path.GetFileName(fileName), true);
                    }
                }

                //Backup active mbtiles
                string Destination_DB = Path.Combine(Destination_Folder, TileBrowseSource + ".mbtiles");
                TileCache.MbTileCache.sqlConn.Backup(Destination_DB);

                //Backup contents
                Serilog.Log.Debug("Current files in MapTiles Destination Folder:");
                foreach (string fileName in Directory.GetFiles(Destination_Folder))
                    Serilog.Log.Debug(fileName);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to create backup of CacheDB Databases");
                return false;
            }

            Serilog.Log.Information("Done backing up Map Tiles");
            return true;
        }

        private static bool BackupElevationData(string BackupFolder)
        {
            try
            {
                Serilog.Log.Information("Backing up Elevation Data (GeoTiffFiles)");

                string Source_Folder = Fragment_Preferences.LiveData + "/" + Fragment_Preferences.GeoTiffFolder;
                string Destination_Folder = BackupFolder + "/" + Fragment_Preferences.GeoTiffFolder;
                _ = Directory.CreateDirectory(Destination_Folder);

                //Current contents
                Serilog.Log.Debug("Current files in GeoTiff Source Folder:");
                foreach (string fileName in Directory.GetFiles(Source_Folder))
                    Serilog.Log.Debug(fileName);

                //Avoid Nextcloud etc from seeing folder as a new one to sync
                using (File.Create(Destination_Folder + "/" + ".noimage")) { };
                using (File.Create(Destination_Folder + "/" + ".nomedia")) { };

                //Copy each file
                foreach (string fileName in Directory.GetFiles(Source_Folder))
                {
                    File.Copy(fileName, Destination_Folder + "/" + Path.GetFileName(fileName), true);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to create backup of GeoTiff files");
                return false;
            }

            //Current contents
            Serilog.Log.Debug("Current files in GeoTiff Backup Folder:");
            foreach (string fileName in Directory.GetFiles(BackupFolder + "/" + Fragment_Preferences.GeoTiffFolder))
                Serilog.Log.Debug(fileName);

            Serilog.Log.Information("Done Backing up GeoTiff Files");
            return true;
        }

        private static string? CreateBackupFolder()
        {
            Serilog.Log.Information("Create Backup Folder");

            //Make sure Backup folder exists
            if (!Directory.Exists(Fragment_Preferences.Backups))
            {
                try
                {
                    Directory.CreateDirectory(Fragment_Preferences.Backups);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, $"Failed to Create Backup Folder");
                    return null;
                }
            }

            //Folder Name
            string backupFolderName = Fragment_Preferences.Backups + "/" + (DateTime.Now).ToString("yyMMdd-HHmmss");

            if (!Directory.Exists(backupFolderName))
            {
                try
                {
                    _ = Directory.CreateDirectory(backupFolderName);
                    Serilog.Log.Information($"Done creating BackupFolder '{backupFolderName}'");
                    return backupFolderName;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, $"Failed to Create Backup Folder '{backupFolderName}'");
                    return null;
                }
            }
            else
            {
                Toast.MakeText(Platform.AppContext, "Could not create backup folder", ToastLength.Short)?.Show();
                Serilog.Log.Fatal($"Folder already exists: '{backupFolderName}'");
                return null;
            }
        }

        private static bool CompressBackupFolder(string BackupFolder)
        {
            try
            {
                Serilog.Log.Information("Create single backup file");

                foreach (string fileName in Directory.GetFiles(BackupFolder))
                    Serilog.Log.Debug(fileName);

                string[] allfiles = Directory.GetFiles(BackupFolder, "*", SearchOption.AllDirectories);

                string ZipFile = BackupFolder + ".zip";
                using (var zip = File.OpenWrite(ZipFile))
                using (var zipWriter = WriterFactory.Open(zip, ArchiveType.Zip, CompressionType.None))
                {
                    //zipWriter.WriteAll(BackupFolder, "*", SearchOption.AllDirectories);

                    foreach (string file in allfiles) 
                    {
                        if (file.Contains(".nomedia") == false && file.Contains(".noimage") == false)
                        {
                            string entryPath = Path.GetFileName(file); //Strip off the full Android pathname
                            zipWriter.Write(entryPath, file);
                        }
                    }
                }

                //Move file to Saf
                if (SafBackupService.MoveFileToSaf(Platform.AppContext, ZipFile, Path.GetFileName(ZipFile), "application/zip", null) == false)
                    return false;

                //Remove Temp Folder
                Utils.Misc.EmptyFolder(BackupFolder);

                foreach (string fileName in Directory.GetFiles(Fragment_Preferences.Backups))
                    Serilog.Log.Debug(fileName);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to compress backup files to single folder");
                return false;
            }

            Serilog.Log.Information("Done creating backup archive");
            return true;
        }

        private static bool KeepOnlyNBackups()
        {
            try
            {
                Serilog.Log.Information("Remove old backup files");

                int KeepNBackups = int.Parse(Preferences.Get("KeepNBackups", Fragment_Preferences.KeepNBackups.ToString()));
                SafBackupService.PruneOldFiles(Platform.AppContext, "ZIP", ".zip", KeepNBackups);

                var baseFiles = SafBackupService.ListFilesInFolder(Platform.AppContext);
                foreach (DocumentFile fileName in baseFiles)
                {
                    Serilog.Log.Debug($"Backup files: {fileName.Name} / {fileName.Length} bytes / Modified: {fileName.LastModified}");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to truncate number of backup copies");
                return false;
            }

            Serilog.Log.Information("Done removing old backup files");
            return true;
        }
    }
}
