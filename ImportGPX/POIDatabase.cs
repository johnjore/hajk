﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using SQLite;
using Serilog;
using hajk.Models;

//https://docs.microsoft.com/en-us/xamarin/get-started/quickstarts/database?pivots=windows

namespace hajk.Data
{
    public class POIDatabase
    {
        readonly static SQLiteAsyncConnection database;

        static POIDatabase()
        {
            try
            {
                string dbPath = Path.Combine(Fragment_Preferences.LiveData, Fragment_Preferences.POIDB);

                //new FileInfo(dbPath).Delete();

                database = new SQLiteAsyncConnection(dbPath);
                database.CreateTableAsync<GPXDataPOI>().Wait();
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"POIDatabase - POIDatabase()");
            }
        }

        //Replace existing database
        public static async Task<bool> ReplaceDBAsync(string dbPath)
        {
            try
            {
                if (database != null)
                {
                    await database.CloseAsync();
                }

                File.Copy(dbPath, Path.Combine(Fragment_Preferences.LiveData, Fragment_Preferences.POIDB), true);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to replace POI database file");
                return false;
            }

            return true;
        }

        //Get all waypoints
        public static Task<List<GPXDataPOI>> GetPOIAsync()
        {
            return database.Table<GPXDataPOI>().ToListAsync();
        }

        //Get specific names (Rogaining)
        public static Task<List<GPXDataPOI>> GetPOIAsync(string Name)
        {
            return database.Table<GPXDataPOI>().Where(i => i.Name == Name).ToListAsync();
        }


        // Get a specific waypoint
        public static Task<GPXDataPOI> GetPOIAsync(long? id)
        {
            if (id != null)
                return database.Table<GPXDataPOI>().Where(i => i.Id == id).FirstOrDefaultAsync();
            else return null;
        }

        //Update or save a new waypoint
        public static Task<int> SavePOIAsync(GPXDataPOI waypoint)
        {
            if (waypoint.Id != 0)
            {
                // Update an existing waypoint
                return database.UpdateAsync(waypoint);
            }
            else
            {
                // Save a new waypoint
                return database.InsertAsync(waypoint);
            }
        }

        //Update or save a new waypoint
        public static int SavePOI(GPXDataPOI waypoint)
        {
            if (waypoint.Id != 0)
            {
                // Update an existing waypoint
                return (database.UpdateAsync(waypoint).Result);
            }
            else
            {
                // Save a new waypoint
                return (database.InsertAsync(waypoint).Result);
            }
        }

        // Delete a waypoint
        public static Task<int> DeleteRouteAsync(GPXDataPOI waypoint)
        {            
            return database.DeleteAsync(waypoint);
        }

        // Delete a waypoint
        public static Task<int> DeletePOIAsync(long? id)
        {
            return database.DeleteAsync(database.Table<GPXDataPOI>().Where(i => i.Id == id).FirstAsync().Result);
        }
    }
}
