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
using Android.Widget;

namespace hajk
{
    class DownloadRasterImageMap
    {
        private static int doneCount = 0;
        private static int missingTilesCount = 0;
        private static int totalTilesCount = 0;

        public static async Task DownloadMap(Models.Map map, bool ShowDialog)
        {
            try
            {
                //Reset counters for download
                doneCount = 0;
                missingTilesCount = 0;
                totalTilesCount = 0;
                int intFailedDownloadsCounter = 0;

                //Progress bar
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _ = Progressbar.UpdateProgressBar.CreateGUIAsync(Platform.CurrentActivity.GetString(Resource.String.DownloadTiles));
                    Progressbar.UpdateProgressBar.Progress = 0;
                    Progressbar.UpdateProgressBar.MessageBody = $"{doneCount} of {totalTilesCount} - ({missingTilesCount})";
                });

                //Count required tiles to download
                for (int zoom = map.ZoomMin; zoom <= map.ZoomMax; zoom++)
                {
                    AwesomeTiles.TileRange tiles = GPXUtils.GPXUtils.GetTileRange(zoom, map);
                    (int TotalTiles, int MissingTiles) = CountTiles(tiles, zoom);
                    totalTilesCount += TotalTiles;
                    missingTilesCount += MissingTiles;
                    Progressbar.UpdateProgressBar.Progress = zoom - map.ZoomMin+1;
                    Log.Information($"Need to download '{MissingTiles}' tiles for zoom level '{zoom}', total to download '{missingTilesCount}'");
                }

                //Download missing tiles
                for (int zoom = map.ZoomMin; zoom <= map.ZoomMax; zoom++)
                {
                    AwesomeTiles.TileRange tiles = GPXUtils.GPXUtils.GetTileRange(zoom, map);
                    if (totalTilesCount > 0 && tiles != null)
                    {
                        intFailedDownloadsCounter += await DownloadTiles(tiles, zoom, TileCache.MbTileCache.sqlConn, map.Id, missingTilesCount, totalTilesCount);
                    }
                    else
                    {
                        throw new Exception("How can this be?!?");
                    }
                }

                Progressbar.UpdateProgressBar.Dismiss();
                Log.Debug($"Done downloading map for {map.Id}");

                if (intFailedDownloadsCounter > 0)
                {
                    Toast.MakeText(Platform.AppContext, $"{intFailedDownloadsCounter} map tiles failed to download", ToastLength.Long)?.Show();
                }

                if (ShowDialog)
                {
                    Show_Dialog msg3 = new(Platform.CurrentActivity);
                    await msg3.ShowDialog($"Done", $"Map Download Completed", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
                }               
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, $"DownloadRasterImageMap - DownloadMap()");
            }
        }

        public static (int TotalTiles, int MissingTiles) CountTiles(AwesomeTiles.TileRange range, int zoom)
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
                Log.Fatal(ex, $"Crashed CountTiles");
            }

            return (CountTotalTiles, CountMissingTiles);
        }

        private static async Task<int> DownloadTiles(AwesomeTiles.TileRange range, int zoom, SQLiteConnection conn, int id, int intmissingTiles, int inttotalTiles)
        {
            string OSMServer = string.Empty;
            string TileBulkDownloadSource = Preferences.Get(Platform.CurrentActivity?.GetString(Resource.String.OSM_Browse_Source), Fragment_Preferences.TileBrowseSource);
            int FailedDownloadsCounter = 0;

            var MapSource = Fragment_Preferences.MapSources.Where(x => x.Name.Equals(TileBulkDownloadSource, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (MapSource == null)
            {
                Serilog.Log.Error("No MapSource defined");
                return -1;
            }

            if (TileBulkDownloadSource.Equals("OpenStreetMap", StringComparison.OrdinalIgnoreCase))
            {
                Serilog.Log.Error("Can't use OSM as a bulkdownload server");
                return -1;
            }
            else if (TileBulkDownloadSource.Equals("Custom", StringComparison.OrdinalIgnoreCase))
            {
                var url = Preferences.Get(MapSource.BaseURL, "");
                var token = Preferences.Get(MapSource.Token, "");

                OSMServer = url + token;
            }
            else //Mapbox || Thunderforest || StadiaMaps
            {
                var token = Preferences.Get(MapSource.Token, "");

                if (token == string.Empty || token == "")
                {
                    Show_Dialog msg = new Show_Dialog(Platform.CurrentActivity);
                    var a = $"{TileBulkDownloadSource} requires the token to be set";
                    await msg.ShowDialog($"Token Required", a, Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.CANCEL, Show_Dialog.MessageResult.NONE);

                    return -1;
                }

                OSMServer = MapSource.BaseURL + token;
            }

            try
            {
                await range.ParallelForEachAsync(async tile =>
                {
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
                            Log.Fatal($"Crashed: {ex}");
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
                            else
                            {
                                if (i == 9) //Last attempt
                                {
                                    FailedDownloadsCounter++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Fatal(ex, $"Crashed: {ex}");
                            break;
                        }
                    }

                    //If no blob, exit here
                    if (newTile.tile_data == null)
                    {
                        return;
                    }

                    //Update Reference field
                    if (id != 999999) //Do not update if id is a POI
                    {
                        List<int> r = new();
                        if (newTile.reference != null && newTile.reference != string.Empty)
                        {
                            try
                            {
                                r = JsonSerializer.Deserialize<List<int>>(newTile.reference);
                            }
                            catch (Exception ex)
                            {
                                Log.Fatal(ex, $"Crashed");
                                r.Clear();
                            }
                        }

                        if (r.Contains(id) == false)
                        {
                            r.Add(id);
                            newTile.reference = JsonSerializer.Serialize(r);
                        }
                    }

                    if (MBTilesWriter.WriteTile(newTile) == 0)
                    {
                        Log.Error($"Failed to add rows to database");
                    }
                    else
                    {
                        Log.Information($"Zoomindex: {zoom}, x/y/tmsY: {tile.X}/{tile.Y}/{tmsY}, ID: {tile.Id}. Done:{doneCount}/{missingTilesCount}");
                    }

                    //Update progress counter as the tile is processed, even if unsuccessful
                    Progressbar.UpdateProgressBar.Progress = (int)Math.Ceiling((decimal)(Fragment_Preferences.MaxZoom + (++doneCount) * (100 - Fragment_Preferences.MaxZoom) / inttotalTiles));
                    Progressbar.UpdateProgressBar.MessageBody = $"{doneCount} of {inttotalTiles} - ({intmissingTiles})";
                });
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, $"DownloadRasterImageMap - DownloadTiles()");
            }

            return FailedDownloadsCounter;
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
                Log.Fatal(ex, $"DownloadRasterIamgeMap - DownloadImageAsync()");
            }
            
            return null;
        }

        public static void LoadOSMLayer()
        {
            try
            {
                var tileSource = TileCache.GetOSMBasemap();
                if (tileSource == null)
                {
                    Serilog.Log.Fatal("TileSource is null");
                    return;
                }

                var tileLayer = new TileLayer(tileSource)
                {
                    Name = Fragment_Preferences.TileLayerName,
                };
                Fragments.Fragment_map.map.Layers.Insert(0, tileLayer);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, $"DownloadRasterIamgeMap - LoadOSMLayer()");
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
                Log.Fatal(ex, $"DownloadRasterIamgeMap - ExportMapTiles()");
            }
        }
    }
}
