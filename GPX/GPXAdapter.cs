using Android.Content;
using Android.Support.V7.Widget;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment;
using AndroidX.Fragment.App;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mapsui.Geometries;
using Mapsui.Layers;
using Mapsui.Projection;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Utilities;
using hajk.Data;
using hajk.Fragments;
using Serilog;
using Google.Android.Material.Navigation;

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
            vh.Id = mGpxData[position].Id;
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

                popup.MenuItemClick += async (s, args) =>
                {
                    switch (args.Item.ItemId)
                    {
                        case Resource.Id.gpx_menu_followroute:
                            Log.Information($"Follow route '{vh.Name.Text}'");

                            //Get the route
                            var route  = RouteDatabase.GetRouteAsync(vh.Id).Result;

                            //Add GPX to Map
                            ILayer lineStringLayer = Import.CreateRouteLayer(route.WayPoints, Import.CreateRouteStyle());
                            lineStringLayer.IsMapInfoLayer = true;
                            lineStringLayer.Enabled = true;
                            Fragment_map.map.Layers.Add(lineStringLayer);

                            //Start recording
                            RecordTrack.StartTrackTimer();
                            NavigationView nav = MainActivity.mContext.FindViewById<NavigationView>(Resource.Id.nav_view);
                            var item = nav.Menu.FindItem(Resource.Id.nav_recordtrack);
                            item.SetTitle("Stop Recording");

                            /**/
                            //var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
                            //Fragment_map.mapControl.Navigator.CenterOn(sphericalMercatorCoordinate);

                            //Fragment_map.mapControl.

                            //Switch to map
                            MainActivity.SwitchFragment("Fragment_map", (FragmentActivity)parent.Context);

                            break;
                        case Resource.Id.gpx_menu_showonmap:
                            Log.Information($"Show route on map '{vh.Name.Text}'");

                            //Get the route
                            var route_1 = RouteDatabase.GetRouteAsync(vh.Id).Result;

                            //Add GPX to Map
                            ILayer lineStringLayer_1 = Import.CreateRouteLayer(route_1.WayPoints, Import.CreateRouteStyle());
                            lineStringLayer_1.IsMapInfoLayer = true;
                            lineStringLayer_1.Enabled = true;
                            Fragment_map.map.Layers.Add(lineStringLayer_1);

                            //Switch to map
                            MainActivity.SwitchFragment("Fragment_map", (FragmentActivity)parent.Context);

                            break;
                        case Resource.Id.gpx_menu_deleteroute:
                            Log.Information($"Delete route '{vh.Name.Text}'");

                            Show_Dialog msg1 = new Show_Dialog(MainActivity.mContext);
                            if (await msg1.ShowDialog($"Delete", $"Delete '{vh.Name.Text}' ?", Android.Resource.Attribute.DialogIcon, true, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) == Show_Dialog.MessageResult.YES)
                            {
                                _ = Data.RouteDatabase.DeleteRouteAsync(vh.Id);
                                mGpxData.RemoveAt(vh.AdapterPosition);
                                NotifyDataSetChanged();
                            }
                        
                            break;
                        case Resource.Id.gpx_menu_reverseroute:
                            Log.Information($"Reverse route '{vh.Name.Text}'");
                            Toast.MakeText(parent.Context, "reverse " + vh.AdapterPosition.ToString(), ToastLength.Short).Show();
                            break;
                        case Resource.Id.gpx_menu_exporttogpx:
                            Log.Information($"Export route '{vh.Name.Text}'");
                            Toast.MakeText(parent.Context, "export to gpx " + vh.AdapterPosition.ToString(), ToastLength.Short).Show();
                            break;
                        case Resource.Id.gpx_menu_saveofflinemap:
                            Log.Information($"Download and save offline map '{vh.Name.Text}'");
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
