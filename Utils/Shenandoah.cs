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
        public static (decimal, string) CalculateScale(float distance_km, int ascent_m)
        {
            if (Platform.CurrentActivity == null)
            {
                return (-1, string.Empty);
            }

            try
            {
                decimal shenandoahscale = (decimal)Math.Sqrt((double)((decimal)DistanceConverter.ConvertMetersToFeet(ascent_m) *  2 * (decimal)DistanceConverter.ConvertKilometersToMiles(distance_km)));

                if (shenandoahscale <=  50)
                    return (shenandoahscale, Platform.CurrentActivity.GetString(Resource.String.ShenandoahEasy));

                if (shenandoahscale <= 100)
                    return (shenandoahscale, Platform.CurrentActivity.GetString(Resource.String.ShenandoahModerate));

                if (shenandoahscale <= 150)
                    return (shenandoahscale, Platform.CurrentActivity.GetString(Resource.String.ShenandoahModerateStrenous));

                if (shenandoahscale <= 200)
                    return (shenandoahscale, Platform.CurrentActivity.GetString(Resource.String.ShenandoahStrenuous));

                return (shenandoahscale, Platform.CurrentActivity.GetString(Resource.String.ShenandoahVeryStrenuous));
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to calculate shenandoah's scale / rating");
            }

            return (-1, string.Empty);
        }

        public static void UpdateTextField(TextView? field, decimal ShenandoahsHikingDifficultyScale, string ShenandoahsHikingDifficultyRating)
        {
            if (field != null && ShenandoahsHikingDifficultyScale > -1)
            {
                field.Text = $"Shenandoah: {ShenandoahsHikingDifficultyScale:0} / {ShenandoahsHikingDifficultyRating}";
            }
            else
            {
                Serilog.Log.Error("Failed to display shenandoah's scale / rating");
            }
        }
    }
}
