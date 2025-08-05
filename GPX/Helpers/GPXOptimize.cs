using SharpGPX;
using SharpGPX.GPX1_1;
using SimplifyCSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace hajk.GPX
{
    public class GPXOptimize
    {
        static readonly double tolerence = 0.00008;    //Smaller number => More points

        public static GpxClass Optimize(GpxClass gpx)
        {
            //Dont update original variable, create a copy
            var gpxCopy = CloneGpx(gpx);

            foreach (trkType track in gpxCopy.Tracks)
            {
                foreach (trksegType trkseg in track.trkseg)
                {
                    wptTypeCollection? points = trkseg.trkpt;

                    var op = OptimizePoints(points);
                    if (op != null)
                    {
                        trkseg.trkpt = op;
                    }
                }
            }

            foreach (rteType route in gpxCopy.Routes)
            {
                wptTypeCollection? points = route.rtept;
                                
                var op = OptimizePoints(points);
                if (op != null)
                {
                    route.rtept = op;
                }
            }

            return gpxCopy;
        }

        public static wptTypeCollection? OptimizePoints(wptTypeCollection? points)
        {
            if (points == null)
            {
                return null;
            }
            
            Serilog.Log.Debug("From: " + points.Count.ToString("#,0") + " points");

            //https://github.com/rohaanhamid/simplify-csharp
            bool highQualityEnabled = true;
            IList<wptType> reducedPoints = SimplificationHelpers.Simplify<wptType>(points,
                            (p1, p2) => p1 == p2,
                            (p) => (double)p.lat,
                            (p) => (double)p.lon,
                            tolerence,
                            highQualityEnabled
                            );

            Serilog.Log.Debug("To:   " + reducedPoints.Count.ToString("#,0") + " points");
            
            return new wptTypeCollection(reducedPoints);
        }

        public static GpxClass CloneGpx(GpxClass original)
        {
            string xml = original.ToXml();
            return GpxClass.FromXml(xml);
        }
    }
}