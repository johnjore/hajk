using Android.App;
using Android.Content;
using Android.Graphics.Text;
using Android.Locations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hajk
{
    internal class Naismith
    {
        public static (int, int) CalculateTime_min(float distance_km, decimal walking_speed_kmh, int ascent_m, int descent_m)
        {
            try
            {
                //Distance
                decimal time_m = Convert.ToInt32(distance_km) / walking_speed_kmh * 60;

                //Ascent
                time_m += (ascent_m / Fragment_Preferences.naismith_ascent);

                //Descent
                time_m += (descent_m / Fragment_Preferences.naismith_descent);

                //Breaks
                time_m += Math.Floor(time_m / 120) * Fragment_Preferences.naismith_min_per_2hour;

                //Convert to hours and minutes
                int travel_hours = Convert.ToInt16(Math.Floor(time_m / 60));
                int travel_min = Convert.ToInt16(time_m - 60 * travel_hours);

                return (travel_hours, travel_min);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to calculate travel time");
            }

            return (-1, -1);
        }
    }
}
