using System;
using System.Net;
using System.Threading;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;
using AwesomeTiles;
using Dasync.Collections;
using SQLite;
using hajk.Models;
using Xamarin.Essentials;
using System.Collections.Generic;

namespace hajk
{
    class DownloadRasterImageMap
    {
        static int done = 0;
        static int totalTilesCount = 0;

        public static async Task DownloadMap(Models.Map map)
        {
            if (MainActivity.OfflineDBConn == null)
            {
                MainActivity.OfflineDBConn = MBTilesWriter.CreateDatabaseConnection(MainActivity.rootPath + "/" + PrefsActivity.OfflineDB);
            }

            if (MainActivity.OfflineDBConn == null)
            {
                return;
            }

            for (int zoom = map.ZoomMin; zoom <= map.ZoomMax; zoom++)
            {
                var leftBottom = Tile.CreateAroundLocation(map.BoundsLeft, map.BoundsBottom, zoom);
                var topRight = Tile.CreateAroundLocation(map.BoundsRight, map.BoundsTop, zoom);

                var minX = Math.Min(leftBottom.X, topRight.X);
                var maxX = Math.Max(leftBottom.X, topRight.X);
                var minY = Math.Min(leftBottom.Y, topRight.Y);
                var maxY = Math.Max(leftBottom.Y, topRight.Y);

                var tiles = new AwesomeTiles.TileRange(minX, minY, maxX, maxY, zoom);

                var tilesCount = tiles.Count;
                totalTilesCount += tilesCount;
                Log.Information($"Need to download {tilesCount} tiles for zoom level {zoom}");

                await DownloadTiles(tiles, zoom, MainActivity.OfflineDBConn, map.Id);
            }

            Log.Information($"Done downloading map for {map.Id}");

            Show_Dialog msg3 = new Show_Dialog(MainActivity.mContext);
            await msg3.ShowDialog($"Done", $"GPX Import Completed", Android.Resource.Attribute.DialogIcon, true, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
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

            await range.ParallelForEachAsync(async tile =>
            {
                int tmsY = (int)Math.Pow(2, zoom) - 1 - tile.Y;
                tiles oldTile = new tiles();

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
                else
                {
                    //Url is Invalid
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"DownloadImageAsync(...) crashed: {ex}");
            }

            return null;
        }

        public static void PurgeMapDB(int Id)
        {
            if (MainActivity.OfflineDBConn == null)
            {
                string OfflineDB = MainActivity.rootPath + "/" + PrefsActivity.OfflineDB;
                MainActivity.OfflineDBConn = MBTilesWriter.CreateDatabaseConnection(MainActivity.rootPath + "/" + PrefsActivity.OfflineDB);
            }

            //Remove single reference tiles
            string id = Id.ToString();
            var query = MainActivity.OfflineDBConn.Table<tiles>().Where(x => x.reference == id);
            foreach (tiles maptile in query)
            {
                Log.Debug($"Tile Id: {maptile.id}, Reference: {maptile.reference}");
                MainActivity.OfflineDBConn.Delete(maptile);
            }

            //Remove reference
            query = MainActivity.OfflineDBConn.Table<tiles>().Where(x => x.reference.Contains(id));
            foreach (tiles maptile in query)
            {
                Log.Debug($"Tile Id: {maptile.id}, Reference: {maptile.reference}");

                maptile.reference = maptile.reference.Replace("," + id, "");
                maptile.reference = maptile.reference.Replace(id + ",", "");

                MainActivity.OfflineDBConn.Update(maptile);
            }

            return;
        }

    }
}
