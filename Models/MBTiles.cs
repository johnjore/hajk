using System;
using System.Collections.Generic;
using SQLite;

namespace hajk.Models
{
    public class metadata
    {
        [PrimaryKey]
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
        public DateTime createDate { get; set; }
        public string reference { get; set; }
    }

    public class metadataValues
    {
        //MUST
        public string name { get; set; }
        public string format { get; set; }
        //SHOULD
        public string bounds { get; set; }
        public string center { get; set; }
        public int minzoom { get; set; }
        public string maxzoom { get; set; }
        //MAY
        public string attribution { get; set; }
        public string description { get; set; }
        public string type { get; set; }
        public int version { get; set; }
    }
}
