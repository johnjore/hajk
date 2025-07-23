using hajk.Data;
using hajk.Fragments;
using hajk.Models;
using Serilog;
using SharpGPX;
using System.Threading.Tasks;

namespace hajk.GPX
{
    partial class Menus
    {
        public static async Task ReverseRoute(GPXViewHolder vh)
        {
            if (vh?.Id == null)
            {
                Log.Fatal("vh.Id can't be null here");
                return;
            }
                

            Log.Information($"Reverse route '{vh.Name?.Text}'");

            // Retrieve the route from the database
            GPXDataRouteTrack? routeToReverse = RouteDatabase.GetRouteAsync(vh.Id)?.Result;
            if (routeToReverse == null)
            {
                Log.Fatal($"routetoReverse can't be null here");
                return;
            }

            // Get the GPX data and reverse the items based on type (track or route)
            GpxClass gpxToReverse = GpxClass.FromXml(routeToReverse?.GPX);
            if (routeToReverse?.GPXType == GPXType.Track)
            {
                foreach (var track in gpxToReverse.Tracks)
                {
                    foreach (var segment in track.trkseg)
                    {
                        segment.trkpt.Reverse();
                    }
                }
            }
            else // GPXType.Route
            {
                gpxToReverse.Routes[0].rtept.Reverse();
            }

            // Swap ascent and descent values and update calculatations
            if (routeToReverse?.Ascent != null && routeToReverse?.Descent != null)
            {
                (routeToReverse.Descent, routeToReverse.Ascent) = (routeToReverse.Ascent, routeToReverse.Descent);

                // Recalculate travel time using Naismith's Rule
                (int hours, int minutes) = Naismith.CalculateTime(
                    routeToReverse.Distance,
                    Fragment_Preferences.naismith_speed_kmh,
                    routeToReverse.Ascent,
                    routeToReverse.Descent
                );
                routeToReverse.NaismithTravelTime = $"{hours:D2}:{minutes:D2}";

                // Shenandoah
                routeToReverse.ShenandoahsScale = ShenandoahsHikingDifficulty.CalculateScale(
                    routeToReverse.Distance,
                    routeToReverse.Ascent
                );
            }

            // Generate new thumbnail
            string? ImageBase64String = DisplayMapItems.CreateThumbnail(
                routeToReverse?.GPXType,
                gpxToReverse
            );
            if (ImageBase64String != null)
            {
                routeToReverse.ImageBase64String = ImageBase64String;
            }

            // Update metadata for the reversed route
            routeToReverse.Name += " - reversed";
            routeToReverse.Description += " - reversed";
            routeToReverse.Id = 0; // Ensure this gets saved as a new entry
            routeToReverse.GPX = gpxToReverse.ToXml();

            // Save the new route entry
            var dbID = RouteDatabase.SaveRoute(routeToReverse);

            //Update map tiles with new reference
            await Task.Run(async() =>
            {
                await Import.GetloadOfflineMap(gpxToReverse.Routes[0].GetBounds(), dbID);
            });

            // Update UI: add the new route to the adapter and refresh the RecyclerView
            Adapter.GpxAdapter.mGpxData?.Insert(routeToReverse);
            Fragment_gpx.mAdapter?.NotifyDataSetChanged();
        }
    }
}
