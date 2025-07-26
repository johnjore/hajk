using GPXUtils;
using hajk.Data;
using hajk.Fragments;
using hajk.GPX;
using hajk.Models;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Nts.Extensions;
using Mapsui.Projections;
using Mapsui.Styles;
using NetTopologySuite.Geometries;
using SharpGPX;

namespace hajk
{
    /// <summary>
    /// Static utility class for displaying POIs, routes, and tracks on the map.
    /// </summary>
    internal class DisplayMapItems
    {
        /// <summary>
        /// Adds POIs stored in the database to the map as a MemoryLayer.
        /// </summary>
        public static void AddPOIToMap()
        {
            try
            {
                List<GPXDataPOI> POIs = POIDatabase.GetPOIAsync().Result;
                if (POIs == null || POIs.Count == 0) return;

                var poiLayer = Fragment_map.map.Layers.FindLayer(Fragment_Preferences.Layer_Poi).FirstOrDefault();
                if (poiLayer != null)
                    Fragment_map.map.Layers.Remove(poiLayer);

                var POILayer = new MemoryLayer
                {
                    Name = Fragment_Preferences.Layer_Poi,
                    Tag = Fragment_Preferences.Layer_Poi,
                    Enabled = true,
                    IsMapInfoLayer = true,
                    Features = ConvertListToFeatures(POIs),
                    Style = new SymbolStyle { Enabled = false },
                };

                foreach (ILayer layer in Fragment_map.map.Layers)
                    while (layer.Busy) Thread.Sleep(1);

                Fragment_map.map.Layers.Add(POILayer);
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "DisplayMapItems - AddPOIToMap");
            }
        }

        /// <summary>
        /// Adds a GPX route or track to the map, optionally updating UI and zooming.
        /// </summary>
        public static void AddRouteTrackToMap(GPXDataRouteTrack routetrack, bool UpdateMenu, string? name, bool ZoomAndCenter)
        {
            try
            {
                GpxClass? gpx = null;
                var alreadyExists = Fragment_map.map.Layers.FirstOrDefault(x => x.Name == name);

                if (alreadyExists == null)
                {
                    gpx = GPXOptimize.Optimize(GpxClass.FromXml(routetrack.GPX));
                    Coordinate[]? coords = null;
                    MemoryLayer? lineStringLayer = null;

                    if (routetrack.GPXType == GPXType.Track)
                    {
                        coords = gpx?.Tracks?.FirstOrDefault()?.trkseg?.FirstOrDefault()?.trkpt
                            .Select(p => new Coordinate(SphericalMercator.FromLonLat((double)p.lon, (double)p.lat).ToCoordinate())).ToArray();
                        lineStringLayer = CreateRouteandTrackLayer(coords, Mapsui.Styles.Color.Red, CreateStyle("Red"));
                    }
                    else if (routetrack.GPXType == GPXType.Route)
                    {
                        coords = gpx?.Routes?.FirstOrDefault()?.rtept
                            .Select(p => new Coordinate(SphericalMercator.FromLonLat((double)p.lon, (double)p.lat).ToCoordinate())).ToArray();
                        lineStringLayer = CreateRouteandTrackLayer(coords, Mapsui.Styles.Color.Blue, CreateStyle("Blue"));
                    }
                    else
                    {
                        Serilog.Log.Fatal($"GPXType not supported: {routetrack.GPXType}");
                        return;
                    }

                    if (lineStringLayer == null) return;

                    lineStringLayer.IsMapInfoLayer = true;
                    lineStringLayer.Enabled = true;
                    lineStringLayer.Tag = routetrack.GPXType == GPXType.Route ? Fragment_Preferences.Layer_Route : Fragment_Preferences.Layer_Track;
                    lineStringLayer.Name = name ?? string.Empty;
                    Fragment_map.map.Layers.Add(lineStringLayer);
                }

                if (ZoomAndCenter && gpx != null)
                {
                    var bounds = gpx.GetBounds();
                    if (bounds?.maxlon != null && bounds?.minlat != null && bounds?.minlon != null && bounds?.maxlat != null)
                    {
                        (double x1, double y1) = SphericalMercator.FromLonLat((double)bounds.maxlon, (double)bounds.minlat);
                        (double x2, double y2) = SphericalMercator.FromLonLat((double)bounds.minlon, (double)bounds.maxlat);
                        Fragment_map.mapControl?.Map.Navigator.ZoomToBox(new MRect(x1, y1, x2, y2), MBoxFit.Fit);
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "DisplayMapItems - AddRouteToMap()");
            }

            try
            {
                if (UpdateMenu && Platform.CurrentActivity != null)
                {
                    var toolbar = Platform.CurrentActivity.FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.toolbar);
                    toolbar?.Menu?.FindItem(Resource.Id.action_clearmap)?.SetEnabled(true);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "DisplayMapItems - AddRouteToMap() - Menu Update");
            }
        }

        /// <summary>
        /// Loads and displays all saved tracks on the map.
        /// </summary>
        public static void AddAllTracksToMap()
        {
            try
            {
                var tracks = RouteDatabase.GetTracksAsync()?.Result;
                if (tracks == null || tracks.Count == 0) return;

                foreach (var track in tracks)
                {
                    string? layerName = track.Name + "|" + track.Id;
                    AddRouteTrackToMap(track, false, layerName, false);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "DisplayMapItems - AddTracksToMap()");
            }
        }

        /// <summary>
        /// Creates a map thumbnail for a given route or track.
        /// </summary>
        public static string? CreateThumbnail(GPXType? gpxtype, GpxClass? gpx)
        {
            try
            {
                if (gpx == null) return null;

                List<ILayer> enabledLayers = [];
                foreach (var layer in Fragment_map.map.Layers)
                {
                    if (layer.Enabled && layer.Name != Fragment_Preferences.TileLayerName)
                    {
                        layer.Enabled = false;
                        enabledLayers.Add(layer);
                    }
                }

                var gpxOptimized = GPXOptimize.Optimize(gpx);
                Coordinate[]? coords = null;
                MemoryLayer? lineStringLayer = null;

                if (gpxtype == GPXType.Track)
                {
                    coords = gpxOptimized?.Tracks?.FirstOrDefault()?.trkseg?.FirstOrDefault()?.trkpt
                        .Select(p => new Coordinate(SphericalMercator.FromLonLat((double)p.lon, (double)p.lat).ToCoordinate())).ToArray();
                    lineStringLayer = CreateRouteandTrackLayer(coords, Mapsui.Styles.Color.Red, CreateStyle("Red"));
                }
                else if (gpxtype == GPXType.Route)
                {
                    coords = gpxOptimized?.Routes?.FirstOrDefault()?.rtept
                        .Select(p => new Coordinate(SphericalMercator.FromLonLat((double)p.lon, (double)p.lat).ToCoordinate())).ToArray();
                    lineStringLayer = CreateRouteandTrackLayer(coords, Mapsui.Styles.Color.Blue, CreateStyle("Blue"));
                }
                else
                {
                    Serilog.Log.Fatal($"GPXType not supported: {gpxtype}");
                    return null;
                }

                if (lineStringLayer == null) return null;

                lineStringLayer.Enabled = true;
                Fragment_map.map.Layers.Add(lineStringLayer);
                string? imageBase64 = Import.CreateThumbprintMap(gpx);
                Fragment_map.map.Layers.Remove(lineStringLayer);

                foreach (var layer in enabledLayers)
                    layer.Enabled = true;

                return imageBase64;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "DisplayMapItems - CreateThumbnail");
                return null;
            }
        }
               
        private static MemoryLayer? CreateRouteandTrackLayer(Coordinate[]? coords, Mapsui.Styles.Color sColor, IStyle? style = null)
        {
            if (coords == null || coords.Length < 1)
            {
                return null;
            }

            var features = new List<IFeature>();

            try
            {
                //Lines between each waypoint
                var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
                LineString? lineString = geometryFactory.CreateLineString(coords);

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
                for (int i = 0; i < lineString.NumPoints - 1; i++)
                {
                    //End points for line
                    GPXUtils.Position p1 = new(lineString.Coordinates[i].X, lineString.Coordinates[i].Y, 0, false, null);
                    GPXUtils.Position p2 = new(lineString.Coordinates[i + 1].X, lineString.Coordinates[i + 1].Y, 0, false, null);

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
                Line = new Pen { 
                    Color = Mapsui.Styles.Color.FromString(colour), 
                    Width = 4,
                    PenStyle = PenStyle.Solid
                }
            };
        }

        private static List<IFeature> ConvertListToFeatures(List<GPXDataPOI>? POIs)
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
                string icon = "hajk.Images.Black-dot.png";
                switch (POI.Symbol)
                {
                    case "Drinking Water":
                        icon = "hajk.Images.Drinking-water.png";
                        break;
                    case "Campground":
                        icon = "hajk.Images.Tent.png";
                        break;
                    case "Rogaining":
                        icon = "hajk.Images.RoundFlag.png";
                        break;
                    case "Man Overboard":
                        icon = "hajk.Images.RoundFlag.png";
                        break;
                    case "Lodge":
                        icon = "hajk.Images.Cabin.png";
                        break;
                    case "Flag, Blue":
                        icon = "hajk.Images.FlagBlue.png";
                        break;
                    case "Waypoint":
                        icon = "hajk.Images.FlagCheck.png";
                        break;
                    case "Flag":
                        icon = "hajk.Images.Flag.png";
                        break;
                    case "Dog Unknown":
                        icon = "hajk.Images.Dog.png";
                        break;
                    case "Parking Area":
                        icon = "hajk.Images.Parking.png";
                        break;
                    case "Picnic Area":
                        icon = "hajk.Images.PicnicArea.png";
                        break;
                    case "Car":
                        icon = "hajk.Images.Car.png";
                        break;
                }

                //Style
                feature.Styles.Add(new SymbolStyle
                {
                    SymbolScale = 1.0f,
                    RotateWithMap = true,
                    BitmapId = Utils.Misc.GetBitmapIdForEmbeddedResource(icon),
                    MaxVisible = 30.0f,
                    MinVisible = 0.0f,
                });

                features.Add(feature);
            }

            return features;
        }
    }
}
