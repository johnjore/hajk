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

                //Location circle. Clear all existing feature(s( and create new
                var layer = (WritableLayer)Fragment_map.map.Layers.FindLayer(LocationLayerName).FirstOrDefault();
                if (layer == null)
                {
                    Fragment_map.map.Layers.Add(CreateLocationLayer(sphericalMercatorCoordinate));
                }
                else
                {
                    layer.Clear();
                    layer.Add(CreateLocationMarker(sphericalMercatorCoordinate));
                    layer?.DataHasChanged();
                }

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

        public static WritableLayer CreateLocationLayer(MPoint GPSLocation)
        {
            var layer = new WritableLayer
            {
                Name = LocationLayerName,
                Style = null,
                IsMapInfoLayer = false,
            };

            layer.Add(CreateLocationMarker(GPSLocation));

            return layer;
        }

        private static PointFeature CreateLocationMarker(MPoint GPSLocation)
        {
            var feature = new PointFeature(GPSLocation);

            Color marker = Color.Blue;
            if (Preferences.Get("RecordingTrack", Fragment_Preferences.RecordingTrack))
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
