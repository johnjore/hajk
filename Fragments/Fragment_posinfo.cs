
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment.App;
using GPXUtils;
using hajk.Data;
using hajk.Models;
using OxyPlot.Series;
using SharpGPX;
using System.Diagnostics;

namespace hajk.Fragments
{
    public class Fragment_posinfo : AndroidX.Fragment.App.Fragment
    {
        public override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        public override Android.Views.View? OnCreateView(LayoutInflater? inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            try
            {
                Stopwatch sw = Stopwatch.StartNew();

                var activity = (FragmentActivity)Platform.CurrentActivity;
                var view = inflater?.Inflate(Resource.Layout.fragment_posinfo, container, false);
                view?.SetBackgroundColor(Android.Graphics.Color.White);

                Android.Widget.Button? hideFragment = view?.FindViewById<Android.Widget.Button>(Resource.Id.btn_HideFragment);
                hideFragment.Click += delegate
                {
                    var activity = (FragmentActivity)Platform.CurrentActivity;
                    activity.SupportFragmentManager.BeginTransaction()
                        .Remove((AndroidX.Fragment.App.Fragment)activity.SupportFragmentManager.FindFragmentByTag("Fragment_posinfo"))
                        .Commit();
                    activity.SupportFragmentManager.ExecutePendingTransactions();
                };

                //Make sure data does not change while calculating values
                Android.Locations.Location? GpsLocation = LocationForegroundService.GetLocation();
                if (GpsLocation == null)
                {
                    view.FindViewById<TextView>(Resource.Id.CurrentElevation_m).Text = "No GPS Position";
                    return view;
                }

                //Locations in Position format for where we are, and were we pointed at the map
                Position? GpsPosition = new Position(GpsLocation?.Latitude ?? 0, GpsLocation?.Longitude ?? 0, GpsLocation?.Altitude ?? 0, GpsLocation?.HasAltitude ?? false, null);
                Position? MapPosition = Fragment_map.GetMapPressedCoordinates();
                long? Id = Fragment_map.GetId();

                if (Preferences.Get("RecordingTrack", false) == true)
                {
                    //If recorindg, we process this one
                    var l = view?.FindViewById<LinearLayout>(Resource.Id.notrecording);
                    l.Visibility = ViewStates.Gone;

                    l = view?.FindViewById<LinearLayout>(Resource.Id.recording);
                    l.Visibility = ViewStates.Visible;

                    Status_Recording.ShowStatusPage(view, Id, GpsPosition, MapPosition);
                }
                else
                {
                    var l = view?.FindViewById<LinearLayout>(Resource.Id.recording);
                    l.Visibility = ViewStates.Gone;

                    l = view?.FindViewById<LinearLayout>(Resource.Id.notrecording);
                    l.Visibility = ViewStates.Visible;

                    //If not recording, show stats about the one clicked on
                    Status_Recording.ShowInfoPage(view, Id, GpsPosition, MapPosition);
                }

                
                sw.Stop();
                Serilog.Log.Information($"Elapsed time for OnCreateView - PosInfo : {sw.ElapsedMilliseconds} ms");

                return view;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Fragment_posinfo Crashed");
            }

            return null;
        }
    }
}
