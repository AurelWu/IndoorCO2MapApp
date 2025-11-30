#if WINDOWS || MACCATALYST
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.Spatial
{
    internal class LocationServiceDefault : ILocationService
    {
        public Task<bool> HasLocationPermissionAsync() => Task.FromResult(true);
        public Task<bool> RequestLocationPermissionAsync() => Task.FromResult(true);
        public Task<bool> IsGpsEnabledAsync() => Task.FromResult(false);
        public Task<bool> ShowEnableGpsDialogAsync() => Task.FromResult(false);
        public Task<Location?> GetCurrentLocationAsync() => Task.FromResult<Location?>(null);
    }
}
#endif
