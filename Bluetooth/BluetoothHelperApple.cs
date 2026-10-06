#if IOS
using CoreBluetooth;
using CoreLocation;
using Foundation;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using UIKit;
using System;
using System.Collections.Generic;
using System.Text;

namespace IndoorCO2MapAppV2.Bluetooth 
{
    internal class BluetoothHelperApple : IBluetoothHelper
    {
        readonly CBCentralManager bluetoothManager;
        readonly CLLocationManager locationManager;

        public BluetoothHelperApple()
        {
            bluetoothManager = new CBCentralManager();
            locationManager = new CLLocationManager();
        }

        public bool CheckPermissions()
        {
            var status = CBManager.Authorization == CBManagerAuthorization.AllowedAlways
            ? PermissionStatus.Granted
            : PermissionStatus.Denied;

            if (status == PermissionStatus.Granted) return true;
            else return false;
        }

        public async Task<PermissionStatus> RequestPermissionsAsync()
        {
            // Check if Bluetooth and Location permissions are already granted
            if (CheckPermissions())
                return PermissionStatus.Granted;

            // Request Location permission
            locationManager.RequestWhenInUseAuthorization();

            // Wait for authorization
            var tcs = new TaskCompletionSource<PermissionStatus>();
            locationManager.AuthorizationChanged += (sender, args) =>
            {
                if (args.Status == CLAuthorizationStatus.AuthorizedWhenInUse || args.Status == CLAuthorizationStatus.AuthorizedAlways)
                {
                    tcs.SetResult(PermissionStatus.Granted);
                }
                else
                {
                    tcs.SetResult(PermissionStatus.Denied);
                }
            };

            return await tcs.Task;
        }

        public void EnsureDeclared()
        {
            // Ensure Bluetooth and Location permissions are declared in Info.plist
            bool hasBluetoothUsageDescription = NSBundle.MainBundle.InfoDictionary.ContainsKey(new NSString("NSBluetoothAlwaysUsageDescription"));
            bool hasLocationWhenInUseUsageDescription = NSBundle.MainBundle.InfoDictionary.ContainsKey(new NSString("NSLocationWhenInUseUsageDescription"));

            if (!hasBluetoothUsageDescription || !hasLocationWhenInUseUsageDescription)
            {
                throw new PermissionException("Bluetooth and/or Location permissions are not set in Info.plist.");
            }
        }

        public async Task RequestBluetoothEnableAsync()
        {
            bool result = await Shell.Current.DisplayAlertAsync(
                "Enable Bluetooth",
                "Bluetooth is currently disabled. Would you like to enable it?",
                "Yes",
                "No");

            if (result)
            {
                var url = new NSUrl("App-Prefs:root=Bluetooth");

                if (UIApplication.SharedApplication.CanOpenUrl(url))
                {
                    UIApplication.SharedApplication.OpenUrl(
                        url,
                        new NSDictionary(),
                        null
                    );
                }
            }
        }

        public bool CheckIfBTEnabled()
        {
            // Either manager reporting "on" counts. This helper's own CBCentralManager and
            // Plugin.BLE's settle independently after launch. The status bar now refreshes on
            // Plugin.BLE's state change, and the scan waits for Plugin.BLE's state — reading
            // only ours could still say Unknown at that moment and skip the scan or show ✗.
            return bluetoothManager.State == CBManagerState.PoweredOn
                || CrossBluetoothLE.Current.State == BluetoothState.On;
        }

        public bool HasPermissionInManifest()
        {
            // Ensure Bluetooth and Location permissions are declared in Info.plist
            bool hasBluetoothUsageDescription = NSBundle.MainBundle.InfoDictionary.ContainsKey(new NSString("NSBluetoothAlwaysUsageDescription"));
            bool hasLocationWhenInUseUsageDescription = NSBundle.MainBundle.InfoDictionary.ContainsKey(new NSString("NSLocationWhenInUseUsageDescription"));

            if (!hasBluetoothUsageDescription || !hasLocationWhenInUseUsageDescription)
            {
                return false;
            }
            else return true;
        }
    }
}
#endif