using System;
using SQLite;

namespace hajk.Models
{
    public class Map
    {
        public string Name { get; set; }
        public int ZoomMin { get; set; }
        public int ZoomMax { get; set; }
        public double BoundsLeft { get; set; }
        public double BoundsBottom { get; set; }
        public double BoundsTop { get; set; }
        public double BoundsRight { get; set; }
    }
}
