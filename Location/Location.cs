using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Geometries;
using Mapsui.Styles;
using Xamarin.Essentials;
using Mapsui.Projection;

namespace hajk
{
    class Location
    {
        public static void UpdateLocationMarker(bool navigate)
        {
            var location = Geolocation.GetLastKnownLocationAsync().Result;
            var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);

            if (navigate)
            {
                MainActivity.mapControl.Navigator.CenterOn(sphericalMercatorCoordinate);
            }

            /**///This is bad. Is there not a better way to update the current location than removing and adding layers?
            foreach (ILayer layer in MainActivity.map.Layers.FindLayer("Location"))
            {
                MainActivity.map.Layers.Remove(layer);
            }
            MainActivity.map.Layers.Add(CreateLocationLayer(sphericalMercatorCoordinate));
        }

        public static ILayer CreateLocationLayer(Point GPSLocation)
        {
            return new MemoryLayer
            {
                Name = "Location",
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
            var feature = new Feature { Geometry = GPSLocation};

            feature.Styles.Add(new SymbolStyle
            {
                SymbolScale = 1.5f,
                Fill = null,
                Outline = new Pen { Color = Color.Red, Width = 2.0}
            });

            return feature;
        }
    }
}
