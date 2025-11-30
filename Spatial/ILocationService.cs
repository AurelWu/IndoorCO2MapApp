using Microsoft.Maui.Devices.Sensors;


namespace IndoorCO2MapAppV2.Spatial
{
    public interface ILocationService
    {
        Task<bool> IsGpsEnabledAsync();             // Checks if GPS/location services are ON
        Task<bool> HasLocationPermissionAsync();    // Checks if the app has permission
        Task<bool> ShowEnableGpsDialogAsync();      // Prompts user to enable GPS if off
        Task<bool> RequestLocationPermissionAsync(); // Requests location permission
        Task<Location?> GetCurrentLocationAsync();  // Gets current location (optional, can return null)
    }
}
