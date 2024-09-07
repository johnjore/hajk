using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AndroidX.Fragment;
using AndroidX.Fragment.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using System.Threading;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Android.Content.Res;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using AndroidX.Core.View;
using AndroidX.DrawerLayout.Widget;
using Serilog;
using hajk.Data;
using hajk.Adapter;
using hajk.Fragments;

namespace hajk.Fragments
{
    public class Fragment_stats : AndroidX.Fragment.App.Fragment
    {
        public override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        public override Android.Views.View OnCreateView(LayoutInflater? inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            try
            {
                var view = inflater.Inflate(Resource.Layout.fragment_stats, container, false);
                view.SetBackgroundColor(Android.Graphics.Color.White);

                return view;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, $"Fragment_stats Crashed");
            }

            return null;
        }
    }
}
