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

                    var sourceFile = await FilePicker.PickAsync(options);
                    if (sourceFile != null)
                    {
                        string destinationFile = MainActivity.rootPath + "/MBTiles/" + sourceFile.FileName;

                        if (File.Exists(destinationFile))
                        {
                            Show_Dialog msg1 = new Show_Dialog(MainActivity.mContext);
                            if (await msg1.ShowDialog($"Overwrite", $"Overwrite '{sourceFile.FileName}'", Android.Resource.Attribute.DialogIcon, true, Show_Dialog.MessageResult.NO, Show_Dialog.MessageResult.YES) == Show_Dialog.MessageResult.NO)
                            {
                                return;
                            }
                        }

                        File.Copy(sourceFile.FullPath, destinationFile, true);
                        Fragments.Map.map.Layers.Add(CreateMbTilesLayer(destinationFile, sourceFile.FileName));

                        Show_Dialog msg2 = new Show_Dialog(MainActivity.mContext);
                        await msg2.ShowDialog($"Done", $"Map Imported and Loaded", Android.Resource.Attribute.DialogIcon, true, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
                    }
                }
                catch (Exception ex)
                {
                    Log.Information($"Failed to import map file: '{ex}'");
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
                        Fragments.Map.map.Layers.Add(mbTilesLayer);
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
                        Fragments.Map.map.Layers.Add(mbTilesLayer);
                    }
                }
            }
        }
    }
}
