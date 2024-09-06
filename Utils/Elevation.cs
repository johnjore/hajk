using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpGPX;
using BitMiracle;
using BitMiracle.LibTiff;
using BitMiracle.LibTiff.Classic;
using Mapsui.Projections;
using Microsoft.Maui.Storage;
using GeoTiffCOG;
using Org.W3c.Dom.LS;
using Android.Util;

namespace hajk
{
    internal class Elevation
    {
        public static void GetElevationData(GpxClass gpx)
        {
            string GeoTiffFolder = Fragment_Preferences.rootPath + "/" + Fragment_Preferences.GeoTiffFolder + "/";

            //Make sure GeoTiffFolder exists 
            string? directory = Path.GetDirectoryName(GeoTiffFolder);
            if (!Directory.Exists(GeoTiffFolder)) Directory.CreateDirectory(GeoTiffFolder);

            //Contents
            Serilog.Log.Error("Files in GeoTiff Folder");
            var a = Directory.GetFiles(GeoTiffFolder);
            foreach (string fileName in a)
                Serilog.Log.Error(fileName);


            try
            {
                //Range of tiles
                AwesomeTiles.TileRange tiles = GPXUtils.GPXUtils.GetTileRange(14, gpx); //Fix zoom at 14. Best we get from S3 bucket

                //Download tiles            
                List<string>? FileNames = DownloadElevationTiles(tiles);

                //Test data
                FileNames?.Add(GeoTiffFolder + "14-14796-10082.tif");
                FileNames?.Add(GeoTiffFolder + "14-14795-10082.tif");
                FileNames?.Add(GeoTiffFolder + "14-14796-10081.tif");
                FileNames?.Add(GeoTiffFolder + "14-14795-10081.tif");
                FileNames?.Add(GeoTiffFolder + "14-14796-10080.tif");
                FileNames?.Add(GeoTiffFolder + "14-14795-10080.tif");

                if (FileNames == null || FileNames?.Count == 0)
                {
                    return;
                }

                //Convert Lat/Lon to Mercator
                float lat = -37.56364f;
                float lon = 144.37729f;
                var (x, y) = SphericalMercator.FromLonLat((double)lon, (double)lat);
                Serilog.Log.Debug($"Mercator, X:{x}, Y:{y}");

                try
                {
                    GeoTiff geoTiff = new GeoTiff(GeoTiffFolder + "14-14763-10061.tif");
                    double value = geoTiff.GetElevationAtLatLon(y, x);
                    Serilog.Log.Information($"Elevaton at lat:{lat:N3}, lon:{lon:N3} is '{value}' meters");
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, $"COGGeoTIFF - Elevation data not in tif file");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"COGGeoTIFF - GetElevationData()");
            }

            return;
        }

        private static List<string>? DownloadElevationTiles(AwesomeTiles.TileRange range)
        {
            List<string> FileNames = [];

            try
            {
                string COGGeoTiffServer = Preferences.Get("COGGeoTiffServer", Fragment_Preferences.COGGeoTiffServer);

                foreach (var tile in range)
                {
                    var LocalFileName = Fragment_Preferences.rootPath + "/" + Fragment_Preferences.GeoTiffFolder + "/" + $"{tile.Zoom}-{tile.X}-{tile.Y}.tif";

                    if (Downloaded(LocalFileName) == false)
                    {
                        byte[]? data = null;
                        for (int i = 0; i < 10; i++)
                        {
                            var url = COGGeoTiffServer + $"{tile.Zoom}/{tile.X}/{tile.Y}.tif";
                            data = DownloadImageAsync(url);

                            if (data != null)
                                break;

                            //Thread.Sleep(10000);
                        }

                        FileNames.Add(LocalFileName);

                        Serilog.Log.Information($"x/y: {tile.X}/{tile.Y}, ID: {tile.Id}");
                        WriteCOGGeoTiff(LocalFileName, data);
                    }
                };

                return FileNames;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"COGGeoTIFF - DownloadElevationTiles()");
                return null;
            }
        }

        private static void WriteCOGGeoTiff(string? imageUrl, byte[]? data)
        {
            if (data == null || imageUrl == null)
            {
                return;
            }

            try
            {
                Serilog.Log.Information("Saving: ${ImageUrl}");
                System.IO.File.WriteAllBytes(imageUrl, data);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"COGGeoTIFF - WriteCOGGeoTiff()");
            }
        }

        private static bool Downloaded(string imageUrl)
        {
            try
            {
                Serilog.Log.Verbose($"Checking if '{imageUrl}' exists");

                // Only if file or placeholder does not exist
                if (File.Exists(imageUrl) || File.Exists(imageUrl + ".hajk"))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"COGGeoTIFF - Downloaded()");
            }

            return false;
        }

        private static byte[]? DownloadImageAsync(string imageUrl)
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
                using var httpResponse = _httpClient.GetAsync(imageUrl).Result;
                if (httpResponse.StatusCode == HttpStatusCode.OK)
                {
                    return httpResponse.Content.ReadAsByteArrayAsync().Result;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"COGGeoTIFF - DownloadImageAsync()");
            }

            return null;
        }
    }
}
