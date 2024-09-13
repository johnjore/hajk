using Kotlin.JS;
using System;
using System.Collections.Generic;

namespace hajk.Models.Elevation
{
    public class Location
    {
        public double lat { get; set; }
        public double lng { get; set; }
    }

    public class Result
    {
        public string? dataset { get; set; }
        public double elevation { get; set; }
        public Location? location { get; set; }
    }

    public class ElevationData
    {
        public IList<Result>? results { get; set; }
        public string? status { get; set; }
    }
}
