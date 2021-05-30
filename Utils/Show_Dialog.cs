using Android.App;
using System.Threading.Tasks;
using Xamarin.Essentials;

//https://forums.xamarin.com/discussion/comment/251826/#Comment_251826

public class Show_Dialog
{
    public enum MessageResult
    {
        NONE = 0,
        OK = 1,
        CANCEL = 2,
        ABORT = 3,
        RETRY = 4,
        IGNORE = 5,
        YES = 6,
        NO = 7
    }

    readonly Activity mcontext;
    public Show_Dialog(Activity activity) : base()
    {
        this.mcontext = activity;
    }

    public Task<MessageResult> ShowDialog(string Title, string Message, int IconAttribute = Android.Resource.Attribute.AlertDialogIcon, bool SetCancelable = false, MessageResult PositiveButton = MessageResult.OK, MessageResult NegativeButton = MessageResult.NONE, MessageResult NeutralButton = MessageResult.NONE)
    {
        var tcs = new TaskCompletionSource<MessageResult>();

        var builder = new AlertDialog.Builder(mcontext);
        builder.SetIconAttribute(IconAttribute);
        builder.SetTitle(Title);
        builder.SetMessage(Message);
        builder.SetInverseBackgroundForced(false);
        builder.SetCancelable(SetCancelable);

        builder.SetPositiveButton((PositiveButton != MessageResult.NONE) ? PositiveButton.ToString() : string.Empty, (senderAlert, args) =>
        {
            tcs.SetResult(PositiveButton);
        });
        builder.SetNegativeButton((NegativeButton != MessageResult.NONE) ? NegativeButton.ToString() : string.Empty, delegate
        {
            tcs.SetResult(NegativeButton);
        });
        builder.SetNeutralButton((NeutralButton != MessageResult.NONE) ? NeutralButton.ToString() : string.Empty, delegate
        {
            tcs.SetResult(NeutralButton);
        });

        MainThread.BeginInvokeOnMainThread(() =>
        {
        });

        MainThread.BeginInvokeOnMainThread(() =>
        {
            builder.Show();
        });

        return tcs.Task;
    }
}
