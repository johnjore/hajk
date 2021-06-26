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

//Location service: https://github.com/shernandezp/XamarinForms.LocationService

namespace hajk
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener
    {
        public static Activity mContext;
        public static RouteDatabase routedatabase;
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
        };

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            //Init and permissions
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            RequestPermissions(permission, 0);

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

            /**///This is poor. Create a dedicated background thread for location data
            Log.Debug($"Add location marker");
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Location.GetCurrentLocation();
                Location.UpdateLocationMarker(true);
            });

            //Update location every UpdateGPSLocation_s seconds
            //This is plain stupid. Why not Int type in preferences
            int UpdateGPSLocation_s = Int32.Parse(Preferences.Get("UpdateGPSLocation", PrefsActivity.UpdateGPSLocation_s.ToString()));
            Timer Order_Timer = new Timer(new TimerCallback(Location.UpdateLocationMarker), null, 0, UpdateGPSLocation_s * 1000);

            Log.Debug($"Create Fragments");
            var FragmentsTransaction = SupportFragmentManager.BeginTransaction();
            FragmentsTransaction.Add(Resource.Id.fragment_container, new Fragment_gpx(), "Fragment_gpx");
            FragmentsTransaction.Add(Resource.Id.fragment_container, new Fragment_map(), "Fragment_map");
            FragmentsTransaction.Commit();

            Log.Debug($"Save context");
            mContext = this;

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
                Log.Information($"Clear gpx entries from map");

                IEnumerable<ILayer> layers = Fragment_map.map.Layers.Where(x => (string)x.Tag == "route" || (string)x.Tag == "track");
                foreach (ILayer rt in layers)
                {
                    Fragment_map.map.Layers.Remove(rt);
                }

                //Disable the menu item, nothing to clear
                item.SetEnabled(false);
                
                return true;
            }
            return base.OnOptionsItemSelected(item);
        }

        protected override void OnStart()
        {            
            base.OnStart();
            Log.Information($"OnStart()");
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

            //Terminate Location Task
            if (Location.cts != null && !Location.cts.IsCancellationRequested)
                Location.cts.Cancel();

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

                var task = Task.Run(async () =>
                {
                    while (Preferences.Get("TrackLocation", PrefsActivity.TrackLocation) == false)
                    {
                        Log.Information($"Re-center Map on our location");
                        Location.UpdateLocationMarker(true);

                        await Task.Delay(500);
                    }
                });
            }
            else
            {
                fab.Background.SetTintList(ColorStateList.ValueOf(Android.Graphics.Color.LightBlue));
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
                    item.SetTitle("Record Track");
                }
                else
                {
                    RecordTrack.StartTrackTimer();
                    item.SetTitle("Stop Recording");
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

            switch (Fragment_Tag) {
                case "Fragment_gpx":
                    Fragment_map.mapControl.Visibility = ViewStates.Invisible;
                    FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Invisible;
                    
                    item.SetTitle("Map");
                    break;
                case "Fragment_map":
                    Fragment_map.mapControl.Visibility = ViewStates.Visible;
                    FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Visible;

                    item.SetTitle("Routes / Tracks");
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

                    item.SetTitle("Map");
                    break;
                case "Fragment_map":
                    Fragment_map.mapControl.Visibility = ViewStates.Visible;
                    mContext.FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Visible;

                    item.SetTitle("Routes / Tracks");
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
