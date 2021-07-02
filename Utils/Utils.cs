using Android.Graphics;
using Android.Util;
using System;
using System.IO;
using System.Reflection;

namespace Utils
{
    public class Misc
    {
        public static string ConvertBitMapToString(Bitmap bitmap)
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

        public static Bitmap ConvertStringToBitmap(string mystr)
        {
            byte[] decodedString = Base64.Decode(mystr, Base64Flags.Default);
            Bitmap decodedByte = BitmapFactory.DecodeByteArray(decodedString, 0, decodedString.Length);

            return decodedByte;
        }

        public static int GetBitmapIdForEmbeddedResource(string imagePath)
        {
            var assembly = typeof(hajk.MainActivity).GetTypeInfo().Assembly;
            var image = assembly.GetManifestResourceStream(imagePath);
            var bitmapId = Mapsui.Styles.BitmapRegistry.Instance.Register(image);
            return bitmapId;
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
    }
}
