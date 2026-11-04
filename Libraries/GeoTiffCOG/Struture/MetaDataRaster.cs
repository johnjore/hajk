namespace GeoTiffCOG.Struture
{
    public class MetaDataRaster
    {
        public int Width;
        public int Height;
        public int SamplesPerPixel;
        public int BitsPerSample;
        public double TiePointLon;
        public double TiePointLat;
        public double PixelScaleX;
        public double PixelScaleY;
        public IEnumerable<GeoPoint> Coordinates;
        public byte[][] Buffer;
    }
}
