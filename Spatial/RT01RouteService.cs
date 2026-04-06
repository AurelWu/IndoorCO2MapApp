using IndoorCO2MapAppV2.DebugTools;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Text.Json;

namespace IndoorCO2MapAppV2.Spatial
{
    public class RT01RouteService
    {
        public static RT01RouteService Instance { get; } = new();

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
        private const string RT01_URL = "https://indoorco2map-planet.s3.eu-central-1.amazonaws.com/tiles/transit.routes";
        private const int ENTRY = 20;

        private static readonly string IndexCachePath =
            Path.Combine(FileSystem.CacheDirectory, "rt01_index.bin");
        private static readonly string ETagCachePath =
            Path.Combine(FileSystem.CacheDirectory, "rt01_index.etag");

        private byte[]? _indexCache;
        private uint _dataOffset;
        private readonly Dictionary<long, RouteGeometry> _routeCache = new();
        private readonly SemaphoreSlim _lock = new(1, 1);

        private RT01RouteService() { }

        public async Task<RouteGeometry?> FetchRouteGeometryAsync(long routeId, CancellationToken ct = default)
        {
            lock (_routeCache)
            {
                if (_routeCache.TryGetValue(routeId, out var cached))
                    return cached;
            }

            await _lock.WaitAsync(ct);
            try
            {
                // Re-check after acquiring lock
                lock (_routeCache)
                {
                    if (_routeCache.TryGetValue(routeId, out var cached))
                        return cached;
                }

                // Load index if needed
                if (_indexCache == null)
                {
                    // Always fetch the 16-byte header — tiny and gives us compIdxLen + _dataOffset
                    Logger.WriteToLog("RT01RouteService: fetching header...");
                    var header = await FetchRange(RT01_URL, 0, 15, ct);
                    string magic = System.Text.Encoding.ASCII.GetString(header, 0, 4);
                    if (magic != "RT01")
                    {
                        Logger.WriteToLog($"RT01RouteService: unexpected magic '{magic}'");
                        return null;
                    }
                    uint compIdxLen = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8));
                    _dataOffset = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12));

                    // Load stored ETag (null if not cached yet or file missing)
                    string? storedETag = null;
                    if (File.Exists(ETagCachePath))
                        storedETag = File.ReadAllText(ETagCachePath).Trim();

                    // Conditional GET for index range
                    long idxFrom = 16, idxTo = 16 + compIdxLen - 1;
                    using var resp = await FetchRangeRaw(RT01_URL, idxFrom, idxTo, storedETag, ct);

                    if (resp.StatusCode == HttpStatusCode.NotModified && File.Exists(IndexCachePath))
                    {
                        _indexCache = File.ReadAllBytes(IndexCachePath);
                        Logger.WriteToLog($"RT01RouteService: index loaded from disk cache ({_indexCache.Length / 1024.0:F1} KB decompressed)");
                    }
                    else if (resp.IsSuccessStatusCode)
                    {
                        var compIdx = await resp.Content.ReadAsByteArrayAsync(ct);
                        _indexCache = DecompressGzip(compIdx);
                        Logger.WriteToLog($"RT01RouteService: index downloaded ({compIdx.Length / 1024.0:F1} KB compressed → {_indexCache.Length / 1024.0:F1} KB decompressed)");

                        File.WriteAllBytes(IndexCachePath, _indexCache);

                        var newETag = resp.Headers.ETag?.Tag;
                        if (!string.IsNullOrEmpty(newETag))
                            File.WriteAllText(ETagCachePath, newETag);
                    }
                    else
                    {
                        Logger.WriteToLog($"RT01RouteService: unexpected HTTP {resp.StatusCode} fetching index");
                        return null;
                    }
                }

                // Binary search for routeId
                int lo = 0, hi = _indexCache.Length / ENTRY - 1;
                long foundOffset = -1;
                uint foundLength = 0;
                while (lo <= hi)
                {
                    int mid = (lo + hi) / 2;
                    long midId = BinaryPrimitives.ReadInt64LittleEndian(_indexCache.AsSpan(mid * ENTRY));
                    if (midId == routeId)
                    {
                        foundOffset = BinaryPrimitives.ReadUInt32LittleEndian(_indexCache.AsSpan(mid * ENTRY + 8));
                        foundLength = BinaryPrimitives.ReadUInt32LittleEndian(_indexCache.AsSpan(mid * ENTRY + 12));
                        break;
                    }
                    else if (midId < routeId) lo = mid + 1;
                    else hi = mid - 1;
                }

                if (foundOffset < 0)
                {
                    Logger.WriteToLog($"RT01RouteService: route {routeId} not found in index");
                    return null;
                }

                // Fetch and decompress blob
                long blobStart = _dataOffset + foundOffset;
                var blobComp = await FetchRange(RT01_URL, blobStart, blobStart + foundLength - 1, ct);
                var blobJson = DecompressGzip(blobComp);

                // Parse JSON
                using var doc = JsonDocument.Parse(blobJson);
                var root = doc.RootElement;

                string? color = null;
                if (root.TryGetProperty("color", out var colorEl))
                    color = colorEl.GetString();

                var points = new List<(double Lon, double Lat)>();
                if (root.TryGetProperty("geometry", out var geomEl))
                {
                    foreach (var coord in geomEl.EnumerateArray())
                    {
                        if (coord.GetArrayLength() >= 2)
                            points.Add((coord[0].GetDouble(), coord[1].GetDouble()));
                    }
                }

                Logger.WriteToLog($"RT01RouteService: route {routeId} → {points.Count} points, color={color}");

                var result = new RouteGeometry(points, color);
                lock (_routeCache)
                    _routeCache[routeId] = result;

                return result;
            }
            catch (OperationCanceledException) { return null; }
            catch (Exception ex)
            {
                Logger.WriteToLog($"RT01RouteService: error fetching route {routeId}: {ex.Message}");
                return null;
            }
            finally
            {
                _lock.Release();
            }
        }

        private static async Task<HttpResponseMessage> FetchRangeRaw(
            string url, long from, long to, string? ifNoneMatch, CancellationToken ct)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(from, to);
            if (!string.IsNullOrEmpty(ifNoneMatch))
                req.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
            return await _http.SendAsync(req, ct);
        }

        private static async Task<byte[]> FetchRange(string url, long from, long to, CancellationToken ct)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(from, to);
            var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }

        private static byte[] DecompressGzip(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var out2 = new MemoryStream();
            gz.CopyTo(out2);
            return out2.ToArray();
        }
    }
}
