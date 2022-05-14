﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeoTiffCOG.Struture
{
    [Serializable()]
    public class HeightMap
    {
        public HeightMap(int width, int height)
        {
            Width = Math.Abs(width);
            Height = Math.Abs(height);
            Coordinates = null;
            Count = width * height;
            Minimum = 15000;
            Maximum = -15000;
        }

        private BoundingBox _bbox;
        public BoundingBox BoundingBox
        {
            get
            {
                if (_bbox == null)
                {
                    //Logger.Info("Computing bbox...");
                    _bbox = this.Coordinates.GetBoundingBox();
                }
                return _bbox;
            }
            set
            {
                _bbox = value;
            }
        }

        public IEnumerable<GeoPoint> Coordinates { get; set; }

        /// <summary>
        /// Coordinate count
        /// </summary>
        public int Count { get; set; }

        public float Minimum { get; set; }
        public float Maximum { get; set; }
        public float Range
        {
            get { return Maximum - Minimum; }
        }

        public int Width { get; private set; }
        public int Height { get; private set; }

        public HeightMap Clone()
        {
            return (HeightMap)this.MemberwiseClone();
        }

    }
}
