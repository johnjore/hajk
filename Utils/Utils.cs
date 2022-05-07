using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mapsui.Layers;
using Android.Graphics;
using Android.Util;
using AndroidX.AppCompat.App;
using Xamarin.Essentials;
using hajk;

namespace Utils
{
    public class Misc
    {
        public static void ExtractInitialMap(Android.App.Activity activity, string dbFile)
        {
            try
            {
                Serilog.Log.Verbose($"Checking if '{dbFile}' exists");
                // Only if file does not exist
                if (!File.Exists(dbFile))
                {
                    Serilog.Log.Verbose($"Extracting embedded world map");
                    using (FileStream writeStream = new FileStream(dbFile, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        activity.Assets.Open(PrefsActivity.CacheDB).CopyTo(writeStream);
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Utils - ExtractInitialMap()");
            }
        }

        public static void BatterySaveModeNotification()
        {
            try
            {
                //Subscribe to events
                Battery.EnergySaverStatusChanged += OnEnergySaverStatusChanged;

                //Check if enabled or not
                OnEnergySaverStatusChanged(null, null);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Utils - BatterySaveModeNotification()");
            }
        }

        private static void OnEnergySaverStatusChanged(object sender, EnergySaverStatusChangedEventArgs e)
        {
            if (Battery.EnergySaverStatus == EnergySaverStatus.Off)
                return;

            using var alert = new AlertDialog.Builder(hajk.MainActivity.mContext);
            alert.SetTitle(hajk.MainActivity.mContext.Resources.GetString(hajk.Resource.String.BatterySaveModeEnabledTitle));
            alert.SetMessage(hajk.MainActivity.mContext.Resources.GetString(hajk.Resource.String.BatterySaveModeEnabledDescription));
            alert.SetNeutralButton(hajk.Resource.String.Ok, (sender, args) => { });
            var dialog = alert.Create();
            dialog.Show();
        }

        public static void LocationPermissionNotification()
        {
            if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(hajk.MainActivity.mContext, Android.Manifest.Permission.AccessBackgroundLocation) != (int) Android.Content.PM.Permission.Granted)
            {
                hajk.MainActivity.mContext.RequestPermissions( new string[] { Android.Manifest.Permission.AccessBackgroundLocation }, 0);
            }

            if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(hajk.MainActivity.mContext, Android.Manifest.Permission.AccessBackgroundLocation) != (int)Android.Content.PM.Permission.Granted)
            {
                using var alert = new Android.App.AlertDialog.Builder(hajk.MainActivity.mContext);
                alert.SetTitle(hajk.MainActivity.mContext.Resources.GetString(hajk.Resource.String.LocationPermissionTitle));
                alert.SetMessage(hajk.MainActivity.mContext.Resources.GetString(hajk.Resource.String.LocationPermissionDescription));
                alert.SetNeutralButton(hajk.Resource.String.Ok, (sender, args) => { });
                var dialog = alert.Create();
                dialog.SetCancelable(false);
                dialog.Show();
            }
        }

        public static string ConvertBitMapToString(Bitmap bitmap)
        {
            try
            {
                byte[] bitmapData;
                using (MemoryStream stream = new MemoryStream())
                {
                    bitmap.Compress(Bitmap.CompressFormat.Jpeg, 50, stream);
                    bitmapData = stream.ToArray();
                }

                string ImageBase64 = Convert.ToBase64String(bitmapData);

                return ImageBase64;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Utils - ConverBitMapToString()");
                return null;
            }
        }

        public static Bitmap ConvertStringToBitmap(string mystr)
        {
            if (mystr == null)
            {
                return null;
            }

            if (mystr == string.Empty)
            {
                return null;
            }

            try
            {
                byte[] decodedString = Base64.Decode(mystr, Base64Flags.Default);
                Bitmap decodedByte = BitmapFactory.DecodeByteArray(decodedString, 0, decodedString.Length);
                return decodedByte;
            }
            catch
            {
                return null;
            }
        }

        public static int GetBitmapIdForEmbeddedResource(string imagePath)
        {
            try
            {
                var assembly = typeof(hajk.MainActivity).GetTypeInfo().Assembly;
                var image = assembly.GetManifestResourceStream(imagePath);
                var bitmapId = Mapsui.Styles.BitmapRegistry.Instance.Register(image);
                return bitmapId;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Utils - AcessOSMLayerDirect()");
                return 0;
            }
        }

        public static Mapsui.Geometries.Point CalculateCenter(double BoundsRight, double BoundsTop, double BoundsLeft, double BoundsBottom)
        {
            double lat = (BoundsLeft + BoundsRight) / 2;
            double lng = (BoundsBottom + BoundsTop) / 2;

            Mapsui.Geometries.Point p = new Mapsui.Geometries.Point()
            {
                X = lng,
                Y = lat
            };

            return p;
        }

        public static Mapsui.Geometries.Point CalculateQuarter(double BoundsRight, double BoundsTop, double BoundsLeft, double BoundsBottom)
        {
            double lat = (((BoundsLeft + BoundsRight) / 2) + BoundsRight) / 2;
            double lng = (((BoundsBottom + BoundsTop) / 2) + BoundsTop) / 2;

            Mapsui.Geometries.Point p = new Mapsui.Geometries.Point()
            {
                X = lng,
                Y = lat
            };

            return p;
        }

        public static bool ClearTrackRoutesFromMap()
        {
            //Serilog.Log.Information($"Clear gpx entries from map");

            try
            {
                //Remove recorded waypoints
                hajk.RecordTrack.trackGpx.Waypoints.Clear();

                IEnumerable<ILayer> layers = hajk.Fragments.Fragment_map.map.Layers.Where(x => (string)x.Tag == "route" || (string)x.Tag == "track" || (string)x.Tag == "tracklayer");
                foreach (ILayer rt in layers)
                {
                    hajk.Fragments.Fragment_map.map.Layers.Remove(rt);
                }

                return true;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Utils - ClearTrackRoutesFromMap()");
                return false;
            }
        }

        public static void PromptToConfirmExit()
        {
            using (var alert = new AlertDialog.Builder(hajk.MainActivity.mContext))
            {
                alert.SetTitle(hajk.MainActivity.mContext.Resources.GetString(hajk.Resource.String.ExitTitle));
                alert.SetMessage(hajk.MainActivity.mContext.Resources.GetString(hajk.Resource.String.ExitPrompt));
                alert.SetPositiveButton(hajk.Resource.String.Yes, (sender, args) => { hajk.MainActivity.mContext.FinishAffinity(); });
                alert.SetNegativeButton(hajk.Resource.String.No, (sender, args) => { });

                var dialog = alert.Create();
                dialog.Show();
            }
        }

        public static string KMvsM(double value)
        {
            if (value > 1000)
            {
                value /= 1000;
                return value.ToString("N2") + "km";
            }

            return value.ToString("N0") + "m";
        }
    }
}
