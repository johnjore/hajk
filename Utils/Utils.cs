using Android.Graphics;
using Android.Util;
using System;
using System.IO;

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
    }
}
