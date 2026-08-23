#if ANDROID
using Microsoft.Maui.ApplicationModel;
using System;
using Android.OS;

namespace IndoorCO2MapAppV2.Bluetooth
{
    // Android 13+ Post Notifications permission (needed so the recording
    // foreground service notification is actually visible)
    public class AndroidPostNotificationsPermission : Permissions.BasePlatformPermission
    {

        public override (string androidPermission, bool isRuntime)[] RequiredPermissions
        {
            get
            {

                if (OperatingSystem.IsAndroidVersionAtLeast(33))
                {
                    return
                    [

                        (Android.Manifest.Permission.PostNotifications, true)

                    ];
                }
                return [];
            }
        }

    }
}
#endif
