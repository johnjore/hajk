using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using hajk.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace hajk.Adapter
{
    public class GroupedGpxAdapter : RecyclerView.Adapter
    {
        private const int ViewTypeFolder = 0;
        private const int ViewTypeItem = 1;

        private readonly List<GpxListItem> _visibleItems = new();
        private readonly List<GpxFolder> _folders = new();

        public GroupedGpxAdapter(List<GpxFolder> folders)
        {
            _folders = folders;
            _folders.FirstOrDefault()?.Let(f => f.IsExpanded = true);
            BuildVisibleItems();
        }

        private void BuildVisibleItems()
        {
            _visibleItems.Clear();
            foreach (var folder in _folders)
            {
                _visibleItems.Add(folder);
                if (folder.IsExpanded)
                {
                    foreach (var item in folder.Items)
                    {
                        _visibleItems.Add(new GpxChild { Data = item });
                    }
                }
            }
        }

        public override int ItemCount => _visibleItems.Count;

        public override int GetItemViewType(int position)
        {
            return _visibleItems[position] switch
            {
                GpxFolder => ViewTypeFolder,
                GpxChild => ViewTypeItem,
                _ => throw new InvalidOperationException("Unknown item type")
            };
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            if (viewType == ViewTypeFolder)
            {
                Android.Views.View view = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.item_folder, parent, false);
                return new GpxFolderViewHolder(view, OnFolderClick);
            }
            else
            {
                var view = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.activity_gpx, parent, false);
                return new GPXViewHolder(view, OnClick);
            }
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            if (holder is GpxFolderViewHolder folderHolder && _visibleItems[position] is GpxFolder folder)
            {
                folderHolder.Bind(folder);
            }
            else if (holder is GPXViewHolder itemHolder && _visibleItems[position] is GpxChild child)
            {
                GpxAdapterHelper.BindViewHolder(itemHolder, child.Data);
            }
        }

        private void OnFolderClick(GpxFolder folder)
        {
            folder.IsExpanded = !folder.IsExpanded;
            BuildVisibleItems();
            NotifyDataSetChanged();
        }

        private void OnClick(int position)
        {
            // Optional: implement click handling
        }
    }

    public abstract class GpxListItem { }

    public class GpxFolder : GpxListItem
    {
        public string Name { get; set; }
        public bool IsExpanded { get; set; }
        public List<GpxData.GPXDataRouteTrackExtended> Items { get; set; } = new();
    }

    public class GpxChild : GpxListItem
    {
        public GpxData.GPXDataRouteTrackExtended Data { get; set; }
    }

    public class GpxFolderViewHolder : RecyclerView.ViewHolder
    {
        private readonly TextView _title;
        private GpxFolder _item;

        public GpxFolderViewHolder(Android.Views.View itemView, Action<GpxFolder> onClick) : base(itemView)
        {
            _title = itemView.FindViewById<TextView>(Resource.Id.folderName);
            itemView.Click += (s, e) => onClick?.Invoke(_item);
        }

        public void Bind(GpxFolder item)
        {
            _item = item;
            _title.Text = $"{(item.IsExpanded ? "▼" : "▶")} {item.Name}";
        }
    }

    public static class GpxAdapterHelper
    {
        public static void BindViewHolder(GPXViewHolder vh, GpxData.GPXDataRouteTrackExtended data)
        {
            // Reuse existing logic from GpxAdapter.OnBindViewHolder
            // You can extract the full OnBindViewHolder logic into here if needed
            // Example:
            vh.Id = data.Id;
            vh.GPXType = data.GPXType;
            vh.Name.Text = data.Name;
            vh.Distance.Text = $"Length: {data.Distance:N1}km";
            vh.Ascent.Text = data.Ascent > 0 ? $"Ascent: {data.Ascent}m" : "Ascent: N/A";
            vh.Descent.Text = data.Descent > 0 ? $"Descent: {data.Descent}m" : "Descent: N/A";
            vh.NaismithTravelTime.Text = $"⏱ {data.NaismithTravelTime ?? "N/A"}";
            // Further logic for logos, maps, etc. can be extracted from existing adapter
        }
    }

    public static class Extensions
    {
        public static void Let<T>(this T obj, Action<T> action)
        {
            if (obj != null) action(obj);
        }
    }
}
