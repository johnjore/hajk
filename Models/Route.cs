using SQLite;

namespace hajk.Models
{
    public class Route
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; }
        public float Distance { get; set; }
        public int Ascent { get; set; }
        public string Description { get; set; }
        public string GPX { get; set; }
        /**///Create map image with route layer. Auto zoom to level n to see the full route
        public string ImageBase64String { get; set; }
    }
}
