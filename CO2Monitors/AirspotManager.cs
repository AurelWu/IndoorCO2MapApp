using IndoorCO2MapAppV2.Bluetooth;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.CO2Monitors
{
    internal class AirspotManager : BaseCO2MonitorManager
    {
        private static readonly Guid SERVICE_UUID = new("6e400001-b5a3-f393-e0a9-e50e24dcca9e");
        private static readonly Guid WRITE_CHARACTERISTIC_UUID = new("6e400002-b5a3-f393-e0a9-e50e24dcca9e");
        private static readonly Guid NOTIFY_CHARACTERISTIC_UUID = new("6e400003-b5a3-f393-e0a9-e50e24dcca9e");

        private IService? _service;
        private ICharacteristic? _writerCharacteristic;
        private ICharacteristic? _notifyCharacteristic;

        private readonly Dictionary<int, AirSpotDataPage> _dataPages = [];

        private CancellationTokenSource? _cts;

        public override async Task<bool> InitializeAsync(IDevice device)
        {
            ActiveDevice = device;
            BLEDeviceManager.ActiveMonitorManager = this;

            _service = await TryGetServiceAsync(device, SERVICE_UUID);
            if (_service == null)
            {
                Console.WriteLine("Airspot service not found.");
                return false;
            }

            _writerCharacteristic = await TryGetCharacteristicAsync(_service, WRITE_CHARACTERISTIC_UUID);
            _notifyCharacteristic = await TryGetCharacteristicAsync(_service, NOTIFY_CHARACTERISTIC_UUID);

            if (!IsGattValid())
            {
                Console.WriteLine("Failed to find required characteristics.");
                return false;
            }

            // Subscribe to notifications
            if(_notifyCharacteristic!= null)
            {
                _notifyCharacteristic.ValueUpdated -= OnNotifyValueChanged;
                _notifyCharacteristic.ValueUpdated += OnNotifyValueChanged;
                await _notifyCharacteristic.StartUpdatesAsync();
            }            


            Console.WriteLine("AirspotManager initialized.");
            return true;
        }

        protected override bool IsGattValid()
        {
            return _service != null && _writerCharacteristic != null && _notifyCharacteristic != null;
        }      

        private void OnNotifyValueChanged(object? sender, CharacteristicUpdatedEventArgs args)
        {
            var data = args.Characteristic.Value;
            if (data == null || data.Length < 4) return;

            // Airspot page response: 0xFF 0xAA 0x0C 0x80
            if (data[0] == 0xFF && data[1] == 0xAA && data[2] == 0x0C && data[3] == 0x80)
            {
                var page = new AirSpotDataPage(data);
                _dataPages[page.PageID] = page;
            }
            // Live data response: 0x?? 0x?? 0x01 0x02
            else if (data[2] == 0x01 && data[3] == 0x02 && data.Length >= 10)
            {
                CurrentCO2Value = (data[8] << 8) | data[9];
            }
        }

        private static byte CalculateChecksum(byte[] command)
        {
            int sum = 0;
            foreach (var b in command) sum += b;
            return (byte)(sum & 0xFF);
        }

        private async Task<bool> SendCommandAsync(byte[] command, CancellationToken token = default)
        {
            if (_writerCharacteristic == null || !_writerCharacteristic.CanWrite)
                return false;

            var fullCommand = command.Concat([CalculateChecksum(command)]).ToArray();
            try
            {
                await _writerCharacteristic.WriteAsync(fullCommand, token);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static byte[] CreateReadPageCommand(ushort pageNumber)
        {
            return
            [
                0xFF, 0xAA, 0x0C, 0x02,
                (byte)(pageNumber >> 8),
                (byte)(pageNumber & 0xFF)
            ];
        }

        public async Task CollectPagesAsync(int pagesToCollectCount = 10)
        {
            if (_writerCharacteristic == null) return;

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            int maxPageId = 16384;
            int currentPage = _dataPages.Values
                .Where(p => p.FinishedPage)
                .Select(p => p.PageID)
                .DefaultIfEmpty(maxPageId - 1)
                .Max();

            var pagesToCollect = new List<ushort>();
            for (int i = 0; i < pagesToCollectCount; i++)
            {
                int page = (currentPage - i + maxPageId) % maxPageId;
                if (_dataPages.TryGetValue(page, out var existingPage) && existingPage.FinishedPage)
                    continue;
                pagesToCollect.Add((ushort)page);
            }

            foreach (var page in pagesToCollect)
            {
                token.ThrowIfCancellationRequested();  // stop if requested
                await SendCommandAsync(CreateReadPageCommand(page), token);
                await Task.Delay(50, token);           // respect cancellation
            }
        }

        protected override async Task<int> DoReadCurrentCO2Async()
        {
            // Send live data request
            if (_writerCharacteristic == null) return 0;

            byte[] command = [0xFF, 0xAA, 0x01, 0x01, 0x01];
            await SendCommandAsync(command);

            // Return the last known value
            await Task.Delay(50);
            return CurrentCO2Value;
        }

        protected override async Task<int> DoReadUpdateIntervalAsync()
        {
            // We assume interval is stored in last byte of live data
            if (_writerCharacteristic == null) return 0;

            byte[] command = [0xFF, 0xAA, 0x01, 0x01, 0x01];
            await SendCommandAsync(command);

            await Task.Delay(50);
            return 0; // TODO: Airspot interval read logic if needed
        }

        protected override async Task<ushort[]?> DoReadHistoryAsync(ushort requestedValues)
        {
            if (_writerCharacteristic == null) return null;

            var allEntries = _dataPages.Values
                .SelectMany(p => p.Timestamps.Zip(p.CO2Values, (ts, co2) => (ts, co2)))
                .Where(x => x.ts != 0xFFFFFFFF)
                .OrderBy(x => x.ts)
                .ToList();

            if (allEntries.Count == 0) return [];

            int takeCount = Math.Min(requestedValues, (ushort)allEntries.Count);
            return [.. allEntries.TakeLast(takeCount).Select(x => (ushort)x.co2)];
        }

        public override async ValueTask DisposeAsync()
        {
            if (_notifyCharacteristic != null)
            {
                _notifyCharacteristic.ValueUpdated -= OnNotifyValueChanged;
                try { await _notifyCharacteristic.StopUpdatesAsync(); } catch { }
            }

            if (_cts != null)
            {
                await _cts.CancelAsync(); // cancel all ongoing operations
                _cts.Dispose();
                _cts = null;
            }

            if (ActiveDevice != null && ActiveDevice.State == DeviceState.Connected)
            {
                try { await BLEDeviceManager.Instance._adapter.DisconnectDeviceAsync(ActiveDevice); } catch { }
            }

            _service = null;
            _writerCharacteristic = null;
            _notifyCharacteristic = null;
            ActiveDevice = null;
        }
    }
}
