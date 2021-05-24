using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Content;
using Mapsui.Geometries;
using Mapsui.Layers;
using Mapsui.Projection;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Utilities;
using Xamarin.Essentials;
using SharpGPX.GPX1_1;
using SharpGPX.GPX1_0;
using SharpGPX;
using Android.App;
using hajk.Data;

namespace hajk
{
    public class Import
    {
        public static ILayer GetRoute()
        {
            var strRoute = string.Empty;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                GpxClass gpxData = await PickAndParse();

                if (gpxData == null)
                    return;

                foreach (var route in gpxData.Routes)
                {
                    if (route.GetGarminExt() != null)
                    {
                        Console.WriteLine("Route '{0}' has Garmin extension", route.name);

                        /**/ //Read Garmin's extended routing attributes
                        var a = route.GetGarminExt();
                    }

                    string mapRoute = "LINESTRING(";
                    decimal mapDistanceKm = 0;
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
                            mapDistanceKm += (decimal)Distance((double)route.rtept[i - 1].lat, (double)route.rtept[i - 1].lon, (double)route.rtept[i].lat, (double)route.rtept[i].lon, 'K');
                        }

                        /**///Calculate ascent / descent data
                    }
                    mapRoute += ")";

                    //Add to route DB
                    var r = new Models.Route
                    {
                        Name = route.name,
                        Distance = mapDistanceKm,
                        Ascent = 0, /**///Fix this
                        Description = route.desc,
                        WayPoints = mapRoute
                    };
                    RouteDatabase.SaveRouteAsync(r).Wait();

                    //Add to map
                    ILayer lineStringLayer = CreateRouteLayer(mapRoute, CreateRouteStyle());
                    //MainActivity.map.Layers.Remove(RouteLayer);
                    MainActivity.map.Layers.Add(lineStringLayer);
                }
            });

            return null;
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
                if (await msg1.ShowDialog($"{result.FileName}", $"Found {gpx.Routes.Count} {r} and {gpx.Tracks.Count} {t}. Import?", Android.Resource.Attribute.DialogIcon, true, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.CANCEL) != Show_Dialog.MessageResult.YES)
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
