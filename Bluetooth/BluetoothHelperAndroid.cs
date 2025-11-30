#if ANDROID
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.Content;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Application = Android.App.Application;

namespace IndoorCO2MapAppV2.Bluetooth
{
    internal class BluetoothHelperAndroid : IBluetoothHelper
    {
        private readonly BluetoothAdapter? bluetoothAdapter;
        private readonly Context context;

        public BluetoothHelperAndroid()
        {
            context = Application.Context;

            // Android 12+ (API 31+)
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                BluetoothManager? bluetoothManager = context.GetSystemService(Context.BluetoothService) as BluetoothManager ?? throw new Exception("No Bluetooth on Device");
                bluetoothAdapter = bluetoothManager.Adapter;
            }
            else
            {
                // Android <12
                bluetoothAdapter = BluetoothAdapter.DefaultAdapter;
                if (bluetoothAdapter == null)
                {
                    throw new Exception("No Bluetooth on Device");
                }
            }
        }

        // Check if Bluetooth hardware is enabled
        public bool CheckIfBTEnabled() => bluetoothAdapter?.IsEnabled ?? false;

        // Check if all required permissions are granted
        public bool CheckPermissions()
        {
            // Basic permissions
            var permissions = new[]
            {
                Android.Manifest.Permission.Bluetooth,
                Android.Manifest.Permission.AccessFineLocation
            };

            // Add Android 12+ permissions
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                permissions =
                [
                    Android.Manifest.Permission.AccessFineLocation,
                    Android.Manifest.Permission.BluetoothConnect,
                    Android.Manifest.Permission.BluetoothScan
                ];
            }

            foreach (var p in permissions)
            {
                if (ContextCompat.CheckSelfPermission(context, p) != Permission.Granted)
                    return false;
            }

            return true;
        }

        // Request runtime permissions
        public async Task<PermissionStatus> RequestPermissionsAsync()
        {
            if (CheckPermissions())
                return PermissionStatus.Granted;

            // Request Location
            var locationStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            PermissionStatus bluetoothConnectStatus = PermissionStatus.Granted;
            PermissionStatus bluetoothScanStatus = PermissionStatus.Granted;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            {
                bluetoothConnectStatus = await Permissions.RequestAsync<AndroidBluetoothConnectPermission>();
                bluetoothScanStatus = await Permissions.RequestAsync<AndroidBluetoothScanPermission>();
            }

            return (locationStatus == PermissionStatus.Granted &&
                    bluetoothConnectStatus == PermissionStatus.Granted &&
                    bluetoothScanStatus == PermissionStatus.Granted)
                ? PermissionStatus.Granted
                : PermissionStatus.Denied;
        }

        // Ask user to enable Bluetooth with Shell alert
        public async Task RequestBluetoothEnableAsync()
        {
            if (CheckIfBTEnabled())
                return;

            bool result = await Shell.Current.DisplayAlertAsync(
                "Enable Bluetooth",
                "Bluetooth is currently disabled. Would you like to enable it?",
                "Yes",
                "No");

            if (result)
            {
                var enableIntent = new Intent(BluetoothAdapter.ActionRequestEnable);
                enableIntent.AddFlags(ActivityFlags.NewTask);
                context.StartActivity(enableIntent);
            }
        }

        // Ensure required permissions are declared in AndroidManifest.xml
        public void EnsureDeclared()
        {
            if (!HasPermissionInManifest())
                throw new PermissionException("Bluetooth and/or Location permissions are not set in AndroidManifest.xml.");
        }

        // Check manifest for all required permissions
        public bool HasPermissionInManifest()
        {
            if (context == null) return false;

            try
            {
                PackageInfo? packageInfo = null;
                if (context?.PackageManager != null && context.PackageName != null)
                {
                    packageInfo = context.PackageManager.GetPackageInfo(
                        context.PackageName,
                        PackageInfoFlags.Permissions
                    );
                }

                if (packageInfo?.RequestedPermissions == null)
                    return false;

                var requiredPermissions = new[]
                {
            Android.Manifest.Permission.Bluetooth,
            Android.Manifest.Permission.BluetoothAdmin,
            Android.Manifest.Permission.AccessFineLocation
        };

                if (OperatingSystem.IsAndroidVersionAtLeast(31))
                {
                    requiredPermissions =
                    [
                Android.Manifest.Permission.Bluetooth,
                Android.Manifest.Permission.BluetoothAdmin,
                Android.Manifest.Permission.AccessFineLocation,
                Android.Manifest.Permission.BluetoothConnect,
                Android.Manifest.Permission.BluetoothScan
            ];
                }

                foreach (var perm in requiredPermissions)
                {
                    bool found = false;
                    foreach (var requested in packageInfo.RequestedPermissions)
                    {
                        if (requested != null && requested.Equals(perm, System.StringComparison.OrdinalIgnoreCase))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found) return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
#endif
