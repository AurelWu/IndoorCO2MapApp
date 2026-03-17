using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace IndoorCO2MapAppV2.CO2Monitors;

// =====================================================================
// STRATEGY
// - No CMD_LIVE sent — sensor pushes live CO2 every ~5s on its own
// - Current page number requested exactly ONCE per measurement session
// - On first DoReadHistoryAsync call: backfills historical pages so
//   pre-recording / resume works correctly. Assumes 1 min per entry,
//   16 entries per page = ~16 min per page.
// - Page advancement tracked locally after backfill
// - History uses Unix timestamps as keys:
//     • Deduplicates re-reads of the same page naturally
//     • Filters out button-press entries (gap < 60s to previous entry)
//     • Always sorted correctly regardless of page order
// - History survives BLE reconnects (same device)
// =====================================================================
internal sealed class AirspotProvider : BaseCO2MonitorProvider
{
    // ================= UUIDs =================

    private static readonly Guid SERVICE_UUID = new("6e400001-b5a3-f393-e0a9-e50e24dcca9e");
    private static readonly Guid WRITE_UUID = new("6e400002-b5a3-f393-e0a9-e50e24dcca9e");
    private static readonly Guid NOTIFY_UUID = new("6e400003-b5a3-f393-e0a9-e50e24dcca9e");

    // ================= CONSTANTS =================

    private const int RequestTimeoutMs = 5_000;
    private const int RetryCount = 3;
    private const int ResponseCooldownMs = 300;
    private const int CO2_SENTINEL = 0xFFFF;

    // Assumed interval between sensor history entries in minutes.
    // TODO: read from sensor once the command is known.
    private const int AssumedEntryIntervalMinutes = 1;

    // Number of entries per flash page (fixed by sensor firmware).
    private const int EntriesPerPage = 16;

    // Minutes of history covered by a single full page.
    private const int MinutesPerPage = EntriesPerPage * AssumedEntryIntervalMinutes; // = 16

    // Minimum gap between consecutive entries to filter out button presses.
    private const int MinEntryGapSeconds = 60;

    // ================= BLE =================

    private IService? _service;
    private ICharacteristic? _writer;
    private ICharacteristic? _notify;

    private volatile bool _isActive;
    private string? _activeDeviceId;

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private readonly SemaphoreSlim _initLock = new(1, 1);

    // ================= PACKET PIPELINE =================

    private Channel<byte[]>? _packetChannel;
    private Task? _packetRouterTask;

    // ================= PAGE TRACKING =================
    // Survive BLE reconnects — only cleared on new device / full dispose.

    private ushort? _startPage;           // page when measurement began (set once)
    private ushort _currentTrackedPage;  // page we are actively polling
    private bool _backfillDone;        // true once historical backfill is complete

    // Full time series: SortedDictionary<Unix timestamp, CO2 ppm>
    // Timestamp key ensures deduplication and correct ordering.
    private readonly SortedDictionary<uint, int> _allEntries = new();
    private readonly object _entriesLock = new();

    private readonly ConcurrentDictionary<int, TaskCompletionSource<AirspotDataPage>> _pendingPageRequests = new();

    private volatile bool _historyReadInProgress;

    // ================= REQUEST STATE =================

    private volatile TaskCompletionSource<ushort>? _currentPageTcs;

    // ================= INITIALIZATION =================

    public override async Task<bool> InitializeAsync(IDevice device)
    {
        Logger.WriteToLog("AirspotProvider|InitializeAsync - start", LogMode.Verbose);

        await _initLock.WaitAsync();
        try
        {
            string deviceId = device.Id.ToString();

            if (_isActive &&
                ActiveDevice != null &&
                _activeDeviceId == deviceId &&
                _service != null &&
                _writer != null &&
                _notify != null)
            {
                Logger.WriteToLog("AirspotProvider|InitializeAsync - reusing existing session", LogMode.Verbose);
                return true;
            }

            bool isReconnect = _activeDeviceId == deviceId && _startPage != null;

            if (_isActive)
            {
                Logger.WriteToLog(isReconnect
                    ? "AirspotProvider|InitializeAsync - reconnecting same device, preserving history"
                    : "AirspotProvider|InitializeAsync - new device, full reset",
                    LogMode.Verbose);

                await TearDownBleAsync(clearHistory: !isReconnect);
            }

            ActiveDevice = device;
            _activeDeviceId = deviceId;

            _service = await TryGetServiceAsync(device, SERVICE_UUID);
            if (_service == null)
            {
                Logger.WriteToLog("AirspotProvider|InitializeAsync - service not found", LogMode.Verbose);
                return false;
            }

            _writer = await TryGetCharacteristicAsync(_service, WRITE_UUID);
            _notify = await TryGetCharacteristicAsync(_service, NOTIFY_UUID);

            if (_writer == null || _notify == null || !_writer.CanWrite)
            {
                Logger.WriteToLog("AirspotProvider|InitializeAsync - writer/notify characteristic invalid", LogMode.Verbose);
                return false;
            }

            _packetChannel = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            _historyReadInProgress = false;
            _currentPageTcs = null;
            _pendingPageRequests.Clear();

            _notify.ValueUpdated += OnNotify;

            try
            {
                await _notify.StartUpdatesAsync();
            }
            catch (Exception e)
            {
                Logger.WriteToLog("AirspotProvider|InitializeAsync - StartUpdatesAsync exception: " + e.Message, LogMode.Verbose);
                _notify.ValueUpdated -= OnNotify;
                _service = null;
                _writer = null;
                _notify = null;
                ActiveDevice = null;
                _activeDeviceId = null;
                return false;
            }

            _isActive = true;
            _packetRouterTask = Task.Run(PacketRouterLoop);

            // No CMD_LIVE — sensor pushes live CO2 automatically on connect.

            if (isReconnect)
                Logger.WriteToLog($"AirspotProvider|InitializeAsync - reconnected, resuming from page {_currentTrackedPage}, {_allEntries.Count} entries preserved", LogMode.Verbose);
            else
                Logger.WriteToLog("AirspotProvider|InitializeAsync - completed", LogMode.Verbose);

            return true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    protected override bool IsGattValid()
        => _isActive && _writer != null && _notify != null && _service != null;

    // ================= NOTIFICATIONS =================

    private void OnNotify(object? sender, CharacteristicUpdatedEventArgs e)
    {
        if (!_isActive || _packetChannel == null)
            return;

        var data = e.Characteristic.Value;
        if (data == null || data.Length < 4)
            return;

        Logger.WriteToLog($"OnNotify|raw ({data.Length}b): {BitConverter.ToString(data)}", LogMode.Verbose);
        _packetChannel.Writer.TryWrite(data);
    }

    // ================= PACKET ROUTER =================

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

                RoutePacket(data);
            }
        }
        catch (ChannelClosedException)
        {
            Logger.WriteToLog("PacketRouterLoop|channel closed – expected", LogMode.Verbose);
        }
        catch (OperationCanceledException)
        {
            Logger.WriteToLog("PacketRouterLoop|operation cancelled – expected", LogMode.Verbose);
        }
        catch (Exception ex)
        {
            Logger.WriteToLog($"PacketRouterLoop|unhandled exception: {ex}", LogMode.Verbose);
        }
    }

    private void RoutePacket(byte[] data)
    {
        // ── History page: FF AA 0C 80 ─────────────────────────────────────
        if (data.Length >= 4 &&
            data[0] == 0xFF && data[1] == 0xAA &&
            data[2] == 0x0C && data[3] == 0x80)
        {
            RouteHistoryPage(data);
            return;
        }

        // ── Current page response: FF AA 0C 01 ───────────────────────────
        if (data.Length >= 7 &&
            data[0] == 0xFF && data[1] == 0xAA &&
            data[2] == 0x0C && data[3] == 0x01)
        {
            RouteCurrentPage(data);
            return;
        }

        // ── Live CO2 push: FF AA 01 02 ────────────────────────────────────
        if (data.Length >= 10 && data[2] == 0x01 && data[3] == 0x02)
        {
            RouteLiveCO2(data);
            return;
        }

        // ── Sensor status push: FF AA 20 01 ──────────────────────────────
        if (data.Length >= 5 &&
            data[0] == 0xFF && data[1] == 0xAA &&
            data[2] == 0x20 && data[3] == 0x01)
        {
            Logger.WriteToLog($"PacketRouterLoop|sensor status – state=0x{data[4]:X2}", LogMode.Verbose);
            return;
        }

        Logger.WriteToLog($"PacketRouterLoop|unhandled: {BitConverter.ToString(data)}", LogMode.Verbose);
    }

    private void RouteHistoryPage(byte[] data)
    {
        try
        {
            var page = new AirspotDataPage(data);

            if (_pendingPageRequests.TryRemove(page.PageID, out var tcs))
                tcs.TrySetResult(page);
            else
                Logger.WriteToLog($"PacketRouterLoop|history page {page.PageID} – no pending TCS", LogMode.Verbose);
        }
        catch (Exception ex)
        {
            Logger.WriteToLog($"PacketRouterLoop|failed to parse history page: {ex.Message}", LogMode.Verbose);
            Logger.WriteToLog($"PacketRouterLoop|raw: {BitConverter.ToString(data)}", LogMode.Verbose);
        }
    }

    private void RouteCurrentPage(byte[] data)
    {
        ushort page = (ushort)((data[4] << 8) | data[5]);
        Logger.WriteToLog($"PacketRouterLoop|current page response: {page}", LogMode.Verbose);

        var tcs = _currentPageTcs;
        if (tcs != null && !tcs.Task.IsCompleted)
            tcs.TrySetResult(page);
        else
            Logger.WriteToLog("PacketRouterLoop|current page – no active TCS (dropped)", LogMode.Verbose);
    }

    private void RouteLiveCO2(byte[] data)
    {
        int value = (data[8] << 8) | data[9];

        if (value == CO2_SENTINEL)
        {
            Logger.WriteToLog("PacketRouterLoop|live CO2 sentinel – sensor measurement cycle", LogMode.Verbose);
            return;
        }

        CurrentCO2Value = value;
        Logger.WriteToLog($"PacketRouterLoop|live CO2: {value} ppm", LogMode.Verbose);
    }

    // ================= COMMANDS =================

    private static readonly byte[] CMD_CURRENT_PAGE = { 0xFF, 0xAA, 0x0B, 0x01, 0x00 };

    private static byte[] CMD_READ_PAGE(ushort page) => new[]
    {
        (byte)0xFF, (byte)0xAA, (byte)0x0C, (byte)0x02,
        (byte)(page >> 8), (byte)(page & 0xFF)
    };

    private static byte Checksum(byte[] cmd)
        => (byte)(cmd.Sum(b => b) & 0xFF);

    private async Task SendAsync(byte[] cmd, string label)
    {
        if (!_isActive || _writer == null)
            return;

        await _writeLock.WaitAsync();
        try
        {
            var packet = cmd.Concat(new[] { Checksum(cmd) }).ToArray();
            Logger.WriteToLog($"SendAsync|[{label}]: {BitConverter.ToString(packet)}", LogMode.Verbose);
            await _writer.WriteAsync(packet);
        }
        catch (Exception ex)
        {
            Logger.WriteToLog($"SendAsync|[{label}] failed: {ex.Message}", LogMode.Verbose);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ================= REQUESTS =================

    private async Task<ushort?> RequestCurrentPageAsync()
    {
        if (!IsGattValid())
            return null;

        await _requestLock.WaitAsync();
        try
        {
            for (int attempt = 1; attempt <= RetryCount; attempt++)
            {
                var tcs = new TaskCompletionSource<ushort>(TaskCreationOptions.RunContinuationsAsynchronously);
                _currentPageTcs = tcs;

                try
                {
                    Logger.WriteToLog($"RequestCurrentPageAsync|attempt {attempt}", LogMode.Verbose);
                    await SendAsync(CMD_CURRENT_PAGE, "CMD_CURRENT_PAGE");

                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(RequestTimeoutMs));

                    if (completed == tcs.Task)
                    {
                        ushort result = await tcs.Task;
                        Logger.WriteToLog($"RequestCurrentPageAsync|got page {result}", LogMode.Verbose);
                        await Task.Delay(ResponseCooldownMs);
                        return result;
                    }

                    Logger.WriteToLog($"RequestCurrentPageAsync|timeout attempt {attempt}", LogMode.Verbose);
                    tcs.TrySetCanceled();
                    await Task.Delay(ResponseCooldownMs);
                }
                finally
                {
                    Interlocked.CompareExchange(ref _currentPageTcs, null, tcs);
                }
            }

            return null;
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private async Task<AirspotDataPage?> RequestPageAsync(ushort page)
    {
        if (!IsGattValid())
            return null;

        await _requestLock.WaitAsync();
        try
        {
            for (int attempt = 1; attempt <= RetryCount; attempt++)
            {
                var tcs = new TaskCompletionSource<AirspotDataPage>(TaskCreationOptions.RunContinuationsAsynchronously);

                if (_pendingPageRequests.TryRemove(page, out var oldTcs))
                    oldTcs.TrySetCanceled();

                _pendingPageRequests[page] = tcs;

                try
                {
                    Logger.WriteToLog($"RequestPageAsync|page {page} attempt {attempt}", LogMode.Verbose);
                    await SendAsync(CMD_READ_PAGE(page), $"CMD_READ_PAGE({page})");

                    var completed = await Task.WhenAny(tcs.Task, Task.Delay(RequestTimeoutMs));

                    if (completed == tcs.Task && !tcs.Task.IsCanceled)
                    {
                        var result = await tcs.Task;
                        Logger.WriteToLog($"RequestPageAsync|page {page} received", LogMode.Verbose);
                        await Task.Delay(ResponseCooldownMs);
                        return result;
                    }

                    Logger.WriteToLog($"RequestPageAsync|page {page} timeout attempt {attempt}", LogMode.Verbose);
                    tcs.TrySetCanceled();
                    await Task.Delay(ResponseCooldownMs);
                }
                finally
                {
                    _pendingPageRequests.TryRemove(
                        new KeyValuePair<int, TaskCompletionSource<AirspotDataPage>>(page, tcs));
                }
            }

            return null;
        }
        finally
        {
            _requestLock.Release();
        }
    }

    // ================= HISTORY COLLECTION =================

    private async Task<ushort[]?> CollectHistoryAsync(ushort minutes)
    {
        if (!IsGattValid())
            return BuildResult(minutes);

        _historyReadInProgress = true;
        try
        {
            // ── First call: get current page + backfill historical pages ──
            if (_startPage == null)
            {
                Logger.WriteToLog("CollectHistoryAsync|first call – requesting current page (once only)", LogMode.Verbose);

                ushort? current = await RequestCurrentPageAsync();
                if (current == null)
                {
                    Logger.WriteToLog("CollectHistoryAsync|failed to get current page", LogMode.Verbose);
                    return null;
                }

                _startPage = current.Value;
                _currentTrackedPage = current.Value;
                Logger.WriteToLog($"CollectHistoryAsync|start page = {_startPage}", LogMode.Verbose);

                // Backfill: calculate how many pages back we need to cover
                // the requested minutes. Each page holds ~16 entries at 1
                // min/entry = ~16 minutes. We add 1 extra page as buffer to
                // make sure we have enough even if the current page is partly
                // filled.
                int pagesNeeded = (int)Math.Ceiling((double)minutes / MinutesPerPage) + 1;
                int oldestPage = Math.Max(1, _startPage.Value - pagesNeeded);

                Logger.WriteToLog($"CollectHistoryAsync|backfill: minutes={minutes}, pagesNeeded={pagesNeeded}, reading pages {oldestPage}..{_startPage.Value}", LogMode.Verbose);

                for (int p = oldestPage; p <= _startPage.Value; p++)
                {
                    if (!IsGattValid())
                        break;

                    var backfillPage = await RequestPageAsync((ushort)p);
                    if (backfillPage != null)
                        MergePageEntries(backfillPage);
                    else
                        Logger.WriteToLog($"CollectHistoryAsync|backfill: could not read page {p}, skipping", LogMode.Verbose);
                }

                _backfillDone = true;
                Logger.WriteToLog($"CollectHistoryAsync|backfill complete, {_allEntries.Count} entries loaded", LogMode.Verbose);

                return BuildResult(minutes);
            }

            // ── Subsequent calls: just poll current tracked page ──────────
            var page = await RequestPageAsync(_currentTrackedPage);
            if (page == null)
            {
                Logger.WriteToLog($"CollectHistoryAsync|failed to read page {_currentTrackedPage}, returning cached", LogMode.Verbose);
                return BuildResult(minutes);
            }

            MergePageEntries(page);

            // Page full → advance to next page
            if (page.FinishedPage)
            {
                ushort nextPage = (ushort)(_currentTrackedPage + 1);
                Logger.WriteToLog($"CollectHistoryAsync|page {_currentTrackedPage} full, reading next page {nextPage}", LogMode.Verbose);

                var nextPageData = await RequestPageAsync(nextPage);
                if (nextPageData != null)
                {
                    MergePageEntries(nextPageData);
                    _currentTrackedPage = nextPage;
                    Logger.WriteToLog($"CollectHistoryAsync|now tracking page {_currentTrackedPage}", LogMode.Verbose);
                }
                else
                {
                    Logger.WriteToLog($"CollectHistoryAsync|could not read page {nextPage}, staying on {_currentTrackedPage}", LogMode.Verbose);
                }
            }

            return BuildResult(minutes);
        }
        finally
        {
            _historyReadInProgress = false;
        }
    }

    private void MergePageEntries(AirspotDataPage page)
    {
        lock (_entriesLock)
        {
            int added = 0;
            int skipped = 0;

            for (int i = 0; i < page.Timestamps.Count && i < page.CO2Values.Count; i++)
            {
                uint ts = (uint)page.Timestamps[i];
                int co2 = page.CO2Values[i];

                if (co2 == CO2_SENTINEL || co2 <= 200 || ts == 0xFFFFFFFF)
                    continue;

                if (_allEntries.ContainsKey(ts))
                    continue;

                // Filter button-press entries: gap to previous entry < 60s
                if (_allEntries.Count > 0)
                {
                    uint lastTs = _allEntries.Keys.Last();
                    int gap = (int)(ts - lastTs);

                    if (gap < MinEntryGapSeconds)
                    {
                        Logger.WriteToLog($"MergePageEntries|skipping button-press entry ts={ts} co2={co2} (gap={gap}s)", LogMode.Verbose);
                        skipped++;
                        continue;
                    }
                }

                _allEntries[ts] = co2;
                added++;
            }

            Logger.WriteToLog($"MergePageEntries|page {page.PageID}: +{added} added, {skipped} skipped, total={_allEntries.Count}", LogMode.Verbose);
        }
    }

    private ushort[] BuildResult(ushort minutes)
    {
        lock (_entriesLock)
        {
            if (_allEntries.Count == 0)
                return Array.Empty<ushort>();

            var values = _allEntries.Values
                .Select(v => (ushort)Math.Clamp(v, 0, ushort.MaxValue))
                .ToArray();

            if (minutes > 0 && values.Length > minutes)
                values = values.TakeLast(minutes).ToArray();

            Logger.WriteToLog($"BuildResult|{values.Length} values (requested={minutes}, stored={_allEntries.Count})", LogMode.Verbose);
            return values;
        }
    }

    // ================= BASE OVERRIDES =================

    protected override Task<int> DoReadCurrentCO2Async()
    {
        Logger.WriteToLog($"DoReadCurrentCO2Async|{CurrentCO2Value} ppm (unsolicited push)", LogMode.Verbose);
        return Task.FromResult(CurrentCO2Value);
    }

    protected override Task<int> DoReadUpdateIntervalAsync()
        => Task.FromResult(-99);

    protected override async Task<ushort[]?> DoReadHistoryAsync(ushort minutes, int interval)
        => await CollectHistoryAsync(minutes);

    // ================= DISPOSE =================

    public override async ValueTask DisposeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            await DisposeInternalAsync();
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task DisposeInternalAsync()
    {
        if (!_isActive && _notify == null && _service == null && _writer == null)
            return;

        await TearDownBleAsync(clearHistory: true);
        Logger.WriteToLog("AirspotProvider disposed", LogMode.Verbose);
    }

    // clearHistory=false → BLE torn down, history survives (reconnect)
    // clearHistory=true  → full reset (new device or dispose)
    private async Task TearDownBleAsync(bool clearHistory)
    {
        _isActive = false;
        _historyReadInProgress = false;

        _packetChannel?.Writer.TryComplete();

        foreach (var kvp in _pendingPageRequests)
            kvp.Value.TrySetCanceled();
        _pendingPageRequests.Clear();

        _currentPageTcs?.TrySetCanceled();
        _currentPageTcs = null;

        if (_notify != null)
        {
            _notify.ValueUpdated -= OnNotify;
            //try { await _notify.StopUpdatesAsync(); } catch { }
        }

        _packetChannel = null;
        _packetRouterTask = null;
        _service = null;
        _writer = null;
        _notify = null;

        if (clearHistory)
        {
            _startPage = null;
            _currentTrackedPage = 0;
            _backfillDone = false;
            ActiveDevice = null;
            _activeDeviceId = null;

            lock (_entriesLock)
                _allEntries.Clear();

            Logger.WriteToLog("TearDownBleAsync|BLE + history cleared", LogMode.Verbose);
        }
        else
        {
            Logger.WriteToLog($"TearDownBleAsync|BLE cleared, history preserved ({_allEntries.Count} entries, page {_currentTrackedPage})", LogMode.Verbose);
        }
    }
}