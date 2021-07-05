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

        public GpxData()
        {
            List<GPXDataRouteTrack> result = RouteDatabase.GetRouteAsync().Result;
            gpx = result;
        }

        public int NumGpx
        {
            get { return gpx.Count; }
        }

        public GPXDataRouteTrack this[int i]
        {
            get { return gpx[i]; }
        }

        internal void RemoveAt(int adapterPosition)
        {
            gpx.RemoveAt(adapterPosition);
        }

        internal int Add(GPXDataRouteTrack route)
        {
            gpx.Add(route);
            return gpx.Count;
        }
    }

    public class GPXViewHolder : RecyclerView.ViewHolder
    {
        public int Id;
        public GPXType GPXType;
        public ImageView GPXTypeLogo;
        public TextView Name { get; set; }
        public TextView Distance { get; set; }
        public TextView img_more;
        public ImageView Image { get; set; }
        
        public GPXViewHolder(View itemview, Action<int> listener) : base(itemview)
        {
            GPXTypeLogo = itemview.FindViewById<ImageView>(Resource.Id.GPXTypeLogo);
            Name = itemview.FindViewById<TextView>(Resource.Id.Name);
            Distance = itemview.FindViewById<TextView>(Resource.Id.Distance);
            img_more = itemview.FindViewById<TextView>(Resource.Id.textViewOptions);

            /**///Not yet implemented
            Image = itemview.FindViewById<ImageView>(Resource.Id.imageView);
            //itemview.Click += (sender, e) => listener(Position);
        }
    }
}
