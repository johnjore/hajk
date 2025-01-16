using System;
using System.Linq;
using System.Collections.Generic;
using BruTile;
using BruTile.Cache;
using BruTile.Predefined;
using BruTile.Web;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using SQLite;
using hajk.Models;
using Android.Systems;
using hajk.Models.MapSource;

//From https://github.com/spaddlewit/MBTilesPersistentCache


namespace hajk
{
    class TileCache
    {
        static MbTileCache? mbTileCache;

        public static ITileSource? GetOSMBasemap(string cacheFilename)
        {
            try
            {
                if (mbTileCache == null)
                {
                    mbTileCache = new MbTileCache(cacheFilename, "png");
                }

                HttpTileSource src;
                string TileBrowseSource = Preferences.Get(Platform.CurrentActivity?.GetString(Resource.String.OSM_Browse_Source), Fragment_Preferences.TileBrowseSource);

                var MapSource = Fragment_Preferences.MapSources.Where(x => x.Name.Equals(TileBrowseSource, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (MapSource == null) 
                {
                    Serilog.Log.Error("No MapSource defined");
                    return null;
                }
                
                if (TileBrowseSource.Equals("OpenStreetMap", StringComparison.OrdinalIgnoreCase))
                {
                    src = new(new GlobalSphericalMercator(Fragment_Preferences.MinZoom, Fragment_Preferences.MaxZoom),
                        MapSource.BaseURL, new[] { "a", "b", "c" }, 
                        name: MapSource.Name,
                        persistentCache: mbTileCache,
                        userAgent: MapSource.Name + " in Mapsui (hajk)",
                        attribution: new Attribution("(c) " + MapSource.Name + " contributors", "https://www.openstreetmap.org/copyright"));
                }
                else if (TileBrowseSource.Equals("Custom", StringComparison.OrdinalIgnoreCase))
                {
                    var url = Preferences.Get(MapSource.BaseURL, "");
                    var token = Preferences.Get(MapSource.Token, "");

                    src = new(new GlobalSphericalMercator(Fragment_Preferences.MinZoom, Fragment_Preferences.MaxZoom),
                        url + token,
                        name: MapSource.Name,
                        persistentCache: mbTileCache,
                        userAgent: MapSource.Name + " in Mapsui (hajk)");
                }
                else //Mapbox || Thunderforest || Stadia Maps
                {
                    var token = Preferences.Get(MapSource.Token, "");

                    if (token == string.Empty || token == "")
                    {
                        Show_Dialog msg = new Show_Dialog(Platform.CurrentActivity);

                        var a = $"{TileBrowseSource} requires the token to be set";
                        msg.ShowDialog($"Token Required", a, Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.CANCEL, Show_Dialog.MessageResult.NONE);

                        return null;
                    }

                    src = new(new GlobalSphericalMercator(Fragment_Preferences.MinZoom, Fragment_Preferences.MaxZoom),
                        MapSource.BaseURL + token,
                        name: MapSource.Name,
                        persistentCache: mbTileCache,
                        userAgent: MapSource.Name + " in Mapsui (hajk)");
                }

                return src;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"TileCache - GetOSMBasemap()");
                return null;
            }
        }

        public class MbTileCache : IPersistentCache<byte[]>, IDisposable
        {
            public static List<MbTileCache> openConnections = new List<MbTileCache>();
            public static SQLiteConnection sqlConn = null;

            public MbTileCache(string filename, string format)
            {
                sqlConn = InitializeTileCache(filename, format);
                openConnections.Add(this);
            }

            /// <summary>
            /// Flips the Y coordinate from OSM to TMS format and vice versa.
            /// </summary>
            /// <param name="level">zoom level</param>
            /// <param name="row">Y coordinate</param>
            /// <returns>inverted Y coordinate</returns>
            static int OSMtoTMS(int level, int row)
            {
                return (1 << level) - row - 1;
            }

            public void Add(TileIndex index, byte[] tile)
            {
                try
                {
                    tiles mbtile = new tiles
                    {
                        zoom_level = index.Level,
                        tile_column = index.Col,
                        tile_row = OSMtoTMS(index.Level, index.Row),
                        tile_data = tile,
                        createDate = DateTime.UtcNow,
                        reference = string.Empty, //Blank when browsing
                    };

                    AddTile(mbtile);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, $"TileCache - Add()");
                }
            }

            public static void AddTile(tiles mbtile)
            {
                lock (sqlConn)
                {
                    tiles oldTile = sqlConn.Table<tiles>().Where(x => x.zoom_level == mbtile.zoom_level && x.tile_column == mbtile.tile_column && x.tile_row == mbtile.tile_row).FirstOrDefault();

                    if (oldTile == null)
                    {
                        sqlConn.Insert(mbtile);
                        return;
                    }
                    else
                    {
                        mbtile.id = oldTile.id;
                        mbtile.reference = oldTile.reference;
                        sqlConn.Update(mbtile);
                        return;
                    }
                }
            }

            public void Dispose()
            {
                try
                {
                    if (sqlConn != null)
                    {
                        lock (sqlConn)
                        {
                            sqlConn.Close();
                            sqlConn.Dispose();
                            sqlConn = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, $"TileCache - Dispose()");
                }
            }

            public byte[] Find(TileIndex index)
            {
                try
                {
                    int rowNum = OSMtoTMS(index.Level, index.Row);

                    lock (sqlConn)
                    {
                        tiles oldTile = sqlConn.Table<tiles>().Where(x => x.zoom_level == index.Level && x.tile_column == index.Col && x.tile_row == rowNum).FirstOrDefault();

                        if (oldTile != null)
                        {
                            if ((oldTile.reference == null || oldTile.reference == string.Empty) && (DateTime.UtcNow - oldTile.createDate).TotalDays >= Fragment_Preferences.OfflineMaxAge)
                            {
                                return null;
                            }
                            else
                            {
                                return oldTile.tile_data;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, $"TileCache - Find()");
                }

                return null;
            }

            public void Remove(TileIndex index)
            {
                /**/
                //Todo
            }

            public static SQLiteConnection GetConnection()
            {
                return sqlConn;
            }
        }

        public static SQLiteConnection? InitializeTileCache(string? filename, string? format)
        {
            try
            {
                //https://github.com/mapbox/mbtiles-spec/blob/master/1.3/spec.md
                var sqlConn = new SQLiteConnection(filename, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex, true);
                sqlConn.CreateTable<metadata>();
                sqlConn.CreateTable<tiles>();
                sqlConn.CreateIndex("tile_index", "tiles", new string[] { "zoom_level", "tile_column", "tile_row" }, true);

                var metaList = new List<metadata>
                {
                    //MUST 
                    new metadata { name = "name", value = Platform.CurrentActivity.Resources.GetString(Resource.String.app_name) },
                    new metadata { name = "format", value = format },
                    //SHOULD
                    new metadata { name = "bounds", value = "-180.0,-90.0,180.0,90.0" },                 //Whole world
                    new metadata { name = "center", value = "0,0," + Fragment_Preferences.MinZoom.ToString() }, //Center of world
                    new metadata { name = "minzoom", value = Fragment_Preferences.MinZoom.ToString() },
                    new metadata { name = "maxzoom", value = Fragment_Preferences.MaxZoom.ToString() },
                    //MAY
                    new metadata { name = "attribution", value = "(c) OpenStreetMap contributors https://www.openstreetmap.org/copyright" },
                    new metadata { name = "description", value = "Offline database for " + Platform.CurrentActivity.Resources.GetString(Resource.String.app_name) },
                    new metadata { name = "type", value = "baselayer" },
                    new metadata { name = "version", value = "1" }
                };

                foreach (var meta in metaList)
                {
                    sqlConn.InsertOrReplace(meta);
                }

                //Update Pragma(s)
                sqlConn.Execute("PRAGMA application_id=0x4d504258;");

                return sqlConn;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"TileCache - MBTileCache()");
            }

            return null;
        }
    }
}
