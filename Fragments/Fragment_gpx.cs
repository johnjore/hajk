using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.AppCompat.Widget;
using AndroidX.Core.View;
using AndroidX.DrawerLayout.Widget;
using AndroidX.Fragment;
using AndroidX.Fragment.App;
using AndroidX.RecyclerView;
using AndroidX.RecyclerView.Widget;
using hajk.Adapter;
using hajk.Data;
using hajk.Fragments;
using Microsoft.Maui.Controls;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace hajk.Fragments
{
    public class Fragment_gpx : AndroidX.Fragment.App.Fragment
    {
        private static RecyclerView? mRecycleView;
        private RecyclerView.LayoutManager? mLayoutManager;
        private static GpxData? mGpxData;
        public static GpxAdapter? mAdapter;
        public static Models.GPXType GPXDisplay = Models.GPXType.Route;
        private static IParcelable _recyclerViewState;
        private static Dictionary<Models.GPXType, IParcelable> _scrollStates = new();        

        public override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }

        public override void OnPause()
        {
            base.OnPause();

            //Save scrollstate
            _scrollStates[Fragment_gpx.GPXDisplay] = mLayoutManager?.OnSaveInstanceState();
        }

        public override void OnResume()
        {
            base.OnResume();

            //Restore scrollstate
            if (_scrollStates.TryGetValue(Fragment_gpx.GPXDisplay, out var savedState) && savedState != null)
            {
                mLayoutManager?.OnRestoreInstanceState(savedState);
            }
        }

        public override Android.Views.View? OnCreateView(LayoutInflater inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            try
            {
                Android.Views.View? view = inflater.Inflate(Resource.Layout.fragment_gpx, container, false);
                //view?.SetBackgroundColor(Android.Graphics.Color.White);

                bool isInitialSelection = true; //Dont fire the spinner during initialization

                //Spinner - Sort Ascending or Decending
                Spinner? mSpinnerOrder = view?.FindViewById<Spinner>(Resource.Id.spinnerSortedBy);
                if (mSpinnerOrder != null && view != null)
                {
                    var adapter = new ArrayAdapter<string>(view?.Context, Android.Resource.Layout.SimpleSpinnerItem, Fragment_Preferences.SortByOrder);
                    adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
                    mSpinnerOrder.Adapter = adapter;
                    mSpinnerOrder.SetSelection(Preferences.Get("GPXSortingOrder", (int)Fragment_Preferences.GPXSortingOrder));

                    mSpinnerOrder.ItemSelected += (s, e) =>
                    {
                        if (isInitialSelection)
                        {
                            //isInitialSelection = false; //Dont set to false here. 2nd Spinner does this
                            return;
                        }

                        Preferences.Set("GPXSortingOrder", e.Position);
                        mAdapter?.UpdateItems(GPXDisplay);
                    };
                }

                //Spinner - Sort by name, length, date etc
                Spinner? mSpinnerType = view?.FindViewById<Spinner>(Resource.Id.spinnerSelection);
                if (mSpinnerType != null && view != null)
                {
                    var adapter = new ArrayAdapter<string>(view?.Context, Android.Resource.Layout.SimpleSpinnerItem, Fragment_Preferences.SortByOptions);
                    adapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
                    mSpinnerType.Adapter = adapter;
                    mSpinnerType.SetSelection(Preferences.Get("GPXSortingChoice", Fragment_Preferences.GPXSortingChoice));

                    mSpinnerType.ItemSelected += (s, e) =>
                    {
                        if (isInitialSelection)
                        {
                            isInitialSelection = false;
                            return;
                        }

                        Preferences.Set("GPXSortingChoice", e.Position);
                        mAdapter?.UpdateItems(GPXDisplay);
                    };
                }

                //RecycleView
                mRecycleView = view?.FindViewById<RecyclerView>(Resource.Id.recyclerView);
                if (mRecycleView != null && view != null)
                {
                    mLayoutManager = new LinearLayoutManager(view?.Context);
                    mRecycleView.SetLayoutManager(mLayoutManager);
                    mRecycleView.Visibility = ViewStates.Visible;
                    
                    mGpxData = new GpxData(GPXDisplay);
                    mAdapter = new GpxAdapter(mGpxData);
                    //mAdapter.ItemClick += GpxAdapter.MAdapter_ItemClick;
                    mRecycleView.SetAdapter(mAdapter);
                }

                return view;
            }
            catch (Exception ex)
            {
                Serilog.Log.Fatal(ex, $"Fragment_gpx - OnCreateView()");
            }

            return null;
        }
    }
}
