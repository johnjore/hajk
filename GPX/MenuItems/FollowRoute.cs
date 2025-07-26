using Android.Views;
using AndroidX.Fragment.App;
using hajk.Data;
using hajk.Fragments;
using Mapsui;
using Mapsui.Projections;
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
        public static void FollowRoute(GPXViewHolder vh, ViewGroup parent)
        {
            Log.Information($"Follow route or track '{vh.Name.Text}'");

            //Get the route or track, and optimize it
            var routetrack = RouteDatabase.GetRouteAsync(vh.Id).Result;
            GpxClass gpx = GPXOptimize.Optimize(GpxClass.FromXml(routetrack.GPX));

            if (routetrack.GPXType == GPXType.Track)
            {
                gpx.Routes.Add(gpx.Tracks[0].ToRoutes()[0]);
            }

            string? mapRoute = Import.ConvertRouteToLineString(gpx.Routes[0]);

            //Add GPX to Map
            DisplayMapItems.AddRouteToMap(mapRoute, GPXType.Route, true, vh.Name.Text + "|" + vh.Id.ToString());

            //Center on imported route
            var bounds = gpx.GetBounds();
            //Point p = Utils.Misc.CalculateCenter((double)bounds.maxlat, (double)bounds.minlon, (double)bounds.minlat, (double)bounds.maxlon);
            //var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(p.X, p.Y);
            //Fragment_map.mapControl.Map.Navigator.CenterOn(sphericalMercatorCoordinate);

            //Zoom
            //Fragment_map.mapControl.Map.Navigator.ZoomTo(PrefsActivity.MaxZoom);

            //Show the full route
            var (x1, y1) = SphericalMercator.FromLonLat((double)bounds.maxlon, (double)bounds.minlat);
            var (x2, y2) = SphericalMercator.FromLonLat((double)bounds.minlon, (double)bounds.maxlat);
            Fragment_map.mapControl?.Map.Navigator.ZoomToBox(new MRect(x1, y1, x2, y2), MBoxFit.Fit);

            //Switch to map
            ProcessFragmentChanges.SwitchFragment(Fragment_Preferences.Fragment_Map, (FragmentActivity)parent?.Context);

            //Save Route for off-route detection
            MainActivity.ActiveRoute = gpx;

            //Start recording
            RecordTrack.StartTrackTimer();
        }
    }
}
