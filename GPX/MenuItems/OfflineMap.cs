using Android.Views;
using Android.Widget;
using hajk.Data;
using hajk.Fragments;
using SharpGPX;
using SharpGPX.GPX1_1;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using hajk.Models;

namespace hajk.GPX
{
    partial class Menus
    {
        public static async Task Download_And_Save_Offline_Map(GPXViewHolder vh, ViewGroup parent, int menuitem)
        {
            if (vh == null)
            {
                Serilog.Log.Fatal("vh can't be null here");
                return;
            }

            Log.Information(Resource.String.download_and_save_offline_map + " '" + vh.Name?.Text + "' / '" + vh.Id + "'");

            //If using OSM, cancel out here
            string? TileBrowseSource = Preferences.Get(Platform.CurrentActivity?.GetString(Resource.String.OSM_Browse_Source), Fragment_Preferences.TileBrowseSource);
            if (TileBrowseSource.Equals("OpenStreetMap", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("Can't use OSM as a bulkdownload server");
                Toast.MakeText(parent.Context, "Can't use OpenStreetMap Server for bulk downloading.", ToastLength.Long).Show();

                return;
            }

            var route_to_download = RouteDatabase.GetRouteAsync(vh.Id).Result;
            GpxClass gpx_to_download = GpxClass.FromXml(route_to_download.GPX);


            //Get map tiles
            if (vh.GPXType == GPXType.Route && gpx_to_download.Routes.Count == 1)
            {
                //Download elevation data first
                await Elevation.DownloadElevationData(gpx_to_download);

                await Import.GetloadOfflineMap(gpx_to_download.Routes[0].GetBounds(), vh.Id);
            }
            else if (vh.GPXType == GPXType.Track && gpx_to_download.Tracks.Count == 1)
            {
                //await Import.GetloadOfflineMap(gpx_to_download.Tracks[0].GetBounds(), vh.Id);
            }
            else
            {
                Serilog.Log.Fatal("Unknown and unhandled GPXType or too many routes/tracks in GPX");
                return;
            }

            //Update route with elevation data
            await Task.Run(() =>
            {
                if (vh.GPXType == GPXType.Route)
                {
                    rteType? updated_route = Elevation.LookupElevationData(gpx_to_download.Routes[0]);

                    if (updated_route != null)
                    {
                        gpx_to_download.Routes.Clear();
                        gpx_to_download.Routes.Add(updated_route);
                        (route_to_download.Ascent, route_to_download.Descent) = Elevation.CalculateAscentDescent(updated_route);

                        //Set Start position
                        route_to_download.GPXStartLocation = $"{gpx_to_download.Routes[0].rtept[0].lat.ToString(CultureInfo.InvariantCulture)},{gpx_to_download.Routes[0].rtept[0].lon.ToString(CultureInfo.InvariantCulture)}";
                    }
                }
                else if (vh.GPXType == GPXType.Track)
                {
                    /*trkType? updated_track = Elevation.LookupElevationData(gpx_to_download.Tracks[0]);

                    if (updated_track != null)
                    {
                        gpx_to_download.Tracks.Clear();
                        gpx_to_download.Tracks.Add(updated_track);
                        (route_to_download.Ascent, route_to_download.Descent) = Elevation.CalculateAscentDescent(updated_track);
                    }*/

                    //Elevation data
                    (route_to_download.Ascent, route_to_download.Descent) = Elevation.CalculateAscentDescent(gpx_to_download.Tracks[0]);

                    //Set Start position
                    route_to_download.GPXStartLocation = $"{gpx_to_download.Tracks[0].trkseg[0].trkpt[0].lat.ToString(CultureInfo.InvariantCulture)},{gpx_to_download.Tracks[0].trkseg[0].trkpt[0].lon.ToString(CultureInfo.InvariantCulture)}";
                }
                else
                {
                    Serilog.Log.Fatal("Unknown and unhandled GPXType");
                    return;
                }
            });

            //Elevation plot, if we have data
            if (route_to_download.Ascent > 0 || route_to_download.Descent > 0)
            {
                var elevationModel = Helpers.CreatePlotModel(gpx_to_download);
                if (elevationModel != null && vh.TrackRouteElevation != null)
                {
                    vh.TrackRouteElevation.Model = elevationModel;

                    Fragment_gpx.mAdapter?.NotifyItemChanged(menuitem);
                }
            }

            //Naismith's Travel Time
            (int travel_hours, int travel_min) = Naismith.CalculateTime(route_to_download.Distance, Fragment_Preferences.naismith_speed_kmh, route_to_download.Ascent, route_to_download.Descent);
            route_to_download.NaismithTravelTime = $"{string.Format("{0:D2}", travel_hours)}:{string.Format("{0:D2}", travel_min)}";
            if (vh.NaismithTravelTime != null && travel_hours > -1 && travel_min > -1)
            {
                vh.NaismithTravelTime.Text = $"Naismith: {route_to_download.NaismithTravelTime}";
            }

            //Shenandoah's Hiking Difficulty
            route_to_download.ShenandoahsScale = ShenandoahsHikingDifficulty.CalculateScale(route_to_download.Distance, route_to_download.Ascent);
            string ShenandoahsHikingDifficultyRating = ShenandoahsHikingDifficulty.CalculateRating(route_to_download.ShenandoahsScale);
            ShenandoahsHikingDifficulty.UpdateTextField(vh?.ShenandoahsHikingDifficulty, route_to_download.ShenandoahsScale, ShenandoahsHikingDifficultyRating);

            //Update record with additional data
            route_to_download.GPX = gpx_to_download.ToXml();

            //Update GUI
            if (vh.Ascent != null && vh.Descent != null)
            {
                vh.Ascent.Text = $"Ascent: {route_to_download.Ascent}m";
                vh.Descent.Text = $"Descent: {route_to_download.Descent}m";

                Fragment_gpx.mAdapter?.NotifyItemChanged(menuitem);
            }

            //Create / Update thumbsize map
            Toast.MakeText(Platform.AppContext, "Creating new overview image", ToastLength.Short)?.Show();
            string? ImageBase64String = DisplayMapItems.CreateThumbnail(vh.GPXType, gpx_to_download);
            if (ImageBase64String != null)
            {
                route_to_download.ImageBase64String = ImageBase64String;

                var bitmap = Utils.Misc.ConvertStringToBitmap(ImageBase64String);
                if (bitmap != null)
                {
                    vh?.TrackRouteMap?.SetImageResource(0);
                    vh?.TrackRouteMap?.SetImageBitmap(bitmap);
                }

                Fragment_gpx.mAdapter?.NotifyItemChanged(menuitem);
            }

            //Save to the database
            RouteDatabase.SaveRoute(route_to_download);

            Toast.MakeText(Platform.AppContext, "Finished downloads", ToastLength.Short)?.Show();

            Fragment_gpx.mAdapter.UpdateItems(route_to_download.GPXType);
        }
    }
}
