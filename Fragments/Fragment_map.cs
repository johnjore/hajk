using Android.OS;
using Android.Views;
using AndroidX.Fragment.App;
using CoordinateSharp;
using DotSpatial.Projections.Transforms;
using GPXUtils;
using hajk;
using hajk.Data;
using hajk.Fragments;
using hajk.Models;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Manipulations;
using Mapsui.Projections;
using Mapsui.UI.Android;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace hajk.Fragments
{
    public class Fragment_map : AndroidX.Fragment.App.Fragment
    {
        public static MapControl? mapControl = null;
        public static Mapsui.Map map = new();
        private static Position? MapPressed = null;
        private static long? Id = null;
        private static long? MovingPOI = -1;               //For MapInfo, if >=0, next tap is new location for POI #

        private CancellationTokenSource? _pendingSingleTap;

        // How long to wait after a SingleTap to see whether a DoubleTap follows
        private static readonly TimeSpan SingleTapDelay = TimeSpan.FromMilliseconds(300);

        private void CancelPendingSingleTap()
        {
            _pendingSingleTap?.Cancel();
            _pendingSingleTap = null;
        }

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

                Serilog.Log.Information($"Add scalebar - X");
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
                    TextAlignment = Alignment.Center,
                    ScaleBarMode = ScaleBarMode.Both,
                    UnitConverter = MetricUnitConverter.Instance,
                    SecondaryUnitConverter = ImperialUnitConverter.Instance,
                    Margin = new MRect(10, 20),
                });

                Serilog.Log.Information($"Import POIs");
                if (Preferences.Get("DrawPOIOnGui", Fragment_Preferences.DrawPOIonGui_b))
                {
                    Task.Run(() => DisplayMapItems.AddPOIToMap());
                }

                Serilog.Log.Information($"Show All Tracks on Map");
                if (Preferences.Get("DrawTracksOnGui", Fragment_Preferences.DrawTracksOnGui_b))
                {
                    Task.Run(() => DisplayMapItems.AddAllTracksToMap());
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

        // Runs the action after SingleTapDelay, unless a DoubleTap cancels it first.
        private void RunAfterSingleTapDelay(Func<Task> action)
        {
            var cts = new CancellationTokenSource();
            _pendingSingleTap = cts;

            Task.Run(async () =>
            {
            try
            {
                    await Task.Delay(SingleTapDelay, cts.Token);
                    await action();
                }
                catch (System.OperationCanceledException)
                {
                    // A DoubleTap arrived, so the single tap action is skipped.
                }
                catch (Exception ex)
                {
                    Serilog.Log.Fatal(ex, $"Fragment_map - RunAfterSingleTapDelay()");
                }
            });
        }

        // Places the POI that is being moved (MovingPOI) at the tapped world position.
        private async Task MovePOIAsync(double worldX, double worldY)
        {
            try
            {
                if (MovingPOI < 0)
                    return;

                var movingId = MovingPOI;
                MovingPOI = -1;

                GPXDataPOI p = await POIDatabase.GetPOIAsync(movingId);
                var latLon = SphericalMercator.ToLonLat(worldX, worldY);
                p.Lat = (decimal)latLon.lat;
                p.Lon = (decimal)latLon.lon;

                var r = POIDatabase.SavePOI(p);
                DisplayMapItems.AddPOIToMap();
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Fragment_map - MovePOIAsync()");
            }
        }

        private void MapOnInfo(object? sender, MapInfoEventArgs? args)
        {
            try
            {
                if (args == null || args.GestureType == null || args.Map == null || args.Map.Layers == null)
                    return;

                // A DoubleTap is always preceded by a SingleTap. Cancel that SingleTap's pending action first, before any of the early returns below.
                if (args.GestureType == GestureType.DoubleTap)
                    CancelPendingSingleTap();

                var mapInfo = args.GetMapInfo(args.Map.Layers);

                if (mapInfo == null || mapInfo.WorldPosition == null)
                    return;

                //Update MapPressed
                var b = SphericalMercator.ToLonLat(mapInfo.WorldPosition.X, mapInfo.WorldPosition.Y);
                MapPressed = new Position(b.lat, b.lon, 0, false, null);
                Serilog.Log.Debug($"Route Object. GPS Position: " + b.ToString());

                //Moving POI. The next tap places the POI. This is checked before "Create POI?" so the DoubleTap that follows a placing SingleTap can't also open the "Create POI?" dialog.
                if (MovingPOI >= 0)
                {
                    var worldX = mapInfo.WorldPosition.X;
                    var worldY = mapInfo.WorldPosition.Y;

                    if (args.GestureType == GestureType.SingleTap)
                        RunAfterSingleTapDelay(() => MovePOIAsync(worldX, worldY)); // A DoubleTap cancels this and places it itself.
                    else
                        _ = Task.Run(() => MovePOIAsync(worldX, worldY));

                    return;
                }

                //Create POI?
                if (mapInfo.Feature.Fields.Count() == 0 && args.GestureType == GestureType.DoubleTap && mapInfo?.WorldPosition != null)
                {
                    Task.Run(async () =>
                    {
                        var lonLat = SphericalMercator.ToLonLat(mapInfo.WorldPosition.X, mapInfo.WorldPosition.Y);
                        var text = "Location:";

                        //GPS
                        text += $"\nGPS: {lonLat.lat.ToString("0.000000")}, {lonLat.lon.ToString("0.000000")}";

                        //UTM
                        CoordinateSharp.UniversalTransverseMercator? utm = GPX.UTMHelpers.LatLontoUTM(lonLat.lat, lonLat.lon);
                        text += ($"\nUTM: {utm?.LongZone}{utm?.LatZone} {utm?.Easting.ToString("0")} E {utm?.Northing.ToString("0")} N");

                        Show_Dialog msg = new(Platform.CurrentActivity);
                        var result = await msg.ShowDialog("Create POI?", text, Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO);
                        if (result == Show_Dialog.MessageResult.YES)
                        {
                            Serilog.Log.Debug($"{mapInfo}");

                            GPXDataPOI p = new()
                            {
                                Name = "Manual Entry",
                                Description = "",
                                Symbol = null,
                                Lat = (decimal)lonLat.lat,
                                Lon = (decimal)lonLat.lon,
                            };

                            var r = POIDatabase.SavePOI(p);
                            DisplayMapItems.AddPOIToMap();
                        }

                        return;
                    });
                }

                if (mapInfo.Layer?.Name == null)
                    return;

                if (mapInfo.Layer.Tag == null)
                    return;

                //Simplify
                var layer = mapInfo.Layer;
                var style = mapInfo.Style;

                //POI?
                if (layer.Name == Fragment_Preferences.Layer_Poi && layer.Tag.ToString() == Fragment_Preferences.Layer_Poi && mapInfo != null && mapInfo.WorldPosition != null)
                {
                    Serilog.Log.Debug($"POI Object");
                    var lonLat = SphericalMercator.ToLonLat(mapInfo.WorldPosition.X, mapInfo.WorldPosition.Y);
                    var text = "Name:\n" + mapInfo?.Feature["name"] + "\n\nDescription:\n" + mapInfo?.Feature["description"] + "\n\nLocation:\n";

                    // Local copy, because the delayed dialog below must not use the Id field (it can change during the delay).
                    var poiId = Convert.ToInt64(mapInfo?.Feature?["id"]);
                    Id = poiId;

                    //GPS
                    text += $"GPS: {lonLat.lat.ToString("0.000000")}, {lonLat.lon.ToString("0.000000")}";

                    //Add UTM values
                    CoordinateSharp.UniversalTransverseMercator? utm = GPX.UTMHelpers.LatLontoUTM(lonLat.lat, lonLat.lon);
                    text += ($"\nUTM: {utm?.LongZone}{utm?.LatZone} {utm?.Easting.ToString("0")} E {utm?.Northing.ToString("0")} N");

                    Serilog.Log.Debug($"{text}/n{poiId}");

                    //Delete POI. Deferred, so a DoubleTap on the same POI cancels it and only "Move POI?" is shown.
                    if (args.GestureType == GestureType.SingleTap)
                    {
                        RunAfterSingleTapDelay(async () =>
                        {
                            Show_Dialog msg = new(Platform.CurrentActivity);
                            var result = await msg.ShowDialog("Delete POI?", text, Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO);
                            if (result == Show_Dialog.MessageResult.YES)
                            {
                                Serilog.Log.Debug($"Deleting {poiId} from POI DB and MemoryLayer");
                                var r = await POIDatabase.DeletePOIAsync(poiId);
                                DisplayMapItems.AddPOIToMap();
                            }
                        });
                        }

                        //Move POI
                    if (args.GestureType == GestureType.DoubleTap)
                        {
                        Task.Run(async () =>
                        {
                            try
                            {
                                Show_Dialog msg = new(Platform.CurrentActivity);
                            var result = await msg.ShowDialog("Move POI?", text, Android.Resource.Attribute.DialogIcon, false, Show_Dialog.MessageResult.YES, Show_Dialog.MessageResult.NO);
                            if (result == Show_Dialog.MessageResult.YES)
                            {
                                    Serilog.Log.Debug($"Moving {poiId} ing POI DB and MemoryLayer");
                                    MovingPOI = poiId;
                            }
                            }
                            catch (Exception ex)
                            {
                                Serilog.Log.Fatal(ex, $"Fragment_map - Move POI dialog");
                        }
                    });
                }
                }

                //Track?
                if (layer.Tag.ToString() == Fragment_Preferences.Layer_Track ||
                    layer.Tag.ToString() == Fragment_Preferences.Layer_Route)
                {
                    var activity = (FragmentActivity?)Platform.CurrentActivity;
                    Id = 0;
                    long i = 0;
                    bool success = long.TryParse(layer.Name.Split('|')[1], out i);
                    if (success)
                    {
                        Id = i;
                    }

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

        /**///Disabled on 261105 - Used?
        /*
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

        public static Position? GetMapPressedCoordinates()
        {
            return MapPressed;
        }

        public static long? GetId()
        {
            return Id;
        }
    }
}