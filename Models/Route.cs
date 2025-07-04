using SQLite;

namespace hajk.Models
{
    public class GPXDataRouteTrack
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }                             //Autoincremented record ID number
        public GPXType GPXType{ get; set; }                     //0 if Route, 1 if Track
        public string? Name { get; set; }                       //Name of route / track
        public float Distance { get; set; }                     //Calculated distance / length
        public int Ascent { get; set; }                         //Calculated ascent
        public int Descent { get; set; }                        //Calculated descent
        public string? Description { get; set; }                //Description of route / track
        public string? GPX { get; set; }                        //Full GPX XML file
        public string? ImageBase64String { get; set; }          //Thumbnail of route on map
        public string? NaismithTravelTime { get; set; }         //Needed for sorting option
        public float ShenandoahsScale { get; set; }             //Needed for sorting option
        public string? GPXStartLocation { get; set; }           //Route or Track's start location - Calculated from GPX
    }

    public enum GPXType : uint
    {
        Route = 0,
        Track = 1
    }

    public enum SortOrder: uint
    {
        Ascending = 0,
        Descending = 1
    }
}
