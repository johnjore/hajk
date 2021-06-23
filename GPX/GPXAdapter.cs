using Android.Content;
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
            vh.Distance.Text = (mGpxData[position].Distance) .ToString("N2") + " km";
            //vh.Image.SetImageResource(mPhotoAlbum[position].mPhotoID);
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            View itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.activity_gpx, parent, false);
            GPXViewHolder vh = new GPXViewHolder(itemView, OnClick);            

            vh.img_more.Click += (o, e) =>
            {
                Android.Support.V7.Widget.PopupMenu popup = new Android.Support.V7.Widget.PopupMenu(parent.Context, vh.img_more);
                popup.Inflate(Resource.Menu.menu_gpx);
                popup.MenuItemClick += (s, args) =>
                {
                    //Context context = parent.Context;
                    //FragmentManager fm = ((Activity)context).FragmentManager;
                    switch (args.Item.ItemId)
                    {
                        case Resource.Id.gpx_menu_followroute:
                            Toast.MakeText(parent.Context, "follow " + vh.AdapterPosition.ToString(), ToastLength.Short).Show();
                            break;
                        case Resource.Id.gpx_menu_showonmap:
                            Toast.MakeText(parent.Context, "show on map  " + vh.AdapterPosition.ToString(), ToastLength.Short).Show();
                            break;
                        case Resource.Id.gpx_menu_deleteroute:
                            Toast.MakeText(parent.Context, "delete " + vh.AdapterPosition.ToString(), ToastLength.Short).Show();
                            break;
                        case Resource.Id.gpx_menu_reverseroute:
                            Toast.MakeText(parent.Context, "reverse " + vh.AdapterPosition.ToString(), ToastLength.Short).Show();
                            break;
                        case Resource.Id.gpx_menu_exporttogpx:
                            Toast.MakeText(parent.Context, "export to gpx " + vh.AdapterPosition.ToString(), ToastLength.Short).Show();
                            break;
                        case Resource.Id.gpx_menu_saveofflinemap:
                            Toast.MakeText(parent.Context, "save offline map " + vh.AdapterPosition.ToString(), ToastLength.Short).Show();
                            break;
                    }
                };

                popup.Show();
            };

            return vh;
        }

        private void OnClick(int obj)
        {
            ItemClick?.Invoke(this, obj);
        }

        /*public static void MAdapter_ItemClick(object sender, int e)
        {
            int gpxNum = e + 1;
            Toast.MakeText(MainActivity.mContext, "This is route/track number " + gpxNum, ToastLength.Short).Show();
        }*/
    }
}
