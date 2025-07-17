using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Net.Vcn;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using AndroidX.Core.View;
using AndroidX.DrawerLayout.Widget;
using AndroidX.Fragment;
using AndroidX.Fragment.App;
using GeoTiffCOG;
using GPXUtils;
using hajk.Adapter;
using hajk.Data;
using hajk.Fragments;
using Java.Nio.FileNio.Attributes;
using Mapsui.Projections;
using Microcharts;
using Microcharts.Droid;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Serilog;
using SharpCompress.Compressors.RLE90;
using SharpGPX;
using SharpGPX.GPX1_1;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Android.Telephony.CarrierConfigManager;

namespace hajk.Fragments
{
    public class Fragment_posinfo : AndroidX.Fragment.App.Fragment
    {
        public override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        public override Android.Views.View? OnCreateView(LayoutInflater? inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            try
            {
                var activity = (FragmentActivity)Platform.CurrentActivity;
                var view = inflater?.Inflate(Resource.Layout.fragment_posinfo, container, false);
                view?.SetBackgroundColor(Android.Graphics.Color.White);

                Android.Widget.Button? hideFragment = view?.FindViewById<Android.Widget.Button>(Resource.Id.btn_HideFragment);
                hideFragment.Click += delegate
                {
                    var activity = (FragmentActivity)Platform.CurrentActivity;
                    activity.SupportFragmentManager.BeginTransaction()
                        .Remove((AndroidX.Fragment.App.Fragment)activity.SupportFragmentManager.FindFragmentByTag("Fragment_posinfo"))
                        .Commit();
                    activity.SupportFragmentManager.ExecutePendingTransactions();
                };

                //Make sure data does not change while calculating values
                Android.Locations.Location? GpsLocation = LocationForegroundService.GetLocation();

                //Debug - Remove me
                //if (GpsLocation == null) { GpsLocation = new Android.Locations.Location("manual"){Latitude = -37.80818, Longitude = 144.88439, Altitude = 99, };}

                if (GpsLocation == null)
                {
                    view.FindViewById<TextView>(Resource.Id.CurrentElevation_m).Text = "No GPS Position";
                    return view;
                }

                //Locations in Position format for where we are, and were we pointed at the map
                GPXUtils.Position? GpsPosition = new GPXUtils.Position(GpsLocation.Latitude, GpsLocation.Longitude, 0, false, null);
                GPXUtils.Position? MapPosition = Fragment_map.GetMapPressedCoordinates();

                //Elevation (Altitude)
                try
                {
                    //From GPS
                    string GPSAltitude = "N/A";
                    if (GpsLocation.HasAltitude)
                    {
                        GPSAltitude = Math.Round((double)GpsLocation.Altitude).ToString("N0") + "m";
                    }

                    //From GeoTiff
                    decimal mapElevation = Elevation.LookupElevationData(GpsPosition);
                    string MapAltitude = (mapElevation >= 0) ? mapElevation.ToString("N0") + "m" : "N/A";

                    //From pointed finger
                    decimal PointElevation = Elevation.LookupElevationData(MapPosition);
                    string PointAltitude = (PointElevation >= 0) ? PointElevation.ToString("N0") + "m" : "N/A";
                    
                    //Update GUI
                    view.FindViewById<TextView>(Resource.Id.CurrentElevation_m).Text = $"{char.ConvertFromUtf32(0x1f5fb)} - {char.ConvertFromUtf32(0x1f4cd)} (GPS/Map): {GPSAltitude} / {MapAltitude} - {char.ConvertFromUtf32(0x1f4cc)}: {PointAltitude}";
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, "posinfo - Crashed calculating 'CurrentElevation_m'");
                }

                /*
                //Distance Straight Line from GPSLocation to MapPosition
                string b = "Distance - Direct - to selected point: ";
                try
                {
                    var DistanceStraightLine_m = (new PositionHandler().CalculateDistance(MapPosition, GpsLocation, DistanceType.Meters));
                    string v = Utils.Misc.KMvsM(DistanceStraightLine_m);
                    view.FindViewById<TextView>(Resource.Id.DistanceStraightLine_m).Text = b + v;
                }
                catch (Exception ex)
                {
                    view.FindViewById<TextView>(Resource.Id.DistanceStraightLine_m).Text = b + "N/A";
                    Serilog.Log.Fatal(ex, "posinfo - Crashed calculating 'DistanceStraightLine_m'");
                }*/

                //Distance, Ascent and Descent following Route From current GPSLocation to MapPosition
                try
                {
                    view.FindViewById<TextView>(Resource.Id.MapPosition).Text = "Not Following a Route!";

                    if (MainActivity.ActiveRoute != null)
                    {
                        rteType route = MainActivity.ActiveRoute.Routes.First();

                        /**/
                        //MapPoint closest to Route. Distance should be 0... Can't we find this quicker by looking for LatLng in the route?
                        //MapPoint should already know the item pressed?
                        //Optimizing map, removes a lot of points - But does this really matter?
                        (var m1, var route_index_end) = MapInformation.FindClosestWayPoint(route, MapPosition);
                        Serilog.Log.Information($"m1: {m1} - route_index_end: {route_index_end}");
                        
                        //WayPoint we are closest to?
                        (var p1, var route_index_start) = MapInformation.FindClosestWayPoint(route, GpsPosition);
                        Serilog.Log.Information($"m1: {p1} - route_index_start: {route_index_start}");

                        //Get how much Ascent, Descent and Distance along the track to the MapPoint
                        (var AscentGPSLocationMapLocation_m, var DescentGPSLocationMapLocation_m, var DistanceGPSLocationMapLocation_m)= GPXUtils.GPXUtils.CalculateElevationDistanceData(route.rtept, route_index_start, route_index_end);
                        Serilog.Log.Information($"AscentGPSLocationMapLocation_m: {AscentGPSLocationMapLocation_m} - DescentGPSLocationMapLocation_m: {DescentGPSLocationMapLocation_m} - DistanceGPSLocationMapLocation_m: {DistanceGPSLocationMapLocation_m}");

                        //Add distance from GPSLocation to first waypoint. We might not be on-top of it. If we are, distance should be 0...
                        Serilog.Log.Information($"DistanceGPSLocationMapLocation_m - 1: {DistanceGPSLocationMapLocation_m}");
                        DistanceGPSLocationMapLocation_m += (int)(new PositionHandler().CalculateDistance(p1, GpsLocation, DistanceType.Meters));
                        Serilog.Log.Information($"DistanceGPSLocationMapLocation_m - 2: {DistanceGPSLocationMapLocation_m}");

                        //Add distance from m1 to MapPosition
                        DistanceGPSLocationMapLocation_m += (int)(new PositionHandler().CalculateDistance(m1, MapPosition, DistanceType.Meters));
                        Serilog.Log.Information($"DistanceGPSLocationMapLocation_m - 3: {DistanceGPSLocationMapLocation_m}");

                        /**///What about elevation changes for m1->e1 and GPS->p1 ?
                            //Elevation changes between route index's?

                        //Show the values
                        var mapPositionText = $"{'\u27f7'} {Utils.Misc.KMvsM(DistanceGPSLocationMapLocation_m)} / " +
                                              $"{'\u25B2'} {AscentGPSLocationMapLocation_m:N0}m / " +
                                              $"{'\u25BC'} {DescentGPSLocationMapLocation_m:N0}m";
                        view.FindViewById<TextView>(Resource.Id.MapPosition).Text = mapPositionText;
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, "posinfo - Crashed calculating 'Ascent, Descent and Distance from GPS Location to Map Position'");
                }

                //Data from start of walk to GpsPosition
                view.FindViewById<TextView>(Resource.Id.RecordedWaypointsAndTime).Text = $"Waypoints: {RecordTrack.trackGpx.Waypoints.Count.ToString()} / Duration: n/a or not recording";
                var CompletedText = $"{char.ConvertFromUtf32(0x1f7e2)} N/A / " +
                                    $"{char.ConvertFromUtf32(0x1f53c)} N/A / " +
                                    $"{char.ConvertFromUtf32(0x1f53d)} N/A";
                view.FindViewById<TextView>(Resource.Id.Completed).Text = CompletedText;

                //If recording
                if ((Preferences.Get("RecordingTrack", false) == true) && (RecordTrack.trackGpx.Waypoints.Count > 0))
                {
                    try
                    {
                        var RecordedWaypointAndTime = $"Waypoints: {RecordTrack.trackGpx.Waypoints.Count.ToString()} / " +
                            "Duration: " + (DateTime.Now - RecordTrack.trackGpx.Waypoints.First().time).ToString(@"hh\:mm\:ss");
                        view.FindViewById<TextView>(Resource.Id.RecordedWaypointsAndTime).Text = RecordedWaypointAndTime;
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Fatal(ex, "posinfo - Crashed calculating 'DurationTime'");
                    }

                    //Calculate Ascent / Descent from Start to Current Position
                    try
                    {
                        (int TrackAscentFromStart_m, int TrackDescentFromStart_m, int TrackDistanceFromStart_m) = GPXUtils.GPXUtils.CalculateElevationDistanceData(RecordTrack.trackGpx.Waypoints, 0, RecordTrack.trackGpx.Waypoints.Count-1);
                        string v = Utils.Misc.KMvsM(TrackDistanceFromStart_m);
                        Serilog.Log.Debug($"TrackDistanceFromStart_m: '{TrackDistanceFromStart_m.ToString()}', v: '{v}', TrackAscentFromStart_m: '{TrackAscentFromStart_m.ToString()}', TrackDescentFromStart_m: '{TrackDescentFromStart_m}'");

                        CompletedText = $"{char.ConvertFromUtf32(0x1f7e2)} {v} / " +
                                        $"{char.ConvertFromUtf32(0x1f53c)} {TrackAscentFromStart_m:N0}m / " +
                                        $"{char.ConvertFromUtf32(0x1f53d)} {TrackDescentFromStart_m:N0}m";
                        view.FindViewById<TextView>(Resource.Id.Completed).Text = CompletedText;
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Fatal(ex, "posinfo - Crashed calculating Completed 'Distance / Ascent / Descent'");
                    }
                }

                ConfigureGraph(view, GpsLocation, MapPosition);

                ConfigureGraphDone(view);

                return view;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Fragment_posinfo Crashed");
            }

            return null;
        }

        private void ConfigureGraph(Android.Views.View view, Android.Locations.Location GpsLocation, GPXUtils.Position MapPosition)
        {
            try
            {
                var chartView = view.FindViewById<ChartView>(Resource.Id.chartElevation);
                chartView.Visibility = ViewStates.Gone;

                if (MainActivity.ActiveRoute == null)
                    return;

                var route = MainActivity.ActiveRoute.Routes.First();
                if (route == null)
                    return;

                if (route.rtept.Count == 0)
                    return;

                /**/
                //MapPoint closest to Route. Distance should be 0... Can't we find this quicker by looking for LatLng in the route?
                var r2 = MapInformation.FindClosestWayPoint(route, new GPXUtils.Position(MapPosition.Latitude, MapPosition.Longitude, 0, false, null));
                var map_index = r2.Item2;

                //WayPoint we are closest to
                var r1 = MapInformation.FindClosestWayPoint(route, new GPXUtils.Position(GpsLocation.Latitude, GpsLocation.Longitude, 0, false, null));
                var gps_index = r1.Item2;

                List<ChartEntry> entries = new List<ChartEntry>();

                //Entries
                SKColor Color = SKColor.Parse("#00ff00"); //Green
                for (int i = 0; i < route.rtept.Count; i++)
                {
                    var entry = new ChartEntry((float)route.rtept[i].ele)
                    {
                        Color = Color,
                    };

                    //If GPS Position, change to Red and add label
                    if (i == gps_index)
                    {
                        Color = SKColor.Parse("#ff0000"); //Red
                        entry.Color = Color;
                        entry.Label = "G";
                        entry.TextColor = SKColor.Parse("#000000"); //Black
                    }

                    //If Map Position, add label
                    if (i == map_index)
                    {
                        Color = SKColor.Parse("#800080"); //Purple
                        entry.Color = Color;
                        entry.Label = "M";
                        entry.TextColor = SKColor.Parse("#000000"); //Black
                    }

                    //Start
                    if (i == 0)
                    {
                        entry.Label = "0";
                        entry.TextColor = SKColor.Parse("#000000"); //Black
                    }

                    //End
                    if (i == route.rtept.Count - 1)
                    {
                        var t = Import.ParseGPXtoRoute(route);
                        entry.Label = (t.Item2 / 1000).ToString("N1");
                        entry.TextColor = SKColor.Parse("#000000"); //Purple
                    }

                    entries.Add(entry);
                }

                //Chart configuration
                const string text = "0";
                var typeface = SKFontManager.Default.MatchCharacter(text[0]);
                var a = SKFontManager.Default;

                var chart = new LineChart
                {
                    LineMode = LineMode.Straight,
                    LineSize = 5,
                    PointMode = PointMode.None,
                    AnimationDuration = TimeSpan.FromSeconds(0),
                    LabelOrientation = Microcharts.Orientation.Vertical, //Change to Horizontal when support for X axis is done
                    LabelTextSize = 40.0f,
                    LabelColor = SKColor.Parse("#000000"),
                    Margin = 10,
                    ShowYAxisLines = true,
                    ShowYAxisText = true,
                    YAxisTextPaint = new SKPaint()
                    {
                        Typeface = SKTypeface.Default,
                        TextSize = 32.0f,
                        TextAlign = SKTextAlign.Left,
                    },
                    Entries = entries
                };

                //Set the Chart
                chartView.Chart = chart;
                chartView.Visibility = ViewStates.Visible;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "posinfo - Crashed while creating elevation graph");
            }
        }

        private void ConfigureGraphDone(Android.Views.View view)
        {
            try
            {
                var chartView = view.FindViewById<ChartView>(Resource.Id.chartElevationDone);
                chartView.Visibility = ViewStates.Gone;

                if (MainActivity.ActiveRoute == null)
                    return;

                var route = MainActivity.ActiveRoute.Routes.First();
                if (route == null)
                    return;

                if (route.rtept.Count == 0)
                    return;

                if (RecordTrack.trackGpx.Waypoints.Count == 0)
                    return;

                List<ChartEntry> entries = new List<ChartEntry>();

                //Entries
                for (int i = 0; i < RecordTrack.trackGpx.Waypoints.Count; i++)
                {
                    var entry = new ChartEntry((float)RecordTrack.trackGpx.Waypoints[i].ele)
                    {
                        Color = SKColor.Parse("#00ff00"), //Green
                    };

                    //Start
                    if (i == 0)
                    {
                        entry.Label = "0";
                        entry.TextColor = SKColor.Parse("#000000"); //Black
                    }

                    //End
                    if (i == RecordTrack.trackGpx.Waypoints.Count - 1)
                    {
                        var (mapRoute, mapDistance_m, ListLatLonEle) = Import.ParseGPXtoRoute(route);
                        entry.Label = (mapDistance_m / 1000).ToString("N1");
                        entry.TextColor = SKColor.Parse("#000000"); //Purple
                    }

                    entries.Add(entry);
                }

                //Chart configuration
                const string text = "0";
                var typeface = SKFontManager.Default.MatchCharacter(text[0]);
                var a = SKFontManager.Default;

                var chart = new LineChart
                {
                    LineMode = LineMode.Straight,
                    LineSize = 5,
                    PointMode = PointMode.None,
                    AnimationDuration = TimeSpan.FromSeconds(0),
                    LabelOrientation = Microcharts.Orientation.Vertical, //Change to Horizontal when support for X axis is done
                    LabelTextSize = 40.0f,
                    LabelColor = SKColor.Parse("#000000"),
                    Margin = 10,
                    ShowYAxisLines = true,
                    ShowYAxisText = true,
                    YAxisTextPaint = new SKPaint()
                    {
                        Typeface = SKTypeface.Default,
                        TextSize = 32.0f,
                        TextAlign = SKTextAlign.Left,
                    },
                    Entries = entries
                };

                //Set the Chart
                chartView.Chart = chart;
                chartView.Visibility = ViewStates.Visible;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "posinfo - Crashed while creating elevation graph");
            }
        }
    }
}
