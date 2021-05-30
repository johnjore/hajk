using SQLite;

namespace hajk.Models
{
    public class metadata
    {
        public string name { get; set; }
        public string value { get; set; }
    }

    public class tiles
    {
        [PrimaryKey, AutoIncrement]
        public int id { get; set; }
        public int zoom_level { get; set; }
        public int tile_column { get; set; }
        public int tile_row { get; set; }
        public byte[] tile_data { get; set; }
    }

    public class metadataValues
    {
        public string name { get; set; }
        public string description { get; set; }
        public string version { get; set; }
        public string minzoom { get; set; }
        public string maxzoom { get; set; }
        public string center { get; set; }
        public string bounds { get; set; }
        public string type { get; set; }
        public string format { get; set; }
    }
}
