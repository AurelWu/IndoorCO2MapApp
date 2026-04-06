using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using IndoorCO2MapAppV2.DebugTools;
using ZstdSharp;
using IndoorCO2MapAppV2.Utility;

namespace IndoorCO2MapAppV2.Spatial
{
    /// <summary>
    /// Fetches nearby transit stations and routes from transit.pmtiles.
    /// Returns deduplicated stations (station preferred over stop when same name)
    /// and routes filterable by OSM route type.
    /// </summary>
    internal sealed class PMTilesTransitService
    {
        private static readonly Lazy<PMTilesTransitService> _instance = new(() => new PMTilesTransitService());
        public static PMTilesTransitService Instance => _instance.Value;

        private const string TileUrl = "https://indoorco2map-planet.s3.eu-central-1.amazonaws.com/tiles/transit.pmtiles";
        private const int Zoom = 14;

        private static readonly HttpClient _http = new();

        private PmHeader? _header;
        private List<DirEntry>? _rootDir;
        private DateTime _cacheTime = DateTime.MinValue;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        private PMTilesTransitService() { }

        // ── Data types ────────────────────────────────────────────────────────

        private readonly record struct PmHeader(
            ulong RootDirOffset, ulong RootDirLen,
            ulong LeafDirOffset, ulong LeafDirLen,
            ulong DataOffset,
            byte InternalComp, byte TileComp);

        private readonly record struct DirEntry(ulong TileId, ulong Offset, uint Length, uint RunLength);

        // Candidate for station disambiguation
        private sealed class StationCandidate
        {
            public long Id;
            public string Name = "";
            public double Lat, Lon;
            public int Priority; // 0 = station (best), 1 = stop/other
        }

        // ── Public API ────────────────────────────────────────────────────────

        public async Task<(List<LocationData> stations, List<TransitLineData> routes)> SearchAsync(
            double userLat, double userLon, int rangeMeters,
            CancellationToken ct = default)
        {
            var (header, rootDir) = await EnsureHeaderAsync(ct);
            var tiles = GetTilesForRadius(userLat, userLon, rangeMeters);
            Logger.WriteToLog($"PMTilesTransit: querying {tiles.Count} tile(s) range={rangeMeters}m");

            // Collect raw data per layer
            var stationCandidates = new Dictionary<string, StationCandidate>(); // key = name (lower)
            var routeMap = new Dictionary<long, TransitLineData>(); // key = @id
            var seenRouteIds = new HashSet<long>();

            foreach (var (tx, ty) in tiles)
            {
                ct.ThrowIfCancellationRequested();
                ulong tileId = TileXYZToId(Zoom, tx, ty);
                var entry = await FindTileAsync(rootDir, tileId, header, ct);
                if (entry == null) continue;

                long start = (long)(header.DataOffset + entry.Value.Offset);
                long end = start + (long)entry.Value.Length - 1;
                var raw = await FetchRangeAsync(start, end, ct);
                var tileData = Decompress(raw, header.TileComp);

                DecodeTile(tileData, userLat, userLon, tx, ty, rangeMeters,
                    stationCandidates, routeMap, seenRouteIds);
            }

            // Finalize stations: keep best candidate per name
            var stations = new List<LocationData>();
            foreach (var kv in stationCandidates)
            {
                var c = kv.Value;
                stations.Add(new LocationData("node", c.Id, c.Name, c.Lat, c.Lon, userLat, userLon));
            }
            stations.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            var routes = routeMap.Values.ToList();

            Logger.WriteToLog($"PMTilesTransit: {stations.Count} stations, {routes.Count} routes");
            return (stations, routes);
        }

        // ── Header + root dir caching ─────────────────────────────────────────

        private async Task<(PmHeader header, List<DirEntry> rootDir)> EnsureHeaderAsync(CancellationToken ct)
        {
            if (_header.HasValue && _rootDir != null && (DateTime.UtcNow - _cacheTime).TotalHours < 24)
                return (_header.Value, _rootDir);

            await _initLock.WaitAsync(ct);
            try
            {
                if (_header.HasValue && _rootDir != null && (DateTime.UtcNow - _cacheTime).TotalHours < 24)
                    return (_header.Value, _rootDir);

                var hBytes = await FetchRangeAsync(0, 126, ct);
                var h = new PmHeader(
                    RootDirOffset: BinaryPrimitives.ReadUInt64LittleEndian(hBytes.AsSpan(8)),
                    RootDirLen:    BinaryPrimitives.ReadUInt64LittleEndian(hBytes.AsSpan(16)),
                    LeafDirOffset: BinaryPrimitives.ReadUInt64LittleEndian(hBytes.AsSpan(40)),
                    LeafDirLen:    BinaryPrimitives.ReadUInt64LittleEndian(hBytes.AsSpan(48)),
                    DataOffset:    BinaryPrimitives.ReadUInt64LittleEndian(hBytes.AsSpan(56)),
                    InternalComp:  hBytes[97],
                    TileComp:      hBytes[98]);

                var rootRaw = await FetchRangeAsync((long)h.RootDirOffset, (long)(h.RootDirOffset + h.RootDirLen - 1), ct);
                var rootDir = ParseDirectory(Decompress(rootRaw, h.InternalComp));

                _header = h;
                _rootDir = rootDir;
                _cacheTime = DateTime.UtcNow;
                Logger.WriteToLog($"PMTilesTransit: header loaded, {rootDir.Count} root entries");
                return (h, rootDir);
            }
            finally
            {
                _initLock.Release();
            }
        }

        // ── Tile bounding box ─────────────────────────────────────────────────

        private static List<(long x, long y)> GetTilesForRadius(double lat, double lon, int rangeMeters)
        {
            double latDelta = rangeMeters / 111320.0;
            double lonDelta = rangeMeters / (111320.0 * Math.Cos(lat * Math.PI / 180.0));

            var (x0, y0) = LatLonToTile(lat - latDelta, lon - lonDelta, Zoom); // SW (high y)
            var (x1, y1) = LatLonToTile(lat + latDelta, lon + lonDelta, Zoom); // NE (low y)

            var tiles = new List<(long, long)>();
            for (long tx = x0; tx <= x1; tx++)
                for (long ty = y1; ty <= y0; ty++)
                    tiles.Add((tx, ty));
            return tiles;
        }

        // ── MVT decode (two-layer: stations + routes) ─────────────────────────

        private static void DecodeTile(
            byte[] data, double userLat, double userLon,
            long tx, long ty, int rangeMeters,
            Dictionary<string, StationCandidate> stationCandidates,
            Dictionary<long, TransitLineData> routeMap,
            HashSet<long> seenRouteIds)
        {
            int pos = 0;
            while (pos < data.Length)
            {
                var (f, w) = ReadTag(data, ref pos);
                if (f == 3 && w == 2)
                {
                    int len = (int)ReadVarint(data.AsSpan(), ref pos);
                    DecodeLayerMessage(data.AsSpan(pos, len).ToArray(),
                        userLat, userLon, tx, ty, rangeMeters,
                        stationCandidates, routeMap, seenRouteIds);
                    pos += len;
                }
                else SkipField(data, ref pos, w);
            }
        }

        private static void DecodeLayerMessage(
            byte[] data, double userLat, double userLon,
            long tx, long ty, int rangeMeters,
            Dictionary<string, StationCandidate> stationCandidates,
            Dictionary<long, TransitLineData> routeMap,
            HashSet<long> seenRouteIds)
        {
            int pos = 0;
            uint extent = 4096;
            string layerName = "";
            var keys = new List<string>();
            var values = new List<string>();
            var rawFeatures = new List<byte[]>();

            while (pos < data.Length)
            {
                int pb = pos;
                var (f, w) = ReadTag(data, ref pos);
                if (f == 0 || pos == pb) { pos = pb + 1; continue; }

                if      (f == 1 && w == 2) layerName = ReadString(data, ref pos);
                else if (f == 5 && w == 0) extent = (uint)ReadVarint(data.AsSpan(), ref pos);
                else if (f == 3 && w == 2) keys.Add(ReadString(data, ref pos));
                else if (f == 4 && w == 2)
                {
                    int vl = (int)ReadVarint(data.AsSpan(), ref pos);
                    if (pos + vl > data.Length) break;
                    values.Add(DecodeValue(data.AsSpan(pos, vl).ToArray()));
                    pos += vl;
                }
                else if (f == 2 && w == 2)
                {
                    int fl = (int)ReadVarint(data.AsSpan(), ref pos);
                    if (pos + fl > data.Length) break;
                    rawFeatures.Add(data.AsSpan(pos, fl).ToArray());
                    pos += fl;
                }
                else SkipField(data, ref pos, w);
            }

            bool isStations = layerName == "stations";
            bool isRoutes = layerName == "routes";
            if (!isStations && !isRoutes) return;

            foreach (var fd in rawFeatures)
            {
                var (px, py, props) = DecodeFeature(fd, keys, values);
                if (props.Count == 0) continue;

                double flon = (tx + (double)px / extent) / Math.Pow(2, Zoom) * 360.0 - 180.0;
                double merc = Math.PI - 2.0 * Math.PI * (ty + (double)py / extent) / Math.Pow(2, Zoom);
                double flat = 180.0 / Math.PI * Math.Atan(Math.Sinh(merc));

                if (!props.TryGetValue("@id", out var idStr) || !long.TryParse(idStr, out long osmId))
                    continue;

                if (isStations)
                {
                    if (!props.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
                        continue;

                    // Distance filter
                    double dist = Haversine.GetDistanceInMeters(userLat, userLon, flat, flon);
                    if (dist > rangeMeters) continue;

                    // Station vs stop priority: 0 = station (best), 1 = everything else
                    int priority = 1;
                    if (props.TryGetValue("public_transport", out var pt) && pt == "station")
                        priority = 0;

                    string nameKey = name.ToLowerInvariant();
                    if (stationCandidates.TryGetValue(nameKey, out var existing))
                    {
                        // Replace if better priority, or same priority with lower ID
                        if (priority < existing.Priority ||
                            (priority == existing.Priority && osmId < existing.Id))
                        {
                            existing.Id = osmId;
                            existing.Lat = flat;
                            existing.Lon = flon;
                            existing.Priority = priority;
                        }
                    }
                    else
                    {
                        stationCandidates[nameKey] = new StationCandidate
                        {
                            Id = osmId, Name = name, Lat = flat, Lon = flon, Priority = priority
                        };
                    }
                }
                else // routes
                {
                    if (seenRouteIds.Contains(osmId)) continue;
                    seenRouteIds.Add(osmId);

                    if (!props.TryGetValue("name", out var routeName) || string.IsNullOrWhiteSpace(routeName))
                        continue;

                    string vehicleType = props.TryGetValue("route", out var rt) ? rt : "";

                    routeMap[osmId] = new TransitLineData(vehicleType, "relation", osmId, routeName, flat, flon);
                }
            }
        }

        // ── Directory parsing ─────────────────────────────────────────────────

        private static List<DirEntry> ParseDirectory(byte[] data)
        {
            var span = data.AsSpan();
            int pos = 0;
            int n = (int)ReadVarint(span, ref pos);
            var tileIds = new ulong[n];
            var runLens = new uint[n];
            var lengths = new uint[n];
            var offsets = new ulong[n];

            ulong lastId = 0;
            for (int i = 0; i < n; i++) { lastId += ReadVarint(span, ref pos); tileIds[i] = lastId; }
            for (int i = 0; i < n; i++) runLens[i] = (uint)ReadVarint(span, ref pos);
            for (int i = 0; i < n; i++) lengths[i] = (uint)ReadVarint(span, ref pos);
            for (int i = 0; i < n; i++)
            {
                ulong raw = ReadVarint(span, ref pos);
                offsets[i] = (raw == 0 && i > 0) ? offsets[i - 1] + lengths[i - 1]
                                                  : (raw == 0 ? 0UL : raw - 1);
            }

            var entries = new List<DirEntry>(n);
            for (int i = 0; i < n; i++)
                entries.Add(new DirEntry(tileIds[i], offsets[i], lengths[i], runLens[i]));
            return entries;
        }

        private async Task<DirEntry?> FindTileAsync(List<DirEntry> entries, ulong tileId, PmHeader h, CancellationToken ct)
        {
            int lo = 0, hi = entries.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                long cmp = (long)tileId - (long)entries[mid].TileId;
                if      (cmp > 0) lo = mid + 1;
                else if (cmp < 0) hi = mid - 1;
                else
                {
                    var e = entries[mid];
                    if (e.RunLength == 0) return await FollowLeafAsync(e, tileId, h, ct);
                    return e;
                }
            }
            if (hi >= 0)
            {
                var e = entries[hi];
                if (e.RunLength == 0) return await FollowLeafAsync(e, tileId, h, ct);
                if (tileId - e.TileId < e.RunLength) return e;
            }
            return null;
        }

        private async Task<DirEntry?> FollowLeafAsync(DirEntry e, ulong tileId, PmHeader h, CancellationToken ct)
        {
            if (e.Length == 0) return null;
            long from = (long)(h.LeafDirOffset + e.Offset);
            long to   = from + (long)e.Length - 1;
            var raw = await FetchRangeAsync(from, to, ct);
            var leafEntries = ParseDirectory(Decompress(raw, h.InternalComp));
            return await FindTileAsync(leafEntries, tileId, h, ct);
        }

        // ── Feature decode ────────────────────────────────────────────────────

        private static (long x, long y, Dictionary<string, string> props) DecodeFeature(
            byte[] data, List<string> keys, List<string> values)
        {
            int pos = 0;
            long curX = 0, curY = 0;
            bool gotPt = false;
            var props = new Dictionary<string, string>();
            var tagInts = new List<int>();

            while (pos < data.Length)
            {
                int pb = pos;
                var (f, w) = ReadTag(data, ref pos);
                if (f == 0 || pos == pb) { pos = pb + 1; continue; }

                if (f == 2 && w == 2)
                {
                    int len = (int)ReadVarint(data.AsSpan(), ref pos);
                    int end = pos + len;
                    if (end > data.Length) break;
                    while (pos < end) tagInts.Add((int)ReadVarint(data.AsSpan(), ref pos));
                }
                else if (f == 4 && w == 2)
                {
                    int len = (int)ReadVarint(data.AsSpan(), ref pos);
                    int end = pos + len;
                    if (end > data.Length) break;
                    if (!gotPt && pos < end)
                    {
                        uint cmd = (uint)ReadVarint(data.AsSpan(), ref pos);
                        if ((cmd & 0x7) == 1 && (cmd >> 3) > 0)
                        {
                            curX += DecodeZigZag((uint)ReadVarint(data.AsSpan(), ref pos));
                            curY += DecodeZigZag((uint)ReadVarint(data.AsSpan(), ref pos));
                            gotPt = true;
                        }
                    }
                    pos = end;
                }
                else SkipField(data, ref pos, w);
            }

            for (int i = 0; i + 1 < tagInts.Count; i += 2)
            {
                int ki = tagInts[i], vi = tagInts[i + 1];
                if (ki < keys.Count && vi < values.Count)
                    props[keys[ki]] = values[vi];
            }

            return (curX, curY, props);
        }

        private static string DecodeValue(byte[] data)
        {
            int pos = 0;
            while (pos < data.Length)
            {
                var (f, w) = ReadTag(data, ref pos);
                if (f == 1 && w == 2) return ReadString(data, ref pos);
                if (f == 2 && w == 5) { float v = BitConverter.ToSingle(data, pos); pos += 4; return v.ToString(System.Globalization.CultureInfo.InvariantCulture); }
                if (f == 3 && w == 1) { double v = BitConverter.ToDouble(data, pos); pos += 8; return v.ToString(System.Globalization.CultureInfo.InvariantCulture); }
                if (f == 4 && w == 0) return ((long)ReadVarint(data.AsSpan(), ref pos)).ToString();
                if (f == 5 && w == 0) return ReadVarint(data.AsSpan(), ref pos).ToString();
                if (f == 6 && w == 0) { ulong v = ReadVarint(data.AsSpan(), ref pos); return ((long)(v >> 1) ^ -(long)(v & 1)).ToString(); }
                if (f == 7 && w == 0) return ReadVarint(data.AsSpan(), ref pos) != 0 ? "true" : "false";
                SkipField(data, ref pos, w);
            }
            return "";
        }

        // ── Tile math ─────────────────────────────────────────────────────────

        private static (long x, long y) LatLonToTile(double lat, double lon, int z)
        {
            double latRad = lat * Math.PI / 180.0;
            long n = 1L << z;
            long x = (long)((lon + 180.0) / 360.0 * n);
            long y = (long)((1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
            return (Math.Max(0, Math.Min(n - 1, x)), Math.Max(0, Math.Min(n - 1, y)));
        }

        private static ulong TileXYZToId(int z, long x, long y)
        {
            ulong acc = 0;
            for (int i = 0; i < z; i++) acc += (1UL << i) * (1UL << i);
            return acc + HilbertXYToIndex(1UL << z, (ulong)x, (ulong)y);
        }

        private static ulong HilbertXYToIndex(ulong n, ulong x, ulong y)
        {
            ulong d = 0;
            for (ulong s = n >> 1; s > 0; s >>= 1)
            {
                ulong rx = (x & s) > 0 ? 1UL : 0UL;
                ulong ry = (y & s) > 0 ? 1UL : 0UL;
                d += s * s * ((3UL * rx) ^ ry);
                if (ry == 0)
                {
                    if (rx == 1) { x = s - 1 - x; y = s - 1 - y; }
                    (x, y) = (y, x);
                }
            }
            return d;
        }

        // ── HTTP range fetch ──────────────────────────────────────────────────

        private static async Task<byte[]> FetchRangeAsync(long from, long to, CancellationToken ct)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, TileUrl);
            req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(from, to);
            var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }

        // ── Compression ───────────────────────────────────────────────────────

        private static byte[] Decompress(byte[] data, byte comp)
        {
            if (data.Length >= 4 && data[0] == 0x28 && data[1] == 0xB5 && data[2] == 0x2F && data[3] == 0xFD)
                return DecompressZstd(data);
            if (data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B)
                return DecompressGzip(data);
            return comp switch { 2 => DecompressGzip(data), 4 => DecompressZstd(data), _ => data };
        }

        private static byte[] DecompressGzip(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var out2 = new MemoryStream();
            gz.CopyTo(out2);
            return out2.ToArray();
        }

        private static byte[] DecompressZstd(byte[] data)
        {
            using var d = new Decompressor();
            ulong? sz = Decompressor.GetDecompressedSize(data);
            ulong alloc = sz.HasValue ? Math.Min(sz.Value, 100_000_000UL) : (ulong)data.Length * 4;
            var result = new byte[alloc];
            int written = d.Unwrap(data, result);
            return result[..written];
        }

        // ── Protobuf helpers ──────────────────────────────────────────────────

        private static (int fieldNum, int wireType) ReadTag(byte[] data, ref int pos)
        {
            ulong tag = ReadVarint(data.AsSpan(), ref pos);
            return ((int)(tag >> 3), (int)(tag & 0x7));
        }

        private static ulong ReadVarint(Span<byte> data, ref int pos)
        {
            ulong result = 0; int shift = 0;
            while (pos < data.Length)
            {
                byte b = data[pos++];
                result |= (ulong)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
            return result;
        }

        private static string ReadString(byte[] data, ref int pos)
        {
            int len = (int)ReadVarint(data.AsSpan(), ref pos);
            if (pos + len > data.Length) return "";
            var s = Encoding.UTF8.GetString(data, pos, len);
            pos += len;
            return s;
        }

        private static void SkipField(byte[] data, ref int pos, int wireType)
        {
            switch (wireType)
            {
                case 0: ReadVarint(data.AsSpan(), ref pos); break;
                case 1: pos = Math.Min(pos + 8, data.Length); break;
                case 2: int skip = (int)ReadVarint(data.AsSpan(), ref pos); pos = Math.Min(pos + skip, data.Length); break;
                case 5: pos = Math.Min(pos + 4, data.Length); break;
                default: pos++; break;
            }
        }

        private static long DecodeZigZag(uint n) => (long)(n >> 1) ^ -(long)(n & 1);
    }
}
