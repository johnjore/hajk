using System;

//https://danielsaidi.com/blog/2011/02/04/calculate-distance-and-bearing-between-two-positions

namespace GPXUtils
{
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
        public Position(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
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
}
