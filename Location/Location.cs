using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Geometries;
using Mapsui.Styles;
using Xamarin.Essentials;
using Mapsui.Projection;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace hajk
{
    class Location
    {
        private static readonly string LocationLayerName = "Location";
        public static Xamarin.Essentials.Location location = null;

        public static void UpdateLocationMarker(object state)
        {
            UpdateLocationMarker(false);
        }

        public static void UpdateLocationMarker(bool navigate)
        {
            try
            {
                Point sphericalMercatorCoordinate = null;

                if (location == null)
                    return;

                sphericalMercatorCoordinate = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
                if (sphericalMercatorCoordinate == null)
                    return;

                if (navigate)
                {
                    Fragments.Fragment_map.mapControl.Navigator.CenterOn(sphericalMercatorCoordinate);
                }

                ILayer layer = Fragments.Fragment_map.map.Layers.FindLayer(LocationLayerName).FirstOrDefault();
                if (layer == null)
                {
                    Fragments.Fragment_map.map.Layers.Add(CreateLocationLayer(sphericalMercatorCoordinate));
                    layer = Fragments.Fragment_map.map.Layers.FindLayer(LocationLayerName).FirstOrDefault();
                } 

                var feature = layer.GetFeaturesInView(layer.Envelope, 99).FirstOrDefault();
                if (feature == null)
                {
                    Serilog.Log.Information($"No features?");
                    return;
                }

                feature.Geometry = sphericalMercatorCoordinate;
                layer.DataHasChanged();

                //Fragments.Fragment_map.map.Home = n => n.CenterOn(sphericalMercatorCoordinate);
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
            
            feature.Styles.Add(new SymbolStyle
            {
                SymbolScale = 1.5f,
                Fill = null,
                Outline = new Pen { Color = Color.Blue, Width = 2.0 }
            });

            return feature;
        }

        public static void UpdateLocationFeature()
        {
            Color marker = Color.Blue;

            ILayer layer = Fragments.Fragment_map.map.Layers.FindLayer(LocationLayerName).FirstOrDefault();
            if (layer == null)
            {
                Serilog.Log.Information($"No layer?");
                return;
            }

            var feature = layer.GetFeaturesInView(layer.Envelope, 99).FirstOrDefault();
            if (feature == null)
            {
                Serilog.Log.Information($"No features?");
                return;
            }

            if (Preferences.Get("RecordingTrack", PrefsActivity.RecordingTrack))
            {
                marker = Color.Red;
            }

            feature.Styles.Clear();
            feature.Styles.Add(new SymbolStyle
            {
                SymbolScale = 1.5f,
                Fill = null,
                Outline = new Pen { Color = marker, Width = 2.0 }
            });
        }
    }
}
