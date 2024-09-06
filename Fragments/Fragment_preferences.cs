using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using AndroidX.Preference;
using System.Globalization;
using Serilog;
using hajk.Fragments;
using hajk.Models.MapSource;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Google.Android.Material.FloatingActionButton;

namespace hajk
{
    [Activity(Label = "preferences")]
    public class Fragment_Preferences : PreferenceFragmentCompat, ISharedPreferencesOnSharedPreferenceChangeListener
    {
        //Misc
        public const string Fragment_Map = "Fragment_Map";
        public const string Fragment_GPX = "Fragment_GPX";
        public const string Fragment_Settings = "Fragment_Settings";

        //Settings
        public readonly static int UpdateGPSLocation_s = 1;             //How often do we update the GUI with our current location
        public readonly static int freq_s = 5;                          //How often do we get/save current position for track recordings
        public readonly static bool DrawTrackOnGui_b = true;            //Draw recorded track on screen, or not
        public readonly static bool EnableOffRouteWarning = true;       //Enable warning if Off-Route
        public readonly static int OffTrackDistanceWarning_m = 100;     //Warn if distance from route is greater than n meters
        public readonly static int freq_OffRoute_s = 60;                //How often to check if OffRoute
        public readonly static int OffRouteSnooze_m = 5;                //Default alarm snooze in minutes
        public readonly static bool DrawPOIonGui_b = true;              //Draw POI on GUI at Startup
        public readonly static bool DrawTracksOnGui_b = true;           //Draw Tracks on GUI at Startup
        public readonly static bool DisableMapRotate_b = false;         //Disable Map Rotate. Lock North up

        //Runtime only
        public const string COGGeoTiffServer = "https://elevation-tiles-prod.s3.amazonaws.com/geotiff/"; //Cloud Optimized GeoTiff Server
        public const string ElevationData = "ElevationData.tif";        //Elevation data
        public const string POIDB = "POI.db3";                          //Database to store all POI (WayPoints)
        public const string RouteDB = "Routes.db3";                     //Database to store all routes
        public const string CacheDB = "CacheDB.mbtiles";                //Database to store offline tiles
        public const string logFile = "walkabout_.txt";                 //Log file
        public readonly static string rootPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
        public const int MinZoom = 0;                                   //MinZoom level to use
        public const int MaxZoom = 16;                                  //MaxZoom level to use
        public const int LocationTimer = 1000;                          //How often new LocationData is provided
        public const int LocationDistance = 0;                          //Minimum distance for new LocationData
        public readonly static bool RecordingTrack = false;             //True when recording a Track
        public readonly static bool TrackLocation = false;              //True when map is continiously moved to center on our location
        public readonly static int OfflineMaxAge = 90;                  //Don't refresh tiles until this threashhold in days

        //Location Service
        public const int SERVICE_RUNNING_NOTIFICATION_ID = 11000;
        public const string NOTIFICATION_CHANNEL_ID = "no.jore.walkabout";
        public const string channelName = "walkabout app service";
        public const string SERVICE_STARTED_KEY = "has_service_been_started";
        public const string BROADCAST_MESSAGE_KEY = "broadcast_message";
        public const string NOTIFICATION_BROADCAST_ACTION = "walkabout.Notification.Action";
        public const string ACTION_START_SERVICE = "walkabout.action.START_SERVICE";
        public const string ACTION_STOP_SERVICE = "walkabout.action.STOP_SERVICE";
        public const string ACTION_MAIN_ACTIVITY = "walkabout.action.MAIN_ACTIVITY";

        //Tile / MapSources
        public const string TileBulkDownloadSource = "OpenStreetMap";   //Default Tile Server for Bulk Dowloads
        public const string TileBrowseSource = "OpenStreetMap";         //Default Tile Server for Browsing

        public static List<MapSource> MapSources = [
            new MapSource("OpenStreetMap", @"https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", ""),
            new MapSource("Mapbox", @"https://api.mapbox.com/styles/v1/mapbox/outdoors-v12/tiles/{z}/{x}/{y}?access_token=", "MapBoxToken"),
            new MapSource("Thunderforest", @"https://tile.thunderforest.com/outdoors/{z}/{x}/{y}.png?apikey=", "ThunderforestToken"),
            new MapSource("Custom", "CustomServerURL", "CustomToken"),
        ];

        public override void OnCreatePreferences(Bundle? savedInstanceState, string? rootKey)
        {
            //Load Preference layout
            SetPreferencesFromResource(Resource.Xml.Preferences, rootKey);

            //Populate the ListPreference's
            CreateArrayList((ListPreference)FindPreference(Platform.CurrentActivity?.GetString(Resource.String.OSM_Browse_Source)));
            CreateArrayList((ListPreference)FindPreference(Platform.CurrentActivity?.GetString(Resource.String.OSM_BulkDownload_Source)));

            //Set Summary to "Not Set" or "Hidden" for sensitive fields
            SetSummary((EditTextPreference)FindPreference(Platform.CurrentActivity?.GetString(Resource.String.MapboxToken)));
            SetSummary((EditTextPreference)FindPreference(Platform.CurrentActivity?.GetString(Resource.String.ThunderforestToken)));
            SetSummary((EditTextPreference)FindPreference(Platform.CurrentActivity?.GetString(Resource.String.CustomServerURL)));
            SetSummary((EditTextPreference)FindPreference(Platform.CurrentActivity?.GetString(Resource.String.CustomToken)));

            //Hide FloatingActionButton
            Platform.CurrentActivity.FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Invisible;

            //Create callback for when a setting is changed
            PreferenceManager.GetDefaultSharedPreferences(Platform.AppContext)?.RegisterOnSharedPreferenceChangeListener(this);
        }

        private static void CreateArrayList(ListPreference? lp)
        {
            if (lp == null)
            {
                return;
            }

            string[] entries = MapSources.Select(x => x.Name).ToArray();

            lp.SetEntries(entries);
            lp.SetEntryValues(entries);
            lp.SetDefaultValue(MapSources[0].Name);

            /*
            //Using app:useSimpleSummaryProvider="true" instead
            var prefs = PreferenceManager.GetDefaultSharedPreferences(Application.Context);
            if (prefs != null && prefs.Contains(lp.DialogTitle))
            {
                lp.Summary = prefs.GetString(lp.DialogTitle, MapSources[0].Name);
            }
            */
        }

        private static void SetSummary(EditTextPreference? etp)
        {
            var prefs = PreferenceManager.GetDefaultSharedPreferences(Application.Context);
            if (prefs == null || etp == null)
            {
                return;
            }
            
            if (etp.Text == null || etp.Text == string.Empty || etp.Text == "")
            {
                etp.Summary = Platform.CurrentActivity?.GetString(Resource.String.NotSet);
            }
            else
            {
                etp.Summary = Platform.CurrentActivity?.GetString(Resource.String.Hidden);
            }
        }

        public void OnSharedPreferenceChanged(ISharedPreferences? prefs, string? key)
        {
            if (prefs == null || key == null)
            {
                return;
            }

            Preference? pref = FindPreference(key);

            /*if (pref is ListPreference)
            {
                ListPreference listPref = (ListPreference)pref;
                listPref.Summary = listPref.Entry;
            }*/
            if (pref is EditTextPreference)
            {
                EditTextPreference editTextPref = (EditTextPreference)pref;

                if (prefs.Contains(key) && key.Contains("Token") || key.Contains("CustomServerURL"))
                {
                    var preference_setting = prefs.GetString(key, "");
                    if (preference_setting == string.Empty || preference_setting == "")
                    {
                        editTextPref.Summary = Platform.CurrentActivity?.GetString(Resource.String.NotSet);
                    }
                    else 
                    {
                        editTextPref.Summary = Platform.CurrentActivity?.GetString(Resource.String.Hidden);
                    }
                }
            }

        }

        public override void OnDestroy()
        {
            //
            bool LockMapRotation = Preferences.Get("MapLockNorth", false);
            if (LockMapRotation)
            {
                Fragment_map.map.Navigator.RotateTo(0, 0);
            }
            Fragment_map.map.Navigator.RotationLock = LockMapRotation;
            Log.Verbose($"Set map rotation (lock or not):" + LockMapRotation.ToString());

            //Show FloatingActionButton
            Platform.CurrentActivity.FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Visible;

            base.OnDestroy();
        }
    }
}
