using hajk.Data;
using hajk.Fragments;
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
        public static async Task DeleteRoute(GPXViewHolder vh)
        {
            Log.Information($"Delete route '{vh.Name.Text}'");

            Show_Dialog msg1 = new(Platform.CurrentActivity);
            if (await msg1.ShowDialog($"Delete", $"Delete '{vh.Name.Text}' ?", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) == Show_Dialog.MessageResult.YES)
            {
                //Remove map tiles
                MBTilesWriter.PurgeMapDB(vh.Id);

                //Remove from route DB
                _ = RouteDatabase.DeleteRouteAsync(vh.Id);

                //Remove from GUI
                Adapter.GpxAdapter.mGpxData.RemoveAt(vh.AdapterPosition);
                Fragment_gpx.mAdapter.NotifyDataSetChanged();
            }
        }
    }
}
