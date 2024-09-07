using Android.OS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Android.Views;


namespace hajk.Progressbar
{
    public class UpdateProgressBar(Android.Widget.ProgressBar pb, Google.Android.Material.TextView.MaterialTextView strTextView) : AsyncTask<int, int, string>
    {
        public static int Progress
        {
            get { return _progress; }
            set { _progress = value; }
        }        
        public static string MessageBody
        {
            get { return _messageBody; }
            set { _messageBody = value; }
        }
        
        private static int _progress = 0;
        private static string _messageBody = string.Empty;
        private readonly Android.Widget.ProgressBar mpb = pb;
        private readonly Google.Android.Material.TextView.MaterialTextView? strTextUpdate = strTextView;
        private static Android.App.Dialog? dialog = null;

        public static void CreateGUI(string strTitle)
        {
            if (Platform.CurrentActivity == null)
            {
                return;
            }

            LayoutInflater? layoutInflater = LayoutInflater.From(Platform.CurrentActivity);
            Android.Views.View? progressDialogBox = layoutInflater?.Inflate(Resource.Layout.progressbardialog, null);
            AndroidX.AppCompat.App.AlertDialog.Builder alertDialogBuilder = new(Platform.CurrentActivity);
            alertDialogBuilder.SetView(progressDialogBox);
            var progressBar = progressDialogBox?.FindViewById<Android.Widget.ProgressBar>(Resource.Id.progressBar);
            var progressBarText1 = progressDialogBox?.FindViewById<Google.Android.Material.TextView.MaterialTextView>(Resource.Id.progressBarText1);
            var progressBarText2 = progressDialogBox?.FindViewById<Google.Android.Material.TextView.MaterialTextView>(Resource.Id.progressBarText2);
            if (progressBar == null || progressBarText1 == null || progressBarText2 == null)
            {
                return;
            }

            progressBar.Max = 100;
            progressBar.Progress = 0;
            progressBarText1.Text = strTitle;

            dialog = alertDialogBuilder.Create();
            dialog.SetCancelable(true);
            dialog.Show();

            var uptask = new UpdateProgressBar(progressBar, progressBarText2);
            uptask.Execute();
        }

        protected override string RunInBackground(params int[] @params)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(24))
            {
                while (_progress < 100)
                {
                    mpb.SetProgress(_progress, false);

                    if (strTextUpdate != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            strTextUpdate.Text = _messageBody;
                        });
                    };

                    //Runs continuously. Slow down the GUI update
                    Thread.Sleep(100);
                }
            }

            dialog?.Cancel();
            return "finish";
        }
    }
}
