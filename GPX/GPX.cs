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
        private readonly List<Route> gpx;

        public GpxData()
        {
            List<Route> result = RouteDatabase.GetRouteAsync().Result;
            gpx = result;
        }

        public int NumGpx
        {
            get { return gpx.Count; }
        }
                
        public Route this[int i]
        {
            get { return gpx[i]; }
        }

        internal void RemoveAt(int adapterPosition)
        {
            gpx.RemoveAt(adapterPosition);
        }
    }

    public class GPXViewHolder : RecyclerView.ViewHolder
    {
        public ImageView Image { get; set; }
        public TextView Name { get; set; }
        public TextView Distance { get; set; }
        public TextView img_more;
        public int Id;
        public GPXViewHolder(View itemview, Action<int> listener) : base(itemview)
        {
            Image = itemview.FindViewById<ImageView>(Resource.Id.imageView);
            Name = itemview.FindViewById<TextView>(Resource.Id.Name);
            Distance = itemview.FindViewById<TextView>(Resource.Id.Distance);
            img_more = itemview.FindViewById<TextView>(Resource.Id.textViewOptions);
            //itemview.Click += (sender, e) => listener(Position);
        }
    }
}
