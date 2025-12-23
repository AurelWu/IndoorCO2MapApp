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

    private volatile bool _isActive;

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // ================= PACKET PIPELINE =================

    private Channel<byte[]>? _packetChannel;
    private readonly SortedDictionary<int, AirspotDataPage> _pages = new();

    // ================= INITIALIZATION =================

    public override async Task<bool> InitializeAsync(IDevice device)
    {
        Logger.WriteToLog("AirspotProvider|InitializeAsync - start", LogMode.Verbose);

        await DisposeAsync(); // ensure clean state

        ActiveDevice = device;

        _service = await TryGetServiceAsync(device, SERVICE_UUID);
        if (_service == null) return false;

        _writer = await TryGetCharacteristicAsync(_service, WRITE_UUID);
        _notify = await TryGetCharacteristicAsync(_service, NOTIFY_UUID);

        if (_writer == null || _notify == null || !_writer.CanWrite)
            return false;

        _packetChannel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _isActive = true;

        _notify.ValueUpdated += OnNotify;
        try
        {
            await _notify.StartUpdatesAsync();
        }
        catch (Exception e)
        {
            Logger.WriteToLog("Exception during await _notify.StartUpdatesAsync: " + e.Message);
        }
        

        _ = Task.Run(PacketRouterLoop);

        Logger.WriteToLog("AirspotProvider|InitializeAsync - completed", LogMode.Verbose);
        return true;
    }

    protected override bool IsGattValid()
        => _isActive && _writer != null && _notify != null;

    // ================= NOTIFICATIONS =================

    private void OnNotify(object? sender, CharacteristicUpdatedEventArgs e)
    {
        if (!_isActive || _packetChannel == null)
            return;

        var data = e.Characteristic.Value;
        if (data == null || data.Length < 4)
            return;

        _packetChannel.Writer.TryWrite(data);
    }

    // ================= PACKET LOOP =================

    private async Task PacketRouterLoop()
    {
        try
        {
            if (_packetChannel == null)
                return;

            await foreach (var data in _packetChannel.Reader.ReadAllAsync())
            {
                if (!_isActive)
                    break;

                // History page
                if (data[0] == 0xFF && data[1] == 0xAA &&
                    data[2] == 0x0C && data[3] == 0x80)
                {
                    var page = new AirspotDataPage(data);
                    _pages[page.PageID] = page;
                }
                // Live data
                else if (data.Length >= 10 && data[2] == 0x01 && data[3] == 0x02)
                {
                    CurrentCO2Value = (data[8] << 8) | data[9];
                    Logger.WriteToLog("PacketRouterLoop|new CO2Value: " + CurrentCO2Value);
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
        catch (ChannelClosedException)
        {
            Logger.WriteToLog("PacketRouterLoop|Channel closed - expected", LogMode.Verbose);
        }
        catch (Exception ex)
        {
            Logger.WriteToLog($"PacketRouterLoop|Unhandled exception: {ex}", LogMode.Verbose);
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

    private async Task SendAsync(byte[] cmd)
    {
        if (!_isActive || _writer == null)
            return;

        await _writeLock.WaitAsync();
        try
        {
            var packet = cmd.Concat(new[] { Checksum(cmd) }).ToArray();
            await _writer.WriteAsync(packet);
        }
        catch
        {
            // BLE failures are expected
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ================= REQUESTS =================

    private TaskCompletionSource<ushort>? _currentPageTcs;

    private async Task<ushort?> RequestCurrentPageAsync()
    {
        if (!IsGattValid())
            return null;

        _currentPageTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            await SendAsync(CMD_CURRENT_PAGE);

            var completed = await Task.WhenAny(
                _currentPageTcs.Task,
                Task.Delay(TimeSpan.FromSeconds(5)));

            return completed == _currentPageTcs.Task
                ? _currentPageTcs.Task.Result
                : null;
        }
        finally
        {
            _currentPageTcs = null;
        }
    }

    // ================= BASE OVERRIDES =================

    protected override async Task<int> DoReadCurrentCO2Async()
    {
        if (!IsGattValid())
            return CurrentCO2Value;

        await SendAsync(CMD_LIVE);
        await Task.Delay(150);

        return CurrentCO2Value;
    }

    protected override Task<int> DoReadUpdateIntervalAsync()
        => Task.FromResult(-99);

    protected override async Task<ushort[]?> DoReadHistoryAsync(ushort minutes, int interval)
    {
        if (!IsGattValid())
            return null;

        await CollectPagesAsync((minutes / 16) + 2);

        ushort? current = await RequestCurrentPageAsync();
        if (current == null)
            return null;

        const int maxPages = 16384;
        int page = current.Value;

        var newestFirst = new List<ushort>();

        for (int i = 0; i < _pages.Count; i++)
        {
            if (_pages.TryGetValue(page, out var p))
            {
                foreach (var v in p.CO2Values.Where(v => v != 0xFFFF).Reverse())
                    newestFirst.Add((ushort)v);
            }

            page = (page - 1 + maxPages) % maxPages;
        }

        newestFirst.Reverse();

        return newestFirst
            .TakeLast(minutes)
            .ToArray();
    }

    public async Task CollectPagesAsync(int count)
    {
        if (!IsGattValid())
            return;

        ushort? current = await RequestCurrentPageAsync();
        if (current == null)
            return;

        const int maxPages = 16384;

        for (int i = 0; i < count; i++)
        {
            int page = ((int)current - i + maxPages) % maxPages;

            if (_pages.TryGetValue(page, out var p) && p.FinishedPage)
                continue;

            await SendAsync(CMD_READ_PAGE((ushort)page));
            await Task.Delay(50);
        }
    }

    // ================= DISPOSE =================

    public override async ValueTask DisposeAsync()
    {
        if (!_isActive)
            return;

        _isActive = false;

        _packetChannel?.Writer.TryComplete();

        if (_notify != null)
        {
            _notify.ValueUpdated -= OnNotify;

            try
            {
                await _notify.StopUpdatesAsync();
            }
            catch
            {
                // Windows BLE may throw or hang internally
            }
        }

        _service = null;
        _writer = null;
        _notify = null;
        ActiveDevice = null;

        Logger.WriteToLog("AirspotProvider disposed", LogMode.Verbose);
    }
}
