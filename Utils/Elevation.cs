﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Essentials;
using SharpGPX;
using BitMiracle;
using BitMiracle.LibTiff;
using BitMiracle.LibTiff.Classic;
using Mapsui.Projections;
using GeoTiffCOG;

namespace hajk
{
    internal class Elevation
    {
        public static void GetElevationData(GpxClass gpx)
        {
            try
            {
                //Range of tiles
                AwesomeTiles.TileRange tiles = GPXUtils.GPXUtils.GetTileRange(14, gpx); //Fix zoom at 14. Best we get from S3 bucket

                //Download tiles            
                List<string>? FileNames = DownloadElevationTiles(tiles);

                //Test data
                FileNames?.Add(Fragment_Preferences.rootPath + "/" + "14-14796-10082.tif");
                FileNames?.Add(Fragment_Preferences.rootPath + "/" + "14-14795-10082.tif");
                FileNames?.Add(Fragment_Preferences.rootPath + "/" + "14-14796-10081.tif");
                FileNames?.Add(Fragment_Preferences.rootPath + "/" + "14-14795-10081.tif");
                FileNames?.Add(Fragment_Preferences.rootPath + "/" + "14-14796-10080.tif");
                FileNames?.Add(Fragment_Preferences.rootPath + "/" + "14-14795-10080.tif");

                if (FileNames == null || FileNames?.Count == 0)
                {
                    return;
                }

                //Merge to single tile
                MergeElevationTiles(FileNames);

                //Convert Lat/Lon to Mercator
                float lat = -37.94607f;
                float lon = 144.40093f;
                var (x, y) = SphericalMercator.FromLonLat((double)lon, (double)lat);
                Serilog.Log.Debug($"Mercator, X:{x}, Y:{y}");

                try
                {
                    GeoTiff geoTiff = new GeoTiff(Fragment_Preferences.rootPath + "/" + "14-14763-10061.tif");
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

        private static void MergeElevationTiles(List<string>? FileNames)
        {
            if (FileNames == null)
            {
                return;
            }

            try
            {
                //var files = new List<byte[]>();
                foreach (var file in FileNames)
                {
                    using (Tiff image = Tiff.Open(file, "r"))
                    {
                        if (image == null)
                        {
                            Serilog.Log.Error("Could not open incoming image");
                            return;
                        }

                        // Check that it is of a type that we support
                        FieldValue[] value = image.GetField(TiffTag.BITSPERSAMPLE);
                        if (value == null)
                        {
                            Serilog.Log.Error("Undefined number of bits per sample");
                            return;
                        }

                        short bps = value[0].ToShort();
                        /*if (bps != 1)
                        {
                            Serilog.Log.Error("Unsupported number of bits per sample");
                            return;
                        }*/

                        value = image.GetField(TiffTag.SAMPLESPERPIXEL);
                        if (value == null)
                        {
                            Serilog.Log.Error("Undefined number of samples per pixel");
                            return;
                        }

                        short spp = value[0].ToShort();
                        if (spp != 1)
                        {
                            Serilog.Log.Error("Unsupported number of samples per pixel");
                            return;
                        }

                        // Read in the possibly multiple strips
                        int stripSize = image.StripSize();
                        int stripMax = image.NumberOfStrips();
                        Serilog.Log.Information($"stripSize: {stripSize}. stripMax: {stripMax}");

                        //Tiff image = Tiff.Open(file, "r"))
                        image.Close();
                    }

                    //files.Add(File.ReadAllBytes(file));
                }

                /*
                var tif = Tiff.Open(@"file", "r");
                var num = tif.NumberOfDirectories();
                for (short i = 0; i < num; i++)
                {
                    //set current page
                    tif.SetDirectory(i);

                    Bitmap bmp = GetBitmapFormTiff(tif);
                    bmp.Save(string.Format(@"newfile{0}.bmp", i));
                }*/

                //var targetFileData = TiffHelper.MergeTiff(files.ToArray());
                //File.WriteAllBytes(MainActivity.rootPath + "/" + ElevationData, targetFileData);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"COGGeoTIFF - MergeElevationTiles()");
            }
        }

        private static List<string>? DownloadElevationTiles(AwesomeTiles.TileRange range)
        {
            List<string> FileNames = [];

            try
            {
                string COGGeoTiffServer = Preferences.Get("COGGeoTiffServer", Fragment_Preferences.COGGeoTiffServer);

                foreach (var tile in range)
                {
                    var LocalFileName = Fragment_Preferences.rootPath + "/" + $"{tile.Zoom}-{tile.X}-{tile.Y}.tif";

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

        private static void WriteCOGGeoTiff(string imageUrl, byte[]? data)
        {
            if (data == null)
            {
                return;
            }

            try
            {
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
