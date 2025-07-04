using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Android.Views;
using hajk.Data;
using hajk.Fragments;
using hajk.Models;
using hajk.Models.DefaultPrefSettings;
using SharpCompress.Common;
using SharpCompress.Readers;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static hajk.TileCache;
using AndroidX.Fragment.App;

namespace hajk.Fragments
{
    public class Fragment_restore : AndroidX.Fragment.App.DialogFragment
    {
        private enum RestoreSelection
        {
            Merge,
            Overwrite,
            Skip,
        }
        private static readonly string tmpFolder = Fragment_Preferences.rootPath + "/Temp";

        public static Fragment_restore NewInstace(Bundle bundle)
        {
            var fragment = new Fragment_restore
            {
                Arguments = bundle
            };
            return fragment;
        }

        public override Android.Views.View? OnCreateView(LayoutInflater? inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            Android.Views.View? view = inflater?.Inflate(Resource.Layout.fragment_restore, container, false);
            if (view == null)
            {
                Serilog.Log.Fatal("View is null");
                return null;
            }

            view?.SetBackgroundColor(Android.Graphics.Color.White);
            Dialog?.Window?.RequestFeature(WindowFeatures.NoTitle);
            Dialog?.SetCanceledOnTouchOutside(false);

            //Only options from backup file should be visible in GUI
            var z1 = view?.FindViewById<TextView>(Resource.Id.lblRestorePreferences);
            var z2 = (view?.FindViewById<RadioGroup>(Resource.Id.radioPreferences));
            if (File.Exists(tmpFolder + "/" + Fragment_Preferences.SavedSettings) == false && z1 != null && z2 != null)
            {
                z1.Visibility = ViewStates.Gone;
                z2.Visibility = ViewStates.Gone;
            }

            var a1 = view?.FindViewById<TextView>(Resource.Id.lblRestoreRouteTrack);
            var a2 = (view?.FindViewById<RadioGroup>(Resource.Id.radioRouteTrack));
            if (File.Exists(tmpFolder + "/" + Fragment_Preferences.RouteDB) == false && a1 != null && a2 != null)
            {
                a1.Visibility = ViewStates.Gone;
                a2.Visibility = ViewStates.Gone;
            }

            var b1 = view?.FindViewById<TextView>(Resource.Id.lblRestorePOI);
            var b2 = view?.FindViewById<RadioGroup>(Resource.Id.radioPOI);
            if (File.Exists(tmpFolder + "/" + Fragment_Preferences.POIDB) == false && b1 != null && b2 != null)
            {
                b1.Visibility = ViewStates.Gone;
                b2.Visibility = ViewStates.Gone;
            }

            var c1 = view?.FindViewById<TextView>(Resource.Id.lblRestoreMap);
            var c2 = view?.FindViewById<RadioGroup>(Resource.Id.radioMapData);
            if (Directory.Exists(tmpFolder + "/" + Path.GetFileName(Fragment_Preferences.MapFolder)) == false && c1 != null && c2 != null)
            {
                c1.Visibility = ViewStates.Gone;
                c2.Visibility = ViewStates.Gone;
            }

            var d1 = view?.FindViewById<TextView>(Resource.Id.lblRestoreElevation);
            var d2 = view?.FindViewById<RadioGroup>(Resource.Id.radioElevationData);
            if (Directory.Exists(tmpFolder + "/" + Fragment_Preferences.GeoTiffFolder) == false && d1 != null && d2 != null)
            {
                d1.Visibility = ViewStates.Gone;
                d2.Visibility = ViewStates.Gone;
            }

            //Buttons
            Android.Widget.Button? btnCancel = view?.FindViewById<Android.Widget.Button>(Resource.Id.btnCancel2);
            if (btnCancel != null)
            {
                btnCancel.Click += delegate
                {
                    //Remove unpacked files
                    Utils.Misc.EmptyFolder(tmpFolder);
                    Dismiss();
                };
            }

            Android.Widget.Button? btnRunRestore = view?.FindViewById<Android.Widget.Button>(Resource.Id.btnRestore);
            if (btnRunRestore != null)
            {
                btnRunRestore.Click += async delegate
                {
                    Dismiss();

                    try
                    {
                        RadioGroup? rb0 = view?.FindViewById<RadioGroup>(Resource.Id.radioPreferences);
                        if (rb0 != null && rb0.Visibility == ViewStates.Visible && rb0.CheckedRadioButtonId != Resource.Id.rb_RT_Skip)
                        {
                            RestorePreferences(tmpFolder);
                        }

                        await Task.Run(() =>
                        {
                            RadioGroup? rb1 = view?.FindViewById<RadioGroup>(Resource.Id.radioRouteTrack);
                            if (rb1 != null && rb1.Visibility == ViewStates.Visible && rb1.CheckedRadioButtonId != Resource.Id.rb_RT_Skip)
                            {
                                MainThread.InvokeOnMainThreadAsync(() =>
                                {
                                    Progressbar.UpdateProgressBar.Progress = 0.0;
                                    Progressbar.UpdateProgressBar.MessageBody = $"";
                                    _ = Progressbar.UpdateProgressBar.CreateGUIAsync("Restoring Routes and Tracks");
                                });

                                //Do not remove. Needed to clear progressbars
                                Thread.Sleep(10);

                                if (rb1.CheckedRadioButtonId == Resource.Id.rb_RT_Merge)
                                {
                                    RestoreRouteTrackData(RestoreSelection.Merge);
                                }
                                else
                                {
                                    RestoreRouteTrackData(RestoreSelection.Overwrite);
                                }
                            }
                        });

                        await Task.Run(() =>
                        {
                            RadioGroup? rb2 = view?.FindViewById<RadioGroup>(Resource.Id.radioPOI);
                            if (rb2 != null && rb2.Visibility == ViewStates.Visible && rb2.CheckedRadioButtonId != Resource.Id.rb_POI_Skip)
                            {
                                MainThread.InvokeOnMainThreadAsync(() =>
                                {
                                    Progressbar.UpdateProgressBar.Progress = 0.0;
                                    Progressbar.UpdateProgressBar.MessageBody = $"";
                                    _ = Progressbar.UpdateProgressBar.CreateGUIAsync("Restoring POIs");
                                });

                                //Do not remove. Needed to clear progressbars
                                Thread.Sleep(10);

                                if (rb2.CheckedRadioButtonId == Resource.Id.rb_POI_Merge)
                                {
                                    RestorePOIData(RestoreSelection.Merge);
                                }
                                else
                                {
                                    RestorePOIData(RestoreSelection.Overwrite);
                                }
                            }
                        });

                        await Task.Run(() =>
                        {
                            RadioGroup? rb3 = view?.FindViewById<RadioGroup>(Resource.Id.radioMapData);
                            if (rb3 != null && rb3.Visibility == ViewStates.Visible && rb3.CheckedRadioButtonId != Resource.Id.rb_Map_Skip)
                            {
                                MainThread.InvokeOnMainThreadAsync(() =>
                                {
                                    Progressbar.UpdateProgressBar.Progress = 0.0;
                                    Progressbar.UpdateProgressBar.MessageBody = $"";
                                    _ = Progressbar.UpdateProgressBar.CreateGUIAsync("Restoring Map Tiles");
                                });

                                //Do not remove. Needed to clear progressbars
                                Thread.Sleep(10);

                                if (rb3.CheckedRadioButtonId == Resource.Id.rb_Map_Merge)
                                {
                                    RestoreMapTiles(RestoreSelection.Merge);
                                }
                                else
                                {
                                    RestoreMapTiles(RestoreSelection.Overwrite);
                                }
                            }
                        });

                        await Task.Run(() =>
                        {
                            RadioGroup? rb4 = view?.FindViewById<RadioGroup>(Resource.Id.radioElevationData);
                            if (rb4 != null && rb4.Visibility == ViewStates.Visible && rb4.CheckedRadioButtonId != Resource.Id.rb_Elevation_Skip)
                            {
                                MainThread.InvokeOnMainThreadAsync(() =>
                                {
                                    Progressbar.UpdateProgressBar.Progress = 0.0;
                                    Progressbar.UpdateProgressBar.MessageBody = $"";
                                    _ = Progressbar.UpdateProgressBar.CreateGUIAsync("Restoring Elevation Tiles");
                                });

                                //Do not remove. Needed to clear progressbars
                                Thread.Sleep(10);

                                if (rb4.CheckedRadioButtonId == Resource.Id.rb_Elevation_Merge)
                                {
                                    RestoreElevationData(RestoreSelection.Merge);
                                }
                                else
                                {
                                    RestoreElevationData(RestoreSelection.Overwrite);
                                }
                            }
                        });

                        //Remove unpacked files
                        Utils.Misc.EmptyFolder(tmpFolder);

                        //Show we are done
                        if (Platform.CurrentActivity != null)
                        {
                            Show_Dialog msg = new(Platform.CurrentActivity);
                            msg.ShowDialog(Platform.CurrentActivity.GetString(Resource.String.Done), "Restore completed", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Fatal(ex, "Failed to run restore");
                    }
                };
            }

            return view;
        }
                
        private static bool RestorePreferences(string RestoreFolder)
        {
            try
            {
                Serilog.Log.Information("Restore Preferences/Settings");

                if (File.Exists(RestoreFolder + "/" + Fragment_Preferences.SavedSettings) == false)
                {
                    Serilog.Log.Information("No settings / preference file to restore");
                    return false;
                }

                AppContext.SetSwitch("System.Reflection.NullabilityInfoContext.IsSupported", true);
                string jsonString = File.ReadAllText(RestoreFolder + "/" + Fragment_Preferences.SavedSettings);
                DefaultPrefSettings? prefSettings = JsonSerializer.Deserialize<DefaultPrefSettings>(jsonString);
                if (prefSettings == null)
                {
                    Serilog.Log.Error("No settings to restore");
                    return false;
                }
                //Div
                Preferences.Set("DrawPOIOnGui", prefSettings.DrawPOIOnGui);
                Preferences.Set("freq_s_OffRoute", prefSettings.freq_s_OffRoute.ToString());
                Preferences.Set("EnableOffRouteWarning", prefSettings.EnableOffRouteWarning);
                Preferences.Set("OffTrackDistanceWarning_m", prefSettings.OffTrackDistanceWarning_m.ToString());
                Preferences.Set("OffTrackRouteSnooze_m", prefSettings.OffTrackRouteSnooze_m.ToString());
                Preferences.Set("DrawTracksOnGui", prefSettings.DrawTracksOnGui);
                Preferences.Set("DrawTrackOnGui", prefSettings.DrawTrackOnGui);
                Preferences.Set("freq", prefSettings.freq.ToString());
                Preferences.Set("MapLockNorth", prefSettings.MapLockNorth);
                Preferences.Set("OSM_Browse_Source", prefSettings.OSM_Browse_Source);
                Preferences.Set("CustomServerURL ", prefSettings.CustomServerURL);
                Preferences.Set("CustomToken", prefSettings.CustomToken);
                Preferences.Set("StadiaToken", prefSettings.StadiaToken);
                Preferences.Set("MapboxToken", prefSettings.MapboxToken);
                Preferences.Set("ThunderforestToken", prefSettings.ThunderforestToken);                

                Preferences.Set("mapScale", prefSettings.mapScale);
                Preferences.Set("mapUTMZone", prefSettings.mapUTMZone);

                //Backup Settings
                Preferences.Set("BackupPreferences", prefSettings.BackupPreferences);
                Preferences.Set("BackupRoute&TrackData", prefSettings.BackupRouteTrackData);
                Preferences.Set("BackupPOIData", prefSettings.BackupPOIData);
                Preferences.Set("BackupMapTiles", prefSettings.BackupMapTiles);
                Preferences.Set("BackupElevationData", prefSettings.BackupElevationData);
                Preferences.Set("KeepNBackups", prefSettings.KeepNBackups.ToString());
                Preferences.Set("BackupElevationData", prefSettings.EnableBackupAtStartup);

                //GPX Routes / Track Sorting Preferences
                Preferences.Set("GPXSortingOrder", prefSettings.GPXSortingOrder.ToString());
                Preferences.Set("GPXSortingChoice", prefSettings.GPXSortingChoice.ToString());

                //Remove Settings file
                /**///File.Delete(RestoreFolder + "/" + Fragment_Preferences.SavedSettings);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to restore settings/preferences");
                return false;
            }

            Serilog.Log.Information("Done restore Preferences");
            return true;
        }

        private static bool RestorePOIData(RestoreSelection RestoreChoice)
        {
            try
            {
                string RestoreFile = tmpFolder + "/" + Fragment_Preferences.POIDB;
                Serilog.Log.Information("Restore POI Data");

                if (RestoreChoice == RestoreSelection.Overwrite)
                {
                    Task.Run(async () =>
                    {
                        Progressbar.UpdateProgressBar.Progress = 10;
                        await POIDatabase.ReplaceDBAsync(RestoreFile);
                        Progressbar.UpdateProgressBar.Progress = 90;
                    });
                }

                if (RestoreChoice == RestoreSelection.Merge)
                {
                    Progressbar.UpdateProgressBar.MessageBody = $"Connecting to databases...";
                    var ImportDB = new SQLiteConnection(RestoreFile, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex, true);
                    var POIToImport = ImportDB.Table<GPXDataPOI>();
                    if (POIToImport == null || POIToImport.Count() == 0)
                    {
                        Serilog.Log.Warning("No POI found to restore");
                        Progressbar.UpdateProgressBar.Dismiss();
                        return false;
                    }

                    Serilog.Log.Debug($"POI to import: " + POIToImport.Count().ToString());
                    double ProgressBarIncrement = ((double)100 / POIToImport.Count());
                    Serilog.Log.Debug($"ProgressBarIncrement '{ProgressBarIncrement}'");

                    //All Existing POIs
                    List<GPXDataPOI> allPOIs = POIDatabase.GetPOIAsync().Result;
                    allPOIs.AddRange(POIDatabase.GetPOIAsync().Result);

                    //Import each one
                    foreach (GPXDataPOI newPOI in POIToImport)
                    {
                        Progressbar.UpdateProgressBar.Progress += ProgressBarIncrement;
                        Progressbar.UpdateProgressBar.MessageBody = $"'{newPOI.Name}'";
                        Serilog.Log.Debug($"Restoring: '{newPOI.Name}'");
                        
                        GPXDataPOI? oldPOI = allPOIs.Where(x =>
                            x.Name == newPOI.Name &&
                            x.Description == newPOI.Description &&
                            x.Symbol == newPOI.Symbol &&
                            x.Lat == newPOI.Lat &&
                            x.Lon == newPOI.Lon
                        ).FirstOrDefault();

                        if (oldPOI == null)
                        {
                            newPOI.Id = 0;
                            POIDatabase.SavePOIAsync(newPOI);
                        }

                        //else its a duplicate, do not import                           
                    }

                    ImportDB.Close();

                    //Show POI's on screen
                    DisplayMapItems.AddPOIToMap();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to restore POI Database");
                return false;
            }

            Progressbar.UpdateProgressBar.Dismiss();
            Serilog.Log.Information("Done restoring POI Data");
            return true;
        }

        private static bool RestoreRouteTrackData(RestoreSelection RestoreChoice)
        {
            Serilog.Log.Information("Restore Route & Track Data");

            try
            {
                string RestoreFile = tmpFolder + "/" + Fragment_Preferences.RouteDB;

                if (RestoreChoice == RestoreSelection.Overwrite)
                {
                    Task.Run(async () =>
                    {
                        Progressbar.UpdateProgressBar.Progress = 10;
                        await RouteDatabase.ReplaceDBAsync(RestoreFile);
                        Progressbar.UpdateProgressBar.Progress = 90;
                    });
                }

                if (RestoreChoice == RestoreSelection.Merge)
                {
                    Progressbar.UpdateProgressBar.MessageBody = $"Connecting to databases...";
                    var ImportDB = new SQLiteConnection(RestoreFile, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex, true);
                    var TrackRoutesToImport = ImportDB.Table<GPXDataRouteTrack>();
                    if (TrackRoutesToImport == null || TrackRoutesToImport.Count() == 0)
                    {
                        Serilog.Log.Warning("No Tracks or Routes found to restore");
                        Progressbar.UpdateProgressBar.Dismiss();
                        return false;
                    }

                    Serilog.Log.Debug($"Activities to import: " + TrackRoutesToImport.Count().ToString());
                    double ProgressBarIncrement = ((double)100 / TrackRoutesToImport.Count());
                    Serilog.Log.Debug($"ProgressBarIncrement '{ProgressBarIncrement}'");

                    //All Existing routes and tracks
                    List<GPXDataRouteTrack> allTracksRoutes = RouteDatabase.GetRoutesAsync().Result;
                    allTracksRoutes.AddRange(RouteDatabase.GetTracksAsync().Result);

                    //Import each one
                    foreach (GPXDataRouteTrack newActivity in TrackRoutesToImport)
                    {
                        Progressbar.UpdateProgressBar.Progress += ProgressBarIncrement;
                        Progressbar.UpdateProgressBar.MessageBody = $"'{newActivity.Name}'";
                        Serilog.Log.Debug($"Restoring: '{newActivity.Name}'");

                        GPXDataRouteTrack? oldActivity = allTracksRoutes.Where(x =>
                            x.GPXType == newActivity.GPXType &&
                            x.Name == newActivity.Name &&
                            x.Distance == newActivity.Distance &&
                            x.Ascent == newActivity.Ascent &&
                            x.Descent == newActivity.Descent &&
                            x.Description == newActivity.Description &&
                            x.GPX == newActivity.GPX
                        ).FirstOrDefault();

                        if (oldActivity == null)
                        {
                            newActivity.Id = 0;
                            RouteDatabase.SaveRoute(newActivity);
                        }

                        //else its a duplicate, do not import                           
                    }

                    ImportDB.Close();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to restore Route & Track Database");
                return false;
            }

            Progressbar.UpdateProgressBar.Dismiss();
            Serilog.Log.Information("Done restoring Route & Track Data");
            return true;
        }

        private static bool RestoreMapTiles(RestoreSelection RestoreChoice)
        {
            try
            {
                Serilog.Log.Information("Restoring Map Tiles");
                string RestoreFolder = tmpFolder + "/" + Path.GetFileName(Fragment_Preferences.MapFolder);

                if (RestoreChoice == RestoreSelection.Overwrite)
                {
                    //Remove OSM Layer
                    Progressbar.UpdateProgressBar.Progress = 5;
                    var OSMLayer = Fragment_map.map.Layers.FindLayer("OSM").FirstOrDefault();
                    if (OSMLayer == null)
                    {
                        Serilog.Log.Error("OSM Layer not found?");
                        Progressbar.UpdateProgressBar.Dismiss();
                        return false;
                    }

                    Progressbar.UpdateProgressBar.Progress = 10;
                    Fragment_map.map.Layers.Remove(OSMLayer);
                }

                var filesToRestore = Directory.GetFiles(RestoreFolder);
                Serilog.Log.Debug($"MapTile files to restore from backup: " + filesToRestore.Count().ToString());
                double ProgressBarIncrement = ((double)90 / filesToRestore.Count());
                Serilog.Log.Debug($"ProgressBarIncrement '{ProgressBarIncrement}'");

                string TileBrowseSource = Preferences.Get(Platform.CurrentActivity?.GetString(Resource.String.OSM_Browse_Source), Fragment_Preferences.TileBrowseSource);

                foreach (string fileName in Directory.GetFiles(RestoreFolder))
                {
                    if (RestoreChoice == RestoreSelection.Merge && fileName.Contains(TileBrowseSource) == true)
                    {
                        //Do Nothing
                    }
                    else
                    {
                        File.Copy(fileName, Fragment_Preferences.MapFolder + "/" + Path.GetFileName(fileName), true);
                    }

                    Progressbar.UpdateProgressBar.Progress += ProgressBarIncrement;
                    Progressbar.UpdateProgressBar.MessageBody = $"'{Path.GetFileName(fileName)}'";
                }


                if (RestoreChoice == RestoreSelection.Overwrite)
                { 
                    DownloadRasterImageMap.LoadOSMLayer();
                    Progressbar.UpdateProgressBar.Progress = 100;
                }

                if (RestoreChoice == RestoreSelection.Merge)
                {
                    Progressbar.UpdateProgressBar.MessageBody = $"Connecting to database...";
                    var RestoreFile = RestoreFolder + "/" + TileBrowseSource + ".mbtiles";
                    var Restore_DB = new SQLiteConnection(RestoreFile, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex, true);
                    var tiles = Restore_DB.Table<tiles>();
                    Serilog.Log.Debug($"Tiles to import from backup: " + tiles.Count().ToString());
                    ProgressBarIncrement = ((double)100 / tiles.Count());
                    Serilog.Log.Debug($"ProgressBarIncrement '{ProgressBarIncrement}'");

                    lock (MbTileCache.sqlConn)
                    {
                        foreach (tiles newTile in tiles)
                        {
                            Progressbar.UpdateProgressBar.Progress += ProgressBarIncrement;
                            Progressbar.UpdateProgressBar.MessageBody = $"'{newTile.zoom_level} / {newTile.tile_row} / {newTile.tile_column}'";

                            //Do we already have the tile?
                            try
                            {
                                tiles oldTile = MbTileCache.sqlConn.Table<tiles>().Where(x => x.zoom_level == newTile.zoom_level && x.tile_column == newTile.tile_column && x.tile_row == newTile.tile_row).FirstOrDefault();
                                if ((oldTile != null) && (oldTile.createDate >= newTile.createDate))
                                {
                                    //Same or older tile in backup
                                    continue;
                                }

                                if (oldTile != null)
                                {
                                    //Update existing tile
                                    newTile.reference = oldTile.reference;
                                    newTile.id = oldTile.id;
                                }
                                else
                                {
                                    //Insert new tile
                                    newTile.reference = string.Empty;
                                    newTile.id = 0;
                                }

                                newTile.createDate = DateTime.UtcNow;
                                MBTilesWriter.WriteTile(newTile);
                            }
                            catch (Exception ex)
                            {
                                Serilog.Log.Error(ex, $"Failed to restore tiles");
                            }
                        }
                    }

                    Restore_DB.Close();
                    Restore_DB.Dispose();
                    Restore_DB = null;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to restore CacheDB Database");
                return false;
            }

            Progressbar.UpdateProgressBar.Dismiss();
            Serilog.Log.Information("Done restoring Map Tiles");
            return true;
        }

        private static bool RestoreElevationData(RestoreSelection RestoreChoice)
        {
            Serilog.Log.Information("Restore Elevation Data (GeoTiffFiles)");
            string Source_Folder = tmpFolder + "/" + Fragment_Preferences.GeoTiffFolder;
            string Destination_Folder = Fragment_Preferences.LiveData + "/" + Fragment_Preferences.GeoTiffFolder;

            try
            {
                if (RestoreChoice == RestoreSelection.Overwrite)
                {
                    Progressbar.UpdateProgressBar.Progress = 1;
                    Utils.Misc.EmptyFolder(Destination_Folder);
                    Progressbar.UpdateProgressBar.Progress = 4;
                }

                //Make sure it's there
                if (Directory.Exists(Destination_Folder) == false)
                {
                    Directory.CreateDirectory(Destination_Folder);
                }
                Progressbar.UpdateProgressBar.Progress = 5;

                //Existing Source files
                Serilog.Log.Debug("Source folder:");
                int fileCounter = 0;
                foreach (string fileName in Directory.GetFiles(Source_Folder))
                {
                    fileCounter++;
                    Serilog.Log.Debug(fileName);
                }

                //Existing Destination files
                Serilog.Log.Debug("Destination folder:");
                foreach (string fileName in Directory.GetFiles(Destination_Folder))
                    Serilog.Log.Debug(fileName);

                double ProgressBarIncrement = ((double)95 / (double)fileCounter);
                Serilog.Log.Debug($"ProgressBarIncrement '{ProgressBarIncrement}'");

                //Copy each file
                foreach (string fileName in Directory.GetFiles(Source_Folder))
                {
                    Progressbar.UpdateProgressBar.Progress += ProgressBarIncrement;
                    Progressbar.UpdateProgressBar.MessageBody = $"{Path.GetFileName(fileName)}";

                    File.Copy(fileName, Destination_Folder + "/" + Path.GetFileName(fileName), true);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to restore GeoTiff files");
                return false;
            }

            //Current contents
            Serilog.Log.Debug("Current files in GeoTiff Live Folder:");
            foreach (string fileName in Directory.GetFiles(Destination_Folder))
                Serilog.Log.Debug(fileName);

            Progressbar.UpdateProgressBar.Dismiss();
            Serilog.Log.Information("Done restoring GeoTiff Files");
            return true;
        }
    }
}