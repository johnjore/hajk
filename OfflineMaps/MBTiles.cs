using System;
using System.IO;
using SQLite;
using hajk.Models;
using Serilog;

//This is a rewrite of https://github.com/bertt/MBTiles

namespace hajk
{
    public class MBTilesWriter
    {
        public static SQLiteConnection CreateDatabaseConnection(string db)
        {
            if (!File.Exists(db))
            {
                //Create
                try
                {
                    metadataValues metadata = new metadataValues
                    {
                        name = "OfflineDB",
                        description = "Created by hajk",
                        version = "1",
                        format = "png",
                    };

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
                var sqliteConnection = new SQLiteConnection(db, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex, true);
                return sqliteConnection;
            }

            return null;
        }

        public static int WriteTile(SQLiteConnection sqliteConnection, tiles mbtile)
        {
            if (sqliteConnection == null)
            {
                return 0;
            }

            if (mbtile.tile_data == null)
            {
                return 0;
            }

            try
            {
                if (mbtile.id == 0)
                {
                    return sqliteConnection.Insert(mbtile);
                }

                return sqliteConnection.Update(mbtile);
            }
            catch (Exception ex)
            {
                Log.Error($"WriteTile(...) crashed: {ex}");
                return 0;
            }
        }

        private static SQLiteConnection CreateDatabase(string name)
        {
            try
            {
                //Open database
                var sqliteConnection = new SQLiteConnection(name, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex, true);

                //Create tables
                sqliteConnection.CreateTable<metadata>();
                sqliteConnection.CreateTable<tiles>();

                //Create indexes
                string[] a = { "zoom_level", "tile_column", "tile_row" };
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
            try
            {
                conn.Execute($"INSERT INTO metadata (name, value) VALUES ('name', '{metadata.name}');");
                conn.Execute($"INSERT INTO metadata (name, value) VALUES ('description', '{metadata.description}');");
                conn.Execute($"INSERT INTO metadata (name, value) VALUES ('version', '{metadata.version}');");
                conn.Execute($"INSERT INTO metadata (name, value) VALUES ('format', 'pbf');");
            }
            catch (Exception ex)
            {
                Log.Error($"InsertMetadata(...) crashed: {ex}");
            }
        }            
    }
}
