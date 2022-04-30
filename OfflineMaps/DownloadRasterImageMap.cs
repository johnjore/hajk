using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Serilog;
using Dasync.Collections;
using SQLite;
using hajk.Models;
using Xamarin.Essentials;
using Mapsui.Layers;
using Android.Views;

namespace hajk
{
    class DownloadRasterImageMap
    {
        static int done = 0;
        static int totalTilesCount = 0;

        public static async Task DownloadMap(Models.Map map)
        {
            try 
            { 
                if (AccessOSMLayerDirect() == false)
                {
                    return;
                }

                //Reset counters for next download
                done = 0;
                totalTilesCount = 0;

                for (int zoom = map.ZoomMin; zoom <= map.ZoomMax; zoom++)
                {
                    AwesomeTiles.TileRange tiles = GPXUtils.GPXUtils.GetTileRange(zoom, map);
                    totalTilesCount += tiles.Count;
                    Log.Information($"Need to download {tiles.Count} tiles for zoom level {zoom}");
                }

                for (int zoom = map.ZoomMin; zoom <= map.ZoomMax; zoom++)
                {
                    AwesomeTiles.TileRange tiles = GPXUtils.GPXUtils.GetTileRange(zoom, map);
                    await DownloadTiles(tiles, zoom, MainActivity.OfflineDBConn, map.Id);
                }

                Import.progress = 999;
                Log.Information($"Done downloading map for {map.Id}");

                Show_Dialog msg3 = new Show_Dialog(MainActivity.mContext);
                await msg3.ShowDialog($"Done", $"GPX Import Completed", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);

                LoadOSMLayer();
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"DownloadRasterImageMap - DownloadMap()");
            }
        }

        private static async Task DownloadTiles(AwesomeTiles.TileRange range, int zoom, SQLiteConnection conn, int id)
        {
            string OSMServer = Preferences.Get("OSMServer", PrefsActivity.OSMServer_s);

            //Same, but without parallell processing
            /*            
            foreach (var tile in range)
            {
                byte[] data = null;
                for (int i = 0; i < 10; i++)
                {
                    var url = OSMServer + $"{zoom}/{tile.X}/{tile.Y}.png";
                    data = await DownloadImageAsync(url);
                    if (data != null)
                        break;

                    Thread.Sleep(10000);
                }

                Log.Information($"Zoomindex: {zoom}, x/y: {tile.X}/{tile.Y}, ID: {tile.Id}. Done:{++done}/{totalTilesCount}");
                WriteOsmSQlite(data, zoom, tile.X, tile.Y);
            };
            */
            try
            {
                await range.ParallelForEachAsync(async tile =>
                {
                    int tmsY = (int)Math.Pow(2, zoom) - 1 - tile.Y;
                    tiles oldTile = new tiles();

                    //Update Progressbar
                    Import.progress = (int)Math.Floor((decimal)done * 100 / totalTilesCount);
                    Import.progressBarText2.Text = $"{done} of {totalTilesCount}";
                    Import.progressBarText2.Invalidate();

                    for (int i = 0; i < 10; i++)
                    {
                        try
                        {
                            oldTile = conn.Table<tiles>().Where(x => x.zoom_level == zoom && x.tile_column == tile.X && x.tile_row == tmsY).FirstOrDefault();
                            if ((oldTile != null) && ((DateTime.UtcNow - oldTile.createDate).TotalDays < PrefsActivity.OfflineMaxAge))
                            {
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Crashed: {ex}");
                        }

                        try
                        {
                            var url = OSMServer + $"{zoom}/{tile.X}/{tile.Y}.png";
                            var data = await DownloadImageAsync(url);
                            oldTile = new tiles()
                            {
                                tile_data = data,
                                tile_row = tmsY,
                                tile_column = tile.X,
                                zoom_level = zoom,
                                createDate = DateTime.UtcNow,
                            };

                            if (oldTile.tile_data != null)
                            {
                                break;
                            }

                            Thread.Sleep(10000);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Crashed: {ex}");
                        }
                    }

                    if (oldTile.tile_data == null)
                    {
                        return;
                    }

                    ++done;

                    if (oldTile.reference == null)
                    {
                        oldTile.reference = id.ToString();
                    }
                    else
                    {
                        if (oldTile.reference.Contains(id.ToString()))
                        {
                            return;
                        }

                        oldTile.reference += "," + id.ToString();
                    }

                    Log.Information($"Zoomindex: {zoom}, x/y/tmsY: {tile.X}/{tile.Y}/{tmsY}, ID: {tile.Id}. Done:{done}/{totalTilesCount}");
                    MBTilesWriter.WriteTile(conn, oldTile);
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"DownloadRasterImageMap - DownloadTiles()");
            }
        }

        private static async Task<byte[]> DownloadImageAsync(string imageUrl)
        {
            HttpClientHandler clientHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
            };
            var _httpClient = new HttpClient(clientHandler)
            {
                Timeout = TimeSpan.FromSeconds(60)
            };

            try
            {
                using var httpResponse = await _httpClient.GetAsync(imageUrl);
                if (httpResponse.StatusCode == HttpStatusCode.OK)
                {
                    return await httpResponse.Content.ReadAsByteArrayAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"DownloadRasterIamgeMap - DownloadImageAsync()");
            }

            return null;
        }

        private static bool AccessOSMLayerDirect()
        {
            try
            {
                //Remove
                var OSMLayer = Fragments.Fragment_map.map.Layers.FindLayer("OSM").FirstOrDefault();
                if (OSMLayer != null)
                {
                    Fragments.Fragment_map.map.Layers.Remove(OSMLayer);
                }

                //First time
                if (MainActivity.OfflineDBConn == null)
                {
                    MainActivity.OfflineDBConn = MBTilesWriter.CreateDatabaseConnection(MainActivity.rootPath + "/" + PrefsActivity.CacheDB);
                }

                //Empty?
                if (MainActivity.OfflineDBConn == null)
                {
                    return false;
                }

                //No handle?
                if (MainActivity.OfflineDBConn.Handle == null)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"DownloadRasterIamgeMap - AcessOSMLayerDirect()");
            }

            //Success
            return true;
        }

        public static void LoadOSMLayer()
        {
            try
            {
                if (MainActivity.OfflineDBConn != null)
                {
                    MainActivity.OfflineDBConn.Close();
                    MainActivity.OfflineDBConn = null;
                }

                var tileSource = TileCache.GetOSMBasemap(MainActivity.rootPath + "/" + PrefsActivity.CacheDB);
                var tileLayer = new TileLayer(tileSource)
                {
                    Name = "OSM",
                };
                Fragments.Fragment_map.map.Layers.Insert(0, tileLayer);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"DownloadRasterIamgeMap - LoadOSMLayer()");
            }
        }

        public static void PurgeMapDB(int Id)
        {
            try
            {
                if (AccessOSMLayerDirect() == false)
                {
                    return;
                }

                string id = Id.ToString();
                Log.Debug($"Remove Id: {id}");

                //Remove single reference tiles
                var query = MainActivity.OfflineDBConn.Table<tiles>().Where(x => x.reference == id);
                Log.Debug($"Query Count: " + query.Count().ToString());
                foreach (tiles maptile in query)
                {
                    Log.Debug($"Tile Id: {maptile.id}, Reference: {maptile.reference}");
                    MainActivity.OfflineDBConn.Delete(maptile);
                }

                //Remove reference
                query = MainActivity.OfflineDBConn.Table<tiles>().Where(x => x.reference.Contains(id));
                Log.Debug($"Query Count: " + query.Count().ToString());
                foreach (tiles maptile in query)
                {
                    Log.Debug($"Tile Id: {maptile.id}, Before: {maptile.reference}");

                    maptile.reference = maptile.reference.Replace("," + id, "");
                    maptile.reference = maptile.reference.Replace(id + ",", "");

                    Log.Debug($"Tile Id: {maptile.id}, After: {maptile.reference}");
                    MainActivity.OfflineDBConn.Update(maptile);
                }

                LoadOSMLayer();
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"DownloadRasterIamgeMap - AcessOSMLayerDirect()");
            }            
        }

        public static void ExportMapTiles(int Id, string strFileName)
        {
            try
            {
                if (AccessOSMLayerDirect() == false)
                {
                    return;
                }

                //Save tiles here
                var ExportDB = MBTilesWriter.CreateDatabaseConnection(strFileName);

                //Get tiles
                string id = Id.ToString();
                var query = MainActivity.OfflineDBConn.Table<tiles>().Where(x => x.reference.Contains(id));
                Log.Debug($"Query Count: " + query.Count().ToString());
                foreach (tiles maptile in query)
                {
                    Log.Debug($"Tile Id: {maptile.id}");

                    //Fix the id, and clear the reference, so insert, not update is used
                    maptile.id = 0;
                    maptile.reference = "";

                    MBTilesWriter.WriteTile(ExportDB, maptile);
                }
                ExportDB.Close();
                ExportDB = null;

                var m = MainActivity.mContext;
                Show_Dialog msg = new Show_Dialog(m);
                msg.ShowDialog(m.GetString(Resource.String.Done), m.GetString(Resource.String.MapExportCompleted), Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);

                LoadOSMLayer();
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"DownloadRasterIamgeMap - ExportMapTiles()");
            }
        }

        public static void ImportMapTiles()
        {            
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
                        if (AccessOSMLayerDirect() == false)
                        {
                            return;
                        }
                      
                        var ImportDB = MBTilesWriter.CreateDatabaseConnection(sourceFile.FullPath);
                        var tiles = ImportDB.Table<tiles>();

                        Log.Debug($"Tiles to import: " + tiles.Count().ToString());
                        tiles oldTile = new tiles();
                        foreach (tiles newTile in tiles)
                        {
                            //Do we already have the tile?
                            try
                            {
                                oldTile = MainActivity.OfflineDBConn.Table<tiles>().Where(x => x.zoom_level == newTile.zoom_level && x.tile_column == newTile.tile_column && x.tile_row == newTile.tile_row).FirstOrDefault();
                                if ((oldTile != null) && ((DateTime.UtcNow - oldTile.createDate).TotalDays < PrefsActivity.OfflineMaxAge))
                                {
                                    continue;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"Crashed: {ex}");
                            }

                            //For Insert
                            newTile.id = 0;
                            newTile.reference = null;

                            //Use reference from oldTile for update
                            if (oldTile != null)
                            {
                                newTile.reference = oldTile.reference;
                                newTile.id = 0;
                            }
                            
                            MBTilesWriter.WriteTile(MainActivity.OfflineDBConn, newTile);
                        }
                        ImportDB.Close();
                        ImportDB = null;

                        LoadOSMLayer();

                        var m = MainActivity.mContext;
                        Show_Dialog msg = new Show_Dialog(m);
                        await msg.ShowDialog(m.GetString(Resource.String.Done), m.GetString(Resource.String.MapTilesImported), Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to import map file: '{ex}'");
                }
            });
        }
    }
}
