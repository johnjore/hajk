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
using GPXUtils;

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

                Show_Dialog msg3 = new Show_Dialog(MainActivity.mContext);
                await msg3.ShowDialog($"Done", $"GPX Import Completed", Android.Resource.Attribute.DialogIcon, true, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
            });

            return null;
        }

        public static void AddGPXTrack(trkType track, bool DownloadOfflineMap)
        {
            //Get Track and distance from GPX
            var t = GPXtoRoute(track.ToRoutes()[0]);
            string mapTrack = t.Item1;
            float mapDistanceKm = t.Item2;

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
            };
            RouteDatabase.SaveRouteAsync(r).Wait();

            //Update RecycleView with new entry
            int i = Fragment_gpx.mAdapter.mGpxData.Add(r);
            Fragment_gpx.mAdapter.NotifyItemInserted(i);

            //Does the user want the maps downloaded?
            if (DownloadOfflineMap)
            {
                GetloadOfflineMap(track.GetBounds(), track.name);
            }

            //Add to map
            AddRouteToMap(mapTrack, GPXType.Track);
        }

        public static void AddGPXRoute(rteType route, bool DownloadOfflineMap)
        {
            //Get Route and distance from GPX
            var t = GPXtoRoute(route);
            string mapRoute = t.Item1;
            float mapDistanceKm = t.Item2;

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
            };
            RouteDatabase.SaveRouteAsync(r).Wait();

            //Update RecycleView with new entry
            int i = Fragment_gpx.mAdapter.mGpxData.Add(r);
            Fragment_gpx.mAdapter.NotifyItemInserted(i);

            //Does the user want the maps downloaded?
            if (DownloadOfflineMap)
            {
                GetloadOfflineMap(route.GetBounds(), route.name);
            }

            //Add to map
            AddRouteToMap(mapRoute, GPXType.Route);
        }

        private static async void GetloadOfflineMap(boundsType bounds, string name)
        {
            Models.Map map = new Models.Map
            {
                Name = Regex.Replace(name, @"[^\u0000-\u007F]+", ""), //Removes non-ascii characters from filename
                ZoomMin = PrefsActivity.MinZoom,
                ZoomMax = PrefsActivity.MaxZoom,
                BoundsLeft = (double)bounds.minlat,
                BoundsBottom = (double)bounds.maxlon,
                BoundsRight = (double)bounds.maxlat,
                BoundsTop = (double)bounds.minlon
            };

            //Download map
            await DownloadRasterImageMap.DownloadMap(map);

            //Load map
            string dbPath = MainActivity.rootPath + "/MBTiles/" + map.Name + ".mbtiles";
            Log.Information($"Loading '{dbPath}' as layer name '{map.Name}'");
            Fragment_map.map.Layers.Add(OfflineMaps.CreateMbTilesLayer(dbPath, map.Name));
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
            if (route.GetGarminExt() != null)
            {
                Console.WriteLine("Route '{0}' has Garmin extension", route.name);

                /**/ //Read Garmin's extended routing attributes
                //var a = route.GetGarminExt();
            }

            string mapRoute = "LINESTRING(";
            float mapDistanceKm = 0.0f;
            var p = new PositionHandler();
            for (int i = 0; i < route.rtept.Count; i++)
            {
                //WayPoint
                if (!(mapRoute.Equals("LINESTRING(")))
                {
                    mapRoute += ",";
                }
                mapRoute += route.rtept[i].lat.ToString() + " " + route.rtept[i].lon.ToString();

                //Calculate Distance
                if (i >= 1)
                {
                    var p1 = new Position((float)route.rtept[i - 1].lat, (float)route.rtept[i - 1].lon);
                    var p2 = new Position((float)route.rtept[i].lat, (float)route.rtept[i].lon);
                    mapDistanceKm += (float)p.CalculateDistance(p1, p2, DistanceType.Kilometers);
                }

                /**///Calculate ascent / descent data
            }
            mapRoute += ")";

            return (mapRoute, mapDistanceKm);
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
                        /**///What is mime type for GPX files?!?
                        //{ DevicePlatform.Android, new string[] { "gpx/gpx"} },
                        { DevicePlatform.Android, null },
                    })
                };

                var result = await FilePicker.PickAsync(options);

                if (result == null)
                    return null;

                Console.WriteLine("FileName: " + result.FileName + ", FilePath: " + result.FullPath);
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

                Console.WriteLine("Waypoints.Count: " + gpx.Waypoints.Count.ToString());
                Console.WriteLine("Routes.Count: " + gpx.Routes.Count.ToString());
                Console.WriteLine("Track.Count: " + gpx.Tracks.Count.ToString());
                Console.WriteLine("Lower Left - MinLat: " + bounds.minlat.ToString() + ", MaxLon: " + bounds.maxlon.ToString());
                Console.WriteLine("Top Right  - MaxLat: " + bounds.maxlat.ToString() + ", MinLon: " + bounds.minlon.ToString());

                string r = "routes";
                if (gpx.Routes.Count == 1)
                    r = "route";

                string t = "tracks";
                if (gpx.Tracks.Count == 1)
                    t = "track";

                Show_Dialog msg1 = new Show_Dialog(MainActivity.mContext);
                if (await msg1.ShowDialog($"{result.FileName}", $"Found {gpx.Routes.Count} {r} and {gpx.Tracks.Count} {t}. Import?", Android.Resource.Attribute.DialogIcon, true, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) != Show_Dialog.MessageResult.YES)
                {
                    return null;
                }

                return gpx;
            }
            catch (Exception)
            {
                // The user canceled or something went wrong
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

            //Add arrow halfway between waypoints
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
                });
                features.Add(FeatureArrow);

                //Waypoins
                var FeatureWaypoint = new Feature { Geometry = lineString.Vertices[i] };
                FeatureWaypoint.Styles.Add(new SymbolStyle
                {
                    SymbolScale = 0.7f,
                    MaxVisible = 2.0f,
                    MinVisible = 0.0f,
                    RotateWithMap = true,
                    SymbolRotation = 45,
                    SymbolType = SymbolType.Ellipse,
                    Fill = null, // new Brush { FillStyle = FillStyle.Cross, Color = Color.Red, Background = Color.Transparent},
                    Outline = new Pen { Color = Color.Blue, Width = 1.5f }
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
                Line = { Color = Color.FromString("Red"), Width = 4, PenStyle = PenStyle.Dot },
            };
        }
    }
}
