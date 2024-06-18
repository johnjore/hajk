using Mapsui;
using Mapsui.Layers;
using Mapsui.Providers;
using Mapsui.Nts;
using Mapsui.Styles;
using Mapsui.Projections;
using Mapsui.Extensions;
using Mapsui.Nts.Extensions;
using Xamarin.Essentials;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using hajk.Fragments;

namespace hajk
{
    class Location
    {
        private static readonly string LocationLayerName = "Location";
        public static Xamarin.Essentials.Location? location = null;

        public static void UpdateLocationMarker(object state)
        {
            UpdateLocationMarker(false);
        }

        public static void UpdateLocationMarker(bool navigate)
        {
            if (location == null)
            {
                return;
            }

            try
            {
                MPoint? sphericalMercatorCoordinate = SphericalMercator.FromLonLat(location.Longitude, location.Latitude).ToMPoint();
                if (sphericalMercatorCoordinate == null)
                {
                    return;
                }

                if (navigate)
                {
                    /**///Zoom should be in Fragment_map.cs, OnCreateView
                    Fragment_map.map.Navigator.CenterOn(sphericalMercatorCoordinate);
                    Fragment_map.map.Navigator.ZoomToLevel(PrefsActivity.MaxZoom);
                }

                ILayer? layer = Fragment_map.map.Layers.FindLayer(LocationLayerName).FirstOrDefault();
                if (layer == null)
                {
                    Fragment_map.map.Layers.Add(CreateLocationLayer(sphericalMercatorCoordinate));
                    layer = Fragment_map.map.Layers.FindLayer(LocationLayerName).FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Location - UpdateLocationMarker()");
            }
        }

        public static ILayer CreateLocationLayer(MPoint GPSLocation)
        {
            return new MemoryLayer
            {
                Name = LocationLayerName,
                Features = CreateLocationFeatures(GPSLocation),
                Style = null,
                IsMapInfoLayer = true
            };
        }

        private static List<IFeature> CreateLocationFeatures(MPoint GPSLocation)
        {
            return new List<IFeature>
            {
                new PointFeature(CreateLocationMarker(GPSLocation)),
            };
        }

        private static PointFeature CreateLocationMarker(MPoint GPSLocation)
        {
            var feature = new PointFeature(GPSLocation);

            feature.Styles.Add(new SymbolStyle
            {
                SymbolScale = 1.5f,
                Fill = null,
                Outline = new Pen { Color = Color.Blue, Width = 2.0 }
            });

            return feature;
        }

        //Set loction beacon color, red if recording, blue if not
        public static void UpdateLocationFeature()
        {
            Color marker = Color.Blue;

            ILayer? layer = Fragment_map.map.Layers.FindLayer(LocationLayerName).FirstOrDefault();
            if (layer == null)
            {
                Serilog.Log.Debug($"No layer?");
                return;
            }
            //var feature =layer.GetFeatures(MRect, double)

            //var feature = layer.GetFeaturesInView(layer.Envelope, 99).FirstOrDefault();
            var feature = layer.GetFeatures(layer.Extent, 99).FirstOrDefault();
            if (feature == null)
            {
                Serilog.Log.Debug($"No features?");
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
