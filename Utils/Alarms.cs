﻿using Android.App;
using Android.Content;
using Android.Locations;
using Android.OS;
using Android.Views;
using Android.Widget;
using Java.Util.Functions;
using System;

namespace hajk.Utilities
{
    internal class Alarms
    {
        public static volatile PowerManager.WakeLock? wakelock;
        public static volatile int counter = 0;

        public static void CreateAlarm()
        {
            Intent intent = new Intent(Platform.CurrentActivity, typeof(AlarmReceiver));
            PendingIntent? pi = PendingIntent.GetBroadcast(Platform.CurrentActivity, 0, intent, PendingIntentFlags.Immutable);
            AlarmManager? alarmManager = Android.App.Application.Context.GetSystemService(Context.AlarmService) as AlarmManager;

            if (pi != null && alarmManager is AlarmManager)
            {
                long AlarmTimeInMilliseconds = DateTimeOffset.Now.ToUnixTimeMilliseconds() + (Fragment_Preferences.WakeLockInterval * 1000);
                alarmManager.Set(AlarmType.RtcWakeup, AlarmTimeInMilliseconds, pi);
                
                DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(AlarmTimeInMilliseconds);
                Serilog.Log.Debug($"Alarm set for {dateTimeOffset.DateTime.ToLocalTime()}");
            }
        }

        public static void CancelAlarm()
        {
            AlarmManager? alarmManager = Android.App.Application.Context.GetSystemService(Context.AlarmService) as AlarmManager;
            if (OperatingSystem.IsAndroidVersionAtLeast(34))
            {
                alarmManager?.CancelAll();
            }
            else
            {
                /**/
                Serilog.Log.Debug("AlarmCancel not Implemented for API versions 33 and lower");
            }
        }

        [BroadcastReceiver]
        public class AlarmReceiver : BroadcastReceiver
        {
            public override void OnReceive(Context? context, Intent? intent)
            {
                Serilog.Log.Debug("Alarm going Off: " + (DateTime.Now).ToString("HH:mm:ss"));
                CreateAlarm();

                Android.Locations.Location? GpsLocation = LocationForegroundService.GetLocation();
                if (GpsLocation == null)
                {
                    if (wakelock?.IsHeld == false && Preferences.Get("EnableWakeLock", Fragment_Preferences.EnableWakeLock))
                    {
                        Serilog.Log.Debug("Acquireing Lock");
                        wakelock?.Acquire();
                        Thread.Sleep(1000);
                    }

                    LocationHelper locationHelper = new LocationHelper(Platform.CurrentActivity);
                    locationHelper.GetCurrentLocation();
                }
                else
                {
                    DateTime gpsUTCDateTime = DateTimeOffset.FromUnixTimeMilliseconds(GpsLocation.Time).DateTime;
                    //Serilog.Log.Debug($"gpsUTCDateTime: {gpsUTCDateTime}");
                    //Serilog.Log.Debug($"Add Seconds   : {gpsUTCDateTime.AddSeconds(Fragment_Preferences.freq_s * 2)}");
                    //Serilog.Log.Debug($"UTC Now       : {DateTime.UtcNow}");

                    if (gpsUTCDateTime.AddSeconds(Fragment_Preferences.freq_s * 2) < DateTime.UtcNow && (Preferences.Get("RecordingTrack", false) == true))
                    {
                        if (wakelock?.IsHeld == false && Preferences.Get("EnableWakeLock", Fragment_Preferences.EnableWakeLock))
                        {
                            Serilog.Log.Debug("Acquireing Lock");
                            wakelock?.Acquire();
                            Thread.Sleep(1000);
                        }

                        LocationHelper locationHelper = new LocationHelper(Platform.CurrentActivity);
                        locationHelper.GetCurrentLocation();
                    }
                }
            }
        }
    }
}

public class LocationHelper
{
    private LocationManager locationManager;
    private Activity activity;

    public LocationHelper(Activity activity)
    {
        this.activity = activity;
        locationManager = (LocationManager)activity.GetSystemService(Context.LocationService);
    }

    public void GetCurrentLocation()
    {
        Serilog.Log.Debug("Get new location data");

        // The main executor runs the callback on the main thread
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            locationManager.GetCurrentLocation(
                LocationManager.GpsProvider,
                null,  // Use default location settings (null for criteria)
                activity.MainExecutor,
                new LocationConsumer(location =>
                {
                    if (location != null)
                    {
                        Serilog.Log.Debug($"Location Updated from Alarm - {DateTime.Now:hh:mm:ss}: {location.Latitude:0.00000}, {location.Longitude:0.00000}");
                        hajk.Utilities.Alarms.wakelock?.Release();

                        //Update location variable
                        hajk.LocationForegroundService.SetLocation(location);

                        //Recording track?
                        if ((Preferences.Get("RecordingTrack", false) == true))
                        {
                            Task.Run(() => hajk.RecordTrack.GetGPSLocationEvent(location));
                        }

                        //Update location marker
                        bool LockedMap = Preferences.Get("TrackLocation", false);
                        Task.Run(() => hajk.Location.UpdateLocationMarker(LockedMap, location));
                    }
                })
            );
        }
        else
        {
            var location = locationManager.GetLastKnownLocation(LocationManager.GpsProvider);
            if (location != null)
            {
                Serilog.Log.Debug($"Location Updated from Alarm - {DateTime.Now:hh:mm:ss}: {location.Latitude:0.00000}, {location.Longitude:0.00000}");

                //Update location variable
                hajk.LocationForegroundService.SetLocation(location);

                //Recording track?
                if ((Preferences.Get("RecordingTrack", false) == true))
                {
                    Task.Run(() => hajk.RecordTrack.GetGPSLocationEvent(location));
                }

                //Update location marker
                bool LockedMap = Preferences.Get("TrackLocation", false);
                Task.Run(() => hajk.Location.UpdateLocationMarker(LockedMap, location));
            }
        }
    }

    // Custom implementation of Java.Util.Functions.IConsumer<Location> for callback
    private class LocationConsumer : Java.Lang.Object, IConsumer
    {
        private readonly Action<Android.Locations.Location> _onLocationReceived;

        public LocationConsumer(Action<Android.Locations.Location> onLocationReceived)
        {
            _onLocationReceived = onLocationReceived;
        }

        public void Accept(Java.Lang.Object? obj)
        {
            // Cast the Java object to a Location and call the provided callback
            if (obj is Android.Locations.Location location)
            {
                if (hajk.Utilities.Alarms.wakelock?.IsHeld == true)
                {
                    Serilog.Log.Debug("Releasing Lock");
                    hajk.Utilities.Alarms.wakelock?.Release();
                }
                _onLocationReceived?.Invoke(location);
            }
            else
            {
                Serilog.Log.Information("No new locationdata provided. Powersave?");

                Thread.Sleep(1000);
                if (hajk.Utilities.Alarms.counter++ <= 5)
                {
                    Serilog.Log.Debug($"Counter: {hajk.Utilities.Alarms.counter}");
                    LocationHelper locationHelper = new LocationHelper(Platform.CurrentActivity);
                    locationHelper.GetCurrentLocation();
                }
                else
                {
                    hajk.Utilities.Alarms.counter = 0;

                    Serilog.Log.Debug("Emergency Releasing Lock");
                    hajk.Utilities.Alarms.wakelock?.Release();
                }
            }
        }
    }
}