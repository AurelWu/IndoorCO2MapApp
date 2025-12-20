using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using System.Threading.Channels;

namespace IndoorCO2MapAppV2.CO2Monitors;

internal sealed class AirspotProvider : BaseCO2MonitorProvider
{
    private static readonly Guid SERVICE_UUID = new("6e400001-b5a3-f393-e0a9-e50e24dcca9e");
    private static readonly Guid WRITE_CHARACTERISTIC_UUID = new("6e400002-b5a3-f393-e0a9-e50e24dcca9e");
    private static readonly Guid NOTIFY_CHARACTERISTIC_UUID = new("6e400003-b5a3-f393-e0a9-e50e24dcca9e");

    private IService? _service;
    private ICharacteristic? _writer;
    private ICharacteristic? _notify;

    private CancellationTokenSource? _cts;

    private bool _notificationSetupDone = false;

    private CancellationToken Token => _cts?.Token ?? CancellationToken.None;

    // ================= PACKET PIPELINE =================

    private readonly Channel<byte[]> _packetChannel =
        Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly Dictionary<int, AirspotDataPage> _pages = new();

    // ================= INITIALIZATION =================

    public override async Task<bool> InitializeAsync(IDevice device)
    {
        //return true; //for debugging: return true so connection stays active but dont do anything (no notification etc) for testing purposes
        if (_notificationSetupDone && IsGattValid()) return true; //already initialized so can skip
        ActiveDevice = device;
        CO2MonitorManager.Instance.ActiveCO2MonitorProvider = this;

        _service = await TryGetServiceAsync(device, SERVICE_UUID);
        if (_service == null) return false;

        _writer = await TryGetCharacteristicAsync(_service, WRITE_CHARACTERISTIC_UUID);
        _notify = await TryGetCharacteristicAsync(_service, NOTIFY_CHARACTERISTIC_UUID);

        if (_writer == null || _notify == null || !_writer.CanWrite)
            return false;

        _notify.ValueUpdated -= OnNotify;
        _notify.ValueUpdated += OnNotify;
        await _notify.StartUpdatesAsync();

        _cts = new CancellationTokenSource();
        _ = Task.Run(() => PacketRouterLoop(_cts.Token));

        Logger.WriteToLog("Airspot initialized and protocol loop started");
        _notificationSetupDone = true;
        return true;
    }

    protected override bool IsGattValid() =>
        _service != null &&
        _writer != null &&
        _notify != null &&
        _writer.CanWrite;

    // ================= NOTIFICATIONS =================

    private void OnNotify(object? sender, CharacteristicUpdatedEventArgs e)
    {
        var data = e.Characteristic.Value;
        if (data == null || data.Length < 4) return;

        _packetChannel.Writer.TryWrite(data);
    }

    // ================= PROTOCOL LOOP =================

    private async Task PacketRouterLoop(CancellationToken token)
    {
        try
        {
            await foreach (var data in _packetChannel.Reader.ReadAllAsync(token))
            {
                // History page
                if (data[0] == 0xFF && data[1] == 0xAA && data[2] == 0x0C && data[3] == 0x80)
                {
                    var page = new AirspotDataPage(data);
                    _pages[page.PageID] = page;
                    Logger.WriteToLog($"Airspot History Page {page.PageID} | {Convert.ToHexString(data)}", minimumLogMode: LogMode.Verbose);
                }
                // Live data
                else if (data[2] == 0x01 && data[3] == 0x02 && data.Length >= 10)
                {
                    CurrentCO2Value = (data[8] << 8) | data[9];
                    Logger.WriteToLog($"Airspot Live CO2 = {CurrentCO2Value}", minimumLogMode: LogMode.Verbose);
                }
                // Current page response
                else if (data[0] == 0xFF && data[1] == 0xAA && data[2] == 0x0C && data[3] == 0x01 && data.Length >= 7)
                {
                    //TODO check if second last byte is 0000 (rather than 0004 timesync or anything else) - also should have not only >=7 but defined length
                    ushort currentPage = (ushort)((data[4] << 8) | data[5]);
                    _currentPageTcs?.TrySetResult(currentPage);
                    Logger.WriteToLog($"Airspot Current Page = {currentPage}", minimumLogMode: LogMode.Verbose);
                }
                else
                {
                    Logger.WriteToLog("Airspot Other Packet | " + Convert.ToHexString(data), minimumLogMode: LogMode.Verbose);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.WriteToLog("Airspot protocol loop cancelled");
        }
    }

    // ================= COMMANDS =================

    private static readonly byte[] CMD_LIVE = { 0xFF, 0xAA, 0x01, 0x01, 0x01 };
    private static readonly byte[] CMD_CURRENT_PAGE = { 0xFF, 0xAA, 0x0B, 0x01, 0x00 };

    private static byte[] CMD_READ_PAGE(ushort page) => new byte[]
    {
        0xFF, 0xAA, 0x0C, 0x02,
        (byte)(page >> 8),
        (byte)(page & 0xFF)
    };

    private static byte Checksum(byte[] cmd)
        => (byte)(cmd.Sum(b => b) & 0xFF);

    private async Task SendAsync(byte[] cmd, CancellationToken token)
    {
        if (_writer == null)
            throw new InvalidOperationException("BLE not initialized");

        var full = cmd.Concat(new[] { Checksum(cmd) }).ToArray();
        Logger.WriteToLog("Writing CMD to log |" + Convert.ToHexString(full), minimumLogMode: LogMode.Verbose);
        await _writer.WriteAsync(full, token);
    }

    // ================= REQUESTS =================

    private TaskCompletionSource<ushort>? _currentPageTcs;

    private async Task<ushort?> RequestCurrentPageAsync(CancellationToken token)
    {
        if (!IsGattValid()) return null;
        if (_currentPageTcs != null)
            throw new InvalidOperationException("Current page request already active");

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
            Logger.WriteToLog("Airspot current page request timed out", minimumLogMode: LogMode.Verbose);
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
        => Task.FromResult(-99); //for now we just return a negative value to show its not implemented

    protected override async Task<ushort[]?> DoReadHistoryAsync(ushort minutes, int interval)
    {
        if (!IsGattValid()) return null;

        // Collect pages starting from newest
        await CollectPagesAsync((minutes/16)+2);

        const int maxPages = 16384;
        var entries = new List<ushort>();

        // Find newest page
        ushort? current = await RequestCurrentPageAsync(Token);
        if (current == null) return null;

        int page = current.Value;

        for (int i = 0; i < _pages.Count; i++)
        {
            if (_pages.TryGetValue(page, out var p))
            {
                var reversed = p.CO2Values
                .Where(c => (ushort)c != 0xFFFF)   // removes values from unfinished current page set to FFFF
                .Reverse()                 // reversed order, newest first
                .ToArray();                // or .ToList() if you want a list
               
                // Reverse values in page to get newest first
                foreach (var c in reversed)
                {
                    if (c != 0xFFFF) entries.Add((ushort)c);
                }
            }

            // Move to previous page with rollover
            page = (page - 1 + maxPages) % maxPages;
        }
        entries.Reverse(); // now we reverse again so newest is last
        // Return only requested number of minutes
        return entries.TakeLast(minutes).ToArray();
    }

    public async Task CollectPagesAsync(int count)
    {
        //return; //exit early for testing
        
        if (!IsGattValid()) return;

        ushort? current = await RequestCurrentPageAsync(Token);
        if (current == null) return;

        const int maxPages = 16384;
        //return; // 
        for (int i = 0; i < count; i++)
        {
            int page = ((int)current - i + maxPages) % maxPages;
            Logger.WriteToLog("Sending read page command for page: " + page,minimumLogMode: LogMode.Verbose);
            if (_pages.TryGetValue(page, out var p) && p.FinishedPage) continue;
            await SendAsync(CMD_READ_PAGE((ushort)page), Token);
            await Task.Delay(50, Token);
        }
    }

    // ================= DISPOSE =================

    public override async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (_notify != null)
        {
            _notify.ValueUpdated -= OnNotify;
            try { await _notify.StopUpdatesAsync(); } catch { }
        }

        if (ActiveDevice != null)
        {
            try { await BLEDeviceManager.Instance._adapter.DisconnectDeviceAsync(ActiveDevice); } catch { }
        }

        _service = null;
        _writer = null;
        _notify = null;
        ActiveDevice = null;        
        Logger.WriteToLog("Airspot disposed", minimumLogMode: LogMode.Verbose);
    }
}
