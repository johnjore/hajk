using System;
using System.Threading.Tasks;
using Android.OS;
using Android.Views;
using AndroidX.Fragment;
using AndroidX.Fragment.App;
using Xamarin.Essentials;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Projections;
using Mapsui.UI;
using Mapsui.UI.Android;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;
using Serilog;
using GPXUtils;
using Mapsui.Layers;

namespace hajk.Fragments
{
    public class Fragment_map : AndroidX.Fragment.App.Fragment
    {
        public static MapControl? mapControl;
        public static Mapsui.Map map = new();
        public static Position? MapPosition = null;        /**///Pass this as an argument instead of global variable

        public override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        public override View? OnCreateView(LayoutInflater? inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            if (inflater == null)
            {
                Log.Error($"inflator can't be null here");
                return null;
            }

            try
            {
                var view = inflater.Inflate(Resource.Layout.fragment_map, container, false);
                if (view == null)
                {
                    Log.Error($"View can't be null here");
                    return null;
                }
                view.SetBackgroundColor(Android.Graphics.Color.White);

                Log.Debug($"Create mapControl");
                mapControl = view.FindViewById<MapControl>(Resource.Id.mapcontrol);
                if (mapControl == null)
                {
                    Log.Error($"mapControl can't be null here");
                    return null;
                }

                map = new Mapsui.Map
                {
                    CRS = "EPSG:3857", //https://epsg.io/3857,
                };
                mapControl.Map = map;

                bool LockMapRotation = Preferences.Get("MapLockNorth", false);
                Log.Verbose($"Set map rotation (lock or not):" + LockMapRotation.ToString());
                map.Navigator.RotationLock = LockMapRotation;

                Log.Debug($"Cache downloaded tiles");
                DownloadRasterImageMap.LoadOSMLayer();

                Log.Debug($"Add scalebar");
                map.Widgets.Add(new ScaleBarWidget(map)
                {
                    MaxWidth = 300,
                    ShowEnvelop = true,
                    Font = new Font { FontFamily = "sans serif", Size = 20 },
                    TickLength = 15,
                    TextColor = new Color(0, 0, 0, 255),
                    Halo = new Color(0, 0, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    TextAlignment = Alignment.Left,
                    ScaleBarMode = ScaleBarMode.Both,
                    UnitConverter = MetricUnitConverter.Instance,
                    SecondaryUnitConverter = NauticalUnitConverter.Instance,
                    MarginX = 10,
                    MarginY = 20,
                });

                Log.Debug($"Import POIs");
                if (Preferences.Get("DrawPOIOnGui", PrefsActivity.DrawPOIonGui_b))
                {
                    Task.Run(() => Import.AddPOIToMap());
                }

                Log.Debug($"Show All Tracks on Map");
                if (Preferences.Get("DrawTracksOnGui", PrefsActivity.DrawTracksOnGui_b))
                {
                    Task.Run(() => Import.AddTracksToMap());
                }

                //Log.Debug($"Set Zoom");
                /**///Not working
                //Fragment_map.map.Navigator.ZoomToLevel(PrefsActivity.MaxZoom);

                mapControl.Info += MapOnInfo;

                return view;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Fragment_map - OnCreateView() Crashed");
            }

            return null;
        }

        private void MapOnInfo(object? sender, MapInfoEventArgs? args)
        {
            try
            {
                if (args == null)
                    return;

                if (args.MapInfo?.Feature == null)
                    return;

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
                if (layer.Name == "Poi" && layer.Tag.ToString() == "poi")
                {
                    Log.Debug($"POI Object");
                    var b = SphericalMercator.ToLonLat(args.MapInfo.WorldPosition.X, args.MapInfo.WorldPosition.Y);
                    Log.Debug($"POI Object. GPS Position: " + b.ToString());

                    //var c = args.MapInfo.Feature.Fields.GetEnumerator();
                }

                //Track?
                if (layer.Name == "RouteLayer" && layer.Tag.ToString() == "track")
                {
                    Log.Debug($"Track Object");
                }

                /**///Need to filter out the arrows
                //Route?
                if (layer.Name == "RouteLayer" && layer.Tag.ToString() == "route" && style?.ToString() == "Mapsui.Styles.SymbolStyle")
                {
                    var b = SphericalMercator.ToLonLat(args.MapInfo.WorldPosition.X, args.MapInfo.WorldPosition.Y);
                    MapPosition = new Position(b.lon, b.lat, 0);
                    Log.Debug($"Route Object. GPS Position: " + b.ToString());

                    var activity = (FragmentActivity?)Platform.CurrentActivity;

                    //Remove the old fragment, before creating a new one. Maybe replace this with update, instead of delete and create... /**/
                    var fragment = activity?.SupportFragmentManager.FindFragmentByTag("Fragment_posinfo");
                    if (fragment != null)
                    {
                        activity?.SupportFragmentManager.BeginTransaction()
                            .Remove((AndroidX.Fragment.App.Fragment?)activity?.SupportFragmentManager?.FindFragmentByTag("Fragment_posinfo"))
                            .Commit();
                        activity?.SupportFragmentManager.ExecutePendingTransactions();
                    }

                    //Create fragment
                    activity?.SupportFragmentManager.BeginTransaction()
                        .Add(Resource.Id.fragment_container, new Fragment_posinfo(), "Fragment_posinfo")
                        .Commit();
                    activity?.SupportFragmentManager.ExecutePendingTransactions();

                    //Show fragment
                    activity?.SupportFragmentManager.BeginTransaction()
                        .Show(activity?.SupportFragmentManager?.FindFragmentByTag("Fragment_posinfo"))
                        .Commit();
                    activity?.SupportFragmentManager.ExecutePendingTransactions();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Fragment_map - MapInfo()");
            }
        }
    }
}
