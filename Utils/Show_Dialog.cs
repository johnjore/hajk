using Android.App;
using Android.OS;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Android.Content;
using Java.Lang;

//https://forums.xamarin.com/discussion/comment/251826/#Comment_251826

public class Show_Dialog(Activity activity) : object()
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

    public Task<MessageResult>? ShowDialog(string Title, string Message, int IconAttribute = Android.Resource.Attribute.AlertDialogIcon, bool SetCancelable = false, MessageResult PositiveButton = MessageResult.OK, MessageResult NegativeButton = MessageResult.NONE, MessageResult NeutralButton = MessageResult.NONE)
    {
        while (activity.IsFinishing)
        {
            System.Threading.Thread.Sleep(10);
            activity = Platform.WaitForActivityAsync().Result;
        }

        if (activity == null)
        {
            Serilog.Log.Fatal("Fix dialog box: activity is null");
            return null;
        }

        if (activity.IsFinishing)
        {
            Serilog.Log.Fatal("Fix dialog box: '" + activity.IsFinishing + "'");
            return null;
        }

        var tcs = new TaskCompletionSource<MessageResult>();
        var builder = new AlertDialog.Builder(activity);
        builder.SetIconAttribute(IconAttribute);
        builder.SetTitle(Title);
        builder.SetMessage(Message);
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
            builder.Show();
        });

        return tcs.Task;
    }
}
