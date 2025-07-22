using Android.Content;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment;
using AndroidX.Fragment.App;
using AndroidX.RecyclerView.Widget;
using GeoTiffCOG.Struture;
using Google.Android.Material.Navigation;
using GPXUtils;
using hajk.Data;
using hajk.Fragments;
using hajk.GPX;
using hajk.Models;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Utilities;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Serilog;
using SharpGPX;
using SharpGPX.GPX1_1;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hajk.Adapter
{
    public class GpxAdapter : RecyclerView.Adapter
    {
        public event EventHandler<int> ItemClick;
        public static GpxData? mGpxData;

        public GpxAdapter(GpxData? gpxData)
        {
            mGpxData = gpxData;
        }

        public override int ItemCount
        {
            get { return mGpxData.NumGpx; }
        }

        public override void OnBindViewHolder(AndroidX.RecyclerView.Widget.RecyclerView.ViewHolder holder, int position)
        {
            try
            {
                GPXViewHolder? vh = holder as GPXViewHolder;

                if (vh == null || vh.Name == null || vh.Distance == null || vh.Ascent == null || vh.Descent == null || vh.GPXTypeLogo == null || vh.TrackRouteMap == null || vh.TrackRouteElevation == null || vh.NaismithTravelTime == null)
                    return;

                vh.Id = mGpxData[position].Id;
                vh.GPXType = mGpxData[position].GPXType;
                vh.Name.Text = mGpxData[position].Name;
                vh.Distance.Text = "Length: " + (mGpxData[position].Distance).ToString("N1") + "km";
                
                //Ascent / Descent fields
                vh.Ascent.Text = "Ascent: ";
                vh.Descent.Text = "Descent: ";
                var a = (mGpxData[position].Ascent);
                var d = (mGpxData[position].Descent);

                if (a == 0 && d == 0)
                {
                    vh.Ascent.Text += Platform.CurrentActivity.GetString(Resource.String.NA);
                    vh.Descent.Text += Platform.CurrentActivity.GetString(Resource.String.NA);
                }
                else
                {
                    vh.Ascent.Text += a.ToString() + "m";
                    vh.Descent.Text += d.ToString() + "m";
                }

                if (vh.GPXType == GPXType.Route)
                {
                    vh.GPXTypeLogo.SetImageResource(Resource.Drawable.route);
                } else if (vh.GPXType == GPXType.Track)
                {
                    vh.GPXTypeLogo.SetImageResource(Resource.Drawable.track);
                } else
                {
                    Serilog.Log.Fatal("GPXType must be a route or track");
                }

                //Naismith Travel Time
                if (mGpxData[position].NaismithTravelTime != null)
                {
                    vh.NaismithTravelTime.Text = $"{'\u23f1'} {mGpxData[position].NaismithTravelTime}";
                }
                else
                {
                    vh.NaismithTravelTime.Text = $"{'\u23f1'} N/A";
                }

                //Shenandoah's Hiking Difficulty
                float ShenandoahsHikingDifficultyScale = mGpxData[position].ShenandoahsScale;
                string ShenandoahsHikingDifficultyRating = ShenandoahsHikingDifficulty.CalculateRating(ShenandoahsHikingDifficultyScale);
                ShenandoahsHikingDifficulty.UpdateTextField(vh?.ShenandoahsHikingDifficulty, ShenandoahsHikingDifficultyScale, ShenandoahsHikingDifficultyRating);

                //Set default view for map and elevation
                vh.TrackRouteMap.Visibility = ViewStates.Visible;
                vh.TrackRouteElevation.Visibility = ViewStates.Gone;

                //We now need the full record of the item for the remaining items
                var routetrackInfo = RouteDatabase.GetRouteAsync(mGpxData[position].Id).Result;
                vh?.TrackRouteMap.SetImageResource(0);   //Clear it, as it's reused
                if (routetrackInfo != null)
                {
                    //Map Thumbprint of route / track
                    if (routetrackInfo.ImageBase64String != null && routetrackInfo.ImageBase64String.Length > 0)
                    {
                        string ImageBase64String = routetrackInfo.ImageBase64String;
                        if (ImageBase64String != null)
                        {
                            var bitmap = Utils.Misc.ConvertStringToBitmap(ImageBase64String);
                            if (bitmap != null)
                            {
                                vh?.TrackRouteMap.SetImageBitmap(bitmap);
                            }
                        }
                    }

                    //Elevation plot
                    if (routetrackInfo.GPX != null && routetrackInfo.GPX.Length > 0)
                    {
                        var elevationModel = Helpers.CreatePlotModel(GpxClass.FromXml(routetrackInfo.GPX));
                        if (elevationModel != null)
                        {
                            vh.TrackRouteElevation.Model = elevationModel;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"GpxAdapter - OnBindViewHolder()");
            }
        }

        public override RecyclerView.ViewHolder? OnCreateViewHolder(ViewGroup? parent, int viewType)
        {
            try
            {
                Android.Views.View? itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.activity_gpx, parent, false);
                GPXViewHolder vh = new(itemView, OnClick);

                if (vh == null)
                {
                    return null;
                }

                //Toggle between map and elevation profile
                if (vh.TrackRouteMap != null && vh.TrackRouteElevation != null)
                {
                    vh.TrackRouteMap.Click += (o, e) => ToggleMapElevationProfile(vh);
                    vh.TrackRouteElevation.Click += (o, e) => ToggleMapElevationProfile(vh);
                }

                //Popup menu
                if (vh.Img_more != null)
                {
                    vh.Img_more.Click += (o, e) =>
                    {
                        PopupMenu popup = new(parent.Context, vh.Img_more);
                        popup?.Inflate(Resource.Menu.menu_gpx);

                        if (popup == null || popup.Menu == null)
                        {
                            return;
                        }

                        //Fix menu text
                        if (vh.GPXType == GPXType.Track)
                        {
                            popup?.Menu?.FindItem(Resource.Id.gpx_menu_followroute)?.SetTitle(Resource.String.follow_track);
                            popup?.Menu?.FindItem(Resource.Id.gpx_menu_deleteroute)?.SetTitle(Resource.String.delete_track);
                            popup?.Menu?.FindItem(Resource.Id.gpx_menu_reverseroute)?.SetTitle(Resource.String.Reverse_track);
                        }

                        popup.MenuItemClick += async (s, args) =>
                        {
                            switch (args?.Item?.ItemId)
                            {
                                case var value when value == Resource.Id.gpx_menu_followroute:
                                    Menus.FollowRoute(vh, parent);

                                    break;
                                case var value when value == Resource.Id.gpx_menu_showonmap:
                                    Menus.ShowOnMap(vh, parent);

                                    break;
                                case var value when value == Resource.Id.gpx_menu_deleteroute:
                                    await Menus.DeleteRoute(vh);

                                    break;
                                case var value when value == Resource.Id.gpx_menu_reverseroute:
                                    Menus.ReverseRoute(vh);

                                    break;
                                case var value when value == Resource.Id.gpx_menu_exportgpx:
                                    Menus.ExportGPX(vh, parent);

                                    break;
                                case var value when value == Resource.Id.gpx_menu_exportmap:
                                    Menus.ExportMap(vh, parent);

                                    break;
                                case var value when value == Resource.Id.gpx_menu_saveofflinemap:
                                    await Menus.Download_And_Save_Offline_Map(vh, parent, args.Item.ItemId);

                                    break;
                            }
                        };

                        popup.Show();
                    };
                }

                return vh;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"GpxAdapter - RecylerView.ViewHolder()");
            }

            return null;
        }

        private static void ToggleMapElevationProfile(GPXViewHolder vh)
        {
            vh.TrackRouteElevation.SetMinimumHeight(vh.TrackRouteMap.Height);

            if (vh.TrackRouteMap.Visibility == ViewStates.Visible)
            {
                vh.TrackRouteMap.Visibility = ViewStates.Gone; //Renitialize the object?
                vh.TrackRouteElevation.Visibility = ViewStates.Visible;
            }
            else
            {
                vh.TrackRouteMap.Visibility = ViewStates.Visible;
                vh.TrackRouteElevation.Visibility = ViewStates.Gone;
            }

        }

        private void OnClick(int obj)
        {
            ItemClick?.Invoke(this, obj);
        }

        public void UpdateItems(GPXType gpxtype)
        {
            mGpxData = new GpxData(gpxtype);

            NotifyDataSetChanged();
        }

        /*
        public static void MAdapter_ItemClick(object sender, int e)
        {
            int gpxNum = e + 1;
            Toast.MakeText(Platform.AppContext, "This is route/track number " + gpxNum, ToastLength.Short).Show();
        }
        */
    }
}
