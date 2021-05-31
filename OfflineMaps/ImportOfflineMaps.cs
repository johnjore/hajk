using Mapsui.Layers;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using BruTile.MbTiles;
using System.IO;
using Serilog;

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
                    var options = new PickOptions
                    {
                        PickerTitle = "Please select a map file",
                        FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                        {
                            /**///What is mime type for mbtiles ?!?
                            //{ DevicePlatform.Android, new string[] { "mbtiles"} },
                            { DevicePlatform.Android, null },
                        })
                    };

                    var result = await FilePicker.PickAsync(options);
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


        public static void LoadAllOfflineMaps()
        {
            string MBTilesPath = MainActivity.rootPath + "/MBTiles";
            
            var filesList = Directory.GetFiles(MBTilesPath);

            //Load Australia First
            foreach (var file in filesList)
            {
                if (file.EndsWith(".mbtiles"))
                {
                    if (file.EndsWith("Country.mbtiles"))
                    {
                        Log.Information($"File {file}");

                        //Map not clear. GPX visible
                        var mbTilesTileSource = new MbTilesTileSource(new SQLiteConnectionString(file, true));

                        //GPX not visible
                        //var mbTilesTileSource = new MbTilesTileSource(new SQLiteConnectionString(file, true), null, MbTilesType.Overlay, true, true);

                        var mbTilesLayer = new TileLayer(mbTilesTileSource) { Name = file };
                        MainActivity.map.Layers.Add(mbTilesLayer);
                    }
                }
            }

            //Load the rest 
            foreach (var file in filesList)
            {
                if (file.EndsWith(".mbtiles"))
                {
                    if (!file.EndsWith("Country.mbtiles"))
                    {
                        Log.Information($"File {file}");

                        //Map not clear. GPX visible
                        var mbTilesTileSource = new MbTilesTileSource(new SQLiteConnectionString(file, true));

                        //GPX not visible
                        //var mbTilesTileSource = new MbTilesTileSource(new SQLiteConnectionString(file, true), null, MbTilesType.Overlay, true, true);

                        var mbTilesLayer = new TileLayer(mbTilesTileSource) { Name = file };
                        MainActivity.map.Layers.Add(mbTilesLayer);
                    }
                }
            }

        }
    }
}
