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
using hajk.Models;
using Serilog;
using Google.Android.Material.Navigation;
using SharpGPX;

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
                            Log.Information($"Follow route or track '{vh.Name.Text}'");

                            //Get the route or track
                            var routetrack  = RouteDatabase.GetRouteAsync(vh.Id).Result;
                            GpxClass gpx = GpxClass.FromXml(routetrack.GPX);

                            if (routetrack.GPXType == GPXType.Track)
                            {
                                gpx.Routes.Add(gpx.Tracks[0].ToRoutes()[0]);
                            }
                            string mapRoute = Import.GPXtoRoute(gpx.Routes[0]).Item1;

                            //Add GPX to Map
                            Import.AddRouteToMap(mapRoute, GPXType.Route);

                            //Center on imported route
                            var bounds = gpx.GetBounds();
                            Point p = Utils.Misc.CalculateCenter((double)bounds.maxlat, (double)bounds.minlon, (double)bounds.minlat, (double)bounds.maxlon);
                            var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(p.X, p.Y);
                            Fragment_map.mapControl.Navigator.CenterOn(sphericalMercatorCoordinate);

                            //Zoom
                            Fragment_map.mapControl.Navigator.ZoomTo(PrefsActivity.MaxZoom);

                            //Switch to map
                            MainActivity.SwitchFragment("Fragment_map", (FragmentActivity)parent.Context);

                            //Start recording
                            RecordTrack.StartTrackTimer();
                            NavigationView nav = MainActivity.mContext.FindViewById<NavigationView>(Resource.Id.nav_view);
                            var item_nav = nav.Menu.FindItem(Resource.Id.nav_recordtrack);
                            item_nav.SetTitle("Stop Recording");

                            break;
                        case Resource.Id.gpx_menu_showonmap:
                            Log.Information($"Show route on map '{vh.Name.Text}'");

                            //Get the route
                            var routetrack_1 = RouteDatabase.GetRouteAsync(vh.Id).Result;
                            GpxClass gpx_1 = GpxClass.FromXml(routetrack_1.GPX);

                            if (routetrack_1.GPXType == GPXType.Track)
                            {
                                gpx_1.Routes.Add(gpx_1.Tracks[0].ToRoutes()[0]);
                            }
                            string mapRouteTrack_1 = Import.GPXtoRoute(gpx_1.Routes[0]).Item1;

                            //Add GPX to Map
                            Import.AddRouteToMap(mapRouteTrack_1, routetrack_1.GPXType);

                            //Center on imported route
                            var bounds_1 = gpx_1.GetBounds();
                            Point p_1 = Utils.Misc.CalculateCenter((double)bounds_1.maxlat, (double)bounds_1.minlon, (double)bounds_1.minlat, (double)bounds_1.maxlon);
                            var sphericalMercatorCoordinate_1 = SphericalMercator.FromLonLat(p_1.X, p_1.Y);
                            Fragment_map.mapControl.Navigator.CenterOn(sphericalMercatorCoordinate_1);

                            //Zoom
                            Fragment_map.mapControl.Navigator.ZoomTo(PrefsActivity.MaxZoom);

                            //Switch to map
                            MainActivity.SwitchFragment("Fragment_map", (FragmentActivity)parent.Context);

                            break;
                        case Resource.Id.gpx_menu_deleteroute:
                            Log.Information($"Delete route '{vh.Name.Text}'");

                            Show_Dialog msg1 = new Show_Dialog(MainActivity.mContext);
                            if (await msg1.ShowDialog($"Delete", $"Delete '{vh.Name.Text}' ?", Android.Resource.Attribute.DialogIcon, true, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) == Show_Dialog.MessageResult.YES)
                            {
                                _ = RouteDatabase.DeleteRouteAsync(vh.Id);
                                mGpxData.RemoveAt(vh.AdapterPosition);
                                NotifyDataSetChanged();
                            }
                        
                            break;
                        case Resource.Id.gpx_menu_reverseroute:
                            Log.Information($"Reverse route '{vh.Name.Text}'");

                            //Get the route
                            var route_to_reverse = RouteDatabase.GetRouteAsync(vh.Id).Result;
                            GpxClass gpx_to_reverse = GpxClass.FromXml(route_to_reverse.GPX);

                            if (route_to_reverse.GPXType == GPXType.Track)
                            {
                                gpx_to_reverse.Tracks[0].trkseg.Reverse();
                            }
                            else
                            {
                                gpx_to_reverse.Routes[0].rtept.Reverse();
                            }

                            //Reverse and save as new entry
                            route_to_reverse.Name += " - reversed";
                            route_to_reverse.Description += " - reversed";
                            route_to_reverse.Id = 0;
                            route_to_reverse.GPX = gpx_to_reverse.ToXml();
                            RouteDatabase.SaveRouteAsync(route_to_reverse).Wait();

                            //Update RecycleView with new entry
                            int i = Fragment_gpx.mAdapter.mGpxData.Add(route_to_reverse);
                            Fragment_gpx.mAdapter.NotifyItemInserted(i);

                            break;
                        case Resource.Id.gpx_menu_exportgpx:
                            Log.Information($"Export route '{vh.Name.Text}'");

                            View view = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.get_userinput, parent, false);
                            Android.Support.V7.App.AlertDialog.Builder alertbuilder = new Android.Support.V7.App.AlertDialog.Builder(parent.Context);
                            alertbuilder.SetView(view);
                            var userdata = view.FindViewById<EditText>(Resource.Id.editText);
                            userdata.Text = DateTime.Now.ToString("yyyy-MM-dd HH-mm") + " - " + vh.Name.Text + ".gpx";

                            alertbuilder.SetCancelable(false)
                            .SetPositiveButton("Submit", delegate
                            {
                                //Get the route
                                var route_to_export = RouteDatabase.GetRouteAsync(vh.Id).Result;
                                GpxClass gpx_to_export = GpxClass.FromXml(route_to_export.GPX);

                                string gpxPath = Path.Combine(MainActivity.rootPath, userdata.Text);
                                gpx_to_export.ToFile(gpxPath);
                            })
                            .SetNegativeButton("Cancel", delegate
                            {
                                alertbuilder.Dispose();
                            });
                            Android.Support.V7.App.AlertDialog dialog = alertbuilder.Create();
                            dialog.Show();

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
