namespace IndoorCO2MapAppV2.Bluetooth
{
    public interface IBluetoothHelper
    {
        bool CheckIfBTEnabled();
        bool CheckPermissions();
        Task<PermissionStatus> RequestPermissionsAsync();
        Task RequestBluetoothEnableAsync();
        bool HasPermissionInManifest();
        void EnsureDeclared();
    }
}
