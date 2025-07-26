using Android.Views;
using AndroidX.Fragment.App;
using hajk.Data;
using Serilog;

namespace hajk.GPX
{
    partial class Menus
    {
        public static void ShowOnMap(GPXViewHolder vh, ViewGroup parent)
        {
            var layerName = vh?.Name?.Text + "|" + vh?.Id;
            Log.Information($"Show route on map '{layerName}'");

            //Get the route
            var routetrack = RouteDatabase.GetRouteAsync(vh.Id).Result;

            //Add GPX to Map
            DisplayMapItems.AddRouteTrackToMap(routetrack, true, layerName, true);

            //Switch to map
            if (parent.Context != null)
            {
                ProcessFragmentChanges.SwitchFragment(Fragment_Preferences.Fragment_Map, (FragmentActivity)parent.Context);
            }
        }
    }
}
