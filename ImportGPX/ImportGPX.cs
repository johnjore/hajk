using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Fragment.App;
using GPXUtils;
using hajk.Data;
using hajk.Fragments;
using hajk.Models;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Rendering.Skia;
using Mapsui;
using SharpGPX.GPX1_1.Garmin;
using SharpGPX.GPX1_1.Topografix;
using SharpGPX.GPX1_1;
using SharpGPX;
using System.Xml;
using System;

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
            GpxClass? gpx = new GpxClass();

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
                        Show_Dialog msg2 = new Show_Dialog(Platform.CurrentActivity); //Don't use cached value
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

                        Show_Dialog msg1 = new Show_Dialog(Platform.CurrentActivity); //Don't use cached value
                        Show_Dialog.MessageResult dialogResult = await msg1.ShowDialog($"'{fileName.FileName}'", $"Found {tempGPX.Routes.Count} {r}, {tempGPX.Tracks.Count} {t} and {tempGPX.Waypoints.Count} {p}. Import?", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO);
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
                    if (await msg.ShowDialog($"Offline Map", $"Download map for offline usage?", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) == Show_Dialog.MessageResult.YES)
                    {
                        DownloadOfflineMap = true;
                    }
                }

                foreach (rteType route in gpxData.Routes)
                {
                    if (AddGPXRoute(route, DownloadOfflineMap).Result == false)
                    {
                        Toast.MakeText(Platform.AppContext, $"Failed to import '{route.name}'", ToastLength.Short)?.Show();
                    }
                }
                
                foreach (trkType track in gpxData.Tracks)
                {
                    if (AddGPXTrack(track, DownloadOfflineMap).Result == false)
                    {
                        Toast.MakeText(Platform.AppContext, $"Failed to import '{track.name}'", ToastLength.Short)?.Show();
                    };
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

                Serilog.Log.Information($"Done importing gpx file");
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

                    if (CheckFile_Walkabout(stream) == false)
                    {
                        MainThread.BeginInvokeOnMainThread(async () =>
                        {
                            Show_Dialog msg2 = new Show_Dialog(Platform.CurrentActivity); //Don't use cached value
                            await msg2.ShowDialog($"{uri.PathSegments[uri.PathSegments.Count - 1]}", "is not a valid GPX file. Unable to import file.", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.OK);
                        });
                        return;
                    }

                    string contents = String.Empty;
                    stream = Platform.CurrentActivity?.ContentResolver?.OpenInputStream(uri);
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

                    string r = (gpx.Routes.Count == 1) ? "route" : "routes";
                    string t = (gpx.Tracks.Count == 1) ? "track" : "tracks";
                    string p = (gpx.Waypoints.Count == 1) ? "POI" : "POIs";

                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        Show_Dialog msg = new Show_Dialog(Platform.CurrentActivity);
                        if (await msg.ShowDialog($"{uri.LastPathSegment}", $"Found {gpx.Routes.Count} {r}, {gpx.Tracks.Count} {t} and {gpx.Waypoints.Count} {p}. Import?", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) == Show_Dialog.MessageResult.YES)
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
                GPXDataPOI p = new GPXDataPOI
                {
                    Name = wptType.name,
                    Description = wptType.desc,
                    Symbol = wptType.sym,
                    Lat = wptType.lat,
                    Lon = wptType.lon
                };
                POIDatabase.SavePOI(p);

                if (DownloadOfflineMap)
                {
                    var b = new boundsType(p.Lat, p.Lat, p.Lon, p.Lon);
                    GetloadOfflineMap(b, -1, null);
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
                
                //Clear existing GPX routes from map, else they will be included
                Utils.Misc.ClearTrackRoutesFromMap();

                //Add to map
                DisplayMapItems.AddRouteToMap(mapRoute, GPXType.Route, false, route.name);
                               
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
                    wptType wptType = new wptType
                    {
                        lat = (decimal)i.Latitude,
                        lon = (decimal)i.Longitude,
                        ele = (decimal)i.Elevation,
                        eleSpecified = true,
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
                };
                RouteDatabase.SaveRoute(r);

                //If we are downloading GeoTiff and tiles
                if (DownloadOfflineMap)
                {
                    //GeoTiff files
                    await Elevation.DownloadElevationData(newGPX);

                    //Calculate ascent/descent
                    List<GPXUtils.Position>? tmp = await Elevation.LookupElevationData(LatLon);
                    if (tmp != null)
                    {
                        LatLon = tmp;
                        (r.Ascent, r.Descent) = Elevation.CalculateAscentDescent(LatLon);
                    }

                    //Map tiles
                    await GetloadOfflineMap(route.GetBounds(), r.Id, null);
                }

                //Create thumbsize map and save to DB
                r.ImageBase64String = CreateThumbprintMap(newGPX);

                //Save updated entry to DB
                RouteDatabase.SaveRouteAsync(r).Wait();

                //Update RecycleView with new entry, if the fragment exists
                FragmentActivity? activity = Platform.CurrentActivity as FragmentActivity;
                if (activity?.SupportFragmentManager.FindFragmentByTag(Fragment_Preferences.Fragment_GPX) != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Fragment_gpx.mAdapter?.mGpxData.Insert(r);
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

                //Clear existing GPX routes from map, else they will be included
                Utils.Misc.ClearTrackRoutesFromMap();

                //Add to map
                DisplayMapItems.AddRouteToMap(mapRoute, GPXType.Track, false, track.name);

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
                    wptType wptType = new wptType
                    {
                        lat = (decimal)i.Latitude,
                        lon = (decimal)i.Longitude,
                        ele = (decimal)i.Elevation,
                        eleSpecified = true,
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
                };
                RouteDatabase.SaveRoute(r);

                //If we are downloading GeoTiff and tiles
                if (DownloadOfflineMap)
                {
                    //GeoTiff files
                    await Elevation.DownloadElevationData(newGPX);

                    //Calculate ascent/descent
                    List<GPXUtils.Position>? tmp = await Elevation.LookupElevationData(LatLon);
                    if (tmp != null)
                    {
                        LatLon = tmp;
                        (r.Ascent, r.Descent) = Elevation.CalculateAscentDescent(LatLon);
                    }

                    //Map tiles
                    await GetloadOfflineMap(track.GetBounds(), r.Id, null);
                }

                //Create thumbsize map and save to DB
                r.ImageBase64String = CreateThumbprintMap(newGPX);

                //Save updated entry to DB
                RouteDatabase.SaveRouteAsync(r).Wait();

                //Update RecycleView with new entry, if the fragment exists
                FragmentActivity? activity = Platform.CurrentActivity as FragmentActivity;
                if (activity?.SupportFragmentManager.FindFragmentByTag(Fragment_Preferences.Fragment_GPX) != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Fragment_gpx.mAdapter?.mGpxData.Insert(r);
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

        public static async Task GetloadOfflineMap(boundsType bounds, int id, string? strFilePath)
        {
            try
            {
                Models.Map map = new()
                {
                    Id = id,
                    ZoomMin = Fragment_Preferences.MinZoom,
                    ZoomMax = Fragment_Preferences.MaxZoom,
                    BoundsLeft = (double)bounds.minlat,
                    BoundsBottom = (double)bounds.maxlon,
                    BoundsRight = (double)bounds.maxlat,
                    BoundsTop = (double)bounds.minlon
                };

                //Get all missing tiles
                await DownloadRasterImageMap.DownloadMap(map, false);

                //Also exporting?
                if (strFilePath != null)
                {
                    DownloadRasterImageMap.ExportMapTiles(id, strFilePath);
                }

                //Refresh with new map
                Serilog.Log.Information($"Done downloading map for {map.Id}");
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Import - GetloadOfflineMap()");
            }
        }

        public static (string?, float, List<GPXUtils.Position>?) ParseGPXtoRoute(rteType? route)
        {
            try
            {
                if (route == null)
                {
                    return (null, 0, null);
                }

                float mapDistance_m = 0.0f;
                var p = new PositionHandler();
                var p2 = new GPXUtils.Position(0, 0, 0, false, null);
                List<GPXUtils.Position> ListLatLon = [];

                for (int i = 0; i < route.rtept.Count; i++)
                {
                    ListLatLon.Add(new GPXUtils.Position((double)route.rtept[i].lat, (double)route.rtept[i].lon, 0, false, null));

                    var rtePteExt = route.rtept[i].GetExt<RoutePointExtension>();
                    if (rtePteExt != null)
                    {
                        Serilog.Log.Debug("Route '{0}' has Garmin extension", route.name);

                        for (int j = 0; j < rtePteExt.rpt.Count; j++)
                        {
                            ListLatLon.Add(new GPXUtils.Position((double)rtePteExt.rpt[j].lat, (double)rtePteExt.rpt[j].lon, 0, false, null));

                            //Previous leg
                            if (j == 0 && p2.Latitude != 0 && p2.Longitude != 0)
                            {
                                var p1 = new GPXUtils.Position((float)rtePteExt.rpt[j].lat, (float)rtePteExt.rpt[j].lon, 0, false, null);
                                mapDistance_m += (float)p.CalculateDistance(p1, p2, DistanceType.Meters);
                            }

                            //First leg
                            if (j == 0)
                            {
                                var p1 = new GPXUtils.Position((float)route.rtept[i].lat, (float)route.rtept[i].lon, 0, false, null);
                                p2 = new GPXUtils.Position((float)rtePteExt.rpt[j].lat, (float)rtePteExt.rpt[j].lon, 0, false, null);
                                mapDistance_m += (float)p.CalculateDistance(p1, p2, DistanceType.Meters);
                            }

                            //All other legs
                            if (j >= 1)
                            {
                                var p1 = new GPXUtils.Position((float)rtePteExt.rpt[j - 1].lat, (float)rtePteExt.rpt[j - 1].lon, 0, false, null);
                                p2 = new GPXUtils.Position((float)rtePteExt.rpt[j].lat, (float)rtePteExt.rpt[j].lon, 0, false, null);
                                mapDistance_m += (float)p.CalculateDistance(p1, p2, DistanceType.Meters);
                            }
                        }

                        //Any points?
                        if (rtePteExt.rpt.Count == 0)
                            rtePteExt = null;
                    }

                    if (rtePteExt == null)
                    {
                        //Previous leg
                        if (i >= 1)
                        {
                            var p1 = new GPXUtils.Position((float)route.rtept[i - 1].lat, (float)route.rtept[i - 1].lon, 0, false, null);
                            p2 = new GPXUtils.Position((float)route.rtept[i].lat, (float)route.rtept[i].lon, 0, false, null);
                            mapDistance_m += (float)p.CalculateDistance(p1, p2, DistanceType.Meters);
                        }
                    }
                }

                //Convert the list to a string
                string mapRoute = ConvertLatLonListToLineString(ListLatLon);

                return (mapRoute, mapDistance_m, ListLatLon);
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Import - GPXtoRoute()");
            }

            return (null, 0, null);
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

        private static bool CheckFile_Walkabout(Stream stream)
        {
            try
            {
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.Load(stream);
                return xmlDocument.DocumentElement != null && (xmlDocument.DocumentElement.NamespaceURI == "http://www.topografix.com/GPX/1/0" || xmlDocument.DocumentElement.NamespaceURI == "http://www.topografix.com/GPX/1/1");
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug("GpxClass.CheckFile: Error reading stream. Contents is not xml:\r\n{1}", ex);
                return false;
            }
        }
    }
}
