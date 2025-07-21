using Android.Health.Connect.DataTypes;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView;
using AndroidX.RecyclerView.Widget;
using GPXUtils;
using hajk.Data;
using hajk.Fragments;
using hajk.Models;
using OxyPlot.Xamarin.Android;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace hajk
{
    public class GpxData
    {
        public class GPXDataRouteTrackExtended : GPXDataRouteTrack
        {
            public double? DistanceToStart { get; set; }
        }

        private readonly List<GPXDataRouteTrackExtended>? gpx;

        //Returns list of all items in list
        public GpxData(GPXType gpxtype)
        {
            List<GPXDataRouteTrack>? queryResult = RouteDatabase.GetSelectedDataAsync(gpxtype).Result;
            var GPXSortingChoice = Preferences.Get("GPXSortingChoice", Fragment_Preferences.GPXSortingChoice);

            //Must match Fragment_Preferences.SortByOptions
            switch (GPXSortingChoice)
            {
                case 0:
                    //Distance from here
                    gpx = SortByLocation(queryResult);
                    break;
                case 1:
                    //Date Added
                    gpx = SortByID(queryResult);
                    break;
                case 2:
                    //Alphabetically
                    gpx = SortByName(queryResult);
                    break;
                case 3:
                    //Distance of Route/Track
                    gpx = SortByLength(queryResult);
                    break;
                case 4:
                    //Shenandoah
                    gpx = SortByDifficulty(queryResult);
                    break;
                case 5:
                    //Ascent
                    gpx = SortByAscent(queryResult);
                    break;
                case 6:
                    gpx = SortByNeismithTime(queryResult);
                    break;
                default:
                    //Should not happen...
                    gpx = null;
                    break;
            };

            //Reverse order?
            if ((SortOrder)Preferences.Get("GPXSortingOrder", (int)Fragment_Preferences.GPXSortingOrder) == SortOrder.Descending)
            {
                gpx?.Reverse();
            }
        }

        private static List<GPXDataRouteTrackExtended>? SortByLocation(List<GPXDataRouteTrack> queryResult)
        {
            //If we dont have a current location, we can't calculate the distances
            var _currentLocation = LocationForegroundService.GetLocation();

            if (_currentLocation == null)
            {
                Serilog.Log.Error($"No location data to calculate distance from here");
                return null;
            }

            //Calculate distances
            var p = new PositionHandler();
            List<GPXDataRouteTrackExtended>? data1 = queryResult
                .OrderBy(item => item.ShenandoahsScale)
                .Select(item => new GPXDataRouteTrackExtended
                {
                    Id = item.Id,
                    GPXType = item.GPXType,
                    Name = item.Name,
                    Distance = item.Distance,
                    Ascent = item.Ascent,
                    Descent = item.Descent,
                    Description = item.Description,
                    GPX = item.GPX,
                    ImageBase64String = item.ImageBase64String,
                    NaismithTravelTime = item.NaismithTravelTime,
                    ShenandoahsScale = item.ShenandoahsScale,
                    GPXStartLocation = item.GPXStartLocation,
                    DistanceToStart = p.CalculateDistance(item.GPXStartLocation, _currentLocation, DistanceType.Meters),
                })
                .ToList();
            
            //Sort the records based on Distance from our location - Note: This is reversed, and will be undone later
            if ((SortOrder)Preferences.Get("GPXSortingOrder", (int)Fragment_Preferences.GPXSortingOrder) == SortOrder.Ascending)
            {
                return (data1.OrderBy(item => item.DistanceToStart).ToList());
            }
            else
            {
                return (data1.OrderByDescending(item => item.DistanceToStart).ToList());
            }
        }

        private static List<GPXDataRouteTrackExtended>? SortByDifficulty(List<GPXDataRouteTrack> queryResult)
        {
            //Sort based on when added to DB (the ID increases with each record)
            return queryResult
                .OrderBy(item => item.ShenandoahsScale)
                .Select(item => new GPXDataRouteTrackExtended
                {
                    Id = item.Id,
                    GPXType = item.GPXType,
                    Name = item.Name,
                    Distance = item.Distance,
                    Ascent = item.Ascent,
                    Descent = item.Descent,
                    Description = item.Description,
                    GPX = item.GPX,
                    ImageBase64String = item.ImageBase64String,
                    NaismithTravelTime = item.NaismithTravelTime,
                    ShenandoahsScale = item.ShenandoahsScale,
                    GPXStartLocation = item.GPXStartLocation,
                    DistanceToStart = 0,
                })
                .ToList();
        }

        private static List<GPXDataRouteTrackExtended>? SortByLength(List<GPXDataRouteTrack> queryResult)
        {
            return queryResult
                .OrderBy(item => item.Distance)
                .Select(item => new GPXDataRouteTrackExtended
                {
                    Id = item.Id,
                    GPXType = item.GPXType,
                    Name = item.Name,
                    Distance = item.Distance,
                    Ascent = item.Ascent,
                    Descent = item.Descent,
                    Description = item.Description,
                    GPX = item.GPX,
                    ImageBase64String = item.ImageBase64String,
                    NaismithTravelTime = item.NaismithTravelTime,
                    ShenandoahsScale = item.ShenandoahsScale,
                    GPXStartLocation = item.GPXStartLocation,
                    DistanceToStart = 0,
                })
                .ToList();
        }

        private static List<GPXDataRouteTrackExtended>? SortByName(List<GPXDataRouteTrack> queryResult)
        {
            return queryResult
                .OrderBy(item => item.Name)
                .Select(item => new GPXDataRouteTrackExtended
                {
                    Id = item.Id,
                    GPXType = item.GPXType,
                    Name = item.Name,
                    Distance = item.Distance,
                    Ascent = item.Ascent,
                    Descent = item.Descent,
                    Description = item.Description,
                    GPX = item.GPX,
                    ImageBase64String = item.ImageBase64String,
                    NaismithTravelTime = item.NaismithTravelTime,
                    ShenandoahsScale = item.ShenandoahsScale,
                    GPXStartLocation = item.GPXStartLocation,
                    DistanceToStart = 0,
                })
                .ToList();
        }

        private static List<GPXDataRouteTrackExtended>? SortByID(List<GPXDataRouteTrack> queryResult)
        {
            //Sort based on when added to DB (the ID increases with each record)
            return queryResult
                        .OrderByDescending(item => item.Id)
                        .Select(item => new GPXDataRouteTrackExtended
                        {
                            Id = item.Id,
                            GPXType = item.GPXType,
                            Name = item.Name,
                            Distance = item.Distance,
                            Ascent = item.Ascent,
                            Descent = item.Descent,
                            Description = item.Description,
                            GPX = item.GPX,
                            ImageBase64String = item.ImageBase64String,
                            NaismithTravelTime = item.NaismithTravelTime,
                            ShenandoahsScale = item.ShenandoahsScale,
                            GPXStartLocation = item.GPXStartLocation,
                            DistanceToStart = 0,
                        })
                        .ToList();
        }

        private static List<GPXDataRouteTrackExtended>? SortByAscent(List<GPXDataRouteTrack> queryResult)
        {
            return queryResult
                .OrderBy(item => item.Ascent)
                .Select(item => new GPXDataRouteTrackExtended
                {
                    Id = item.Id,
                    GPXType = item.GPXType,
                    Name = item.Name,
                    Distance = item.Distance,
                    Ascent = item.Ascent,
                    Descent = item.Descent,
                    Description = item.Description,
                    GPX = item.GPX,
                    ImageBase64String = item.ImageBase64String,
                    NaismithTravelTime = item.NaismithTravelTime,
                    ShenandoahsScale = item.ShenandoahsScale,
                    GPXStartLocation = item.GPXStartLocation,
                    DistanceToStart = 0,
                })
                .ToList();
        }

        private static List<GPXDataRouteTrackExtended>? SortByNeismithTime(List<GPXDataRouteTrack> queryResult)
        {
            return queryResult
                .OrderBy(item => ParseNaismith(item.NaismithTravelTime))
                .Select(item => new GPXDataRouteTrackExtended
                {
                    Id = item.Id,
                    GPXType = item.GPXType,
                    Name = item.Name,
                    Distance = item.Distance,
                    Ascent = item.Ascent,
                    Descent = item.Descent,
                    Description = item.Description,
                    GPX = item.GPX,
                    ImageBase64String = item.ImageBase64String,
                    NaismithTravelTime = item.NaismithTravelTime,
                    ShenandoahsScale = item.ShenandoahsScale,
                    GPXStartLocation = item.GPXStartLocation,
                    DistanceToStart = 0,
                })
                .ToList();
        }

        public static TimeSpan ParseNaismith(string time)
        {
            if (string.IsNullOrWhiteSpace(time))
                return TimeSpan.MaxValue; // Sort null/blank to end

            if (TimeSpan.TryParse(time, out var result))
                return result;

            return TimeSpan.MaxValue;
        }

        //Returns # of items in list
        public int NumGpx
        {
            get { return gpx.Count; }
        }

        // Returns specific item
        public GPXDataRouteTrack this[int i]
        {
            get { return gpx[i]; }
        }

        //Remove route at position adapterPosition
        internal void RemoveAt(int adapterPosition)
        {
            gpx.RemoveAt(adapterPosition);
        }

        //Add route at the beginning of the list
        internal int Insert(GPXDataRouteTrack route)
        {
            var p = new PositionHandler();

            //For displaying purposes, convert class to include DistanceToStart
            var r = new GPXDataRouteTrackExtended
            {
                Id = route.Id,
                GPXType = route.GPXType,
                Name = route.Name,
                Distance = route.Distance,
                Ascent = route.Ascent,
                Descent = route.Descent,
                Description = route.Description,
                GPX = route.GPX,
                ImageBase64String = route.ImageBase64String,
                NaismithTravelTime = route.NaismithTravelTime,
                ShenandoahsScale = route.ShenandoahsScale,
                GPXStartLocation = route.GPXStartLocation,
                DistanceToStart = p.CalculateDistance(route.GPXStartLocation, LocationForegroundService.GetLocation(), DistanceType.Meters)
            };

            gpx?.Insert(0, r);
            Fragment_gpx.mAdapter.UpdateItems(route.GPXType);

            return gpx.Count;

        }
    }

    public class GPXViewHolder : AndroidX.RecyclerView.Widget.RecyclerView.ViewHolder
    {
        public int Id { get; set; }
        public GPXType? GPXType { get; set; }
        public ImageView? GPXTypeLogo { get; set; }
        public TextView? Name { get; set; }
        public TextView? Distance { get; set; }
        public TextView? Ascent { get; set; }
        public TextView? Descent { get; set; }
        public TextView? NaismithTravelTime { get; set; }
        public TextView? ShenandoahsHikingDifficulty { get; set; }
        public TextView? Img_more { get; set; }
        public ImageView? TrackRouteMap { get; set; }
        public PlotView? TrackRouteElevation { get; set; }

        public GPXViewHolder(Android.Views.View itemview, Action<int> listener) : base(itemview)
        {
            GPXTypeLogo = itemview?.FindViewById<ImageView>(Resource.Id.GPXTypeLogo);
            Name = itemview?.FindViewById<TextView>(Resource.Id.Name);
            Distance = itemview?.FindViewById<TextView>(Resource.Id.Distance);
            Ascent = itemview?.FindViewById<TextView>(Resource.Id.Ascent);
            Descent = itemview?.FindViewById<TextView>(Resource.Id.Descent);
            NaismithTravelTime = itemview?.FindViewById<TextView>(Resource.Id.NaismithTravelTime);
            ShenandoahsHikingDifficulty = itemview?.FindViewById<TextView>(Resource.Id.ShenandoahsHikingDifficulty);
            Img_more = itemview?.FindViewById<TextView>(Resource.Id.textViewOptions);
            TrackRouteMap = itemview?.FindViewById<ImageView>(Resource.Id.TrackRouteMap);
            TrackRouteElevation = itemview?.FindViewById<PlotView>(Resource.Id.TrackRouteElevation);

            //itemview.Click += (sender, e) => listener(Position);
        }
    }
}
