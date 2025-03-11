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
using hajk.GPX;
using Org.Apache.Http.Conn.Routing;

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
                Serilog.Log.Fatal(ex, $"DisplayMapItems - AddPOIToMap");
            }
        }

        public static void AddRouteToMap(string? mapRoute, GPXType? gpxtype, bool UpdateMenu, string? name)
        {
            try
            {
                /**///Should use more than just Name. Can we use database ID and Name? If both matches, don't add new memorylayer?
                var AlreadyExists = Fragment_map.map.Layers.Where(x => x.Name == name).FirstOrDefault();
                if (AlreadyExists != null)
                {
                    Serilog.Log.Information($"Already Added 'mapRoute' '{name}' to Map");
                    return;
                }

                //Add layer
                ILayer? lineStringLayer;
                if (gpxtype == GPXType.Route)
                {
                    lineStringLayer = CreateRouteandTrackLayer(mapRoute, Mapsui.Styles.Color.Blue, CreateStyle("Blue"));
                    if (lineStringLayer != null)
                    {
                        lineStringLayer.Tag = Fragment_Preferences.Layer_Route;
                    }
                }
                else
                {
                    lineStringLayer = CreateRouteandTrackLayer(mapRoute, Mapsui.Styles.Color.Red, CreateStyle("Red"));
                    if (lineStringLayer != null)
                    {
                        lineStringLayer.Tag = Fragment_Preferences.Layer_Track;
                    }
                }

                if (lineStringLayer == null)
                {
                    return;
                }

                if (name != null)
                {
                    lineStringLayer.Name = name;
                }

                lineStringLayer.IsMapInfoLayer = true;
                lineStringLayer.Enabled = true;
                Fragment_map.map.Layers.Add(lineStringLayer);
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"DisplayMapItems - AddRouteToMap()");
            }

            //Enable menu
            try
            {
                if (UpdateMenu)
                {
                    AndroidX.AppCompat.Widget.Toolbar? toolbar = Platform.CurrentActivity.FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.toolbar);
                    toolbar?.Menu?.FindItem(Resource.Id.action_clearmap)?.SetEnabled(true);
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
                    GpxClass gpx = GPXOptimize.Optimize(GpxClass.FromXml(track.GPX));

                    //Reduce the number of waypoints before drawing on map
                    //gpx = GPX.GPXOptimize.Optimize(gpx);
                    
                    if (track.GPXType == GPXType.Track)
                    {
                        gpx.Routes.Add(gpx.Tracks[0].ToRoutes()[0]);
                    }
                    string? mapRouteTrack = Import.ParseGPXtoRoute(gpx.Routes[0]).Item1;
                    if (mapRouteTrack != null)
                    {
                        //Menus etc not yet created as app not fully initialized. Dirty workaround
                        AddRouteToMap(mapRouteTrack, GPXType.Track, false, gpx.Metadata.name);
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"DisplayMapItems - AddTracksToMap()");
            }
        }

        public static string? CreateThumbnail(GPXType? gpxtype, GpxClass? gpx)
        {
            try
            {
                if (gpx == null)
                {
                    return null;
                }

                //Save list of enabled layers (excluding tile layer)
                List<ILayer> enabledLayers = [];
                for (int i = 0; i < Fragment_map.map.Layers.Count; i++)
                {
                    if (Fragment_map.map.Layers[i].Enabled && Fragment_map.map.Layers[i].Name != Fragment_Preferences.TileLayerName)
                    {
                        Fragment_map.map.Layers[i].Enabled = false;
                        enabledLayers.Add(Fragment_map.map.Layers[i]);
                    }
                }

                //Add layer for new route
                ILayer? lineStringLayer;
                if (gpxtype == GPXType.Route)
                {
                    string mapRoute = Import.ParseGPXtoRoute(gpx.Routes[0]).Item1;
                    lineStringLayer = CreateRouteandTrackLayer(mapRoute, Mapsui.Styles.Color.Blue, CreateStyle("Blue"));
                }
                else if (gpxtype == GPXType.Track)
                {
                    string mapRoute = Import.ParseGPXtoRoute(gpx.Tracks[0].ToRoutes()[0]).Item1;
                    lineStringLayer = CreateRouteandTrackLayer(mapRoute, Mapsui.Styles.Color.Red, CreateStyle("Red"));
                }
                else
                {
                    Serilog.Log.Fatal("Unsupported gpxtype");
                    return null;
                }

                if (lineStringLayer == null)
                {
                    return null;
                }

                //Show only route/track on map tiles, create thumbprint, and remove the route/track again
                lineStringLayer.Enabled = true;
                Fragment_map.map.Layers.Add(lineStringLayer);
                string? ImageBase64String = Import.CreateThumbprintMap(gpx);
                Fragment_map.map.Layers.Remove(lineStringLayer);

                //Re-enable the layers
                foreach (ILayer layer in enabledLayers)
                {
                    layer.Enabled = true;
                }

                return ImageBase64String;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"DisplayMapItems - AddRouteToMap()");
            }

            return null;
        }

        private static ILayer? CreateRouteandTrackLayer(string? strRoute, Mapsui.Styles.Color sColor, IStyle? style = null)
        {

            if (strRoute == null)
            {
                return null;
            }

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
                    GPXUtils.Position p1 = new(GPSlineString.Coordinates[i].X, GPSlineString.Coordinates[i].Y, 0, false, null);
                    GPXUtils.Position p2 = new(GPSlineString.Coordinates[i + 1].X, GPSlineString.Coordinates[i + 1].Y, 0, false, null);

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
                Serilog.Log.Fatal(ex, $"DisplayMapItems - CreateRouteAndTrackLayer()");
            }

            return new MemoryLayer
            {
                Features = features,
                Style = style
            };
        }

        public static IStyle? CreateStyle(string? colour)
        {
            if (colour == null)
            {
                Serilog.Log.Fatal("Do not call CreateStyle without a 'colour'");
                return null;
            }

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
                    MaxVisible = 30.0f,
                    MinVisible = 0.0f,
                });

                features.Add(feature);
            }

            return features;
        }
    }
}
