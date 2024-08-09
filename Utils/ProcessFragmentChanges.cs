using Android.Content;
using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.Runtime;
using Android.Views;
using AndroidX.AppCompat.App;
using AndroidX.Core.App;
using AndroidX.Core.View;
using AndroidX.Fragment;
using AndroidX.Fragment.App;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Navigation;
using hajk.Fragments;
using Java.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace hajk
{
    internal class ProcessFragmentChanges
    {
        public static void SwitchFragment(string Fragment_Tag, IMenuItem item)
        {
            FragmentActivity? cActivity = (FragmentActivity)Platform.CurrentActivity;

            if (cActivity == null || Fragment_Tag == null || Fragment_Tag == string.Empty)
            {
                return;
            }

            var frag = cActivity.SupportFragmentManager.FindFragmentByTag(Fragment_Tag);
            if (frag != null)
            {
                cActivity.SupportFragmentManager.BeginTransaction()
                    .Show(frag)
                    .Commit();
                cActivity.SupportFragmentManager.ExecutePendingTransactions();
            }

            NavigationView nav = cActivity.FindViewById<NavigationView>(Resource.Id.nav_view);

            switch (Fragment_Tag)
            {
                case Fragment_Preferences.Fragment_GPX:
                    Fragment_map.mapControl.Visibility = ViewStates.Invisible;
                    cActivity.FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Invisible;

                    if (item.TitleFormatted.ToString() == cActivity.Resources.GetString(Resource.String.Routes))
                    {
                        var mi = nav.Menu.FindItem(Resource.Id.nav_routes);
                        mi.SetTitle(Resource.String.Map);
                        mi.SetIcon(Resource.Drawable.maps);
                    }

                    if (item.TitleFormatted.ToString() == cActivity.Resources.GetString(Resource.String.Tracks))
                    {
                        var mi = nav.Menu.FindItem(Resource.Id.nav_tracks);
                         mi.SetTitle(Resource.String.Map);
                         mi.SetIcon(Resource.Drawable.maps);
                    }

                    break;
                case Fragment_Preferences.Fragment_Map:
                    Fragment_map.mapControl.Visibility = ViewStates.Visible;
                    cActivity.FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Visible;

                    IMenuItem mi1;
                    mi1 = nav?.Menu?.FindItem(Resource.Id.nav_routes);
                    mi1?.SetTitle(Resource.String.Routes);
                    mi1?.SetIcon(Resource.Drawable.route);
                    mi1 = nav?.Menu?.FindItem(Resource.Id.nav_tracks);
                    mi1?.SetTitle(Resource.String.Tracks);
                    mi1?.SetIcon(Resource.Drawable.track);

                    break;
                case "Fragment_posinfo":

                    break;

            }
        }

        public static void SwitchFragment(string Fragment_Tag, FragmentActivity activity)
        {
            var sfm = activity.SupportFragmentManager;

            var frag = sfm.FindFragmentByTag(Fragment_Tag);
            if (frag != null) {
                sfm.BeginTransaction()
                    .Show(frag)
                    .Commit();
                sfm.ExecutePendingTransactions();
            }

            NavigationView? nav = Platform.CurrentActivity.FindViewById<NavigationView>(Resource.Id.nav_view);
            IMenuItem? item;

            switch (Fragment_Tag)
            {
                case Fragment_Preferences.Fragment_GPX:
                    /**///This never runs?!?
                    /*
                    Fragment_map.mapControl.Visibility = ViewStates.Invisible;
                    mContext.FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Invisible;

                    item = nav.Menu.FindItem(Resource.Id.nav_routes);
                    item.SetTitle(Resource.String.Map);

                    item = nav.Menu.FindItem(Resource.Id.nav_tracks);
                    item.SetTitle(Resource.String.Map);
                    */
                    break;
                case Fragment_Preferences.Fragment_Map:
                    sfm.BeginTransaction()
                        .Remove(sfm.FindFragmentByTag(Fragment_Preferences.Fragment_GPX))
                        .Commit();
                    sfm.ExecutePendingTransactions();

                    Fragment_map.mapControl.Visibility = ViewStates.Visible;
                    Platform.CurrentActivity.FindViewById<FloatingActionButton>(Resource.Id.fab).Visibility = ViewStates.Visible;

                    item = nav?.Menu.FindItem(Resource.Id.nav_routes);
                    item?.SetTitle(Resource.String.Routes);
                    item?.SetIcon(Resource.Drawable.route);

                    item = nav?.Menu.FindItem(Resource.Id.nav_tracks);
                    item?.SetTitle(Resource.String.Tracks);
                    item?.SetIcon(Resource.Drawable.track);

                    break;
            }
        }
    }
}
