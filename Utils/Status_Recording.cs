using Android.Media;
using Android.Renderscripts;
using Android.Views;
using Android.Widget;
using CoordinateSharp;
using ExCSS;
using GPXUtils;
using hajk.Data;
using hajk.Models;
using Mapsui.Extensions;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Xamarin.Android;
using SharpGPX;
using SharpGPX.GPX1_1;
using SharpGPX.GPX1_1.Garmin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace hajk
{
    internal class Status_Recording
    {
        public class Graph
        {
            public AreaSeries? Series { get; set; }
            public double Distance { get; set; }
            public double MinX { get; set; }
            public double MaxX { get; set; }
        }
        static List<Graph> g = new List<Graph>();

        public static void ShowStatusPage(Android.Views.View view, long? Id, Position? GpsPosition, Position? MapPosition)
        {
            try
            {
                SharpGPX.GPX1_1.rteType route = null;                  // Active Route / Track for calculations, inc Off-Route detection
                if (MainActivity.ActiveRoute != null)
                {
                    route = MainActivity.ActiveRoute.Routes.FirstOrDefault();
                }
                else
                {
                    GPXDataRouteTrack GPXRouteTrack = Task.Run(() => RouteDatabase.GetRouteAsync((int)Id)).GetAwaiter().GetResult();
                    if (GPXRouteTrack == null)
                        return;

                    GpxClass gpx = GpxClass.FromXml(GPXRouteTrack.GPX);
                    if (GPXRouteTrack.GPXType == GPXType.Track)
                    {
                        gpx.Routes.Add(gpx.Tracks.FirstOrDefault().ToRoutes()[0]);
                    }

                    route = gpx.Routes.FirstOrDefault();
                }

                g.Clear();
                DisplayElevationData(view, GpsPosition, MapPosition);
                InformationAtMapLocation(view, route, GpsPosition, MapPosition);
                double DistanceTravelled = (RecordedStats(view) / 1000.0);
                RouteStats(view, route, GpsPosition, MapPosition);

                PlotView? plotView = view.FindViewById<PlotView>(Resource.Id.oxyPlotWalkDone);
                if (plotView != null)
                {
                    plotView.Model = new PlotModel
                    {
                        Axes =
                            {
                                new LinearAxis
                                {
                                    Position = AxisPosition.Bottom,
                                    FormatAsFractions = false,
                                    Unit = $"Travelled {DistanceTravelled:N2} / {'\u25B2'} {RollingElevationAnalyzer.TotalAscent:N0}m / {'\u25BC'} {RollingElevationAnalyzer.TotalDescent:N0}m",
                                },
                                new LinearAxis
                                {
                                    Position = AxisPosition.Left,
                                    FormatAsFractions = false,
                                    Minimum = g.Min(graph => graph.MinX) * 0.9,
                                    Maximum = g.Max(graph => graph.MaxX) * 1.1,
                                    Unit = "Elevation, m"
                                }
                            }
                    };
                    foreach (Graph s in g)
                    {
                        plotView.Model.Series.Add(s.Series);
                    }

                    plotView.Visibility = ViewStates.Visible;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Crashed showing stats");
            }
        }

        public static void ShowInfoPage(Android.Views.View view, long? Id, Position? GpsPosition, Position? MapPosition)
        {
            try
            {
                //Get the route or track from database
                GPXDataRouteTrack GPXRouteTrack = Task.Run(() => RouteDatabase.GetRouteAsync((int)Id)).GetAwaiter().GetResult();
                if (GPXRouteTrack == null)
                    return;

                //Our Route to Display information about
                string gColour = "#007ACC";
                string fColour = "#337AB7";
                GpxClass gpx = GpxClass.FromXml(GPXRouteTrack.GPX);
                if (GPXRouteTrack.GPXType == GPXType.Track)
                {
                    gpx.Routes.Add(gpx.Tracks.FirstOrDefault().ToRoutes()[0]);
                    gColour = "#D32F2F";
                    fColour = "#E57373";
                }

                //Graph of track / route
                g.Clear();
                g.Add(CreateSeries(gpx.Routes.FirstOrDefault().rtept, gColour, fColour, GPXRouteTrack.Distance, 0, gpx.Routes.FirstOrDefault().rtept.Count));

                PlotView? plotView = view.FindViewById<PlotView>(Resource.Id.oxyPlot2);
                if (plotView != null)
                {
                    plotView.Model = new PlotModel
                    {
                        Title = GPXRouteTrack.Name,
                        Axes =
                            {
                                new LinearAxis
                                {
                                    Position = AxisPosition.Bottom,
                                    FormatAsFractions = false,
                                    Unit = $"Length {GPXRouteTrack.Distance:N2}km / {'\u25B2'} {GPXRouteTrack.Ascent:N0}m / {'\u25BC'} {GPXRouteTrack.Descent:N0}m",
                                },
                                new LinearAxis
                                {
                                    Position = AxisPosition.Left,
                                    FormatAsFractions = false,
                                    Minimum = g.Min(graph => graph.MinX) * 0.9,
                                    Maximum = g.Max(graph => graph.MaxX) * 1.1,
                                    Unit = "Elevation, m"
                                }
                            }
                    };
                    foreach (Graph s in g)
                    {
                        plotView.Model.Series.Add(s.Series);
                    }
                    plotView.Visibility = ViewStates.Visible;

                    //Naismith
                    view.FindViewById<TextView>(Resource.Id.textView1).Text = $"{'\u23f1'} {GPXRouteTrack.NaismithTravelTime}";

                    //Shenandoah
                    float ShenandoahsHikingDifficultyScale = GPXRouteTrack.ShenandoahsScale;
                    string ShenandoahsHikingDifficultyRating = ShenandoahsHikingDifficulty.CalculateRating(ShenandoahsHikingDifficultyScale);
                    ShenandoahsHikingDifficulty.UpdateTextField(view.FindViewById<TextView>(Resource.Id.textView3), ShenandoahsHikingDifficultyScale, ShenandoahsHikingDifficultyRating);

                    //Not used
                    view.FindViewById<TextView>(Resource.Id.textView2).Text = $"";
                    view.FindViewById<TextView>(Resource.Id.textView4).Text = $"";
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Crashed showing stats");
            }
        }

        public static void DisplayElevationData(Android.Views.View view, Position? GpsPosition, Position? MapPosition)
        {
            try
            {
                //From GPS
                string GPSAltitude = "N/A";
                if (GpsPosition.ElevationSpecified)
                {
                    GPSAltitude = Math.Round((double)GpsPosition.Elevation).ToString("N0") + "m";
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
        }

        public static void InformationAtMapLocation(Android.Views.View view, rteType route, Position? GpsPosition, Position? MapPosition)
        {
            //Distance, Ascent and Descent following Route From current GPSLocation to MapPosition
            try
            {
                view.FindViewById<TextView>(Resource.Id.MapPosition).Text = "Calculate info to point on map";

                /**/
                //MapPoint closest to Route. Distance should be 0... Can't we find this quicker by looking for LatLng in the route?
                //MapPoint should already know the item pressed?
                //Optimizing map, removes a lot of points - But does this really matter?
                (var m1, var route_index_end) = MapInformation.FindClosestWayPoint(route, MapPosition);
                Serilog.Log.Information($"m1: {m1} - route_index_end: {route_index_end}");

                //WayPoint we are closest to?
                (var p1, var route_index_start) = MapInformation.FindClosestWayPoint(route, GpsPosition);
                Serilog.Log.Information($"m1: {p1} - route_index_start: {route_index_start}");

                if (route_index_start <= 0 || route_index_end <= 0)
                {
                    Serilog.Log.Warning($"route_index_start <= 0 || route_index_end <= 0) - Can't continue");
                    return;
                }

                //Get how much Ascent, Descent and Distance along the track to the MapPoint
                (var AscentGPSLocationMapLocation_m, var DescentGPSLocationMapLocation_m, var DistanceGPSLocationMapLocation_m) = GPXUtils.GPXUtils.CalculateElevationDistanceData(route.rtept, route_index_start, route_index_end);
                Serilog.Log.Information($"AscentGPSLocationMapLocation_m: {AscentGPSLocationMapLocation_m} - DescentGPSLocationMapLocation_m: {DescentGPSLocationMapLocation_m} - DistanceGPSLocationMapLocation_m: {DistanceGPSLocationMapLocation_m}");

                //Add distance from GPSLocation to first waypoint. We might not be on-top of it. If we are, distance should be 0...
                Serilog.Log.Information($"DistanceGPSLocationMapLocation_m - 1: {DistanceGPSLocationMapLocation_m}");
                DistanceGPSLocationMapLocation_m += (int)(new PositionHandler().CalculateDistance(p1, GpsPosition, GPXUtils.DistanceType.Meters));
                Serilog.Log.Information($"DistanceGPSLocationMapLocation_m - 2: {DistanceGPSLocationMapLocation_m}");

                //Add distance from m1 to MapPosition
                DistanceGPSLocationMapLocation_m += (int)(new PositionHandler().CalculateDistance(m1, MapPosition, GPXUtils.DistanceType.Meters));
                Serilog.Log.Information($"DistanceGPSLocationMapLocation_m - 3: {DistanceGPSLocationMapLocation_m}");

                /**///What about elevation changes for m1->e1 and GPS->p1 ?
                //Elevation changes between route index's?

                //Show the values
                var mapPositionText = $"{'\u27f7'} {Utils.Misc.KMvsM(DistanceGPSLocationMapLocation_m)} / " +
                                        $"{'\u25B2'} {AscentGPSLocationMapLocation_m:N0}m / " +
                                        $"{'\u25BC'} {DescentGPSLocationMapLocation_m:N0}m";
                view.FindViewById<TextView>(Resource.Id.MapPosition).Text = mapPositionText;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "posinfo - Crashed calculating 'Ascent, Descent and Distance from GPS Location to Map Position'");
            }

        }

        public static int RecordedStats(Android.Views.View view)
        {
            try
            {
                view.FindViewById<TextView>(Resource.Id.RecordedWaypointsAndTime).Text = $"Waypoints: {RecordTrack.trackGpx.Waypoints.Count.ToString()} / Duration: n/a or not recording";

                if (RecordTrack.trackGpx.Waypoints.Count > 0)
                {
                    var RecordedWaypointAndTime = $"Waypoints: {RecordTrack.trackGpx.Waypoints.Count.ToString()} / " +
                        "Duration: " + (DateTime.Now - RecordTrack.trackGpx.Waypoints.First().time).ToString(@"hh\:mm\:ss");
                    view.FindViewById<TextView>(Resource.Id.RecordedWaypointsAndTime).Text = RecordedWaypointAndTime;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "posinfo - Crashed calculating 'DurationTime'");
            }

            //Calculate Distance / Ascent / Descent from Start to Current Position
            try
            {
                var CompletedText = $"{char.ConvertFromUtf32(0x1f7e2)} N/A / " +
                                    $"{char.ConvertFromUtf32(0x1f53c)} N/A / " +
                                    $"{char.ConvertFromUtf32(0x1f53d)} N/A";
                view.FindViewById<TextView>(Resource.Id.Completed).Text = CompletedText;

                if (RecordTrack.trackGpx.Waypoints.Count > 0)
                {
                    (int TrackAscentFromStart_m, int TrackDescentFromStart_m, int TrackDistanceFromStart_m) = GPXUtils.GPXUtils.CalculateElevationDistanceData(RecordTrack.trackGpx.Waypoints, 0, RecordTrack.trackGpx.Waypoints.Count - 1);
                    var DistanceTravelled = (TrackDistanceFromStart_m / 1000.0).ToString("N2") + "km";
                    Serilog.Log.Debug($"TrackDistanceFromStart_m: '{TrackDistanceFromStart_m.ToString()}', DistanceTravelled: '{DistanceTravelled}', TrackAscentFromStart_m: '{TrackAscentFromStart_m.ToString()}', TrackDescentFromStart_m: '{TrackDescentFromStart_m}'");

                    CompletedText = $"{char.ConvertFromUtf32(0x1f7e2)} {DistanceTravelled:N2} / " +
                                    $"{char.ConvertFromUtf32(0x1f53c)} {TrackAscentFromStart_m:N0}m / " +
                                    $"{char.ConvertFromUtf32(0x1f53d)} {TrackDescentFromStart_m:N0}m";
                    view.FindViewById<TextView>(Resource.Id.Completed).Text = CompletedText;

                    //Graph from RecordTrack
                    Graph s1 = CreateSeries(RecordTrack.trackGpx?.Waypoints, "#4CAF50", "#00FFFFFF", 0.0, 0, RecordTrack.trackGpx?.Waypoints.Count - 1);    //Recording, the past
                    g.Add(s1);

                    return TrackDistanceFromStart_m;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error($"Failed to calculate distance/ascent/descent from start of recording");
            }

            return 0;            
        }

        public static void RouteStats(Android.Views.View view, rteType route, Position? GpsPosition, Position? MapPosition)
        {
            try
            {
                //MapPoint closest to Route. Distance should be 0... Can't we find this quicker by looking for LatLng in the route?
                var (dummy1, map_index) = MapInformation.FindClosestWayPoint(route, new Position(MapPosition.Latitude, MapPosition.Longitude, 0, false, null));
                //WayPoint we are closest to
                var (dummy2, gps_index) = MapInformation.FindClosestWayPoint(route, new Position(GpsPosition.Latitude, GpsPosition.Longitude, 0, false, null));
                //Swap around if needed
                if (map_index < gps_index) (map_index, gps_index) = (gps_index, map_index);

                Graph s2 = CreateSeries(route?.rtept, "#F44336", "#00FFFFFF", 0.0, 0, gps_index + 1);                        //From Start to GPS position
                g.Add(s2);
                Graph s3 = CreateSeries(route?.rtept, "#2196F3", "#00FFFFFF", s2.Distance, gps_index, map_index + 1);          //From GPS position to Map Position
                g.Add(s3);
                Graph s4 = CreateSeries(route?.rtept, "#FFC107", "#00FFFFFF", s3.Distance, map_index, route?.rtept.Count);     //From Map Position to End
                g.Add(s4);
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "posinfo - Crashed calculating Completed 'Distance / Ascent / Descent'");
            }
        }

        private static Graph? CreateSeries(wptTypeCollection? waypoints, string? hexcolor1, string? hexcolor2, double distance_m, int start, int? end)
        {
            if (waypoints == null)
                return null;

            //Sanitize end point
            if (end > waypoints?.Count - 1)
                end = waypoints?.Count - 1;

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
                var newdistance_m = (double)ph.CalculateDistance(p1, p2, GPXUtils.DistanceType.Meters);
                distance_m += newdistance_m;

                //Only add to plot if valid elevation data, and we've moved from previous point
                if (!curr.eleSpecified || newdistance_m <= 0)
                    continue;

                ele = (double)curr.ele;
                series.Points.Add(new DataPoint(distance_m / 1000, ele));

                if (ele > max) max = ele;
                if (ele < min) min = ele;
            }

            var s = new Graph()
            {
                Series = series,
                MinX = min,
                MaxX = max,
                Distance = distance_m
            };

            return s;
        }
    }
}
