using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using hajk.Models;
using hajk.Data;

namespace hajk
{
    public class GpxData
    {
        private readonly List<GPXDataRouteTrack> gpx;

        //Returns list of all items in list
        public GpxData()
        {
            List<GPXDataRouteTrack> result = RouteDatabase.GetRouteAsync().Result;
            result.Reverse();
            gpx = result;
        }

        //Returns # of items in list
        public int NumGpx
        {
            get { return gpx.Count; }
        }

        // Returns specific item
        public GPXDataRouteTrack this[int i]
        {
            get { return gpx[i]; }
        }

        //Remove route at position adapterPosition
        internal void RemoveAt(int adapterPosition)
        {
            gpx.RemoveAt(adapterPosition);
        }

        //Add route at the beginning of the list
        internal int Insert(GPXDataRouteTrack route)
        {
            gpx.Insert(0, route);
            return gpx.Count;
        }

        //Add route at the end of the list
        internal int Add_(GPXDataRouteTrack route)
        {
            gpx.Add(route);
            return gpx.Count;
        }

    }

    public class GPXViewHolder : RecyclerView.ViewHolder
    {
        public int Id { get; set; }
        public GPXType GPXType { get; set; }
        public ImageView GPXTypeLogo { get; set; }
        public TextView Name { get; set; }
        public TextView Distance { get; set; }
        public TextView Img_more { get; set; }
        public ImageView TrackRouteMap { get; set; }

        public GPXViewHolder(View itemview, Action<int> listener) : base(itemview)
        {
            GPXTypeLogo = itemview.FindViewById<ImageView>(Resource.Id.GPXTypeLogo);
            Name = itemview.FindViewById<TextView>(Resource.Id.Name);
            Distance = itemview.FindViewById<TextView>(Resource.Id.Distance);
            Img_more = itemview.FindViewById<TextView>(Resource.Id.textViewOptions);
            TrackRouteMap = itemview.FindViewById<ImageView>(Resource.Id.TrackRouteMap);

            //itemview.Click += (sender, e) => listener(Position);
        }
    }
}
