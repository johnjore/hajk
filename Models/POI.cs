using SQLite;

namespace hajk.Models
{
    public class GPXDataPOI
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Symbol { get; set; }
        public decimal Lat { get; set; }
        public decimal Lon { get; set; }
    }
}
