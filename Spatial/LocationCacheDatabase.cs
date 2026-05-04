using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.ExtensionMethods;
using SQLite;

namespace IndoorCO2MapAppV2.Spatial
{
    public class LocationCacheDatabase
    {
        private readonly SQLiteAsyncConnection db;

        public LocationCacheDatabase(string path)
        {
            db = new SQLiteAsyncConnection(path);

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await db.CreateTableAsync<CachedLocation>().ConfigureAwait(false);
        }

        public Task InsertOrReplaceAsync(LocationData loc)
        {
            var cached = new CachedLocation
            {
                Key = $"{loc.Type}_{loc.ID}",
                Type = loc.Type,
                ID = loc.ID,
                Name = loc.Name,
                Latitude = loc.Latitude,
                Longitude = loc.Longitude
            };

            return db.InsertOrReplaceAsync(cached);
        }

        public async Task<HashSet<LocationData>> GetAllAsync(double userLat, double userLon)
        {
            var list = await db.Table<CachedLocation>().ToListAsync().ConfigureAwait(false);

            return list
                .Select(c => c.ToLocationData(userLat, userLon))
                .ToHashSet();
        }

        public async Task TrimAsync(int maxRows)
        {
            int count = await db.Table<CachedLocation>().CountAsync().ConfigureAwait(false);
            if (count <= maxRows) return;
            int toDelete = count - maxRows;
            await db.ExecuteAsync(
                $"DELETE FROM CachedLocation WHERE rowid IN (SELECT rowid FROM CachedLocation ORDER BY rowid ASC LIMIT {toDelete})")
                .ConfigureAwait(false);
        }

        public Task ClearAsync()
            => db.DeleteAllAsync<CachedLocation>();
    }
}
