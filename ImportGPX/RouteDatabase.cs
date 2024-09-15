using System;
using System.Collections.Generic;
using System.IO;
using SQLite;
using hajk.Models;
using Microsoft.Maui.Storage;

//https://docs.microsoft.com/en-us/xamarin/get-started/quickstarts/database?pivots=windows

namespace hajk.Data
{
    public class RouteDatabase
    {
        readonly static SQLiteAsyncConnection database;

        static RouteDatabase()
        {
            try
            {
                string dbPath = Path.Combine(Fragment_Preferences.rootPath, Preferences.Get("RouteDB", Fragment_Preferences.RouteDB));
                database = new SQLiteAsyncConnection(dbPath);
                database.CreateTableAsync<GPXDataRouteTrack>().Wait();
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"RouteDatabase - RouteDatabase()");
            }
        }

        public static Task<List<GPXDataRouteTrack>> GetRoutesAsync()
        {
            //Get all routes
            return database.Table<GPXDataRouteTrack>().Where(i => i.GPXType == GPXType.Route).ToListAsync();
        }

        public static Task<List<GPXDataRouteTrack>> GetTracksAsync()
        {
            //Get all tracks
            return database.Table<GPXDataRouteTrack>().Where(i => i.GPXType == GPXType.Track).ToListAsync();
        }

        public static Task<GPXDataRouteTrack> GetRouteAsync(int id)
        {
            // Get a specific route or track
            return database.Table<GPXDataRouteTrack>().Where(i => i.Id == id).FirstOrDefaultAsync();
        }

        public static Task<int> SaveRouteAsync(GPXDataRouteTrack route)
        {
            if (route.Id != 0)
            {
                // Update an existing route
                return database.UpdateAsync(route);
            }
            else
            {
                // Save a new route
                return database.InsertAsync(route);
            }
        }

        public static void SaveRoute(GPXDataRouteTrack route)
        {
            if (route.Id != 0)
            {
                // Update an existing route
                database.UpdateAsync(route).Wait();
            }
            else
            {
                // Save a new route
                database.InsertAsync(route).Wait();
            }
        }

        public static Task<int> DeleteRouteAsync(GPXDataRouteTrack route)
        {
            // Delete a route
            return database.DeleteAsync(route);
        }

        public static Task<int> DeleteRouteAsync(int id)
        {
            // Delete a route
            return database.DeleteAsync(database.Table<GPXDataRouteTrack>().Where(i => i.Id == id).FirstAsync().Result);
        }
    }
}
