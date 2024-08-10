using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hajk
{
    class CompassData
    {
        private static double RotationAngle;

        public static double GetRotationAngle()
        {
            return RotationAngle;
        }


        public static void EnableCompass()
        {
            if (Compass.Default.IsSupported && !Compass.Default.IsMonitoring)
            {
                Compass.Default.ReadingChanged += OnCompassReadingChanged;
                Compass.Default.Start(SensorSpeed.UI, applyLowPassFilter: true);
            }
        }

        public static void DisableCompass()
        {
            if (Compass.Default.IsSupported && Compass.Default.IsMonitoring)
            {
                Compass.Default.Stop();
                Compass.Default.ReadingChanged -= OnCompassReadingChanged;
            }
        }

        private static void OnCompassReadingChanged(object? sender, CompassChangedEventArgs e)
        {
            //Serilog.Log.Debug($"HeadingMagneticNorth: {e.Reading.HeadingMagneticNorth}, RotationAngle: {360 - e.Reading.HeadingMagneticNorth}");
            RotationAngle = 360 - e.Reading.HeadingMagneticNorth;
        }
    }
}
