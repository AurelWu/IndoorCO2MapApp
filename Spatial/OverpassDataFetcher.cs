using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.Spatial
{
    public sealed class OverpassDataFetcher
    {
        // --- Singleton ---
        private static readonly Lazy<OverpassDataFetcher> _instance =
            new(() => new OverpassDataFetcher());

        public static OverpassDataFetcher Instance => _instance.Value;

        // --- UI-friendly state ---
        public OverpassFetchState State { get; } = new();

        // --- Networking ---
        private readonly HttpClient _httpClient;
        private Task<string?>? _inFlightFetch;
        private readonly SemaphoreSlim _lock = new(1, 1);

        // Overpass endpoints
        private readonly string _primaryEndpoint = "https://overpass-api.de/api/interpreter";
        private readonly string _secondaryEndpoint = "https://overpass.private.coffee/api/interpreter";

        private bool _useSecondary = false;

        // Cancellation
        private CancellationTokenSource? _cts;

        private OverpassDataFetcher(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "IndoorCO2DataRecorder/1.0 (https://indoorco2Map.com; contact: aurelwuensch@proton.me)"
            );
        }

        /// <summary>
        /// Public fetch entry. Ensures only one concurrent fetch even if UI calls multiple times.
        /// </summary>
        public async Task<string?> FetchOverpassDataAsync(string query, CancellationToken externalToken = default)
        {
            // If already running a fetch, reuse it
            if (_inFlightFetch != null)
                return await _inFlightFetch;

            await _lock.WaitAsync(externalToken);
            try
            {
                if (_inFlightFetch != null)
                    return await _inFlightFetch;

                _inFlightFetch = FetchInternalAsync(query, externalToken);
                return await _inFlightFetch;
            }
            finally
            {
                _inFlightFetch = null;
                _lock.Release();
            }
        }


        private async Task<string?> FetchInternalAsync(string query, CancellationToken externalToken)
        {
            // Reset state
            State.IsFetching = true;
            State.LastFailed = false;
            State.UsingAlternative = false;
            State.LastError = null;

            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

            try
            {
                // ---------------------------
                // Attempt #1 (primary or secondary)
                // ---------------------------
                try
                {
                    var json = await TryFetchAsync(query, _useSecondary ? _secondaryEndpoint : _primaryEndpoint);
                    return json;
                }
                catch (Exception ex1)
                {
                    State.LastError = ex1.Message;

                    // Switch to alternative for next try
                    _useSecondary = !_useSecondary;
                    State.UsingAlternative = true;
                }

                // ---------------------------
                // Attempt #2 (fallback)
                // ---------------------------
                try
                {
                    var json = await TryFetchAsync(query, _useSecondary ? _secondaryEndpoint : _primaryEndpoint);
                    return json;
                }
                catch (Exception ex2)
                {
                    State.LastError = ex2.Message;
                    State.LastFailed = true;
                    return null;
                }
            }
            finally
            {
                State.IsFetching = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task<string> TryFetchAsync(string query, string endpoint)
        {
            var content = new StringContent(
                "data=" + Uri.EscapeDataString(query),
                System.Text.Encoding.UTF8,
                "application/x-www-form-urlencoded"
            );

            var response = await _httpClient.PostAsync(endpoint, content, _cts!.Token);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync(_cts.Token);
        }

        public void CancelActiveFetch()
        {
            _cts?.Cancel();
        }
    }
}
