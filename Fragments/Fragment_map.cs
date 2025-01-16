using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Android.OS;
using Android.Views;
using AndroidX.Fragment;
using AndroidX.Fragment.App;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.UI;
using Mapsui.UI.Android;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using GPXUtils;
using static System.Net.Mime.MediaTypeNames;
using hajk.Data;
using Mapsui.Nts.Extensions;
using hajk.Models;

namespace hajk.Fragments
{
    public class Fragment_map : AndroidX.Fragment.App.Fragment
    {
        public static MapControl? mapControl = null;
        public static Mapsui.Map map = new();
        public static Position? MapPosition = null;        /**///Pass this as an argument instead of global variable
        private static long MovingPOI = -1;               //For MapInfo, if >=0, next tap is new location for POI #

        public override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        public override Android.Views.View? OnCreateView(LayoutInflater? inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            if (inflater == null)
            {
                Serilog.Log.Fatal($"inflator can't be null here");
                return null;
            }

            try
            {
                var view = inflater.Inflate(Resource.Layout.fragment_map, container, false);
                if (view == null)
                {
                    Serilog.Log.Fatal($"View can't be null here");
                    return null;
                }
                view.SetBackgroundColor(Android.Graphics.Color.White);

                Serilog.Log.Debug($"Create mapControl");

                mapControl = view.FindViewById<MapControl>(Resource.Id.mapcontrol);
                if (mapControl == null)
                {
                    Serilog.Log.Fatal($"mapControl can't be null here");
                    return null;
                }

                map = new Mapsui.Map
                {
                    CRS = "EPSG:3857", //https://epsg.io/3857,
                };
                mapControl.Map = map;

                bool LockMapRotation = Preferences.Get("MapLockNorth", false);
                Serilog.Log.Information($"Set map rotation (lock or not):" + LockMapRotation.ToString());
                map.Navigator.RotationLock = LockMapRotation;

                Serilog.Log.Information($"Cache downloaded tiles");
                DownloadRasterImageMap.LoadOSMLayer();

                
                //Wait for each layer to complete
                foreach (ILayer layer in map.Layers)
                {
                    while (layer.Busy)
                    {
                        Serilog.Log.Information("Waiting for layers to initialize before adding scalebar");
                        Thread.Sleep(1);
                    }
                }

                Serilog.Log.Information($"Add scalebar");
                map.Widgets.Add(new ScaleBarWidget(map)
                {
                    MaxWidth = 300,
                    ShowEnvelop = true,
                    Font = new Mapsui.Styles.Font { FontFamily = "sans serif", Size = 20 },
                    TickLength = 15,
                    TextColor = new Mapsui.Styles.Color(0, 0, 0, 255),
                    Halo = new Mapsui.Styles.Color(0, 0, 0, 0),
                    HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Left,
                    VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Bottom,
                    TextAlignment = Alignment.Left,
                    ScaleBarMode = ScaleBarMode.Both,
                    UnitConverter = MetricUnitConverter.Instance,
                    SecondaryUnitConverter = NauticalUnitConverter.Instance,
                    MarginX = 10,
                    MarginY = 20,
                });

                Serilog.Log.Information($"Import POIs");
                if (Preferences.Get("DrawPOIOnGui", Fragment_Preferences.DrawPOIonGui_b))
                {
                    Task.Run(() => DisplayMapItems.AddPOIToMap());
                }

                Serilog.Log.Information($"Show All Tracks on Map");
                if (Preferences.Get("DrawTracksOnGui", Fragment_Preferences.DrawTracksOnGui_b))
                {
                    Task.Run(() => DisplayMapItems.AddTracksToMap());
                }

                Serilog.Log.Information("Show Recording Track on Map?");
                if ((Preferences.Get("RecordingTrack", false) == true))
                {
                    Serilog.Log.Information("Recording in progress, show on GUI");
                    Task.Run(() =>
                    {
                        RecordTrack.ShowRecordedTrack();
                    });
                }

                mapControl.Info += MapOnInfo;

                /**///Not working. Can't overwrite the symbol that covers the current location with a circle around the location
                /*
                var a = new MyLocationLayer(Fragment_map.map)
                {
                    IsCentered = false,
                    Enabled = true,
                    Name = "XYZ",
                    Style = null, //new SymbolStyle { SymbolScale = 1.5f, Fill = null, Outline = new Pen { Color = Color.Blue, Width = 2.0 } },
                    IsMapInfoLayer = false,
                    a
                };
                map.Layers.Add(a);
                */

                return view;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Fragment_map - OnCreateView() Crashed");
            }

            return null;
        }

        private void MapOnInfo(object? sender, MapInfoEventArgs? args)
        {
            try
            {
                if (args == null)
                    return;

                if (args.MapInfo == null)
                    return;

                //Create POI?
                if (args.MapInfo?.Feature == null && args.NumTaps >= 2 && args.MapInfo?.WorldPosition != null)
                {
                    Task.Run(async () =>
                    {
                        var (lon, lat) = SphericalMercator.ToLonLat(args.MapInfo.WorldPosition.X, args.MapInfo.WorldPosition.Y);
                        var text = "GPS Coordinates:\n" + lat.ToString("0.000000") + ", " + lon.ToString("0.000000");

                        Show_Dialog msg = new(Platform.CurrentActivity);
                        var result = await msg.ShowDialog("Create POI?", text, Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO);
                        if (result == Show_Dialog.MessageResult.YES)
                        {
                            Serilog.Log.Debug($"{args.MapInfo}");                            

                            GPXDataPOI p = new()
                            {
                                Name = "Manual Entry",
                                Description = "",
                                Symbol = null,
                                Lat = (decimal)lat,
                                Lon = (decimal)lon,
                            };
                            
                            var r = POIDatabase.SavePOI(p);
                            DisplayMapItems.AddPOIToMap();
                        }

                        return;
                    });
                }

                if (args.MapInfo == null)
                    return;

                //Moving POI
                if (MovingPOI >= 0 && args.MapInfo.WorldPosition != null)
                {
                    GPXDataPOI p = POIDatabase.GetPOIAsync(MovingPOI).Result;
                    var (lon, lat) = SphericalMercator.ToLonLat(args.MapInfo.WorldPosition.X, args.MapInfo.WorldPosition.Y);
                    p.Lat = (decimal)lat;
                    p.Lon = (decimal)lon;

                    var r = POIDatabase.SavePOI(p);
                    DisplayMapItems.AddPOIToMap();

                    MovingPOI = -1;
                }


                if (args.MapInfo.Layer?.Name == null)
                    return;

                if (args.MapInfo.Layer.Tag == null)
                    return;

                if (args.MapInfo.WorldPosition == null)
                    return;

                //Simplify
                var layer = args.MapInfo.Layer;
                var style = args.MapInfo.Style;

                //POI?
                if (layer.Name == Fragment_Preferences.Layer_Poi && layer.Tag.ToString() == Fragment_Preferences.Layer_Poi && args.MapInfo != null && args.MapInfo.WorldPosition != null)
                {
                    Serilog.Log.Debug($"POI Object");
                    long id = Convert.ToInt64(args.MapInfo?.Feature["id"]);
                    var (lon, lat) = SphericalMercator.ToLonLat(args.MapInfo.WorldPosition.X, args.MapInfo.WorldPosition.Y);
                    var text = "Name:\n" + args.MapInfo?.Feature["name"] + "\n\nDescription:\n" + args.MapInfo?.Feature["description"] + "\n\nGPS Coordinates:\n";
                    text += lat.ToString("0.000000") + ", " + lon.ToString("0.000000");
                    Serilog.Log.Debug($"{text} {id}");

                    Task.Run(async () =>
                    {
                        Show_Dialog msg = new(Platform.CurrentActivity);

                        //Delete POI
                        if (args.NumTaps == 1)
                        {
                            var result = await msg.ShowDialog("Delete POI?", text, Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO);
                            if (result == Show_Dialog.MessageResult.YES)
                            {
                                Serilog.Log.Debug($"Deleting {id} from POI DB and MemoryLayer");
                                var r = await POIDatabase.DeletePOIAsync(id);
                                DisplayMapItems.AddPOIToMap();
                            }
                        }

                        //Move POI
                        if (args.NumTaps == 2)
                        {
                            var result = await msg.ShowDialog("Move POI?", text, Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO);
                            if (result == Show_Dialog.MessageResult.YES)
                            {
                                Serilog.Log.Debug($"Moving {id} ing POI DB and MemoryLayer");
                                MovingPOI = id;
                            }
                        }
                    });
                }

                //Track?
                if (layer.Name == Fragment_Preferences.Layer_Route && layer.Tag.ToString() == Fragment_Preferences.Layer_Track)
                {
                    Serilog.Log.Debug($"Track Object");
                }

                /**///Need to filter out the arrows
                //Route?
                if (layer.Name == Fragment_Preferences.Layer_Route && layer.Tag.ToString() == Fragment_Preferences.Layer_Route && style?.ToString() == "Mapsui.Styles.SymbolStyle" && args.MapInfo != null && args.MapInfo.WorldPosition != null)
                {
                    var b = SphericalMercator.ToLonLat(args.MapInfo.WorldPosition.X, args.MapInfo.WorldPosition.Y);
                    MapPosition = new Position(b.lon, b.lat, 0, null);
                    Serilog.Log.Debug($"Route Object. GPS Position: " + b.ToString());

                    var activity = (FragmentActivity?)Platform.CurrentActivity;

                    //Remove the old fragment, before creating a new one. Maybe replace this with update, instead of delete and create... /**/
                    var fragment = activity?.SupportFragmentManager.FindFragmentByTag("Fragment_posinfo");
                    if (fragment != null)
                    {
                        activity?.SupportFragmentManager.BeginTransaction()
                            .Remove(fragment)
                            .Commit();
                        activity?.SupportFragmentManager.ExecutePendingTransactions();
                    }

                    //Create fragment
                    activity?.SupportFragmentManager.BeginTransaction()
                        .Add(Resource.Id.fragment_container, new Fragment_posinfo(), "Fragment_posinfo")
                        .Commit();
                    activity?.SupportFragmentManager.ExecutePendingTransactions();

                    //Show fragment
                    var frag = activity?.SupportFragmentManager?.FindFragmentByTag("Fragment_posinfo");
                    if (frag != null)
                    {
                        activity?.SupportFragmentManager.BeginTransaction()
                            .Show(frag)
                            .Commit();
                        activity?.SupportFragmentManager.ExecutePendingTransactions();
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Fragment_map - MapInfo()");
            }
        }

        public Func<IFeature?, string> FeatureToText { get; set; } = (f) =>
        {
            if (f is null) return string.Empty;

            var result = new StringBuilder();
            foreach (var field in f.Fields)
            {
                result.Append($"{field}: {f[field]} - ");
            }
            result.Remove(result.Length - 2, 2);
            return result.ToString();
        };
    }
}
