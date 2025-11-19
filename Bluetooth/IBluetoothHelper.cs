namespace IndoorCO2MapAppV2.Bluetooth
{
    public interface IBluetoothHelper
    {
        bool CheckIfBTEnabled();
        bool CheckStatus();
        Task<PermissionStatus> RequestAsync();
        Task RequestBluetoothEnableAsync();
        bool HasPermissionInManifest();
        void EnsureDeclared();
    }
}
