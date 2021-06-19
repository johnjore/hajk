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




using System.IO;
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
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Navigation;
using Google.Android.Material.Snackbar;
using Mapsui;
using Mapsui.Geometries;
using Mapsui.Layers;
using Mapsui.Projection;
using Mapsui.UI;
using Mapsui.UI.Android;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Utilities;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;
using BruTile.Predefined;
using BruTile.Web;
using Serilog;
using Xamarin.Essentials;
using hajk.Data;
using hajk.Adapter;
using hajk.Fragments;

namespace hajk.Fragments
{
    public class Fragment1 : Fragment
    {
        RecyclerView mRecycleView;
        RecyclerView.LayoutManager mLayoutManager;
        GpxData mGpxData;
        GpxAdapter mAdapter;

        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.fragment1, container, false);

            mGpxData = new GpxData();
            mRecycleView = view.FindViewById<RecyclerView>(Resource.Id.recyclerView);
            mRecycleView.Visibility = ViewStates.Visible;

            mLayoutManager = new LinearLayoutManager(view.Context);
            mRecycleView.SetLayoutManager(mLayoutManager);
            mAdapter = new GpxAdapter(mGpxData);
            mAdapter.ItemClick += GpxAdapter.MAdapter_ItemClick;
            mRecycleView.SetAdapter(mAdapter);

            return view;
        }
    }
}
