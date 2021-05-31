using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Views;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
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
using hajk.Data;
using Xamarin.Essentials;
using Android.Content;
using System.IO;
using System.Threading;
using System.Net;
using Android.Content.Res;

//Location service: https://github.com/shernandezp/XamarinForms.LocationService

namespace hajk
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener
    {
        public static Activity mContext;
        public static Mapsui.Map map = new Mapsui.Map();
        public static MapControl mapControl;
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

            //GUI
            SetContentView(Resource.Layout.activity_main);
            Toolbar toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
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
            Preferences.Set("RecordingTrack", false);

            //Add map
            mapControl = FindViewById<MapControl>(Resource.Id.mapcontrol);
            map = new Mapsui.Map
            {
                CRS = "EPSG:3857", //https://epsg.io/3857
                Transformation = new MinimalTransformation(),
            };
            mapControl.Map = map;

            /**///Change to configuration item due to usage policy
            //var tileSource = new HttpTileSource(new GlobalSphericalMercator(), "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", new[] { "a", "b", "c" }, name: "OpenStreetMap", userAgent: "OpenStreetMap in Mapsui (hajk)");
            var tileSource = TileCache.GetOSMBasemap(rootPath + "/CacheDB.mbtiles");
            var tileLayer = new TileLayer(tileSource)
            {
                Name = "OSM",
            };
            map.Layers.Add(tileLayer);

            //Import all Offline Maps
            OfflineMaps.LoadAllOfflineMaps();

            /**///This is poor. Create a dedicated background thread for location data
            //Add location marker
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Location.GetCurrentLocation();
                Location.UpdateLocationMarker(true);
            });

            // Add scalebar
            map.Widgets.Add(new ScaleBarWidget(map)
            {
                MaxWidth = 300,
                ShowEnvelop = true,
                Font = new Font { FontFamily = "sans serif", Size = 20 },
                TickLength = 15,
                TextColor = new Color(0, 0, 0, 255),
                Halo = new Color(0, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                TextAlignment = Alignment.Left,
                ScaleBarMode = ScaleBarMode.Both,
                UnitConverter = MetricUnitConverter.Instance,
                SecondaryUnitConverter = NauticalUnitConverter.Instance,
                MarginX = 10,
                MarginY = 20,
            });

            //Update location every UpdateGPSLocation_s seconds
            //This is plain stupid. Why not Int type in preferences
            int UpdateGPSLocation_s = Int32.Parse(Preferences.Get("UpdateGPSLocation", PrefsActivity.UpdateGPSLocation_s.ToString()));
            Timer Order_Timer = new Timer(new TimerCallback(Location.UpdateLocationMarker), null, 0, UpdateGPSLocation_s * 1000);

            mContext = this;
        }

        public override void OnBackPressed()
        {
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            if (drawer.IsDrawerOpen(GravityCompat.Start))
            {
                drawer.CloseDrawer(GravityCompat.Start);
            }
            else
            {
                base.OnBackPressed();
            }
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
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

            return base.OnOptionsItemSelected(item);
        }

        protected override void OnDestroy()
        {
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
                /**///and display on map. Change this to open Route view instead. Give user option to download maps for each route / track
                Import.GetRoute();
            }
            else if (id == Resource.Id.nav_offlinemap)
            {
                /**///This is temp until app can download tiles and store in sqlite DB on device
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

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}
