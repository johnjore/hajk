
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment;
using AndroidX.Fragment.App;
using GPXUtils;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Xamarin.Android;
using Serilog;
using SharpGPX;
using SharpGPX.GPX1_1;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
                Stopwatch sw = Stopwatch.StartNew();

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
                string DistanceTravelled = "Distance, km";
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
                        (int TrackAscentFromStart_m, int TrackDescentFromStart_m, int TrackDistanceFromStart_m) = GPXUtils.GPXUtils.CalculateElevationDistanceData(RecordTrack.trackGpx.Waypoints, 0, RecordTrack.trackGpx.Waypoints.Count - 1);
                        DistanceTravelled = (TrackDistanceFromStart_m / 1000).ToString("N2") + "km";
                        Serilog.Log.Debug($"TrackDistanceFromStart_m: '{TrackDistanceFromStart_m.ToString()}', DistanceTravelled: '{DistanceTravelled}', TrackAscentFromStart_m: '{TrackAscentFromStart_m.ToString()}', TrackDescentFromStart_m: '{TrackDescentFromStart_m}'");

                        CompletedText = $"{char.ConvertFromUtf32(0x1f7e2)} {DistanceTravelled:N2} / " +
                                        $"{char.ConvertFromUtf32(0x1f53c)} {TrackAscentFromStart_m:N0}m / " +
                                        $"{char.ConvertFromUtf32(0x1f53d)} {TrackDescentFromStart_m:N0}m";
                        view.FindViewById<TextView>(Resource.Id.Completed).Text = CompletedText;


                        //Graph
                        var (series1, MinX1, MaxX1, distance1) = CreateSeries(RecordTrack.trackGpx?.Waypoints, "#4CAF50", "#00FFFFFF", 0.0, 0, RecordTrack.trackGpx?.Waypoints.Count - 1);    //Recording, the past
                        //var (series5, MinX5, MaxX5) = CreateSeries(MainActivity.ActiveRoute?.Routes.First().rtept, "#F44336");  //Whole Track/Route

                        var route = MainActivity.ActiveRoute?.Routes.First();
                        //MapPoint closest to Route. Distance should be 0... Can't we find this quicker by looking for LatLng in the route?
                        var (dummy1, map_index) = MapInformation.FindClosestWayPoint(route, new GPXUtils.Position(MapPosition.Latitude, MapPosition.Longitude, 0, false, null));
                        //WayPoint we are closest to
                        var (dummy2, gps_index) = MapInformation.FindClosestWayPoint(route, new GPXUtils.Position(GpsLocation.Latitude, GpsLocation.Longitude, 0, false, null));
                        //Swap around if needed
                        if (map_index < gps_index) (map_index, gps_index) = (gps_index, map_index);

                        var (series2, MinX2, MaxX2, distance2) = CreateSeries(route?.rtept, "#F44336", "#00FFFFFF", 0.0, 0, gps_index+1);                        //From Start to GPS position
                        var (series3, MinX3, MaxX3, distance3) = CreateSeries(route?.rtept, "#2196F3", "#00FFFFFF", distance2, gps_index, map_index+1);          //From GPS position to Map Position
                        var (series4, MinX4, MaxX4, distance4) = CreateSeries(route?.rtept, "#FFC107", "#00FFFFFF", distance3, map_index, route?.rtept.Count);   //From Map Position to End


                        PlotView? plotView = view.FindViewById<PlotView>(Resource.Id.oxyPlotWalkDone);
                        if (plotView != null && (series1 != null || series2 != null || series3 != null || series4 != null))
                        {
                            plotView.Model = new PlotModel
                            {
                                Series = { series1, series2, series3, series4 },
                                Axes =
                                {
                                    new LinearAxis
                                    {
                                        Position = AxisPosition.Bottom,
                                        FormatAsFractions = false,
                                        Unit = $"Travelled {DistanceTravelled:N2} / {'\u25B2'} {ElevationAnalyzer.TotalAscent:N0}m / {'\u25BC'} {ElevationAnalyzer.TotalDescent:N0}m",
                                    },
                                    new LinearAxis
                                    {
                                        Position = AxisPosition.Left,
                                        FormatAsFractions = false,
                                        Minimum = new[] { MinX1, MinX2, MinX3, MinX4 }.Min() * 0.9,
                                        Maximum = new[] { MaxX1, MaxX2, MaxX3, MaxX4 }.Max() * 1.1,
                                        Unit = "Elevation, m"
                                    }
                                }
                            };

                            plotView.Visibility = ViewStates.Visible;
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Fatal(ex, "posinfo - Crashed calculating Completed 'Distance / Ascent / Descent'");
                    }
                }

                //ConfigureGraph(view, GpsLocation, MapPosition);

                sw.Stop();
                Serilog.Log.Information($"Elapsed time for OnCreateView - PosInfo : {sw.ElapsedMilliseconds} ms");

                return view;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Fragment_posinfo Crashed");
            }

            return null;
        }
        
        private (AreaSeries? series, double MinX, double MaxX, double distance) CreateSeries(wptTypeCollection? waypoints, string? hexcolor1, string? hexcolor2, double distance_m, int start, int? end)
        {
            if (waypoints == null)
                return (null, 0, 0, 0);

            //Sanitize end point
            if (end > waypoints?.Count-1)
                end = waypoints?.Count-1;

            double ele = (double)waypoints[start].ele;
            double min = ele, max = ele;

            //Create the series with first datapoint
            var series = new AreaSeries
            {
                MarkerType = MarkerType.None,
                MarkerSize = 1,
                MarkerStroke = OxyColors.White,
                Color = OxyColor.Parse(hexcolor1),
                Fill = OxyColor.Parse(hexcolor2),
                Points = { new DataPoint(distance_m / 1000, ele) },
            };

            var ph = new PositionHandler();

            for (int i = start + 1; i < end; i++)
            {
                var prev = waypoints[i - 1];
                var curr = waypoints[i];

                //Calculate Distance to previous point
                var p1 = new GPXUtils.Position((float)prev.lat, (float)prev.lon, 0, false, null);
                var p2 = new GPXUtils.Position((float)curr.lat, (float)curr.lon, 0, false, null);
                var newdistance_m = (double)ph.CalculateDistance(p1, p2, DistanceType.Meters);
                distance_m += newdistance_m;

                //Only add to plot if valid elevation data, and we've moved from previous point
                if (!curr.eleSpecified || newdistance_m <= 0)
                    continue;

                ele = (double)curr.ele;
                series.Points.Add(new DataPoint(distance_m / 1000, ele));

                if (ele > max) max = ele;
                if (ele < min) min = ele;
                Serilog.Log.Information(i.ToString());
            }

            return (Series: series, MinX: min, MaxX: max, Distance: distance_m);
        }
    }
}
