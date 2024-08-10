using System;
using System.Threading;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Content;
using Android.Content.Res;
using AndroidX.AppCompat.App;
using AndroidX.Fragment.App;
using AndroidX.Core.View;
using AndroidX.DrawerLayout.Widget;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Navigation;
using Google.Android.Material.Snackbar;
using Serilog;
using hajk.Data;
using hajk.Fragments;
using hajk.Models;
using SQLite;
using SharpGPX;
using Android.Content.PM;
using System.Diagnostics;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace hajk
{
    [Activity(Name = "no.johnjore.hajk.MainActivity", Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener
    {
        public static RouteDatabase? routedatabase;
        public static GpxClass? ActiveRoute = null;                  // Active Route / Track for calculations, inc Off-Route detection

        protected override async void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            //Init
            Platform.Init(Application);
            await Platform.WaitForActivityAsync();

            ServicePointManager.ServerCertificateValidationCallback = (message, certificate, chain, sslPolicyErrors) => true;

            //Preferences.Clear();
            //new FileInfo(rootPath + "/" + PrefsActivity.CacheDB).Delete();

            //Logging
            string _Path = System.IO.Path.Combine(Fragment_Preferences.rootPath, Preferences.Get("logFile", Fragment_Preferences.logFile));
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.AndroidLog(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj} ({SourceContext}) {Exception}")
                .WriteTo.File(_Path, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 2, outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj} ({SourceContext}) {Exception}{NewLine}"
                ).CreateLogger();
            Log.Information($"Logging to '{_Path}'");

            //Enable Mapsui logging
            MapsuiLogging.AttachMapsuiLogging();

            try
            {
                //Extract initial map, if not there
                Utils.Misc.ExtractInitialMap(this, Fragment_Preferences.rootPath + "/" + Fragment_Preferences.CacheDB);

                //GUI
                SetContentView(Resource.Layout.activity_main);
                AndroidX.AppCompat.Widget.Toolbar? toolbar = FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.toolbar);
                if (toolbar != null)
                {
                    SetSupportActionBar(toolbar);                    
                }

                FloatingActionButton? fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
                if (fab != null)
                {
                    fab.Click += FabOnClick;                    
                }

                DrawerLayout? drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
                ActionBarDrawerToggle toggle = new(this, drawer, toolbar, Resource.String.navigation_drawer_open, Resource.String.navigation_drawer_close);
                drawer?.AddDrawerListener(toggle);
                toggle.SyncState();

                NavigationView? navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
                navigationView?.SetNavigationItemSelectedListener(this);

                SupportFragmentManager.BeginTransaction()
                    .Add(Resource.Id.fragment_container, new Fragment_map(), Fragment_Preferences.Fragment_Map)
                    .Commit();
                SupportFragmentManager.ExecutePendingTransactions();

                //Sanity
                Log.Debug($"Set RecordingTrack to false - sanity check");
                Preferences.Set("RecordingTrack", false);
                Preferences.Set("TrackLocation", false);

                //App Permissions
                await Utilities.AppPermissions.RequestAppPermissions(this);              

                Log.Debug($"Create Location Service");
                if (await Permissions.CheckStatusAsync<Permissions.LocationAlways>() == PermissionStatus.Granted || await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() == PermissionStatus.Granted)
                {
                    Intent locationServiceIntent = new(this, typeof(LocationForegroundService));
                    locationServiceIntent.SetAction(Fragment_Preferences.ACTION_START_SERVICE);
                    if (OperatingSystem.IsAndroidVersionAtLeast(26))
                    {
                        StartForegroundService(locationServiceIntent);
                    }
                    else
                    {
                        StartService(locationServiceIntent);
                    }
                }

                Log.Debug($"Notify user if battery save mode is enabled?");
                Utilities.BatteryOptimization.BatterySaveModeNotification();

                Log.Debug($"Notify user if location permission does not allow background collection");
                await Utilities.AppPermissions.LocationPermissionNotification(this);

                //Enable Compass Sensor
                CompassData.EnableCompass();

                //Disable battery optimization
                /**///Utilities.BatteryOptimization.SetDozeOptimization(this);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, $"MainActivity - OnCreate");
            }
        }

        public override void OnBackPressed()
        {
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            if (drawer != null)
            {
                if (drawer.IsDrawerOpen(GravityCompat.Start))
                {
                    drawer.CloseDrawer(GravityCompat.Start);
                }
                else
                {
                    var c = SupportFragmentManager.FindFragmentByTag(Fragment_Preferences.Fragment_Settings);
                    if (SupportFragmentManager.Fragments.Contains(c))
                    {
                        /*var FragmentsTransaction1 = SupportFragmentManager.BeginTransaction()
                            .Remove(SupportFragmentManager.FindFragmentByTag(Fragment_Preferences.Fragment_Settings))
                            .Commit();
                        SupportFragmentManager.ExecutePendingTransactions();
                        */
                        SupportFragmentManager.PopBackStack();
                    }
                    else
                    {
                        //base.OnBackPressed();
                        Utils.Misc.PromptToConfirmExit();
                    }
                }
            }
        }

        public override bool OnCreateOptionsMenu(IMenu? menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);

            //Disable menu item until a GPX file has been overlayed map
            var item = menu?.FindItem(Resource.Id.action_clearmap);
            item?.SetEnabled(false);

            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                Log.Information($"Change to Settings");

                SupportFragmentManager.BeginTransaction()
                    .SetReorderingAllowed(true)
                    .Replace(Resource.Id.fragment_container, new Fragment_Preferences(), Fragment_Preferences.Fragment_Settings)
                    .AddToBackStack(Fragment_Preferences.Fragment_Settings)
                    .Commit();
                SupportFragmentManager.ExecutePendingTransactions();

                return true;
            }
            else if (id == Resource.Id.action_clearmap)
            {
                return Utils.Misc.ClearTrackRoutesFromMap();
            }
            else if (id == Resource.Id.AddRogainingPOI)
            {
                AndroidX.Fragment.App.FragmentTransaction? fragmentTransaction = SupportFragmentManager.BeginTransaction();
                AndroidX.Fragment.App.Fragment? fragmentPrev = SupportFragmentManager.FindFragmentByTag("dialog");
                if (fragmentPrev != null)
                {
                    fragmentTransaction?.Remove(fragmentPrev);
                }

                fragmentTransaction?.AddToBackStack(null);

                Fragment_markers dialogFragment = Fragment_markers.NewInstace(null);
                if (fragmentTransaction != null)
                {
                    dialogFragment.Show(fragmentTransaction, "dialog");
                }
            }
            else if (id == Resource.Id.ExportRogainingPOI)
            {
                Export.ExportPOI("Rogaining");
            }

            return base.OnOptionsItemSelected(item);
        }

        protected override void OnStart()
        {
            Log.Information($"OnStart()");

            //Check if passed GPX file as intent
            Import.GPXImportfromIntent(Intent);

            base.OnStart();
        }

        protected override void OnStop()
        {
            Log.Information($"OnStop()");
            base.OnStop();
        }

        protected override void OnPause()
        {
            Log.Information($"OnPause()");
            base.OnPause();
        }

        protected override void OnResume()
        {
            Log.Information($"OnResume()");
            base.OnResume();
        }

        protected override void OnDestroy()
        {
            try
            {
                Log.Information($"OnDestroy()");

                //Location Service
                Intent locationServiceIntent = new(this, typeof(LocationForegroundService));
                locationServiceIntent.SetAction(Fragment_Preferences.ACTION_STOP_SERVICE);
                StopService(locationServiceIntent);

                //Cleanup Log file
                Log.CloseAndFlush();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, $"OnDestroy()");
            }

            base.OnDestroy();
        }

        private void FabOnClick(object sender, EventArgs eventArgs)
        {
            //Toggle tracking
            bool currentStatus = Preferences.Get("TrackLocation", true);
            Preferences.Set("TrackLocation", !currentStatus);

            //Floating Button
            FloatingActionButton? fab = FindViewById<FloatingActionButton>(Resource.Id.fab);

            if (fab == null)
            {
                return;
            }

            //Inverse as pref has been updated
            if (Preferences.Get("TrackLocation", true) == true)
            {
                fab.Background?.SetTintList(ColorStateList.ValueOf(Android.Graphics.Color.Red));
                Fragment_map.mapControl.Map.Navigator.PanLock = true;
                Fragment_map.mapControl.Map.Navigator.RotationLock = true;
            }
            else
            {
                fab.Background?.SetTintList(ColorStateList.ValueOf(Android.Graphics.Color.LightBlue));
                Fragment_map.mapControl.Map.Navigator.PanLock = false;
                Fragment_map.mapControl.Map.Navigator.RotationLock = false;
            }

            /*View view = (View)sender;
            Snackbar.Make(view, "Replace with your own action", Snackbar.LengthLong)
                .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();*/
        }

        public bool OnNavigationItemSelected(IMenuItem item)
        {
            int id = item.ItemId;

            if (id == Resource.Id.nav_import)
            {
                //Import GPX file (routes, tracks and POI), save to SQLite DB
                Import.ImportGPX();
            }
            else if (id == Resource.Id.nav_offlinemap)
            {
                //Import .mbtiles file
                MBTilesWriter.ImportMapTiles();
            }
            else if (id == Resource.Id.nav_recordtrack)
            {
                if (Preferences.Get("RecordingTrack", Fragment_Preferences.RecordingTrack))
                {
                    RecordTrack.EndTrackTimer();
                    item.SetTitle(Resource.String.Record_Track);

                    //Disable the menu item for pause / resume
                    Platform.CurrentActivity.FindViewById<NavigationView>(Resource.Id.nav_view)
                        .Menu?.FindItem(Resource.Id.nav_PauseResumeRecordTrack)
                        .SetTitle(Resource.String.PauseRecord_Track)
                        .SetEnabled(false);

                    Platform.CurrentActivity.FindViewById<NavigationView>(Resource.Id.nav_view).Invalidate();
                }
                else
                {
                    RecordTrack.StartTrackTimer();
                }
            }
            else if (id == Resource.Id.nav_PauseResumeRecordTrack)
            {
                var item_nav = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.FindViewById<NavigationView>(Resource.Id.nav_view)?.Menu.FindItem(Resource.Id.nav_PauseResumeRecordTrack);

                if (item_nav?.TitleFormatted?.ToString() == Resources?.GetString(Resource.String.PauseRecord_Track))
                {
                    Preferences.Set("RecordingTrack", false);
                    item_nav?.SetTitle(Resource.String.ResumeRecord_Track);
                }
                else
                {
                    Preferences.Set("RecordingTrack", true);
                    item_nav?.SetTitle(Resource.String.PauseRecord_Track);
                }
            }
            else if (id == Resource.Id.nav_routes)
            {
                if (Fragment_map.mapControl?.Visibility == ViewStates.Invisible)
                {
                    NavigationView? nav = this?.FindViewById<NavigationView>(Resource.Id.nav_view);
                    item = nav?.Menu.FindItem(Resource.Id.nav_routes);

                    if (item?.TitleFormatted?.ToString() == Resources?.GetString(Resource.String.Map))
                    {
                        ProcessFragmentChanges.SwitchFragment(Fragment_Preferences.Fragment_Map, item);

                        SupportFragmentManager.BeginTransaction()
                            .Remove(SupportFragmentManager.FindFragmentByTag(Fragment_Preferences.Fragment_GPX))
                            .Commit();
                        SupportFragmentManager.ExecutePendingTransactions();
                    }
                    else
                    {
                        IMenuItem mi = nav?.Menu?.FindItem(Resource.Id.nav_tracks);
                        mi?.SetTitle(Resource.String.Track);
                        mi?.SetIcon(Resource.Drawable.track);

                        Fragment_gpx.GPXDisplay = Models.GPXType.Route;

                        SupportFragmentManager.BeginTransaction()
                            .Remove(SupportFragmentManager.FindFragmentByTag(Fragment_Preferences.Fragment_GPX))
                            .Add(Resource.Id.fragment_container, new Fragment_gpx(), Fragment_Preferences.Fragment_GPX)
                            .Commit();
                        SupportFragmentManager.ExecutePendingTransactions();

                        ProcessFragmentChanges.SwitchFragment(Fragment_Preferences.Fragment_GPX, item);
                    }
                }
                else
                {
                    Fragment_gpx.GPXDisplay = Models.GPXType.Route;

                    SupportFragmentManager.BeginTransaction()
                        .Add(Resource.Id.fragment_container, new Fragment_gpx(), Fragment_Preferences.Fragment_GPX)
                        .Commit();
                    SupportFragmentManager.ExecutePendingTransactions();

                    ProcessFragmentChanges.SwitchFragment(Fragment_Preferences.Fragment_GPX, item);
                }
            }
            else if (id == Resource.Id.nav_tracks)
            {
                if (Fragment_map.mapControl?.Visibility == ViewStates.Invisible)
                {
                    NavigationView nav = this.FindViewById<NavigationView>(Resource.Id.nav_view);
                    item = nav?.Menu?.FindItem(Resource.Id.nav_tracks);

                    if (item?.TitleFormatted?.ToString() == Resources?.GetString(Resource.String.Map))
                    {
                        ProcessFragmentChanges.SwitchFragment(Fragment_Preferences.Fragment_Map, item);

                        SupportFragmentManager.BeginTransaction()
                            .Remove(SupportFragmentManager.FindFragmentByTag(Fragment_Preferences.Fragment_GPX))
                            .Commit();
                        SupportFragmentManager.ExecutePendingTransactions();
                    }
                    else
                    {
                        IMenuItem mi = nav?.Menu?.FindItem(Resource.Id.nav_routes);
                        mi?.SetTitle(Resource.String.Routes);
                        mi?.SetIcon(Resource.Drawable.route);

                        Fragment_gpx.GPXDisplay = Models.GPXType.Track;

                        SupportFragmentManager.BeginTransaction()
                            .Remove(SupportFragmentManager.FindFragmentByTag(Fragment_Preferences.Fragment_GPX))
                            .Add(Resource.Id.fragment_container, new Fragment_gpx(), Fragment_Preferences.Fragment_GPX)
                            .Commit();
                        SupportFragmentManager.ExecutePendingTransactions();

                        ProcessFragmentChanges.SwitchFragment(Fragment_Preferences.Fragment_GPX, item);
                    }
                }
                else
                {
                    Fragment_gpx.GPXDisplay = Models.GPXType.Track;

                    SupportFragmentManager.BeginTransaction()
                        .Add(Resource.Id.fragment_container, new Fragment_gpx(), Fragment_Preferences.Fragment_GPX)
                        .Commit();
                    SupportFragmentManager.ExecutePendingTransactions();

                    ProcessFragmentChanges.SwitchFragment(Fragment_Preferences.Fragment_GPX, item);
                }
            }
            else if (id == Resource.Id.about)
            {
                using var alert = new AndroidX.AppCompat.App.AlertDialog.Builder(Microsoft.Maui.ApplicationModel.Platform.CurrentActivity);
                alert.SetTitle(Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Resources.GetString(Resource.String.About));
                alert.SetMessage(Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Resources.GetString(Resource.String.Build) + ": " + Microsoft.Maui.ApplicationModel.AppInfo.Version.ToString());
                alert.SetNeutralButton(Resource.String.Ok, (sender, args) => { });
                var dialog = alert.Create();
                dialog.Show();
            }
            else if (id == Resource.Id.backup)
            {
                //Backup tiles DB to Download folder. Change to pick folder?
                string? DownLoadFolder = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;

                //MBTiles
                SQLiteConnection DBBackupConnection = TileCache.MbTileCache.sqlConn;
                string backupFileName = DownLoadFolder + "/Backup-" + Resources?.GetString(Resource.String.app_name) + "-" + (DateTime.Now).ToString("yyMMdd-HHmmss") + ".mbtiles";
                DBBackupConnection.Backup(backupFileName);

                //Route DB
                string dbPath = Path.Combine(Fragment_Preferences.rootPath, Preferences.Get("RouteDB", Fragment_Preferences.RouteDB));
                DBBackupConnection = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex, true);
                backupFileName = DownLoadFolder + "/Backup-" + Resources?.GetString(Resource.String.app_name) + "-" + (DateTime.Now).ToString("yyMMdd-HHmmss") + ".db3";
                DBBackupConnection.Backup(backupFileName);
                DBBackupConnection.Close();

                //POI DB
                dbPath = Path.Combine(Fragment_Preferences.rootPath, Preferences.Get("POIDB", Fragment_Preferences.POIDB));
                DBBackupConnection = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex, true);
                backupFileName = DownLoadFolder + "/Backup-" + Resources?.GetString(Resource.String.app_name) + "-" + (DateTime.Now).ToString("yyMMdd-HHmmss") + ".poi.db3";
                DBBackupConnection.Backup(backupFileName);
                DBBackupConnection.Close();

                //Message
                using var alert = new AndroidX.AppCompat.App.AlertDialog.Builder(Microsoft.Maui.ApplicationModel.Platform.CurrentActivity);
                alert.SetTitle(Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Resources?.GetString(Resource.String.Backup));
                alert.SetMessage(Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Resources?.GetString(Resource.String.Done));
                alert.SetNeutralButton(Resource.String.Ok, (sender, args) => { });
                var dialog = alert.Create();
                dialog.Show();
            }
            else if (id == Resource.Id.activity_restore)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        var options = new PickOptions
                        {
                            PickerTitle = "Please select an activities file",
                            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                            {
                                /**///What is mime type for db3 files?!?
                                //{ DevicePlatform.Android, new string[] { "mbtiles"} },
                                { DevicePlatform.Android, null },
                            })
                        };

                        var sourceFile = await FilePicker.PickAsync(options);
                        if (sourceFile != null)
                        {
                            var ImportDB = new SQLiteConnection(sourceFile.FullPath, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex, true);
                            var TrackRoutesToImport = ImportDB.Table<GPXDataRouteTrack>();
                            Log.Debug($"Activities to import: " + TrackRoutesToImport.Count().ToString());

                            if (TrackRoutesToImport == null || TrackRoutesToImport.Count() == 0)
                            {
                                //Nothing to Import
                                return;
                            }

                            //All Existing routes and tracks
                            List<GPXDataRouteTrack> allTracksRoutes = RouteDatabase.GetRoutesAsync().Result;
                            allTracksRoutes.AddRange(RouteDatabase.GetTracksAsync().Result);

                            foreach (GPXDataRouteTrack newActivity in TrackRoutesToImport)
                            {
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
                            ImportDB.Dispose();
                            ImportDB = null;

                            Show_Dialog msg = new(Platform.CurrentActivity);
                            await msg.ShowDialog(Platform.CurrentActivity.GetString(Resource.String.Done), Platform.CurrentActivity.GetString(Resource.String.ActivitiesImported), Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Failed to import map file: '{ex}'");
                    }
                });
            }
            else if (id == Resource.Id.db_maintenance)
            {
                Task.Run(() =>
                {
                    SQLiteConnection DBMaintenanceConnection = TileCache.MbTileCache.sqlConn;

                    lock (DBMaintenanceConnection)
                    {
                        //MBTilesWriter.PurgeOldTiles();
                        //MBTilesWriter.RefreshOldTiles();

                        int totalRecordsBefore = DBMaintenanceConnection.Table<tiles>().Count();
                        Log.Debug($"Total Records: {totalRecordsBefore}");

                        //Delete entries without a blob
                        DBMaintenanceConnection.Table<tiles>().Where(x => x.tile_data == null).Delete();
                        int totalRecordsEmptyBlob = DBMaintenanceConnection.Table<tiles>().Count();
                        Log.Debug($"Total Records after empty blob: {totalRecordsEmptyBlob}");

                        //Remove duplicates
                        foreach (tiles dbTile in DBMaintenanceConnection.Table<tiles>().Reverse())
                        {
                            Log.Debug($"{dbTile.id}");
                            DBMaintenanceConnection.Table<tiles>().Where(x => x.zoom_level == dbTile.zoom_level && x.tile_column == dbTile.tile_column && x.tile_row == dbTile.tile_row && x.id != dbTile.id).Delete();
                        }
                        int totalRecordsDuplicate = DBMaintenanceConnection.Table<tiles>().Count();

                        Log.Debug($"Total Records: {totalRecordsBefore}");
                        Log.Debug($"Total Records after empty blob: {totalRecordsEmptyBlob}");
                        Log.Debug($"Total Records after duplicate: {totalRecordsDuplicate}");
                    }
                });
            }
            
            DrawerLayout? drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            drawer?.CloseDrawer(GravityCompat.Start);
            drawer.Invalidate();
            return true;
        }
    }
}
