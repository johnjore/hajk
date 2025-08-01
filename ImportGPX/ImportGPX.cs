﻿using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Fragment.App;
using GPXUtils;
using hajk.Data;
using hajk.Fragments;
using hajk.Models;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Rendering.Skia;
using NetTopologySuite.Noding;
using SharpGPX;
using SharpGPX.GPX1_1;
using SharpGPX.GPX1_1.Garmin;
using SharpGPX.GPX1_1.Topografix;
using System;
using System.Globalization;
using System.Xml;

namespace hajk
{
    public class Import
    {
        /// <summary>
        /// Start a GPX import sequence
        ///     a) Ask for file to import (and validate it)
        ///     b) Process the GPX file
        /// </summary>
        public static void ImportGPX()
        {
            Task.Run(() =>
            {
                GpxClass? gpxData = Import.PickAndParseGPX().Result;

                //Anything to process?
                if (gpxData == null)
                {
                    return;
                }

                //Anything to process?
                if (gpxData.Routes.Count == 0 && gpxData.Tracks.Count == 0 && gpxData.Waypoints.Count == 0)
                {
                    return;
                }

                ProcessGPX(gpxData);
            });
        }

        /// <summary>
        /// Pick a (single) file to import. Validate its a valid GPX file before asking if to continue to import file. Contents of GPX file is returned as a string
        /// </summary>
        private static async Task<GpxClass?> PickAndParseGPX()
        {
            GpxClass? gpx = new();

            try
            {
                PickOptions? options = new()
                {
                    PickerTitle = "Select GPX file to import",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, new[] { "application/gpx", "application/octet-stream" } }
                    })
                };

                IEnumerable<FileResult> result = await FilePicker.PickMultipleAsync(options);

                if (result == null || !result.Any())
                {
                    Serilog.Log.Information("No files selected");
                    return null;
                }

                if (Platform.CurrentActivity == null)
                {
                    Serilog.Log.Information("Platform.CurrentActivity is null");
                    return null;
                }

                foreach (FileResult fileName in result)
                {
                    if (GpxClass.CheckFile(fileName.FullPath) == false)
                    {
                        Show_Dialog? msg2 = new (Platform.CurrentActivity); //Don't use cached value
                        await msg2.ShowDialog($"'{fileName}'", "is not a valid GPX file. Unable to import file.", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.OK);
                    }
                    else
                    {
                        //We have a valid GPX file (we think)
                        var stream = await fileName.OpenReadAsync();
                        string contents = string.Empty;
                        using (var reader = new StreamReader(stream))
                        {
                            contents = reader.ReadToEnd();
                        }

                        GpxClass? tempGPX = GpxClass.FromXml(contents);
                        string r = (tempGPX.Routes.Count == 1) ? "route" : "routes";
                        string t = (tempGPX.Tracks.Count == 1) ? "track" : "tracks";
                        string p = (tempGPX.Waypoints.Count == 1) ? "POI" : "POIs";

                        Show_Dialog? msg1 = new (Platform.CurrentActivity); //Don't use cached value
                        Show_Dialog.MessageResult dialogResult = await msg1?.ShowDialog($"'{fileName.FileName}'", $"Found {tempGPX.Routes.Count} {r}, {tempGPX.Tracks.Count} {t} and {tempGPX.Waypoints.Count} {p}. Import?", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO);
                        if (dialogResult.Equals(Show_Dialog.MessageResult.YES))
                        {
                            //Append tempGPX to GPX
                            foreach (rteType route in tempGPX.Routes)
                            {
                                gpx.Routes.Add(route);
                            }

                            foreach (trkType track in tempGPX.Tracks)
                            {
                                gpx.Tracks.Add(track);
                            }

                            foreach (wptType waypoint in tempGPX.Waypoints)
                            {
                                gpx.Waypoints.Add(waypoint);
                            }
                        }
                    }
                }

                //Could be populated with 0 routes, 0 tracks and 0 waypoints (POIs)
                if (gpx.Routes.Count == 0 && gpx.Tracks.Count == 0 && gpx.Waypoints.Count == 0)
                {
                    return null;
                }
                else
                {
                    return gpx;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Import - PickAndParseGPX()");
            }

            return gpx;
        }

        /// <summary>
        /// Loop through gpxData and process each route, track and POI
        /// </summary>
        private static async void ProcessGPX(GpxClass? gpxData)
        {
            try
            {
                bool DownloadOfflineMap = false;
                Show_Dialog? msg = null;

                if (gpxData == null || Platform.CurrentActivity == null)
                {
                    return;
                }

                //Only ask if we have routes and/or tracks and/or Waypoints to import
                if (gpxData.Routes.Count > 0 || gpxData.Tracks.Count > 0 || gpxData.Waypoints.Count > 0)
                {
                    //Does the user want maps downloaded for offline usage?
                    msg = new(Platform.CurrentActivity);
                    if (await msg?.ShowDialog($"Offline Map", $"Download map for offline usage?", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) == Show_Dialog.MessageResult.YES)
                    {
                        DownloadOfflineMap = true;
                    }
                }

                if (Looper.MyLooper() == null)
                {
                    Looper.Prepare();
                }

                foreach (rteType route in gpxData.Routes)
                {
                    Serilog.Log.Information($"Importing '{route.name}'");

                    if (AddGPXRoute(route, DownloadOfflineMap)?.Result == false)
                    {
                        Toast.MakeText(Platform.AppContext, $"Failed to import '{route.name}'", ToastLength.Short)?.Show();
                    }
                    else
                    {
                        Serilog.Log.Information($"Done importing '{route.name}'");
                        Toast.MakeText(Platform.AppContext, $"Imported '{route.name}'", ToastLength.Short)?.Show();
                    }
                }

                foreach (trkType track in gpxData.Tracks)
                {
                    Serilog.Log.Information($"Importing {track.name}");
                    
                    if (AddGPXTrack(track, DownloadOfflineMap)?.Result == false)
                    {
                        Toast.MakeText(Platform.AppContext, $"Failed to import '{track.name}'", ToastLength.Short)?.Show();
                    }
                    else
                    {
                        Serilog.Log.Information($"Done importing '{track.name}'");
                        Toast.MakeText(Platform.AppContext, $"Imported '{track.name}'", ToastLength.Short)?.Show();
                    }
                }

                foreach (wptType wptType in gpxData.Waypoints)
                {
                    AddGPXWayPoint(wptType, DownloadOfflineMap);
                }

                //Display Imported POIs, regardless of settings. Importing with nothing visible will confuse users
                if (gpxData.Waypoints.Count > 0)
                {
                    DisplayMapItems.AddPOIToMap();
                }

                Serilog.Log.Information($"Done importing all items in gpx file");
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Import - GetRoute()");
            };
        }

        public static void GPXImportfromIntent(Android.Content.Intent? intent)
        {
            try
            {
                if (intent == null || intent.Action == null || intent.Scheme == null || intent.Type == null || intent.Data == null || intent.Data.Path == null)
                    return;

                var action = intent.Action;
                if ((action.CompareTo("android.intent.action.VIEW") == 0) && (intent.Scheme.CompareTo("content") == 0) && !String.IsNullOrEmpty(intent.DataString))
                {
                    var data = intent.DataString;
                    var uri = intent.Data;

                    Serilog.Log.Debug("Content intent detected: " + action + " : " + data.ToString() + " : " + intent.Type.ToString() + " : " + uri.Path.ToString());

                    Stream? stream = Platform.CurrentActivity?.ContentResolver?.OpenInputStream(uri);
                    if (stream == null)
                    {
                        Serilog.Log.Fatal("'stream' is null");
                        return;
                    }

                    string contents = String.Empty;
                    using (var reader = new StreamReader(stream))
                    {
                        contents = reader.ReadToEndAsync().Result;
                    }

                    if (contents == null)
                    {
                        Serilog.Log.Fatal("'contents' is null");
                        return;
                    }

                    if (contents.Length == 0)
                    {
                        Serilog.Log.Fatal("'contents.Length == 0'");
                        return;
                    }

                    GpxClass gpx = GpxClass.FromXml(contents);
                    if (gpx == null)
                    {
                        Serilog.Log.Fatal("'gpx' is null");
                        return;
                    }

                    if (CheckFile_Walkabout(contents) == false && Platform.CurrentActivity != null)
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            Show_Dialog? msg2 = new (Platform.CurrentActivity); //Don't use cached value
                            await msg2?.ShowDialog($"{uri?.PathSegments[uri.PathSegments.Count - 1]}", "is not a valid GPX file. Unable to import file.", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.OK);
                        });
                        return;
                    }

                    string r = (gpx.Routes.Count == 1) ? "route" : "routes";
                    string t = (gpx.Tracks.Count == 1) ? "track" : "tracks";
                    string p = (gpx.Waypoints.Count == 1) ? "POI" : "POIs";

                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        Show_Dialog msg = new (Platform.CurrentActivity);
                        if (await msg?.ShowDialog($"{uri.LastPathSegment}", $"Found {gpx.Routes.Count} {r}, {gpx.Tracks.Count} {t} and {gpx.Waypoints.Count} {p}. Import?", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) == Show_Dialog.MessageResult.YES)
                        {
                            await Task.Run(() =>
                            {
                                ProcessGPX(gpx);
                            });
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Import - GPXImportfromIntent()");
            }
        }

        private static void AddGPXWayPoint(wptType wptType, bool DownloadOfflineMap)
        {
            try
            {
                //Add to POI DB
                GPXDataPOI p = new()
                {
                    Name = wptType.name,
                    Description = Utilities.HtmlUtils.StripHtml(wptType.desc),
                    Symbol = wptType.sym,
                    Lat = wptType.lat,
                    Lon = wptType.lon
                };
                POIDatabase.SavePOI(p);

                if (DownloadOfflineMap)
                {
                    Task.Run(async () =>
                    {
                        var b = new boundsType(p.Lat, p.Lat, p.Lon, p.Lon);
                        await DownloadRasterImageMap.DownloadMap(b, -1);
                    });
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Import - AddGPXWayPoint()");
            }
        }

        private static async Task<bool>? AddGPXRoute(rteType route, bool DownloadOfflineMap)
        {
            try
            {
                //Parse and extract information from GPX file
                (string? mapRoute, float mapDistance_m, List<GPXUtils.Position>? LatLon) = ParseGPXtoRoute(route);

                //Sanity check the GPX data
                if (mapRoute == null || mapRoute == "" || mapRoute == string.Empty)
                {
                    Serilog.Log.Information("mapRoute is empty");
                    return false;
                }

                if (mapDistance_m == 0.0f)
                {
                    Serilog.Log.Information("Distance is 0m");
                    return false;
                }

                if (LatLon == null || LatLon.Count == 0)
                {
                    Serilog.Log.Information("No LatLon coordinates");
                    return false;
                }

                //Create a standalone GPX
                var newGPX = new GpxClass()
                {
                    Metadata = new metadataType()
                    {
                        name = route.name,
                        desc = route.desc,
                    },
                };

                //Update route as it might contain Extensions. Not very efficient...
                route.rtept.Clear();

                foreach (var i in LatLon)
                {
                    wptType wptType = new ()
                    {
                        lat = (decimal)i.Latitude,
                        lon = (decimal)i.Longitude,
                        ele = (decimal)i.Elevation,
                        eleSpecified = i.ElevationSpecified,
                    };

                    route.rtept.Add(wptType);
                }
                newGPX.Routes.Add(route);

                //Add to routetrack DB (Does not include map, elevation profile or ascent/descent information)
                GPXDataRouteTrack r = new()
                {
                    GPXType = GPXType.Route,
                    Name = route.name,
                    Distance = mapDistance_m / 1000,
                    Ascent = 0,
                    Descent = 0,
                    Description = route.desc,
                    GPX = newGPX.ToXml(),
                    GPXStartLocation = $"{LatLon[0].Latitude.ToString(CultureInfo.InvariantCulture)},{LatLon[0].Longitude.ToString(CultureInfo.InvariantCulture)}",
                };
                r.Id = RouteDatabase.SaveRoute(r);

                //If we are downloading GeoTiff and tiles
                if (DownloadOfflineMap)
                {
                    await Task.Run(async () =>
                    {
                        await AddGPXToDatabase(r.Id);
                    });
                }

                //Update RecycleView with new entry, if the fragment exists
                FragmentActivity? activity = Platform.CurrentActivity as FragmentActivity;
                if (activity?.SupportFragmentManager.FindFragmentByTag(Fragment_Preferences.Fragment_GPX) != null && Fragment_gpx.GPXDisplay == Models.GPXType.Route)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Adapter.GpxAdapter.mGpxData.Insert(RouteDatabase.GetRouteAsync(r.Id).Result);
                        Fragment_gpx.mAdapter?.NotifyDataSetChanged();
                    });
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Import - AddGPXRoute()");
            }

            return true;
        }
                
        private static async Task<bool>? AddGPXTrack(trkType track, bool DownloadOfflineMap)
        {            
            try
            {
                //Parse and extract information from GPX file
                (string? mapRoute, float mapDistance_m, List<GPXUtils.Position>? LatLon) = ParseGPXtoRoute(track.ToRoutes()[0]);

                //Sanity check the GPX data
                if (mapRoute == null || mapRoute == "" || mapRoute == string.Empty)
                {
                    Serilog.Log.Information("mapRoute is empty");
                    return false;
                }

                if (mapDistance_m == 0.0f)
                {
                    Serilog.Log.Information("Distance is 0m");
                    return false;
                }

                if (LatLon == null || LatLon.Count == 0)
                {
                    Serilog.Log.Information("No LatLon coordinates");
                    return false;
                }

                //Create a standalone GPX
                var newGPX = new GpxClass()
                {
                    Metadata = new metadataType()
                    {
                        name = track.name,
                        desc = track.desc,
                    },
                };

                //Update route as it might contain Extensions. Not very efficient...
                track.trkseg[0].trkpt.Clear();

                foreach (var i in LatLon)
                {
                    wptType wptType = new ()
                    {
                        lat = (decimal)i.Latitude,
                        lon = (decimal)i.Longitude,
                        ele = (decimal)i.Elevation,
                        eleSpecified = i.ElevationSpecified,
                    };

                    track.trkseg[0].trkpt.Add(wptType);
                }
                newGPX.Tracks.Add(track);

                //Add to routetrack DB (Does not include map, elevation profile or ascent/descent information)
                GPXDataRouteTrack r = new()
                {
                    GPXType = GPXType.Track,
                    Name = track.name,
                    Distance = mapDistance_m / 1000,
                    Ascent = 0,
                    Descent = 0,
                    Description = track.desc,
                    GPX = newGPX.ToXml(),
                    GPXStartLocation = $"{LatLon[0].Latitude.ToString(CultureInfo.InvariantCulture)},{LatLon[0].Longitude.ToString(CultureInfo.InvariantCulture)}",
                };
                r.Id = RouteDatabase.SaveRoute(r);

                //If we are downloading GeoTiff and tiles
                if (DownloadOfflineMap)
                {
                    await Task.Run(async () =>
                    {
                        await AddGPXToDatabase(r.Id);
                    });
                }

                //Update RecycleView with new entry, if the fragment exists
                FragmentActivity? activity = Platform.CurrentActivity as FragmentActivity;
                if (activity?.SupportFragmentManager.FindFragmentByTag(Fragment_Preferences.Fragment_GPX) != null && Fragment_gpx.GPXDisplay == Models.GPXType.Track)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Adapter.GpxAdapter.mGpxData.Insert(RouteDatabase.GetRouteAsync(r.Id).Result);
                        Fragment_gpx.mAdapter?.NotifyDataSetChanged();
                    });
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Import - AddGPXTrack()");
            }

            return true;
        }

        public static string? CreateThumbprintMap(GpxClass newGPX)
        {
            try
            {
                //Create viewport to match screen size
                if (Platform.CurrentActivity?.Resources?.DisplayMetrics?.WidthPixels != null)
                {
                    var viewport = new Viewport()
                    {
                        Width = Platform.CurrentActivity.Resources.DisplayMetrics.WidthPixels,
                        Height = 1000 /**///Any value large enough to not cause issues
                    };

                    //Set loction to match route/ track and change viewport to fill available size
                    Fragment_map.mapControl?.Map.Navigator.SetViewport(viewport);
                    //Fragment_map.mapControl?.Map.Navigator.ZoomToBox(viewport.ToExtent());  <--- Should work in 5.x?!?!?
                }

                //Overlay GPX on map and zoom
                var bounds = newGPX.GetBounds();

                var (min_x, min_y) = SphericalMercator.FromLonLat((double)bounds.maxlon, (double)bounds.minlat);
                var (max_x, max_y) = SphericalMercator.FromLonLat((double)bounds.minlon, (double)bounds.maxlat);

                /**///Workaround for https://github.com/Mapsui/Mapsui/issues/3048
                int diff = 1500;
                if (Math.Abs(min_x - max_x) < diff)
                {
                    double adjustment = (diff - Math.Abs(max_x - min_x)) / 2.0;
                    if (min_x > max_x)
                    {
                        min_x += adjustment;
                        max_x -= adjustment;
                    }
                    else
                    {
                        min_x -= adjustment;
                        max_x += adjustment;
                    }
                }

                if (Math.Abs(min_y - max_y) < diff)
                {
                    double adjustment = (diff - Math.Abs(max_y - min_y)) / 2.0;
                    if (min_y > max_y)
                    {
                        min_y += adjustment;
                        max_y -= adjustment;
                    }
                    else
                    {
                        min_y -= adjustment;
                        max_y += adjustment;
                    }
                }

                Fragment_map.mapControl?.Map.Navigator.ZoomToBox(new MRect(min_x, min_y, max_x, max_y), MBoxFit.Fit);
                Fragment_map.mapControl?.Map.Navigator.RotateTo(0.0);

                //Wait for each layer to complete
                foreach (ILayer layer in Fragment_map.map.Layers)
                {
                    while (layer.Busy)
                    {
                        System.Threading.Thread.Sleep(2);
                    }
                }

                //Create the thumbprint
                MemoryStream bitmap = new MapRenderer().RenderToBitmapStream(Fragment_map.map.Navigator.Viewport, Fragment_map.map.Layers, Fragment_map.map.BackColor);
                bitmap.Position = 0;
                string ImageBase64String = Convert.ToBase64String(bitmap.ToArray());

                return ImageBase64String;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Import - CreateThumbprintMap()");
            }

            return null;
        }

        public static (string?, float, List<Position>?) ParseGPXtoRoute(rteType? route)
        {
            try
            {
                if (route == null)
                {
                    Serilog.Log.Information("Route is 'null'");
                    return (null, 0, null);
                }

                List<Position>? ListLatLon = GetAllWayPointsFromRoute(route);
                if (ListLatLon == null || ListLatLon.Count <= 1)
                {
                    Serilog.Log.Information("Unable to extract WayPoints from route, ListLatLon is 'null', or 1 or less");
                    return (null, 0, null);
                }

                var p = new PositionHandler();
                double maxDistance = Fragment_Preferences.ElevationDistanceLookup; //Divvy up the leg in sectionss
                float mapDistance_m = 0.0f;
                List<Position>? ListLatLonEle = [];

                for (int i = 1; i < ListLatLon.Count; i++)
                {
                    var p1 = ListLatLon[i - 1];
                    var p2 = ListLatLon[i];

                    //Add elements 0 to n-1
                    ListLatLonEle.Add(p1);

                    float distance_m = (float)p.CalculateDistance(p1, p2, DistanceType.Meters);
                    mapDistance_m += distance_m;

                    //Add more points if distance between p1 and p2 is too great
                    if (distance_m > maxDistance)
                    {
                        int Chunks = Convert.ToInt32(Math.Ceiling((double)distance_m / maxDistance));

                        for (int j = 1; j < Chunks; j++)
                        {
                            var p3 = Utils.Misc.CalculateNofM(p1, p2, distance_m, (double)((double)distance_m / (double)Chunks * (double)j));
                            ListLatLonEle.Add(new Position(p3.X, p3.Y, -99999, false, null));
                            Serilog.Log.Debug($"{p3.X}, {p3.Y}");
                        }
                    }
                }

                //Add last item
                ListLatLonEle?.Add(ListLatLon.Last());

                //Convert the list to a string
                string mapRoute = ConvertLatLonListToLineString(ListLatLonEle);

                return (mapRoute, mapDistance_m, ListLatLonEle);
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Import - ParseGPXtoRoute()");
            }

            return (null, 0, null);
        }

        private static List<Position>? GetAllWayPointsFromRoute(rteType? route)
        {
            if (route == null)
                return null;

            List<Position> ListLatLon = [];

            for (int i = 0; i < route.rtept.Count; i++)
            {
                ListLatLon.Add(new Position((double)route.rtept[i].lat, (double)route.rtept[i].lon, (double)route.rtept[i].ele, route.rtept[i].eleSpecified, null));

                var rtePteExt = route.rtept[i].GetExt<RoutePointExtension>();
                if (rtePteExt != null)
                {
                    Serilog.Log.Debug($"Route '{route.name}' has Garmin extension. Adding additional route points");

                    for (int j = 0; j < rtePteExt.rpt.Count; j++)
                    {
                        ListLatLon.Add(new Position((double)rtePteExt.rpt[j].lat, (double)rtePteExt.rpt[j].lon, 0, false, null));
                    }
                }
            }

            return ListLatLon;
        }

        private static string ConvertLatLonListToLineString(List<GPXUtils.Position> ListLatLon)
        {
            var LineString = "LINESTRING(";

            try
            {
                for (int i = 0; i < ListLatLon.Count; i++)
                {
                    if (i != 0)
                    {
                        LineString += ",";
                    }

                    LineString += ListLatLon[i].Latitude + " " + ListLatLon[i].Longitude;
                }

                LineString += ")";
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Import - ConvertLatLonListToLineString()");
            }

            return LineString;
        }

        public static string? ConvertRouteToLineString(rteType? route)
        {
            if (route == null)
                return null;


            var LineString = "LINESTRING(";

            try
            {
                for (int i = 0; i < route.rtept.Count; i++)
                {
                    if (i != 0)
                    {
                        LineString += ",";
                    }

                    LineString += route.rtept[i].lat+ " " + route.rtept[i].lon;
                }

                LineString += ")";
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Import - ConvertRouteToLineString()");
            }

            return LineString;
        }

        private static bool CheckFile_Walkabout(String content)
        {
            try
            {
                XmlDocument xmlDocument = new ();
                xmlDocument.LoadXml(content);
                return xmlDocument.DocumentElement != null && (xmlDocument.DocumentElement.NamespaceURI == "http://www.topografix.com/GPX/1/0" || xmlDocument.DocumentElement.NamespaceURI == "http://www.topografix.com/GPX/1/1");
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug("GpxClass.CheckFile: Error reading stream. Contents is not xml:\r\n{1}", ex);
                return false;
            }
        }

        private static async Task<bool> AddGPXToDatabase(int databaseId)
        {
            var route_to_download = RouteDatabase.GetRouteAsync(databaseId).Result;
            GpxClass gpx_to_import = GpxClass.FromXml(route_to_download.GPX);

            //Get elevation data first
            await Elevation.DownloadElevationData(gpx_to_import);

            //Get map tiles
            await DownloadRasterImageMap.DownloadMap(gpx_to_import.GetBounds(), databaseId);

            //Update with elevation data
            await Task.Run(() =>
            {
                if (route_to_download.GPXType == GPXType.Route)
                {
                    rteType? updated_route = Elevation.LookupElevationData(gpx_to_import.Routes[0]);

                    if (updated_route != null)
                    {
                        gpx_to_import.Routes.Clear();
                        gpx_to_import.Routes.Add(updated_route);
                        (route_to_download.Ascent, route_to_download.Descent) = Elevation.CalculateAscentDescent(updated_route);
                    }
                }
                else if (route_to_download.GPXType == GPXType.Track)
                {
                    trkType? updated_track = Elevation.LookupElevationData(gpx_to_import.Tracks[0]);

                    if (updated_track != null)
                    {
                        gpx_to_import.Tracks.Clear();
                        gpx_to_import.Tracks.Add(updated_track);
                        (route_to_download.Ascent, route_to_download.Descent) = Elevation.CalculateAscentDescent(updated_track);
                    }
                }
                else
                {
                    Serilog.Log.Fatal("Unknown and unhandled GPXType");
                    return;
                }
            });

            //Add elevation data
            route_to_download.GPX = gpx_to_import.ToXml();
            
            //Naismith Travel Time
            (int travel_hours, int travel_min) = Naismith.CalculateTime(route_to_download.Distance, Fragment_Preferences.naismith_speed_kmh, route_to_download.Ascent, route_to_download.Descent);
            route_to_download.NaismithTravelTime = $"{string.Format("{0:D2}", travel_hours)}:{ string.Format("{0:D2}", travel_min)}";

            //Shenandoahs's Hiking Difficulty
            float ShenandoahsHikingDifficultyScale = ShenandoahsHikingDifficulty.CalculateScale(route_to_download.Distance, route_to_download.Ascent);
            route_to_download.ShenandoahsScale = ShenandoahsHikingDifficultyScale;

            //Create Looper if needed for Toast messages
            if (Looper.MyLooper() == null)
            {
                Looper.Prepare();
            }

            //Create / Update thumbsize map            
            Toast.MakeText(Platform.AppContext, "Creating new overview image", ToastLength.Short)?.Show();
            string? ImageBase64String = DisplayMapItems.CreateThumbnail(route_to_download.GPXType, gpx_to_import);
            if (ImageBase64String != null)
            {
                route_to_download.ImageBase64String = ImageBase64String;
            }

            //Save to DB
            RouteDatabase.SaveRouteAsync(route_to_download).Wait();

            Toast.MakeText(Platform.AppContext, $"Finished downloads for '{route_to_download.Name}'", ToastLength.Short)?.Show();
            return true;
        }

        public static async Task<bool> UpdateRouteOrTrack(int index)
        {
            try
            {
                GPXDataRouteTrack route = RouteDatabase.GetRouteAsync(index).Result;
                GpxClass gpx = GpxClass.FromXml(route.GPX);

                //Get tiles (Elevation and Map)
                await Elevation.DownloadElevationData(gpx);
                await DownloadRasterImageMap.DownloadMap(gpx.GetBounds(), index);

                //Update route with elevation data
                await Task.Run(() =>
                {
                    if (route.GPXType == GPXType.Route)
                    {
                        rteType? updated_route = Elevation.LookupElevationData(gpx.Routes[0]);

                        if (updated_route != null)
                        {
                            gpx.Routes.Clear();
                            gpx.Routes.Add(updated_route);
                            (route.Ascent, route.Descent) = Elevation.CalculateAscentDescent(updated_route);
                        }
                    }
                    else if (route.GPXType == GPXType.Track)
                    {
                        //Elevation data
                        (route.Ascent, route.Descent) = Elevation.CalculateAscentDescent(gpx.Tracks[0]);
                    }
                    else
                    {
                        Serilog.Log.Fatal("Unknown and unhandled GPXType");
                        return;
                    }
                });

                //Naismith Travel Time
                (int travel_hours, int travel_min) = Naismith.CalculateTime(route.Distance, Fragment_Preferences.naismith_speed_kmh, route.Ascent, route.Descent);
                route.NaismithTravelTime = $"{string.Format("{0:D2}", travel_hours)}:{string.Format("{0:D2}", travel_min)}";

                //Shenandoahs's Hiking Difficulty
                float ShenandoahsHikingDifficultyScale = ShenandoahsHikingDifficulty.CalculateScale(route.Distance, route.Ascent);
                route.ShenandoahsScale = ShenandoahsHikingDifficultyScale;

                //GPX
                route.GPX = gpx.ToXml();

                //Update with new data
                RouteDatabase.SaveRouteAsync(route).Wait();

                //Create / Update thumbsize map
                if (Looper.MyLooper() == null)
                {
                    Looper.Prepare();
                }

                Toast.MakeText(Platform.AppContext, "Creating new overview image", ToastLength.Short)?.Show();
                string? ImageBase64String = DisplayMapItems.CreateThumbnail(route.GPXType, gpx);
                if (ImageBase64String != null)
                {
                    route.ImageBase64String = ImageBase64String;
                    RouteDatabase.SaveRouteAsync(route).Wait();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "Crashed");
            }

            Toast.MakeText(Platform.AppContext, "Finished downloads", ToastLength.Short)?.Show();

            return true;
        }
    }
}
