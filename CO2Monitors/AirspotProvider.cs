using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using System.Threading.Channels;

namespace IndoorCO2MapAppV2.CO2Monitors;

internal sealed class AirspotProvider : BaseCO2MonitorProvider
{
    // ================= UUIDs =================

    private static readonly Guid SERVICE_UUID = new("6e400001-b5a3-f393-e0a9-e50e24dcca9e");
    private static readonly Guid WRITE_UUID = new("6e400002-b5a3-f393-e0a9-e50e24dcca9e");
    private static readonly Guid NOTIFY_UUID = new("6e400003-b5a3-f393-e0a9-e50e24dcca9e");

    // ================= BLE =================

    private IService? _service;
    private ICharacteristic? _writer;
    private ICharacteristic? _notify;

    private CancellationTokenSource? _cts;
    private volatile bool _isActive;

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private CancellationToken Token => _cts?.Token ?? CancellationToken.None;

    // ================= PACKET PIPELINE =================

    private readonly Channel<byte[]> _packetChannel =
        Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly SortedDictionary<int, AirspotDataPage> _pages = new();

    // ================= INITIALIZATION =================

    public override async Task<bool> InitializeAsync(IDevice device)
    {
        Logger.WriteToLog("AirspotProvider|InitializeAsync called before DisposeAsync", LogMode.Verbose);
        await DisposeAsync(); // ensure clean state
        Logger.WriteToLog("AirspotProvider|InitializeAsync called, after DisposeAsync", LogMode.Verbose);
        ActiveDevice = device;
        //CO2MonitorManager.Instance.ActiveCO2MonitorProvider = this;

        _service = await TryGetServiceAsync(device, SERVICE_UUID);
        if (_service == null) return false;

        _writer = await TryGetCharacteristicAsync(_service, WRITE_UUID);
        _notify = await TryGetCharacteristicAsync(_service, NOTIFY_UUID);

        if (_writer == null || _notify == null || !_writer.CanWrite)
            return false;

        _cts = new CancellationTokenSource();
        _isActive = true;

        _notify.ValueUpdated += OnNotify;
        await _notify.StartUpdatesAsync();

        _ = Task.Run(() => PacketRouterLoop(_cts.Token));

        Logger.WriteToLog("Airspot initialized",minimumLogMode: LogMode.Verbose);
        return true;
    }

    protected override bool IsGattValid()
        => _isActive && _writer != null && _notify != null;

    // ================= NOTIFICATIONS =================

    private void OnNotify(object? sender, CharacteristicUpdatedEventArgs e)
    {
        if (!_isActive) return;

        var data = e.Characteristic.Value;
        if (data == null || data.Length < 4) return;

        _packetChannel.Writer.TryWrite(data);
    }

    // ================= PROTOCOL LOOP =================

    private async Task PacketRouterLoop(CancellationToken token)
    {
        if (token.IsCancellationRequested)
            return; // if cancellation is already requested before this is called and not during reading the data then we can instantly return

        try
        {
            await foreach (var data in _packetChannel.Reader.ReadAllAsync(token))
            {
                if (!_isActive || token.IsCancellationRequested) break;

                // History page
                if (data[0] == 0xFF && data[1] == 0xAA && data[2] == 0x0C && data[3] == 0x80)
                {
                    var page = new AirspotDataPage(data);
                    _pages[page.PageID] = page;
                }
                // Live data
                else if (data.Length >= 10 && data[2] == 0x01 && data[3] == 0x02)
                {
                    CurrentCO2Value = (data[8] << 8) | data[9];
                }
                // Current page response
                else if (data.Length >= 7 &&
                         data[0] == 0xFF && data[1] == 0xAA &&
                         data[2] == 0x0C && data[3] == 0x01)
                {
                    ushort page = (ushort)((data[4] << 8) | data[5]);
                    _currentPageTcs?.TrySetResult(page);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.WriteToLog("PacketRouterLoop|OperationCanceledException - [expected Behaviour]", LogMode.Verbose);
        }
    }

    // ================= COMMANDS =================

    private static readonly byte[] CMD_LIVE = { 0xFF, 0xAA, 0x01, 0x01, 0x01 };
    private static readonly byte[] CMD_CURRENT_PAGE = { 0xFF, 0xAA, 0x0B, 0x01, 0x00 };

    private static byte[] CMD_READ_PAGE(ushort page) => new[]
    {
        (byte)0xFF, (byte)0xAA, (byte)0x0C, (byte)0x02,
        (byte)(page >> 8), (byte)(page & 0xFF)
    };

    private static byte Checksum(byte[] cmd)
        => (byte)(cmd.Sum(b => b) & 0xFF);

    private async Task SendAsync(byte[] cmd, CancellationToken token)
    {
        if (!_isActive || _writer == null) return;

        await _writeLock.WaitAsync(token);
        try
        {
            var packet = cmd.Concat(new[] { Checksum(cmd) }).ToArray();
            await _writer.WriteAsync(packet, token);
        }
        catch
        {
            // BLE failures are expected — swallow
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ================= REQUESTS =================

    private TaskCompletionSource<ushort>? _currentPageTcs;

    private async Task<ushort?> RequestCurrentPageAsync(CancellationToken token)
    {
        if (!IsGattValid()) return null;

        _currentPageTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            await SendAsync(CMD_CURRENT_PAGE, token);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
            return await _currentPageTcs.Task.WaitAsync(linked.Token);
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

    // ================= BASE OVERRIDES =================

    protected override async Task<int> DoReadCurrentCO2Async()
    {
        if (!IsGattValid()) return CurrentCO2Value;

        await SendAsync(CMD_LIVE, Token);
        await Task.Delay(150, Token);
        return CurrentCO2Value;
    }

    protected override Task<int> DoReadUpdateIntervalAsync()
        => Task.FromResult(-99);

    protected override async Task<ushort[]?> DoReadHistoryAsync(ushort minutes, int interval)
    {
        if (!IsGattValid())
            return null;

        // Collect enough pages to cover requested minutes
        await CollectPagesAsync((minutes / 16) + 2);

        ushort? current = await RequestCurrentPageAsync(Token);
        if (current == null)
            return null;

        const int maxPages = 16384;
        int page = current.Value;

        var newestFirst = new List<ushort>();

        // Iterate pages: newest => oldest
        for (int i = 0; i < _pages.Count; i++)
        {
            if (_pages.TryGetValue(page, out var p))
            {
                // Within page: newest => oldest
                foreach (var v in p.CO2Values
                                    .Where(v => v != 0xFFFF)
                                    .Reverse())
                {
                    newestFirst.Add((ushort)v);
                }
            }

            page = (page - 1 + maxPages) % maxPages;
        }

        // Convert to chronological order: oldest => newest
        newestFirst.Reverse();

        return newestFirst
            .TakeLast(minutes)
            .ToArray();
    }


    public async Task CollectPagesAsync(int count)
    {
        if (!IsGattValid()) return;

        ushort? current = await RequestCurrentPageAsync(Token);
        if (current == null) return;

        const int maxPages = 16384;

        for (int i = 0; i < count; i++)
        {
            int page = ((int)current - i + maxPages) % maxPages;
            if (_pages.TryGetValue(page, out var p) && p.FinishedPage) continue;

            await SendAsync(CMD_READ_PAGE((ushort)page), Token);
            await Task.Delay(50, Token);
        }
    }

    // ================= DISPOSE =================

    public override async ValueTask DisposeAsync()
    {
        Logger.WriteToLog($"AirspotProvider|DisposeAsync called and before initial sanity check: isactive: {_isActive} | cts: {_cts}", LogMode.Verbose);
        if (!_isActive && _cts == null) return;
        Logger.WriteToLog("AirspotProvider|DisposeAsync called and past initial sanity check", LogMode.Verbose);

        _isActive = false;

        await _cts?.CancelAsync();

        if (_notify != null)
        {
            Logger.WriteToLog("AirspotProvider|DisposeAsync called | before unsubscribing from OnNotify", LogMode.Verbose);
            _notify.ValueUpdated -= OnNotify;

            try 
            {
                Logger.WriteToLog("AirspotProvider|DisposeAsync called | before await _notify.StopUpdatesAsync()", LogMode.Verbose);
                await _notify.StopUpdatesAsync();
                Logger.WriteToLog("AirspotProvider|DisposeAsync called | after await _notify.StopUpdatesAsync()", LogMode.Verbose);
            } 
            catch 
            {
                Logger.WriteToLog("AirspotProvider|DisposeAsync called | exception caught during await _notify.StopUpdatesAsync()", LogMode.Verbose);
            }
        }

        _cts?.Dispose();
        _cts = null;

        if (ActiveDevice != null)
        {
            try
            {
                Logger.WriteToLog("AirspotProvider|DisposeAsync called | before await BLEDeviceManager.Instance._adapter.DisconnectDeviceAsync(ActiveDevice); ", LogMode.Verbose);
                await BLEDeviceManager.Instance._adapter
                    .DisconnectDeviceAsync(ActiveDevice);
                Logger.WriteToLog("AirspotProvider|DisposeAsync called | after await BLEDeviceManager.Instance._adapter.DisconnectDeviceAsync(ActiveDevice); ", LogMode.Verbose);
            }
            catch 
            {
                Logger.WriteToLog("AirspotProvider|DisposeAsync called | exception during await BLEDeviceManager.Instance._adapter.DisconnectDeviceAsync(ActiveDevice); ", LogMode.Verbose);
            }
        }

        _service = null;
        _writer = null;
        _notify = null;
        ActiveDevice = null;

        Logger.WriteToLog("Airspot disposed", minimumLogMode: LogMode.Verbose);
    }
}
