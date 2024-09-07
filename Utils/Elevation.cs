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
using GeoTiffCOG;
using GPXUtils;
using Android.Widget;

namespace hajk
{
    internal class Elevation
    {
        public static async void GetElevationData(GpxClass gpx)
        {
            try
            {
                string GeoTiffFolder = Fragment_Preferences.rootPath + "/" + Fragment_Preferences.GeoTiffFolder + "/";

                //Make sure GeoTiff folder exists 
                string? directory = Path.GetDirectoryName(GeoTiffFolder);
                if (!Directory.Exists(GeoTiffFolder)) Directory.CreateDirectory(GeoTiffFolder);

                //Current contents
                Serilog.Log.Verbose("Files in GeoTiff Folder:");
                foreach (string fileName in Directory.GetFiles(GeoTiffFolder))
                    Serilog.Log.Verbose(fileName);

                //Range of tiles
                AwesomeTiles.TileRange? tiles = GPXUtils.GPXUtils.GetTileRange(14, gpx); //Fix zoom at 14. Best we get from S3 bucket

                //Count missing tiles
                int intMissingTiles = 0;
                foreach (var tile in tiles)
                {
                    var LocalFileName = Fragment_Preferences.rootPath + "/" + Fragment_Preferences.GeoTiffFolder + "/" + $"{tile.Zoom}-{tile.X}-{tile.Y}.tif";
                    if (Downloaded(LocalFileName) == false)
                    {
                        Serilog.Log.Information($"Need to download elevation tile: '{LocalFileName}'");
                        intMissingTiles++;
                    }
                }
                Serilog.Log.Information($"Need to download '{intMissingTiles}' elevation tiles");

                //Nothing to download?
                if (intMissingTiles == 0)
                {
                    Toast.MakeText(Platform.AppContext, "Elevation tiles already downloaded", ToastLength.Short)?.Show();
                    return;
                }

                //Download tiles
                await DownloadElevationTilesAsync(tiles, intMissingTiles);

                return;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"COGGeoTIFF - GetElevationData()");
            }

            return;
        }

        public static double LookupElevationData(Position? pos)
        {
            if (pos == null)
            {
                return 99999;
            }

            try
            {
                var tiles = GPXUtils.GPXUtils.GetTileRange(14, new Position(pos.Latitude, pos.Longitude, 0));                
                var tile = tiles.FirstOrDefault();      //Should only be a single tile for a single GPS Position
                var LocalFileName = Fragment_Preferences.rootPath + "/" + Fragment_Preferences.GeoTiffFolder + "/" + $"{tiles.Zoom}-{tile?.X}-{tile?.Y}.tif";

                if (!File.Exists(LocalFileName))
                {
                    Serilog.Log.Error($"COGGeoTIFF - Missing Elevation file: '{tiles.Zoom}-{tile?.X}-{tile?.Y}.tif' for Lat/Lng: '{pos.Latitude} {pos.Longitude}'");
                    return 99998;
                }

                var geoTiff = new GeoTiff(LocalFileName);
                var (x, y) = SphericalMercator.FromLonLat((double)pos.Longitude, (double)pos.Latitude);
                double value = geoTiff.GetElevationAtLatLon(y, x);
                Serilog.Log.Information($"Elevaton at lat:{pos.Latitude:N4}, lon:{pos.Longitude:N4} is '{value}' meters");

                return value;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"COGGeoTIFF - Elevation data not in tif file");
                return 999997;
            }
        }

        private static async Task<List<string>>? DownloadElevationTilesAsync(AwesomeTiles.TileRange? range, int intMissingtiles)
        {
            if (range == null)
            {
                return [];
            }

            //Misc
            List<string> FileNames = [];
            string COGGeoTiffServer = Preferences.Get("COGGeoTiffServer", Fragment_Preferences.COGGeoTiffServer);
            int doneCount = 0;

            //Progress bar
            Progressbar.UpdateProgressBar.CreateGUI($"Downloading elevation tiles");
            Progressbar.UpdateProgressBar.Progress = 0;
            Progressbar.UpdateProgressBar.MessageBody = $"{doneCount} of {intMissingtiles}";

            try
            {
                await Task.Run(() =>
                {
                    foreach (var tile in range)
                    {
                        var LocalFileName = Fragment_Preferences.rootPath + "/" + Fragment_Preferences.GeoTiffFolder + "/" + $"{tile.Zoom}-{tile.X}-{tile.Y}.tif";

                        if (Downloaded(LocalFileName) == false)
                        {
                            Serilog.Log.Information($"Going to download {LocalFileName}");
                            byte[]? data = null;
                            for (int i = 0; i < 10; i++)
                            {
                                var url = COGGeoTiffServer + $"{tile.Zoom}/{tile.X}/{tile.Y}.tif";
                                data = DownloadImageAsync(url);
                                                                
                                if (data != null)
                                {
                                    Serilog.Log.Information($"Downloaded Elevation Tile: x/y: {tile.X}/{tile.Y}, ID: {tile.Id} from '{url}'");

                                    FileNames.Add(LocalFileName);
                                    WriteCOGGeoTiff(LocalFileName, data);

                                    //Update progress bar
                                    Progressbar.UpdateProgressBar.Progress = (int)Math.Floor((decimal)++doneCount * 100 / intMissingtiles);
                                    Progressbar.UpdateProgressBar.MessageBody = $"{doneCount} of {intMissingtiles}";

                                    break;
                                }
                                else
                                {
                                    Serilog.Log.Information($"Failed to download Elevation Tile: x/y: {tile.X}/{tile.Y}, ID: {tile.Id} on attempt '{i}'");
                                }
                            }
                        }
                    };
                });               
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"COGGeoTIFF - DownloadElevationTiles()");
            }

            //Anything above 99 will close the ProgressBar GUI
            Progressbar.UpdateProgressBar.Progress = 100;

            return FileNames;
        }

        private static void WriteCOGGeoTiff(string? imageUrl, byte[]? data)
        {
            if (data == null || imageUrl == null)
            {
                return;
            }

            try
            {
                Serilog.Log.Information($"Saving: {imageUrl}");
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
                Serilog.Log.Error(ex, "COGGeoTIFF - Downloaded()");
            }

            return false;
        }

        private static byte[]? DownloadImageAsync(string imageUrl)
        {
            var clientHandler = new HttpClientHandler
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
