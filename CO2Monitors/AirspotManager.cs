using IndoorCO2MapAppV2.Bluetooth;
using Microsoft.Maui.Controls;
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

        private readonly Dictionary<int, AirspotDataPage> _dataPages = [];
        private CancellationTokenSource? _cts;

        // TaskCompletionSources for awaiting specific notifications
        private TaskCompletionSource<int>? _liveDataTcs;
        private TaskCompletionSource<ushort>? _currentPageTcs;


        public override async Task<bool> InitializeAsync(IDevice device)
        {
            ActiveDevice = device;
            BLEDeviceManager.ActiveMonitorManager = this;

            _service = await TryGetServiceAsync(device, SERVICE_UUID);
            if (_service == null) return false;

            _writerCharacteristic = await TryGetCharacteristicAsync(_service, WRITE_CHARACTERISTIC_UUID);
            _notifyCharacteristic = await TryGetCharacteristicAsync(_service, NOTIFY_CHARACTERISTIC_UUID);

            if (!IsGattValid()) return false;

            // Subscribe to notifications
            if (_notifyCharacteristic != null)
            {
                _notifyCharacteristic.ValueUpdated -= OnNotifyValueChanged;
                _notifyCharacteristic.ValueUpdated += OnNotifyValueChanged;
                await _notifyCharacteristic.StartUpdatesAsync();
            }

            return true;
        }

        protected override bool IsGattValid() =>
            _service != null && _writerCharacteristic != null && _notifyCharacteristic != null;

        private void OnNotifyValueChanged(object? sender, CharacteristicUpdatedEventArgs args)
        {
            var data = args.Characteristic.Value;
            if (data == null || data.Length < 4) return;

            string hex = Convert.ToHexString(data);
            Console.WriteLine("data: " +hex);
            // Page response
            if (data[0] == 0xFF && data[1] == 0xAA && data[2] == 0x0C && data[3] == 0x80)
            {
                var page = new AirspotDataPage(data);
                _dataPages[page.PageID] = page;
            }
            // Live data response
            else if (data[2] == 0x01 && data[3] == 0x02 && data.Length >= 10)
            {
                int co2 = (data[8] << 8) | data[9];
                CurrentCO2Value = co2;
                _liveDataTcs?.TrySetResult(co2); // safely complete TCS if awaiting
            }
            else if (data[0] == 0xFF && data[1] == 0xAA && data[2] == 0x0C && data[3] == 0x01 && data.Length >= 7)
            {
                ushort page = (ushort)((data[4] << 8) | data[5]);
                _currentPageTcs?.TrySetResult(page);
                return;
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
            if (_writerCharacteristic == null || !_writerCharacteristic.CanWrite) return false;

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

        private static readonly byte[] GetLiveCO2DataCommand =
            [
            0xFF, 0xAA, 0x01, 0x01, 0x01
            ];

        private static readonly byte[] GetCurrentPageNumberCommand =
            [
                0xFF, 0xAA, 0x0B, 0x01, 0x00
            ];

        private static byte[] CreateReadPageCommand(ushort pageNumber) =>
            [
                0xFF, 0xAA, 0x0C, 0x02,
                (byte)(pageNumber >> 8),
                (byte)(pageNumber & 0xFF)
            ];

        public async Task CollectPagesAsync(int pagesToCollectCount = 10)
        {
            if (_writerCharacteristic == null)
                return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            ushort? currentPage = await RequestCurrentPageAsync(token);

            
            int maxPageId = 16384;
            int startPage = currentPage ?? (maxPageId - 1);

            var pagesToCollect = new List<ushort>();

            for (int i = 0; i < pagesToCollectCount; i++)
            {
                int page = (startPage - i + maxPageId) % maxPageId;

                if (_dataPages.TryGetValue(page, out var existingPage) && existingPage.FinishedPage)
                    continue;

                pagesToCollect.Add((ushort)page);
            }

            foreach (var page in pagesToCollect)
            {
                token.ThrowIfCancellationRequested();
                await SendCommandAsync(CreateReadPageCommand(page), token);
                await Task.Delay(50, token);
            }
        }

        protected override async Task<int> DoReadCurrentCO2Async()
        {
            if (_writerCharacteristic == null) return 0;

            _cts ??= new CancellationTokenSource();
            var token = _cts.Token;

            _liveDataTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            byte[] command = GetLiveCO2DataCommand;
            await SendCommandAsync(command, token);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

            try
            {
                if(_liveDataTcs != null)
                {
                    return await _liveDataTcs.Task.WaitAsync(linkedCts.Token);
                }
                else
                {
                    return CurrentCO2Value;
                }
            }
            catch
            {
                return CurrentCO2Value; // fallback on timeout or cancellation
            }
            finally
            {
                _liveDataTcs = null;
            }
        }

        protected override async Task<int> DoReadUpdateIntervalAsync()
        {
            //TODO: check if airspot directly exposes that info, if not we can still infer from the history
            //return await DoReadCurrentCO2Async();
            return 0;
        }

        protected override async Task<ushort[]?> DoReadHistoryAsync(ushort amountOfMinutes)
        {
            //TODO: convert from minutes to amount of datapoints based on interval between timestamps
            if (_writerCharacteristic == null) return null;

            // Fetch the latest pages before building history
            await CollectPagesAsync(10); // for now hardcoded to 10 => should eventually be dynamic based on time since recording start and interval (which we can get from timestamps)

            var allEntries = _dataPages.Values
                .SelectMany(p => p.Timestamps.Zip(p.CO2Values, (ts, co2) => (ts, co2)))
                .Where(x => x.ts != 0xFFFFFFFF)
                .OrderBy(x => x.ts)
                .ToList();

            if (allEntries.Count == 0) return [];

            int takeCount = Math.Min(amountOfMinutes, (ushort)allEntries.Count);
            return [.. allEntries.TakeLast(takeCount).Select(x => (ushort)x.co2)];
        }

        private async Task<ushort?> RequestCurrentPageAsync(CancellationToken token)
        {
            if (_writerCharacteristic == null)
                return null;

            var tcs = new TaskCompletionSource<ushort>(TaskCreationOptions.RunContinuationsAsynchronously);
            _currentPageTcs = tcs;

            await SendCommandAsync(GetCurrentPageNumberCommand, token);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);

            try
            {
                return await tcs.Task.WaitAsync(linked.Token);
            }
            catch
            {
                return null;
            }
            finally
            {
                _currentPageTcs = null;
            }
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
                await _cts.CancelAsync(); 
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
