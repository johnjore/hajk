using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimplifyCSharp
{
    public class Simplifier2D<T> : BaseSimplifier<T>
    {
        readonly Func<T, double> _xExtractor;
        readonly Func<T, double> _yExtractor;

        public Simplifier2D(Func<T, T, Boolean> equalityChecker,
            Func<T, double> xExtractor, Func<T, double> yExtractor) :
            base(equalityChecker)
        {
            _xExtractor = xExtractor;
            _yExtractor = yExtractor;
        }

        protected override double GetSquareDistance(T p1, T p2)
        {
            double dx = _xExtractor(p1) - _xExtractor(p2);
            double dy = _yExtractor(p1) - _yExtractor(p2);

            return dx * dx + dy * dy;
        }

        protected override double GetSquareSegmentDistance(T p0, T p1, T p2)
        {
            double x1 = _xExtractor(p1);
            double y1 = _yExtractor(p1);
            double x2 = _xExtractor(p2);
            double y2 = _yExtractor(p2);
            double x0 = _xExtractor(p0);
            double y0 = _yExtractor(p0);

            double dx = x2 - x1;
            double dy = y2 - y1;

            double t;

            if (dx != 0.0d || dy != 0.0d)
            {
                t = ((x0 - x1) * dx + (y0 - y1) * dy)
                        / (dx * dx + dy * dy);

                if (t > 1.0d)
                {
                    x1 = x2;
                    y1 = y2;
                }
                else if (t > 0.0d)
                {
                    x1 += dx * t;
                    y1 += dy * t;
                }
            }

            dx = x0 - x1;
            dy = y0 - y1;

            return dx * dx + dy * dy;
        }
    }
}