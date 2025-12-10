#if MACCATALYST
using CoreBluetooth;
using CoreLocation;
using Foundation;
using UIKit;
using Microsoft.Maui.ApplicationModel;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.Bluetooth
{
    internal class BluetoothHelperMacCatalyst : IBluetoothHelper
    {
        readonly CBCentralManager bluetoothManager;
        readonly CLLocationManager locationManager;

        public BluetoothHelperMacCatalyst()
        {
            bluetoothManager = new CBCentralManager();
            locationManager = new CLLocationManager();
        }

        public bool CheckPermissions()
        {
            var status = CBManager.Authorization == CBManagerAuthorization.AllowedAlways
                ? PermissionStatus.Granted
                : PermissionStatus.Denied;

            return status == PermissionStatus.Granted;
        }

        public async Task<PermissionStatus> RequestPermissionsAsync()
        {
            if (CheckPermissions())
                return PermissionStatus.Granted;

            locationManager.RequestWhenInUseAuthorization();

            var tcs = new TaskCompletionSource<PermissionStatus>();
            locationManager.AuthorizationChanged += (sender, args) =>
            {
                if (args.Status == CLAuthorizationStatus.AuthorizedWhenInUse || args.Status == CLAuthorizationStatus.AuthorizedAlways)
                    tcs.SetResult(PermissionStatus.Granted);
                else
                    tcs.SetResult(PermissionStatus.Denied);
            };

            return await tcs.Task;
        }

        public void EnsureDeclared()
        {
            bool hasBluetoothUsageDescription = NSBundle.MainBundle.InfoDictionary.ContainsKey(new NSString("NSBluetoothAlwaysUsageDescription"));
            bool hasLocationWhenInUseUsageDescription = NSBundle.MainBundle.InfoDictionary.ContainsKey(new NSString("NSLocationWhenInUseUsageDescription"));

            if (!hasBluetoothUsageDescription || !hasLocationWhenInUseUsageDescription)
                throw new PermissionException("Bluetooth and/or Location permissions are not set in Info.plist.");
        }

        public async Task RequestBluetoothEnableAsync()
        {       
            if (CheckIfBTEnabled())
            return;

            await Shell.Current.DisplayAlertAsync(
            "Enable Bluetooth",
            "Bluetooth is currently disabled. Please enable it manually in System Settings → Bluetooth.",
            "OK");
        }


        public bool CheckIfBTEnabled() => bluetoothManager.State == CBManagerState.PoweredOn;

        public bool HasPermissionInManifest()
        {
            bool hasBluetoothUsageDescription = NSBundle.MainBundle.InfoDictionary.ContainsKey(new NSString("NSBluetoothAlwaysUsageDescription"));
            bool hasLocationWhenInUseUsageDescription = NSBundle.MainBundle.InfoDictionary.ContainsKey(new NSString("NSLocationWhenInUseUsageDescription"));
            return hasBluetoothUsageDescription && hasLocationWhenInUseUsageDescription;
        }
    }
}
#endif
