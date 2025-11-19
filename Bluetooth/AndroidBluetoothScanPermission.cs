#if ANDROID
using Android.Bluetooth;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Application = Android.App.Application;

namespace IndoorCO2MapAppV2.Bluetooth
{
    // Android 12+ Bluetooth Scan permission
    public class AndroidBluetoothScanPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions
        {
            get
            {
                if (OperatingSystem.IsAndroidVersionAtLeast(31))
                {
                    return
                    [
                        (Android.Manifest.Permission.BluetoothScan, true)
                    ];
                }

                return [];
            }
        }
    }
}
#endif