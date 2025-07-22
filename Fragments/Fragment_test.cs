using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment.App;
using AndroidX.RecyclerView.Widget;
using System;
using System.Collections.Generic;
using System.Linq;


namespace hajk.Fragments
{
    public class FolderRecyclerFragment : Fragment
    {
        private RecyclerView _recyclerView;
        private MyAdapter _adapter;

        public override Android.Views.View? OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate(Resource.Layout.fragment_folder_recycler, container, false);
            _recyclerView = view.FindViewById<RecyclerView>(Resource.Id.recyclerView);
            _recyclerView.SetLayoutManager(new LinearLayoutManager(Context));

            var folders = GetDummyData();
            _adapter = new MyAdapter(folders);
            _recyclerView.SetAdapter(_adapter);

            return view;
        }

        private List<FolderItem> GetDummyData()
        {
            return new List<FolderItem>
            {
                new FolderItem
                {
                    FolderName = "Fruits",
                    Children = new List<ChildItem>
                    {
                        new ChildItem { Title = "Apple" },
                        new ChildItem { Title = "Banana" },
                        new ChildItem { Title = "Orange" },
                    }
                },
                new FolderItem
                {
                    FolderName = "Vegetables",
                    Children = new List<ChildItem>
                    {
                        new ChildItem { Title = "Carrot" },
                        new ChildItem { Title = "Spinach" },
                        new ChildItem { Title = "Tomato" },
                    }
                }
            };
        }
    }

    public abstract class RecyclerItem { }

    public class FolderItem : RecyclerItem
    {
        public string FolderName { get; set; }
        public bool IsExpanded { get; set; } = false;
        public List<ChildItem> Children { get; set; } = new();
    }

    public class ChildItem : RecyclerItem
    {
        public string Title { get; set; }
    }

    public class MyAdapter : RecyclerView.Adapter
    {
        private const int ViewTypeFolder = 0;
        private const int ViewTypeChild = 1;

        private List<RecyclerItem> visibleItems;
        private List<FolderItem> originalData;

        public MyAdapter(List<FolderItem> folders)
        {
            originalData = folders;
            BuildVisibleList();
        }

        private void BuildVisibleList()
        {
            visibleItems = new List<RecyclerItem>();
            foreach (var folder in originalData)
            {
                visibleItems.Add(folder);
                if (folder.IsExpanded)
                    visibleItems.AddRange(folder.Children);
            }
        }

        public override int ItemCount => visibleItems.Count;

        public override int GetItemViewType(int position)
        {
            return visibleItems[position] switch
            {
                FolderItem => ViewTypeFolder,
                ChildItem => ViewTypeChild,
                _ => throw new Exception("Unknown type")
            };
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            if (viewType == ViewTypeFolder)
            {
                Android.Views.View view = LayoutInflater.From(parent.Context)
                    .Inflate(Resource.Layout.item_folder, parent, false);
                return new FolderViewHolder(view, OnFolderClick);
            }
            else
            {
                Android.Views.View view = LayoutInflater.From(parent.Context)
                    .Inflate(Resource.Layout.item_child, parent, false);
                return new ChildViewHolder(view);
            }
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var item = visibleItems[position];
            if (holder is FolderViewHolder fvh)
                fvh.Bind((FolderItem)item);
            else if (holder is ChildViewHolder cvh)
                cvh.Bind((ChildItem)item);
        }

        private void OnFolderClick(FolderItem folder)
        {
            folder.IsExpanded = !folder.IsExpanded;
            BuildVisibleList();
            NotifyDataSetChanged();
        }
    }

    public class FolderViewHolder : RecyclerView.ViewHolder
    {
        private TextView _folderNameText;
        private FolderItem _item;
        private readonly Action<FolderItem> _onClick;

        public FolderViewHolder(Android.Views.View itemView, Action<FolderItem> onClick) : base(itemView)
        {
            _folderNameText = itemView.FindViewById<TextView>(Resource.Id.folderName);
            _onClick = onClick;
            itemView.Click += (s, e) => _onClick?.Invoke(_item);
        }

        public void Bind(FolderItem item)
        {
            _item = item;
            _folderNameText.Text = $"{(item.IsExpanded ? "▼" : "▶")} {item.FolderName}";
        }
    }

    public class ChildViewHolder : RecyclerView.ViewHolder
    {
        private TextView _childTitleText;

        public ChildViewHolder(Android.Views.View itemView) : base(itemView)
        {
            _childTitleText = itemView.FindViewById<TextView>(Resource.Id.childTitle);
        }

        public void Bind(ChildItem item)
        {
            _childTitleText.Text = item.Title;
        }
    }
}

