namespace GeoTiffCOG.Struture
{
    public enum DEMFileRegistrationMode
    {
        /// <summary>
        /// Grid/node-registered: cells are centered on lines of latitude and longitude (usually there is one pixel overlap for each tile).
        /// </summary>
        Grid,
        /// <summary>
        /// Cell/pixel-registered: cell edges are along lines of latitude and longitude.
        /// </summary>
        Cell,
    }
}
