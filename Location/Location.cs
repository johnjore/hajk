using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Geometries;
using Mapsui.Styles;
using Xamarin.Essentials;
using Mapsui.Projection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace hajk
{
    class Location
    {
        private static readonly string LocationLayerName = "Location";
        public static CancellationTokenSource cts;
        public static Xamarin.Essentials.Location location = null;

        public static void UpdateLocationMarker(object state)
        {
            UpdateLocationMarker(false);
        }

        public static void UpdateLocationMarker(bool navigate)
        {
            try
            {
                //var location = Geolocation.GetLastKnownLocationAsync().Result;
                _ = GetCurrentLocation();

                var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);

                if (navigate)
                {
                    Fragments.Fragment_map.mapControl.Navigator.CenterOn(sphericalMercatorCoordinate);
                }

                /**///This is bad. Is there not a better way to update the current location than removing and adding layers?
                foreach (ILayer layer in Fragments.Fragment_map.map.Layers.FindLayer(LocationLayerName))
                {
                    Fragments.Fragment_map.map.Layers.Remove(layer);
                }
                Fragments.Fragment_map.map.Layers.Add(CreateLocationLayer(sphericalMercatorCoordinate));
            }
            catch (Exception ex)
            {
                Serilog.Log.Information($"No location information? '{ex}'");
            }
        }

        public static async Task GetCurrentLocation()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(2));
                cts = new CancellationTokenSource();
                location = await Geolocation.GetLocationAsync(request, cts.Token);

                if (location != null)
                {
                    Console.WriteLine($"Latitude: {location.Latitude}, Longitude: {location.Longitude}, Altitude: {location.Altitude}");
                }
            }
            catch (FeatureNotSupportedException fnsEx)
            {
                Serilog.Log.Information($"FeatureNotSupportedException: '{fnsEx}'");
            }
            catch (FeatureNotEnabledException fneEx)
            {
                Serilog.Log.Information($"FeatureNotEnabledException: '{fneEx}'");
            }
            catch (PermissionException pEx)
            {
                Serilog.Log.Information($"PermissionException: '{pEx}'");
            }
            catch (Exception ex)
            {
                Serilog.Log.Information($"Unable to get location: '{ex}'");
            }
        }

        public static ILayer CreateLocationLayer(Point GPSLocation)
        {
            return new MemoryLayer
            {
                Name = LocationLayerName,
                DataSource = CreateMemoryProviderWithDiverseSymbols(GPSLocation),
                Style = null,
                IsMapInfoLayer = true
            };
        }

        private static MemoryProvider CreateMemoryProviderWithDiverseSymbols(Point GPSLocation)
        {
            return new MemoryProvider(CreateLocationMarker(GPSLocation));
        }

        private static Features CreateLocationMarker(Point GPSLocation)
        {
            var features = new Features
            {
                CreateLocationFeature(GPSLocation)
            };
            return features;
        }

        private static IFeature CreateLocationFeature(Point GPSLocation)
        {
            var feature = new Feature { Geometry = GPSLocation };
            Color marker = Color.Blue;

            if (Preferences.Get("RecordingTrack", PrefsActivity.RecordingTrack))
            {
                marker = Color.Red;
            }

            feature.Styles.Add(new SymbolStyle
            {
                SymbolScale = 1.5f,
                Fill = null,
                Outline = new Pen { Color = marker, Width = 2.0 }
            });

            return feature;
        }
    }
}
