using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;
using AwesomeTiles;
using Mapsui.Geometries;
using Dasync.Collections;
using SQLite;
using hajk.Models;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Xamarin.Essentials;

namespace hajk
{
    class DownloadRasterImageMap
    {
        static int done = 0;
        static int totalTilesCount = 0;

        public static async Task DownloadMap(Models.Map map)
        {
            //Make sure the folder for the offline maps exists
            InitMBTilesFolder();

            //Calculate the the sqlite / mbtiles metadata
            Point p = Utils.Misc.CalculateCenter(map.BoundsRight, map.BoundsTop, map.BoundsLeft, map.BoundsBottom);
            metadataValues metadata = new metadataValues
            {
                name = map.Name,
                description = "Created by hajk",
                version = "1",
                minzoom = map.ZoomMin.ToString(),
                maxzoom = map.ZoomMax.ToString(),
                center = p.X.ToString().Replace(",", ".") + "," + p.Y.ToString().Replace(",", "."),
                bounds = map.BoundsTop.ToString().Replace(",", ".") + "," + map.BoundsLeft.ToString().Replace(",", ".") + "," + map.BoundsBottom.ToString().Replace(",", ".") + "," + map.BoundsRight.ToString().Replace(",", "."),
                format = "png",
                type = "png",
            };

            SQLiteConnection conn = MBTilesWriter.CreateDatabase(MainActivity.rootPath + "/MBTiles/" + map.Name + ".mbtiles", metadata);
            if (conn == null)
                return;

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

                await DownloadTiles(tiles, zoom, conn);
            }
            conn.Close();
            Log.Information($"Done downloading map for {metadata.name}");

        }

        private static async Task DownloadTiles(AwesomeTiles.TileRange range, int zoom, SQLiteConnection conn)
        {
            string OSMServer = Preferences.Get("OSMServer", PrefsActivity.OSMServer_s);

            //Same, but without parallell processing. 
/*            foreach (var tile in range)
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
            };*/

            await range.ParallelForEachAsync(async tile =>
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
                WriteOsmSQlite(data, zoom, tile.X, tile.Y, conn);
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
                using (var httpResponse = await _httpClient.GetAsync(imageUrl))
                {
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
            }
            catch (Exception ex)
            {
                //Handle Exception
                Log.Error($"DownloadImageAsync(...) crashed: {ex}");
            }

            return null;
        }

        private static void WriteOsmSQlite(byte[] osmData, int zoomIndex, int x, int y, SQLiteConnection conn)
        {
            Tiles.Tools.Tile tile = new Tiles.Tools.Tile()
            {
                Z = zoomIndex,
                Y = y,
                X = x
            };

            MBTilesWriter.WriteTile(conn, tile, osmData);
        }

        private static void InitMBTilesFolder()
        {
            string MBTilesPath = MainActivity.rootPath + "/MBTiles";
            if (!File.Exists(MBTilesPath))
            {
                Directory.CreateDirectory(MBTilesPath);
            }
        }
    }
}
