using hajk;
using hajk.Data;
using hajk.Fragments;
using hajk.Models;
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
        public static void ReverseRoute(GPXViewHolder vh)
        {
            Log.Information($"Reverse route '{vh.Name.Text}'");

            //Get the route
            GPXDataRouteTrack route_to_reverse = RouteDatabase.GetRouteAsync(vh.Id).Result;
            GpxClass gpx_to_reverse = GpxClass.FromXml(route_to_reverse.GPX);

            if (route_to_reverse.GPXType == GPXType.Track)
            {
                foreach (SharpGPX.GPX1_1.trkType track in gpx_to_reverse.Tracks)
                {
                    foreach (SharpGPX.GPX1_1.trksegType trkseg in track.trkseg)
                    {
                        trkseg.trkpt.Reverse();
                    }
                }
            }
            else
            {
                gpx_to_reverse.Routes[0].rtept.Reverse();
            }

            //Reverse and save as new entry
            route_to_reverse.Name += " - reversed";
            route_to_reverse.Description += " - reversed";
            route_to_reverse.Id = 0;
            route_to_reverse.GPX = gpx_to_reverse.ToXml();
            RouteDatabase.SaveRoute(route_to_reverse);

            //Update RecycleView with new entry
            //_ = Fragment_gpx.mAdapter.mGpxData.Insert(route_to_reverse);
            Adapter.GpxAdapter.mGpxData.Insert(route_to_reverse);
            Fragment_gpx.mAdapter.NotifyDataSetChanged();
        }
    }
}
