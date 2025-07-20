using System;
using System.Collections.Generic;
using System.Linq;

namespace hajk
{
    public static class ElevationAnalyzer
    {
        private static readonly int _smoothingWindow = 5;
        private static readonly double _threshold = 3.0;

        private static readonly Queue<double> _recentElevations = new Queue<double>(_smoothingWindow);
        private static double? _lastSmoothedElevation = null;

        public static double TotalAscent { get; private set; }
        public static double TotalDescent { get; private set; }

        /// <summary>
        /// Adds a new raw elevation, returns smoothed elevation, and updates ascent/descent.
        /// </summary>
        public static double AddElevation(double rawElevation)
        {
            if (_recentElevations.Count == _smoothingWindow)
                _recentElevations.Dequeue();

            _recentElevations.Enqueue(rawElevation);
            double smoothed = _recentElevations.Average();

            if (_lastSmoothedElevation.HasValue)
            {
                double delta = smoothed - _lastSmoothedElevation.Value;

                if (delta > _threshold)
                    TotalAscent += delta;
                else if (delta < -_threshold)
                    TotalDescent -= delta;
            }

            _lastSmoothedElevation = smoothed;
            return smoothed;
        }

        /// <summary>
        /// Resets ascent, descent, and elevation history.
        /// </summary>
        public static void Reset()
        {
            _recentElevations.Clear();
            _lastSmoothedElevation = null;
            TotalAscent = 0;
            TotalDescent = 0;
        }
    }
}