using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using SQLite;
using hajk.Models;
using static hajk.TileCache;

//This is a partial rewrite of https://github.com/bertt/MBTiles

namespace hajk
{
    public class MBTilesWriter
    {
        public static SQLiteConnection? CreateDatabaseConnection(string db)
        {
            if (!File.Exists(db))
            {
                //How can it not exist if used as cache?
                Log.Error($"MBTilesWriter - CreateDatabaseConnection() - Open");
            }
            else
            {
                //Open database
                try
                {
                    var sqliteConnection = new SQLiteConnection(db, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex, true);
                    return sqliteConnection;
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, $"MBTilesWriter - CreateDatabaseConnection() - Open");
                }
            }

            return null;
        }

        public static int WriteTile(SQLiteConnection sqlConnection, tiles mbtile)
        {
            if (mbtile.tile_data == null)
            {
                return 0;
            }

            lock (sqlConnection)
            {
                try
                {
                    if (mbtile.id == 0)
                    {
                        return sqlConnection.Insert(mbtile);
                    }
                    else
                    {
                        return sqlConnection.Update(mbtile);
                    }
                }
                catch (Exception ex)
                {
                    Log.Fatal(ex, $"MBTilesWriter - WriteTile()");
                    return 0;
                }
            }
        }

        public static int WriteTile(tiles mbtile)
        {
            if (TileCache.MbTileCache.sqlConn == null)
            {
                return 0;
            }

            return (WriteTile(TileCache.MbTileCache.sqlConn, mbtile));
        }

        public static async Task PurgeMapTile(int Id, AwesomeTiles.Tile tile)
        {
            try
            {
                Log.Debug($"Remove tile, or reference to tile, from: {Id}, {tile.Zoom}/{tile.X}/{tile.Y}");

                lock (MbTileCache.sqlConn)
                {
                    int tmsY = (int)Math.Pow(2, tile.Zoom) - 1 - tile.Y;
                    var maptile = MbTileCache.sqlConn.Table<tiles>().Where(x => x.zoom_level == tile.Zoom && x.tile_column == tile.X && x.tile_row == tmsY).FirstOrDefault();
                    if (maptile != null)
                    {
                        //When single entry, old or new (JSON) format
                        var oldFormat = Id.ToString();
                        var newFormat = "[" + Id.ToString() + "]";
                        if (maptile.reference == oldFormat || maptile.reference == newFormat)
                        {
                            Log.Debug($"Tile Id: {maptile.id}, Reference: {maptile.reference}");
                            MbTileCache.sqlConn.Delete(maptile);
                        }
                        else if (maptile.reference.Contains(Id.ToString()))
                        {
                            //When multiple entries in reference field, remove reference, but do not delete tile
                            //Carefull: Captures variants of 1151 15 and 5 when looking for '5'
                            List<int>? reference = JsonSerializer.Deserialize<List<int>>(maptile.reference);
                            if (reference.Contains(Id))
                            {
                                Log.Debug($"Tile Id: {maptile.id}, Reference: {maptile.reference}");
                                reference.Remove(Id);
                                maptile.reference = JsonSerializer.Serialize(reference);
                                Log.Debug($"Tile Id: {maptile.id}, Reference: {maptile.reference}");
                                MbTileCache.sqlConn.Update(maptile);
                            }
                        }
                    }
                    else
                    {
                        Log.Error($"Should not run this line. It means we're trying to process a tile thats not identified as part of the tiles for the GPX: maptile is null");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "PurgeMapTile()");
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
                        var ImportDB = new SQLiteConnection(sourceFile.FullPath, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex, true);
                        var tiles = ImportDB.Table<tiles>();

                        Log.Debug($"Tiles to import: " + tiles.Count().ToString());
                        lock (MbTileCache.sqlConn)
                        {
                            foreach (tiles newTile in tiles)
                            {
                                //Do we already have the tile?
                                try
                                {
                                    tiles oldTile = MbTileCache.sqlConn.Table<tiles>().Where(x => x.zoom_level == newTile.zoom_level && x.tile_column == newTile.tile_column && x.tile_row == newTile.tile_row).FirstOrDefault();
                                    if ((oldTile != null) && (oldTile.createDate >= newTile.createDate))
                                    {
                                        continue;
                                    }

                                    if (oldTile != null)
                                    {
                                        //Update existing tile
                                        newTile.reference = oldTile.reference;
                                        newTile.id = oldTile.id;
                                    }
                                    else
                                    {
                                        //Insert new tile
                                        newTile.reference = string.Empty;
                                        newTile.id = 0;
                                    }

                                    newTile.createDate = DateTime.UtcNow;
                                    MBTilesWriter.WriteTile(newTile);
                                }
                                catch (Exception ex)
                                {
                                    Log.Fatal($"Crashed: {ex}");
                                }
                            }
                        }

                        ImportDB.Close();
                        ImportDB.Dispose();
                        ImportDB = null;

                        Show_Dialog msg = new(Platform.CurrentActivity);
                        await msg.ShowDialog(Platform.CurrentActivity.GetString(Resource.String.Done), Platform.CurrentActivity.GetString(Resource.String.MapTilesImported), Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
                    }
                }
                catch (Exception ex)
                {
                    Log.Fatal($"Failed to import map file: '{ex}'");
                }
            });
        }

        public static void PurgeOldTiles()
        {
            lock (MbTileCache.sqlConn)
            {
                var query = MbTileCache.sqlConn.Table<tiles>().Where(x => (DateTime.UtcNow - x.createDate).TotalDays > Fragment_Preferences.OfflineMaxAge);
                Log.Debug($"Query Count: " + query.Count().ToString());
                foreach (tiles maptile in query)
                {
                    Log.Debug($"Tile Id: {maptile.id}, Reference: {maptile.reference}");
                    /**/
                    //MbTileCache.sqlConn.Delete(maptile);
                }
            }
        }

        public static async void RefreshOldTiles()
        {
            string OSMServer = string.Empty;
            string TileBulkDownloadSource = Preferences.Get(Platform.CurrentActivity?.GetString(Resource.String.OSM_Browse_Source), Fragment_Preferences.TileBrowseSource);

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
            else //Mapbox || Thunderforest || Stadia Maps
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

            var query = MbTileCache.sqlConn.Table<tiles>().Where(x => (DateTime.UtcNow - x.createDate).TotalDays > Fragment_Preferences.OfflineMaxAge);
            Log.Debug($"Query Count: " + query.Count().ToString());

            foreach (tiles maptile in query)
            {
                var url = OSMServer + $"{maptile.zoom_level}/{maptile.tile_column}/{maptile.tile_row}.png";
                var data = await DownloadRasterImageMap.DownloadImageAsync(url);

                if (data != null)
                {
                    //Update
                    maptile.tile_data = data;
                    MbTileCache.sqlConn.Update(maptile);
                }
            }
        }
    }
}
