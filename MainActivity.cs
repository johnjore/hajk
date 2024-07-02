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
using Xamarin.Essentials;
using static Xamarin.Essentials.Permissions;
using hajk.Data;
using hajk.Fragments;
using hajk.Models;
using SQLite;
using SharpGPX;

namespace hajk
{
    [Activity(Name = "no.johnjore.hajk.MainActivity", Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener
    {
        public static RouteDatabase? routedatabase;
        public static GpxClass? ActiveRoute = null;                  // Active Route / Track for calculations, inc Off-Route detection

        protected override async void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            //Init
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);

            ServicePointManager.ServerCertificateValidationCallback = (message, certificate, chain, sslPolicyErrors) => true;

            //Preferences.Clear();
            //new FileInfo(rootPath + "/" + PrefsActivity.CacheDB).Delete();            

            //Logging
            string _Path = System.IO.Path.Combine(PrefsActivity.rootPath, Preferences.Get("logFile", PrefsActivity.logFile));
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
                Utils.Misc.ExtractInitialMap(this, PrefsActivity.rootPath + "/" + PrefsActivity.CacheDB);

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
                if (drawer != null && toggle != null)
                {
                    drawer.AddDrawerListener(toggle);
                    toggle.SyncState();
                }

                NavigationView? navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
                navigationView?.SetNavigationItemSelectedListener(this);

                //Sanity
                Log.Debug($"Set RecordingTrack to false - sanity check");
                Preferences.Set("RecordingTrack", false);
                Preferences.Set("TrackLocation", false);

                //App Permissions
                await Utilities.AppPermissions.RequestAppPermissions(this);

                Log.Debug($"Create Fragment_Map");
                var FragmentsTransaction = SupportFragmentManager.BeginTransaction();
                FragmentsTransaction.Add(Resource.Id.fragment_container, new Fragment_map(), "Fragment_map");
                FragmentsTransaction.Commit();

                Log.Debug($"Create Location Service");
                if (await CheckStatusAsync<LocationAlways>() == PermissionStatus.Granted || await CheckStatusAsync<LocationWhenInUse>() == PermissionStatus.Granted)
                {
                    Intent locationServiceIntent = new(Platform.CurrentActivity, typeof(LocationForegroundService));
                    locationServiceIntent.SetAction(PrefsActivity.ACTION_START_SERVICE);
                    if (OperatingSystem.IsAndroidVersionAtLeast(26))
                    {
                        Platform.CurrentActivity.StartForegroundService(locationServiceIntent);
                    }
                    else
                    {
                        Platform.CurrentActivity.StartService(locationServiceIntent);
                    }
                }

                Log.Debug($"Notify user if battery save mode is enabled?");
                Utilities.BatteryOptimization.BatterySaveModeNotification();

                Log.Debug($"Notify user if location permission does not allow background collection");
                await Utilities.AppPermissions.LocationPermissionNotification(this);

                //Disable battery optimization
                Utilities.BatteryOptimization.SetDozeOptimization(this);
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
                    //base.OnBackPressed();
                    Utils.Misc.PromptToConfirmExit();
                }
            }
            else
            {
                //base.OnBackPressed();
                Utils.Misc.PromptToConfirmExit();
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
                StartActivity(new Intent(this, typeof(PrefsActivity)));
                return true;
            }
            else if (id == Resource.Id.action_clearmap)
            {
                return Utils.Misc.ClearTrackRoutesFromMap();
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
                locationServiceIntent.SetAction(PrefsActivity.ACTION_STOP_SERVICE);
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
                //Import GPX file (routes and tracks), save to SQLite DB
                Import.GetRoute();
            }
            else if (id == Resource.Id.nav_offlinemap)
            {
                //Import .mbtiles file
                MBTilesWriter.ImportMapTiles();
            }
            else if (id == Resource.Id.nav_recordtrack)
            {
                if (Preferences.Get("RecordingTrack", PrefsActivity.RecordingTrack))
                {
                    RecordTrack.EndTrackTimer();
                    item.SetTitle(Resource.String.Record_Track);

                    //Disable the menu item for pause / resume
                    Platform.CurrentActivity.FindViewById<NavigationView>(Resource.Id.nav_view)
                        .Menu.FindItem(Resource.Id.nav_PauseResumeRecordTrack)
                        .SetTitle(Resource.String.PauseRecord_Track)
                        .SetEnabled(false);

                    NavigationView nav = Platform.CurrentActivity.FindViewById<NavigationView>(Resource.Id.nav_view);
                    nav.Menu.FindItem(Resource.Id.nav_PauseResumeRecordTrack).SetVisible(false);
                    nav.Invalidate();
                }
                else
                {
                    RecordTrack.StartTrackTimer();
                    item.SetTitle(Resource.String.Stop_Recording);

                    //Enable the menu item for pause / resume
                    Platform.CurrentActivity.FindViewById<NavigationView>(Resource.Id.nav_view)
                        .Menu.FindItem(Resource.Id.nav_PauseResumeRecordTrack)
                        .SetTitle(Resource.String.PauseRecord_Track)
                        .SetEnabled(true);

                    NavigationView? nav = Platform.CurrentActivity.FindViewById<NavigationView>(Resource.Id.nav_view);
                    nav?.Menu?.FindItem(Resource.Id.nav_PauseResumeRecordTrack).SetVisible(true);
                    nav?.Invalidate();
                }
            }
            else if (id == Resource.Id.nav_PauseResumeRecordTrack)
            {
                NavigationView? nav = Platform.CurrentActivity?.FindViewById<NavigationView>(Resource.Id.nav_view);
                var item_nav = nav?.Menu.FindItem(Resource.Id.nav_PauseResumeRecordTrack);

                if (item_nav?.TitleFormatted?.ToString() == Resources?.GetString(Resource.String.PauseRecord_Track))
                {
                    //Pause the timer
                    //RecordTrack.Timer_Order.Change(Timeout.Infinite, Timeout.Infinite);
                    Preferences.Set("RecordingTrack", false);

                    item_nav?.SetTitle(Resource.String.ResumeRecord_Track);
                }
                else
                {
                    //Resume the timer
                    //int freq_s = Int32.Parse(Preferences.Get("freq", PrefsActivity.freq_s.ToString()));
                    //RecordTrack.Timer_Order.Change(0, freq_s * 1000);
                    Preferences.Set("RecordingTrack", true);

                    item_nav.SetTitle(Resource.String.PauseRecord_Track);
                }
            }
            else if (id == Resource.Id.nav_routes)
            {
                if (Fragment_map.mapControl?.Visibility == ViewStates.Invisible)
                {
                    NavigationView? nav = this?.FindViewById<NavigationView>(Resource.Id.nav_view);
                    item = nav?.Menu.FindItem(Resource.Id.nav_routes);

                    if (item?.TitleFormatted?.ToString() == Resources.GetString(Resource.String.Map))
                    {
                        SwitchFragment("Fragment_map", item);

                        SupportFragmentManager.BeginTransaction()
                            .Remove((AndroidX.Fragment.App.Fragment)SupportFragmentManager.FindFragmentByTag("Fragment_gpx"))
                            .Commit();
                        SupportFragmentManager.ExecutePendingTransactions();
                    }
                    else
                    {
                        SupportFragmentManager.BeginTransaction()
                            .Remove((AndroidX.Fragment.App.Fragment)SupportFragmentManager.FindFragmentByTag("Fragment_gpx"))
                            .Commit();
                        SupportFragmentManager.ExecutePendingTransactions();

                        nav?.Menu?.FindItem(Resource.Id.nav_tracks).SetTitle(Resource.String.Track);
                        Fragment_gpx.GPXDisplay = Models.GPXType.Route;

                        SupportFragmentManager.BeginTransaction()
                            .Add(Resource.Id.fragment_container, new Fragment_gpx(), "Fragment_gpx")
                            .Commit();
                        SupportFragmentManager.ExecutePendingTransactions();

                        SwitchFragment("Fragment_gpx", item);
                    }
                }
                else
                {
                    Fragment_gpx.GPXDisplay = Models.GPXType.Route;

                    SupportFragmentManager.BeginTransaction()
                        .Add(Resource.Id.fragment_container, new Fragment_gpx(), "Fragment_gpx")
                        .Commit();
                    SupportFragmentManager.ExecutePendingTransactions();

                    SwitchFragment("Fragment_gpx", item);
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
                        SwitchFragment("Fragment_map", item);

                        SupportFragmentManager.BeginTransaction()
                            .Remove((AndroidX.Fragment.App.Fragment)SupportFragmentManager.FindFragmentByTag("Fragment_gpx"))
                            .Commit();
                        SupportFragmentManager.ExecutePendingTransactions();
                    }
                    else
                    {
                        SupportFragmentManager.BeginTransaction()
                            .Remove((AndroidX.Fragment.App.Fragment)SupportFragmentManager.FindFragmentByTag("Fragment_gpx"))
                            .Commit();
                        SupportFragmentManager.ExecutePendingTransactions();

                        nav?.Menu?.FindItem(Resource.Id.nav_routes).SetTitle(Resource.String.Routes);
                        Fragment_gpx.GPXDisplay = Models.GPXType.Track;

                        SupportFragmentManager.BeginTransaction()
                            .Add(Resource.Id.fragment_container, new Fragment_gpx(), "Fragment_gpx")
                            .Commit();
                        SupportFragmentManager.ExecutePendingTransactions();

                        SwitchFragment("Fragment_gpx", item);
                    }
                }
                else
                {
                    Fragment_gpx.GPXDisplay = Models.GPXType.Track;

                    SupportFragmentManager.BeginTransaction()
                        .Add(Resource.Id.fragment_container, new Fragment_gpx(), "Fragment_gpx")
                        .Commit();
                    SupportFragmentManager.ExecutePendingTransactions();

                    SwitchFragment("Fragment_gpx", item);
                }
            }
            else if (id == Resource.Id.about)
            {
                using var alert = new AndroidX.AppCompat.App.AlertDialog.Builder(Platform.CurrentActivity);
                alert.SetTitle(Platform.CurrentActivity?.Resources.GetString(Resource.String.About));
                alert.SetMessage(Platform.CurrentActivity?.Resources.GetString(Resource.String.Build) + ": " + AppInfo.Version.ToString());
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
                string backupFileName = DownLoadFolder + "/Backup-" + Resources.GetString(Resource.String.app_name) + "-" + (DateTime.Now).ToString("yyMMdd-HHmmss") + ".mbtiles";
                DBBackupConnection.Backup(backupFileName);

                //Route DB
                string dbPath = Path.Combine(PrefsActivity.rootPath, Preferences.Get("RouteDB", PrefsActivity.RouteDB));
                DBBackupConnection = new SQLiteConnection(dbPath, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex, true);
                backupFileName = DownLoadFolder + "/Backup-" + Resources.GetString(Resource.String.app_name) + "-" + (DateTime.Now).ToString("yyMMdd-HHmmss") + ".db3";
                DBBackupConnection.Backup(backupFileName);
                DBBackupConnection.Close();

                //Message
                using var alert = new AndroidX.AppCompat.App.AlertDialog.Builder(Platform.CurrentActivity);
                alert.SetTitle(Platform.CurrentActivity?.Resources.GetString(Resource.String.Backup));
                alert.SetMessage(Platform.CurrentActivity?.Resources.GetString(Resource.String.Done));
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
            return true;
        }

        public void SwitchFragment(string Fragment_Tag, IMenuItem item)
        {
            SupportFragmentManager.BeginTransaction().Show(SupportFragmentManager.FindFragmentByTag(Fragment_Tag));
            SupportFragmentManager.BeginTransaction().Commit();
            NavigationView nav = this.FindViewById<NavigationView>(Resource.Id.nav_view);

            switch (Fragment_Tag)
            {
                case "Fragment_gpx":
                    Fragment_map.mapControl.Visibility = ViewStates.Invisible;
                    FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Invisible;

                    if (item.TitleFormatted.ToString() == Resources.GetString(Resource.String.Routes))
                    {
                        nav.Menu.FindItem(Resource.Id.nav_routes).SetTitle(Resource.String.Map);
                    }

                    if (item.TitleFormatted.ToString() == Resources.GetString(Resource.String.Tracks))
                    {
                        nav.Menu.FindItem(Resource.Id.nav_tracks).SetTitle(Resource.String.Map);
                    }

                    break;
                case "Fragment_map":
                    Fragment_map.mapControl.Visibility = ViewStates.Visible;
                    FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Visible;

                    nav.Menu.FindItem(Resource.Id.nav_routes).SetTitle(Resource.String.Routes);
                    nav.Menu.FindItem(Resource.Id.nav_tracks).SetTitle(Resource.String.Tracks);

                    break;
                case "Fragment_posinfo":

                    break;
            }
        }

        public static void SwitchFragment(string Fragment_Tag, FragmentActivity activity)
        {
            var sfm = activity.SupportFragmentManager;
            sfm.BeginTransaction().Show(sfm.FindFragmentByTag(Fragment_Tag));
            sfm.BeginTransaction().Commit();
            sfm.ExecutePendingTransactions();

            NavigationView? nav = Platform.CurrentActivity.FindViewById<NavigationView>(Resource.Id.nav_view);
            IMenuItem? item;

            switch (Fragment_Tag)
            {
                case "Fragment_gpx":
                    /**///This never runs?!?
                    /*
                    Fragment_map.mapControl.Visibility = ViewStates.Invisible;
                    mContext.FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Invisible;

                    item = nav.Menu.FindItem(Resource.Id.nav_routes);
                    item.SetTitle(Resource.String.Map);

                    item = nav.Menu.FindItem(Resource.Id.nav_tracks);
                    item.SetTitle(Resource.String.Map);
                    */
                    break;
                case "Fragment_map":
                    sfm.BeginTransaction()
                        .Remove((AndroidX.Fragment.App.Fragment)sfm.FindFragmentByTag("Fragment_gpx"))
                        .Commit();
                    sfm.ExecutePendingTransactions();

                    Fragment_map.mapControl.Visibility = ViewStates.Visible;
                    Platform.CurrentActivity.FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Visible;

                    item = nav?.Menu.FindItem(Resource.Id.nav_routes);
                    item?.SetTitle(Resource.String.Routes);

                    item = nav?.Menu.FindItem(Resource.Id.nav_tracks);
                    item?.SetTitle(Resource.String.Tracks);

                    break;
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}
