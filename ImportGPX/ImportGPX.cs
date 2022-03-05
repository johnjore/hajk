using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Mapsui.Geometries;
using Mapsui.Layers;
using Mapsui.Projection;
using Mapsui.Providers;
using Mapsui.Styles;
using Xamarin.Essentials;
using SharpGPX;
using hajk.Data;
using hajk.Models;
using hajk.Fragments;
using Serilog;
using SharpGPX.GPX1_1;
using SharpGPX.GPX1_1.Garmin;
using SharpGPX.GPX1_1.Topografix;
using GPXUtils;
using Mapsui.Rendering.Skia;
using Mapsui.Utilities;
using Mapsui;

namespace hajk
{
    public class Import
    {
        public static ILayer GetRoute()
        {
            var strRoute = string.Empty;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                bool DownloadOfflineMap = false;
                GpxClass gpxData = await PickAndParse();

                if (gpxData == null)
                    return;

                //Does the user want maps downloaded for offline usage?
                Show_Dialog msg1 = new Show_Dialog(MainActivity.mContext);
                if (await msg1.ShowDialog($"Offline Map", $"Download map for offline usage?", Android.Resource.Attribute.DialogIcon, true, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) == Show_Dialog.MessageResult.YES)
                {
                    DownloadOfflineMap = true;
                }

                foreach (rteType route in gpxData.Routes)
                {
                    AddGPXRoute(route, DownloadOfflineMap);
                }

                foreach (trkType track in gpxData.Tracks)
                {
                    AddGPXTrack(track, DownloadOfflineMap);
                }

                Log.Information($"Done importing gpx file");
            });

            return null;
        }

        public static void AddGPXTrack(trkType track, bool DownloadOfflineMap)
        {
            //Get Track and distance from GPX
            var t = GPXtoRoute(track.ToRoutes()[0]);
            string mapTrack = t.Item1;
            float mapDistanceKm = t.Item2;

            //Clear existing GPX routes from map, else they will be included
            Utils.Misc.ClearTrackRoutesFromMap();

            //Add to map
            AddRouteToMap(mapTrack, GPXType.Track);

            //Create a standalone GPX
            var newGPX = new GpxClass()
            {
                Metadata = new metadataType()
                {
                    name = track.name,
                    desc = track.desc,
                },
            };
            newGPX.Tracks.Add(track);

            //Create thumbsize map
            string ImageBase64String = CreateThumbprintMap(newGPX);

            //Add to routetrack DB
            GPXDataRouteTrack r = new GPXDataRouteTrack
            {
                GPXType = GPXType.Track,
                Name = track.name,
                Distance = mapDistanceKm,
                Ascent = 0, /**///Fix this
                Descent = 0, /**///Fix this
                Description = track.desc,
                GPX = newGPX.ToXml(),
                ImageBase64String = ImageBase64String,
            };
            RouteDatabase.SaveRoute(r);

            //Update RecycleView with new entry
            _ = Fragment_gpx.mAdapter.mGpxData.Insert(r);
            Fragment_gpx.mAdapter.NotifyDataSetChanged();

            //Does the user want the maps downloaded?
            if (DownloadOfflineMap)
            {
                GetloadOfflineMap(track.GetBounds(), r.Id, null);
            }
        }

        public static void AddGPXRoute(rteType route, bool DownloadOfflineMap)
        {
            //Get Route and distance from GPX
            var t = GPXtoRoute(route);
            string mapRoute = t.Item1;
            float mapDistanceKm = t.Item2;

            //Clear existing GPX routes from map, else they will be included
            Utils.Misc.ClearTrackRoutesFromMap();

            //Add to map
            AddRouteToMap(mapRoute, GPXType.Route);

            //Create a standalone GPX
            var newGPX = new GpxClass()
            {
                Metadata = new metadataType()
                {
                    name = route.name,
                    desc = route.desc,
                },
            };
            newGPX.Routes.Add(route);

            //Create thumbsize map
            string ImageBase64String = CreateThumbprintMap(newGPX);

            //Add to routetrack DB
            GPXDataRouteTrack r = new GPXDataRouteTrack
            {
                GPXType = GPXType.Route,
                Name = route.name,
                Distance = mapDistanceKm,
                Ascent = 0, /**///Fix this
                Descent = 0, /**///Fix this
                Description = route.desc,
                GPX = newGPX.ToXml(),
                ImageBase64String = ImageBase64String,
            };
            RouteDatabase.SaveRoute(r);

            //Update RecycleView with new entry
            _ = Fragment_gpx.mAdapter.mGpxData.Insert(r);
            Fragment_gpx.mAdapter.NotifyDataSetChanged();

            //Does the user want the maps downloaded?
            if (DownloadOfflineMap)
            {
                GetloadOfflineMap(route.GetBounds(), r.Id, null);
            }
        }

        public static string CreateThumbprintMap(GpxClass newGPX)
        {
            //Overlay GPX on map and zoom
            var bounds = newGPX.GetBounds();
            var min = SphericalMercator.FromLonLat((double)bounds.maxlon, (double)bounds.minlat);
            var max = SphericalMercator.FromLonLat((double)bounds.minlon, (double)bounds.maxlat);

            //Set location to match route/track
            Fragment_map.mapControl.Navigator.RotateTo(0.0);
            Fragment_map.mapControl.Navigator.NavigateTo(new BoundingBox(min, max), ScaleMethod.Fit);

            //Create viewport to match GPX list
            var viewport = new Viewport(Fragment_map.mapControl.Viewport)
            {
                Width = MainActivity.wTrackRouteMap
            };

            //Set loction to match route/ track (again). Why?
            var navigator = new Navigator(Fragment_map.map, viewport);
            navigator.RotateTo(0, 0);
            navigator.NavigateTo(new BoundingBox(min, max), ScaleMethod.Fit);

            //Wait for each layer to complete
            foreach (ILayer layer in Fragment_map.map.Layers)
            {
                while (layer.Busy)
                {
                    System.Threading.Thread.Sleep(10);
                }
            }

            //Create the thumbprint
            MemoryStream bitmap = new MapRenderer().RenderToBitmapStream(viewport, Fragment_map.map.Layers, Fragment_map.map.BackColor);
            bitmap.Position = 0;
            string ImageBase64String = Convert.ToBase64String(bitmap.ToArray());

            return ImageBase64String;
        }

        public static async void GetloadOfflineMap(boundsType bounds, int id, string strFilePath)
        {
            Models.Map map = new Models.Map
            {
                Id = id,
                ZoomMin = PrefsActivity.MinZoom,
                ZoomMax = PrefsActivity.MaxZoom,
                BoundsLeft = (double)bounds.minlat,
                BoundsBottom = (double)bounds.maxlon,
                BoundsRight = (double)bounds.maxlat,
                BoundsTop = (double)bounds.minlon
            };

            //Get all missing tiles
            await DownloadRasterImageMap.DownloadMap(map);

            //Also exporting?
            if (strFilePath != null)
            {
                DownloadRasterImageMap.ExportMapTiles(id, strFilePath);
            }
            
            //Refresh with new map
            DownloadRasterImageMap.LoadOSMLayer();
            Log.Information($"Done downloading map for {map.Id}");            
        }

        public static void AddRouteToMap(string mapRoute, GPXType gpxtype)
        {
            //Add layer
            ILayer lineStringLayer;
            if (gpxtype == GPXType.Route)
            {
                lineStringLayer = CreateRouteLayer(mapRoute, CreateRouteStyle());
                lineStringLayer.Tag = "route";
            }
            else
            {
                lineStringLayer = CreateRouteLayer(mapRoute, CreateTrackStyle());
                lineStringLayer.Tag = "track";
            }
            lineStringLayer.IsMapInfoLayer = true;
            lineStringLayer.Enabled = true;
            Fragment_map.map.Layers.Add(lineStringLayer);

            //Enable menu
            AndroidX.AppCompat.Widget.Toolbar toolbar = MainActivity.mContext.FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.toolbar);
            toolbar.Menu.FindItem(Resource.Id.action_clearmap).SetEnabled(true);
        }

        public static (string, float) GPXtoRoute(rteType route)
        {
            try
            {
                string mapRoute = "LINESTRING(";
                float mapDistanceKm = 0.0f;
                var p = new PositionHandler();
                var p2 = new Position(0, 0);
                for (int i = 0; i < route.rtept.Count; i++)
                {
                    //WayPoint
                    if (!(mapRoute.Equals("LINESTRING(")))
                    {
                        mapRoute += ",";
                    }
                    mapRoute += route.rtept[i].lat.ToString() + " " + route.rtept[i].lon.ToString();

                    var rtePteExt = route.rtept[i].GetExt<RoutePointExtension>();
                    if (rtePteExt != null)
                    {
                        Log.Debug("Route '{0}' has Garmin extension", route.name);

                        for (int j = 0; j < rtePteExt.rpt.Count(); j++)
                        {
                            mapRoute += "," + rtePteExt.rpt[j].lat.ToString() + " " + rtePteExt.rpt[j].lon.ToString();

                            //Previous leg
                            if (j == 0 && p2.Latitude != 0 && p2.Longitude != 0)
                            {
                                var p1 = new Position((float)rtePteExt.rpt[j].lat, (float)rtePteExt.rpt[j].lon);
                                mapDistanceKm += (float)p.CalculateDistance(p1, p2, DistanceType.Kilometers);
                            }

                            //First leg
                            if (j == 0)
                            {
                                var p1 = new Position((float)route.rtept[i].lat, (float)route.rtept[i].lon);
                                p2 = new Position((float)rtePteExt.rpt[j].lat, (float)rtePteExt.rpt[j].lon);
                                mapDistanceKm += (float)p.CalculateDistance(p1, p2, DistanceType.Kilometers);
                            }

                            //All other legs
                            if (j >= 1)
                            {
                                var p1 = new Position((float)rtePteExt.rpt[j - 1].lat, (float)rtePteExt.rpt[j - 1].lon);
                                p2 = new Position((float)rtePteExt.rpt[j].lat, (float)rtePteExt.rpt[j].lon);
                                mapDistanceKm += (float)p.CalculateDistance(p1, p2, DistanceType.Kilometers);
                            }
                        }
                    }

                    if (rtePteExt == null)
                    {
                        //Previous leg
                        if (i >= 1)
                        {
                            var p1 = new Position((float)route.rtept[i - 1].lat, (float)route.rtept[i - 1].lon);
                            p2 = new Position((float)route.rtept[i].lat, (float)route.rtept[i].lon);
                            mapDistanceKm += (float)p.CalculateDistance(p1, p2, DistanceType.Kilometers);
                        }
                    }


                    /**///Calculate ascent / descent data
                }
                mapRoute += ")";

                return (mapRoute, mapDistanceKm);
            }
            catch (Exception ex)
            {
                Log.Error($"Crashed while parsing gpx: {ex}");
                return (null, 0);
            }          
        }

        private static async Task<GpxClass> PickAndParse()
        {
            try
            {
                var options = new PickOptions
                {
                    PickerTitle = "Please select a GPX file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        /**///What is mime type for GPX files?!? bin
                        //{ DevicePlatform.Android, new string[] { "gpx/gpx"} },
                        { DevicePlatform.Android, null },
                    })
                };

                var result = await FilePicker.PickAsync(options);

                if (result == null)
                    return null;

                if (result.FileName.EndsWith("gpx", StringComparison.OrdinalIgnoreCase) == false)
                    return null;

                var stream = await result.OpenReadAsync();
                string contents = string.Empty;
                using (var reader = new StreamReader(stream))
                {
                    contents = reader.ReadToEnd();
                }

                GpxClass gpx = GpxClass.FromXml(contents);
                var bounds = gpx.GetBounds();                

                string r = (gpx.Routes.Count == 1) ? "route" : "routes";
                string t = (gpx.Tracks.Count == 1) ? "track" : "tracks";

                Show_Dialog msg1 = new Show_Dialog(MainActivity.mContext);
                if (await msg1.ShowDialog($"{result.FileName}", $"Found {gpx.Routes.Count} {r} and {gpx.Tracks.Count} {t}. Import?", Android.Resource.Attribute.DialogIcon, true, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) != Show_Dialog.MessageResult.YES)
                    return null;

                return gpx;
            }
            catch (Exception ex)
            {
                // The user canceled or something went wrong
                Log.Error($"Import GPX Crashed: " + ex.ToString());
            }

            return null;
        }

        public static ILayer CreateRouteLayer(string strRoute, IStyle style = null)
        {
            var features = new Features();
            var GPSlineString = (LineString)Geometry.GeomFromText(strRoute);

            //Lines between each waypoint
            var lineString = new LineString(GPSlineString.Vertices.Select(v => SphericalMercator.FromLonLat(v.Y, v.X)));
            features.Add(new Feature { Geometry = lineString });

            //End of route
            var FeatureEnd = new Feature { Geometry = lineString.EndPoint };
            FeatureEnd.Styles.Add(new SymbolStyle
            {
                SymbolScale = 1.5f,
                MaxVisible = 2.0f,
                MinVisible = 0.0f,
                RotateWithMap = true,
                SymbolType = SymbolType.Ellipse,
                Fill = new Brush { FillStyle = FillStyle.Cross, Color = Color.Red, Background = Color.Transparent },
                Outline = new Pen { Color = Color.Red, Width = 1.5f }
            });
            features.Add(FeatureEnd);

            //Start of route
            var FeatureStart = new Feature { Geometry = lineString.StartPoint };
            FeatureStart.Styles.Add(new SymbolStyle
            {
                SymbolScale = 1.5f,
                MaxVisible = 2.0f,
                MinVisible = 0.0f,
                RotateWithMap = true,
                SymbolType = SymbolType.Ellipse,
                Fill = new Brush { FillStyle = FillStyle.Cross, Color = Color.Green, Background = Color.Transparent },
                Outline = new Pen { Color = Color.Green, Width = 1.5f }
            });
            features.Add(FeatureStart);

            //Add arrow halfway between waypoints and highlight routing points
            var bitmapId = Utils.Misc.GetBitmapIdForEmbeddedResource("hajk.Images.Arrow-up.svg");
            for (int i = 0; i < GPSlineString.NumPoints - 1; i++)
            {
                //End points for line
                Position p1 = new Position(GPSlineString.Vertices[i].X, GPSlineString.Vertices[i].Y);
                Position p2 = new Position(GPSlineString.Vertices[i + 1].X, GPSlineString.Vertices[i + 1].Y);

                //Center point on line for arrow
                Point p_center = Utils.Misc.CalculateCenter(lineString.Vertices[i].Y, lineString.Vertices[i].X, lineString.Vertices[i + 1].Y, lineString.Vertices[i + 1].X);

                //Bearing of arrow
                var p = new PositionHandler();
                var angle = p.CalculateBearing(p1, p2);

                var FeatureArrow = new Feature { Geometry = p_center };
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
                });
                features.Add(FeatureArrow);

                //Waypoints
                var FeatureWaypoint = new Feature { Geometry = lineString.Vertices[i] };
                FeatureWaypoint.Styles.Add(new SymbolStyle
                {
                    SymbolScale = 0.7f,
                    MaxVisible = 2.0f,
                    MinVisible = 0.0f,
                    RotateWithMap = true,
                    SymbolRotation = 0,
                    SymbolType = SymbolType.Ellipse,
                    Fill = new Brush { FillStyle = FillStyle.Cross, Color = Color.Blue, Background = Color.Transparent },
                    Outline = new Pen { Color = Color.Blue, Width = 1.5f },
                });
                features.Add(FeatureWaypoint);
            }

            return new MemoryLayer
            {
                DataSource = new MemoryProvider(features),
                Name = "RouteLayer",
                Style = style
            };
        }

        public static ILayer CreateTrackLayer(string strTrack, IStyle style = null)
        {
            var features = new Features();

            //Convert from string and line strings
            var lineString = (LineString)Geometry.GeomFromText(strTrack);
            lineString = new LineString(lineString.Vertices.Select(v => SphericalMercator.FromLonLat(v.Y, v.X)));
            features.Add(new Feature { Geometry = lineString });

            //Waypoint markers
            foreach (var waypoint in lineString.Vertices)
            {
                var feature = new Feature { Geometry = waypoint };
                feature.Styles.Add(new SymbolStyle
                {
                    SymbolScale = 0.2f,
                    MaxVisible = 10.0f,
                    MinVisible = 0.0f,
                    RotateWithMap = true,
                    SymbolType = SymbolType.Ellipse,
                    Fill = new Brush { FillStyle = FillStyle.Dotted, Color = Color.Red, Background = Color.Transparent },
                    Outline = new Pen { Color = Color.Red, Width = 0.2f },

                });
                features.Add(feature);
            }

            return new MemoryLayer
            {
                DataSource = new MemoryProvider(features),
                Name = "TrackLayer",
                Style = style
            };
        }

        public static IStyle CreateRouteStyle()
        {
            return new VectorStyle
            {
                Fill = null,
                Outline = null,
                Line = { Color = Color.FromString("Blue"), Width = 4, PenStyle = PenStyle.Solid }
            };
        }

        public static IStyle CreateTrackStyle()
        {
            return new VectorStyle
            {
                Fill = null,
                Outline = null,
                Line = { Color = Color.FromString("Red"), Width = 4, PenStyle = PenStyle.Solid },
            };
        }
    }
}
