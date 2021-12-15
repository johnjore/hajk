using Android.App;
using Android.OS;
using Android.Preferences;
using Xamarin.Essentials;

namespace hajk
{
    [Activity(Label = "preferences")]
    public class PrefsActivity : PreferenceActivity
    {
        //Settings
        public readonly static int UpdateGPSLocation_s = 1;     //How often do we update the GUI with our current location
        public readonly static int freq_s = 5;                  //How often do we get/save current position for track recordings
        public readonly static bool DrawTrackOnGui_b = true;    //Draw recorded track on screen, or not
        static public readonly string OSMServer_s = "https://cloudstorage.jore.no/tile/"; /**///Replace with something different...

        //Runtime only
        public const string RouteDB = "Routes.db3";           //Database to store all routes
        public const string OfflineDB = "OfflineDB.mbtiles";  //Database to store offline tiles
        public const string logFile = "hajk_.txt";            //Log file
        public const int MinZoom = 0;                         //MinZoom level to use
        public const int MaxZoom = 17;                        //MaxZoom level to use
        public readonly static bool RecordingTrack = false;             //True when recording a Track
        public readonly static bool TrackLocation = false;              //True when map is continiously moved to center on our location
        public readonly static int OfflineMaxAge = 30;                  //Don't refresh tiles until this threashhold in days


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

            AddPreferencesFromResource(Resource.Xml.Preferences);
        }
    }
}
