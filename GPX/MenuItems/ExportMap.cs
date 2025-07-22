using Android.Views;
using Android.Widget;
using hajk.Data;
using SharpGPX;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using hajk.Models;

namespace hajk.GPX
{
    partial class Menus
    {
        public static void ExportMap(GPXViewHolder? vh, ViewGroup parent)
        {
            Log.Information($"Export Map '{vh?.Name.Text}'");

            Android.Views.View? view2 = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.get_userinput, parent, false);
            AndroidX.AppCompat.App.AlertDialog.Builder alertbuilder2 = new(parent.Context);
            alertbuilder2.SetView(view2);
            var userdata2 = view2.FindViewById<EditText>(Resource.Id.editText);
            userdata2.Text = DateTime.Now.ToString("yyyy-MM-dd HH-mm") + " - " + vh.Name.Text + ".mbtiles";

            alertbuilder2.SetCancelable(false)
            .SetPositiveButton(Resource.String.Submit, delegate
            {
                string? DownLoadFolder = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
                if (DownLoadFolder != null)
                {
                    string mbtTilesPath = DownLoadFolder + "/" + userdata2.Text;

                    var route_to_download = RouteDatabase.GetRouteAsync(vh.Id).Result;
                    GpxClass gpx_to_download = GpxClass.FromXml(route_to_download.GPX);

                    if (vh.GPXType == GPXType.Track)
                    {
                        Import.GetloadOfflineMap(gpx_to_download.Tracks[0].GetBounds(), vh.Id, mbtTilesPath);
                    }

                    if (vh.GPXType == GPXType.Route)
                    {
                        Import.GetloadOfflineMap(gpx_to_download.Routes[0].GetBounds(), vh.Id, mbtTilesPath);
                    }
                }
            })
            .SetNegativeButton(Resource.String.Cancel, delegate
            {
                alertbuilder2.Dispose();
            });
            AndroidX.AppCompat.App.AlertDialog dialog2 = alertbuilder2.Create();
            dialog2.Show();
        }
    }
}
