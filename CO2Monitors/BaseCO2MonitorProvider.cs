using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.DebugTools;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

public abstract class BaseCO2MonitorProvider : IAsyncDisposable
{
    protected const int RetryCount = 3;
    protected const int RetryDelayMs = 100;

    public IDevice? ActiveDevice { get; protected set; }

    public int CurrentCO2Value { get; protected set; }

    //public int MeasurementIntervalInSeconds { get; protected set; } = 60;

    public bool IsInitialized =>
        ActiveDevice != null &&
        ActiveDevice.State == DeviceState.Connected &&
        IsGattValid();

    protected abstract bool IsGattValid();

    public async Task<bool> EnsureConnectionIsValidAsync()
    {
        bool debugGattCheck = IsGattValid();
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

        // 3. Run initialization again || right now for airspot this will early exit if its still the same Provider once its been initialized once
        return await InitializeAsync(ActiveDevice);
    }

    protected static async Task<IService?> TryGetServiceAsync(IDevice device, Guid uuid)
    {
        if (device == null) return null;
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
            ICharacteristic? chr;
            try
            {
                chr = await service.GetCharacteristicAsync(uuid);
                if (chr != null) return chr;
            }
            catch (Exception e)
            {
                Logger.WriteToLog("failed to get characteristic: " + e.ToString());
            }
            
            await Task.Delay(RetryDelayMs);
        }
        return null;
    }

    public abstract Task<bool> InitializeAsync(IDevice device);
    public async Task<int> ReadCurrentCO2SafeAsync()
    {
        if (!await EnsureConnectionIsValidAsync())
            return 0;

        return await DoReadCurrentCO2Async();
    }

    protected abstract Task<int> DoReadCurrentCO2Async();

    public async Task<int> ReadUpdateIntervalSafeAsync()
    {
        if (!await EnsureConnectionIsValidAsync())
            return -1;

        return await DoReadUpdateIntervalAsync();
    }

    protected abstract Task<int> DoReadUpdateIntervalAsync();


    public async Task<ushort[]?> ReadHistorySafeAsync(ushort amountOfMinutes, int sensorUpdateInterval)
    {
        if (!await EnsureConnectionIsValidAsync())
            return null;

        return await DoReadHistoryAsync(amountOfMinutes, sensorUpdateInterval);
    }
    protected abstract Task<ushort[]?> DoReadHistoryAsync(ushort startIndex, int sensorUpdateInterval);

    public virtual ValueTask DisposeAsync()
    {
        // Some implementations might need cleanup (e.g., notifications)
        return ValueTask.CompletedTask;
    }
}

