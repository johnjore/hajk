using GPXUtils;
using hajk.Data;
using hajk.Fragments;
using hajk.Models;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts.Extensions;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using SharpGPX;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace hajk
{
    internal class DisplayMapItems
    {
        public static void AddPOIToMap()
        {
            try
            {
                List<GPXDataPOI> POIs = POIDatabase.GetPOIAsync().Result;

                if (POIs == null || POIs.Count == 0)
                {
                    return;
                }

                var poiLayer = Fragment_map.map.Layers.FindLayer(Fragment_Preferences.Layer_Poi).FirstOrDefault();
                if (poiLayer != null)
                {
                    Fragment_map.map.Layers.Remove(poiLayer);
                }

                //Add layer
                var POILayer = new MemoryLayer
                {
                    Name = Fragment_Preferences.Layer_Poi,
                    Tag = Fragment_Preferences.Layer_Poi,
                    Enabled = true,
                    IsMapInfoLayer = true,
                    Features = ConvertListToInumerable(POIs),
                    Style = new SymbolStyle { Enabled = false },
                };

                //Wait for each layer to complete
                foreach (ILayer layer in Fragment_map.map.Layers)
                {
                    while (layer.Busy)
                    {
                        Thread.Sleep(1);
                    }
                }

                Fragment_map.map.Layers.Add(POILayer);
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"MapItems - AddPOIToMap");
            }
        }

        public static void AddRouteToMap(string mapRoute, GPXType gpxtype, bool UpdateMenu)
        {
            try
            {
                //Add layer
                ILayer lineStringLayer;
                if (gpxtype == GPXType.Route)
                {
                    lineStringLayer = CreateRouteandTrackLayer(mapRoute, Mapsui.Styles.Color.Blue, CreateStyle("Blue"));
                    lineStringLayer.Tag = Fragment_Preferences.Layer_Route;
                }
                else
                {
                    lineStringLayer = CreateRouteandTrackLayer(mapRoute, Mapsui.Styles.Color.Red, CreateStyle("Red"));
                    lineStringLayer.Tag = Fragment_Preferences.Layer_Track;
                }
                lineStringLayer.IsMapInfoLayer = true;
                lineStringLayer.Enabled = true;
                Fragment_map.map.Layers.Add(lineStringLayer);
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"MapItems - AddRouteToMap()");
            }

            //Enable menu
            try
            {
                if (UpdateMenu)
                {
                    AndroidX.AppCompat.Widget.Toolbar toolbar = Platform.CurrentActivity.FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.toolbar);
                    toolbar.Menu.FindItem(Resource.Id.action_clearmap).SetEnabled(true);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Import - AddRouteToMap()");
            }
        }

        public static void AddTracksToMap()
        {
            var Tracks = RouteDatabase.GetTracksAsync().Result;

            if (Tracks == null)
                return;

            if (Tracks.Count == 0)
                return;

            try
            {
                foreach (var track in Tracks)
                {
                    GpxClass gpx = GpxClass.FromXml(track.GPX);

                    if (track.GPXType == GPXType.Track)
                    {
                        gpx.Routes.Add(gpx.Tracks[0].ToRoutes()[0]);
                    }
                    string? mapRouteTrack = Import.ParseGPXtoRoute(gpx.Routes[0]).Item1;

                    //Menus etc not yet created as app not fully initialized. Dirty workaround
                    AddRouteToMap(mapRouteTrack, GPXType.Track, false);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Import - AddTracksToMap()");
            }
        }

        private static ILayer CreateRouteandTrackLayer(string strRoute, Mapsui.Styles.Color sColor, IStyle? style = null)
        {
            var features = new List<IFeature>();

            try
            {
                var GPSlineString = (LineString)new WKTReader().Read(strRoute);

                //Lines between each waypoint
                var lineString = new LineString(GPSlineString.Coordinates.Select(v => SphericalMercator.FromLonLat(v.Y, v.X).ToCoordinate()).ToArray());
                features.Add(new GeometryFeature { Geometry = lineString });

                //End of route
                var FeatureEnd = new GeometryFeature { Geometry = lineString.EndPoint };
                FeatureEnd.Styles.Add(new SymbolStyle
                {
                    SymbolScale = 1.5f,
                    MaxVisible = 2.0f,
                    MinVisible = 0.0f,
                    RotateWithMap = true,
                    SymbolType = SymbolType.Ellipse,
                    Fill = new Mapsui.Styles.Brush { FillStyle = FillStyle.Cross, Color = Mapsui.Styles.Color.Red, Background = Mapsui.Styles.Color.Transparent },
                    Outline = new Pen { Color = Mapsui.Styles.Color.Red, Width = 1.5f }
                });
                features.Add(FeatureEnd);

                //Start of route
                var FeatureStart = new GeometryFeature { Geometry = lineString.StartPoint };
                FeatureStart.Styles.Add(new SymbolStyle
                {
                    SymbolScale = 1.5f,
                    MaxVisible = 2.0f,
                    MinVisible = 0.0f,
                    RotateWithMap = true,
                    SymbolType = SymbolType.Ellipse,
                    Fill = new Mapsui.Styles.Brush { FillStyle = FillStyle.Cross, Color = Mapsui.Styles.Color.Green, Background = Mapsui.Styles.Color.Transparent },
                    Outline = new Pen { Color = Mapsui.Styles.Color.Green, Width = 1.5f }
                });
                features.Add(FeatureStart);

                //Add arrow halfway between waypoints and highlight routing points
                var bitmapId = Utils.Misc.GetBitmapIdForEmbeddedResource("hajk.Images.Arrow-up.png");
                for (int i = 0; i < GPSlineString.NumPoints - 1; i++)
                {
                    //End points for line
                    GPXUtils.Position p1 = new(GPSlineString.Coordinates[i].X, GPSlineString.Coordinates[i].Y, 0, null);
                    GPXUtils.Position p2 = new(GPSlineString.Coordinates[i + 1].X, GPSlineString.Coordinates[i + 1].Y, 0, null);

                    //Quarter point on line for arrow
                    MPoint p_quarter = Utils.Misc.CalculateQuarter(lineString.Coordinates[i].Y, lineString.Coordinates[i].X, lineString.Coordinates[i + 1].Y, lineString.Coordinates[i + 1].X);

                    //Bearing of arrow
                    var p = new PositionHandler();
                    var angle = p.CalculateBearing(p1, p2);

                    var FeatureArrow = new PointFeature(new MPoint(p_quarter));
                    FeatureArrow.Styles.Add(new SymbolStyle
                    {
                        BitmapId = bitmapId,
                        SymbolScale = 0.5f,
                        MaxVisible = 2.0f,
                        MinVisible = 0.0f,
                        RotateWithMap = true,
                        SymbolRotation = angle,
                        SymbolOffset = new Offset(0, 0),
                        SymbolType = SymbolType.Triangle,
                        Fill = new Mapsui.Styles.Brush { FillStyle = FillStyle.Solid, Color = sColor, Background = sColor },
                        Outline = new Pen { Color = sColor, Width = 1.5f },
                    });
                    features.Add(FeatureArrow);

                    //Waypoints
                    var FeatureWaypoint = new GeometryFeature { Geometry = lineString.Coordinates[i].ToPoint() };
                    FeatureWaypoint.Styles.Add(new SymbolStyle
                    {
                        SymbolScale = 0.7f,
                        MaxVisible = 1.5f,
                        MinVisible = 0.0f,
                        RotateWithMap = true,
                        SymbolRotation = 0,
                        SymbolType = SymbolType.Ellipse,
                        Fill = new Mapsui.Styles.Brush { FillStyle = FillStyle.Hollow, Color = Mapsui.Styles.Color.Transparent, Background = Mapsui.Styles.Color.Transparent },
                        Outline = new Pen { Color = sColor, Width = 1.5f },
                    });
                    features.Add(FeatureWaypoint);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"MapItems - CreateRouteAndTrackLayer()");
            }

            return new MemoryLayer
            {
                Features = features,
                Name = Fragment_Preferences.Layer_Route,
                Style = style
            };
        }

        public static IStyle CreateStyle(string? colour)
        {
            return new VectorStyle
            {
                Fill = null,
                Outline = null,
                Line = { Color = Mapsui.Styles.Color.FromString(colour), Width = 4, PenStyle = PenStyle.Solid }
            };
        }

        private static IEnumerable<IFeature> ConvertListToInumerable(List<GPXDataPOI>? POIs)
        {
            if (POIs == null || POIs.Count == 0)
            {
                return [];
            }

            var features = new List<IFeature>();

            foreach (GPXDataPOI POI in POIs)
            {
                var mpoint = SphericalMercator.FromLonLat((double)POI.Lon, (double)POI.Lat).ToMPoint();
                var feature = new PointFeature(mpoint);
                feature["name"] = POI.Name;
                feature["description"] = POI.Description;
                feature["id"] = POI.Id;

                //Icon
                string svg = "hajk.Images.Black-dot.png";
                switch (POI.Symbol)
                {
                    case "Drinking Water":
                        svg = "hajk.Images.Drinking-water.png";
                        break;
                    case "Campground":
                        svg = "hajk.Images.Tent.png";
                        break;
                    case "Rogaining":
                        svg = "hajk.Images.RoundFlag.png";
                        break;
                    case "Man Overboard":
                        svg = "hajk.Images.RoundFlag.png";
                        break;
                }

                //Style
                feature.Styles.Add(new SymbolStyle
                {
                    SymbolScale = 1.0f,
                    RotateWithMap = true,
                    BitmapId = Utils.Misc.GetBitmapIdForEmbeddedResource(svg),
                });

                features.Add(feature);
            }

            return features;
        }
    }
}
