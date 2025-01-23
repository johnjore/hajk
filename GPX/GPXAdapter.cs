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
using Microsoft.Maui.Storage;
using Serilog;
using SharpGPX;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using GPXUtils;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot;
using Org.Apache.Http.Conn.Routing;

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
                GPXViewHolder? vh = holder as GPXViewHolder;

                if (vh == null || vh.Name == null || vh.Distance == null || vh.Ascent == null || vh.Descent == null || vh.GPXTypeLogo == null || vh.TrackRouteMap == null || vh.TrackRouteElevation == null)
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

                //Map Thumbprint of route / track
                vh.TrackRouteMap.SetImageResource(0);   //Clear it, as it's reused
                string ImageBase64String = mGpxData[position].ImageBase64String;
                if (ImageBase64String != null)
                {
                    var bitmap = Utils.Misc.ConvertStringToBitmap(ImageBase64String);
                    if (bitmap != null)
                    {
                        vh.TrackRouteMap.SetImageBitmap(bitmap);
                    }
                }

                //Elevation plot
                var elevationModel = CreatePlotModel(GpxClass.FromXml(mGpxData[position].GPX));
                if (elevationModel != null) {
                    vh.TrackRouteElevation.Model = elevationModel;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"GpxAdapter - OnBindViewHolder()");
            }
        }

        private static PlotModel? CreatePlotModel(GpxClass gpx)
        {
            if (gpx == null)
                return null;

            var plotModel = new PlotModel { };

            var series1 = new LineSeries
            {
                MarkerType = MarkerType.None,
                MarkerSize = 1,
                MarkerStroke = OxyColors.White
            };

            //Graph max / min
            decimal min = 0;
            decimal max = 0;
            double distance_km = 0.0;
            var ph = new PositionHandler();

            //Routes
            if (gpx.Routes != null && gpx.Routes.Count > 0)
            {
                //Fist item to plot is first item at pos 0
                if (gpx.Routes[0].rtept[0] == null)
                    return null;

                //First item
                var elevation = (double)gpx.Routes[0].rtept[0].ele;
                min = (decimal)elevation;
                max = (decimal)elevation;
                series1.Points.Add(new DataPoint(0, elevation));

                //Start at index 1 so we can calculate distance from index 0 as new position on x axis
                for (int i = 1; i < gpx.Routes[0].rtept.Count; i++)
                {
                    //Calculate Distance to previous point and add as a datapoint
                    var p1 = new GPXUtils.Position((float)gpx.Routes[0].rtept[i - 1].lat, (float)gpx.Routes[0].rtept[i - 1].lon, 0, null);
                    var p2 = new GPXUtils.Position((float)gpx.Routes[0].rtept[i    ].lat, (float)gpx.Routes[0].rtept[i    ].lon, 0, null);
                    distance_km += (double)ph.CalculateDistance(p1, p2, DistanceType.Kilometers);
                    series1.Points.Add(new DataPoint(distance_km, (double)gpx.Routes[0].rtept[i].ele));

                    //Find Max
                    if (gpx.Routes[0].rtept[i].ele > max)
                    {
                        max = gpx.Routes[0].rtept[i].ele;
                    }

                    //Find Min
                    if (gpx.Routes[0].rtept[i].ele < min)
                    {
                        min = gpx.Routes[0].rtept[i].ele;
                    }
                }
            }

            //Tracks
            if (gpx.Tracks.Count > 0)
            {
                //Fist item to plot is first item at pos 0
                if (gpx.Tracks[0] == null || gpx.Tracks[0].trkseg[0] == null || gpx.Tracks[0].trkseg[0].trkpt[0] == null)
                    return null;

                //First item
                var elevation = (double)gpx.Tracks[0].trkseg[0].trkpt[0].ele;
                min = (decimal)elevation;
                max = (decimal)elevation;
                series1.Points.Add(new DataPoint(0, elevation));

                //The rest
                foreach (SharpGPX.GPX1_1.trkType track in gpx.Tracks)
                {
                    foreach (SharpGPX.GPX1_1.trksegType trkseg in track.trkseg)
                    {
                        //Start at index 1 so we can calculate distance from index 0 as new position on x axis
                        for (int i = 1; i < trkseg.trkpt.Count; i++)
                        {
                            //Calculate Distance to previous point and add as a datapoint
                            var p1 = new GPXUtils.Position((float)trkseg.trkpt[i - 1].lat, (float)trkseg.trkpt[i - 1].lon, 0, null);
                            var p2 = new GPXUtils.Position((float)trkseg.trkpt[i    ].lat, (float)trkseg.trkpt[i    ].lon, 0, null);
                            distance_km += (double)ph.CalculateDistance(p1, p2, DistanceType.Kilometers);
                            series1.Points.Add(new DataPoint(distance_km, (double)trkseg.trkpt[i].ele));

                            //Finx Max
                            if (trkseg.trkpt[i].ele > max)
                            {
                                max = trkseg.trkpt[i].ele;
                            }

                            //Find Min
                            if (trkseg.trkpt[i].ele < min)
                            {
                                min = trkseg.trkpt[i].ele;
                            }
                        }
                    }
                }
            }

            //Axes
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, FormatAsFractions = true, Unit = "km" });
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Maximum = (double)(max + max / 10), Minimum = (double)(min - min / 10), Unit = "m" });

            plotModel.Series.Add(series1);

            return plotModel;
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
                                    Gpx_Menu_Followroute(vh, parent);

                                    break;
                                case var value when value == Resource.Id.gpx_menu_showonmap:
                                    Gpx_Menu_ShowOnMap(vh, parent);

                                    break;
                                case var value when value == Resource.Id.gpx_menu_deleteroute:
                                    await Gpx_Menu_DeleteRoute(vh);

                                    break;
                                case var value when value == Resource.Id.gpx_menu_reverseroute:
                                    Gpx_Menu_ReverseRoute(vh);

                                    break;
                                case var value when value == Resource.Id.gpx_menu_exportgpx:
                                    Gpx_Menu_Exportgpx(vh, parent);

                                    break;
                                case var value when value == Resource.Id.gpx_menu_exportmap:
                                    Gpx_Menu_Exportmap(vh, parent);

                                    break;
                                case var value when value == Resource.Id.gpx_menu_saveofflinemap:
                                    await Download_And_Save_Offline_Map(vh, parent, args.Item.ItemId);

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

        /*
        public static void MAdapter_ItemClick(object sender, int e)
        {
            int gpxNum = e + 1;
            Toast.MakeText(Platform.AppContext, "This is route/track number " + gpxNum, ToastLength.Short).Show();
        }
        */

        private static void Gpx_Menu_Followroute(GPXViewHolder vh, ViewGroup parent)
        {
            Log.Information($"Follow route or track '{vh.Name.Text}'");

            //Get the route or track, and optimize it
            var routetrack = RouteDatabase.GetRouteAsync(vh.Id).Result;
            GpxClass gpx = GPXOptimize.Optimize(GpxClass.FromXml(routetrack.GPX));

            if (routetrack.GPXType == GPXType.Track)
            {
                gpx.Routes.Add(gpx.Tracks[0].ToRoutes()[0]);
            }

            string mapRoute = Import.ParseGPXtoRoute(gpx.Routes[0]).Item1;

            //Add GPX to Map
            DisplayMapItems.AddRouteToMap(mapRoute, GPXType.Route, true, vh.Name.Text);

            //Center on imported route
            var bounds = gpx.GetBounds();
            //Point p = Utils.Misc.CalculateCenter((double)bounds.maxlat, (double)bounds.minlon, (double)bounds.minlat, (double)bounds.maxlon);
            //var sphericalMercatorCoordinate = SphericalMercator.FromLonLat(p.X, p.Y);
            //Fragment_map.mapControl.Map.Navigator.CenterOn(sphericalMercatorCoordinate);

            //Zoom
            //Fragment_map.mapControl.Map.Navigator.ZoomTo(PrefsActivity.MaxZoom);

            //Show the full route
            var (x1, y1) = SphericalMercator.FromLonLat((double)bounds.maxlon, (double)bounds.minlat);
            var (x2, y2) = SphericalMercator.FromLonLat((double)bounds.minlon, (double)bounds.maxlat);
            Fragment_map.mapControl?.Map.Navigator.ZoomToBox(new MRect(x1, y1, x2, y2), MBoxFit.Fit);

            //Switch to map
            ProcessFragmentChanges.SwitchFragment(Fragment_Preferences.Fragment_Map, (FragmentActivity)parent?.Context);

            //Save Route for off-route detection
            MainActivity.ActiveRoute = gpx;

            //Start recording
            RecordTrack.StartTrackTimer();
        }

        private static void Gpx_Menu_ShowOnMap(GPXViewHolder vh, ViewGroup parent)
        {
            Log.Information($"Show route on map '{vh.Name.Text}'");

            //Enumerate route/tracks on Layer, and only add if not already displayed
            /**/

            //Get the route and optimize
            var routetrack_2 = RouteDatabase.GetRouteAsync(vh.Id).Result;
            GpxClass gpx_2 = GPXOptimize.Optimize(GpxClass.FromXml(routetrack_2.GPX));

            if (routetrack_2.GPXType == GPXType.Track)
            {
                gpx_2.Routes.Add(gpx_2.Tracks[0].ToRoutes()[0]);
            }
            string mapRouteTrack_2 = Import.ParseGPXtoRoute(gpx_2.Routes[0]).Item1;

            //Add GPX to Map
            DisplayMapItems.AddRouteToMap(mapRouteTrack_2, routetrack_2.GPXType, true, vh.Name.Text);

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
        }

        private async Task Gpx_Menu_DeleteRoute(GPXViewHolder vh)
        {
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
        }

        private static void Gpx_Menu_ReverseRoute(GPXViewHolder vh)
        {
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
        }

        private static void Gpx_Menu_Exportgpx(GPXViewHolder vh, ViewGroup parent)
        {
            Log.Information($"Export route '{vh.Name.Text}'");

            Android.Views.View? view = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.get_userinput, parent, false);
            AndroidX.AppCompat.App.AlertDialog.Builder? alertbuilder = new(parent.Context);
            alertbuilder.SetView(view);
            EditText? userdata = view?.FindViewById<EditText>(Resource.Id.editText);
            userdata.Text = DateTime.Now.ToString("yyyy-MM-dd HH-mm") + " - " + vh.Name.Text + ".gpx";

            alertbuilder?.SetCancelable(false)
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
            AndroidX.AppCompat.App.AlertDialog dialog = alertbuilder?.Create();
            dialog?.Show();
        }
                            
        private static void Gpx_Menu_Exportmap(GPXViewHolder? vh, ViewGroup parent)
        {
            Log.Information($"Export Map '{vh?.Name.Text}'");

            Android.Views.View? view2 = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.get_userinput, parent, false);
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
                        Import.GetloadOfflineMap(gpx_to_download.Tracks[0].GetBounds(), vh.Id, mbtTilesPath);
                    }

                    if (vh.GPXType == GPXType.Route)
                    {
                        Import.GetloadOfflineMap(gpx_to_download.Routes[0].GetBounds(), vh.Id, mbtTilesPath);
                    }
                }
            })
            .SetNegativeButton(Resource.String.Cancel, delegate
            {
                alertbuilder2.Dispose();
            });
            AndroidX.AppCompat.App.AlertDialog dialog2 = alertbuilder2.Create();
            dialog2.Show();
        }

        private static async Task Download_And_Save_Offline_Map(GPXViewHolder vh, ViewGroup parent, int menuitem)
        {
            Log.Information(Resource.String.download_and_save_offline_map + " '{vh.Name.Text} / {vh.Id}'");

            //If using OSM, cancel out here
            string? TileBulkDownloadSource = Preferences.Get(Platform.CurrentActivity?.GetString(Resource.String.OSM_BulkDownload_Source), Fragment_Preferences.TileBulkDownloadSource);
            if (TileBulkDownloadSource.Equals("OpenStreetMap", StringComparison.OrdinalIgnoreCase))
            {
                Log.Warning("Can't use OSM as a bulkdownload server");
                Toast.MakeText(parent.Context, "Can't use OpenStreetMap Server for bulk downloading.", ToastLength.Long).Show();

                return;
            }

            var route_to_download = RouteDatabase.GetRouteAsync(vh.Id).Result;
            GpxClass gpx_to_download = GpxClass.FromXml(route_to_download.GPX);

            //Get elevation data first
            await Elevation.GetElevationData(gpx_to_download);

            //Get map tiles
            if (vh.GPXType == GPXType.Route)
            {
                await Import.GetloadOfflineMap(gpx_to_download.Routes[0].GetBounds(), vh.Id, null);
            }
            else if (vh.GPXType == GPXType.Track)
            {
                await Import.GetloadOfflineMap(gpx_to_download.Tracks[0].GetBounds(), vh.Id, null);
            }


            //Clear existing GPX routes from map, else they will be included in thumbprint
            Utils.Misc.ClearTrackRoutesFromMap();
            string mapRouteGPX;

            //Parse and draw route on map
            if (vh.GPXType == GPXType.Route)
            {
                mapRouteGPX = Import.ParseGPXtoRoute(gpx_to_download.Routes[0]).Item1;
                DisplayMapItems.AddRouteToMap(mapRouteGPX, GPXType.Route, true, vh.Name.Text);
            }
            else if (vh.GPXType == GPXType.Track)
            {
                mapRouteGPX = Import.ParseGPXtoRoute(gpx_to_download.Tracks[0].ToRoutes()[0]).Item1;
                DisplayMapItems.AddRouteToMap(mapRouteGPX, GPXType.Track, true, vh.Name.Text);
            }

            //Create / Update thumbsize map
            string? ImageBase64String = Import.CreateThumbprintMap(gpx_to_download);
            route_to_download.ImageBase64String = ImageBase64String;
            RouteDatabase.SaveRouteAsync(route_to_download).Wait();

            //Update RecycleView with new entry
            vh?.TrackRouteMap?.SetImageResource(0);
            if (ImageBase64String != null)
            {
                var bitmap = Utils.Misc.ConvertStringToBitmap(ImageBase64String);
                if (bitmap != null)
                {
                    vh?.TrackRouteMap?.SetImageBitmap(bitmap);
                }
            }

            Fragment_gpx.mAdapter?.NotifyItemChanged(menuitem);

            Toast.MakeText(Platform.AppContext, "Finished downloads", ToastLength.Short)?.Show();
        }
    }
}
