#if !WINDOWS
using BruTile.Cache;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;

namespace IndoorCO2MapAppV2.Spatial
{
    /// <summary>
    /// The one OSM tile layer every map in the app uses. Centralised because the
    /// tile usage policy applies per request: an identifying User-Agent, the
    /// canonical hostname (no a/b/c subdomain rotation) and an on-disk cache so the
    /// same tiles aren't refetched on every app start.
    ///
    /// Mapsui's own OSM source already supplies the correct URL and the attribution
    /// metadata, so only the User-Agent and the cache need filling in. Note Mapsui 5
    /// does not render attribution anywhere — each map has to show it itself, see
    /// <see cref="AppIdentity.OsmAttribution"/>.
    /// </summary>
    public static class OsmTileSource
    {
        private const long MaxCacheBytes = 50L * 1024 * 1024;

        // Well above the 7 days the policy asks for when cache headers aren't read.
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromDays(14);

        private static readonly object CacheLock = new();
        private static bool _cacheReady;

        public static TileLayer Create()
        {
            EnsureCache();
            return OpenStreetMap.CreateTileLayer(AppIdentity.UserAgent);
        }

        /// <summary>
        /// Installs the shared on-disk cache once per process. Must run before the
        /// first <see cref="OpenStreetMap.CreateTileLayer"/> call, since the tile
        /// source captures <c>DefaultCache</c> at construction.
        /// </summary>
        private static void EnsureCache()
        {
            lock (CacheLock)
            {
                if (_cacheReady) return;
                _cacheReady = true;  // set first: a broken cache must not retry on every map

                try
                {
                    var cacheDir = Path.Combine(FileSystem.CacheDirectory, "osmtiles");
                    Directory.CreateDirectory(cacheDir);
                    TrimTileCache(cacheDir, MaxCacheBytes);
                    OpenStreetMap.DefaultCache = new FileCache(cacheDir, "png", CacheExpiry);
                }
                catch
                {
                    // Without a persistent cache the maps still work, just less politely.
                }
            }
        }

        private static void TrimTileCache(string cacheDir, long maxBytes)
        {
            try
            {
                var files = new DirectoryInfo(cacheDir)
                    .GetFiles("*.png", SearchOption.AllDirectories)
                    .OrderBy(f => f.LastWriteTimeUtc)
                    .ToList();
                long total = files.Sum(f => f.Length);
                foreach (var file in files)
                {
                    if (total <= maxBytes) break;
                    total -= file.Length;
                    file.Delete();
                }
            }
            catch { }
        }
    }
}
#endif
