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
                        Fragments.Fragment_map.map.Layers.Add(CreateMbTilesLayer(destinationFile, sourceFile.FileName));

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
            var mbTilesLayer = new TileLayer(mbTilesTileSource)
            {
                Name = name
            };
            return mbTilesLayer;
        }

        public static void LoadOSMMaps()
        {
            var tileSource = TileCache.GetOSMBasemap(MainActivity.rootPath + "/" + PrefsActivity.CacheDB);
            var tileLayer = new TileLayer(tileSource)
            {
                Name = "OSM",
            };
            Fragments.Fragment_map.map.Layers.Add(tileLayer);
        }
    }
}
