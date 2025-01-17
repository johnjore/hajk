using Google.Android.Material.FloatingActionButton;
using hajk.Fragments;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts.Extensions;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;


namespace hajk
{
    class Location
    {
        private static readonly string LocationLayerName = "Location";

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

                FloatingActionButton? fabCompass = Platform.CurrentActivity?.FindViewById<FloatingActionButton>(Resource.Id.fabCompass);
                if (fabCompass != null)
                {
                    var rotationAngle = (float)CompassData.GetRotationAngle();
                    fabCompass.Rotation = rotationAngle;
                    //Serilog.Log.Debug($"RotationAngle: {rotationAngle}");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Location - UpdateLocationMarker()");
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

            Mapsui.Styles.Color marker = Mapsui.Styles.Color.Blue;
            if (Preferences.Get("RecordingTrack", Fragment_Preferences.RecordingTrack))
            {
                marker = Mapsui.Styles.Color.Red;
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
