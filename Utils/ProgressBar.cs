using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using Google.Android.Material.TextView;

namespace hajk.Progressbar
{
    public class UpdateProgressBar
    {
        public static double Progress
        {
            get { return _progress; }
            set { _progress = value; }
        }

        public static string MessageBody
        {
            get { return _messageBody; }
            set { _messageBody = value; }
        }

        private static double _progress = 0;
        private static string _messageBody = string.Empty;
        private readonly Android.Widget.ProgressBar _progressBar;
        private readonly MaterialTextView _textView;
        private static Dialog? _dialog;

        public UpdateProgressBar(Android.Widget.ProgressBar progressBar, MaterialTextView textView)
        {
            _progressBar = progressBar;
            _textView = textView;
        }

        public static async Task CreateGUIAsync(string strTitle)
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
            var progressBarText1 = progressDialogBox?.FindViewById<MaterialTextView>(Resource.Id.progressBarText1);
            var progressBarText2 = progressDialogBox?.FindViewById<MaterialTextView>(Resource.Id.progressBarText2);

            if (progressBar == null || progressBarText1 == null || progressBarText2 == null)
            {
                return;
            }

            progressBar.Max = 100;
            progressBar.Progress = 0;
            progressBarText1.Text = strTitle;

            _dialog = alertDialogBuilder.Create();
            _dialog.SetCancelable(true);
            _dialog.Show();

            var updateProgressBar = new UpdateProgressBar(progressBar, progressBarText2);
            await updateProgressBar.RunAsync();
        }

        public static void Dismiss()
        {
            Progress = 100;
            Thread.Sleep(10);

            _dialog?.Dismiss();
        }

        public async Task RunAsync()
        {
            var progress = new Progress<double>(percent =>
            {
                // Update the ProgressBar and TextView on the UI thread
                _progressBar.Progress = Convert.ToInt32(percent);
                _textView.Text = _messageBody;
            });

            await Task.Run(() => DoWork(progress));
            _dialog?.Dismiss();
        }

        private static void DoWork(IProgress<double> progress)
        {
            while (_progress < 100)
            {
                progress.Report(_progress);
                Thread.Sleep(10);
            }
        }
    }
}
