#if IOS
using CoreLocation;
using Foundation;
using Microsoft.Maui.Devices.Sensors;
using UIKit;
using System;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.Spatial
{
    internal class LocationServiceApple : ILocationService
    {
        CLLocationManager locationManager;

        public LocationServiceApple()
        {
            locationManager = new CLLocationManager
            {
                DesiredAccuracy = CLLocation.AccuracyBest,
                ActivityType = CLActivityType.Other
            };
        }

        public Task<bool> HasLocationPermissionAsync()
        {
            // Use the instance property (required for iOS 14+)
            var status = locationManager.AuthorizationStatus;

            bool granted =
                status == CLAuthorizationStatus.AuthorizedAlways ||
                status == CLAuthorizationStatus.AuthorizedWhenInUse;

            return Task.FromResult(granted);
        }

        public async Task<bool> RequestLocationPermissionAsync()
        {
            if (!await HasLocationPermissionAsync())
            {
                locationManager.RequestWhenInUseAuthorization();
            }
            return await HasLocationPermissionAsync();
        }

        public Task<bool> IsGpsEnabledAsync()
        {
            return Task.FromResult(CLLocationManager.LocationServicesEnabled);
        }

        public async Task<bool> ShowEnableGpsDialogAsync()
        {
            if (!CLLocationManager.LocationServicesEnabled)
            {
                var page = Application.Current!.Windows[0]!.Page!;

                bool result = await page.DisplayAlertAsync(
                    "Enable GPS",
                    "Location services are disabled. Please enable GPS in Settings.",
                    "Open Settings",
                    "Cancel");

                if (result)
                {
                    await UIApplication.SharedApplication.OpenUrlAsync(
                        new NSUrl(UIApplication.OpenSettingsUrlString),
                        new UIApplicationOpenUrlOptions()
                    );
                }

                return result;
            }

            return true;
        }

        public async Task<Location?> GetCurrentLocationAsync()
        {
            try
            {
                if (!await HasLocationPermissionAsync() || !CLLocationManager.LocationServicesEnabled)
                    return null;

                var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
                return await Geolocation.Default.GetLocationAsync(request);
            }
            catch
            {
                return null;
            }
        }
    }
}
#endif
