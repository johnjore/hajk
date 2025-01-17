using System;
using Android.OS;
using Android.Views;
using Android.Widget;
using CoordinateSharp;
using hajk.Models;

namespace hajk.Fragments
{
    public class Fragment_gps_poi : AndroidX.Fragment.App.DialogFragment
    {
        public static Fragment_gps_poi NewInstace(Bundle bundle)
        {
            var fragment = new Fragment_gps_poi
            {
                Arguments = bundle
            };
            return fragment;
        }

        public override Android.Views.View? OnCreateView(LayoutInflater? inflater, ViewGroup? container, Bundle? savedInstanceState)
        {
            Android.Views.View? view = inflater?.Inflate(Resource.Layout.fragment_gps_poi, container, false);
            view?.SetBackgroundColor(Android.Graphics.Color.White);
            Dialog?.Window?.RequestFeature(WindowFeatures.NoTitle);
            Dialog?.Window?.SetSoftInputMode(SoftInput.AdjustResize);
            Dialog?.SetCanceledOnTouchOutside(false);

            //Set Focus
            var editLatLon = view?.FindViewById<Android.Widget.EditText>(Resource.Id.editLatLonZone);
            editLatLon?.RequestFocus();

            //Buttons
            Android.Widget.Button? btnCancel = view?.FindViewById<Android.Widget.Button>(Resource.Id.btnCancel);
            if (btnCancel != null)
            {
                btnCancel.Click += delegate
                {
                    Dismiss();
                };
            }

            Android.Widget.Button? btnAddGPSPOI = view?.FindViewById<Android.Widget.Button>(Resource.Id.btnAddGPSPOI);
            if (btnAddGPSPOI != null)
            {
                btnAddGPSPOI.Click += delegate
                {
                    Dismiss();

                    try
                    {
                        string strLatLon = view?.FindViewById<EditText>(Resource.Id.editLatLonZone)?.Text;
                        Coordinate c;
                        if (Coordinate.TryParse(strLatLon, out c))
                        {
                            Serilog.Log.Information($"Converted '{strLatLon}' to '{c}'"); //N 80º 20' 44.999" E 23º 45' 22.987"   

                            GPXDataPOI p = new()
                            {
                                Name = "Manual Entry",
                                Description = "Lat/Lng",
                                Symbol = null,
                                Lat = (decimal)c.Latitude.DecimalDegree,
                                Lon = (decimal)c.Longitude.DecimalDegree,
                            };

                            if (Data.POIDatabase.SavePOI(p) > 0)
                            {
                                DisplayMapItems.AddPOIToMap();
                                Toast.MakeText(Android.App.Application.Context, $"Added POI {c}", ToastLength.Short)?.Show();
                            }
                            else
                            {
                                Toast.MakeText(Android.App.Application.Context, "Failed to add POI to database", ToastLength.Long)?.Show();
                            }
                        }
                        else
                        {
                            Toast.MakeText(Android.App.Application.Context, "Failed to parse string", ToastLength.Long)?.Show();
                        }
                                                
                        //Reset GUI
                        (view?.FindViewById<EditText>(Resource.Id.editLatLonZone)).Text = string.Empty;
                        (view?.FindViewById<EditText>(Resource.Id.editLatLonZone)).RequestFocus();
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Fatal(ex, "Failed to create GPS (Lat/Lng) POI");
                    }
                };
            }

            return view;
        }
    }
}
