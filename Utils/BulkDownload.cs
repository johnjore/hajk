using Android.App;
using Android.OS;
using Android.Widget;
using hajk.Data;
using hajk.Models;
using SharpGPX;
using SharpGPX.GPX1_1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hajk.Utilities
{
    internal class BulkDownload
    {
        public static void RunBulkDownload()
        {
            bool[] checkedItems = [
                Preferences.Get("BulkDownloadRoutes", true),
                Preferences.Get("BulkDownloadTracks", true),
                Preferences.Get("BulkDownloadPOI", true),
            ];

            string[] items = [
                "Routes",
                "Tracks",
                "POIs",
            ];

            //Create dialogbox
            AlertDialog.Builder builder = new(Platform.CurrentActivity);
            builder.SetTitle(Resource.String.BulkDownloadOptions);

            //Update GUI
            builder.SetMultiChoiceItems(items, checkedItems, (senderDialog, args) =>
            {
                checkedItems[args.Which] = args.IsChecked;
            });

            //Start the Downloads
            builder.SetPositiveButton(Resource.String.Ok, (senderDialog, args) =>
            {
                //Update/save preferences
                Preferences.Set("BulkDownloadRoutes", checkedItems[0]);
                Preferences.Set("BulkDownloadTracks", checkedItems[1]);
                Preferences.Set("BulkDownloadPOI", checkedItems[2]);

                if (Preferences.Get("BulkDownloadRoutes", true) == false &&
                    Preferences.Get("BulkDownloadTracks&TrackData", true) == false &&
                    Preferences.Get("BulkDownloadPOI", true) == false)
                {
                    Serilog.Log.Warning("Nothing selected to download");
                    Toast.MakeText(Platform.AppContext, "Nothing to download", ToastLength.Long)?.Show();
                    return;
                }

                Task.Run(() =>
                {
                    PerformBulkDownloads();
                });
            });

            builder.SetNegativeButton(Resource.String.Cancel, (senderDialog, args) =>
            {
                //Update/save preferences
                Preferences.Set("BulkDownloadRoutes", checkedItems[0]);
                Preferences.Set("BulkDownloadTracks", checkedItems[1]);
                Preferences.Set("BulkDownloadPOI", checkedItems[2]);
            });

            builder.Create();
            builder.Show();
        }

        private static async void PerformBulkDownloads()
        {
            if (Preferences.Get("BulkDownloadRoutes", true))
            {
                GpxData? mGpxData = new GpxData(Models.GPXType.Route);
                await Task.Run(async () =>
                {
                    if (Looper.MyLooper() == null)
                    {
                        Looper.Prepare();
                    }

                    for (int i = 0; i < mGpxData.NumGpx; i++)
                    {
                        Serilog.Log.Information($"Processing '{mGpxData[i].Name}'");

                        if (await Import.UpdateRouteOrTrack(mGpxData[i].Id) == false)
                        {
                            Serilog.Log.Information($"Failed to update '{mGpxData[i].Name}'");
                            Toast.MakeText(Platform.AppContext, $"Failed to update '{mGpxData[i].Name}'", ToastLength.Short)?.Show();
                        }
                        else
                        {
                            Serilog.Log.Information($"Done updating '{mGpxData[i].Name}'");
                            Toast.MakeText(Platform.AppContext, $"Updated '{mGpxData[i].Name}'", ToastLength.Short)?.Show();
                        }
                    }
                });
            }

            if (Preferences.Get("BulkDownloadTracks", true))
            {
                GpxData? mGpxData = new GpxData(Models.GPXType.Track);
                await Task.Run(async () =>
                {
                    if (Looper.MyLooper() == null)
                    {
                        Looper.Prepare();
                    }

                    for (int i = 0; i < mGpxData.NumGpx; i++)
                    {
                        Serilog.Log.Information($"Processing '{mGpxData[i].Name}'");

                        if (await Import.UpdateRouteOrTrack(mGpxData[i].Id) == false)
                        {
                            Serilog.Log.Information($"Failed to update '{mGpxData[i].Name}'");
                            Toast.MakeText(Platform.AppContext, $"Failed to update '{mGpxData[i].Name}'", ToastLength.Short)?.Show();
                        }
                        else
                        {
                            Serilog.Log.Information($"Done updating '{mGpxData[i].Name}'");
                            Toast.MakeText(Platform.AppContext, $"Updated '{mGpxData[i].Name}'", ToastLength.Short)?.Show();
                        }
                    }
                });
            }

            if (Preferences.Get("BulkDownloadPOI", true))
            {
                await Task.Run(async () =>
                {
                    List<GPXDataPOI> POIs = POIDatabase.GetPOIAsync().Result;

                    foreach (GPXDataPOI POI in POIs)
                    {
                        Serilog.Log.Debug(POI.Name);

                        GpxClass gpx = new()
                        {
                            Waypoints = [new()
                            {
                                lat = (decimal)POI.Lat,
                                lon = (decimal)POI.Lon,
                            }]
                        };

                        //Download Elevation data
                        await Elevation.DownloadElevationData(gpx);

                        var bounds = gpx.Waypoints.GetBounds();

                        //Get all missing tiles
                        await DownloadRasterImageMap.DownloadMap(bounds, 999999, false);
                    }
                });
            }
        }
    }
}
