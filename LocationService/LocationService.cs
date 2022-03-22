using System;
using Android.App;
using Android.Util;
using Android.Content;
using Android.OS;
using System.Threading;
using AndroidX.Core.App;
using AndroidX.LocalBroadcastManager.Content;
using Xamarin.Essentials;

namespace hajk
{
	[Service]
	public class LocationService : Service
	{
		GPSLocation gpslocation;
		bool isStarted;
		Handler handler;
		Action runnable;

		public override void OnCreate()
		{
			base.OnCreate();
			Serilog.Log.Information($"OnCreate: the location service is initializing.");

			gpslocation = new GPSLocation();
			handler = new Handler();

			runnable = new Action(() =>
			{
				if (gpslocation == null)
				{
					Serilog.Log.Information($"No location information");
				}
				else
				{
					Xamarin.Essentials.Location loc = gpslocation.GetGPSLocationData();
					if (loc != null)
					{
						if (loc.Speed == null)
							loc.Speed = 0;

						Serilog.Log.Information($"Location Service - Update Lat: {loc.Latitude:N5}, Lon: {loc.Longitude:N5}, Altitude: {loc.Altitude:N2}m, Speed: {loc.Speed:N2}m/s, DateStamp: {loc.Timestamp.LocalDateTime}");
						Location.location = loc;
					}
					//Serilog.Log.Information($"Location Service running...");
					Intent i = new Intent(PrefsActivity.NOTIFICATION_BROADCAST_ACTION);
					i.PutExtra(PrefsActivity.BROADCAST_MESSAGE_KEY, "What is the purpose of this?");
					LocalBroadcastManager.GetInstance(this).SendBroadcast(i);

					int UpdateGPSLocation_s = Int32.Parse(Preferences.Get("UpdateGPSLocation", PrefsActivity.UpdateGPSLocation_s.ToString()));
					
					//Update the location beacon on map
					Location.UpdateLocationMarker(Preferences.Get("TrackLocation", false) == false);

					handler.PostDelayed(runnable, UpdateGPSLocation_s * 1000);
				}
			});
		}

		public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
		{
			if (intent.Action.Equals(PrefsActivity.ACTION_START_SERVICE))
			{
				if (isStarted)
				{
					Serilog.Log.Information($"OnStartCommand: The location service is already running.");
				}
				else
				{
					Serilog.Log.Information($"OnStartCommand: The location service is starting.");
					RegisterForegroundService();
					int UpdateGPSLocation_s = Int32.Parse(Preferences.Get("UpdateGPSLocation", PrefsActivity.UpdateGPSLocation_s.ToString()));
					handler.PostDelayed(runnable, UpdateGPSLocation_s * 1000);
					isStarted = true;
				}
			}
			else if (intent.Action.Equals(PrefsActivity.ACTION_STOP_SERVICE))
			{
				Serilog.Log.Information($"OnStartCommand: The location service is stopping.");
				gpslocation = null;
				StopForeground(true);
				StopSelf();
				isStarted = false;
			}
			
			return StartCommandResult.Sticky;
		}


		public override IBinder OnBind(Intent intent)
		{
			// Return null because this is a pure started service. A hybrid service would return a binder that would allow access to the GetFormattedStamp() method.
			return null;
		}


		public override void OnDestroy()
		{
			// We need to shut things down.
			Serilog.Log.Information($"OnDestroy: The location service is shutting down.");

			// Stop the handler.
			handler.RemoveCallbacks(runnable);

			// Remove the notification from the status bar.
			var notificationManager = (NotificationManager)GetSystemService(NotificationService);
			notificationManager.Cancel(PrefsActivity.SERVICE_RUNNING_NOTIFICATION_ID);

			gpslocation = null;
			isStarted = false;
			base.OnDestroy();
		}

		void RegisterForegroundService()
		{
            NotificationChannel chan = new NotificationChannel(PrefsActivity.NOTIFICATION_CHANNEL_ID, PrefsActivity.channelName, NotificationImportance.None)
            {
                LockscreenVisibility = NotificationVisibility.Private
            };
            NotificationManager manager = (NotificationManager)GetSystemService(Context.NotificationService);
			manager.CreateNotificationChannel(chan);

			NotificationCompat.Builder notificationBuilder = new NotificationCompat.Builder(this, PrefsActivity.NOTIFICATION_CHANNEL_ID);
			Notification notification = notificationBuilder
				.SetOngoing(true)
				.SetSmallIcon(Resource.Drawable.track)
				.SetContentText(Resources.GetString(Resource.String.notification_text))
				.SetPriority(1)
				.SetCategory(Notification.CategoryService)				
				.SetContentIntent(BuildIntentToShowMainActivity())
				.Build();

			// Enlist this instance of the service as a foreground service
			StartForeground(PrefsActivity.SERVICE_RUNNING_NOTIFICATION_ID, notification);
		}

		/// <summary>
		/// Builds a PendingIntent that will display the main activity of the app. This is used when the 
		/// user taps on the notification; it will take them to the main activity of the app.
		/// </summary>
		/// <returns>The content intent.</returns>
		PendingIntent BuildIntentToShowMainActivity()
		{
			/**/
			//Needs fixing. Dont want to restart the app, just switch to it...
			//Service does not start?
			var notificationIntent = new Intent(this, typeof(MainActivity));
			notificationIntent.SetAction(PrefsActivity.ACTION_MAIN_ACTIVITY);
			notificationIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTask);
			notificationIntent.PutExtra(PrefsActivity.SERVICE_STARTED_KEY, true);

			var pendingIntent = PendingIntent.GetActivity(this, 0, notificationIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
			return pendingIntent;
		}
	}
}
