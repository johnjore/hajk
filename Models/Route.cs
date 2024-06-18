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
    }

    public enum GPXType : uint
    {
        Route = 0,
        Track = 1
    }
}
