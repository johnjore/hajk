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
                    return view;

                //Locations in Position format for where we are, and were we pointed at the map
                GPXUtils.Position? GpsPosition = new GPXUtils.Position(GpsLocation.Latitude, GpsLocation.Longitude, 0, false, null);
                GPXUtils.Position? MapPosition = Fragment_map.GetMapPressedCoordinates();


                //Current Elevation (Altitude) - GPS
                string b = "Current Elevation (GPS): ";
                try
                {
                    if (GpsLocation.HasAltitude)
                    {
                        view.FindViewById<TextView>(Resource.Id.CurrentElevationGPS_m).Text = b + Math.Round((double)GpsLocation.Altitude).ToString("N0") + "m";
                    }
                    else
                    {
                        view.FindViewById<TextView>(Resource.Id.CurrentElevationGPS_m).Text = b + "N/A";
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, "posinfo - Crashed calculating 'CurrentElevationGPS_m'");
                    view.FindViewById<TextView>(Resource.Id.CurrentElevationGPS_m).Text = b + "N/A";
                }

                //Lookup Elevation at MapPosition (Altitude) - Map
                b = "Current Elevation (Map): ";
                try
                {
                    var mapElevation = Elevation.LookupElevationData(GpsPosition);
                    view.FindViewById<TextView>(Resource.Id.CurrentElevationMap_m).Text = b + mapElevation.ToString("N0") + "m";
                }
                catch (Exception ex)
                {
                    view.FindViewById<TextView>(Resource.Id.CurrentElevationMap_m).Text = b + "N/A";
                    Serilog.Log.Fatal(ex, "posinfo - Crashed calculating 'CurrentElevationMap_m'");
                }

                //Lookup Elevation at MapPosition (Altitude)
                b = "Elevation at selected point: ";
                try
                {
                    var mapElevation = Elevation.LookupElevationData(MapPosition);
                    view.FindViewById<TextView>(Resource.Id.ElevationMap_m).Text = b + mapElevation.ToString("N0") + "m";
                }
                catch (Exception ex)
                {
                    view.FindViewById<TextView>(Resource.Id.ElevationMap_m).Text = b + "N/A";
                    Serilog.Log.Fatal(ex, "posinfo - Crashed calculating 'ElevationMap_m'");
                }

                //Distance Straight Line from GPSLocation to MapPosition
                b = "Distance - Direct - to selected point: ";
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
                }

                //Ascent, Descent and Distance Along Route From GPSLocation to MapPosition
                try
                {
                    view.FindViewById<TextView>(Resource.Id.AscentGPSLocationMapLocation_m).Text = "Ascent From here to Map: Not Following a Route!";
                    view.FindViewById<TextView>(Resource.Id.DescentGPSLocationMapLocation_m).Text = "Descent From here to Map: Not Following a Route!";
                    view.FindViewById<TextView>(Resource.Id.DistanceGPSLocationMapLocation_m).Text = "Distance From here to Map: Not Following a Route!";

                    if (MainActivity.ActiveRoute != null)
                    {
                        rteType route = MainActivity.ActiveRoute.Routes.First();



                        /**/
                        //MapPoint closest to Route. Distance should be 0... Can't we find this quicker by looking for LatLng in the route?
                        //MapPoint should already know the item pressed?
                        //Optimizing map, removes a lot of points - But does this really matter?
                        (var m1, var route_index_end) = MapInformation.FindClosestWayPoint(route, MapPosition);

                        //WayPoint we are closest to?
                        (var p1, var route_index_start) = MapInformation.FindClosestWayPoint(route, GpsPosition);

                        //Get how much Ascent, Descent and Distance along the track to the MapPoint
                        (var AscentGPSLocationMapLocation_m, var DescentGPSLocationMapLocation_m, var DistanceGPSLocationMapLocation_m)= GPXUtils.GPXUtils.CalculateElevationDistanceData(route.rtept, route_index_start, route_index_end);

                        //Add distance from GPSLocation to first waypoint. We might not be on-top of it. If we are, distance should be 0...
                        DistanceGPSLocationMapLocation_m += (int)(new PositionHandler().CalculateDistance(p1, GpsLocation, DistanceType.Meters));

                        //Add distance from m1 to MapPosition
                        DistanceGPSLocationMapLocation_m += (int)(new PositionHandler().CalculateDistance(m1, MapPosition, DistanceType.Meters));


                        /**///What about elevation changes for m1->e1 and GPS->p1 ?
                        //Elevation changes between route index's?


                        //Show the values
                        view.FindViewById<TextView>(Resource.Id.DistanceGPSLocationMapLocation_m).Text = "Distance - Route - to selected point: " + Utils.Misc.KMvsM(DistanceGPSLocationMapLocation_m);
                        view.FindViewById<TextView>(Resource.Id.AscentGPSLocationMapLocation_m).Text = "Ascent - Route - From here to selected point: " + AscentGPSLocationMapLocation_m.ToString("N0") + "m";
                        view.FindViewById<TextView>(Resource.Id.DescentGPSLocationMapLocation_m).Text = "Descent - Route - From here to seleced point: " + DescentGPSLocationMapLocation_m.ToString("N0") + "m";
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, "posinfo - Crashed calculating 'Ascent, Descent and Distance from GPS Location to Map Position'");
                }

                //Data from start of walk to GpsPosition
                view.FindViewById<TextView>(Resource.Id.RecordedWaypoints).Text = $"Waypoints: {RecordTrack.trackGpx.Waypoints.Count.ToString()}";
                view.FindViewById<TextView>(Resource.Id.DurationTime).Text = "Duration: n/a or not recording";
                view.FindViewById<TextView>(Resource.Id.AscentFromStart_m).Text = "Ascent: n/a or not recording";
                view.FindViewById<TextView>(Resource.Id.DescentFromStart_m).Text = "Descent: n/a or not recording";
                view.FindViewById<TextView>(Resource.Id.TrackDistanceFromStart_m).Text = "Distance: n/a or not recording";
                
                if ((Preferences.Get("RecordingTrack", false) == true) && (RecordTrack.trackGpx.Waypoints.Count > 0))
                {
                    //Timespan since first recording
                    try
                    {
                        view.FindViewById<TextView>(Resource.Id.DurationTime).Text = "Time since start of recording: " + (DateTime.Now - RecordTrack.trackGpx.Waypoints.First().time).ToString(@"hh\:mm\:ss");
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Fatal(ex, "posinfo - Crashed calculating 'DurationTime'");
                    }

                    //Calculate Ascent / Descent from Start to Current Position
                    try
                    {
                        (int TrackAscentFromStart_m, int TrackDescentFromStart_m, int TrackDistanceFromStart_m) = GPXUtils.GPXUtils.CalculateElevationDistanceData(RecordTrack.trackGpx.Waypoints, 0, RecordTrack.trackGpx.Waypoints.Count);
                        string v = Utils.Misc.KMvsM(TrackDistanceFromStart_m);
                        Serilog.Log.Debug($"TrackDistanceFromStart_m: '{TrackDistanceFromStart_m.ToString()}', v: '{v}', TrackAscentFromStart_m: '{TrackAscentFromStart_m.ToString()}', TrackDescentFromStart_m: '{TrackDescentFromStart_m}'");


                        view.FindViewById<TextView>(Resource.Id.AscentFromStart_m).Text = "Walked - Ascent: " + TrackAscentFromStart_m.ToString("N0") + "m";
                        view.FindViewById<TextView>(Resource.Id.DescentFromStart_m).Text = "Walked - Descent: " + TrackDescentFromStart_m.ToString("N0") + "m";
                        view.FindViewById<TextView>(Resource.Id.TrackDistanceFromStart_m).Text = "Walked - Distance: " + v;
                        
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Fatal(ex, "posinfo - Crashed calculating 'Ascent / Descent'");
                    }
                }

                ConfigureGraph(view, GpsLocation, MapPosition);

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
    }
}
