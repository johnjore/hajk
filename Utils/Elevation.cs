﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpGPX;
using SharpGPX.GPX1_1;
using Mapsui.Projections;
using GeoTiffCOG;
using GPXUtils;
using Android.Widget;
using Android.OS;

namespace hajk
{
    internal class Elevation
    {
        /// <summary>
        /// Download Elevation data files (GeoTiff) from AWS S3 bucket
        /// </summary>
        public static async Task<bool> DownloadElevationData(GpxClass gpx)
        {
            try
            {
                string GeoTiffFolder = Fragment_Preferences.LiveData + "/" + Fragment_Preferences.GeoTiffFolder + "/";

                //Make sure GeoTiff folder exists 
                if (!Directory.Exists(GeoTiffFolder)) 
                {
                    Directory.CreateDirectory(GeoTiffFolder);
                }

                //Current contents
                /*
                Serilog.Log.Debug("Current files in GeoTiff Folder:");
                foreach (string fileName in Directory.GetFiles(GeoTiffFolder))
                    Serilog.Log.Debug(fileName);
                */

                //Range of tiles
                AwesomeTiles.TileRange? tiles = GPXUtils.GPXUtils.GetTileRange(Fragment_Preferences.Elevation_Tile_Zoom, gpx);

                //Tiles to process?
                if (tiles == null)
                {
                    return false;
                }

                //Count missing tiles
                int intMissingTiles = 0;
                foreach (var tile in tiles)
                {
                    var LocalFileName = $"{tile.Zoom}-{tile.X}-{tile.Y}.tif";
                    var FullFileName = Fragment_Preferences.LiveData + "/" + Fragment_Preferences.GeoTiffFolder + "/" + LocalFileName;
                    if (Downloaded(FullFileName) == false)
                    {
                        Serilog.Log.Information($"Need to download elevation tile: '{LocalFileName}'");
                        intMissingTiles++;
                    }
                }
                Serilog.Log.Information($"Need to download '{intMissingTiles}' elevation tiles");

                //Nothing to download?
                if (intMissingTiles == 0)
                {
                    if (Looper.MyLooper() == null)
                    {
                        Looper.Prepare();
                    }

                    Toast.MakeText(Platform.AppContext, "Elevation tiles already downloaded", ToastLength.Short)?.Show();
                    return true;
                }

                //Download elevation tiles
                (List<string>? FileNames, int MissingTilesCounter) = await DownloadElevationTilesAsync(tiles, intMissingTiles);
                if (MissingTilesCounter > 0)
                {
                    if (Looper.MyLooper() == null)
                    {
                        Looper.Prepare();
                    }

                    Toast.MakeText(Platform.AppContext, $"{MissingTilesCounter} elevation tiles failed to download", ToastLength.Long)?.Show();
                }

                //Current contents
                /*Serilog.Log.Debug("Files in GeoTiff Folder (after downloads):");
                foreach (string fileName in Directory.GetFiles(GeoTiffFolder))
                    Serilog.Log.Debug(fileName);
                */
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"COGGeoTIFF - GetElevationData()");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Lookup elevation data from a List of Position and return same list with Elevation field populated
        /// </summary>
        /*
        public static async Task<List<Position>?>? LookupElevationData(List<Position>? ListLatLon)
        {
            //Any data to process?
            if (ListLatLon == null)
                return null;

            //Progressbar
            int doneCount = 0;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _ = Progressbar.UpdateProgressBar.CreateGUIAsync($"Looking up elevation data");
                Progressbar.UpdateProgressBar.Progress = 0;
                Progressbar.UpdateProgressBar.MessageBody = $"{doneCount} of {ListLatLon.Count * 2}";
            });

            await Task.Run(() =>
            {
                //Add the GeoTiffFile name to the GPS position
                for (int i = 0; i < ListLatLon.Count; i++)
                {
                    //Data
                    AwesomeTiles.Tile? tmp1 = GPXUtils.GPXUtils.GetTileRange(Fragment_Preferences.Elevation_Tile_Zoom, new Position(ListLatLon[i].Latitude, ListLatLon[i].Longitude, 0, false, null))?.FirstOrDefault();
                    ListLatLon[i].GeoTiffFileName = Fragment_Preferences.LiveData + "/" + Fragment_Preferences.GeoTiffFolder + "/" + $"{Fragment_Preferences.Elevation_Tile_Zoom}-{tmp1?.X}-{tmp1?.Y}.tif";

                    //Update progress bar
                    Progressbar.UpdateProgressBar.Progress = (int)Math.Floor((decimal)++doneCount * 100 / (ListLatLon.Count * 2));
                    Progressbar.UpdateProgressBar.MessageBody = $"{doneCount} of {ListLatLon.Count * 2}";

                    //Thread.Sleep(200);
                }

                //Unique filenames
                var FileNames = ListLatLon.Select(x => x.GeoTiffFileName).Distinct().Order();

                //Loop through each filename and extract the relevant elvation data
                foreach (var FileName in FileNames)
                {
                    if (FileName == null || File.Exists(FileName) == false)
                    {
                        Serilog.Log.Error($"COGGeoTIFF - FileName is Null, or does not exist: '{FileName}'");
                        throw new InvalidOperationException("Filename is null, or does not exist");
                    }

                    //Loop through identical FileNames
                    var geoTiff = new GeoTiff(FileName);

                    foreach (var e in ListLatLon.Where(p => (p.GeoTiffFileName == FileName)))
                    {
                        var (y, x) = SphericalMercator.FromLonLat((double)e.Longitude, (double)e.Latitude);

                        try
                        {
                            var tmpElevationData = geoTiff.GetElevationAtLatLon(x, y);

                            if (tmpElevationData > -100)
                            {
                                e.Elevation = tmpElevationData;
                                e.ElevationSpecified = true;

                                //Serilog.Log.Debug($"Elevation at lat:{e.Latitude:N4}, lon:{e.Longitude:N4} is '{e.Elevation}' meters");
                            }
                            else
                            {
                                e.Elevation = 0;
                                e.ElevationSpecified = false;
                                Serilog.Log.Error($"Elevation data is lower than -100m for x:{x} and y:{y} in {FileName}");
                                //Serilog.Log.Debug($"Elevation at lat:{e.Latitude:N4}, lon:{e.Longitude:N4} is '{e.Elevation}' meters");
                            }
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Error(ex, $"Failed to lookup ElevationData for x:{x} and y:{y} in {FileName}");
                            e.Elevation = 0;
                            e.ElevationSpecified = false;
                        }

                        //Update progress bar
                        Progressbar.UpdateProgressBar.Progress = (int)Math.Floor((decimal)++doneCount * 100 / (ListLatLon.Count * 2));
                        Progressbar.UpdateProgressBar.MessageBody = $"{doneCount} of {ListLatLon.Count * 2}";

                        //Thread.Sleep(200);
                    }

                    geoTiff.Dispose();
                }
            });

            Progressbar.UpdateProgressBar.Dismiss();

            return ListLatLon;
        }
        */

        /// <summary>
        /// Lookup elevation data from a route and return same route with Elevation field populated
        /// </summary>
        public static rteType? LookupElevationData(rteType? route)
        {
            try
            {
                //Any data to process?
                if (route == null)
                {
                    return null;
                }

                //Progressbar
                int doneCount = 0;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _ = Progressbar.UpdateProgressBar.CreateGUIAsync($"Looking up elevation data");
                    Progressbar.UpdateProgressBar.Progress = 0;
                    Progressbar.UpdateProgressBar.MessageBody = $"{doneCount} of {route.rtept.Count}";
                });

                //Add the GeoTiffFile name to the GPS position
                for (int i = 0; i < route.rtept.Count; i++)
                {
                    //Data
                    AwesomeTiles.Tile? tmp1 = GPXUtils.GPXUtils.GetTileRange(Fragment_Preferences.Elevation_Tile_Zoom, new Position((double)route.rtept[i].lat, (double)route.rtept[i].lon, 0, false, null))?.FirstOrDefault();
                    route.rtept[i].src = Fragment_Preferences.LiveData + "/" + Fragment_Preferences.GeoTiffFolder + "/" + $"{Fragment_Preferences.Elevation_Tile_Zoom}-{tmp1?.X}-{tmp1?.Y}.tif";
                }

                //Unique filenames
                var FileNames = route.rtept.Select(x => x.src).Distinct().Order();

                //Loop through each filename and extract the relevant elvation data
                foreach (var FileName in FileNames)
                {
                    if (FileName == null || File.Exists(FileName) == false)
                    {
                        Serilog.Log.Error($"COGGeoTIFF - FileName is Null, or does not exist: '{FileName}'");
                        throw new InvalidOperationException("Filename is null, or does not exist");
                    }

                    //Loop through identical FileNames
                    Serilog.Log.Information($"Looking Up Elevation Data in '{FileName}'");
                    var geoTiff = new GeoTiff(FileName);

                    foreach (var e in route.rtept.Where(p => (p.src == FileName)))
                    {
                        var (y, x) = SphericalMercator.FromLonLat((double)e.lon, (double)e.lat);
                        try
                        {
                            var tmpElevationData = Convert.ToDecimal(geoTiff.GetElevationAtLatLon(x, y));

                            if (tmpElevationData > -100)
                            {
                                e.ele = tmpElevationData;
                                e.eleSpecified = true;
                                //Serilog.Log.Debug($"Elevation at lat:{e.Latitude:N4}, lon:{e.Longitude:N4} is '{e.Elevation}' meters");
                            }
                            else
                            {
                                e.ele = -99999;
                                e.eleSpecified = false;
                                Serilog.Log.Error($"Elevation data is lower than -100m, {tmpElevationData}, for x:{x} and y:{y} in {FileName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Error(ex, $"Failed to lookup ElevationData for x:{x}, y{y} in {FileName}");
                            e.ele = -99999;
                            e.eleSpecified = false;
                        }
                        finally
                        {
                            //Update progress bar
                            Progressbar.UpdateProgressBar.Progress = (int)Math.Floor((decimal)++doneCount * 100 / (route.rtept.Count));
                            Progressbar.UpdateProgressBar.MessageBody = $"{doneCount} of {route.rtept.Count}";
                        }
                    }

                    geoTiff.Dispose();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to updated GPX with elevation data");
            }
            finally
            {
                Progressbar.UpdateProgressBar.Dismiss();
            }

            return route;
        }

        /// <summary>
        /// Lookup elevation data from a track and return same trac with Elevation field populated
        /// </summary>
        public static trkType? LookupElevationData(trkType? track)
        {
            try
            {
                //Any data to process?
                if (track == null)
                {
                    return null;
                }

                //Progressbar
                int doneCount = 0;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _ = Progressbar.UpdateProgressBar.CreateGUIAsync($"Looking up elevation data");
                    Progressbar.UpdateProgressBar.Progress = 0;
                    Progressbar.UpdateProgressBar.MessageBody = $"{doneCount} of {track.trkseg[0].trkpt.Count}";
                });

                //Add the GeoTiffFile name to the GPS position
                for (int i = 0; i < track.trkseg[0].trkpt.Count; i++)
                {
                    //Data
                    AwesomeTiles.Tile? tmp1 = GPXUtils.GPXUtils.GetTileRange(Fragment_Preferences.Elevation_Tile_Zoom, new Position((double)track.trkseg[0].trkpt[i].lat, (double)track.trkseg[0].trkpt[i].lon, 0, false, null))?.FirstOrDefault();
                    track.trkseg[0].trkpt[i].src = Fragment_Preferences.LiveData + "/" + Fragment_Preferences.GeoTiffFolder + "/" + $"{Fragment_Preferences.Elevation_Tile_Zoom}-{tmp1?.X}-{tmp1?.Y}.tif";
                }

                //Unique filenames
                var FileNames = track.trkseg[0].trkpt.Select(x => x.src).Distinct().Order();

                //Loop through each filename and extract the relevant elvation data
                foreach (var FileName in FileNames)
                {
                    if (FileName == null || File.Exists(FileName) == false)
                    {
                        Serilog.Log.Error($"COGGeoTIFF - FileName is Null, or does not exist: '{FileName}'");
                        throw new InvalidOperationException("Filename is null, or does not exist");
                    }

                    //Loop through identical FileNames
                    var geoTiff = new GeoTiff(FileName);

                    foreach (var e in track.trkseg[0].trkpt.Where(p => (p.src == FileName)))
                    {
                        var (y, x) = SphericalMercator.FromLonLat((double)e.lon, (double)e.lat);

                        try
                        {
                            var tmpElevationData = Convert.ToDecimal(geoTiff.GetElevationAtLatLon(x, y));

                            if (tmpElevationData > -100)
                            {
                                e.ele = tmpElevationData;
                                e.eleSpecified = true;
                                //Serilog.Log.Debug($"Elevation at lat:{e.Latitude:N4}, lon:{e.Longitude:N4} is '{e.Elevation}' meters");
                            }
                            else
                            {
                                e.ele = 0;
                                e.eleSpecified = false;
                                Serilog.Log.Error($"Elevation data is lower than -100m for x:{x} and y:{y} in {FileName}");
                                //Serilog.Log.Debug($"Elevation at lat:{e.Latitude:N4}, lon:{e.Longitude:N4} is '{e.Elevation}' meters");
                            }
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Error(ex, $"Failed to lookup ElevationData for x:{x}, y{y} in {FileName}");
                            e.ele = 0;
                            e.eleSpecified = false;                            
                        }
                        finally
                        {
                            //Update progress bar
                            Progressbar.UpdateProgressBar.Progress = (int)Math.Floor((decimal)++doneCount * 100 / (track.trkseg[0].trkpt.Count));
                            Progressbar.UpdateProgressBar.MessageBody = $"{doneCount} of {track.trkseg[0].trkpt.Count}";
                        }
                    }

                    geoTiff.Dispose();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to updated GPX with elevation data");
            }
            finally
            {
                Progressbar.UpdateProgressBar.Dismiss();
            }

            return track;
        }


        /// <summary>
        /// Loop through a list of Positions with elevation data and calculate how much ascent and descent
        /// </summary>
        /// <param name="LatLonEle"></param>
        /// <returns>ascent and descent in meters</returns>
        public static (int, int) CalculateAscentDescent(List<Position> LatLonEle)
        {
            /**///List of Positions needs slicing to make more granular

            double ascent = 0;
            double descent = 0;

            //Only include where "ElevationSpecified == true"
            var filtered_list = LatLonEle.Where(x => x.ElevationSpecified == true).ToList();

            try
            {
                for (int j = 0; j < filtered_list.Count - 1; j++)
                {
                    var j0 = filtered_list[j].Elevation;
                    var j1 = filtered_list[j + 1].Elevation;

                    if (j0 > j1)
                    {
                        descent += j0 - j1;
                    }
                    else
                    {
                        ascent += j1 - j0;
                    }
                }

                return ((int)ascent, (int)descent);
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"CalculateAscentDescent(List<Position>)");
                return (0, 0);
            }
        }

        /// <summary>
        /// Loop through a route with elevation data and calculate how much ascent and descent
        /// </summary>
        /// <param name="route"></param>
        /// <returns>ascent and descent in meters</returns>
        public static (int, int) CalculateAscentDescent(rteType route)
        {
            /**///List of Positions needs slicing to make more granular

            decimal ascent = 0;
            decimal descent = 0;
            
            //Only include where "eleSpecified == true"
            var filtered_route = route.rtept.Where(x => x.eleSpecified == true).ToList();

            try
            {
                for (int j = 0; j < filtered_route.Count - 1; j++)
                {
                    var j0 = filtered_route[j].ele;
                    var j1 = filtered_route[j + 1].ele;

                    if (j0 > j1)
                    {
                        descent += j0 - j1;
                    }
                    else
                    {
                        ascent += j1 - j0;
                    }
                }

                return ((int)ascent, (int)descent);
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"CalculateAscentDescent(rteType)");
                return (0, 0);
            }
        }

        /// <summary>
        /// Loop through a route with elevation data and calculate how much ascent and descent
        /// </summary>
        /// <param name="track"></param>
        /// <returns>ascent and descent in meters</returns>
        public static (int, int) CalculateAscentDescent(trkType track)
        {
            var route = track.ToRoutes();

            if (route.Count != 1)
            {
                Serilog.Log.Fatal("Why is this not 1?");
                return (0, 0);
            }

            (var ascent, var descent) = CalculateAscentDescent(route[0]);
            return ((int)ascent, (int)descent);
        }

        private static async Task<(List<string>?, int)>? DownloadElevationTilesAsync(AwesomeTiles.TileRange? range, int intMissingtiles)
        {
            if (range == null)
            {
                return ([], -1);
            }

            //Misc
            List<string> FileNames = [];
            string COGGeoTiffServer = Fragment_Preferences.COGGeoTiffServer;
            int doneCount = 0;
            int FailedtoDownloadTilesCounter = 0;

            //Progress bar
            _ = Progressbar.UpdateProgressBar.CreateGUIAsync($"Downloading elevation tiles");
            Progressbar.UpdateProgressBar.Progress = 0;
            Progressbar.UpdateProgressBar.MessageBody = $"{doneCount} of {intMissingtiles}";

            await Task.Run(() =>
            {
                try
                {
                    foreach (var tile in range)
                    {
                        var LocalFileName = Fragment_Preferences.LiveData + "/" + Fragment_Preferences.GeoTiffFolder + "/" + $"{tile.Zoom}-{tile.X}-{tile.Y}.tif";

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
                                    if (i == 9)
                                    {
                                        FailedtoDownloadTilesCounter++;
                                    }
                                }
                            }
                        }
                    };
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, $"COGGeoTIFF - DownloadElevationTiles()");
                }
                finally
                {
                    Progressbar.UpdateProgressBar.Dismiss();
                }
            });

            return (FileNames, FailedtoDownloadTilesCounter);
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
                Serilog.Log.Fatal(ex, $"COGGeoTIFF - WriteCOGGeoTiff()");
            }
        }

        private static bool Downloaded(string imageUrl)
        {
            try
            {
                Serilog.Log.Debug($"Checking if '{imageUrl}' exists");

                // Only if file or placeholder does not exist
                if (File.Exists(imageUrl) || File.Exists(imageUrl + ".hajk"))
                {
                    Serilog.Log.Debug("File exists. Return true");
                    return true;
                }
                else
                {
                    Serilog.Log.Debug("File does not exist");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, "COGGeoTIFF - Downloaded() Failed");
                return false;
            }
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
                Serilog.Log.Fatal(ex, $"COGGeoTIFF - DownloadImageAsync()");
            }

            return null;
        }
    }
}
