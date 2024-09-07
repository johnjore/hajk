/*
 * using AndroidX.Core.Content;
using Android.OS;
using Android.Locations;
using Android.Content;
using Java.Lang;

public class MyForegroundService : Service
{
    private LocationManager _locationManager;

    public override void OnCreate()
    {
        base.OnCreate();

        _locationManager = (LocationManager)GetSystemService(LocationService);
    }

    private async Task GetCurrentLocationAsync()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S) // API level 30+
        {
            try
            {
                var location = await Task.Run(() =>
                {
                    delegate int MyDelegate();

                    MyDelegate myDelegate = delegate
                    {
                        return 1;
                    };

                    _locationManager.GetCurrentLocation(
                        LocationManager.GpsProvider,
                        null,
                        ContextCompat.GetMainExecutor(Application.Context),
                        location =>
                        {
                            if (location != null)
                            {
                                // Handle successful location retrieval
                                // Process location data here (e.g., update UI, store in DB)
                                Console.WriteLine($"Location: Latitude: {location.Latitude}, Longitude: {location.Longitude}");
                            }
                            else
                            {
                                // Handle null location (e.g., GPS disabled)
                                Console.WriteLine("Location unavailable");
                            }
                        });
                });

                if (location != null)
                {
                    Console.WriteLine($"Location: Latitude: {location.Latitude}, Longitude: {location.Longitude}");                    
                }
                else
                {
                    Console.WriteLine("Location unavailable");
                }
            }
            catch (SecurityException ex)
            {
                Console.WriteLine($"Security exception: {ex.Message}");
                // Handle location permission issues
            }
        }
        else
        {
            Console.WriteLine("GetCurrentLocation not supported for API level below 30");
            // Handle API level incompatibility (consider alternative methods)
        }
    }

    
    public override IBinder OnBind(Intent intent)
    {
        return null;
    }
}
*/