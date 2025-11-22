using IndoorCO2MapAppV2.Bluetooth;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

internal abstract class BaseCO2MonitorManager
{
    protected const int RetryCount = 3;
    protected const int RetryDelayMs = 100;

    public IDevice? ActiveDevice { get; protected set; }

    public int CurrentCO2Value { get; protected set; }

    public bool IsInitialized =>
        ActiveDevice != null &&
        ActiveDevice.State == DeviceState.Connected &&
        IsGattValid();

    protected abstract bool IsGattValid();

    public async Task<bool> EnsureConnectionIsValidAsync()
    {
        // 1. If valid, nothing to do
        if (IsInitialized)
            return true;

        // If no device assigned, cannot recover
        if (ActiveDevice == null)
            return false;

        // 2. Try reconnect if needed
        if (ActiveDevice.State != DeviceState.Connected)
        {
            try
            {
                await BLEDeviceManager.Instance._adapter.ConnectToDeviceAsync(ActiveDevice);
            }
            catch
            {
                return false;
            }
        }

        // 3. Run initialization again
        return await InitializeAsync(ActiveDevice);
    }

    protected static async Task<IService?> TryGetServiceAsync(IDevice device, Guid uuid)
    {
        for (int i = 0; i < RetryCount; i++)
        {
            var svc = await device.GetServiceAsync(uuid);
            if (svc != null) return svc;
            await Task.Delay(RetryDelayMs);
        }
        return null;
    }

    protected static async Task<ICharacteristic?> TryGetCharacteristicAsync(IService service, Guid uuid)
    {
        for (int i = 0; i < RetryCount; i++)
        {
            var chr = await service.GetCharacteristicAsync(uuid);
            if (chr != null) return chr;
            await Task.Delay(RetryDelayMs);
        }
        return null;
    }

    public abstract Task<bool> InitializeAsync(IDevice device);
    public abstract Task<int> ReadCurrentCO2Async();
    public abstract Task<ushort[]> ReadHistoryAsync(ushort startIndex);
}

