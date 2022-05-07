using System;
using System.Threading;
using System.Net;
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
using hajk.Data;
using hajk.Fragments;
using SQLite;
using SharpGPX;

//Location service: https://github.com/shernandezp/XamarinForms.LocationService

namespace hajk
{
    [Activity(Name = "no.johnjore.hajk.MainActivity", Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener
    {
        public static Activity mContext;
        public static RouteDatabase routedatabase;
        private Intent BatteryOptimizationsIntent;
        public static int wTrackRouteMap = 0;
        public static SQLiteConnection OfflineDBConn = null;
        public static GpxClass ActiveRoute = null;                  // Active Route / Track for calculations, inc Off-Route detection

        //Location Service
        Intent startLocationServiceIntent;
        bool isLocationServiceStarted = false;

#if DEBUG
        public static string rootPath = Android.App.Application.Context.GetExternalFilesDir(null).AbsolutePath;
#else
        public static string rootPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
#endif

        readonly string[] permissions =
        {
            Android.Manifest.Permission.AccessCoarseLocation,
            Android.Manifest.Permission.AccessFineLocation,
            Android.Manifest.Permission.ReadExternalStorage,
            Android.Manifest.Permission.WriteExternalStorage,
            Android.Manifest.Permission.Internet,
            Android.Manifest.Permission.AccessNetworkState,
            Android.Manifest.Permission.RequestIgnoreBatteryOptimizations,
            Android.Manifest.Permission.Vibrate,
        };

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            //Init
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);

            //Permissions
            while (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.AccessFineLocation) != (int)Android.Content.PM.Permission.Granted ||
                   AndroidX.Core.Content.ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.WriteExternalStorage) != (int)Android.Content.PM.Permission.Granted)
            {
                RequestPermissions(permissions, 0);
            }

            //In debug mode, we use the Downloads folder for easy access, but app will only see files it creates. Uninstalling and re-installing same app, will not provide access to the old database files
#if DEBUG
            RequestPermissions(new string[] { Android.Manifest.Permission.ManageExternalStorage }, 0);
#endif

            ServicePointManager.ServerCertificateValidationCallback = (message, certificate, chain, sslPolicyErrors) => true;

            //Preferences.Clear();

            //Logging
            string _Path = System.IO.Path.Combine(rootPath, Preferences.Get("logFile", PrefsActivity.logFile));
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.AndroidLog(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj} ({SourceContext}) {Exception}")
                .WriteTo.File(_Path, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 2, outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj} ({SourceContext}) {Exception}{NewLine}"
                ).CreateLogger();
            Log.Information($"Logging to '{_Path}'");

            try
            {
                Log.Debug($"Save context for future usage");
                mContext = this;

                //Extract initial map, if not there
                Utils.Misc.ExtractInitialMap(mContext, rootPath + "/" + PrefsActivity.CacheDB);

                SetContentView(Resource.Layout.activity_main);
                AndroidX.AppCompat.Widget.Toolbar toolbar = FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.toolbar);
                SetSupportActionBar(toolbar);

                FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
                fab.Click += FabOnClick;

                DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
                ActionBarDrawerToggle toggle = new ActionBarDrawerToggle(this, drawer, toolbar, Resource.String.navigation_drawer_open, Resource.String.navigation_drawer_close);
                drawer.AddDrawerListener(toggle);
                toggle.SyncState();

                NavigationView navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
                navigationView.SetNavigationItemSelectedListener(this);

                //Sanity
                Log.Debug($"Set RecordingTrack to false - sanity check");
                Preferences.Set("RecordingTrack", false);

                Log.Debug($"Create Location Service");
                OnNewIntent(this.Intent);
                if (savedInstanceState != null)
                {
                    isLocationServiceStarted = savedInstanceState.GetBoolean(PrefsActivity.SERVICE_STARTED_KEY, false);
                    Log.Debug($"isLocationServiceStarted {isLocationServiceStarted}");
                }

                startLocationServiceIntent = new Intent(this, typeof(LocationService));
                startLocationServiceIntent.SetAction(PrefsActivity.ACTION_START_SERVICE);

                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
                {
                    Log.Debug($"Start Foreground Service");
                    StartForegroundService(startLocationServiceIntent);
                }
                else
                {
                    Log.Debug($"Start Foreground Service");
                    StartService(startLocationServiceIntent);
                }
                isLocationServiceStarted = true;

                Log.Debug($"LocationMarker");
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (Location.location == null)
                    {
                        var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(5));
                        Location.location = await Geolocation.GetLocationAsync(request, new CancellationTokenSource().Token);
                    }
                    Location.UpdateLocationMarker(true);
                });

                Log.Debug($"Create Fragment_Map");
                var FragmentsTransaction = SupportFragmentManager.BeginTransaction();
                FragmentsTransaction.Add(Resource.Id.fragment_container, new Fragment_map(), "Fragment_map");
                FragmentsTransaction.Commit();

                Log.Debug($"Save width for TrackRouteMap (Needs fixing...)");
                /**///Save width... There must be a better way...
                wTrackRouteMap = Resources.DisplayMetrics.WidthPixels;

                Log.Debug($"Notify user if battery save mode is enabled?");
                Utils.Misc.BatterySaveModeNotification();

                Log.Debug($"Notify user if location permission does not allow background collection");
                Utils.Misc.LocationPermissionNotification();
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

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);

            //Disable menu item until a GPX file has been overlayed map
            var item = menu.FindItem(Resource.Id.action_clearmap);
            item.SetEnabled(false);

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

            //Disable battery optimization
            SetDozeOptimization();

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

                //Cleanup Log file
                Log.CloseAndFlush();

                if (OfflineDBConn != null)
                    OfflineDBConn.Close();

                //Re-enable battery optimization
                //ClearDozeOptimization();

                //Location Service
                StopService(startLocationServiceIntent);
                isLocationServiceStarted = false;
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
            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);

            //Inverse as pref has been updated
            if (Preferences.Get("TrackLocation", true) == false)
            {
                fab.Background.SetTintList(ColorStateList.ValueOf(Android.Graphics.Color.Red));
                Location.UpdateLocationMarker(true);
                Fragment_map.mapControl.Map.PanLock = true;
            }
            else
            {
                fab.Background.SetTintList(ColorStateList.ValueOf(Android.Graphics.Color.LightBlue));
                Fragment_map.mapControl.Map.PanLock = false;
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
                DownloadRasterImageMap.ImportMapTiles();
            }
            else if (id == Resource.Id.nav_recordtrack)
            {
                if (Preferences.Get("RecordingTrack", PrefsActivity.RecordingTrack))
                {
                    RecordTrack.SaveTrack();
                    item.SetTitle(Resource.String.Record_Track);

                    //Disable the menu item for pause / resume
                    MainActivity.mContext.FindViewById<NavigationView>(Resource.Id.nav_view)
                        .Menu.FindItem(Resource.Id.nav_PauseResumeRecordTrack)
                        .SetTitle(Resource.String.PauseRecord_Track)
                        .SetEnabled(false);

                    NavigationView nav = MainActivity.mContext.FindViewById<NavigationView>(Resource.Id.nav_view);
                    nav.Menu.FindItem(Resource.Id.nav_PauseResumeRecordTrack).SetVisible(false);
                    nav.Invalidate();
                }
                else
                {
                    RecordTrack.StartTrackTimer();
                    item.SetTitle(Resource.String.Stop_Recording);

                    //Enable the menu item for pause / resume
                    MainActivity.mContext.FindViewById<NavigationView>(Resource.Id.nav_view)
                        .Menu.FindItem(Resource.Id.nav_PauseResumeRecordTrack)
                        .SetTitle(Resource.String.PauseRecord_Track)
                        .SetEnabled(true);

                    NavigationView nav = MainActivity.mContext.FindViewById<NavigationView>(Resource.Id.nav_view);
                    nav.Menu.FindItem(Resource.Id.nav_PauseResumeRecordTrack).SetVisible(true);
                    nav.Invalidate();
                }
            }
            else if (id == Resource.Id.nav_PauseResumeRecordTrack)
            {
                NavigationView nav = MainActivity.mContext.FindViewById<NavigationView>(Resource.Id.nav_view);
                var item_nav = nav.Menu.FindItem(Resource.Id.nav_PauseResumeRecordTrack);

                if (item_nav.TitleFormatted.ToString() == Resources.GetString(Resource.String.PauseRecord_Track))
                { 
                    //Pause the timer
                    RecordTrack.Timer_Order.Change(Timeout.Infinite, Timeout.Infinite);

                    item_nav.SetTitle(Resource.String.ResumeRecord_Track);
                }
                else
                {
                    //Resume the timer
                    int freq_s = Int32.Parse(Preferences.Get("freq", PrefsActivity.freq_s.ToString()));
                    RecordTrack.Timer_Order.Change(0, freq_s * 1000);

                    item_nav.SetTitle(Resource.String.PauseRecord_Track);
                }
            }
            else if (id == Resource.Id.nav_routes)
            {
                if (Fragment_map.mapControl.Visibility == ViewStates.Invisible)
                {
                    NavigationView nav = mContext.FindViewById<NavigationView>(Resource.Id.nav_view);
                    item = nav.Menu.FindItem(Resource.Id.nav_routes);

                    if (item.TitleFormatted.ToString() == Resources.GetString(Resource.String.Map))
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

                        nav.Menu.FindItem(Resource.Id.nav_tracks).SetTitle(Resource.String.Tracks);
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
                if (Fragment_map.mapControl.Visibility == ViewStates.Invisible)
                {
                    NavigationView nav = mContext.FindViewById<NavigationView>(Resource.Id.nav_view);
                    item = nav.Menu.FindItem(Resource.Id.nav_tracks);
                    
                    if (item.TitleFormatted.ToString() == Resources.GetString(Resource.String.Map))
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

                        nav.Menu.FindItem(Resource.Id.nav_routes).SetTitle(Resource.String.Routes);
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
                using var alert = new AndroidX.AppCompat.App.AlertDialog.Builder(MainActivity.mContext);
                alert.SetTitle(MainActivity.mContext.Resources.GetString(Resource.String.About));
                alert.SetMessage(MainActivity.mContext.Resources.GetString(Resource.String.Build) + ": " + AppInfo.Version.ToString() );
                alert.SetNeutralButton(hajk.Resource.String.Ok, (sender, args) => { });
                var dialog = alert.Create();
                dialog.Show();
            }

            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            drawer.CloseDrawer(GravityCompat.Start);
            return true;
        }

        public void SwitchFragment(string Fragment_Tag, IMenuItem item)
        {
            SupportFragmentManager.BeginTransaction().Show(SupportFragmentManager.FindFragmentByTag(Fragment_Tag));
            SupportFragmentManager.BeginTransaction().Commit();
            NavigationView nav = mContext.FindViewById<NavigationView>(Resource.Id.nav_view);

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

            NavigationView nav = mContext.FindViewById<NavigationView>(Resource.Id.nav_view);
            IMenuItem item;

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
                    mContext.FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Visible;

                    item = nav.Menu.FindItem(Resource.Id.nav_routes);
                    item.SetTitle(Resource.String.Routes);

                    item = nav.Menu.FindItem(Resource.Id.nav_tracks);
                    item.SetTitle(Resource.String.Tracks);

                    break;
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        public void SetDozeOptimization()
        {
            //https://social.msdn.microsoft.com/Forums/en-US/895f0759-e05d-4747-b72b-e16a2e8dbcf9/developing-a-location-background-service?forum=xamarinforms
            if (Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
            {
                var packageName = mContext.PackageName;
                var pm = (PowerManager)mContext.GetSystemService(Context.PowerService);
                if (!pm.IsIgnoringBatteryOptimizations(packageName))
                {
                    BatteryOptimizationsIntent = new Intent();
                    BatteryOptimizationsIntent.AddFlags(ActivityFlags.NewTask);
                    BatteryOptimizationsIntent.SetAction(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
                    BatteryOptimizationsIntent.SetData(Android.Net.Uri.Parse("package:" + packageName));
                    mContext.StartActivity(BatteryOptimizationsIntent);
                }
            }
        }

        public void ClearDozeOptimization()
        {
            //https://social.msdn.microsoft.com/Forums/en-US/895f0759-e05d-4747-b72b-e16a2e8dbcf9/developing-a-location-background-service?forum=xamarinforms
            if (null != BatteryOptimizationsIntent && Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
            {
                BatteryOptimizationsIntent.ReplaceExtras(new Bundle());
                BatteryOptimizationsIntent.SetAction("");
                BatteryOptimizationsIntent.SetData(null);
                BatteryOptimizationsIntent.SetFlags(0);
            }
        }

        protected override void OnNewIntent(Intent intent)
        {
            if (intent == null)
            {
                return;
            }

            var bundle = intent.Extras;
            if (bundle != null)
            {
                if (bundle.ContainsKey(PrefsActivity.SERVICE_STARTED_KEY))
                {
                    isLocationServiceStarted = true;
                }
            }
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            outState.PutBoolean(PrefsActivity.SERVICE_STARTED_KEY, isLocationServiceStarted);
            base.OnSaveInstanceState(outState);
        }
    }
}
