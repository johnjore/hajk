﻿using System;
using System.Text;
using CoordinateSharp;
using hajk.Data;
using hajk.Models;


namespace hajk.GPX
{
    public class UTMHelpers
    {
        public static int UTMtoLatLon(string? a, int b, long UTMX, long UTMY)
        {
            UniversalTransverseMercator utm = new UniversalTransverseMercator(a, b, UTMX, UTMY);
            Coordinate c = UniversalTransverseMercator.ConvertUTMtoLatLong(utm);
            c.FormatOptions.Format = CoordinateFormatType.Decimal;
            c.FormatOptions.Round = 6;
            Serilog.Log.Verbose($"{c.Latitude}, {c.Longitude}");

            GPXDataPOI p = new()
            {
                Name = "Rogaining",
                Description = "Rogaining",
                Symbol = "Rogaining",
                Lat = (decimal)c.Latitude.ToDouble(),
                Lon = (decimal)c.Longitude.ToDouble()
            };

            int result = POIDatabase.SavePOI(p);
            DisplayMapItems.AddPOIToMap();

            return result;
        }

        public static UniversalTransverseMercator? LatLontoUTM(double latitude, double longitude)
        {
            try
            {
                Coordinate c = new(latitude, longitude);
                UniversalTransverseMercator utm = c.UTM;
                return utm;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Failed to convert lat: '{latitude}', lon: '{longitude}' to UTM format");
            }

            return null;
        }

    }
}