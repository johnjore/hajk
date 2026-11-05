using Android.OS;
using Android.Views;

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
                Serilog.Log.Fatal(ex, $"Fragment_stats Crashed");
            }

            return null;
        }
    }
}
