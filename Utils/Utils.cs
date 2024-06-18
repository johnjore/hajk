using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Android.App;
using Android.Graphics;
using Android.Util;
using Xamarin.Essentials;
using Mapsui.Layers;
using hajk;

namespace Utils
{
    public class Misc
    {
        public static void ExtractInitialMap(Activity activity, string dbFile)
        {
            try
            {
                Serilog.Log.Verbose($"Checking if '{dbFile}' exists");

                if (File.Exists(dbFile) == false)
                {
                    Serilog.Log.Verbose($"Extracting embedded world map");
                    using (var writeStream = new FileStream(dbFile, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        activity?.Assets?.Open(PrefsActivity.CacheDB).CopyTo(writeStream);
                    }
                }
                else
                {
                    Serilog.Log.Verbose($"Embedded world map already exists");
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

        private static void OnEnergySaverStatusChanged(object? sender, EnergySaverStatusChangedEventArgs? e)
        {
            if (Battery.EnergySaverStatus == EnergySaverStatus.Off)
                return;

            using var alert = new AlertDialog.Builder(MainActivity.mContext);
            alert.SetTitle(MainActivity.mContext?.Resources?.GetString(hajk.Resource.String.BatterySaveModeEnabledTitle));
            alert.SetMessage(MainActivity.mContext?.Resources?.GetString(hajk.Resource.String.BatterySaveModeEnabledDescription));
            alert.SetNeutralButton(hajk.Resource.String.Ok, (sender, args) => { });
            var dialog = alert.Create();
            dialog?.Show();
        }

        public static void LocationPermissionNotification()
        {
            //Not required
            /*if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(MainActivity.mContext, Android.Manifest.Permission.AccessBackgroundLocation) != (int) Android.Content.PM.Permission.Granted)
            {
                MainActivity.mContext.RequestPermissions( new string[] { Android.Manifest.Permission.AccessBackgroundLocation }, 0);
            }*/
            if (MainActivity.mContext == null)
                return;

            if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(MainActivity.mContext, Android.Manifest.Permission.AccessBackgroundLocation) != (int)Android.Content.PM.Permission.Granted)
            {
                using var alert = new AlertDialog.Builder(MainActivity.mContext);
                alert.SetTitle(MainActivity.mContext.Resources?.GetString(hajk.Resource.String.LocationPermissionTitle));
                alert.SetMessage(MainActivity.mContext.Resources?.GetString(hajk.Resource.String.LocationPermissionDescription));
                alert.SetNeutralButton(hajk.Resource.String.Ok, (sender, args) => { });
                var dialog = alert.Create();
                dialog?.SetCancelable(false);
                dialog?.Show();
            }
        }

        public static string? ConvertBitMapToString(Bitmap bitmap)
        {
            try
            {
                byte[] bitmapData;
                using (var stream = new MemoryStream())
                {
                    bitmap.Compress(Bitmap.CompressFormat.Jpeg, 50, stream);
                    bitmapData = stream.ToArray();
                }

                return Convert.ToBase64String(bitmapData);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Utils - ConverBitMapToString()");
            }

            return null;
        }

        public static Bitmap? ConvertStringToBitmap(string mystr)
        {
            if (mystr == null || mystr == string.Empty)
            {
                return null;
            }

            try
            {
                byte[]? decodedString = Base64.Decode(mystr, Base64Flags.Default);

                if (decodedString != null)
                {
                    Bitmap? decodedByte = BitmapFactory.DecodeByteArray(decodedString, 0, decodedString.Length);
                    return decodedByte;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "ConvertStringToBitmap");
            }

            return null;
        }

        public static int GetBitmapIdForEmbeddedResource(string imagePath)
        {
            try
            {
                var assembly = typeof(MainActivity).GetTypeInfo().Assembly;
                var image = assembly.GetManifestResourceStream(imagePath);

                if (image == null)
                {
                    Serilog.Log.Error($"Utils - GetBitmapIdForEmbeddedResource() is null for {imagePath}");
                    return 0;
                }

                var bitmapId = Mapsui.Styles.BitmapRegistry.Instance.Register(image);
                return bitmapId;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Utils - GetBitmapIdForEmbeddedResource()");
                return 0;
            }
        }

        public static Mapsui.MPoint CalculateCenter(double BoundsRight, double BoundsTop, double BoundsLeft, double BoundsBottom)
        {
            return new Mapsui.MPoint()
            {
                X = (BoundsBottom + BoundsTop) / 2,
                Y = (BoundsLeft + BoundsRight) / 2,
            };
        }

        public static Mapsui.MPoint CalculateQuarter(double BoundsRight, double BoundsTop, double BoundsLeft, double BoundsBottom)
        {
            return new Mapsui.MPoint()
            {
                X = (((BoundsBottom + BoundsTop) / 2) + BoundsTop) / 2,
                Y = (((BoundsLeft + BoundsRight) / 2) + BoundsRight) / 2,
            };
        }

        public static bool ClearTrackRoutesFromMap()
        {
            //Serilog.Log.Information($"Clear gpx entries from map");

            try
            {
                //Remove recorded waypoints
                RecordTrack.trackGpx.Waypoints.Clear();

                IEnumerable<ILayer> layers = hajk.Fragments.Fragment_map.map.Layers.Where(x => (string?)x.Tag == "route" || (string?)x.Tag == "track" || (string?)x.Tag == "tracklayer");
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
            if (MainActivity.mContext == null || MainActivity.mContext.Resources == null)
            {
                Serilog.Log.Error($"Utils - PromptToConfirmExit()");
                return;
            }

            using (var alert = new AlertDialog.Builder(MainActivity.mContext))
            {
                alert.SetTitle(MainActivity.mContext.Resources.GetString(hajk.Resource.String.ExitTitle));
                alert.SetMessage(MainActivity.mContext.Resources.GetString(hajk.Resource.String.ExitPrompt));
                alert.SetPositiveButton(hajk.Resource.String.Yes, (sender, args) => { MainActivity.mContext.FinishAffinity(); });
                alert.SetNegativeButton(hajk.Resource.String.No, (sender, args) => { });

                var dialog = alert.Create();
                dialog?.Show();
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
