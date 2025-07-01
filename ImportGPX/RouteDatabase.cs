using hajk.Models;
using Microsoft.Maui.Storage;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;

//https://docs.microsoft.com/en-us/xamarin/get-started/quickstarts/database?pivots=windows

namespace hajk.Data
{
    public class RouteDatabase
    {
        readonly static SQLiteAsyncConnection? database;

        static RouteDatabase()
        {
            try
            {
                string dbPath = Path.Combine(Fragment_Preferences.LiveData, Fragment_Preferences.RouteDB);
                database = new SQLiteAsyncConnection(dbPath);
                database.CreateTableAsync<GPXDataRouteTrack>().Wait();
                database.CreateIndexAsync("GPXType_index", "GPXDataRouteTrack", "GPXType").Wait();
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"RouteDatabase - RouteDatabase()");
            }
        }

        public static async Task<bool> ReplaceDBAsync(string dbPath)
        {
            try
            {
                if (database != null)
                {
                    await database.CloseAsync();
                }

                File.Copy(dbPath, Path.Combine(Fragment_Preferences.LiveData, Fragment_Preferences.RouteDB), true);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to replace route and track database file");
                return false;
            }

            return true;
        }

        public static Task<List<GPXDataRouteTrack>>? GetRoutesAsync()
        {
            //Get all routes
            return database.Table<GPXDataRouteTrack>().Where(i => i.GPXType == GPXType.Route).ToListAsync();
        }

        public static Task<List<GPXDataRouteTrack>>? GetSelectedDataAsync(GPXType gpxtype)
        {
            //Get all field, except get GPX or Image (takes too long)
            switch (gpxtype)
            {
                case GPXType.Route:
                    return database?.QueryAsync<GPXDataRouteTrack>("SELECT Id, GPXType, Name, Distance, Ascent, Descent, Description FROM GPXDataRouteTrack WHERE GPXType = 0", GPXType.Route);
                case GPXType.Track:
                    return database?.QueryAsync<GPXDataRouteTrack>("SELECT Id, GPXType, Name, Distance, Ascent, Descent, Description FROM GPXDataRouteTrack WHERE GPXType = 1", GPXType.Track);
                default:
                    return null;
            }
        }

        public static Task<List<GPXDataRouteTrack>>? GetTracksAsync()
        {
            //Get all tracks
            return database?.Table<GPXDataRouteTrack>().Where(i => i.GPXType == GPXType.Track).ToListAsync();
        }

        public static Task<GPXDataRouteTrack>? GetRouteAsync(int id)
        {
            // Get a specific route or track
            return database?.Table<GPXDataRouteTrack>().Where(i => i.Id == id).FirstOrDefaultAsync();
        }

        public static Task<int>? SaveRouteAsync(GPXDataRouteTrack route)
        {
            if (route.Id != 0)
            {
                // Update an existing route
                return database?.UpdateAsync(route);
            }
            else
            {
                // Save a new route
                return database?.InsertAsync(route);
            }
        }

        public static int SaveRoute(GPXDataRouteTrack route)
        {
            if (route.Id != 0)
            {
                // Update an existing route
                database?.UpdateAsync(route).Wait();
            }
            else
            {
                // Save a new route
                database?.InsertAsync(route).Wait();
            }

            return (route.Id);
        }

        public static Task<int>? DeleteRouteAsync(GPXDataRouteTrack route)
        {
            // Delete a route
            return database?.DeleteAsync(route);
        }

        public static Task<int>? DeleteRouteAsync(int id)
        {
            // Delete a route
            return database?.DeleteAsync(database.Table<GPXDataRouteTrack>().Where(i => i.Id == id).FirstAsync().Result);
        }
    }
}
