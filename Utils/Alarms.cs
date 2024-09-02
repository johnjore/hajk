using Android.App;
using Android.Content.PM;
using Android.Content;
using Android.Hardware;
using Android.Locations;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Widget;
using AndroidX.Activity;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;


namespace hajk.Utilities
{
    internal class Alarms
    {
        public static void CreateAlarm()
        {
            int time = Fragment_Preferences.freq_s * 2;

            Intent intent = new Intent(Platform.CurrentActivity, typeof(AlarmReceiver));
            PendingIntent? pi = PendingIntent.GetBroadcast(Platform.CurrentActivity, 0, intent, PendingIntentFlags.Immutable);
            AlarmManager? alarmManager = Application.Context.GetSystemService(Context.AlarmService) as AlarmManager;

            if (pi != null && alarmManager != null)
            {
                Serilog.Log.Debug("Current Date: " + (DateTime.Now).ToString("HH:mm:ss") + ". Alarm set for: " + (DateTime.Now.AddSeconds(time)).ToString("HH:mm:ss"));
                long AlermTimeInMilliseconds = DateTimeOffset.Now.ToUnixTimeMilliseconds() + (time * 1000);

                alarmManager.Set(AlarmType.RtcWakeup, AlermTimeInMilliseconds, pi);
            }
        }


        [BroadcastReceiver]
        public class AlarmReceiver : BroadcastReceiver
        {
            public override void OnReceive(Context? context, Intent? intent)
            {
                Serilog.Log.Debug("Alarm going Off: " + (DateTime.Now).ToString("HH:mm:ss") + "  -----------------------------------------------------------------------------------------------------------------");
                Android.Locations.Location? GpsLocation = LocationForegroundService.GetLocation();

                if (GpsLocation != null)
                {
                    DateTime gpsUTCDateTime = DateTimeOffset.FromUnixTimeMilliseconds(GpsLocation.Time).DateTime;
                    Serilog.Log.Debug($"gpsUTCDateTime: {gpsUTCDateTime}");
                    Serilog.Log.Debug($"Now           : {DateTime.UtcNow}");

                    if (gpsUTCDateTime.AddSeconds(Fragment_Preferences.freq_s * 2) < DateTime.UtcNow && (Preferences.Get("RecordingTrack", false) == true))
                    {
                        Serilog.Log.Debug("Add location to Recording");

                        var _locationManager = (LocationManager)Platform.CurrentActivity.GetSystemService(Context.LocationService);
/*                        _locationManager.GetCurrentLocation(
                           LocationManager.GpsProvider,
                           null, // Assuming null for the location request
                           ContextCompat.GetMainExecutor(Application.Context),
                           new LocationListener(OnLocationReceived));
*/
                        
                        /*
                        _locationManager.GetCurrentLocation(
                            LocationManager.GpsProvider,
                            null,
                            ContextCompat.GetMainExecutor(Application.Context),
                            location =>
                            {
                                if (location != null)
                                {
                                    Console.WriteLine($"Location: Latitude: {location.Latitude}, Longitude: {location.Longitude}");
                                }
                                else
                                {
                                    Console.WriteLine("Location unavailable");
                                }
                            });
                        */

                        //We have a location, update map accordingly


                        /*if (location != null)
                        {
                            Task.Run(() => RecordTrack.GetGPSLocationEvent(location));
                        }*/
                    }
                }

                CreateAlarm();
            }
            private void OnLocationReceived(Microsoft.Maui.Devices.Sensors.Location location)
            {
                Console.WriteLine("Notification");
                if (location != null)
                {
                    // Handle successful location retrieval
                }
                else
                {
                    // Handle null location
                    Console.WriteLine("Location unavailable");
                }
            }
        }

    
        // LocationListener delegate definition
        private class LocationListener : Java.Lang.Object, Java.Util.Functions.IConsumer //IConsumer<Location>
        {
            private readonly Action<Microsoft.Maui.Devices.Sensors.Location> _callback;

            public LocationListener(Action<Microsoft.Maui.Devices.Sensors.Location> callback)
            {
                Serilog.Log.Debug("LocationListener #2");
                _callback = callback;
            }

            public void Accept(Java.Lang.Object? location)
            {
//                Microsoft.Maui.Devices.Sensors.Location gpslocation = (Microsoft.Maui.Devices.Sensors.Location)location;
                if (location != null)
                {
                    //Do something with location
                }
                Serilog.Log.Debug("Accept");
            }
        }
    }
}
