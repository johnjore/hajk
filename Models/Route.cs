using SQLite;

namespace hajk.Models
{
    public class GPXDataRouteTrack
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public GPXType GPXType{ get; set; }
        public string? Name { get; set; }
        public float Distance { get; set; }
        public int Ascent { get; set; }
        public int Descent { get; set; }
        public string? Description { get; set; }
        public string? GPX { get; set; }
        public string? ImageBase64String { get; set; }
        public string? NaismithTravelTime { get; set; }         //Added June 30 2025 - GPXAdapter is slow scrolling due to excess calculations
        public float ShenandoahsScale { get; set; }             //Added June 30 2025 - GPXAdapter is slow scrolling due to excess calculations
    }

    public enum GPXType : uint
    {
        Route = 0,
        Track = 1
    }
}
