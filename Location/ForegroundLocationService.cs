using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Util;
using Android.Content;
using Android.Runtime;
using Android.Locations;
using Android.OS;
using Android.Preferences;
using AndroidX.Core.App;
using AndroidX.Fragment.App;
using Microsoft.Maui.Storage;
using hajk.Fragments;

namespace hajk
{
    [Service(ForegroundServiceType = Android.Content.PM.ForegroundService.TypeLocation)]
    public class LocationForegroundService : Service, ILocationListener
    {
        public static bool isStarted = false;  //Is ForegroundService running?
        private static Android.Locations.Location? currentLocation;

        public void OnProviderDisabled(string provider)
        {
            Serilog.Log.Debug("OnProviderDisabled - '" + provider + "'");
        }

        public void OnProviderEnabled(string provider)
        {
            Serilog.Log.Debug("OnProviderEnabled - '" + provider + "'");
        }

        public void OnStatusChanged(string? provider, [GeneratedEnum] Availability status, Bundle? extras)
        {
            Serilog.Log.Debug("OnStatusChanged - '" + provider + "'");

            LocationManager? locationManager = GetSystemService(LocationService) as LocationManager;
            locationManager?.RemoveUpdates(this);

            string lProvider = InitializeLocationManager();

            if (lProvider != null && locationManager != null)
            {
                var intTimer = Fragment_Preferences.LocationTimer;
                var intDistance = Fragment_Preferences.LocationDistance;
                Serilog.Log.Debug($"ServiceRunning: Creating callback service for LocationUpdates, every " + intTimer.ToString() + "s and " + intDistance.ToString() + "m");
                locationManager = GetSystemService(LocationService) as LocationManager;
                locationManager?.RequestLocationUpdates(lProvider, intTimer, intDistance, this, Looper.MainLooper);
            }
            else
            {
                Serilog.Log.Error("Failed to create 'RequestLocationUpdates'");
            }
        }

        public void OnLocationChanged(Android.Locations.Location location)
        {
            if (location == null)
            {
                return;
            }

            Serilog.Log.Debug($"Location Updated - {DateTime.Now:hh:mm:ss}: {location.Latitude:0.00000}, {location.Longitude:0.00000}");

            //Update location variable
            currentLocation = location;

            //Recording track?
            if ((Preferences.Get("RecordingTrack", false) == true))
            {
                Task.Run(() => RecordTrack.GetGPSLocationEvent(location));
            }

            //Update location marker
            bool LockedMap = Preferences.Get("TrackLocation", false);
            Task.Run(() => Location.UpdateLocationMarker(LockedMap, location));
        }

        public static Android.Locations.Location? GetLocation()
        {
            try
            {
                if (currentLocation == null)
                {
                    var _Location = Geolocation.GetLastKnownLocationAsync().Result;
                    if (_Location == null)
                    {
                        // Try to get a fresh location
                        _Location = Geolocation.GetLocationAsync(new GeolocationRequest
                        {
                            DesiredAccuracy = GeolocationAccuracy.Medium,
                            Timeout = TimeSpan.FromSeconds(5)
                        }).Result;
                    }

                    if (_Location != null && currentLocation == null)
                    {
                        currentLocation = new Android.Locations.Location("manual")
                        {
                            Latitude = _Location.Latitude,
                            Longitude = _Location.Longitude
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, $"Error getting location");
            }

            return currentLocation;
        }

        public static void SetLocation(Android.Locations.Location location)
        {
            currentLocation = location;
        }

        private string InitializeLocationManager()
        {
            LocationManager? lm = GetSystemService(LocationService) as LocationManager;

            if (lm == null)
            {
                Serilog.Log.Error("locationManager is null. Can't continue");
                return string.Empty;
            }

            IList<string> acceptableLocationProviders;

            if (OperatingSystem.IsAndroidVersionAtLeast(34))
            {
                acceptableLocationProviders = lm.GetProviders(true);
            }
            else
            {
                Criteria criteriaForLocationService = new()
                {
                    Accuracy = Accuracy.Fine,
                    SpeedRequired = true,
                    SpeedAccuracy = Accuracy.Fine
                };

                acceptableLocationProviders = lm.GetProviders(criteriaForLocationService, true);
            }

            //Choose GPS over all other options
            string? provider = acceptableLocationProviders.Where(x => x.Equals("gps")).FirstOrDefault();
            if (provider != null)
            {
                return provider;
            }

            //Else choose first option offered
            if (acceptableLocationProviders.Any())
            {
                Serilog.Log.Debug($"LocationProvider: '{acceptableLocationProviders.First()}'");
                return acceptableLocationProviders.First();
            }

            return string.Empty;
        }

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            if (intent is null || intent.Action is null)
            {
                Serilog.Log.Warning($"OnStartCommand: intent or intent.Action is null ");
                return StartCommandResult.Sticky;
            }

            if (intent.Action.Equals(Fragment_Preferences.ACTION_START_SERVICE))
            {
                if (isStarted)
                {
                    Serilog.Log.Information($"OnStartCommand: The location service is already running");
                }
                else
                {
                    Serilog.Log.Information($"OnStartCommand: The location service is starting");

                    OnStatusChanged(null, Availability.Available, null);

                    isStarted = true;
                    RegisterForegroundService();
                }
            }
            else if (intent.Action.Equals(Fragment_Preferences.ACTION_STOP_SERVICE))
            {
                Serilog.Log.Information($"OnStartCommand: The location service is stopping.");

                if (OperatingSystem.IsAndroidVersionAtLeast(24))
                {
                    StopForeground(StopForegroundFlags.Remove);
                }
                else
                {
                    StopForeground(true);
                }

                StopSelf();
                isStarted = false;
            }

            return StartCommandResult.Sticky;
        }

        public override IBinder? OnBind(Intent? intent)
        {
            // Return null because this is a pure started service. A hybrid service would return a binder that would allow access to the GetFormattedStamp() method.
            return null;
        }

        public override void OnDestroy()
        {
            Serilog.Log.Information($"OnDestroy: The location service is shutting down.");

            //Service is no longer "Started"
            isStarted = false;

            try
            {
                //Stop listing to location updates
                LocationManager? lm = GetSystemService(LocationService) as LocationManager;
                lm?.RemoveUpdates(this);
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal($"Crashed: " + ex.ToString());
            }

            base.OnDestroy();
        }

        private void RegisterForegroundService()
        {
            if (Resources is null)
            {
                Serilog.Log.Warning($"RegisterForegroundService: resources is null, returning early");
                return;
            }

            NotificationManager? nManager = GetSystemService(Context.NotificationService) as NotificationManager;
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                NotificationChannel nChannel = new(Fragment_Preferences.NOTIFICATION_CHANNEL_ID, Fragment_Preferences.channelName, NotificationImportance.Low)
                {
                    LockscreenVisibility = NotificationVisibility.Private,
                };

                nManager?.CreateNotificationChannel(nChannel);
            }

            NotificationCompat.Builder? notificationBuilder = new(this, Fragment_Preferences.NOTIFICATION_CHANNEL_ID);
            Notification? notification;
            if (OperatingSystem.IsAndroidVersionAtLeast(28))
            {
                notification = notificationBuilder
                .SetOngoing(true)
                .SetSmallIcon(Resource.Drawable.track)
                .SetContentText(Resources?.GetString(Resource.String.notification_text))
                .SetAutoCancel(true)
                .SetPriority((int)NotificationPriority.Low)
                .SetCategory(Notification.CategoryNavigation)
                .SetContentIntent(BuildIntentToShowMainActivity())
                .Build();
            }
            else
            {
                notification = notificationBuilder
                .SetOngoing(true)
                .SetSmallIcon(Resource.Drawable.track)
                .SetContentText(Resources?.GetString(Resource.String.notification_text))
                .SetAutoCancel(true)
                .SetPriority((int)NotificationPriority.Low)
                .SetContentIntent(BuildIntentToShowMainActivity())
                .Build();
            }

            // Enlist this instance as a foreground service
            StartForeground(Fragment_Preferences.SERVICE_RUNNING_NOTIFICATION_ID, notification);

            //We have a location, update map accordingly
            LocationManager? _locationManager = GetSystemService(LocationService) as LocationManager;
            var location = _locationManager?.GetLastKnownLocation(LocationManager.GpsProvider);
            if (location != null)
            {
                Location.UpdateLocationMarker(true, location);
                Fragment_map.map.Navigator.ZoomToLevel(Fragment_Preferences.MaxZoom);
            }
        }

        /// <summary>
        /// Builds a PendingIntent that will display the main activity of the app. This is used when the 
        /// user taps on the notification; it will take them to the main activity of the app.
        /// </summary>
        /// <returns>The content intent.</returns>
        PendingIntent? BuildIntentToShowMainActivity()
        {
            Intent notificationIntent = new Intent(this, typeof(MainActivity));
            notificationIntent.SetAction(Fragment_Preferences.ACTION_MAIN_ACTIVITY);
            //notificationIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.NewTask);
            notificationIntent.SetFlags(ActivityFlags.ReorderToFront);
            notificationIntent.PutExtra(Fragment_Preferences.SERVICE_STARTED_KEY, true);

            if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                return PendingIntent.GetActivity(this, 0, notificationIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            }
            else
            {
                return PendingIntent.GetActivity(this, 0, notificationIntent, PendingIntentFlags.UpdateCurrent);
            }
        }
    }
}
