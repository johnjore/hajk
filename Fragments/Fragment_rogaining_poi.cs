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
using Microsoft.Maui.Storage;


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

        public override Android.Views.View? OnCreateView(LayoutInflater? inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            Android.Views.View? view = inflater?.Inflate(Resource.Layout.fragment_rogaining_poi, container, false);
            view?.SetBackgroundColor(Android.Graphics.Color.White);
            Dialog?.Window?.RequestFeature(WindowFeatures.NoTitle);
            Dialog?.SetCanceledOnTouchOutside(false);
        
            //Populate with some defaults / last uage            
            var mapUTMZone = view?.FindViewById<EditText>(Resource.Id.editUTMZone);
            if (mapUTMZone != null)
            {
                mapUTMZone.Text = Preferences.Get("mapUTMZone", Fragment_Preferences.DefaultUTMZone).ToString();
            }
            var mapScale = view?.FindViewById<EditText>(Resource.Id.editMapScale);
            if (mapScale != null)
            {
                mapScale.Text = Preferences.Get("mapScale", Fragment_Preferences.DefaultMapScale).ToString();
            }

            //Buttons
            Android.Widget.Button? btnCancel = view?.FindViewById<Android.Widget.Button>(Resource.Id.btnCancel);
            if (btnCancel != null)
            {
                btnCancel.Click += delegate
                {
                    Dismiss();
                };
            }

            Android.Widget.Button? btnAddPOI = view?.FindViewById<Android.Widget.Button>(Resource.Id.btnAddPOI);
            if (btnAddPOI != null)
            {
                btnAddPOI.Click += delegate
                {
                    //Dismiss();

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

                        int result = GPX.UTMHelpers.UTMtoLatLon(a, b, UTM_X, UTM_Y);
                        if (result > 0)
                        {
                            Toast.MakeText(Android.App.Application.Context, "Added POI to database", ToastLength.Short)?.Show();
                        }
                        else
                        {
                            Toast.MakeText(Android.App.Application.Context, "Failed to add POI to database", ToastLength.Long)?.Show();
                        }
                        
                        //Reset GUI
                        (view?.FindViewById<EditText>(Resource.Id.editOffsetX)).Text = string.Empty;
                        (view?.FindViewById<EditText>(Resource.Id.editOffsetY)).Text = string.Empty;
                        (view?.FindViewById<EditText>(Resource.Id.editUTMX)).RequestFocus();
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Fatal(ex, "Failed to create UTMPOI");
                    }
                };
            }

            return view;
        }
    }
}
