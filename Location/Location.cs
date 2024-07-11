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

        public static void UpdateLocationMarker(object state)
        {
            UpdateLocationMarker(false);
        }

        public static void UpdateLocationMarker(bool navigate, Android.Locations.Location location)
        {
            try
            {
                MPoint? sphericalMercatorCoordinate = (SphericalMercator.FromLonLat(location.Longitude, location.Latitude)).ToMPoint();
                if (sphericalMercatorCoordinate == null)
                {
                    return;
                }

                //Location circle. Remove if it exists, re-create with new location. Would it be better/faster to update feature with new position?
                ILayer? layer = Fragment_map.map.Layers.FindLayer(LocationLayerName).FirstOrDefault();
                if (layer != null)
                {
                    Fragment_map.map.Layers.Remove(layer);
                }
                Fragment_map.map.Layers.Add(CreateLocationLayer(sphericalMercatorCoordinate));

                if (navigate)
                {
                    Fragment_map.mapControl.Map.Navigator.PanLock = false;
                    Fragment_map.mapControl.Map.Navigator.RotationLock = false;
                    Fragment_map.map.Navigator.CenterOn(sphericalMercatorCoordinate);
                    if (Preferences.Get("TrackLocation", false))
                    {
                        Fragment_map.mapControl.Map.Navigator.PanLock = true;
                        Fragment_map.mapControl.Map.Navigator.RotationLock = true;
                    }
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
                IsMapInfoLayer = false,
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

            Color marker = Color.Blue;
            if (Preferences.Get("RecordingTrack", PrefsFragment.RecordingTrack))
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

        //Set loction beacon color, red if recording, blue if not
        public static void UpdateLocationFeature()
        {
            ILayer? layer = Fragment_map.map.Layers.FindLayer(LocationLayerName).FirstOrDefault();
            if (layer == null)
            {
                Serilog.Log.Debug($"No layer?");
                return;
            }

            var feature = layer.GetFeatures(layer.Extent, 99).FirstOrDefault();
            if (feature == null)
            {
                Serilog.Log.Debug($"No features?");
                return;
            }

            //Recording or "normal"
            Color marker = Color.Blue;
            if (Preferences.Get("RecordingTrack", PrefsFragment.RecordingTrack))
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
