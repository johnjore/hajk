using Android.Content;
using Android.Views;
using Android.Widget;
using hajk.Data;
using hajk.Models;
using Serilog;
using SharpGPX;
using System;
namespace hajk.GPX
{
    partial class Menus
    {
        public static void ExportMap(GPXViewHolder? vh, ViewGroup parent)
        {
            if (vh == null || parent?.Context == null)
                return;

            Log.Information($"Export route '{vh?.Name?.Text}'");

            Android.Views.View? view = LayoutInflater.From(parent?.Context).Inflate(Resource.Layout.get_userinput, parent, false);
            AndroidX.AppCompat.App.AlertDialog.Builder? alertbuilder = new(parent?.Context);
            alertbuilder.SetView(view);
            EditText? userdata = view?.FindViewById<EditText>(Resource.Id.editText);

            //Suggested sanitized filename
            userdata.Text = FileNameSanitizer.Sanitize(DateTime.Now.ToString("yyMMdd") + "-" + vh?.Name?.Text);

            alertbuilder?.SetCancelable(false)
            .SetPositiveButton(Resource.String.Submit, async delegate
            {
                //Make sure folder exists
                if (Directory.Exists(Fragment_Preferences.ShareFolder) == false)
                    Directory.CreateDirectory(Fragment_Preferences.ShareFolder);

                //Sanitize the filename
                string safeName = FileNameSanitizer.Sanitize(Path.GetFileNameWithoutExtension(userdata.Text));
                string? fileToShare = Path.Combine(Fragment_Preferences.ShareFolder, safeName + ".mbtiles");

                GPXDataRouteTrack? route_to_export = RouteDatabase.GetRouteAsync(vh.Id)?.Result;
                GpxClass gpx_to_export = GpxClass.FromXml(route_to_export?.GPX);

                //Download
                await Import.GetloadOfflineMap(gpx_to_export.GetBounds(), vh.Id);

                //Export
                await DownloadRasterImageMap.ExportMapTiles(gpx_to_export, fileToShare);

                //Share
                Share.ShareFile(Android.App.Application.Context, fileToShare, "application/octet-stream");
            })
            .SetNegativeButton(Resource.String.Cancel, delegate
            {
                alertbuilder.Dispose();
            });

            AndroidX.AppCompat.App.AlertDialog dialog = alertbuilder?.Create();
            dialog?.Show();
        }
    }
}
