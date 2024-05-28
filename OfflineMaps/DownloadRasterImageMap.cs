using Android.Views;
using Dasync.Collections;
using hajk.Models;
using Mapsui.Layers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SQLite;
using Xamarin.Essentials;
using static hajk.TileCache;

namespace hajk
{
    class DownloadRasterImageMap
    {
        static int done = 0;
        static int missingTilesCount = 0;
        static int totalTilesCount = 0;

        public static async Task DownloadMap(Models.Map map, bool ShowDialog)
        {
            try 
            { 
                //Reset counters for download
                done = 0;
                missingTilesCount = 0;
                totalTilesCount = 0;

                for (int zoom = map.ZoomMin; zoom <= map.ZoomMax; zoom++)
                {
                    AwesomeTiles.TileRange tiles = GPXUtils.GPXUtils.GetTileRange(zoom, map);
                    var tilesCounted = await CountTiles(tiles, zoom);
                    totalTilesCount += tilesCounted.TotalTiles;
                    missingTilesCount += tilesCounted.MissingTiles;
                    Log.Information($"Need to download '{tilesCounted.MissingTiles}' tiles for zoom level '{zoom}', total to download '{missingTilesCount}'");
                }

                for (int zoom = map.ZoomMin; zoom <= map.ZoomMax; zoom++)
                {
                    AwesomeTiles.TileRange tiles = GPXUtils.GPXUtils.GetTileRange(zoom, map);
                    await DownloadTiles(tiles, zoom, TileCache.MbTileCache.sqlConn, map.Id, missingTilesCount, totalTilesCount);
                }

                Import.progress = 999;
                Log.Verbose($"Done downloading map for {map.Id}");

                if (ShowDialog)
                {
                    Show_Dialog msg3 = new Show_Dialog(MainActivity.mContext);
                    await msg3.ShowDialog($"Done", $"Map Download Completed", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
                }

                Import.progress = 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"DownloadRasterImageMap - DownloadMap()");
            }
        }

        private static async Task<(int TotalTiles, int MissingTiles)> CountTiles(AwesomeTiles.TileRange range, int zoom)
        {
            int CountMissingTiles = 0;
            int CountTotalTiles = 0;

            await range.ParallelForEachAsync(async tile =>
            {
                try
                {
                    int tmsY = (int)Math.Pow(2, zoom) - 1 - tile.Y;
                    tiles dbTile = MbTileCache.sqlConn.Table<tiles>().Where(x => x.zoom_level == zoom && x.tile_column == tile.X && x.tile_row == tmsY).FirstOrDefault();
                    if ((dbTile == null) || ((DateTime.UtcNow - dbTile.createDate).TotalDays >= PrefsActivity.OfflineMaxAge))
                    {
                        CountMissingTiles++;
                    }

                    CountTotalTiles++;
                }
                catch (Exception ex)
                {
                    Log.Error($"Crashed: {ex}");
                }
            });

            return (CountTotalTiles, CountMissingTiles);
        }

        private static async Task DownloadTiles(AwesomeTiles.TileRange range, int zoom, SQLiteConnection conn, int id, int intmissingTiles, int inttotalTiles)
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
                    //Update Progressbar
                    Import.progress = (int)Math.Floor((decimal)done * 100 / inttotalTiles);

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Import.progressBarText2.Text = $"{done} of {inttotalTiles} - ({intmissingTiles})";
                    });

                    int tmsY = (int)Math.Pow(2, zoom) - 1 - tile.Y;
                    tiles newTile = new tiles();

                    for (int i = 0; i < 10; i++)
                    {
                        tiles oldTile = null;
                        try
                        {
                            oldTile = conn.Table<tiles>().Where(x => x.zoom_level == zoom && x.tile_column == tile.X && x.tile_row == tmsY).FirstOrDefault();
                            if ((oldTile != null) && ((DateTime.UtcNow - oldTile.createDate).TotalDays < PrefsActivity.OfflineMaxAge))
                            {
                                //Tile blob is upto date. No need to download. Break out of for-loop. Update reference
                                newTile = oldTile;
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Crashed: {ex}");
                            break;
                        }

                        try
                        {
                            var url = OSMServer + $"{zoom}/{tile.X}/{tile.Y}.png";
                            var data = await DownloadImageAsync(url);

                            if (data != null)
                            {
                                //Update / create tile
                                newTile.tile_data = data;
                                newTile.tile_row = tmsY;
                                newTile.tile_column = tile.X;
                                newTile.zoom_level = zoom;
                                newTile.createDate = DateTime.UtcNow;

                                //Old data to keep?
                                if (oldTile != null)
                                {
                                    newTile.id = oldTile.id;
                                    newTile.reference = oldTile.reference;
                                }
                                else
                                {
                                    newTile.id = 0;
                                }
                                
                                //Break out of loop as we have an updatd blob
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"Crashed: {ex}");
                            break;
                        }
                    }

                    //Update progress counter as the tile is processed, even if unsuccessful
                    ++done;

                    //If no blob, exit here
                    if (newTile.tile_data == null)
                    {
                        return;
                    }

                    //Update Reference field
                    if (newTile.reference == string.Empty || newTile.reference == null)
                    {
                        newTile.reference = id.ToString();
                    }
                    else if (newTile.reference.Contains(id.ToString()))
                    {
                        //Do nothing if already added as a reference
                    }
                    else
                    {
                        newTile.reference += "," + id.ToString();
                    }

                    if (MBTilesWriter.WriteTile(newTile) == 0)
                    {
                        Log.Error($"Failed to add rows to database");
                    } else
                    {
                        Log.Information($"Zoomindex: {zoom}, x/y/tmsY: {tile.X}/{tile.Y}/{tmsY}, ID: {tile.Id}. Done:{done}/{missingTilesCount}");
                    }
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

       public static void LoadOSMLayer()
        {
            try
            {
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

        public static void ExportMapTiles(int Id, string strFileName)
        {
            try
            {
                //Save tiles here
                SQLiteConnection ExportDB = InitializeTileCache(strFileName, "png");
                
                //Get tiles
                string id = Id.ToString();
                lock (MbTileCache.sqlConn)
                {
                    var query = MbTileCache.sqlConn.Table<tiles>().Where(x => x.reference.Contains(id));

                    Log.Debug($"Query Count: " + query.Count().ToString());
                    foreach (tiles maptile in query)
                    {
                        Log.Debug($"Tile Id: {maptile.id}");

                        //Clear the ID and reference
                        maptile.id = 0;
                        maptile.reference = null;

                        //Add to DB
                        ExportDB.Insert(maptile);
                    }
                }
                ExportDB.Close();
                ExportDB.Dispose();
                ExportDB = null;

                var m = MainActivity.mContext;
                Show_Dialog msg = new Show_Dialog(m);
                msg.ShowDialog(m.GetString(Resource.String.Done), m.GetString(Resource.String.MapExportCompleted), Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"DownloadRasterIamgeMap - ExportMapTiles()");
            }
        }
    }
}
