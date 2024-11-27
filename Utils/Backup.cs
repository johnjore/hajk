using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using Android.Content;
using Android.Widget;
using Android.App;
using Android.OS;
using System;
using Android.Content.Res;
using SQLite;
using hajk.Models;
using System.Text.Json;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace hajk
{
    internal class Backup
    {
        public static void ShowBackupDialog()
        {
            //Size of live data
            long RouteDBMB = (new FileInfo(Fragment_Preferences.LiveData + "/" + Fragment_Preferences.RouteDB).Length) / 1024 / 1024;
            long POIDBKB = (new FileInfo(Fragment_Preferences.LiveData + "/" + Fragment_Preferences.POIDB).Length) / 1024;
            long MapSizeMB = (new FileInfo(Fragment_Preferences.LiveData + "/" + Fragment_Preferences.CacheDB).Length) / 1024 / 1024;
            long GeoTiffMB = Utils.Misc.DirectorySizeBytes(new DirectoryInfo(Fragment_Preferences.LiveData + "/" + Fragment_Preferences.GeoTiffFolder)) / 1024 / 1024;

            //Warning: Do NOT Change order, unless checkedItems is also updated!
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
            AlertDialog.Builder builder = new AlertDialog.Builder(Platform.CurrentActivity);
            builder.SetTitle(Resource.String.BackupOptions);

            //Update GUI
            builder.SetMultiChoiceItems(items, checkedItems, (senderDialog, args) =>
            {
                checkedItems[args.Which] = args.IsChecked;
            });

            //Create the backup
            builder.SetPositiveButton(Resource.String.Ok, (senderDialog, args) =>
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

                string? BackupFolder = CreateBackupFolder();
                if (BackupFolder == null || BackupFolder == string.Empty)
                {
                    Serilog.Log.Fatal("No backup folder to use - Very sad");
                    return;
                }

                //Backups
                if (Preferences.Get("BackupPreferences", true))
                {
                    if (BackupPreferences(BackupFolder) == false)
                    {
                        Serilog.Log.Error("Failed to backup Preferences");
                        Toast.MakeText(Platform.AppContext, "Could not backup Preferences", ToastLength.Long)?.Show();
                    }
                }

                if (Preferences.Get("BackupRoute&TrackData", true))
                {
                    if (BackupRouteTrackData(BackupFolder) == false)
                    {
                        Serilog.Log.Error("Failed to backup Route & Track Data");
                        Toast.MakeText(Platform.AppContext, "Could not backup Route & Track Data", ToastLength.Long)?.Show();
                    }
                }

                if (Preferences.Get("BackupPOIData", true))
                {
                    if (BackupPOIData(BackupFolder) == false)
                    {
                        Serilog.Log.Error("Failed to backup POI data");
                        Toast.MakeText(Platform.AppContext, "Could not backup POI Data", ToastLength.Long)?.Show();
                    }
                }

                if (Preferences.Get("BackupMapTiles", true))
                {
                    if (BackupMapTiles(BackupFolder) == false)
                    {
                        Serilog.Log.Error("Failed to backup Map tiles");
                        Toast.MakeText(Platform.AppContext, "Could not backup Map tiles", ToastLength.Long)?.Show();
                    }
                }

                if (Preferences.Get("BackupElevationData", true))
                {
                    if (BackupElevationData(BackupFolder) == false)
                    {
                        Serilog.Log.Error("Failed to backup Elevation data");
                        Toast.MakeText(Platform.AppContext, "Could not backup Elevation data", ToastLength.Long)?.Show();
                    }
                }

                if (CompressBackupFolder(BackupFolder) == false)
                {
                    Serilog.Log.Error("Failed to archive backup");

                    Show_Dialog msg = new(Platform.CurrentActivity);
                    msg.ShowDialog("Failed", "Could not create backup", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
                }

                if (KeepOnlyNBackups() == false)
                {
                    Toast.MakeText(Platform.AppContext, "Failed to remove oldest backup(s)", ToastLength.Long)?.Show();
                }

                Toast.MakeText(Platform.AppContext, "Backup completed", ToastLength.Long)?.Show();
            });

            builder.SetNegativeButton(Resource.String.Cancel, (senderDialog, args) =>
            {
            });

            builder.Create();
            builder.Show();
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
                    KeepNBackups = int.Parse(Preferences.Get("KeepNBackups", Fragment_Preferences.KeepNBackups.ToString())),
                    OSM_BulkDownload_Source = Preferences.Get("Tile Bulk Download Source", Fragment_Preferences.TileBulkDownloadSource),
                    OSM_Browse_Source = Preferences.Get("OSM_Browse_Source", Fragment_Preferences.TileBrowseSource),
                    CustomServerURL = Preferences.Get("CustomServerURL ", ""),
                    CustomToken = Preferences.Get("CustomToken", ""),
                    MapboxToken = Preferences.Get("MapboxToken", ""),
                    ThunderforestToken = Preferences.Get("ThunderforestToken", ""),

                    mapScale = Preferences.Get("mapScale", Fragment_Preferences.DefaultMapScale),
                    mapUTMZone = Preferences.Get("mapUTMZone", Fragment_Preferences.DefaultUTMZone),

                    BackupPreferences = Preferences.Get("BackupPreferences", true),
                    BackupRouteTrackData = Preferences.Get("BackupRoute&TrackData", true),
                    BackupPOIData = Preferences.Get("BackupPOIData", true),
                    BackupMapTiles = Preferences.Get("BackupMapTiles", true),
                    BackupElevationData = Preferences.Get("BackupElevationData", true),                    
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

                SQLiteConnection DBBackupConnection = new SQLiteConnection(Source_DB, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex, true);

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

                SQLiteConnection DBBackupConnection = new SQLiteConnection(Source_DB, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex, true);

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
                string Destination_DB = Path.Combine(BackupFolder, Fragment_Preferences.CacheDB);
                TileCache.MbTileCache.sqlConn.Backup(Destination_DB);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to create backup of CacheDB Database");
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
                Directory.CreateDirectory(Destination_Folder);

                //Copy each file
                foreach (string fileName in Directory.GetFiles(Source_Folder))
                {
                    File.Copy(fileName, Destination_Folder + "/" + Path.GetFileName(fileName));
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
                    Directory.CreateDirectory(backupFolderName);
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

                string Destination_Archive = BackupFolder + ".zip";
                using (var zip = File.OpenWrite(Destination_Archive))
                using (var zipWriter = WriterFactory.Open(zip, ArchiveType.Zip, CompressionType.None))
                {
                    zipWriter.WriteAll(BackupFolder, "*", SearchOption.AllDirectories);
                }

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

            Serilog.Log.Information("Done creating single backup file");
            return true;
        }

        private static bool KeepOnlyNBackups()
        {
            try
            {
                Serilog.Log.Information("Remove old backup files");

                var backupFiles = new DirectoryInfo(Fragment_Preferences.Backups).EnumerateFiles()
                    .OrderBy(fi => fi.CreationTime).ToList();

                if (backupFiles == null)
                {
                    Serilog.Log.Error("backupFiles is 'null' ?");
                    return false;
                }

                if (backupFiles.Count == 0)
                {
                    Serilog.Log.Error("# of backupFiles is '0', which is impossible if we've successfully taken a backup?");
                    return false;
                }

                int KeepNBackups = int.Parse(Preferences.Get("KeepNBackups", Fragment_Preferences.KeepNBackups.ToString()));
                if (backupFiles.Count < KeepNBackups)
                {
                    Serilog.Log.Information($"Not reached max backup copies: '{KeepNBackups}'");
                    return true;
                }

                //How many files to remove?
                for (int i = 0; i < backupFiles.Count - KeepNBackups; i++)
                {
                    File.Delete(backupFiles[i].FullName);
                }

                //Backup files
                backupFiles = new DirectoryInfo(Fragment_Preferences.Backups).EnumerateFiles()
                    .OrderBy(fi => fi.CreationTime).ToList();

                foreach (var fileName in backupFiles)
                {
                    Serilog.Log.Debug($"Backup files: {fileName.FullName} / {fileName.Length} bytes");
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
