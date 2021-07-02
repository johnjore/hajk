using System;
using System.Collections.Generic;
using System.IO;
using SQLite;
using hajk.Models;
using System.Threading.Tasks;
using Xamarin.Essentials;

//https://docs.microsoft.com/en-us/xamarin/get-started/quickstarts/database?pivots=windows

namespace hajk.Data
{
    public class RouteDatabase
    {
        readonly static SQLiteAsyncConnection database;

        static RouteDatabase()
        {
            string dbPath = Path.Combine(MainActivity.rootPath, Preferences.Get("RouteDB", PrefsActivity.RouteDB));
            database = new SQLiteAsyncConnection(dbPath);
            database.CreateTableAsync<GPXDataRouteTrack>().Wait();
        }

        public static Task<List<GPXDataRouteTrack>> GetRouteAsync()
        {
            //Get all routes
            return database.Table<GPXDataRouteTrack>().ToListAsync();
        }

        public static Task<GPXDataRouteTrack> GetRouteAsync(int id)
        {
            // Get a specific route
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
