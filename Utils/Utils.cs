﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Android.App;
using Android.Graphics;
using Android.Util;
using Mapsui.Layers;
using hajk;
using Microsoft.Maui.ApplicationModel;

namespace Utils
{
    public class Misc
    {
        //Clear out a folder, no confirmation
        public static void EmptyFolder(string directoryName)
        {
            try
            {
                if (directoryName == null || directoryName.Length == 0)
                {
                    Serilog.Log.Warning($"No directory name provided: '{directoryName}'");
                    return;
                }

                System.IO.DirectoryInfo di = new (directoryName);

                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }

                foreach (DirectoryInfo dir in di.GetDirectories())
                {
                    dir.Delete(true);
                }

                Directory.Delete(directoryName);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Failed to empty folder '{directoryName}'");
            }
        }


        //Calculate and return size of folder in bytes
        public static long DirectorySizeBytes(DirectoryInfo d)
        {
            long size = 0;
            // Add file sizes.
            FileInfo[] fis = d.GetFiles();
            foreach (FileInfo fi in fis)
            {
                size += fi.Length;
            }

            // Add subdirectory sizes.
            DirectoryInfo[] dis = d.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += DirectorySizeBytes(di);
            }
            return size;
        }

        public static void ExtractInitialMap(Activity activity, string dbFile)
        {
            try
            {
                Serilog.Log.Debug($"Checking if '{dbFile}' exists");

                if (File.Exists(dbFile) == false)
                {
                    Serilog.Log.Debug($"Extracting embedded world map");
                    using (var writeStream = new FileStream(dbFile, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        activity?.Assets?.Open("OpenStreetMap.mbtiles").CopyTo(writeStream);
                    }
                }
                else
                {
                    Serilog.Log.Debug($"Embedded world map already exists");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Utils - ExtractInitialMap()");
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
                Serilog.Log.Fatal(ex, $"Utils - ConverBitMapToString()");
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
                Serilog.Log.Fatal(ex, "ConvertStringToBitmap");
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
                    Serilog.Log.Fatal($"Utils - GetBitmapIdForEmbeddedResource() is null for {imagePath}");
                    return 0;
                }

                var bitmapId = Mapsui.Styles.BitmapRegistry.Instance.Register(image);
                return bitmapId;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Utils - GetBitmapIdForEmbeddedResource()");
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

        public static Mapsui.MPoint CalculateNofM(GPXUtils.Position p1, GPXUtils.Position p2, float p1p2_Ddistance, double interval)
        {
            double fraction = interval / p1p2_Ddistance;

            return new Mapsui.MPoint()
            {
                X = p1.Latitude  + (fraction * (p2.Latitude  - p1.Latitude )),
                Y = p1.Longitude + (fraction * (p2.Longitude - p1.Longitude))
            };
        }

        public static bool ClearTrackRoutesFromMap()
        {
            //Serilog.Log.Information($"Clear gpx entries from map");

            try
            {
                //Remove recorded waypoints
                /**///Bug?: Why delete the recording itself?
                //RecordTrack.trackGpx.Waypoints.Clear();

                IEnumerable<ILayer> layers = hajk.Fragments.Fragment_map.map.Layers.Where(x => (string?)x.Tag == Fragment_Preferences.Layer_Route || (string?)x.Tag == Fragment_Preferences.Layer_Track);
                foreach (ILayer rt in layers)
                {
                    hajk.Fragments.Fragment_map.map.Layers.Remove(rt);
                }

                return true;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Utils - ClearTrackRoutesFromMap()");
                return false;
            }
        }

        public static void PromptToConfirmExit()
        {
            if (Platform.CurrentActivity == null || Platform.CurrentActivity.Resources == null)
            {
                Serilog.Log.Error($"Utils - PromptToConfirmExit()");
                return;
            }

            
            using (var alert = new AlertDialog.Builder(Platform.CurrentActivity))
            {
                alert.SetTitle(Platform.CurrentActivity?.Resources?.GetString(hajk.Resource.String.ExitTitle));
                alert.SetMessage(Platform.CurrentActivity?.Resources?.GetString(hajk.Resource.String.ExitPrompt));
                alert.SetPositiveButton(hajk.Resource.String.Yes, (sender, args) => 
                {
                    Task.Run(async () =>
                    {
                        if ((Preferences.Get("RecordingTrack", false) == true))
                        {
                            await RecordTrack.EndTrackTimer();
                        }

                        Platform.CurrentActivity?.FinishAffinity();
                    });
                });
                alert.SetNegativeButton(hajk.Resource.String.No, (sender, args) => 
                {
                });

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
