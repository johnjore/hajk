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
using Mapsui.Utilities;
using Xamarin.Essentials;
using SharpGPX;
using hajk.Data;
using hajk.Models;
using hajk.Fragments;
using hajk.Adapter;
using Serilog;
using SharpGPX.GPX1_1;
using AndroidX.Fragment.App;

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

                    //Add to route DB
                    var r = new Route
                    {
                        Name = route.name,
                        Distance = mapDistanceKm,
                        Ascent = 0, /**///Fix this
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
                        var bounds = route.GetBounds();
                        //map.BoundsLeft = -37.5718; map.BoundsRight = -37.5076; map.BoundsBottom = 145.5424; map.BoundsTop = 145.5189;
                        //Left: -10.2455, Top:  110.5426 Right: -43.2748, Bottom:  154.3179

                        Models.Map map = new Models.Map
                        {
                            Name = Regex.Replace(route.name, @"[^\u0000-\u007F]+", ""), //Removes non-ascii characters from filename
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

                    //Add to map
                    AddRouteToMap(mapRoute);

                    /*var mbTilesTileSource = new MbTilesTileSource(new SQLiteConnectionString(file, true), null, MbTilesType.Overlay, true, true);
                    var mbTilesLayer = new TileLayer(mbTilesTileSource) { Name = file };
                    MainActivity.map.Layers.Add(mbTilesLayer);*/
                }

                Show_Dialog msg3 = new Show_Dialog(MainActivity.mContext);
                await msg3.ShowDialog($"Done", $"GPX Import Completed", Android.Resource.Attribute.DialogIcon, true, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
            });

            return null;
        }

        public static void AddRouteToMap(string mapRoute)
        {
            //Add layer
            ILayer lineStringLayer = CreateRouteLayer(mapRoute, CreateRouteStyle());
            lineStringLayer.IsMapInfoLayer = true;
            lineStringLayer.Enabled = true;
            lineStringLayer.Tag = "route";
            Fragment_map.map.Layers.Add(lineStringLayer);

            //Enable menu
            AndroidX.AppCompat.Widget.Toolbar toolbar = MainActivity.mContext.FindViewById<AndroidX.AppCompat.Widget.Toolbar>(Resource.Id.toolbar);
            toolbar.Menu.FindItem(Resource.Id.action_clearmap).SetEnabled(true);
        }

        //https://www.geodatasource.com/developers/c-sharp
        private static double Distance(double lat1, double lon1, double lat2, double lon2, char unit)
        {
            if ((lat1 == lat2) && (lon1 == lon2))
            {
                return 0;
            }
            else
            {
                double theta = lon1 - lon2;
                double dist = Math.Sin(Deg2Rad(lat1)) * Math.Sin(Deg2Rad(lat2)) + Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) * Math.Cos(Deg2Rad(theta));
                dist = Math.Acos(dist);
                dist = Rad2Deg(dist);
                dist = dist * 60 * 1.1515;
                if (unit == 'K')
                {
                    dist *= 1.609344;
                }
                else if (unit == 'N')
                {
                    dist *= 0.8684;
                }
                return (dist);
            }
        }

        private static double Deg2Rad(double deg)
        {
            return (deg * Math.PI / 180.0);
        }

        private static double Rad2Deg(double rad)
        {
            return (rad / Math.PI * 180.0);
        }

        public static (string, float) GPXtoRoute(rteType route)
        {
            if (route.GetGarminExt() != null)
            {
                Console.WriteLine("Route '{0}' has Garmin extension", route.name);

                /**/ //Read Garmin's extended routing attributes
                var a = route.GetGarminExt();
            }

            string mapRoute = "LINESTRING(";
            float mapDistanceKm = 0;
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
                    mapDistanceKm += (float)Distance((float)route.rtept[i - 1].lat, (float)route.rtept[i - 1].lon, (float)route.rtept[i].lat, (float)route.rtept[i].lon, 'K');
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

            //Convert from string and line strings
            var lineString = (LineString)Geometry.GeomFromText(strRoute);
            lineString = new LineString(lineString.Vertices.Select(v => SphericalMercator.FromLonLat(v.Y, v.X)));
            features.Add(new Feature { Geometry = lineString });

            //Waypoint markers
            foreach (var waypoint in lineString.Vertices)
            {
                var feature = new Feature { Geometry = waypoint };
                feature.Styles.Add(new SymbolStyle
                {
                    SymbolScale = 1.0f,
                    MaxVisible = 2.0f,
                    MinVisible = 0.0f,
                    RotateWithMap = true, /**/// For future when adding arrows?
                    SymbolType = SymbolType.Ellipse,
                    Fill = null, /**/// new Brush { FillStyle = FillStyle.Cross, Color = Color.Red, Background = Color.Transparent},
                    Outline = new Pen { Color = Color.Blue, Width = 1.5f }
                });
                features.Add(feature);
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
                    Outline = new Pen { Color = Color.Red, Width = 0.2f }
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
                Line = { Color = Color.FromString("Red"), Width = 4, PenStyle = PenStyle.Dot }
            };
        }
    }
}
