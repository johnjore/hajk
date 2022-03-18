using System;
using Android.OS;
using Android.Views;
using AndroidX.Fragment.App;
using Mapsui;
using Mapsui.Projection;
using Mapsui.UI;
using Mapsui.UI.Android;
using Mapsui.Styles;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;
using Log = Serilog.Log;
using Xamarin.Essentials;

namespace hajk.Fragments
{
    public class Fragment_map : Fragment
    {
        public static MapControl mapControl;
        public static Mapsui.Map map = new Mapsui.Map();

        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.fragment_map, container, false);

            Log.Debug($"Create mapControl");
            mapControl = view.FindViewById<MapControl>(Resource.Id.mapcontrol);
            map = new Mapsui.Map
            {
                CRS = "EPSG:3857", //https://epsg.io/3857
                Transformation = new MinimalTransformation(),
            };
            mapControl.Map = map;

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
                Import.AddPOIToMap();
            }

            Log.Debug($"Set Zoom");
            mapControl.Navigator.ZoomTo(PrefsActivity.MaxZoom);

            mapControl.Info += MapOnInfo;

            return view;
        }

        private void MapOnInfo(object sender, MapInfoEventArgs args)
        {
            if (args.MapInfo?.Feature == null)
                return;

            if (args.MapInfo.Layer.Name == null)
                return;

            if (args.MapInfo.Layer.Tag == null)
                return;

            //Simplify
            var layer = args.MapInfo.Layer;
            var style = args.MapInfo.Style;

            if (layer.Name == "RouteLayer" && layer.Tag.ToString() == "track")
            {
                Log.Debug($"Track Object");
            }

            /**///Need to filter out the arrows
            if (layer.Name == "RouteLayer" && layer.Tag.ToString() == "route" && style.ToString() == "Mapsui.Styles.SymbolStyle")
            {
                var b = SphericalMercator.ToLonLat(args.MapInfo.WorldPosition.X, args.MapInfo.WorldPosition.Y);
                Log.Debug($"Route Object. GPS Position: " + b.ToString());


            }
        }
    }
}
