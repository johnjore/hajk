using System;
using System.Collections.Generic;
using System.Linq;

public static class RollingElevationAnalyzer
{
    private static int _smoothingWindow = 3;
    private static double _noiseBand = 0.35;
    private static double _minimumGain = 4.0;

    private static Queue<double> _recentElevations = new Queue<double>();
    private static double _prevSmoothed = double.NaN;
    private static double _gainAccumulator = 0;
    private static double _lossAccumulator = 0;

    public static double TotalAscent { get; private set; }
    public static double TotalDescent { get; private set; }

    /// <summary>
    /// Initialize or reset the analyzer with optional parameters.
    /// </summary>
    public static void Initialize(int smoothingWindow = 3, double noiseBand = 0.35, double minimumGain = 4.0)
    {
        if (smoothingWindow <= 0)
            throw new ArgumentException("Smoothing window must be positive", nameof(smoothingWindow));
        if (noiseBand < 0)
            throw new ArgumentException("Noise band cannot be negative", nameof(noiseBand));
        if (minimumGain < 0)
            throw new ArgumentException("Minimum gain cannot be negative", nameof(minimumGain));

        _smoothingWindow = smoothingWindow;
        _noiseBand = noiseBand;
        _minimumGain = minimumGain;

        _recentElevations = new Queue<double>();
        _prevSmoothed = double.NaN;
        _gainAccumulator = 0;
        _lossAccumulator = 0;
        TotalAscent = 0;
        TotalDescent = 0;
    }

    /// <summary>
    /// Add a new elevation point, update ascent/descent totals, and return smoothed elevation.
    /// </summary>
    public static double AddElevation(double newElevation)
    {
        if (_recentElevations.Count == _smoothingWindow)
            _recentElevations.Dequeue();

        _recentElevations.Enqueue(newElevation);

        double smoothed = _recentElevations.Average();

        if (double.IsNaN(_prevSmoothed))
        {
            _prevSmoothed = smoothed;
            return smoothed;
        }

        double delta = smoothed - _prevSmoothed;

        if (delta > _noiseBand)
        {
            _gainAccumulator += delta;
            if (_gainAccumulator >= _minimumGain)
            {
                TotalAscent += _gainAccumulator;
                _gainAccumulator = 0;
                _lossAccumulator = 0;
            }
        }
        else if (delta < -_noiseBand)
        {
            _lossAccumulator += -delta;
            if (_lossAccumulator >= _minimumGain)
            {
                TotalDescent += _lossAccumulator;
                _lossAccumulator = 0;
                _gainAccumulator = 0;
            }
        }
        else
        {
            // delta within noise band — ignore
        }

        _prevSmoothed = smoothed;
        return smoothed;
    }
}
