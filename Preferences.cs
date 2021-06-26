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
        public readonly static int UpdateGPSLocation_s = 5;     //How often do we update the GUI with our current location
        public readonly static int freq_s = 5;                  //How often do we get/save current position for track recordings
        public readonly static bool DrawTrackOnGui_b = true;    //Draw recorded track on screen, or not
        static public readonly string  OSMServer_s = "https://cloudstorage.jore.no/tile/"; /**///Replace with something different...

        //Runtime only
        public readonly static string RouteDB = "Routes.db3";   //Database to store all routes
        public readonly static string logFile = "hajk_.txt";    //Log file
        public readonly static bool RecordingTrack = false;     //True when recording a Track
        public readonly static bool TrackLocation = false;      //True when map is continiously moved to center on our location
        public readonly static int MinZoom = 13;                //MinZoom level to use
        public readonly static int MaxZoom = 17;                //MaxZoom level to use

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            AddPreferencesFromResource(Resource.Xml.Preferences);
        }
    }
}
