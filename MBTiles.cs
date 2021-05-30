using System;
using SQLite;
using Tiles.Tools;
using hajk.Models;
using Serilog;
using System.IO;

//This is a rewrite of https://github.com/bertt/MBTiles

namespace hajk
{
    public class MBTilesWriter
    {
        public static SQLiteConnection CreateDatabase(string db, metadataValues metadata)
        {
            if (!File.Exists(db))
            {
                //Create
                try
                {
                    var sqliteConnection = CreateDatabase(db);
                    InsertMetadata(sqliteConnection, metadata);
                    return sqliteConnection;
                }
                catch (Exception ex)
                {
                    Log.Error($"CreateDatabase(...) crashed: {ex}");
                }
            } 
            else
            {
                //Open database
                var sqliteConnection = new SQLiteConnection(db);
                return sqliteConnection;
            }

            return null;
        }

        public static int WriteTile(SQLiteConnection sqliteConnection, Tile t, byte[] data)
        {
            try
            {
                // mbtiles uses tms format so reverse y-axis...
                int tmsY = (int)Math.Pow(2, t.Z) - 1 - t.Y;
                int rowsAffected = 0;

                //Create tile for DB
                tiles mbtile = new tiles
                {
                    zoom_level = t.Z,
                    tile_column = t.X,
                    tile_row = tmsY,
                    tile_data = data,
                    createDate = DateTime.UtcNow
            };

                tiles oldTile = sqliteConnection.Table<tiles>().Where(x => x.zoom_level == t.Z && x.tile_column == t.X && x.tile_row == tmsY).FirstOrDefault();
                if (oldTile == null)
                {
                    //rowsAffected = sqliteConnection.Execute($"INSERT INTO tiles (zoom_level, tile_column, tile_row, tile_data) VALUES ({t.Z}, {t.X}, {tmsY}, @bytes)", data);
                    rowsAffected = sqliteConnection.Insert(mbtile);
                }
                else
                {
                    mbtile.id = oldTile.id;
                    rowsAffected = sqliteConnection.Update(mbtile);
                }
                
                return rowsAffected;
            }
            catch (Exception ex)
            {
                Log.Error($"WriteTile(...) crashed: {ex}");
            }

            return 0;
        }

        private static SQLiteConnection CreateDatabase(string name)
        {
            try
            {
                //Open database
                var sqliteConnection = new SQLiteConnection(name);

                //Create tables
                sqliteConnection.CreateTable<metadata>();
                sqliteConnection.CreateTable<tiles>();

                //Create indexes
                string[] a = {"zoom_level", "tile_column", "tile_row"};
                sqliteConnection.CreateIndex("tile_index", "tiles", a, true);
                sqliteConnection.CreateIndex("name", "metadata", "name", true);

                return sqliteConnection;
            }
            catch (Exception ex)
            {
                Log.Error($"CreateDatabase(...) crashed: {ex}");
            }

            return null;
        }

        private static void InsertMetadata(SQLiteConnection conn, metadataValues metadata)
        {
            try {
                conn.Execute($"INSERT INTO metadata (name, value) VALUES ('name', '{metadata.name}');");
                conn.Execute($"INSERT INTO metadata (name, value) VALUES ('description', '{metadata.description}');");
                conn.Execute($"INSERT INTO metadata (name, value) VALUES ('bounds', '{metadata.bounds}');");
                conn.Execute($"INSERT INTO metadata (name, value) VALUES ('center', '{metadata.center}');");
                conn.Execute($"INSERT INTO metadata (name, value) VALUES ('minzoom', '{metadata.minzoom}');");
                conn.Execute($"INSERT INTO metadata (name, value) VALUES ('maxzoom', '{metadata.maxzoom}');");
                conn.Execute($"INSERT INTO metadata (name, value) VALUES ('version', '{metadata.version}');");
                conn.Execute($"INSERT INTO metadata (name, value) VALUES ('type', '{metadata.type}');");
                conn.Execute($"INSERT INTO metadata (name, value) VALUES ('format', 'pbf');");
            }
            catch (Exception ex)
            {
                Log.Error($"InsertMetadata(...) crashed: {ex}");
            }
        }
    }
}
