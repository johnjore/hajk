using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Core.View;
using AndroidX.DrawerLayout.Widget;
using AndroidX.Fragment.App;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Navigation;
using Google.Android.Material.Snackbar;
using hajk.Data;
using hajk.Fragments;
using hajk.Models;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using Sentry;
using Sentry.Android;
using Sentry.Serilog;
using Serilog;
using Serilog.Events;
using SharpGPX;
using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

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
            if (Application != null) Platform.Init(this, savedInstanceState);
            await Platform.WaitForActivityAsync();

            ServicePointManager.ServerCertificateValidationCallback = (message, certificate, chain, sslPolicyErrors) => true;

            //Preferences.Clear();
            //new FileInfo(rootPath + "/" + PrefsActivity.CacheDB).Delete();

            //Clear out all backups
            //Utils.Misc.EmptyFolder(Fragment_Preferences.Backups);

            //Export backup files to download folder
            /*string? Source_Folder = Fragment_Preferences.Backups;
            string? Destination_Folder = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
            foreach (string fileName in Directory.GetFiles(Source_Folder))
            {
                File.Copy(fileName, Destination_Folder + "/" + Path.GetFileName(fileName));
            }*/

            //Delete all map files
            /*foreach (string fileName in Directory.GetFiles(Fragment_Preferences.MapFolder))
            {
                File.Delete(fileName);
            }            
            */
            //Preferences.Set("freq", Fragment_Preferences.freq_s.ToString());
            //Preferences.Set("OffTrackDistanceWarning_m", Fragment_Preferences.OffTrackDistanceWarning_m.ToString());
            //Preferences.Set("OffTrackRouteSnooze_m", Fragment_Preferences.OffRouteSnooze_m.ToString());
            //Preferences.Set("freq_s_OffRoute", Fragment_Preferences.freq_OffRoute_s.ToString());
            //Preferences.Set("KeepNBackups", Fragment_Preferences.KeepNBackups.ToString());

#if DEBUG
            if (string.IsNullOrEmpty(Resources?.GetString(Resource.String.Sentry_APIKey)))
            {
               Serilog.Log.Information("Sentry DSN entry is missing and in debug mode");
            }
#endif

            //Logging
            string _Path = System.IO.Path.Combine(Fragment_Preferences.rootPath, Fragment_Preferences.logFile);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.AndroidLog(
                    outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj} ({SourceContext}) {Exception}"
                )
                .WriteTo.File(
                    _Path, 
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 2, 
                    fileSizeLimitBytes: 2*1024*1024,
                    outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj} ({SourceContext}) {Exception}{NewLine}"
                )
                .WriteTo.Sentry(options =>
                {
                    options.Dsn = Resources?.GetString(Resource.String.Sentry_APIKey);
                    options.Debug = true;
                    options.TracesSampleRate = 1.0;
                    options.MinimumBreadcrumbLevel = LogEventLevel.Debug;
                    options.MinimumEventLevel = LogEventLevel.Warning;
                })
            .CreateLogger();

            Log.Information($"Logging to '{_Path}'");
            
            //Enable Mapsui logging
            MapsuiLogging.AttachMapsuiLogging();

            //Make sure LiveData folder exists
            if (!Directory.Exists(Fragment_Preferences.LiveData))
            {
                try
                {
                    Directory.CreateDirectory(Fragment_Preferences.LiveData);
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, $"MainActivity - Failed to Create LiveData Folder");
                }            
            }

            try
            {
                //Extract initial map, if not there
                Utils.Misc.ExtractInitialMap(this, Fragment_Preferences.LiveData + "/" + "OpenStreetMap.mbtiles");

                //GUI
                SetContentView(Resource.Layout.activity_main);
                AndroidX.AppCompat.Widget.Toolbar? toolbar = FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.toolbar);
                if (toolbar != null)
                {
                    SetSupportActionBar(toolbar);
                }

                FloatingActionButton? fabCompass = FindViewById<FloatingActionButton>(Resource.Id.fabCompass);
                if (fabCompass != null)
                {
                    fabCompass.Click += FabOnClick;                    
                }

                FloatingActionButton? fabCamera = FindViewById<FloatingActionButton>(Resource.Id.fabCamera);
                if (fabCamera != null)
                {
                    fabCamera.Click += FabCamera_Click;
                }

                DrawerLayout? drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
                ActionBarDrawerToggle toggle = new(this, drawer, toolbar, Resource.String.navigation_drawer_open, Resource.String.navigation_drawer_close);
                drawer?.AddDrawerListener(toggle);
                toggle.SyncState();

                NavigationView? navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
                navigationView?.SetNavigationItemSelectedListener(this);

                /**/
                Thread.Sleep(1000);
                SupportFragmentManager.BeginTransaction()
                    .Add(Resource.Id.fragment_container, new Fragment_map(), Fragment_Preferences.Fragment_Map)
                    .Commit();
                SupportFragmentManager.ExecutePendingTransactions();

                //Sanity
                Log.Debug($"Set RecordingTrack to false - sanity check");
                Preferences.Set("RecordingTrack", false);
                Preferences.Set("TrackLocation", false);

                //App Permissions                
                Log.Debug($"Requesting Application Permissions");
                await Utilities.AppPermissions.CheckAndRequestPermissionAsync<Permissions.LocationWhenInUse>();
                await Utilities.AppPermissions.CheckAndRequestPermissionAsync<Permissions.LocationAlways>();
                if (OperatingSystem.IsAndroidVersionAtLeast(33))
                {
                    await Utilities.AppPermissions.CheckAndRequestPermissionAsync<Permissions.PostNotifications>();
                }
                
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
                Utilities.BatteryOptimization.SetDozeOptimization(this);

                Log.Information($"Daily Backup?");
                if (Preferences.Get("EnableBackupAtStartup", Fragment_Preferences.EnableBackupAtStartup))
                {
                    MainThread.BeginInvokeOnMainThread(() => Backup.RunDailyBackup());
                }

                //Enumerate all files
                Serilog.Log.Debug($"All files in rootPath, '{Fragment_Preferences.rootPath}'");
                var allFiles = Directory.GetFiles(Fragment_Preferences.rootPath, "*", SearchOption.AllDirectories);
                foreach (var file in allFiles)
                {
                    Serilog.Log.Debug(file);
                }

                //Wakelock - Wake-up phone when recording and in battery save mode, and screenlocked
                if (Preferences.Get("EnableWakeLock", Fragment_Preferences.EnableWakeLock))
                {
                    PowerManager? powerManager = (PowerManager)Android.App.Application.Context.GetSystemService(Context.PowerService) as PowerManager;
                    Utilities.Alarms.wakelock = powerManager?.NewWakeLock(WakeLockFlags.Full | WakeLockFlags.AcquireCausesWakeup | WakeLockFlags.OnAfterRelease, "walkabout:Alarms");
                    Utilities.Alarms.wakelock?.SetReferenceCounted(false);
                }

                //Restore recording checkpoint file?
                RecordTrack.RestoreCheckPoint();

                //Clean share folder
                Share.CleanSharefolder();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, $"MainActivity - OnCreate");
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            SafBackupService.HandleFolderSelection(this, requestCode, resultCode, data);
        }

        public override void OnBackPressed()
        {
            DrawerLayout? drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            if (drawer != null)
            {
                if (drawer.IsDrawerOpen(GravityCompat.Start))
                {
                    drawer.CloseDrawer(GravityCompat.Start);
                }
                else
                {
                    var c = SupportFragmentManager.FindFragmentByTag(Fragment_Preferences.Fragment_Settings);
                    if (c != null && SupportFragmentManager.Fragments.Contains(c))
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
            if (id == Resource.Id.AddCurrentLocationAsPOI)
            {
                var CurrentLocation = LocationForegroundService.GetLocation();
                if (CurrentLocation != null)
                {
                    GPXDataPOI p = new()
                    {
                        Name = "Manual Entry",
                        Description = "",
                        Symbol = null,
                        Lat = (decimal)CurrentLocation.Latitude,
                        Lon = (decimal)CurrentLocation.Longitude,
                    };

                    if (POIDatabase.SavePOI(p) > 0)
                    {
                        DisplayMapItems.AddPOIToMap();
                    }
                    else
                    {
                        Serilog.Log.Error("Failed to add POI to database");
                    }                    
                }
            }
            if (id == Resource.Id.SaveGPSasPOI)
            {
                AndroidX.Fragment.App.FragmentTransaction? fragmentTransaction = SupportFragmentManager.BeginTransaction();
                AndroidX.Fragment.App.Fragment? fragmentPrev = SupportFragmentManager.FindFragmentByTag("dialog");
                if (fragmentPrev != null)
                {
                    fragmentTransaction?.Remove(fragmentPrev);
                }

                fragmentTransaction?.AddToBackStack(null);

                Fragment_gps_poi dialogFragment = Fragment_gps_poi.NewInstace(null);
                if (fragmentTransaction != null)
                {
                    dialogFragment.Show(fragmentTransaction, "dialog");
                }
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
            else if (id == Resource.Id.action_clearmap)
            {
                return Utils.Misc.ClearTrackRoutesFromMap();
            }
            else if (id == Resource.Id.action_settings)
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

            //Create alarm
            Utilities.Alarms.CreateAlarm();

            base.OnPause();
        }

        protected override void OnResume()
        {
            Log.Information($"OnResume()");

            //Cancel any alarms scheduled
            Utilities.Alarms.CancelAlarm();

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
                Log.Fatal(ex, $"OnDestroy()");
            }

            base.OnDestroy();
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private void FabOnClick(object? sender, EventArgs? eventArgs)
        {
            //Toggle tracking
            bool currentStatus = Preferences.Get("TrackLocation", true);
            Preferences.Set("TrackLocation", !currentStatus);

            //Floating Button
            FloatingActionButton? fabCompass = FindViewById<FloatingActionButton>(Resource.Id.fabCompass);

            if (fabCompass == null)
            {
                return;
            }

            //Inverse as pref has been updated
            if (Preferences.Get("TrackLocation", true) == true)
            {
                fabCompass.Background?.SetTintList(ColorStateList.ValueOf(Android.Graphics.Color.Red));
                Fragment_map.mapControl.Map.Navigator.PanLock = true;
                Fragment_map.mapControl.Map.Navigator.RotationLock = true;
            }
            else
            {
                fabCompass.Background?.SetTintList(ColorStateList.ValueOf(Android.Graphics.Color.ParseColor("#ff33b5e5")));
                Fragment_map.mapControl.Map.Navigator.PanLock = false;
                Fragment_map.mapControl.Map.Navigator.RotationLock = false;
            }

            /*View view = (View)sender;
            Snackbar.Make(view, "Replace with your own action", Snackbar.LengthLong)
                .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();*/
        }

        private void FabCamera_Click(object? sender, EventArgs e)
        {
            Intent cameraIntent = new("android.media.action.STILL_IMAGE_CAMERA");
            StartActivity(cameraIntent);
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
                    Platform.CurrentActivity?
                        .FindViewById<NavigationView>(Resource.Id.nav_view)?
                        .Menu?
                        .FindItem(Resource.Id.nav_PauseResumeRecordTrack)?
                        .SetTitle(Resource.String.PauseRecord_Track)
                        .SetEnabled(false);

                    Platform.CurrentActivity?
                        .FindViewById<NavigationView>(Resource.Id.nav_view)?
                        .Invalidate();
                }
                else
                {
                    RecordTrack.StartTrackTimer();
                }
            }
            else if (id == Resource.Id.nav_PauseResumeRecordTrack)
            {
                var item_nav = Microsoft.Maui.ApplicationModel.
                    Platform.CurrentActivity?
                    .FindViewById<NavigationView>(Resource.Id.nav_view)?
                    .Menu.FindItem(Resource.Id.nav_PauseResumeRecordTrack);

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
                /**///Fragment_gpx.GPXDisplay is not saved correctly for handling saving the scrollbar locations
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
                        IMenuItem? mi = nav?.Menu?.FindItem(Resource.Id.nav_tracks);
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
                        IMenuItem? mi = nav?.Menu?.FindItem(Resource.Id.nav_routes);
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
                alert.SetNegativeButton("Upload Log files", (sender, args) => {
                    SentrySdk.CaptureMessage("Log files", scope =>
                    {
                        //Copy log files to Backup folder
                        var logFolder = Path.Combine(Fragment_Preferences.Backups, DateTime.Now.ToString("yyyy-MM-dd HHmm"));
                        if (Directory.Exists(logFolder) == false)
                            Directory.CreateDirectory(logFolder);

                        Serilog.Log.Debug($"All log files in rootPath, '{Fragment_Preferences.rootPath}'");
                        var allFiles = Directory.GetFiles(Fragment_Preferences.rootPath, "walkabout*", SearchOption.AllDirectories);
                        foreach (var file in allFiles)
                        {
                            long length = new System.IO.FileInfo(file).Length;
                            Serilog.Log.Debug($"Name: {file}, Size: {length} bytes");
                            if (length >= 20 * 1024 * 1024)
                                Serilog.Log.Error("Attachments too large for Sentry!");
                            
                            scope.AddAttachment(file);
                            File.Copy(file, Path.Combine(logFolder, Path.GetFileName(file)), true);
                        }

                        //Include checkpoint file, if it exists
                        if (File.Exists(Fragment_Preferences.CheckpointGPX))
                        {
                            scope.AddAttachment(Fragment_Preferences.CheckpointGPX);
                            File.Copy(Fragment_Preferences.CheckpointGPX, Path.Combine(logFolder, Path.GetFileName(Fragment_Preferences.CheckpointGPX)), true);
                        }
                    });
                });
                var dialog = alert.Create();
                dialog.Show();
            }
            else if (id == Resource.Id.backup)
            {
                Backup.ShowBackupDialog();
            }
            else if (id == Resource.Id.restore)
            {
                Restore.ShowRestoreDialogAsync();
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
            drawer?.Invalidate();
            return true;
        }
    }
}
