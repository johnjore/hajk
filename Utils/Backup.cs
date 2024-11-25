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
                //Update preferences
                Preferences.Set("BackupPreferences", checkedItems[0]);
                Preferences.Set("BackupRoute&TrackData", checkedItems[1]);
                Preferences.Set("BackupPOIData", checkedItems[2]);
                Preferences.Set("BackupMapTiles", checkedItems[3]);
                Preferences.Set("BackupElevationData", checkedItems[4]);

                string? BackupFolder = CreateBackupFolder();
                if (BackupFolder == null || BackupFolder == string.Empty)
                {
                    Serilog.Log.Error("No backup folder to use - Very sad");
                    return;
                }

                //Backups
                if (Preferences.Get("BackupPreferences", true))
                {
                    BackupPreferences(BackupFolder);
                }

                if (Preferences.Get("BackupRoute&TrackData", true))
                {
                    BackupRouteTrackData(BackupFolder);
                }

                if (Preferences.Get("BackupPOIData", true))
                {
                    BackupPOIData(BackupFolder);
                }

                if (Preferences.Get("BackupMapTiles", true))
                {
                    BackupMapTiles(BackupFolder);
                }

                if (Preferences.Get("BackupElevationData", true))
                {
                    BackupElevationData(BackupFolder);
                }
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
                var PrefSettings = new Models.DefaultPrefSettings.DefaultPrefSettings
                {
                    RecordingTrack = Preferences.Get("RecordingTrack", Fragment_Preferences.RecordingTrack),
                    DrawPOIOnGui = Preferences.Get("DrawPOIOnGui", Fragment_Preferences.DrawPOIonGui_b),
                    freq_s_OffRoute = int.Parse(Preferences.Get("freq_s_OffRoute", Fragment_Preferences.freq_OffRoute_s.ToString())),
                    EnableOffRouteWarning = Preferences.Get("EnableOffRouteWarning", Fragment_Preferences.EnableOffRouteWarning),
                    OffTrackDistanceWarning_m = int.Parse(Preferences.Get("OffTrackDistanceWarning_m", Fragment_Preferences.OffTrackDistanceWarning_m.ToString())),
                    OffTrackRouteSnooze_m = int.Parse(Preferences.Get("OffTrackRouteSnooze_m", Fragment_Preferences.OffRouteSnooze_m.ToString())),
                    DrawTracksOnGui = Preferences.Get("DrawTracksOnGui", Fragment_Preferences.DrawTracksOnGui_b),
                    DrawTrackOnGui = Preferences.Get("DrawTrackOnGui", Fragment_Preferences.DrawTrackOnGui_b),
                    freq = int.Parse(Preferences.Get("freq", Fragment_Preferences.freq_s.ToString())),
                    OSM_BulkDownload_Source = Preferences.Get("OSM_BulkDownload_Source", ""),
                    OSM_Browse_Source = Preferences.Get("OSM_Browse_Source", "OpenStreetMap"),
                    MapboxToken = Preferences.Get("MapboxToken", ""),
                    ThunderforestToken = Preferences.Get("ThunderforestToken", ""),
                    CustomServerURL = Preferences.Get("CustomServerURL ", ""),
                    CustomToken = Preferences.Get("CustomToken", ""),

                    BackupPreferences = Preferences.Get("BackupPreferences", true),
                    BackupRouteTrackData = Preferences.Get("BackupRoute&TrackData", true),
                    BackupPOIData = Preferences.Get("BackupPOIData", true),
                    BackupMapTiles = Preferences.Get("BackupMapTiles", true),
                    BackupElevationData = Preferences.Get("BackupElevationData", true),
                    MapLockNorth = Preferences.Get("MapLockNorth", false),
                    TrackLocation = Preferences.Get("TrackLocation", false),
                    mapUTMZone = Preferences.Get("mapUTMZone", "54H"),
                    mapScale = Preferences.Get("mapScale", 25000L),
                };

                AppContext.SetSwitch("System.Reflection.NullabilityInfoContext.IsSupported", true);
                File.WriteAllText(BackupFolder + "/" + Fragment_Preferences.SavedSettings, JsonSerializer.Serialize(PrefSettings));
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to create backup of settings/preferences");
                return false;
            }

            return true;
        }

        private static bool BackupRouteTrackData(string BackupFolder)
        {
            try
            {
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

            return true;
        }

        private static bool BackupPOIData(string BackupFolder)
        {
            try
            {
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

            return true;
        }

        private static bool BackupMapTiles(string BackupFolder)
        {
            try
            {
                string Destination_DB = Path.Combine(BackupFolder, Fragment_Preferences.CacheDB);
                TileCache.MbTileCache.sqlConn.Backup(Destination_DB);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to create backup of CacheDB Database");
                return false;
            }

            return true;
        }

        private static bool BackupElevationData(string BackupFolder)
        {
            try
            {
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

            return true;
        }

        private static string? CreateBackupFolder()
        {
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
                    return backupFolderName;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, $"Failed to Create Backup Folder");
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
    }
}


