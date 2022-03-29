using System;
using System.Linq;
using System.Collections.Generic;
using FFI.NVector;
using SharpGPX.GPX1_1;

//https://danielsaidi.com/blog/2011/02/04/calculate-distance-and-bearing-between-two-positions

namespace GPXUtils
{
    public static class GPXUtils
    {
        public static (int, int, int) CalculateElevationDistanceData(wptTypeCollection Waypoints, int start_index, int end_index)
        {
            decimal ascent = 0;
            decimal descent = 0;
            decimal distance = 0;

            try
            {
                //Flip around?
                if (start_index > end_index)
                {
                    (end_index, start_index) = (start_index, end_index);
                }

                //Calculate distance and the gain and loss of elevation
                for (int j = start_index; j < end_index - 1; j++)
                {
                    decimal j0 = Waypoints[j].ele;
                    decimal j1 = Waypoints[j + 1].ele;

                    if (j0 > j1)
                    {
                        descent += j0 - j1;
                    }
                    else
                    {
                        ascent += j1 - j0;
                    }
                    
                    var p1 = new Position((float)Waypoints[j].lat, (float)Waypoints[j + 1].lon, 0);
                    var p2 = new Position((float)Waypoints[j].lat, (float)Waypoints[j + 1].lon, 0);
                    var p = new PositionHandler();
                    distance += (decimal)p.CalculateDistance(p1, p2, DistanceType.Kilometers) * 1000;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"GPXUtils - CalculationElevationData()");
                return (0, 0, 0);
            }

            return ((int)ascent, (int)descent, (int)distance);
        }
    }

    public class AngleConverter
    {
        public double ConvertDegreesToRadians(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        public double ConvertRadiansToDegrees(double angle)
        {
            return 180.0 * angle / Math.PI;
        }
    }

    public static class DistanceConverter
    {
        public static double ConvertMilesToKilometers(double miles)
        {
            return miles * 1.609344;
        }

        public static double ConvertKilometersToMiles(double kilometers)
        {
            return kilometers * 0.621371192;
        }
    }

    public enum DistanceType
    {
        Miles = 0,
        Kilometers = 1
    }

    public class Position
    {
        public Position(double latitude, double longitude, double elevation)
        {
            Latitude = latitude;
            Longitude = longitude;
            Elevation = elevation;
        }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Elevation { get; set; }
    }

    public interface IBearingCalculator
    {
        double CalculateBearing(Position position1, Position position2);
    }

    public interface IDistanceCalculator
    {
        double CalculateDistance(Position position1, Position position2, DistanceType distanceType1);
    }

    public interface IRhumbBearingCalculator
    {
        double CalculateRhumbBearing(Position position1, Position position2);
    }

    public interface IRhumbDistanceCalculator
    {
        double CalculateRhumbDistance(Position position1, Position position2, DistanceType distanceType);
    }

    public class PositionHandler : IBearingCalculator, IDistanceCalculator, IRhumbBearingCalculator, IRhumbDistanceCalculator
    {
        private readonly AngleConverter angleConverter;

        public PositionHandler()
        {
            angleConverter = new AngleConverter();
        }

        public static double EarthRadiusInKilometers { get { return 6367.0; } }
        public static double EarthRadiusInMiles { get { return 3956.0; } }        

        public double CalculateBearing(Position position1, Position position2)
        {
            var lat1 = angleConverter.ConvertDegreesToRadians(position1.Latitude);
            var lat2 = angleConverter.ConvertDegreesToRadians(position2.Latitude);
            var long1 = angleConverter.ConvertDegreesToRadians(position2.Longitude);
            var long2 = angleConverter.ConvertDegreesToRadians(position1.Longitude);
            var dLon = long1 - long2;

            var y = Math.Sin(dLon) * Math.Cos(lat2);
            var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
            var brng = Math.Atan2(y, x);

            return (angleConverter.ConvertRadiansToDegrees(brng) + 360) % 360;
        }

        public double CalculateDistance(Position position1, Position position2, DistanceType distanceType)
        {
            var R = (distanceType == DistanceType.Miles) ? EarthRadiusInMiles : EarthRadiusInKilometers;
            var dLat = angleConverter.ConvertDegreesToRadians(position2.Latitude) - angleConverter.ConvertDegreesToRadians(position1.Latitude);
            var dLon = angleConverter.ConvertDegreesToRadians(position2.Longitude) - angleConverter.ConvertDegreesToRadians(position1.Longitude);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(angleConverter.ConvertDegreesToRadians(position1.Latitude)) * Math.Cos(angleConverter.ConvertDegreesToRadians(position2.Latitude)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var distance = c * R;

            return Math.Round(distance, 2);
        }

        public double CalculateDistance(Position position1, Xamarin.Essentials.Location location2, DistanceType distanceType)
        {
            var position2 = new Position(location2.Latitude, location2.Longitude, 0);
            return CalculateDistance(position1, position2, distanceType);
        }

        public double CalculateRhumbBearing(Position position1, Position position2)
        {
            var lat1 = angleConverter.ConvertDegreesToRadians(position1.Latitude);
            var lat2 = angleConverter.ConvertDegreesToRadians(position2.Latitude);
            var dLon = angleConverter.ConvertDegreesToRadians(position2.Longitude - position1.Longitude);

            var dPhi = Math.Log(Math.Tan(lat2 / 2 + Math.PI / 4) / Math.Tan(lat1 / 2 + Math.PI / 4));
            if (Math.Abs(dLon) > Math.PI) dLon = (dLon > 0) ? -(2 * Math.PI - dLon) : (2 * Math.PI + dLon);
            var brng = Math.Atan2(dLon, dPhi);

            return (angleConverter.ConvertRadiansToDegrees(brng) + 360) % 360;
        }

        public double CalculateRhumbDistance(Position position1, Position position2, DistanceType distanceType)
        {
            var R = (distanceType == DistanceType.Miles) ? EarthRadiusInMiles : EarthRadiusInKilometers;
            var lat1 = angleConverter.ConvertDegreesToRadians(position1.Latitude);
            var lat2 = angleConverter.ConvertDegreesToRadians(position2.Latitude);
            var dLat = angleConverter.ConvertDegreesToRadians(position2.Latitude - position1.Latitude);
            var dLon = angleConverter.ConvertDegreesToRadians(Math.Abs(position2.Longitude - position1.Longitude));

            var dPhi = Math.Log(Math.Tan(lat2 / 2 + Math.PI / 4) / Math.Tan(lat1 / 2 + Math.PI / 4));
            var q = Math.Cos(lat1);
            if (dPhi != 0) q = dLat / dPhi;  // E-W line gives dPhi=0
                                             // if dLon over 180° take shorter rhumb across 180° meridian:
            if (dLon > Math.PI) dLon = 2 * Math.PI - dLon;
            var dist = Math.Sqrt(dLat * dLat + q * q * dLon * dLon) * R;

            return dist;
        }        
    }

    //This is from https://www.ffi.no/en/research/n-vector
    public static class CrossTrackCalculations
    {
        // Calculate Cross track distance (XTE)
        public static Tuple<double[], double> CalculateCrossTrackDistance(wptType a1, wptType a2, Position c)
        {
            NVMath _NV = new NVMath();

            var r_Earth = 6371e3; // m, mean Earth radius

            // input as lat/long in deg:
            var n_EA1_E = _NV.lat_long2n_E(_NV.rad((double)a1.lat), _NV.rad((double)a1.lon));
            var n_EA2_E = _NV.lat_long2n_E(_NV.rad((double)a2.lat), _NV.rad((double)a2.lon));
            var n_EB_E = _NV.lat_long2n_E(_NV.rad(c.Latitude), _NV.rad(c.Longitude));

            // Find the unit normal to the great circle between n_EA1_E and n_EA2_E:
            var c_E = _NV.unit(Utilities.Cross(n_EA1_E, n_EA2_E));

            // Find the great circle cross track distance:
            var s_xt = (Math.Acos(Utilities.Dot(c_E, n_EB_E)) - Math.PI / 2) * r_Earth;

            return Tuple.Create(c_E, Math.Abs(s_xt));
        }

        // Calculate intersection of a1-a2 and c-c_E
        public static Position CalculateCrossTrackPosition(wptType a1, wptType a2, Position c, double[] c_E)
        {
            NVMath _NV = new NVMath();

            // input as lat/long in deg:
            var n_EA1_E = _NV.lat_long2n_E(_NV.rad((double)a1.lat), _NV.rad((double)a1.lon));
            var n_EA2_E = _NV.lat_long2n_E(_NV.rad((double)a2.lat), _NV.rad((double)a2.lon));
            var n_EB1_E = _NV.lat_long2n_E(_NV.rad(c.Latitude), _NV.rad(c.Longitude));
            var n_EB2_E = c_E;

            // Find the intersection between the two paths, n_EC_E:
            var n_EC_E_tmp = _NV.unit(Utilities.Cross(Utilities.Cross(n_EA1_E, n_EA2_E), Utilities.Cross(n_EB1_E, n_EB2_E)));

            // n_EC_E_tmp is one of two solutions, the other is -n_EC_E_tmp. Select the one that is closest to n_EA1_E, by selecting sign from the dot product between n_EC_E_tmp and n_EA1_E:
            var n_EC_E = Utilities.VecMul(Math.Sign(Utilities.Dot(n_EC_E_tmp, n_EA1_E)), n_EC_E_tmp);

            // Convert to lat, lon in Degrees
            var pos_EC = new Position(_NV.deg(_NV.n_E2lat(n_EC_E)), _NV.deg(_NV.n_E2long(n_EC_E)), 0);

            return pos_EC;
        }

        //Calculate distance between two locations
        public static double CalculateDistance(wptType a1, Position c_E)
        {
            NVMath _NV = new NVMath();

            // input as lat/long in deg:
            var n_EA_E = _NV.lat_long2n_E(_NV.rad((double)a1.lat), _NV.rad((double)a1.lon));
            var n_EB_E = _NV.lat_long2n_E(_NV.rad(c_E.Latitude), _NV.rad(c_E.Longitude));

            var r_Earth = 6371e3; // m, mean Earth radius

            // The great circle distance is given by equation (16) in Gade (2010):
            var s_AB = Math.Atan2(Utilities.Norm(Utilities.Cross(n_EA_E, n_EB_E)), Utilities.Dot(n_EA_E, n_EB_E)) * r_Earth;

            return Math.Abs(s_AB);
        }

        //Calculate distance between two locations
        public static double CalculateDistance(wptType wpt_a1, wptType a2)
        {
            var a2_p = new Position((double)a2.lat, (double)a2.lon, 0);
            var s_AB = CalculateDistance(wpt_a1, a2_p);

            return s_AB;
        }

        //Calculate distance between two locations
        public static double CalculateDistance(Position a1, Position a2_p)
        {
            var wpt_a1 = new wptType((double)a1.Latitude, (double)a1.Longitude);
            var s_AB = CalculateDistance(wpt_a1, a2_p);

            return s_AB;
        }

        //Calculate distance between two locations
        public static double CalculateDistance(wptType wpt_a1, Xamarin.Essentials.Location a2)
        {
            var a2_p = new Position((double)a2.Latitude, (double)a2.Longitude, 0);
            var s_AB = CalculateDistance(wpt_a1, a2_p);

            return s_AB;
        }
    }

    public static class MapInformation
    {
        //Find WayPoint we are closest to. Use it, incorrectly, as start point for calculations
        public static (Position, int) FindClosestWayPoint(rteType route, Position position)
        {
            try
            {
                if (route == null)
                    return (null, -1);

                int pos_index_i = 0;           //Index to position closest to GPS Position
                double pos_distance = 0.0f;    //Distance to position

                for (int i = 0; i < route.rtept.Count; i++)
                {
                    double mapDistanceMeters = CrossTrackCalculations.CalculateDistance(route.rtept[i], position);

                    if (mapDistanceMeters < pos_distance || pos_distance == 0)
                    {
                        pos_distance = mapDistanceMeters;
                        pos_index_i = i;
                        Serilog.Log.Debug($"Shortest Location is index: '" + pos_index_i.ToString() + "' and '" + pos_distance.ToString("N2") + "m");
                    }
                }

                var pos_w = new Position((float)route.rtept[pos_index_i].lat, (float)route.rtept[pos_index_i].lon, 0);
                return (pos_w, pos_index_i);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "GPXUtils - FindClosestWayPoint()");
            }

            return (null, -1);
        }
    }
}
