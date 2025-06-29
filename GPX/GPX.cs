using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using hajk.Models;
using hajk.Data;
using OxyPlot.Xamarin.Android;

namespace hajk
{
    public class GpxData
    {
        private readonly List<GPXDataRouteTrack> gpx;

        //Returns list of all items in list
        public GpxData(GPXType gpxtype)
        {
            List<GPXDataRouteTrack>? result = gpxtype switch
            {
                GPXType.Route => RouteDatabase.GetRoutesAsync().Result,
                GPXType.Track => RouteDatabase.GetTracksAsync().Result,
                _ => null,
            };

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

    public class GPXViewHolder : AndroidX.RecyclerView.Widget.RecyclerView.ViewHolder
    {
        public int Id { get; set; }
        public GPXType? GPXType { get; set; }
        public ImageView? GPXTypeLogo { get; set; }
        public TextView? Name { get; set; }
        public TextView? Distance { get; set; }
        public TextView? Ascent { get; set; }
        public TextView? Descent { get; set; }
        public TextView? NaismithTravelTime { get; set; }
        public TextView? ShenandoahsHikingDifficulty { get; set; }
        public TextView? Img_more { get; set; }
        public ImageView? TrackRouteMap { get; set; }
        public PlotView? TrackRouteElevation { get; set; }

        public GPXViewHolder(Android.Views.View itemview, Action<int> listener) : base(itemview)
        {
            GPXTypeLogo = itemview?.FindViewById<ImageView>(Resource.Id.GPXTypeLogo);
            Name = itemview?.FindViewById<TextView>(Resource.Id.Name);
            Distance = itemview?.FindViewById<TextView>(Resource.Id.Distance);
            Ascent = itemview?.FindViewById<TextView>(Resource.Id.Ascent);
            Descent = itemview?.FindViewById<TextView>(Resource.Id.Descent);
            NaismithTravelTime = itemview?.FindViewById<TextView>(Resource.Id.NaismithTravelTime);
            ShenandoahsHikingDifficulty = itemview?.FindViewById<TextView>(Resource.Id.ShenandoahsHikingDifficulty);
            Img_more = itemview?.FindViewById<TextView>(Resource.Id.textViewOptions);
            TrackRouteMap = itemview?.FindViewById<ImageView>(Resource.Id.TrackRouteMap);
            TrackRouteElevation = itemview?.FindViewById<PlotView>(Resource.Id.TrackRouteElevation);

            //itemview.Click += (sender, e) => listener(Position);
        }
    }
}
