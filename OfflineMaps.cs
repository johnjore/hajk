using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Mapsui.Layers;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using BruTile.MbTiles;

namespace hajk
{
    class OfflineMaps
    {
        public static void LoadMap()
        {
            var strRoute = string.Empty;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var result = await FilePicker.PickAsync();
                    if (result != null)
                    {
                        MainActivity.map.Layers.Add(CreateMbTilesLayer(result.FullPath, "regular"));
                    }
                }
                catch (Exception)
                {
                    // The user canceled or something went wrong
                }
            });
        }

        public static TileLayer CreateMbTilesLayer(string path, string name)
        {
            var mbTilesTileSource = new MbTilesTileSource(new SQLiteConnectionString(path, true));
            var mbTilesLayer = new TileLayer(mbTilesTileSource) { Name = name };
            return mbTilesLayer;
        }
    }
}
