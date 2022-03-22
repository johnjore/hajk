using System;
using System.Collections.Generic;
using BruTile;
using BruTile.Cache;
using BruTile.Predefined;
using BruTile.Web;
using SQLite;
using hajk.Models;

//From https://github.com/spaddlewit/MBTilesPersistentCache


namespace hajk
{
    class TileCache
    {
        static MbTileCache mbTileCache;

        public static ITileSource GetOSMBasemap(string cacheFilename)
        {
            try
            {
                if (mbTileCache == null)
                    mbTileCache = new MbTileCache(cacheFilename, "png");

                HttpTileSource src = new HttpTileSource(new GlobalSphericalMercator(PrefsActivity.MinZoom, PrefsActivity.MaxZoom),
                    "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
                    new[] { "a", "b", "c" }, name: "OpenStreetMap",
                    persistentCache: mbTileCache,
                    userAgent: "OpenStreetMap in Mapsui (hajk)",
                    attribution: new Attribution("(c) OpenStreetMap contributors", "https://www.openstreetmap.org/copyright"));

                return src;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"TileCache - GetOSMBasemap()");
                return null;
            }            
        }

        public class MbTileCache : IPersistentCache<byte[]>, IDisposable
        {
            public static List<MbTileCache> openConnections = new List<MbTileCache>();
            SQLiteConnection sqlConn = null;

            public MbTileCache(string filename, string format)
            {
                try
                {
                    sqlConn = new SQLiteConnection(filename);
                    openConnections.Add(this);
                    sqlConn.CreateTable<metadata>();
                    sqlConn.CreateTable<tiles>();

                    var metaList = new List<metadata>
                    {
                        new metadata { name = "name", value = "Offline" },
                        new metadata { name = "type", value = "baselayer" },
                        new metadata { name = "version", value = "1" },
                        new metadata { name = "description", value = "Offline" },
                        new metadata { name = "format", value = format }
                    };

                    foreach (var meta in metaList)
                        sqlConn.InsertOrReplace(meta);

                    double[] originalBounds = new double[4] { double.MaxValue, double.MaxValue, double.MinValue, double.MinValue }; // In WGS1984, the total extent of all bounds
                    sqlConn.InsertOrReplace(new metadata { name = "bounds", value = string.Join(",", originalBounds) });
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, $"TileCache - MBTileCache()");
                }
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
                        tile_row = index.Row,
                        tile_data = tile,
                        createDate = DateTime.UtcNow
                    };

                    mbtile.tile_row = OSMtoTMS(mbtile.zoom_level, mbtile.tile_row);

                    lock (sqlConn)
                    {
                        tiles oldTile = sqlConn.Table<tiles>().Where(x => x.zoom_level == mbtile.zoom_level && x.tile_column == mbtile.tile_column && x.tile_row == mbtile.tile_row).FirstOrDefault();

                        if (oldTile == null)
                        {
                            sqlConn.Insert(mbtile);
                            return;
                        }

                        if ((DateTime.UtcNow - oldTile.createDate).TotalDays >= 30)
                        {
                            mbtile.id = oldTile.id;
                            sqlConn.Update(mbtile);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, $"TileCache - Add()");
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
                    Serilog.Log.Error(ex, $"TileCache - Dispose()");
                }
            }

            public byte[] Find(TileIndex index)
            {
                try
                {
                    int level = index.Level;
                    int rowNum = OSMtoTMS(level, index.Row);

                    lock (sqlConn)
                    {
                        tiles oldTile = sqlConn.Table<tiles>().Where(x => x.zoom_level == level && x.tile_column == index.Col && x.tile_row == rowNum).FirstOrDefault();

                        // You may also want to put a check here to 'age' the tile, i.e., if it is too old, return null so a new one is fetched.
                        if (oldTile != null)
                            return oldTile.tile_data;
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, $"TileCache - Find()");
                }

                return null;
            }

            public void Remove(TileIndex index)
            {
                // We don't remove
            }
        }
    }
}
