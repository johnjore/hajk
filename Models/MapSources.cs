using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace hajk.Models.MapSource
{
    public class MapSource
    {
        public string Name { get; private set; }        
        public string BaseURL { get; private set; }
        public string Token { get; private set; }

        public MapSource(string name, string baseURL, string token)
        {
            Name = name;
            BaseURL = baseURL;
            Token = token;
        }
    }
}
