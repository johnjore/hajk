using Android.Views;
using Dasync.Collections;
using hajk.Models;
using Mapsui.Layers;
using Mapsui.Tiling.Layers;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SQLite;
using static hajk.TileCache;
using Microsoft.Maui.Networking;

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

                await Task.Run(() =>
                {
                    for (int zoom = map.ZoomMin; zoom <= map.ZoomMax; zoom++)
                    {
                        AwesomeTiles.TileRange tiles = GPXUtils.GPXUtils.GetTileRange(zoom, map);
                        (int TotalTiles, int MissingTiles) = CountTiles(tiles, zoom);
                        totalTilesCount += TotalTiles;
                        missingTilesCount += MissingTiles;
                        Log.Information($"Need to download '{MissingTiles}' tiles for zoom level '{zoom}', total to download '{missingTilesCount}'");
                    }
                });

                for (int zoom = map.ZoomMin; zoom <= map.ZoomMax; zoom++)
                {
                    AwesomeTiles.TileRange tiles = GPXUtils.GPXUtils.GetTileRange(zoom, map);
                    if (totalTilesCount > 0 && tiles != null)
                    {
                        await DownloadTiles(tiles, zoom, TileCache.MbTileCache.sqlConn, map.Id, missingTilesCount, totalTilesCount);
                    }
                    else
                    {
                        throw new Exception("How can this be?!?");
                    }

                }

                Import.progress = 999;
                Log.Verbose($"Done downloading map for {map.Id}");

                if (ShowDialog)
                {
                    Show_Dialog msg3 = new(Platform.CurrentActivity);
                    await msg3.ShowDialog($"Done", $"Map Download Completed", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
                }

                Import.progress = 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"DownloadRasterImageMap - DownloadMap()");
            }
        }

        private static (int TotalTiles, int MissingTiles) CountTiles(AwesomeTiles.TileRange range, int zoom)
        {
            int CountMissingTiles = 0;
            int CountTotalTiles = 0;

            try
            {
                foreach (var tile in range)
                {
                    int tmsY = (int)Math.Pow(2, zoom) - 1 - tile.Y;
                    tiles dbTile = MbTileCache.sqlConn.Table<tiles>().Where(x => x.zoom_level == zoom && x.tile_column == tile.X && x.tile_row == tmsY).FirstOrDefault();
                    if ((dbTile == null) || ((DateTime.UtcNow - dbTile.createDate).TotalDays > Fragment_Preferences.OfflineMaxAge))
                    {
                        CountMissingTiles++;
                    }

                    CountTotalTiles++;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Crashed CountTiles");
            }

            return (CountTotalTiles, CountMissingTiles);
        }

        private static async Task DownloadTiles(AwesomeTiles.TileRange range, int zoom, SQLiteConnection conn, int id, int intmissingTiles, int inttotalTiles)
        {
            string OSMServer = string.Empty;
            string TileBulkDownloadSource = Preferences.Get(Platform.CurrentActivity?.GetString(Resource.String.OSM_BulkDownload_Source), Fragment_Preferences.TileBulkDownloadSource);
           
            var MapSource = Fragment_Preferences.MapSources.Where(x => x.Name.Equals(TileBulkDownloadSource, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (MapSource == null)
            {
                Serilog.Log.Error("No MapSource defined");
                return;
            }

            if (TileBulkDownloadSource.Equals("OpenStreetMap", StringComparison.OrdinalIgnoreCase))
            {
                Serilog.Log.Error("Can't use OSM as a bulkdownload server");
                return;
            }
            else if (TileBulkDownloadSource.Equals("Custom", StringComparison.OrdinalIgnoreCase))
            {
                var url = Preferences.Get(MapSource.BaseURL, "");
                var token = Preferences.Get(MapSource.Token, "");

                OSMServer = url + token;
            }
            else //Mapbox || Thunderforst
            {
                var token = Preferences.Get(MapSource.Token, "");

                if (token == string.Empty || token == "")
                {
                    Show_Dialog msg = new Show_Dialog(Platform.CurrentActivity);
                    var a = $"{TileBulkDownloadSource} requires the token to be set";
                    await msg.ShowDialog($"Token Required", a, Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.CANCEL, Show_Dialog.MessageResult.NONE);

                    return;
                }

                OSMServer = MapSource.BaseURL + token;
            }

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
                    tiles newTile = new();

                    for (int i = 0; i < 10; i++)
                    {
                        tiles oldTile;
                        try
                        {
                            oldTile = conn.Table<tiles>().Where(x => x.zoom_level == zoom && x.tile_column == tile.X && x.tile_row == tmsY).FirstOrDefault();
                            if ((oldTile != null) && ((DateTime.UtcNow - oldTile.createDate).TotalDays < Fragment_Preferences.OfflineMaxAge))
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
                            var url = OSMServer.Replace("{z}", zoom.ToString()).Replace("{x}", tile.X.ToString()).Replace("{y}", tile.Y.ToString());
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
                                if (oldTile == null)
                                {
                                    newTile.id = 0;
                                    newTile.reference = string.Empty;

                                    intmissingTiles--;
                                }
                                else
                                {
                                    newTile.id = oldTile.id;
                                    newTile.reference = oldTile.reference;
                                }

                                //Break out of loop as we have an updatd blob
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Crashed: {ex}");
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
                    List<int> r = new();
                    if (newTile.reference != null && newTile.reference != string.Empty)
                    {
                        try
                        {
                            r = JsonSerializer.Deserialize<List<int>>(newTile.reference);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Crashed. Clear reference: {ex}");
                            r.Clear();
                        }
                    }

                    if (r.Contains(id) == false)
                    {
                        r.Add(id);
                        newTile.reference = JsonSerializer.Serialize(r);
                    }

                    if (MBTilesWriter.WriteTile(newTile) == 0)
                    {
                        Log.Error($"Failed to add rows to database");
                    }
                    else
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

        public static async Task<byte[]> DownloadImageAsync(string imageUrl)
        {
            HttpClientHandler clientHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; }
            };
            HttpClient _httpClient = new(clientHandler)
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
                var tileSource = TileCache.GetOSMBasemap(Fragment_Preferences.rootPath + "/" + Fragment_Preferences.CacheDB);
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
                lock (MbTileCache.sqlConn)
                {
                    //Careful: Captures variants of 1151 15 and 5 when looking for '5'
                    var query = MbTileCache.sqlConn.Table<tiles>().Where(x => x.reference.Contains(Id.ToString()));

                    Log.Debug($"Query Count: " + query.Count().ToString());
                    foreach (tiles maptile in query)
                    {
                        //Is this the tile we are looking for?
                        var r = JsonSerializer.Deserialize<List<int>>(maptile.reference);
                        if (r.Contains(Id))
                        {
                            Log.Debug($"Tile Id: {maptile.id}");

                            //Clear the ID and reference
                            maptile.id = 0;
                            maptile.reference = string.Empty;

                            //Add to DB
                            ExportDB.Insert(maptile);
                        }
                    }
                }
                ExportDB.Close();
                ExportDB.Dispose();

                Show_Dialog msg = new(Platform.CurrentActivity);
                msg.ShowDialog(Platform.CurrentActivity.GetString(Resource.String.Done), Platform.CurrentActivity.GetString(Resource.String.MapExportCompleted), Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"DownloadRasterIamgeMap - ExportMapTiles()");
            }
        }
    }
}
