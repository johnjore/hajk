using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimplifyCSharp
{
    public class Simplifier3D<T> : BaseSimplifier<T>
    {
        readonly Func<T, double> _xExtractor;
        readonly Func<T, double> _yExtractor;
        readonly Func<T, double> _zExtractor;

        public Simplifier3D(Func<T, T, Boolean> equalityChecker,
            Func<T, double> xExtractor, Func<T, double> yExtractor, Func<T, double> zExtractor) :
            base(equalityChecker)
        {
            _xExtractor = xExtractor;
            _yExtractor = yExtractor;
            _zExtractor = zExtractor;
        }

        protected override double GetSquareDistance(T p1, T p2)
        {
            double dx = _xExtractor(p1) - _xExtractor(p2);
            double dy = _yExtractor(p1) - _yExtractor(p2);
            double dz = _zExtractor(p1) - _zExtractor(p2);

            return dx * dx + dy * dy + dz * dz;
        }

        protected override double GetSquareSegmentDistance(T p0, T p1, T p2)
        {
            double x0, y0, z0, x1, y1, z1, x2, y2, z2, dx, dy, dz, t;

            x1 = _xExtractor(p1);
            y1 = _yExtractor(p1);
            z1 = _zExtractor(p1);
            x2 = _xExtractor(p2);
            y2 = _yExtractor(p2);
            z2 = _zExtractor(p2);
            x0 = _xExtractor(p0);
            y0 = _yExtractor(p0);
            z0 = _zExtractor(p0);

            dx = x2 - x1;
            dy = y2 - y1;
            dz = z2 - z1;

            if (dx != 0.0d || dy != 0.0d || dz != 0.0d)
            {
                t = ((x0 - x1) * dx + (y0 - y1) * dy + (z0 - z1) * dz)
                        / (dx * dx + dy * dy + dz * dz);

                if (t > 1.0d)
                {
                    x1 = x2;
                    y1 = y2;
                    z1 = z2;
                }
                else if (t > 0.0d)
                {
                    x1 += dx * t;
                    y1 += dy * t;
                    z1 += dz * t;
                }
            }

            dx = x0 - x1;
            dy = y0 - y1;
            dz = z0 - z1;

            return dx * dx + dy * dy + dz * dz;
        }
    }
}