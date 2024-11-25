using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System;
using Android.Content;
using Android.Widget;
using Android.App;
using Android.OS;
using System;

namespace hajk
{
    internal class Backup
    {
        public static void ShowBackupDialog()
        {
            //Size of live data
            long RouteDBMB = (new FileInfo(Fragment_Preferences.LiveData + "/" + Fragment_Preferences.RouteDB).Length) / 1024 / 1024;
            long POIDBKB = (new FileInfo(Fragment_Preferences.LiveData + "/" + Fragment_Preferences.POIDB).Length) / 1024;
            long MapSizeMB = (new FileInfo(Fragment_Preferences.LiveData + "/" + Fragment_Preferences.CacheDB).Length) / 1024 / 1024;
            long GeoTiffMB = Utils.Misc.DirectorySizeBytes(new DirectoryInfo(Fragment_Preferences.LiveData + "/" + Fragment_Preferences.GeoTiffFolder)) / 1024 / 1024;

            //Warning: Do NOT Change order, unless checkedItems is also updated!
            string[] items = [
                "Preferences",
                "Route/Track Data (" + RouteDBMB.ToString() + " MB)",
                "POI Data (" + POIDBKB.ToString() + " KB)",
                "Map Tiles (" + MapSizeMB.ToString() + " MB)",
                "Elevation Data (" + GeoTiffMB.ToString() + " MB)",
            ];

            bool[] checkedItems = [
                Preferences.Get("BackupPreferences", true),
                Preferences.Get("BackupRoute&TrackData", true),
                Preferences.Get("BackupPOIData", true),
                Preferences.Get("BackupMapTiles", true), 
                Preferences.Get("BackupElevationData", true),                
            ];

            //Create dialogbox
            AlertDialog.Builder builder = new AlertDialog.Builder(Platform.CurrentActivity);
            builder.SetTitle(Resource.String.BackupOptions);

            //Update GUI
            builder.SetMultiChoiceItems(items, checkedItems, (senderDialog, args) =>
            {
                checkedItems[args.Which] = args.IsChecked;
            });

            //Create the backup
            builder.SetPositiveButton(Resource.String.Ok, (senderDialog, args) =>
            { 
                //Update preferences
                Preferences.Set("BackupPreferences", checkedItems[0]);
                Preferences.Set("BackupRoute&TrackData", checkedItems[1]);
                Preferences.Set("BackupPOIData", checkedItems[2]);
                Preferences.Set("BackupMapTiles", checkedItems[3]);
                Preferences.Set("BackupElevationData", checkedItems[4]);

                //Take Backups
                for (int i = 0; i < checkedItems.Length; i++) 
                {
                    Serilog.Log.Information(i.ToString() + "CheckedItem:" + checkedItems[i]);
                    

                }
            });

            builder.SetNegativeButton(Resource.String.Cancel, (senderDialog, args) =>
            {
            });

            builder.Create();
            builder.Show();
        }
    }
}
