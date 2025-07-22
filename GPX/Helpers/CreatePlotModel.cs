using GPXUtils;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using SharpGPX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hajk.GPX
{
    partial class Helpers
    {
        public static PlotModel? CreatePlotModel(GpxClass gpx)
        {
            if (gpx == null)
                return null;

            var plotModel = new PlotModel { };

            var series1 = new AreaSeries
            {
                MarkerType = MarkerType.None,
                MarkerSize = 1,
                MarkerStroke = OxyColors.White,
                Color = OxyColor.FromArgb(255, 21, 101, 192),
                Fill = OxyColor.FromArgb(128, 144, 202, 249),
            };

            //Graph max / min
            decimal min = 0;
            decimal max = 0;
            double distance_km = 0.0;
            var ph = new PositionHandler();

            //Routes
            /**/ //What are we doing for routes 1 to n ?!?
            if (gpx.Routes != null && gpx.Routes.Count > 0)
            {
                //Fist item to plot is first item at pos 0
                if (gpx.Routes[0].rtept[0] == null)
                    return null;

                //First item
                var elevation = (double)gpx.Routes[0].rtept[0].ele;
                min = (decimal)elevation;
                max = (decimal)elevation;
                series1.Points.Add(new DataPoint(0, elevation));

                //Start at index 1 so we can calculate distance from index 0 as new position on x axis
                for (int i = 1; i < gpx.Routes[0].rtept.Count; i++)
                {
                    //Calculate Distance to previous point
                    var p1 = new GPXUtils.Position((float)gpx.Routes[0].rtept[i - 1].lat, (float)gpx.Routes[0].rtept[i - 1].lon, 0, false, null);
                    var p2 = new GPXUtils.Position((float)gpx.Routes[0].rtept[i].lat, (float)gpx.Routes[0].rtept[i].lon, 0, false, null);
                    distance_km += (double)ph.CalculateDistance(p1, p2, DistanceType.Kilometers);

                    //Only add valid elevation data
                    if (gpx.Routes[0].rtept[i].eleSpecified == true)
                    {
                        series1.Points.Add(new DataPoint(distance_km, (double)gpx.Routes[0].rtept[i].ele));

                        //Find Max
                        if (gpx.Routes[0].rtept[i].ele > max)
                        {
                            max = gpx.Routes[0].rtept[i].ele;
                        }

                        //Find Min
                        if (gpx.Routes[0].rtept[i].ele < min)
                        {
                            min = gpx.Routes[0].rtept[i].ele;
                        }
                    }
                }
            }

            //Tracks
            /**/ //What are we doing for tracks 1 to n ?!?
            if (gpx.Tracks != null && gpx.Tracks.Count > 0)
            {
                //Fist item to plot is first item at pos 0
                if (gpx.Tracks[0] == null || gpx.Tracks[0].trkseg[0] == null || gpx.Tracks[0].trkseg[0].trkpt[0] == null)
                    return null;

                //First item
                var elevation = (double)gpx.Tracks[0].trkseg[0].trkpt[0].ele;
                min = (decimal)elevation;
                max = (decimal)elevation;
                series1.Points.Add(new DataPoint(0, elevation));

                //The rest
                foreach (SharpGPX.GPX1_1.trkType track in gpx.Tracks)
                {
                    foreach (SharpGPX.GPX1_1.trksegType trkseg in track.trkseg)
                    {
                        //Start at index 1 so we can calculate distance from index 0 as new position on x axis
                        for (int i = 1; i < trkseg.trkpt.Count; i++)
                        {
                            //Calculate Distance to previous point and add as a datapoint
                            var p1 = new GPXUtils.Position((float)trkseg.trkpt[i - 1].lat, (float)trkseg.trkpt[i - 1].lon, 0, false, null);
                            var p2 = new GPXUtils.Position((float)trkseg.trkpt[i].lat, (float)trkseg.trkpt[i].lon, 0, false, null);
                            distance_km += (double)ph.CalculateDistance(p1, p2, DistanceType.Kilometers);

                            //Only add valid elevation data
                            if (trkseg.trkpt[i].eleSpecified == true)
                            {
                                series1.Points.Add(new DataPoint(distance_km, (double)trkseg.trkpt[i].ele));

                                //Finx Max
                                if (trkseg.trkpt[i].ele > max)
                                {
                                    max = trkseg.trkpt[i].ele;
                                }

                                //Find Min
                                if (trkseg.trkpt[i].ele < min)
                                {
                                    min = trkseg.trkpt[i].ele;
                                }
                            }
                        }
                    }
                }
            }

            //Axes
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, FormatAsFractions = true, Unit = "km" });
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Maximum = (double)(max + max / 10), Minimum = (double)(min - min / 10), Unit = "m" });

            plotModel.Series.Add(series1);

            return plotModel;
        }
    }
}
