#if ANDROID
using Microsoft.Maui.ApplicationModel;
using System;
using Android.OS;

namespace IndoorCO2MapAppV2.Bluetooth
{
    // Android 12+ Bluetooth Connect permission
    public class AndroidBluetoothConnectPermission : Permissions.BasePlatformPermission
    {

        public override (string androidPermission, bool isRuntime)[] RequiredPermissions
        {
            get
            {

                if (OperatingSystem.IsAndroidVersionAtLeast(31)) 
                {
                    return
                    [

                        (Android.Manifest.Permission.BluetoothConnect, true)
                        
                    ];
                }
                return [];
            }
        }

    }
}
#endif
