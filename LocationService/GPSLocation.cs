﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace hajk
{
	public class GPSLocation
	{
        public static Xamarin.Essentials.Location location = null;

        public GPSLocation()
		{
		}

		public Xamarin.Essentials.Location GetGPSLocationData()
		{
            try
            {
                var task = Task.Run(async () =>
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(2));
                    CancellationTokenSource cts = new CancellationTokenSource();
                    location = await Geolocation.GetLocationAsync(request, cts.Token);
                });
            }
            catch (FeatureNotSupportedException fnsEx)
            {
                Serilog.Log.Information($"FeatureNotSupportedException: '{fnsEx}'");
            }
            catch (FeatureNotEnabledException fneEx)
            {
                Serilog.Log.Information($"FeatureNotEnabledException: '{fneEx}'");
            }
            catch (PermissionException pEx)
            {
                Serilog.Log.Information($"PermissionException: '{pEx}'");
            }
            catch (Exception ex)
            {
                Serilog.Log.Information($"Unable to get location: '{ex}'");
            }

            return location;
		}
	}
}