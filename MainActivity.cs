using System;
using System.IO;
using System.Threading;
using System.Net;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Content;
using Android.Content.Res;
using Android.Support.V7.Widget;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using AndroidX.Fragment;
using AndroidX.Fragment.App;
using AndroidX.Core.View;
using AndroidX.DrawerLayout.Widget;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Navigation;
using Google.Android.Material.Snackbar;
using Mapsui;
using Mapsui.Geometries;
using Mapsui.Layers;
using Mapsui.Projection;
using Mapsui.UI;
using Mapsui.UI.Android;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Utilities;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;
using BruTile.Predefined;
using BruTile.Web;
using Serilog;
using Xamarin.Essentials;
using hajk.Data;
using hajk.Adapter;
using hajk.Fragments;
using SQLite;

//Location service: https://github.com/shernandezp/XamarinForms.LocationService

namespace hajk
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener
    {
        public static Activity mContext;
        public static RouteDatabase routedatabase;
        private Intent BatteryOptimizationsIntent;
        public static int wTrackRouteMap = 0;
        public static SQLiteConnection OfflineDBConn = null;

        //Location Service
        Intent startLocationServiceIntent;
        bool isLocationServiceStarted = false;

#if DEBUG
        public static string rootPath = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
#else
        public static string rootPath = Android.OS.Environment.DataDirectory.AbsolutePath;
#endif

        readonly string[] permission =
        {
            Android.Manifest.Permission.AccessCoarseLocation,
            Android.Manifest.Permission.AccessFineLocation,
            Android.Manifest.Permission.ReadExternalStorage,
            Android.Manifest.Permission.WriteExternalStorage,
            Android.Manifest.Permission.Internet,
            Android.Manifest.Permission.AccessNetworkState,
            Android.Manifest.Permission.RequestIgnoreBatteryOptimizations,            
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
                RequestPermissions(permission, 0);
            }

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

            Log.Debug($"Create toolbar");
            SetContentView(Resource.Layout.activity_main);
            AndroidX.AppCompat.Widget.Toolbar toolbar = FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            Log.Debug($"Create floating action button");
            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;

            Log.Debug($"Create drawer layout");
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            ActionBarDrawerToggle toggle = new ActionBarDrawerToggle(this, drawer, toolbar, Resource.String.navigation_drawer_open, Resource.String.navigation_drawer_close);
            drawer.AddDrawerListener(toggle);
            toggle.SyncState();

            Log.Debug($"Create navigation view");
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
            }

            startLocationServiceIntent = new Intent(this, typeof(LocationService));
            startLocationServiceIntent.SetAction(PrefsActivity.ACTION_START_SERVICE);

            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            {
                StartForegroundService(startLocationServiceIntent);
            }
            else
            {
                StartService(startLocationServiceIntent);
            }
            isLocationServiceStarted = true;

            Log.Debug($"Add location marker");
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (Location.location == null)
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(5));
                    Location.location = await Geolocation.GetLocationAsync(request, new CancellationTokenSource().Token);
                }
                Location.UpdateLocationMarker(true);
            });

            Log.Debug($"Create Fragments");
            var FragmentsTransaction = SupportFragmentManager.BeginTransaction();
            FragmentsTransaction.Add(Resource.Id.fragment_container, new Fragment_gpx(), "Fragment_gpx");
            FragmentsTransaction.Add(Resource.Id.fragment_container, new Fragment_map(), "Fragment_map");
            FragmentsTransaction.Commit();

            Log.Debug($"Save context");
            mContext = this;
            
            Log.Debug($"Save width for TrackRouteMap (Needs fixing...)");
            /**///Save width... There must be a better way...
            wTrackRouteMap = Resources.DisplayMetrics.WidthPixels;

            Log.Debug($"Done with OnCreate()");
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
                    base.OnBackPressed();
                }
            }
            else
            {
                base.OnBackPressed();
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
            base.OnStart();
            Log.Information($"OnStart()");

            //Disable battery optimization
            SetDozeOptimization();
        }

        protected override void OnStop()
        {
            base.OnStart();
            Log.Information($"OnStart()");
        }

        protected override void OnPause()
        {
            base.OnResume();
            Log.Information($"OnPause()");
        }

        protected override void OnResume()
        {
            base.OnResume();
            Log.Information($"OnResume()");
        }

        protected override void OnDestroy()
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
                OfflineMaps.LoadMap();
            }
            else if (id == Resource.Id.nav_recordtrack)
            {
                if (Preferences.Get("RecordingTrack", PrefsActivity.RecordingTrack))
                {
                    RecordTrack.SaveTrack();
                    item.SetTitle(Resource.String.Record_Track);
                }
                else
                {
                    RecordTrack.StartTrackTimer();
                    item.SetTitle(Resource.String.Stop_Recording);
                }
            }
            else if (id == Resource.Id.nav_manage)
            {
                if (Fragment_map.mapControl.Visibility == ViewStates.Invisible)
                {
                    SwitchFragment("Fragment_map", item);
                }
                else
                {
                    SwitchFragment("Fragment_gpx", item);
                }
            }
            else if (id == Resource.Id.nav_share)
            {

            }
            else if (id == Resource.Id.nav_send)
            {

            }

            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            drawer.CloseDrawer(GravityCompat.Start);
            return true;
        }

        public void SwitchFragment(string Fragment_Tag, IMenuItem item)
        {
            SupportFragmentManager.BeginTransaction().Show(SupportFragmentManager.FindFragmentByTag(Fragment_Tag));
            SupportFragmentManager.BeginTransaction().Commit();

            switch (Fragment_Tag)
            {
                case "Fragment_gpx":
                    Fragment_map.mapControl.Visibility = ViewStates.Invisible;
                    FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Invisible;

                    item.SetTitle(Resource.String.Map);
                    break;
                case "Fragment_map":
                    Fragment_map.mapControl.Visibility = ViewStates.Visible;
                    FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Visible;

                    item.SetTitle(Resource.String.Routes_Tracks);
                    break;
            }
        }

        public static void SwitchFragment(string Fragment_Tag, FragmentActivity activity)
        {
            var sfm = activity.SupportFragmentManager;
            sfm.BeginTransaction().Show(sfm.FindFragmentByTag(Fragment_Tag));
            sfm.BeginTransaction().Commit();

            NavigationView nav = mContext.FindViewById<NavigationView>(Resource.Id.nav_view);
            var item = nav.Menu.FindItem(Resource.Id.nav_manage);

            switch (Fragment_Tag)
            {
                case "Fragment_gpx":
                    Fragment_map.mapControl.Visibility = ViewStates.Invisible;
                    mContext.FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Invisible;

                    item.SetTitle(Resource.String.Map);
                    break;
                case "Fragment_map":
                    Fragment_map.mapControl.Visibility = ViewStates.Visible;
                    mContext.FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Visible;

                    item.SetTitle(Resource.String.Routes_Tracks);
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
