﻿using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using AndroidX.DocumentFile.Provider;
using AndroidX.Preference;
using Google.Android.Material.FloatingActionButton;
using hajk.Fragments;
using hajk.Models;
using hajk.Models.MapSource;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Serilog;
using System.Globalization;

namespace hajk
{
    [Activity(Label = "preferences")]
    public class Fragment_Preferences : PreferenceFragmentCompat, ISharedPreferencesOnSharedPreferenceChangeListener
    {
        //Fragment names
        public const string Fragment_Map = "Fragment_Map";
        public const string Fragment_GPX = "Fragment_GPX";
        public const string Fragment_Settings = "Fragment_Settings";

        //Mapsui Layer Names
        public const string Layer_Poi = "Poi";
        public const string Layer_Route = "Route";
        public const string Layer_Track = "Track";

        //GeoTiff
        public const string COGGeoTiffServer = "https://s3.amazonaws.com/elevation-tiles-prod/v2/geotiff/"; //V2 of Cloud Optimized GeoTiff Server
        public readonly static int Elevation_Tile_Zoom = 14;            //Best AWS bucket has
        public readonly static double ElevationDistanceLookup = 90.0;   //Add additional waypoints every n meters

        //Settings
        public readonly static int freq_s = 5;                          //How often do we get/save current position for track recordings
        public readonly static bool DrawTrackOnGui_b = true;            //Draw recorded track on screen, or not
        public readonly static bool EnableOffRouteWarning = true;       //Enable warning if Off-Route
        public readonly static int OffTrackDistanceWarning_m = 100;     //Warn if distance from route is greater than n meters
        public readonly static int freq_OffRoute_s = 60;                //How often to check if OffRoute
        public readonly static int OffRouteSnooze_m = 5;                //Default alarm snooze in minutes
        public readonly static bool DrawPOIonGui_b = true;              //Draw POI on GUI at Startup
        public readonly static bool DrawTracksOnGui_b = true;           //Draw Tracks on GUI at Startup
        public readonly static bool DisableMapRotate_b = false;         //Disable Map Rotate. Lock North up
        public readonly static int KeepNBackups = 10;                   //Backup copies to keep
        public readonly static bool EnableBackupAtStartup = true;       //Enable backup when starting app (once per day)
        public readonly static bool EnableWakeLock = true;              //Enable the usage of wakelock to get GPS data?

        //GPX Routes / Tracks Sorting Values
        public readonly static SortOrder GPXSortingOrder = SortOrder.Ascending; //Sort list in ascending or descending order
        public readonly static string[] SortByOrder = [                //GPX Sorting Options - Do NOT change the order
            "Ascending",
            "Descending"];
        public readonly static int GPXSortingChoice = 0;                //Default sorting option for GPX routes/tracks
        public readonly static string[] SortByOptions = [               //GPX Sorting Options - Do NOT change the order
            "Distance from here",
            "Date Added",
            "Name",
            "Length of Route/Track",
            "Shenandoah's Rating",
            "Ascent",
            "Neismiths Travel Time"];

        //Runtime only
        public const string POIDB = "POI.db3";                          //Database to store all POI (WayPoints)
        public const string RouteDB = "Routes.db3";                     //Database to store all routes
        public const string SavedSettings = "SavedSettings.json";       //Saved Application Settings / Preferences
        public const string logFile = "walkabout_.txt";                 //Log file
        public const string GeoTiffFolder = "GeoTiff";                  //Folder for all GeoTiff Files
        public const string TileLayerName = "OSM";                      //Name of Tilelayer
        public readonly static string rootPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
        public readonly static string LiveData = rootPath + "/" + "LiveData";   //Live Data
        public readonly static string? DownloadFolder = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
        public readonly static string Backups = rootPath + "/" + "Backups";                 //Working folder Backups
        public readonly static string MapFolder = LiveData + "/" + "MapTiles";              //Folder to store offline tiles (one file per MapSource)
        public readonly static string CheckpointGPX = LiveData + "/" + "Checkpoint.gpx";    //Checkpoint file for GPX recordings
        public readonly static string ShareFolder = Path.Combine(rootPath, "Sharing");      //Folder for sharing files - Cleaned up app at startup

        public const int MinZoom = 0;                                   //MinZoom level to use
        public const int MaxZoom = 16;                                  //MaxZoom level to use
        public const int LocationTimer = 1000;                          //How often new LocationData is provided
        public const int LocationDistance = 0;                          //Minimum distance for new LocationData
        public readonly static bool RecordingTrack = false;             //True when recording a Track
        public readonly static bool TrackLocation = false;              //True when map is continiously moved to center on our location
        public readonly static int OfflineMaxAge = 90;                  //Don't refresh tiles until this threashhold in days
        public const int WakeLockInterval = 10;                         //How long before each wakelock request

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
        public const string TileBrowseSource = "OpenStreetMap";         //Default Tile Server

        public static List<MapSource> MapSources = [
            new MapSource("OpenStreetMap", @"https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", ""),
            new MapSource("Mapbox", @"https://api.mapbox.com/styles/v1/mapbox/outdoors-v12/tiles/{z}/{x}/{y}?access_token=", "MapboxToken"),
            new MapSource("Thunderforest", @"https://tile.thunderforest.com/outdoors/{z}/{x}/{y}.png?apikey=", "ThunderforestToken"),
            new MapSource("Stadia Maps", @"https://tiles.stadiamaps.com/tiles/outdoors/{z}/{x}/{y}.png?api_key=" , "StadiaToken"),
            new MapSource("Custom", "CustomServerURL", "CustomToken"),
        ];

        //Rogaining
        public const long DefaultMapScale = 25000L;                     //Default Map Scale
        public const string DefaultUTMZone = "54H";                     //Default UTM Zone

        //Naismith's
        public const decimal naismith_ascent = 10;                      //1min per 10m or 8.33m? /**/
        public const decimal naismith_descent = 50;                     //1min per 50m or 16.67m? /**/
        public const decimal naismith_speed_kmh = 3.5M;                 //Walking speed in km/h
        public const decimal naismith_min_per_2hour = 15.0M;            //15min break every 2h

        public override void OnCreatePreferences(Bundle? savedInstanceState, string? rootKey)
        {
            //Load Preference layout
            SetPreferencesFromResource(Resource.Xml.Preferences, rootKey);

            //Select Backup Folder
            SetSummary((Preference)FindPreference(Platform.CurrentActivity?.GetString(Resource.String.BackupFolderKey)), SafServices.PrefKeySafFolderUri);
            Preference? backupPref = (Preference)FindPreference(Platform.CurrentActivity?.GetString(Resource.String.BackupFolderKey));
            backupPref.PreferenceClick += (sender, e) =>
            {
                // Call SAF folder picker
                SafServices.RequestFolderSelection(Platform.CurrentActivity);
            };
            
            //Populate the ListPreference's
            CreateArrayList((ListPreference)FindPreference(Platform.CurrentActivity?.GetString(Resource.String.OSM_Browse_Source)));

            //Set Summary to "Not Set" or "Hidden" for sensitive fields
            SetSummary((EditTextPreference)FindPreference(Platform.CurrentActivity?.GetString(Resource.String.StadiaToken)));
            SetSummary((EditTextPreference)FindPreference(Platform.CurrentActivity?.GetString(Resource.String.MapboxToken)));
            SetSummary((EditTextPreference)FindPreference(Platform.CurrentActivity?.GetString(Resource.String.ThunderforestToken)));
            SetSummary((EditTextPreference)FindPreference(Platform.CurrentActivity?.GetString(Resource.String.CustomServerURL)));
            SetSummary((EditTextPreference)FindPreference(Platform.CurrentActivity?.GetString(Resource.String.CustomToken)));

            //Hide FloatingActionButton
            Platform.CurrentActivity.FindViewById<FloatingActionButton>(Resource.Id.fabCompass).Visibility = ViewStates.Invisible;
            Platform.CurrentActivity.FindViewById<FloatingActionButton>(Resource.Id.fabCamera).Visibility = ViewStates.Invisible;

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
            var prefs = PreferenceManager.GetDefaultSharedPreferences(Android.App.Application.Context);
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

        private static void SetSummary(Preference? pref, string Key)
        {
            var prefs = PreferenceManager.GetDefaultSharedPreferences(Android.App.Application.Context);
            if (prefs == null || pref == null)
            {
                return;
            }

            var uri = prefs.GetString(Key, null);

            //Backup Folder Specific
            if (uri != null && Key.Equals(SafServices.PrefKeySafFolderUri))
            {
                var folderUri = Android.Net.Uri.Parse(uri);
                var doc = DocumentFile.FromTreeUri(Android.App.Application.Context, folderUri);
                pref.Summary = $"Backup Folder: {doc?.Name}";
            }
        }
        
        public void OnSharedPreferenceChanged(ISharedPreferences? prefs, string? key)
        {
            if (prefs == null || key == null)
            {
                return;
            }

            //Update Summary
            if (key.Equals(SafServices.PrefKeySafFolderUri))
            {
                SetSummary((Preference)FindPreference(Platform.CurrentActivity?.GetString(Resource.String.BackupFolderKey)), SafServices.PrefKeySafFolderUri);
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

            var a = Preferences.Get("Wakelock", Fragment_Preferences.EnableWakeLock);
            if (Preferences.Get("Wakelock", Fragment_Preferences.EnableWakeLock))
            {
                PowerManager? powerManager = (PowerManager)Android.App.Application.Context.GetSystemService(Context.PowerService) as PowerManager;
                Utilities.Alarms.wakelock = powerManager?.NewWakeLock(WakeLockFlags.Full | WakeLockFlags.AcquireCausesWakeup | WakeLockFlags.OnAfterRelease, "walkabout:Alarms");
                Utilities.Alarms.wakelock?.SetReferenceCounted(false);
            }
            else
            {
                if (Utilities.Alarms.wakelock?.IsHeld == true)
                {
                    Utilities.Alarms.wakelock.Release();
                }
                Utilities.Alarms.wakelock = null;
            }


            //Show FloatingActionButton
            Platform.CurrentActivity.FindViewById<FloatingActionButton>(Resource.Id.fabCompass).Visibility = ViewStates.Visible;
            Platform.CurrentActivity.FindViewById<FloatingActionButton>(Resource.Id.fabCamera).Visibility = ViewStates.Visible;

            base.OnDestroy();
        }
    }
}
