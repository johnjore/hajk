using Android.App;
using Android.OS;
using Android.Preferences;
using Serilog;
using Xamarin.Essentials;
using hajk.Fragments;

namespace hajk
{
    [Activity(Label = "preferences")]
    public class PrefsActivity : PreferenceActivity
    {
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
        public const string logFile = "hajk_.txt";                      //Log file
        public const int MinZoom = 0;                                   //MinZoom level to use
        public const int MaxZoom = 16;                                  //MaxZoom level to use
        public readonly static bool RecordingTrack = false;             //True when recording a Track
        public readonly static bool TrackLocation = false;              //True when map is continiously moved to center on our location
        public readonly static int OfflineMaxAge = 90;                  //Don't refresh tiles until this threashhold in days        

        //Location Service
        public const int SERVICE_RUNNING_NOTIFICATION_ID = 10000;
        public const string NOTIFICATION_CHANNEL_ID = "no.jore.hajk";
        public const string channelName = "hajk app service";
        public const string SERVICE_STARTED_KEY = "has_service_been_started";
        public const string BROADCAST_MESSAGE_KEY = "broadcast_message";
        public const string NOTIFICATION_BROADCAST_ACTION = "hajk.Notification.Action";
        public const string ACTION_START_SERVICE = "hajk.action.START_SERVICE";
        public const string ACTION_STOP_SERVICE = "hajk.action.STOP_SERVICE";
        public const string ACTION_MAIN_ACTIVITY = "hajk.action.MAIN_ACTIVITY";

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (OperatingSystem.IsAndroidVersionAtLeast(29))
            {
                /**///Fix me
                AddPreferencesFromResource(Resource.Xml.Preferences);
            }
            else
            {
                AddPreferencesFromResource(Resource.Xml.Preferences);
            }
        }

        protected override void OnDestroy()
        {
            //
            bool LockMapRotation = Preferences.Get("MapLockNorth", false);
            if (LockMapRotation)
            {
                Fragment_map.map.Navigator.RotateTo(0, 0);
            }
            Fragment_map.map.Navigator.RotationLock = LockMapRotation;
            Log.Verbose($"Set map rotation (lock or not):" + LockMapRotation.ToString());

            base.OnDestroy();
        }
    }
}
