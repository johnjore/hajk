using hajk.Models;
using SharpGPX;
using SharpGPX.GPX1_1;


namespace hajk
{
    public class Export
    {
        public static void ExportPOI(string name)
        {
            List<GPXDataPOI> wpt = Data.POIDatabase.GetPOIAsync(name).Result;

            var gpx = new GpxClass()
            {
                Metadata = new metadataType()
                {
                    author = new personType("walkabout"),
                    name = name,
                },
            };

            foreach (GPXDataPOI p in wpt)
            {
                var wptType = new wptType()
                {
                    lat = p.Lat,
                    lon = p.Lon,
                    sym = "Man Overboard",
                };

                gpx.AddWaypoint(wptType);
            }

            GpxClass.XmlWriterSettings.Encoding = System.Text.Encoding.UTF8;
            string? DownLoadFolder = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
            string backupFileName = DownLoadFolder + "/POI-" + name + "-" + (DateTime.Now).ToString("yyMMdd-HHmmss") + ".gpx";
            gpx.ToFile(backupFileName);
        }
    }
}
