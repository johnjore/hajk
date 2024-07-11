using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment;
using AndroidX.Fragment.App;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using AndroidX.Core.View;
using AndroidX.DrawerLayout.Widget;
using AndroidX.RecyclerView;
using Serilog;
using Xamarin.Essentials;


namespace hajk.Fragments
{
    public class Fragment_markers : AndroidX.Fragment.App.DialogFragment
    {
        public static Fragment_markers NewInstace(Bundle bundle)
        {
            var fragment = new Fragment_markers
            {
                Arguments = bundle
            };
            return fragment;
        }

        public override View? OnCreateView(LayoutInflater? inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            View? view = inflater?.Inflate(Resource.Layout.fragment_markers, container, false);
            view?.SetBackgroundColor(Android.Graphics.Color.White);
            Dialog?.Window?.RequestFeature(WindowFeatures.NoTitle);
            Dialog?.SetCanceledOnTouchOutside(false);
        
            //Populate with some defaults / last uage            
            var mapUTMZone = view?.FindViewById<EditText>(Resource.Id.editUTMZone);
            if (mapUTMZone != null)
            {
                mapUTMZone.Text = Preferences.Get("mapUTMZone", "54H").ToString();
            }
            var mapScale = view?.FindViewById<EditText>(Resource.Id.editMapScale);
            if (mapScale != null)
            {
                mapScale.Text = Preferences.Get("mapScale", 25000L).ToString();
            }

            //Buttons
            Button? btnCancel = view?.FindViewById<Button>(Resource.Id.btnCancel);
            if (btnCancel != null)
            {
                btnCancel.Click += delegate
                {
                    Dismiss();
                };
            }

            Button? btnAddPOI = view?.FindViewById<Button>(Resource.Id.btnAddPOI);
            if (btnAddPOI != null)
            {
                btnAddPOI.Click += delegate
                {
                    Dismiss();

                    try
                    {
                        //Save new defaults
                        long intmapScale = Convert.ToInt64(mapScale?.Text);
                        Preferences.Set("mapScale", intmapScale);
                        Preferences.Set("mapUTMZone", mapUTMZone?.Text?.ToUpper());

                        //Get and cleanup the values
                        long utmX = Convert.ToInt64(view?.FindViewById<EditText>(Resource.Id.editUTMX)?.Text);
                        long utmY = Convert.ToInt64(view?.FindViewById<EditText>(Resource.Id.editUTMY)?.Text);
                        int offsetX = Convert.ToInt16(view?.FindViewById<EditText>(Resource.Id.editOffsetX)?.Text);
                        int offsetY = Convert.ToInt16(view?.FindViewById<EditText>(Resource.Id.editOffsetY)?.Text);

                        long UTM_X = (offsetX * intmapScale / 1000) + utmX * 1000;
                        long UTM_Y = (offsetY * intmapScale / 1000) + utmY * 1000;

                        var a = mapUTMZone?.Text?[mapUTMZone.Text.Length - 1].ToString().ToUpper();
                        var b = Convert.ToInt16(mapUTMZone?.Text?.Substring(0, mapUTMZone.Text.Length - 1));

                        UTMtoWGS84LatLon.UTMtoLatLon(a, b, UTM_X, UTM_Y);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Failed to create UTMPOI");
                    }
                };
            }

            return view;
        }
    }
}
