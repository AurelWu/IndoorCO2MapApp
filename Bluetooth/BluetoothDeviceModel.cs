using Plugin.BLE.Abstractions.Contracts;

namespace IndoorCO2MapAppV2.Bluetooth
{
    public record BluetoothDeviceModel(IDevice Device)
    {
        public string Name => Device.Name ?? "Unknown";
        public string Id => Device.Id.ToString();       
        public int Rssi => Device.Rssi;
    }
}
