using Android.App;
using Android.Content;
using Android.Graphics.Text;
using Android.Locations;
using Android.Widget;
using GPXUtils;
using Microcharts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//https://www.nps.gov/shen/planyourvisit/how-to-determine-hiking-difficulty.htm

namespace hajk
{
    internal class ShenandoahsHikingDifficulty
    {
        public static float CalculateScale(float distance_km, int ascent_m)
        {
            try
            {
                float shenandoahscale = (float)Math.Sqrt((double)((decimal)DistanceConverter.ConvertMetersToFeet(ascent_m) *  2 * (decimal)DistanceConverter.ConvertKilometersToMiles(distance_km)));
                return shenandoahscale;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to calculate shenandoah's scale");
            }

            return (-1);
        }

        public static string CalculateRating(float shenandoahscale)
        {
            try
            {
                if (Platform.CurrentActivity == null)
                {
                    return ("N/A");
                }

                if (shenandoahscale <= 50)
                    return (Platform.CurrentActivity.GetString(Resource.String.ShenandoahEasy));

                if (shenandoahscale <= 100)
                    return (Platform.CurrentActivity.GetString(Resource.String.ShenandoahModerate));

                if (shenandoahscale <= 150)
                    return (Platform.CurrentActivity.GetString(Resource.String.ShenandoahModerateStrenous));

                if (shenandoahscale <= 200)
                    return (Platform.CurrentActivity.GetString(Resource.String.ShenandoahStrenuous));

                return (Platform.CurrentActivity.GetString(Resource.String.ShenandoahVeryStrenuous));
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to calculate shenandoah's rating");
            }

            return "N/A";
        }

        public static void UpdateTextField(TextView? field, float ShenandoahsHikingDifficultyScale, string ShenandoahsHikingDifficultyRating)
        {
            if (field != null)
            {
                if (ShenandoahsHikingDifficultyScale <= 0)
                {
                    field.Text = $"Shenandoah: N/A";
                }
                else if (ShenandoahsHikingDifficultyScale > 0)
                {
                    field.Text = $"Shenandoah: {ShenandoahsHikingDifficultyScale:0} / {ShenandoahsHikingDifficultyRating}";
                }
            }
            else
            {
                Serilog.Log.Error("Failed to display shenandoah's scale / rating");
            }
        }
    }
}
