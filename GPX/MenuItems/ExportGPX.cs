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

namespace hajk.GPX
{
    partial class Menus
    {
        public static void ExportGPX(GPXViewHolder vh, ViewGroup parent)
        {
            Log.Information($"Export route '{vh.Name.Text}'");

            Android.Views.View? view = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.get_userinput, parent, false);
            AndroidX.AppCompat.App.AlertDialog.Builder? alertbuilder = new(parent.Context);
            alertbuilder.SetView(view);
            EditText? userdata = view?.FindViewById<EditText>(Resource.Id.editText);
            userdata.Text = DateTime.Now.ToString("yyyy-MM-dd HH-mm") + " - " + vh.Name.Text + ".gpx";

            alertbuilder?.SetCancelable(false)
            .SetPositiveButton(Resource.String.Submit, delegate
            {
                //Get the route
                var route_to_export = RouteDatabase.GetRouteAsync(vh.Id).Result;
                GpxClass gpx_to_export = GpxClass.FromXml(route_to_export.GPX);

                //Clear the src field. Internal only
                for (int i = 0; i < gpx_to_export.Routes[0].rtept.Count; i++)
                {
                    gpx_to_export.Routes[0].rtept[i].src = "";
                }

                string? DownLoadFolder = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
                if (DownLoadFolder != null)
                {
                    string gpxPath = Path.Combine(DownLoadFolder, userdata.Text);
                    gpx_to_export.ToFile(gpxPath);
                }
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
