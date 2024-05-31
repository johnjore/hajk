using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AndroidX.Fragment;
using AndroidX.Fragment.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Graphics;
using System.Threading;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Android.Content.Res;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using AndroidX.Core.View;
using AndroidX.DrawerLayout.Widget;
using Serilog;
using Xamarin.Essentials;
using hajk.Data;
using hajk.Adapter;
using hajk.Fragments;
using GPXUtils;
using SharpGPX.GPX1_1;
using Microcharts;
using Microcharts.Droid;
using SkiaSharp;

namespace hajk.Fragments
{
    public class Fragment_posinfo : AndroidX.Fragment.App.Fragment
    {
        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            try
            {
                var activity = (FragmentActivity)MainActivity.mContext;
                var view = inflater.Inflate(Resource.Layout.fragment_posinfo, container, false);
                view.SetBackgroundColor(Color.White);

                Button hideFragment = view.FindViewById<Button>(Resource.Id.btn_HideFragment);
                hideFragment.Click += delegate
                {
                    var activity = (FragmentActivity)MainActivity.mContext;
                    activity.SupportFragmentManager.BeginTransaction()
                        .Remove((AndroidX.Fragment.App.Fragment)activity.SupportFragmentManager.FindFragmentByTag("Fragment_posinfo"))
                        .Commit();
                    activity.SupportFragmentManager.ExecutePendingTransactions();
                };


                //Make sure data does not change while calculating values
                Xamarin.Essentials.Location GpsLocation = new GPSLocation().GetGPSLocationData();
                GPXUtils.Position MapPosition = Fragment_map.MapPosition;

                //Current Elevation (Altitude)
                try
                {
                    double ele = Math.Round((double)GpsLocation.Altitude);
                    view.FindViewById<TextView>(Resource.Id.CurrentElevation_m).Text = "Current Elevation: " + ele.ToString("N0") + "m";
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "posinfo - Crashed calculating 'CurrentElevation_m'");
                }

                //Elevation at MapPosition (Altitude)
                try
                {
                    rteType route = MainActivity.ActiveRoute.Routes.First();

                    //MapPoint closest to Route. Distance should be 0... Can't we find this quicker by looking for LatLng in the route?
                    var r1 = MapInformation.FindClosestWayPoint(route, new GPXUtils.Position(MapPosition.Latitude, MapPosition.Longitude, 0));
                    var route_index_end = r1.Item2;

                    var p = route.rtept[route_index_end];

                    view.FindViewById<TextView>(Resource.Id.ElevationMap_m).Text = "MapPoint Elevation: " + p.ele.ToString("N0") + "m";
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "posinfo - Crashed calculating 'ElevationMap_m'");
                }

                //Distance Straight Line from GPSLocation to MapPosition
                try
                {
                    var DistanceStraightLine_m = (new PositionHandler().CalculateDistance(MapPosition, GpsLocation, DistanceType.Meters));

                    string v = Utils.Misc.KMvsM(DistanceStraightLine_m);
                    view.FindViewById<TextView>(Resource.Id.DistanceStraightLine_m).Text = "GPS to Map: " + v;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "posinfo - Crashed calculating 'DistanceStraightLine_m'");
                }

                //Ascent, Descent and Distance Along Route From GPSLocation to MapPosition
                try
                {
                    if (MainActivity.ActiveRoute != null)
                    {
                        rteType route = MainActivity.ActiveRoute.Routes.First();

                        /**/
                        //MapPoint closest to Route. Distance should be 0... Can't we find this quicker by looking for LatLng in the route?
                        var r2 = MapInformation.FindClosestWayPoint(route, new GPXUtils.Position(MapPosition.Latitude, MapPosition.Longitude, 0));
                        var route_index_end = r2.Item2;

                        //WayPoint we are closest to
                        var r1 = MapInformation.FindClosestWayPoint(route, new GPXUtils.Position(GpsLocation.Latitude, GpsLocation.Longitude, 0));
                        var p1 = r1.Item1;
                        var route_index_start = r1.Item2;

                        var a = GPXUtils.GPXUtils.CalculateElevationDistanceData(route.rtept, route_index_start, route_index_end - 1);
                        var AscentGPSLocationMapLocation_m = a.Item1;
                        var DescentGPSLocationMapLocation_m = a.Item2;
                        var DistanceGPSLocationMapLocation_m = a.Item3;

                        //Add distance from GPSLocation to first waypoint. We might not be on-top of it. If we are, distance should be 0...
                        DistanceGPSLocationMapLocation_m += (int)(new PositionHandler().CalculateDistance(p1, GpsLocation, DistanceType.Meters));
                        string v = Utils.Misc.KMvsM(DistanceGPSLocationMapLocation_m);

                        view.FindViewById<TextView>(Resource.Id.AscentGPSLocationMapLocation_m).Text = "Ascent From GPS to Map: " + AscentGPSLocationMapLocation_m.ToString("N0") + "m";
                        view.FindViewById<TextView>(Resource.Id.DescentGPSLocationMapLocation_m).Text = "Descent From GPS to Map: " + DescentGPSLocationMapLocation_m.ToString("N0") + "m";
                        view.FindViewById<TextView>(Resource.Id.DistanceGPSLocationMapLocation_m).Text = "Distance From GPS to Map: " + v;
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "posinfo - Crashed calculating 'Ascent, Descent and Distance from GPS Location to Map Position'");
                }


                //Distance Along Route to MapPosition from Start
                if ((Preferences.Get("RecordingTrack", false) == true) && (RecordTrack.trackGpx.Waypoints.Count > 0))
                {
                    //Duration since first recording
                    try
                    {
                        view.FindViewById<TextView>(Resource.Id.DurationTime).Text = "Duration: " + (DateTime.Now - RecordTrack.trackGpx.Waypoints.First().time).ToString(@"hh\:mm\:ss");
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "posinfo - Crashed calculating 'DurationTime'");
                    }

                    //Distance Straight Line from Start to Map Position
                    try
                    {
                        var PositionStart = new GPXUtils.Position((double)RecordTrack.trackGpx.Waypoints.First().lat, (double)RecordTrack.trackGpx.Waypoints.First().lon, 0);

                        var DistanceStraightLine_m = (new PositionHandler().CalculateDistance(MapPosition, PositionStart, DistanceType.Meters));
                        string v = Utils.Misc.KMvsM(DistanceStraightLine_m);

                        view.FindViewById<TextView>(Resource.Id.DistanceStraightLineFromStart_m).Text = "Straight Distance From Start To Map: " + v;
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "posinfo - Crashed calculating 'DistanceStraightLineFromStart_m'");
                    }

                    //Distance Straight Line from Start to Current Position
                    try
                    {
                        var PositionStart = new GPXUtils.Position((double)RecordTrack.trackGpx.Waypoints.First().lat, (double)RecordTrack.trackGpx.Waypoints.First().lon, 0);

                        var DistanceStraightLine_m = (new PositionHandler().CalculateDistance(PositionStart, GpsLocation, DistanceType.Meters));
                        string v = Utils.Misc.KMvsM(DistanceStraightLine_m);

                        view.FindViewById<TextView>(Resource.Id.DistanceStraightLineFromStartGPS_m).Text = "Striaght Distance From Start To GPS: " + v;
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "posinfo - Crashed calculating 'DistanceStraightLineFromStartGPS_m'");
                    }

                    //Calculate Ascent / Descent from Start to Current Position
                    try
                    {
                        var a = GPXUtils.GPXUtils.CalculateElevationDistanceData(RecordTrack.trackGpx.Waypoints, 0, RecordTrack.trackGpx.Waypoints.Count);
                        int TrackAscentFromStart_m = a.Item1;
                        int TrackDescentFromStart_m = a.Item2;
                        int TrackDistanceFromStart_m = a.Item3;
                        string v = Utils.Misc.KMvsM(TrackDistanceFromStart_m);
                        Serilog.Log.Debug("Item3: '" + a.Item3.ToString() + "', TrackDistanceFromStart_m: '" + TrackDistanceFromStart_m.ToString() + "', v: '" + v + "'");

                        view.FindViewById<TextView>(Resource.Id.AscentFromStart_m).Text = "Ascent From Start To GPS: " + TrackAscentFromStart_m.ToString("N0") + "m";
                        view.FindViewById<TextView>(Resource.Id.DescentFromStart_m).Text = "Descent From Start To GPS: " + TrackDescentFromStart_m.ToString("N0") + "m";
                        view.FindViewById<TextView>(Resource.Id.TrackDistanceFromStart_m).Text = "Distance From Start To GPS: " + v;
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "posinfo - Crashed calculating 'Ascent / Descent'");
                    }
                }

                if ((Preferences.Get("RecordingTrack", false) == false) || (RecordTrack.trackGpx.Waypoints.Count <= 0))
                {
                    try
                    {
                        view.FindViewById<TextView>(Resource.Id.DurationTime).Text = "n/a";
                        view.FindViewById<TextView>(Resource.Id.DistanceStraightLineFromStart_m).Text = "n/a";
                        view.FindViewById<TextView>(Resource.Id.DistanceStraightLineFromStartGPS_m).Text = "n/a";
                        view.FindViewById<TextView>(Resource.Id.AscentFromStart_m).Text = "n/a";
                        view.FindViewById<TextView>(Resource.Id.DescentFromStart_m).Text = "n/a";
                        view.FindViewById<TextView>(Resource.Id.TrackDistanceFromStart_m).Text = "n/a";
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "posinfo - Crashed while setting empty values");
                    }
                }

                ConfigureGraph(view, GpsLocation, MapPosition);

                return view;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Fragment_posinfo Crashed");
            }

            return null;
        }

        private void ConfigureGraph(View view, Xamarin.Essentials.Location GpsLocation, GPXUtils.Position MapPosition)
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
                var r2 = MapInformation.FindClosestWayPoint(route, new GPXUtils.Position(MapPosition.Latitude, MapPosition.Longitude, 0));
                var map_index = r2.Item2;

                //WayPoint we are closest to
                var r1 = MapInformation.FindClosestWayPoint(route, new GPXUtils.Position(GpsLocation.Latitude, GpsLocation.Longitude, 0));
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
                        var t = Import.GPXtoRoute(route, false);
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
                Serilog.Log.Error(ex, "posinfo - Crashed while creating elevation graph");
            }
        }
    }
}
