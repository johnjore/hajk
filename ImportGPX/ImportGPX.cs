﻿using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Fragment.App;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Nts;
using Mapsui.Nts.Extensions;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Providers;
using Mapsui.Rendering.Skia;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using Mapsui.Utilities;
using Mapsui.UI;
using Mapsui.UI.Android;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;
using Xamarin.Essentials;
using hajk.Data;
using hajk.Models;
using hajk.Fragments;
using Serilog;
using SharpGPX;
using SharpGPX.GPX1_1;
using SharpGPX.GPX1_1.Garmin;
using SharpGPX.GPX1_1.Topografix;
using GPXUtils;
using AndroidX.ConstraintLayout.Core.Widgets;
using hajk.GPX;

namespace hajk
{
    public class Import
    {
        /**///Why do we need this as global variables
        public static Android.App.Dialog? dialog = null;
        public static int progress = 0;
        public static Google.Android.Material.TextView.MaterialTextView? progressBarText2 = null;

        public static void ImportGPX()
        {
            Task.Run(() =>
            {
                GpxClass? gpxData = Import.PickAndParse().Result;
                ProcessGPX(gpxData);
            });

            return;
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

                    Serilog.Log.Verbose("Content intent detected: " + action + " : " + data.ToString() + " : " + intent.Type.ToString() + " : " + uri.Path.ToString());
                    Stream? stream = Platform.CurrentActivity?.ContentResolver?.OpenInputStream(uri);

                    string fileContents = String.Empty;
                    using (var reader = new StreamReader(stream))
                    {
                        fileContents = reader.ReadToEndAsync().Result;
                    }

                    if (fileContents == null)
                        return;

                    if (fileContents.Length == 0)
                        return;

                    GpxClass gpxData = GpxClass.FromXml(fileContents);

                    if (gpxData == null)
                        return;

                    string r = (gpxData.Routes.Count == 1) ? "route" : "routes";
                    string t = (gpxData.Tracks.Count == 1) ? "track" : "tracks";
                    string p = (gpxData.Waypoints.Count == 1) ? "POI" : "POIs";

                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        Show_Dialog msg = new Show_Dialog(Platform.CurrentActivity);
                        if (await msg.ShowDialog($"{uri.LastPathSegment}", $"Found {gpxData.Routes.Count} {r}, {gpxData.Tracks.Count} {t} and {gpxData.Waypoints.Count} {p}. Import?", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) == Show_Dialog.MessageResult.YES)
                        {
                            //Only ask if we have routes and/or tracks and/or Waypoints to import, and we have internet access. Most don't have a local tile server
                            if ((gpxData.Routes.Count > 0 || gpxData.Tracks.Count > 0 || gpxData.Waypoints.Count > 0) && Connectivity.NetworkAccess == Xamarin.Essentials.NetworkAccess.Internet)
                            {
                                Import.ProcessGPX(gpxData);
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Import - GPXImportfromIntent()");
            }
        }

        private static async void ProcessGPX(GpxClass gpxData)
        {
            try
            {
                bool DownloadOfflineMap = false;

                if (gpxData == null)
                    return;

                //Only ask if we have routes and/or tracks and/or Waypoints to import, and we have internet access. Most don't have a local tile server
                if ((gpxData.Routes.Count > 0 || gpxData.Tracks.Count > 0 || gpxData.Waypoints.Count > 0) && Connectivity.NetworkAccess == Xamarin.Essentials.NetworkAccess.Internet)
                {
                    //Does the user want maps downloaded for offline usage?                
                    Show_Dialog msg1 = new Show_Dialog(Platform.CurrentActivity);
                    if (await msg1.ShowDialog($"Offline Map", $"Download map for offline usage?", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) == Show_Dialog.MessageResult.YES)
                    {
                        DownloadOfflineMap = true;
                    }

                    if (await msg1.ShowDialog($"Track/Route", $"Optimize Number of Waypoints?", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) == Show_Dialog.MessageResult.YES)
                    {
                        gpxData = GPXOptimize.Optimize(gpxData);
                    }
                }

                foreach (rteType route in gpxData.Routes)
                {
                    AddGPXRoute(route, DownloadOfflineMap);
                }

                foreach (trkType track in gpxData.Tracks)
                {
                    AddGPXTrack(track, DownloadOfflineMap);
                }

                foreach (wptType wptType in gpxData.Waypoints)
                {
                    AddGPXWayPoint(wptType, DownloadOfflineMap);
                }

                //Display Imported POIs, regardless of settings. Importing with nothing visible will confuse users
                if (gpxData.Waypoints.Count > 0)
                {
                    Import.AddPOIToMap();
                }

                Log.Information($"Done importing gpx file");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Import - GetRoute()");
            };
        }

        public static void AddGPXWayPoint(wptType wptType, bool DownloadOfflineMap)
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
                    GetloadOfflineMap(b, -1, null, false);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Import - AddGPXWayPoint()");
            }
        }

        public static void AddGPXTrack(trkType track, bool DownloadOfflineMap)
        {
            try
            {
                //Get Track and distance from GPX
                var t = GPXtoRoute(track.ToRoutes()[0], true);
                string mapTrack = t.Item1;
                float mapDistance_m = t.Item2;
                int ascent = t.Item3;
                int descent = t.Item4;
                List<GPXUtils.Position> LatLon = t.Item5;

                //Clear existing GPX routes from map, else they will be included
                Utils.Misc.ClearTrackRoutesFromMap();

                //Add to map
                AddRouteToMap(mapTrack, GPXType.Track, true);

                //Create a standalone GPX
                var newGPX = new GpxClass()
                {
                    Metadata = new metadataType()
                    {
                        name = track.name,
                        desc = track.desc,
                    },
                };

                //Update track as it might contain Extensions. Not very efficient...
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

                //Create thumbsize map
                string ImageBase64String = CreateThumbprintMap(newGPX);

                //Add to routetrack DB
                GPXDataRouteTrack r = new GPXDataRouteTrack
                {
                    GPXType = GPXType.Track,
                    Name = track.name,
                    Distance = (mapDistance_m / 1000),
                    Ascent = ascent,
                    Descent = descent,
                    Description = track.desc,
                    GPX = newGPX.ToXml(),
                    ImageBase64String = ImageBase64String,
                };
                RouteDatabase.SaveRoute(r);

                //Update RecycleView with new entry, if the fragment exists
                var activity = (FragmentActivity)Platform.CurrentActivity;
                var gpx = activity.SupportFragmentManager.FindFragmentByTag("Fragment_gpx");
                if (gpx != null)
                {
                    _ = Fragment_gpx.mAdapter.mGpxData.Insert(r);
                    Fragment_gpx.mAdapter.NotifyDataSetChanged();
                }

                //Does the user want the maps downloaded?
                if (DownloadOfflineMap)
                {
                    GetloadOfflineMap(track.GetBounds(), r.Id, null, false);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Import - AddGPXTrack()");
            }
        }

        public static void AddGPXRoute(rteType route, bool DownloadOfflineMap)
        {
            try
            {
                //Get Route and distance from GPX
                var t = GPXtoRoute(route, true);
                string mapRoute = t.Item1;
                float mapDistance_km = t.Item2;
                int ascent = t.Item3;
                int descent = t.Item4;
                List<GPXUtils.Position> LatLon = t.Item5;

                //Clear existing GPX routes from map, else they will be included
                Utils.Misc.ClearTrackRoutesFromMap();

                //Add to map
                AddRouteToMap(mapRoute, GPXType.Route, true);

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

                if (DownloadOfflineMap)
                {
                    Elevation.GetElevationData(newGPX);
                }
                //return;

                //Create thumbsize map
                string ImageBase64String = CreateThumbprintMap(newGPX);

                //Add to routetrack DB
                GPXDataRouteTrack r = new GPXDataRouteTrack
                {
                    GPXType = GPXType.Route,
                    Name = route.name,
                    Distance = mapDistance_km / 1000,
                    Ascent = ascent,
                    Descent = descent,
                    Description = route.desc,
                    GPX = newGPX.ToXml(),
                    ImageBase64String = ImageBase64String,
                };
                RouteDatabase.SaveRoute(r);

                //Update RecycleView with new entry, if the fragment exists
                var activity = (FragmentActivity)Platform.CurrentActivity;
                var gpx = activity.SupportFragmentManager.FindFragmentByTag("Fragment_gpx");
                if (gpx != null)
                {
                    _ = Fragment_gpx.mAdapter.mGpxData.Insert(r);
                    Fragment_gpx.mAdapter.NotifyDataSetChanged();
                }

                //Does the user want the maps downloaded?
                if (DownloadOfflineMap)
                {
                    GetloadOfflineMap(route.GetBounds(), r.Id, null, false);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Import - AddGPXRoute()");
            }
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
                Serilog.Log.Error(ex, $"Import - CreateThumbprintMap()");
            }

            return null;
        }

        public static async void GetloadOfflineMap(boundsType bounds, int id, string strFilePath, bool ShowDialog)
        {
            //Progress bar
            LayoutInflater? layoutInflater = LayoutInflater.From(Platform.CurrentActivity);
            View? progressDialogBox = layoutInflater?.Inflate(Resource.Layout.progressbardialog, null);
            AndroidX.AppCompat.App.AlertDialog.Builder alertDialogBuilder = new (Platform.CurrentActivity);
            alertDialogBuilder.SetView(progressDialogBox);
            var progressBar = progressDialogBox?.FindViewById<ProgressBar>(Resource.Id.progressBar);
            if (progressBar != null)
            {
                progressBar.Max = 100;
                progressBar.Progress = 0;
            }
            var progressBarText1 = progressDialogBox?.FindViewById<Google.Android.Material.TextView.MaterialTextView>(Resource.Id.progressBarText1);
            if (progressBarText1 != null)
            {
                progressBarText1.Text = $"{Platform.CurrentActivity.GetString(Resource.String.DownloadTiles)}";
            }
            progressBarText2 = progressDialogBox.FindViewById<Google.Android.Material.TextView.MaterialTextView>(Resource.Id.progressBarText2);
            dialog = alertDialogBuilder.Create();
            dialog.SetCancelable(false);
            dialog.Show();
            UpdatePB uptask = new UpdatePB(progressBar);
            uptask?.Execute(0);

            try
            {
                Models.Map map = new Models.Map
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
                Log.Information($"Done downloading map for {map.Id}");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Import - GetloadOfflineMap()");
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
                    string mapRouteTrack = Import.GPXtoRoute(gpx.Routes[0], false).Item1;

                    //Menus etc not yet created as app not fully initialized. Dirty workaround
                    Import.AddRouteToMap(mapRouteTrack, GPXType.Track, false);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Import - AddTracksToMap()");
            }
        }

        public static void AddPOIToMap()
        {
            try
            {
                List<GPXDataPOI> POIs = POIDatabase.GetPOIAsync().Result;

                if (POIs == null)
                    return;

                if (POIs.Count == 0)
                    return;

                var poiLayer = Fragment_map.map.Layers.FindLayer("Poi").FirstOrDefault();
                if (poiLayer != null)
                {
                    Fragment_map.map.Layers.Remove(poiLayer);
                }

                //Add layer
                var POILayer = new MemoryLayer
                {
                    Name = "Poi",
                    Tag = "poi",
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
                Serilog.Log.Error(ex, $"Import - AddPOIToMap");
            }
        }

        public static IEnumerable<IFeature> ConvertListToInumerable(List<GPXDataPOI> POIs)
        {
            try
            {
                return POIs.Select(c =>
                {
                    var point = SphericalMercator.FromLonLat((double)c.Lon, (double)c.Lat).ToMPoint();
                    var feature = new PointFeature(point);
                    feature["name"] = c.Name;
                    feature["description"] = c.Description;
                    feature["id"] = c.Id;

                    //Icon
                    string svg = "hajk.Images.Black-dot.svg";
                    switch (c.Symbol)
                    {
                        case "Drinking Water":
                            svg = "hajk.Images.Drinking-water.svg";
                            break;
                        case "Campground":
                            svg = "hajk.Images.Tent.svg";
                            break;
                        case "Rogaining":
                            svg = "hajk.Images.RoundFlag.svg";
                            break;
                        case "Man Overboard":
                            svg = "hajk.Images.RoundFlag.svg";
                            break;
                    }

                    //Style
                    feature.Styles.Add(new SymbolStyle
                    {
                        SymbolScale = 1.0f,
                        RotateWithMap = true,
                        BitmapId = Utils.Misc.GetBitmapIdForEmbeddedResource(svg),
                    });

                    return feature;
                });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Import - ConverTListToInumerable()");
            }

            return null;
        }

        public static void AddRouteToMap(string mapRoute, GPXType gpxtype, bool UpdateMenu)
        {
            try
            {
                //Add layer
                ILayer lineStringLayer;
                if (gpxtype == GPXType.Route)
                {
                    lineStringLayer = CreateRouteLayer(mapRoute, Color.Blue, CreateStyle("Blue"));
                    lineStringLayer.Tag = "route";
                }
                else
                {
                    lineStringLayer = CreateRouteLayer(mapRoute, Color.Red, CreateStyle("Red"));
                    lineStringLayer.Tag = "track";
                }
                lineStringLayer.IsMapInfoLayer = true;
                lineStringLayer.Enabled = true;
                Fragment_map.map.Layers.Add(lineStringLayer);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Import - AddRouteToMap()");
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
                Serilog.Log.Error(ex, $"Import - AddRouteToMap()");
            }
        }

        public static (string, float, int, int, List<GPXUtils.Position>) GPXtoRoute(rteType route, bool getAscentDescent)
        {
            int ascent = 0;
            int descent = 0;

            try
            {
                float mapDistance_m = 0.0f;
                var p = new PositionHandler();
                var p2 = new GPXUtils.Position(0, 0, 0);
                List<GPXUtils.Position> ListLatLon = new List<GPXUtils.Position>();

                for (int i = 0; i < route.rtept.Count; i++)
                {
                    ListLatLon.Add(new GPXUtils.Position((double)route.rtept[i].lat, (double)route.rtept[i].lon, 0));

                    var rtePteExt = route.rtept[i].GetExt<RoutePointExtension>();
                    if (rtePteExt != null)
                    {
                        Log.Debug("Route '{0}' has Garmin extension", route.name);

                        for (int j = 0; j < rtePteExt.rpt.Count; j++)
                        {
                            ListLatLon.Add(new GPXUtils.Position((double)rtePteExt.rpt[j].lat, (double)rtePteExt.rpt[j].lon, 0));

                            //Previous leg
                            if (j == 0 && p2.Latitude != 0 && p2.Longitude != 0)
                            {
                                var p1 = new GPXUtils.Position((float)rtePteExt.rpt[j].lat, (float)rtePteExt.rpt[j].lon, 0);
                                mapDistance_m += (float)p.CalculateDistance(p1, p2, DistanceType.Meters);
                            }

                            //First leg
                            if (j == 0)
                            {
                                var p1 = new GPXUtils.Position((float)route.rtept[i].lat, (float)route.rtept[i].lon, 0);
                                p2 = new GPXUtils.Position((float)rtePteExt.rpt[j].lat, (float)rtePteExt.rpt[j].lon, 0);
                                mapDistance_m += (float)p.CalculateDistance(p1, p2, DistanceType.Meters);
                            }

                            //All other legs
                            if (j >= 1)
                            {
                                var p1 = new GPXUtils.Position((float)rtePteExt.rpt[j - 1].lat, (float)rtePteExt.rpt[j - 1].lon, 0);
                                p2 = new GPXUtils.Position((float)rtePteExt.rpt[j].lat, (float)rtePteExt.rpt[j].lon, 0);
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
                            var p1 = new GPXUtils.Position((float)route.rtept[i - 1].lat, (float)route.rtept[i - 1].lon, 0);
                            p2 = new GPXUtils.Position((float)route.rtept[i].lat, (float)route.rtept[i].lon, 0);
                            mapDistance_m += (float)p.CalculateDistance(p1, p2, DistanceType.Meters);
                        }
                    }
                }

                //Convert the list to a string
                string mapRoute = ConvertLatLonListToLineString(ListLatLon);

                //Get elevation data? (Requires internet access)
                if (getAscentDescent && Connectivity.NetworkAccess == Xamarin.Essentials.NetworkAccess.Internet)
                {
                    ListLatLon = GetElevationData(ListLatLon);

                    if (ListLatLon != null)
                    {
                        var a = CalculateAscentDescent(ListLatLon);
                        ascent = a.Item1;
                        descent = a.Item2;
                    }
                }

                return (mapRoute, mapDistance_m, ascent, descent, ListLatLon);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Import - GPXtoRoute()");
                return (null, 0, 0, 0, null);
            }
        }

        private static (int, int) CalculateAscentDescent(List<GPXUtils.Position> LatLonEle)
        {
            double ascent = 0;
            double descent = 0;

            try
            {
                for (int j = 0; j < LatLonEle.Count - 1; j++)
                {
                    var j0 = LatLonEle[j].Elevation;
                    var j1 = LatLonEle[j + 1].Elevation;

                    if (j0 > j1)
                    {
                        descent += j0 - j1;
                    }
                    else
                    {
                        ascent += j1 - j0;
                    }
                }

                return ((int)ascent, (int)descent);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Import - CalculateAscentDescent()");
                return (0, 0);
            }
        }

        private static List<GPXUtils.Position> GetElevationData(List<GPXUtils.Position> ListLatLon)
        {
            List<GPXUtils.Position> ElevationData = new List<GPXUtils.Position>();
            var LatLon = String.Empty;

            try
            {
                for (int i = 0; i < ListLatLon.Count - 1; i++)
                {
                    //Max 100 at a time
                    if ((i != 0) && (i % 100 == 0))
                    {
                        //API is rate limited. Unless this is the first API call, add a sleep now
                        if (i != 100)
                        {
                            System.Threading.Thread.Sleep(1000);
                        }

                        var a = DownloadElevationData(LatLon);

                        //Do we have elevation datae?
                        if (a == null)
                            return ListLatLon;

                        ElevationData = ElevationData.Concat(a).ToList();
                        LatLon = String.Empty;
                    }

                    if (i % 100 != 0)
                    {
                        LatLon += "|";
                    }

                    LatLon += ListLatLon[i].Latitude + "," + ListLatLon[i].Longitude;
                }

                //The rest
                if (LatLon != String.Empty)
                {
                    var a = DownloadElevationData(LatLon);

                    //Do we have elevation datae?
                    if (a == null)
                        return ListLatLon;

                    ElevationData = ElevationData.Concat(a).ToList();
                }

                return ElevationData;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Import - GetElevationData()");
            }

            return ListLatLon;
        }

        private static List<GPXUtils.Position>? DownloadElevationData(string LatLon)
        {
            var Pre = $"https://api.opentopodata.org/v1/aster30m?locations=";
            var Post = $"&interpolation=bilinear&nodata_value=null";
            List<GPXUtils.Position> ElevationData = new List<GPXUtils.Position>();

            try
            {
                //Get the data
                var eleJSON = DownloadElevationDataAsync(Pre + LatLon + Post);

                //Do we have elevation data?
                if (eleJSON == null)
                {
                    return null;
                }

                //Convert from string to JSON
                Models.Elevation.ElevationData elevationData = JsonSerializer.Deserialize<Models.Elevation.ElevationData>(eleJSON);

                //Data is ok?
                if (elevationData?.status != "OK")
                {
                    return null;
                }

                //Any data?
                if (elevationData.results.Count < 1)
                {
                    return null;
                }

                //Convert from JSON to List
                foreach (var result in elevationData.results)
                {
                    var a = new GPXUtils.Position(result.location.lat, result.location.lng, result.elevation);
                    ElevationData.Add(a);
                }

                return ElevationData;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Import - DownloadElevationData()");
            }

            return null;
        }

        private static string DownloadElevationDataAsync(string ElevationUrl)
        {
            HttpClientHandler clientHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
            };
            var _httpClient = new HttpClient(clientHandler)
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            try
            {
                using var httpResponse = _httpClient.GetAsync(ElevationUrl).Result;
                if (httpResponse.StatusCode == HttpStatusCode.OK)
                {
                    return httpResponse.Content.ReadAsStringAsync().Result;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Import - DownloadElevationDataAsync()");
            }

            Log.Error($"DownloadElevationDataAsync(...) failed to download elevation data");
            return null;
        }

        private static string ConvertLatLonListToLineString(List<GPXUtils.Position> ListLatLon)
        {
            var LineString = "LINESTRING(";

            try
            {
                for (int i = 0; i < ListLatLon.Count; i++)
                {
                    if (i == 0)
                    {
                        LineString += ListLatLon[i].Latitude + " " + ListLatLon[i].Longitude;
                    }

                    if (i != 0)
                    {
                        LineString += "," + ListLatLon[i].Latitude + " " + ListLatLon[i].Longitude;
                    }
                }

                LineString += ")";
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Import - ConvertLatLonListToLineString()");
            }

            return LineString;
        }

        public static async Task<GpxClass> PickAndParse()
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
                {
                    return null;
                }

                if (result.FileName.EndsWith("gpx", StringComparison.OrdinalIgnoreCase) == false)
                {
                    return null;
                }

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
                string p = (gpx.Waypoints.Count == 1) ? "POI" : "POIs";

                Show_Dialog msg1 = new Show_Dialog(Platform.CurrentActivity);
                if (await msg1.ShowDialog($"{result.FileName}", $"Found {gpx.Routes.Count} {r}, {gpx.Tracks.Count} {t} and {gpx.Waypoints.Count} {p}. Import?", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) != Show_Dialog.MessageResult.YES)
                {
                    return null;
                }

                return gpx;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Import - PickAndParse()");
            }

            return null;
        }

        public static ILayer CreateRouteLayer(string strRoute, Mapsui.Styles.Color sColor, IStyle? style = null)
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
                    Fill = new Brush { FillStyle = FillStyle.Cross, Color = Color.Red, Background = Color.Transparent },
                    Outline = new Pen { Color = Color.Red, Width = 1.5f }
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
                    Fill = new Brush { FillStyle = FillStyle.Cross, Color = Color.Green, Background = Color.Transparent },
                    Outline = new Pen { Color = Color.Green, Width = 1.5f }
                });
                features.Add(FeatureStart);

                //Add arrow halfway between waypoints and highlight routing points
                var bitmapId = Utils.Misc.GetBitmapIdForEmbeddedResource("hajk.Images.Arrow-up.svg");
                for (int i = 0; i < GPSlineString.NumPoints - 1; i++)
                {
                    //End points for line
                    GPXUtils.Position p1 = new (GPSlineString.Coordinates[i].X, GPSlineString.Coordinates[i].Y, 0);
                    GPXUtils.Position p2 = new (GPSlineString.Coordinates[i + 1].X, GPSlineString.Coordinates[i + 1].Y, 0);

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
                        Fill = new Brush { FillStyle = FillStyle.Solid, Color = sColor, Background = sColor },
                        Outline = new Pen { Color = sColor, Width = 1.5f },
                    });
                    features.Add(FeatureArrow);

                    //Waypoints
                    var FeatureWaypoint = new GeometryFeature { Geometry = lineString.Coordinates[i].ToPoint() };
                    FeatureWaypoint.Styles.Add(new SymbolStyle
                    {
                        SymbolScale = 0.7f,
                        MaxVisible = 2.0f,
                        MinVisible = 0.0f,
                        RotateWithMap = true,
                        SymbolRotation = 0,
                        SymbolType = SymbolType.Ellipse,
                        Fill = new Brush { FillStyle = FillStyle.Dotted, Color = Color.Transparent, Background = Color.Transparent },
                        Outline = new Pen { Color = sColor, Width = 1.5f },
                    });
                    features.Add(FeatureWaypoint);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Import - CreateRouteLayer()");
            }

            return new MemoryLayer
            {
                Features = features,
                Name = "RouteLayer",
                Style = style
            };
        }

        public static ILayer CreateTrackLayer(wptTypeCollection Waypoints, IStyle? style = null)
        {
            var features = new List<IFeature>();

            try
            {
                var lineString = new LineString(Waypoints.Select(v => SphericalMercator.FromLonLat((double)v.lon, (double)v.lat).ToCoordinate()).ToArray());
                features.Add(new GeometryFeature { Geometry = lineString });

                //Waypoint markers
                foreach (var waypoint in lineString.Coordinates)
                {
                    var feature = new PointFeature(new MPoint(waypoint.ToMPoint()));
                    feature.Styles.Add(new SymbolStyle
                    {
                        SymbolScale = 0.2f,
                        MaxVisible = 10.0f,
                        MinVisible = 0.0f,
                        RotateWithMap = true,
                        SymbolType = SymbolType.Ellipse,
                        Fill = new Brush { FillStyle = FillStyle.Dotted, Color = Color.Transparent, Background = Color.Transparent },
                        Outline = new Pen { Color = Color.Red, Width = 0.2f },
                    });
                    features.Add(feature);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Import - CreateTrackLayer()");
            }

            return new MemoryLayer
            {
                Features = features,
                Name = "TrackLayer",
                Style = style
            };
        }

        public static IStyle CreateStyle(string? colour)
        {
            return new VectorStyle
            {
                Fill = null,
                Outline = null,
                Line = { Color = Color.FromString(colour), Width = 4, PenStyle = PenStyle.Solid }
            };
        }

        public class UpdatePB : AsyncTask<int, int, string>
        {
            readonly ProgressBar mpb;

            public UpdatePB(ProgressBar pb)
            {
                this.mpb = pb;
            }

            protected override string RunInBackground(params int[] @params)
            {
                if (OperatingSystem.IsAndroidVersionAtLeast(24))
                {
                    while (progress < 100)
                    {
                        mpb.SetProgress(progress, false);
                    }
                }

                Import.dialog?.Cancel();
                return "finish";
            }
        }
    }
}
