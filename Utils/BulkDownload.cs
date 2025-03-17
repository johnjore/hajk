using Android.App;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hajk.Utilities
{
    internal class BulkDownload
    {
        public static void RunBulkDownload()
        {
            bool[] checkedItems = [
                Preferences.Get("BulkDownloadRoutes", true),
                Preferences.Get("BulkDownloadTracks", true),
                Preferences.Get("BulkDownloadPOI", true),
            ];

            string[] items = [
                "Routes",
                "Tracks",
                "POIs",
            ];

            //Create dialogbox
            AlertDialog.Builder builder = new(Platform.CurrentActivity);
            builder.SetTitle(Resource.String.BulkDownloadOptions);

            //Update GUI
            builder.SetMultiChoiceItems(items, checkedItems, (senderDialog, args) =>
            {
                checkedItems[args.Which] = args.IsChecked;
            });

            //Start the Downloads
            builder.SetPositiveButton(Resource.String.Ok, (senderDialog, args) =>
            {
                //Update/save preferences
                Preferences.Set("BulkDownloadRoutes", checkedItems[0]);
                Preferences.Set("BulkDownloadTracks", checkedItems[1]);
                Preferences.Set("BulkDownloadPOI", checkedItems[2]);

                if (Preferences.Get("BulkDownloadRoutes", true) == false &&
                    Preferences.Get("BulkDownloadTracks&TrackData", true) == false &&
                    Preferences.Get("BulkDownloadPOI", true) == false)
                {
                    Serilog.Log.Warning("Nothing selected to download");
                    Toast.MakeText(Platform.AppContext, "Nothing to download", ToastLength.Long)?.Show();
                    return;
                }

                Task.Run(() =>
                {
                    //PerformBackups(ProgressBarIncrement);
                });
            });

            builder.SetNegativeButton(Resource.String.Cancel, (senderDialog, args) =>
            {
                //Update/save preferences
                Preferences.Set("BulkDownloadRoutes", checkedItems[0]);
                Preferences.Set("BulkDownloadTracks", checkedItems[1]);
                Preferences.Set("BulkDownloadPOI", checkedItems[2]);
            });

            builder.Create();
            builder.Show();
        }
    }
}
