using BitMiracle.LibTiff.Classic;

namespace GeoTiffCOG.Struture
{
    internal class TraceTiffErrorHandler : TiffErrorHandler
    {
        public override void ErrorHandler(Tiff tif, string method, string format, params object[] args)
        {
            //Trace.TraceError(method + " " + string.Format(format, args));
        }
        public override void ErrorHandlerExt(Tiff tif, object clientData, string method, string format, params object[] args)
        {

            //Debug.WriteLine(method + " " + string.Format(format, args));
        }
        public override void WarningHandler(Tiff tif, string method, string format, params object[] args)
        {

            //Debug.WriteLine(method + " " + string.Format(format, args));
        }
        public override void WarningHandlerExt(Tiff tif, object clientData, string method, string format, params object[] args)
        {

            // Debug.WriteLine(method + " " + string.Format(format, args));
        }
    }
}
