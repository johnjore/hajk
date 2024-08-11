using Android.Content;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment.App;
using AndroidX.Fragment;
using AndroidX.RecyclerView.Widget;
using GeoTiffCOG.Struture;
using Google.Android.Material.Navigation;
using hajk.Data;
using hajk.Fragments;
using hajk.GPX;
using hajk.Models;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Utilities;
using Mapsui;
using Microsoft.Maui.ApplicationModel;
using Serilog;
using SharpGPX;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public override void OnBindViewHolder(AndroidX.RecyclerView.Widget.RecyclerView.ViewHolder holder, int position)
        {
            try
            {
                GPXViewHolder vh = holder as GPXViewHolder;
                vh.Id = mGpxData[position].Id;
                vh.GPXType = mGpxData[position].GPXType;
                vh.Name.Text = mGpxData[position].Name;
                vh.Distance.Text = "Length: " + (mGpxData[position].Distance).ToString("N1") + "km";
                vh.Ascent.Text = "Ascent: " + (mGpxData[position].Ascent).ToString() + "m";
                vh.Descent.Text = "Descent: " + (mGpxData[position].Descent).ToString() + "m";

                if (vh.GPXType == GPXType.Route)
                {
                    vh.GPXTypeLogo.SetImageResource(Resource.Drawable.route);
                }

                if (vh.GPXType == GPXType.Track)
                {
                    vh.GPXTypeLogo.SetImageResource(Resource.Drawable.track);
                }

                //Clear it, as it's reused
                vh.TrackRouteMap.SetImageResource(0);
                string ImageBase64String = mGpxData[position].ImageBase64String;
                if (ImageBase64String != null)
                {
                    var bitmap = Utils.Misc.ConvertStringToBitmap(ImageBase64String);
                    if (bitmap != null)
                    {
                        vh.TrackRouteMap.SetImageBitmap(bitmap);
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"GpxAdapter - OnBindViewHolder()");
            }

        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            try
            {
                View itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.activity_gpx, parent, false);
                GPXViewHolder vh = new(itemView, OnClick);

                vh.Img_more.Click += (o, e) =>
                {
                    PopupMenu popup = new(parent.Context, vh.Img_more);
                    popup.Inflate(Resource.Menu.menu_gpx);

                    //Fix menu text
                    if (vh.GPXType == GPXType.Track)
                    {
                        popup.Menu.FindItem(Resource.Id.gpx_menu_followroute).SetTitle(Resource.String.follow_track);
                        popup.Menu.FindItem(Resource.Id.gpx_menu_deleteroute).SetTitle(Resource.String.delete_track);
                        popup.Menu.FindItem(Resource.Id.gpx_menu_reverseroute).SetTitle(Resource.String.Reverse_track);
                        popup.Menu.FindItem(Resource.Id.gpx_menu_optimize).SetTitle(Resource.String.optimize_track);
                    }

                    popup.MenuItemClick += async (s, args) =>
                    {
                        switch (args.Item.ItemId)
                        {
                            case var value when value == Resource.Id.gpx_menu_followroute:
                                Log.Information($"Follow route or track '{vh.Name.Text}'");

                                //Get the route or track
                                var routetrack = RouteDatabase.GetRouteAsync(vh.Id).Result;
                                GpxClass gpx = GpxClass.FromXml(routetrack.GPX);

                                if (routetrack.GPXType == GPXType.Track)
                                {
                                    gpx.Routes.Add(gpx.Tracks[0].ToRoutes()[0]);
                                }
                                string mapRoute = Import.GPXtoRoute(gpx.Routes[0], false).Item1;

                                //Add GPX to Map
                                Import.AddRouteToMap(mapRoute, GPXType.Route, true);

                                //Center on imported route
                                var bounds = gpx.GetBounds();
                                //Point p = Utils.Misc.CalculateCenter((double)bounds.maxlat, (double)bounds.minlon, (double)bounds.minlat, (double)bounds.maxlon);
                                //var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(p.X, p.Y);
                                //Fragment_map.mapControl.Map.Navigator.CenterOn(sphericalMercatorCoordinate);

                                //Zoom
                                //Fragment_map.mapControl.Map.Navigator.ZoomTo(PrefsActivity.MaxZoom);

                                //Show the full route
                                var min_1 = SphericalMercator.FromLonLat((double)bounds.maxlon, (double)bounds.minlat);
                                var max_1 = SphericalMercator.FromLonLat((double)bounds.minlon, (double)bounds.maxlat);
                                Fragment_map.mapControl?.Map.Navigator.ZoomToBox(new MRect(min_1.x, min_1.y, max_1.x, max_1.y), MBoxFit.Fit);

                                //Switch to map
                                ProcessFragmentChanges.SwitchFragment(Fragment_Preferences.Fragment_Map, (FragmentActivity)parent.Context);

                                //Save Route for off-route detection
                                MainActivity.ActiveRoute = gpx;

                                //Start recording
                                RecordTrack.StartTrackTimer();

                                break;
                            case var value when value == Resource.Id.gpx_menu_showonmap:
                                Log.Information($"Show route on map '{vh.Name.Text}'");

                                //Get the route
                                var routetrack_2 = RouteDatabase.GetRouteAsync(vh.Id).Result;
                                GpxClass gpx_2 = GpxClass.FromXml(routetrack_2.GPX);

                                if (routetrack_2.GPXType == GPXType.Track)
                                {
                                    gpx_2.Routes.Add(gpx_2.Tracks[0].ToRoutes()[0]);
                                }
                                string mapRouteTrack_2 = Import.GPXtoRoute(gpx_2.Routes[0], false).Item1;

                                //Add GPX to Map
                                Import.AddRouteToMap(mapRouteTrack_2, routetrack_2.GPXType, true);

                                //Center on imported route
                                var bounds_2 = gpx_2.GetBounds();
                                //Point p_1 = Utils.Misc.CalculateCenter((double)bounds_1.maxlat, (double)bounds_1.minlon, (double)bounds_1.minlat, (double)bounds_1.maxlon);
                                //var sphericalMercatorCoordinate_1 = SphericalMercator.FromLonLat(p_1.X, p_1.Y);
                                //Fragment_map.mapControl.Navigator.CenterOn(sphericalMercatorCoordinate_1);

                                //Zoom
                                //Fragment_map.mapControl.Navigator.ZoomTo(PrefsActivity.MaxZoom);

                                //Show the full route
                                var (x1, y1) = SphericalMercator.FromLonLat((double)bounds_2.maxlon, (double)bounds_2.minlat);
                                var (x2, y2) = SphericalMercator.FromLonLat((double)bounds_2.minlon, (double)bounds_2.maxlat);
                                Fragment_map.mapControl?.Map.Navigator.ZoomToBox(new MRect(x1, y1, x2, y2), MBoxFit.Fit);

                                //Switch to map
                                if (parent.Context != null)
                                {
                                    ProcessFragmentChanges.SwitchFragment(Fragment_Preferences.Fragment_Map, (FragmentActivity)parent.Context);
                                }

                                break;
                            case var value when value == Resource.Id.gpx_menu_deleteroute:
                                Log.Information($"Delete route '{vh.Name.Text}'");

                                Show_Dialog msg1 = new(Platform.CurrentActivity);
                                if (await msg1.ShowDialog($"Delete", $"Delete '{vh.Name.Text}' ?", Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO) == Show_Dialog.MessageResult.YES)
                                {
                                    //Remove map tiles
                                    MBTilesWriter.PurgeMapDB(vh.Id);

                                    //Remove from route DB
                                    _ = RouteDatabase.DeleteRouteAsync(vh.Id);

                                    //Remove from GUI
                                    mGpxData.RemoveAt(vh.AdapterPosition);
                                    NotifyDataSetChanged();
                                }

                                break;
                            case var value when value == Resource.Id.gpx_menu_reverseroute:
                                Log.Information($"Reverse route '{vh.Name.Text}'");

                                //Get the route
                                GPXDataRouteTrack route_to_reverse = RouteDatabase.GetRouteAsync(vh.Id).Result;
                                GpxClass gpx_to_reverse = GpxClass.FromXml(route_to_reverse.GPX);

                                if (route_to_reverse.GPXType == GPXType.Track)
                                {
                                    foreach (SharpGPX.GPX1_1.trkType track in gpx_to_reverse.Tracks)
                                    {
                                        foreach (SharpGPX.GPX1_1.trksegType trkseg in track.trkseg)
                                        {
                                            trkseg.trkpt.Reverse();
                                        }
                                    }
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
                                _ = Fragment_gpx.mAdapter.mGpxData.Insert(route_to_reverse);
                                Fragment_gpx.mAdapter.NotifyDataSetChanged();

                                break;
                            case var value when value == Resource.Id.gpx_menu_optimize:
                                Log.Information($"Optimize '{vh.Name.Text}'");

                                //Get the GPX
                                GPXDataRouteTrack item_to_optimize = RouteDatabase.GetRouteAsync(vh.Id).Result;
                                var GPXOptimized = GPXOptimize.Optimize(GpxClass.FromXml(item_to_optimize.GPX)).ToXml();

                                //Update object
                                item_to_optimize.GPX = GPXOptimized;

                                //Save. ID is unchanged
                                RouteDatabase.SaveRouteAsync(item_to_optimize).Wait();

                                break;
                            case var value when value == Resource.Id.gpx_menu_exportgpx:
                                Log.Information($"Export route '{vh.Name.Text}'");

                                View view = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.get_userinput, parent, false);
                                AndroidX.AppCompat.App.AlertDialog.Builder alertbuilder = new(parent.Context);
                                alertbuilder.SetView(view);
                                var userdata = view.FindViewById<EditText>(Resource.Id.editText);
                                userdata.Text = DateTime.Now.ToString("yyyy-MM-dd HH-mm") + " - " + vh.Name.Text + ".gpx";

                                alertbuilder.SetCancelable(false)
                                .SetPositiveButton(Resource.String.Submit, delegate
                                {
                                    //Get the route
                                    var route_to_export = RouteDatabase.GetRouteAsync(vh.Id).Result;
                                    GpxClass gpx_to_export = GpxClass.FromXml(route_to_export.GPX);

                                    string? DownLoadFolder = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)?.AbsolutePath;
                                    if (DownLoadFolder != null)
                                    {
                                        string gpxPath = Path.Combine(DownLoadFolder, userdata.Text);
                                        gpx_to_export.ToFile(gpxPath);
                                    }
                                })
                                .SetNegativeButton(Resource.String.Cancel, delegate
                                {
                                    alertbuilder.Dispose();
                                });
                                AndroidX.AppCompat.App.AlertDialog dialog = alertbuilder.Create();
                                dialog.Show();

                                break;
                            case var value when value == Resource.Id.gpx_menu_exportmap:
                                Log.Information($"Export Map '{vh.Name.Text}'");

                                View view2 = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.get_userinput, parent, false);
                                AndroidX.AppCompat.App.AlertDialog.Builder alertbuilder2 = new(parent.Context);
                                alertbuilder2.SetView(view2);
                                var userdata2 = view2.FindViewById<EditText>(Resource.Id.editText);
                                userdata2.Text = DateTime.Now.ToString("yyyy-MM-dd HH-mm") + " - " + vh.Name.Text + ".mbtiles";

                                alertbuilder2.SetCancelable(false)
                                .SetPositiveButton(Resource.String.Submit, delegate
                                {
                                    string? DownLoadFolder = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads).AbsolutePath;
                                    if (DownLoadFolder != null)
                                    {
                                        string mbtTilesPath = DownLoadFolder + "/" + userdata2.Text;

                                        var route_to_download = RouteDatabase.GetRouteAsync(vh.Id).Result;
                                        GpxClass gpx_to_download = GpxClass.FromXml(route_to_download.GPX);

                                        if (vh.GPXType == GPXType.Track)
                                        {
                                            Import.GetloadOfflineMap(gpx_to_download.Tracks[0].GetBounds(), vh.Id, mbtTilesPath, false);
                                        }

                                        if (vh.GPXType == GPXType.Route)
                                        {
                                            Import.GetloadOfflineMap(gpx_to_download.Routes[0].GetBounds(), vh.Id, mbtTilesPath, false);
                                        }
                                    }
                                })
                                .SetNegativeButton(Resource.String.Cancel, delegate
                                {
                                    alertbuilder2.Dispose();
                                });
                                AndroidX.AppCompat.App.AlertDialog dialog2 = alertbuilder2.Create();
                                dialog2.Show();

                                break;
                            case var value when value == Resource.Id.gpx_menu_saveofflinemap:
                                Log.Information(Resource.String.download_and_save_offline_map + " '{vh.Name.Text} / {vh.Id}'");
                                //Toast.MakeText(parent.Context, "save offline map " + vh.AdapterPosition.ToString(), ToastLength.Short).Show();

                                //Clear existing GPX routes from map, else they will be included
                                Utils.Misc.ClearTrackRoutesFromMap();

                                var route_to_download = RouteDatabase.GetRouteAsync(vh.Id).Result;
                                GpxClass gpx_to_download = GpxClass.FromXml(route_to_download.GPX);

                                string mapRouteGPX = string.Empty;
                                if (vh.GPXType == GPXType.Track)
                                {
                                    Import.GetloadOfflineMap(gpx_to_download.Tracks[0].GetBounds(), vh.Id, null, true);

                                    mapRouteGPX = Import.GPXtoRoute(gpx_to_download.Tracks[0].ToRoutes()[0], false).Item1;
                                    Import.AddRouteToMap(mapRouteGPX, GPXType.Track, true);
                                }

                                if (vh.GPXType == GPXType.Route)
                                {
                                    Import.GetloadOfflineMap(gpx_to_download.Routes[0].GetBounds(), vh.Id, null, false);

                                    mapRouteGPX = Import.GPXtoRoute(gpx_to_download.Routes[0], false).Item1;
                                    Import.AddRouteToMap(mapRouteGPX, GPXType.Route, true);
                                }

                                //Create / Update thumbsize map
                                string ImageBase64String = Import.CreateThumbprintMap(gpx_to_download);
                                route_to_download.ImageBase64String = ImageBase64String;
                                RouteDatabase.SaveRouteAsync(route_to_download).Wait();

                                //Update RecycleView with new entry
                                vh.TrackRouteMap.SetImageResource(0);
                                if (ImageBase64String != null)
                                {
                                    var bitmap = Utils.Misc.ConvertStringToBitmap(ImageBase64String);
                                    if (bitmap != null)
                                    {
                                        vh.TrackRouteMap.SetImageBitmap(bitmap);
                                    }
                                }
                                Fragment_gpx.mAdapter.NotifyItemChanged(args.Item.ItemId);

                                break;
                        }
                    };

                    popup.Show();
                };

                return vh;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"GpxAdapter - RecylerView.ViewHolder()");
            }

            return null;
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
