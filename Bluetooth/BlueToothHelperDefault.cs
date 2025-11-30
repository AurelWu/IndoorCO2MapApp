#if WINDOWS || !ANDROID && !IOS
using Microsoft.Maui.ApplicationModel;

namespace IndoorCO2MapAppV2.Bluetooth
{
    internal class BluetoothHelperDefault : IBluetoothHelper
    {
        // Windows does not expose BT enable/disable state to apps,
        // so we simply return true.
        public bool CheckIfBTEnabled() => true;

        // No runtime permissions needed on Windows.
        public bool CheckStatus() => true;

        // Always granted.
        public Task<PermissionStatus> RequestAsync()
            => Task.FromResult(PermissionStatus.Granted);

        // Cannot open Windows Bluetooth settings automatically
        // in MAUI, but we can show a message.
        public Task RequestBluetoothEnableAsync()
        {
            return Shell.Current.DisplayAlertAsync(
                "Bluetooth",
                "Bluetooth cannot be controlled from this app on Windows. Please use system settings if needed.",
                "OK");
        }

        // No manifest on Windows to inspect.
        public void EnsureDeclared() { }

        public bool HasPermissionInManifest() => true;
    }
}
#endif
