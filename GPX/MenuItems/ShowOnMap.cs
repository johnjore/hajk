using Android.Views;
using AndroidX.Fragment.App;
using hajk;
using hajk.Data;
using hajk.Fragments;
using hajk.GPX;
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
        public static void ShowOnMap(GPXViewHolder vh, ViewGroup parent)
        {
            Log.Information($"Show route on map '{vh.Name.Text}'");

            //Enumerate route/tracks on Layer, and only add if not already displayed
            /**/

            //Get the route and optimize
            var routetrack_2 = RouteDatabase.GetRouteAsync(vh.Id).Result;
            GpxClass gpx_2 = GPXOptimize.Optimize(GpxClass.FromXml(routetrack_2.GPX));

            if (routetrack_2.GPXType == GPXType.Track)
            {
                gpx_2.Routes.Add(gpx_2.Tracks[0].ToRoutes()[0]);
            }

            string? mapRouteTrack_2 = Import.ConvertRouteToLineString(gpx_2.Routes[0]);

            //Add GPX to Map
            DisplayMapItems.AddRouteToMap(mapRouteTrack_2, routetrack_2.GPXType, true, vh.Name.Text);

            //Center on imported route
            var bounds_2 = gpx_2.GetBounds();
            //Point p_1 = Utils.Misc.CalculateCenter((double)bounds_1.maxlat, (double)bounds_1.minlon, (double)bounds_1.minlat, (double)bounds_1.maxlon);
            //var sphericalMercatorCoordinate_1 = SphericalMercator.FromLonLat(p_1.X, p_1.Y);
            //Fragment_map.mapControl.Navigator.CenterOn(sphericalMercatorCoordinate_1);

            //Zoom
            //Fragment_map.mapControl.Navigator.ZoomTo(PrefsActivity.MaxZoom);

            //Show the full route
            var (x1, y1) = SphericalMercator.FromLonLat((double)bounds_2.maxlon, (double)bounds_2.minlat);
            var (x2, y2) = SphericalMercator.FromLonLat((double)bounds_2.minlon, (double)bounds_2.maxlat);
            Fragment_map.mapControl?.Map.Navigator.ZoomToBox(new MRect(x1, y1, x2, y2), MBoxFit.Fit);

            //Switch to map
            if (parent.Context != null)
            {
                ProcessFragmentChanges.SwitchFragment(Fragment_Preferences.Fragment_Map, (FragmentActivity)parent.Context);
            }
        }
    }
}
