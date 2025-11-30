#if ANDROID
using Android.Content;
using AndLoc = Android.Locations ;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.Spatial
{
    internal class LocationServiceAndroid : ILocationService
    {
        public async Task<bool> HasLocationPermissionAsync()
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            return status == PermissionStatus.Granted;
        }

        public async Task<bool> RequestLocationPermissionAsync()
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            return status == PermissionStatus.Granted;
        }

        public async Task<bool> IsGpsEnabledAsync()
        {
            var locationManager = (AndLoc.LocationManager?)Android.App.Application.Context
                .GetSystemService(Context.LocationService);

            bool enabled = locationManager?.IsProviderEnabled(AndLoc.LocationManager.GpsProvider) ?? false;

            return enabled;
        }

        public async Task<bool> ShowEnableGpsDialogAsync()
        {
            // Get the current visible page
            var page = Application.Current!.Windows.FirstOrDefault()?.Page; //Application.Current should never be null, thats why "!" is used so the static analyser is happy.
            if (page == null)
                return false; // should not happen, but safe fallback

            bool result = await page.DisplayAlertAsync(
                "Enable GPS",
                "GPS is currently disabled. Would you like to enable it?",
                "Yes", "No");

            if (result)
            {
                var intent = new Intent(Settings.ActionLocationSourceSettings);
                Platform.CurrentActivity?.StartActivity(intent);
            }

            return result;
        }

        public async Task<Location?> GetCurrentLocationAsync()
        {
            try
            {
                var hasPermission = await HasLocationPermissionAsync();
                if (!hasPermission) return null;

                var gpsEnabled = await IsGpsEnabledAsync();
                if (!gpsEnabled) return null;

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
