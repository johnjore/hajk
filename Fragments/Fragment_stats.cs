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
using Android.Support.V7.Widget;
using Android.Content.Res;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using AndroidX.Core.View;
using AndroidX.DrawerLayout.Widget;
using Serilog;
using Xamarin.Essentials;
using hajk.Data;
using hajk.Adapter;
using hajk.Fragments;

namespace hajk.Fragments
{
    public class Fragment_stats : Fragment
    {
        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.fragment_stats, container, false);

            return view;
        }
    }
}
