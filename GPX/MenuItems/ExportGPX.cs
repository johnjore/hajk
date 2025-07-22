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
        public static void ExportGPX(GPXViewHolder? vh, ViewGroup parent)
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
            .SetPositiveButton(Resource.String.Submit, delegate
            {
                //Make sure folder exists
                if (Directory.Exists(Fragment_Preferences.ShareFolder) == false)
                    Directory.CreateDirectory(Fragment_Preferences.ShareFolder);

                //Get the route
                var route_to_export = RouteDatabase.GetRouteAsync(vh.Id).Result;
                GpxClass gpx_to_export = GpxClass.FromXml(route_to_export.GPX);

                if (vh.GPXType == GPXType.Track)
                {
                    //Clear some fields as they are internal use only
                    for (int i = 0; i < gpx_to_export.Tracks[0].trkseg[0].trkpt.Count; i++)
                    {
                        gpx_to_export.Tracks[0].trkseg[0].trkpt[i].src = "";
                        gpx_to_export.Tracks[0].trkseg[0].trkpt[i].cmt = "";
                    }
                }
                else if (vh.GPXType == GPXType.Route)
                {
                    //Clear some fields as they are internal use only
                    for (int i = 0; i < gpx_to_export.Routes[0].rtept.Count; i++)
                    {
                        gpx_to_export.Routes[0].rtept[i].src = "";
                        gpx_to_export.Routes[0].rtept[i].cmt = "";
                    }
                }
                else
                {
                    Log.Fatal($"GPXType not supported");
                }

                //Sanitize the filename
                string safeName = FileNameSanitizer.Sanitize(Path.GetFileNameWithoutExtension(userdata.Text));
                string? fileToShare = Path.Combine(Fragment_Preferences.ShareFolder, safeName + ".gpx");

                gpx_to_export.ToFile(fileToShare);
                Share.ShareFile(Android.App.Application.Context, fileToShare, "application/gpx+xml");
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
