using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using System;

namespace hajk.Adapter
{
    public class GpxAdapter : RecyclerView.Adapter
    {
        public event EventHandler<int> ItemClick;
        public GpxData mGpxData;

        public GpxAdapter(GpxData gpxData)
        {
            mGpxData = gpxData;
        }

        public override int ItemCount
        {
            get { return mGpxData.NumGpx; }
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            GPXViewHolder vh = holder as GPXViewHolder;
            vh.Name.Text = mGpxData[position].Name;
            vh.Distance.Text = (mGpxData[position].Distance).ToString();
            //vh.Image.SetImageResource(mPhotoAlbum[position].mPhotoID);
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            View itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.activity_gpx, parent, false);
            GPXViewHolder vh = new GPXViewHolder(itemView, OnClick);
            return vh;
        }

        private void OnClick(int obj)
        {
            ItemClick?.Invoke(this, obj);
        }

        public static void MAdapter_ItemClick(object sender, int e)
        {
            int gpxNum = e + 1;
            Toast.MakeText(MainActivity.mContext, "This is photo number " + gpxNum, ToastLength.Short).Show();
        }
    }
}
