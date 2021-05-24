using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Geometries;
using Mapsui.Styles;
using Xamarin.Essentials;
using Mapsui.Projection;
using System;

namespace hajk
{
    class Location
    {
        private static readonly string LocationLayerName = "Location";


        public static void UpdateLocationMarker(object state)
        {
            UpdateLocationMarker(false);
        }

        public static void UpdateLocationMarker(bool navigate)
        {
            try
            {
                var location = Geolocation.GetLastKnownLocationAsync().Result;
                var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);

                if (navigate)
                {
                    MainActivity.mapControl.Navigator.CenterOn(sphericalMercatorCoordinate);
                }

                /**///This is bad. Is there not a better way to update the current location than removing and adding layers?
                foreach (ILayer layer in MainActivity.map.Layers.FindLayer(LocationLayerName))
                {
                    MainActivity.map.Layers.Remove(layer);
                }
                MainActivity.map.Layers.Add(CreateLocationLayer(sphericalMercatorCoordinate));
            }
            catch (Exception ex)
            {
                Serilog.Log.Information($"No location information? '{ex}'");
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
