using System;
using System.Linq;
using System.Collections.Generic;
using hajk.Models;
using FFI.NVector;
using SharpGPX;
using SharpGPX.GPX1_1;
using Microsoft.Maui.Devices.Sensors;

//https://danielsaidi.com/blog/2011/02/04/calculate-distance-and-bearing-between-two-positions

namespace GPXUtils
{
    public static class GPXUtils
    {
        public static AwesomeTiles.TileRange? GetTileRange(int zoom, hajk.Models.Map map)
        {
            try
            {
                var leftBottom = AwesomeTiles.Tile.CreateAroundLocation(map.BoundsLeft, map.BoundsBottom, zoom);
                var topRight = AwesomeTiles.Tile.CreateAroundLocation(map.BoundsRight, map.BoundsTop, zoom);

                if (leftBottom == null || topRight == null)
                {
                    return null;
                }

                var minX = Math.Min(leftBottom.X, topRight.X);
                var maxX = Math.Max(leftBottom.X, topRight.X);
                var minY = Math.Min(leftBottom.Y, topRight.Y);
                var maxY = Math.Max(leftBottom.Y, topRight.Y);

                var tiles = new AwesomeTiles.TileRange(minX, minY, maxX, maxY, zoom);
                //Serilog.Log.Information($"Need to download {tiles.Count} tiles for zoom level {zoom}");
                return tiles;

            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"GPXUtils - GetTileRange()");
            }

            return null;
        }

        public static AwesomeTiles.TileRange? GetTileRange(int zoom, GpxClass gpx)
        {
            try
            {
                var bounds = gpx.GetBounds();
                var leftBottom = AwesomeTiles.Tile.CreateAroundLocation((double)bounds.minlat, (double)bounds.minlon, zoom);
                var topRight = AwesomeTiles.Tile.CreateAroundLocation((double)bounds.maxlat, (double)bounds.maxlon, zoom);

                if (leftBottom == null || topRight == null)
                {
                    return null;
                }

                var minX = Math.Min(leftBottom.X, topRight.X);
                var maxX = Math.Max(leftBottom.X, topRight.X);
                var minY = Math.Min(leftBottom.Y, topRight.Y);
                var maxY = Math.Max(leftBottom.Y, topRight.Y);

                var tiles = new AwesomeTiles.TileRange(minX, minY, maxX, maxY, zoom);
                //Serilog.Log.Information($"Need to download {tiles.Count} tiles for zoom level {zoom}");
                return tiles;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"GPXUtils - GetTileRange()");
            }

            return null;
        }

        public static AwesomeTiles.TileRange? GetTileRange(int zoom, Position pos)
        {
            try
            {
                var leftBottom = AwesomeTiles.Tile.CreateAroundLocation(pos.Latitude, pos.Longitude, zoom);
                var topRight = AwesomeTiles.Tile.CreateAroundLocation(pos.Latitude, pos.Longitude, zoom);

                if (leftBottom == null || topRight == null)
                {
                    return null;
                }

                var minX = Math.Min(leftBottom.X, topRight.X);
                var maxX = Math.Max(leftBottom.X, topRight.X);
                var minY = Math.Min(leftBottom.Y, topRight.Y);
                var maxY = Math.Max(leftBottom.Y, topRight.Y);

                var tiles = new AwesomeTiles.TileRange(minX, minY, maxX, maxY, zoom);
                //Serilog.Log.Information($"Need to download {tiles.Count} tiles for zoom level {zoom}");
                return tiles;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"GPXUtils - GetTileRange()");
            }

            return null;
        }

        public static AwesomeTiles.TileRange? GetTileRange(int zoom, boundsType bounds)
        {
            try
            {
                var leftBottom = AwesomeTiles.Tile.CreateAroundLocation((double)bounds.minlat, (double)bounds.minlon, zoom);
                var topRight = AwesomeTiles.Tile.CreateAroundLocation((double)bounds.maxlat, (double)bounds.maxlon, zoom);

                if (leftBottom == null || topRight == null)
                {
                    return null;
                }

                var minX = Math.Min(leftBottom.X, topRight.X);
                var maxX = Math.Max(leftBottom.X, topRight.X);
                var minY = Math.Min(leftBottom.Y, topRight.Y);
                var maxY = Math.Max(leftBottom.Y, topRight.Y);

                var tiles = new AwesomeTiles.TileRange(minX, minY, maxX, maxY, zoom);
                //Serilog.Log.Information($"Need to download {tiles.Count} tiles for zoom level {zoom}");
                return tiles;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"GPXUtils - GetTileRange()");
            }

            return null;
        }

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
                for (int j = start_index; j < end_index; j++)
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

                    var p = new PositionHandler();
                    var p1 = new Position((float)Waypoints[j    ].lat, (float)Waypoints[j    ].lon, 0, false, null);
                    var p2 = new Position((float)Waypoints[j + 1].lat, (float)Waypoints[j + 1].lon, 0, false, null);
                    distance += (decimal)p.CalculateDistance(p1, p2, DistanceType.Meters);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"GPXUtils - CalculationElevationData()");
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

        public static double ConvertMetersToFeet(double meters)
        {
            return meters * 3.280839895;
        }
    }

    public enum DistanceType
    {
        Miles = 0,
        Kilometers = 1,
        Meters = 2,
    }

    public class Position
    {
        public Position(double latitude, double longitude, double elevation, bool eleSpecified, string? geotifffilename)
        {
            Latitude = latitude;
            Longitude = longitude;
            Elevation = elevation;
            ElevationSpecified = eleSpecified;
            GeoTiffFileName = geotifffilename;
        }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Elevation { get; set; }
        public bool ElevationSpecified { get; set; }
        public string? GeoTiffFileName { get; set; }
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

        public static double EarthRadiusInMeters { get { return 6371008.7714; } }
        public static double EarthRadiusInMiles { get { return 3956.0; } }        

        public double CalculateBearing(Position position1, Position position2)
        {
            var lat1 = angleConverter.ConvertDegreesToRadians(position1.Latitude);
            var lon1 = angleConverter.ConvertDegreesToRadians(position1.Longitude);
            var lat2 = angleConverter.ConvertDegreesToRadians(position2.Latitude);
            var lon2 = angleConverter.ConvertDegreesToRadians(position2.Longitude);

            var dLon = lon2 - lon1;

            var y = Math.Sin(dLon) * Math.Cos(lat2);
            var x = Math.Cos(lat1) * Math.Sin(lat2) -
                    Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

            var brng = Math.Atan2(y, x);
            return (angleConverter.ConvertRadiansToDegrees(brng) + 360) % 360;
        }

        public double CalculateProjectedBearing(double x1, double y1, double x2, double y2)
        {
            var dx = x2 - x1;
            var dy = y2 - y1;

            var angleRad = Math.Atan2(dx, dy);
            var angleDeg = angleRad * 180.0 / Math.PI;

            return (angleDeg + 360) % 360;
        }

        public double CalculateProjectedBearing(Position p1, Position p2)
        {
            var dx = p2.Latitude - p1.Latitude;
            var dy = p2.Longitude - p1.Longitude;

            var angleRad = Math.Atan2(dx, dy);
            var angleDeg = angleRad * 180.0 / Math.PI;

            return (angleDeg + 360) % 360;
        }

        public double CalculateDistance(Position position1, Position position2, DistanceType distanceType)
        {
            double R = 0;
            //R = (distanceType == DistanceType.Miles) ? EarthRadiusInMiles : EarthRadiusInKilometers;

            switch (distanceType)
            {
                case DistanceType.Meters: { R = EarthRadiusInMeters; break; }
                case DistanceType.Kilometers: { R = EarthRadiusInMeters / 1000; break; }
                case DistanceType.Miles: { R = EarthRadiusInMiles; break; }
            }
           
            var dLat = angleConverter.ConvertDegreesToRadians(position2.Latitude) - angleConverter.ConvertDegreesToRadians(position1.Latitude);
            var dLon = angleConverter.ConvertDegreesToRadians(position2.Longitude) - angleConverter.ConvertDegreesToRadians(position1.Longitude);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(angleConverter.ConvertDegreesToRadians(position1.Latitude)) * Math.Cos(angleConverter.ConvertDegreesToRadians(position2.Latitude)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var distance = c * R;

            return Math.Round(distance, 2);
        }        

        public double CalculateDistance(Position position1, Android.Locations.Location location2, DistanceType distanceType)
        {
            var position2 = new Position(location2.Latitude, location2.Longitude, 0, false, null);
            return CalculateDistance(position1, position2, distanceType);
        }

        public double CalculateDistance(string? latlon, Android.Locations.Location location2, DistanceType distanceType)
        {
            if (latlon == null || latlon.Length < 3)
            {
                Serilog.Log.Warning($"Failed to calculate distance as latlon is not a valid value '{latlon}'");
                return -1;
            }

            try
            {
                var parts = latlon.Split(',');
                var position1 = new Position(double.Parse(parts[0]), double.Parse(parts[1]), 0, false, null);

                var position2 = new Position(location2.Latitude, location2.Longitude, 0, false, null);

                return CalculateDistance(position1, position2, distanceType);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Failed to calculate distance by parsing latlon: '{latlon}'");
                return 9999999;
            }
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
            double R = 0;
            //R = (distanceType == DistanceType.Miles) ? EarthRadiusInMiles : EarthRadiusInKilometers;

            switch (distanceType)
            {
                case DistanceType.Meters: { R = EarthRadiusInMeters; break; }
                case DistanceType.Kilometers: { R = EarthRadiusInMeters * 1000; break; }
                case DistanceType.Miles: { R = EarthRadiusInMiles; break; }
            }
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
            NVMath _NV = new();

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
            NVMath _NV = new();

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
            var pos_EC = new Position(_NV.deg(_NV.n_E2lat(n_EC_E)), _NV.deg(_NV.n_E2long(n_EC_E)), 0, false, null);

            return pos_EC;
        }

        //Calculate distance between two locations
        public static double CalculateDistance(wptType a1, Position c_E)
        {
            NVMath _NV = new();

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
            var a2_p = new Position((double)a2.lat, (double)a2.lon, 0, false,null);
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
        public static double CalculateDistance(wptType wpt_a1, Location a2)
        {
            var a2_p = new Position((double)a2.Latitude, (double)a2.Longitude, 0, false, null);
            var s_AB = CalculateDistance(wpt_a1, a2_p);

            return s_AB;
        }
    }

    public static class MapInformation
    {
        //Find WayPoint we are closest to. Use it, incorrectly, as start point for calculations
        public static (Position?, int) FindClosestWayPoint(rteType? route, Position? position)
        {
            try
            {
                if (route == null || position == null)
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

                var pos_w = new Position((float)route.rtept[pos_index_i].lat, (float)route.rtept[pos_index_i].lon, 0, false, null);
                return (pos_w, pos_index_i);
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "GPXUtils - FindClosestWayPoint()");
            }

            return (null, -1);
        }
    }
}
