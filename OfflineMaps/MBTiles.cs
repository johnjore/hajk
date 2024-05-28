using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using SQLite;
using hajk.Models;
using static hajk.TileCache;
using Xamarin.Essentials;

//This is a partial rewrite of https://github.com/bertt/MBTiles

namespace hajk
{
    public class MBTilesWriter
    {
        public static SQLiteConnection CreateDatabaseConnection(string db)
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
                    Log.Error(ex, $"MBTilesWriter - CreateDatabaseConnection() - Open");
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
                    Log.Error(ex, $"MBTilesWriter - WriteTile()");
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

        public static void PurgeMapDB(int Id)
        {
            try
            {
                string id = Id.ToString();
                Log.Debug($"Remove Id: {id}");

                lock (MbTileCache.sqlConn)
                {
                    //Remove single reference tiles
                    var query = MbTileCache.sqlConn.Table<tiles>().Where(x => x.reference == id);
                    Log.Debug($"Query Count: " + query.Count().ToString());
                    foreach (tiles maptile in query)
                    {
                        Log.Debug($"Tile Id: {maptile.id}, Reference: {maptile.reference}");
                        MbTileCache.sqlConn.Delete(maptile);
                    }

                    //Remove reference
                    query = MbTileCache.sqlConn.Table<tiles>().Where(x => x.reference.Contains(id));
                    Log.Debug($"Query Count: " + query.Count().ToString());
                    foreach (tiles maptile in query)
                    {
                        Log.Debug($"Tile Id: {maptile.id}, Before: {maptile.reference}");

                        maptile.reference = maptile.reference.Replace("," + id, "");
                        maptile.reference = maptile.reference.Replace(id + ",", "");

                        Log.Debug($"Tile Id: {maptile.id}, After: {maptile.reference}");
                        MbTileCache.sqlConn.Update(maptile);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "PurgeMapDb()");
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
                                        newTile.reference = null;
                                        newTile.id = 0;
                                    }

                                    newTile.createDate = DateTime.UtcNow;
                                    MBTilesWriter.WriteTile(newTile);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error($"Crashed: {ex}");
                                }
                            }
                        }

                        ImportDB.Close();
                        ImportDB.Dispose();
                        ImportDB = null;

                        var m = MainActivity.mContext;
                        Show_Dialog msg = new Show_Dialog(m);
                        await msg.ShowDialog(m.GetString(Resource.String.Done), m.GetString(Resource.String.MapTilesImported), Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.NONE, Show_Dialog.MessageResult.OK);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to import map file: '{ex}'");
                }
            });
        }
    }
}
