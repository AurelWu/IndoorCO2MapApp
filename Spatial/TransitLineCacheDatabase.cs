using IndoorCO2MapAppV2.Utility;
using SQLite;

namespace IndoorCO2MapAppV2.Spatial
{
    public class TransitLineCacheDatabase
    {
        private readonly SQLiteAsyncConnection _db;

        public TransitLineCacheDatabase(string path)
        {
            _db = new SQLiteAsyncConnection(path);
            _ = _db.CreateTableAsync<CachedTransitLine>();
        }

        public Task InsertOrReplaceAsync(TransitLineData line)
            => _db.InsertOrReplaceAsync(new CachedTransitLine
            {
                Key = $"{line.NWRType}_{line.ID}",
                VehicleType = line.VehicleType,
                ID = line.ID,
                NWRType = line.NWRType,
                Name = line.Name,
                Latitude = line.Latitude,
                Longitude = line.Longitude,
            });

        public async Task<List<TransitLineData>> GetAllAsync(double userLat, double userLon, double rangeMeters)
        {
            var list = await _db.Table<CachedTransitLine>().ToListAsync().ConfigureAwait(false);
            return list
                .Where(c => Haversine.GetDistanceInMeters(userLat, userLon, c.Latitude, c.Longitude) <= rangeMeters)
                .Select(c => new TransitLineData(c.VehicleType, c.NWRType, c.ID, c.Name, c.Latitude, c.Longitude))
                .ToList();
        }

        public Task ClearAsync() => _db.DeleteAllAsync<CachedTransitLine>();
    }
}
